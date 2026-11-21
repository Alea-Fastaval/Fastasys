using Fastasys.ApiService.Data.Entities;

namespace Fastasys.ApiService.Data.Seed;

// The participant categories and organizer areas the user gave on 2026-11-11.
public static class ParticipantLookupSeed
{
    // Only Arrangør is an organizer category, as in Infosys after migration 1035
    public static readonly ParticipantCategory[] Categories =
    [
        new() { Id = ParticipantCategory.ParticipantId, Name = "Deltager" },
        new() { Id = ParticipantCategory.OrganizerId, Name = "Arrangør", IsOrganizer = true },
        new() { Id = 3, Name = "Junior" },
        new() { Id = 4, Name = "Forældre" },
        new() { Id = 5, Name = "Freeloader" },
    ];

    // 1 and 2 keep the ids Infosys code depends on; the rest follow in the order the user listed them.
    // English names were not given.
    private static readonly string[] OtherAreas =
    [
        "Arrangørkærlighed",
        "Arrangørmessen",
        "AV & Tech",
        "Bar",
        "Brætspilscafé",
        "Brætspilsdesignansvarlig",
        "Opsætning/nedpilning",
        "Butikker",
        "DirtBuster",
        "Brætspilsdommer",
        "Scenariedommer",
        "Fastafamily",
        "FastaQuests",
        "Fastaval Junior",
        "FTTF (Fastaval-translator-task-force)",
        "Game Rush",
        "TV",
        "Heltestyrken",
        "Holdlægning",
        "Hovedarrangørgruppe",
        "Indkøb & Transport",
        "Infoen",
        "International",
        "IT",
        "Kaffekro",
        "Kiosk",
        "Kommunikation",
        "Køkken",
        "Levnedsmiddel",
        "Madudlevering",
        "Miljø/affaldshåndtering",
        "Morgenmad",
        "Økonomi",
        "Opvask",
        "Otto-produktion",
        "Ottofest",
        "Ottos stue",
        "Receptioner",
        "Regelformidleransvarlig",
        "Scenarieansvarlig",
        "Sildesalat",
        "SoMe",
        "Spillederansvarlig",
        "Spilstart (scenarier + brætspil)",
        "Streaming (under Fastaval)",
        "Streaming (under Ottoshow)",
        "Super Søndag",
        "Synopseakademiet",
        "Tema og Wear",
        "Tryghed & Tryghedsvært",
        "Ungdom og Efterskolerne",
        "Æresgæst",
        "Æresotto",
        "Grafik",
    ];

    public static readonly OrganizerArea[] OrganizerAreas =
    [
        new() { Id = OrganizerArea.ScenarioAuthorId, NameDanish = "Scenarieforfatter" },
        new() { Id = OrganizerArea.BoardGameDesignerId, NameDanish = "Brætspilsdesigner" },
        .. OtherAreas.Select((name, index) => new OrganizerArea { Id = index + 3, NameDanish = name }),
    ];
}
