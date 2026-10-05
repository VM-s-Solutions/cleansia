using Cleansia.Core.AppServices.Services.Interfaces;
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
    /// balance. False means no movement: an ordinary retry, erased account, or no applied credit.
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
    /// Return now the credit leg of a refund whose card leg is left <c>Pending</c> for the re-drive, on the
    /// refund's own key. It is the credit share of the same held slice the refund seam froze the card row
    /// from, so the re-drive reads it back as part of that slice and asks Stripe for the same amount on the
    /// same key, which Stripe may already have paid; a different amount there is refused every time.
    /// </summary>
    public static async Task<bool> ReturnPendingRefundCreditLegAsync(
        this ICreditAccountRepository creditAccountRepository,
        IRefundRepository refundRepository,
        Order order,
        RefundRequest request,
        CancellationToken cancellationToken)
    {
        var alreadyReturned = await creditAccountRepository.GetReturnedTotalForOrderAsync(order.Id, cancellationToken);
        var held = RefundService.HeldToWhatIsLeft(
            order,
            request.Amount,
            await refundRepository.GetSucceededRefundTotalForOrderAsync(order.Id, cancellationToken),
            alreadyReturned,
            await creditAccountRepository.GetDisputeSettledTotalForOrderAsync(order.Id, cancellationToken));
        var (_, creditShare) = RefundService.SplitAcrossTenders(order, held, alreadyReturned);
        return await creditAccountRepository.ReturnCreditAsync(
            order, creditShare, RefundService.BuildRefundKey(request), request.ActorId, cancellationToken);
    }

    /// <summary>What the credit leg of the refund keyed <paramref name="refundKey"/> put back.</summary>
    public static Task<decimal> GetReturnedForRefundAsync(
        this ICreditAccountRepository creditAccountRepository,
        string refundKey,
        CancellationToken cancellationToken) =>
        creditAccountRepository.GetReturnedAmountAsync(KeyPrefix + refundKey, cancellationToken);

    /// <summary>
    /// Give back ALL of an order's credit that has not already come back, because the order ended
    /// without the card being charged — the stale-order sweep, the recurring auto-cancel, an expired
    /// Stripe session, or a customer cancelling before they paid — or ended with no card refund of its
    /// own here: a no-show cancellation whose card refund could not be claimed, or a platform cancellation
    /// of an order already partly refunded. A partial refund's credit leg is not returned twice. A refund
    /// claimed and left pending returns its credit leg on its own key instead
    /// (<see cref="ReturnPendingRefundCreditLegAsync"/>).
    ///
    /// <para><b>Never more than was paid.</b> A complaint settled in credit gave part of the sale back on
    /// neither tender. On an order that took no payment the credit is all that was paid, so the settlement
    /// comes off it in full. On one that took a card payment the whole price was paid, so the credit is held
    /// to what the sale has left after <paramref name="cardRefunded"/>, the credit already returned and the
    /// settlement; netting the settlement off the credit there would keep credit the customer is owed.</para>
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
    public static async Task<bool> ReturnUnpaidOrderCreditAsync(
        this ICreditAccountRepository creditAccountRepository,
        Order order,
        decimal cardRefunded,
        string actorId,
        CancellationToken cancellationToken)
    {
        if (order.CreditAppliedAmount <= 0m)
        {
            return false;
        }

        var alreadyReturned = await creditAccountRepository.GetReturnedTotalForOrderAsync(order.Id, cancellationToken);
        var settledInCredit = await creditAccountRepository.GetDisputeSettledTotalForOrderAsync(order.Id, cancellationToken);
        var paid = order.TookNoPayment ? order.CreditAppliedAmount : order.TotalPrice;
        return await creditAccountRepository.ReturnCreditAsync(
            order,
            Math.Min(
                order.CreditAppliedAmount - alreadyReturned,
                paid - cardRefunded - alreadyReturned - settledInCredit),
            $"order-ended-unpaid:{order.Id}",
            actorId,
            cancellationToken);
    }
}
