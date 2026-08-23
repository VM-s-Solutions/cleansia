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
            .GetActiveForUserNoTrackingAsync(userId, cancellationToken);

        // PastDue and expired enrolments are excluded by that ONE predicate, shared with every other
        // benefit. No second membership predicate is invented here (owner ruling: PastDue keeps nothing,
        // cut on first payment failure).
        if (membership == null)
        {
            return noWaiver;
        }

        var plan = membership.MembershipPlan;
        if (plan == null || !plan.AllowsExpressUpgrade || plan.ExpressUpgradesPerMonth <= 0)
        {
            return noWaiver;
        }

        // A benefit-specific narrowing layered OVER the shared predicate, and deliberately not folded
        // into it: the shared predicate answers "is there a live membership", this conjunct answers "has
        // it been paid for yet". A trialing member IS active and keeps the discount and the cancellation
        // window — harmonizing IsInTrialAt into ActiveForUserQuery would silently strip both.
        // The quota still reports the plan's number so the client can say when waivers start, rather
        // than rendering a bare zero that reads as "you used them up".
        if (membership.IsInTrialAt(nowUtc))
        {
            return noWaiver with { Quota = plan.ExpressUpgradesPerMonth };
        }

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
