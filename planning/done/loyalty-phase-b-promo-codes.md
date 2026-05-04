# Loyalty Phase B — Promo codes

Customer-facing first-order/marketing discount codes. Phase A established tier discounts; this adds **manually-issued promo codes** (e.g. `WELCOME20`) that customers redeem at booking time.

## Decisions (locked from earlier conversation)

| Item | Value |
|---|---|
| Code lifecycle | Admin-issued (CRUD comes in L4); customer redeems once at booking |
| Stacking | Forbidden — best single discount applies (tier OR promo, not both) |
| Initial seed | None — admin adds codes via SQL or L4 UI later |
| Per-user limit | Configurable per code; default 1 redemption per user |
| Global limit | Configurable per code; default unlimited |
| Validity window | Optional `ValidFrom` / `ValidUntil` — both nullable |
| Minimum order | Optional `MinimumOrderAmount` (CZK) |
| Tenant scoping | Codes scoped per tenant |
| Status on cancel | If the order using a promo is cancelled → redemption record kept (audit), code "consumed" stays consumed (anti-abuse) |
| Discount precedence | Promo discount calculated AFTER tier discount evaluation; engine picks whichever is larger |

## Domain model

### `PromoCode` (admin-issued)

```csharp
class PromoCode : Auditable, ITenantEntity
{
    string Code { get; private set; }              // unique per tenant, uppercase canonical (WELCOME20)
    PromoCodeType Type { get; private set; }       // PercentDiscount | FixedDiscount
    decimal? DiscountPercent { get; private set; } // when Type=PercentDiscount, 0..1
    decimal? DiscountAmount { get; private set; }  // when Type=FixedDiscount, CZK amount
    string? CurrencyId { get; private set; }       // when Type=FixedDiscount; null = tenant default
    decimal? MinimumOrderAmount { get; private set; }
    int MaxRedemptionsPerUser { get; private set; } // default 1
    int? GlobalMaxRedemptions { get; private set; } // null = unlimited
    int CurrentRedemptionsCount { get; private set; } // denormalized for fast cap-check
    DateTimeOffset? ValidFrom { get; private set; }
    DateTimeOffset? ValidUntil { get; private set; }
    bool IsActive { get; set; }                    // admin toggle to suspend without deleting
    string? Description { get; private set; }      // admin-facing note ("Spring 2026 launch", etc.)

    // Behavior
    private PromoCode() { }
    public static PromoCode CreatePercent(string code, decimal percent, ...) { ... }
    public static PromoCode CreateFixed(string code, decimal amount, string currencyId, ...) { ... }
    public void IncrementRedemptions(string actorId);
    public void Deactivate(string actorId);
    public bool IsRedeemableAt(DateTimeOffset now) =>
        IsActive
        && (ValidFrom == null || now >= ValidFrom)
        && (ValidUntil == null || now <= ValidUntil)
        && (GlobalMaxRedemptions == null || CurrentRedemptionsCount < GlobalMaxRedemptions);
}

[SwaggerEnumAsInt]
public enum PromoCodeType { PercentDiscount = 1, FixedDiscount = 2 }
```

**Storage**: `PromoCodes` table, unique `(TenantId, Code)` index, `Code` stored as uppercase, lookup is case-insensitive client-side normalization.

### `PromoCodeRedemption` (audit log)

```csharp
class PromoCodeRedemption : Auditable, ITenantEntity
{
    string PromoCodeId { get; private set; }
    PromoCode PromoCode { get; private set; }
    string UserId { get; private set; }
    string OrderId { get; private set; }            // every redemption is tied to an order
    decimal AppliedDiscount { get; private set; }   // actual CZK discount
    DateTimeOffset RedeemedOn { get; private set; }
}
```

Index on `(PromoCodeId, UserId)` for the per-user cap check. Index on `OrderId` for "did this order use a promo?" lookups.

### Order columns

Add to `Order` entity (mirror the Phase A `TierDiscountAmount` + `TierAtPurchase` pattern):

- `decimal? PromoDiscountAmount`
- `string? PromoCodeId` (FK, nullable)

So receipts/order details can render a "Promo discount: -X CZK (WELCOME20)" line.

## Service layer

Extend or sibling-add to `LoyaltyService`. Recommended: new `IPromoCodeService` since concerns are distinct.

