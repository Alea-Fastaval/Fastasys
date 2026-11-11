using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using Fastasys.ApiService.Data;
using Fastasys.ApiService.Data.Entities;
using Fastasys.ApiService.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Fastasys.ApiService.Controllers;

// Infosys deltager/visalle: the id and "fornavn efternavn" of every participant
public record ParticipantSummaryDto(int Id, string Name);

// Infosys deltager/visdeltager/:id:. Details is null for users who may only view participants: Infosys hides
// everything below the personal info, the notes included, from its Read-only roles.
public record ParticipantDto(
    int Id,
    string FirstName,
    string LastName,
    string Nickname,
    string Email,
    string PhoneNumber,
    string AddressLine1,
    string AddressLine2,
    string ZipCode,
    string City,
    string CountryCode,
    string CountryName,
    DateOnly? BirthDate,
    string Pronouns,
    PreferredLanguage PreferredLanguage,
    int CategoryId,
    string CategoryName,
    int AmountPaid,
    bool SmsSignup,
    // Infosys "Vis forrige" / "Vis næste": the participant with the nearest lower / higher id
    int? PreviousId,
    int? NextId,
    bool CanEdit,
    ParticipantDetailsDto? Details
);

public record ParticipantDetailsDto(
    int? OrganizerAreaId,
    string? OrganizerAreaName,
    bool IsAuthorOrDesigner,
    int? GameActivityId,
    string? GameActivityTitle,
    int ExtraVouchers,
    IReadOnlyList<ParticipantLanguage> ActivityLanguages,
    IReadOnlyList<SleepingArea> SleepingAreas,
    int DesiredHeroTasks,
    int DesiredActivities,
    bool SuperGameMaster,
    int? MaxGameMasterRuns,
    bool JuniorGameMaster,
    bool SuperRulesGuide,
    int? MaxRulesGuideGames,
    bool SuperHero,
    bool SetupHelpMonday,
    bool SetupHelpTuesday,
    bool FinancialSupport,
    bool IsNoShow,
    bool IsAnnulled,
    bool IsActivityLocked,
    DateTime SignedUpAt,
    DateTime? CheckedInAt,
    string YouthClub,
    bool PhotoConsent,
    string AdminNote,
    string MessageToParticipant,
    string PaymentNote,
    string? CommentNote,
    string? HeroForceNote,
    string? JuniorContact,
    string? JuniorComment,
    string? SleepingNote
);

public record ParticipantCategoryDto(int Id, string Name);

public record OrganizerAreaDto(int Id, string NameDanish, string? NameEnglish);

public record CountryDto(string Code, string NameDanish, string NameEnglish);

public record GameOptionDto(int Id, string Title, string TitleEnglish);

// The choices of the selects on the participant page and in the forms
public record ParticipantOptionsDto(
    IReadOnlyList<ParticipantCategoryDto> Categories,
    IReadOnlyList<OrganizerAreaDto> OrganizerAreas,
    IReadOnlyList<CountryDto> Countries,
    IReadOnlyList<GameOptionDto> Games
);

// The fields of the Infosys create form (deltager/opret) that work there. Land, Arrangørområde, Scenarie and the
// activity languages are left out: Infosys ignores them on create (bug reported to the user on 2026-11-11).
public abstract record ParticipantFieldsDto
{
    public DateOnly? BirthDate { get; init; }
    public int CategoryId { get; init; }
    public int AmountPaid { get; init; }
    public string? FirstName { get; init; }
    public string? LastName { get; init; }
    public string? Email { get; init; }
    public string? AddressLine1 { get; init; }
    public string? AddressLine2 { get; init; }
    public string? ZipCode { get; init; }
    public string? City { get; init; }
    public string? PhoneNumber { get; init; }
    public bool SmsSignup { get; init; }
    public PreferredLanguage PreferredLanguage { get; init; }
    public bool SuperHero { get; init; }
    public bool IsAuthorOrDesigner { get; init; }
    public bool SetupHelpMonday { get; init; }
    public bool SetupHelpTuesday { get; init; }
    public int DesiredActivities { get; init; }
    public int DesiredHeroTasks { get; init; }
    public bool IsNoShow { get; init; }
    public bool SuperGameMaster { get; init; }
    public string? YouthClub { get; init; }
}

