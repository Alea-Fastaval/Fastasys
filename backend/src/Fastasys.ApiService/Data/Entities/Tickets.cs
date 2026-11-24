using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace Fastasys.ApiService.Data.Entities;

/// <summary>
/// A ticket type (Infosys: indgang): an entrance, a night's sleep, an Alea membership, or anything else a participant
/// can buy, such as a fee, the party or a mattress. Infosys worked out what a type is from words in its name; here
/// Kind, IsAllConvention and IsDigital say it, and the name is only a label (agreed 2026-11-24).
/// </summary>
[Table("ticket_types")]
public class TicketType
{
    public const int MaxNameLength = 64;

    public int Id { get; set; }

    // Infosys: type, varchar(64)
    [MaxLength(MaxNameLength)]
    public string Name { get; set; } = string.Empty;

    // Infosys: pris, whole DKK
    public int Price { get; set; }

    // Infosys: start. Infosys stores a date and time, but only the day is ever used, so only the day is kept
    // (agreed 2026-11-24).
    public DateOnly Day { get; set; }

    public TicketKind Kind { get; set; }

    // "Partout": the ticket is for the whole convention, not only for Day. Infosys: "partout", "fastafamily", "alle"
    // or "leje" in the name (Indgang::spansAllConvention).
    public bool IsAllConvention { get; set; }

    // Infosys: "digital" in the name (Indgang::isDigital)
    public bool IsDigital { get; set; }
}

/// <summary>What a ticket type is. More kinds come with the features that need them (the signup, payments).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<TicketKind>))]
public enum TicketKind
{
    // Infosys: "indgang" or "Dagsbillet" in the name (Indgang::isEntrance)
    Entrance,

    // Infosys: "overnatning" in the name (Indgang::isSleepTicket)
    Sleeping,

    // Infosys: "medlemskab" in the name (Indgang::isAleaMembership)
    AleaMembership,

    // Everything else: fees, the party, support, a mattress and the like
    Other,
}

/// <summary>A ticket a participant has (Infosys: deltagere_indgang). A participant has a ticket type at most once.</summary>
[Table("participant_tickets")]
public class ParticipantTicket
{
    public int ParticipantId { get; set; }
    public virtual Participant Participant { get; set; } = null!;

    public int TicketTypeId { get; set; }
    public virtual TicketType TicketType { get; set; } = null!;
}
