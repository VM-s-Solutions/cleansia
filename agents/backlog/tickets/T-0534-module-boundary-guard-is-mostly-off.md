---
id: T-0534
title: The module-boundary guard is mostly OFF — the base config grants allow-everything to all 44 libs that spread it
status: in_progress
size: M
owner: frontend
created: 2026-08-04
updated: 2026-08-04
depends_on: []
blocks: [T-0533]
stories: []
adrs: [0031]
layers: [frontend]
security_touching: false
manual_steps: []
sprint: 15
source: found by the web lane while pinning the dashboard fix; recorded in `e4dd27f5` — *"The module-
  boundary guard is mostly OFF … The catalog claims general enforcement; it has none."* Filed by the PM
  in the sprint-15 reconciliation.
---

## Context

`src/Cleansia.App/eslint.base.config.mjs:18-23` declares:

```js
depConstraints: [
  { sourceTag: '*', onlyDependOnLibsWithTags: ['*'] },
]
```

That is an **allow-everything** constraint. Every lib that spreads the base config inherits it, and
**44 libs under `libs/` have a local `eslint.config.*`** (counted at HEAD). So the only libs the
`@nx/enforce-module-boundaries` rule actually governs are the ones **without** a local config — the
inverse of what the catalog claims.

**This is a guard that reads as enforcement and is not.** That is worse than no guard: it is why
T-0533 (a customer lib importing four types from the partner client) could ship and stay shipped, and
why nobody was surprised by it. `agents/knowledge/patterns-frontend.md` asserts general enforcement;
it currently has none.

It compounds with a second fact recorded in the same commit: **lint runs with `continue-on-error`**
(`.github/workflows/frontend-ci.yml:73`). So even where the rule *does* apply, a violation cannot fail
CI today. Turning the constraint on without knowing the baseline would therefore change nothing
visible — and turning it on *and* making lint blocking without a staged plan would red every PR. Both
halves need thinking about together, which is why this is `M` and not `S`.

**Verified at HEAD (committed) 2026-08-04:** the constraint text, the 44-lib count, and the
`continue-on-error` line all read as described.

> **Working-tree state, checked separately and reported separately.** The live lane has already moved the
> constraints out of the base into a new **untracked** `src/Cleansia.App/eslint.module-boundaries.config.mjs`
> with real `scope:` / `type:` rules, and has begun tagging projects (e.g.
> `libs/core/customer-services/project.json` now carries `['scope:customer', 'type:util']`). Its own header
> comment names the defect verbatim — *"one had degenerated to `sourceTag:'*' -> onlyDependOnLibsWithTags:['*']`,
> i.e. allow everything"*. **None of it is committed.** The ACs below are what that diff still has to be
> gated against — in particular **AC4's mutation proof** and **AC5's measured violation count**, which are
> the two things a tag rollout most often ships without.

## Acceptance criteria

- [ ] **AC1 — the base constraint is no longer allow-everything.** Given `eslint.base.config.mjs`, When
      it is read, Then `depConstraints` expresses the real layering (app-scope tags and type tags), not
      `'*' → ['*']`.
- [ ] **AC2 — the tag vocabulary is written down where a developer will find it.** Given a new lib, When
      its author looks for which tags to apply, Then the answer is in
      `agents/knowledge/patterns-frontend.md` (scope tags per app + type tags), not inferred from
      neighbours. **This ticket may write that file** — it is the catalog's own claim that is being made
      true.
- [ ] **AC3 — every lib carries tags.** Given all 64 lib roots, When their `project.json` files are
      read, Then each has a `tags` array consistent with AC2. **Evidence:** a script or a test that
      enumerates them and fails on a missing tag — not a manual list, which rots.
- [ ] **AC4 — the guard demonstrably catches the known violation.** Given T-0533's import (a
      `customer-services` file importing from `@cleansia/partner-services`), When lint runs on that lib
      with the constraint in place, Then it **errors**. **Evidence: mutation proof** — re-introduce the
      import, show the error, revert. An assertion that "the rule is on" is not this AC.
- [ ] **AC5 — the current violation count is MEASURED and recorded, not guessed.** Given the new
      constraints, When lint runs across the workspace, Then the exact number of boundary violations and
      the libs they sit in are written into this ticket's status log. **This number is the input to
      T-0536** and must be produced before this ticket closes.
