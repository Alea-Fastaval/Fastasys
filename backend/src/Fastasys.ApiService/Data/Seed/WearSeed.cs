using Fastasys.ApiService.Data.Entities;

namespace Fastasys.ApiService.Data.Seed;

// The sizes Infosys's migration 1034-wear-variants.sql created, in its order (agreed 2026-11-26)
public static class WearSeed
{
    private static readonly (string Danish, string English)[] SizeNames =
    [
        ("Baby 56(0-1 md)", "Baby 56(0-1 mo)"),
        ("Baby 62(2-3 md)", "Baby 62(2-3 mo)"),
        ("Baby 68(3-6 md)", "Baby 68(3-6 mo)"),
        ("Baby 74(6-9 md)", "Baby 74(6-9 mo)"),
        ("Baby 80(9-12 md)", "Baby 80(9-12 mo)"),
        ("Baby 86/92(1-2 år)", "Baby 86/92(1-2 yr)"),
        ("Barn 98/104(3-4 år)", "Child 98/104(3-4 yr)"),
        ("Barn 110/116(5-6 år)", "Child 110/116(5-6 yr)"),
        ("Barn 122/128(7-8 år)", "Child 122/128(7-8 yr)"),
        ("Barn 134/146(9-11 år)", "Child 134/146(9-11 yr)"),
        ("Barn 152/164(12-14 år)", "Child 152/164(12-14 yr)"),
        ("S", "S"),
        ("M", "M"),
        ("L", "L"),
        ("XL", "XL"),
        ("XXL", "XXL"),
        ("3XL", "3XL"),
        ("4XL", "4XL"),
        ("5XL", "5XL"),
    ];

    public static readonly WearAttribute[] Sizes =
    [
        .. SizeNames.Select(
            (name, index) =>
                new WearAttribute
                {
                    Id = index + 1,
                    Kind = WearAttributeKind.Size,
                    NameDanish = name.Danish,
                    NameEnglish = name.English,
                    Position = index + 1,
                }
        ),
    ];
}
