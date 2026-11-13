---
id: T-0932
title: "End-to-end tests, phase 1: the case catalogue, the DEV suite and one triaged run"
size: L
owner: Michael Chaban
created: 2026-11-13
updated: 2026-11-13
depends_on: []
blocks: []
stories: []
adrs: []
layers: [qa, frontend, ios, android, docs]
security_touching: false
---

## Context

Mike, 13 November 2026: test the whole platform end to end (registration, booking, taking the job and on), with
agents in different roles. His rulings: on DEV; through the five APIs, the three web apps and the four mobile apps;
the cases and the suite committed to the repo; the core journeys first.

## Doing

1. A case catalogue for all ten flows of `docs/flows/`, in `e2e/cases/`, each case citing the rule it checks.
2. A Playwright suite for the core journeys through the APIs and the three web apps, runnable against DEV with one
   command.
3. Mobile runbooks for the same journeys on the four apps: AXe on an iOS simulator, adb on an Android emulator.
4. One full run on DEV by role agents (QA lead; customer, guest, cleaner and admin actors; a triager; a reporter),
   triaged into a findings register, and a report for Mike.

## Not doing

- Fixing any finding: each is reported, and Mike chooses.
- PRO. Running the DEV suite in PR CI.
- Automated XCUITest or Compose UI suites: mobile is agent-driven in this phase.
- Executable tests for memberships, GDPR, disputes, loyalty and security sweeps: catalogued only.
- Apple and Google sign-in, real e-mail delivery, load testing.

## Done

The catalogue, the suite and the runbooks are merged; one full DEV run is triaged into `e2e/runs/`; Mike has the
findings report.

## Work record

- 2026-11-13 — Filed and started on `feature/e2e-phase1`.
