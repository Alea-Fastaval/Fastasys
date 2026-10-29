using Fastasys.ApiService.Controllers;
using Fastasys.ApiService.Data;
using Fastasys.ApiService.Data.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Fastasys.Tests;

// Pins the Infosys behaviour of aktivitet/visalle, aktivitet/vis, aktivitet/opret, aktivitet/edit and aktivitet/slet.
public class ActivitiesControllerTests
{
    private static InfosysDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<InfosysDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        var context = new InfosysDbContext(options);
        context.Database.EnsureCreated();
        return context;
    }

    private static ActivitiesController CreateController(InfosysDbContext db) =>
        new(db, NullLogger<ActivitiesController>.Instance);

    private static SaveActivityDto ValidDto() => new(
        Title: "Testscenarie",
        TitleEnglish: "Test scenario",
        Author: "Test Forfatter",
        Type: ActivityType.Scenario,
        Description: "Dansk foromtale",
        DescriptionEnglish: "English blurb",
        Note: "Intern note",
        DurationHours: 4.5m,
        MinPlayersPerTeam: 3,
        MaxPlayersPerTeam: 5,
        GameMastersPerTeam: 1,
        Price: 30,
        MaxSignups: 40,
        MinAge: 13,
        MaxAge: 17,
        CanSignUp: true,
        IsHidden: false,
        IsRoomExclusive: true,
        IsTimeExclusive: false,
        IsReplayable: false,
        PlayerLanguage: ActivityLanguage.DanishAndEnglish,
        GameMasterLanguage: ActivityLanguage.English,
        KarmaType: KarmaType.Medium,
        AutoSignupCategory: AutoSignupCategory.Author,
        WordPressId: 1234
    );

    [Fact]
    public async Task GetAll_ReturnsHiddenActivitiesToo()
    {
        using var db = CreateDbContext();
        db.Activities.AddRange(
            new Activity { Title = "Synlig" },
            new Activity { Title = "Skjult", IsHidden = true, CanSignUp = false });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await CreateController(db).GetAll();

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var list = Assert.IsAssignableFrom<IEnumerable<ActivitySummaryDto>>(ok.Value);
        Assert.Equal(2, list.Count());
    }

    [Fact]
    public async Task GetAll_SortsTitlesLikePhpStrcmp()
    {
        // strcmp compares bytes: uppercase before lowercase, and Å/Æ/Ø after z in code point order (Å, Æ, Ø),
        // not in Danish alphabetical order (Æ, Ø, Å).
        using var db = CreateDbContext();
        foreach (var title in new[] { "Ørneredet", "beta", "Åen", "alpha", "Æblerov", "Beta" })
        {
            db.Activities.Add(new Activity { Title = title });
        }
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await CreateController(db).GetAll();

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var titles = Assert.IsAssignableFrom<IEnumerable<ActivitySummaryDto>>(ok.Value).Select(a => a.Title);
        Assert.Equal(new[] { "Beta", "alpha", "beta", "Åen", "Æblerov", "Ørneredet" }, titles);
    }

    [Fact]
    public async Task Get_UnknownId_ReturnsNotFound()
    {
        using var db = CreateDbContext();

        var result = await CreateController(db).Get(999);

        Assert.IsType<NotFoundObjectResult>(result.Result);
    }

    [Fact]
    public async Task Create_StoresEveryFormField()
    {
        using var db = CreateDbContext();
        var dto = ValidDto();

        var result = await CreateController(db).Create(dto);

        var created = Assert.IsType<CreatedAtActionResult>(result.Result);
        var returned = Assert.IsType<ActivityDto>(created.Value);
        var stored = await db.Activities.SingleAsync(TestContext.Current.CancellationToken);

        Assert.Equal(stored.Id, returned.Id);
        Assert.Equal("Testscenarie", stored.Title);
        Assert.Equal("Test scenario", stored.TitleEnglish);
        Assert.Equal("Test Forfatter", stored.Author);
        Assert.Equal(ActivityType.Scenario, stored.Type);
        Assert.Equal("Dansk foromtale", stored.Description);
        Assert.Equal("English blurb", stored.DescriptionEnglish);
        Assert.Equal("Intern note", stored.Note);
        Assert.Equal(4.5m, stored.DurationHours);
        Assert.Equal(3, stored.MinPlayersPerTeam);
        Assert.Equal(5, stored.MaxPlayersPerTeam);
        Assert.Equal(1, stored.GameMastersPerTeam);
        Assert.Equal(30, stored.Price);
        Assert.Equal(40, stored.MaxSignups);
        Assert.Equal(13, stored.MinAge);
        Assert.Equal(17, stored.MaxAge);
        Assert.True(stored.CanSignUp);
        Assert.False(stored.IsHidden);
        Assert.True(stored.IsRoomExclusive);
        Assert.False(stored.IsTimeExclusive);
        Assert.False(stored.IsReplayable);
        Assert.Equal(ActivityLanguage.DanishAndEnglish, stored.PlayerLanguage);
        Assert.Equal(ActivityLanguage.English, stored.GameMasterLanguage);
        Assert.Equal(KarmaType.Medium, stored.KarmaType);
        Assert.Equal(AutoSignupCategory.Author, stored.AutoSignupCategory);
        Assert.Equal(1234, stored.WordPressId);
    }

    [Fact]
    public async Task Create_SetsUpdatedAt()
    {
        using var db = CreateDbContext();
        var before = DateTimeOffset.UtcNow;

        await CreateController(db).Create(ValidDto());

        var stored = await db.Activities.SingleAsync(TestContext.Current.CancellationToken);
        Assert.InRange(stored.UpdatedAt, before, DateTimeOffset.UtcNow);
    }

    [Fact]
    public async Task Create_LeavesTeasersEmpty()
    {
        // Teasers are not on the Infosys form; opretAktivitet sets them to ''.
        using var db = CreateDbContext();

        await CreateController(db).Create(ValidDto());

        var stored = await db.Activities.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(string.Empty, stored.TeaserDanish);
        Assert.Equal(string.Empty, stored.TeaserEnglish);
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData(0, null)]
    [InlineData(1, 1)]
    [InlineData(200, 200)]
    public async Task Create_AgeZeroOrEmptyMeansNoRequirement(int? posted, int? expected)
    {
        // Infosys: empty($post->min_age) removes the requirement, and empty("0") is true.
        using var db = CreateDbContext();

        await CreateController(db).Create(ValidDto() with { MinAge = posted, MaxAge = posted });

        var stored = await db.Activities.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(expected, stored.MinAge);
        Assert.Equal(expected, stored.MaxAge);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(201)]
    public async Task Create_RejectsAgeOutsideFormLimits(int age)
    {
        // The Infosys form uses <input type="number" min="0" max="200"> for both ages.
        using var db = CreateDbContext();
        var controller = CreateController(db);

        Assert.IsType<BadRequestObjectResult>((await controller.Create(ValidDto() with { MinAge = age })).Result);
        Assert.IsType<BadRequestObjectResult>((await controller.Create(ValidDto() with { MaxAge = age })).Result);
        Assert.Empty(db.Activities);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1000)]
    public async Task Create_AcceptsMaxSignupsWithinFormLimits(int maxSignups)
    {
        using var db = CreateDbContext();

        var result = await CreateController(db).Create(ValidDto() with { MaxSignups = maxSignups });

        Assert.IsType<CreatedAtActionResult>(result.Result);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(1001)]
    public async Task Create_RejectsMaxSignupsOutsideFormLimits(int maxSignups)
    {
        // The Infosys form uses <input type="number" min="0" max="1000"> for max tilmeldinger.
        using var db = CreateDbContext();

        var result = await CreateController(db).Create(ValidDto() with { MaxSignups = maxSignups });

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task Create_AllowsEmptyTextFields()
    {
        // Infosys has no required fields on the activity form.
        using var db = CreateDbContext();
        var dto = ValidDto() with { Title = null, TitleEnglish = "", Author = null, Description = null, DescriptionEnglish = null, Note = null };

        var result = await CreateController(db).Create(dto);

        Assert.IsType<CreatedAtActionResult>(result.Result);
        var stored = await db.Activities.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(string.Empty, stored.Title);
        Assert.Equal(string.Empty, stored.Note);
    }

    [Fact]
    public async Task Create_RejectsTitleLongerThanColumn()
    {
        // aktiviteter.navn / title_en / author are varchar(256)
        using var db = CreateDbContext();
        var controller = CreateController(db);
        var tooLong = new string('x', 257);

        Assert.IsType<BadRequestObjectResult>((await controller.Create(ValidDto() with { Title = tooLong })).Result);
        Assert.IsType<BadRequestObjectResult>((await controller.Create(ValidDto() with { TitleEnglish = tooLong })).Result);
        Assert.IsType<BadRequestObjectResult>((await controller.Create(ValidDto() with { Author = tooLong })).Result);
        Assert.IsType<CreatedAtActionResult>((await controller.Create(ValidDto() with { Title = new string('x', 256) })).Result);
    }

    [Fact]
    public async Task Create_RejectsUndefinedEnumValues()
    {
        using var db = CreateDbContext();
        var controller = CreateController(db);

        Assert.IsType<BadRequestObjectResult>((await controller.Create(ValidDto() with { Type = (ActivityType)99 })).Result);
        Assert.IsType<BadRequestObjectResult>((await controller.Create(ValidDto() with { KarmaType = (KarmaType)4 })).Result);
        Assert.IsType<BadRequestObjectResult>((await controller.Create(ValidDto() with { PlayerLanguage = (ActivityLanguage)9 })).Result);
        Assert.IsType<BadRequestObjectResult>((await controller.Create(ValidDto() with { AutoSignupCategory = (AutoSignupCategory)9 })).Result);
    }

    [Fact]
    public async Task Update_ReplacesFormFields_KeepsTeasers_RefreshesUpdatedAt()
    {
        using var db = CreateDbContext();
        var old = DateTimeOffset.UtcNow.AddDays(-3);
        var activity = new Activity
        {
            Title = "Gammel titel",
            TeaserDanish = "Dansk teaser",
            TeaserEnglish = "English teaser",
            UpdatedAt = old
        };
        db.Activities.Add(activity);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await CreateController(db).Update(activity.Id, ValidDto() with { Title = "Ny titel", Type = ActivityType.Quest });

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var returned = Assert.IsType<ActivityDto>(ok.Value);
        Assert.Equal("Ny titel", returned.Title);
        Assert.Equal(ActivityType.Quest, returned.Type);
        Assert.Equal("Dansk teaser", returned.TeaserDanish);
        Assert.Equal("English teaser", returned.TeaserEnglish);
        Assert.True(returned.UpdatedAt > old);
    }

    [Fact]
    public async Task Update_EmptyAgesRemoveExistingRequirement()
    {
        using var db = CreateDbContext();
        var activity = new Activity { Title = "Med alder", MinAge = 15, MaxAge = 18 };
        db.Activities.Add(activity);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        await CreateController(db).Update(activity.Id, ValidDto() with { MinAge = 0, MaxAge = null });

        var stored = await db.Activities.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Null(stored.MinAge);
        Assert.Null(stored.MaxAge);
    }

    [Fact]
    public async Task Update_UnknownId_ReturnsNotFound()
    {
        using var db = CreateDbContext();

        var result = await CreateController(db).Update(999, ValidDto());

        Assert.IsType<NotFoundObjectResult>(result.Result);
    }

    [Fact]
    public async Task Update_InvalidInput_ReturnsBadRequest_AndChangesNothing()
    {
        using var db = CreateDbContext();
        var activity = new Activity { Title = "Uændret", MaxSignups = 5 };
        db.Activities.Add(activity);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await CreateController(db).Update(activity.Id, ValidDto() with { Title = "Ændret", MaxSignups = 5000 });

        Assert.IsType<BadRequestObjectResult>(result.Result);
        var stored = await db.Activities.AsNoTracking().SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal("Uændret", stored.Title);
    }

    [Fact]
    public async Task Delete_ActivityWithoutSchedules_IsRemoved()
    {
        using var db = CreateDbContext();
        var activity = new Activity { Title = "Slet mig" };
        db.Activities.Add(activity);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await CreateController(db).Delete(activity.Id);

        Assert.IsType<OkObjectResult>(result);
        Assert.Empty(db.Activities);
    }

    [Fact]
    public async Task Delete_ActivityWithSchedules_IsRefused()
    {
        // Infosys: afviklinger.aktivitet_id has no ON DELETE CASCADE, so the delete fails and the activity stays.
        using var db = CreateDbContext();
        var activity = new Activity { Title = "Har afviklinger" };
        db.Activities.Add(activity);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        db.Schedules.Add(new Schedule
        {
            ActivityId = activity.Id,
            StartTime = new DateTime(2027, 4, 1, 9, 0, 0, DateTimeKind.Utc),
            EndTime = new DateTime(2027, 4, 1, 13, 0, 0, DateTimeKind.Utc)
        });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await CreateController(db).Delete(activity.Id);

        Assert.IsType<ConflictObjectResult>(result);
        Assert.Single(db.Activities);
        Assert.Single(db.Schedules);
    }

    [Fact]
    public async Task Delete_UnknownId_ReturnsNotFound()
    {
        using var db = CreateDbContext();

        var result = await CreateController(db).Delete(999);

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public void KarmaType_KeepsLegacyNumericValues()
    {
        // KARMATYPE_BIG = 1, KARMATYPE_MEDIUM = 2, KARMATYPE_SMALL = 3, 0 = none; used by the karma calculation.
        Assert.Equal(0, (int)KarmaType.None);
        Assert.Equal(1, (int)KarmaType.Big);
        Assert.Equal(2, (int)KarmaType.Medium);
        Assert.Equal(3, (int)KarmaType.Small);
    }
}
