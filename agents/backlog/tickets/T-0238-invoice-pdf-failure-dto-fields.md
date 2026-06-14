---
id: T-0238
title: Expose PdfGenerationFailed/PdfGenerationError on admin EmployeeInvoice DTOs (closes Q-W3-3 / T-0171d AC4 display)
status: ready
size: S
owner: pm
created: 2026-06-12
updated: 2026-06-14
depends_on: [T-0171]
blocks: []
stories: []
adrs: []
layers: [backend, frontend]
security_touching: false
manual_steps: [nswag-regen]
sprint: 6
source: Q-W3-3 (questions/open.md) — T-0171d AC4 display gap (Wave-3 close)
---

## Context
T-0171d's AC4 requires the admin invoice list/detail to show the **explicit failed state** of PDF
generation, but `EmployeeInvoiceDto` / `EmployeeInvoiceDetailDto` don't expose the domain fields
`PdfGenerationFailed` / `PdfGenerationError` (`EmployeeInvoice.cs:46-51`). The Wave-3 default
shipped a **proxy**: a retry-PDF action on any non-cancelled invoice with empty `pdfBlobName` — so a
*failed* generation and a *still-pending* one look identical and the stored error text is invisible.
Filed as **Q-W3-3** (blocking: partial — AC4 display only). This ticket is the follow-up that closes
the proxy: add both fields to the two DTOs + mappers, owner regenerates the admin client, frontend
renders the failed flag + error message.

**Gated on the owner's Q-W3-3 answer** (expected yes — the default already anticipates it) and on
the **nswag-regen** hold-point between the backend and frontend halves. No-decision note: the
behavior was specified by T-0171d AC4; this is contract completion, not new design — skips the panel.

## Acceptance criteria
- [ ] **AC1 (backend)** — `EmployeeInvoiceDto` and `EmployeeInvoiceDetailDto` carry
  `PdfGenerationFailed` (bool) + `PdfGenerationError` (string?) mapped from the domain; xUnit mapper
  tests red-first.
- [ ] **AC2 (hold-point)** — MANUAL_STEP `nswag-regen` (admin client) flagged to the owner; frontend
  half HELD until confirmed.
- [ ] **AC3 (frontend)** — Admin invoice list shows a distinct failed indicator (vs pending); detail
  shows `PdfGenerationError` text; retry action unchanged. i18n ×5 for any new strings.
- [ ] **AC4** — T-0171d's AC4 is then fully satisfied; note the closure in T-0171's ticket and move
  Q-W3-3 to `answered.md`.

## Out of scope
- Q-W3-2 (open.md — currency on partner pay DTOs): separate decision, do not fold here.
- Any change to PDF generation/retry mechanics (`RegenerateInvoicePdf` stands).

## Implementation notes
Pure additive DTO change → backward-compatible regen. Follow the record-DTO positional convention.
Frontend: extend the existing invoice list/detail facades (T-0171d libs), three data states intact.

## Status log
- 2026-06-12 — draft (created by pm at Wave-3 close; Q-W3-3 made a ticket)
- 2026-06-14 — **ready** (PM, Wave-6 intake / Batch **6B**). Dep T-0171✓. Contract completion of T-0171d
  AC4 (the owner default already anticipated "yes") → no panel. **Backend DTO half first → flag
  `nswag-regen (admin)` to the owner → HOLD the frontend half** until the regenerated admin client is
  confirmed. On closure, move Q-W3-3 to `answered.md` and note the closure in T-0171. Disjoint files from
  the rest of 6B. Plan: `status/sprint-8.md` §3 Batch 6B.

## Review
<!-- reviewer / security / optimizer write verdicts here; PM reconciles before advancing state -->
