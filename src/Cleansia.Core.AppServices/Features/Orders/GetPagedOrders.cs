#nullable enable
using System.Security.Claims;
using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Features.Orders.DTOs;
using Cleansia.Core.AppServices.Features.Orders.Filters;
using Cleansia.Core.AppServices.Mappers;
using Cleansia.Core.AppServices.Shared.DTOs.RequestModels;
using Cleansia.Core.AppServices.Shared.DTOs.ResponseModels;
using Cleansia.Core.Domain.Enums;
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
        IOrderRepository orderRepository,
        IOrderAccessService orderAccessService,
        IUserSessionProvider userSessionProvider)
        : IRequestHandler<Request, PagedData<OrderListItem>>
    {
        public async Task<PagedData<OrderListItem>> Handle(Request request, CancellationToken cancellationToken)
        {
            var role = userSessionProvider.GetTypedUserClaim(ClaimTypes.Role)?.Value;
            var isAdmin = role == UserProfile.Administrator.ToString();

            string? callerEmployeeId = null;
            if (!isAdmin)
            {
                callerEmployeeId = await orderAccessService.GetCallerEmployeeIdAsync(cancellationToken);
                if (string.IsNullOrEmpty(callerEmployeeId))
                {
                    return new List<OrderListItem>().MapToDto(0, request);
                }
            }

            DateTime? cleaningDateFrom = request.Filter?.CleaningDateFrom;
            if (request.Filter?.HasAvailableSpots == true && cleaningDateFrom is null)
            {
                cleaningDateFrom = DateTime.UtcNow.AddHours(-2);
            }

            var specification = OrderSpecification.Create(
                request.Filter?.Id,
                request.Filter?.IsActive,
                isAdmin ? request.Filter?.CustomerName : null,
                isAdmin ? request.Filter?.CustomerEmail : null,
                isAdmin ? request.Filter?.CustomerPhone : null,
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
            var orders = await orderRepository
                .GetPagedSort<OrderSort>(request.Offset, request.Limit, filter, request.Sort.MapToDomain())
                .Include(o => o.OrderStatusHistory)
                .Include(o => o.Currency)
                .Include(o => o.SelectedPackages)
                    .ThenInclude(sp => sp.Package)
                .Include(o => o.SelectedServices)
                    .ThenInclude(sp => sp.Service)
                        .ThenInclude(s => s.Category)
                .Include(o => o.CustomerAddress)
                .Include(o => o.AssignedEmployees)
                    .ThenInclude(ae => ae.Employee)
                        .ThenInclude(e => e!.User)
                .AsSplitQuery()
                .AsNoTracking()
                .ToListAsync(cancellationToken);

            var items = orders.Select(order =>
            {
                var dto = order.MapToDto();
                if (isAdmin)
                {
                    return dto;
                }

                var isAssigned = callerEmployeeId != null
                    && order.AssignedEmployees.Any(ae => ae.EmployeeId == callerEmployeeId);
                if (isAssigned)
                {
                    return dto;
                }

                return dto with
                {
                    CustomerName = string.Empty,
                    CustomerEmail = string.Empty,
                    CustomerPhone = string.Empty,
                    CustomerAddress = string.Empty,
                };
            }).ToList();

            return items.MapToDto(totalItems, request);
        }
    }
}
