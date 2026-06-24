using Cleansia.Core.AppServices.Features.PromoCodes.Admin.DTOs;
using Cleansia.Core.AppServices.Mappers;
using Cleansia.Core.AppServices.Shared.DTOs.RequestModels;
using Cleansia.Core.AppServices.Shared.DTOs.ResponseModels;
using Cleansia.Core.Domain.Loyalty;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Sorting;
using Cleansia.Core.Domain.Specifications;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SortDefinition = Cleansia.Core.Domain.Sorting.Common.SortDefinition;
using SortDirection = Cleansia.Core.Domain.Sorting.Common.SortDirection;

namespace Cleansia.Core.AppServices.Features.PromoCodes.Admin;

/// <summary>
/// Paged redemption log for a single promo code (admin diagnostics view).
/// Most-recent-first.
/// </summary>
public class GetPromoCodeRedemptions
{
    public class Request : DataRangeRequest, IRequest<PagedData<PromoCodeRedemptionListItem>>
    {
        public string PromoCodeId { get; init; } = default!;
    }

    internal class Handler(IPromoCodeRedemptionRepository redemptionRepository)
        : IRequestHandler<Request, PagedData<PromoCodeRedemptionListItem>>
    {
        public async Task<PagedData<PromoCodeRedemptionListItem>> Handle(Request request, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(request.PromoCodeId))
            {
                return Enumerable.Empty<PromoCodeRedemptionListItem>().MapToDto(0, request);
            }

            var specification = PromoCodeRedemptionSpecification.Create(promoCodeId: request.PromoCodeId);
            var filter = specification.SatisfiedBy();

            var total = await redemptionRepository.GetCountAsync(filter, cancellationToken);
            var data = await redemptionRepository
                .GetPagedSort<PromoCodeRedemptionSort>(request.Offset, request.Limit, filter, ResolveSort(request))
                .Include(r => r.User)
                .AsNoTracking()
                .Select(redemption => redemption.MapToRedemptionListItem())
                .ToListAsync(cancellationToken);

            return data.MapToDto(total, request);
        }

        // Preserves the historical newest-first default: the bespoke repo ordered by
        // RedeemedOn desc, and the empty-sort GetPagedSort path applies no ordering.
        private static IEnumerable<SortDefinition> ResolveSort(Request request)
        {
            var sort = request.Sort.MapToDomain().ToList();
            return sort.Count > 0
                ? sort
                : [new SortDefinition { Field = nameof(PromoCodeRedemption.RedeemedOn), Direction = SortDirection.Descending }];
        }
    }
}
