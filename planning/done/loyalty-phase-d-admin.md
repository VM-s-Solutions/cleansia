# Loyalty Phase D — Admin tooling

Admin UI in the existing customer-admin app (`apps/cleansia-admin.app`) for managing Phase A/B/C primitives. Backend was deliberately left without admin endpoints during A/B/C; D adds them.

## Scope decisions

| Surface | Build now? |
|---|---|
| Promo code CRUD (list / create / edit / deactivate) | YES — without this admin can't issue codes |
| Tier config edit (thresholds + discount % + perks) | YES — lets you tune tiers without redeploying |
| Manual point grant ("give user X 500 tier-points") | YES — common ops/support need |
| User loyalty account view (balance + activity + tier) | YES — support diagnostics |
| Referral analytics (top referrers, viral coefficient) | NO — defer to a polish wave; basic list is enough |
| Promo code analytics (uses, revenue impact) | NO — basic list with redemption count is enough; deeper analytics later |
| Bulk operations (import codes from CSV, bulk-grant) | NO — premature; admin can use SQL for one-offs |

## Backend additions

### Admin API surface (Cleansia.Web.Admin)

#### Promo codes
```
GET    /api/admin/PromoCode/GetPaged                 — list with filters (active, expired, etc.)
GET    /api/admin/PromoCode/GetById/{id}             — detail with redemption history
POST   /api/admin/PromoCode/Create                   — create new code
PUT    /api/admin/PromoCode/Update/{id}              — edit (limited fields — code itself is immutable)
POST   /api/admin/PromoCode/Deactivate/{id}          — soft-disable (keeps history)
GET    /api/admin/PromoCode/GetRedemptions/{id}      — paged redemption log per code
```

Editable fields: `IsActive`, `ValidFrom`, `ValidUntil`, `MinimumOrderAmount`, `MaxRedemptionsPerUser`, `GlobalMaxRedemptions`, `Description`. NOT editable: `Code`, `Type`, `DiscountPercent` / `DiscountAmount` (changing these mid-flight would create audit nightmares — admin deactivates and creates a new code).

#### Tier configs
```
GET    /api/admin/LoyaltyTier/GetAll                 — current 4 tier configs
PUT    /api/admin/LoyaltyTier/Update/{id}            — edit threshold / discount / perks
POST   /api/admin/LoyaltyTier/UpdatePerks/{id}       — special endpoint for the JSON array
```

Editable: `LifetimePointsThreshold`, `DiscountPercent`, `MinimumOrderAmountForDiscount`, `PerksJson`. NOT editable: `Tier` (the enum value itself).

**Important**: editing thresholds mid-flight retroactively reclassifies users. Example: if you raise Silver from 500 → 1000 pts, users with 500..999 pts go from Silver to Bronze. Admin UI should warn before saving with a "X users will be downgraded" preview. Implementation: a small endpoint `POST /api/admin/LoyaltyTier/PreviewThresholdImpact` that takes the proposed new thresholds and returns counts.

#### Manual point grants
```
POST   /api/admin/Loyalty/GrantPoints                — body: { userId, points, reason }
POST   /api/admin/Loyalty/RevokePoints               — body: { userId, points, reason }
```

These insert `LoyaltyTransaction` rows with `Source = ManualGrant`. The `reason` (free-text, max 500 chars) is stored in `Description`. Audit trail is the existing transactions table.

#### User loyalty inspection
```
GET    /api/admin/Loyalty/GetUserAccount/{userId}    — full account snapshot
GET    /api/admin/Loyalty/GetUserActivity/{userId}   — paged activity for a specific user
```

These reuse the customer-side query logic but bypass the "current user from JWT" path — admin specifies userId explicitly.

#### Referral inspection (basic)
```
GET    /api/admin/Referral/GetPaged                  — list all referrals with filters (status, date range)
GET    /api/admin/Referral/GetByUser/{userId}        — referrals where user is referrer OR referred
```

No edit/delete endpoints — referrals are append-only audit data.

## Permissions (PolicyBuilder additions)

All grant to `PhysicalPolicy.AdminOnly`:

