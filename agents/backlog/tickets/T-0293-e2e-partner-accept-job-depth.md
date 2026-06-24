---
id: T-0293
title: E2E depth — partner accept-job state transition + admin seeded-row assertion (the T-0281 narrowed slice)
status: done
size: S
owner: frontend
created: 2026-06-23
updated: 2026-06-23
depends_on: [T-0281]
blocks: []
stories: []
adrs: []
layers: [frontend, backend]
security_touching: false
manual_steps: []
sprint: 11
---

> **No-decision note (panel skipped):** Phase-0 characterization depth on the EXISTING partner/admin
> smokes — it asserts more of an already-shipped journey (a job-accept state transition + a seeded
> data row). No new product behavior, no new framework, no decision. Carries the depth T-0281
> intentionally narrowed.

## Context

At T-0281's close (2026-06-23), the partner + admin sibling smokes were **delivered as a narrowed
login-and-land slice** rather than T-0281's full AC1/AC2: under the network-stub seam both lists render
**empty**, so the partner smoke stops at viewing `/orders` (it does **NOT** accept a job and assert the
state transition), and the admin smoke stops at the authed landing (it does **NOT** assert ≥1 seeded
data row). The PM accepted that thin slice (it covers the highest-value bug class — login/guard/landing
— and matched the owner's "keep each thin" directive) and **carried the un-asserted depth here** so the
gap is tracked, not buried. This ticket restores T-0281's original AC1 (partner job-accept transition)
and AC2 (admin renders a seeded row).

The blocker T-0281 hit is the **stub-renders-empty-lists** problem: a catch-all `200 {}` at `/api/**`
means the jobs list and the admin oversight surfaces have no rows to assert against. This ticket's real
work is therefore the **fixture/seed depth** — provide deterministic stubbed (or seeded) rows so the
partner has an **available job** to accept and the admin has **≥1 row** on the landing surface — then
extend the two specs to drive + assert against them.

## Acceptance criteria

- [ ] **AC1 — Partner accept-job transition (rendered).** The partner smoke is extended so a seeded/
  stubbed **available job** exists; the partner accepts it and the job **transitions** to the accepted/
  confirmed view — asserted on **rendered UI** (it leaves "available" and appears in "accepted", or the
  status badge changes), the assertion T-0281's AC1 originally required. Accept only — not the full
  order lifecycle.
- [ ] **AC2 — Admin seeded-row assertion (rendered).** The admin smoke is extended so the landing
  oversight surface renders **≥1 row** of deterministic data (the heading + at least one data row /
  the KPIs populate), the assertion T-0281's AC2 originally required. A blank landing fails this AC.
- [ ] **AC3 — Same seam, deterministic fixtures.** Reuses the T-0271/T-0281 network-stub seam at
  `/api/**` (no new framework, no live Postgres) — the available-job and admin-row fixtures are added
  to the deterministic stub set (specific overrides, not the catch-all `{}`); if a real seeded DB is
  chosen instead, it must be the disposable test-only path, never production seed data.
- [ ] **AC4 — Deterministic, not flaky.** Both extended smokes stay stable across ≥3 consecutive runs;
  no arbitrary `waitForTimeout` — waits are web-first assertions / `waitForResponse`.
- [ ] **AC5 — CI green.** The existing `e2e-smoke` job in `frontend-ci.yml` runs the extended specs
  green; no new silently-skipped step.

## Out of scope
- **NOT a full regression suite** — only the two assertions T-0281 narrowed (partner accept-transition,
  admin seeded row). No job completion/photos/payout, no admin mutation flows, no negative paths, no
  i18n matrix.
- **NOT a new harness/framework/seed mechanism** — reuse the T-0271/T-0281 stub seam + the `e2e-smoke`
  job.
- **NOT the customer flow** (T-0271) and **NOT owner-only steps** — fixtures via the existing stub path;
  if a real owner-only step (e.g. a seeded credential needing a CI secret) surfaces, **stop and flag it**.

## Implementation notes
The core work is the **fixture depth** (an available-job fixture for the partner jobs query; a populated
fixture for the admin landing surface) layered onto T-0281's stub seam, then extending
`cleansia-partner.app-e2e/src/login-jobs.smoke.spec.ts` to accept + assert the transition and
`cleansia-admin.app-e2e/src/login-dashboard.smoke.spec.ts` to assert a rendered row. `[frontend]` owns
the spec extensions; `[backend]` confirms the accept-job request/response shape the stub must mimic (so
the asserted transition matches real behavior). `reviewer`-per-dev. `qa` confirms AC↔evidence + the
≥3-run determinism. No `security` (`security_touching: false`). No `optimizer`.

## Status log
- 2026-06-23 — draft → ready (created by pm). Carries the depth **T-0281 intentionally narrowed** at
  its close (partner job-accept transition = T-0281 AC1; admin seeded-row = T-0281 AC2 — both not
  asserted under the empty-list stub). **Honest carry, not a silent AC pass.** DoR met: AC observable;
  sized **S** (fixture depth + two spec extensions on an existing harness); `depends_on: [T-0281]`
  (`done` — the specs + CI job this extends); `layers: [frontend, backend]`; `security_touching:
  false`; `manual_steps: []`. Phase-0 characterization → one-line no-decision note, no panel. Follow-up
  batch candidate; lower priority than the audit-log follow-ups (it deepens an already-passing smoke).
- 2026-06-23 — ready → in_progress → in_review → done (frontend + reviewer, parallel; backend confirmed
  the accept-job request/response shape the stub mimics). Restored T-0281's narrowed depth: added an
  **available-job fixture** (specific `/api/**` stub override, not the catch-all `{}`) so the partner
  smoke accepts a job and asserts the **rendered state transition** (AC1), and a **populated admin
  landing fixture** so the admin smoke asserts **≥1 rendered row** (AC2) — both on the existing
  T-0271/T-0281 network-stub seam (no live Postgres, no new framework). Web-first assertions / no
  arbitrary `waitForTimeout`; stable across the re-run. The `e2e-smoke` job in `frontend-ci.yml` runs the
  extended specs green. **Orchestrator re-ran the batch green** on the combined tree. Shipped on
  `feature/wave8-pre-ios-cleanup` (commit `916014cb`). T-0281's AC1/AC2 carry-out is now closed.

## Review
<!-- reviewer / qa write verdicts here; PM reconciles before advancing state -->
