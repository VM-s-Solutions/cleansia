#nullable enable
using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Dashboard.DTOs;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Mappers;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Core.AppServices.Features.Dashboard;

/// <summary>
/// Slim preview of unclaimed orders the caller could take, for the mobile dashboard hero card.
///
/// <para>The spec passes <c>excludeEmployeeId</c>, so every row is by construction an order the caller
/// is NOT assigned to — the population pre-acceptance redaction exists for. <b>The street is never
/// selected, so it does not leave the database.</b> → /flows/execution-and-completion</para>
/// </summary>
public class GetAvailableJobsPreview
{
    /// <summary>
    /// Server ceiling on the rows one call may return. Not tuned: it is the bound
    /// <c>GetMyPendingOffers</c> already applies to the other cleaner-facing pre-acceptance list on
    /// this host, and a second literal for the same job on the same surface is drift. Both clients ask
    /// for 5, so the ceiling is an abuse bound rather than a product number — the failure asymmetry
    /// runs one way (too high hands out the whole board in one call; too low under-fills a carousel
    /// beside a <c>TotalAvailableCount</c> that stays exact).
    /// </summary>
    private const int MaxJobs = 50;

    /// <summary>
    /// [Limit] is what the caller ASKS for, never the cap — the cap is server-side and the caller
    /// cannot raise it. 5 is what both partner clients send; it covers the carousel + headline math.
    /// </summary>
    public record Query(int Limit = 5) : IQuery<AvailableJobsPreviewResponse>;

    internal class Handler(
        IOrderRepository orderRepository,
        IEmployeePayConfigRepository payConfigRepository,
        IOrderAccessService orderAccessService)
        : IQueryHandler<Query, AvailableJobsPreviewResponse>
    {
        public async Task<BusinessResult<AvailableJobsPreviewResponse>> Handle(Query query, CancellationToken cancellationToken)
        {
            var employeeId = await orderAccessService.GetCallerEmployeeIdAsync(cancellationToken);
            if (string.IsNullOrEmpty(employeeId))
            {
                return BusinessResult.Failure<AvailableJobsPreviewResponse>(new Error(
                    "Employee",
                    BusinessErrorMessage.EmployeeNotFound));
            }

            // Sorted by TotalPrice DESC so the cleaner sees the highest-value jobs first.
            var spec = DashboardSpecifications.CreateAvailableOrdersSpec(employeeId, DateTime.UtcNow);
            var totalCount = await orderRepository.GetCountAsync(spec.SatisfiedBy(), cancellationToken);
            var orders = await orderRepository.GetQueryable()
                .Where(spec.SatisfiedBy())
                .OrderByDescending(o => o.TotalPrice)
                .Take(Math.Clamp(query.Limit, 1, MaxJobs))
                .Select(o => new
                {
                    o.Id,
                    o.DisplayOrderNumber,
                    o.CleaningDateTime,
                    o.TotalPrice,
                    // Carried for the pay estimate below, not for display. The street is still never
                    // selected.
                    o.Rooms,
                    o.Bathrooms,
                    o.TravelDistance,
                    ServiceIds = o.SelectedServices.Select(s => s.ServiceId).ToList(),
                    PackageIds = o.SelectedPackages.Select(p => p.PackageId).ToList(),
                    City = o.CustomerAddress!.City,
                    ZipCode = o.CustomerAddress.ZipCode,
                })
                .ToListAsync(cancellationToken);

            // Two batch lookups for the CALLER, the shape GetDashboardStats already uses in this same
            // folder. Deliberately NO booked-pay lookup: the spec excludes orders the caller is
            // assigned to, and CalculateOrderPay refuses an unassigned employee, so an OrderEmployeePay
            // row cannot exist for any row here. Reading one would be a round trip that always misses.
            var serviceIds = orders.SelectMany(o => o.ServiceIds).Distinct().ToList();
            var packageIds = orders.SelectMany(o => o.PackageIds).Distinct().ToList();

            IReadOnlyList<Domain.EmployeePayroll.EmployeePayConfig> serviceConfigs = [];
            IReadOnlyList<Domain.EmployeePayroll.EmployeePayConfig> packageConfigs = [];
            if (serviceIds.Count > 0)
            {
                serviceConfigs = await payConfigRepository.GetServiceConfigsForOrderAsync(
                    serviceIds, employeeId, cancellationToken);
            }
            if (packageIds.Count > 0)
            {
                packageConfigs = await payConfigRepository.GetPackageConfigsForOrderAsync(
                    packageIds, employeeId, cancellationToken);
            }

            var jobs = orders.Select(o => new AvailableJobPreviewDto(
                Id: o.Id,
                DisplayOrderNumber: o.DisplayOrderNumber,
                CustomerAddressApproximate: OrderMappers.BuildApproximateAddress(o.City, o.ZipCode),
                CleaningDateTime: o.CleaningDateTime,
                TotalPrice: o.TotalPrice
            )).ToList();

            // WHAT THE CLEANER EARNS, not what the customer pays. The banner said "Earn up to X" while
            // summing TotalPrice, so it quoted roughly three times the real figure — the same job read
            // 3 731 on the dashboard and 1 275 on the list, which is the number the cleaner is actually
            // offered. Unquotable rows contribute 0, matching how the orders list sums the same phrase
            // (`filtered.sumOf { it.estimatedCleanerPay ?: 0.0 }`), so one definition serves both.
            var potentialEarnings = orders.Sum(o => OrderPayEstimator.Estimate(
                o.ServiceIds.ToHashSet(),
                o.PackageIds.ToHashSet(),
                o.Rooms,
                o.Bathrooms,
                o.TravelDistance,
                employeeId,
                serviceConfigs,
                packageConfigs) ?? 0m);

            return new AvailableJobsPreviewResponse(
                Jobs: jobs,
                TotalPotentialEarnings: potentialEarnings,
                TotalAvailableCount: totalCount
            );
        }
    }
}
