---
id: T-0852
title: Wait for booking test operations to finish before asserting their results
size: S
owner: ios
created: 2026-10-13
updated: 2026-10-13
depends_on: []
blocks: []
stories: []
adrs: []
layers: [ios]
security_touching: false
---

## Context

Finding (a) of the post-Wave E handover. `BookingViewModelTests.drainQuote()` yields five times,
which does not establish that an asynchronous quote or market reload has completed. PR #328's
first iOS attempt failed `testSelectionsTheMarketDoesNotOfferArePrunedWithANotice`; master's
push run 38392890468, attempt 1, failed `testTheQuoteNamesTheDraftsCountry`. Both subsequently
passed on a re-run. The latest checks on master `737bdb4` were verified green on 2026-10-13.

## Owner decision — 2026-10-13

Mike: "Fix the findings as well".

The governing record is
`/Users/michael/.codex/scratchpads/cleansia-wave-f-2026-10-11/next-wave/decisions-2026-10-13.md`.
The approved first phase fixes the shared wait, covers both failed tests, and makes each RED
with a scratch delay in its fake before proving GREEN. PR #328's local closure travels first.

## Doing

- Ground-truth the shared wait and both failures.
- Replace the shared yield helper within `BookingViewModelTests` with a bounded condition wait.
  The two failed tests observe quote completion and the pruning event. Preserve other suites'
  use of the booking fakes.
- Prove both failures with scratch delays in the catalogue and quote fakes, then prove both pass
  with those delays and the fix.
- Run the complete local gates, a separate implementation review, and read-only record checks;
  wait for exact-head CI before the guarded merge.

## NOT doing

- No production booking behavior, API contract, or generated spec change.
- No change to the other suites that use the booking fakes.
- No change to `CreateRecurringViewModelTests`' separate yield-based wait; it remains reported.
- No graphify rebuild, DEV reset or deploy, or action against PRO in this phase.

## Acceptance criteria

- [x] **AC1** — Both named tests fail at the pre-fix head with delayed fake responses.
- [x] **AC2** — Both named tests pass with the fix and delayed fake responses.
- [x] **AC3** — The shared helper waits for its supplied condition and fails within a bounded
  timeout; the two failed tests' conditions identify completed work.
- [x] **AC4** — Local gates and exact-head CI pass, with any re-run and its cause recorded.

## Work record

- 2026-10-13 — `origin/master` verified at `737bdb4`; PR #328's closure `45d565b` was
  cherry-picked as the first branch commit `70abac813`. No deploy or reset ran.
- 2026-10-13 — Finding confirmed before implementation against both saved CI failure logs and
  the current tree. The shared helper now polls an explicit condition against a two-second
  monotonic deadline, suspends between checks, and reports timeout at the caller's file/line.
  All 27 callers in this test class and its extensions use it. Quotes wait for terminal state
  and the completed request; pruning waits for its event, which follows both selection writes.
  Silent/no-change catalogue paths observe the attempted request and retain their existing
  unchanged-state assertions; those paths have no separate post-return acknowledgement.
- 2026-10-13 — Scratch RED: both named tests failed with a 20 ms delay before each fake's counter
  mutation. GREEN: both passed with identical delayed fake bytes (SHA-256
  `59022fc1f0140d523f8fb7b8cf33265b8adafd4d4dc80526f7ba0134e2a9181d`). The fakes were restored
  byte-for-byte (SHA-256 `5127838b117fef4daba23f75074c22ccef374fada9623d7bfa89756920617d6e`).
  Each named test then passed 200 repetitions without the delay: 400 runs, zero failures.
- 2026-10-13 — Full local iOS schemes ran in CI order on 18.6: Core 776 passed; partner 1,113
  passed; customer 1,779 passed, one skipped, zero failed. Customer on 16.4 likewise had 1,779
  passed, one skipped, zero failed. The skipped case is
  `ProfileEditChipTests/testVoiceOverAnnouncesTheWholeLabelWhileTheChipIsTruncated`; its
  `UIAccessibility.isVoiceOverRunning` guard was false. A separate 18.6 attempt after enabling
  the fresh simulator's documented accessibility preference remained skipped. No separate
  16.4 attempt ran. This does not establish a VoiceOver result.
- 2026-10-13 — Pinned SwiftFormat 0.60.1 lint and SwiftLint 0.65.0 strict passed. Ran
  `scripts/tests/check-local-config.test.sh`, `scripts/generate-api-clients.sh`, and
  `xcodegen generate` in both app directories. The generated outputs are ignored; no mobile
  spec dump or NSwag regeneration ran. The workspace package pins stayed byte-identical.
