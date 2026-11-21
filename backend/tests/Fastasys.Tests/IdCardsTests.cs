using Fastasys.ApiService.Controllers;
using Fastasys.ApiService.Data;
using Fastasys.ApiService.Data.Entities;
using Fastasys.ApiService.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Fastasys.Tests;

// Pins the Infosys ID cards (IdTemplateController, IdTemplateModel, PhotoController, PhotoModel and
// IdTemplateRenderer's data) as ported on 2026-11-18
public class IdCardsTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static InfosysDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<InfosysDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        var context = new InfosysDbContext(options);
        // Seeds the participant categories and organizer areas
        context.Database.EnsureCreated();
        return context;
    }

    private static IdCardTemplatesController Templates(InfosysDbContext db) =>
        new(db, NullLogger<IdCardTemplatesController>.Instance);

    private static ParticipantPhotoController Photos(InfosysDbContext db) =>
        new(db, NullLogger<ParticipantPhotoController>.Instance);

    private static IdCardsController Cards(InfosysDbContext db, DateOnly? conventionStart = null) =>
        new(db, Options.Create(new ConventionOptions { Start = conventionStart }));

    private static T Value<T>(ActionResult<T> result) =>
        Assert.IsAssignableFrom<T>(Assert.IsAssignableFrom<ObjectResult>(result.Result).Value);

    private static async Task<IdCardTemplate> AddTemplateAsync(InfosysDbContext db, string name, bool withBackground = true, params IdCardTemplateItem[] items)
    {
        var template = new IdCardTemplate
        {
            Name = name,
            Background = withBackground ? TestImages.Png(1011, 638) : null,
            BackgroundContentType = withBackground ? "image/png" : null,
            Width = withBackground ? 1011 : 0,
            Height = withBackground ? 638 : 0,
            Items = items.ToList()
        };
        db.IdCardTemplates.Add(template);
        await db.SaveChangesAsync(Ct);
        db.ChangeTracker.Clear();
        return template;
    }

    private static async Task AddParticipantAsync(InfosysDbContext db, int id, Action<Participant>? setup = null)
    {
        var participant = new Participant { Id = id, FirstName = "Testa", LastName = "Testesen" };
        setup?.Invoke(participant);
        db.Participants.Add(participant);
        await db.SaveChangesAsync(Ct);
        db.ChangeTracker.Clear();
    }

    private static IdCardTemplateItemDto Text(IdCardDataSource source, int x = 10, int y = 20) =>
        new(IdCardItemType.Text, x, y, 200, 50, 0, source);

    // --- Templates (the "ID skabeloner" page) ---

    [Fact]
    public async Task Create_MakesAnEmptyTemplateWithTheTrimmedName()
    {
        using var db = CreateDbContext();

        var created = Value(await Templates(db).Create(new CreateIdCardTemplateDto("  Arrangør27  ")));

        Assert.Equal(("Arrangør27", 0, 0, false), (created.Name, created.Width, created.Height, created.HasBackground));
        Assert.Empty(created.Items);
        Assert.Equal("Arrangør27", Value(await Templates(db).Get(created.Id)).Name);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("   ")]
    public async Task Create_NeedsAName(string? name)
    {
        // Infosys's page sends nothing without a name, and its endpoint refuses an empty one
        using var db = CreateDbContext();

        Assert.IsType<BadRequestObjectResult>((await Templates(db).Create(new CreateIdCardTemplateDto(name))).Result);
    }

    [Fact]
    public async Task Update_ReplacesTheNameAndAllItems()
    {
        using var db = CreateDbContext();
        var template = await AddTemplateAsync(db, "Gammel", true,
            new IdCardTemplateItem { Type = IdCardItemType.Barcode, X = 1, Y = 1, Width = 200, Height = 100 });
        var items = new[]
        {
            new IdCardTemplateItemDto(IdCardItemType.Photo, 30, 40, 213, 295, 0, IdCardDataSource.Name),
            Text(IdCardDataSource.Pronouns),
        };

        var updated = Value(await Templates(db).Update(template.Id, new UpdateIdCardTemplateDto("Ny", items)));

        // A photo doesn't keep a data source (Infosys stores '')
        Assert.Equal("Ny", updated.Name);
        Assert.Equal(
            new[]
            {
                new IdCardTemplateItemDto(IdCardItemType.Photo, 30, 40, 213, 295, 0, null),
                Text(IdCardDataSource.Pronouns),
            },
            Value(await Templates(db).Get(template.Id)).Items);
        Assert.Equal(2, await db.IdCardTemplateItems.CountAsync(Ct));
    }

    [Theory]
    [InlineData(IdCardItemType.Text, 0, 0, 0, null)]
    [InlineData(IdCardItemType.Photo, -1, 0, 0, null)]
    [InlineData(IdCardItemType.Photo, 0, -5, 0, null)]
    [InlineData(IdCardItemType.Barcode, 0, 0, 360, null)]
    [InlineData(IdCardItemType.Barcode, 0, 0, -1, null)]
    public async Task Update_RefusesItemsTheEditorCantMake(IdCardItemType type, int x, int width, int rotation, IdCardDataSource? source)
    {
        using var db = CreateDbContext();
        var template = await AddTemplateAsync(db, "Skabelon");
        var item = new IdCardTemplateItemDto(type, x, 0, width, 10, rotation, source);

        var result = await Templates(db).Update(template.Id, new UpdateIdCardTemplateDto("Skabelon", [item]));

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task UpdateBackground_SetsTheCardsSizeFromTheImage()
    {
        using var db = CreateDbContext();
        var template = await AddTemplateAsync(db, "Skabelon", withBackground: false);

        var updated = Value(await Templates(db).UpdateBackground(template.Id, new IdCardBackgroundDto(TestImages.DataUrl(TestImages.Jpeg(1200, 800), "image/jpeg"))));

        Assert.Equal((1200, 800, true), (updated.Width, updated.Height, updated.HasBackground));
        var background = Assert.IsType<FileContentResult>(await Templates(db).GetBackground(template.Id));
        Assert.Equal("image/jpeg", background.ContentType);
        Assert.Equal(TestImages.Jpeg(1200, 800), background.FileContents);
    }

    [Fact]
    public async Task UpdateBackground_RefusesWhatIsntAnImage()
    {
        using var db = CreateDbContext();
        var template = await AddTemplateAsync(db, "Skabelon", withBackground: false);
        var svg = TestImages.DataUrl("<svg/>"u8.ToArray(), "image/svg+xml");

        Assert.IsType<BadRequestObjectResult>((await Templates(db).UpdateBackground(template.Id, new IdCardBackgroundDto(svg))).Result);
        Assert.IsType<NotFoundObjectResult>(await Templates(db).GetBackground(template.Id));
    }

    [Fact]
    public async Task Copy_MakesCopyOfWithItsOwnBackgroundAndItems()
    {
        // Infosys idtemplate.js copyTemplateHandler: "Copy of " + the name, the same background and items
        using var db = CreateDbContext();
        var original = await AddTemplateAsync(db, "Arrangør27", true,
            new IdCardTemplateItem { Type = IdCardItemType.Text, X = 5, Y = 6, Width = 7, Height = 8, Rotation = 90, DataSource = IdCardDataSource.Name });

        var copy = Value(await Templates(db).Copy(original.Id));

        Assert.NotEqual(original.Id, copy.Id);
        Assert.Equal("Copy of Arrangør27", copy.Name);
        Assert.Equal((1011, 638, true), (copy.Width, copy.Height, copy.HasBackground));
        Assert.Equal([new IdCardTemplateItemDto(IdCardItemType.Text, 5, 6, 7, 8, 90, IdCardDataSource.Name)], copy.Items);
        // The copy's background is its own: a new one on the original doesn't change it
        await Templates(db).UpdateBackground(original.Id, new IdCardBackgroundDto(TestImages.DataUrl(TestImages.Gif(50, 50))));
        Assert.Equal(1011, Value(await Templates(db).Get(copy.Id)).Width);
    }

    [Fact]
    public async Task Delete_LetsCategoriesAndParticipantsFallBack()
    {
        // Infosys's foreign keys cascade: the category has no template, the participant gets the category's
        using var db = CreateDbContext();
        var doomed = await AddTemplateAsync(db, "Væk");
        var kept = await AddTemplateAsync(db, "Bliver");
        var category = await db.ParticipantCategories.SingleAsync(c => c.Id == ParticipantCategory.OrganizerId, Ct);
        category.IdCardTemplateId = doomed.Id;
        await db.SaveChangesAsync(Ct);
        await AddParticipantAsync(db, 1, p => p.IdCardTemplateId = doomed.Id);
        await AddParticipantAsync(db, 2, p => p.IdCardTemplateId = kept.Id);

        Assert.IsType<NoContentResult>(await Templates(db).Delete(doomed.Id));

        db.ChangeTracker.Clear();
        Assert.Null((await db.ParticipantCategories.SingleAsync(c => c.Id == ParticipantCategory.OrganizerId, Ct)).IdCardTemplateId);
        Assert.Null((await db.Participants.SingleAsync(p => p.Id == 1, Ct)).IdCardTemplateId);
        Assert.Equal(kept.Id, (await db.Participants.SingleAsync(p => p.Id == 2, Ct)).IdCardTemplateId);
        Assert.Equal(["Bliver"], Value(await Templates(db).GetAll()).Select(t => t.Name));
    }

    [Fact]
    public async Task Categories_GetAndClearTheirTemplate()
    {
        // Infosys IdTemplateModel::updateCategoryTemplate: the empty choice removes the category's template
        using var db = CreateDbContext();
        var template = await AddTemplateAsync(db, "Arrangør27");
        var controller = Templates(db);

        Assert.IsType<NoContentResult>(await controller.UpdateCategory(ParticipantCategory.OrganizerId, new UpdateCategoryIdCardTemplateDto(template.Id)));
        var categories = Value(await controller.GetCategories());
        Assert.Equal(template.Id, categories.Single(c => c.CategoryId == ParticipantCategory.OrganizerId).TemplateId);
        Assert.Equal([1, 2, 3, 4, 5], categories.Select(c => c.CategoryId));

        Assert.IsType<NoContentResult>(await controller.UpdateCategory(ParticipantCategory.OrganizerId, new UpdateCategoryIdCardTemplateDto(0)));
        Assert.Null(Value(await controller.GetCategories()).Single(c => c.CategoryId == ParticipantCategory.OrganizerId).TemplateId);

        Assert.IsType<BadRequestObjectResult>(await controller.UpdateCategory(ParticipantCategory.OrganizerId, new UpdateCategoryIdCardTemplateDto(999)));
        Assert.IsType<NotFoundObjectResult>(await controller.UpdateCategory(99, new UpdateCategoryIdCardTemplateDto(null)));
    }

    // --- Photos ---

    [Fact]
    public async Task Photo_IsStoredAndFetchedBehindTheApi()
    {
        using var db = CreateDbContext();
        await AddParticipantAsync(db, 7);
        var original = TestImages.Jpeg(3000, 4000);
        var cropped = TestImages.Png(213, 295);

        var result = await Photos(db).Update(7, new UpdateParticipantPhotoDto(TestImages.DataUrl(original, "image/jpeg"), TestImages.DataUrl(cropped)));

        Assert.IsType<NoContentResult>(result);
        var croppedFile = Assert.IsType<FileContentResult>(await Photos(db).GetCropped(7));
        Assert.Equal("image/png", croppedFile.ContentType);
        Assert.Equal(cropped, croppedFile.FileContents);
        var originalFile = Assert.IsType<FileContentResult>(await Photos(db).GetOriginal(7));
        Assert.Equal("image/jpeg", originalFile.ContentType);
        Assert.Equal(original, originalFile.FileContents);
    }

    [Fact]
    public async Task Photo_ReplacesTheEarlierOne()
    {
        using var db = CreateDbContext();
        await AddParticipantAsync(db, 7);
        await Photos(db).Update(7, new UpdateParticipantPhotoDto(TestImages.DataUrl(TestImages.Png(1, 1)), TestImages.DataUrl(TestImages.Png(1, 1))));

        await Photos(db).Update(7, new UpdateParticipantPhotoDto(TestImages.DataUrl(TestImages.Gif(2, 2)), TestImages.DataUrl(TestImages.Gif(2, 2))));

        Assert.Equal(1, await db.ParticipantPhotos.CountAsync(Ct));
        Assert.Equal("image/gif", Assert.IsType<FileContentResult>(await Photos(db).GetCropped(7)).ContentType);
    }

    [Fact]
    public async Task Photo_MustBeTwoImagesForAKnownParticipant()
    {
        using var db = CreateDbContext();
        await AddParticipantAsync(db, 7);
        var png = TestImages.DataUrl(TestImages.Png(1, 1));

        Assert.IsType<BadRequestObjectResult>(await Photos(db).Update(7, new UpdateParticipantPhotoDto(png, null)));
        Assert.IsType<BadRequestObjectResult>(await Photos(db).Update(7, new UpdateParticipantPhotoDto("data:text/plain;base64,aGVq", png)));
        Assert.IsType<NotFoundObjectResult>(await Photos(db).Update(8, new UpdateParticipantPhotoDto(png, png)));
        Assert.IsType<NotFoundObjectResult>(await Photos(db).GetCropped(7));
    }

    // --- The cards (Infosys id-card/render?ids=...) ---

    [Fact]
    public async Task Cards_UseTheParticipantsOwnTemplate_OrElseTheirCategorys()
    {
        using var db = CreateDbContext();
        var categoryTemplate = await AddTemplateAsync(db, "Kategori");
        var ownTemplate = await AddTemplateAsync(db, "Egen");
        var category = await db.ParticipantCategories.SingleAsync(c => c.Id == ParticipantCategory.OrganizerId, Ct);
        category.IdCardTemplateId = categoryTemplate.Id;
        await db.SaveChangesAsync(Ct);
        await AddParticipantAsync(db, 1, p => p.CategoryId = ParticipantCategory.OrganizerId);
        await AddParticipantAsync(db, 2, p => { p.CategoryId = ParticipantCategory.OrganizerId; p.IdCardTemplateId = ownTemplate.Id; });
        // No template of their own and none for their category
        await AddParticipantAsync(db, 3, p => p.CategoryId = ParticipantCategory.ParticipantId);

        var cards = Value(await Cards(db).Get("2-1-3-99"));

        Assert.Equal([(2, ownTemplate.Id), (1, categoryTemplate.Id)], cards.Cards.Select(c => (c.ParticipantId, c.TemplateId)));
        Assert.Equal([categoryTemplate.Id, ownTemplate.Id], cards.Templates.Select(t => t.Id));
    }

    [Fact]
    public async Task Cards_ReadTheIdsLikePhpIntval_AndKeepRepeats()
    {
        using var db = CreateDbContext();
        var template = await AddTemplateAsync(db, "Egen");
        await AddParticipantAsync(db, 12, p => p.IdCardTemplateId = template.Id);

        var cards = Value(await Cards(db).Get("12abc-x-12"));

        Assert.Equal([12, 12], cards.Cards.Select(c => c.ParticipantId));
        Assert.Empty(Value(await Cards(db).Get(null)).Cards);
    }

    [Fact]
    public async Task Cards_LeaveOutTemplatesWithoutABackground()
    {
        // Infosys's page failed on such a template
        using var db = CreateDbContext();
        var template = await AddTemplateAsync(db, "Tom", withBackground: false);
        await AddParticipantAsync(db, 1, p => p.IdCardTemplateId = template.Id);

        var cards = Value(await Cards(db).Get("1"));

        Assert.Empty(cards.Cards);
        Assert.Empty(cards.Templates);
    }

    [Fact]
    public async Task Cards_HaveTheTextsTheBarcodeAndWhetherThereIsAPhoto()
    {
        // Infosys IdTemplateRenderer::addTextElement and Deltagere::getEan8Number with the convention's year. Agreed
        // 2026-11-21: "Dr. + Navn", "Scenarie" and "Ungdomsskole" were dropped and the pronouns added.
        using var db = CreateDbContext();
        var template = await AddTemplateAsync(db, "Egen");
        await AddParticipantAsync(db, 42, p =>
        {
            p.IdCardTemplateId = template.Id;
            p.OrganizerAreaId = OrganizerArea.BoardGameDesignerId;
            p.Pronouns = "fsfo";
        });
        await AddParticipantAsync(db, 43, p => { p.IdCardTemplateId = template.Id; p.Nickname = "Testo"; });
        await AddParticipantAsync(db, 44, p =>
        {
            p.IdCardTemplateId = template.Id;
            p.Pronouns = "ask";
            p.PreferredLanguage = PreferredLanguage.English;
        });
        db.ParticipantPhotos.Add(new ParticipantPhoto { ParticipantId = 42, Original = [1], OriginalContentType = "image/png", Cropped = [1], CroppedContentType = "image/png" });
        await db.SaveChangesAsync(Ct);

        var cards = Value(await Cards(db, new DateOnly(2027, 3, 24)).Get("42-43-44")).Cards;

        var first = cards[0];
        Assert.True(first.HasPhoto);
        Assert.Equal(ParticipantRules.Ean8ForParticipant(42, 2027), first.Barcode);
        Assert.Equal(
            new Dictionary<IdCardDataSource, string>
            {
                [IdCardDataSource.Id] = "42",
                [IdCardDataSource.Name] = "Testa Testesen",
                [IdCardDataSource.OrganizerArea] = "Brætspilsdesigner",
                // In the participant's language (Danish by default)
                [IdCardDataSource.Pronouns] = "Hun/Hende",
            },
            first.Texts);
        // Deltagere::getName: the nickname when there is one. Pronouns not given yet ("none") leave the box blank.
        Assert.False(cards[1].HasPhoto);
        Assert.Equal("Testo", cards[1].Texts[IdCardDataSource.Name]);
        Assert.Equal(string.Empty, cards[1].Texts[IdCardDataSource.Pronouns]);
        Assert.Equal(string.Empty, cards[1].Texts[IdCardDataSource.OrganizerArea]);
        Assert.Equal("Ask me", cards[2].Texts[IdCardDataSource.Pronouns]);
    }
}
