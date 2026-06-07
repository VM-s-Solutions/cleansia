---
id: T-0160
title: "AUD-01a: Refund entity + EF config + PaymentStatus.PartiallyRefunded + RefundReason enum (the refund projection schema)"
status: draft
size: M
owner: —
created: 2026-06-06
updated: 2026-06-06
depends_on: []
blocks: [T-0162, T-0164]
stories: []
adrs: [0006, 0009]
layers: [backend, db]
security_touching: false
manual_steps: [ef-migration]
sprint: 2
source: ADR-0009 follow-up AUD-01a (was the panel's single AUD-01, split a..e); ADR-0006 D5 projection + ADR-0009 D4 enums
---

## Context

Wave-2 BUILD of the refund decision frozen in **ADR-0006** (the seam) and **ADR-0009** (the policy).
This child lands the **schema foundation** the rest of the refund build sits on: the `Refund` projection
entity, its EF config + unique `RefundKey` index, and the two additive enum changes. It ships no business
logic — that is AUD-01b (seam impl), AUD-01c (admin command + allocator), AUD-01d (loyalty), AUD-01e
(cancel/dispute migration).

ADR anchors (verified 2026-06-06):
- `PaymentStatus` is `Pending=1 … Disputed=5` with **no `PartiallyRefunded`**
  (`src/Cleansia.Core.Domain/Enums/PaymentStatus.cs:7-13`). ADR-0009 D4 adds `PartiallyRefunded = 6`.
- There is **no `RefundReason` enum** anywhere (only Stripe's own `RefundReasons` at `StripeClient.cs:66`).
  ADR-0006 D1 / ADR-0009 D4 name `RefundReason { CustomerCancellation, DisputeResolution, AdminDiscretion,
  ServiceNotRendered }`.
- The `Refund` projection shape is frozen by ADR-0006 D5:
  `Refund { Id, OrderId, ReceiptId?, DisputeId?, Amount, Currency, RefundKey (unique), Reason,
  StripeRefundId?, Source (AppRefund|Chargeback), Status (Pending|Succeeded|Failed), CreatedOn, ConfirmedOn? }`
  — `ITenantEntity`, FK to `Order`, nullable FK to `Receipt`/`Dispute`.

## Acceptance criteria
- [ ] **AC1 — `RefundReason` enum exists.** Given no `RefundReason` exists today, When this lands, Then
  `Cleansia.Core.Domain.Enums.RefundReason` exists with exactly `CustomerCancellation`,
  `DisputeResolution`, `AdminDiscretion`, `ServiceNotRendered` (ADR-0009 D4), with `[SwaggerEnumAsInt]` if
  it crosses the wire. Evidence: enum type + a test asserting the four values.
- [ ] **AC2 — `PaymentStatus.PartiallyRefunded = 6` added.** Given `PaymentStatus` ends at `Disputed = 5`,
  When this lands, Then `PartiallyRefunded = 6` is appended **without** changing the existing wire values
  (additive, no breaking read). Evidence: enum value + a test confirming `Pending=1…Disputed=5` unchanged.
- [ ] **AC3 — `Refund` entity exists with the ADR-0006 D5 shape.** Given the frozen projection, When this
  lands, Then a `Refund : ITenantEntity` entity exists with the D5 fields, an FK to `Order`, nullable FKs
  to `Receipt`/`Dispute`, and a `Source` (AppRefund|Chargeback) + `Status` (Pending|Succeeded|Failed).
  Evidence: entity + EF entity configuration.
- [ ] **AC4 — Unique index on `Refund.RefundKey`.** Given ADR-0006 D3's S7a/S7b backstop, When the EF
  config is read, Then there is a **unique index on `RefundKey`** so a concurrent double-issue collapses on
  23505 (the seam resolves-to-existing — AUD-01b). Evidence: EF config index + a migration that creates it.
- [ ] **AC5 — Migration flagged owner-only.** Given the new table + enum column changes, When this ticket
  closes, Then `manual_step: ef-migration` is flagged to the owner and the dependent build (AUD-01b/c/d) is
  held until the owner confirms the migration is applied. (Claude never runs `dotnet ef`.)

## Out of scope
- `IRefundService` implementation, the Stripe call, the idempotency-key param on `IStripeClient` — AUD-01b.
- The admin command, the allocator, `RefundPolicy` — AUD-01c.
- Loyalty clawback (`RevokeForPartialRefundAsync`, `LoyaltyEarnSource.OrderPartiallyRefunded`) — AUD-01d.
- Migrating `CancelOrder`/`ResolveDispute` onto the seam — AUD-01e.
- The `PackageService.PriceWeight` schema — AUD-02p (T-0165).

## Implementation notes
- **Governing ADRs:** ADR-0006 D5 (the `Refund` projection shape, the unique-`RefundKey` rule) and
  ADR-0009 D4 (the exact enum values). Cite, do not re-decide.
- **Routing:** db (entity + EF config + migration) with a reviewer in parallel. `security_touching: false`
  (no auth/secret surface; the privileged refund **command** is AUD-01c, which is `security_touching`).
- **Manual step:** `ef-migration` (owner) — the `Refund` table + the `PaymentStatus`/`RefundReason` column
  changes. Per ADR-0009 these fold with AUD-01d's loyalty-source migration if sequenced together; flag
  separately if not.
- **TEST-FIRST:** assert enum values + the unique-`RefundKey` constraint (a duplicate insert throws 23505)
  red-first per `agents/knowledge/testing.md`.

## Status log
- 2026-06-06 — draft (created by pm from ADR-0009 follow-up AUD-01a; gated on T-0140 done ✓; Wave-2 build)

## Review
<!-- reviewer / security / optimizer write verdicts here; PM reconciles before advancing state -->
