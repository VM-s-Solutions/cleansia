# Loyalty — Phase A (foundation + tier display)

Wolt-style tier loyalty. **No spendable points.** Points exist only as the metric driving tier progression. Tier perks come as automatic discounts on bookings. Promo codes (Phase B) and referrals (Phase C) come later.

## Decisions (locked)

| Item | Value |
|---|---|
| Tier names | `BronzeCleaner` / `SilverMopper` / `GoldPolisher` / `PlatinumSparkler` |
| Lifetime-point thresholds | 0 / 500 / 2000 / 5000 |
| Earn rate per completed order | `floor(basePrice / 10)` tier-points (1 pt per 10 CZK) |
| Tier discount % | 0 / 5 (≥1000 CZK only) / 10 / 15 |
| Tier downgrade | Never (always-Gold once achieved) |
| Point expiry | Never |
| Stacking | Forbidden — best single discount applies |
| Sign-up bonus | 0 |
| Cancellation | Revoke earn (negative ledger entry) |
| Refund (partial) | Revoke proportional |
| Currency for thresholds | Order's `basePrice` regardless of currency. CZK assumed (existing single-tenant default). Multi-currency expansion is a Phase B+ concern. |

Cancellation policy: when an order in `Completed` status is later cancelled (rare but possible via support), the original earn entry stays AND a negative-points entry is appended with `Source = OrderCancelled`. Lifetime points and tier are recomputed. The user might lose their tier — acceptable, mirrors industry norm.

## Domain model

### `LoyaltyAccount` (1:1 with User)

```csharp
class LoyaltyAccount : Auditable, ITenantEntity
{
    string UserId { get; private set; }       // unique, FK
    User User { get; private set; }

    int LifetimePoints { get; private set; }  // monotonically tracked, but recomputed from ledger on grant/revoke
    LoyaltyTier CurrentTier { get; private set; }
    DateTimeOffset TierAchievedOn { get; private set; }
    int CompletedBookingsCount { get; private set; }

    private readonly List<LoyaltyTransaction> _transactions = new();
    public IReadOnlyCollection<LoyaltyTransaction> Transactions => _transactions.AsReadOnly();

    private LoyaltyAccount() { }
    public static LoyaltyAccount Create(string userId)
    {
        var a = new LoyaltyAccount
        {
            UserId = userId,
            LifetimePoints = 0,
            CurrentTier = LoyaltyTier.BronzeCleaner,
            TierAchievedOn = DateTimeOffset.UtcNow,
            CompletedBookingsCount = 0,
        };
        return a;
    }

    public void GrantPoints(int points, LoyaltyEarnSource source, string? orderId, string actorId)
    {
        // Append-only ledger entry; recompute denormalized fields.
        var tx = LoyaltyTransaction.Create(Id, LoyaltyTransactionType.Earn, points, source, orderId);
        _transactions.Add(tx);
        LifetimePoints += points;
        if (source == LoyaltyEarnSource.OrderCompleted) CompletedBookingsCount += 1;
        RecomputeTier();
        Updated(actorId, DateTimeOffset.UtcNow);
    }

    public void RevokePoints(int points, LoyaltyEarnSource source, string? orderId, string actorId)
    {
        // Negative ledger entry. Lifetime + bookings count both walk back; tier may downgrade.
        var tx = LoyaltyTransaction.Create(Id, LoyaltyTransactionType.Revoke, -points, source, orderId);
        _transactions.Add(tx);
        LifetimePoints = Math.Max(0, LifetimePoints - points);
        if (source == LoyaltyEarnSource.OrderCompleted) CompletedBookingsCount = Math.Max(0, CompletedBookingsCount - 1);
        RecomputeTier();
        Updated(actorId, DateTimeOffset.UtcNow);
    }

    private void RecomputeTier()
    {
        var newTier = LifetimePoints switch
        {
            >= 5000 => LoyaltyTier.PlatinumSparkler,
            >= 2000 => LoyaltyTier.GoldPolisher,
            >= 500 => LoyaltyTier.SilverMopper,
            _ => LoyaltyTier.BronzeCleaner,
        };
        if (newTier != CurrentTier)
        {
            CurrentTier = newTier;
            TierAchievedOn = DateTimeOffset.UtcNow;
        }
    }
}
```

### `LoyaltyTier` enum + `LoyaltyTierConfig` table

```csharp
[SwaggerEnumAsInt]
public enum LoyaltyTier
{
    BronzeCleaner = 1,
    SilverMopper = 2,
    GoldPolisher = 3,
    PlatinumSparkler = 4,
}

class LoyaltyTierConfig : Auditable, ITenantEntity
{
    LoyaltyTier Tier { get; private set; }                  // unique per tenant
    int LifetimePointsThreshold { get; private set; }
    decimal DiscountPercent { get; private set; }            // 0..1
    decimal? MinimumOrderAmountForDiscount { get; private set; }  // null = always applies
    string PerksJson { get; private set; }                   // serialized perks list for UI
}
```

