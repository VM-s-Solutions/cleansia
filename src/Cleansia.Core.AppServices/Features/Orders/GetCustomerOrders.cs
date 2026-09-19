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

public class GetCustomerOrders
{
    public class Request : DataRangeRequest, IRequest<PagedData<OrderListItem>>
    {
        public OrderFilter? Filter { get; init; }
    }

    internal class Handler(
        IOrderRepository orderRepository,
        IUserSessionProvider userSessionProvider)
        : IRequestHandler<Request, PagedData<OrderListItem>>
    {
        public async Task<PagedData<OrderListItem>> Handle(Request request, CancellationToken cancellationToken)
        {
            var userId = userSessionProvider.GetUserId();
            if (string.IsNullOrEmpty(userId))
            {
                return new List<OrderListItem>().MapToDto(0, request);
            }

            var specification = OrderSpecification.Create(
                id: request.Filter?.Id,
                isActive: request.Filter?.IsActive,
                displayOrderNumber: request.Filter?.DisplayOrderNumber,
                cleaningDateFrom: request.Filter?.CleaningDateFrom,
                cleaningDateTo: request.Filter?.CleaningDateTo,
                paymentStatuses: request.Filter?.PaymentStatuses,
                paymentTypes: request.Filter?.PaymentTypes,
                minTotalPrice: request.Filter?.MinTotalPrice,
                maxTotalPrice: request.Filter?.MaxTotalPrice,
                orderStatuses: request.Filter?.OrderStatuses,
                userId: userId,
                currencyId: request.Filter?.CurrencyId);

            var filter = specification.SatisfiedBy();

            // The customer's own orders in every operating company: a booking belongs to the market's
            // operator, and this list is the one place the customer sees them all. Pinned by their own
            // id from the JWT, which the specification carries as well.
            var totalItems = await orderRepository.GetCountForOwnerAsync(userId, filter, cancellationToken);
            // Server-side projection onto exactly the columns the list DTO reads — the previous
            // full-graph Include set paid ~8 split queries per page for mostly unread columns.
            var rows = await orderRepository
                .GetPagedSortForOwner<OrderSort>(userId, request.Offset, request.Limit, filter, request.Sort.MapToDomain())
                .SelectOrderListRows()
                .AsSplitQuery()
                .ToListAsync(cancellationToken);

            var items = rows.Select(row => row.MapToDto()).ToList();

            return items.MapToDto(totalItems, request);
        }
    }
}
