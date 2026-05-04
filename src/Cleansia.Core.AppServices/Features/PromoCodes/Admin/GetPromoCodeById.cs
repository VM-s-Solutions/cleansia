using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.PromoCodes.Admin.DTOs;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Core.AppServices.Features.PromoCodes.Admin;

/// <summary>
/// Single-promo-code admin detail. Returns the persisted shape plus a fresh
/// redemption count from the redemption table.
/// </summary>
public class GetPromoCodeById
{
    public record Query(string PromoCodeId) : IQuery<PromoCodeDetailDto>;

    public class Handler(
        IPromoCodeRepository promoCodeRepository,
        IPromoCodeRedemptionRepository redemptionRepository) : IQueryHandler<Query, PromoCodeDetailDto>
    {
        public async Task<BusinessResult<PromoCodeDetailDto>> Handle(Query request, CancellationToken cancellationToken)
        {
            var code = await promoCodeRepository.GetQueryable()
                .AsNoTracking()
                .Include(c => c.Currency)
                .FirstOrDefaultAsync(c => c.Id == request.PromoCodeId, cancellationToken);

            if (code == null)
            {
                return BusinessResult.Failure<PromoCodeDetailDto>(
                    new Error(BusinessErrorMessage.PromoNotFound, BusinessErrorMessage.PromoNotFound));
            }

            var redemptionCount = await redemptionRepository
                .CountByPromoCodeAsync(code.Id, cancellationToken);

            var dto = new PromoCodeDetailDto(
                Id: code.Id,
                Code: code.Code,
                Type: code.Type,
                DiscountPercent: code.DiscountPercent,
                DiscountAmount: code.DiscountAmount,
                CurrencyId: code.CurrencyId,
                CurrencyCode: code.Currency?.Code,
                MinimumOrderAmount: code.MinimumOrderAmount,
                MaxRedemptionsPerUser: code.MaxRedemptionsPerUser,
                GlobalMaxRedemptions: code.GlobalMaxRedemptions,
                CurrentRedemptionsCount: code.CurrentRedemptionsCount,
                RedemptionCount: redemptionCount,
                ValidFrom: code.ValidFrom,
                ValidUntil: code.ValidUntil,
                IsActive: code.IsActive,
                Description: code.Description,
                CreatedOn: code.CreatedOn,
                UpdatedOn: code.UpdatedOn);

            return BusinessResult.Success(dto);
        }
    }
}
