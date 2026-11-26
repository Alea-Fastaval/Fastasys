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

namespace Fastasys.Tests;

// Pins Infosys's Wear pages (WearController: Wear-typer, one type, create, edit, delete, the order, Rediger wear
// egenskaber, image upload, Bestillings-oversigt, the order lists, Wear-udlevering) and the participant's wear
// (deltagere_wear_order), ported on 2026-11-26 with the agreed bug fixes.
public class WearTests(TestWebApplicationFactory factory) : IClassFixture<TestWebApplicationFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    // Attributes: the seeded sizes 12 S, 13 M, 14 L, 15 XL; models 101 Herre and 102 Dame; colors 111 Sort and 112 Hvid
    private const int S = 12,
        M = 13,
        L = 14,
        XL = 15;
    private const int Herre = 101,
        Dame = 102,
        Sort = 111,
        Hvid = 112;

    private static InfosysDbContext CreateDbContext()
    {
        var context = new InfosysDbContext(
            new DbContextOptionsBuilder<InfosysDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options
        );
        context.Database.EnsureCreated();
        return context;
    }

    private static WearTypesController Types(InfosysDbContext db) => new(db, NullLogger<WearTypesController>.Instance);

    private static WearAttributesController Attributes(InfosysDbContext db) =>
        new(db, NullLogger<WearAttributesController>.Instance);

    private static WearImagesController Images(InfosysDbContext db) =>
        new(db, NullLogger<WearImagesController>.Instance);

    private static WearOrdersController Orders(InfosysDbContext db) =>
        new(db, NullLogger<WearOrdersController>.Instance);

    private static WearHandoutController Handout(InfosysDbContext db) =>
        new(db, NullLogger<WearHandoutController>.Instance);

    private static ParticipantWearController ParticipantWear(InfosysDbContext db) =>
        new(db, NullLogger<ParticipantWearController>.Instance);

    private static T Value<T>(ActionResult<T> result) =>
        Assert.IsAssignableFrom<T>(Assert.IsAssignableFrom<ObjectResult>(result.Result).Value);

    private static object? Field(IActionResult result, string name)
    {
        var value = Assert.IsAssignableFrom<ObjectResult>(result).Value!;
        return value.GetType().GetProperty(name)?.GetValue(value);
    }

    // Wear type 1 "T-shirt" (position 2) with prices 1 (Deltager 150) and 2 (Arrangør 100), variant 0 = Herre/Dame,
    // Sort, S/M and variant 1 = Herre, Hvid, L. Wear type 2 "Kasket" (position 1) with price 3 (Deltager 80) and no
    // variants. Participants 1-3, 3 annulled; 1 and 2 checked in.
    private static async Task SeedAsync(InfosysDbContext db)
    {
        db.WearAttributes.AddRange(
            new WearAttribute
            {
                Id = Herre,
                Kind = WearAttributeKind.Model,
                NameDanish = "Herre",
                Position = 1,
            },
            new WearAttribute
            {
                Id = Dame,
                Kind = WearAttributeKind.Model,
                NameDanish = "Dame",
                Position = 2,
            },
            new WearAttribute
            {
                Id = Sort,
                Kind = WearAttributeKind.Color,
                NameDanish = "Sort",
                Position = 1,
            },
            new WearAttribute
            {
                Id = Hvid,
                Kind = WearAttributeKind.Color,
                NameDanish = "Hvid",
                Position = 2,
            }
        );
        var shirt = new WearType
        {
            Id = 1,
            NameDanish = "T-shirt",
            Position = 2,
        };
        shirt.Prices.Add(
            new WearPrice
            {
                Id = 1,
                ParticipantCategoryId = ParticipantCategory.ParticipantId,
                Price = 150,
            }
        );
        shirt.Prices.Add(
            new WearPrice
            {
                Id = 2,
                ParticipantCategoryId = ParticipantCategory.OrganizerId,
                Price = 100,
            }
        );
        foreach (var attributeId in new[] { Herre, Dame, Sort, S, M })
            shirt.VariantAttributes.Add(new WearVariantAttribute { Variant = 0, AttributeId = attributeId });
        foreach (var attributeId in new[] { Herre, Hvid, L })
            shirt.VariantAttributes.Add(new WearVariantAttribute { Variant = 1, AttributeId = attributeId });
        var cap = new WearType
        {
            Id = 2,
            NameDanish = "Kasket",
            Position = 1,
        };
        cap.Prices.Add(
            new WearPrice
            {
                Id = 3,
                ParticipantCategoryId = ParticipantCategory.ParticipantId,
                Price = 80,
            }
        );
        db.WearTypes.AddRange(shirt, cap);
        foreach (var id in new[] { 1, 2, 3 })
            db.Participants.Add(
                new Participant
                {
                    Id = id,
                    FirstName = "Deltager",
                    LastName = id.ToString(),
                    IsAnnulled = id == 3,
                    CheckedInAt = id == 3 ? null : new DateTime(2027, 3, 24, 10, 0, 0, DateTimeKind.Utc),
                    CategoryId = id == 2 ? ParticipantCategory.OrganizerId : ParticipantCategory.ParticipantId,
                }
            );
        await db.SaveChangesAsync(Ct);
        db.ChangeTracker.Clear();
    }

    private static async Task AddOrderAsync(
        InfosysDbContext db,
        int id,
        int participantId,
        int priceId,
        int amount,
        bool received,
        params int[] attributeIds
    )
    {
        var order = new ParticipantWearOrder
        {
            Id = id,
            ParticipantId = participantId,
            WearPriceId = priceId,
            Amount = amount,
            IsReceived = received,
        };
        foreach (var attributeId in attributeIds)
            order.Attributes.Add(new ParticipantWearOrderAttribute { AttributeId = attributeId });
        db.ParticipantWearOrders.Add(order);
        await db.SaveChangesAsync(Ct);
        db.ChangeTracker.Clear();
    }

    private static WearRules.Attribute A(int id, WearAttributeKind kind, string name, int position = 1) =>
        new(id, kind, name, position);

    // ---- WearRules ----

    [Fact]
    public void OrderName_AddsTheAttributesThatArentASizeInTheOrderModelDesignColor()
    {
        // DeltagereWear::getWearName('da', false) and getSizeName
        var attributes = new[]
        {
            A(1, WearAttributeKind.Color, "Sort"),
            A(2, WearAttributeKind.Size, "M"),
            A(3, WearAttributeKind.Model, "Herre", 5),
            A(4, WearAttributeKind.Design, "Logo"),
        };

        Assert.Equal("T-shirt-Herre-Logo-Sort", WearRules.OrderName("T-shirt", attributes));
        Assert.Equal("M", WearRules.SizeName(attributes));
        Assert.Equal("Kasket", WearRules.OrderName("Kasket", []));
        Assert.Null(WearRules.SizeName([A(1, WearAttributeKind.Color, "Sort")]));
    }

    [Fact]
    public void OrderName_KeepsTheLastAttributeOfAKindByPosition()
    {
        // DeltagereWear::getAttributes reads by position and keeps one attribute per kind
        var attributes = new[] { A(1, WearAttributeKind.Color, "Hvid", 2), A(2, WearAttributeKind.Color, "Sort", 1) };

        Assert.Equal("T-shirt-Hvid", WearRules.OrderName("T-shirt", attributes));
    }

    [Fact]
    public void MatchesVariant_TakesOneAttributeOfEachKindInOneVariant()
    {
        // Infosys only limited the choice in the browser (fixed 2026-11-26)
        var herre = A(Herre, WearAttributeKind.Model, "Herre");
        var dame = A(Dame, WearAttributeKind.Model, "Dame");
        var sort = A(Sort, WearAttributeKind.Color, "Sort");
        var hvid = A(Hvid, WearAttributeKind.Color, "Hvid");
        var s = A(S, WearAttributeKind.Size, "S");
        var l = A(L, WearAttributeKind.Size, "L");
        var variants = new List<WearRules.VariantAttribute>
        {
            new(0, herre),
            new(0, dame),
            new(0, sort),
            new(0, s),
            new(1, herre),
            new(1, hvid),
            new(1, l),
        };

        Assert.True(WearRules.MatchesVariant([dame, sort, s], variants));
        Assert.True(WearRules.MatchesVariant([herre, hvid, l], variants));
        // Hvid is only in variant 1 and S only in variant 0
        Assert.False(WearRules.MatchesVariant([herre, hvid, s], variants));
        // A kind is missing, two of a kind, or the same attribute twice
        Assert.False(WearRules.MatchesVariant([dame, sort], variants));
        Assert.False(WearRules.MatchesVariant([herre, dame, sort, s], variants));
        Assert.False(WearRules.MatchesVariant([dame, sort, s, s], variants));
        // A wear type without variants takes no attributes
        Assert.True(WearRules.MatchesVariant([], []));
        Assert.False(WearRules.MatchesVariant([s], []));
    }

    [Fact]
    public void Breakdown_CountsEachCombinationOfEachVariantPerSize()
    {
        // WearModel::getWearBreakdown and createVariantCombinations
        var herre = A(Herre, WearAttributeKind.Model, "Herre", 1);
        var dame = A(Dame, WearAttributeKind.Model, "Dame", 2);
        var sort = A(Sort, WearAttributeKind.Color, "Sort", 1);
        var hvid = A(Hvid, WearAttributeKind.Color, "Hvid", 2);
        var s = A(S, WearAttributeKind.Size, "S", 12);
        var m = A(M, WearAttributeKind.Size, "M", 13);
        var l = A(L, WearAttributeKind.Size, "L", 14);
        var xl = A(XL, WearAttributeKind.Size, "XL", 15);
        var cap = new WearRules.WearTypeVariants(2, "Kasket", []);
        var shirt = new WearRules.WearTypeVariants(
            1,
            "T-shirt",
            [
                new(0, sort),
                new(0, dame),
                new(0, herre),
                new(0, s),
                new(0, m),
                new(1, herre),
                new(1, hvid),
                new(1, l),
                // The combination Herre/Sort again: Infosys counts it on the first row only
                new(2, herre),
                new(2, sort),
                new(2, xl),
            ]
        );
        var orders = new List<WearRules.Order>
        {
            new(1, 1, 2, [herre, sort, s]),
            new(2, 1, 1, [herre, sort, m]),
            new(3, 1, 1, [dame, sort, s]),
            new(4, 1, 3, [herre, hvid, l]),
            new(5, 2, 4, []),
            new(6, 1, 1, [herre, sort]),
            // Dame/Hvid isn't in any variant: Infosys left it out of the table (fixed 2026-11-26)
            new(7, 1, 1, [dame, hvid, m]),
            new(8, 1, 1, [herre, sort, xl]),
        };

        var rows = WearRules.Breakdown([cap, shirt], orders);

        Assert.Equal(
            [
                (2, "", true, 4, 4, ""),
                (1, "Herre,Sort", true, 5, 1, "12:2,13:1,15:1"),
                (1, "Dame,Sort", false, 1, 0, "12:1"),
                (1, "Herre,Hvid", true, 3, 0, "14:3"),
                (1, "Herre,Sort", true, 0, 0, ""),
                (1, "Dame,Hvid", true, 1, 0, "13:1"),
            ],
            rows.Select(r =>
                (
                    r.WearTypeId,
                    string.Join(",", r.Attributes.Select(a => a.NameDanish)),
                    r.StartsVariant,
                    r.Total,
                    r.NoSize,
                    string.Join(",", r.BySize.OrderBy(b => b.Key).Select(b => $"{b.Key}:{b.Value}"))
                )
            )
        );
    }

    // ---- Wear types ----

    [Fact]
    public async Task GetAll_ListsTheTypesByPosition()
    {
        using var db = CreateDbContext();
        await SeedAsync(db);

        Assert.Equal(
            [new WearTypeSummaryDto(2, "Kasket", "", 1), new WearTypeSummaryDto(1, "T-shirt", "", 2)],
            Value(await Types(db).GetAll())
        );
    }

    [Fact]
    public async Task GetCategories_ListsTheParticipantCategoriesForThePrices()
    {
        // WearModel::getAllParticipantCategories (the page sorts them by name)
        using var db = CreateDbContext();

        var categories = Value(await Types(db).GetCategories()).ToList();

        Assert.Equal(["Deltager", "Arrangør", "Junior", "Forældre", "Freeloader"], categories.Select(c => c.Name));
    }

    [Fact]
    public async Task Get_ShowsVariantsByKindPricesInIdOrderAndTheOrders()
    {
        using var db = CreateDbContext();
        await SeedAsync(db);
        await AddOrderAsync(db, 1, 1, 1, 2, false, Herre, Sort, S);
        await AddOrderAsync(db, 2, 3, 1, 1, false, Dame, Sort, M);

        var wear = Value(await Types(db).Get(1));

        // Wear::getVariants: model, design, color, size; each kind by position
        Assert.Equal(
            ["Herre,Dame,Sort,S,M", "Herre,Hvid,L"],
            wear.Variants.Select(v => string.Join(",", v.Select(a => a.NameDanish)))
        );
        // The annulled participant's order counts here: it still blocks removing the price
        Assert.Equal(
            [
                new WearPriceDto(1, ParticipantCategory.ParticipantId, "Deltager", 150, 2),
                new WearPriceDto(2, ParticipantCategory.OrganizerId, "Arrangør", 100, 0),
            ],
            wear.Prices
        );
        Assert.Equal(2, wear.OrderCount);
        Assert.IsType<NotFoundObjectResult>((await Types(db).Get(99)).Result);
    }

    private static CreateWearTypeDto ValidCreate() =>
        new()
        {
            NameDanish = "Hoodie",
            NameEnglish = "Hoodie",
            DescriptionDanish = "Varm",
            MaxOrder = 2,
            IsRequired = true,
            Prices = [new WearPriceFieldsDto(ParticipantCategory.ParticipantId, 350)],
            Variants =
            [
                [S, M],
                [],
                [L, L],
            ],
        };

    [Fact]
    public async Task Create_PutsTheTypeLastAndDropsEmptyVariants()
    {
        // WearModel::createWear: position MAX + 1; the variants are numbered by their place, an empty one is dropped
        using var db = CreateDbContext();
        await SeedAsync(db);

        var created = Value(await Types(db).Create(ValidCreate()));

        var wear = await db.WearTypes.Include(w => w.VariantAttributes).SingleAsync(w => w.Id == created.Id, Ct);
        Assert.Equal(3, wear.Position);
        Assert.Equal(2, wear.MaxOrder);
        Assert.True(wear.IsRequired);
        Assert.Equal(
            [(0, S), (0, M), (1, L)],
            wear.VariantAttributes.OrderBy(v => v.Variant)
                .ThenBy(v => v.AttributeId)
                .Select(v => (v.Variant, v.AttributeId))
        );
        Assert.Equal([new WearPriceDto(created.Prices[0].Id, 1, "Deltager", 350, 0)], created.Prices);
    }

    [Fact]
    public async Task Create_SavesAnEmptyMaksBestillingAs0()
    {
        // Infosys: (!empty($post->max_order)) ? $post->max_order : 0
        using var db = CreateDbContext();

        var created = Value(
            await Types(db).Create(ValidCreate() with { MaxOrder = null, Prices = [], Variants = null })
        );

        Assert.Equal(0, created.MaxOrder);
        Assert.Equal(1, (await db.WearTypes.SingleAsync(Ct)).Position);
    }

    [Theory]
    [InlineData("", "The name is required")]
    [InlineData("0", "The name is required")]
    public async Task Create_RefusesAnEmptyName(string name, string error)
    {
        // PHP's empty() is true for "0"
        using var db = CreateDbContext();

        var result = Assert.IsType<BadRequestObjectResult>(
            (await Types(db).Create(ValidCreate() with { NameDanish = name })).Result
        );

        Assert.Equal(error, Field(result, "message"));
    }

    [Fact]
    public void Validate_ChecksPricesMaksBestillingAttributesAndImages()
    {
        // Infosys took any text as a price and Maks bestilling (fixed 2026-11-26)
        var categories = new HashSet<int> { 1, 2 };
        var attributes = new HashSet<int> { S, M };
        var images = new HashSet<int> { 7 };
        (bool, string) Check(CreateWearTypeDto dto) =>
            WearTypesController.Validate(dto, categories, attributes, images);
        var valid = new CreateWearTypeDto
        {
            NameDanish = "Hoodie",
            Prices = [new WearPriceFieldsDto(1, 0)],
            Variants =
            [
                [S],
            ],
            ImageLinks = [new WearImageLinkDto(7, null), new WearImageLinkDto(7, M)],
        };

        Assert.Equal((true, ""), Check(valid));
        Assert.False(Check(valid with { NameDanish = new string('x', 65) }).Item1);
        Assert.False(Check(valid with { NameEnglish = new string('x', 65) }).Item1);
        Assert.False(Check(valid with { MaxOrder = -1 }).Item1);
        Assert.False(Check(valid with { Prices = [new WearPriceFieldsDto(1, -1)] }).Item1);
        Assert.False(Check(valid with { Prices = [new WearPriceFieldsDto(1, null)] }).Item1);
        Assert.False(Check(valid with { Prices = [new WearPriceFieldsDto(9, 10)] }).Item1);
        Assert.Equal(
            (false, "A participant category can have only one price"),
            Check(valid with { Prices = [new WearPriceFieldsDto(1, 10), new WearPriceFieldsDto(1, 20)] })
        );
        Assert.False(
            Check(
                valid with
                {
                    Variants =
                    [
                        [99],
                    ],
                }
            ).Item1
        );
        Assert.False(Check(valid with { ImageLinks = [new WearImageLinkDto(8, null)] }).Item1);
        Assert.False(Check(valid with { ImageLinks = [new WearImageLinkDto(7, 99)] }).Item1);
    }

    [Fact]
    public async Task Update_MatchesPricesByCategoryAndReplacesVariantsAndImages()
    {
        // WearModel::updatePrices: a listed category's price is changed or added, an unlisted one removed
        using var db = CreateDbContext();
        await SeedAsync(db);
        db.WearImages.Add(
            new WearImage
            {
                Id = 7,
                FileName = "shirt.png",
                ContentType = "image/png",
                Data = [1],
            }
        );
        await db.SaveChangesAsync(Ct);

        var updated = Value(
            await Types(db)
                .Update(
                    1,
                    new UpdateWearTypeDto
                    {
                        NameDanish = "T-shirt 2027",
                        Prices =
                        [
                            new WearPriceFieldsDto(ParticipantCategory.ParticipantId, 175),
                            new WearPriceFieldsDto(3, 50),
                        ],
                        Variants =
                        [
                            [Herre, Sort, M],
                            [Herre, Hvid, L],
                        ],
                        ImageLinks = [new WearImageLinkDto(7, null), new WearImageLinkDto(7, Sort)],
                    }
                )
        );

        Assert.Equal("T-shirt 2027", updated.NameDanish);
        Assert.Equal(
            [(1, ParticipantCategory.ParticipantId, 175), (updated.Prices[1].Id, 3, 50)],
            updated.Prices.Select(p => (p.Id, p.ParticipantCategoryId, p.Price))
        );
        Assert.Equal(
            ["Herre,Sort,M", "Herre,Hvid,L"],
            updated.Variants.Select(v => string.Join(",", v.Select(a => a.NameDanish)))
        );
        Assert.Equal([new WearImageLinkDto(7, null), new WearImageLinkDto(7, Sort)], updated.ImageLinks);
    }

    [Fact]
    public async Task Update_RefusesToRemoveAnOrderedPrice()
    {
        // Infosys: the foreign key from deltagere_wear_order gave an error page (fixed 2026-11-26). An annulled
        // participant's order counts.
        using var db = CreateDbContext();
        await SeedAsync(db);
        await AddOrderAsync(db, 1, 3, 2, 1, false, Herre, Sort, S);

        var result = (
            await Types(db)
                .Update(
                    1,
                    new UpdateWearTypeDto
                    {
                        NameDanish = "T-shirt",
                        Prices = [new WearPriceFieldsDto(ParticipantCategory.ParticipantId, 150)],
                    }
                )
        ).Result!;

        Assert.IsType<ConflictObjectResult>(result);
        Assert.Equal("priceOrdered", Field(result, "reason"));
        Assert.Equal("Arrangør", Field(result, "categoryName"));
        Assert.Equal(2, await db.WearPrices.CountAsync(p => p.WearTypeId == 1, Ct));
    }

    [Fact]
    public async Task Delete_IsRefusedWhileTheTypeHasOrders()
    {
        // Infosys: "Denne wear type har allerede N bestillinger"
        using var db = CreateDbContext();
        await SeedAsync(db);
        await AddOrderAsync(db, 1, 3, 1, 1, false, Herre, Sort, S);
        await AddOrderAsync(db, 2, 1, 2, 1, false, Dame, Sort, M);

        var refused = await Types(db).Delete(1);

        Assert.IsType<ConflictObjectResult>(refused);
        Assert.Equal("inUse", Field(refused, "reason"));
        Assert.Equal(2, Field(refused, "orderCount"));

        Assert.IsType<OkObjectResult>(await Types(db).Delete(2));
        Assert.False(await db.WearTypes.AnyAsync(w => w.Id == 2, Ct));
        Assert.False(await db.WearPrices.AnyAsync(p => p.WearTypeId == 2, Ct));
    }

    [Fact]
    public async Task Move_SwapsTheTypeWithItsNeighbour()
    {
        // Infosys switch_row; at the top nothing happens
        using var db = CreateDbContext();
        await SeedAsync(db);
        var types = Types(db);
        string Order() =>
            string.Join(",", db.WearTypes.AsNoTracking().OrderBy(w => w.Position).ThenBy(w => w.Id).Select(w => w.Id));

        Assert.IsType<NoContentResult>(await types.Move(1, new MoveWearTypeDto(MoveDirection.Up)));
        Assert.Equal("1,2", Order());
        Assert.IsType<NoContentResult>(await types.Move(1, new MoveWearTypeDto(MoveDirection.Up)));
        Assert.Equal("1,2", Order());
        Assert.IsType<NoContentResult>(await types.Move(1, new MoveWearTypeDto(MoveDirection.Down)));
        Assert.Equal("2,1", Order());
        Assert.IsType<BadRequestObjectResult>(await types.Move(1, new MoveWearTypeDto(null)));
        Assert.IsType<NotFoundObjectResult>(await types.Move(99, new MoveWearTypeDto(MoveDirection.Up)));
    }

    [Fact]
    public async Task Move_ChangesPlacesEvenWithTheSamePosition()
    {
        using var db = CreateDbContext();
        await SeedAsync(db);
        foreach (var wear in db.WearTypes)
            wear.Position = 5;
        await db.SaveChangesAsync(Ct);

        // By position and then id, 1 comes first
        Assert.IsType<NoContentResult>(await Types(db).Move(1, new MoveWearTypeDto(MoveDirection.Down)));

        Assert.Equal([2, 1], db.WearTypes.AsNoTracking().OrderBy(w => w.Position).ThenBy(w => w.Id).Select(w => w.Id));
    }

    // ---- Attributes ----

    [Fact]
    public async Task Attributes_AreListedByKindAndPosition_AndNewOnesGoLastInTheirKind()
    {
        // WearModel::getAttributes and setAttribute (MAX(position) + 1 within the kind)
        using var db = CreateDbContext();
        await SeedAsync(db);

        var created = Value(
            await Attributes(db)
                .Create(
                    new CreateWearAttributeDto
                    {
                        Kind = WearAttributeKind.Model,
                        NameDanish = "Barn",
                        NameEnglish = "Kid",
                    }
                )
        );
        var all = Value(await Attributes(db).GetAll()).ToList();

        Assert.Equal(new WearAttributeDto(created.Id, WearAttributeKind.Model, "Barn", "Kid", 3), created);
        Assert.True(created.Id >= WearAttribute.FirstNewId);
        Assert.Equal(
            ["Herre", "Dame", "Barn", "Sort", "Hvid", "Baby 56(0-1 md)"],
            all.Take(6).Select(a => a.NameDanish)
        );
        Assert.Equal(19 + 5, all.Count);
    }

    [Fact]
    public async Task Attributes_UpdateChangesTheNames()
    {
        using var db = CreateDbContext();
        await SeedAsync(db);

        var updated = Value(
            await Attributes(db)
                .Update(Sort, new UpdateWearAttributeDto { NameDanish = "Sort (ny)", NameEnglish = "Black" })
        );

        Assert.Equal(new WearAttributeDto(Sort, WearAttributeKind.Color, "Sort (ny)", "Black", 1), updated);
        Assert.IsType<NotFoundObjectResult>((await Attributes(db).Update(999, new UpdateWearAttributeDto())).Result);
        Assert.IsType<BadRequestObjectResult>(
            (await Attributes(db).Update(Sort, new UpdateWearAttributeDto { NameDanish = new string('x', 65) })).Result
        );
        Assert.IsType<BadRequestObjectResult>(
            (await Attributes(db).Create(new CreateWearAttributeDto { NameDanish = "X" })).Result
        );
    }

    // ---- Images ----

    // The smallest PNG ImageFiles accepts: the signature and an IHDR chunk of 1 x 1 pixels
    private static readonly byte[] Png =
    [
        0x89,
        0x50,
        0x4E,
        0x47,
        0x0D,
        0x0A,
        0x1A,
        0x0A,
        0,
        0,
        0,
        0x0D,
        0x49,
        0x48,
        0x44,
        0x52,
        0,
        0,
        0,
        1,
        0,
        0,
        0,
        1,
    ];

    private static string DataUrl(byte[] data) => "data:image/png;base64," + Convert.ToBase64String(data);

    [Fact]
    public async Task Images_AreStoredByTheirBytes_AndAFileNameOnlyOnce()
    {
        // Infosys uploadImage: "Infosys kunne ikke genkende filen som et billede", "Der findes allerede en fil med samme navn"
        using var db = CreateDbContext();
        var images = Images(db);

        var created = Value(await images.Create(new CreateWearImageDto("C:\\fakepath\\shirt.png", DataUrl(Png))));
        var notImage = (
            await images.Create(new CreateWearImageDto("evil.php", DataUrl("GIF0<?php"u8.ToArray())))
        ).Result!;
        var taken = (await images.Create(new CreateWearImageDto("shirt.png", DataUrl(Png)))).Result!;
        Value(await images.Create(new CreateWearImageDto("cap.png", DataUrl(Png))));

        Assert.Equal("shirt.png", created.FileName);
        Assert.Equal("notImage", Field(notImage, "reason"));
        Assert.IsType<ConflictObjectResult>(taken);
        Assert.Equal("nameTaken", Field(taken, "reason"));
        Assert.Equal(["cap.png", "shirt.png"], Value(await images.GetAll()).Select(i => i.FileName));
        var file = Assert.IsType<FileContentResult>(await images.GetFile(created.Id));
        Assert.Equal("image/png", file.ContentType);
        Assert.Equal(Png, file.FileContents);
    }

    // ---- Order lists ----

    [Fact]
    public async Task Breakdown_LeavesOutAnnulledParticipantsAndListsEverySize()
    {
        using var db = CreateDbContext();
        await SeedAsync(db);
        await AddOrderAsync(db, 1, 1, 1, 2, false, Herre, Sort, S);
        await AddOrderAsync(db, 2, 3, 1, 5, false, Herre, Sort, S);
        await AddOrderAsync(db, 3, 2, 3, 1, true);

        var breakdown = Value(await Orders(db).GetBreakdown());

        Assert.Equal(19, breakdown.Sizes.Count);
        Assert.Equal("Baby 56(0-1 md)", breakdown.Sizes[0].NameDanish);
        var cap = breakdown.Rows[0];
        Assert.Equal((2, "Kasket", 1, 1), (cap.WearTypeId, cap.WearTypeName, cap.Total, cap.NoSize));
        var herreSort = breakdown.Rows[1];
        Assert.Equal(["Herre", "Sort"], herreSort.Attributes.Select(a => a.NameDanish));
        Assert.Equal(2, herreSort.Total);
        Assert.Equal([new WearSizeCountDto(S, 2)], herreSort.SizeCounts);
    }

    [Fact]
    public async Task OrderList_ListsParticipantsWithAllTheirOrders_UnfilledOnlyThoseWithOneNotHandedOut()
    {
        // Infosys wear/detailed and wear/unfilled; annulled participants are left out (agreed 2026-11-26)
        using var db = CreateDbContext();
        await SeedAsync(db);
        (await db.Participants.SingleAsync(p => p.Id == 2, Ct)).Nickname = "Kaptajnen";
        await db.SaveChangesAsync(Ct);
        db.ChangeTracker.Clear();
        await AddOrderAsync(db, 1, 2, 2, 1, true, Dame, Sort, M);
        await AddOrderAsync(db, 2, 1, 1, 2, true, Herre, Sort, S);
        await AddOrderAsync(db, 3, 1, 3, 1, false);
        await AddOrderAsync(db, 4, 3, 1, 1, false, Herre, Hvid, L);

        var all = Value(await Orders(db).GetAll()).ToList();
        var unfilled = Value(await Orders(db).GetAll(unfilled: true)).ToList();

        Assert.Equal(
            [
                (1, "Deltager 1", "Deltager", "T-shirt-Herre-Sort,Kasket"),
                (2, "Kaptajnen", "Arrangør", "T-shirt-Dame-Sort"),
            ],
            all.Select(p => (p.ParticipantId, p.Name, p.CategoryName, string.Join(",", p.Orders.Select(o => o.Name))))
        );
        Assert.Equal(new WearOrderLineDto(2, "T-shirt-Herre-Sort", "Deltager", 2, "S", true), all[0].Orders[0]);
        // Participant 1 has an order not handed out, and all of their orders are shown
        Assert.Equal([1], unfilled.Select(p => p.ParticipantId));
        Assert.Equal(2, unfilled[0].Orders.Count);
    }

    [Fact]
    public async Task MiniList_ShowsTheOrdersWithTheAttributes_AndNoSizeOnlyThoseWithoutASize()
    {
        // Infosys wear/detailed/:id:; its size=none link showed every order of the type (fixed 2026-11-26)
        using var db = CreateDbContext();
        await SeedAsync(db);
        await AddOrderAsync(db, 1, 1, 1, 2, false, Herre, Sort, S);
        await AddOrderAsync(db, 2, 2, 2, 1, false, Herre, Sort);
        await AddOrderAsync(db, 3, 1, 1, 1, false, Dame, Sort, S);
        await AddOrderAsync(db, 4, 3, 1, 1, false, Herre, Sort, S);

        var bySize = Value(await Orders(db).GetByType(1, [Herre, Sort, S]));
        var noSize = Value(await Orders(db).GetByType(1, [Herre, Sort], noSize: true));

        Assert.Equal("T-shirt", bySize.WearTypeName);
        Assert.Equal(["Herre", "Sort", "S"], bySize.Attributes.Select(a => a.NameDanish));
        Assert.Equal([1], bySize.Orders.Select(o => o.Order.Id));
        Assert.Equal(
            new WearMiniListOrderDto(
                1,
                "Deltager 1",
                new WearOrderLineDto(1, "T-shirt-Herre-Sort", "Deltager", 2, "S", false)
            ),
            bySize.Orders[0]
        );
        Assert.Equal([2], noSize.Orders.Select(o => o.Order.Id));
        Assert.IsType<BadRequestObjectResult>((await Orders(db).GetByType(1, [999])).Result);
        Assert.IsType<NotFoundObjectResult>((await Orders(db).GetByType(99, null)).Result);
    }

    [Fact]
    public async Task UpdateReceived_MarksTheOrderItself()
    {
        // Infosys flipped the first order with the same participant and price, so with two sizes of the same T-shirt it
        // could change the wrong one (fixed 2026-11-26)
        using var db = CreateDbContext();
        await SeedAsync(db);
        await AddOrderAsync(db, 1, 1, 1, 1, false, Herre, Sort, S);
        await AddOrderAsync(db, 2, 1, 1, 1, false, Herre, Sort, M);

        Assert.IsType<NoContentResult>(await Orders(db).UpdateReceived(2, new UpdateWearOrderReceivedDto(true)));

        Assert.Equal(
            [false, true],
            db.ParticipantWearOrders.AsNoTracking().OrderBy(o => o.Id).Select(o => o.IsReceived)
        );
        Assert.IsType<BadRequestObjectResult>(await Orders(db).UpdateReceived(2, new UpdateWearOrderReceivedDto(null)));
        Assert.IsType<NotFoundObjectResult>(await Orders(db).UpdateReceived(99, new UpdateWearOrderReceivedDto(true)));
    }

    // ---- Wear-udlevering ----

    [Fact]
    public async Task HandOut_MarksAllTheParticipantsWear()
    {
        // WearModel::markWearReceived: the lines show the participant's category
        using var db = CreateDbContext();
        await SeedAsync(db);
        await AddOrderAsync(db, 1, 1, 1, 2, false, Herre, Sort, S);
        await AddOrderAsync(db, 2, 1, 3, 1, false);

        var result = Value(await Handout(db).HandOut(new WearHandoutInputDto("1")));

        Assert.Equal(1, result.ParticipantId);
        Assert.Equal("Deltager 1", result.Name);
        Assert.Equal("Deltager", result.CategoryName);
        Assert.Equal(
            [new WearHandoutItemDto("T-shirt-Herre-Sort", 2, "S"), new WearHandoutItemDto("Kasket", 1, null)],
            result.Items
        );
        Assert.All(db.ParticipantWearOrders.AsNoTracking(), o => Assert.True(o.IsReceived));
    }

    [Fact]
    public async Task HandOut_TakesAScannedBadge()
    {
        // EANToNumber: the EAN-8 on the ID card
        using var db = CreateDbContext();
        await SeedAsync(db);
        await AddOrderAsync(db, 1, 2, 3, 1, false);
        var year = TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTime.UtcNow, "Europe/Copenhagen").Year;

        var result = Value(
            await Handout(db).HandOut(new WearHandoutInputDto(ParticipantRules.Ean8ForParticipant(2, year)))
        );

        Assert.Equal(2, result.ParticipantId);
        Assert.Equal(2, WearHandoutController.ParticipantIdOf("00002", year));
        Assert.Null(WearHandoutController.ParticipantIdOf("abc", year));
    }

    [Fact]
    public async Task HandOut_RefusesWithoutChangingAnything()
    {
        // Infosys stopped at the first order already handed out and kept the ones before it changed (fixed
        // 2026-11-26). It meant to refuse a participant who isn't checked in; that is enforced (agreed 2026-11-26).
        using var db = CreateDbContext();
        await SeedAsync(db);
        await AddOrderAsync(db, 1, 1, 1, 1, false, Herre, Sort, S);
        await AddOrderAsync(db, 2, 1, 3, 1, true);
        await AddOrderAsync(db, 3, 3, 3, 1, false);
        var participant2 = await db.Participants.SingleAsync(p => p.Id == 2, Ct);
        participant2.IsNoShow = true;
        await db.SaveChangesAsync(Ct);
        db.ChangeTracker.Clear();

        async Task<string?> Reason(string? input) =>
            Field((await Handout(db).HandOut(new WearHandoutInputDto(input))).Result!, "reason") as string;

        Assert.Equal("alreadyReceived", await Reason("1"));
        Assert.False((await db.ParticipantWearOrders.AsNoTracking().SingleAsync(o => o.Id == 1, Ct)).IsReceived);
        Assert.Equal("noShow", await Reason("2"));
        Assert.Equal("notCheckedIn", await Reason("3"));
        Assert.Equal("notFound", await Reason("99"));
        Assert.Equal("noInput", await Reason(""));
        Assert.Equal("noInput", await Reason("0"));
    }

    [Fact]
    public async Task HandOut_RefusesAParticipantWithoutWear()
    {
        using var db = CreateDbContext();
        await SeedAsync(db);

        var result = (await Handout(db).HandOut(new WearHandoutInputDto("1"))).Result!;

        Assert.Equal("noWear", Field(result, "reason"));
        Assert.Equal(1, Field(result, "participantId"));
    }

    [Fact]
    public async Task Undo_TakesBackAllTheWear_OnlyWhenAllIsHandedOut()
    {
        // WearModel::undoReceiveWear doesn't check the check-in
        using var db = CreateDbContext();
        await SeedAsync(db);
        await AddOrderAsync(db, 1, 1, 1, 1, true, Herre, Sort, S);
        await AddOrderAsync(db, 2, 1, 3, 1, true);
        await AddOrderAsync(db, 3, 3, 3, 1, true);
        await AddOrderAsync(db, 4, 3, 1, 1, false, Herre, Sort, M);

        var undone = Value(await Handout(db).Undo(new WearHandoutInputDto("1")));
        var refused = (await Handout(db).Undo(new WearHandoutInputDto("3"))).Result!;

        Assert.Equal(2, undone.Items.Count);
        Assert.Equal(
            [false, false],
            db.ParticipantWearOrders.AsNoTracking().Where(o => o.ParticipantId == 1).Select(o => o.IsReceived)
        );
        Assert.Equal("notReceived", Field(refused, "reason"));
        Assert.True((await db.ParticipantWearOrders.AsNoTracking().SingleAsync(o => o.Id == 3, Ct)).IsReceived);
    }

    // ---- The participant's wear ----

    [Fact]
    public async Task ParticipantWear_GetListsTheOrdersAndTheTypesByPosition()
    {
        using var db = CreateDbContext();
        await SeedAsync(db);
        await AddOrderAsync(db, 1, 1, 2, 3, true, Herre, Sort, S);

        var wear = Value(await ParticipantWear(db).Get(1));

        Assert.Equal(
            [
                new ParticipantWearOrderRowDto(
                    1,
                    1,
                    2,
                    wear.Orders[0].AttributeIds,
                    "T-shirt-Herre-Sort",
                    "S",
                    "Arrangør",
                    100,
                    3,
                    true
                ),
            ],
            wear.Orders
        );
        Assert.Equal([S, Herre, Sort], wear.Orders[0].AttributeIds.Order());
        Assert.Equal(["Kasket", "T-shirt"], wear.WearTypes.Select(w => w.NameDanish));
        Assert.Equal(
            [new WearPriceOptionDto(1, "Deltager", 150), new WearPriceOptionDto(2, "Arrangør", 100)],
            wear.WearTypes[1].Prices
        );
        Assert.Equal(2, wear.WearTypes[1].Variants.Count);
        Assert.IsType<NotFoundObjectResult>((await ParticipantWear(db).Get(99)).Result);
    }

    [Fact]
    public async Task ParticipantWear_UpdateKeepsUdleveretOnKeptOrders()
    {
        // Infosys deleted every order and added the posted ones back, so "Udleveret" was reset (fixed 2026-11-26)
        using var db = CreateDbContext();
        await SeedAsync(db);
        await AddOrderAsync(db, 1, 1, 1, 1, true, Herre, Sort, S);
        await AddOrderAsync(db, 2, 1, 3, 1, true);

        var result = await ParticipantWear(db)
            .Update(
                1,
                new UpdateParticipantWearDto([
                    new ParticipantWearOrderFieldsDto(1, null, null, 3),
                    new ParticipantWearOrderFieldsDto(null, 1, [Dame, Sort, M], 2),
                ])
            );

        Assert.IsType<NoContentResult>(result);
        var orders = await db
            .ParticipantWearOrders.AsNoTracking()
            .Include(o => o.Attributes)
            .OrderBy(o => o.Id)
            .ToListAsync(Ct);
        Assert.Equal([(1, 3, true), (orders[1].Id, 2, false)], orders.Select(o => (o.Id, o.Amount, o.IsReceived)));
        Assert.Equal([M, Dame, Sort], orders[1].Attributes.Select(a => a.AttributeId).Order());
    }

    [Theory]
    [InlineData(null, 1, new[] { Herre, Sort, S }, 0, "The amount must be a whole number of 1 or more")]
    [InlineData(
        null,
        1,
        new[] { Herre, Hvid, S },
        1,
        "The attributes aren't a choice in one of the wear type's variants"
    )]
    [InlineData(null, 3, new[] { S }, 1, "The attributes aren't a choice in one of the wear type's variants")]
    [InlineData(null, 99, new int[0], 1, "Unknown wear price")]
    [InlineData(null, 1, new[] { Herre, Sort, 999 }, 1, "Unknown wear attribute")]
    [InlineData(5, null, null, 1, "Unknown wear order")]
    public async Task ParticipantWear_UpdateRefusesInvalidOrders(
        int? id,
        int? priceId,
        int[]? attributeIds,
        int amount,
        string error
    )
    {
        // Infosys saved any amount and attributes it was sent (fixed 2026-11-26)
        using var db = CreateDbContext();
        await SeedAsync(db);
        await AddOrderAsync(db, 1, 1, 3, 1, true);
        await AddOrderAsync(db, 5, 2, 3, 1, true);

        var result = await ParticipantWear(db)
            .Update(
                1,
                new UpdateParticipantWearDto([new ParticipantWearOrderFieldsDto(id, priceId, attributeIds, amount)])
            );

        Assert.Equal(error, Field(Assert.IsType<BadRequestObjectResult>(result), "message"));
        Assert.Equal(2, await db.ParticipantWearOrders.CountAsync(Ct));
    }

    [Fact]
    public async Task ParticipantWear_UpdateRemovesUnlistedOrdersAndRefusesAnOrderListedTwice()
    {
        using var db = CreateDbContext();
        await SeedAsync(db);
        await AddOrderAsync(db, 1, 1, 3, 1, false);
        await AddOrderAsync(db, 2, 1, 1, 1, false, Herre, Sort, S);

        var twice = await ParticipantWear(db)
            .Update(
                1,
                new UpdateParticipantWearDto([
                    new ParticipantWearOrderFieldsDto(1, null, null, 1),
                    new ParticipantWearOrderFieldsDto(1, null, null, 2),
                ])
            );
        Assert.IsType<BadRequestObjectResult>(twice);

        Assert.IsType<NoContentResult>(
            await ParticipantWear(db)
                .Update(1, new UpdateParticipantWearDto([new ParticipantWearOrderFieldsDto(2, null, null, 1)]))
        );
        Assert.Equal([2], db.ParticipantWearOrders.AsNoTracking().Select(o => o.Id));
        Assert.IsType<NoContentResult>(await ParticipantWear(db).Update(1, new UpdateParticipantWearDto(null)));
        Assert.Empty(db.ParticipantWearOrders);
        Assert.IsType<NotFoundObjectResult>(await ParticipantWear(db).Update(99, new UpdateParticipantWearDto([])));
    }

    // ---- Authorization ----

    private async Task<HttpClient> ClientAsync(string? privilege)
    {
        if (privilege == null)
            return factory.CreateClient();
        var username = $"wear-{Guid.NewGuid():N}";
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
    [InlineData(
        null,
        HttpStatusCode.Unauthorized,
        HttpStatusCode.Unauthorized,
        HttpStatusCode.Unauthorized,
        HttpStatusCode.Unauthorized
    )]
    [InlineData(
        "participant_edit",
        HttpStatusCode.Forbidden,
        HttpStatusCode.Forbidden,
        HttpStatusCode.Forbidden,
        HttpStatusCode.Forbidden
    )]
    [InlineData(
        "wear_participant_edit",
        HttpStatusCode.Forbidden,
        HttpStatusCode.Forbidden,
        HttpStatusCode.Forbidden,
        HttpStatusCode.Forbidden
    )]
    [InlineData(
        "wear_list_view",
        HttpStatusCode.Forbidden,
        HttpStatusCode.Forbidden,
        HttpStatusCode.Forbidden,
        HttpStatusCode.Forbidden
    )]
    [InlineData("wear_view", HttpStatusCode.OK, HttpStatusCode.OK, HttpStatusCode.Forbidden, HttpStatusCode.Forbidden)]
    [InlineData("wear_edit", HttpStatusCode.OK, HttpStatusCode.OK, HttpStatusCode.Created, HttpStatusCode.OK)]
    public async Task WearTypes_WearViewMayLook_WearEditMayChange(
        string? privilege,
        HttpStatusCode look,
        HttpStatusCode breakdown,
        HttpStatusCode create,
        HttpStatusCode images
    )
    {
        using var client = await ClientAsync(privilege);

        Assert.Equal(look, (await client.GetAsync("/api/wear-types", Ct)).StatusCode);
        Assert.Equal(look, (await client.GetAsync("/api/wear-attributes", Ct)).StatusCode);
        Assert.Equal(breakdown, (await client.GetAsync("/api/wear-orders/breakdown", Ct)).StatusCode);
        Assert.Equal(
            create,
            (
                await client.PostAsJsonAsync("/api/wear-types", new CreateWearTypeDto { NameDanish = "Hue" }, Ct)
            ).StatusCode
        );
        Assert.Equal(images, (await client.GetAsync("/api/wear-images", Ct)).StatusCode);
    }

    [Theory]
    [InlineData(null, HttpStatusCode.Unauthorized, HttpStatusCode.Unauthorized, HttpStatusCode.Unauthorized)]
    [InlineData("wear_view", HttpStatusCode.Forbidden, HttpStatusCode.Forbidden, HttpStatusCode.Forbidden)]
    [InlineData("wear_edit", HttpStatusCode.Forbidden, HttpStatusCode.Forbidden, HttpStatusCode.Forbidden)]
    [InlineData("wear_list_view", HttpStatusCode.OK, HttpStatusCode.Forbidden, HttpStatusCode.Forbidden)]
    [InlineData("wear_distribute_edit", HttpStatusCode.OK, HttpStatusCode.NotFound, HttpStatusCode.NotFound)]
    public async Task OrderLists_NeedTheWearList_HandingOutNeedsWearDistribute(
        string? privilege,
        HttpStatusCode lists,
        HttpStatusCode mark,
        HttpStatusCode handout
    )
    {
        // An unknown order and participant: 404 once the user may hand out wear
        using var client = await ClientAsync(privilege);

        Assert.Equal(lists, (await client.GetAsync("/api/wear-orders?unfilled=true", Ct)).StatusCode);
        Assert.Equal(
            mark,
            (
                await client.PutAsJsonAsync(
                    "/api/wear-orders/987654/received",
                    new UpdateWearOrderReceivedDto(true),
                    Ct
                )
            ).StatusCode
        );
        Assert.Equal(
            handout,
            (await client.PostAsJsonAsync("/api/wear-handout", new WearHandoutInputDto("987654"), Ct)).StatusCode
        );
    }

    [Theory]
    [InlineData(null, HttpStatusCode.Unauthorized)]
    [InlineData("participant_edit", HttpStatusCode.Forbidden)]
    [InlineData("wear_edit", HttpStatusCode.Forbidden)]
    [InlineData("wear_participant_edit", HttpStatusCode.NotFound)]
    public async Task ParticipantWear_NeedsWearParticipantEdit(string? privilege, HttpStatusCode expected)
    {
        // An unknown participant: 404 once the user may change wear at all
        using var client = await ClientAsync(privilege);

        Assert.Equal(expected, (await client.GetAsync("/api/participants/987654/wear", Ct)).StatusCode);
        Assert.Equal(
            expected,
            (
                await client.PutAsJsonAsync("/api/participants/987654/wear", new UpdateParticipantWearDto([]), Ct)
            ).StatusCode
        );
    }
}