- [ ] **AC6 — the change does not red CI on debt it did not introduce.** Given `frontend-ci.yml` still
      runs lint with `continue-on-error`, When this ticket lands, Then that stays true **unless AC5's
      count is zero**. Flipping lint to blocking is **T-0536's** call, made against a known baseline.
      Say explicitly in the status log which of the two situations applies.
- [ ] **AC7 — the three apps still build.** `npm run build:cleansia-partner`, `build:cleansia-admin`,
      `build:cleansia-customer`.

## Out of scope

- **Fixing the violations AC5 counts** — that is **T-0536** (and T-0533 for the one already known).
  This ticket makes them visible; it does not clean them.
- **Making lint blocking in CI** — T-0536, once the baseline is known.
- The generated-DTO literal ratchet — **T-0535**. Different rule, different file
  (`eslint.generated-dto.config.mjs`), same reason it is advisory today.

## Implementation notes

**Archetype:** `agents/knowledge/patterns-frontend.md` (lib layering) + `consistency.md` (guards must be
able to fail).

The workspace is Nx 21 flat config. A per-lib `eslint.config.mjs` spreading the base is the shipped
idiom — the fix is in the base's constraint table plus each project's `tags`, **not** in deleting the
per-lib configs.

**One trap, already paid for once this sprint:** `e4dd27f5` found a lib with **no Nx project at all**,
which put it outside lint, test *and* this guard simultaneously. T-0537 owns preventing that recurring;
if a lib is invisible to Nx, no constraint here can govern it.

## Status log
- 2026-08-04 — created by pm during the sprint-15 reconciliation, at `in_progress`: a frontend instance
  is already working this finding. The ACs exist so the reviewer has something to gate against — in
  particular **AC4's mutation proof** and **AC5's measured count**, neither of which the in-flight work
  was briefed to produce.
- 2026-08-05 — frontend: AC1/AC2/AC3 were **already committed** in `7ddc491e` (the working-tree note in
  the Context is stale — nothing is untracked). AC4 and AC5, the two the note predicted would ship
  missing, **had shipped missing**, and the reason is AC6: with lint `continue-on-error`, there was
  nothing for a mutation proof to redden. This lane supplies both, plus the gate that makes AC4
  meaningful. See `## Review`.

## Review

### Gate 0 — the ticket's premise is stale at HEAD, in the ticket's favour

`eslint.base.config.mjs` no longer contains `depConstraints` at all: both it and the root
`eslint.config.mjs` spread `moduleBoundariesRules()` from
`src/Cleansia.App/eslint.module-boundaries.config.mjs`, which carries one real table
(`scope:*` → allow-list, plus orthogonal `type:*` rules). That landed in **`7ddc491e`**, committed.
The Context's *"None of it is committed"* note is **wrong at HEAD** — `git status` is clean over
`src/Cleansia.App/` and the file is tracked. No lane needs to re-do AC1.

### AC1 — satisfied at HEAD (verified, not inherited)

`eslint.base.config.mjs:2,10` imports and spreads `moduleBoundariesRules()`. The table is 8
constraints: `scope:shared → [shared]`, `scope:cleansia → [shared, cleansia]`, and
`scope:{customer,partner,admin} → [self, shared]`, plus `type:feature → [feature, ui, data, util]`,
`type:data → [data, util]`, `type:ui → [ui, util]`. No `'*' → ['*']` anywhere in the workspace.

### AC2 — satisfied at HEAD, and extended by this lane

`agents/knowledge/patterns-frontend.md` §"Module boundaries" carries the tag table (scope per app,
`type:feature|ui|data|util|app`), the one-file-two-spreaders rule, and the untagged-project error.
This lane **corrected one false sentence in it** — it claimed cross-app imports were *"caught by
`nx lint` in CI"*, which `continue-on-error: true` makes untrue — and added the real enforcer, the
recorded classes, and the tag-typo probe below.

### AC3 — satisfied, and **deliberately not extended with a tag-vocabulary rule**

