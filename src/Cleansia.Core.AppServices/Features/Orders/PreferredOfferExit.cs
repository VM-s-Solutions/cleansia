using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;

namespace Cleansia.Core.AppServices.Features.Orders;

/// <summary>
/// ADR-0045 D5.1 / D7.2 — whether the customer may still name a second cleaner on this order. The ONE
/// evaluation, called by <c>ChoosePreferredCleaner</c> as its gate and by the order-detail mapper as
/// <c>canChooseAnother</c>. Never two implementations: a flag that reads <see langword="true"/> where
/// the server refuses is the defect this ADR ruled blocking, and the read side must therefore be the
/// write side's own answer rather than a copy of its terms.
///
/// <para><b>Offerability is conjoined, never re-derived</b> (ADR-0037). Without it a CANCELLED booking
/// with a future cleaning time and nobody on it satisfied every other term: the exit would have granted
/// an exclusive hold and pushed a named cleaner about work nobody will do. That is the failure the
/// owner's <c>Q-BROWSE-01</c> ruling (b) closed one entry point over, so it is spelled with the same
/// rule every other surface reads — a status list here would admit the unpaid card booking, which is
/// not a fulfilment state at all. <c>Completed</c> was already blocked, but only incidentally, by the
/// assignment count.</para>
///
/// <para><b>The caller's entitlement is a term, not a client's job.</b> The command's first gate is an
/// active Plus membership, so a rule that spoke only about the order offered the button to every
/// non-member and refused it on tap. It is an explicit parameter rather than a lookup inside, so a call
/// site that has not answered the question cannot compile.</para>
///
/// <para>The lead-time term goes through <c>BookingPolicy.ComputePreferredHold</c> and never a client
/// constant — a device that hard-codes eight hours is a second copy of the policy on a surface the
/// platform cannot redeploy. It is a SNAPSHOT and may go stale between render and tap; the command is
/// the gate and the client tolerates the refusal.</para>
///
/// <para>The exit is a LONG-LEAD affordance by construction: <c>ComputePreferredHold</c> returns zero
/// below <c>2 * StandardLeadTimeHours</c>, so the last eight hours of every fill window can never carry
/// a second reservation — 100% / 74% / 37% / 7% of the post-lapse window at 8 / 12 / 24 / 120 h leads.
/// That bound is disclosed rather than hidden: a flag that hides its bound is worse than a feature
/// that has one.</para>
/// </summary>
public static class PreferredOfferExit
{
    public static bool IsOpen(Order order, bool callerHasActiveMembership, DateTime nowUtc)
        => callerHasActiveMembership
           && order.RecurringTemplateId is null
           && OrderAvailability.IsOfferable(
               order.CurrentStatus, order.PaymentType, order.PaymentStatus, order.RecurringTemplateId)
           && order.PreferredOfferRound < BookingPolicy.MaxPreferredOfferRounds
           && order.AssignedEmployees.Count == 0
           && !PreferredOffer.HasLiveReservation(
               order.PreferredEmployeeId, order.PreferredHoldUntilUtc, nowUtc)
           && BookingPolicy.ComputePreferredHold(order.CleaningDateTime, nowUtc) > TimeSpan.Zero;

    /// <summary>
    /// The entitlement input to <see cref="IsOpen"/>, resolved once here so the flag and the command
    /// cannot drift apart on what "a member" means. A caller with no user id is nobody's member.
    /// </summary>
    public static async Task<bool> CallerHasActiveMembershipAsync(
        IUserSessionProvider userSessionProvider,
        IUserMembershipRepository userMembershipRepository,
        CancellationToken cancellationToken)
    {
        var userId = userSessionProvider.GetUserId();

        return !string.IsNullOrEmpty(userId)
            && await userMembershipRepository
                .GetActiveForUserNoTrackingAsync(userId, cancellationToken) is not null;
    }
}
