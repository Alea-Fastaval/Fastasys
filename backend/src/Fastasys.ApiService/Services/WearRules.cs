using Fastasys.ApiService.Data.Entities;

namespace Fastasys.ApiService.Services;

/// <summary>
/// The wear rules Infosys spreads over Wear, DeltagereWear and WearModel: an order's texts, whether an order's
/// attributes fit the wear type's variants, and the counts of Bestillings-oversigt.
/// </summary>
public static class WearRules
{
    // The order Infosys shows the attribute kinds in (Wear::getAttributeOrder): model, design, color, then size
    public static readonly WearAttributeKind[] NonSizeKinds =
    [
        WearAttributeKind.Model,
        WearAttributeKind.Design,
        WearAttributeKind.Color,
    ];

    // An attribute as the rules need it
    public sealed record Attribute(int Id, WearAttributeKind Kind, string NameDanish, int Position);

    // An attribute of a wear type's variant (Infosys wear_attribute_available)
    public sealed record VariantAttribute(int Variant, Attribute Attribute);

    // An order counted in Bestillings-oversigt
    public sealed record Order(int Id, int WearTypeId, int Amount, IReadOnlyList<Attribute> Attributes);

    // A wear type in Bestillings-oversigt, with its variants
    public sealed record WearTypeVariants(int Id, string Name, IReadOnlyList<VariantAttribute> VariantAttributes);

    // A row of Bestillings-oversigt: the wear type and its model/design/color, the total, the amount without a size and
    // the amount per size attribute id. StartsVariant marks the first row of each variant (Infosys's 'first' class).
    public sealed record BreakdownRow(
        int WearTypeId,
        string WearTypeName,
        IReadOnlyList<Attribute> Attributes,
        bool StartsVariant,
        int Total,
        int NoSize,
        IReadOnlyDictionary<int, int> BySize
    );

    // DeltagereWear::getAttributes(false): the attributes that aren't a size, one per kind, in the order model, design,
    // color. Infosys reads them by position and keeps the last one of each kind.
    public static IReadOnlyList<Attribute> NonSize(IEnumerable<Attribute> attributes)
    {
        var byKind = attributes
            .Where(a => a.Kind != WearAttributeKind.Size)
            .OrderBy(a => a.Position)
            .ThenBy(a => a.Id)
            .GroupBy(a => a.Kind)
            .ToDictionary(g => g.Key, g => g.Last());
        return NonSizeKinds.Where(byKind.ContainsKey).Select(k => byKind[k]).ToList();
    }

    // DeltagereWear::getWearName('da', false): the wear type's name, then "-" and the Danish name of each attribute
    // that isn't a size, e.g. "T-shirt-Sort"
    public static string OrderName(string wearName, IEnumerable<Attribute> attributes) =>
        wearName + string.Concat(NonSize(attributes).Select(a => "-" + a.NameDanish));

    // DeltagereWear::getSizeName: the Danish name of the order's size, or null when it has none
    public static string? SizeName(IEnumerable<Attribute> attributes) =>
        attributes
            .Where(a => a.Kind == WearAttributeKind.Size)
            .OrderBy(a => a.Position)
            .ThenBy(a => a.Id)
            .LastOrDefault()
            ?.NameDanish;

    // Whether an order's attributes are one choice in one of the wear type's variants: exactly one attribute of each
    // kind the variant has, all of them in that variant. A wear type without variants takes no attributes. Infosys only
    // limited the choice in the browser and saved whatever it was sent (fixed 2026-11-26).
    public static bool MatchesVariant(
        IReadOnlyCollection<Attribute> chosen,
        IReadOnlyCollection<VariantAttribute> variants
    )
    {
        if (variants.Count == 0)
            return chosen.Count == 0;
        if (chosen.Select(a => a.Id).Distinct().Count() != chosen.Count)
            return false;

        return variants
            .GroupBy(v => v.Variant)
            .Any(variant =>
            {
                var kinds = variant.Select(v => v.Attribute.Kind).Distinct().ToList();
                return chosen.Count == kinds.Count
                    && kinds.All(kind => chosen.Count(a => a.Kind == kind) == 1)
                    && chosen.All(a => variant.Any(v => v.Attribute.Id == a.Id));
            });
    }

