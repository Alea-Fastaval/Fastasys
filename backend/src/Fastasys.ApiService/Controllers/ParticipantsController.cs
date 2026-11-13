using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;
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
// everything below the personal info, the notes included, from its Read-only roles. The exception is the Pris
// breakdown box (Price), which Infosys shows to everyone who can open the page.
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
    // The Pris breakdown box. null until the ticket and payment system is ported (see ParticipantPriceDto).
    ParticipantPriceDto? Price,
    ParticipantDetailsDto? Details
);

// The Pris breakdown box of the Infosys participant page (visdeltager.phtml, #prisbreakdown), in whole DKK like
// Infosys. Infosys shows it to its Read-only roles and in "Betalings mode" too, unlike the other boxes.
// The box shows each field below on its own line, then:
// - "Pris for tilmelding (inklusiv ændringer fra infosys)" = SignupTotal
// - "Forudbetalt" = AmountPaid
// - "Difference for tilmelding" = SignupTotal - AmountPaid
// - "Pris for ikke tildelte aktiviteter" = SignedUpActivities - AssignedActivities
// - "Total pris ved chek-ind" = CheckInTotal
// - "Difference ved check-ind (CheckInTotal - AmountPaid):" = CheckInTotal - AmountPaid
//
// To fill it when tickets, food, wear, activity signups and the signup's price are ported (Deltagere::calc*):
// - OriginalPrice, "Oprindelig pris fra tilmelding": deltagere.original_price, the total the signup calculated and
//   stored. It is not part of the totals. Fastasys has no column for it yet; it comes with the signup.
// - Entry, "Indgang + overnatning" (calcEntry): the sum of indgang.pris over the participant's entrance tickets plus
//   the sum over their sleep tickets (see ParticipantTicketDto.Line). Infosys counts a ticket type that is both twice.
// - Food, "Mad" (calcFood): the sum of mad.pris over the participant's meals in madtider.dato order, skipping meals
//   with a price of 0 or less. With Økonomisk støtte (FinancialSupport) the first 2 breakfasts (Copenhagen hour
//   before 10) and the first 2 dinners (hour 16 or later) that have a price are free (Madtider::isBreakfast/isDinner).
// - Wear, "Wear" (calcWear): the sum of UnitPrice × Amount over the wear orders (see ParticipantWearOrderDto).
// - SignedUpActivities, "Aktiviteter" (calcActivities(true)): the sum of aktiviteter.pris over the participant's
//   signup wishes (deltagere_tilmeldinger) with tilmeldingstype 'spiller'. Every wish counts, so wishing two runs
//   of a paid activity counts its price twice.
// - AssignedActivities (calcActivities(false)): the sum of Activity.Price over the participant's seats with Role
//   Player. For participants with work_area 2 (Brætspilsdesigner), Infosys also counts the runs of activities with
//   auto_signup_category 'designer' (Deltagere::getPladser and getAutoSpots). Seats and activity prices are ported
//   already; the auto spots are not.
// - Other, "Andet (fest, gebyrer, etc)" (calcOtherStuff): the sum of indgang.pris over the tickets that are neither
//   entrance, sleep nor Alea membership: party, fees, "Rig onkel", mattress and the like.
// - Alea, "Alea" (calcAlea): the sum of indgang.pris over the tickets whose type contains "medlemskab".
// Infosys's totals also add 300 for each of rig_onkel and hemmelig_onkel (calcRichBastard), with no line in the box.
// Those admin-only columns are not ported; new signups buy "Rig onkel" tickets instead, which are in Other.
public record ParticipantPriceDto(
    int OriginalPrice,
    int Entry,
    int Food,
    int Wear,
    int SignedUpActivities,
    int AssignedActivities,
    int Other,
    int Alea)
{
    // Deltagere::calcSignupTotal: the price of what the participant signed up for, with activities from their signup
    // wishes. Infosys's online payment and debtor list use it.
    public int SignupTotal => Entry + Food + SignedUpActivities + Wear + Alea + Other;

    // Deltagere::calcRealTotal: the same with activities from the seats they were given. Infosys's check-in settles
    // the participant's balance against it.
    public int CheckInTotal => Entry + Food + AssignedActivities + Wear + Alea + Other;
}

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
    string? SleepingNote,
    // The Wear box. null until wear is ported (see ParticipantWearOrderDto); then the participant's orders.
    IReadOnlyList<ParticipantWearOrderDto>? WearOrders,
    // The Indgang/Mad box: the tickets (null until entrance is ported, see ParticipantTicketDto) and the meals
    // (null until food is ported, see ParticipantMealDto)
    IReadOnlyList<ParticipantTicketDto>? Tickets,
    IReadOnlyList<ParticipantMealDto>? Meals,
    // The Soveplads box. null until sleeping places are ported (see ParticipantSleepingPlaceDto).
    IReadOnlyList<ParticipantSleepingPlaceDto>? SleepingPlaces,
    // The Aktiviteter box: the participant's seats on runs and, for board game designers, their designer rows
    IReadOnlyList<ParticipantActivityDto> Activities
);

