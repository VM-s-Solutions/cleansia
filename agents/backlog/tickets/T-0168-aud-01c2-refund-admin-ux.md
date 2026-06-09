---
id: T-0168
title: "AUD-01c2 (split of T-0162): Admin partial-refund UX on order detail (choose lines + reason, facade, i18n ×5)"
status: draft
size: M
owner: —
created: 2026-06-07
updated: 2026-06-07
depends_on: [T-0167]
blocks: []
stories: []
adrs: [0001, 0009]
layers: [frontend]
security_touching: false
manual_steps: [nswag-regen]
sprint: 2
source: split of T-0162 (AUD-01c) — frontend half (admin refund UX); ADR-0009
---

## Context

Frontend half of the L-split of **T-0162 (AUD-01c)**. The admin-app order-detail refund action: choose
order lines (an `OrderService`, an `OrderPackage`, or — via the bundled-gross basis — a single bundled
service) + a `RefundReason`, preview the allocated amounts, and submit to the backend command (T-0167).
**Held until the owner regenerates the admin NSwag client** (the command DTO is added by T-0167).

## Acceptance criteria
- [ ] **AC1 — Admin refund UX on order detail.** On a completed order, a refund action lets the admin choose
  lines + reason via `<cleansia-*>`/PrimeNG controls (no raw form controls), logic in a facade (not the
  component), all strings via `TranslatePipe` with keys in all 5 locales (en/cs/sk/uk/ru). Evidence:
  facade + component + `*.html` using shared components; i18n keys in all 5 files.
- [ ] **AC2 — Error-contract parity.** Every backend `BusinessErrorMessage` code the T-0167 command can
  return has a matching `errors.*` translation in all 5 locales. Evidence: the key set matches the backend
  error codes (contract-parity check).
- [ ] **AC3 — Bundled-service selection surfaces.** When a package line is expanded, its individual bundled
  services are selectable for partial refund (driven by the weight-split gross from T-0231/T-0167 AC7).
  Evidence: a facade/component test selecting one bundled service.
- [ ] **AC4 — Three explicit data states + OnPush.** The refund panel renders loading / empty / error states
  explicitly; presentational components are `OnPush`. Evidence: component spec.

## Out of scope
- The backend command/allocator/policy (T-0167). The bundled-gross schema (T-0231). Re-deciding the
  allocation or window — frozen by ADR-0009.

## Implementation notes
- **Hard dependency on T-0167** (`done`) + the owner `nswag-regen` (admin client carries the refund command).
  Do not start the component until the regen is confirmed.
- **Routing:** frontend (admin-app) + reviewer in parallel. Not `security_touching` (the privileged check is
  server-side in T-0167) — but the reviewer confirms no client-trusted authorization.
- **TEST-FIRST:** the facade spec first, then the component.

## Status log
- 2026-06-07 — draft (created by pm as the frontend half of the T-0162 L-split; depends_on T-0167 +
  owner nswag-regen; Wave-2 build).
- 2026-06-09 — frontend (v1): admin refund UX landed on order-detail (`admin-order-refund.*`) — line/reason/
  override form, facade calling `adminRefundClient.partial`, error-code→`errors.refund.*` map, i18n ×5, 3
  states + OnPush, 15 specs. Parallel reviewer caught **2 blockers**: F1 — package lines sent
  `{serviceId: undefined, packageId}`, which the backend validator rejects (every line must carry a
  serviceId); F2 — `payment_status.partially_refunded` i18n missing in all 5 locales. AC3 (bundled-service
  selection) was honestly blocked: the order DTO exposed a package's services only as display-name strings
  (no IDs), so a valid `{serviceId, packageId}` bundled line couldn't be built. Owner chose the full fix.
- 2026-06-09 — backend prerequisite **T-0168b** landed (APPROVE): additive `PackageDetails.IncludedServiceItems:
  [{Id,Name}]` (`PackageServiceRef`) + mapper, customer-app `string[]` consumer untouched, 778 tests. Owner
  regenerated the admin client.
- 2026-06-09 — frontend (rework, ACCEPTED): F1 fixed — facade now emits `serviceId: line.id` for EVERY line
  (`bundled` adds `packageId`), no whole-package line ever emitted; regression guard test asserts no empty
  serviceId. AC3 delivered — each package expands its `includedServiceItems` into selectable bundled rows
  grouped under the package. F2 fixed — `payment_status.partially_refunded` + `refund.line_kind.bundled` +
  `refund.package_group` added in all 5 locales. Reviewer APPROVE-WITH-NITS (re-derived the line mapping +
  the F1 guard); all 4 ACs met. Verified independently: `nx test order-management` 19/20 green (the 1 failure
  is the pre-existing, unmodified `order-management.component.spec.ts` HttpClient-DI defect — unchanged from
  HEAD, out of scope). Nit: the `admin-*` selector-prefix lint is pre-existing across the whole lib —
  flagged for an eslint-config alignment / cleanup ticket, not a T-0168 regression.

## Review
- 2026-06-09 reviewer (rework): **APPROVE-WITH-NITS** — F1/F2/AC3 genuinely fixed, AC1–AC4 met, refund
  suites green; only nit is the lib-wide pre-existing selector-prefix lint.