public record CreateParticipantDto : ParticipantFieldsDto;

// The fields of the Infosys edit form (deltager/retdeltager) that work there: the create fields plus the activity
// languages and Annulleret. Land, Arrangørområde and Scenarie are ignored by Infosys (bug reported 2026-11-11).
public record UpdateParticipantDto : ParticipantFieldsDto
{
    // Infosys only changes the languages when at least one is ticked
    public IReadOnlyList<ParticipantLanguage>? ActivityLanguages { get; init; }
    public bool IsAnnulled { get; init; }
}

// Infosys deltager/ajax/editable/:id: changes one field from the participant page
public record UpdateParticipantFieldDto(JsonElement Value);

[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "ParticipantView")]
public class ParticipantsController : ControllerBase
{
    // Column sizes of Infosys deltagere
    private const int MaxTinyUnsigned = 255;
    private const int MinTinySigned = -128;
    private const int MaxTinySigned = 127;

    private const string NotFoundMessage = "Participant not found";

    private readonly InfosysDbContext _db;
    private readonly IAuthorizationService _authorization;
    private readonly ILogger<ParticipantsController> _logger;

    public ParticipantsController(InfosysDbContext db, IAuthorizationService authorization, ILogger<ParticipantsController> logger)
    {
        _db = db;
        _authorization = authorization;
        _logger = logger;
    }

    // Infosys deltager/visalle without a search: every participant in id order
    [HttpGet]
    public async Task<ActionResult<IEnumerable<ParticipantSummaryDto>>> GetAll()
    {
        var participants = await _db.Participants
            .AsNoTracking()
            .OrderBy(p => p.Id)
            .Select(p => new ParticipantSummaryDto(p.Id, p.FirstName + " " + p.LastName))
            .ToListAsync();

        return Ok(participants);
    }

    // Infosys deltager/visdeltager/:id:. Like Infosys, the id may also be a scanned badge (EAN-8 or EAN-13).
    [HttpGet("{id:long}")]
    public async Task<ActionResult<ParticipantDto>> Get(long id)
    {
        var participantId = ToParticipantId(id);
        if (participantId == null) return NotFound(new { message = NotFoundMessage });

        var participant = await LoadAsync(participantId.Value);
        if (participant == null) return NotFound(new { message = NotFoundMessage });

        return Ok(await ToDtoAsync(participant));
    }

    [HttpGet("options")]
    public async Task<ActionResult<ParticipantOptionsDto>> GetOptions()
    {
        // Sorting by Danish name happens in the browser (Infosys: ORDER BY name_da COLLATE utf8mb4_danish_ci)
        var categories = await _db.ParticipantCategories.AsNoTracking().OrderBy(c => c.Id)
            .Select(c => new ParticipantCategoryDto(c.Id, c.Name)).ToListAsync();
        var areas = await _db.OrganizerAreas.AsNoTracking().OrderBy(a => a.Id)
            .Select(a => new OrganizerAreaDto(a.Id, a.NameDanish, a.NameEnglish)).ToListAsync();
        var countries = await _db.Countries.AsNoTracking().OrderBy(c => c.Code)
            .Select(c => new CountryDto(c.Code, c.NameDanish, c.NameEnglish)).ToListAsync();
        // Infosys ParticipantModel::getGames: activities of type braet or rolle
        var games = await _db.Activities.AsNoTracking()
            .Where(a => a.Type == ActivityType.BoardGame || a.Type == ActivityType.Scenario)
            .OrderBy(a => a.Id)
            .Select(a => new GameOptionDto(a.Id, a.Title, a.TitleEnglish)).ToListAsync();

        return Ok(new ParticipantOptionsDto(categories, areas, countries, games));
    }

