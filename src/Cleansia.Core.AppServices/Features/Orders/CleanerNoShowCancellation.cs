using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Services;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Credit;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Notifications;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Queue.Abstractions;
using Cleansia.Infra.Common.Validations;
using Microsoft.Extensions.Logging;

namespace Cleansia.Core.AppServices.Features.Orders;

/// <summary>
/// Cancelling a booking nobody cleaned because of us: no fee, the whole card refund, the customer's own
/// applied credit back, the apology credit, and a message saying which of those actually happened. The
/// one body behind the unfilled sweep and an administrator's cleaner-no-show confirmation, so the two pay
/// a customer the same. → /product/business-rules#when-the-cleaner-cancels-or-no-shows
/// </summary>
public sealed class CleanerNoShowCancellation(
    ICreditAccountRepository creditAccountRepository,
    IRefundService refundService,
    INotificationProducer notificationProducer,
    GuestOrderAccessTokenIssuer guestAccessTokenIssuer,
    IPendingDispatch pending,
    ILogger<CleanerNoShowCancellation> logger)
{
    /// <param name="Transition">The Cancelled track this cancellation appended.</param>
    /// <param name="RefundedAmount">The card refund that went through, or null.</param>
    /// <param name="RefundPending">A card refund was owed and did not go through; the hourly re-drive owns it.</param>
    /// <param name="ApologyAmount">The apology credit issued, or null.</param>
    public record Outcome(
        OrderStatusTrack Transition, decimal? RefundedAmount, bool RefundPending, decimal? ApologyAmount);

    public async Task<Outcome> ExecuteAsync(
        Order order, CancelledBy cancelledBy, string actorId, DateTime nowUtc, CancellationToken cancellationToken)
    {
        order.Cancel(
            nowUtc,
            cancelledBy,
            feeRate: 0m,
            refundAmount: order.TotalPrice,
            reason: OrderCancellationReasons.NoCleanerAvailable);
        var transition = OrderStatusTrack.Create(OrderStatus.Cancelled, order);
        order.AddOrderStatus(transition);

        var tookNoPayment = order.TookNoPayment;
        var refundOwed = order.PaymentType == PaymentType.Card
            && order.PaymentStatus == PaymentStatus.Paid
            && order.TotalPrice > 0m;
        decimal? refundedAmount = null;
        if (refundOwed && order.HasRefundableChargeSurface)
        {
            refundedAmount = await TryRefundAsync(order, actorId, cancellationToken);
        }

        // A refund of the whole sale returns the applied credit on its own leg. Without one the credit
        // comes back here, now; the re-drive of a failed refund nets off what already went back.
        if (refundedAmount is null)
        {
            await creditAccountRepository.ReturnUnpaidOrderCreditAsync(order, actorId, cancellationToken);
        }

        var apology = await TryIssueApologyCreditAsync(order, actorId, cancellationToken);
        var refundPending = refundOwed && refundedAmount is null;
        await NotifyCustomerAsync(order, refundedAmount, tookNoPayment, refundPending, apology, cancellationToken);

        await GuestCancellationEmail.EnqueueAsync(order, EmailLocale.Resolve(order.LanguageCode),
            refundedAmount, guestAccessTokenIssuer, pending, cancellationToken);

        return new Outcome(transition, refundedAmount, refundPending, apology);
    }

    /// <summary>
    /// The full card refund: what it returned, or null when it did not go through. A failure is logged
    /// for a person and carried on from — the cancellation is still right, and the pending refund row is
    /// re-driven hourly.
    /// </summary>
    private async Task<decimal?> TryRefundAsync(Order order, string actorId, CancellationToken cancellationToken)
    {
        BusinessResult<RefundResult> refund;
        try
        {
            refund = await refundService.IssueRefundAsync(
                new RefundRequest(order.Id, order.TotalPrice, RefundReason.ServiceNotRendered, actorId),
                cancellationToken);
        }
        catch (Exception ex) when (RefundService.IsStripeTransportFailure(ex, cancellationToken))
        {
            logger.LogError(ex,
                "Could not reach Stripe to refund no-show order {OrderId}; the refund is left pending",
                order.Id);
            return null;
        }

        if (refund.IsFailure)
        {
            logger.LogError(
                "Could not refund no-show order {OrderId}: {Error}; the refund is left pending",
                order.Id, refund.Error?.Message);
            return null;
        }

        return refund.Value!.Amount;
    }

    /// <summary>
    /// The apology credit: the amount issued, or null — without failing the cancellation — whenever it
    /// cannot honestly be given. A guest has no account to hold it; a currency with no authored
    /// <c>Currency.NoShowCredit</c> pays none rather than borrowing another currency's figure; and an
    /// account on the books of a company frozen for archive takes no write, so issuing there would fail
    /// the cancellation's commit and, in the sweep, every order after it.
    /// </summary>
    private async Task<decimal?> TryIssueApologyCreditAsync(
        Order order, string actorId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(order.UserId))
        {
            return null;
        }

        var amount = order.Currency?.NoShowCredit;
        if (amount is null or <= 0m)
        {
            logger.LogWarning(
                "No apology credit on no-show order {OrderId}: none is authored for {CurrencyCode}. "
                    + "The refund was not affected.",
                order.Id, order.Currency?.Code ?? order.CurrencyId);
            return null;
        }

        if (await creditAccountRepository.IsOnFrozenCompanyBooksAsync(order.UserId, order.CurrencyId, cancellationToken))
        {
            logger.LogWarning(
                "No apology credit on no-show order {OrderId}: the customer's credit account is on a company "
                    + "frozen for archive. The refund was not affected.",
                order.Id);
            return null;
        }

        var account = await creditAccountRepository.EnsureForUserAsync(
            order.UserId, order.CurrencyId, cancellationToken);
        if (account is null)
            return null;

        // One key per ORDER: the ledger's unique IdempotencyKey collapses a second grant.
        account.Issue(
            amount: amount.Value,
            reason: CreditTransactionReason.CleanerNoShow,
            idempotencyKey: $"cleaner-noshow:{order.Id}",
            issuedBy: actorId,
            orderId: order.Id,
            note: order.AssignedEmployees.Count > 0
                ? "The assigned cleaner did not arrive."
                : "No cleaner was assigned when the booking's time arrived.");

        return amount.Value;
    }

    /// <summary>
    /// ONE message per cancelled order, so the bare order id is a safe subject. The keys that announce
    /// the apology credit are sent only when it was issued, and each says what happened to the money;
    /// otherwise the plain cancellation, which promises nothing.
    /// </summary>
    private Task NotifyCustomerAsync(
        Order order,
        decimal? refundedAmount,
        bool tookNoPayment,
        bool refundPending,
        decimal? apology,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(order.UserId))
        {
            return Task.CompletedTask;
        }

        var args = new Dictionary<string, string>
        {
            ["orderId"] = order.Id,
            ["orderNumber"] = order.DisplayOrderNumber,
        };
        var outcomeKey = apology is null ? null
            : refundedAmount is not null ? NotificationEventCatalog.OrderNoCleanerRefunded
            : refundPending ? NotificationEventCatalog.OrderNoCleanerRefundPending
            : tookNoPayment ? NotificationEventCatalog.OrderNoCleanerNothingCharged
            : null;
        if (outcomeKey is not null)
        {
            args["amount"] = MoneyText.Format(apology!.Value, order.Currency!);
        }

        return notificationProducer.NotifyAsync(
            order.UserId,
            outcomeKey ?? NotificationEventCatalog.OrderCancelled,
            args,
            order.TenantId,
            order.Id,
            cancellationToken);
    }
}
