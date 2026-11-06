---
id: T-0907
title: Partner Orders redesign, "the working day" — day panels, day line, pay tile, run-sheet job detail, on iOS and Android
size: L
owner: Michael Chaban
created: 2026-11-01
updated: 2026-11-01
depends_on: [T-0902]
blocks: []
stories: []
adrs: []
layers: [ios, android, docs]
security_touching: true
---

## Context

Mike asked for the Partner Orders page to be redesigned with the method used for the booking confirmation (the taste
skill: one dealt style, three concepts, three judges, two verify-and-fix passes), covering all three tabs and the job
detail. He approved the design on 1 November 2026 ("I like the design of the partner app order overview page") and the
build with every call as recommended ("Yup, go ahead with all of them"). It replaces T-0902's tab layout (uncommitted,
`in_review`) and rebuilds the job detail, which T-0902 left as it was.

- Review page: https://claude.ai/artifact/TXBVxPGUHvfAAFxKSPDLFj
- Build spec, mock and frames: `/Users/michael/cleansia-work.noindex/partner-orders-taste-20261101/final/`
  (`SPEC.md`, `frame.html`, `shots/`). §0 is the scope and §0.2 the decided calls.
- Evidence: `/Users/michael/cleansia-work.noindex/partner-orders-build2-20261101/`

Security-touching because of M13: the crew-mate's given name and phone are shown to the other cleaner (rule S4). It is
reviewed by the security agent during the build.

## Acceptance criteria

- [x] **AC1** — CleansiaCore (iOS) gains the three public dynamic tokens and the locked `SlideToConfirm` variant of SPEC
  §0 item 0. Android's partner `SlideToCommit` gains the locked appearance. Both changes keep existing callers'
  defaults.
- [x] **AC2** — Available, Active and History and the take sheet's frame are built as drawn in `final/shots` on both
  platforms. This covers the day panels with one leaf each, the day line, the pay tile, the status line (one builder),
  the locked slide with its reason and countdown, the week strip, cash by payment status, and every state of §T2.3,
  §T3.2 and §T4.2.
- [x] **AC3** — The job detail is built for every state of §D3, with the run sheet, the board-rows store and the
  no-row entries. The removal page replaces the alert. The detail never offers a control the server refuses at that
  moment: set-off before start − 60, Take on an overlapping job, Complete without the after photo, Complete on a card
  job that has not been charged.
- [x] **AC4** — Strings of §T6 and §D8 are in en, cs, sk, uk and ru on both platforms, including the withdraw dialog's
  en rename (§0.2). Keys fall out only after a whole-tree grep shows nothing still reads them.
- [x] **AC5** — Tests: §T9 and §D10 pass on both platforms. The tests §D10 lists to rewrite or retire are handled.
  Behavioural rules are proven red first, then green.
- [x] **AC6** — Native captures at the mock's sizes match the frames state for state. They have no orphan word and no
  blank band over 40 pt inside a panel.
- [x] **AC7** — Security review of M13 done. The verdict is recorded, and the fallback (given name only, no phone) is
  applied if the review refuses the disclosure.
- [x] **AC8** — The docs passages of §0 item 6 describe the built behaviour, and `check-docs-refs` passes.

## Out of scope

- Backend, API, NSwag or migration changes.
- Pay rules, including T-0905.
- The contract text and the take sheet's four lines.
- The bottom bar, the app shell, Dashboard, Invoices, Profile, the customer apps, web and admin.
- The findings in SPEC §D11.
- Commit, push, TestFlight and DEV.

## Work record

- 2026-11-01 — Filed at build start. The tree was snapshotted at HEAD `2aa449afa`: 212 dirty paths, hashed in the
  evidence folder.
- 2026-11-01 — Built on iOS and Android. Each platform went through tabs, then the detail, then captures, then code and
  security review, then a fix pass. After that came two more fix rounds and a parity sweep. The sweep found 34 behaviour
  differences between the platforms; all were verified real and fixed. Two read-only reviews closed the work, both
  APPROVED. Hand-offs are in the evidence folder under `handoff/`.
