using Cleansia.Core.Domain.Orders;

namespace Cleansia.Core.AppServices.Features.Orders;

/// <summary>
/// ADR-0045 D5.1 / D7.2 — whether the customer may still name a second cleaner on this order. The ONE
/// evaluation, called by <c>ChoosePreferredCleaner</c> as its gate and by the order-detail mapper as
/// <c>canChooseAnother</c>. Never two implementations: a flag that reads <see langword="true"/> where
/// the server refuses is the defect this ADR ruled blocking, and the read side must therefore be the
/// write side's own answer rather than a copy of its terms.
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
    public static bool IsOpen(Order order, DateTime nowUtc)
        => order.RecurringTemplateId is null
           && order.PreferredOfferRound < BookingPolicy.MaxPreferredOfferRounds
           && order.AssignedEmployees.Count == 0
           && !PreferredOffer.HasLiveReservation(
               order.PreferredEmployeeId, order.PreferredHoldUntilUtc, nowUtc)
           && BookingPolicy.ComputePreferredHold(order.CleaningDateTime, nowUtc) > TimeSpan.Zero;
}