    // Infosys deltager/opret ("Opret deltager")
    [HttpPost]
    [Authorize(Policy = "ParticipantManagement")]
    public async Task<ActionResult<ParticipantDto>> Create([FromBody] CreateParticipantDto dto)
    {
        var (isValid, error) = await ValidateAsync(dto);
        if (!isValid) return BadRequest(new { message = error });

        var now = DateTime.UtcNow;
        var participant = new Participant { CreatedAt = now, UpdatedAt = now, SignedUpAt = now };
        Apply(participant, dto);
        Normalize(participant);

        // Two participants created at the same time can get the same lowest free id; the second one tries again
        const int attempts = 3;
        for (var attempt = 1; ; attempt++)
        {
            var usedIds = await _db.Participants.Select(p => p.Id).ToListAsync();
            participant.Id = ParticipantRules.LowestFreeId(usedIds);
            _db.Participants.Add(participant);
            try
            {
                await _db.SaveChangesAsync();
                break;
            }
            catch (DbUpdateException) when (attempt < attempts)
            {
                _db.Entry(participant).State = EntityState.Detached;
            }
        }

        _logger.LogInformation("Participant {ParticipantId} created by user {UserId}", participant.Id, CurrentUserId());

        var created = await LoadAsync(participant.Id);
        return CreatedAtAction(nameof(Get), new { id = participant.Id }, await ToDtoAsync(created!));
    }

    // Infosys deltager/update/:id: ("Opdater deltager")
    [HttpPut("{id:int}")]
    [Authorize(Policy = "ParticipantManagement")]
    public async Task<ActionResult<ParticipantDto>> Update(int id, [FromBody] UpdateParticipantDto dto)
    {
        var participant = await _db.Participants.FindAsync(id);
        if (participant == null) return NotFound(new { message = NotFoundMessage });

        var (isValid, error) = await ValidateAsync(dto);
        if (!isValid) return BadRequest(new { message = error });
        if (dto.ActivityLanguages != null && dto.ActivityLanguages.Any(l => !Enum.IsDefined(l)))
        {
            return BadRequest(new { message = "Unknown activity language" });
        }

        Apply(participant, dto);
        if (dto.ActivityLanguages is { Count: > 0 })
        {
            participant.ActivityLanguages = dto.ActivityLanguages.Distinct().Order().ToList();
        }
        participant.IsAnnulled = dto.IsAnnulled;
        Normalize(participant);
        participant.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        _logger.LogInformation("Participant {ParticipantId} updated by user {UserId}", id, CurrentUserId());

        return Ok(await ToDtoAsync((await LoadAsync(id))!));
    }

    // Infosys deltager/ajax/editable/:id: (click-to-edit on the participant page) and deltager/updatenote (the
    // note pages). Only the fields the participant page offers can be changed; Infosys set any column it was sent.
    [HttpPut("{id:int}/fields/{field}")]
    [Authorize(Policy = "ParticipantManagement")]
    public async Task<ActionResult<ParticipantDto>> UpdateField(int id, string field, [FromBody] UpdateParticipantFieldDto dto)
    {
        var participant = await _db.Participants.FindAsync(id);
        if (participant == null) return NotFound(new { message = NotFoundMessage });

        var (isValid, error) = await ApplyFieldAsync(participant, field, dto.Value);
        if (!isValid) return BadRequest(new { message = error });

        Normalize(participant);
        participant.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        _logger.LogInformation("Participant {ParticipantId} field {Field} updated by user {UserId}", id, field, CurrentUserId());

        return Ok(await ToDtoAsync((await LoadAsync(id))!));
    }