// What the participant is on a row of the Aktiviteter box (Infosys pladser.type): "spiller", "spilleder", or
// "designer" for a designer's own game
[JsonConverter(typeof(JsonStringEnumConverter<ParticipantActivityRole>))]
public enum ParticipantActivityRole
{
    Player,
    GameMaster,
    Designer
}

// One row of the Aktiviteter box of the Infosys participant page (visdeltager.phtml, .aktivitetboxen):
// Aktivitet (Title, linked to the activity), Tid ("Onsdag 10:00 -> Onsdag 14:00"), S / SL (Role), Hold (RunNumber,
// linked to the run) and Sted ("RoomName, RoomLocation").
// The rows of a board game designer's own game and of the activities with automatic signup have no run, so RunId and
// RunNumber are null and the room is the schedule's meeting room, which can be missing.
public record ParticipantActivityDto(
    int ActivityId,
    string Title,
    DateTime Start,
    DateTime End,
    ParticipantActivityRole Role,
    int? RunId,
    int? RunNumber,
    string? RoomName,
    string? RoomLocation);

// The line of the Indgang/Mad box a ticket is shown on
[JsonConverter(typeof(JsonStringEnumConverter<TicketLine>))]
public enum TicketLine
{
    // "Indgang"
    Entrance,
    // "Overnatning"
    Sleeping,
    // "Andet"
    Other
}

// One ticket (Infosys indgang) in the Indgang/Mad box of the Infosys participant page (visdeltager.phtml,
// #indgang_mad_info). The Indgang and Overnatning lines show "Partout", or else the short Danish name of Start's day
// ("Ons"), followed by " (Digital)" for a digital ticket (Indgang::getShortDescription). The Andet line shows Name.
// Each line joins its tickets with ", ".
// To fill it when entrance is ported:
// - The tickets are the participant's deltagere_indgang rows, in indgang id order (DeltagereIndgang::getDeltagerIndgang).
// - Line: Entrance when the type contains "indgang" or "Dagsbillet" (Indgang::isEntrance), Sleeping when it
//   contains "overnatning" (Indgang::isSleepTicket), otherwise Other; case-insensitive. Infosys shows a type with
//   both words on both lines, which one Line can't express. Give the Fastasys ticket types explicit flags instead
//   of matching on their names, and decide that case then.
// - IsAllConvention: the type contains "partout", "fastafamily", "alle" or "leje" (Indgang::spansAllConvention).
// - Start: indgang.start (a UTC instant; the box shows its Copenhagen day).
// - IsDigital: the type contains "digital".
// - Name, for the Andet line (Indgang::getDescription in Danish): "Fest" for the type "Ottofest", "Champagne" for
//   "Ottofest - Champagne", otherwise the type itself.
public record ParticipantTicketDto(TicketLine Line, bool IsAllConvention, DateTime Start, bool IsDigital, string Name);

