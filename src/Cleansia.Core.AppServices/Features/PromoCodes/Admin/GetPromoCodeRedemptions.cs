using Cleansia.Core.AppServices.Features.PromoCodes.Admin.DTOs;
using Cleansia.Core.AppServices.Shared.DTOs.ResponseModels;
using Cleansia.Core.Domain.Repositories;
using MediatR;

namespace Cleansia.Core.AppServices.Features.PromoCodes.Admin;

/// <summary>
/// Paged redemption log for a single promo code (admin diagnostics view).
/// Most-recent-first.
/// </summary>
public class GetPromoCodeRedemptions
{
    public record Query(string PromoCodeId, int Offset = 0, int Limit = 20)
        : IRequest<PagedData<PromoCodeRedemptionListItem>>;

    internal class Handler(IPromoCodeRedemptionRepository redemptionRepository)
        : IRequestHandler<Query, PagedData<PromoCodeRedemptionListItem>>
    {
        public async Task<PagedData<PromoCodeRedemptionListItem>> Handle(Query request, CancellationToken cancellationToken)
        {
            var pageNumber = request.Limit > 0 ? (request.Offset / request.Limit) + 1 : 1;

            if (string.IsNullOrEmpty(request.PromoCodeId))
            {
                return new PagedData<PromoCodeRedemptionListItem>(
                    pageNumber, request.Limit, 0, Array.Empty<PromoCodeRedemptionListItem>());
            }

            var total = await redemptionRepository.CountByPromoCodeAsync(request.PromoCodeId, cancellationToken);
            var items = await redemptionRepository.GetPagedByPromoCodeAsync(
                request.PromoCodeId, request.Offset, request.Limit, cancellationToken);

            var data = items
                .Select(r => new PromoCodeRedemptionListItem(
                    Id: r.Id,
                    PromoCodeId: r.PromoCodeId,
                    UserId: r.UserId,
                    UserEmail: r.User?.Email,
                    OrderId: r.OrderId,
                    AppliedDiscount: r.AppliedDiscount,
                    RedeemedOn: r.RedeemedOn))
                .ToList();

            return new PagedData<PromoCodeRedemptionListItem>(pageNumber, request.Limit, total, data);
        }
    }
}
