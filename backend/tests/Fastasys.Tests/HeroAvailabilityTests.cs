using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Fastasys.ApiService.Controllers;
using Fastasys.ApiService.Data;
using Fastasys.ApiService.Data.Entities;
using Fastasys.ApiService.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Fastasys.Tests;

// Pins Infosys's "Heltestyrken, Tilmeldinger" (deltagere_gdstilmeldinger) and its edit page
// (deltager/updategdstilmeldinger), ported on 2026-11-26 with the four agreed bug fixes.
public class HeroAvailabilityTests(TestWebApplicationFactory factory) : IClassFixture<TestWebApplicationFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    // Fastaval 2027: Wednesday 24 to Monday 29 March
    private static readonly ConventionOptions Convention = new()
    {
        Start = new DateOnly(2027, 3, 24),
        End = new DateOnly(2027, 3, 29),
    };

    private static DateOnly Day(int day) => new(2027, 3, day);

    private static InfosysDbContext CreateDbContext()
    {
        var context = new InfosysDbContext(
            new DbContextOptionsBuilder<InfosysDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options
        );
        context.Database.EnsureCreated();
        return context;
    }

    private static ParticipantHeroAvailabilityController Availability(InfosysDbContext db) =>
        new(db, Options.Create(Convention), NullLogger<ParticipantHeroAvailabilityController>.Instance);

    private static T Value<T>(ActionResult<T> result) =>
        Assert.IsAssignableFrom<T>(Assert.IsAssignableFrom<ObjectResult>(result.Result).Value);

    // Participants 1 and 2; the 7 Infosys categories are seeded (1 Forplejning, 2 Service, 5 Brandvagt, ...)
    private static async Task SeedAsync(
        InfosysDbContext db,
        params (int Participant, int Day, HeroPeriod Period, int? Category)[] availability
    )
    {
        foreach (var id in new[] { 1, 2 })
            db.Participants.Add(
                new Participant
                {
                    Id = id,
                    FirstName = "Deltager",
                    LastName = id.ToString(),
                }
            );
        db.ParticipantHeroAvailability.AddRange(
            availability.Select(a => new ParticipantHeroAvailability
            {
                ParticipantId = a.Participant,
                Date = Day(a.Day),
                Period = a.Period,
                CategoryId = a.Category,
            })
        );
        await db.SaveChangesAsync(Ct);
        db.ChangeTracker.Clear();
    }

    [Fact]
    public void HoursText_IsTheHoursInfosysStoresInThePeriod()
    {
        Assert.Equal("7-12", HeroAvailabilityRules.HoursText(HeroPeriod.Morning));
        Assert.Equal("12-17", HeroAvailabilityRules.HoursText(HeroPeriod.Afternoon));
        Assert.Equal("17-24", HeroAvailabilityRules.HoursText(HeroPeriod.Evening));
    }

    [Fact]
    public async Task Get_ListsThePeriodsByDayAndTime_AnyCategoryFirst()
    {
        // Infosys's primary key order put "12-17" and "17-24" before "7-12" (fixed 2026-11-26)
        using var db = CreateDbContext();
        await SeedAsync(
            db,
            (1, 25, HeroPeriod.Morning, 1),
            (1, 24, HeroPeriod.Evening, null),
            (1, 24, HeroPeriod.Morning, 5),
            (1, 24, HeroPeriod.Morning, null),
            (1, 24, HeroPeriod.Afternoon, 2),
            (2, 24, HeroPeriod.Morning, null)
        );

        var page = Value(await Availability(db).Get(1));

        Assert.Equal(
            [
                new HeroAvailabilityDto(null, null, Day(24), HeroPeriod.Morning),
                new HeroAvailabilityDto(5, "Brandvagt", Day(24), HeroPeriod.Morning),
                new HeroAvailabilityDto(2, "Service", Day(24), HeroPeriod.Afternoon),
                new HeroAvailabilityDto(null, null, Day(24), HeroPeriod.Evening),
                new HeroAvailabilityDto(1, "Forplejning", Day(25), HeroPeriod.Morning),
            ],
            page.Availability
        );
        Assert.Equal(
            [
                "Forplejning",
                "Service",
                "Rengøring",
                "Manuelt arbejde",
                "Brandvagt",
                "James (Otto-fest tjener)",
                "Ungdomslounge",
            ],
            page.Categories.Select(c => c.NameDanish)
        );
        Assert.IsType<NotFoundObjectResult>((await Availability(db).Get(99)).Result);
    }

    [Fact]
    public async Task Update_KeepsAnyCategoryPeriods_AndAPeriodListedTwiceOnce()
    {
        // Infosys dropped the signup's periods for any category on every save (empty("0")), and failed on its primary
        // key after deleting everything when a period was listed twice (fixed 2026-11-26)
        using var db = CreateDbContext();
        await SeedAsync(
            db,
            (1, 24, HeroPeriod.Morning, null),
            (1, 25, HeroPeriod.Evening, 3),
            (2, 24, HeroPeriod.Morning, null)
        );

        var result = await Availability(db)
            .Update(
                1,
                new UpdateParticipantHeroAvailabilityDto([
                    new HeroAvailabilityFieldsDto(null, Day(24), HeroPeriod.Morning),
                    new HeroAvailabilityFieldsDto(5, Day(26), HeroPeriod.Afternoon),
                    new HeroAvailabilityFieldsDto(5, Day(26), HeroPeriod.Afternoon),
                ])
            );

        Assert.IsType<NoContentResult>(result);
        Assert.Equal(
            [(Day(24), HeroPeriod.Morning, (int?)null), (Day(26), HeroPeriod.Afternoon, 5)],
            db.ParticipantHeroAvailability.AsNoTracking()
                .Where(a => a.ParticipantId == 1)
                .OrderBy(a => a.Date)
                .ToList()
                .Select(a => (a.Date, a.Period, a.CategoryId))
        );
        // The other participant's period is untouched
        Assert.Single(db.ParticipantHeroAvailability.Where(a => a.ParticipantId == 2));
    }

    [Fact]
    public async Task Update_RemovesEverythingForAnEmptyList_AndRefusesAnUnknownParticipant()
    {
        using var db = CreateDbContext();
        await SeedAsync(db, (1, 24, HeroPeriod.Morning, null));

        Assert.IsType<NoContentResult>(
            await Availability(db).Update(1, new UpdateParticipantHeroAvailabilityDto(null))
        );
        Assert.Empty(db.ParticipantHeroAvailability);
        Assert.IsType<NotFoundObjectResult>(
            await Availability(db).Update(99, new UpdateParticipantHeroAvailabilityDto([]))
        );
    }

    [Fact]
    public async Task Update_ChangesNothingWhenAPeriodIsInvalid()
    {
        using var db = CreateDbContext();
        await SeedAsync(db, (1, 24, HeroPeriod.Morning, null));

        var result = await Availability(db)
            .Update(
                1,
                new UpdateParticipantHeroAvailabilityDto([
                    new HeroAvailabilityFieldsDto(null, Day(25), HeroPeriod.Morning),
                    new HeroAvailabilityFieldsDto(99, Day(25), HeroPeriod.Morning),
                ])
            );

        Assert.IsType<BadRequestObjectResult>(result);
        Assert.Single(db.ParticipantHeroAvailability);
    }

    [Fact]
    public void Validate_NeedsADayAPartOfTheDayAndAKnownCategory_OnAConventionDay()
    {
        var categories = new HashSet<int> { 1, 2 };
        (bool, string) Check(HeroAvailabilityFieldsDto period, params DateOnly[] current) =>
            ParticipantHeroAvailabilityController.Validate([period], categories, Convention, current.ToHashSet());

        Assert.Equal((true, ""), Check(new HeroAvailabilityFieldsDto(null, Day(24), HeroPeriod.Morning)));
        Assert.Equal((true, ""), Check(new HeroAvailabilityFieldsDto(2, Day(29), HeroPeriod.Evening)));
        Assert.False(Check(new HeroAvailabilityFieldsDto(null, null, HeroPeriod.Morning)).Item1);
        Assert.False(Check(new HeroAvailabilityFieldsDto(null, Day(24), null)).Item1);
        Assert.False(Check(new HeroAvailabilityFieldsDto(null, Day(24), (HeroPeriod)9)).Item1);
        Assert.Equal(
            (false, "Unknown hero force category"),
            Check(new HeroAvailabilityFieldsDto(9, Day(24), HeroPeriod.Morning))
        );
        Assert.Equal(
            (false, "A period must be on one of the convention's days"),
            Check(new HeroAvailabilityFieldsDto(null, Day(30), HeroPeriod.Morning))
        );
        // A day the participant has already stays possible, and without the convention's dates nothing is checked
        Assert.Equal((true, ""), Check(new HeroAvailabilityFieldsDto(null, Day(30), HeroPeriod.Morning), Day(30)));
        Assert.Equal(
            (true, ""),
            ParticipantHeroAvailabilityController.Validate(
                [new HeroAvailabilityFieldsDto(null, Day(30), HeroPeriod.Morning)],
                categories,
                new ConventionOptions(),
                new HashSet<DateOnly>()
            )
        );
    }

    private async Task<HttpClient> ClientAsync(string? privilege)
    {
        if (privilege == null)
            return factory.CreateClient();
        var username = $"hero-{Guid.NewGuid():N}";
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<InfosysDbContext>();
        var role = new Role { Name = username };
        role.RolePrivileges.Add(
            new RolePrivilege
            {
                Privilege = new Privilege { Key = privilege, Name = privilege },
            }
        );
        var user = new User
        {
            Username = username,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("password"),
            IsActive = true,
        };
        user.UserRoles.Add(new UserRole { Role = role });
        db.Users.Add(user);
        await db.SaveChangesAsync(Ct);
        var auth = await scope.ServiceProvider.GetRequiredService<IAuthService>().LoginAsync(new(username, "password"));
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.Token);
        return client;
    }

    [Theory]
    [InlineData(null, HttpStatusCode.Unauthorized)]
    [InlineData("participant_edit", HttpStatusCode.Forbidden)]
    [InlineData("hero_force_edit", HttpStatusCode.Forbidden)]
    [InlineData("hero_force_participant_edit", HttpStatusCode.NotFound)]
    public async Task HeroAvailability_NeedsHeroForceParticipantEdit(string? privilege, HttpStatusCode expected)
    {
        // An unknown participant: 404 once the user may change the availability at all
        using var client = await ClientAsync(privilege);

        Assert.Equal(expected, (await client.GetAsync("/api/participants/987654/hero-availability", Ct)).StatusCode);
        Assert.Equal(
            expected,
            (
                await client.PutAsJsonAsync(
                    "/api/participants/987654/hero-availability",
                    new UpdateParticipantHeroAvailabilityDto([]),
                    Ct
                )
            ).StatusCode
        );
    }
}
