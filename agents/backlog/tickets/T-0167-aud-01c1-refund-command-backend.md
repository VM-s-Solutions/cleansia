---
id: T-0167
title: "AUD-01c1 (split of T-0162): Admin partial-refund command + share-of-TotalPrice allocator + RefundPolicy (window/fee) + PartiallyRefunded summary (backend)"
status: done
size: M
owner: —
created: 2026-06-07
updated: 2026-06-15
depends_on: [T-0160, T-0161, T-0231]
blocks: [T-0168, T-0170, T-0173]
stories: []
adrs: [0001, 0006, 0009]
layers: [backend]
security_touching: true
manual_steps: [nswag-regen]
sprint: 2
source: split of T-0162 (AUD-01c) — backend half (allocator + policy + command + summary); ADR-0009 D1/D2/D3
---

## Context

Backend half of the L-split of **T-0162 (AUD-01c)**. Delivers the admin partial-refund **command** end:
the share-of-frozen-`Order.TotalPrice` allocator (ADR-0009 D2), the caller-side `RefundPolicy` (14-day soft
window + fee-bearer rule, ADR-0009 D1/D3), the admin-only command that issues through the `IRefundService`
seam (T-0161), and the derived `PaymentStatus.PartiallyRefunded` summary (ADR-0006 D5). No UI here — the
admin refund UX is the sibling child **T-0168**.

Depends on the refund schema (T-0160), the seam (T-0161), and the bundled-gross basis (**T-0231**, the
AUD-02p backend child) so AC7's bundled-service path can compute a gross. Backend + frontend children may
contract-lock against ADR-0009 in parallel, but the command's bundled-line path stays red until T-0231 is
`done`.

## Acceptance criteria
- [ ] **AC1 — Allocator: share of frozen `Order.TotalPrice`** (ADR-0009 D2). `lineRefund_i =
  round(lineGross_i / Σ(lineGross) × Order.TotalPrice, 2)`; last chosen line absorbs the sub-cent residual;
  discount + express surcharge are NEVER re-applied (already embedded in `TotalPrice`). Evidence:
  TC-REFUND-ALLOC — a discounted+express order reconciles penny-perfect to `TotalPrice`.
- [ ] **AC2 — VAT apportioned, null-rate guarded** (ADR-0009 D2). `refundVat_i = round(lineRefund_i ×
  AppliedVatRate/(100+AppliedVatRate), 2)`, `0` when `AppliedVatRate` is null. Evidence: TC-REFUND-VAT
  (payer + non-payer).
- [ ] **AC3 — `RefundPolicy` 14-day SOFT window** (ADR-0009 D1). Caller-side `RefundPolicy.IsWithinWindow`
  (sibling to `BookingPolicy`, NOT in `IRefundService`) allows within 14 days of `Order.CompletedAt`; a
  closed window needs a persisted non-empty admin override reason; null `CompletedAt` is closed-by-default;
  `Source=Chargeback` is exempt. Evidence: TC-REFUND-WINDOW.
- [ ] **AC4 — Fee bearer driven by `RefundReason`** (ADR-0009 D3). Platform absorbs the Stripe fee on
  `ServiceNotRendered`/`DisputeResolution`; deducts only on `AdminDiscretion`; cancel-fee path untouched.
  Evidence: TC-REFUND-FEE.
- [ ] **AC5 — `PaymentStatus` summary derived** (ADR-0009 D2 / ADR-0006 D5). `0 < Σ succeeded < charged` ⇒
  `PartiallyRefunded`; at equality ⇒ `Refunded`; order lifecycle stays `Completed`. Evidence:
  TC-REFUND-CEILING.
- [ ] **AC6 — Admin-only command** (ADR-0001). Gated by an `AdminOnly` `Policy.*` permission (additive map
  row + same-PR frozen-snapshot update, ADR-0001 D2); non-admin denied (403). Evidence: per-permission test.
- [ ] **AC7 — Bundled-service refund works** (depends on T-0231). `lineGross` for a single bundled service
  derives from the weight-split of `Package.Price` (ADR-0009 D5) and feeds the D2 allocator unchanged.
  Evidence: a test refunding one bundled service then the rest never exceeds the package line's share of
  `TotalPrice`.

## Out of scope
- The admin refund UX (T-0168). The `Refund` entity/enums (T-0160), the seam (T-0161), `PackageService
  .PriceWeight` schema+backfill (T-0231). Loyalty clawback wiring is T-0163/T-0164; this command **calls**
  `RevokeForPartialRefundAsync` if T-0163 is `done`.
- Re-deciding the allocation formula, window, or fee rule — frozen by ADR-0009.

## Implementation notes
- **Hard dependency on T-0231** for AC7's bundled-line gross. Contract-lock the command/DTO shape against
  ADR-0009 first so T-0168 (UX) can start against a fixed contract.
- **Serialization:** `Policy.cs`/`PolicyBuilder.cs` cluster (head T-0100) — serialize the `AdminOnly` map
  edit against the other Wave-2 `Policy.cs` editors (T-0170, T-0173). `LoyaltyService.cs` clawback call is
  T-0163/T-0164.