    // WearModel::getWearBreakdown ("Bestillings-oversigt"). Each wear type, in the order given, gets a row per
    // combination of model, design and color in each of its variants (one row without attributes when it has no
    // variants or a variant has only sizes); a row counts the orders with that combination, per size. Like Infosys, an
    // order is counted on the first row with its combination, so a combination in two variants shows 0 on the second.
    // Infosys left out orders whose combination isn't in any variant any more (they were only in an HTML comment);
    // they get rows of their own after the wear type's rows (fixed 2026-11-26).
    public static IReadOnlyList<BreakdownRow> Breakdown(
        IReadOnlyList<WearTypeVariants> wearTypes,
        IReadOnlyList<Order> orders
    )
    {
        var pool = orders
            .OrderBy(o => o.Id)
            .GroupBy(o => Key(o.WearTypeId, NonSize(o.Attributes)))
            .ToDictionary(g => g.Key, g => g.ToList());
        var rows = new List<BreakdownRow>();

        foreach (var wearType in wearTypes)
        {
            var variants = wearType
                .VariantAttributes.GroupBy(v => v.Variant)
                .OrderBy(g => g.Key)
                .Select(g => g.Select(v => v.Attribute).ToList())
                .ToList();
            if (variants.Count == 0)
                variants.Add([]);

            foreach (var variant in variants)
            {
                var startsVariant = true;
                foreach (var combination in Combinations(variant))
                {
                    var key = Key(wearType.Id, combination);
                    var matching = pool.Remove(key, out var found) ? found : [];
                    rows.Add(Row(wearType, combination, startsVariant, matching));
                    startsVariant = false;
                }
            }

            var leftOver = pool.Where(p => p.Value[0].WearTypeId == wearType.Id).OrderBy(p => p.Value[0].Id).ToList();
            var startsLeftOver = true;
            foreach (var (key, matching) in leftOver)
            {
                pool.Remove(key);
                rows.Add(Row(wearType, NonSize(matching[0].Attributes), startsLeftOver, matching));
                startsLeftOver = false;
            }
        }

        return rows;
    }

    // WearModel::createVariantCombinations: every combination of the variant's models, designs and colors, models
    // outermost, each kind's attributes by position
    private static List<IReadOnlyList<Attribute>> Combinations(IReadOnlyList<Attribute> variant)
    {
        var combinations = new List<IReadOnlyList<Attribute>> { new List<Attribute>() };
        for (var k = NonSizeKinds.Length - 1; k >= 0; k--)
        {
            var kind = NonSizeKinds[k];
            var values = variant.Where(a => a.Kind == kind).OrderBy(a => a.Position).ThenBy(a => a.Id).ToList();
            if (values.Count == 0)
                continue;

            combinations = values
                .SelectMany(value =>
                    combinations.Select(rest =>
                        (IReadOnlyList<Attribute>)
                            new List<Attribute> { value }
                                .Concat(rest)
                                .ToList()
                    )
                )
                .ToList();
        }

        return combinations;
    }

    private static BreakdownRow Row(
        WearTypeVariants wearType,
        IReadOnlyList<Attribute> attributes,
        bool startsVariant,
        IReadOnlyList<Order> orders
    )
    {
        var bySize = new Dictionary<int, int>();
        var noSize = 0;
        foreach (var order in orders)
        {
            var size = order
                .Attributes.Where(a => a.Kind == WearAttributeKind.Size)
                .OrderBy(a => a.Position)
                .ThenBy(a => a.Id)
                .LastOrDefault();
            if (size == null)
                noSize += order.Amount;
            else
                bySize[size.Id] = bySize.GetValueOrDefault(size.Id) + order.Amount;
        }

        return new BreakdownRow(
            wearType.Id,
            wearType.Name,
            attributes,
            startsVariant,
            orders.Sum(o => o.Amount),
            noSize,
            bySize
        );
    }

    private static string Key(int wearTypeId, IEnumerable<Attribute> nonSizeAttributes) =>
        wearTypeId + ":" + string.Join(",", nonSizeAttributes.Select(a => a.Id));
}
