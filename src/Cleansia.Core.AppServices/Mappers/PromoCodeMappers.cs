using Cleansia.Core.AppServices.Features.PromoCodes.Admin.DTOs;
using Cleansia.Core.Domain.Loyalty;

namespace Cleansia.Core.AppServices.Mappers;

public static class PromoCodeMappers
{
    public static PromoCodeListItem MapToListItem(this PromoCode promoCode)
    {
        return new PromoCodeListItem(
            Id: promoCode.Id,
            Code: promoCode.Code,
            Type: promoCode.Type,
            DiscountPercent: promoCode.DiscountPercent,
            DiscountAmount: promoCode.DiscountAmount,
            CurrencyId: promoCode.CurrencyId,
            CurrencyCode: promoCode.Currency != null ? promoCode.Currency.Code : null,
            MinimumOrderAmount: promoCode.MinimumOrderAmount,
            MaxRedemptionsPerUser: promoCode.MaxRedemptionsPerUser,
            GlobalMaxRedemptions: promoCode.GlobalMaxRedemptions,
            CurrentRedemptionsCount: promoCode.CurrentRedemptionsCount,
            ValidFrom: promoCode.ValidFrom,
            ValidUntil: promoCode.ValidUntil,
            IsActive: promoCode.IsActive,
            Description: promoCode.Description,
            CreatedOn: promoCode.CreatedOn);
    }
}
