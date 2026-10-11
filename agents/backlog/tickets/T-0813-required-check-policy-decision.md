---
id: T-0813
title: Wave E C06 — required-check policy and CI runtime: a decision for the owner
size: S
owner: pm
created: 2026-10-11
updated: 2026-10-11
depends_on: [T-0808]
blocks: []
stories: []
adrs: []
layers: [docs]
security_touching: true
---

## Context

The roadmap: "only if evidence shows it is necessary, present a concrete decision on required-check policy and CI billing/runtime trade-offs before changing any required status, workflow gate, or budget-sensitive configuration." Checked against the read-only Wave E ground truth of 2026-10-11 (off-tree: `/Users/michael/.codex/scratchpads/cleansia-wave-e-2026-10-11/raw/ground-truth/`). **Nothing is changed by this ticket.** It is Mike's decision.

**What is enforced.** Ruleset 6739260 requires one status context, `build`, strict, with no source app pinned. Four jobs emit that name:
- Backend, path-filtered;
- Frontend, every PR;
- Android, every PR;
- iOS, path-filtered.

Docs CI, Secret scan and Frontend e2e-smoke run on every PR but are **not** required. iOS Symbols and booking-policy parity are path-filtered and not required. CLAUDE.md says seven workflows gate a PR and README says six; only `build` is enforced.

**Observed 2026-10-11:**
- A **running** `build` holds the gate. PR #320 stayed `blocked` until its last `build` finished, then flipped to `clean` within 45 s.
- **Unobserved:** whether a **failed** `build` blocks when another `build` is green. PR #319's first head was in exactly that state (Android red, three green), and nobody merged it.
- The team "VM" has `always` bypass on both rulesets. It was used once in the month (#295, merged with no `build` reported), and the agents' credential can bypass too. Tonight's merges went through the PR merge API with a SHA guard on a `clean` state.
- The repo is public, so hosted runners cost $0 today. Median iOS CI run: 21.2 min.

## Options (from the ground truth)

| | Change | Gate effect | Runtime / billing |
|---|---|---|---|
| A | Keep `build`, document what it means | none; membership can drift silently if a job gains `name:` | none |
| B | Observe the failed-build case passively on the next mixed head | settles A vs E | none |
| C | Pin `build` to the GitHub Actions app (id 15368) | stops a manually posted `build` status from counting | none |
| D | Also require Docs CI, Secret scan (and optionally e2e-smoke) | today's advisory red becomes blocking | none; they already run |
| E | Distinct job names + an always-reporting change detector | every workflow independently required; GitHub's recommended shape | small Linux job per PR |
| F | Run Backend and iOS on every PR; require distinct names | simplest unambiguous | about 33 % more macOS jobs (+~20 min wait on docs-only PRs); $0 while public |
| G | One aggregate required job | single gate; needs `if: always()` | consolidation, or a polling runner |
| H | Bypass policy: keep `always`, move to `pull_request`, or remove | separate from the check set | none |

**Ground truth's suggested default, for Mike to approve or reject:**
1. A now, with B to settle the failed-build case.
2. Then C + D, a tightening at zero runtime cost.
3. E only if B shows a red `build` can be masked, or if path-filtered gates must become required.

**Local evidence for the CI-side options (T-0809, 2026-10-11).** Measured on an M4, 10 cores, Node 22.23.3, all 74 test projects, n=3:

| Configuration | Empty Jest cache | Warm Jest cache |
|---|---:|---:|
| default workers (CI today) | 829 s | 242 s |
| `--maxWorkers=1` | 304 s | 96 s |

Under default workers the worker-exit warning appears only cold. Hosted runners have 4 vCPUs, so the size of the gain will differ there; a hosted A/B (n ≥ 5) on a PR that affects all projects should come before either change.

Two related CI decisions from T-0809 and T-0812 belong in the same packet:
- the Nx/Jest cache and parallelism in Frontend CI, and the iOS SPM cache and parallel schemes;
- whether to pin `ubuntu-24.04` before GitHub's Ubuntu 26 migration (from 2026-10-19), or to validate 26 first.

## Acceptance criteria

- [ ] **AC1** — Mike chooses. Until then nothing in the rulesets or workflows changes.
