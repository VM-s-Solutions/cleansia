#nullable enable
using System.ComponentModel.DataAnnotations;
using Cleansia.Core.AppServices.Authentication;
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
        [Range(1, 100)]
        [Display(Order = int.MaxValue)]
        public override int Limit
        {
            get => base.Limit;
            init => base.Limit = value;
        }

        public OrderFilter? Filter { get; init; }
    }

    internal class Handler(
        IOrderRepository orderRepository,
        IUserSessionProvider userSessionProvider,
        IOrderAccessService orderAccessService)
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
                currencyId: request.Filter?.CurrencyId);

            var filter = specification.SatisfiedBy();

            // The customer's own orders in every operating company, and those made without an account under
            // their e-mail: a booking belongs to the market's operator, and this list is the one place the
            // customer sees them all. Pinned by their own id from the JWT, or, on a booking that names no
            // account, by the JWT's e-mail.
            var guestEmail = orderAccessService.ListedGuestEmailForCaller();
            var totalItems = await orderRepository.GetCountListedForCustomerAsync(
                userId, guestEmail, filter, cancellationToken);
            // Server-side projection onto exactly the columns the list DTO reads — the previous
            // full-graph Include set paid ~8 split queries per page for mostly unread columns.
            var rows = await orderRepository
                .GetPagedSortListedForCustomer<OrderSort>(
                    userId, guestEmail, request.Offset, request.Limit, filter, request.Sort.MapToDomain().ForOrderPage())
                .SelectOrderListRows()
                .AsSplitQuery()
                .ToListAsync(cancellationToken);

            var items = rows
                .Select(row => row.IsGuestBooking ? row.MapToDto().RedactForListedGuestBooking() : row.MapToDto())
                .ToList();

            return items.MapToDto(totalItems, request);
        }
    }
}
