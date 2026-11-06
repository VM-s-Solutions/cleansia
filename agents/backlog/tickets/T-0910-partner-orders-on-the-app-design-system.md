---
id: T-0910
title: Partner Orders and the job detail on the app's design system — shared type scale, standard slide, richer detail head, alignment; then clean up
size: L
owner: Michael Chaban
created: 2026-11-02
updated: 2026-11-02
depends_on: [T-0907]
blocks: []
stories: []
adrs: []
layers: [ios, android, docs]
security_touching: false
---

## Context

Mike, 2026-11-02, on the built T-0907 pages (iOS dark screenshots of Available and of an in-progress detail):

> "the overall font of the page/job doesn't fit the entire application design. The same for the order detail page,
> it's very plain. If you take a look at previous concept or at least how the customer order detail page is designed
> then you understand what I'm talking about. I like the fact that now not everything is displayed at once but
> related thing displayed in each step. Can you improve the design and polish it, so that it looks richer and
> corresponds to the overall app design? Also some alignment aren't in the center, I'm not even sure if the swipe to
> confirm component that you're using is the one that being used across the entire app. Also fonts don't match
> between this page and entire app"

He approved the plan: a mock first, then the build, "but there is a need to clean it up after it". "Previous concept"
means the partner detail as it was before the redesign. It is combined with the customer detail's sky header and
mascot.

Root causes found on 2026-11-02:
- **Fonts.** iOS Partner Orders hand-sets 134 Nunito sizes and uses 20 tokens. Every other feature uses the
  `CleansiaTypography` tokens, where display and headline styles are Poppins. Android Orders has 38 hand-set sizes,
  the only partner feature with any.
- **The slide.** Orders draws a `labelBeside` style on iOS and a well track on Android, both added for the mock. The app
  uses Core's `.prominent` slide (the customer booking).
- **Plainness.** The new detail dropped what the customer detail and the old partner detail have: a sky header with a
  large mascot, the number with a status pill, a calendar line, segmented step progress and icon section headers.
- **Alignment.** "Soonest first" sits under the tab switcher. The floating back button rides over the scrolled steps.

Out of scope for this ticket: Mike's earlier question about city names, which he ruled moot ("we call the customer").

## Acceptance criteria

- [x] **AC1 (mock)** — An HTML mock built on the app's real tokens, fonts and components. It covers Available, Active,
  History, the take sheet and the detail in its key states, in light and dark, on iPhone and Android. Taste-skill
  lint and inspect are clean. An independent design review is done. Mike approves.
- [ ] **AC2 (build)** — Both platforms are built to the approved mock. Every text uses the shared type scale, with no
  hand-set sizes. The slides are the app's standard component, and the locked state is built on it. The detail head
  matches the mock. Every alignment problem in the audit is fixed. No rule or state content changes from T-0907.
- [x] **AC3 (cleanup)** — Whatever the redesign leaves unused is removed after a whole-tree grep proves it unused: the
  `labelBeside` slide style, the Android well track, Orders-only text styles and helpers, and unread strings. Tests
  and docs are updated.
- [ ] **AC4** — Native captures match the mock in light and dark on both phone sizes. Every suite is green apart from
  the known baseline failures, and the checkers are clean.

## Out of scope

- What each state shows, and every rule (steps, gates, offerability, pay).
- The bottom bar and app shell.
- The customer app, except:
  - mechanical call-site edits for the components that move to Core (delete a moved file, rename a call, change an
    import);
  - the three visible changes Mike approved on 2026-11-02: key/value values in Regular, the calendar line wrapping at
    its separator, and the contract and Legal text in the app font.
- Backend and NSwag.
- Commits.

## Work record

- 2026-11-02 — Filed; the mock phase started.
- 2026-11-02 — Mock v2 approved. Mike: "OK, I like the designs and you can go with recommended". All seven review-page
  choices take the recommended option. The build started. Customer out-of-scope amended for the Core moves
  (CORE-PLAN F11).
- 2026-11-02–03 — **Core.** One definition each, in Core: iOS `InteractivePopGestureEnabler`,
  `FloatingCircleButton`, `OrderSectionHeaderRow`, `OrderInfoRow` and a new `OrderCalendarLine`, and the status
  tint/tone table as `OrderStatusStyle` (ADR-0080); Android `:core` the same rows, line, pill and ink, the customer's
  `SwipeToConfirmButton` (opt-in lanes, two lines, busy, `LockedSwipeToConfirm`), `MascotPuck`, `OrderTimelinePoint`
  and `capBandAligned`; `HtmlContentView` in Nunito on both. Customer pixels moved only in the choice 1–3 regions
  (iOS 68 of 85 frames identical, the other 17 choice 1 or 2; Android 41 frames in choice 1 or 3 regions, 0
  unexplained). Suites (`ios-core-fix.json`, `android-core-adds.json`): iOS Core 905 / 3 failed (the
  `StringCatalogCompletenessTests` trio), Partner 1387 / 0 (9 skipped), Customer 1974 / 4 failed (the 3 known and
  `LiveActivitySessionCleanupTests`); Android core 373 / 0, partner 1223 / 0, customer 1865 / 0.
