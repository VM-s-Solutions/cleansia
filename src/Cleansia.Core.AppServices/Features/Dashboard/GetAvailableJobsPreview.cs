#nullable enable
using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Dashboard.DTOs;
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
                    City = o.CustomerAddress!.City,
                    ZipCode = o.CustomerAddress.ZipCode,
                })
                .ToListAsync(cancellationToken);

            var jobs = orders.Select(o => new AvailableJobPreviewDto(
                Id: o.Id,
                DisplayOrderNumber: o.DisplayOrderNumber,
                CustomerAddressApproximate: OrderMappers.BuildApproximateAddress(o.City, o.ZipCode),
                CleaningDateTime: o.CleaningDateTime,
                TotalPrice: o.TotalPrice
            )).ToList();

            return new AvailableJobsPreviewResponse(
                Jobs: jobs,
                TotalPotentialEarnings: jobs.Sum(j => j.TotalPrice),
                TotalAvailableCount: totalCount
            );
        }
    }
}
