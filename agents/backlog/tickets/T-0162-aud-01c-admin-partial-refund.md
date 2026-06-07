---
id: T-0162
title: "AUD-01c: Admin partial-refund command over the seam + share-of-TotalPrice allocator + RefundPolicy (window/fee) + PartiallyRefunded summary + admin refund UX"
status: draft
size: L
owner: —
created: 2026-06-06
updated: 2026-06-06
depends_on: [T-0160, T-0161, T-0165]
blocks: []
stories: []
adrs: [0001, 0006, 0009]
layers: [backend, frontend]
security_touching: true
manual_steps: [nswag-regen]
sprint: 2
source: ADR-0009 D1/D2/D3 follow-up AUD-01c (the partial-refund build)
---

## Context

Wave-2 BUILD: the admin partial-refund flow ADR-0009 designed. An admin selects one or more order lines
(an `OrderService`, an `OrderPackage`, or — via AUD-02p — a single **bundled service** inside a package),
the command computes each line's refund as its **share of the frozen `Order.TotalPrice`** (ADR-0009 D2),
applies the `RefundPolicy` window + fee-bearer rule (ADR-0009 D1/D3), issues the refund through the
`IRefundService` seam (AUD-01b), and moves `PaymentStatus` to `PartiallyRefunded`. It is **admin-only**
(ADR-0001).

**This depends on AUD-02p (T-0165)** — a single bundled service has no gross until `PackageService.PriceWeight`
exists (ADR-0009 D5, fact 8); the allocator cannot refund a bundled line without it.

> **SIZE = L — must be SPLIT before it goes `ready`** (PM constraint). It is captured here as a single L so
> its dependency edges (T-0160, T-0161, T-0165) and the ADR contract are tracked; the PM splits it into
> backend (allocator + `RefundPolicy` + command + `PartiallyRefunded` summary) and frontend (admin refund
> UX) children, contract-locked first, before promoting any child to `ready`.

## Acceptance criteria
- [ ] **AC1 — Allocator: share of frozen `Order.TotalPrice`.** Given ADR-0009 D2, When a partial refund of
  chosen lines is computed, Then `lineRefund_i = round(lineGross_i / Σ(lineGross) × Order.TotalPrice, 2)`,
  the **last chosen line absorbs the sub-cent residual**, and discount + express surcharge are **NEVER**
  re-applied (they are already embedded in `TotalPrice`). Evidence: TC-REFUND-ALLOC — a discounted+express
  order's partial refund reconciles to `TotalPrice`, penny-perfect.
- [ ] **AC2 — VAT apportioned, null-rate guarded.** Given ADR-0009 D2/fact 3, When VAT is apportioned, Then
  `refundVat_i = round(lineRefund_i × AppliedVatRate/(100+AppliedVatRate), 2)`, and `0` when
  `AppliedVatRate` is null (non-VAT-payer). Evidence: TC-REFUND-VAT (VAT-payer and non-VAT-payer cases).
- [ ] **AC3 — `RefundPolicy` window (14-day SOFT).** Given ADR-0009 D1, When a refund is requested, Then a
  caller-side `RefundPolicy.IsWithinWindow` (sibling to `BookingPolicy`, NOT in `IRefundService`) allows it
  within 14 calendar days of `Order.CompletedAt`; a closed window requires a non-empty admin override
  reason (persisted); a null `CompletedAt` is closed-by-default; a `Source=Chargeback` refund is exempt.
  Evidence: TC-REFUND-WINDOW (day-14 in, day-15 out, null closed, override-with-reason re-opens,
  override-without-reason rejected).
- [ ] **AC4 — Fee bearer driven by `RefundReason`.** Given ADR-0009 D3, When the refund amount is shaped,
  Then the platform **absorbs** the Stripe fee on `ServiceNotRendered`/`DisputeResolution` (customer gets
  the full allocated amount) and **deducts** it only on `AdminDiscretion`; the cancel-fee path is untouched.
  Evidence: TC-REFUND-FEE.
- [ ] **AC5 — `PaymentStatus` summary is derived.** Given ADR-0009 D2 / ADR-0006 D5, When cumulative
  succeeded refunds are `0 < Σ < amountCharged`, Then `PaymentStatus = PartiallyRefunded`; at equality,
  `Refunded`; the order **lifecycle** status stays `Completed`. Evidence: TC-REFUND-CEILING — partial → partial
  → full never exceeds the charge; status transitions correctly.
- [ ] **AC6 — Admin-only command (ADR-0001).** Given ADR-0001 + ADR-0006 D6, When the command is added, Then
  it is gated by an `AdminOnly` `Policy.*` permission (additive map row + same-PR frozen-snapshot update,
  ADR-0001 D2); a non-admin is denied (403). Evidence: per-permission test (admin passes, customer/employee
  denied).
- [ ] **AC7 — Bundled-service refund works (depends on AUD-02p).** Given `PackageService.PriceWeight` exists
  (T-0165), When an admin refunds a single bundled service, Then `lineGross` is derived from the weight-split
  of `Package.Price` (ADR-0009 D5) and feeds the D2 allocator unchanged. Evidence: a test refunding one
  bundled service then the rest of the package never exceeds the package line's share of `TotalPrice`.
- [ ] **AC8 — Admin refund UX.** Given the admin-app order detail, When an admin opens a completed order,
  Then a refund action lets them choose lines + reason via `<cleansia-*>`/PrimeNG controls (no raw form
  controls), logic lives in a facade, all strings use `TranslatePipe` with keys in all 5 locales, and every
  backend `BusinessErrorMessage` code has a matching `errors.*` translation. Evidence: facade + component +
  i18n in all 5 files.

## Out of scope
- The `Refund` entity/enums (T-0160), the seam impl (T-0161), the `PackageService.PriceWeight` schema +
  backfill (T-0165 — a hard dependency), the loyalty clawback (T-0164 wires the call; the method is T-0163's),
  migrating `CancelOrder`/`ResolveDispute` (T-0164).
- Re-deciding the allocation formula, window, or fee rule — frozen by ADR-0009.

## Implementation notes
- **Hard dependency on AUD-02p (T-0165):** AC7 cannot land until `PackageService.PriceWeight` + the bundled
  gross derivation exist. The backend (allocator) and frontend (UX) children may contract-lock against
  ADR-0009 in parallel, but the bundled-line path stays red until T-0165 is `done`.
- **Governing ADRs:** ADR-0009 D1/D2/D3/D5 (window/allocator/fee/bundled-gross), ADR-0006 D2/D5/D7 (ceiling,
  projection, payment-state), ADR-0001 (admin-only). The window is **caller-side `RefundPolicy`**, never in
  the seam (ADR-0009 verification #1).
- **Serialization:** the `PolicyBuilder.cs`/`Policy.cs` cluster (head T-0100, then the Wave-2 linear chain
  T-0170→…); adding the `AdminOnly` refund permission edits `Policy.cs` + the snapshot — serialize against
  the other Wave-2 `PolicyBuilder` editors. The `LoyaltyService.cs` clawback call is T-0164.
- **`security_touching: true`**, **`nswag-regen`** (admin client gains the refund command — hold the
  frontend child until the owner regenerates).
- **TEST-FIRST:** TC-REFUND-ALLOC/VAT/WINDOW/FEE/CEILING + the per-permission `AdminOnly` test, red-first.

## Status log
- 2026-06-06 — draft (created by pm from ADR-0009 follow-up AUD-01c; depends_on T-0160, T-0161, T-0165;
  L — must be split before ready; Wave-2 build)

## Review
<!-- reviewer / security / optimizer write verdicts here; PM reconciles before advancing state -->