- 2026-11-01 — AC7: the security review of M13 is ACCEPT-WITH-CONDITIONS on both platforms, so the fallback was not
  applied. The conditions were met:
  - C1: the crew-to-crew disclosure is now recorded as intended in S4, in the business-rules data-window table and in
    `#crew-access`.
  - C2: the crew-mate's number goes only to the dialer and is pinned by a test on each platform.
  - C3: Call is offered only while the job is in progress, on her own job, and only while the mate is unmarked. This is
    one rule per platform, with a test.
- 2026-11-01 — Repairs outside the platform files, each forced by the build:
  - The catalog citations the build broke in `consistency.md` (the disclosure roster) and `patterns-mobile.md`.
  - The iOS extractor of `check-available-status-parity`, which now accepts the `take(clash:…)` resolver, plus its
    self-test target.
  - The `cash_limit_at` cs/sk allow-list entry in the Core catalog test.
  - The docs sentences the build made false.
- 2026-11-01 — Gates run by the coordinator on the final tree:
  - CleansiaPartner: 1372 tests, 0 failures (7 capture tests skipped).
  - CleansiaCore package: 869 tests, 3 failures. All three are in `StringCatalogCompletenessTests`, and every key they
    name is unchanged since the build started.
  - CleansiaCustomer: 1958 tests. The 3 tests that failed before still fail (CashChoiceBinding and two
    CustomerReadableWidth tests); no others do.
  - Android: partner-app 1214 tests and core 347, both 0 failures.
  - swiftformat and swiftlint are clean on the touched files.
  - Every repo checker is clean, apart from two failures that predate the build: `check-legal-drafts` and
    `check-consistency` E1 (from T-0902).
  - Preservation: of the 212 dirty files from the start, 153 are byte-identical. The 59 that changed all sit in this
    build's areas. The 126 new paths are all expected.
- 2026-11-01 — Disk: 50 `.xcresult` bundles created by this build were deleted. Their logs and exported captures are
  kept, and the list is in `deleted-xcresults-20261101.txt`. Status `in_review`; nothing committed.
- 2026-11-01 — Reopened. Mike: "Fix the findings and also improve the design for job card". He chose to build the
  recommendation directly; the brief is `round4/BRIEF.md` in the evidence folder.
- 2026-11-02 — Round 4 built on both platforms, then reviewed, then a parity check (5 differences, fixed).
  - **The card:**
    - Every day-line mark is at least 12 pt/dp wide.
    - Haléře are drawn at the unit's size.
    - One row structure: window and area beside the tile; the scope and the flags full width underneath.
    - Amber only for cash; the dirt level is in neutral ink.
    - A scope's last item never starts a line alone.
  - **The findings:**
    - The redesign's dead code is removed.
    - The iOS currency fallback is added.
    - Android's disabled Decline has a visible disabled style.
    - The week strip prints no count while Available loads.
    - The Available badge and *See N open jobs* no longer count ended rows, and are hidden at 0.
    - The iOS door-code copy is local-only, with a 5-minute expiry.
    - The detail's no-row, removal, cancelled and late states are documented.
  - **Accepted parity fixes:** a figure in several currencies keeps its fractions; iOS money takes its decimal mark from
    the in-app language.
  - **Coordinator fixes:**
    - The lone-word tie on iOS's own-job rows (red, then green).
    - Three stale postcode comments.
    - Three new interpolated `Text` literals made `verbatim`, so builds stop extracting them as junk keys. The two junk
      keys removed from the Partner catalog.
  - **Gates on the final tree:**
    - CleansiaPartner: 1386 tests, 0 failures.
    - CleansiaCustomer: 1966 tests; only the 3 tests that already failed.
    - CleansiaCore: 869 tests. Its catalog findings are a subset of the earlier run.
    - Android: partner-app 1222, customer-app 1865, core 347, all with 0 failures.
    - Checkers clean, apart from the two pre-existing failures.
    - Preservation: 135/212 identical; all 76 changed files in owned areas. The one gone path is the dead
      `ContinuousProgressBar.kt`.
  - **Disk:** the coordinator deleted a stale 6.2 GB `build/DerivedData-partner` cache. It was last written on
    2026-10-31 and no lane used it.
