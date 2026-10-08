---
id: T-0804
title: Remove the redundant board seat count
size: M
owner: backend
created: 2026-10-08
updated: 2026-10-08
depends_on: [T-0803]
blocks: []
stories: []
adrs: []
layers: [backend, db, docs]
security_touching: true
---

## Context

Mike selected the recommended focused B010 scope after merging Wave A PR #311 on
2026-10-08. The approved estimate is 1–2 engineering days including verification and
paired measurements. This is one branch and PR for one production cause. INDEX.md
is the only status authority.

The defect remains on merged master `6fa0492badf9ab5c9f30fee3beb680f7fcc1f265`:
`OrderSpecification` conjoins a takeable-seat count when `HasAvailableSpots` is true,
then repeats that count inside the restricted caller's open-and-offerable branch.
Retaining the outer `P` permits `P AND (assigned OR (P AND offerable))` to become
`P AND (assigned OR offerable)`. False/null availability branches still need the
inner count. Wave A board p95 was 630.50 ms after correct default sorting; this wave
targets the 300 ms audit budget without promising a gain.

## Doing

Conditionally omit the repeated inner count only when the outer predicate requires
it. Add meaningful regressions to an existing relational test fixture. Preserve all
other filters and capture current before/after evidence.

## Acceptance criteria

- [x] **AC1** — Restricted availability-true exact-count and ordered-ID queries emit
  one takeable-seat aggregate rather than two. The SQL-shape assertion fails before
  the change and passes afterward.
- [x] **AC2** — True/false/null and restricted/unrestricted results retain exact
  counts and IDs across own/foreign/no assignments, full/empty/cover seats, zero
  capacity, offerability, hold, currency, cash, activity and tenant guards.
- [x] **AC3** — Existing projection, deterministic order, revalidation, paging,
  frozen-pay and security regressions pass. API, DTO, policy and client inputs stay
  identical. No schema, index, migration or generator change is introduced.
- [x] **AC4** — Both HTTP arms use one newly anchored 100000-order synthetic fixture,
  Release profile, identities and instrumentation. Each selected cohort has five
  runs of 100 samples with excluded warmup. Exact-query cohorts, retained bodies,
  unique same-host trace joins and bounded read-only plans support median/p95 results.
  Any remaining budget miss is reported without expanding scope.
- [ ] **AC5** — Applicable full backend verification, existing repo checkers and docs
  build pass. Latest master is merged before final verification. Independent source,
  security and evidence review passes, and existing applicable PR CI is green. Mike
  approves the concrete PR before merging.

## NOT doing

Broad Wave B, other waves or unranked findings; projection/query-count rewrites,
approximate totals, caches, exports/cursor endpoints, API/client/cap/offset/sort
changes; schema/index/migration/planner/JIT changes; web assets, native startup,
logging/compression or CI changes. No new maintained framework, type, harness or
gate. No personal settings or string-catalog changes. No DEV/PRO, az, providers,
APNs, physical devices, TestFlight/distribution or deployment.

## Done looks like

One narrowly scoped PR with the conditional predicate change, regressions, paired
measurements, independent review and green applicable CI, ready for Mike's merge
decision. Missing the 300 ms target does not authorize another optimisation.

## Implementation notes

Reuse current specification composition. The caller-assigned branch stays outside
offerability; cover-requested seats retain their count semantics. False currently
means no outer availability filter, just like null. Preserve the existing empty
string check, status use, hold/currency/cash predicates and handler count/projection.

Scratch evidence and exact command ledger:
`/Users/michael/.codex/scratchpads/cleansia-board-query-2026-10-08`.
Historical audit: [AUDIT-2026-10-07.md](../../AUDIT-2026-10-07.md).
Current wave evidence: [BOARD-QUERY-2026-10-08.md](../../BOARD-QUERY-2026-10-08.md).

## Work record

- 2026-10-08 — Mike selected recommended B010. Latest master was fetched and isolated
  branch `fix/board-seat-predicate` starts at the Wave A merge. An unchanged source
  archive and successful Release solution build were captured before production
  edits. Fresh fixture preparation and RED characterization are in progress.
- 2026-10-08 — Final unchanged RED executes 31 cases: 30 pass and the duplicate-seat
  SQL-shape assertion fails as intended. The conditional change passes all 31.
  False/null retain their original seat-first composition. The exact final source
  also builds in Release for the AFTER request arm; the focused GREEN source's sole
  terminal-newline difference is archived and disclosed.
- 2026-10-08 — BEFORE completes 2500 successful scored requests plus 50 warmups,
  with retained bodies and unique same-host trace joins. Relevant fixture rows,
  statistics, OIDs/pages, indexes, planner and JIT settings match the frozen clone.
  Eleven captured SELECT case/bind vectors each have five read-only plan samples,
  separately from endpoint timing. The paired AFTER clone matches all non-clock
  fields and its 50-request smoke comparison passes exact response bytes.

- 2026-10-08 — Both scored arms and independent retained-byte/per-run review close.
  True-board p95 is 637.367→140.797 ms (-77.9%); controls stay within0.9%.
  All9748 backend tests, nine repo checkers, eight checker self-test files and
  docs build pass. The corrected existing graph rebuild includes both changed
  C# files with matching source hashes. All trace overrides reset; owned hosts
  and measurement container stop, with raw evidence and volume retained.
- 2026-10-08 — Latestmaster was fetched/merged again at17:02:32UTC; no new changes.
  Implementation cause commit is `79309d206dbfe6ee1ced7bbb32765e8b3c060305`.
  Existing applicable PR CI and Mike's merge approval are the remaining gates.

## Review

Independent implementation/security review accepts the conditional algebra with
the outer predicate retained and false/null unchanged. The 26 added regressions
are meaningful and the saved GREEN TRX has 31 passes without skips/errors.
Paired scored evidence and full local applicable verification pass. The before /
after true-board server median/p90/p95 is 619.508/629.561/637.367 to
137.196/139.684/140.797 ms, 500 samples per arm, all response groups identical.
The 9748 full backend tests, nine checkers, eight checker self-test files and
docs build pass. Full source/build lineage and independent evidence review are
retained. Current-head CI and Mike's concrete merge approval remain the AC5
gates. False/null p95 remains near one second and is outside this focused scope.