    // Infosys deltager/delete/:id: ("Slet for pokker"). Infosys only offers it for annulled participants; that is
    // now checked here too. Their seats on runs are removed with them, as in Infosys.
    [HttpDelete("{id:int}")]
    [Authorize(Policy = "ParticipantManagement")]
    public async Task<IActionResult> Delete(int id)
    {
        var participant = await _db.Participants.FindAsync(id);
        if (participant == null) return NotFound(new { message = NotFoundMessage });

        if (!participant.IsAnnulled)
        {
            return Conflict(new { message = "Only annulled participants can be deleted" });
        }

        _db.RunSeats.RemoveRange(_db.RunSeats.Where(s => s.ParticipantId == id));
        _db.Participants.Remove(participant);
        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // Something else still refers to the participant
            return Conflict(new { message = "Could not delete the participant" });
        }

        _logger.LogInformation("Participant {ParticipantId} deleted by user {UserId}", id, CurrentUserId());

        return Ok(new { message = "Participant deleted successfully" });
    }

    // A participant id, or the id in a scanned badge (Infosys ParticipantModel::findDeltager)
    private static int? ToParticipantId(long input)
    {
        var text = input.ToString(CultureInfo.InvariantCulture);
        var fromBarcode = ParticipantRules.IdFromBarcode(text, CopenhagenNow().Year);
        var id = fromBarcode ?? input;
        return id is > 0 and <= int.MaxValue ? (int)id : null;
    }

    private static DateTime CopenhagenNow() =>
        TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTime.UtcNow, "Europe/Copenhagen");

    // Returns whether the form fields are valid and, if not, why
    private async Task<(bool IsValid, string Error)> ValidateAsync(ParticipantFieldsDto dto)
    {
        if (!await _db.ParticipantCategories.AnyAsync(c => c.Id == dto.CategoryId)) return (false, "Unknown category");
        if (!Enum.IsDefined(dto.PreferredLanguage)) return (false, "Unknown preferred language");

        var (textValid, textError) = ValidateLengths(
            ("First name", dto.FirstName, 128),
            ("Last name", dto.LastName, 256),
            ("Email", dto.Email, 512),
            ("Address", dto.AddressLine1, 128),
            ("Address line 2", dto.AddressLine2, 128),
            ("Zip code", dto.ZipCode, 32),
            ("City", dto.City, 128),
            ("Phone number", dto.PhoneNumber, 32),
            ("Youth club", dto.YouthClub, 128));
        if (!textValid) return (false, textError);

        if (dto.DesiredActivities is < 0 or > MaxTinyUnsigned) return (false, $"Desired activities must be between 0 and {MaxTinyUnsigned}");
        if (dto.DesiredHeroTasks is < 0 or > MaxTinyUnsigned) return (false, $"Desired hero tasks must be between 0 and {MaxTinyUnsigned}");

        return (true, string.Empty);
    }

    private static (bool IsValid, string Error) ValidateLengths(params (string Name, string? Value, int Max)[] fields)
    {
        foreach (var (name, value, max) in fields)
        {
            if ((value?.Length ?? 0) > max) return (false, $"{name} can be at most {max} characters");
        }

        return (true, string.Empty);
    }

    private static void Apply(Participant participant, ParticipantFieldsDto dto)
    {
        participant.BirthDate = dto.BirthDate;
        participant.CategoryId = dto.CategoryId;
        participant.AmountPaid = dto.AmountPaid;
        participant.FirstName = dto.FirstName ?? string.Empty;
        participant.LastName = dto.LastName ?? string.Empty;
        participant.Email = dto.Email ?? string.Empty;
        participant.AddressLine1 = dto.AddressLine1 ?? string.Empty;
        participant.AddressLine2 = dto.AddressLine2 ?? string.Empty;
        participant.ZipCode = dto.ZipCode ?? string.Empty;
        participant.City = dto.City ?? string.Empty;
        participant.PhoneNumber = dto.PhoneNumber ?? string.Empty;
        participant.SmsSignup = dto.SmsSignup;
        participant.PreferredLanguage = dto.PreferredLanguage;
        participant.SuperHero = dto.SuperHero;
        participant.IsAuthorOrDesigner = dto.IsAuthorOrDesigner;
        participant.SetupHelpMonday = dto.SetupHelpMonday;
        participant.SetupHelpTuesday = dto.SetupHelpTuesday;
        participant.DesiredActivities = dto.DesiredActivities;
        participant.DesiredHeroTasks = dto.DesiredHeroTasks;
        participant.IsNoShow = dto.IsNoShow;
        participant.SuperGameMaster = dto.SuperGameMaster;
        participant.YouthClub = dto.YouthClub ?? string.Empty;
    }

    // Infosys Deltagere::insert and ::update normalize the phone number and e-mail on every save
    private static void Normalize(Participant participant)
    {
        participant.PhoneNumber = ParticipantRules.NormalizePhone(participant.PhoneNumber);
        participant.Email = ParticipantRules.NormalizeEmail(participant.Email);
    }

    // Sets one field from the participant page. Returns whether the value was valid and, if not, why.
    private async Task<(bool IsValid, string Error)> ApplyFieldAsync(Participant p, string field, JsonElement value)
    {
        switch (field)
        {
            // Infosys edits the name and the address as one text each; they are two fields each here, so a name
            // or address with spaces is no longer split wrongly (bug fixed on 2026-11-11)
            case "firstName": return SetText(value, 128, v => p.FirstName = v);
            case "lastName": return SetText(value, 256, v => p.LastName = v);
            case "addressLine1": return SetText(value, 128, v => p.AddressLine1 = v);
            case "addressLine2": return SetText(value, 128, v => p.AddressLine2 = v);
            case "nickname": return SetText(value, 255, v => p.Nickname = v);
            case "email": return SetText(value, 512, v => p.Email = v);
            case "phoneNumber": return SetText(value, 32, v => p.PhoneNumber = v);
            case "zipCode": return SetText(value, 32, v => p.ZipCode = v);
            case "city": return SetText(value, 128, v => p.City = v);
            case "youthClub": return SetText(value, 128, v => p.YouthClub = v);
            case "adminNote": return SetText(value, null, v => p.AdminNote = v);
            case "messageToParticipant": return SetText(value, null, v => p.MessageToParticipant = v);
            case "paymentNote": return SetText(value, null, v => p.PaymentNote = v);
            case "commentNote": return SetText(value, null, v => p.CommentNote = v);
            case "heroForceNote": return SetText(value, null, v => p.HeroForceNote = v);
            case "juniorContact": return SetText(value, null, v => p.JuniorContact = v);
            case "juniorComment": return SetText(value, null, v => p.JuniorComment = v);
            case "sleepingNote": return SetText(value, null, v => p.SleepingNote = v);

            case "smsSignup": return SetBool(value, v => p.SmsSignup = v);
            case "photoConsent": return SetBool(value, v => p.PhotoConsent = v);
            case "isAuthorOrDesigner": return SetBool(value, v => p.IsAuthorOrDesigner = v);
            case "superGameMaster": return SetBool(value, v => p.SuperGameMaster = v);
            case "juniorGameMaster": return SetBool(value, v => p.JuniorGameMaster = v);
            case "superRulesGuide": return SetBool(value, v => p.SuperRulesGuide = v);
            case "setupHelpMonday": return SetBool(value, v => p.SetupHelpMonday = v);
            case "setupHelpTuesday": return SetBool(value, v => p.SetupHelpTuesday = v);
            case "financialSupport": return SetBool(value, v => p.FinancialSupport = v);
            case "isNoShow": return SetBool(value, v => p.IsNoShow = v);
            case "isAnnulled": return SetBool(value, v => p.IsAnnulled = v);
            case "isActivityLocked": return SetBool(value, v => p.IsActivityLocked = v);

            case "amountPaid": return SetInt(value, int.MinValue, int.MaxValue, v => p.AmountPaid = v);
            case "extraVouchers": return SetInt(value, MinTinySigned, MaxTinySigned, v => p.ExtraVouchers = v);
            case "desiredActivities": return SetInt(value, 0, MaxTinyUnsigned, v => p.DesiredActivities = v);
            case "desiredHeroTasks": return SetInt(value, 0, MaxTinyUnsigned, v => p.DesiredHeroTasks = v);
            case "maxGameMasterRuns": return SetOptionalInt(value, 0, MaxTinyUnsigned, v => p.MaxGameMasterRuns = v);
            case "maxRulesGuideGames": return SetOptionalInt(value, 0, MaxTinyUnsigned, v => p.MaxRulesGuideGames = v);

            // Infosys stored 0000-00-00 when the date couldn't be read; an invalid date is now refused (bug fixed
            // on 2026-11-11). null means unknown.
            case "birthDate":
                if (value.ValueKind == JsonValueKind.Null) { p.BirthDate = null; return (true, string.Empty); }
                if (value.ValueKind == JsonValueKind.String
                    && DateOnly.TryParseExact(value.GetString(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
                {
                    p.BirthDate = date;
                    return (true, string.Empty);
                }
                return (false, "Birth date must be a date (yyyy-MM-dd) or null");

            case "pronouns":
                if (value.ValueKind == JsonValueKind.String && ParticipantRules.IsValidPronouns(value.GetString()))
                {
                    p.Pronouns = value.GetString()!;
                    return (true, string.Empty);
                }
                return (false, "Unknown pronouns");

            case "preferredLanguage":
                if (value.ValueKind == JsonValueKind.String && Enum.TryParse<PreferredLanguage>(value.GetString(), out var language)
                    && Enum.IsDefined(language) && !int.TryParse(value.GetString(), out _))
                {
                    p.PreferredLanguage = language;
                    return (true, string.Empty);
                }
                return (false, "Unknown preferred language");

            case "categoryId":
                if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var categoryId)
                    && await _db.ParticipantCategories.AnyAsync(c => c.Id == categoryId))
                {
                    p.CategoryId = categoryId;
                    return (true, string.Empty);
                }
                return (false, "Unknown category");

            case "countryCode":
                if (value.ValueKind == JsonValueKind.String && value.GetString() is { } code
                    && await _db.Countries.AnyAsync(c => c.Code == code))
                {
                    p.CountryCode = code;
                    return (true, string.Empty);
                }
                return (false, "Unknown country");

            // Infosys "Intet arrangørområde" clears it
            case "organizerAreaId":
                if (value.ValueKind == JsonValueKind.Null) { p.OrganizerAreaId = null; return (true, string.Empty); }
                if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var areaId)
                    && await _db.OrganizerAreas.AnyAsync(a => a.Id == areaId))
                {
                    p.OrganizerAreaId = areaId;
                    return (true, string.Empty);
                }
                return (false, "Unknown organizer area");

            // Infosys "Intet scenarie/brætspil" clears it; the page only offers scenarios and board games
            case "gameActivityId":
                if (value.ValueKind == JsonValueKind.Null) { p.GameActivityId = null; return (true, string.Empty); }
                if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var gameId)
                    && await _db.Activities.AnyAsync(a => a.Id == gameId && (a.Type == ActivityType.BoardGame || a.Type == ActivityType.Scenario)))
                {
                    p.GameActivityId = gameId;
                    return (true, string.Empty);
                }
                return (false, "Unknown scenario or board game");

            default:
                return (false, "Unknown field");
        }
    }

    private static (bool IsValid, string Error) SetText(JsonElement value, int? maxLength, Action<string> set)
    {
        if (value.ValueKind != JsonValueKind.String) return (false, "Value must be a text");
        var text = value.GetString()!;
        if (maxLength != null && text.Length > maxLength) return (false, $"Value can be at most {maxLength} characters");
        set(text);
        return (true, string.Empty);
    }

    private static (bool IsValid, string Error) SetBool(JsonElement value, Action<bool> set)
    {
        if (value.ValueKind is not (JsonValueKind.True or JsonValueKind.False)) return (false, "Value must be true or false");
        set(value.GetBoolean());
        return (true, string.Empty);
    }

    private static (bool IsValid, string Error) SetInt(JsonElement value, int min, int max, Action<int> set)
    {
        if (value.ValueKind != JsonValueKind.Number || !value.TryGetInt32(out var number)) return (false, "Value must be a whole number");
        if (number < min || number > max) return (false, $"Value must be between {min} and {max}");
        set(number);
        return (true, string.Empty);
    }

    private static (bool IsValid, string Error) SetOptionalInt(JsonElement value, int min, int max, Action<int?> set)
    {
        if (value.ValueKind == JsonValueKind.Null) { set(null); return (true, string.Empty); }
        return SetInt(value, min, max, v => set(v));
    }

    private async Task<Participant?> LoadAsync(int id) =>
        await _db.Participants
            .AsNoTracking()
            .Include(p => p.Category)
            .Include(p => p.Country)
            .Include(p => p.OrganizerArea)
            .Include(p => p.GameActivity)
            .FirstOrDefaultAsync(p => p.Id == id);

    private async Task<ParticipantDto> ToDtoAsync(Participant p)
    {
        var canEdit = (await _authorization.AuthorizeAsync(User, "ParticipantManagement")).Succeeded;
        var previousId = await _db.Participants.Where(o => o.Id < p.Id).OrderByDescending(o => o.Id).Select(o => (int?)o.Id).FirstOrDefaultAsync();
        var nextId = await _db.Participants.Where(o => o.Id > p.Id).OrderBy(o => o.Id).Select(o => (int?)o.Id).FirstOrDefaultAsync();

        var details = canEdit
            ? new ParticipantDetailsDto(
                p.OrganizerAreaId,
                p.OrganizerArea?.NameDanish,
                p.IsAuthorOrDesigner,
                p.GameActivityId,
                p.GameActivity?.Title,
                p.ExtraVouchers,
                p.ActivityLanguages,
                p.SleepingAreas,
                p.DesiredHeroTasks,
                p.DesiredActivities,
                p.SuperGameMaster,
                p.MaxGameMasterRuns,
                p.JuniorGameMaster,
                p.SuperRulesGuide,
                p.MaxRulesGuideGames,
                p.SuperHero,
                p.SetupHelpMonday,
                p.SetupHelpTuesday,
                p.FinancialSupport,
                p.IsNoShow,
                p.IsAnnulled,
                p.IsActivityLocked,
                p.SignedUpAt,
                p.CheckedInAt,
                p.YouthClub,
                p.PhotoConsent,
                p.AdminNote,
                p.MessageToParticipant,
                p.PaymentNote,
                p.CommentNote,
                p.HeroForceNote,
                p.JuniorContact,
                p.JuniorComment,
                p.SleepingNote)
            : null;

        return new ParticipantDto(
            p.Id,
            p.FirstName,
            p.LastName,
            p.Nickname,
            p.Email,
            p.PhoneNumber,
            p.AddressLine1,
            p.AddressLine2,
            p.ZipCode,
            p.City,
            p.CountryCode,
            p.Country?.NameDanish ?? string.Empty,
            p.BirthDate,
            p.Pronouns,
            p.PreferredLanguage,
            p.CategoryId,
            p.Category?.Name ?? string.Empty,
            p.AmountPaid,
            p.SmsSignup,
            previousId,
            nextId,
            canEdit,
            details);
    }

    private string? CurrentUserId() => User?.FindFirstValue(ClaimTypes.NameIdentifier);
}
