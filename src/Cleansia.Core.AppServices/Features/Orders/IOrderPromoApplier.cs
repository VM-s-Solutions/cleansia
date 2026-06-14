using Cleansia.Core.Domain.Orders;

namespace Cleansia.Core.AppServices.Features.Orders;

/// <summary>
/// Owns the promo-code concern lifted out of <see cref="CreateOrder.Handler"/>: the pre-persist
/// <see cref="PreviewAsync"/> (compute the discount to fold into the order's price snapshot) and the
/// post-persist <see cref="ApplyAsync"/> (append the redemption ledger row once the order id exists).
///
/// The contract preserves the handler's original semantics exactly:
///   * preview is a no-op (zero discount, null code id) unless both a promo code and a logged-in user
///     are present, and only adopts the discount when the preview succeeds;
///   * apply runs only when a positive discount was previewed against a real code + user, and is
///     <b>best-effort</b> — a failed apply is logged and swallowed, never rolled back and never blocks
///     the booking (the customer already paid; the promo simply goes untracked).
/// </summary>
public interface IOrderPromoApplier
{
    Task<OrderPromoPreview> PreviewAsync(
        CreateOrder.Command command,
        string userId,
        decimal rawSubtotal,
        string currencyId,
        CancellationToken cancellationToken);

    Task ApplyAsync(
        CreateOrder.Command command,
        string userId,
        Order order,
        OrderPromoPreview preview,
        string currencyId,
        CancellationToken cancellationToken);
}

/// <summary>
/// Outcome of <see cref="IOrderPromoApplier.PreviewAsync"/>: the discount to fold into the order price
/// snapshot and the promo code id to persist on the order. <see cref="None"/> represents "no promo
/// applied" (zero discount, no code id) — the handler feeds these straight into the order factory.
/// </summary>
public record OrderPromoPreview(decimal DiscountAmount, string? PromoCodeId)
{
    public static OrderPromoPreview None { get; } = new(0m, null);
}
