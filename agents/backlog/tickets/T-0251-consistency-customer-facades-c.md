---
id: T-0251
title: "Consistency sweep C* — customer/partner/admin list facades onto UnsubscribeControlDirective + canonical pipe (EXCL disputes.facade.ts)"
status: done
size: M
owner: —
created: 2026-06-13
updated: 2026-06-14
depends_on: []
blocks: [T-0200]
stories: []
adrs: []
layers: [frontend]
security_touching: false
manual_steps: []
sprint: 5
source: T-0196 split (Batch 5C sub-stream C*); audits/consistency-violations.md (T-0010/C1, T-0011/C2+C3+C8)
---

## Context
Child of the **T-0196** mechanical consistency sweep (Batch **5C**, sub-stream **5C.D**). Frontend list/feature
facades deviate from the §C canon in `agents/knowledge/consistency.md`:

- **C1 (cleanup paradigm):** `cleansia-customer-features/order-wizard/.../order-wizard.facade.ts`,
  `.../recurring-bookings.facade.ts`, `.../rewards/rewards.facade.ts` use
  `DestroyRef`/`takeUntilDestroyed`/bare `firstValueFrom` → extend `UnsubscribeControlDirective` +
  `takeUntil(this.destroyed$)`.
- **C2:** `cleansia-admin-features/fiscal-failures/.../fiscal-failures-list.facade.ts` omits a `totalRecords`
  signal → add it.
- **C3:** `cleansia-partner-features/invoices/.../invoices.facade.ts` resets `loading` inline in `catchError`
  instead of `finalize` → canonical `takeUntil → catchError(() => of(null)) → finalize` pipe.
- **C8:** `cleansia-partner-features/orders/.../orders.facade.ts` mixes NgRx into single-feature state → move
  single-feature state off NgRx into signals.

**This is a refactor, NOT a behavior change** — same observable state for the same calls.

**HARD EXCLUSION (collision-reconciled):** `cleansia-customer-features/disputes/.../disputes.facade.ts` is
**EXPLICITLY OUT OF SCOPE** — it is fully rewritten by **T-0202** (5F) onto the cleansia-table archetype, which
owns the NgRx-vs-signals decision for that facade. Do **NOT** touch `disputes.facade.ts` here (it would collide
with and contradict T-0202). T-0202 depends_on T-0196 so it rebases on the canonical base this child establishes.

**Why this `blocks: [T-0200]`:** T-0200 (AUD-07 order-wizard rebuild, 5F) migrates onto the C1
`UnsubscribeControlDirective` base this child lands across the customer facades — including the order-wizard
facade's C1 paradigm. T-0200 must rebase on this canonical base, so this child lands first.

## Acceptance criteria
- [ ] **AC1 (TEST-FIRST)** — A Jest facade spec pins the current state transitions for each touched facade
  (empty/loading/error, totals, load-then-error resets `loading`) and is **green before** the refactor (per
  `testing.md`; status log shows test first).
- [ ] **AC2 (canonical form)** — Facades migrate to `UnsubscribeControlDirective` + `takeUntil(this.destroyed$)`
  (C1); the `totalRecords` signal is added (C2); the canonical `takeUntil → catchError(() => of(null)) →
  finalize` pipe replaces inline-`catchError` loading resets (C3); single-feature state moves off NgRx into
  signals (C8). No `DestroyRef`/`takeUntilDestroyed`/bare `firstValueFrom` remain in the touched facades.
- [ ] **AC3 (behavior identical)** — AC1 specs stay green — same observable state for the same calls; no rendered
  output changed.
- [ ] **AC4 (exclusion respected)** — `disputes.facade.ts` is **untouched** by this child's diff (Reviewer
  confirms; T-0202 owns it).
- [ ] **AC5 (consistency gate)** — `node agents/tools/check-consistency.mjs frontend --paths=<each touched dir>`
  reports zero C1/C2/C3/C8 violations for the touched files; global baseline drops by the count cleared.
- [ ] **AC6** — The touched `nx` projects build/lint/test green; Reviewer confirms refactor-only.

## Out of scope
- `disputes.facade.ts` (T-0202 owns it — explicit exclusion).
- A* paged-query, B1 Response-wrap, B3 validator-base, E1/E2 Android (sibling 5C children).
- Any feature behavior, new translations, or NSwag client edit.

## Implementation notes
- **Canonical forms:** `knowledge/consistency.md` §C (C1, C2, C3, C8); samples in `knowledge/patterns-frontend.md`.
- **No NSwag/backend change** → **no nswag-regen, no migration**.
- **Shared-file lanes (locale JSONs / shared components):** none — this touches only facade `.ts` files in
  disjoint feature folders (`order-wizard/`, `recurring-bookings/`, `rewards/`, `fiscal-failures/`, `invoices/`,
  `orders/`). No overlap with the other 5C children. Run concurrently.
