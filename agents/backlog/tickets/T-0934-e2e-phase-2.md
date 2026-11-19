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

- 2026-11-18 — Day 1 on DEV, which ran `5df227390` (T-0936, deployed on 17 November): the suite's book, settle, people,
  orders, execute and work stages on the main run 202611180700, and the iOS (202611180640) and Android (202611180645)
  runbooks. The Android build carried the Stripe test key, so the card rows ran on both phones. Twelve test fixes were
  committed between invocations.
- 2026-11-19 — Day 2: DEV's 02:00 UTC close of 16 to 18 November was read, then the payroll stage ran, in which the
  run's Accountant pressed Close now once in the admin web (F-012). PAYOUT-057 failed on a product defect (F-075), and
  TAKE-025, the run's last step, stopped at the DEV-only publish SQL before it wrote anything (F-076). The run is
  triaged in `e2e/runs/2026-11-18/findings.md` (F-075 to F-108) and the README's uncovered list rewritten; a review
  moved F-082 and F-088 off *by design* and recounted the coverage, and a second read-only check against the run's
  evidence corrected the register's citations without changing a verdict.
  Of the 146 P1 cases, 133 passed in phase 2 and 9 stand on phase 1; PAYOUT-057 failed; TAKE-025, BOOK-037's web half
  and AUTH-049's iOS lock were not run or only partly shown.
- 2026-11-19 — Shipped in #387 (merge `c24378926`), CI green on every check, with 18 test fixes and the catalogue fixes
  for F-088 and F-105. Mike has the findings report (https://claude.ai/artifact/MXwcyCRR12iNDUjjD122p7). Open for him:
  F-075 and F-093 (P2), F-076 (TAKE-025's re-run needs the publish SQL fixed, a new date and his OK), F-082, F-088,
  F-089 and F-092 (his choices), F-081 and F-095 (P3), and the test follow-ups F-087, F-090, F-102 and F-103. A finding
  becomes a ticket only when he picks it.
