---
id: T-0281
title: E2E sibling smokes — partner accept-job + admin login-and-land (reuse the T-0271 harness)
status: ready
size: M
owner: —
created: 2026-06-22
updated: 2026-06-22
depends_on: [T-0271]
blocks: []
stories: []
adrs: []
layers: [frontend, backend]
security_touching: false
manual_steps: []
sprint: 9
---

> **No-decision note (panel skipped):** Phase-0 characterization smokes pinning *existing* partner/admin
> journeys, reusing the T-0271 Playwright + seed/boot CI foundation. No new product behavior, no new
> framework, no architectural decision — the apps, routes, and endpoints all already exist.

## Context

Owner P3. T-0271 ships the **customer** booking → checkout smoke and explicitly defers partner/admin
flows as "explicit follow-ups". This ticket files those siblings. T-0271 builds the reusable
foundation — the CI job that boots Postgres + seeds it + serves an app + runs Playwright, and the
deterministic seed mechanism. **These two smokes reuse that foundation**; they are thin specs against
the **already-scaffolded** `cleansia-partner.app-e2e` and `cleansia-admin.app-e2e` projects (each has
its own `playwright.config.ts`, currently holding only `example.spec.ts`).

Two thin flows (kept deliberately small per the owner's "keep each thin"):
- **Partner accept-job:** partner logs in → lands on available jobs → accepts a seeded available job →
  the job moves to the partner's accepted/confirmed list (asserted on rendered UI).
- **Admin login-and-land:** admin logs in → lands on the admin dashboard/home → a key oversight surface
  renders (e.g. the orders or dashboard landing renders its first data row / heading). A minimal
  "the app boots and the authed landing renders" smoke.

## Acceptance criteria

- [ ] **AC1 — Partner accept-job smoke (rendered).** Given the partner app served against the seeded
  backend, When a seeded partner logs in and accepts a seeded available job, Then the job transitions to
  the partner's accepted/confirmed view — asserted on **rendered UI** (the job leaves "available" and
  appears in "accepted", or the status badge changes), not a network shortcut. The smoke does not drive
  the full order lifecycle — accept only.
- [ ] **AC2 — Admin login-and-land smoke (rendered).** Given the admin app served against the seeded
  backend, When a seeded admin logs in, Then the admin lands on the dashboard/home and a key oversight
  surface **renders with seeded data** (a visible heading + at least one data row / the dashboard
  KPIs render). A blank/erroring landing fails this AC.
- [ ] **AC3 — Reuses the T-0271 foundation.** Both specs run via the **same CI seed/boot pattern**
  T-0271 established (boot Postgres → seed → serve app → run Playwright), extended to also boot the
  partner and admin apps + their APIs. **No new seed mechanism and no new framework** — the seed is the
  same deterministic path T-0271 chose (extended with the partner/admin/job rows these flows need); if
  the existing seed lacks a partner user, an available job, or an admin user, the **test-only** seed is
  extended (not production seed data).
- [ ] **AC4 — Deterministic, not flaky.** Each smoke is stable across ≥3 consecutive runs; no arbitrary
  `waitForTimeout` — waits are web-first assertions / `waitForResponse`. Seeded deterministically
  (fixed rows); re-runs don't collide (disposable seeded DB or self-cleanup).
- [ ] **AC5 — Two specs, scaffolds replaced.** `cleansia-partner.app-e2e/src/` and
  `cleansia-admin.app-e2e/src/` each gets **one** real smoke spec that **replaces/supersedes** its
  `example.spec.ts`; the CI job runs the real smokes (not the "Welcome" examples), and they are wired so
  PR runs actually execute them (no silent Nx-affected skip).
- [ ] **AC6 — Behavior discipline + seam honesty.** Characterization, not change — if a flow can't reach
  its end because the **app** is broken, that's a **bug finding** (file a ticket), not a reason to patch
  product code here. The seed/auth seam used is recorded (spec header + Review) per the T-0271 precedent.
  `manual_steps: []` unless a real owner-only step surfaces (e.g. a seeded credential needing a CI secret)
  — then **stop and flag it**.

## Out of scope
- **NOT a full regression suite.** One thin happy-path smoke per app. No job completion/photos/payout,
  no admin mutation flows (refund/dispute/chargeback), no negative paths, no i18n matrix.
- **NOT the customer flow** — that is T-0271 (kept as the customer smoke).
- **NOT a new harness/framework/seed mechanism** — reuse T-0271's. If T-0271's seed/boot job needs
  refactoring to be reusable for three apps, that refactor rides T-0271 or a small follow-up, not a fork.
- **NOT owner-only steps** — no EF migration / nswag regen. Seed via the existing/test-only path.

## Implementation notes

**Depends on T-0271 landing** (its CI Playwright job + deterministic seed/boot are the foundation these
reuse). Once T-0271 is `done`: `[backend]` extends the seed/boot to cover partner+admin apps + the
partner/job/admin seed rows; `[frontend]` writes the two specs against the existing
`cleansia-partner.app-e2e` / `cleansia-admin.app-e2e` configs. Lock the seed/boot extension first, then
write the specs. The two specs are **independent** → after the shared seed/boot is ready, they
parallelize (one dev+reviewer each) or run serially (small enough for one dev).

**Routing:** `[frontend]` (specs) + `[backend]` (seed/boot extension). `reviewer`-per-dev. `qa` confirms
AC↔evidence + the ≥3-run determinism (AC4). No `security` (`security_touching: false` — exercises
existing surfaces, adds no endpoint/authz/DTO). `optimizer` N/A.

## Status log
- 2026-06-22 — draft → ready (created by pm). Owner P3 expansion: keep T-0271 as the customer smoke; add
  partner accept-job + admin login-and-land as siblings. Structured as **one sibling ticket carrying both
  thin smokes** (each app's scaffold replaced) — both reuse T-0271's seed/boot CI foundation, hence
  **`depends_on: [T-0271]`**. Verified: `cleansia-partner.app-e2e` + `cleansia-admin.app-e2e` exist as
  scaffold-only projects (per T-0271's harness facts). No-decision (Phase-0 characterization). Sized **M**
  (2 specs + seed/boot extension to 3 apps; if the multi-app boot/seed grows past M at dispatch, stop and
  split the partner and admin smokes into separate tickets). `manual_steps: []`.

## Review
<!-- reviewer / qa write verdicts here; PM reconciles before advancing state -->
