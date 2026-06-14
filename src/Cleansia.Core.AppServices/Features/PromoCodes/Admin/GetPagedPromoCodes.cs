#nullable enable
using Cleansia.Core.AppServices.Features.PromoCodes.Admin.DTOs;
using Cleansia.Core.AppServices.Features.PromoCodes.Admin.Filters;
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

public class GetPagedPromoCodes
{
    public class Request : DataRangeRequest, IRequest<PagedData<PromoCodeListItem>>
    {
        public PromoCodeFilter? Filter { get; init; }
    }

    internal class Handler(IPromoCodeRepository promoCodeRepository)
        : IRequestHandler<Request, PagedData<PromoCodeListItem>>
    {
        public async Task<PagedData<PromoCodeListItem>> Handle(Request request, CancellationToken cancellationToken)
        {
            var specification = PromoCodeSpecification.Create(
                isActive: request.Filter?.Active,
                expired: request.Filter?.Expired,
                expiredReference: request.Filter?.Expired is null ? null : DateTimeOffset.UtcNow,
                searchCode: request.Filter?.SearchCode);

            var filter = specification.SatisfiedBy();

            var totalItems = await promoCodeRepository.GetCountAsync(filter, cancellationToken);
            var items = await promoCodeRepository
                .GetPagedSort<PromoCodeSort>(request.Offset, request.Limit, filter, ResolveSort(request))
                .Include(c => c.Currency)
                .AsNoTracking()
                .Select(promoCode => promoCode.MapToListItem())
                .ToListAsync(cancellationToken);

            return items.MapToDto(totalItems, request);
        }

        // Preserves the historical newest-first default: the bespoke repo ordered by
        // CreatedOn desc, and the empty-sort GetPagedSort path applies no ordering.
        private static IEnumerable<SortDefinition> ResolveSort(Request request)
        {
            var sort = request.Sort.MapToDomain().ToList();
            return sort.Count > 0
                ? sort
                : [new SortDefinition { Field = nameof(PromoCode.CreatedOn), Direction = SortDirection.Descending }];
        }
    }
}
