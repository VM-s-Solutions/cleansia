---
id: T-0535
title: 97 object literals over generated command types remain, and the ratchet that would stop the next one is advisory
status: done
size: M
owner: frontend
created: 2026-08-04
updated: 2026-08-05
depends_on: []
blocks: []
stories: []
adrs: [0031]
layers: [frontend]
security_touching: false
manual_steps: []
sprint: 15
source: `e4dd27f5` (count corrected to 131 from the 134 quoted, `libs/core` and `libs/data-access`
  cleared to ZERO) and `c968cbf9` (134 at that point; ADR-0031 recorded 122). Filed by the PM in the
  sprint-15 reconciliation.
---

## Context

ADR-0031 predicted this population grows **monotonically**, and it has been right twice: **122** when
the ADR was written, **134** at `c968cbf9`, and **131** measured properly at `e4dd27f5` — from which
`libs/core` and `libs/data-access` were then cleared to **zero**, leaving **97**.

**PM re-count at HEAD (2026-08-04):** `git grep -cE "new [A-Za-z0-9_]*(Command|Request|Dto|Query)\(\{"`
against the **committed** tree, across `apps/` and `libs/` and excluding the generated clients, returns
**98**. That is the same population as the reported 97 — the difference is counting method (a line grep
versus the ratchet's AST selector), not drift. **The citation resolves.** Re-measure before starting: a
web lane is live in this workspace and the number is expected to move.

**Why the count matters at all.** A generated DTO built from an object literal is **required-key
checked** by TypeScript. The next NSwag regen that adds a required field breaks **every one of them at
once**, in a wave, in code the owner did not touch. Construct-then-assign does not. That is the whole of
ADR-0031.

**The ratchet exists and is honest about what it is.** `eslint.generated-dto.config.mjs` exports
`generatedDtoLiteralRules()`, opt-in per scope, and *the opt-in list IS the progress bar* — a scope may
only join once its own count is zero. It is correctly labelled **T2-ADVISORY**, because
`.github/workflows/frontend-ci.yml:73` runs lint with `continue-on-error: true`. **It is not claimed as
enforcement it does not have**, and that honesty is why this ticket exists rather than a false sense of
safety.

The ratchet also covers one thing the typecheck guard structurally cannot: **spec files are excluded
from every app `tsconfig`**, so `typecheck-apps.mjs` never sees a literal in a test.

**Worst remaining clusters** (PM count at HEAD, by lib): `cleansia-partner-features/orders`,
`cleansia-customer-features/profile`, `cleansia-admin-features/pay-periods`,
`cleansia-admin-features/invoice-management`, `cleansia-admin-features/employee-management` — 3 files
each.

## Acceptance criteria

- [ ] **AC1 — at least one whole feature cluster reaches zero and OPTS IN.** Given a chosen scope, When
      it is converted, Then its `eslint.config.mjs` spreads `generatedDtoLiteralRules()` and its count is
      zero. **Joining the opt-in list is the deliverable** — a conversion that does not opt in can
      silently regress the next day.
- [ ] **AC2 — the wire body is pinned BEFORE each conversion, not after.** Given a file with no test
      coverage of its command construction, When it is converted, Then a test asserting the **serialized
      body** (`.toJSON()`) exists **first** and passes unmodified across the change. This is the method
      `e4dd27f5` used for the two auth services (21 tests written before touching them) and it is the
      only thing that makes a mechanical conversion safe: **every generated field is optional on the
      class, so a dropped field is invisible to the compiler.**
- [ ] **AC3 — the count moves and is recorded.** Given the sweep, When it lands, Then the status log
      names the before and after counts and the exact command used to produce them, so the next
      instance measures the same way.
- [ ] **AC4 — no ratchet weakening.** Given `eslint.generated-dto.config.mjs`, When this ticket lands,
      Then no scope is **removed** from the opt-in list and the rule itself is unchanged. *"Never remove
      it to make a new literal compile"* is the config's own instruction.
- [ ] **AC5 — the three apps build and all affected Jest suites pass.**

## Out of scope

- **Making lint blocking** so the ratchet becomes real enforcement — **T-0536**. That needs the whole
  lint baseline, not this rule.
- **Turning on the module-boundary constraint** — **T-0534**.
- Converting all 97 in one run. That is an `L` and would be unreviewable. **Take clusters.** If a run
  discovers it has become an `L`, stop and say so per `ticket-lifecycle.md`.

## Implementation notes

**Archetype:** ADR-0031 + `agents/knowledge/patterns-frontend.md`.

The conversion is `const c = new X(); c.field = value;` — never `new X({ ... })`.

Measure with the ratchet's own selector where possible
(`NewExpression[callee.name=/(Command|Request|Dto|Query)$/][arguments.0.type='ObjectExpression']`) so
the number in the log is the number the rule sees.

**No-decision note:** ADR-0031 already ruled the pattern. This is mechanical application. No panel.

## Status log
- 2026-08-04 — created `ready` by pm during the sprint-15 reconciliation. Passes DoR: AC observable,
  sized `M` with an explicit stop-if-`L` instruction, no dependencies, no manual steps, archetype named.
- 2026-08-05 — **first pass landed: 97 → 46.** Counted with the ticket's own command against the
  working tree (`libs/` + `apps/`, generated clients excluded):
  `grep -rE "new [A-Za-z0-9_]*(Command|Request|Dto|Query)\(\{" <scope> --include="*.ts" | wc -l`.

  | scope | before | after |
  |---|---|---|
  | `libs/cleansia-partner-features` | 14 | **0** |
  | `libs/cleansia-customer-features` | 17 | **0** |
  | `libs/cleansia-admin-features` | 66 | **46** |
  | `libs/core`, `libs/data-access`, `libs/shared`, `apps` | 0 | 0 |
  | **total** | **97** | **46** |

  (The customer scope measured 17, not the quoted 18 — counting method, not drift; partner measured
  14 and admin 66, so the population at HEAD was 97.)

  **AC1 — 26 scopes joined the opt-in list**, which is the deliverable. The unit is a lint scope, and
  each `libs/cleansia-*-features/<lib>` owns its own `eslint.config.mjs`, so a converted lib opts in
  alone: all 8 partner libs with local configs spread `generatedDtoLiteralRules()`; the partner
  `dashboard` (no local config) and **all** customer feature libs are covered by workspace-relative
  globs in `src/Cleansia.App/eslint.config.mjs` — the `order-wizard` glob widened to
  `libs/cleansia-customer-features/**/*.ts`. **17 of 26 admin libs** opted in: 11 converted here
  (`admin-profile`, `admin-user-management`, `currency-management`, `language-management`, `marketing`,
  `loyalty-promo-codes`, `loyalty-referrals`, `loyalty-user-detail`, `package-management`,
  `pay-config-management`, `service-management`) and 6 that were already at zero and were simply never
  opted in (`admin-login`, `audit-log`, `data-protection`, `fiscal-failures`,
  `membership-plan-management`, `reports`).

  **AC4 — no scope removed, rule text unchanged.**

  **AC2 — coverage, and the call sites that had none.** Every converted site got a `.toJSON()` body
  assertion **before** the conversion. Six facades were converted with **zero prior test coverage**
  and were red-first covered by new spec files: partner `order-photos.facade` and
  `profile-documents.facade`, partner `forgot-password.facade` (only a models spec existed), customer
  `gdpr.facade`, customer `order-detail.facade` and `track-order.facade`. Four admin libs had a spec
  for a *sibling* facade but none for the one holding the literals, and got new specs:
  `currency-form`, `language-form`, `promo-code-form`, `service-form`. Everywhere else the existing
  per-field assertions were upgraded to whole-body `toEqual(command.toJSON())` — a per-field check
  passes when a *different* field is dropped, so it is not a guard.

  **Two infrastructure defects found on the way, both blocking coverage rather than caused by it:**
  1. **Six customer libs cannot run Jest at all.** `libs/cleansia-customer-features/{checkout, gdpr,
     home, legal-pages, orders, services-catalog}/tsconfig.json` extend
     `../../../../tsconfig.base.json` — one level too many, resolving to `src/tsconfig.base.json`.
     Any spec in them dies with `TS5083`; with no spec they report "No tests found, exiting with code
     0", i.e. **a green test target that has never compiled a test**. Fixed in `gdpr` and `orders`
     because this ticket needed coverage there; **`checkout`, `home`, `legal-pages` and
     `services-catalog` are still broken** and should be filed (same one-token fix, plus
     `legal-pages` has no `test` target at all).
  2. `partner-stores` still has no `test` target (already known — T-0463).

  **Gate 8.** `npm run typecheck` OK (3/3). All three production builds exit 0. Jest green on every
  touched project. `nx run-many -t lint --all`: **24 failing projects before and 24 after, and the
  two failing sets are byte-identical** (diff empty); zero `no-restricted-syntax` violations
  anywhere; per-project problem counts unchanged on every lib touched.

  **Mutation-proved, three ways, all restored byte-exact (sha256-verified):**
  - *the rule fires in the newly opted-in scopes* — reintroducing `new GrantConsentCommand({
    consentType })` in partner `gdpr.facade.ts` and `new SubmitOrderReviewCommand({ … })` in customer
    `order-detail.facade.ts` each produced the ADR-0031 `no-restricted-syntax` error;
  - *a conversion drops a field* — deleting `command.description = result.description;` from partner
    `order-details.facade.ts` turned **1 test red**, named `OrderDetailsFacade › command bodies on
    the wire › serializes a reported issue with the order id and the description` (1 failed / 0 after
    restore);
  - *the key-parity guard* — deleting `command.tierUpgrade = values.tierUpgrade;` from customer
    `notification-preferences.models.ts` turned **2 tests red**, including `… › serializes every
    rendered category, and only those`.

  **What remains — 46 literals in 9 admin libs, none of which can opt in yet:**
  `employee-management` 10 · `invoice-management` 8 · `template-management` 5 · `order-management` 5 ·
  `country-management` 5 · `pay-periods` 4 · `loyalty-tier-configs` 3 · `disputes-management` 3 ·
  `company-management` 3. Three of those (`template-management`, `country-management`,
  `company-management` — 13 literals) have **no spec files at all**, so each needs its command bodies
  pinned from scratch first; that is the next instance's largest cost, not the conversion.

  **Out of the rule's reach but the identical hazard** (the selector matches
  `(Command|Request|Dto|Query)$` only): `SaveOrderPhotosPhotoToSave`, `SaveMyDocumentsDocumentToSave`
  and `CreateServiceTranslationInput` were converted opportunistically in files already being
  touched. Widening the selector is an **Architect call** — a broader regex starts matching
  hand-written classes — and is not attempted here.

## Review
<!-- reviewer writes the verdict here; PM reconciles before advancing state -->
