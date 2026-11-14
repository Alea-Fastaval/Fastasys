using System.Security.Claims;
using Fastasys.ApiService.Controllers;
using Fastasys.ApiService.Data;
using Fastasys.ApiService.Data.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Fastasys.Tests;

// Pins the "Tilmeldinger" tables of the Infosys activity page (visaktivitet.phtml, ActivityModel::getDoubleBookings
// and getDesiredActivityStats, Afviklinger::getSignupByType, Deltagere::isBusyBetween)
public class ActivitySignupsControllerTests
{
    private static readonly CancellationToken Ct = TestContext.Current.CancellationToken;

    // Wednesday 24 March 2027 at the given UTC hour
    private static DateTime Wednesday(int hour) => new(2027, 3, 24, hour, 0, 0, DateTimeKind.Utc);

    private sealed class FakeAuthorization(bool mayEditParticipants) : IAuthorizationService
    {
        public Task<AuthorizationResult> AuthorizeAsync(ClaimsPrincipal user, object? resource, IEnumerable<IAuthorizationRequirement> requirements) =>
            Task.FromResult(mayEditParticipants ? AuthorizationResult.Success() : AuthorizationResult.Failed());

        public Task<AuthorizationResult> AuthorizeAsync(ClaimsPrincipal user, object? resource, string policyName) =>
            Task.FromResult(mayEditParticipants ? AuthorizationResult.Success() : AuthorizationResult.Failed());
    }

    private static ActivitySignupsController CreateController(InfosysDbContext db, bool mayEditParticipants = true) =>
        new(db, new FakeAuthorization(mayEditParticipants));

