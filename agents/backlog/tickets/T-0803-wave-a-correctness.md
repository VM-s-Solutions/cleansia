---
id: T-0803
title: Restore SSR data, compatible paging and iOS session cleanup
size: L
owner: pm
created: 2026-10-07
updated: 2026-10-08
depends_on: []
blocks: []
stories: []
adrs: []
layers: [backend, frontend, android, ios, docs]
security_touching: true
---

## Context

Mike selected recommended Wave A on 2026-10-07 after the [technical audit](../../AUDIT-2026-10-07.md).
The approved estimate is 4–7 engineering days including regression verification and comparable
measurements. This is one approved wave, branch and PR; each independent cause receives its own commit.
The backlog INDEX is the only status authority.

The current source is `c66a03168d3810d79299dfca48688bf7168d814a`, verified against latest master
before work. The customer landing page suppresses public data errors and renders placeholders;
administrative navigation advertises pages that shared validation rejects; excessive page sizes can
materialize large responses; order pages lack a deterministic default before paging. iOS session
teardown leaves persisted partner checklist ticks and customer Live Activities outside the wipe set.

## Acceptance criteria

- [x] **AC1 / W-07** — Real SSR and hydrated browser runs receive nonempty market, currency, plan,
  service and package data without FileReader/JSON parsing failures. Generated transport works in
  Node and through JSON TransferState; typed DTOs, errors and anonymous cache exclusions remain intact.
- [x] **AC2 / B002** — Existing advertised order pages beyond offset 500 can be reached with correct
  rows and counters. Supported navigation and API validation agree, including validation boundaries.
- [x] **AC3 / B003** — Interactive order-list requests have the recommended 100-row ceiling after a documented
  consumer inventory. Existing web/native consumers are aligned; shared noninteractive/full-list
  consumers retain their intended completeness. Any unresolved deployed-client incompatibility is
  presented to Mike before a nonadditive restriction is applied.
- [x] **AC4 / B011** — Ordinary order pages without a supplied sort use CreatedOn descending then
  Id descending before Skip/Take. Supplied sorts retain their meaning and a stable unique tie-breaker;
  currency-leading financial ordering remains intact. No schema change is required.
- [x] **AC5 / MOB-S11-01** — Partner checklist ticks are cleared by normal logout and terminal auth
  teardown. A pending account-deletion response keeps the session and ticks. Unrelated global
  preferences remain intact.
- [x] **AC6 / MOB-S11-03** — Customer session termination marks shared session state inactive,
  clears account-specific Live Activity state and ends actual ActivityKit activities, including
  restored activities absent from the in-memory registry. Successful deletion and terminal auth
  use the same teardown; failed deletion preserves the session.
- [x] **AC7** — Each cause has a named regression assertion demonstrated red before its fix and
  green afterward. Current paired before/after measurements use the audit method and equivalent
  fixtures/profile with at least five observations; raw evidence and exact command ledger stay in
  the scratchpad. Correctness gains are distinguished from unmeasured speed gains.
- [x] **AC8** — Applicable backend, web, Android and iOS verification, repo checkers and docs build complete;
  an independent review covers the changed behavior and S1–S12. Latest master is merged before final
  verification. The PR carries measurement results and green CI; merging waits for Mike's approval.

## Out of scope

Waves B–F, unranked audit candidates, visual/design changes, new features, cursor/export endpoints,
schema changes, provider/APNs experiments, physical-device claims, TestFlight, deployment, DEV reset,
PRO access and all az commands. Preserve the intentional native splash hold. No new maintained
framework, general harness, type layer or CI gate. Do not change .claude/settings.local.json or commit
Xcode string-catalog churn. No opportunistic cleanup or catalog-policy edits.

## Implementation notes

