using System.Security.Claims;
using Fastasys.ApiService.Data;
using Fastasys.ApiService.Data.Entities;
using Fastasys.ApiService.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Fastasys.ApiService.Controllers;

// A ticket type (Infosys indgang) with the number of participants who have it (Infosys "Antal solgt", which counts
// annulled participants too)
public record TicketTypeDto(
    int Id,
    string Name,
    int Price,
    DateOnly Day,
    TicketKind Kind,
    bool IsAllConvention,
    int SoldCount
);

// The fields of the Infosys create/edit form (indgang/createentry, indgang/editentry): Type, Pris and Start ("Dag"
// here), plus the fields that say what the type is (agreed 2026-11-24, replacing Infosys's words in the name)
public abstract record TicketTypeFieldsDto
{
    public string? Name { get; init; }
    public int? Price { get; init; }
    public DateOnly? Day { get; init; }
    public TicketKind? Kind { get; init; }
    public bool IsAllConvention { get; init; }
}

public record CreateTicketTypeDto : TicketTypeFieldsDto;

public record UpdateTicketTypeDto : TicketTypeFieldsDto;

// One heading of the Infosys statistics page (entry/stats): the ticket types with the same name, with the number of
// participants who have each. The page makes the heading from Kind and IsAllConvention (Indgang::getProperType).
public record TicketStatsGroupDto(
    string Name,
    TicketKind Kind,
    bool IsAllConvention,
    IReadOnlyList<TicketStatsDayDto> Days
);

// IsAllConvention: the page shows "Partout" instead of the day
public record TicketStatsDayDto(DateOnly Day, int Count, bool IsAllConvention);

