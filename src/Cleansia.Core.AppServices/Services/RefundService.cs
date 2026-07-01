using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Clients.Abstractions.Stripe;
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
            // A prior Pending/Failed attempt exists — reuse its row + amount (do NOT insert a second) and
            // re-drive Stripe with the same key below.
            refund = existing;
        }
        else
        {
            var consumed = await refundRepository.GetSucceededRefundTotalForOrderAsync(
                order.Id, cancellationToken);
            var refundable = order.TotalPrice - consumed;
            var amount = Math.Min(request.Amount, refundable);
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
        order.UpdatePaymentStatus(succeededConsumed + refund.Amount >= order.TotalPrice
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

    // RefundKey = refund:{OrderId}:{purpose}, purpose ∈ { cancel, dispute:{DisputeId}, admin:{RefundRequestId} }
    // (ADR-0006 D3). Deterministic on the domain inputs, never a Guid/timestamp, so a retry/redelivery
    // and a concurrent double-issue collapse onto the one key.
    private static string BuildRefundKey(RefundRequest request)
    {
        var purpose = request.Reason switch
        {
            RefundReason.CustomerCancellation => "cancel",
            RefundReason.DisputeResolution => $"dispute:{request.DisputeId}",
            _ => $"admin:{request.RefundRequestId}",
        };
        return $"refund:{request.OrderId}:{purpose}";
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
