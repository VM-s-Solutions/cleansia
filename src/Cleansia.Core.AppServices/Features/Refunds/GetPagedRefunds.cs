using Cleansia.Core.AppServices.Features.Refunds.DTOs;
using Cleansia.Core.AppServices.Features.Refunds.Filters;
using Cleansia.Core.AppServices.Features.Refunds.Mappers;
using Cleansia.Core.AppServices.Mappers;
using Cleansia.Core.AppServices.Shared.DTOs.RequestModels;
using Cleansia.Core.AppServices.Shared.DTOs.ResponseModels;
using Cleansia.Core.Domain.Payments;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Sorting;
using Cleansia.Core.Domain.Specifications;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SortDefinition = Cleansia.Core.Domain.Sorting.Common.SortDefinition;
using SortDirection = Cleansia.Core.Domain.Sorting.Common.SortDirection;

namespace Cleansia.Core.AppServices.Features.Refunds;

/// <summary>The administrator's list of the company's card refunds, newest first by default.</summary>
public class GetPagedRefunds
{
    public class Request : DataRangeRequest, IRequest<PagedData<RefundListItem>>
    {
        public RefundFilter? Filter { get; init; }
    }

    internal class Handler(IRefundRepository refundRepository)
        : IRequestHandler<Request, PagedData<RefundListItem>>
    {
        public async Task<PagedData<RefundListItem>> Handle(Request request, CancellationToken cancellationToken)
        {
            var specification = RefundSpecification.Create(
                states: request.Filter?.States,
                userId: request.Filter?.UserId,
                orderId: request.Filter?.OrderId,
                requestedFrom: request.Filter?.RequestedFrom,
                requestedTo: request.Filter?.RequestedTo);

            var filter = specification.SatisfiedBy();

            var totalItems = await refundRepository.GetCountAsync(filter, cancellationToken);
            var items = await refundRepository
                .GetPagedSort<RefundSort>(request.Offset, request.Limit, filter, ResolveSort(request))
                .Include(r => r.Order)
                .AsNoTracking()
                .Select(r => r.MapToListItem())
                .ToListAsync(cancellationToken);

            return items.MapToDto(totalItems, request);
        }

        private static IEnumerable<SortDefinition> ResolveSort(Request request)
        {
            var sort = request.Sort.MapToDomain().ToList();
            return sort.Count > 0
                ? sort
                : [new SortDefinition { Field = nameof(Refund.CreatedOn), Direction = SortDirection.Descending }];
        }
    }
}
