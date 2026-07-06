#nullable enable
using System.Security.Claims;
using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Extensions;
using Cleansia.Core.AppServices.Features.Dashboard.DTOs;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.EmployeePayroll;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;

namespace Cleansia.Core.AppServices.Features.Dashboard;

public class GetDashboardStats
{
    public record Query(string? EmployeeId = null) : IQuery<DashboardStatsDto>;

    public class Handler(
        IOrderRepository orderRepository,
        IEmployeeInvoiceRepository employeeInvoiceRepository,
        IOrderEmployeePayRepository orderEmployeePayRepository,
        IEmployeePayConfigRepository payConfigRepository,
        IPayPeriodRepository payPeriodRepository,
        IOrderAccessService orderAccessService,
        ICurrencyResolutionService currencyResolutionService,
        IUserSessionProvider userSessionProvider)
        : IQueryHandler<Query, DashboardStatsDto>
    {
        // Payroll runs ~5 business days after the period closes. Sane default
        // until PayPeriod gains an explicit PayoutDate field.
        private const int PayoutOffsetDays = 5;

        public async Task<BusinessResult<DashboardStatsDto>> Handle(Query query, CancellationToken cancellationToken)
        {
            var role = userSessionProvider.GetTypedUserClaim(ClaimTypes.Role)?.Value;
            string? employeeId;

            if (role == UserProfile.Administrator.ToString())
            {
                employeeId = query.EmployeeId;
            }
            else
            {
                employeeId = await orderAccessService.GetCallerEmployeeIdAsync(cancellationToken);
            }

            if (string.IsNullOrEmpty(employeeId))
            {
                return BusinessResult.Failure<DashboardStatsDto>(new Error(
                    "Employee",
                    BusinessErrorMessage.EmployeeNotFound));
            }

            // Resolve the caller's timezone from the X-Time-Zone
            // request header (set by every mobile + web client).
            // Falls back to UTC if the client didn't send one or sent
            // an id the host's tz db doesn't know. Day boundaries
            // computed in this zone so "today" matches the cleaner's
            // wall clock, then converted back to UTC for the SQL
            // comparison against the timestamptz columns.
            var tz = ResolveTimeZone(userSessionProvider.GetTimeZoneId());
            var nowLocal = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz);
            var todayStartLocal = DateTime.SpecifyKind(nowLocal.Date, DateTimeKind.Unspecified);
            var todayEndLocal = todayStartLocal.AddDays(1);
            // ISO week = Monday-start. C# DayOfWeek treats Sunday as 0, so the
            // "-1, wrap to 6" pattern lands us on Monday.
            var daysSinceMonday = ((int)todayStartLocal.DayOfWeek + 6) % 7;
            var weekStartLocal = todayStartLocal.AddDays(-daysSinceMonday);
            var weekEndLocal = weekStartLocal.AddDays(7);

            // Convert each local boundary to UTC for the timestamptz
            // comparisons. ConvertTimeToUtc treats the input as wall-
            // clock in `tz` and returns the matching UTC instant —
            // exactly what the query needs.
            var todayStart = TimeZoneInfo.ConvertTimeToUtc(todayStartLocal, tz);
            var todayEnd = TimeZoneInfo.ConvertTimeToUtc(todayEndLocal, tz);
            var weekStart = TimeZoneInfo.ConvertTimeToUtc(weekStartLocal, tz);
            var weekEnd = TimeZoneInfo.ConvertTimeToUtc(weekEndLocal, tz);


            // Monthly windows use the same zone-aware "now" so a
            // user just past midnight on the 1st sees the new month.
            var (currentMonthStart, currentMonthEnd) = ConvertMonthRangeToUtc(nowLocal.GetCurrentMonthRange(), tz);
            var (previousMonthStart, previousMonthEnd) = ConvertMonthRangeToUtc(nowLocal.GetPreviousMonthRange(), tz);

            var availableOrdersCount = await GetAvailableOrdersCountAsync(employeeId, cancellationToken);
            var activeOrdersCount = await GetActiveOrdersCountAsync(employeeId, cancellationToken);

            // Counts come from Order.CompletedAt directly via the
            // OrderRepository — NOT via OrderEmployeePay. The pay
            // table is only populated after admin payroll runs
            // CalculateOrderPay; gating the cleaner's "today /
            // this week jobs done" count on that would mean a
            // freshly-completed order shows as "0 done" until
            // payroll catches up, which is wrong from the
            // cleaner's POV. Earnings (below) still go through
            // OrderEmployeePay because money the cleaner hasn't
            // actually had booked is not earnings yet.
            var completedCounts = await orderRepository.CountCompletedForEmployeeWindowsAsync(
                employeeId,
                currentMonthStart, currentMonthEnd,
                previousMonthStart, previousMonthEnd,
                todayStart, todayEnd,
                weekStart, weekEnd,
                cancellationToken);

            // Earnings — sum per-order, using booked OrderEmployeePay
            // when it exists, otherwise the per-employee pay-config
            // estimate (same logic the orders list uses for the
            // EstimatedCleanerPay chip). Without this fallback the
            // dashboard showed "0 Kč earned" while the orders list
            // showed "1238 Kč" for the same job — because payroll
            // hasn't run yet and OrderEmployeePay rows don't exist.
            // The cleaner sees the honest "what you've earned today"
            // number even before admin processes the pay period.
            //
            // We do this in three steps shared across the three
            // windows (today / this-week / last-month):
            //   1. Pull the orders completed in the week OR last-month
            //      window in ONE round trip (today ⊆ week, so the today
            //      rows are a subset) and partition in memory with the
            //      same inclusive bounds the per-window fetches used.
            //   2. Load pay configs ONCE for the union of all
            //      service / package ids touched by any window.
            //   3. For each window, sum: booked pay (if a row exists)
            //      OR estimator output OR 0 if no config matches.
            var fetchedCompletedOrders = await orderRepository.GetCompletedOrdersInEitherRangeAsync(
                employeeId, weekStart, weekEnd, previousMonthStart, previousMonthEnd, cancellationToken);

            List<Cleansia.Core.Domain.Orders.Order> InWindow(DateTime start, DateTime end) =>
                fetchedCompletedOrders.Where(o => o.CompletedAt >= start && o.CompletedAt <= end).ToList();

            var todayCompletedOrderRows = InWindow(todayStart, todayEnd);
            var weekCompletedOrderRows = InWindow(weekStart, weekEnd);
            var lastMonthCompletedOrderRows = InWindow(previousMonthStart, previousMonthEnd);

            var allCompletedOrders = fetchedCompletedOrders
                .DistinctBy(o => o.Id)
                .ToList();
            var allServiceIds = allCompletedOrders
                .SelectMany(o => o.SelectedServices.Select(s => s.ServiceId))
                .Distinct()
                .ToList();
            var allPackageIds = allCompletedOrders
                .SelectMany(o => o.SelectedPackages.Select(p => p.PackageId))
                .Distinct()
                .ToList();

            IReadOnlyList<EmployeePayConfig> serviceConfigs = Array.Empty<EmployeePayConfig>();
            IReadOnlyList<EmployeePayConfig> packageConfigs = Array.Empty<EmployeePayConfig>();
            IReadOnlyDictionary<string, decimal> bookedPayByOrderId =
                new Dictionary<string, decimal>(0);
            if (allCompletedOrders.Count > 0)
            {
                if (allServiceIds.Count > 0)
                {
                    serviceConfigs = await payConfigRepository.GetServiceConfigsForOrderAsync(
                        allServiceIds, employeeId, cancellationToken);
                }
                if (allPackageIds.Count > 0)
                {
                    packageConfigs = await payConfigRepository.GetPackageConfigsForOrderAsync(
                        allPackageIds, employeeId, cancellationToken);
                }
                var allOrderIds = allCompletedOrders.Select(o => o.Id).ToList();
                bookedPayByOrderId = await orderEmployeePayRepository.GetTotalPayByOrderIdsAsync(
                    allOrderIds, employeeId, cancellationToken);
            }

            decimal SumWindow(IEnumerable<Cleansia.Core.Domain.Orders.Order> windowOrders) => windowOrders.Sum(o =>
                bookedPayByOrderId.TryGetValue(o.Id, out var booked)
                    ? booked
                    : OrderPayEstimator.Estimate(o, employeeId, serviceConfigs, packageConfigs) ?? 0m);

            var todayEarnings = SumWindow(todayCompletedOrderRows);
            var weekEarnings = SumWindow(weekCompletedOrderRows);
            var lastMonthEarnings = SumWindow(lastMonthCompletedOrderRows);

            var latestInvoice = await employeeInvoiceRepository.GetLatestInvoiceAsync(
                employeeId, cancellationToken);

            var pendingEarnings = await orderEmployeePayRepository.SumPendingEarningsAsync(employeeId, cancellationToken);

            // Pay-period context. Active period drives the progress bar; the
            // payout date is derived from EndDate + offset (since PayPeriod
            // doesn't yet have an explicit PayoutDate). All nullable — the
            // mobile screen renders the section only when CurrentPayPeriodStart
            // is set.
            var activePeriod = await payPeriodRepository.GetActivePeriodAsync(cancellationToken);
            DateTime? periodStart = activePeriod is null ? null
                : DateTime.SpecifyKind(activePeriod.StartDate.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);
            DateTime? periodEnd = activePeriod is null ? null
                : DateTime.SpecifyKind(activePeriod.EndDate.ToDateTime(TimeOnly.MaxValue), DateTimeKind.Utc);
            DateTime? nextPayoutDate = periodEnd?.AddDays(PayoutOffsetDays);

            // Aggregate rating across reviews on orders this employee was on.
            var (avgRating, ratingCount) = await orderRepository.GetAverageRatingForEmployeeAsync(
                employeeId, cancellationToken);

            // Display currency derives from the employee's approved
            // work country (Employee.WorkCountryId -> CountryConfiguration
            // .DefaultCurrencyCode), with the platform global default as
            // a safety net for unapproved cleaners. Without a sensible
            // server-side value the mobile dashboard fell back to the
            // device locale — a Prague cleaner on a US-locale emulator
            // saw "$" instead of "Kč".
            var currencyCode = await currencyResolutionService
                .ResolveCurrencyCodeForEmployeeAsync(employeeId, cancellationToken);

            return new DashboardStatsDto(
                AvailableOrdersCount: availableOrdersCount,
                MyActiveOrdersCount: activeOrdersCount,
                ThisMonthCompletedOrders: completedCounts.ThisMonth,
                LastMonthCompletedOrders: completedCounts.LastMonth,
                TodayEarnings: todayEarnings,
                TodayCompletedCount: completedCounts.Today,
                WeekEarnings: weekEarnings,
                WeekCompletedCount: completedCounts.Week,
                LastMonthEarnings: lastMonthEarnings,
                CurrentPeriodEarnings: pendingEarnings > 0 ? pendingEarnings : latestInvoice?.TotalAmount ?? 0,
                CurrentPayPeriodStart: periodStart,
                CurrentPayPeriodEnd: periodEnd,
                NextPayoutDate: nextPayoutDate,
                AverageRating: avgRating,
                RatingCount: ratingCount,
                LatestInvoiceStatus: latestInvoice?.Status.ToString(),
                CurrencyCode: currencyCode
            );
        }