- 2026-10-13 — Full frontend unit run: 5,038 passed in 408 suites across 71 projects with tests;
  three other test targets contained no tests. All three production builds and three smoke
  suites (seven cases) passed. Android compile passed; the full test command
  used `--rerun-tasks`: core 296, partner 839, customer 1,683 passed, zero failures/errors/skips.
  Docs references, catalogue claims, VitePress build, iOS symbols and booking-policy parity,
  including their self-tests, passed. The history secret scan passed on the closure head;
  the new committed head's scan remains due.
- 2026-10-13 — Evidence root:
  `/Users/michael/.codex/scratchpads/cleansia-next-wave-2026-10-13/p1/`.
  `developer/validation.json` (SHA-256
  `7db1aebedae8a6c20b0df79c0e5982037eaa5b1466356fe0ba3d68961d355caf`) preserves exact commands,
  counts, result bundles, scratch hashes and generation receipts; `local/` holds the other
  local gates. This fix was checked only by commands. No CHANGELOG entry: test-only change.
  Graphify was not run, per the approved NOT list. `CreateRecurring`'s separate wait remains
  reported, outside this fix. Exact-head CI and guarded merge remain due.

## Review

The pre-implementation read-only verdict is **CONFIRMED** (`verifier-before.md` in the
evidence root).

Implementation review: **approve-with-notes** (`reviewer-round1.md`). Independent execution
passed all 53 tests in `BookingViewModelTests` on 18.6, without rebuilding. Pinned formatting
and strict lint passed; five off-tree checks of the exact helper verified immediate success,
delayed success, timeout and caller diagnostics. The fakes' restored hash and 200+200 repeat
counts were independently checked. No required code correction was found. Two catalogue waits
prove request entry only; no-op predicates rely on current synchronous guards. Those limits
remain recorded above. The general consistency checker does not scan Swift and was **NOT RUN**
for this change, not a pass.

Record refutation: round 1 confirmed three imprecisions in evidence paths, historical wait
citations and T-0839's current approval wording; all were corrected. Fresh round 2:
**CLEAN. No confirmed material residuals.** (`refuter-round2.md` in the evidence root).
It checked the records, actual/proposed commit messages and PR text against primary receipts
and confirmed unchanged reviewed source hashes. Refutation stops at this clean round.

### PR #329 closure — 2026-10-13

The remaining gates above were completed on head
`b67c985b710fc466458635083d7cc16c1963393a`. The committed-head history secret scan,
docs references, catalogue claims and VitePress build passed. Exact-head CI passed **7/7
checks on attempt 1**, across six workflows: Android CI, Frontend CI (build and e2e),
iOS CI, Docs CI, iOS Symbols and Secret scan. No job was re-run. Backend and booking-policy
parity were not triggered; the latter passed locally.

[#329](https://github.com/VM-s-Solutions/cleansia/pull/329) merged at
`2026-10-13T15:09:42Z`, by a merge request guarded with the tested head SHA. Merge
`155e14067322262d17d5224346621da55860a19c` has the same tree as that head.
The current master's post-merge checks are tracked separately; this closure records the
PR's completed gates. T-0813 B remains passive: this head had no failed build.

Closure receipt:
`/Users/michael/.codex/scratchpads/cleansia-wave-f-2026-10-11/raw/ci/nw-p1-pr329-ci-merge-closure-001.json`,
SHA-256 `a89da38f3eb132b9e0ce9463d2f272cd1eaeb8f04c6317c3d25c8550367a8ab5`.
It hashes the check snapshot, saved job logs and annotation summary, pre-merge read,
guarded merge and deployment-run reads. Fresh GETs for `deploy-dev.yml`, `deploy-pro.yml`,
`deploy-docs.yml` and `execute-sql.yml`, created since the merge timestamp, each returned
zero runs. No reset, deploy or PRO operation ran in this phase. Graphify was not run.
This closure travels as the first commit of the backend branch from that merge.

- 2026-10-13 — Separate post-merge master proof for #329: all four triggered push checks passed on merge155e14067322262d17d5224346621da55860a19c, first attempts. Snapshot/log/annotation receipts are retained under Wave F `raw/ci/nw-p1-master-155e14067322-poll-001` and `raw/ci/nw-p1-master-155e14067322/evidence`; next-wave `p1/master-ci-receipt.json` SHA256c15e24d47a6fcb0de9e97b463384c3a97f4e663804ef45065b427ecb88a336ad. This augments the separate master proof, not the already committed #329 pre-merge closure. No deploy was dispatched.
