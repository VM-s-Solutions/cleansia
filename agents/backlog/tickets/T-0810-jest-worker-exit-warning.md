---
id: T-0810
title: Wave E C03 — Jest's worker-exit warning: diagnose without hiding it, and make CI show test output again
size: S
owner: pm
created: 2026-10-11
updated: 2026-10-11
depends_on: [T-0808]
blocks: []
stories: []
adrs: []
layers: [frontend]
security_touching: false
---

## Context

The 2026-10-07 audit (C-03) saw Jest print "A worker process has failed to exit gracefully" in Frontend CI. The roadmap asks to investigate open-handle behaviour "without hiding leaks, reducing coverage, or weakening test completion checks". Scope was checked against the read-only Wave E ground truth of 2026-10-11 (off-tree: `/Users/michael/.codex/scratchpads/cleansia-wave-e-2026-10-11/raw/ground-truth/`).

**Found:**
- **The warning can no longer be seen in CI.** Nx 23.2.1, which arrived with the T-0807 dependency refresh, shows only failed tasks' output by default. The two latest successful runs print "Output of 74 successful tasks were not shown", and have 0 matches for the warning.
- **No leaking handle was found.** One `--detectOpenHandles` run for each of the 13 projects that ever printed the warning found zero open handles. Suite and test counts were identical to CI, and each process exited about 0.4 s after its tests.
- **No fix is indicated.** Jest prints the warning when a worker is still alive 500 ms after it was told to end. jsdom timers are cleared after every test file. With no resource, file or line identified, there is nothing to tear down.
- **The cause is unresolved.** The leading hypothesis, unverified, is slow worker exit under load: 3 Nx tasks × (CPUs − 1) Jest workers on a 4-vCPU runner.

## Doing

- `--output-style=static` on Frontend CI's *Unit tests (affected)* step, so every passing suite's output, counts and warnings stays in the log. The job name `build`, what runs and what passes or fails are unchanged.
- The T-0809 measurement runs with the same flag and records the warning count per run.

## NOT doing

- No test or teardown edit, because nothing was found to fix.
- No `forceExit`, no `workerGracefulExitTimeout` change, no coverage or skip change.
- No `maxWorkers` or `--parallel` change (T-0809 / T-0813).
- The lint step's hidden success output is reported only; it is informational, not a gate.

## Acceptance criteria

- [x] **AC1** — The flag lands. Locally, a run with it shows every project's Jest summary.
- [x] **AC2** — The diagnosis and the open hypothesis are recorded here. The next PR that touches `src/Cleansia.App` will show whether hosted CI still prints the warning.

## Review

- **AC1.** The flag is on Frontend CI's unit-test step. Hosted CI on PR #321 ran `nx affected -t test … --ci --output-style=static`; no project was affected, so no tasks ran. Every T-0809 measurement log shows each project's Jest summary under the same flag.
- **AC2, new evidence from T-0809.** The warning reproduces locally only in the three cold runs at default workers: 7, 4 and 7 times, at load 35–47 on 10 cores. It never appears in the twelve warm runs or in any of the six runs with bounded workers (`--maxWorkers=1` or `3`), even cold. That supports the slow-exit-under-oversubscription hypothesis; no handle leak was found.

  Bounding CI's workers would make the warning moot, but that is a CI decision: T-0813, with T-0809's numbers.

- 2026-10-11 — Shipped in PR #321. Exact-head CI on `ee60678` passed every triggered workflow: Android and Frontend `build`, Frontend e2e-smoke, Docs and Secret scan. Merged normally with the SHA guard as `c39928c`. No deploy ran. Evidence: `raw/ci/wave-e-ci-merge-closure-001.json` in the off-tree Wave E folder.
- 2026-10-12 — **AC2, the hosted check (T-0820, WCD-28).**
  - PR #322's exact-head Frontend CI (job `114700837478`, head `1136e16`) selected every project, because that PR's `nx.json` and lockfile changes affect all of them.
  - 74 test tasks and 77 lint tasks ran under `--output-style=static`: 71 Jest summaries, 408 suites and 5,038 tests passed, and none failed.
  - The log contains `failed to exit gracefully` once, in `cleansia-customer-orders` (18 suites, 331 tests).
  - For comparison, locally on the same tree (Node 22.23.3, npm 10.9.9, `run-many -t test --all --parallel=3 --ci --maxWorkers=1`), all 74 projects passed with 0 occurrences.
  - The hosted runner's default workers are where the warning still shows. That is consistent with the oversubscription hypothesis above; bounding the workers stays T-0813's decision.
- 2026-10-12 — **A second hosted observation (T-0811, [#325](https://github.com/VM-s-Solutions/cleansia/pull/325)).**
  - Exact-head Frontend CI on `be776dc` (job `114802928983`) ran all 74 test projects after the switch to inferred `jest` targets: 408 suites and 5,038 tests passed.
  - The log contains `failed to exit gracefully` 9 times, in nine projects: cleansia-customer-orders, cleansia-partner-invoices, cleansia-partner-orders, customer-stores, employee-management, invoice-management, membership-plan-management, order-management and pay-config-management. #322's run had 1.
  - One run each. The production builds were also 30–40% slower on #325's runner, so the count is not attributed to the conversion. Bounding the workers stays T-0813's runtime packet.