// One meal on the Mad list of the Indgang/Mad box:
// "{short Danish name of Time's day} {Category} — (Udleveret|Ikke udleveret)".
// To fill it when food is ported:
// - The meals are the participant's deltagere_madtider rows, ordered by madtider.dato
//   (DeltagereMadtider::getDeltagerMadtider).
// - Time is madtider.dato (a UTC instant; the box shows its Copenhagen day). Category is mad.kategori of the meal's
//   mad_id.
// - IsReceived is deltagere_madtider.received = 1.
public record ParticipantMealDto(DateTime Time, string Category, bool IsReceived);

// One row of the Soveplads box (visdeltager.phtml, #sleeping): Tidsrum "{Starts} -> {Ends}" and Sted (Room).
// Infosys writes each day with PHP date('l, j/n'), e.g. "Wednesday, 24/3", with English day names on a Danish page.
// To fill it when sleeping places are ported:
// - The rows are the participant's participants_sleepingplaces rows, ordered by starts
//   (ParticipantModel::getSleepDataForParticipant). Infosys's edit page saves each night as 22:00 to 10:00 the
//   next day, or the whole convention as one row.
// - Starts and Ends are UTC instants; the box shows their Copenhagen days.
// - Room is the room's name (lokaler.beskrivelse).
public record ParticipantSleepingPlaceDto(DateTime Starts, DateTime Ends, string Room);

// One line of the Wear box on the Infosys participant page (visdeltager.phtml, #wear_info):
// "{Amount} stk. {Name}, str. {Size} — {UnitPrice × Amount},- ({UnitPrice},-) — (Udleveret|Ikke udleveret)".
// To fill it when wear is ported:
// - The orders are deltagere_wear_order for the participant, in id order (Infosys has no ORDER BY).
// - Name is DeltagereWear::getWearName('da', false): the wear's navn, then "-" + desc_da of each non-size
//   attribute (deltagere_wear_order_attributes → wear_attributes) in the order model, design, color.
// - Size is desc_da of the order's attribute of type "size", or null when it has none.
// - UnitPrice is wearpriser.pris of the order's wearpris_id (the price for the participant's category, chosen
//   when the order was made). Whole DKK, like the rest of the prices. The price breakdown's "Wear" line is the
//   sum of UnitPrice × Amount (Deltagere::calcWear).
// - IsReceived is deltagere_wear_order.received = 't'.
public record ParticipantWearOrderDto(int Amount, string Name, string? Size, int UnitPrice, bool IsReceived);

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

// The fields of the Infosys create form (deltager/opret)
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

    // Infosys's forms have Land, Arrangørområde, Scenarie and (on create) the activity languages, but ignore them
    // because the columns were renamed. Fixed on 2026-11-11: they are saved.
    // Land: null keeps the country (DK on create)
    public string? CountryCode { get; init; }

    // null is "Intet arrangørområde" / "Intet scenarie/brætspil"
    public int? OrganizerAreaId { get; init; }
    public int? GameActivityId { get; init; }

    // Only used when at least one is ticked, as in the Infosys edit form; otherwise a new participant gets dansk
    // (the column default) and an existing one keeps their languages
    public IReadOnlyList<ParticipantLanguage>? ActivityLanguages { get; init; }
}

public record CreateParticipantDto : ParticipantFieldsDto;

// The fields of the Infosys edit form (deltager/retdeltager): the create fields plus Annulleret
public record UpdateParticipantDto : ParticipantFieldsDto
{
    public bool IsAnnulled { get; init; }
}

// Infosys deltager/ajax/editable/:id: changes one field from the participant page
public record UpdateParticipantFieldDto(JsonElement Value);

// One search: the quick search from the page header, or the search box with its logic and how it is combined with
// the result of the steps before it. Infosys kept the result in the session; here the browser sends the searches
// again (they are kept in the URL, agreed 2026-11-11).
public record ParticipantSearchStepDto(
    string? Quick,
    ParticipantCriteria? Criteria,
    SearchLogic Logic = SearchLogic.And,
    SearchCombination Combination = SearchCombination.New
);