Inventory consumers before tightening limits, including mobile requests and UI page-size options.
Use existing paging/sort/session/cache abstractions. Regenerate API clients from generator-supported
configuration rather than hand-editing generated files. Any wire/API incompatibility that inventory
cannot resolve within this approved policy requires a concrete coordinated decision from Mike.
Behavioral documentation belongs in docs; measurement helpers and test copies remain outside the
maintained tree. Use simulator comparisons, with distribution/provider gaps explicitly retained.

Scratch evidence: `/Users/michael/.codex/scratchpads/cleansia-wave-a-2026-10-07`.
Original audit evidence: `/Users/michael/.codex/scratchpads/cleansia-audit-2026-10-07`.

## Work record

- 2026-10-07 20:59 Europe/Prague — Mike selected recommended Wave A; latest master verified,
  isolated checkout created, and the three implementation areas are re-grounding current source.
  Existing personal settings remain in the original checkout.
- 2026-10-07 21:19 UTC — Customer transport and both native session-cleanup causes implemented.
  Qualified red/green cases and complete web/primary native suites are retained. Customer web
  paired comparisons complete; native after comparisons are running on the same owned guest and
  unchanged before API fixture. No order-limit restriction applied while coordination is pending.
- 2026-10-08 — Mike explicitly approved the recommended 100-row ceiling for all paged order
  endpoints, including both mobile APIs, after the concrete TestFlight compatibility question.
  The shared non-order 100000 limit and unrelated catalogue 1000 callers remain intact. The change
  continues on the same Wave A branch and draft PR #311, within the original 4–7 day estimate.
  The pre-cap commit passed all eight applicable CI jobs; cap regression, generated contract
  refresh and final-head verification are in progress. Deployment and merging remain separate.

## Review

Current evidence and qualifications are in [the Wave A report](../../WAVE-A-2026-10-07.md).
The independent combined review found no remaining source/security defect in the implemented scope,
with S1–S12 boundaries qualified. Backend author execution is not independent backend approval;
root complete backend verification, paired API comparisons, graph refresh and corrected Admin/API/SQL
row evidence pass. The separate AFTER SQL-count pass and independent combined evidence review
also pass: 55 matched reads, 440 successful EF commands versus 430 before, with one additional
scalar page-ID read on each Admin/board request. The coordinated limit was approved and implemented on 2026-10-08. Full final local verification passes; fresh final-head CI is still pending. The correct default board sorting raises server
median from 287.0 to 614.0 ms (p95 304.9 to 630.5 ms); this measured cost is retained for review.

- Final cap local verification: 8443 unit, 845 integration and 434 host tests pass; 105/105 final
  HTTP boundaries pass and all 90 SQL traces match Hosting logs. Three web SDKs, 453 Swift and
  429 Kotlin generated files retain exact pre-cap hashes. Final-head CI remains the publication
  gate; the pre-cap commit passed all eight applicable jobs.


## Merge closure — 2026-10-08

Mike explicitly approved merging PR [#311](https://github.com/VM-s-Solutions/cleansia/pull/311), including the disclosed board latency cost and CI budget misses. It merged at 13:27:42 UTC as `6fa0492badf9ab5c9f30fee3beb680f7fcc1f265`. Its tree is identical to verified cap head `9028d9827625de30d9aa37a9187072028a2a5eea`; all implementation cause commits are preserved. All eight PR jobs across seven workflows succeeded, with independent review and exact job-ID/URL reconciliation. Local full backend verification passed 8443 unit, 845 integration and 434 host tests. The CI integration step succeeded; its returned log has no count summary, so the local count remains separate evidence. The final PR body supplements the earlier CI-pending publication snapshot above. Board p95 remains 630.50 ms and Backend/Frontend/iOS workflow walls remain 20.95/24.60/23.98 minutes. No deployment, DEV reset, PRO/provider or TestFlight operation followed the merge.

Canonical status is recorded only in INDEX.md. This closure bookkeeping is carried into the next approved wave branch, preserving the single implementation PR for Wave A.
