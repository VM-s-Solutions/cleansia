---
id: T-0241
title: Admin-app eslint selector-prefix alignment (kill the recurring baseline noise)
status: done
size: S
owner: pm
created: 2026-06-12
updated: 2026-06-15
depends_on: []
blocks: []
stories: []
adrs: []
layers: [frontend]
security_touching: false
manual_steps: []
sprint: 6
source: recurring Wave-3 baseline noise (flagged since Batch 3A; T-0191 frontend fixed one instance ad hoc)
---

## Context
Reviewers flagged the same lint baseline noise on every Wave-3 admin-frontend ticket since Batch 3A:
admin feature libs carry inconsistent Angular eslint **selector-prefix** config — some lib
`eslint.config.mjs` files use the scaffold default `lib`/`app` prefix while the catalog mandates the
`cleansia` prefix (the T-0191 frontend slice fixed `package-management`'s `lib`→`cleansia` ad hoc;
several Wave-3-scaffolded libs also shipped with missing jest/eslint/tsconfig infra that had to be
patched mid-review). Each new lib regenerates the noise and each reviewer re-flags it.
**No-decision note:** mechanical config alignment to the existing `cleansia` convention — skips the
panel.

## Acceptance criteria
- [ ] **AC1** — Every lib under `libs/cleansia-admin-features/**` (and the admin app) enforces
  `@angular-eslint/component-selector` + `directive-selector` with prefix `cleansia` in its eslint
  config; no lib retains the scaffold `lib`/`app` prefix.
- [ ] **AC2** — `npx nx lint` across the admin projects is green — any selector renamed to comply is
  updated at all usage sites (templates included) in the same change; admin production build green.
- [ ] **AC3** — The Nx generator defaults (`nx.json` / generators config) set the `cleansia` prefix
  so newly scaffolded libs are born compliant — the noise stops recurring, not just today's instances.
- [ ] **AC4** — Convention noted in `agents/knowledge/patterns-frontend.md` if not already explicit.

## Out of scope
- Customer/partner feature libs (sweep separately if the same drift exists — note findings to PM).
- Any non-selector eslint rule changes.

## Status log
- 2026-06-12 — draft (created by pm at Wave-3 close; recurring reviewer baseline noise since 3A)
- 2026-06-14 — **ready** (PM, Wave-6 intake / Batch **6C**). No-decision mechanical config alignment to the
  `cleansia` selector-prefix convention → skips the panel. **Lane FE-config:** edits admin eslint configs +
  the Nx generator default (`nx.json` generators block) + the admin app. **Parallel to the T-0259→T-0239
  chain** UNLESS it needs the same `nx.json` generators block they touch — if so, serialize after T-0259.
  Plan: `status/sprint-8.md` §3 Batch 6C.