All 64 lib roots + 3 apps carry `scope:*` and `type:*`; the enumerating enforcer is
`agents/tools/check-nx-project-registration.mjs` rule **NX-2** (tags asserted by **presence**), whose
46-scenario self-test is green and whose workflow is blocking. `node agents/tools/
check-nx-project-registration.mjs` → exit 0, *"read 64 lib root(s), 64 registered project(s), 65
alias(es), 3 rostered app(s), 208 tsconfig(s), 67 jest config(s), 67 test target(s) … 0 violation(s),
0 known"*.

That tool's header defers the **vocabulary** to this ticket. **This lane declines to add it, on
measured evidence rather than taste.** Probe: rewrote `libs/core/customer-services/project.json`'s
tags to `["scope:cusomer","type:util"]` and reintroduced the cross-app import. A typo does **not**
quietly switch that lib's scope rule off — every consumer's allow-list stops containing the target's
tag, so the workspace went from **19 boundary violations to 117** (91 `cross-scope` + 7
`untagged-project`, since `type:util` is not a `sourceTag` so nothing matches the lib as a source).
Restored byte-exact (sha256 on both files verified), back to 19. A hand-kept list of legal tag values
would be a second source of truth for something the constraint table already enforces through the
consumers — the exact shape of rot this ticket exists to remove.

### AC4 — the mutation proof, on the real workspace

T-0533's own import, reintroduced verbatim into
`libs/core/customer-services/src/lib/services/customer-auth.service.ts`:

```
module-boundaries: linted 1340 file(s), 20 @nx/enforce-module-boundaries violation(s) … 18 known
    1  cross-scope
module-boundaries: 1 drift(s) from the recorded set:
  NEW      libs/core/customer-services/src/lib/services/customer-auth.service.ts::cross-scope (x1)
```

**RED, exit 1.** Restored from a pre-mutation copy, `shasum -a 256 -c` → `OK`, re-run → **exit 0, 0
drift**. The same proof was run for T-0455's cycle (one restored import → **18 NEW
`circular-dependency` drifts**, restore verified by checksum, back to 0).

The mutation proof is only possible because of the gate below. Against `nx lint` alone it would have
proven nothing: the violation prints and CI stays green.

### AC5 — the measured violation count

`npx nx run-many -t lint --all --skip-nx-cache`, whole workspace, both sides:

| | before (`6a901ed0`) | after |
|---|---|---|
| projects with ≥1 lint error | 24 | **18** |
| total lint errors | 186 | **139** |
| total lint warnings | 163 | **163** |
| **`@nx/enforce-module-boundaries`** | **66** | **19** |

The 66 broke down as `circular-dependency` 47, `buildable-from-non-buildable` 14,
`static-import-of-lazy` 4, `deep-relative-import` 1. **T-0455 (same lane) retired all 47.** The
surviving 19, by file, are the `KNOWN` set in `agents/tools/check-module-boundaries.mjs`:

- **14 × `buildable-from-non-buildable`**, all in `libs/shared/components` — it carries a
  `package.json`, so Nx treats it as buildable and refuses its imports of non-buildable shared libs.
  A publishable-or-not decision about one lib, not an import rewrite.
- **4 × `static-import-of-lazy`** — `apps/cleansia.app/src/app/{app.ts, components/footer/…,
  components/navbar/…}` and `apps/cleansia-partner.app/src/app/app.component.ts` statically import
  `@cleansia/components` while each app's own `app.routes.ts` lazy-loads it.
- **1 × `deep-relative-import`** —
  `libs/cleansia-admin-features/invoice-management/src/lib/invoice-detail/invoice-detail.facade.ts:16`
  reaches `../../../../employee-management/src/lib/components` for `RejectDialogComponent`.

**`cross-scope` = 0 and `untagged-project` = 0.** Those are the two the tag scheme exists for, and
they are the two the gate holds at zero. This is T-0536's input.

### AC6 — CI, answered plainly

**`frontend-ci.yml`'s lint step stays `continue-on-error: true`.** AC5's count is not zero (139 lint
errors, 83 of them a11y), so the alternative branch does not apply and flipping it remains T-0536's.

**But "leave it non-blocking" would have meant shipping this ticket with no enforcement at all, and
the ticket's own AC4 would have been unprovable.** Two independent reasons that step could never
carry this rule, both verified:

1. `continue-on-error: true` (`frontend-ci.yml:73`) — a violation there sets no exit code.
2. It is `nx affected -t lint`. A boundary violation is a statement about a **pair** of projects, and
   the half that *reports* it is frequently not the half that was edited — of the 47 circular errors,
   45 printed inside the `*-stores` libs while the offending import was one line in `*-services`.

So the boundary slice was given **its own gate that can go red**, on the shape this repo already
standardized for exactly this problem (`nx-project-registration.yml`, `offerability-parity.yml`):

- `agents/tools/check-module-boundaries.mjs` — runs `npx eslint . --format json` **from the workspace
  root**, filters to `@nx/enforce-module-boundaries`, classifies each message, and compares against an
  **exact-match ratchet in both directions** (a new violation is red; so is a recorded one that was
  fixed without deleting its entry). Root-config-only is deliberate and is the direct answer to this
  ticket's finding: a gate assembled from the per-project configs inherits the hole those configs
  *were* the hole. Equivalence checked — the single root pass reports the same 19 violations in the
  same 19 files as the 70-project `nx run-many` run.
- Anti-vacuity (ADR-0032 D3): zero files linted, a walk below an 800-file floor, an unparseable
  report, or an eslint exit outside {0,1} are all hard failures.
- `agents/tools/check-module-boundaries.test.mjs` — 21 scenarios over synthetic eslint reports.
  **Stub the tool's body to `process.exit(0)` and 21 of 21 go red** (zero survivors); restored
  byte-exact by checksum and re-run green.
- `.github/workflows/module-boundaries.yml` — its own repo-root workflow, self-test first, **no
  `continue-on-error`**.

**Tier, stated honestly:** the boundary rule is **T1-CI** through that workflow. It is **not** T1-CI
through `nx lint`, and no catalog entry should say it is.

### AC7 — the three apps build

`npx nx build <app> --configuration=production --skip-nx-cache` → `cleansia.app` **0**,
`cleansia-partner.app` **0**, `cleansia-admin.app` **0**. Plus
`npx nx run-many -t test --all --skip-nx-cache` → **67 projects green**, and
`node agents/tools/check-nx-project-registration.mjs` → exit 0 with its self-test at **46/46**.

### Catalog-edit routing

Three edits to `agents/knowledge/patterns-frontend.md`, all routed **inline**, all recorded per
`conventions.md` §"Who ratifies a catalog edit":

1. **The enforcement correction in §"Module boundaries"** (`nx lint` shows, the gate stops) — a
   correction inside an existing rule's own scope, test 4. **Test 1 sweep:** no call site changes
   status; the recorded set *is* the current state, so the baseline is zero by construction.
2. **"An interceptor that dispatches to the store lives in the STORE lib"** — **test 1 sweep:**
   `grep -rn "dispatch(" libs/core/*-services/src` (excluding `client/`) → **zero hits**;
   `grep -rln "HttpInterceptorFn = " libs apps` → 12 files, all already on the right side of the new
   table (3 shared cross-app, 6 per-app client, 3 store). No shipped code becomes a deviation.
   **Test 2 floor:** searched `patterns-frontend.md` and `consistency.md` for `interceptor` /
   `Interceptor`. The only sentence about interceptor *location* is `patterns-frontend.md:424`,
   *"**Cross-app** HTTP concerns live as `HttpInterceptorFn`s in `libs/core/services/…`"* — expressly
   scoped to cross-app concerns, and a per-app store-dispatching interceptor is not a sub-case of
   "cross-app". `consistency.md`'s only hits are backend (`HttpLoggingInterceptor` E10,
   `DbCommandInterceptor`). Nothing governs the subject at any level → floor claimed, inline.
   **Test 3:** frontend only, built and run in this ticket.
3. **"A lower lib calls a higher one through a token"** — **test 1 sweep:** the only instance of the
   inverted shape was `customer-auth.service.ts`'s `inject(SavedAddressStore)`, fixed in this change;
   `grep -rn "stores'" libs/core/*-services/src` → zero. **Test 2 floor:** searched both catalog
   files for `InjectionToken` / `injection token` → **no hits at all**; no sentence covers how a lower
   lib reaches a higher one. Floor claimed, inline.

Each entry constrains call sites, so each carries `**Enforced by:** <named enforcer> — <tier token>`
in the catalog text.
