using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Clients.Abstractions.Stripe;
using Cleansia.Core.Domain.Credit;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Payments;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using StripeException = Stripe.StripeException;

namespace Cleansia.Core.AppServices.Services;

public sealed class RefundService(
    IRefundRepository refundRepository,
    IOrderRepository orderRepository,
    ICreditAccountRepository creditAccountRepository,
    IStripeClientFactory stripeClientFactory,
    ILogger<RefundService> logger) : IRefundService
{
    public async Task<BusinessResult<RefundResult>> IssueRefundAsync(
        RefundRequest request, CancellationToken cancellationToken)
    {
        var order = await orderRepository.GetByIdAsync(request.OrderId, cancellationToken);
        if (order is null)
        {
            return BusinessResult.Failure<RefundResult>(new Error(
                nameof(request.OrderId), BusinessErrorMessage.OrderNotFound));
        }

        var refundKey = BuildRefundKey(request);

        // Split the requested slice of the SALE across the two tenders it was settled with, before
        // anything else looks at an amount. On an order that took no credit this is the identity - the
        // card share IS the request - so every shipped refund is byte-unchanged.
        //
        // The credit leg nets off what has ALREADY gone back, exactly as the card leg nets off
        // `consumed` below. Without it the two legs use different denominators: a second refund
        // request would have its card share clamped by the ceiling while its credit share was
        // recomputed from the full CreditAppliedAmount, and a customer could be handed their credit
        // twice by asking for a full refund twice under two different purposes.
        var creditAlreadyReturned = await creditAccountRepository.GetReturnedTotalForOrderAsync(
            order.Id, cancellationToken);
        var split = SplitAcrossTenders(order, request.Amount, creditAlreadyReturned);

        // Resolve-to-existing ONLY for a terminally-Succeeded refund. A Pending/Failed row from a prior
        // attempt whose Stripe call never confirmed must NOT short-circuit as success — it has to be
        // re-driven through Stripe (the deterministic refundKey is Stripe's idempotency key, so a replay
        // issues the refund exactly once). Returning a Pending row as success is the phantom-refund bug:
        // the money never moved but the caller would notify the customer it did.
        var existing = await refundRepository.GetByRefundKeyAsync(refundKey, cancellationToken);
        if (existing is { Status: RefundStatus.Succeeded })
        {
            return ResolveToExisting(existing);
        }

        if (!order.HasRefundableChargeSurface)
        {
            return BusinessResult.Failure<RefundResult>(new Error(
                nameof(request.OrderId), BusinessErrorMessage.RefundOrderNotRefundable));
        }

        Refund refund;
        if (existing is not null)
        {
            // A prior Pending/Failed attempt exists — reuse its row (do NOT insert a second) and re-drive
            // Stripe with the same key below. But RE-CHECK the live refundable ceiling first: RefundKeys are
            // per-purpose, so a different-purpose refund (e.g. an admin refund) may have SUCCEEDED since this
            // row was created, dropping TotalPrice - consumed below the row's frozen amount. Re-driving the
            // stale amount would over-refund; clamp it to the live ceiling (or fail if nothing remains).
            var consumed = await refundRepository.GetSucceededRefundTotalForOrderAsync(
                order.Id, cancellationToken);
            var refundable = CardRefundCeiling(order, consumed);
            if (refundable <= 0m)
            {
                return BusinessResult.Failure<RefundResult>(new Error(
                    nameof(request.Amount), BusinessErrorMessage.RefundNothingRefundable));
            }

            existing.ClampAmountTo(refundable);
            refund = existing;
        }
        else
        {
            var consumed = await refundRepository.GetSucceededRefundTotalForOrderAsync(
                order.Id, cancellationToken);
            var refundable = CardRefundCeiling(order, consumed);
            var amount = Math.Min(split.Card, refundable);
            if (amount <= 0m)
            {
                return BusinessResult.Failure<RefundResult>(new Error(
                    nameof(request.Amount), BusinessErrorMessage.RefundNothingRefundable));
            }

            // Claim the key BEFORE Stripe so a concurrent double-issue has exactly one winner: the loser's
            // insert collides on the unique RefundKey index (PG 23505, S7a/S7b, ADR-0006 D3). The row is
            // Pending here; its Succeeded status + the payment-status flip are written only after Stripe
            // confirms (D7).
            refund = Refund.Create(
                orderId: order.Id,
                refundKey: refundKey,
                amount: amount,
                currency: order.Currency.Code,
                reason: request.Reason,
                source: RefundSource.AppRefund,
                disputeId: request.DisputeId,
                windowOverrideReason: request.WindowOverrideReason);
            refundRepository.Add(refund);

            try
            {
                await refundRepository.CommitAsync(cancellationToken);
            }
            catch (DbUpdateException ex) when (IsUniqueViolation(ex))
            {
                refundRepository.Rollback();
                var winner = await refundRepository.GetByRefundKeyAsync(refundKey, cancellationToken);
                if (winner is null)
                {
                    throw;
                }

                if (winner.Status == RefundStatus.Succeeded)
                {
                    logger.LogInformation(
                        "Refund collapsed on RefundKey unique-violation for order {OrderId} — resolved to existing succeeded refund {RefundId}, no second Stripe refund issued.",
                        order.Id, winner.Id);
                    return ResolveToExisting(winner);
                }

                // The winner is Pending/Failed — re-drive its Stripe call (same key → Stripe replays once).
                refund = winner;
            }
        }

        var stripe = stripeClientFactory.CreateClient();
        try
        {
            // Route by charge surface: a web order carries a Checkout Session; a mobile (PaymentSheet)
            // order carries only a PaymentIntent (T-0347 suppresses its Session). Prefer the Session when
            // present so the established web refund path is byte-unchanged.
            if (!string.IsNullOrEmpty(order.StripeSessionId))
            {
                await stripe.RefundCheckoutSessionAsync(order.StripeSessionId, refund.Amount, refundKey, cancellationToken);
            }
            else
            {
                await stripe.RefundPaymentIntentAsync(order.StripePaymentIntentId!, refund.Amount, refundKey, cancellationToken);
            }
        }
        catch (StripeException ex)
        {
            // Confirm-then-record (ADR-0006): the Refund row stays Pending and PaymentStatus is left
            // un-flipped, so a failed Stripe call never produces a phantom Refunded — and the caller gets a
            // Failure, never a false "refund initiated". A later retry re-enters here on the same key and
            // re-drives Stripe (idempotent), so the refund is eventually issued exactly once.
            logger.LogError(ex,
                "Stripe refund failed for order {OrderId} on key {RefundKey}; refund left pending for retry.",
                order.Id, refundKey);
            return BusinessResult.Failure<RefundResult>(new Error(
                nameof(request.Amount), BusinessErrorMessage.RefundFailed));
        }

        var succeededConsumed = await refundRepository.GetSucceededRefundTotalForOrderAsync(
            order.Id, cancellationToken);
        refund.MarkSucceeded(stripeRefundId: null, confirmedOnUtc: DateTimeOffset.UtcNow);

        // The card leg is settled; give back the credit leg too, in the SAME commit. Keyed off the
        // deterministic refund key, so a re-driven refund returns the credit exactly once.
        await ReturnCreditShareAsync(order, split.Credit, refundKey, request.ActorId, cancellationToken);

        // FULLY REFUNDED IS A TEST ON THE CARD LEG AGAINST THE CARD TOTAL, not against TotalPrice.
        // GetSucceededRefundTotalForOrderAsync sums the Refunds table, which holds card refunds only —
        // so on an order settled with 500 credit and 1500 card, comparing against 2000 could never be
        // reached and the order would sit PartiallyRefunded forever. The split above is proportional,
        // so the credit leg is exhausted at exactly the moment the card leg is, and this one comparison
        // is true for both. On an order that took no credit it reduces to the original expression.
        var cardTotal = order.TotalPrice - order.CreditAppliedAmount;
        order.UpdatePaymentStatus(succeededConsumed + refund.Amount >= cardTotal
            ? PaymentStatus.Refunded
            : PaymentStatus.PartiallyRefunded);
        await refundRepository.CommitAsync(cancellationToken);

        logger.LogInformation(
            "Refund {RefundId} issued for order {OrderId}: {Amount} {Currency} ({Reason}).",
            refund.Id, order.Id, refund.Amount, order.Currency.Code, request.Reason);

        return BusinessResult.Success(new RefundResult(
            RefundId: refund.Id,
            RefundKey: refundKey,
            Amount: refund.Amount,
            Status: RefundStatus.Succeeded,
            ResolvedToExisting: false));
    }

    private static BusinessResult<RefundResult> ResolveToExisting(Refund existing) =>
        BusinessResult.Success(new RefundResult(
            RefundId: existing.Id,
            RefundKey: existing.RefundKey,
            Amount: existing.Amount,
            Status: existing.Status,
            ResolvedToExisting: true));

    /// <summary>
    /// The most that can still go back to the CARD.
    ///
    /// <para>It is not the order total. Credit is a tender: an order settled with 500 of credit and
    /// 1500 of card is a 2000 sale, but only 1500 ever reached Stripe — and the card cannot give back
    /// money the card never took. Without subtracting it, a customer who paid partly in credit could
    /// be refunded the whole 2000 in cash, converting credit into money at will.</para>
    ///
    /// <para>The credit half goes back on its own leg — see <see cref="SplitAcrossTenders"/>. It used
    /// to be an admin's job to re-issue it by hand, which read as human-in-the-loop but was not: the
    /// ruling is about DECIDING to compensate a customer, and unwinding a tender the platform already
    /// took is not a decision. Left manual it was simply money the customer lost.</para>
    ///
    /// <para>Both call sites above compute this, which is why it is one function: they drifted apart
    /// once already in this file's history and the result was a stale amount being re-driven.</para>
    /// </summary>
    public static decimal CardRefundCeiling(Order order, decimal consumed) =>
        CardChargedAmount(order) - consumed;

    /// <summary>
    /// What the card was ever asked for. The sale, less whatever credit settled.
    ///
    /// <para><b>This is the denominator for "fully refunded", not <c>TotalPrice</c>.</b>
    /// <c>GetSucceededRefundTotalForOrderAsync</c> sums the Refunds table, which holds CARD refunds
    /// only — so on a 2000 order settled with 500 credit, giving back every cent the card ever took
    /// reaches 1500 and stops. Compared against 2000 that reads as PartiallyRefunded, and no further
    /// refund can ever close the gap because this ceiling is by then zero. The order would be stuck
    /// mid-refund forever, on money that had entirely gone back.</para>
    ///
    /// <para>It is public and shared for the reason the docstring above already gives about this
    /// file's history: the ceiling and the terminal test drifted apart once, and the fix was to make
    /// them one expression rather than two that agree today.</para>
    /// </summary>
    public static decimal CardChargedAmount(Order order) =>
        order.TotalPrice - order.CreditAppliedAmount;

    /// <summary>
    /// How a slice of the sale divides across the two tenders that settled it.
    ///
    /// <para>PROPORTIONAL, deliberately. A 2000 order settled with 500 credit and 1500 card, cancelled
    /// at a 50% fee, gives back 750 to the card and 250 to the balance — the customer has paid 1000 in
    /// total, in the same mix they paid it. The alternatives both pick a winner: card-first hands back
    /// real money and lets the credit expire against the fee, credit-first does the reverse. Neither is
    /// more correct, and proportional needs nobody to choose.</para>
    ///
    /// <para>Rounded to whole minor units with the CARD taking the remainder, so the two legs always
    /// sum to exactly the requested amount and Stripe is never handed a fraction of a cent.</para>
    ///
    /// <para>An order that took no credit splits to (request, 0) — the identity. That is why this can
    /// sit in front of every refund the platform issues without changing any of them.</para>
    /// </summary>
    public static (decimal Card, decimal Credit) SplitAcrossTenders(
        Order order, decimal requested, decimal creditAlreadyReturned = 0m)
    {
        if (requested <= 0m || order.CreditAppliedAmount <= 0m || order.TotalPrice <= 0m)
        {
            return (Math.Max(0m, requested), 0m);
        }

        var slice = Math.Min(requested, order.TotalPrice);
        var credit = Math.Round(
            slice * order.CreditAppliedAmount / order.TotalPrice, 2, MidpointRounding.AwayFromZero);

        // Never give back more credit than is still out — not merely more than was applied. The card
        // leg is clamped by the caller against a ceiling that already subtracts prior refunds; this is
        // the credit leg's half of the same subtraction, and without it the two legs disagree about
        // how much of the order has already been unwound.
        var creditRemaining = Math.Max(0m, order.CreditAppliedAmount - creditAlreadyReturned);
        credit = Math.Min(credit, creditRemaining);

        return (slice - credit, credit);
    }

    /// <summary>
    /// Put the credit leg back on the customer's balance, through the one place that builds a return
    /// key. A false answer is the ordinary retry — the key was already used — not a failure, because
    /// the money is already back.
    /// </summary>
    private async Task ReturnCreditShareAsync(
        Order order, decimal creditShare, string refundKey, string actorId, CancellationToken cancellationToken)
    {
        var returned = await creditAccountRepository.ReturnCreditAsync(
            order, creditShare, refundKey, actorId, cancellationToken);

        if (!returned && creditShare > 0m)
        {
            logger.LogInformation(
                "Credit return of {Amount} for order {OrderId} was a no-op on refund key {RefundKey} — already returned.",
                creditShare, order.Id, refundKey);
        }
    }


    // RefundKey = refund:{OrderId}:{purpose}[:{DisputeId}][:{RefundRequestId}] (ADR-0006 D3).
    // Deterministic on the domain inputs, never a Guid/timestamp, so a retry/redelivery and a
    // concurrent double-issue collapse onto the one key.
    //
    // THE DISTINGUISHING ID IS NOW HONOURED ON EVERY REASON. It used to appear only in the `admin`
    // branch, so a caller that passed one under CustomerCancellation or DisputeResolution had it
    // silently dropped — and IssuePartialRefund passes exactly that, its line selection. Two
    // different partial refunds on one order therefore built the SAME key, the second resolved to
    // the first's succeeded row, and the handler reported success while no money moved.
    //
    // Every shipped key is byte-identical under this shape, which is why it is safe: CancelOrder and
    // AdminCancelOrder pass neither optional id (refund:{id}:cancel), ResolveDispute passes only a
    // DisputeId (refund:{id}:dispute:{did}), and AdminRefundOrder passes only RefundRequestId "full"
    // (refund:{id}:admin:full). Only the partial-refund path gains a segment — the one that needed it.
    // Public for the same reason StripeClient.ToMinorUnits is: it is a pure function whose exact
    // output is the contract, and the only honest way to test it is to call it. The fake in
    // IssuePartialRefundHandlerTests used to RESTATE this algorithm instead — which is precisely why
    // two of its three branches went unexercised while the suite stayed green.
    public static string BuildRefundKey(RefundRequest request)
    {
        var segments = new List<string>
        {
            "refund",
            request.OrderId,
            request.Reason switch
            {
                RefundReason.CustomerCancellation => "cancel",
                RefundReason.DisputeResolution => "dispute",
                _ => "admin",
            },
        };

        if (!string.IsNullOrEmpty(request.DisputeId))
        {
            segments.Add(request.DisputeId);
        }

        if (!string.IsNullOrEmpty(request.RefundRequestId))
        {
            segments.Add(request.RefundRequestId);
        }

        return string.Join(':', segments);
    }

    private static bool IsUniqueViolation(DbUpdateException exception)
    {
        const string UniqueViolation = "23505";
        for (Exception? inner = exception.InnerException; inner is not null; inner = inner.InnerException)
        {
            var sqlState = inner.GetType()
                .GetProperty("SqlState")?
                .GetValue(inner) as string;
            if (sqlState == UniqueViolation)
            {
                return true;
            }
        }

        return false;
    }
}