- 2026-06-15 — **review** (frontend, FE-config lane; ran after T-0259 which was already in `review` and did
  NOT touch the `nx.json` generators block, so no collision).
  - **AC1** — every admin feature lib's `eslint.config.mjs` now enforces `component-selector` +
    `directive-selector` with `prefix: 'cleansia'`. The 11 libs still on the scaffold `lib` prefix
    (admin-login, disputes-management, template-management, country-management, invoice-management,
    language-management, employee-management, fiscal-failures, reports, order-management, pay-periods) were
    flipped `lib`→`cleansia`; the 8 already on `cleansia` were left. Each lib's `project.json` `prefix`
    field flipped `lib`→`cleansia` too (the generator's per-project default). **In-scope config repair:**
    `reports` + `fiscal-failures` shipped a broken eslint config (`tseslint.config(...)` importing the root
    `eslint.config.mjs` with NO `@angular-eslint` plugin → the config *crashed* on load with
    "Could not find plugin @angular-eslint", so the selector rule wasn't actually enforced — AC1 demands
    enforcement, not a crash). Rewrote both to mirror the working neighbor shape (base config +
    `nx.configs['flat/angular']`/`flat/angular-template` spreads). Fixing `reports`'s config *uncovered* 4
    pre-existing template a11y errors the crash had masked (reported below, NOT fixed — non-selector).
  - **AC2** — zero `@angular-eslint/component-selector`/`directive-selector` "should start with prefix"
    errors and zero plugin-load crashes across the whole admin scope (verified by `nx run-many -t lint`
    grep). 12 non-compliant selectors were renamed to `cleansia-admin-*` and every element usage updated in
    the same change: `admin-order-ops|order-refund|order-photos|photo-gallery` (order-management),
    `admin-payroll-ops` (invoice-management), `admin-pay-period-ops` (pay-periods),
    `lib-template-management|email-type-detail|email-template-form|email-template-list`
    (template-management), `lib-disputes-management|dispute-detail` (disputes-management). Removed the two
    now-stale `// eslint-disable-next-line @angular-eslint/component-selector` comments on the renamed
    payroll-ops/pay-period-ops production components. **Admin production build green**
    (`nx build cleansia-admin.app --configuration=production` → "Application bundle generation complete /
    Successfully ran target build"; only pre-existing NG8107 + bundle-budget warnings). Libs that had ONLY
    selector errors are now fully lint-green (admin-login, fiscal-failures + disputes-management, and the
    already-compliant ones). Libs still red carry ONLY pre-existing non-selector debt (see deviations).
  - **AC3** — `nx.json` `generators` block: added `prefix: 'cleansia'` to `@nx/angular:library` and
    `@nx/angular:component` so newly scaffolded admin libs/components are born compliant.
  - **AC4** — `agents/knowledge/patterns-frontend.md` updated: noted the generator default + the admin
    app's `prefix: ['app', 'cleansia']` (`app` for the bootstrap/route shells `app-root`/`app-unauthorized`,
    `cleansia` for everything else).
  - **Admin app** — `eslint.config.mjs` selector prefix changed `'app'` → `['app', 'cleansia']` (the rule
    accepts an array): keeps the conventional `app-root` (bootstrapped in `index.html`) and the lazy
    `app-unauthorized` route shell while enforcing `cleansia` for all feature components. No new lint errors
    (the app's 2 pre-existing `@nx/enforce-module-boundaries` "static import of lazy lib" errors are
    unchanged — verified identical against the HEAD config).
  - **TDD note:** this is a mechanical config/selector alignment with no behavior change; the regression net
    is the existing colocated specs (re-run green for the renamed-selector libs: order-management 40/41,
    invoice-management 34/34, pay-periods 16/16, disputes-management 26/26; template-management has no
    specs) + the production build. Specs reference component **classes/file paths**, not selector strings,
    so the renames need no spec edits.
  - **Deviations / findings (reported, NOT fixed — out of this lane's "no non-selector eslint rule changes"
    scope):**
    (1) Pre-existing non-selector lint **errors** remain in several admin libs (template a11y
    `click-events-have-key-events` / `interactive-supports-focus` / `label-has-associated-control`,
    `no-inferrable-types`, an `@nx/enforce-module-boundaries` relative-import in invoice-management) and in
    company-management / employee-management / pay-config-management / loyalty-tier-configs / marketing —
    these predate T-0241 and are unrelated to the selector prefix. Same class T-0259 reported as
    out-of-scope lint debt.
    (2) `reports` now surfaces 4 pre-existing template a11y errors that its previously-crashing eslint
    config had masked — a net honesty improvement (the rules now actually run), but the a11y fixes
    themselves are non-selector and out of scope.
    (3) Pre-existing **test** failure: `order-management.component.spec.ts` (the list component's
    `should create`) throws `NullInjectorError: No provider for _HttpClient` — the spec never provides
    `HttpClient`/`AdminClient`. Spec file is unmodified by this lane; failure is independent of the selector
    renames (the order-detail sub-component specs I touched the surroundings of all pass).
    (4) Most admin feature libs still have empty `tags: []` in `project.json` (so `tag:scope:admin` only
    matches 3 projects) — that's the T-0259/T-0239 tagging lane, left untouched to avoid lane collision.
    (5) Customer/partner feature libs were left as-is (explicit out-of-scope); a quick check shows the
    partner `forgot-password` config and several partner libs still carry mixed prefixes — flagged to PM
    for a separate sweep per the ticket's out-of-scope note.
- 2026-06-15 — **review (round 2, fixes reviewer CHANGES REQUESTED — blocking AC1 gap)** (frontend, FE-config lane).
  - **AC1 gap closed:** the four admin libs that had NO `eslint.config.mjs` — `company-management`,
    `loyalty-tier-configs`, `marketing`, `pay-config-management` — now each carry one, mirroring the working
    neighbor shape (`eslint.base.config.mjs` + `nx.configs['flat/angular'|'flat/angular-template']` spreads,
    `component-selector`/`directive-selector` `prefix: 'cleansia'`). Before: flat-config fell back to the root
    `eslint.config.mjs` (no `@angular-eslint` plugin), so the selector rule never enforced — a regenerated
    component would not be caught.
  - **RED captured (pre-fix):** `eslint --print-config` (run at the project cwd, where Nx's inferred `eslint .`
    lint target resolves flat-config) returned `@angular-eslint/component-selector` count = **0** for all four.
  - **GREEN (post-fix):** same probe returns count = **1** with `prefix: "cleansia"` for all four;
    `directive-selector` likewise loads. `nx lint <lib>` now runs the angular rule set against these libs.
  - **AC2 held:** ZERO `component-selector`/`directive-selector` errors across all four (their selectors were
    already `cleansia-admin-*`; no rename needed, no template touched). `marketing` and `pay-config-management`
    are lint-pass (exit 0). `company-management` (2) and `loyalty-tier-configs` (1) now surface **pre-existing
    non-selector template a11y errors** (`click-events-have-key-events`/`interactive-supports-focus` /
    `label-has-associated-control`) that the absent config had masked — identical honesty-improvement to the
    `reports` repair; these are non-selector debt, the reviewer's deviation #1 already lists this class
    out-of-scope, NOT fixed here.
  - **`project.json`:** all four already carried `prefix: "cleansia"` (prior round's per-project default);
    untouched by this round. The four libs have no jest/spec scaffolding (T-0259's lane per deviation #4), so
    the regression net for this config-only change is the lint-rule-load probes + the unchanged admin build
    (eslint config files are not in the build/test compilation graph). No source/selector/template modified.
  - **Files changed this round (only these):** four new `eslint.config.mjs` under
    `libs/cleansia-admin-features/{company-management,loyalty-tier-configs,marketing,pay-config-management}/`.

## Review
<!-- reviewer / security / optimizer write verdicts here; PM reconciles before advancing state -->

### Reviewer verdict — 2026-06-15 — CHANGES REQUESTED

Mechanical config/selector-alignment ticket; verified by project-scoped `nx lint`, `eslint --print-config`/run probes, the admin production build, and the consistency check (not solution-graph). One blocking AC1 gap; everything else passes.

**Verified PASS:**
- AC1 (configs that exist): all 21 admin lib `eslint.config.mjs` enforce `component-selector`+`directive-selector` with `prefix: 'cleansia'` (42/42 prefix lines); all 25 `project.json` carry `prefix: "cleansia"` (valid JSON). The `reports`/`fiscal-failures` repair is correct — HEAD configs imported the root `eslint.config.mjs` (no `@angular-eslint` plugin) and would crash on load; both now mirror the working neighbor shape (`eslint.base.config.mjs` + `nx.configs['flat/angular'|'flat/angular-template']`). A crashing config enforces nothing, so the repair is in-scope for AC1.
- AC2: 12 selectors renamed to `cleansia-admin-*` with every element usage updated (templates verified, zero orphaned `<admin-*>`/`<lib-*>`). `nx lint` on order/invoice/pay-periods/disputes/template/reports/fiscal-failures shows ZERO `component-selector`/`directive-selector` and ZERO plugin-load errors. `admin-login` fully green. Admin production build GREEN (`nx build cleansia-admin.app --configuration=production` → "Successfully ran target build"; only pre-existing NG8107 + bundle-budget warnings) — proves the renames are consistent.
- AC3: `nx.json` generators block adds `prefix: 'cleansia'` to `@nx/angular:library` + `@nx/angular:component`.
- AC4 (T-0241 portion): the selector-prefix paragraph in `patterns-frontend.md` (generator default + admin app `['app','cleansia']` array rationale) is a correct small clarification, not a standard redefinition — no Architect call needed.
- Admin app `eslint.config.mjs` `'app'` → `['app','cleansia']`: justified deviation (keeps `app-root`/`app-unauthorized` shells), no new lint errors.
- Spec edits: removed only the now-unused `/* eslint-disable @angular-eslint/component-selector */` block (mock selectors are all `cleansia-*`, so the disable is genuinely dead); no test logic changed, no vacuous tests. Stale `// eslint-disable-next-line component-selector` line comments on the two production components correctly removed. No T-NNNN/AC-n comments added.
- Gate 8: consistency check on the admin scope shows 16 pre-existing C3/D2 violations, all in files this ticket does NOT touch; ZERO new violations introduced on the selector axis.

**MUST FIX (blocking AC1):**
1. Four admin libs have NO `eslint.config.mjs` at all, so `@angular-eslint/component-selector` never runs for them — AC1 ("Every lib under `libs/cleansia-admin-features/**` enforces ... prefix `cleansia`") is not met:
   - `company-management`, `loyalty-tier-configs`, `marketing`, `pay-config-management`.
   Proof: `nx lint marketing` (and the other three) emits ONLY `@nx/enforce-module-boundaries`/`no-non-null-assertion`/`no-explicit-any` (root-config rules) — the selector rule is absent because flat-config falls back to the root `eslint.config.mjs`, which has no `@angular-eslint` plugin. Their selectors happen to already be `cleansia-admin-*`, but that is luck, not enforcement: a regenerated/new component would not be caught — the exact recurring-noise regression AC1 exists to close. This is the same non-enforcement failure mode the dev correctly repaired for `reports`/`fiscal-failures`, just applied inconsistently. T-0259's scope is customer/partner libs + `loyalty-promo-codes`, NOT these 4 admin libs, so the gap is uncovered and undisclosed (the dev's deviation #4 mentions only `tags`, not the missing eslint config). Fix: add an `eslint.config.mjs` to each of the 4 (mirror the neighbor shape with `prefix: 'cleansia'`) so `nx lint` actually enforces the selector rule; re-run `nx lint <lib>` to confirm the angular rule loads. (If full jest/tsconfig.spec scaffolding for these libs is deemed T-0259's, at minimum the eslint config — the selector-enforcement axis — is T-0241's.)

**Notes for PM (non-blocking):**
- The dev-listed `patterns-frontend.md` diff also contains a large "Module boundaries — the per-app client..." section. That is **T-0239's** content (T-0239 AC4: "boundary rule + tag scheme documented in patterns-frontend.md"), coexisting in the shared working tree — not T-0241's. The same applies to the root `src/Cleansia.App/eslint.config.mjs` scope-tag change (also not in T-0241's owned diff). Reconcile these with the T-0239 lane at commit time; do not double-attribute to T-0241.
- Out-of-scope findings the dev reported stand: pre-existing non-selector lint debt (a11y, no-inferrable-types, module-boundary), `reports`'s 4 newly-surfaced a11y errors (honesty improvement), and the pre-existing `order-management.component.spec.ts` HttpClient failure (confirmed unmodified by this lane). Partner/customer prefix drift (e.g. `forgot-password`) is explicitly out of scope — flagged to PM for a separate sweep.

**Verification achieved:** project-scoped `nx lint` (renamed/repaired libs + the 4 gap libs), `eslint`-run config-resolution probes, admin production build (green), consistency check. Orchestrator should run the authoritative clean-workspace lint/build.