- `CanViewPromoCodes`
- `CanCreatePromoCode`
- `CanUpdatePromoCode`
- `CanDeactivatePromoCode`
- `CanViewLoyaltyTierConfigs`
- `CanUpdateLoyaltyTierConfig`
- `CanGrantLoyaltyPoints`
- `CanViewUserLoyalty`
- `CanViewReferrals`

Mirrors the existing admin-permission naming convention.

## Admin web app surface

### New feature module: `cleansia-admin-features/loyalty/`

Routes:
- `/admin/loyalty/promos` — promo code list (table with filters)
- `/admin/loyalty/promos/new` — create form
- `/admin/loyalty/promos/{id}` — detail + edit + redemption history
- `/admin/loyalty/tiers` — tier config table (4 rows, edit-in-place)
- `/admin/loyalty/users/{userId}` — single user's loyalty account + manual grant action + activity log
- `/admin/loyalty/referrals` — referrals list (paged)

Existing admin nav adds a "Loyalty" parent item with these children.

### Component structure (mirror existing admin features)

Look at how `cleansia-admin-features/services` or `cleansia-admin-features/users` are structured. Likely:
- `loyalty.routes.ts`
- `promo-codes/promo-codes-list.component.ts`
- `promo-codes/promo-code-detail.component.ts`
- `promo-codes/promo-code-form.component.ts` (shared between create + edit)
- `tier-configs/tier-configs.component.ts`
- `user-loyalty/user-loyalty-detail.component.ts`
- `user-loyalty/grant-points-dialog.component.ts`
- `referrals/referrals-list.component.ts`
- `loyalty.facade.ts` — single facade for the whole module (signals)

### Promo create/edit form
- Code input — uppercase normalized on blur, validated against `^[A-Z0-9]{3,20}$` regex
- Type dropdown — Percent / Fixed
- Discount input — % (5..50) for Percent, decimal CZK for Fixed
- Currency dropdown — only enabled for Fixed type
- Validity window — date pickers (both optional)
- Min order — optional decimal
- Max per user — int (default 1)
- Global max — int or "unlimited"
- Description — textarea (admin-facing notes)
- Active toggle

### Tier edit form
- 4-row table inline-edit OR per-tier modal
- Threshold-change preview shows "if you save: X users will be downgraded, Y will be upgraded"
- Perks JSON editor — start with raw JSON textarea, polish to a chip-builder UI later

### Grant points dialog
- User picker (search by email — uses existing `GetPagedUsers` admin endpoint)
- Direction toggle: Grant / Revoke
- Points int input
- Reason textarea (required, max 500)
- Confirm → calls endpoint → shows new balance in toast

## Mobile? Web customer-app changes?

Phase D is admin-only. **No mobile or customer-web changes** — both already support the Phase A/B/C surfaces those admin endpoints expose data into.

Exception: the customer activity feed will start showing `Source = ManualGrant` rows when admins grant points. Phase A's `tx_manual` i18n key already covers this.

## Migration

No new tables — all CRUD operates on tables created in Phase A/B/C. No schema changes needed.

## Phasing within L4

The four surfaces are mostly independent but share the admin module scaffolding. Recommended:

1. **L4-B1 — Backend admin endpoints**: all six controllers (PromoCode, LoyaltyTier, Loyalty user-side, Referral). Permissions added. ~1 day.
2. **MANUAL_STEP**: NSwag regen for admin client.
3. **L4-A1 — Admin UI: promo codes**: highest-priority since admin can't issue any codes without it. ~1 day.
4. **L4-A2 — Admin UI: tier configs**: ~half day.
5. **L4-A3 — Admin UI: user loyalty + grant points**: ~half day.
6. **L4-A4 — Admin UI: referrals list**: ~quarter day.

A1-A4 can overlap if you have multiple agents — they share the loyalty.facade.ts but otherwise touch different files.

## Out of scope for L4
- Bulk import (CSV upload of promo codes)
- Per-tier granular perks editor (JSON textarea is fine for v1)
- Real-time analytics / dashboards
- A/B testing of promo codes
- Email campaigns triggered from promo expiry
- CSV export of promo redemptions / referral data