    private static InfosysDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<InfosysDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        var context = new InfosysDbContext(options);
        context.Database.EnsureCreated();
        return context;
    }

    private static T Ok<T>(ActionResult<T> result) =>
        Assert.IsAssignableFrom<T>(Assert.IsAssignableFrom<ObjectResult>(result.Result).Value);

    // Activity 1 ("Spillet", time-exclusive, can't be replayed) with schedules 10 (Wednesday 10-14) and 11 (18-22),
    // run 100 on schedule 10 and run 110 on schedule 11; activity 2 ("Andet") with schedule 20 at the given time
    private static async Task<InfosysDbContext> SeedAsync(Action<InfosysDbContext> setup, DateTime? otherStart = null, bool replayable = false, bool timeExclusive = true)
    {
        var db = CreateDbContext();
        db.Rooms.Add(new Room { Id = 1, Name = "Lokale" });
        db.Activities.AddRange(
            new Activity { Id = 1, Title = "Spillet", MaxPlayersPerTeam = 5, GameMastersPerTeam = 1, IsReplayable = replayable, IsTimeExclusive = timeExclusive },
            new Activity { Id = 2, Title = "Andet", MaxPlayersPerTeam = 5, GameMastersPerTeam = 1 });
        var other = otherStart ?? Wednesday(18);
        db.Schedules.AddRange(
            new Schedule { Id = 10, ActivityId = 1, StartTime = Wednesday(10), EndTime = Wednesday(14) },
            new Schedule { Id = 11, ActivityId = 1, StartTime = Wednesday(18), EndTime = Wednesday(22) },
            new Schedule { Id = 20, ActivityId = 2, StartTime = other, EndTime = other.AddHours(4) });
        db.Runs.AddRange(
            new Run { Id = 100, ScheduleId = 10, Number = 1, RoomId = 1 },
            new Run { Id = 110, ScheduleId = 11, Number = 1, RoomId = 1 },
            new Run { Id = 200, ScheduleId = 20, Number = 1, RoomId = 1 });
        for (var id = 1; id <= 4; id++)
        {
            db.Participants.Add(new Participant { Id = id, FirstName = $"Fornavn{id}", LastName = $"Efternavn{id}" });
        }

        await db.SaveChangesAsync(Ct);
        setup(db);
        await db.SaveChangesAsync(Ct);
        db.ChangeTracker.Clear();
        return db;
    }

    private static ScheduleSignup Wish(int participantId, int scheduleId, RunSeatRole role, int priority) =>
        new() { ParticipantId = participantId, ScheduleId = scheduleId, Role = role, Priority = priority };

    private static RunSeat Seat(int runId, int seatNumber, int participantId, RunSeatRole role = RunSeatRole.Player) =>
        new() { RunId = runId, SeatNumber = seatNumber, ParticipantId = participantId, Role = role };

    private static async Task<IReadOnlyList<SignupPoolRowDto>> RowsAsync(InfosysDbContext db, int scheduleId, bool mayEditParticipants = true) =>
        Ok(await CreateController(db, mayEditParticipants).Get(1)).Single(s => s.ScheduleId == scheduleId).Signups;

    [Fact]
    public async Task Get_IsNotFoundForAnUnknownActivity()
    {
        using var db = CreateDbContext();

        Assert.IsType<NotFoundObjectResult>((await CreateController(db).Get(99)).Result);
    }

    [Fact]
    public async Task Get_ListsEachScheduleWithGameMastersFirstAndThenByPriority()
    {
        using var db = await SeedAsync(d => d.ScheduleSignups.AddRange(
            Wish(1, 10, RunSeatRole.Player, 2),
            Wish(2, 10, RunSeatRole.Player, 1),
            Wish(3, 10, RunSeatRole.GameMaster, 0),
            Wish(4, 20, RunSeatRole.Player, 1)));

        var tables = Ok(await CreateController(db).Get(1));

        Assert.Equal([10, 11], tables.Select(t => t.ScheduleId));
        Assert.Equal([3, 2, 1], tables[0].Signups.Select(r => r.ParticipantId));
        Assert.Empty(tables[1].Signups);
        var row = tables[0].Signups[0];
        Assert.Equal("Fornavn3 Efternavn3", row.Name);
        Assert.Equal((RunSeatRole.GameMaster, 0), (row.Role, row.Priority));
        // Karma is not ported yet
        Assert.Null(row.Karma);
    }

    [Fact]
    public async Task Get_ShowsTheAgeTheLanguageFlagAndTheNote()
    {
        using var db = await SeedAsync(d =>
        {
            var p = d.Participants.Single(x => x.Id == 1);
            p.BirthDate = new DateOnly(2000, 1, 1);
            p.ActivityLanguages = [ParticipantLanguage.English];
            p.CommentNote = "  Glæder mig  ";
            var other = d.Participants.Single(x => x.Id == 2);
            other.ActivityLanguages = [ParticipantLanguage.Danish, ParticipantLanguage.English];
            d.ScheduleSignups.AddRange(Wish(1, 10, RunSeatRole.Player, 1), Wish(2, 10, RunSeatRole.Player, 2));
        });

        var rows = await RowsAsync(db, 10);
        var hidden = await RowsAsync(db, 10, mayEditParticipants: false);

        Assert.True(rows[0].Age >= 26);
        Assert.Equal("uk", rows[0].Language);
        Assert.Equal("Glæder mig", rows[0].Note);
        // Infosys shows a flag only for exactly Danish or exactly English
        Assert.Null(rows[1].Language);
        // An unknown birth date has no age (Infosys: the years since 1970)
        Assert.Null(rows[1].Age);
        // The notes are for users who may edit participants
        Assert.Null(hidden[0].Note);
    }

    [Fact]
    public async Task Get_HidesTheParticipantsWhoAreOnATeam()
    {
        // On the schedule's runs (any role): every row of theirs on the schedule. Playing on another run of the
        // activity: their player rows, as the activity can't be replayed.
        using var db = await SeedAsync(d =>
        {
            d.RunSeats.AddRange(Seat(100, 1, 1, RunSeatRole.GameMaster), Seat(110, 1, 2));
            d.ScheduleSignups.AddRange(
                Wish(1, 10, RunSeatRole.Player, 1),
                Wish(2, 10, RunSeatRole.Player, 1),
                Wish(2, 10, RunSeatRole.GameMaster, 0),
                Wish(3, 10, RunSeatRole.Player, 1));
        });

        var onTeam = (await RowsAsync(db, 10)).ToDictionary(r => (r.ParticipantId, r.Role), r => r.IsOnTeam);

        Assert.True(onTeam[(1, RunSeatRole.Player)]);
        Assert.True(onTeam[(2, RunSeatRole.Player)]);
        Assert.False(onTeam[(2, RunSeatRole.GameMaster)]);
        Assert.False(onTeam[(3, RunSeatRole.Player)]);
    }

    [Fact]
    public async Task Get_ShowsPlayersOfAReplayableActivityOnItsOtherSchedules()
    {
        using var db = await SeedAsync(
            d =>
            {
                d.RunSeats.Add(Seat(110, 1, 2));
                d.ScheduleSignups.Add(Wish(2, 10, RunSeatRole.Player, 1));
            },
            replayable: true);

        Assert.False((await RowsAsync(db, 10)).Single().IsOnTeam);
    }

    [Fact]
    public async Task Get_MarksParticipantsWhoAreBusyThen()
    {
        // Participant 1 plays the other activity at 12-16 (time-exclusive); participant 2 plays it at 14-18 (touching)
        using var db = await SeedAsync(
            d =>
            {
                d.RunSeats.Add(Seat(200, 1, 1));
                d.ScheduleSignups.AddRange(Wish(1, 10, RunSeatRole.Player, 1), Wish(2, 10, RunSeatRole.Player, 2));
            },
            otherStart: Wednesday(12));

        var rows = await RowsAsync(db, 10);

        Assert.True(rows.Single(r => r.ParticipantId == 1).IsBusy);
        Assert.False(rows.Single(r => r.ParticipantId == 2).IsBusy);
    }

    [Fact]
    public async Task Get_MarksNoOneBusyForAnActivityThatIsNotTimeExclusive()
    {
        using var db = await SeedAsync(
            d =>
            {
                d.RunSeats.Add(Seat(200, 1, 1));
                d.ScheduleSignups.Add(Wish(1, 10, RunSeatRole.Player, 1));
            },
            otherStart: Wednesday(12),
            timeExclusive: false);

        Assert.False((await RowsAsync(db, 10)).Single().IsBusy);
    }

    [Fact]
    public async Task Get_MarksActivityLockedParticipants()
    {
        using var db = await SeedAsync(d =>
        {
            d.Participants.Single(p => p.Id == 1).IsActivityLocked = true;
            d.ScheduleSignups.Add(Wish(1, 10, RunSeatRole.Player, 1));
        });

        Assert.True((await RowsAsync(db, 10)).Single().IsLocked);
    }

    [Fact]
    public async Task Get_MarksAGameMasterWithAnotherWishAtTheSameTimeAsDoubleBooked()
    {
        // Participant 1 wants to run schedule 10 (10-14) and play the other activity at 12-16: every row of theirs on
        // schedule 10 is marked. Participant 2 only wants to play both, participant 3 runs it and plays at 14-18.
        using var db = await SeedAsync(
            d => d.ScheduleSignups.AddRange(
                Wish(1, 10, RunSeatRole.GameMaster, 0),
                Wish(1, 10, RunSeatRole.Player, 1),
                Wish(1, 20, RunSeatRole.Player, 1),
                Wish(2, 10, RunSeatRole.Player, 1),
                Wish(2, 20, RunSeatRole.Player, 1)),
            otherStart: Wednesday(12));

        var rows = await RowsAsync(db, 10);

        Assert.All(rows.Where(r => r.ParticipantId == 1), r => Assert.True(r.IsDoubleBooked));
        Assert.False(rows.Single(r => r.ParticipantId == 2).IsDoubleBooked);
    }

    [Fact]
    public async Task Get_ShowsTheHeartFromTheWantedNumberOfActivities()
    {
        // Participant 1 wants 2 and plays 2: a filled heart. Participant 2 wants 3 and plays 1: an empty one.
        // Participant 3 wants none: no heart. Participant 4 wants 2 and plays 2, but has two wishes on the activity:
        // Infosys counts each seat once per wish, so 2 - 4 gives an empty heart (legacy, reported 2026-11-14).
        using var db = await SeedAsync(d =>
        {
            d.Participants.Single(p => p.Id == 1).DesiredActivities = 2;
            d.Participants.Single(p => p.Id == 2).DesiredActivities = 3;
            d.Participants.Single(p => p.Id == 4).DesiredActivities = 2;
            d.RunSeats.AddRange(Seat(200, 1, 1), Seat(110, 1, 1), Seat(200, 2, 2), Seat(200, 3, 4), Seat(110, 2, 4));
            d.ScheduleSignups.AddRange(
                Wish(1, 10, RunSeatRole.Player, 1),
                Wish(2, 10, RunSeatRole.Player, 1),
                Wish(3, 10, RunSeatRole.Player, 1),
                Wish(4, 10, RunSeatRole.Player, 1),
                Wish(4, 11, RunSeatRole.Player, 2));
        }, replayable: true);

        var hearts = (await RowsAsync(db, 10)).ToDictionary(r => r.ParticipantId, r => r.DesiredActivitiesReached);

        Assert.True(hearts[1]);
        Assert.False(hearts[2]);
        Assert.Null(hearts[3]);
        Assert.False(hearts[4]);
    }
}
