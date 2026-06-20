using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Orders;
using Microsoft.Extensions.Logging;

namespace Cleansia.Core.AppServices.Features.Orders;

/// <summary>
/// Default <see cref="IOrderPromoApplier"/>. Wraps <see cref="IPromoCodeService"/> preview/apply with
/// the same guard conditions, apply-subtotal math, and best-effort logged-and-swallowed semantics the
/// handler had inline — extracted verbatim, no behavior change.
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
        OrderPromoPreview preview,
        string currencyId,
        CancellationToken cancellationToken)
    {
        if (preview.DiscountAmount <= 0m
            || string.IsNullOrEmpty(command.PromoCode)
            || string.IsNullOrEmpty(userId))
        {
            return;
        }

        // Best-effort: failure logs but never rolls back — the customer already
        // paid and the promo just doesn't get tracked. Apply runs post-persist so
        // the redemption row gets the order id. Subtotal re-grosses the discount
        // back onto the persisted total to match the previewed pre-discount base.
        var applyResult = await promoCodeService.ApplyAsync(
            command.PromoCode,
            userId,
            order.Id,
            order.TotalPrice + preview.DiscountAmount,
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
