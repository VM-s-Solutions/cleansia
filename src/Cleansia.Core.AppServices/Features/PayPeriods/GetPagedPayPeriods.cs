#nullable enable
using Cleansia;
using Cleansia.Core.AppServices.Features.PayPeriods.DTOs;
using Cleansia.Core.AppServices.Features.PayPeriods.Filters;
using Cleansia.Core.AppServices.Mappers;
using Cleansia.Core.AppServices.Shared.DTOs.RequestModels;
using Cleansia.Core.AppServices.Shared.DTOs.ResponseModels;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Sorting;
using Cleansia.Core.Domain.Specifications;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Core.AppServices.Features.PayPeriods;

public class GetPagedPayPeriods
{
    public class Request : DataRangeRequest, IRequest<PagedData<PayPeriodDto>>
    {
        public PayPeriodFilter? Filter { get; init; }
    }

    internal class Handler(
        IPayPeriodRepository payPeriodRepository)
        : IRequestHandler<Request, PagedData<PayPeriodDto>>
    {
        public async Task<PagedData<PayPeriodDto>> Handle(Request request, CancellationToken cancellationToken)
        {
            var specification = PayPeriodSpecification.Create(
                status: request.Filter?.Status,
                year: request.Filter?.Year);

            var filter = specification.SatisfiedBy();

            var totalItems = await payPeriodRepository.GetCountAsync(filter, cancellationToken);
            var items = await payPeriodRepository
                .GetPagedSort<PayPeriodSort>(request.Offset, request.Limit, filter, request.Sort.MapToDomain())
                .AsNoTracking()
                .Select(period => period.MapToDto())
                .ToListAsync(cancellationToken);

            return items.MapToDto(totalItems, request);
        }
    }
}
