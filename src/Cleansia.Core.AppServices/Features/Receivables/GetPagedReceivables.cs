using Cleansia.Core.AppServices.Features.Receivables.DTOs;
using Cleansia.Core.AppServices.Features.Receivables.Filters;
using Cleansia.Core.AppServices.Features.Receivables.Mappers;
using Cleansia.Core.AppServices.Mappers;
using Cleansia.Core.AppServices.Shared.DTOs.RequestModels;
using Cleansia.Core.AppServices.Shared.DTOs.ResponseModels;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Sorting;
using Cleansia.Core.Domain.Specifications;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Core.AppServices.Features.Receivables;

/// <summary>The administrator's list of what customers owe the company, newest first by default.</summary>
public class GetPagedReceivables
{
    public class Request : DataRangeRequest, IRequest<PagedData<ReceivableListItem>>
    {
        public ReceivableFilter? Filter { get; init; }
    }

    internal class Handler(IReceivableRepository receivableRepository)
        : IRequestHandler<Request, PagedData<ReceivableListItem>>
    {
        public async Task<PagedData<ReceivableListItem>> Handle(Request request, CancellationToken cancellationToken)
        {
            var specification = ReceivableSpecification.Create(
                status: request.Filter?.Status,
                kind: request.Filter?.Kind,
                userId: request.Filter?.UserId,
                orderId: request.Filter?.OrderId);

            var filter = specification.SatisfiedBy();

            var totalItems = await receivableRepository.GetCountAsync(filter, cancellationToken);
            var items = await receivableRepository
                .GetPagedSort<ReceivableSort>(request.Offset, request.Limit, filter, request.Sort.MapToDomain())
                .Include(r => r.Order)
                .Include(r => r.Currency)
                .AsNoTracking()
                .Select(r => r.MapToListItem())
                .ToListAsync(cancellationToken);

            return items.MapToDto(totalItems, request);
        }
    }
}
