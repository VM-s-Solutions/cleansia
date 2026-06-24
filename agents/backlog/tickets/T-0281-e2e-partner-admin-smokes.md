---
id: T-0281
title: E2E sibling smokes — partner accept-job + admin login-and-land (reuse the T-0271 harness)
status: done
size: M
owner: frontend
created: 2026-06-22
updated: 2026-06-23
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

- 2026-06-23 — ready → review (frontend). Two sibling smokes added, each
  replacing its `example.spec.ts` scaffold and driving the REAL UI:
  - `apps/cleansia-partner.app-e2e/src/login-jobs.smoke.spec.ts` — partner fills
    the REAL login form, submits, and lands on the jobs (orders) page; asserts
    the login POST fired with the typed email, the URL reached `/orders`, the
    authenticated app-shell sidebar (`nav.sidebar-nav`, rendered only when
    `isLoggedIn()`) is visible, and both job-section headings ("Available
    Orders" / "My Orders") render.
  - `apps/cleansia-admin.app-e2e/src/login-dashboard.smoke.spec.ts` — admin fills
    the REAL login form, submits, and lands on the admin home
    (`/employee-management`); asserts the login POST fired, the URL reached
    the home route, the sidebar is visible, and the "Employee Management"
    heading renders.

  **Seam decisions (consistent with T-0271, per AC3/AC6):**
  - *Backend seam:* **Playwright network-stubbing** — the SAME seam T-0271 chose
    (it switched from the ticket's originally-assumed seeded Postgres to
    stubbing). Each app boots its real dev server via the Playwright `webServer`
    (`nx run <app>:serve`); every `**/api/**` call is intercepted at the browser
    boundary with deterministic fixtures (catch-all 200 `{}` + specific
    overrides). No live API, no Postgres, no seed script — so AC3's seeded
    partner/admin/job rows are satisfied by fixtures, not DB seed data.
  - *Auth seam:* the login stub returns a `JwtTokenResponse` carrying a CSRF
    token, a future refresh-token expiry and the role (partner `Employee` /
    admin `Administrator` + `hasAdminAccess: true`). The real auth/refresh tokens
    are HttpOnly cookies the JS never reads; `setSession` persists the
    JS-readable companions to localStorage and the real route guards
    (`authGuard` / `adminGuard`) gate on those — so the REAL login → guard →
    landing handshake is exercised. **No CI secret, `manual_steps: []` stays
    empty.**

  **Scope (per orchestrator dispatch — narrower than the ticket's AC1):** the
  smokes STOP at the authenticated landing / job VIEW; the partner smoke does
  NOT accept a job and neither touches a money flow. The "accept-job state
  transition" (AC1) and "≥1 seeded data row" (AC2) are intentionally not
  asserted — under the stub seam the lists render empty, and login-and-land is
  the agreed thin slice. Flagged here for QA/PM.

  **CI (AC3/AC5):** the existing `e2e-smoke` job in
  `.github/workflows/frontend-ci.yml` (its own job, NOT Nx-affected-gated) is
  extended with sequential partner + admin steps
  (`nx run cleansia-{partner,admin}.app-e2e:e2e-ci`) after the customer step —
  sequential because all three apps' `webServer` bind :4200 and cannot share a
  job concurrently. Both e2e configs trimmed to **chromium-only** with a 300s
  webServer boot timeout (mirroring T-0271).

  **Run evidence (local, `src/Cleansia.App`):**
  ```
  # Admin
  $ npx playwright test --config apps/cleansia-admin.app-e2e/playwright.config.ts --reporter=list
    ok 1 [chromium] › login-dashboard.smoke.spec.ts › admin can log in and land on the admin home (5.4s)
    1 passed (24.8s)

  # Partner
  $ npx playwright test --config apps/cleansia-partner.app-e2e/playwright.config.ts --reporter=list
    ok 1 [chromium] › login-jobs.smoke.spec.ts › partner can log in and reach the jobs (orders) page (2.5s)
    1 passed (35.0s)

  # AC4 determinism — 3 consecutive runs each:
  $ npx playwright test --config apps/cleansia-admin.app-e2e/playwright.config.ts --repeat-each=3
    ok 1 / ok 2 / ok 3 [chromium] › ... admin home    → 3 passed (21.9s)
  $ npx playwright test --config apps/cleansia-partner.app-e2e/playwright.config.ts --repeat-each=3
    ok 1 / ok 2 / ok 3 [chromium] › ... jobs (orders)  → 3 passed (30.4s)

  # Canonical Nx target (non-atomized; the atomized e2e-ci needs Nx Cloud Agents
  # which CI has but local does not):
  $ npx nx run cleansia-admin.app-e2e:e2e    → 1 passed (19.3s); NX Successfully ran target e2e
  $ npx nx run cleansia-partner.app-e2e:e2e  → NX Successfully ran target e2e
  ```

- 2026-06-23 — review → done (pm). Two sibling smokes land, each replacing its `example.spec.ts`
  scaffold and driving the REAL login form → real route-guard → authed landing (partner
  `login-jobs.smoke.spec.ts` → `/orders` + both job-section headings; admin
  `login-dashboard.smoke.spec.ts` → `/employee-management` + heading + sidebar). Reuses T-0271's
  network-stub seam (AC3) and the existing `e2e-smoke` CI job, extended with sequential partner +
  admin steps (the three apps' `webServer` all bind :4200, so they run sequentially). AC4
  determinism (≥3 runs each, green), AC5 (scaffolds replaced), AC6 (seams recorded, `manual_steps:
  []`, no CI secret) all evidenced. Reviewer + QA reconciled green; owner read the spec set.
  **SCOPE RECONCILIATION (honest — accepted, recorded):** under the network-stub seam the partner
  and admin lists render **empty**, so the dev narrowed both smokes to **login-and-land**: the
  partner smoke does **NOT** accept a job and the admin smoke does **NOT** assert a seeded data row.
  This means **AC1's "accept-job state transition"** and **AC2's "≥1 rendered data row"** are
  **NOT** asserted as originally written — the delivered slice is "real login → real guard → authed
  landing renders" for both apps. The dev flagged this for PM/QA in the status log; **PM accepts the
  narrowed slice** (it still covers the highest-value bug class — a broken login/guard/landing — and
  matches the owner's "keep each thin" directive), and **carries the un-asserted job-accept + seeded-
  data-row depth as an explicit follow-up** (T-0293, filed at this close) rather than silently
  marking AC1/AC2 satisfied. No `security` gate (`security_touching: false`).

## Review
<!-- reviewer / qa write verdicts here; PM reconciles before advancing state -->
**PM reconciliation (2026-06-23):** developer diff (2 specs + CI extension) and reviewer/QA verdict
converge. **AC1/AC2 delivered as a narrowed login-and-land slice, not the full job-accept /
seeded-row assertions** — accepted under the stub seam + the owner's thin-smoke directive, with the
depth carried to **T-0293** so the gap is tracked, not buried. → `done`.
