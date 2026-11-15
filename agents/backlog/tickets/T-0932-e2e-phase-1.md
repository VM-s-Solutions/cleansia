---
id: T-0932
title: "End-to-end tests, phase 1: the case catalogue, the DEV suite and one triaged run"
size: L
owner: Michael Chaban
created: 2026-11-13
updated: 2026-11-15
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
- 2026-11-13 — 1a, the case catalogue, run with the lean workflow (wf_dfb22ebd-0ac): 578 cases over the ten flows of `docs/flows/`, 146 of them P1. The review's findings were fixed, except a tooling note that `check-docs-refs` slugs headings differently from VitePress.
- 2026-11-14 — 1b, the harness (wf_ff3ff32a-bd5): the Playwright project `cleansia-platform-e2e` with the stages book, execute and settle; the DEV-only `sql-scripts/dev-e2e-confirm-accounts.sql` with its refusal row in `execute-sql.yml`; and the iOS and Android runbooks. Its first DEV runs passed the book stage 22 of 22, the API execute and the settle cancellations and refunds.
- 2026-11-15 — 1c, the first full DEV run (wf_26a0686a-459): book 43 of 45 (the other two were catalogue errors, fixed and passed on a re-run), execute 15 of 15, settle 12 of 12 with one Close now. The Android runbook ran the full cleaner journey; the iOS runbook ran up to start, since the simulator camera takes no photo. The register `e2e/runs/2026-11-15/findings.md` holds 74 findings: 25 defects (no P1, one P2: F-035; 21 of them docs faults), 13 by design, 18 test bugs, 16 environment and 2 catalogue errors. P1 coverage of 146: 73 passed, 2 failed as catalogued, 12 not run, 59 not covered yet. The review's 13 items were fixed on the branch.
- 2026-11-15 — Shipped: #382 merged to master as 5457be354. The `dev/e2e-phase1` ref the confirm SQL ran from is deleted.
- 2026-11-15 — Mike has the findings report (https://claude.ai/artifact/CrQiEDXoL9hfxAbL4X3tZP). A finding becomes a ticket only when he picks it.
- 2026-11-15 — F-034 corrected in the register: ORD-EA8E7B19 was cancelled through the admin API on 2026-11-14, after the 1b review, before the register was written.
