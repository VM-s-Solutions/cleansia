namespace Cleansia.Infra.Common.Configuration.Interfaces;

public interface IStripeConfig
{
    /// <summary>
    /// Master switch for taking NEW card payments (section <c>Stripe</c>, key <c>Enabled</c>). Defaults
    /// to <c>true</c>, so an absent setting means card payments work — the switch has to be typed to
    /// turn them off, never inferred from a missing value.
    ///
    /// <para>When false, <b>every surface that CREATES a charge</b> refuses with
    /// <c>order.payment_gateway_unavailable</c>. There are seven, and all seven are gated — an earlier
    /// draft of this switch gated only the first three and left membership and recurring charges live,
    /// which is worse than no switch at all because an operator watching order revenue stop would
    /// reasonably believe card capture had stopped platform-wide:</para>
    /// <list type="bullet">
    /// <item>web checkout session — <c>OrderPaymentDispatcher</c></item>
    /// <item>resume checkout session — <c>ResumeOrderCheckout</c></item>
    /// <item>mobile PaymentSheet intent — <c>CreatePaymentIntent</c></item>
    /// <item>recurring-occurrence card confirm — <c>ConfirmRecurringOrder</c></item>
    /// <item>membership subscription (first invoice bills immediately) — <c>CreateMembershipSubscription</c></item>
    /// <item>membership web checkout — <c>CreateMembershipCheckoutSession</c></item>
    /// <item>membership plan swap (prorates and charges) — <c>SwapMembershipPlan</c></item>
    /// </list>
    /// <para>Cash is unaffected, so the platform keeps trading.</para>
    ///
    /// <para><b>What is deliberately NOT gated, and why.</b> Everything that RETURNS or RELEASES money,
    /// because switching card payments off is exactly when those are needed — a switch that froze them
    /// would trap customer money behind the incident it was flipped for: <c>RefundService</c> (refunds),
    /// <c>MarkCashCollected</c> (cancels an uncaptured intent), <c>CancelMembershipSubscription</c>
    /// (cancellation), and <c>GdprDeletionService</c> (deletes the Stripe customer — an erasure
    /// obligation that cannot wait on an ops toggle).</para>
    ///
    /// <para><b>Adding a new Stripe call?</b> If it creates a charge, gate it and add it to the list
    /// above. <c>CardPaymentsChargeSurfaceCoverageTests</c> fails when a handler takes
    /// <c>IStripeClient</c> without <c>IStripeConfig</c> and is not on its reviewed exemption list.</para>
    /// </summary>
    bool Enabled { get; set; }

    string SecretKey { get; set; }
    string PublishableKey { get; set; }
    string WebhookSecret { get; set; }
    string SuccessUrlBase { get; set; }
    string CancelUrlBase { get; set; }
}