using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Clients.Abstractions.Stripe;
using Cleansia.Core.Domain.Credit;
using Cleansia.Core.Domain.Disputes;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Payments;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Polly;
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

        // Resolve-to-existing ONLY for a terminally-Succeeded refund. A Pending/Failed row from a prior
        // attempt whose Stripe call never confirmed must NOT short-circuit as success — it has to be
        // re-driven through Stripe (the deterministic refundKey is Stripe's idempotency key, so a replay
        // issues the refund exactly once). Returning a Pending row as success is the phantom-refund bug:
        // the money never moved but the caller would notify the customer it did.
        var existing = await refundRepository.GetByRefundKeyAsync(refundKey, cancellationToken);
        if (existing is { Status: RefundStatus.Succeeded })
        {
            return await ResolveToExistingAsync(existing, cancellationToken);
        }

        if (!order.HasRefundableChargeSurface)
        {
            return BusinessResult.Failure<RefundResult>(new Error(
                nameof(request.OrderId), BusinessErrorMessage.RefundOrderNotRefundable));
        }

        // Split the requested slice of the SALE across the two tenders it was settled with, before
        // anything else looks at an amount. On an order that took no credit this is the identity - the
        // card share IS the request - so every shipped refund is byte-unchanged.
        //
        // The credit leg nets off what has ALREADY gone back, exactly as the card leg nets off
        // `consumed` below. Without it the two legs use different denominators: a second refund
        // request would have its card share clamped by the ceiling while its credit share was
        // recomputed from the full CreditAppliedAmount, and a customer could be handed their credit
        // twice by asking for a full refund twice under two different purposes.
        var creditOnThisKey = await creditAccountRepository.GetReturnedForRefundAsync(refundKey, cancellationToken);
        var creditAlreadyReturned = await creditAccountRepository.GetReturnedTotalForOrderAsync(
            order.Id, cancellationToken) - creditOnThisKey;
        var settledInCredit = await creditAccountRepository.GetDisputeSettledTotalForOrderAsync(
            order.Id, cancellationToken);
        var held = await HeldToWhatIsLeftAsync(
            order, request.Amount, refundKey, creditAlreadyReturned, settledInCredit, cancellationToken);
        var split = SplitAcrossTenders(order, held, creditAlreadyReturned);
        if (creditOnThisKey > 0m)
        {
            // This key's credit leg already came back and is part of the slice: the card takes only what the
            // slice has left after it, which is the very card share it had when nothing has moved since, so
            // Stripe sees the same amount on the same key.
            split = (Math.Min(split.Card, held - creditOnThisKey), 0m);
        }

        var consumed = existing is null
            ? await CardRefundedOrOwedAsync(refundRepository, order.Id, refundKey, cancellationToken)
            : await CardRefundedOrOwedBeforeRetryAsync(existing, cancellationToken);
        var refundable = CardRefundCeiling(order, consumed);
        Refund refund;
        var creditShare = split.Credit;
        if (existing is not null)
        {
            // A prior Pending/Failed attempt exists — reuse its row (do NOT insert a second) and re-drive
            // Stripe with the same key below. Stripe may already have paid this row on this key and refuses
            // a different amount there, so the row keeps its amount whenever what is left allows it: every
            // other refund and settlement counted it as owed. The clamps below only bite on a row frozen
            // before that was so. A complaint settled in credit since is taken from this row's credit leg,
            // not its card.
            if (settledInCredit > 0m)
            {
                refundable = Math.Min(refundable, held - creditOnThisKey);
            }

            if (refundable <= 0m)
            {
                return BusinessResult.Failure<RefundResult>(new Error(
                    nameof(request.Amount), BusinessErrorMessage.RefundNothingRefundable));
            }

            existing.ClampAmountTo(refundable);
            refund = existing;
            creditShare = CreditBesideCard(held, refund.Amount, split.Credit, creditOnThisKey);
        }
        else
        {
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
                currency: order.Currency?.Code
                          ?? throw new InvalidOperationException(
                              $"Order {order.Id} has no resolved currency; a refund cannot be denominated."),
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
                    return await ResolveToExistingAsync(winner, cancellationToken);
                }

                // The winner is Pending/Failed — re-drive its Stripe call (same key → Stripe replays once).
                refund = winner;
            }
        }

        return await SettleAsync(order, refund, creditShare, request.ActorId, cancellationToken);
    }

    public async Task<BusinessResult<RefundResult>> RedriveAsync(
        string refundId, string actorId, CancellationToken cancellationToken)
    {
        var refund = await refundRepository.GetByIdAsync(refundId, cancellationToken);
        if (refund is null)
        {
            return BusinessResult.Failure<RefundResult>(new Error(
                nameof(refundId), BusinessErrorMessage.RefundFailed));
        }

        if (refund.Status == RefundStatus.Succeeded)
        {
            return await ResolveToExistingAsync(refund, cancellationToken);
        }

        var order = await orderRepository.GetByIdAsync(refund.OrderId, cancellationToken);
        if (order is null)
        {
            return BusinessResult.Failure<RefundResult>(new Error(
                nameof(refund.OrderId), BusinessErrorMessage.OrderNotFound));
        }

        if (!order.HasRefundableChargeSurface)
        {
            refund.MarkFailed();
            return BusinessResult.Failure<RefundResult>(new Error(
                nameof(refund.OrderId), BusinessErrorMessage.RefundOrderNotRefundable));
        }

        var consumed = await CardRefundedOrOwedBeforeRetryAsync(refund, cancellationToken);
        var refundable = CardRefundCeiling(order, consumed);
        if (refundable <= 0m)
        {
            var confirmed = await refundRepository.GetSucceededRefundTotalForOrderAsync(order.Id, cancellationToken);
            return NothingLeftToRedrive(refund, heldOnlyByPendingRefunds: CardRefundCeiling(order, confirmed) > 0m);
        }

        refund.ClampAmountTo(refundable);

        // The row keeps only the card leg. A credit leg the first attempt already returned on this key is the
        // rest of the slice it refunds, and is not returned again; otherwise the slice is read back through
        // the same proportion SplitAcrossTenders applied.
        var creditOnThisKey = await creditAccountRepository.GetReturnedForRefundAsync(
            refund.RefundKey, cancellationToken);
        var creditAlreadyReturned = await creditAccountRepository.GetReturnedTotalForOrderAsync(
            order.Id, cancellationToken) - creditOnThisKey;
        var settledInCredit = await creditAccountRepository.GetDisputeSettledTotalForOrderAsync(
            order.Id, cancellationToken);
        var cardCharged = CardChargedAmount(order);
        var slice = creditOnThisKey > 0m || cardCharged <= 0m
            ? refund.Amount + creditOnThisKey
            : Math.Round(refund.Amount * order.TotalPrice / cardCharged, 2, MidpointRounding.AwayFromZero);
        var held = await HeldToWhatIsLeftAsync(
            order, slice, refund.RefundKey, creditAlreadyReturned, settledInCredit, cancellationToken);
        var creditShare = creditOnThisKey > 0m ? 0m : SplitAcrossTenders(order, held, creditAlreadyReturned).Credit;

        // Only a held slice changes the row: a re-drive with nothing held keeps its exact amount, so Stripe
        // sees the same parameters on the same idempotency key. A held slice comes off the credit leg first,
        // for the same reason.
        if (held < slice)
        {
            var card = held - creditOnThisKey;
            if (card <= 0m)
            {
                var confirmed = await refundRepository.GetSucceededRefundTotalForOrderAsync(order.Id, cancellationToken);
                return NothingLeftToRedrive(refund, heldOnlyByPendingRefunds:
                    HeldToWhatIsLeft(order, slice, confirmed, creditAlreadyReturned, settledInCredit) - creditOnThisKey > 0m);
            }

            refund.ClampAmountTo(card);
            creditShare = CreditBesideCard(held, refund.Amount, creditShare, creditOnThisKey);
        }

        return await SettleAsync(order, refund, creditShare, actorId, cancellationToken);
    }

    /// <summary>
    /// The card money a retry of <paramref name="refund"/> leaves room for on the charge: refunds Stripe
    /// confirmed, and the pending refunds it must count. A pending refund counts only those claimed before it.
    /// One claimed later either counted its card as owed or, claimed at the same moment, did not see it, and
    /// counting that one here too would leave the two waiting on each other for good. So the older is retried
    /// on its own key: Stripe replays it if it paid it, pays it if it paid neither, and refuses it if it paid
    /// the younger, whose retry still counts the older. A closed refund was counted by no later claim, so it
    /// counts every pending one.
    ///
    /// <para>The card ceiling only. The slice held to what the sale has left still counts every pending refund:
    /// a pending refund's credit leg waits for its card, and a claim made after it was sized on what was left
    /// without that leg, so leaving the younger out there returns that credit twice.</para>
    /// </summary>
    private async Task<decimal> CardRefundedOrOwedBeforeRetryAsync(Refund refund, CancellationToken cancellationToken) =>
        refund.Status == RefundStatus.Pending
            ? await refundRepository.GetSucceededRefundTotalForOrderAsync(refund.OrderId, cancellationToken)
              + await refundRepository.GetPendingRefundTotalClaimedBeforeAsync(refund, cancellationToken)
            : await CardRefundedOrOwedAsync(refundRepository, refund.OrderId, refund.RefundKey, cancellationToken);

    /// <summary>
    /// A re-drive the order has nothing left for. Closed when the refunds Stripe confirmed used up what is
    /// left, so the hourly job stops selecting it. Left pending when only refunds still pending did: Stripe may
    /// have paid this one and refused one of those, and closing this one would leave the money it paid
    /// unrecorded with nobody told.
    /// </summary>
    private BusinessResult<RefundResult> NothingLeftToRedrive(Refund refund, bool heldOnlyByPendingRefunds)
    {
        if (heldOnlyByPendingRefunds)
        {
            logger.LogWarning(
                "Refund {RefundId} of order {OrderId} is held back only by other refunds still pending; it stays pending.",
                refund.Id, refund.OrderId);
            return BusinessResult.Failure<RefundResult>(new Error(
                nameof(refund.Amount), BusinessErrorMessage.RefundFailed));
        }

        refund.MarkFailed();
        return BusinessResult.Failure<RefundResult>(new Error(
            nameof(refund.Amount), BusinessErrorMessage.RefundNothingRefundable));
    }

    /// <summary>
    /// What a retried refund's credit leg returns beside its card amount: never more than the held slice
    /// leaves after the card, and nothing once this key's leg already came back.
    /// </summary>
    private static decimal CreditBesideCard(decimal held, decimal card, decimal splitCredit, decimal creditOnThisKey) =>
        creditOnThisKey > 0m ? 0m : Math.Max(0m, Math.Min(splitCredit, held - card));

    /// <summary>
    /// A fault on the way to Stripe rather than an answer from it: a timeout, a dropped connection, an
    /// open circuit. <see cref="IssueRefundAsync"/> lets these escape after its claim commit, so the row
    /// stays pending; a caller that must not fail with it catches exactly these. A cancellation the
    /// caller asked for is a genuine abort, never a fault.
    /// </summary>
    public static bool IsStripeTransportFailure(Exception ex, CancellationToken cancellationToken) =>
        ex is HttpRequestException or TimeoutException or ExecutionRejectedException
        || (ex is OperationCanceledException && !cancellationToken.IsCancellationRequested);

    private async Task<BusinessResult<RefundResult>> SettleAsync(
        Order order, Refund refund, decimal creditShare, string actorId, CancellationToken cancellationToken)
    {
        var refundKey = refund.RefundKey;
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
                nameof(refund.Amount), BusinessErrorMessage.RefundFailed));
        }

        var succeededConsumed = await refundRepository.GetSucceededRefundTotalForOrderAsync(
            order.Id, cancellationToken);
        refund.MarkSucceeded(stripeRefundId: null, confirmedOnUtc: DateTimeOffset.UtcNow);

        // The credit leg is independently idempotent; an erased account receives only its card refund.
        await ReturnCreditShareAsync(order, creditShare, refundKey, actorId, cancellationToken);

        order.UpdatePaymentStatus(IsFullyRefunded(
                order,
                succeededConsumed + refund.Amount,
                await creditAccountRepository.GetReturnedTotalForOrderAsync(order.Id, cancellationToken),
                await creditAccountRepository.GetDisputeSettledTotalForOrderAsync(order.Id, cancellationToken))
            ? PaymentStatus.Refunded
            : PaymentStatus.PartiallyRefunded);
        await refundRepository.CommitAsync(cancellationToken);

        logger.LogInformation(
            "Refund {RefundId} issued for order {OrderId}: {Amount} {Currency} ({Reason}).",
            refund.Id, order.Id, refund.Amount, order.Currency?.Code, refund.Reason);

        return BusinessResult.Success(new RefundResult(
            RefundId: refund.Id,
            RefundKey: refundKey,
            Amount: refund.Amount,
            Status: RefundStatus.Succeeded,
            ResolvedToExisting: false,
            CreditReturned: await creditAccountRepository.GetReturnedForRefundAsync(refundKey, cancellationToken)));
    }

    private async Task<BusinessResult<RefundResult>> ResolveToExistingAsync(Refund existing, CancellationToken cancellationToken) =>
        BusinessResult.Success(new RefundResult(
            RefundId: existing.Id,
            RefundKey: existing.RefundKey,
            Amount: existing.Amount,
            Status: existing.Status,
            ResolvedToExisting: true,
            CreditReturned: await creditAccountRepository.GetReturnedForRefundAsync(existing.RefundKey, cancellationToken)));

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
    /// Refunded once the card has given back all it took, or once the sale has nothing left to give back:
    /// card refunds, credit returned and complaints settled in credit together reach the price. The card arm
    /// keeps an order whose credit leg was suppressed (an erased account, frozen books) at Refunded; the sale
    /// arm is the one a complaint settled in credit needs, because the hold stops the card short of its charge.
    /// </summary>
    public static bool IsFullyRefunded(
        Order order, decimal cardRefunded, decimal creditReturned, decimal settledInCredit) =>
        cardRefunded >= CardChargedAmount(order)
        || cardRefunded + creditReturned + settledInCredit >= order.TotalPrice;

    /// <summary>
    /// The card money an order has given back, or may already have: refunds Stripe confirmed, and refunds
    /// still pending, which Stripe may have paid before its answer was lost. The pending refund keyed
    /// <paramref name="exceptRefundKey"/> is left out, so a retry is not owed on top of itself.
    /// </summary>
    public static async Task<decimal> CardRefundedOrOwedAsync(
        IRefundRepository refundRepository, string orderId, string? exceptRefundKey, CancellationToken cancellationToken) =>
        await refundRepository.GetSucceededRefundTotalForOrderAsync(orderId, cancellationToken)
        + await refundRepository.GetPendingRefundTotalForOrderAsync(orderId, exceptRefundKey, cancellationToken);

    /// <summary>
    /// The dispute's card refund was asked for and Stripe has not confirmed it. Resolving the dispute again is
    /// the only retry of that refund, so a dispute is not closed while this holds.
    /// </summary>
    public static async Task<bool> HasPendingDisputeRefundAsync(
        IRefundRepository refundRepository, Dispute dispute, CancellationToken cancellationToken) =>
        await refundRepository.GetByRefundKeyAsync(
                BuildRefundKey(new RefundRequest(
                    dispute.OrderId, 0m, RefundReason.DisputeResolution, string.Empty, DisputeId: dispute.Id)),
                cancellationToken)
            is { Status: RefundStatus.Pending };

    /// <summary>
    /// What the sale has not given back on any key but <paramref name="exceptRefundKey"/>: the price, less
    /// card refunds confirmed or pending, credit returned, and complaints settled in credit. The excepted
    /// key's own card refund and credit leg are left out, whatever their state, so the action that owns the
    /// key reads the same figure on a retry.
    /// </summary>
    public static async Task<decimal> LeftToGiveBackAsync(
        IRefundRepository refundRepository,
        ICreditAccountRepository creditAccountRepository,
        Order order,
        string? exceptRefundKey,
        CancellationToken cancellationToken)
    {
        var card = await CardRefundedOrOwedAsync(refundRepository, order.Id, exceptRefundKey, cancellationToken);
        var credit = await creditAccountRepository.GetReturnedTotalForOrderAsync(order.Id, cancellationToken);
        if (exceptRefundKey is not null)
        {
            if (await refundRepository.GetByRefundKeyAsync(exceptRefundKey, cancellationToken)
                is { Status: RefundStatus.Succeeded } own)
            {
                card -= own.Amount;
            }

            credit -= await creditAccountRepository.GetReturnedForRefundAsync(exceptRefundKey, cancellationToken);
        }

        return Math.Max(0m, order.TotalPrice - card - credit
            - await creditAccountRepository.GetDisputeSettledTotalForOrderAsync(order.Id, cancellationToken));
    }

    /// <summary>
    /// The requested slice of the sale, held to what the order has not already given back.
    ///
    /// <para>A complaint settled in credit returns part of the sale on neither tender: it moves no card
    /// money and returns none of the applied credit, so neither leg's own ceiling counts it. Without this
    /// hold a 1000 sale with 300 settled in credit could still be refunded 1000 to the card. With no
    /// settlement the request passes unchanged, so no other refund is touched.</para>
    /// </summary>
    private async Task<decimal> HeldToWhatIsLeftAsync(
        Order order,
        decimal requested,
        string refundKey,
        decimal creditAlreadyReturned,
        decimal settledInCredit,
        CancellationToken cancellationToken) =>
        settledInCredit <= 0m
            ? requested
            : HeldToWhatIsLeft(
                order,
                requested,
                await CardRefundedOrOwedAsync(refundRepository, order.Id, refundKey, cancellationToken),
                creditAlreadyReturned,
                settledInCredit);

    /// <summary>
    /// The pure half of <see cref="HeldToWhatIsLeftAsync"/>, shared with the cancellations that return a
    /// pending refund's credit leg ahead of its card leg (<see cref="CreditUnwind.ReturnPendingRefundCreditLegAsync"/>):
    /// the two must hold the same slice, or the legs disagree about how much of the sale this refund gives back.
    /// </summary>
    public static decimal HeldToWhatIsLeft(
        Order order, decimal requested, decimal cardRefunded, decimal creditReturned, decimal settledInCredit) =>
        settledInCredit <= 0m
            ? requested
            : Math.Min(requested, Math.Max(0m, order.TotalPrice - cardRefunded - creditReturned - settledInCredit));

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
    /// key. A false answer is a no-op, including credit suppressed after account erasure and credit on
    /// the books of a company frozen for archive.
    /// </summary>
    private async Task ReturnCreditShareAsync(
        Order order, decimal creditShare, string refundKey, string actorId, CancellationToken cancellationToken)
    {
        var returned = await creditAccountRepository.ReturnCreditAsync(
            order, creditShare, refundKey, actorId, cancellationToken);

        if (!returned && creditShare > 0m)
        {
            logger.LogInformation(
                "No credit movement of {Amount} for order {OrderId} on refund key {RefundKey}.",
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
