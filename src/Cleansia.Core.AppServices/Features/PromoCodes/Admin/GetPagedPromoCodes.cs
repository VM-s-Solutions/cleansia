using Cleansia.Core.AppServices.Features.PromoCodes.Admin.DTOs;
using Cleansia.Core.AppServices.Shared.DTOs.ResponseModels;
using Cleansia.Core.Domain.Repositories;
using MediatR;

namespace Cleansia.Core.AppServices.Features.PromoCodes.Admin;

/// <summary>
/// Admin-side paged list of promo codes with optional active/expired/search
/// filters. Used by the Loyalty admin module's promo-codes table.
/// </summary>
public class GetPagedPromoCodes
{
    public record Query(
        bool? Active = null,
        bool? Expired = null,
        string? SearchCode = null,
        int Offset = 0,
        int Limit = 20) : IRequest<PagedData<PromoCodeListItem>>;

    internal class Handler(IPromoCodeRepository promoCodeRepository)
        : IRequestHandler<Query, PagedData<PromoCodeListItem>>
    {
        public async Task<PagedData<PromoCodeListItem>> Handle(Query request, CancellationToken cancellationToken)
        {
            var pageNumber = request.Limit > 0 ? (request.Offset / request.Limit) + 1 : 1;

            var (items, total) = await promoCodeRepository.GetPagedAdminAsync(
                request.Active,
                request.Expired,
                request.SearchCode,
                request.Offset,
                request.Limit,
                cancellationToken);

            var data = items
                .Select(c => new PromoCodeListItem(
                    Id: c.Id,
                    Code: c.Code,
                    Type: c.Type,
                    DiscountPercent: c.DiscountPercent,
                    DiscountAmount: c.DiscountAmount,
                    CurrencyId: c.CurrencyId,
                    CurrencyCode: c.Currency?.Code,
                    MinimumOrderAmount: c.MinimumOrderAmount,
                    MaxRedemptionsPerUser: c.MaxRedemptionsPerUser,
                    GlobalMaxRedemptions: c.GlobalMaxRedemptions,
                    CurrentRedemptionsCount: c.CurrentRedemptionsCount,
                    ValidFrom: c.ValidFrom,
                    ValidUntil: c.ValidUntil,
                    IsActive: c.IsActive,
                    Description: c.Description,
                    CreatedOn: c.CreatedOn))
                .ToList();

            return new PagedData<PromoCodeListItem>(pageNumber, request.Limit, total, data);
        }
    }
}
