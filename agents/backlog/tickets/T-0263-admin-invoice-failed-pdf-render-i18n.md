---
id: T-0263
title: Admin invoice failed-PDF render (failed-vs-pending indicator + error text) + i18n — carried frontend half of T-0238
status: blocked
size: S
owner: pm
created: 2026-06-15
updated: 2026-06-15
depends_on: [T-0238]
blocks: []
stories: []
adrs: []
layers: [frontend]
security_touching: false
manual_steps: [nswag-regen]
sprint: 7
source: T-0238 AC3/AC4 (frontend half) — HELD on the admin nswag-regen at Wave-6 close
---

## Context
**Carried frontend half of T-0238.** T-0238 shipped its BACKEND half in Wave 6 (`b8f89202`): the admin
`EmployeeInvoiceDto` / `EmployeeInvoiceDetailDto` now expose `PdfGenerationFailed` (bool) +
`PdfGenerationError` (string?) with red-first mapper tests, and the `nswag-regen (admin)` hold-point was
flagged to the owner. The **frontend half (T-0238 AC3/AC4) could not start** because it depends on the
owner regenerating the admin NSwag client so the generated `EmployeeInvoiceDto`/`EmployeeInvoiceDetailDto`
carry the two new fields. This ticket carries that held frontend AC so it is not lost at wave close.

Until this lands, the admin UI still uses the Wave-3 **proxy** (a retry-PDF action on any non-cancelled
invoice with empty `pdfBlobName`) — a *failed* generation and a *still-pending* one look identical and the
stored `PdfGenerationError` text is invisible to admins. **Q-W3-3 stays OPEN** until this ticket's AC1
lands (do not move it to `answered.md` before then).

**Blocked on `manual_step: nswag-regen (admin)`** — owner-only; the PM holds dispatch until the
regenerated admin client is confirmed to carry the two fields.

## Acceptance criteria
- [ ] **AC1 (failed-vs-pending render)** — The admin invoice list shows a **distinct failed indicator**
  for `PdfGenerationFailed === true`, visually separate from the still-pending (no `pdfBlobName`, not
  failed) state; the detail page surfaces the `PdfGenerationError` text. The existing retry-PDF action is
  unchanged. Three explicit data states intact (loading / loaded / error).
- [ ] **AC2 (i18n ×5)** — Any new user-visible string (failed label, error-prefix) has keys in all five
  admin locales (`en/cs/sk/uk/ru`) under the existing invoice scope; no hardcoded strings (TranslatePipe).
- [ ] **AC3 (closure)** — T-0171d AC4 is then fully satisfied: note the closure in T-0171's ticket and
  **move Q-W3-3 to `answered.md`**. T-0238 is then fully closed (its AC3/AC4 satisfied here).

## Out of scope
- Any backend DTO/mapper change (shipped in T-0238) and any change to PDF generation/retry mechanics
  (`RegenerateInvoicePdf` stands).
- Q-W3-2 (currency on partner pay DTOs) — separate decision, do not fold here.

## Implementation notes
- Consume the regenerated admin client's `EmployeeInvoiceDto.pdfGenerationFailed` /
  `pdfGenerationError` (and the detail DTO) — never hand-edit the NSwag-generated client.
- Extend the existing admin invoice list/detail facades + models (the T-0171d libs); no `any`,
  `OnPush` + signals, `<cleansia-*>`/PrimeNG, logic in the facade.
- Frontend-only; no backend, no ef-migration. The only manual_step is the **admin nswag-regen** this
  ticket is blocked on.

## Status log
- 2026-06-15 — **blocked** (created by pm at Wave-6 close-out). Carries the HELD frontend half of T-0238
  (AC3/AC4). Backend half is `done` (Wave-6 `b8f89202`); this half is **blocked on the owner's admin
  `nswag-regen`** so the generated DTOs carry `PdfGenerationFailed`/`PdfGenerationError`. Unblocks to
  `ready` the moment the owner confirms the regen. On the owner action list (sprint-8 §close-out). Pure
  mechanical contract-completion of a behavior already specified by T-0171d AC4 → no panel (no-decision).

## Review
<!-- reviewer / security / optimizer write verdicts here; PM reconciles before advancing state -->
