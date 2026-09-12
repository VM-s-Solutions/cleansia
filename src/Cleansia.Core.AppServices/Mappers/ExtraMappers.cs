using Cleansia.Core.AppServices.Features.Extras.DTOs;
using Cleansia.Core.Domain.Orders;

namespace Cleansia.Core.AppServices.Mappers;

public static class ExtraMappers
{
    /// <summary>
    /// <paramref name="price"/> is passed in rather than read off the entity — see
    /// <see cref="ServiceMappers"/> for the rule and why the DTO field name does not move.
    /// </summary>
    public static ExtraListItem MapToDto(this Extra extra, decimal price)
    {
        return new ExtraListItem(
            Id: extra.Id,
            Slug: extra.Slug,
            Name: extra.Name,
            Description: extra.Description,
            Price: price,
            DisplayOrder: extra.DisplayOrder,
            Translations: extra.Translations.ToDictionary());
    }

    /// <summary>Every currency's row, keyed by code. See ServiceMappers.MapToAdminDetail.</summary>
    public static AdminExtraDetailDto MapToAdminDetail(this Extra extra, Dictionary<string, decimal> prices)
    {
        return new AdminExtraDetailDto(
            Id: extra.Id,
            Slug: extra.Slug,
            Name: extra.Name,
            Description: extra.Description,
            DisplayOrder: extra.DisplayOrder,
            Prices: prices,
            Translations: extra.Translations.ToDictionary(),
            CreatedOn: extra.CreatedOn,
            UpdatedOn: extra.UpdatedOn);
    }
}
