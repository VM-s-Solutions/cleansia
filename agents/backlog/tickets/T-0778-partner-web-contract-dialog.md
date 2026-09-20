---
id: T-0778
title: Partner web — the contract dialog on the board take and the detail take; the job-detail line and banner; the four keys (ADR-0068 D4, D6)
status: done
size: M
owner: —
created: 2026-09-20
updated: 2026-09-20
depends_on: [T-0777]
blocks: []
stories: []
adrs: [ADR-0068]
layers: [frontend]
security_touching: false
manual_steps: []
sprint: —
---

## Context

ADR-0068 D6: every path into `takeOrder` on the partner web was one tap (`orders.facade.ts:213-251`
at `8a7710b0`); the take now carries the id of the exact contract text row the cleaner read, so every
take passes through a dialog that shows the facts and the text and sends that id. Runs after T-0777's
regenerated `partner-client.ts`, beside T-0779.

## Doing

- `libs/cleansia-partner-features/orders/src/lib/components/work-contract-dialog/` — a PrimeNG dialog
  with a facade; modes `take` / `accept` (loads `getWorkContractPreview(orderId, uiLanguage)`) and
  `read` (loads `getWorkContract(acceptanceId, uiLanguage)`); the facts table (number, date/time window
  from `cleaningDateTimeUtc` + `estimatedMinutes`, price + currency, `locationApproximate`,
  rooms/bathrooms, services, packages, extras) and `contentHtml` through `[innerHTML]` + the
  sanitizer; a `<cleansia-checkbox>` *I have read and accept the contract for work for this job*; the
  primary button enabled by the tick; version and effective date under the title; `read` mode without
  tick or button, with the acceptance facts (*accepted in {language}* when it differs).
- `orders.facade.ts`: `takeOrder(orderId)` opens the dialog; the dialog's submit calls
  `takeOrder({ orderId, acceptedWorkContractTextId: preview.legalDocumentTextId })`; the reload runs
  **once**. The board row's take and the detail's take go through it.
- The job detail: the caller's row is the `workContractAcceptances` entry whose `orderEmployeeId` is
  the caller's own `assignedEmployees` entry; present → the line + **Read the contract**; absent
  while assigned → the banner with a button opening `accept` mode; Start and Complete stay offered and
  map `api.contract.acceptance_required` to the dialog in `accept` mode.
- The mismatch: `api.contract.text_mismatch` → re-run the preview, re-render, untick, notice;
  `api.legal.document_not_found` → notice, button disabled.
- Five locales: the four `api.*` keys + the dialog and detail strings; `PARTNER_SURFACE_ERROR_KEYS`
  gains the four; `error-contract-parity.spec.ts` green.
- Specs: `work-contract-dialog.component.spec.ts`, `work-contract-dialog.facade.spec.ts`,
  `work-contract-dialog.models.spec.ts`, `orders.facade.spec.ts` (the reload exactly once),
  `order-details.facade.spec.ts` / `order-details.helpers.spec.ts` (line, banner, the refusals open
  the dialog).

## NOT

- No swipe/slider on the web (Q-WC-02 default); no PDF; no pending-offers page; no scroll-to-end gate;
  no change to the register form's tick.
- No Android/iOS/customer/admin work; no backend change.

## Done looks like

Taking a job from the board or the detail always passes through the dialog; the take carries the
previewed text id; a reassigned cleaner sees the banner, accepts, and can start or complete; the four
keys read in five locales; Jest green for the lib and the parity spec.

## Acceptance criteria

- [x] **AC1** — the board's Take opens the dialog with the preview's facts and text and a disabled
      primary button; tick + confirm calls `takeOrder` once with `{ orderId, acceptedWorkContractTextId }`
      and the facade's reload runs exactly once (`work-contract-dialog.component.spec.ts`,
      `orders.facade.spec.ts`).
- [x] **AC2** — `contract.text_mismatch` re-fetches the preview, re-renders, unticks and shows the
      notice; `legal.document_not_found` shows its notice with the button disabled
      (`work-contract-dialog.facade.spec.ts`).
- [x] **AC3** — an assigned seat with no row shows the banner and its button, Start is offered, a
      Start / Complete `contract.acceptance_required` opens the dialog in `accept` mode, and acceptance
      replaces the banner with the line (`order-details.facade.spec.ts`, `order-details.helpers.spec.ts`).
- [x] **AC4** — **Read the contract** opens `read` mode from `getWorkContract(acceptanceId)` with the
      stored facts and the accepted version, no tick, no button.
- [x] **AC5** — the five locale files carry the four `api.*` keys and the dialog/detail strings;
      `error-contract-parity.spec.ts` is green.

## Status log

- 2026-09-20 — filed by the docs lane from the batch-9 panel; runs after T-0777, beside T-0779.
- 2026-09-20 — done in **`5effc18d`** (*every take passes through the contract-for-work dialog, and a
  placed cleaner accepts from the job detail*): the dialog component + facade + models under
  `components/work-contract-dialog/`, its stylesheet in the shared assets, the facade hand-off on the
  board and the detail, the detail's line / banner / refusal mapping through `order-details.helpers.ts`,
  the four keys on `PARTNER_SURFACE_ERROR_KEYS` in five locales, and the specs above.
