using System.Data.Common;
using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using BusinessResult = Cleansia.Infra.Common.Validations.BusinessResult;

namespace Cleansia.Core.AppServices.Features.Orders;

public class GetMyServingCleaners
{
    private const int MaxCleaners = 20;

    /// <summary>
    /// ADR-0039 D5 — every slot field is optional, so a client that has not been rebuilt keeps working
    /// and simply gets the unevaluated answer. The three together describe one booking: the instant, and
    /// the selection the server derives its length from. There is deliberately NO date-range parameter —
    /// a range turns a per-booking answer into a schedule feed about a worker, which is a different
    /// decision with a different privacy analysis.
    /// </summary>
    public record Query(
        DateTime? CleaningDateTimeUtc = null,
        IReadOnlyList<string>? SelectedServiceIds = null,
        IReadOnlyList<string>? SelectedPackageIds = null) : ICommand<IReadOnlyList<Response>>;

    public record Response(
        string EmployeeId,
        string FullName,
        DateTime LastServedOn,
        bool? IsAvailableForRequestedSlot);

    /// <summary>
    /// ADR-0039 D12.1 — the favourite-cleaner perk is Plus-only, so the slot answer is a membership
    /// benefit rather than a fact of the list. A non-member's row carries <c>null</c> ("not
    /// evaluated", D5's third state), never <c>true</c>/<c>false</c>: the answer is per-subject and
    /// repeating it reconstructs a named cleaner's calendar, so it cannot be free to everyone with
    /// one completed order. The LIST is deliberately NOT gated (A21) — emptying it is a contract
    /// change both clients render as "the picker never existed".
    /// <paramref name="evaluatedAvailability"/> is <c>null</c> whenever the question was not answered:
    /// no slot in the request, a selection the platform would not book, or a check that could not run.
    /// The gate is what guarantees the check can never answer for a non-member.
    /// </summary>
    public static bool? ResolveSlotAvailability(bool hasActiveMembership, bool? evaluatedAvailability) =>
        hasActiveMembership ? evaluatedAvailability : null;

    public class Handler(
        IOrderRepository orderRepository,
        IServiceRepository serviceRepository,
        IPackageRepository packageRepository,
        IUserMembershipRepository userMembershipRepository,
        IUserSessionProvider userSessionProvider,
        ILogger<Handler> logger) : ICommandHandler<Query, IReadOnlyList<Response>>
    {
        public async Task<BusinessResult<IReadOnlyList<Response>>> Handle(Query query, CancellationToken cancellationToken)
        {
            var userId = userSessionProvider.GetUserId()!;

            // ADR-0039 D12.3 — a departing or GDPR-erased cleaner is soft-deleted (IsActive = false)
            // while the historical Completed order survives, and there is no global IsActive filter
            // (S10). Both flags are checked: Deactivated() on the Employee leaves the User alone and
            // vice versa, so either one alone misses half the departures.
            var rows = await orderRepository.GetQueryable()
                .AsNoTracking()
                .Where(o => o.UserId == userId && o.CurrentStatus == OrderStatus.Completed)
                .SelectMany(
                    o => o.AssignedEmployees.Where(a =>
                        a.Employee != null && a.Employee.IsActive &&
                        a.Employee.User != null && a.Employee.User.IsActive),
                    (o, a) => new
                    {
                        a.EmployeeId,
                        a.Employee!.User!.FirstName,
                        a.Employee.User.LastName,
                        o.CleaningDateTime,
                    })
                .GroupBy(x => new { x.EmployeeId, x.FirstName, x.LastName })
                .Select(g => new
                {
                    g.Key.EmployeeId,
                    g.Key.FirstName,
                    g.Key.LastName,
                    LastServedOn = g.Max(x => x.CleaningDateTime),
                })
                .OrderByDescending(r => r.LastServedOn)
                .Take(MaxCleaners)
                .ToListAsync(cancellationToken);

            var membership = await userMembershipRepository.GetActiveForUserNoTrackingAsync(userId, cancellationToken);

            var busy = membership is null || rows.Count == 0
                ? null
                : await BusyInRequestedSlotAsync(query, [.. rows.Select(r => r.EmployeeId)], cancellationToken);

            var result = rows
                .Select(r => new Response(
                    EmployeeId: r.EmployeeId,
                    FullName: $"{r.FirstName} {r.LastName}".Trim(),
                    LastServedOn: r.LastServedOn,
                    IsAvailableForRequestedSlot: ResolveSlotAvailability(
                        membership is not null,
                        evaluatedAvailability: busy is null ? null : !busy.Contains(r.EmployeeId))))
                .ToList();

            return BusinessResult.Success<IReadOnlyList<Response>>(result);
        }

        /// <summary>
        /// The busy subset for the whole candidate list in ONE query — the same repository call, with
        /// the same window, that <c>IPreferredCleanerHoldResolver</c> makes at submit time. If the two
        /// could answer differently the feature has already failed, so they share the method rather than
        /// the rule. <c>null</c> means the question was not answered, which renders as no marking.
        /// </summary>
        private async Task<IReadOnlySet<string>?> BusyInRequestedSlotAsync(
            Query query, IReadOnlyCollection<string> employeeIds, CancellationToken cancellationToken)
        {
            if (query.CleaningDateTimeUtc is not { } cleaningUtc)
            {
                return null;
            }

            try
            {
                var estimatedMinutes = await EstimateMinutesAsync(query, cancellationToken);

                // A span the platform will not create is a question the picker will not answer: the
                // window end is derived from a caller-chosen selection, so without the cap the request
                // carries an unbounded range parameter under another name.
                if (estimatedMinutes <= 0 || BookingPolicy.ExceedsMaxBookableSpan(estimatedMinutes))
                {
                    return null;
                }

                return await orderRepository.GetBusyEmployeeIdsInWindowAsync(
                    employeeIds, cleaningUtc, cleaningUtc.AddMinutes(estimatedMinutes), cancellationToken);
            }
            catch (DbException ex)
            {
                // Degrade to today's behaviour — an unmarked, selectable row — rather than to a lie in
                // either direction. Marking everyone unavailable would empty the picker on both clients.
                logger.LogWarning(ex, "Preferred-cleaner slot availability could not be evaluated");
                return null;
            }
        }

        /// <summary>
        /// The booking's length, summed in SQL over the same catalog rows <c>OrderDuration</c> sums in
        /// memory on the write path. Two implementations of one definition, held together by
        /// <c>OrderDurationAgreementTests</c> — the picker must not answer about a different job than
        /// the one being booked.
        /// </summary>
        private async Task<int> EstimateMinutesAsync(Query query, CancellationToken cancellationToken)
        {
            var serviceIds = query.SelectedServiceIds ?? [];
            var packageIds = query.SelectedPackageIds ?? [];

            var serviceMinutes = serviceIds.Count == 0
                ? 0
                : await serviceRepository.GetByIds(serviceIds).SumAsync(s => s.EstimatedTime, cancellationToken);

            var packageMinutes = packageIds.Count == 0
                ? 0
                : await packageRepository.GetByIds(packageIds)
                    .SelectMany(p => p.IncludedServices)
                    .SumAsync(ps => ps.Service!.EstimatedTime, cancellationToken);

            return serviceMinutes + packageMinutes;
        }
    }
}
