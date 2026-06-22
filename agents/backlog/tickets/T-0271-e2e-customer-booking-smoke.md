---
id: T-0271
title: Phase-0 E2E smoke — customer booking → checkout-intent critical path (real browser, seeded CI)
status: ready
size: M
owner: —
created: 2026-06-21
updated: 2026-06-21
depends_on: []
blocks: []
stories: []
adrs: []
layers: [frontend, backend]
security_touching: false
manual_steps: []
sprint: 8
---

> **No-decision note (panel skipped):** this is a Phase-0 **foundational quality / characterization**
> ticket — it pins *existing* customer-journey behavior with a thin browser smoke. It introduces **no
> new product behavior and no architectural decision** (the e2e harness, the customer app, the order
> wizard, and the checkout-intent endpoint all already exist). Per `agents/process/deliberation.md` it
> carries this one-line no-decision note and does **not** convene an analyst/architect panel. The two
> implementation seams it leaves open (Stripe-test-mode-vs-stub; seed mechanism) are **engineering**
> choices delegated to the implementer + reviewer, not story/ADR-level decisions.

## Context

A workflow retrospective surfaced the riskiest **uncovered** area for a pre-PROD payments app:
Cleansia has unit (`Cleansia.Tests`), integration (`Cleansia.IntegrationTests`), and host-runtime
(`Cleansia.HostTests`) coverage that verifies the API seams — but **nothing verifies the actual
customer journey end-to-end in a rendered browser**. A whole class of bug (a dead CTA, a broken
route, a wizard step that silently won't advance, an SSR/bootstrap failure) is invisible to every
API-level test and is only caught by a rendered-route / E2E check.

The Nx **Playwright e2e harness already exists** — 3 projects
(`apps/cleansia.app-e2e`, `apps/cleansia-partner.app-e2e`, `apps/cleansia-admin.app-e2e`), each with
its own `playwright.config.ts` (chromium/firefox/webkit, `webServer` boots `nx run <app>:serve`).
But **each contains only the scaffold `src/example.spec.ts`** (`has title` → asserts an `<h1>`
"Welcome") — **zero real flow coverage**. Worse, `src/Cleansia.App/package.json` wires
`"test": "nx e2e cleansia.app-e2e"`, so the headline `npm test` currently runs nothing but the
scaffold. Frontend CI (`.github/workflows/frontend-ci.yml`) runs lint + Jest + the 3 prod builds —
it does **not** boot a backend or Postgres, and does **not** run Playwright at all.

This ticket closes the no-E2E gap with the smallest meaningful slice: **one** happy-path browser
smoke of the **single most important revenue path** — customer lands on the customer app → starts a
booking (the order wizard) → advances through its steps → reaches the payment/checkout handoff and a
checkout-intent is created. This is the **"decide the E2E layer early"** lesson: a *thin* smoke now,
expandable later. Because the harness already exists, the cost is the **spec + the CI seed/boot
wiring**, not a new framework.

This is **Phase-0 / foundational, behavior-focused, characterization-style**: it pins what the
shipped customer flow already does — it must not change product behavior, and a failure means the
flow regressed, not that the test is wrong.

## Acceptance criteria

- [ ] **AC1 — Wizard advances (the core of the smoke).** Given the customer app is served against a
  seeded backend, When the spec lands on the customer app and starts a booking, Then it drives the
  **order wizard through its steps** (selecting at least one seeded service/package and the required
  inputs to satisfy each step's validation) and the wizard **actually advances** step-to-step up to
  the payment/checkout step — asserted on **rendered UI** (visible step/heading transitions or the
  step indicator), not on a network shortcut. A wizard step that fails to advance fails this AC.
- [ ] **AC2 — Checkout-intent is created.** Given the wizard reaches the payment/checkout step, When
  the customer initiates checkout, Then the **order / checkout intent is created** at the
  customer-app↔backend seam (the create-order / checkout-session request returns success and the UI
  reflects the handoff). The smoke **does not complete a real card charge** — it drives **up to the
  Stripe handoff** and stops there (see Implementation notes for the seam choice).
- [ ] **AC3 — Runs green in CI against seeded data.** A CI job boots the **customer app + a seeded
  backend (Postgres)** and runs this Playwright smoke; the job is **green on a clean run**. Seeded
  reference data (at minimum: services, packages, currency, and any catalog/serviced-area row the
  wizard requires to reach checkout) is present before the spec runs (mechanism per Implementation
  notes). The job is added as a **new CI job or an extension of `frontend-ci.yml`**, and is wired so
  it actually executes on PRs (Nx-affected gating must not silently skip it).
- [ ] **AC4 — Deterministic, not flaky.** The smoke is **stable across repeated runs** (evidence:
  the same spec passes on ≥3 consecutive CI/local runs). No arbitrary `waitForTimeout` sleeps —
  waits are on rendered state / network responses (web-first assertions / `expect.poll` /
  `waitForResponse`). Test data is seeded deterministically (fixed seed rows, not random), and the
  spec cleans up after itself or runs against a disposable seeded DB so re-runs don't collide.
- [ ] **AC5 — One spec, scaffold replaced.** The smoke is **one** spec file in
  `apps/cleansia.app-e2e/src/` that **replaces or supersedes** the scaffold `example.spec.ts`
  (`has title`). `npm test` (`nx e2e cleansia.app-e2e`) — and/or the new CI job — runs the **real
  smoke**, not the "Welcome" example. The spec is fast and reads as a single linear happy-path
  journey.
- [ ] **AC6 — Honest seam documentation.** The chosen Stripe seam (test-mode vs stubbed checkout)
  and the chosen seed mechanism are **recorded in the spec/CI** (a short header comment + the
  ticket's Review section) so the next person extending the suite knows the contract. If the
  implementer concludes a real owner-only step is required (e.g. a Stripe **test-mode** API key as a
  CI secret), they **stop and flag it to the PM** as a `manual_steps` entry rather than inventing
  one — this ticket ships `manual_steps: []` and must stay that way unless that flag is raised.

## Out of scope

- **NOT a full regression / exhaustive e2e suite.** One happy-path smoke only. No cancellation, no
  refund, no membership, no recurring-booking, no multi-service permutations, no negative/validation
  paths, no i18n-per-locale matrix.
- **NOT a real card charge.** The smoke stops at the Stripe handoff / checkout-intent creation; it
  never drives a real Stripe charge to completion. (Webhook → `Confirmed` is already covered by the
  Wave-4 webhook integration/host tests — `T-0210`.)
- **NOT partner or admin flows.** `cleansia-partner.app-e2e` and `cleansia-admin.app-e2e` keep their
  scaffolds for now — partner job-management and admin-oversight smokes are **explicit follow-ups**
  (file them when this lands and proves the pattern).
- **NOT a new test framework.** Reuse the **existing** Nx + Playwright harness and the existing
  `playwright.config.ts`. No Cypress, no new runner.
- **NOT the owner-only manual steps.** This ticket does not run an EF migration or NSwag regen. The
  seed must use an **existing** path (see Implementation notes); if a backend DTO/endpoint or schema
  change turns out to be needed to make the flow seedable/testable, that is a **separate** ticket —
  stop and flag it, do not fold it in.

## Implementation notes

**Harness facts (already true — do not re-litigate):**
- Config: `src/Cleansia.App/apps/cleansia.app-e2e/playwright.config.ts` — `baseURL` from
  `BASE_URL` (default `http://localhost:4200`); `webServer.command = npx nx run cleansia.app:serve`,
  `reuseExistingServer: true`. The customer app is **SSR** (`cleansia.app`).
- Scaffold to replace: `src/Cleansia.App/apps/cleansia.app-e2e/src/example.spec.ts`.
- `npm test` (in `src/Cleansia.App/package.json`) = `nx e2e cleansia.app-e2e`.
- CI today: `.github/workflows/frontend-ci.yml` does **not** boot a backend/Postgres and does **not**
  run Playwright. `backend-ci.yml` already spins real Postgres via Testcontainers for the .NET suites
  — a useful reference for how the repo boots Postgres in CI.

**Seam #1 — Stripe handoff (implementer + reviewer decide, then record per AC6).** Options, pick the
simplest that satisfies AC2 deterministically:
- (a) **Drive to the handoff and stop** — assert the create-order / checkout-session request
  succeeds and the UI shows the redirect/handoff to Stripe, without following into Stripe's hosted
  page. Most deterministic; no external dependency. *(Default recommendation.)*
- (b) **Stripe test mode** — if the flow can reach a real Stripe **test-mode** session deterministically
  in CI. This likely needs a test-mode key as a **CI secret** → that is an **owner-only `manual_steps`
  flag**: stop and raise it to the PM (do not self-provision).
- (c) **Stub the checkout boundary** — intercept the checkout call at the network boundary
  (Playwright route interception) and assert the request shape. Acceptable if (a) can't observe the
  intent cleanly.

**Seam #2 — Seed mechanism (implementer + reviewer decide, then record per AC6).** The wizard needs
seeded reference data (services, packages, currency, and any serviced-area/catalog row required to
reach checkout). Prefer an **existing** path — do **not** invent a new owner-only step:
- Candidate: `sql-scripts/insert_seed_data.sql` (an existing seed script) applied to the CI Postgres
  before the app boots, **if** its contents cover the wizard's needs. Confirm coverage; if it's
  short a row the wizard requires, prefer a **test-only seed step** (a script/SQL the CI job runs)
  over editing production seed data.
- Alternative: a dedicated **test-seed** path (a small SQL/seed the e2e job runs against the
  disposable CI DB). Keep it deterministic (fixed ids/rows) so AC4 holds.
- Whatever is chosen, the seed runs **before** the customer app serves, against a **disposable**
  seeded Postgres, so the smoke is repeatable.

**CI wiring (AC3):** add a job that (1) boots Postgres, (2) seeds it, (3) boots the customer API +
the customer app (`nx run cleansia.app:serve` via Playwright's `webServer`, or a built artifact),
(4) runs `nx e2e cleansia.app-e2e`. Either a **new workflow/job** or an **added job in
`frontend-ci.yml`** — but it must boot a backend+DB (the existing `build` job does not), and it must
not be silently skipped by Nx-affected gating. Install Playwright browsers in CI
(`npx playwright install --with-deps chromium`); a single browser (chromium) is enough for a smoke.

**Behavior discipline:** characterization, not change. If the smoke can't reach checkout because the
flow itself is broken, that's a **finding** (file a bug ticket) — do not "fix" product code inside
this ticket to make the smoke pass.

**Routing:** `[frontend]` owns the spec + CI Playwright wiring; `[backend]` owns the seeded-boot /
seed path (and confirms the seed covers the wizard). Lock the seed/boot seam first (so the spec has a
deterministic stack to run against), then write the spec. **Reviewer in parallel** with the
developer; **QA** confirms AC↔evidence and the ≥3-run determinism (AC4). No `security` gate
(`security_touching: false` — it adds no endpoint/authz/DTO; it exercises existing surfaces).
`optimizer` not applicable (a single smoke spec is not a hot path).

## Status log
- 2026-06-21 14:50 — draft → ready (created by pm). Phase-0 foundational quality ticket. Dedup
  checked: no existing E2E flow ticket in INDEX or `audits/` (only `T-0126` references e2e
  tangentially); harness existence + scaffold-only state + `npm test` wiring + `frontend-ci.yml`
  no-backend-boot + `sql-scripts/insert_seed_data.sql` all verified in-repo. No-decision (panel
  skipped per note above). Sized **M** (one spec + CI seed/boot wiring against an existing harness;
  if the CI seed/boot work grows past M at dispatch, stop and split before it runs). `manual_steps:
  []` — implementer must raise the PM flag if a Stripe test-mode CI secret turns out to be required
  (AC6). Deferred to implementer: Stripe seam (recommend (a) drive-to-handoff) + seed mechanism
  (prefer existing `insert_seed_data.sql` or a test-only seed). No `depends_on` (all program waves
  0–7 closed).

## Review
<!-- reviewer / qa write verdicts here; PM reconciles before advancing state -->
