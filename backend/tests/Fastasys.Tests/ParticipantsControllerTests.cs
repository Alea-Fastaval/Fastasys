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

    private static SearchParticipantsDto Search(params ParticipantSearchStepDto[] steps) => new(steps, []);

    [Fact]
    public async Task Search_WithoutSearchesListsEveryoneWithTheChosenColumns()
    {
        using var db = CreateDbContext();
        await AddAsync(db, 2, p => { p.FirstName = "Bo"; p.LastName = "B"; p.IsNoShow = true; p.ActivityLanguages = [ParticipantLanguage.English]; });
        await AddAsync(db, 1, p => { p.FirstName = "Anna"; p.LastName = "A"; p.BirthDate = new DateOnly(2001, 2, 3); });

        var result = Ok(await CreateController(db).Search(new SearchParticipantsDto(null, ["isNoShow", "country", "birthDate", "activityLanguages"])));

        Assert.Null(result.DirectId);
        Assert.Equal([1, 2], result.Rows.Select(r => r.Id));
        Assert.Equal("Anna A", result.Rows[0].Name);
        Assert.Equal(false, result.Rows[0].Values["isNoShow"]);
        Assert.Equal("Danmark", result.Rows[0].Values["country"]);
        Assert.Equal(new DateOnly(2001, 2, 3), result.Rows[0].Values["birthDate"]);
        Assert.Equal(new List<ParticipantLanguage> { ParticipantLanguage.English }, result.Rows[1].Values["activityLanguages"]);
    }

    [Fact]
    public async Task Search_ALoneQuickSearchGoesToTheIdOrTheOnlyParticipantFound()
    {
        using var db = CreateDbContext();
        await AddAsync(db, 1, p => { p.FirstName = "Anna"; p.LastName = "Hansen"; });
        await AddAsync(db, 2, p => { p.FirstName = "Anna"; p.LastName = "Jensen"; });
        var controller = CreateController(db);

        Assert.Equal(2, Ok(await controller.Search(Search(new ParticipantSearchStepDto("2", null)))).DirectId);
        Assert.Equal(1, Ok(await controller.Search(Search(new ParticipantSearchStepDto("hansen", null)))).DirectId);
        var two = Ok(await controller.Search(Search(new ParticipantSearchStepDto("anna", null))));
        Assert.Null(two.DirectId);
        Assert.Equal(2, two.Rows.Count);
        // Only a lone quick search goes straight to a participant
        var combined = Ok(await controller.Search(Search(
            new ParticipantSearchStepDto("anna", null),
            new ParticipantSearchStepDto(null, new ParticipantCriteria { LastName = "Hansen" }, SearchLogic.And, SearchCombination.Intersection))));
        Assert.Null(combined.DirectId);
        Assert.Equal([1], combined.Rows.Select(r => r.Id));
    }

    [Fact]
    public async Task Search_CombinesWithThePreviousResultAlsoWithOr()
    {
        // Infosys gave the union for "Begræns" with "Or" (fixed on 2026-11-11)
        using var db = CreateDbContext();
        await AddAsync(db, 1, p => { p.FirstName = "Anna"; p.City = "Aarhus"; });
        await AddAsync(db, 2, p => { p.FirstName = "Bo"; p.City = "Odense"; });
        await AddAsync(db, 3, p => { p.FirstName = "Carl"; p.City = "Vejle"; });

        var result = Ok(await CreateController(db).Search(Search(
            new ParticipantSearchStepDto(null, new ParticipantCriteria { City = "Aarhus" }, SearchLogic.And),
            new ParticipantSearchStepDto(null, new ParticipantCriteria { FirstName = "Anna", City = "Odense" }, SearchLogic.Or, SearchCombination.Intersection))));

        Assert.Equal([1], result.Rows.Select(r => r.Id));
    }

    [Fact]
    public async Task Search_RefusesUnknownColumnsAndMixedSteps()
    {
        using var db = CreateDbContext();
        var controller = CreateController(db);

        Assert.IsType<BadRequestObjectResult>((await controller.Search(new SearchParticipantsDto(null, ["password"]))).Result);
        Assert.IsType<BadRequestObjectResult>((await controller.Search(Search(new ParticipantSearchStepDto("x", new ParticipantCriteria())))).Result);
        Assert.IsType<BadRequestObjectResult>((await controller.Search(Search(new ParticipantSearchStepDto(null, null)))).Result);
    }

    [Fact]
    public async Task Search_NoteColumnsAreOnlyForEditors()
    {
        // Infosys let its Read-only role add the notes to the list (fixed with the participant page)
        using var db = CreateDbContext();
        await AddAsync(db, 1, p => p.AdminNote = "Intern");

        var viewer = await CreateController(db, canEdit: false).Search(new SearchParticipantsDto(null, ["adminNote"]));
        var editor = Ok(await CreateController(db).Search(new SearchParticipantsDto(null, ["adminNote"])));

        Assert.IsType<ForbidResult>(viewer.Result);
        Assert.Equal("Intern", editor.Rows[0].Values["adminNote"]);
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
    public async Task Create_SavesLandArrangoeromraadeScenarieAndLanguages()
    {
        // Infosys's create form ignores these four fields (fixed on 2026-11-11)
        using var db = CreateDbContext();
        var game = new Activity { Title = "Et brætspil", Type = ActivityType.BoardGame };
        db.Activities.Add(game);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var created = Ok(await CreateController(db).Create(ValidCreate() with
        {
            CountryCode = "SE",
            OrganizerAreaId = OrganizerArea.BoardGameDesignerId,
            GameActivityId = game.Id,
            ActivityLanguages = [ParticipantLanguage.English, ParticipantLanguage.Scandinavian],
        }));

        Assert.Equal("Sverige", created.CountryName);
        Assert.Equal("Brætspilsdesigner", created.Details!.OrganizerAreaName);
        Assert.Equal("Et brætspil", created.Details.GameActivityTitle);
        Assert.Equal([ParticipantLanguage.English, ParticipantLanguage.Scandinavian], created.Details.ActivityLanguages);
    }

    [Fact]
    public async Task Create_WithoutLanguagesOrCountry_GetsDanskAndDanmark()
    {
        using var db = CreateDbContext();

        var created = Ok(await CreateController(db).Create(ValidCreate() with { CountryCode = null, ActivityLanguages = [] }));

        Assert.Equal("DK", created.CountryCode);
        Assert.Equal([ParticipantLanguage.Danish], created.Details!.ActivityLanguages);
    }

    [Fact]
    public async Task Create_RefusesUnknownCountryAreaGameOrLanguage()
    {
        using var db = CreateDbContext();
        var quest = new Activity { Title = "En quest", Type = ActivityType.Quest };
        db.Activities.Add(quest);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var controller = CreateController(db);

        Assert.IsType<BadRequestObjectResult>((await controller.Create(ValidCreate() with { CountryCode = "XX" })).Result);
        Assert.IsType<BadRequestObjectResult>((await controller.Create(ValidCreate() with { OrganizerAreaId = 999 })).Result);
        // The forms only offer scenarios and board games
        Assert.IsType<BadRequestObjectResult>((await controller.Create(ValidCreate() with { GameActivityId = quest.Id })).Result);
        Assert.IsType<BadRequestObjectResult>((await controller.Create(ValidCreate() with { ActivityLanguages = [(ParticipantLanguage)9] })).Result);
        Assert.Empty(db.Participants);
    }

    [Fact]
    public async Task Update_SavesLandArrangoeromraadeAndScenarie()
    {
        // Infosys's edit form ignores these three fields (fixed on 2026-11-11)
        using var db = CreateDbContext();
        var scenario = new Activity { Title = "Et scenarie", Type = ActivityType.Scenario };
        db.Activities.Add(scenario);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        await AddAsync(db, 1, p => p.OrganizerAreaId = OrganizerArea.ScenarioAuthorId);
        var controller = CreateController(db);

        var updated = Ok(await controller.Update(1, ValidUpdate() with { CountryCode = "NO", OrganizerAreaId = null, GameActivityId = scenario.Id }));
        db.ChangeTracker.Clear();
        var keptCountry = Ok(await controller.Update(1, ValidUpdate() with { CountryCode = null, GameActivityId = scenario.Id }));

        Assert.Equal("Norge", updated.CountryName);
        Assert.Null(updated.Details!.OrganizerAreaId);
        Assert.Equal("Et scenarie", updated.Details.GameActivityTitle);
        Assert.Equal("NO", keptCountry.CountryCode);
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
        // Wear, entrance, food, sleeping places and prices are not ported yet; the participant page shows that while
        // these are null
        Assert.Null(participant.Details.WearOrders);
        Assert.Null(participant.Details.Tickets);
        Assert.Null(participant.Details.Meals);
        Assert.Null(participant.Details.SleepingPlaces);
        Assert.Null(participant.Price);
    }

    // Wednesday 24 March 2027 at the given UTC hour (Copenhagen is UTC+1 that week until Sunday)
    private static DateTime Wednesday(int hour) => new(2027, 3, 24, hour, 0, 0, DateTimeKind.Utc);

    private static Activity Game(int id, string title, AutoSignupCategory autoSignup = AutoSignupCategory.None) =>
        new() { Id = id, Title = title, AutoSignupCategory = autoSignup };

    private static Schedule ScheduleOf(int id, Activity activity, DateTime start, Room? room = null) =>
        new() { Id = id, ActivityId = activity.Id, StartTime = start, EndTime = start.AddHours(4), RoomId = room?.Id };

    [Fact]
    public async Task Get_ListsTheParticipantsSeatsByStartTime()
    {
        // Infosys Deltagere::getPladser: the participant's seats, ordered by the schedule's start
        using var db = CreateDbContext();
        await AddAsync(db, 1);
        await AddAsync(db, 2);
        var roomA = new Room { Id = 1, Name = "Lokale A1", Location = "Skolen" };
        var roomB = new Room { Id = 2, Name = "Lokale B2", Location = "" };
        var late = Game(1, "Sent spil");
        var early = Game(2, "Tidligt spil");
        db.AddRange(roomA, roomB, late, early);
        db.AddRange(ScheduleOf(10, late, Wednesday(18)), ScheduleOf(20, early, Wednesday(8)));
        db.Runs.AddRange(
            new Run { Id = 100, ScheduleId = 10, Number = 3, RoomId = 2 },
            new Run { Id = 200, ScheduleId = 20, Number = 1, RoomId = 1 });
        db.RunSeats.AddRange(
            new RunSeat { RunId = 100, SeatNumber = 1, ParticipantId = 1, Role = RunSeatRole.GameMaster },
            new RunSeat { RunId = 200, SeatNumber = 1, ParticipantId = 1, Role = RunSeatRole.Player },
            new RunSeat { RunId = 200, SeatNumber = 2, ParticipantId = 2, Role = RunSeatRole.Player });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var activities = Ok(await CreateController(db).Get(1)).Details!.Activities;

        Assert.Equal(
            new ParticipantActivityDto[]
            {
                new ParticipantActivityDto(2, "Tidligt spil", Wednesday(8), Wednesday(12), ParticipantActivityRole.Player, 200, 1, "Lokale A1", "Skolen"),
                new ParticipantActivityDto(1, "Sent spil", Wednesday(18), Wednesday(22), ParticipantActivityRole.GameMaster, 100, 3, "Lokale B2", ""),
            },
            activities);
    }

    [Fact]
    public async Task Get_ListsNoActivitiesForAParticipantWithoutSeats()
    {
        using var db = CreateDbContext();
        await AddAsync(db, 1);

        Assert.Empty(Ok(await CreateController(db).Get(1)).Details!.Activities);
    }

    [Fact]
    public async Task Get_AddsTheOwnGameAndAutomaticSignupsForBoardGameDesigners()
    {
        // Infosys getPladser adds SpecialSpots for work_area 2: every schedule of the designer's own game as
        // "designer" (getDesignerDuties) and every schedule of the activities with automatic signup for designers as
        // "spiller" (getAutoSpots). They have no run, and the room is the schedule's meeting room.
        using var db = CreateDbContext();
        var room = new Room { Id = 1, Name = "Brætspilscafeen", Location = "Hallen" };
        var ownGame = Game(1, "Mit spil");
        var designerMeeting = Game(2, "Designermøde", AutoSignupCategory.Designer);
        var authorMeeting = Game(3, "Forfattermøde", AutoSignupCategory.Author);
        var seatGame = Game(4, "Andet spil");
        db.AddRange(room, ownGame, designerMeeting, authorMeeting, seatGame);
        db.AddRange(
            ScheduleOf(10, ownGame, Wednesday(14), room),
            ScheduleOf(11, ownGame, Wednesday(9)),
            ScheduleOf(20, designerMeeting, Wednesday(9), room),
            ScheduleOf(30, authorMeeting, Wednesday(9), room),
            ScheduleOf(40, seatGame, Wednesday(9), room));
        db.Runs.Add(new Run { Id = 400, ScheduleId = 40, Number = 2, RoomId = 1 });
        db.RunSeats.Add(new RunSeat { RunId = 400, SeatNumber = 1, ParticipantId = 1, Role = RunSeatRole.Player });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        await AddAsync(db, 1, p => { p.OrganizerAreaId = OrganizerArea.BoardGameDesignerId; p.GameActivityId = 1; });

        var activities = Ok(await CreateController(db).Get(1)).Details!.Activities;

        // At 9:00 the seat comes first, then the own game, then the automatic signup, as PHP's stable uasort keeps them.
        // The author meeting is not for designers.
        Assert.Equal(
            new ParticipantActivityDto[]
            {
                new ParticipantActivityDto(4, "Andet spil", Wednesday(9), Wednesday(13), ParticipantActivityRole.Player, 400, 2, "Brætspilscafeen", "Hallen"),
                new ParticipantActivityDto(1, "Mit spil", Wednesday(9), Wednesday(13), ParticipantActivityRole.Designer, null, null, null, null),
                new ParticipantActivityDto(2, "Designermøde", Wednesday(9), Wednesday(13), ParticipantActivityRole.Player, null, null, "Brætspilscafeen", "Hallen"),
                new ParticipantActivityDto(1, "Mit spil", Wednesday(14), Wednesday(18), ParticipantActivityRole.Designer, null, null, "Brætspilscafeen", "Hallen"),
            },
            activities);
    }

    [Fact]
    public async Task Get_AddsOnlyTheAutomaticSignupsForADesignerWithoutAGame()
    {
        using var db = CreateDbContext();
        var designerMeeting = Game(2, "Designermøde", AutoSignupCategory.Designer);
        db.Add(designerMeeting);
        db.Add(ScheduleOf(20, designerMeeting, Wednesday(9)));
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        await AddAsync(db, 1, p => p.OrganizerAreaId = OrganizerArea.BoardGameDesignerId);

        var activities = Ok(await CreateController(db).Get(1)).Details!.Activities;

        Assert.Equal(ParticipantActivityRole.Player, Assert.Single(activities).Role);
    }

    [Theory]
    [InlineData(OrganizerArea.ScenarioAuthorId)]
    [InlineData(17)]
    public async Task Get_AddsNoAutomaticSignupsForOtherOrganizerAreas(int organizerAreaId)
    {
        // Legacy: getAutoSpots knows authors (1, 54-56) and designers (2, 17-19), but getPladser only asks for
        // work_area 2, so authors and the other designer areas get no extra rows. Kept on purpose for now; the user
        // wants the other areas added later (see LoadActivitiesAsync).
        using var db = CreateDbContext();
        var ownGame = Game(1, "Mit scenarie");
        var authorMeeting = Game(2, "Forfattermøde", AutoSignupCategory.Author);
        var designerMeeting = Game(3, "Designermøde", AutoSignupCategory.Designer);
        db.AddRange(ownGame, authorMeeting, designerMeeting);
        db.AddRange(ScheduleOf(10, ownGame, Wednesday(9)), ScheduleOf(20, authorMeeting, Wednesday(9)), ScheduleOf(30, designerMeeting, Wednesday(9)));
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        await AddAsync(db, 1, p => { p.OrganizerAreaId = organizerAreaId; p.GameActivityId = 1; });

        Assert.Empty(Ok(await CreateController(db).Get(1)).Details!.Activities);
    }

    [Fact]
    public async Task Get_HidesEverythingBelowThePersonalInfoFromViewers()
    {
        // Infosys visdeltager hides the rest of the page, notes included, from the Read-only roles. Only the Pris
        // breakdown box (Price, null until prices are ported) is left for them.
        using var db = CreateDbContext();
        await AddAsync(db, 1, p => { p.AdminNote = "Intern note"; p.Email = "x@example.invalid"; });

        var participant = Ok(await CreateController(db, canEdit: false).Get(1));

        Assert.False(participant.CanEdit);
        Assert.Null(participant.Details);
        Assert.Null(participant.Price);
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
