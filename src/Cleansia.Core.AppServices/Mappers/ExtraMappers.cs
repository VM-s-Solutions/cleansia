using Cleansia.Core.AppServices.Features.Extras.DTOs;
using Cleansia.Core.Domain.Orders;

namespace Cleansia.Core.AppServices.Mappers;

public static class ExtraMappers
{
    public static ExtraListItem MapToDto(this Extra extra)
    {
        return new ExtraListItem(
            Id: extra.Id,
            Slug: extra.Slug,
            Name: extra.Name,
            Description: extra.Description,
            Price: extra.Price,
            DisplayOrder: extra.DisplayOrder,
            Translations: extra.Translations.ToDictionary());
    }
}