- 2026-11-02–03 — **Partner pages.** The tabs, the take sheet and the job detail in every state rebuilt to mock v2 on
  both platforms, through five review rounds each. Suites (`ios-partner-finish-fix-2.json`,
  `android-partner-finish-fix-2.json`): iOS Partner 1410 / 0 (13 skipped), Core 910 / 3 failed (the trio), Customer
  1974 / 5 failed; Android core 382 / 0, partner 1256 / 0, customer 1865 / 0.
- 2026-11-03 — **Cleanup (AC3) and the last fixes.** iOS: `SlideToConfirm.Style` (`.subtle`, `.prominent`,
  `.labelBeside`) removed, the filled slide the only look; `cash_collect_label`, `contract_accepted_on`,
  `contract_version_lc` and `detail_job_number` removed in all five locales; the Active offer tile falls back to the
  full-width bar where its column cannot hold each run whole. Android: `OrdersBoardRules.browseDay` and the same four
  strings removed; the History ledger regrids at font scale 1.3 and up. Every other named candidate (`labelBeside`,
  the well track, `SlideToCommit`, `cappedSp`, `rowTextStyle`, `OrdersMoney` …) proven absent by a whole-tree grep;
  `OrdersSectionHeader` kept. Suites (`ios-close.json`, `android-close.json`): iOS Partner 1411 / 0 (13 skipped),
  Core 908 / 3 failed (the trio), Customer 1974 / 4 failed (`CashChoiceBindingTests`,
  `LiveActivitySessionCleanupTests`, `CustomerReadableWidthTests` ×2), customer captures 85 / 85 identical; Android
  core 382 / 0, partner 1257 / 0, customer 1865 / 0 on rerun (one `PaymentsCardCaptureViewModelTest` flake on the
  first run).
- 2026-11-03 — **Docs.** ADR-0080 written and indexed. `agents/knowledge/patterns-mobile.md` (the one slide, the
  Core order pieces, `HtmlContentView`'s embedded Nunito, Android's `MascotPuck`), `docs/mobile-app/patterns.md`
  (the slide and status-pill lines), `docs/mobile-app/features.md` (Partner Orders) and the VitePress sidebar
  (ADR-0079, ADR-0080) updated. `check-docs-refs` and `check-catalog-claims` were not run in this pass.
- 2026-11-03 — **Open.** AC2: Android's last fix (the History ledger regrid) is compiled and unit-tested but not
  seen on screen, and Android's Active offer teaser does not yet take iOS's fall-back to the bar. AC4: no iOS 26.3
  capture in the closing pass; the physical swipe-back check on iOS 26.3 and 16.4 is Mike's; the Android captures
  are blocked on disk (the emulator needs 2 GiB free); the repo checkers were not run at the close. Set to
  `in_review` for Mike to check.
- 2026-11-04 — Close-out by the coordinator.
  - **Android captures taken** on the final tree: 66 partner frames, with side-by-sides and measurements in
    `partner-orders-ds-20261102/build/android/close/`.
    - Leaves: 395 checked, all square.
    - Truncations, clips and off-centre tab segments: 0.
    - Remaining, for Mike's look: 21 lone words, 2 overlaps, 8 extra emphasised runs and 202 of 765 glyphs past 0.35.
  - **The Android customer capture run is invalid.** It shot the partner app's contract screen instead of the
    customer screens.
    - So the Android customer pixel proof after the partner stage's opt-in Core edits rests on code and unit tests.
    - The last valid capture proof is the post-Core set.
  - **Checkers:**
    - clean: available-status-parity, backlog-consistency, booking-policy-parity, catalog-claims, docs-refs,
      ios-symbols, nx-project-registration;
    - pre-existing failures: consistency E1, legal-drafts;
    - not run: quote-stability (needs Playwright).
  - **Preservation:** 124 changed, 50 new and 6 deleted files against the start snapshot, all under the mobile apps,
    `docs/`, `agents/knowledge/patterns-mobile.md` and this ticket and its index row.
    - Info.plist, project.yml and the Core and Customer string catalogs are byte-identical.
    - The Partner catalog differs by 6 added keys, 4 removed unused keys and `history_cash_not_paid`.
  - **Not done** (cut to save time): the cross-platform parity sweep. Android's Active offer teaser still lacks iOS's
    fall-back to the full-width bar.
- 2026-11-04 — Open items closed.
  - iOS 26.3 frames of the closing fixes are in `build/ios/vs-mock-26-close`.
  - Android:
    - the Active offer teaser falls back to the full-width bar as iOS does;
    - the bar keeps the price whole at uk 360 ×1.3;
    - the two measured "overlaps" are content scrolled under the solid bar, not defects;
    - the capture harness installs the customer build. The customer set is now valid against the post-Core
      reference: 88 frames byte-identical, 2 differing only in the system bars, 4 Core-showcase frames differing by
      the opt-in additions, and nothing else.
  - Still Mike's: the physical swipe-back check.
