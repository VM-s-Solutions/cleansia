---
id: T-0466
title: Test infra — obsolete parameterless PostgreSqlBuilder (removal announced) and one xUnit2031 violation
status: draft
size: S
owner: backend
created: 2026-07-30
updated: 2026-07-30
depends_on: []
blocks: []
stories: []
adrs: []
layers: [backend]
security_touching: false
manual_steps: []
sprint: 14
---

## Context

Filed from **QA's T-0446 AC4 run** as a minor observation, recorded so it is not lost. Three
mechanical items:

| Site | Issue |
|---|---|
| `PostgresContainerFixture.cs:15` | obsolete **parameterless `PostgreSqlBuilder()`** — Testcontainers **4.10.0** announces it **will be removed** |
| `UserMembershipCancellationSweepIndexPlanTests.cs:29` | same |
| `RefreshTokenFlowTests.cs:327` | trips **`xUnit2031`** |

**Why it is worth a ticket rather than a note:** the first two are a **deprecation with an announced
removal**, so this is not cosmetic — it is a build that breaks on a future Testcontainers bump, in the
fixture that **every** integration and host test depends on. Cheap now, a blocked upgrade later.

**Why it is NOT urgent:** all three suites pass today — see the correction in `status/sprint-14.md`
§2.9 (IntegrationTests **108/108**, HostTests **75/75**, unit **2295/2295**, ~5m30s for all three,
run locally on `master`).

## Deliberation

**No-decision.** Mechanical API migration against a documented deprecation. No panel.

## Acceptance criteria

- [ ] **AC1** — Both `PostgreSqlBuilder()` call sites use the non-obsolete form. Evidence: the build
      emits **no** obsolescence warning for these two sites.
- [ ] **AC2** — `RefreshTokenFlowTests.cs:327` no longer trips `xUnit2031`. Fix the **cause** (the
      analyzer flags a real deadlock-risk pattern); **do not suppress the warning** — a `#pragma` here
      would convert a live finding into a hidden one, which is the class of defect this sprint has
      spent its time on.
- [ ] **AC3 (Gate 8)** — All three suites green afterwards **with real counts, run locally**:
      `Cleansia.Tests` (expect **2295**), `Cleansia.IntegrationTests` (expect **108**),
      `Cleansia.HostTests` (expect **75**). **These numbers are known** — a run that reports different
      totals means something else changed and must be investigated, not reported as pass.
- [ ] **AC4** — No behavioural change to any test. This is an API migration; **if a test's result
      changes, stop** — that is a finding, not a refactor.

## Out of scope

- Upgrading Testcontainers itself.
- Any other analyzer warning not listed above. If the build surfaces more, **count them and report the
  number in the status log** for the PM — do not sweep opportunistically.
- Adding new tests.

## Implementation notes

- `PostgresContainerFixture` backs **every** integration/host test — AC3 is the real gate here, not
  AC1. Run all three suites, not just the touched files.
- Docker is required. Per §2.9 it **works locally**; if it does not in your environment, say so
  explicitly rather than inheriting the stale "DEFERRED-TO-CI" caveat this sprint has now retired.

## Status log
- 2026-07-30 — draft (created by pm from QA's T-0446 AC4 run; no-decision, mechanical)
- 2026-07-30 — **`ready` on merit** (DoR met, no dependencies) but **unscheduled** — post-demo filler; it is a deprecation, not a defect.

## Review
<!-- reviewer verdict here -->
