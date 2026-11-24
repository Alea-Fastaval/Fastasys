using System.Net.Http.Headers;
using System.Net.Http.Json;
using Fastasys.ApiService.Controllers;
using Fastasys.ApiService.Data;
using Fastasys.ApiService.Data.Entities;
using Fastasys.ApiService.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Fastasys.Tests;

// Pins Infosys's Indgang pages (EntranceController) and the tickets on the participant (deltagere_indgang), ported on
// 2026-11-24 with explicit kind fields instead of words in the name and the four agreed bug fixes.
public class TicketTypesTests(TestWebApplicationFactory factory) : IClassFixture<TestWebApplicationFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static InfosysDbContext CreateDbContext()
    {
        var context = new InfosysDbContext(
            new DbContextOptionsBuilder<InfosysDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options
        );
        context.Database.EnsureCreated();
        return context;
    }

    private static TicketTypesController Types(InfosysDbContext db) =>
        new(db, NullLogger<TicketTypesController>.Instance);

    private static ParticipantTicketsController Tickets(InfosysDbContext db) =>
        new(db, NullLogger<ParticipantTicketsController>.Instance);

    private static TicketType Type(
        int id,
        string name,
        int day = 24,
        TicketKind kind = TicketKind.Entrance,
        bool partout = false
    ) =>
        new()
        {
            Id = id,
            Name = name,
            Price = 75,
            Day = new DateOnly(2027, 3, day),
            Kind = kind,
            IsAllConvention = partout,
        };

    private static CreateTicketTypeDto ValidCreate() =>
        new()
        {
            Name = "Indgang - Partout",
            Price = 300,
            Day = new DateOnly(2027, 3, 24),
            Kind = TicketKind.Entrance,
            IsAllConvention = true,
        };

    private static T Value<T>(ActionResult<T> result) =>
        Assert.IsAssignableFrom<T>(Assert.IsAssignableFrom<ObjectResult>(result.Result).Value);

    private static async Task SeedAsync(
        InfosysDbContext db,
        IEnumerable<TicketType> types,
        params (int Participant, int Type)[] tickets
    )
    {
        db.TicketTypes.AddRange(types);
        foreach (var id in tickets.Select(t => t.Participant).Distinct())
            db.Participants.Add(
                new Participant
                {
                    Id = id,
                    FirstName = "Deltager",
                    LastName = id.ToString(),
                }
            );
        db.ParticipantTickets.AddRange(
            tickets.Select(t => new ParticipantTicket { ParticipantId = t.Participant, TicketTypeId = t.Type })
        );
        await db.SaveChangesAsync(Ct);
        db.ChangeTracker.Clear();
    }

    [Fact]
    public async Task GetAll_SortsByNameLikeStrcmp_WithTheNumberSold()
    {
        // Infosys entryTypes: usort with strcmp (byte-wise, so capitals first), ties in id order; "Antal solgt"
        // counts every participant who has the type
        using var db = CreateDbContext();
        await SeedAsync(
            db,
            [
                Type(1, "indgang"),
                Type(2, "Overnatning - Enkelt", day: 25),
                Type(3, "Indgang - Enkelt"),
                Type(4, "Indgang - Enkelt", day: 25),
            ],
            (1, 3),
            (2, 3),
            (2, 2)
        );

        var types = Value(await Types(db).GetAll()).ToList();

        Assert.Equal([3, 4, 2, 1], types.Select(t => t.Id));
        Assert.Equal([2, 0, 1, 0], types.Select(t => t.SoldCount));
    }

    [Fact]
    public async Task Get_ShowsOneTypeOr404()
    {
        using var db = CreateDbContext();
        await SeedAsync(db, [Type(5, "Ottofest", kind: TicketKind.Other)], (1, 5));

        var type = Value(await Types(db).Get(5));

        Assert.Equal(
            new TicketTypeDto(5, "Ottofest", 75, new DateOnly(2027, 3, 24), TicketKind.Other, false, false, 1),
            type
        );
        Assert.IsType<NotFoundObjectResult>((await Types(db).Get(6)).Result);
    }

    [Fact]
    public async Task Create_StoresTheFields()
    {
        using var db = CreateDbContext();

        var result = await Types(db).Create(ValidCreate() with { IsDigital = true });

        var created = Assert.IsType<TicketTypeDto>(Assert.IsType<CreatedAtActionResult>(result.Result).Value);
        var stored = await db.TicketTypes.SingleAsync(Ct);
        Assert.Equal(stored.Id, created.Id);
        Assert.Equal("Indgang - Partout", stored.Name);
        Assert.Equal(300, stored.Price);
        Assert.Equal(new DateOnly(2027, 3, 24), stored.Day);
        Assert.Equal(TicketKind.Entrance, stored.Kind);
        Assert.True(stored.IsAllConvention);
        Assert.True(stored.IsDigital);
        Assert.Equal(0, created.SoldCount);
    }

    public static TheoryData<CreateTicketTypeDto, bool> Inputs =>
        new()
        {
            { ValidCreate(), true },
            // Infosys: empty($post->type), which is also true for "0"
            { ValidCreate() with { Name = null }, false },
            { ValidCreate() with { Name = "" }, false },
            { ValidCreate() with { Name = "0" }, false },
            { ValidCreate() with { Name = " " }, true },
            // indgang.type is varchar(64)
            { ValidCreate() with { Name = new string('x', 64) }, true },
            { ValidCreate() with { Name = new string('x', 65) }, false },
            // Infosys create: ctype_digit, whole kroner of 0 or more
            { ValidCreate() with { Price = 0 }, true },
            { ValidCreate() with { Price = -1 }, false },
            { ValidCreate() with { Price = null }, false },
            // Infosys turned an empty start into 1970-01-01 (fixed 2026-11-24)
            { ValidCreate() with { Day = null }, false },
            { ValidCreate() with { Kind = null }, false },
            { ValidCreate() with { Kind = (TicketKind)99 }, false },
        };

    [Theory]
    [MemberData(nameof(Inputs))]
    public void Validate_PinsTheFormRules(CreateTicketTypeDto dto, bool isValid)
    {
        var (valid, error) = TicketTypesController.Validate(dto);

        Assert.Equal(isValid, valid);
        Assert.Equal(isValid, error.Length == 0);
    }

    [Fact]
    public async Task Update_ChecksThePriceLikeCreate()
    {
        // Infosys's edit form stored any price ("abc" or empty became 0); it is checked like create (fixed 2026-11-24)
        using var db = CreateDbContext();
        await SeedAsync(db, [Type(1, "Indgang - Enkelt")]);
        var update = new UpdateTicketTypeDto
        {
            Name = "Dagsbillet",
            Price = -5,
            Day = new DateOnly(2027, 3, 26),
            Kind = TicketKind.Entrance,
        };

        Assert.IsType<BadRequestObjectResult>((await Types(db).Update(1, update)).Result);
        Assert.Equal(75, (await db.TicketTypes.SingleAsync(Ct)).Price);

        var updated = Value(await Types(db).Update(1, update with { Price = 80 }));
        Assert.Equal(
            new TicketTypeDto(1, "Dagsbillet", 80, new DateOnly(2027, 3, 26), TicketKind.Entrance, false, false, 0),
            updated
        );
        Assert.IsType<NotFoundObjectResult>((await Types(db).Update(2, update with { Price = 80 })).Result);
    }

    [Fact]
    public async Task Delete_RefusesATypeThatParticipantsHave()
    {
        // Infosys's foreign key from deltagere_indgang refused it with an error page; it is refused with a reason
        using var db = CreateDbContext();
        await SeedAsync(db, [Type(1, "Indgang - Partout"), Type(2, "Ottofest")], (1, 1));

        var refused = Assert.IsType<ConflictObjectResult>(await Types(db).Delete(1));
        Assert.Contains("inUse", refused.Value!.ToString());
        Assert.IsType<OkObjectResult>(await Types(db).Delete(2));
        Assert.IsType<NotFoundObjectResult>(await Types(db).Delete(2));

        Assert.Equal([1], db.TicketTypes.Select(t => t.Id));
    }

    [Fact]
    public void ToStats_GroupsByNameLikeInfosysKeysTheTypes()
    {
        // EntranceModel::getEntryStats: $return[$type][$start] = $entry
        var types = new List<TicketType>
        {
            Type(1, "Indgang - Partout", partout: true),
            Type(2, "Indgang - Enkelt", day: 25),
            Type(3, "Overnatning - Enkelt", day: 25, kind: TicketKind.Sleeping),
            Type(4, "Indgang - Enkelt", day: 24),
            // The same name and day as type 2: it takes 2's place, so 2 is not counted
            Type(5, "Indgang - Enkelt", day: 25),
        };
        var counts = new Dictionary<int, int>
        {
            [1] = 10,
            [2] = 7,
            [4] = 3,
            [5] = 2,
        };

        var stats = TicketTypesController.ToStats(types, counts);

        Assert.Equal(["Indgang - Partout", "Indgang - Enkelt", "Overnatning - Enkelt"], stats.Select(g => g.Name));
        Assert.Equal([new TicketStatsDayDto(new DateOnly(2027, 3, 24), 10)], stats[0].Days);
        Assert.True(stats[0].IsAllConvention);
        Assert.Equal(
            [new TicketStatsDayDto(new DateOnly(2027, 3, 25), 2), new TicketStatsDayDto(new DateOnly(2027, 3, 24), 3)],
            stats[1].Days
        );
        Assert.Equal(TicketKind.Sleeping, stats[2].Kind);
        Assert.Equal([new TicketStatsDayDto(new DateOnly(2027, 3, 25), 0)], stats[2].Days);
    }

    [Fact]
    public void ToStats_TakesTheHeadingFromTheFirstDaysType()
    {
        // entrystats.phtml uses reset($entry): the type in the heading's first slot, after any replacement
        var types = new List<TicketType>
        {
            Type(1, "Campingvogn", kind: TicketKind.Other),
            Type(2, "Campingvogn", kind: TicketKind.Other, day: 25),
            Type(3, "Campingvogn", kind: TicketKind.Sleeping, partout: true),
        };

        var group = Assert.Single(TicketTypesController.ToStats(types, new Dictionary<int, int>()));

        Assert.Equal(TicketKind.Sleeping, group.Kind);
        Assert.True(group.IsAllConvention);
        Assert.Equal(2, group.Days.Count);
    }

    [Fact]
    public async Task ParticipantTickets_ListsTheTicketsAndEveryType_InIdOrder()
    {
        using var db = CreateDbContext();
        await SeedAsync(
            db,
            [
                Type(3, "Ottofest", kind: TicketKind.Other),
                Type(1, "Indgang - Partout", partout: true),
                Type(2, "Indgang - Enkelt"),
            ],
            (1, 3),
            (1, 1),
            (2, 2)
        );

        var page = Value(await Tickets(db).Get(1));

        Assert.Equal(1, page.ParticipantId);
        Assert.Equal([1, 3], page.Tickets.Select(t => t.Id));
        Assert.Equal([1, 2, 3], page.TicketTypes.Select(t => t.Id));
        Assert.Equal(new TicketTypeOptionDto(1, "Indgang - Partout", new DateOnly(2027, 3, 24), true), page.Tickets[0]);
        Assert.IsType<NotFoundObjectResult>((await Tickets(db).Get(9)).Result);
    }

    [Fact]
    public async Task ParticipantTickets_UpdateReplacesTheTickets()
    {
        // Infosys handleEntranceUpdate deletes the participant's tickets and inserts the posted ones. An id that isn't
        // a ticket type is skipped, as in Infosys; a type posted twice is kept once (Infosys crashed, fixed 2026-11-24).
        using var db = CreateDbContext();
        await SeedAsync(
            db,
            [Type(1, "Indgang - Partout"), Type(2, "Indgang - Enkelt"), Type(3, "Ottofest")],
            (1, 1),
            (1, 2),
            (2, 1)
        );

        var result = await Tickets(db).Update(1, new UpdateParticipantTicketsDto([3, 2, 3, 99]));

        Assert.IsType<NoContentResult>(result);
        Assert.Equal(
            [2, 3],
            db.ParticipantTickets.Where(t => t.ParticipantId == 1).Select(t => t.TicketTypeId).Order()
        );
        Assert.Equal([1], db.ParticipantTickets.Where(t => t.ParticipantId == 2).Select(t => t.TicketTypeId));

        db.ChangeTracker.Clear();
        Assert.IsType<NoContentResult>(await Tickets(db).Update(1, new UpdateParticipantTicketsDto(null)));
        Assert.Empty(db.ParticipantTickets.Where(t => t.ParticipantId == 1));
        Assert.IsType<NotFoundObjectResult>(await Tickets(db).Update(9, new UpdateParticipantTicketsDto([1])));
    }

    private async Task<HttpClient> ClientAsync(string? privilege)
    {
        if (privilege == null)
            return factory.CreateClient();
        var username = $"tickets-{Guid.NewGuid():N}";
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
    [InlineData(null, HttpStatusCode.Unauthorized, HttpStatusCode.Unauthorized)]
    [InlineData("participant_edit", HttpStatusCode.Forbidden, HttpStatusCode.Forbidden)]
    [InlineData("entrance_participant_edit", HttpStatusCode.Forbidden, HttpStatusCode.Forbidden)]
    [InlineData("entrance_view", HttpStatusCode.OK, HttpStatusCode.Forbidden)]
    [InlineData("entrance_edit", HttpStatusCode.OK, HttpStatusCode.Created)]
    public async Task TicketTypes_EntranceViewMayLook_EntranceEditMayChange(
        string? privilege,
        HttpStatusCode look,
        HttpStatusCode create
    )
    {
        using var client = await ClientAsync(privilege);

        Assert.Equal(look, (await client.GetAsync("/api/ticket-types", Ct)).StatusCode);
        Assert.Equal(look, (await client.GetAsync("/api/ticket-types/stats", Ct)).StatusCode);
        Assert.Equal(create, (await client.PostAsJsonAsync("/api/ticket-types", ValidCreate(), Ct)).StatusCode);
    }

    [Theory]
    [InlineData(null, HttpStatusCode.Unauthorized)]
    [InlineData("participant_edit", HttpStatusCode.Forbidden)]
    [InlineData("entrance_edit", HttpStatusCode.Forbidden)]
    [InlineData("entrance_participant_edit", HttpStatusCode.NotFound)]
    public async Task ParticipantTickets_NeedEntranceParticipantEdit(string? privilege, HttpStatusCode expected)
    {
        // An unknown participant: 404 once the user may change tickets at all
        using var client = await ClientAsync(privilege);

        Assert.Equal(expected, (await client.GetAsync("/api/participants/987654/tickets", Ct)).StatusCode);
        Assert.Equal(
            expected,
            (
                await client.PutAsJsonAsync("/api/participants/987654/tickets", new UpdateParticipantTicketsDto([]), Ct)
            ).StatusCode
        );
    }
}
