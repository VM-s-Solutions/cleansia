using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using Microsoft.EntityFrameworkCore;
using BusinessResult = Cleansia.Infra.Common.Validations.BusinessResult;

namespace Cleansia.Core.AppServices.Features.Orders;

public class GetMyServingCleaners
{
    private const int MaxCleaners = 20;

    public record Query : ICommand<IReadOnlyList<Response>>;

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
    /// <paramref name="evaluatedAvailability"/> is <c>null</c> until the availability check itself
    /// ships; the gate is what guarantees the check can never answer for a non-member.
    /// </summary>
    public static bool? ResolveSlotAvailability(bool hasActiveMembership, bool? evaluatedAvailability) =>
        hasActiveMembership ? evaluatedAvailability : null;

    public class Handler(
        IOrderRepository orderRepository,
        IUserMembershipRepository userMembershipRepository,
        IUserSessionProvider userSessionProvider) : ICommandHandler<Query, IReadOnlyList<Response>>
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

            var result = rows
                .Select(r => new Response(
                    EmployeeId: r.EmployeeId,
                    FullName: $"{r.FirstName} {r.LastName}".Trim(),
                    LastServedOn: r.LastServedOn,
                    IsAvailableForRequestedSlot: ResolveSlotAvailability(
                        membership is not null,
                        evaluatedAvailability: null)))
                .ToList();

            return BusinessResult.Success<IReadOnlyList<Response>>(result);
        }
    }
}
