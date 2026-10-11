---
id: T-0809
title: Wave E C01–C02 — measure the local build/test loop, then adopt only local wins that clear the bar
size: M
owner: pm
created: 2026-10-11
updated: 2026-10-11
depends_on: [T-0808]
blocks: []
stories: []
adrs: []
layers: [frontend, backend, docs]
security_touching: false
---

## Context

Wave E of the 2026-10-07 audit roadmap. Mike authorized it on 2026-10-10, with recommended routine choices; the roadmap text is the authority: "profile and improve the local build/test loop using reproducible local evidence; distinguish dependency/download, restore, compile, test, and cache costs." Scope was checked against the read-only Wave E ground truth of 2026-10-11 (off-tree: `/Users/michael/.codex/scratchpads/cleansia-wave-e-2026-10-11/raw/ground-truth/`).

The ground truth confirmed the cost centres but found **no local measurement of any loop**:
- **Web.** Frontend CI's test step is bimodal. When `package.json`, the lockfile, shared Jest/TS/Nx config or shared models change, all 74 projects run (median 1,160 s). Otherwise it takes about 13 s.
- **Jest workers.** Each project is its own Jest process with no worker bound: 3 Nx tasks × (CPUs − 1) workers.
- **No CI cache.** CI persists no Nx, Jest or Angular cache.
- **iOS.** iOS CI's three serial schemes dominate its job.
- **Unverified hypothesis.** C-01's worker contention has not been tested.

## Doing

- A serial, ledgered measurement on a quiet machine. It runs Node 22.23.3 and npm 10.9.9, the same as CI, in a dedicated clean worktree at `620f3c7`, with every cache redirected to scratch.
- **Web cells:**
  - `npm ci` cold and warm;
  - all 74 test projects with a cold and a warm Jest transform cache;
  - a parallel × maxWorkers sweep on the warm cache;
  - Nx cache replay;
  - lint;
  - the three production builds with the Angular cache off (as CI) and warm.
- **Backend cells:**
  - restore cold and warm;
  - a full and a no-op Release build;
  - the unit suite in CI form and in the README form.
- n = 3 per cell; sweep configurations run round-robin. A run counts only if its validation passes, with exact project and test totals.
- Local wins become a docs-only change only if they clear the bar: ≥ 10 % median gain, non-overlapping ranges, identical counts.

## NOT doing

- **No CI-side change.** That covers Nx/Jest cache persistence, parallelism flags, the iOS SPM cache, parallel iOS scheme jobs and Android CI hygiene. They go to the C-06 owner decision packet (T-0813), with these local numbers attached.
- **No committed `maxWorkers` or `cacheDirectory` change.** It would change CI runtime, and an M4 sweep does not transfer to a 4-vCPU runner.
- No `isolatedModules` or type-check change.
- **Android and iOS local cells are not measured tonight.** Their levers are CI-side and owner-gated, and the ground truth already gives CI phase medians from the logs.
- The container suites (integration, host) are not measured locally. Docker on this Mac is not CI-shaped.

## Acceptance criteria

- [ ] **AC1** — A dated cost table per measured loop. It splits download, restore, compile, test and cache replay, and gives median / min / max and test-count identity, with the environment, max RSS and every command ledgered.
- [ ] **AC2** — Either a list of local wins that clear the bar, landed as docs only (0 Nx projects affected), or an explicit "none cleared the bar".
- [ ] **AC3** — CI-side options are recorded on T-0813 with the local evidence.

## Review

Pending the measurement.