        private async Task<int> GetAvailableOrdersCountAsync(string excludeEmployeeId, CancellationToken cancellationToken)
        {
            var specification = DashboardSpecifications.CreateAvailableOrdersSpec(excludeEmployeeId);
            return await orderRepository.GetCountAsync(specification.SatisfiedBy(), cancellationToken);
        }

        private async Task<int> GetActiveOrdersCountAsync(string employeeId, CancellationToken cancellationToken)
        {
            var specification = DashboardSpecifications.CreateActiveOrdersSpec(employeeId);
            return await orderRepository.GetCountAsync(specification.SatisfiedBy(), cancellationToken);
        }

        /// <summary>
        /// Resolve the caller's IANA timezone id (e.g. "Europe/Prague").
        /// Falls back to UTC if the id is missing or unknown to the
        /// host's tz database — the dashboard then behaves as it did
        /// before zone-awareness was added.
        /// </summary>
        private static TimeZoneInfo ResolveTimeZone(string? id)
        {
            if (string.IsNullOrWhiteSpace(id)) return TimeZoneInfo.Utc;
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(id);
            }
            catch (TimeZoneNotFoundException)
            {
                return TimeZoneInfo.Utc;
            }
            catch (InvalidTimeZoneException)
            {
                return TimeZoneInfo.Utc;
            }
        }

        /// <summary>
        /// Convert a (start, end) month range expressed in local wall-
        /// clock time into the matching UTC instants used by the
        /// timestamptz comparisons. Mirrors what we do for the day
        /// and week boundaries above.
        /// </summary>
        private static (DateTime Start, DateTime End) ConvertMonthRangeToUtc(
            (DateTime Start, DateTime End) localRange, TimeZoneInfo tz)
        {
            var startLocal = DateTime.SpecifyKind(localRange.Start, DateTimeKind.Unspecified);
            var endLocal = DateTime.SpecifyKind(localRange.End, DateTimeKind.Unspecified);
            return (
                TimeZoneInfo.ConvertTimeToUtc(startLocal, tz),
                TimeZoneInfo.ConvertTimeToUtc(endLocal, tz));
        }
    }
}
