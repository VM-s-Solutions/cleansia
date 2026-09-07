using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;

namespace Cleansia.Core.AppServices.Services;

/// <summary>
/// Putting an order's credit back, from wherever that order ended.
///
/// <para><b>Why this exists as one place.</b> Credit is spent at checkout by exactly one path, but an
/// order can END in six: the customer cancels, an admin cancels, an admin refunds in full or in part,
/// a dispute resolves, the stale-order sweep gives up, the recurring auto-cancel gives up, or Stripe
/// tells us the session expired. Every one of those has to give the credit back, and every one of them
/// has to build the SAME idempotency key or a retry pays the customer twice. Five copies of that key
/// format is five chances to write it differently.</para>
///
/// <para>It is a pair of extension methods rather than a service because it holds no state and needs
/// no lifetime: the repository it extends is already injected at every call site that needs it, and an
/// interface here would buy a registration, a mock in every affected test, and nothing else.</para>
/// </summary>
public static class CreditUnwind
{
    /// <summary>
    /// Namespaces every return key, so a ledger row's provenance is legible without joining anything.
    /// </summary>
    public const string KeyPrefix = "credit-return:";

    /// <summary>
    /// Return <paramref name="amount"/> of <paramref name="order"/>'s applied credit to the customer's
    /// balance. False means the key was already used — the ordinary retry, and a no-op, not a failure.
    ///
    /// <para><paramref name="keyDiscriminator"/> must be DETERMINISTIC on the domain inputs, never a
    /// Guid or a timestamp: it is the whole idempotency story. A refund passes its own already-
    /// deterministic RefundKey; a cancellation passes the order id.</para>
    /// </summary>
    public static Task<bool> ReturnCreditAsync(
        this ICreditAccountRepository creditAccountRepository,
        Order order,
        decimal amount,
        string keyDiscriminator,
        string actorId,
        CancellationToken cancellationToken)
    {
        if (amount <= 0m || string.IsNullOrEmpty(order.UserId))
        {
            return Task.FromResult(false);
        }

        return creditAccountRepository.TryReturnAsync(
            userId: order.UserId,
            currencyId: order.CurrencyId,
            amount: amount,
            idempotencyKey: KeyPrefix + keyDiscriminator,
            actorId: actorId,
            cancellationToken: cancellationToken,
            orderId: order.Id);
    }

    /// <summary>
    /// Give back ALL of an order's credit, because the order ended without the card ever being
    /// charged — the stale-order sweep, the recurring auto-cancel, an expired Stripe session, or a
    /// customer cancelling before they paid.
    ///
    /// <para><b>All of it, with no cancellation fee taken out.</b> On an unpaid order the platform
    /// collects nothing: there is no charge surface, so the fee the assessor computed is unrecoverable
    /// whatever we do here. Keeping a slice of the customer's credit would make it the ONLY fee ever
    /// collected on an unpaid cancellation — a rule that applies to precisely the customers who happen
    /// to have been compensated for a previous bad clean, and to nobody else.</para>
    ///
    /// <para>Keyed on the order id alone. An order can only end once, so a re-run of a sweep, a
    /// re-delivered webhook and a double-cancel all collapse onto the one ledger row.</para>
    /// </summary>
    public static Task<bool> ReturnUnpaidOrderCreditAsync(
        this ICreditAccountRepository creditAccountRepository,
        Order order,
        string actorId,
        CancellationToken cancellationToken) =>
        creditAccountRepository.ReturnCreditAsync(
            order,
            order.CreditAppliedAmount,
            $"order-ended-unpaid:{order.Id}",
            actorId,
            cancellationToken);
}