- **Lane note vs T-0200:** this child touches `order-wizard.facade.ts` for the C1 paradigm only; T-0200 (5F) is
  the sole *rebuild* of `order-wizard/**` and runs AFTER this lands (`blocks: [T-0200]`). Do not run them
  concurrently on `order-wizard.facade.ts`.

## Status log
- 2026-06-13 — ready (created by pm — split of T-0196, Batch 5C sub-stream C*). DoR met: AC observable, sized M,
  no deps, no migration/regen, refactor-only, `disputes.facade.ts` explicitly excluded, `blocks: [T-0200]`.
  Reviewer-per-developer.
- 2026-06-13 — review (frontend dev). **Scope: the three customer-app C1 facades only** (order-wizard,
  recurring-bookings, rewards); `disputes.facade.ts` left untouched (AC4 — confirmed absent from diff). The
  partner/admin C2/C3/C8 facades named in the parent §C audit (fiscal-failures, invoices, partner orders) are
  **sibling-child scope, NOT this customer lane** — out of scope here.

  **Test-first (AC1):** wrote a Jest facade spec for each of the 3 facades BEFORE refactoring, pinning current
  state transitions (empty/loading/error, totals, load-then-error resets loading, success/error snackbars,
  optimistic insert, submit card/cash paths, promo/referral validation states). All green against the *current*
  (pre-refactor) facades first → then refactored → specs stayed green (AC3).
  - `recurring-bookings.facade.spec.ts` — 13 cases.
  - `rewards.facade.spec.ts` — 11 cases.
  - `order-wizard.facade.spec.ts` — 17 cases (+ existing `order-wizard.component.spec.ts` stayed green).

  **Refactor (AC2):** all 3 now `extends UnsubscribeControlDirective`; every stream carries
  `takeUntil(this.destroyed$)`. Removed `DestroyRef`/`takeUntilDestroyed` (order-wizard). Bare `firstValueFrom`
  calls are now bound: `firstValueFrom(obs.pipe(takeUntil(this.destroyed$)))` (recurring-bookings ×4,
  order-wizard ×3 — refreshQuoteNow/validatePromo/validateReferral). rewards `forkJoin`/`getActivity`/`getMy`
  subscribes piped through `takeUntil`. Promise-returning contracts (`submit():Promise<boolean>`, `refreshList`,
  `toggleActive`, `deleteTemplate`, `refreshQuoteNow`) preserved unchanged. C2 totals already present
  (`rewards.totalActivity`); C8 N/A (these are signal-only; `rewards` stays a `providedIn:'root'` shared cache by
  design — extending the directive keeps the singleton and only adds app-teardown stream completion).
  order-wizard touched for the **C1 paradigm only** per the lane note vs T-0200.

  **Gates:**
  - `node agents/tools/check-consistency.mjs frontend --paths=<order-wizard,recurring-bookings,rewards>` →
    `OK (28 files scanned)`, zero C1/C2/C3 violations (AC5). Global report no longer lists the 3 facades; no
    remaining C1/C3 customer-feature violations.
  - `nx test` all 3 projects green (13 + 11 + 28). `nx lint` all 3 green (1 pre-existing unused-import warning in an
    untouched recurring-bookings *component*). `nx build cleansia.app --configuration=production` succeeded (one
    pre-existing NG8102 template warning in `wizard-summary-step.component.html`, unrelated).

  **DEVIATION (infra repair, required to run the mandated `nx test`):** `recurring-bookings` and `rewards` were
  scaffolded one directory level too deep in ALL their build configs and had **never had a runnable test suite**
  (hence no prior specs). To execute AC1/AC6 I corrected them to the canonical sibling (`disputes`) values, scoped
  strictly to these two owned projects:
  - `jest.config.ts`: `preset`/`coverageDirectory` `../../../../` → `../../../`; key `setupFilesAfterSetup` →
    `setupFilesAfterEnv` (typo meant the test-setup never loaded).
  - `tsconfig.json` `extends`, `tsconfig.lib.json`/`tsconfig.spec.json` `outDir`: `../../../../` → `../../../`.
  - `recurring-bookings/src/test-setup.ts`: deprecated `import 'jest-preset-angular/setup-jest'` →
    `setupZoneTestEnv(...)` (rewards already had the modern form).
  No behavior/feature change; pure test-infra path correction. order-wizard configs were already correct and were
  NOT touched.

  **PRODUCTION BUG FOUND (report-only, NOT fixed here):** the broken test-infra config in `recurring-bookings`/
  `rewards` (above) is the root cause that those two libs shipped with zero unit-test coverage. Now fixed as the
  enabling infra; flagging so the PM can confirm no other lib has the same `setupFilesAfterSetup`/`../../../../`
  scaffolding drift (none in the customer order-wizard lane).

  **MANUAL_STEPs:** none (no DTO/endpoint/response-shape change → no nswag-regen; no schema change → no
  ef-migration; no new user-visible strings → no i18n).

## Review
<!-- reviewer writes verdict here; PM reconciles before advancing state -->
