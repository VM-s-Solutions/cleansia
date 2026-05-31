#nullable enable
using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Dashboard.DTOs;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Common.Validations;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Core.AppServices.Features.Dashboard;

/// <summary>
/// Slim preview of unclaimed orders the calling employee could take right
/// now. Used by the mobile dashboard's "available work" hero card so the
/// cleaner sees "earn up to €X" without pulling the full paged order list.
///
/// Authorisation: same as the main paged-orders endpoint — caller must
/// resolve to an employee. The returned rows exclude orders the caller is
/// already assigned to.
/// </summary>
public class GetAvailableJobsPreview
{
    /// <summary>
    /// [Limit] caps the per-call row count so the dashboard never streams
    /// pages of detail. 5 covers the carousel + headline math.
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

            // Specification matches the "Available Orders" tab on the mobile
            // dashboard: Pending or Confirmed, unassigned spots remain, and
            // not already mine. We sort by TotalPrice DESC so the cleaner
            // sees the highest-value jobs first.
            var spec = DashboardSpecifications.CreateAvailableOrdersSpec(employeeId);
            var totalCount = await orderRepository.GetCountAsync(spec.SatisfiedBy(), cancellationToken);
            var orders = await orderRepository.GetQueryable()
                .Where(spec.SatisfiedBy())
                .OrderByDescending(o => o.TotalPrice)
                .Take(query.Limit)
                .Select(o => new
                {
                    o.Id,
                    o.DisplayOrderNumber,
                    o.CleaningDateTime,
                    o.TotalPrice,
                    Address = o.CustomerAddress,
                })
                .ToListAsync(cancellationToken);

            var jobs = orders.Select(o => new AvailableJobPreviewDto(
                Id: o.Id,
                DisplayOrderNumber: o.DisplayOrderNumber,
                CustomerAddress: FormatAddress(o.Address),
                CleaningDateTime: o.CleaningDateTime,
                TotalPrice: o.TotalPrice
            )).ToList();

            return new AvailableJobsPreviewResponse(
                Jobs: jobs,
                TotalPotentialEarnings: jobs.Sum(j => j.TotalPrice),
                TotalAvailableCount: totalCount
            );
        }

        private static string? FormatAddress(Address? a)
        {
            if (a is null) return null;
            var parts = new[] { a.Street, a.City, a.ZipCode }
                .Where(s => !string.IsNullOrWhiteSpace(s));
            var joined = string.Join(", ", parts);
            return string.IsNullOrWhiteSpace(joined) ? null : joined;
        }
    }
}