// Infosys EntranceController (the Indgang menu): all ticket types, one type, create, edit, delete and statistics.
// Infosys lets superuser and Infonaut use all of it; here entrance_view may look and entrance_edit may change.
[ApiController]
[Route("api/ticket-types")]
[Authorize(Policy = "TicketTypeView")]
public class TicketTypesController(
    InfosysDbContext db,
    IOptions<ConventionOptions> convention,
    ILogger<TicketTypesController> logger
) : ControllerBase
{
    private const string NotFoundMessage = "Ticket type not found";

    // The reason tells the page which message to show
    private static readonly object InUse = new
    {
        message = "Participants have the ticket type, so it can't be deleted",
        reason = "inUse",
    };

    // Infosys indgang/entrytypes ("Alle indgangs-typer"): sorted by name with strcmp (byte-wise, case-sensitive) and
    // ties in id order. Ordinal comparison gives the same order without depending on the database collation.
    [HttpGet]
    public async Task<ActionResult<IEnumerable<TicketTypeDto>>> GetAll()
    {
        var types = await db.TicketTypes.AsNoTracking().ToListAsync();
        var counts = await SoldCountsAsync();

        var sorted = types
            .OrderBy(t => t.Name, StringComparer.Ordinal)
            .ThenBy(t => t.Id)
            .Select(t => ToDto(t, counts))
            .ToList();

        return Ok(sorted);
    }

    // Infosys indgang/entry/:id:
    [HttpGet("{id:int}")]
    public async Task<ActionResult<TicketTypeDto>> Get(int id)
    {
        var type = await db.TicketTypes.AsNoTracking().FirstOrDefaultAsync(t => t.Id == id);
        if (type == null)
            return NotFound(new { message = NotFoundMessage });

        var count = await db.ParticipantTickets.CountAsync(t => t.TicketTypeId == id);
        return Ok(ToDto(type, new Dictionary<int, int> { [id] = count }));
    }

    // Infosys entry/stats ("Statistik")
    [HttpGet("stats")]
    public async Task<ActionResult<IEnumerable<TicketStatsGroupDto>>> GetStats()
    {
        var types = await db.TicketTypes.AsNoTracking().OrderBy(t => t.Id).ToListAsync();
        var counts = await SoldCountsAsync();
        return Ok(ToStats(types, counts));
    }

    // Infosys indgang/createentry ("Opret indgangs-type")
    [HttpPost]
    [Authorize(Policy = "TicketTypeManagement")]
    public async Task<ActionResult<TicketTypeDto>> Create([FromBody] CreateTicketTypeDto dto)
    {
        var (isValid, error) = Validate(dto, convention.Value);
        if (!isValid)
            return BadRequest(new { message = error });

        var type = new TicketType();
        Apply(type, dto, DayToStore(dto, convention.Value)!.Value);

        db.TicketTypes.Add(type);
        await db.SaveChangesAsync();

        logger.LogInformation("Ticket type {TicketTypeId} created by user {UserId}", type.Id, CurrentUserId());

        return CreatedAtAction(nameof(Get), new { id = type.Id }, ToDto(type, new Dictionary<int, int>()));
    }

    // Infosys indgang/editentry/:id: ("Opdater")
    [HttpPut("{id:int}")]
    [Authorize(Policy = "TicketTypeManagement")]
    public async Task<ActionResult<TicketTypeDto>> Update(int id, [FromBody] UpdateTicketTypeDto dto)
    {
        var type = await db.TicketTypes.FindAsync(id);
        if (type == null)
            return NotFound(new { message = NotFoundMessage });

        var (isValid, error) = Validate(dto, convention.Value, type.Day);
        if (!isValid)
            return BadRequest(new { message = error });

        Apply(type, dto, DayToStore(dto, convention.Value, type.Day)!.Value);
        await db.SaveChangesAsync();

        logger.LogInformation("Ticket type {TicketTypeId} updated by user {UserId}", id, CurrentUserId());

        var count = await db.ParticipantTickets.CountAsync(t => t.TicketTypeId == id);
        return Ok(ToDto(type, new Dictionary<int, int> { [id] = count }));
    }

    // Infosys indgang/deleteentry/:id:. Infosys's foreign key from deltagere_indgang refuses to delete a type that
    // participants have, which showed an error page; it is refused with a reason instead (agreed 2026-11-24).
    [HttpDelete("{id:int}")]
    [Authorize(Policy = "TicketTypeManagement")]
    public async Task<IActionResult> Delete(int id)
    {
        var type = await db.TicketTypes.FindAsync(id);
        if (type == null)
            return NotFound(new { message = NotFoundMessage });

        if (await db.ParticipantTickets.AnyAsync(t => t.TicketTypeId == id))
            return Conflict(InUse);

        db.TicketTypes.Remove(type);
        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // A participant got the type at the same time (the foreign key is RESTRICT)
            return Conflict(InUse);
        }

        logger.LogInformation("Ticket type {TicketTypeId} deleted by user {UserId}", id, CurrentUserId());

        return Ok(new { message = "Ticket type deleted successfully" });
    }

    // Infosys EntranceModel::getEntryStats and entrystats.phtml. Infosys keys the types by name and then by start
    // ($return[$type][$start] = $entry): the headings come in the order their name first appears (id order), the days
    // in the order they first appear under the heading, and a later type with the same name and day takes the earlier
    // one's place. A heading is made from its first day's type.
    public static IReadOnlyList<TicketStatsGroupDto> ToStats(
        IReadOnlyList<TicketType> typesInIdOrder,
        IReadOnlyDictionary<int, int> soldCounts
    )
    {
        var names = new List<string>();
        var groups = new Dictionary<string, List<TicketType>>(StringComparer.Ordinal);
        foreach (var type in typesInIdOrder)
        {
            if (!groups.TryGetValue(type.Name, out var days))
            {
                days = [];
                groups[type.Name] = days;
                names.Add(type.Name);
            }

            var index = days.FindIndex(t => t.Day == type.Day);
            if (index >= 0)
                days[index] = type;
            else
                days.Add(type);
        }

        return names
            .Select(name =>
            {
                var days = groups[name];
                return new TicketStatsGroupDto(
                    name,
                    days[0].Kind,
                    days[0].IsAllConvention,
                    days.Select(t => new TicketStatsDayDto(
                            t.Day,
                            soldCounts.GetValueOrDefault(t.Id),
                            t.IsAllConvention
                        ))
                        .ToList()
                );
            })
            .ToList();
    }

    // Returns whether the form fields are valid and, if not, why. Invalid input is expected, so it never throws.
    // currentDay is the day of the type being updated.
    public static (bool IsValid, string Error) Validate(
        TicketTypeFieldsDto dto,
        ConventionOptions convention,
        DateOnly? currentDay = null
    )
    {
        // Infosys refuses a type that PHP's empty() calls empty, which includes "0"
        if (string.IsNullOrEmpty(dto.Name) || dto.Name == "0")
            return (false, "Type is required");
        if (dto.Name.Length > TicketType.MaxNameLength)
            return (false, $"Type can be at most {TicketType.MaxNameLength} characters");

        // Infosys's create form takes digits only (ctype_digit): whole kroner, 0 or more. Its edit form took anything
        // and stored text or an empty price as 0; it is checked like create (fixed 2026-11-24).
        if (dto.Price is null or < 0)
            return (false, "Price must be a whole number of 0 or more");

        // Infosys turned an empty or unreadable start into 1 January 1970 (fixed 2026-11-24)
        if (DayToStore(dto, convention, currentDay) is not { } day)
        {
            return dto.IsAllConvention
                ? (false, "The convention's first day is not configured")
                : (false, "Day is required");
        }

        // The day is picked from the convention's days (agreed 2026-11-25; Infosys's date picker only suggested them).
        // A type keeps a day outside them when it isn't changed. Without the dates in the configuration it can't be
        // checked.
        if (
            !dto.IsAllConvention
            && convention.Start is { } start
            && convention.End is { } end
            && (day < start || day > end)
            && day != currentDay
        )
        {
            return (false, "Day must be one of the convention's days");
        }

        if (dto.Kind is not { } kind || !Enum.IsDefined(kind))
            return (false, "Unknown kind");

        return (true, string.Empty);
    }

    // The day to store. A partout type (for the whole convention) has no day of its own: it gets the convention's first
    // day, whatever was sent, like Infosys's partout types had (agreed 2026-11-25; the form hides "Dag" for it). Without
    // that date in the configuration it keeps its day, or takes the one sent. null when there is no day to store.
    public static DateOnly? DayToStore(
        TicketTypeFieldsDto dto,
        ConventionOptions convention,
        DateOnly? currentDay = null
    ) => dto.IsAllConvention ? convention.Start ?? currentDay ?? dto.Day : dto.Day;

    private static void Apply(TicketType type, TicketTypeFieldsDto dto, DateOnly day)
    {
        type.Name = dto.Name!;
        type.Price = dto.Price!.Value;
        type.Day = day;
        type.Kind = dto.Kind!.Value;
        type.IsAllConvention = dto.IsAllConvention;
    }

    // How many participants have each type (Infosys EntranceModel::getNumbersSold, Indgang::getParticipantCount)
    private async Task<Dictionary<int, int>> SoldCountsAsync() =>
        await db
            .ParticipantTickets.GroupBy(t => t.TicketTypeId)
            .Select(g => new { TicketTypeId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.TicketTypeId, g => g.Count);

    private static TicketTypeDto ToDto(TicketType t, IReadOnlyDictionary<int, int> soldCounts) =>
        new(t.Id, t.Name, t.Price, t.Day, t.Kind, t.IsAllConvention, soldCounts.GetValueOrDefault(t.Id));

    private string? CurrentUserId() => User?.FindFirstValue(ClaimTypes.NameIdentifier);
}