Seeded per-tenant on tenant creation. Discounts as agreed:

| Tier | Threshold | Discount % | Min order |
|---|---|---|---|
| BronzeCleaner | 0 | 0 | n/a |
| SilverMopper | 500 | 5 | 1000 CZK |
| GoldPolisher | 2000 | 10 | 0 |
| PlatinumSparkler | 5000 | 15 | 0 |

`PerksJson` shape:

```json
[
  { "icon": "badge", "labelKey": "loyalty.perks.welcome_badge" },
  { "icon": "percent", "labelKey": "loyalty.perks.discount_5_above_1000" }
]
```

Frontend looks up `labelKey` in i18n. Admin can edit via Phase D admin UI later.

### `LoyaltyTransaction` (append-only ledger)

```csharp
[SwaggerEnumAsInt]
public enum LoyaltyTransactionType { Earn = 1, Revoke = 2 }

[SwaggerEnumAsInt]
public enum LoyaltyEarnSource { OrderCompleted = 1, OrderCancelled = 2, Referral = 3, ManualGrant = 4 }

class LoyaltyTransaction : Auditable, ITenantEntity
{
    string LoyaltyAccountId { get; private set; }
    LoyaltyAccount Account { get; private set; }
    LoyaltyTransactionType Type { get; private set; }
    int Points { get; private set; }                        // signed (positive on Earn, negative on Revoke)
    LoyaltyEarnSource Source { get; private set; }
    string? OrderId { get; private set; }                   // when relevant
    string? Description { get; private set; }               // human-readable for activity feed
    DateTimeOffset OccurredOn { get; private set; }

    public static LoyaltyTransaction Create(string accountId, LoyaltyTransactionType type, int signedPoints, LoyaltyEarnSource source, string? orderId)
        => new() { ... };
}
```

Activity feed reads from this table directly, ordered by `OccurredOn DESC`.

## Repositories

```csharp
public interface ILoyaltyAccountRepository : IRepository<LoyaltyAccount, string>
{
    Task<LoyaltyAccount?> GetByUserIdAsync(string userId, CancellationToken ct);
    Task<LoyaltyAccount> EnsureForUserAsync(string userId, CancellationToken ct); // get-or-create
}

public interface ILoyaltyTierConfigRepository : IRepository<LoyaltyTierConfig, string>
{
    Task<IReadOnlyList<LoyaltyTierConfig>> GetAllForTenantAsync(CancellationToken ct);
    Task<LoyaltyTierConfig?> GetByTierAsync(LoyaltyTier tier, CancellationToken ct);
}

public interface ILoyaltyTransactionRepository : IRepository<LoyaltyTransaction, string>
{
    Task<IReadOnlyList<LoyaltyTransaction>> GetForAccountAsync(string accountId, int offset, int limit, CancellationToken ct);
    Task<int> CountForAccountAsync(string accountId, CancellationToken ct);
}
```

`EnsureForUserAsync` is the pragmatic move — lazy-creates the account on first read or first earn event. Avoids needing to backfill all existing users in the migration.

## Domain service

```csharp
public interface ILoyaltyService
{
    Task GrantForCompletedOrderAsync(string orderId, CancellationToken ct);
    Task RevokeForCancelledOrderAsync(string orderId, CancellationToken ct);
    Task<decimal> ResolveTierDiscountForOrderAsync(string userId, decimal orderTotal, CancellationToken ct);
}
```

`ResolveTierDiscountForOrderAsync` returns the discount amount (decimal CZK, not %) given a user + order subtotal. Used by `CreateOrder.Handler` to apply the tier discount before persisting the price. Returns 0 if user has no account, or tier doesn't qualify (e.g. Silver but order < 1000 CZK).

`GrantForCompletedOrderAsync` is called from `CompleteOrder.Handler`. Idempotent: if a `LoyaltyTransaction` for this `OrderId` with `Source = OrderCompleted` already exists, no-op.

`RevokeForCancelledOrderAsync` is called from `CancelOrder.Handler`. Mirror semantics — only revokes if a prior earn exists for this orderId.

## CQRS handlers (Customer API)

### `GET /api/Loyalty/GetMy`

Returns the calling user's loyalty account snapshot:

```csharp
public class GetMyLoyalty
{
    public record Query() : IQuery<Response>;
    public record Response(
        LoyaltyTier CurrentTier,
        string CurrentTierName,                  // "Bronze Cleaner" — translated server-side or client?
        int LifetimePoints,
        int CompletedBookingsCount,
        DateTimeOffset TierAchievedOn,
        int? PointsToNextTier,                   // null when Platinum
        LoyaltyTier? NextTier,
        decimal CurrentDiscountPercent,
        decimal? CurrentDiscountMinOrderAmount,
        IEnumerable<TierPerk> CurrentPerks);

    public record TierPerk(string Icon, string LabelKey);
}
```

