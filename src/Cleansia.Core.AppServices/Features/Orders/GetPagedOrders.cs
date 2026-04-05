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
            DateTime? cleaningDateFrom = request.Filter?.CleaningDateFrom;
            if (request.Filter?.HasAvailableSpots == true && cleaningDateFrom is null)
            {
                cleaningDateFrom = DateTime.UtcNow.AddHours(-2);
            }

            var specification = OrderSpecification.Create(
                request.Filter?.Id,
                request.Filter?.IsActive,
                request.Filter?.CustomerName,
                request.Filter?.CustomerEmail,
                request.Filter?.CustomerPhone,
                request.Filter?.DisplayOrderNumber,
                request.Filter?.EmployeeId,
                cleaningDateFrom,
                request.Filter?.CleaningDateTo,
                request.Filter?.PaymentStatuses,
                request.Filter?.PaymentTypes,
                request.Filter?.MinTotalPrice,
                request.Filter?.MaxTotalPrice,
                request.Filter?.OrderStatuses,
                request.Filter?.HasAvailableSpots,
                request.Filter?.IsUnassigned,
                request.Filter?.ExcludeEmployeeId);

            var filter = specification.SatisfiedBy();

            var totalItems = await orderRepository.GetCountAsync(filter, cancellationToken);
            var items = await orderRepository
                .GetPagedSort<OrderSort>(request.Offset, request.Limit, filter, request.Sort.MapToDomain())
                .Include(o => o.OrderStatusHistory)
                .Include(o => o.Currency)
                .Include(o => o.SelectedPackages)
                    .ThenInclude(sp => sp.Package)
                .Include(o => o.SelectedServices)
                    .ThenInclude(sp => sp.Service)
                .Include(o => o.CustomerAddress)
                .Include(o => o.AssignedEmployees)
                    .ThenInclude(ae => ae.Employee)
                        .ThenInclude(e => e!.User)
                .AsSplitQuery()
                .AsNoTracking()
                .Select(order => order.MapToDto())
                .ToListAsync(cancellationToken);

            return items.MapToDto(totalItems, request);
        }
    }
}