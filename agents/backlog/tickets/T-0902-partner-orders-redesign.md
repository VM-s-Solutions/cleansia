---
id: T-0902
title: Partner Orders redesign on iOS and Android — calendar cards, tab switcher, Available/Active/History, take sheet with the contract summary, banner removed
size: L
owner: Michael Chaban
created: 2026-10-31
updated: 2026-10-31
depends_on: [T-0903]
blocks: []
stories: []
adrs: [ADR-0068, ADR-0079]
layers: [ios, android, docs]
security_touching: false
---

## Context

Mike reviewed the Partner Orders mock (https://claude.ai/artifact/CABKXmekgnNEEnHddWuFFH) and approved
it with the recommended answers on 31 October 2026 ("I approve and also I like the recommended
approaches…", "yup" to the four plain contract lines, "Also fix the findings"). Build spec, ground truth
and evidence: `/Users/michael/cleansia-work.noindex/partner-orders-build-20261031/` (`SPEC.md`, `gt/`,
`evidence/{ios,android}/`).

## Acceptance criteria

- [x] **AC1** — Orders opens on one sliding tab switcher (Available · Active · History) with counts; it
  measures its labels and never truncates (ru at ×1.3 and AX3 captured).
- [x] **AC2** — Available: a reserved-for-you card first, then calendar date cards with time, pay, area
  (Praha N), the distance band from home, duration, scope and decision flags (overlap, cash, crew, under
  way, dirtiness). A job clashing with one already taken disables Take and names the clash. 100 rows per page
  with *Load more*. Search and device location are gone.
- [x] **AC3** — Active: the cleaner's job leads as a status-first hero (no in-progress banner, no list
  Complete); Later rows follow. History: booked hours, a period control and totals that stay on the empty
  and error states.
- [x] **AC4** — Take sheet opened from the board has five states (loading, ready, submitting, refused,
  taken). Ready shows the facts, the four plain contract lines gated to contract versions 2026-10-05 and
  2026-10-27, the cancel/team/cash lines, then the contract text; a refusal shows the server key's own copy
  with *Back to jobs*. The taken row is read back by id through `GetPaged`, never `GetById`.
- [x] **AC5** — Pull to refresh on every state; a failed refresh with no rows held shows the error state; no
  list fetch raises a snackbar.
- [x] **AC6** — Every card and row speaks one sentence; every action label names its job; the two slides this
  build owns can be activated with VoiceOver/TalkBack actions (strings and semantics unit-tested).
- [x] **AC7** — Strings in all five locales; the 15 iOS / 14 Android removed keys have no references left.

## Out of scope

The order detail, Dashboard, Earnings, Profile, the bottom bar; Partner and admin web UI; the Customer
confirmation; extracting the date tile into Core; the unfilled-seat pay change (T-0905); any contract-text or
legal-seed change; server-side sorts or totals; commit, push, TestFlight, deploy, DEV database work.

## Work record

- 2026-10-31 — Built locally on `codex/mobile-refinements-20261029` (HEAD `2aa449afa`, uncommitted), each
  lane reviewed once and fixed once, every fix reproduced red first.
- iOS: 1,316 Partner tests, 0 failures (10 capture tests skipped unless `PARTNER_ORDERS_CAPTURE=1`);
  SwiftFormat 0.60.1 and SwiftLint 0.65.0 `--strict` clean; `check-ios-symbols` and
  `check-available-status-parity` green. Review fixes: the contract text got 0 pt at AX3 (now a hard 160 pt
  floor, heading inside the capped block); a History period change mid-load showed the previous period's
  rows; row sentences read the scope twice; the cash notice used the device locale; period-control hit
  height and AX3 overflow; two §6.7 focus rules; read mode lost the order number.
- Android: `:partner-app` 1,066 and `:core` 347 unit tests, 0 failures; debug APK builds;
  `check-available-status-parity` 0 violations. Review fixes: Active/History paging could resend forever;
  a failed refresh over an empty pane showed the empty state; the tab knob was narrower than its label; the
  reserved-row removal had no test; five nits.
- Captures: 49 iOS and 60 Android light/dark en/uk/ru cases plus one take recording per platform.
- Docs: a read-only check of ~130 docs claims against the final code found 1 wrong and 6 overstated; all
  corrected. `check-docs-refs` 0 unresolved, `check-backlog-consistency` OK, VitePress build passes.

## Open — owner's call

- **Android take sheet at 360×640:** the contract text gets about one line, because Android grows the
  capped block until the summary's first line shows. iOS keeps the contract at 160 pt and lets the capped
  block scroll. Spec §9.2's two rules cannot both hold at that size.

## Found, not fixed

- Core `SlideToConfirm` (iOS) draws its label under the thumb and clips it at AX3; Core `HtmlContentView`
  does not follow Dynamic Type (follow-up §15.4).
- iOS: AX3 squeeze beside the fixed-width pay caption; the History row subtitle truncates at AX3; flags stack
  one per line rather than wrap; reserved jobs that arrive only through `PendingOffersStore` wait for the next
  Available fetch; orphaned keys `no_matching_orders`, `scope_extras`, `unscheduled`, `swipe_to_complete`.
- Android: three string resources and the `scope_rooms`/`scope_baths` plurals still use ordinary spaces
  around `·` and between number and unit; cs/sk plurals lack `many` (lint, project-wide); `CustomerCard`'s
  intent helpers are copied into `ActiveHero.kt`; non-linear font scaling makes the 1.3× tile cap approximate.
- `check-consistency` E1: Android `OrdersListUiState` (`OrdersListViewModel.kt:85`) now carries the take
  sheet's phase as separate fields (`takeTaken`, `takenRowFailed`, `takeRefusal`); HEAD's version was exempt
  because it held two refresh flags. Not a CI gate.
- Read mode differs: iOS shows the order number in an accepted contract's read view (review fix), Android
  does not (the order line was dropped with the new facts card). Undocumented either way.
- Not verified: a real VoiceOver/TalkBack swipe, live-region announcements, reduced motion, a live API,
  iOS 17+ or a physical device.