```csharp
public interface IPromoCodeService
{
    /// <summary>
    /// Validate + preview a promo code for a given user + order subtotal.
    /// Does NOT create a redemption — that happens at order-create time.
    /// </summary>
    Task<PromoCodePreviewResult> PreviewAsync(string code, string userId, decimal orderSubtotal, CancellationToken ct);

    /// <summary>
    /// Apply a previously-previewed promo code at order-create time.
    /// Inserts a PromoCodeRedemption + bumps the code's redemption counter.
    /// Idempotent on (orderId, codeId) — if the order already has a redemption, no-op.
    /// </summary>
    Task<PromoCodeApplyResult> ApplyAsync(string code, string userId, string orderId, decimal orderSubtotal, CancellationToken ct);
}

public record PromoCodePreviewResult(
    bool Success,
    decimal DiscountAmount,
    string? PromoCodeId,
    PromoCodeError? Error);

public record PromoCodeApplyResult(
    bool Success,
    decimal AppliedDiscount,
    string? PromoCodeId,
    PromoCodeError? Error);

public enum PromoCodeError {
    NotFound,
    Inactive,
    Expired,
    NotYetValid,
    GlobalLimitReached,
    PerUserLimitReached,
    BelowMinimumOrderAmount,
    CurrencyMismatch
}
```

Translation of `PromoCodeError` → user-facing message lives in i18n (`api.promo.not_found`, etc.) — same pattern as existing backend errors.

## Booking integration — discount precedence

The current `CreateOrder.Handler` already applies tier discount after express surcharge (Phase A). Promo discount adds a third layer with **best-discount-wins** logic:

```csharp
// 1. Express surcharge already applied → finalTotalPrice
// 2. Resolve tier discount for this user
var tier = await loyaltyService.ResolveTierDiscountForOrderAsync(command.UserId, finalTotalPrice, ct);

// 3. Resolve promo discount if a code was supplied
PromoCodeApplyResult? promo = null;
if (!string.IsNullOrEmpty(command.PromoCode))
{
    promo = await promoCodeService.ApplyAsync(command.PromoCode, command.UserId, /*placeholder orderId*/, finalTotalPrice, ct);
    // ApplyAsync needs the order id; restructure: do PreviewAsync here, then call ApplyAsync after Order.Create.
}

// 4. Pick the better discount (greater amount)
var tierAmount = tier.DiscountAmount;
var promoAmount = promo?.AppliedDiscount ?? 0;
var (discountSource, discountApplied, tierAtPurchase, promoCodeId) = if (promoAmount > tierAmount)
    ("promo", promoAmount, null, promo!.PromoCodeId)
else
    ("tier", tierAmount, tier.TierAtPurchase, null);

finalTotalPrice -= discountApplied;
// Order.Create now stores TierDiscountAmount/TierAtPurchase (existing) + PromoDiscountAmount/PromoCodeId (new)
```

The "best wins" rule means a customer with a Gold tier (10% off) who enters `WELCOME20` (20% off) gets 20%. A Silver customer (5% off ≥1000 CZK) entering a 3% code gets 5%. This matches typical e-commerce UX.

**Edge case**: Silver's "≥1000 CZK" minimum and a promo's minimum interact. Example: order 800 CZK, customer is Silver, promo is "10% off ≥500 CZK". Tier doesn't apply (below 1000), promo does apply. Result: 10% off. Engine handles this correctly because tier returns 0 below threshold.

## Customer API endpoints

Add to existing `LoyaltyController` (closely-related concept) OR new `PromoCodeController`. Recommend new controller — separation of concerns:

```csharp
[Route("api/[controller]")]
[ApiController]
public class PromoCodeController(IMediator mediator) : CustomerApiController(mediator)
{
    [HttpPost("Validate")]
    [Permission(Policy.CanRedeemPromoCode)]
    public async Task<IActionResult> Validate([FromBody] ValidatePromoCode.Command command, CancellationToken ct)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? string.Empty;
        var enriched = command with { UserId = userId };
        var result = await Mediator.Send(enriched, ct);
        return HandleResult<ValidatePromoCode.Response>(result);
    }
}
```

The `Validate` endpoint is what the booking wizard calls when the user enters a code — instant feedback ("OK, 20% off" or "Code expired"). The actual redemption happens server-side inside `CreateOrder.Handler` so the client can't tamper.

```csharp
public class ValidatePromoCode
{
    public record Command(string Code, decimal OrderSubtotal, string UserId = "") : ICommand<Response>;

    public record Response(
        bool IsValid,
        decimal? DiscountAmount,
        string? ErrorCode);
}
```

