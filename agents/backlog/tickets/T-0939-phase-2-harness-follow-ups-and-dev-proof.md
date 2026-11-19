---
id: T-0939
title: "The E2E harness follow-ups from phase 2, and the phase-2 fixes proven on DEV"
size: M
owner: Michael Chaban
created: 2026-11-19
updated: 2026-11-19
depends_on: [T-0937, T-0938]
blocks: []
stories: []
adrs: []
layers: [qa, docs]
security_touching: false
---

## Context

Mike, 19 November 2026, on the phase-2 E2E report (T-0934, `e2e/runs/2026-11-18/findings.md`): "Fix all of them by
waves making the fixes that pays off in long term, not quick fixes". The findings are fixed in three waves, T-0937,
T-0938 and T-0939, one PR each. This is wave 3. His ruling (AskUserQuestion): TAKE-025 runs on DEV with the publish
of a new cleaner-document version, although every DEV cleaner then has to accept it again.

Every row was ground-truthed against master c24378926 before filing.

## Doing

1. F-087: `assist confirm` fails when the accounts were already confirmed by another dispatch; it accepts a confirmed
   account.
2. F-090: the runbook recipes that did not hold: a phone number in a CZ mobile range, the Android 15 locale set
   through Settings, iOS typing in Czech and Slovak by paste, and the simulator keychain reset.
3. F-102: no phase-2 test reads a night-close invoice's cash set-off; `payroll/10` asserts it.
4. F-103: shared harness helpers: `errorKeys` splits FluentValidation's joined keys, one paced admin sign-in, and
   navigation timeouts on the web sign-ins.
5. On DEV, after T-0937 and T-0938 are deployed: PAYOUT-057 in its six languages, TAKE-025 with the publish, the
   re-render of an invoice whose PDF exists, and the phone rows for F-089, F-092 and F-093.

## Not doing

- New P2 cases (phase 3); PRO.

## Done

Merged with CI green; the DEV run is recorded under `e2e/runs/`, and Mike has the result.

## Work record

- 2026-11-19 — Filed with T-0937 and T-0938, after Mike's rulings.