// Infosys deltager/visalle and deltager/search_result with the chosen extra columns ("Tilføj/fjern kolonne").
// No steps lists every participant.
public record SearchParticipantsDto(IReadOnlyList<ParticipantSearchStepDto>? Steps, IReadOnlyList<string>? Columns);

// Id and "fornavn efternavn" are always shown; Values has the chosen columns
public record ParticipantRowDto(int Id, string Name, IReadOnlyDictionary<string, object?> Values);

// DirectId: the participant a lone quick search goes straight to (an id, a badge, or the only participant found)
public record ParticipantSearchResultDto(int? DirectId, IReadOnlyList<ParticipantRowDto> Rows);

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

    // A URL can only hold so many searches; this is far more than anyone combines by hand
    private const int MaxSearchSteps = 20;

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

    // Infosys deltager/visalle, the quick search (IndexController::main + showSearchResult) and the search box
    // (deltager/ajax/parametersearch). The searches are run in order, each combined with the result so far.
    [HttpPost("search")]
    public async Task<ActionResult<ParticipantSearchResultDto>> Search([FromBody] SearchParticipantsDto dto)
    {
        var steps = dto.Steps ?? [];
        var columns = dto.Columns ?? [];

        var (isValid, error) = ValidateSearch(steps, columns);
        if (!isValid) return BadRequest(new { message = error });

        if (columns.Any(c => ParticipantColumns.All[c].EditorsOnly)
            && !(await _authorization.AuthorizeAsync(User, "ParticipantManagement")).Succeeded)
        {
            return Forbid();
        }

        var participants = await _db.Participants
            .AsNoTracking()
            .Include(p => p.Category)
            .Include(p => p.Country)
            .Include(p => p.OrganizerArea)
            .Include(p => p.GameActivity)
            .OrderBy(p => p.Id)
            .ToListAsync();
        var year = CopenhagenNow().Year;

        IReadOnlyList<int>? found = null;
        foreach (var step in steps)
        {
            // A quick search always starts a new search, as in Infosys
            found = step.Quick != null
                ? ParticipantSearch.Quick(participants, step.Quick, year)
                : ParticipantSearch.Combine(found, ParticipantSearch.Advanced(participants, step.Criteria!, step.Logic, year), step.Combination);
        }

        var ids = (found ?? participants.Select(p => p.Id)).ToHashSet();
        int? directId = null;
        if (steps is [{ Quick: { } quick }])
        {
            directId = ParticipantSearch.DirectId(quick, participants.Select(p => p.Id).ToHashSet(), year)
                ?? (ids.Count == 1 ? ids.Single() : null);
        }

        var rows = participants
            .Where(p => ids.Contains(p.Id))
            .Select(p => new ParticipantRowDto(
                p.Id,
                p.FirstName + " " + p.LastName,
                columns.Distinct().ToDictionary(c => c, c => ParticipantColumns.All[c].Value(p))))
            .ToList();

        return Ok(new ParticipantSearchResultDto(directId, rows));
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

        Apply(participant, dto);
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

    // Returns whether the searches and columns can be used and, if not, why
    private static (bool IsValid, string Error) ValidateSearch(IReadOnlyList<ParticipantSearchStepDto> steps, IReadOnlyList<string> columns)
    {
        if (steps.Count > MaxSearchSteps) return (false, $"At most {MaxSearchSteps} searches can be combined");
        foreach (var step in steps)
        {
            if ((step.Quick == null) == (step.Criteria == null)) return (false, "A search is either a quick search or search criteria");
            if (!Enum.IsDefined(step.Logic)) return (false, "Unknown search logic");
            if (!Enum.IsDefined(step.Combination)) return (false, "Unknown search combination");
            if (step.Criteria is { } c && (!Enum.IsDefined(c.BirthDateRange) || (c.ActivityLanguages?.Any(l => !Enum.IsDefined(l)) ?? false)))
            {
                return (false, "Unknown search value");
            }
        }

        var unknown = columns.FirstOrDefault(c => !ParticipantColumns.All.ContainsKey(c));
        return unknown == null ? (true, string.Empty) : (false, $"Unknown column {unknown}");
    }

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

        if (dto.CountryCode != null && !await IsKnownCountryAsync(dto.CountryCode)) return (false, "Unknown country");
        if (dto.OrganizerAreaId != null && !await IsKnownOrganizerAreaAsync(dto.OrganizerAreaId.Value)) return (false, "Unknown organizer area");
        if (dto.GameActivityId != null && !await IsKnownGameAsync(dto.GameActivityId.Value)) return (false, "Unknown scenario or board game");
        if (dto.ActivityLanguages != null && dto.ActivityLanguages.Any(l => !Enum.IsDefined(l))) return (false, "Unknown activity language");

        return (true, string.Empty);
    }

    private Task<bool> IsKnownCountryAsync(string code) => _db.Countries.AnyAsync(c => c.Code == code);

    private Task<bool> IsKnownOrganizerAreaAsync(int id) => _db.OrganizerAreas.AnyAsync(a => a.Id == id);

    // The participant page and the forms only offer scenarios and board games (Infosys ParticipantModel::getGames)
    private Task<bool> IsKnownGameAsync(int id) =>
        _db.Activities.AnyAsync(a => a.Id == id && (a.Type == ActivityType.BoardGame || a.Type == ActivityType.Scenario));

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
        participant.OrganizerAreaId = dto.OrganizerAreaId;
        participant.GameActivityId = dto.GameActivityId;
        if (dto.CountryCode != null) participant.CountryCode = dto.CountryCode;
        if (dto.ActivityLanguages is { Count: > 0 })
        {
            participant.ActivityLanguages = dto.ActivityLanguages.Distinct().Order().ToList();
        }
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
                    && await IsKnownCountryAsync(code))
                {
                    p.CountryCode = code;
                    return (true, string.Empty);
                }
                return (false, "Unknown country");

            // Infosys "Intet arrangørområde" clears it
            case "organizerAreaId":
                if (value.ValueKind == JsonValueKind.Null) { p.OrganizerAreaId = null; return (true, string.Empty); }
                if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var areaId)
                    && await IsKnownOrganizerAreaAsync(areaId))
                {
                    p.OrganizerAreaId = areaId;
                    return (true, string.Empty);
                }
                return (false, "Unknown organizer area");

            // Infosys "Intet scenarie/brætspil" clears it
            case "gameActivityId":
                if (value.ValueKind == JsonValueKind.Null) { p.GameActivityId = null; return (true, string.Empty); }
                if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var gameId)
                    && await IsKnownGameAsync(gameId))
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
                p.SleepingNote,
                // TODO when wear is ported: load the participant's orders here (see ParticipantWearOrderDto)
                WearOrders: null,
                // TODO when entrance is ported: load the participant's tickets here (see ParticipantTicketDto)
                Tickets: null,
                // TODO when food is ported: load the participant's meals here (see ParticipantMealDto)
                Meals: null,
                // TODO when sleeping places are ported: load them here (see ParticipantSleepingPlaceDto)
                SleepingPlaces: null,
                Activities: await LoadActivitiesAsync(p))
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
            // TODO when tickets, food, wear, activity signups and the signup's price are ported: calculate it here
            // (see ParticipantPriceDto)
            Price: null,
            details);
    }

    // The Aktiviteter box: Infosys Deltagere::getPladser (see ParticipantSpots.LoadAsync)
    private async Task<IReadOnlyList<ParticipantActivityDto>> LoadActivitiesAsync(Participant p) =>
        (await ParticipantSpots.LoadAsync(_db, p))
            .Select(s => new ParticipantActivityDto(
                s.ActivityId, s.Title, s.Start, s.End, s.Role, s.RunId, s.RunNumber, s.RoomName, s.RoomLocation))
            .ToList();

    private string? CurrentUserId() => User?.FindFirstValue(ClaimTypes.NameIdentifier);
}
