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

- [ ] **AC1** — The flag lands. Locally, a run with it shows every project's Jest summary.
- [ ] **AC2** — The diagnosis and the open hypothesis are recorded here. The next PR that touches `src/Cleansia.App` will show whether hosted CI still prints the warning.

## Review

Pending.
