---
id: T-0288
title: Fix latent broken order-management.component.spec.ts (HttpClient inject — no test provider)
status: ready
size: S
owner: —
created: 2026-06-22
updated: 2026-06-22
depends_on: []
blocks: []
stories: []
adrs: []
layers: [frontend]
security_touching: false
manual_steps: []
sprint: 10
---

> **No-decision note (panel skipped):** a pre-existing broken unit spec — no new behavior, no product
> decision. Mechanical test-infra fix (provide the missing test dependency), characterization only.

## Context

Wave-8 leftover, NOT previously ticketed-as-open. `order-management.component.spec.ts`
(`libs/cleansia-admin-features/order-management/src/lib/order-management/`) is a latent **broken** spec:
its `TestBed.configureTestingModule({ imports: [OrderManagementComponent] })` provides **no**
`HttpClient` (no `provideHttpClient(withInterceptorsFromDi())` / `HttpClientTestingModule`), so the
standalone component's (or its facade's) `HttpClient` injection has no provider and the single
`should create` test fails. **Proven failing on `master`** — a pre-existing latent break, independent of
any Wave-8/Wave-9 change. It belongs in this cleanup so it is not silently carried into iOS-era CI.

This is the **only** new Wave-8 follow-up filed here. T-0281 (E2E partner+admin sibling smokes) is
already filed `ready` and **stays in Wave 8's close**, NOT this wave.

## Acceptance criteria

- [ ] **AC1 — Spec compiles + passes.** `nx test` for the order-management lib runs the
  `OrderManagementComponent` spec **green** — the missing test dependency is provided
  (`provideHttpClient` + test backend, or `HttpClientTestingModule`, plus any other unsatisfied DI the
  component/facade needs), following the project's standard standalone-component spec setup.
- [ ] **AC2 — Characterization, not behavior change.** The fix is to the **test harness only**; no
  production component/facade code changes to make the test pass. If the component is genuinely broken
  (not just the test setup), that is a separate **bug finding** to file, not patched here.
- [ ] **AC3 — Proven-fix evidence.** The status log / Review records the **before** (fails on master with
  the no-provider error) and **after** (green) — pinning that this was a real latent break, not a flaky.
- [ ] **AC4 — Mechanical checks green.** `nx test` (the lib) + the admin app prod-build pass;
  `check-consistency.mjs` reports no new violation on the touched dir.

## Out of scope
- Adding **new** test coverage for order-management behavior (this only un-breaks the existing
  `should create` smoke; richer specs are a separate ticket if wanted).
- Any production `order-management.component.ts` / facade change (unless a genuine component bug is found
  → file a separate ticket).
- The E2E sibling smokes (T-0281, Wave 8).

## Implementation notes
Mirror a working standalone-component admin spec in the same lib group for the canonical TestBed setup
(`provideHttpClient(withInterceptorsFromDi())` + `provideHttpClientTesting()`, or the
`HttpClientTestingModule` the codebase uses, plus router/translate test providers as needed). **TDD
posture is "characterization":** confirm the red (no-provider error) first, then make it green by fixing
the harness only. `nx test` + admin prod-build are the evidence.

## Status log
- 2026-06-22 — draft → ready (created by pm). Folded into Wave 9 as the one new Wave-8 leftover (the
  filed-and-ready T-0281 stays in Wave 8's close). DoR: AC observable; sized **S** (one spec harness fix);
  `layers: [frontend]`; `security_touching: false`; `manual_steps: []`; archetype = a working
  standalone-component admin spec. No panel — one-line no-decision note (pre-existing broken spec,
  mechanical fix). Independent — runs concurrently with the audit-log chain.

## Review
<!-- reviewer / qa write verdicts here; PM reconciles before advancing state -->
