---
id: T-0263
title: Admin invoice failed-PDF render (failed-vs-pending indicator + error text) + i18n — carried frontend half of T-0238
status: done
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
- 2026-06-15 — **IMPLEMENTED → status `review`** (frontend). Admin nswag-regen confirmed: the generated
  `EmployeeInvoiceDto`/`EmployeeInvoiceDetailDto` now carry `pdfGenerationFailed` (bool) +
  `pdfGenerationError` (string?). Implemented the failed-vs-pending render against the regenerated client.
  **AC1:** a shared three-state derivation `getInvoicePdfState()` (`failed` when `pdfGenerationFailed`,
  else `ready` when `pdfBlobName` present, else `pending`) drives both views. The invoice **list** gained a
  dedicated "PDF" column with a `#pdfStatusTemplate` badge (`invoice-pdf-badge pdf-{failed|ready|pending}`)
  — a *failed* generation is now visually distinct from a *still-generating* one. The invoice **detail**
  status banner shows the same PDF-state badge and, when `pdfGenerationFailed`, a red `pdf-error-banner`
  surfacing the stored `pdfGenerationError` text (error-prefix + message). The existing retry-PDF action
  (`getInvoiceTableActions` `pi pi-refresh`, visible on no-blob non-cancelled rows) and the detail
  regenerate-PDF button are **unchanged**. Three data states (loading/loaded/error) intact. **AC2:** new
  keys added in all five admin locales (en/cs/sk/uk/ru) under the existing scopes —
  `invoice_management.pdf_status` + `invoice_management.pdf_state.{ready,failed,pending}`, and
  `invoice_detail.{pdf_status,pdf_failed_title,pdf_error_label}` — TranslatePipe only, no hardcoded
  strings; all 5 JSONs parse. OnPush + facade-resident logic + `cleansia-*`/PrimeNG conventions followed.
  **Verification:** `nx build cleansia-admin.app --configuration=production` succeeds (0 errors; only the
  pre-existing NG8107 `employee-detail.html:77` + bundle-budget warnings remain); `nx test
  invoice-management` 34/34 green, `nx test data-protection` 12/12 green. **AC3 (closure)** is a PM/coord
  step (note closure in T-0171, move Q-W3-3 to `answered.md`) — left for the PM to reconcile on review.
  Frontend-only; no backend/ef change.

## Review
<!-- reviewer / security / optimizer write verdicts here; PM reconciles before advancing state -->
