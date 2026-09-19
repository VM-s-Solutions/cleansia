using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Memberships;
using Cleansia.Core.Domain.Repositories;

namespace Cleansia.Core.AppServices.Services;

/// <inheritdoc cref="IExpressWaiverResolver"/>
public sealed class ExpressWaiverResolver(
    IUserMembershipRepository userMembershipRepository,
    IMembershipBenefitUsageRepository benefitUsageRepository,
    IBenefitPeriodKeyFactory periodKeyFactory) : IExpressWaiverResolver
{
    public async Task<ExpressWaiver> ResolveForUserAsync(
        string? userId,
        DateTime? cleaningUtc,
        DateTime nowUtc,
        CancellationToken cancellationToken)
    {
        var periodKey = await periodKeyFactory.GetCalendarKeyAsync(nowUtc, cancellationToken);

        // The window is a property of the SLOT, not of the member, so it is answered for everyone —
        // including guests and exhausted members, whose clients need to distinguish "express, charged"
        // from "not an express slot at all". BookingPolicy owns the rule; the resolver never re-encodes it.
        var inExpressWindow = cleaningUtc.HasValue
            && BookingPolicy.RequiresExpressSurcharge(cleaningUtc.Value, nowUtc);

        var noWaiver = new ExpressWaiver(
            InExpressWindow: inExpressWindow,
            Waived: false,
            Quota: 0,
            RemainingBeforeThisBooking: 0,
            PeriodKey: periodKey);

        if (string.IsNullOrEmpty(userId))
        {
            return noWaiver;
        }

        var membership = await userMembershipRepository
            .GetEntitledForUserNoTrackingAsync(userId, cancellationToken);

        // PastDue, expired AND trialing enrolments are all excluded by that one ENTITLEMENT predicate,
        // shared with every other benefit. No second membership predicate is invented here (owner rulings:
        // PastDue keeps nothing, cut on first payment failure; and 2026-09-08, no benefit before payment).
        if (membership == null)
        {
            return noWaiver;
        }

        var plan = membership.MembershipPlan;
        if (plan == null || !plan.AllowsExpressUpgrade || plan.ExpressUpgradesPerMonth <= 0)
        {
            return noWaiver;
        }

        // The trial narrowing that used to live HERE has moved into the shared entitlement predicate
        // (T-0690, owner ruling 2026-09-08). It was benefit-specific because a trialing member kept the
        // discount and the cancellation window and lost only the metered waiver; under the new ruling a
        // trialing member is entitled to nothing, so the narrowing belongs to every benefit at once and
        // this branch became unreachable. It is deleted rather than left dead: the three-way client state
        // it produced (available / exhausted / waivers-start-on-DATE) has no producer any more, because
        // both admin plan commands now refuse a non-zero trial period.
        var used = await benefitUsageRepository.CountLiveInPeriodAsync(
            userId, MembershipBenefitKind.ExpressUpgrade, periodKey, cancellationToken);

        // The quota is read from the CURRENT plan while the count is over the period, so a mid-month
        // downgrade with more already used clamps to 0 and the granted waivers are not clawed back.
        var remaining = Math.Max(0, plan.ExpressUpgradesPerMonth - used);

        return new ExpressWaiver(
            InExpressWindow: inExpressWindow,
            Waived: inExpressWindow && remaining > 0,
            Quota: plan.ExpressUpgradesPerMonth,
            RemainingBeforeThisBooking: remaining,
            PeriodKey: periodKey,
            UserId: userId,
            UserMembershipId: membership.Id);
    }
}
