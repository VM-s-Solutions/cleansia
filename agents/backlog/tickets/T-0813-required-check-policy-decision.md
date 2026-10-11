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

## Decision — 2026-10-11

Mike: "Go with A+B". `build` stays the one required check and its meaning is written down (A, `docs/deployment/ci-cd.md#required-checks`); the failed-build case is settled by passive observation (B, below). No ruleset, required check or workflow changes. The runtime half of this packet (Frontend CI's Jest workers and caches, iOS SPM caching and parallel schemes, pinning `ubuntu-24.04`) is not part of this ruling and stays as it is.

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

## NOT doing

- **The *gate* sentences in `agents/process/enforcement.md` and `agents/knowledge/conventions.md`.** Both top banners say four checkers gate a pull request: enforcement.md in its checker table (:10-17, "yes, blocking"), conventions.md in its banner (:3-20). There *gate* and *blocking* mean that the checker fails its own CI job (`T1-CI`); none of those jobs is required to merge, only `build` is. Reported for Mike, not changed. If he wants it said, one sentence after the enforcement.md table would do, outside the table, because the conventions.md banner retires when that table changes.

## Acceptance criteria

- [x] **AC1** — Mike chose A+B, 2026-10-11.
- [ ] **AC2** — A: `ci-cd.md#required-checks` says what `build` covers (the jobs that report it, a running one holding the merge, the failed case unknown, the advisory checks, how the set drifts). README and CLAUDE.md say `build` is the only required check. A's commit changes nothing under `.github/` and no ruleset.
- [ ] **AC3** — B: the first qualifying head is read as below, and the verdict is recorded here and in `ci-cd.md#required-checks`.

### What A's write-up changed (lane, 2026-10-11)

The docs lane's edits for A, with the post-Wave E docs findings that ride on it: WCD-01 (all but its enforcement.md part, which is under NOT doing), WCD-02 and WCD-22. They are the lane's work, not part of Mike's ruling.
- `docs/deployment/ci-cd.md`: the intro, the new section, the workflow roster and its triggers, the DEV deploy's trigger and concurrency, and the runner notes;
- the README and CLAUDE.md sentences on what a PR needs to merge, and CLAUDE.md's docs-table counts.

## B — observing the failed-build case (read-only)

B is passive. Whichever lane watches a PR's CI samples a head only when its normal PR monitoring happens to observe one in the state below. It never delays pushing a fix to observe it: a head that moves on before its readings are complete is void. GET only. If no qualifying head is observed before Wave F closes, AC3 moves to a ticket of its own.

**Qualifying state.** An open, non-draft PR into `master`, opened from this repository by the owner's account (not Dependabot). Its current head has at least two `build` check runs, all completed. At least one is `failure`, and at least one `success` completed after every failure. Nothing was re-run (`run_attempt` 1).

**Never** open, push, re-run or update a branch to create this state. Never attempt a merge, call GraphQL, or use any method but GET.

**Reads.** Each goes through `python3 /Users/michael/.codex/scratchpads/cleansia-wave-c-2026-10-09/tools/gh-existing-credential.py --label <alnum-dash> --cwd /Users/michael/.codex/worktrees/wave-a-correctness/cleansia -- api --method GET <path>`, where `R` = `repos/VM-s-Solutions/cleansia`.

1. `R/commits/{sha}/check-runs?check_name=build&filter=all&per_page=100` — id, status, conclusion, started_at, completed_at, app.id, check_suite.id, details_url.
2. `R/actions/runs?head_sha={sha}&per_page=100` — workflow, run id, run_attempt, conclusion.
3. `R/pulls/{n}` — draft, mergeable, mergeable_state, head.sha, base.ref, user.login. While it reads `unknown`, read again every 10 s for up to a minute.
4. `R/git/ref/heads/master`, then `R/compare/{master_sha}...{sha}`. `behind_by` must be 0.
5. `R/pulls/{n}/reviews` and `R/pulls/{n}/comments` must both be empty, so no review thread exists.
6. `R/rulesets/6739260` — `updated_at` must still be `2025-08-15T15:40:28.396+02:00`, and the required checks must still be `[{context: build}]`, strict.
7. `R/commits/{sha}/status` — `total_count` must be 0, so there is no commit status named `build`.

**When.**
- When the failure appears, read (1) and (2).
- Then read (1) every 60 s until no `build` is queued or in progress.
- At 60 s, 120 s and 300 s after the last `build` finishes, read (3), (1) and the master ref.
- Read (4) to (7) once inside that window.
- Control: on the PR's next head whose `build` runs are all green, read (3), (4) and (5) once.

**Verdict.**
- Every reading is `blocked`, and the control is `clean` or `unstable`: a red `build` blocks even beside a later green one, and A stands as written.
- Any reading is `unstable` or `clean`: a later green `build` masks a red one. Change nothing, and report to Mike, because this is the case for option E.
- The reading is `behind`, `dirty`, draft or an `unknown` that does not settle; or there was a re-run, a push or any failed precondition: the occurrence is void, so wait for the next one.
- A head whose failed `build` finished last is not evidence either way.

**Record.**
- Save the raw readings as JSON under the active wave's scratchpad, `raw/ci/t0813-b-pr<n>-<sha12>.json`. Never use `/private/tmp`.
- Add a dated `### Observation` to this ticket: the PR, head, master, the `build` runs, each reading, the control and the verdict.
- Replace the not-yet-observed bullet in `ci-cd.md#required-checks` with the result.
- Tick AC3 and update the INDEX row.

**Not GraphQL.** `CheckRun.isRequired(pullRequestNumber:)` needs a POST to `/graphql`: GitHub's guide says the HTTP verb is POST, and only an introspection query is a GET. It reports for each run whether that run is required to pass before merging, not how a red and a green `build` combine. `PullRequest.mergeStateStatus` returns the same value as REST `mergeable_state`. Use it only if Mike allows a POST read, and never instead of the readings above.
