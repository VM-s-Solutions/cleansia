using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Orders;
using Microsoft.Extensions.Logging;

namespace Cleansia.Core.AppServices.Features.Orders;

/// <summary>
/// Default <see cref="IOrderPromoApplier"/>. Wraps <see cref="IPromoCodeService"/> preview/apply with
/// the guard conditions, apply subtotal and best-effort logged-and-swallowed semantics that
/// <see cref="CreateOrder.Handler"/> used to hold inline.
/// </summary>
public sealed class OrderPromoApplier(
    IPromoCodeService promoCodeService,
    ILogger<OrderPromoApplier> logger) : IOrderPromoApplier
{
    public async Task<OrderPromoPreview> PreviewAsync(
        CreateOrder.Command command,
        string userId,
        decimal rawSubtotal,
        string currencyId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(command.PromoCode) || string.IsNullOrEmpty(userId))
        {
            return OrderPromoPreview.None;
        }

        var preview = await promoCodeService.PreviewAsync(
            command.PromoCode, userId, rawSubtotal, currencyId, cancellationToken);
        return preview.Success
            ? new OrderPromoPreview(preview.DiscountAmount, preview.PromoCodeId)
            : OrderPromoPreview.None;
    }

    public async Task ApplyAsync(
        CreateOrder.Command command,
        string userId,
        Order order,
        decimal rawSubtotal,
        string currencyId,
        CancellationToken cancellationToken)
    {
        // Gate on the ORDER, not the preview: OrderFactory.ResolveLoy003Discount discards a
        // previewed promo when membership+tier is larger, and a redemption recorded off the preview
        // burns the customer's one-shot code for a discount they never received (ADR-0038 §D5.1).
        if (order.PromoCodeId is null
            || order.PromoDiscountAmount is not > 0m
            || string.IsNullOrEmpty(command.PromoCode)
            || string.IsNullOrEmpty(userId))
        {
            return;
        }

        // Best-effort: failure logs but never rolls back — the customer already
        // paid and the promo just doesn't get tracked. rawSubtotal is the handler's own
        // pre-discount base; re-grossing order.TotalPrice is wrong on an express order, where the
        // surcharge is applied AFTER the discount (OrderFactory: ApplyExpressSurcharge(raw - applied)).
        var applyResult = await promoCodeService.ApplyAsync(
            command.PromoCode,
            userId,
            order.Id,
            rawSubtotal,
            currencyId,
            cancellationToken);
        if (!applyResult.Success)
        {
            logger.LogWarning(
                "Promo apply failed after order created. OrderId={OrderId}, Code={Code}, Error={Error}",
                order.Id, command.PromoCode, applyResult.Error);
        }
    }
}
