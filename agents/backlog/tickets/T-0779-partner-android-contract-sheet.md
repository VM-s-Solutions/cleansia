---
id: T-0779
title: Partner Android — the contract sheet with the swipe on the three take paths; the detail line/banner; the shared HTML view; the pin (ADR-0068 D4, D6)
status: done
size: M
owner: —
created: 2026-09-20
updated: 2026-09-20
depends_on: [T-0777]
blocks: [T-0781]
stories: []
adrs: [ADR-0068, ADR-0063]
layers: [android]
security_touching: false
manual_steps: []
sprint: —
---

## Context

ADR-0068 D6: the three take paths on the Android partner app were one tap each
(`OrdersListViewModel.takeOrderInline`, `OrderDetailViewModel.take`, the pending-offers confirm at
`8a7710b0`). The contract text is rendered **in-app** under the swipe — the one departure from
ADR-0063 D9 — so a shared JavaScript-off HTML view lands in `:core` for the customer app to reuse.
Runs after T-0777's re-dumped `partner-mobile-api.json`, beside T-0778.

## Doing

- `:core` — `ui/components/HtmlContentView` (`AndroidView { WebView }`, JavaScript off, no navigation,
  a server HTML string).
- `features/orders/WorkContractSheet.kt` + `WorkContractSheetViewModel.kt`: a bottom sheet taking
  `orderId` + a mode (`Take` / `Accept`) or an `acceptanceId` (`Read`); loads the preview or the
  accepted contract; renders the facts and the text; `SlideToCommit` labelled *Swipe to accept the
  contract for work*; on completion `takeOrder(orderId, acceptedWorkContractTextId)` or
  `acceptWorkContract(...)`; `Read` mode without the slider, with the acceptance facts.
- `OrdersRepository`: `takeOrder(orderId, acceptedWorkContractTextId)`; `acceptWorkContract`,
  `getWorkContractPreview`, `getWorkContract`; `OrdersMutation.AcceptWorkContract` invalidates `Active`.
- The three entry points open the sheet: the list's inline take, the detail's take, the pending offer's
  confirm.
- The detail: the caller's row by `orderEmployeeId == my assignedEmployees entry's id`; the line, or
  the banner with *Accept* opening the sheet in `Accept` mode; Start **and** Complete map
  `contract.acceptance_required` to the sheet in `Accept` mode.
- Mismatch: `contract.text_mismatch` → reload the preview, reset the slider, notice;
  `legal.document_not_found` → notice, slider disabled.
- Strings in five locales (escaped apostrophes); the four keys in the app's `api.*` error map; the
  parity roster.
- Tests: the `TakeOrderCommand` wire pin; `WorkContractSheetViewModelTest` (take called once with the
  previewed id; the mismatch path's view-model state; accept mode; read mode); `OrderDetailViewModelTest`;
  `PendingOffersViewModelTest`.

## NOT

- No customer-app work (T-0781 reuses `HtmlContentView`); no iOS; no scroll-to-end gate; no PDF; no
  change to the `order.assigned` push; no backend change; no change to `SlideToCommit`'s gesture.

## Done looks like

Every take on Android goes through the sheet and the swipe; the take carries the previewed text id; a
reassigned cleaner sees the banner, swipes, starts or completes; `:partner-app:testDebugUnitTest` and
lint green; `mergeDebugResources` clean in all five locales.

## Acceptance criteria

- [x] **AC1** — the board, the detail and a pending offer open the sheet with the preview and a
      slider; completion calls `takeOrder` once with the preview's `legalDocumentTextId` and the panes
      refresh through the existing mutation once (`WorkContractSheetViewModelTest`, `PendingOffersViewModelTest`).
- [x] **AC2** — `contract.text_mismatch` reloads the preview, resets the slider state and exposes the
      notice; `legal.document_not_found` disables the slider with its notice.
- [x] **AC3** — an assigned seat with no row shows the banner; Start / Complete first → the sheet in
      `Accept` mode; the swipe replaces the banner with the line and the action succeeds
      (`OrderDetailViewModelTest`).
- [x] **AC4** — **Read the contract** opens `Read` mode from `getWorkContract(acceptanceId)` with the
      stored facts and the accepted version, no slider.
- [x] **AC5** — the `TakeOrderCommand` wire pin asserts the member and the four keys are in the
      parity roster for five locales.

## Status log

- 2026-09-20 — filed by the docs lane from the batch-9 panel; runs after T-0777, beside T-0778.
- 2026-09-20 — done in **`9fea7853`** (*the contract for work sits under the take — every take opens
  the sheet and swipes the previewed text, an admin-placed seat sees the banner and accepts, both
  parties' line reads the accepted text*): `:core`'s `HtmlContentView`, the sheet + view-model, the
  repository members and mutation, the three entry points, the detail's line / banner / refusal
  mapping, five locales, the wire pin and the view-model tests.
