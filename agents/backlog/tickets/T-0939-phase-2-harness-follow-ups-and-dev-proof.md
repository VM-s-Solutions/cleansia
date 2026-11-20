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
- 2026-11-19 — Started on `fix/t0939-wave3-harness-and-dev-proof`, after T-0938 merged as `d4bc9f045`.
- 2026-11-19 — Part 1 shipped in #390 (merge `607d6b091`): F-087's confirm is proven by a sign-in, the runbook
  recipes of F-090, F-102's set-off assertion, F-103's shared helpers, the F-081 and F-082 assertions, TAKE-025
  behind `E2E_TAKE025=1`, and e2e/README.md's fix-proof run.
- 2026-11-20 — Part 2, the fix-proof run on DEV (master `d4bc9f045` deployed; runs R3 202611200700, I 202611200640, D
  202611200645; one Close now, at 09:03:18Z). Proven on DEV: F-075 (PAYOUT-057's six renders, each answered 200, the
  set-off statement in each language), F-076 (TAKE-025 with the publish as the last step), F-087's confirm by
  sign-in, F-089, F-092's push half, F-093, F-095 and F-102 (set-off 300 = min(360 held, 300 total), transfer 0).
  F-081 and F-082 are proven by tests only. F-088 failed on DEV: both PaymentSheets still offer Stripe Link (F-109,
  P2), so it moves to T-0940. The register is e2e/runs/2026-11-20/findings.md (F-109 to F-117).
- 2026-11-20 — Shipped in #391 (merge `0975badeb`), CI green on every check. Open for Mike: F-111 (the Czech payout
  invoice prints the country in English, P3), F-113 (the Android password eye icon reads English, P3), F-117 (two
  docs sentences on simulator pushes, P3), F-110 (the runner's and the APIs' reach to the DEV database keeps
  stalling), F-092's foreground half and F-087's confirmed-first half (not kept or not reached), and F-081 and F-082
  on DEV (they need a phase-2 run).