- **`security_touching: true`** (money-out, privileged). **`nswag-regen`** — the admin client gains the
  refund command DTO; hold T-0168 until the owner regenerates.
- **TEST-FIRST:** TC-REFUND-ALLOC/VAT/WINDOW/FEE/CEILING + the per-permission `AdminOnly` test, red-first
  (money math + authz are on the strict red→green list).

## Status log
- 2026-06-07 — draft (created by pm as the backend half of the T-0162 L-split; depends_on T-0160, T-0161,
  T-0231; Wave-2 build).
- 2026-06-08 — backend (v1): landed test-first — `RefundPolicy` (14-day soft window + fee-bearer switch),
  `RefundAllocator` (share-of-`TotalPrice` + VAT apportion), `IssuePartialRefund` command (AdminOnly via
  `Policy.CanIssueRefund` → `AdminOnly`), `PartiallyRefunded` summary, bundled-line path via
  `PackagePricing.DeriveIncludedServiceGrosses`. **Parallel reviewer + security caught 3 real money defects
  the dev's tests were vacuous on** (REQUEST-CHANGES / FAIL): (1) allocator dropped the `PerRoomPrice` term
  from the standalone-service gross (ADR-0009 D5.1); (2) fee/VAT/net split-brain on `AdminDiscretion` (VAT
  apportioned from the pre-fee amount, post-fee amount sent); (3) the window-override reason was validated
  then **discarded**, not persisted (ADR-0009 D1 audit requirement). Owner decision taken on the 4th item
  (invented `1.4%+6 CZK` fee): **per-country config value**.
- 2026-06-08 — backend (rework, ACCEPTED): architect pinned the per-country fee seam, dev fixed all 3 + the
  fee config, re-reviewed. **`CountryConfiguration.RefundStripeFeeRate`/`RefundStripeFixedFee`** (nullable
  decimals + `UpdateRefundStripeFee` mutator) hold the fee, resolved via `order.CustomerAddress?.CountryId`
  → `ICountryConfigurationRepository.GetByCountryIdAsync` (the `ReceiptService` precedent); **null country /
  config / either-field → fee 0 (platform absorbs, never throws)**. Fee/VAT/net now all derive from the
  seam-confirmed `result.Amount`. Standalone gross = `BasePrice + PerRoomPrice × (Rooms+Bathrooms)` (matches
  `OrderPricingCalculator.cs:30`). New nullable **`Refund.WindowOverrideReason`** (maxlen 500) persists the
  audit reason, threaded command → `RefundRequest` (additive optional, seam behavior unchanged) →
  `Refund.Create`. Invented fee constants removed. Re-review: reviewer APPROVE-WITH-NITS, security
  PASS-WITH-NOTES — all 3 findings verified fixed against re-derived money. Day-14 inclusive-boundary test
  added (orchestrator). Build 0/0; `Cleansia.Tests` **770** green (refund suite 70).

  **MANUAL_STEP: ef-migration (owner — Claude does not run `dotnet ef`).** Additive, all nullable (safe, no
  backfill): `CountryConfiguration.RefundStripeFeeRate` (decimal? precision 5,4), `RefundStripeFixedFee`
  (decimal? precision 18,2); `Refund.WindowOverrideReason` (varchar(500), null). The squashed `Initial`
  migration + model snapshot are stale until regenerated — IntegrationTests/runtime will `PendingModelChanges`
  until applied.
  **MANUAL_STEP: nswag-regen (owner) — admin client.** New `AdminRefundController` endpoint +
  `IssuePartialRefund.Command`/`.Response` cross the wire. Hold **T-0168** (admin refund UX) until regenerated.
  **OWNER action item (not code):** set CZ `RefundStripeFeeRate=1.4`, `RefundStripeFixedFee=6` via admin/seed
  once the migration lands. Until then null→0 means CZ **absorbs** the Stripe fee on `AdminDiscretion`
  refunds (no DB seed was edited).
  **New `BusinessErrorMessage` keys (need 5-locale `errors.*` i18n — added by T-0168):** `refund.lines_required`,
  `refund.line_invalid`, `refund.override_reason_required`.

## Review
- 2026-06-08 reviewer (rework): **APPROVE-WITH-NITS** — re-derived the money by hand; findings 1/2/3 verified
  fixed; build + 61/61 Refunds tests green. Nits: day-14 boundary test (now added); `RequiresWindowCheck`
  is forward-facing policy API whose first real caller is AUD-01e (AC3 chargeback-exempt is satisfied here by
  `IsWithinWindow` null/window semantics).
- 2026-06-08 security (rework): **PASS-WITH-NOTES** — the v1 BLOCKER (override reason not persisted) is fixed
  end-to-end with a test on the stored value; S-authz / S7a-b idempotency / money-out integrity / no-phantom
  / S4-PII all re-confirmed, no regression. Notes are the expected owner manual steps (ef-migration additive
  nullable; nswag-regen; CZ fee figure).