**Translation strategy**: tier *names* are emitted as the enum value + a stable resource key (`loyalty.tier.bronze_cleaner`). Frontend translates via i18n. Avoids backend i18n coupling.

### `GET /api/Loyalty/GetActivity?offset=0&limit=20`

Paged ledger:

```csharp
public class GetLoyaltyActivity
{
    public record Query(int Offset = 0, int Limit = 20) : IQuery<PagedData<ActivityItem>>;
    public record ActivityItem(
        string Id,
        LoyaltyTransactionType Type,
        int Points,
        LoyaltyEarnSource Source,
        string? OrderId,
        string? OrderDisplayNumber,              // joined from Order
        DateTimeOffset OccurredOn);
}
```

### `GET /api/Loyalty/GetTiers`

Returns all tier configs for the tenant — used by the Rewards tab to render the "tier ladder" view (everyone sees all tiers + their thresholds + perks, with current tier highlighted).

```csharp
public record TierInfo(
    LoyaltyTier Tier,
    int LifetimePointsThreshold,
    decimal DiscountPercent,
    decimal? MinimumOrderAmountForDiscount,
    IEnumerable<TierPerk> Perks);
```

## Integration into existing handlers

### `CompleteOrder.Handler`

After the existing order completion logic + before pipeline commit:

```csharp
await loyaltyService.GrantForCompletedOrderAsync(order.Id, cancellationToken);
```

Earn calculation lives in the service:
```csharp
int pointsEarned = (int)Math.Floor(order.TotalPrice / 10m);
```

Note: uses `TotalPrice`, which already includes any express surcharge but excludes tier discounts (since we apply tier discounts at create-time below). Fine to earn on the full charged amount — rewards customer for actual money paid.

### `CancelOrder.Handler`

After existing cancellation logic + before commit:

```csharp
await loyaltyService.RevokeForCancelledOrderAsync(order.Id, cancellationToken);
```

Idempotent: only revokes if there's a prior earn entry for this order.

### `CreateOrder.Handler`

After computing `finalTotalPrice` (which currently applies the express surcharge):

```csharp
if (!string.IsNullOrEmpty(command.UserId))
{
    var tierDiscount = await loyaltyService.ResolveTierDiscountForOrderAsync(
        command.UserId, finalTotalPrice, cancellationToken);
    finalTotalPrice -= tierDiscount;
}
```

The applied tier discount needs to be persisted on the Order so we can show it in the receipt. **Add fields to Order entity**:
- `decimal? TierDiscountAmount`
- `LoyaltyTier? TierAtPurchase`

Mobile/web order detail can render "Loyalty discount: -X CZK (Gold Polisher)" when present.

## Migration strategy

Single migration, all five new entities + columns on `Order`:

```
AddLoyaltyFoundation
- Create LoyaltyAccounts table
- Create LoyaltyTierConfigs table
- Create LoyaltyTransactions table
- Add Order.TierDiscountAmount (nullable decimal)
- Add Order.TierAtPurchase (nullable int)
- Seed LoyaltyTierConfigs with 4 default tiers (per agreed values above) — seed data file, not migration body, so per-tenant seeding works for new tenants too
```

**Seed file location**: extend `src/Cleansia.Infra.Scripts/SeedData/insert_seed_data.sql` and the root mirror at `sql-scripts/insert_seed_data.sql` with the 4 tier rows. Use a `WHERE NOT EXISTS` guard so re-runs don't error.

No backfill of existing users — `EnsureForUserAsync` lazy-creates accounts on first read/earn.

## Mobile Rewards tab

Replace the Wave 1 marketing placeholder with a real read-only view. Phases:

### Mobile Phase L1.1 — Read-only Rewards tab

Sections, top to bottom:

1. **Hero** — current tier badge (large icon + tier name + threshold), lifetime points number, completed bookings count.
2. **Progress bar** — "X / Y points to next tier" or "You've reached the highest tier".
3. **Current perks card** — list of perks for current tier (icon + label).
4. **Tier ladder** — vertical list of all 4 tiers, each showing threshold + discount perk + status (locked / unlocked / current).
5. **Activity card** — last N transactions, link to "See all".

No code-redemption UI in Phase A. No referral UI. That's Phase B/C.

### Mobile data layer additions (`core/loyalty/`)

