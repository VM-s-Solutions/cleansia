namespace Cleansia.Core.Domain.Orders;

/// <summary>
/// Why the PLATFORM cancelled an order, as a stable key the clients localise.
///
/// <para><b>A key, not a sentence.</b> <see cref="Order.CancellationReason"/> also carries free text
/// when a human cancels, and that text is written by an admin for other staff — it must never be
/// rendered to a customer. Keeping the system reasons as keys is what lets the mapper tell the two
/// apart: it exposes the reason only when <c>CancelledBy</c> is <c>System</c>, so the free-text case
/// cannot leak by accident.</para>
///
/// <para><b>Why a shared constant rather than a literal at each sweep.</b> Three sweeps write these
/// today and every client translates them, so the string is a cross-assembly contract. A typo in a
/// literal would not fail anything — it would reach the customer as an untranslated key, which is
/// exactly the failure this is cheapest to prevent.</para>
/// </summary>
public static class OrderCancellationReasons
{
    /// <summary>
    /// A card booking whose payment never settled. The customer opened the payment sheet and did not
    /// finish, so the order was released rather than left holding a slot nobody paid for.
    ///
    /// <para>This is the one that prompted the whole field: the push said "tap to see why" and the
    /// order detail then said nothing, so the customer had to infer a failed payment from the absence
    /// of a charge.</para>
    /// </summary>
    public const string PaymentNotCompleted = "order.cancelled.payment_not_completed";

    /// <summary>
    /// A recurring occurrence the customer was asked to confirm and did not, cancelled fee-free at the
    /// lead-time cut-off.
    /// </summary>
    public const string RecurringNotConfirmed = "order.cancelled.recurring_not_confirmed";

    /// <summary>
    /// The booking reached its slot with nobody assigned to it. Nobody ever took the job, or everyone
    /// who had taken it came off before the clean.
    ///
    /// <para><b>The one no-show the platform can prove.</b> Every other version of "the cleaner did not
    /// arrive" rests on a missed tap, which is indistinguishable from a cleaner who turned up and forgot
    /// to slide to start. Here there was nobody to tap: the seat was empty at the appointed time, on the
    /// platform's own record. That is why this reason — and only this one — refunds automatically.
    /// → <c>CancelUnfilledOrders</c></para>
    /// </summary>
    public const string NoCleanerAvailable = "order.cancelled.no_cleaner_available";
}
