---
id: T-0934
title: "End-to-end tests, phase 2: every P1 case DEV can run, and one triaged run"
size: L
owner: Michael Chaban
created: 2026-11-15
updated: 2026-11-15
depends_on: [T-0933]
blocks: []
stories: []
adrs: []
layers: [qa, frontend, ios, android, docs]
security_touching: false
---

## Context

Mike, 15 November 2026: after the T-0933 fixes, run phase 2 of the tests, over all the P1 cases phase 1 left open.

## Doing

1. Tests for the 71 P1 cases still open after T-0932: the 59 with no test (`e2e/README.md`, *P1 cases the suite
   does not cover yet*) and the 12 that were not run or only partly shown.
2. The admin web's Close now (F-012), the partner web's customer fields (F-027) and the iOS order detail after a
   take (F-038).
3. One run on DEV over about two days (the fee tiers and one 02:00 UTC night close), triaged into `e2e/runs/`, and a
   report for Mike.

## Not doing

- The 432 P2 cases (phase 3).
- Automated XCUITest or Compose UI suites.
- Fixing any finding: each is reported, and Mike chooses.
- PRO.

## Done

Every P1 case DEV can run has a test or a runbook step; one run is triaged into `e2e/runs/`; Mike has the report;
the PR is merged.

## Work record

- 2026-11-15 — Filed; starts when T-0933 is merged and deployed to DEV.
- 2026-11-15 — Mike's rulings on the plan: Day 1 of the run is Wednesday 18 November and Day 2 Thursday 19 November, after the 02:00 UTC night close of the period 16 to 18 November, with DEV quiet from 18 November 07:00 to 19 November 08:00 UTC; he adds the Stripe test publishable key to the Android build himself; TAKE-025 runs, on a cleaner-document version published on DEV by a DEV-only SQL script as the run's last step (every DEV cleaner then re-accepts); PAY-042 is read by a read-only DEV-only SQL script. Build started on `test/e2e-phase2`.

