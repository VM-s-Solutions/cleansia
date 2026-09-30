using Cleansia.Core.AppServices.Features.SavedCards.DTOs;
using Cleansia.Core.Domain.Users;

namespace Cleansia.Core.AppServices.Features.SavedCards.Mappers;

public static class SavedCardMapper
{
    public static SavedCardDto MapToDto(this SavedCard card) =>
        new(
            Id: card.Id,
            Brand: card.Brand!,
            Last4: card.Last4!,
            ExpMonth: card.ExpMonth!.Value,
            ExpYear: card.ExpYear!.Value,
            CurrencyCode: card.Currency!.Code);
}
