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
}
