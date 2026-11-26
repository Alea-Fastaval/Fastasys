using Fastasys.ApiService.Data.Entities;

namespace Fastasys.ApiService.Data.Seed;

// The hero force task categories in Infosys's database (gdscategories), with their ids (agreed 2026-11-26)
public static class HeroSeed
{
    public static readonly HeroTaskCategory[] Categories =
    [
        new()
        {
            Id = 1,
            NameDanish = "Forplejning",
            NameEnglish = "Food-handling",
        },
        new()
        {
            Id = 2,
            NameDanish = "Service",
            NameEnglish = "Service",
        },
        new()
        {
            Id = 3,
            NameDanish = "Rengøring",
            NameEnglish = "Cleaning",
        },
        new()
        {
            Id = 4,
            NameDanish = "Manuelt arbejde",
            NameEnglish = "Manual labor",
        },
        new()
        {
            Id = 5,
            NameDanish = "Brandvagt",
            NameEnglish = "Night watch",
        },
        new()
        {
            Id = 6,
            NameDanish = "James (Otto-fest tjener)",
            NameEnglish = "James (Otto-celebration waiter)",
        },
        new()
        {
            Id = 7,
            NameDanish = "Ungdomslounge",
            NameEnglish = "Youth lounge",
        },
    ];
}
