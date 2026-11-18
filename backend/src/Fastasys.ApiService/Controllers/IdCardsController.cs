using Fastasys.ApiService.Data;
using Fastasys.ApiService.Data.Entities;
using Fastasys.ApiService.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Fastasys.ApiService.Controllers;

// One ID card to draw: the participant's template, whether they have a photo, their EAN-8 barcode number and the
// texts the template's text items can show
public record IdCardDto(int ParticipantId, int TemplateId, bool HasPhoto, string Barcode, IReadOnlyDictionary<IdCardDataSource, string> Texts);

// The cards in the order asked for, and the templates they use
public record IdCardsDto(IReadOnlyList<IdCardDto> Cards, IReadOnlyList<IdCardTemplateDto> Templates);

// Infosys IdTemplateController::renderIdCards (id-card/render?ids=1-2-3) with IdTemplateModel::fetchIdCardData. Infosys
// drew each card on the server; agreed on 2026-11-18 that the browser draws them from this data.
[ApiController]
[Route("api/id-cards")]
[Authorize(Policy = "ParticipantManagement")]
public class IdCardsController : ControllerBase
{
    private readonly InfosysDbContext _db;
    private readonly ConventionOptions _convention;

    public IdCardsController(InfosysDbContext db, IOptions<ConventionOptions> convention)
    {
        _db = db;
        _convention = convention.Value;
    }

    // ids: participant ids separated by "-", read like PHP intval. As in Infosys, an id without a participant, or a
    // participant with neither their own nor their category's template, is left out. So is a template without a
    // background, which Infosys couldn't draw either (its page failed).
    [HttpGet]
    public async Task<ActionResult<IdCardsDto>> Get([FromQuery] string? ids)
    {
        var wanted = (ids ?? string.Empty)
            .Split('-')
            .Select(ParticipantSearch.PhpIntval)
            .Where(id => id is > 0 and <= int.MaxValue)
            .Select(id => (int)id)
            .ToList();
        if (wanted.Count == 0) return Ok(new IdCardsDto([], []));

        var distinct = wanted.Distinct().ToList();
        var participants = await _db.Participants
            .AsNoTracking()
            .Where(p => distinct.Contains(p.Id))
            .Select(p => new
            {
                p.Id,
                p.FirstName,
                p.LastName,
                p.Nickname,
                p.YouthClub,
                OrganizerArea = p.OrganizerArea != null ? p.OrganizerArea.NameDanish : null,
                Game = p.GameActivity != null ? p.GameActivity.Title : null,
                // Infosys IdTemplateModel::getParticipantTemplate: their own template, or else their category's
                TemplateId = p.IdCardTemplateId ?? p.Category.IdCardTemplateId,
                HasPhoto = _db.ParticipantPhotos.Any(photo => photo.ParticipantId == p.Id)
            })
            .ToDictionaryAsync(p => p.Id);

        var templateIds = participants.Values.Where(p => p.TemplateId != null).Select(p => p.TemplateId!.Value).Distinct().ToList();
        var templates = await _db.IdCardTemplates
            .AsNoTracking()
            .Where(t => templateIds.Contains(t.Id) && t.Background != null)
            .OrderBy(t => t.Id)
            .Select(t => new IdCardTemplateDto(
                t.Id,
                t.Name,
                t.Width,
                t.Height,
                true,
                t.Items
                    .OrderBy(i => i.Id)
                    .Select(i => new IdCardTemplateItemDto(i.Type, i.X, i.Y, i.Width, i.Height, i.Rotation, i.DataSource))
                    .ToList()))
            .ToListAsync();
        var drawable = templates.Select(t => t.Id).ToHashSet();

        // Infosys Deltagere::getEan8Number with the convention's year (getConYear)
        var year = _convention.Start?.Year ?? TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTime.UtcNow, "Europe/Copenhagen").Year;

        var cards = wanted
            .Where(id => participants.TryGetValue(id, out var p) && p.TemplateId is int templateId && drawable.Contains(templateId))
            .Select(id =>
            {
                var p = participants[id];
                var name = IdCardTexts.Name(p.FirstName, p.LastName, p.Nickname);
                return new IdCardDto(
                    p.Id,
                    p.TemplateId!.Value,
                    p.HasPhoto,
                    ParticipantRules.Ean8ForParticipant(p.Id, year),
                    new Dictionary<IdCardDataSource, string>
                    {
                        [IdCardDataSource.Id] = p.Id.ToString(System.Globalization.CultureInfo.InvariantCulture),
                        [IdCardDataSource.Name] = name,
                        [IdCardDataSource.DoctorName] = "Dr. " + name,
                        [IdCardDataSource.OrganizerArea] = p.OrganizerArea ?? string.Empty,
                        [IdCardDataSource.Game] = p.Game ?? string.Empty,
                        [IdCardDataSource.YouthClub] = p.YouthClub
                    });
            })
            .ToList();

        return Ok(new IdCardsDto(cards, templates));
    }
}

// The texts of an ID card (Infosys IdTemplateRenderer::addTextElement)
public static class IdCardTexts
{
    // Infosys Deltagere::getName: the nickname when there is one, otherwise the first and last name
    public static string Name(string firstName, string lastName, string nickname) =>
        !string.IsNullOrEmpty(nickname) ? nickname : $"{firstName} {lastName}".Trim();
}
