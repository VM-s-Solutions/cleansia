#nullable enable
using System.Security.Claims;
using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Features.Orders.DTOs;
using Cleansia.Core.AppServices.Features.Orders.Filters;
using Cleansia.Core.AppServices.Mappers;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.AppServices.Shared.DTOs.RequestModels;
using Cleansia.Core.AppServices.Shared.DTOs.ResponseModels;
using Cleansia.Core.Domain.EmployeePayroll;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Extensions;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.SeedWork;
using Cleansia.Core.Domain.Sorting;
using Cleansia.Core.Domain.Specifications;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

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
        IUserSessionProvider userSessionProvider,
        IEmployeePayConfigRepository payConfigRepository,
        IOrderEmployeePayRepository orderEmployeePayRepository,
        IServiceScopeFactory serviceScopeFactory,
        ILogger<Handler> logger)
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

            // A non-admin cannot filter by a FOREIGN employee. When the client
            // supplies an EmployeeId (the "mine" panes) we pin it to the JWT
            // caller; an empty value (the Available pane) stays unscoped here.
            // RestrictToEmployeeId then constrains the base query to
            // assigned-to-caller OR still-takeable rows, so a foreign-assigned,
            // no-spot row is never returned. Admin keeps the broad filter.
            var employeeIdFilter = isAdmin
                ? request.Filter?.EmployeeId
                : string.IsNullOrEmpty(request.Filter?.EmployeeId) ? null : callerEmployeeId;

            var specification = OrderSpecification.Create(
                request.Filter?.Id,
                request.Filter?.IsActive,
                isAdmin ? request.Filter?.CustomerName : null,
                isAdmin ? request.Filter?.CustomerEmail : null,
                isAdmin ? request.Filter?.CustomerPhone : null,
                request.Filter?.DisplayOrderNumber,
                employeeIdFilter,
                cleaningDateFrom,
                request.Filter?.CleaningDateTo,
                request.Filter?.PaymentStatuses,
                request.Filter?.PaymentTypes,
                request.Filter?.MinTotalPrice,
                request.Filter?.MaxTotalPrice,
                request.Filter?.OrderStatuses,
                request.Filter?.HasAvailableSpots,
                request.Filter?.IsUnassigned,
                request.Filter?.ExcludeEmployeeId,
                restrictToEmployeeId: isAdmin ? null : callerEmployeeId);

            var filter = specification.SatisfiedBy();

            var totalItems = await orderRepository.GetCountAsync(filter, cancellationToken);
            // Includes only what the OrderListItem mapper actually
            // reads. The previous version pulled Employee+User per
            // assigned employee, but the mapper only emits the
            // OrderEmployee row id (line 46 in OrderMappers) — those
            // two ThenIncludes were paying for ~2 split queries of
            // wasted columns per page. AssignedEmployees stays so
            // we can count + emit ids; Employee/User chains gone.
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
                .AsSplitQuery()
                .AsNoTracking()
                .ToListAsync(cancellationToken);

            // Pay-config lookups for the caller — only when we have an
            // employee id. For admins the per-row pay is not meaningful so we
            // leave it null.
            var serviceIdsAcrossPage = orders
                .SelectMany(o => o.SelectedServices.Select(s => s.ServiceId))
                .Distinct()
                .ToList();
            var packageIdsAcrossPage = orders
                .SelectMany(o => o.SelectedPackages.Select(p => p.PackageId))
                .Distinct()
                .ToList();

            IReadOnlyList<EmployeePayConfig> serviceConfigsForCaller = Array.Empty<EmployeePayConfig>();
            IReadOnlyList<EmployeePayConfig> packageConfigsForCaller = Array.Empty<EmployeePayConfig>();
            IReadOnlyDictionary<string, decimal> existingPayByOrderId =
                new Dictionary<string, decimal>(0);
            if (!isAdmin && !string.IsNullOrEmpty(callerEmployeeId) && (serviceIdsAcrossPage.Count > 0 || packageIdsAcrossPage.Count > 0))
            {
                serviceConfigsForCaller = await payConfigRepository.GetServiceConfigsForOrderAsync(
                    serviceIdsAcrossPage, callerEmployeeId, cancellationToken);
                packageConfigsForCaller = await payConfigRepository.GetPackageConfigsForOrderAsync(
                    packageIdsAcrossPage, callerEmployeeId, cancellationToken);
                // Batched per-row pay lookup. One query for the whole
                // page, two columns, no eager-loaded nav graphs —
                // replaces the N+1 GetByOrderAndEmployeeAsync loop
                // below.
                var orderIds = orders.Select(o => o.Id).ToList();
                existingPayByOrderId = await orderEmployeePayRepository.GetTotalPayByOrderIdsAsync(
                    orderIds, callerEmployeeId, cancellationToken);
            }

            // EstimatedCleanerPay sort runs after we materialize the page —
            // the pay is computed in-memory, not on the Order column. So the
            // DB fetch comes back in CreatedOn order, and we re-sort the
            // resulting list before returning. This means the sort is
            // per-page, not global; acceptable for the cleaner's Available
            // tab where pages are typically small and the operator just wants
            // "highest-paying first within what I'm looking at".
            var paySort = request.Sort?.FirstOrDefault(s => string.Equals(
                s.Field, "estimatedCleanerPay", StringComparison.OrdinalIgnoreCase));

            var items = new List<OrderListItem>(orders.Count);
            foreach (var order in orders)
            {
                var dto = order.MapToDto();

                if (!isAdmin && !string.IsNullOrEmpty(callerEmployeeId))
                {
                    // Pre-fetched dictionary lookup — no DB hit. Falls
                    // back to the in-memory estimator (uses the
                    // page-wide pay-config batch) when the cleaner
                    // doesn't have a booked pay row yet.
                    decimal? estimatedCleanerPay = existingPayByOrderId.TryGetValue(order.Id, out var booked)
                        ? booked
                        : OrderPayEstimator.Estimate(order, callerEmployeeId, serviceConfigsForCaller, packageConfigsForCaller);
                    dto = dto with { EstimatedCleanerPay = estimatedCleanerPay };
                }

                if (isAdmin)
                {
                    items.Add(dto);
                    continue;
                }

                var isAssigned = callerEmployeeId != null
                    && order.AssignedEmployees.Any(ae => ae.EmployeeId == callerEmployeeId);
                if (isAssigned)
                {
                    items.Add(dto);
                    continue;
                }

                // Non-assigned (= a still-takeable row the caller is browsing).
                // Full PII, the exact geocoded coordinates, and the
                // confirmation code stay hidden until the caller takes the job;
                // only the coarse CustomerAddressApproximate + pay estimate (the
                // documented pre-accept signals) remain.
                items.Add(dto with
                {
                    CustomerName = string.Empty,
                    CustomerEmail = string.Empty,
                    CustomerPhone = string.Empty,
                    CustomerAddress = string.Empty,
                    ConfirmationCode = string.Empty,
                    CustomerAddressLatitude = null,
                    CustomerAddressLongitude = null,
                });
            }

            // Lazy backfill — fire-and-forget. Anything on this page with a
            // CustomerAddress but null coords gets geocoded in the background;
            // next call sees the populated row.
            var addressIdsToBackfill = orders
                .Where(o => o.CustomerAddress != null
                    && o.CustomerAddress.Latitude == null
                    && o.CustomerAddress.Longitude == null)
                .Select(o => o.CustomerAddress!.Id)
                .Distinct()
                .ToList();

            if (addressIdsToBackfill.Count > 0)
            {
                _ = Task.Run(() => BackfillCoordinatesAsync(addressIdsToBackfill), CancellationToken.None);
            }

            if (paySort != null)
            {
                items = paySort.Direction == Cleansia.Core.Domain.Sorting.Common.SortDirection.Ascending
                    ? items.OrderBy(x => x.EstimatedCleanerPay ?? decimal.MinValue).ToList()
                    : items.OrderByDescending(x => x.EstimatedCleanerPay ?? decimal.MinValue).ToList();
            }

            return items.MapToDto(totalItems, request);
        }

        private async Task BackfillCoordinatesAsync(List<string> addressIds)
        {
            // Each backfill runs in its own scope — the request scope that
            // produced this list is long gone by the time the fire-and-forget
            // Task starts work.
            try
            {
                using var scope = serviceScopeFactory.CreateScope();
                var scopedAddressRepository = scope.ServiceProvider.GetRequiredService<IAddressRepository>();
                var scopedGeocoder = scope.ServiceProvider.GetRequiredService<IAddressGeocoder>();
                var scopedUnitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

                foreach (var addressId in addressIds)
                {
                    var address = await scopedAddressRepository.GetByIdAsync(addressId, CancellationToken.None);
                    if (address == null || (address.Latitude != null && address.Longitude != null))
                    {
                        continue;
                    }
                    await scopedGeocoder.PopulateCoordinatesAsync(address, CancellationToken.None);
                }

                await scopedUnitOfWork.CommitAsync(CancellationToken.None);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Background address geocoding failed for {Count} addresses", addressIds.Count);
            }
        }
    }
}
