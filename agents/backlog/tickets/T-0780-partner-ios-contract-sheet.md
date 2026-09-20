---
id: T-0780
title: Partner iOS — the contract sheet with the swipe on the three take paths; the detail line/banner; the shared HTML view; the pin (ADR-0068 D4, D6)
status: done
size: M
owner: —
created: 2026-09-20
updated: 2026-09-20
depends_on: [T-0777]
blocks: [T-0781]
stories: []
adrs: [ADR-0068, ADR-0063]
layers: [ios]
security_touching: false
manual_steps: []
sprint: —
---

## Context

The same as T-0779 on iOS: the three take paths (`OrdersListViewModel.swift:190`,
`OrderDetailViewModel.swift:156`, `PendingOffersViewModel.swift:133` at `8a7710b0`) become *open the
sheet → read → swipe → take with the previewed text id*; the text renders in-app in a `WKWebView`
wrapper shared through `CleansiaCore`. Runs after T-0777's re-dumped spec, beside T-0783.

## Doing

- `CleansiaCore/Sources/CleansiaCore/Components/HtmlContentView.swift` — a `WKWebView` wrapper
  (JavaScript off, no navigation) for a server HTML string.
- `CleansiaPartner/Sources/Features/Orders/WorkContractSheet.swift` + `WorkContractSheetViewModel.swift`:
  modes `take` / `accept` / `read`; the preview or the accepted contract; the facts; the text in
  `HtmlContentView`; `SlideToConfirm` labelled *Swipe to accept the contract for work*; on completion
  `takeOrder(orderId:acceptedWorkContractTextId:)` or `acceptWorkContract(...)`; `read` without the slider.
- `PartnerOrderClient`: the take signature; `acceptWorkContract`, `getWorkContractPreview`,
  `getWorkContract`; `OrdersStaleness` gains `acceptWorkContract` → `.active`.
- The three entry points open the sheet.
- The detail: the caller's row by seat id; the line or the banner; Start and Complete map
  `contract.acceptance_required` to the sheet in `accept` mode.
- Mismatch handling as T-0779; the four keys and the copy in five locales with the catalogue-guard
  allow-list entries.
- Tests (XCTest, run by CI): the `TakeOrderCommand` wire pin; `WorkContractSheetViewModelTests` (the
  mismatch path asserts the view-model state); `OrderDetailViewModelTests`; `PendingOffersViewModelTests`;
  SwiftFormat with the repo's linebreak rule.

## NOT

- No customer-app work; no Android; no PDF; no scroll gate; no `NotifyOnTheWay` change. No Mac session
  required for CI; the owner eyeballs on a device when he chooses (A7).

## Done looks like

Every take on iOS goes through the sheet and the swipe with the previewed text id; the
reassigned-cleaner path works for Start and Complete; XCTest green for all three targets in CI;
SwiftLint/SwiftFormat clean.

## Acceptance criteria

- [x] **AC1–AC5** — T-0779's five, on iOS, with the same observable outcomes
      (`WorkContractSheetViewModelTests`, `OrderDetailViewModelTests`, `PendingOffersViewModelTests`,
      the wire pin, the catalogue guard).

## Status log

- 2026-09-20 — filed by the docs lane from the batch-9 panel; runs after T-0777, beside T-0783.
- 2026-09-20 — done in **`32fdd5d2`** (*the contract for work sits under the take — every take opens
  the sheet and swipes the previewed text, an admin-placed seat sees the banner and accepts, the line
  reads the accepted text*) + **`53d005f8`** (*the detail drops an acceptance row with no version and
  asks for the cleaner's id alongside the fetch, not after it*). XCTest for the three targets runs in CI.
