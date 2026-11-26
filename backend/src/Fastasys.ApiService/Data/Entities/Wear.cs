using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace Fastasys.ApiService.Data.Entities;

/// <summary>
/// A wear type (Infosys: wear), e.g. "T-shirt", with a price per participant category and the variants it comes in.
/// The schema was approved on 2026-11-26.
/// </summary>
[Table("wear_types")]
public class WearType
{
    public const int MaxNameLength = 64;

    public int Id { get; set; }

    // Infosys: navn, varchar(64)
    [MaxLength(MaxNameLength)]
    public string NameDanish { get; set; } = string.Empty;

    // Infosys: title_en, varchar(64) ("Engelsk titel")
    [MaxLength(MaxNameLength)]
    public string NameEnglish { get; set; } = string.Empty;

    // Infosys: beskrivelse / description_en, text
    public string DescriptionDanish { get; set; } = string.Empty;
    public string DescriptionEnglish { get; set; } = string.Empty;

    // Infosys: position, the order of the wear types in the lists and the signup ("Rækkefølge")
    public int Position { get; set; }

    // Infosys: max_order ("Maks bestilling"), the most a participant may order in the signup; 0 means the signup's
    // default of 10 (SignupApiModel)
    public int MaxOrder { get; set; }

    // Infosys: is_required ("Påkrævet"), used by the signup
    public bool IsRequired { get; set; }

    public virtual ICollection<WearPrice> Prices { get; set; } = new List<WearPrice>();
    public virtual ICollection<WearVariantAttribute> VariantAttributes { get; set; } = new List<WearVariantAttribute>();
    public virtual ICollection<WearImageLink> ImageLinks { get; set; } = new List<WearImageLink>();
}

/// <summary>
/// The price of a wear type for a participant category (Infosys: wearpriser), at most one per category. An order keeps
/// the price it was made with, so its price doesn't change when the participant's category does.
/// </summary>
[Table("wear_prices")]
public class WearPrice
{
    public int Id { get; set; }

    public int WearTypeId { get; set; }
    public virtual WearType WearType { get; set; } = null!;

    // Infosys: brugerkategori_id
    public int ParticipantCategoryId { get; set; }
    public virtual ParticipantCategory ParticipantCategory { get; set; } = null!;

    // Infosys: pris, whole DKK
    public int Price { get; set; }
}

/// <summary>
/// The kinds of wear attributes (Infosys wear_attributes.attribute_type). Infosys let staff add other kinds, but it
/// only ever showed these four, in this order (Wear::getAttributeOrder); the list was fixed on 2026-11-26.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<WearAttributeKind>))]
public enum WearAttributeKind
{
    Model,
    Design,
    Color,
    Size,
}

/// <summary>A value a wear type can come in, e.g. the size "M" or the color "Sort" (Infosys: wear_attributes).</summary>
[Table("wear_attributes")]
public class WearAttribute
{
    public const int MaxNameLength = 64;

    // Ids 1-19 are the sizes Infosys's migration 1034 created; new attributes start at 20
    public const int FirstNewId = 20;

    public int Id { get; set; }

    // Infosys: attribute_type
    public WearAttributeKind Kind { get; set; }

    // Infosys: desc_da / desc_en ("Dansk" / "Engelsk")
    [MaxLength(MaxNameLength)]
    public string NameDanish { get; set; } = string.Empty;

    [MaxLength(MaxNameLength)]
    public string NameEnglish { get; set; } = string.Empty;

    // Infosys: position, the order within the kind
    public int Position { get; set; }
}

/// <summary>
/// An attribute a variant of a wear type comes in (Infosys: wear_attribute_available). A variant is a set of attributes;
/// an order picks one attribute of each kind in one variant.
/// </summary>
[Table("wear_variant_attributes")]
public class WearVariantAttribute
{
    public int WearTypeId { get; set; }
    public virtual WearType WearType { get; set; } = null!;

    // Infosys: variant, numbered from 0
    public int Variant { get; set; }

    public int AttributeId { get; set; }
    public virtual WearAttribute Attribute { get; set; } = null!;
}

/// <summary>A participant's order of a wear type (Infosys: deltagere_wear_order).</summary>
[Table("participant_wear_orders")]
public class ParticipantWearOrder
{
    public int Id { get; set; }

    public int ParticipantId { get; set; }
    public virtual Participant Participant { get; set; } = null!;

    // Infosys: wearpris_id, the wear type and the category price it was ordered at
    public int WearPriceId { get; set; }
    public virtual WearPrice WearPrice { get; set; } = null!;

    // Infosys: antal
    public int Amount { get; set; }

    // Infosys: received ("Udleveret")
    public bool IsReceived { get; set; }

    public virtual ICollection<ParticipantWearOrderAttribute> Attributes { get; set; } =
        new List<ParticipantWearOrderAttribute>();
}

/// <summary>An attribute of an order, e.g. its size (Infosys: deltagere_wear_order_attributes).</summary>
[Table("participant_wear_order_attributes")]
public class ParticipantWearOrderAttribute
{
    public int OrderId { get; set; }
    public virtual ParticipantWearOrder Order { get; set; } = null!;

    public int AttributeId { get; set; }
    public virtual WearAttribute Attribute { get; set; } = null!;
}

/// <summary>
/// An uploaded wear image (Infosys: wear_image, a file in its public uploads folder). Stored in the database behind
/// login, like the ID photos (agreed 2026-11-26). Only the signup shows them.
/// </summary>
[Table("wear_images")]
public class WearImage
{
    public const int MaxFileNameLength = 255;

    public int Id { get; set; }

    // The uploaded file's name; Infosys refused a second file with the same name
    [MaxLength(MaxFileNameLength)]
    public string FileName { get; set; } = string.Empty;

    [MaxLength(100)]
    public string ContentType { get; set; } = string.Empty;

    public byte[] Data { get; set; } = [];
}

/// <summary>
/// An image shown for a wear type, for one of its attributes or for all of them (Infosys: wear_image_connection, where
/// "Alle" was a special attribute row).
/// </summary>
[Table("wear_image_links")]
public class WearImageLink
{
    public int Id { get; set; }

    public int WearImageId { get; set; }
    public virtual WearImage WearImage { get; set; } = null!;

    public int WearTypeId { get; set; }
    public virtual WearType WearType { get; set; } = null!;

    // null means "Alle"
    public int? AttributeId { get; set; }
    public virtual WearAttribute? Attribute { get; set; }
}
