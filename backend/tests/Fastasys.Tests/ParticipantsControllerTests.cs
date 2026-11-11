using System.Security.Claims;
using System.Text.Json;
using Fastasys.ApiService.Controllers;
using Fastasys.ApiService.Data;
using Fastasys.ApiService.Data.Entities;
using Fastasys.ApiService.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Fastasys.Tests;

// Pins the Infosys behaviour of deltager/visalle, visdeltager, opret, update, ajax/editable, updatenote and delete,
// with the bug fixes agreed on 2026-11-11.
public class ParticipantsControllerTests
{
    private static InfosysDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<InfosysDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        var context = new InfosysDbContext(options);
        // Seeds the participant categories, organizer areas and countries
        context.Database.EnsureCreated();
        return context;
    }

    // Answers "may edit participants" without running the real policies
    private sealed class FakeAuthorization(bool canEdit) : IAuthorizationService
    {
        public Task<AuthorizationResult> AuthorizeAsync(ClaimsPrincipal user, object? resource, IEnumerable<IAuthorizationRequirement> requirements) =>
            Task.FromResult(canEdit ? AuthorizationResult.Success() : AuthorizationResult.Failed());

        public Task<AuthorizationResult> AuthorizeAsync(ClaimsPrincipal user, object? resource, string policyName) =>
            Task.FromResult(canEdit ? AuthorizationResult.Success() : AuthorizationResult.Failed());
    }

    private static ParticipantsController CreateController(InfosysDbContext db, bool canEdit = true) =>
        new(db, new FakeAuthorization(canEdit), NullLogger<ParticipantsController>.Instance);

    private static CreateParticipantDto ValidCreate() => new()
    {
        BirthDate = new DateOnly(2001, 2, 3),
        CategoryId = ParticipantCategory.ParticipantId,
        AmountPaid = 450,
        FirstName = "Testa",
        LastName = "Testesen",
        Email = "testa@example.invalid",
        AddressLine1 = "Testvej 1",
        AddressLine2 = "2. th",
        ZipCode = "8000",
        City = "Testby",
        PhoneNumber = "11223344",
        PreferredLanguage = PreferredLanguage.English,
        DesiredActivities = 3,
        DesiredHeroTasks = 2,
        YouthClub = "Testklubben",
    };

    private static UpdateParticipantDto ValidUpdate() => new()
    {
        BirthDate = new DateOnly(2001, 2, 3),
        CategoryId = ParticipantCategory.ParticipantId,
        FirstName = "Testa",
        LastName = "Testesen",
        AddressLine1 = "Testvej 1",
        AddressLine2 = "2. th",
    };

    private static async Task<Participant> AddAsync(InfosysDbContext db, int id, Action<Participant>? setup = null)
    {
        var participant = new Participant { Id = id, FirstName = "Deltager", LastName = id.ToString() };
        setup?.Invoke(participant);
        db.Participants.Add(participant);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        db.ChangeTracker.Clear();
        return participant;
    }

    private static T Ok<T>(ActionResult<T> result) =>
        Assert.IsAssignableFrom<T>(Assert.IsAssignableFrom<ObjectResult>(result.Result).Value);

    private static JsonElement Json(object? value) => JsonSerializer.SerializeToElement(value);

    [Fact]
    public async Task GetAll_ListsEveryParticipantInIdOrderWithTheirLegalName()
    {
        using var db = CreateDbContext();
        await AddAsync(db, 3, p => { p.FirstName = "Carl"; p.LastName = "C"; p.Nickname = "Kalde"; });
        await AddAsync(db, 1, p => { p.FirstName = "Anna"; p.LastName = "A"; p.IsAnnulled = true; });

        var list = Ok(await CreateController(db).GetAll());

        Assert.Equal([new ParticipantSummaryDto(1, "Anna A"), new ParticipantSummaryDto(3, "Carl C")], list);
    }

    [Fact]
    public async Task Create_GivesTheLowestFreeId()
    {
        using var db = CreateDbContext();
        await AddAsync(db, 1);
        await AddAsync(db, 2);
        await AddAsync(db, 4);

        var created = Ok(await CreateController(db).Create(ValidCreate()));

        Assert.Equal(3, created.Id);
    }

    [Fact]
    public async Task Create_StoresTheFormFieldsWithInfosysDefaults()
    {
        using var db = CreateDbContext();
        var before = DateTime.UtcNow;

        var created = Ok(await CreateController(db).Create(ValidCreate() with { PhoneNumber = "+4511223344", Email = " testa@example.invalid " }));

        var stored = await db.Participants.AsNoTracking().SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal("Testa", stored.FirstName);
        Assert.Equal("Testvej 1", stored.AddressLine1);
        Assert.Equal("2. th", stored.AddressLine2);
        Assert.Equal(new DateOnly(2001, 2, 3), stored.BirthDate);
        Assert.Equal(450, stored.AmountPaid);
        Assert.Equal(PreferredLanguage.English, stored.PreferredLanguage);
        // Normalized like Deltagere::insert (with the +45 fix)
        Assert.Equal("11223344", stored.PhoneNumber);
        Assert.Equal("testa@example.invalid", stored.Email);
        // Defaults of the deltagere columns
        Assert.Equal("DK", stored.CountryCode);
        Assert.Equal([ParticipantLanguage.Danish], stored.ActivityLanguages);
        Assert.Equal("none", stored.Pronouns);
        Assert.False(stored.IsAnnulled);
        // created, updated and signed_up are all set to now
        Assert.True(stored.CreatedAt >= before);
        Assert.Equal(stored.CreatedAt, stored.UpdatedAt);
        Assert.Equal(stored.CreatedAt, stored.SignedUpAt);
        Assert.Equal("Danmark", created.CountryName);
        Assert.Equal("Deltager", created.CategoryName);
    }

    [Fact]
    public async Task Create_WithoutBirthDate_StoresItAsUnknown()
    {
        using var db = CreateDbContext();

        var created = Ok(await CreateController(db).Create(ValidCreate() with { BirthDate = null }));

        Assert.Null(created.BirthDate);
    }

    [Fact]
    public async Task Create_RefusesUnknownCategoryAndTooLongText()
    {
        using var db = CreateDbContext();
        var controller = CreateController(db);

        Assert.IsType<BadRequestObjectResult>((await controller.Create(ValidCreate() with { CategoryId = 999 })).Result);
        Assert.IsType<BadRequestObjectResult>((await controller.Create(ValidCreate() with { FirstName = new string('a', 129) })).Result);
        Assert.IsType<BadRequestObjectResult>((await controller.Create(ValidCreate() with { DesiredActivities = 256 })).Result);
        Assert.Empty(db.Participants);
    }

    [Fact]
    public async Task Get_ShowsEverythingToEditors_WithPreviousAndNext()
    {
        using var db = CreateDbContext();
        await AddAsync(db, 2);
        await AddAsync(db, 5, p => { p.AdminNote = "Intern note"; p.CommentNote = "Fra tilmeldingen"; p.OrganizerAreaId = OrganizerArea.BoardGameDesignerId; });
        await AddAsync(db, 9);

        var participant = Ok(await CreateController(db).Get(5));

        Assert.True(participant.CanEdit);
        Assert.Equal(2, participant.PreviousId);
        Assert.Equal(9, participant.NextId);
        Assert.NotNull(participant.Details);
        Assert.Equal("Intern note", participant.Details.AdminNote);
        Assert.Equal("Fra tilmeldingen", participant.Details.CommentNote);
        Assert.Null(participant.Details.SleepingNote);
        Assert.Equal("Brætspilsdesigner", participant.Details.OrganizerAreaName);
    }

    [Fact]
    public async Task Get_HidesEverythingBelowThePersonalInfoFromViewers()
    {
        // Infosys visdeltager hides the rest of the page, notes included, from the Read-only roles
        using var db = CreateDbContext();
        await AddAsync(db, 1, p => { p.AdminNote = "Intern note"; p.Email = "x@example.invalid"; });

        var participant = Ok(await CreateController(db, canEdit: false).Get(1));

        Assert.False(participant.CanEdit);
        Assert.Null(participant.Details);
        Assert.Equal("x@example.invalid", participant.Email);
    }

    [Fact]
    public async Task Get_FindsAParticipantFromTheirBadge()
    {
        using var db = CreateDbContext();
        await AddAsync(db, 42);
        var year = TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTime.UtcNow, "Europe/Copenhagen").Year;
        var badge = long.Parse(ParticipantRules.Ean8ForParticipant(42, year));

        var participant = Ok(await CreateController(db).Get(badge));

        Assert.Equal(42, participant.Id);
    }

    [Fact]
    public async Task Get_UnknownParticipant_IsNotFound()
    {
        using var db = CreateDbContext();

        Assert.IsType<NotFoundObjectResult>((await CreateController(db).Get(77)).Result);
    }

    [Fact]
    public async Task Update_KeepsBothAddressLines()
    {
        // Infosys joined the lines with a space in the form and split them on a comma, losing line 2 (fixed)
        using var db = CreateDbContext();
        await AddAsync(db, 1);

        await CreateController(db).Update(1, ValidUpdate() with { AddressLine1 = "Testvej 1", AddressLine2 = "2. th" });

        var stored = await db.Participants.AsNoTracking().SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal("Testvej 1", stored.AddressLine1);
        Assert.Equal("2. th", stored.AddressLine2);
    }

    [Fact]
    public async Task Update_ChangesTheLanguagesOnlyWhenOneIsTicked()
    {
        using var db = CreateDbContext();
        await AddAsync(db, 1, p => p.ActivityLanguages = [ParticipantLanguage.English]);
        var controller = CreateController(db);

        await controller.Update(1, ValidUpdate() with { ActivityLanguages = [] });
        var unchanged = await db.Participants.AsNoTracking().SingleAsync(TestContext.Current.CancellationToken);
        db.ChangeTracker.Clear();

        await controller.Update(1, ValidUpdate() with { ActivityLanguages = [ParticipantLanguage.Scandinavian, ParticipantLanguage.Danish] });
        var changed = await db.Participants.AsNoTracking().SingleAsync(TestContext.Current.CancellationToken);

        Assert.Equal([ParticipantLanguage.English], unchanged.ActivityLanguages);
        Assert.Equal([ParticipantLanguage.Danish, ParticipantLanguage.Scandinavian], changed.ActivityLanguages);
    }

    [Fact]
    public async Task Update_CanAnnulAndStampsUpdated()
    {
        using var db = CreateDbContext();
        await AddAsync(db, 1, p => p.UpdatedAt = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc));

        var updated = Ok(await CreateController(db).Update(1, ValidUpdate() with { IsAnnulled = true }));

        var stored = await db.Participants.AsNoTracking().SingleAsync(TestContext.Current.CancellationToken);
        Assert.True(updated.Details!.IsAnnulled);
        Assert.True(stored.UpdatedAt > new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public async Task UpdateField_ChangesFirstAndLastNameSeparately()
    {
        // Infosys edited "Navn" as one text and split it at the last space (fixed)
        using var db = CreateDbContext();
        await AddAsync(db, 1);
        var controller = CreateController(db);

        await controller.UpdateField(1, "firstName", new UpdateParticipantFieldDto(Json("Anna")));
        db.ChangeTracker.Clear();
        await controller.UpdateField(1, "lastName", new UpdateParticipantFieldDto(Json("van der Berg")));

        var stored = await db.Participants.AsNoTracking().SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal("Anna", stored.FirstName);
        Assert.Equal("van der Berg", stored.LastName);
    }

    [Fact]
    public async Task UpdateField_RefusesAnInvalidBirthDate_AndNullMeansUnknown()
    {
        // Infosys stored 0000-00-00 when it couldn't read the date (fixed)
        using var db = CreateDbContext();
        await AddAsync(db, 1, p => p.BirthDate = new DateOnly(1990, 1, 1));
        var controller = CreateController(db);

        var refused = await controller.UpdateField(1, "birthDate", new UpdateParticipantFieldDto(Json("31-02-1990")));
        db.ChangeTracker.Clear();
        var kept = await db.Participants.AsNoTracking().SingleAsync(TestContext.Current.CancellationToken);
        var changed = Ok(await controller.UpdateField(1, "birthDate", new UpdateParticipantFieldDto(Json("1991-12-24"))));
        db.ChangeTracker.Clear();
        var cleared = Ok(await controller.UpdateField(1, "birthDate", new UpdateParticipantFieldDto(Json(null))));

        Assert.IsType<BadRequestObjectResult>(refused.Result);
        Assert.Equal(new DateOnly(1990, 1, 1), kept.BirthDate);
        Assert.Equal(new DateOnly(1991, 12, 24), changed.BirthDate);
        Assert.Null(cleared.BirthDate);
    }

    [Theory]
    [InlineData("password")]
    [InlineData("id")]
    [InlineData("createdAt")]
    [InlineData("activityLanguages")]
    public async Task UpdateField_RefusesFieldsThePageDoesNotOffer(string field)
    {
        // Infosys set any column the browser named; only the participant page's fields can be changed now
        using var db = CreateDbContext();
        await AddAsync(db, 1);

        var result = await CreateController(db).UpdateField(1, field, new UpdateParticipantFieldDto(Json("x")));

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task UpdateField_NormalizesThePhoneNumber()
    {
        using var db = CreateDbContext();
        await AddAsync(db, 1);

        var updated = Ok(await CreateController(db).UpdateField(1, "phoneNumber", new UpdateParticipantFieldDto(Json(" 004587654321"))));

        Assert.Equal("87654321", updated.PhoneNumber);
    }

    [Fact]
    public async Task UpdateField_ChecksPronounsAndSelects()
    {
        using var db = CreateDbContext();
        await AddAsync(db, 1);
        var scenario = new Activity { Title = "Et scenarie", Type = ActivityType.Scenario };
        var quest = new Activity { Title = "En quest", Type = ActivityType.Quest };
        db.Activities.AddRange(scenario, quest);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var controller = CreateController(db);

        async Task<bool> Accepted(string field, object? value)
        {
            db.ChangeTracker.Clear();
            return (await controller.UpdateField(1, field, new UpdateParticipantFieldDto(Json(value)))).Result is OkObjectResult;
        }

        Assert.True(await Accepted("pronouns", "fsfo"));
        Assert.False(await Accepted("pronouns", "fsfs"));
        Assert.True(await Accepted("categoryId", ParticipantCategory.OrganizerId));
        Assert.False(await Accepted("categoryId", 99));
        Assert.True(await Accepted("countryCode", "SE"));
        Assert.False(await Accepted("countryCode", "XX"));
        Assert.True(await Accepted("organizerAreaId", OrganizerArea.ScenarioAuthorId));
        Assert.True(await Accepted("organizerAreaId", null));
        Assert.False(await Accepted("organizerAreaId", 9999));
        // The page only offers scenarios and board games
        Assert.True(await Accepted("gameActivityId", scenario.Id));
        Assert.False(await Accepted("gameActivityId", quest.Id));
        Assert.True(await Accepted("gameActivityId", null));
        Assert.True(await Accepted("preferredLanguage", "English"));
        Assert.False(await Accepted("preferredLanguage", "1"));
        Assert.False(await Accepted("isNoShow", "ja"));
        Assert.True(await Accepted("isNoShow", true));
        Assert.False(await Accepted("extraVouchers", 128));
        Assert.True(await Accepted("maxGameMasterRuns", null));
    }

    [Fact]
    public async Task UpdateField_SetsASignupNote()
    {
        using var db = CreateDbContext();
        await AddAsync(db, 1);

        var updated = Ok(await CreateController(db).UpdateField(1, "sleepingNote", new UpdateParticipantFieldDto(Json("Sover let"))));

        Assert.Equal("Sover let", updated.Details!.SleepingNote);
    }

    [Fact]
    public async Task Delete_RefusesAParticipantWhoIsNotAnnulled()
    {
        using var db = CreateDbContext();
        await AddAsync(db, 1);

        var result = await CreateController(db).Delete(1);

        Assert.IsType<ConflictObjectResult>(result);
        Assert.Single(db.Participants);
    }

    [Fact]
    public async Task Delete_RemovesAnAnnulledParticipantAndTheirSeats()
    {
        using var db = CreateDbContext();
        await AddAsync(db, 1, p => p.IsAnnulled = true);
        var activity = new Activity { Title = "Spil", MaxPlayersPerTeam = 4 };
        var room = new Room { Name = "Lokale" };
        db.AddRange(activity, room);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var schedule = new Schedule { ActivityId = activity.Id, StartTime = DateTime.UtcNow, EndTime = DateTime.UtcNow.AddHours(1) };
        db.Add(schedule);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var run = new Run { ScheduleId = schedule.Id, Number = 1, RoomId = room.Id };
        db.Add(run);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        db.Add(new RunSeat { RunId = run.Id, SeatNumber = 1, ParticipantId = 1, Role = RunSeatRole.Player });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        db.ChangeTracker.Clear();

        var result = await CreateController(db).Delete(1);

        Assert.IsType<OkObjectResult>(result);
        Assert.Empty(db.Participants);
        Assert.Empty(db.RunSeats);
    }
}
