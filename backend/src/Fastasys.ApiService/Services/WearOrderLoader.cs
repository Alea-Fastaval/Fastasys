using Fastasys.ApiService.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Fastasys.ApiService.Services;

/// <summary>Loads wear orders with what their texts need: the wear type, the price and the attributes.</summary>
public static class WearOrderLoader
{
    public sealed record LoadedOrder(
        int Id,
        int ParticipantId,
        int WearTypeId,
        string WearTypeName,
        int WearPriceId,
        string PriceCategoryName,
        int UnitPrice,
        int Amount,
        bool IsReceived,
        IReadOnlyList<WearRules.Attribute> Attributes
    )
    {
        // DeltagereWear::getWearName('da', false)
        public string Name => WearRules.OrderName(WearTypeName, Attributes);

        // DeltagereWear::getSizeName, or null without a size
        public string? Size => WearRules.SizeName(Attributes);
    }

    // The orders in id order; Infosys reads them without ORDER BY
    public static async Task<List<LoadedOrder>> LoadAsync(IQueryable<ParticipantWearOrder> orders) =>
        await orders
            .AsNoTracking()
            .OrderBy(o => o.Id)
            .Select(o => new LoadedOrder(
                o.Id,
                o.ParticipantId,
                o.WearPrice.WearTypeId,
                o.WearPrice.WearType.NameDanish,
                o.WearPriceId,
                o.WearPrice.ParticipantCategory.Name,
                o.WearPrice.Price,
                o.Amount,
                o.IsReceived,
                o.Attributes.Select(a => new WearRules.Attribute(
                        a.Attribute.Id,
                        a.Attribute.Kind,
                        a.Attribute.NameDanish,
                        a.Attribute.Position
                    ))
                    .ToList()
            ))
            .ToListAsync();
}
