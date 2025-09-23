#nullable enable
using Cleansia.Core.AppServices.Features.Orders.DTOs;
using Cleansia.Core.AppServices.Features.Orders.Filters;
using Cleansia.Core.AppServices.Mappers;
using Cleansia.Core.AppServices.Shared.DTOs.RequestModels;
using Cleansia.Core.AppServices.Shared.DTOs.ResponseModels;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Sorting;
using Cleansia.Core.Domain.Specifications;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Core.AppServices.Features.Orders;

public class GetPagedOrders
{
    public class Request : DataRangeRequest, IRequest<PagedData<OrderListItem>>
    {
        public OrderFilter? Filter { get; init; }
    }

    internal class Handler(
        IOrderRepository orderRepository)
        : IRequestHandler<Request, PagedData<OrderListItem>>
    {
        public async Task<PagedData<OrderListItem>> Handle(Request request, CancellationToken cancellationToken)
        {
            var specification = OrderSpecification.Create(
                request.Filter?.Id,
                request.Filter?.IsActive,
                request.Filter?.CustomerName,
                request.Filter?.CustomerEmail,
                request.Filter?.CustomerPhone,
                request.Filter?.DisplayOrderNumber,
                request.Filter?.EmployeeId,
                request.Filter?.PackageId,
                request.Filter?.CleaningDateFrom,
                request.Filter?.CleaningDateTo,
                request.Filter?.PaymentStatuses,
                request.Filter?.PaymentTypes,
                request.Filter?.MinTotalPrice,
                request.Filter?.MaxTotalPrice);

            var filter = specification.SatisfiedBy();

            var totalItems = await orderRepository.GetCountAsync(filter, cancellationToken);
            var items = await orderRepository
                .GetPagedSort<OrderSort>(request.Offset, request.Limit, filter, request.Sort.MapToDomain())
                .AsNoTracking()
                .Include(o => o.OrderStatusHistory)
                .Include(o => o.Currency)
                .Include(o => o.SelectedPackage)
                .Include(o => o.Employee)
                    .ThenInclude(e => e.User)
                .Include(o => o.CustomerAddress)
                .Select(order => order.MapToDto())
                .ToListAsync(cancellationToken);

            return items.MapToDto(totalItems, request);
        }
    }
}