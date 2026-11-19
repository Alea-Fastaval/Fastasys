using Fastasys.ApiService.Controllers;
using Fastasys.ApiService.Data;
using Fastasys.ApiService.Data.Entities;
using Fastasys.ApiService.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Fastasys.Tests;

public class KarmaServiceTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private static InfosysDbContext CreateDb() => new(new DbContextOptionsBuilder<InfosysDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static Schedule NewSchedule(string title, KarmaType type = KarmaType.Big, bool exclusive = true) => new()
    {
        Activity = new Activity { Title = title, KarmaType = type, IsTimeExclusive = exclusive, MaxPlayersPerTeam = 5, GameMastersPerTeam = 1 },
        StartTime = new DateTime(2027, 3, 24, 10, 0, 0, DateTimeKind.Utc),
        EndTime = new DateTime(2027, 3, 24, 14, 0, 0, DateTimeKind.Utc)
    };

    private static Run AddSeat(InfosysDbContext db, Participant participant, Schedule schedule, int number, RunSeatRole role = RunSeatRole.Player)
    {
        var run = new Run { Schedule = schedule, Number = number, Room = new Room { Name = $"Room {number}" } };
        run.Seats.Add(new RunSeat { SeatNumber = 1, Participant = participant, Role = role });
        db.Runs.Add(run);
        return run;
    }

    [Fact]
    public async Task UnknownParticipantIsMissing_AndExistingParticipantWithoutInputsHasZero()
    {
        using var db = CreateDb();
        var participant = new Participant { FirstName = "Anna", LastName = "Spiller" };
        db.Add(participant);
        await db.SaveChangesAsync(Ct);
        var service = new KarmaService(db);

        Assert.Null(await service.GetParticipantAsync(999, Ct));
        var result = await service.GetParticipantAsync(participant.Id, Ct);
        Assert.NotNull(result);
        Assert.Equal("Anna Spiller", result.ParticipantName);
        Assert.Equal(0, result.Karma.Total);
        Assert.Empty(result.ExcludedInputs);
        Assert.Empty(await service.CalculateAsync([], Ct));
    }

    [Fact]
    public async Task BatchMatchesIndividualReads_AndExplainsEveryExcludedInput()
    {
        using var db = CreateDb();
        var p = new Participant { FirstName = "Player" };
        var other = new Participant { FirstName = "Other" };
        var empty = new Participant { FirstName = "Empty" };
        var exclusive = NewSchedule("Scenario");
        var dropIn = NewSchedule("Drop-in", KarmaType.Small, false);
        db.AddRange(p, other, empty);
        db.ParticipantSchedules.AddRange(
            new ParticipantSchedule { Participant = p, Schedule = exclusive, Role = "Player", Priority = 1 },
            new ParticipantSchedule { Participant = p, Schedule = exclusive, Role = "GameMaster", Priority = 0 },
            new ParticipantSchedule { Participant = p, Schedule = exclusive, Role = "Player", Priority = null },
            new ParticipantSchedule { Participant = p, Schedule = exclusive, Role = "1", Priority = 2 },
            new ParticipantSchedule { Participant = p, Schedule = dropIn, Role = "Player", Priority = 1 },
            new ParticipantSchedule { Participant = p, Schedule = dropIn, Role = "LegacyObserver", Priority = null });
        AddSeat(db, p, exclusive, 1);
        AddSeat(db, p, exclusive, 2, RunSeatRole.GameMaster);
        AddSeat(db, p, dropIn, 1);
        AddSeat(db, other, NewSchedule("Small game", KarmaType.Small), 1);
        await db.SaveChangesAsync(Ct);
        var service = new KarmaService(db);

        var batch = await service.CalculateAsync([p.Id, other.Id, p.Id, empty.Id], Ct);

        Assert.Equal(3, batch.Count);
        Assert.Equal(-9, batch[p.Id].Karma.Total); // -12 -7 +10
        Assert.Equal(7, batch[other.Id].Karma.Total);
        Assert.Equal(0, batch[empty.Id].Karma.Total);
        Assert.Equal(new KarmaExclusions(2, 1, 1, 1, 1), batch[p.Id].Karma.Exclusions);
        Assert.Equal(6, batch[p.Id].ExcludedInputs.Count);
        var unknown = Assert.Single(batch[p.Id].ExcludedInputs, e => e.Reason == KarmaExclusionReason.UnknownSignupRole);
        Assert.Equal("1", unknown.Role); // A numeric string must not parse as an enum value.
        Assert.Equal(exclusive.Id, unknown.ScheduleId);
        Assert.Equal("Scenario", unknown.ActivityTitle);
        Assert.Equal(KarmaInputSource.Signup, unknown.Source);
        foreach (var id in new[] { p.Id, other.Id, empty.Id })
        {
            var individual = await service.GetParticipantAsync(id, Ct);
            Assert.NotNull(individual);
            Assert.Equal(batch[id].Karma.Total, individual.Karma.Total);
            Assert.Equal(batch[id].Karma.Signups, individual.Karma.Signups);
            Assert.Equal(batch[id].Karma.Assignments, individual.Karma.Assignments);
            Assert.Equal(batch[id].ExcludedInputs, individual.ExcludedInputs);
        }
    }

    [Fact]
    public async Task ReadsUseCurrentPreferencesSeatsAndActivitySettings()
    {
        using var db = CreateDb();
        var p = new Participant();
        var schedule = NewSchedule("Scenario");
        var signup = new ParticipantSchedule { Participant = p, Schedule = schedule, Priority = 1 };
        db.Add(signup);
        var run = AddSeat(db, p, schedule, 1);
        await db.SaveChangesAsync(Ct);
        var service = new KarmaService(db);
        async Task<int> Total() => (await service.GetParticipantAsync(p.Id, Ct))!.Karma.Total;

        Assert.Equal(-2, await Total());
        signup.Priority = 2;
        await db.SaveChangesAsync(Ct);
        Assert.Equal(9, await Total());
        schedule.Activity.KarmaType = KarmaType.Small;
        await db.SaveChangesAsync(Ct);
        Assert.Equal(6, await Total());
        schedule.Activity.IsTimeExclusive = false;
        await db.SaveChangesAsync(Ct);
        Assert.Equal(0, await Total());
        schedule.Activity.IsTimeExclusive = true;
        db.RunSeats.Remove(Assert.Single(run.Seats));
        await db.SaveChangesAsync(Ct);
        Assert.Equal(-1, await Total());
        db.ParticipantSchedules.Remove(signup);
        await db.SaveChangesAsync(Ct);
        Assert.Equal(0, await Total());
    }

    [Fact]
    public async Task AllRunAndNestedScheduleResponsesUseTheSameCurrentParticipantTotal()
    {
        using var db = CreateDb();
        var p = new Participant { FirstName = "Anna" };
        var schedule = NewSchedule("Scenario");
        db.Add(new ParticipantSchedule { Participant = p, Schedule = schedule, Priority = 1 });
        var first = AddSeat(db, p, schedule, 1);
        var second = AddSeat(db, p, schedule, 2);
        var freeRoom = new Room { Name = "Free" };
        var otherRoom = new Room { Name = "Another free room" };
        db.AddRange(freeRoom, otherRoom);
        await db.SaveChangesAsync(Ct);
        var karma = new KarmaService(db);
        var runs = new RunsController(db, NullLogger<RunsController>.Instance, karma);
        var activities = new ActivitiesController(db, NullLogger<ActivitiesController>.Instance, karma);
        static RunDto RunValue(ActionResult<RunDto> value) => Assert.IsType<RunDto>(Assert.IsType<OkObjectResult>(value.Result).Value);
        static ScheduleDto ScheduleValue(ActionResult<ScheduleDto> value) => Assert.IsType<ScheduleDto>(Assert.IsType<OkObjectResult>(value.Result).Value);

        Assert.Equal(8, Assert.Single(RunValue(await runs.Get(first.Id)).Seats).Karma);
        var created = Assert.IsType<RunDto>(Assert.IsType<CreatedAtActionResult>((await runs.Create(new(schedule.Id))).Result).Value);
        Assert.Empty(created.Seats);
        Assert.Equal(18, Assert.Single(RunValue(await runs.CreateSeat(created.Id, new(p.Id, RunSeatRole.Player))).Seats).Karma);
        var nested = ScheduleValue(await activities.GetSchedule(schedule.ActivityId, schedule.Id));
        Assert.All(nested.Runs.SelectMany(r => r.Seats), seat => Assert.Equal(18, seat.Karma));
        var list = Assert.IsType<List<ScheduleDto>>(Assert.IsType<OkObjectResult>((await activities.GetSchedules(schedule.ActivityId)).Result).Value);
        Assert.All(list.SelectMany(s => s.Runs).SelectMany(r => r.Seats), seat => Assert.Equal(18, seat.Karma));

        Assert.Equal(18, Assert.Single(RunValue(await runs.Update(created.Id, new(otherRoom.Id))).Seats).Karma);
        var updatedSchedule = ScheduleValue(await activities.UpdateSchedule(schedule.ActivityId, schedule.Id,
            new(schedule.StartTime, schedule.EndTime, null)));
        Assert.All(updatedSchedule.Runs.SelectMany(r => r.Seats), seat => Assert.Equal(18, seat.Karma));
        Assert.Empty(RunValue(await runs.DeleteSeat(created.Id, p.Id)).Seats);
        Assert.Equal(8, Assert.Single(RunValue(await runs.Get(second.Id)).Seats).Karma);
        await runs.Delete(first.Id, removeParticipants: true);
        Assert.Equal(-2, Assert.Single(RunValue(await runs.Get(second.Id)).Seats).Karma);
    }
}