`ErrorCode` is one of `PromoCodeError`'s values stringified, so the client maps to its own i18n.

### Modified existing handler

`CreateOrder.Command` gains `string? PromoCode` (nullable). Frontend supplies it when the user entered one and hit submit. Backend re-validates inside the handler before applying — Validate endpoint is UX optimization, not the gate.

## New permission

`Policy.CanRedeemPromoCode = nameof(CanRedeemPromoCode)` → `PhysicalPolicy.CustomerOnly` (mirrors `CanCreateOrder`-style). Added to PolicyBuilder.

## Mobile integration

### Booking wizard `ConfirmStep` additions
- New "Promo code" `OutlinedTextField` above the payment-method picker.
- Below the input: an "Apply" button OR auto-validate on debounce (350ms after user stops typing). Auto-validate is friendlier — instant green-check on valid, red-X on invalid.
- When valid: show the discount amount inline ("- 200 CZK").
- The summary card's existing "Subtotal / Express surcharge / Tier discount / Total" stack adds a new "Promo (-X CZK)" line when applicable.

### `BookingPricing.kt` extensions
- Already computes tier discount preview client-side (Phase A). Phase B adds **promo preview as a separate path** — needs network call (Validate endpoint), not pure client math.
- Add `validatePromoCode(code, subtotal)` to a new `PromoCodeRepository` or directly on `BookingViewModel` since it's wizard-scoped.
- Best-discount-wins: client picks max(tier, promo) for display. Backend re-picks at submit time (authoritative).

### Mobile data layer
Mirror the Phase A pattern:
- `core/promo/PromoCodeApi.kt` — Retrofit binding for `POST /api/PromoCode/Validate`
- `core/promo/PromoCodeDtos.kt` — request + response DTOs
- `core/promo/PromoCodeModule.kt` — Hilt provides

No long-lived repository — promo validation is request/response, not cached state.

### i18n keys (EN + CS)
- `booking_promo_code_label` — "Promo code (optional)"
- `booking_promo_code_placeholder` — "Enter code"
- `booking_promo_code_valid` — "Code applied: -%1$s"
- `booking_promo_code_error_not_found` — "Code not found"
- `booking_promo_code_error_expired` — "This code has expired"
- `booking_promo_code_error_used` — "You've already used this code"
- `booking_promo_code_error_min_order` — "Minimum order: %1$s"
- `booking_promo_code_error_inactive` — "Code is no longer available"
- `booking_summary_promo_discount` — "Promo discount"
- `error_promo_not_found` — "Promo code not found"
- `error_promo_expired` — "Promo code expired"
- `error_promo_per_user_limit_reached` — "You've already used this promo"
- `error_promo_global_limit_reached` — "This promo is no longer available"
- `error_promo_below_minimum_order_amount` — "Order doesn't meet the minimum for this promo"
- `error_promo_currency_mismatch` — "Promo doesn't apply to this currency"

## Web integration

Order-wizard's Confirm step gets the same inputs. Web's `RewardsFacade` already exists; promo lives in the booking flow not Rewards. New `PromoCodeService` injected into `OrderWizardFacade`.

Web i18n keys mirror mobile under `pages.order.promo.*`.

## Migration

Single migration:
- Create `PromoCodes` table
- Create `PromoCodeRedemptions` table
- Add `PromoDiscountAmount` (nullable decimal) + `PromoCodeId` (nullable string FK) on `Orders`
- No seed (admin adds codes manually)

## Out of scope for L2
- Admin UI for creating/editing codes (Phase L4)
- Code generation helpers (random alphanumeric, etc.) — admin enters codes manually for now
- Code analytics ("WELCOME20 used 47 times by 32 users") — Phase L4
- Tiered codes ("X% off if Gold+") — overcomplication; let tier discount handle that
- Bundled codes ("Free service when you book Y") — different feature, future scope

## Phasing within L2
1. **L2-B1 — Backend foundation**: entities + service + handler integration + new endpoint + migration. ~half day.
2. **MANUAL_STEP**: migration + NSwag regen.
3. **L2-M1 — Mobile data layer + booking wizard integration**: API + wizard input + summary line. ~3 hours.
4. **L2-W1 — Web booking wizard integration**: same on Angular side. ~3 hours.

L2-M1 + L2-W1 can run in parallel after L2-B1.