- `LoyaltyDtos.kt` — DTOs mirror backend response types
- `LoyaltyApi.kt` — Retrofit binding, three endpoints
- `LoyaltyRepository.kt` — `@Singleton`, exposes `account: StateFlow<LoyaltyAccountDto?>`, `tiers: StateFlow<List<TierInfoDto>>`, `loaded`, `refresh()`. Activity is fetched on-demand in the activity screen, not held in repo.
- `LoyaltyModule.kt` — Hilt provides
- `LoyaltyRepositoryEntryPoint.kt`
- Sign-out plumbing — `clear()` call sites: AuthAuthenticator (×2), AuthRepository.logout, UserRepository.deleteAccount.

### Mobile screens

- `RewardsTab.kt` — full rewrite (current is mock). MainShell pre-fetches alongside catalog/orders.
- `RewardsActivityScreen.kt` — paged activity history (route: `rewards/activity`).

### i18n keys (EN + CS)

- `loyalty.tier.bronze_cleaner` / silver_mopper / gold_polisher / platinum_sparkler
- `loyalty.tier_label_short` (just "Bronze" / "Silver" / "Gold" / "Platinum") for compact pills
- `loyalty.lifetime_points` — "Lifetime points"
- `loyalty.bookings_completed` — "Completed cleanings"
- `loyalty.progress_to_next` — "%1$d / %2$d points to %3$s"
- `loyalty.max_tier_reached` — "You've reached the top — thank you!"
- `loyalty.perks.welcome_badge` — "Welcome to Cleansia rewards"
- `loyalty.perks.discount_5_above_1000` — "5% off bookings over 1000 CZK"
- `loyalty.perks.discount_10` — "10% off all bookings"
- `loyalty.perks.discount_15` — "15% off all bookings"
- `loyalty.perks.priority_support` — "Priority customer support"
- `loyalty.perks.dedicated_pool` — "Dedicated cleaner pool"
- `loyalty.activity_title` — "Recent activity"
- `loyalty.activity_view_all` — "See all"
- `loyalty.tx_earn_order` — "+%1$d pts — Cleaning #%2$s"
- `loyalty.tx_revoke_order` — "%1$d pts — Cancelled #%2$s"
- `loyalty.tx_referral` — "+%1$d pts — Referral bonus"
- `loyalty.empty_activity` — "No activity yet. Book your first cleaning to start earning."

## Web parity (Phase A)

Lower priority for the foundation phase. Recommended scope:

1. Render current tier + lifetime points on the Profile page (small card).
2. New `/rewards` route with the same content as mobile Rewards tab.
3. Same i18n keys (5 locales).

## Out of scope for Phase A

- Promo codes (Phase L2)
- Referrals (Phase L3)
- Admin UI for granting bonus points / configuring tiers (Phase L4)
- Push notifications on tier upgrade (Wave 4 — deferred)
- Loyalty discount on the receipt PDF (cosmetic — bump if needed)

## Execution order

1. **B1 — Backend foundation**: entities + EF config + migration + seed data + repositories + LoyaltyService + 3 query handlers + integrate into CompleteOrder/CancelOrder/CreateOrder.
2. **MANUAL_STEP**: user runs migration + NSwag regen.
3. **M1 — Mobile data layer**: DTOs + API + Repository + EntryPoint + sign-out plumbing.
4. **M2 — Mobile Rewards tab UI**: replace mock with real read-only view.
5. **W1 — Web Rewards page** (parallel with M2): render tier + activity on `/rewards`.

Estimated total: B1 ~half day, M1 ~couple hours, M2 ~half day, W1 ~half day.

## Verification before reporting Phase A done

- A new user gets a `LoyaltyAccount` lazily on first Rewards tab open.
- Completing an order grants `floor(price/10)` points + advances tier when threshold crossed.
- Cancelling a completed order revokes points + may downgrade tier.
- Booking a new order while at Silver+ shows the tier discount in the summary breakdown.
- Order detail (mobile + web) renders the tier discount line if `TierDiscountAmount > 0`.
- Rewards tab matches the agreed thresholds and discounts.
- Rewards activity feed shows recent transactions in correct order with correct amounts.
- All sign-out paths clear the loyalty cache.

## Out-of-scope risks I want to flag

- **Concurrency on tier crossings**: if two CompleteOrder events fire concurrently for the same user (rare — would require parallel orders both completing at the same exact moment), they could each see "user is Silver" and not detect the Gold crossing. Mitigation: rely on EF concurrency token on `LoyaltyAccount.UpdatedOn`. Document but don't over-engineer.
- **Order discount drift**: if backend recomputes tier discount on `CreateOrder` but the mobile booking VM also previews it (so user sees "10% off" before submit), we have the same potential mismatch we just fixed for express surcharge. Phase A backend computes the discount; mobile VM previews via the same logic, sends the discounted total, backend re-validates. Mirror the express-surcharge pattern.
- **Existing orders without `TierAtPurchase`**: pre-loyalty orders simply have null tier-at-purchase. Receipts/order details handle null gracefully (no discount line shown).
