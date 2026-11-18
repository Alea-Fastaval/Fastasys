using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace Fastasys.ApiService.Data.Entities;

/// <summary>
/// An ID card template (Infosys: idtemplates): a background image with photo, text and barcode items placed on it.
/// The card is drawn at the background's size in pixels (Infosys prints at 300 DPI). Schema approved on 2026-11-18;
/// the background is stored here instead of in Infosys's public uploads folder.
/// </summary>
[Table("id_card_templates")]
public class IdCardTemplate
{
    public int Id { get; set; }

    [MaxLength(128)]
    public string Name { get; set; } = string.Empty;

    // PNG, JPEG or GIF; null until a background is uploaded
    public byte[]? Background { get; set; }

    [MaxLength(32)]
    public string? BackgroundContentType { get; set; }

    // The background's size in pixels, 0 without a background
    public int Width { get; set; }
    public int Height { get; set; }

    public virtual ICollection<IdCardTemplateItem> Items { get; set; } = new List<IdCardTemplateItem>();
}

/// <summary>An item on an ID card template (Infosys: idtemplates_items). Positions and sizes are in pixels.</summary>
[Table("id_card_template_items")]
public class IdCardTemplateItem
{
    public int Id { get; set; }

    public int TemplateId { get; set; }
    public virtual IdCardTemplate Template { get; set; } = null!;

    public IdCardItemType Type { get; set; }

    public int X { get; set; }
    public int Y { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }

    // Degrees, clockwise
    public int Rotation { get; set; }

    // What a text item shows; null for photos and barcodes
    public IdCardDataSource? DataSource { get; set; }
}

/// <summary>Infosys idtemplates_items.itemtype: photo, text, barcode.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<IdCardItemType>))]
public enum IdCardItemType
{
    Photo,
    Text,
    Barcode
}

/// <summary>Infosys idtemplates_items.datasource: id, name, doctorname, workarea, scenario, group.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<IdCardDataSource>))]
public enum IdCardDataSource
{
    // "ID"
    Id,
    // "Navn": the nickname, or else the first and last name (Deltagere::getName)
    Name,
    // "Dr. + Navn"
    DoctorName,
    // "Arbejdsområde": the organizer area
    OrganizerArea,
    // "Scenarie": the participant's own scenario or board game
    Game,
    // "Ungdomsskole"
    YouthClub
}

/// <summary>
/// A participant's ID card photo (Infosys: participantphotoidentifiers and the photo-original-* / photo-cropped-*
/// files in its public uploads folder). Schema approved on 2026-11-18: kept in the database, so only logged-in users
/// with access can fetch it. The original is kept so the photo can be cropped again.
/// </summary>
[Table("participant_photos")]
public class ParticipantPhoto
{
    [Key]
    public int ParticipantId { get; set; }
    public virtual Participant Participant { get; set; } = null!;

    public byte[] Original { get; set; } = [];

    [MaxLength(32)]
    public string OriginalContentType { get; set; } = string.Empty;

    // The cut-out used on the ID card, 213 x 295 pixels from the upload page
    public byte[] Cropped { get; set; } = [];

    [MaxLength(32)]
    public string CroppedContentType { get; set; } = string.Empty;

    public DateTime UpdatedAt { get; set; }
}
