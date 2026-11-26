using Fastasys.ApiService.Data.Entities;

namespace Fastasys.ApiService.Services;

/// <summary>The rules of when a participant can help the hero force (Infosys DeltagereGDSTilmeldinger).</summary>
public static class HeroAvailabilityRules
{
    // The hours Infosys stores after the date in period, e.g. "2027-03-24 7-12"
    public static string HoursText(HeroPeriod period) =>
        period switch
        {
            HeroPeriod.Morning => "7-12",
            HeroPeriod.Afternoon => "12-17",
            HeroPeriod.Evening => "17-24",
            _ => throw new ArgumentOutOfRangeException(nameof(period), period, "Unknown hero period"),
        };

    // By day and time of day, then any category before the categories by id. Infosys has no ORDER BY and got its
    // primary key's text order, which put "12-17" and "17-24" before "7-12" (fixed 2026-11-26).
    public static IOrderedEnumerable<T> Order<T>(
        IEnumerable<T> rows,
        Func<T, DateOnly> date,
        Func<T, HeroPeriod> period,
        Func<T, int?> categoryId
    ) => rows.OrderBy(date).ThenBy(period).ThenBy(r => categoryId(r) == null ? 0 : 1).ThenBy(r => categoryId(r) ?? 0);
}
