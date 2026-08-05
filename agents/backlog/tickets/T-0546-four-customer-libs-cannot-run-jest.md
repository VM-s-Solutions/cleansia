---
id: T-0546
title: Four customer feature libs cannot compile a test — a wrong tsconfig `extends` makes their green test target meaningless
status: draft
size: S
owner: frontend
created: 2026-08-05
updated: 2026-08-05
depends_on: []
blocks: []
stories: []
adrs: []
layers: [frontend]
security_touching: false
manual_steps: []
sprint: 15
source: found by frontend while executing T-0535 (needed coverage in two of the affected libs before
  converting their generated-DTO literals; fixed those two in that ticket, filed the rest here)
---

## Context

`libs/cleansia-customer-features/<lib>/tsconfig.json` should extend `../../../tsconfig.base.json`
(three levels up, to `src/Cleansia.App/`). **Six of the fifteen customer feature libs extend
`../../../../tsconfig.base.json`** — one level too many — which resolves to
`/Users/.../cleansia/src/tsconfig.base.json`, a file that does not exist.

The consequence is invisible until someone writes a test:

- **With a spec present**, the Jest run dies at suite level with
  `error TS5083: Cannot read file '.../src/tsconfig.base.json'` and **zero tests run**.
- **With no spec present** — which is the state all of these are in — Jest prints
  `No tests found, exiting with code 0` and Nx reports **`Successfully ran target test`**.

So `nx run-many -t test --all` is green over libs whose test target has **never compiled a single
test**, and the first person to add one gets a failure that looks like their own fault.

**Verified at HEAD (2026-08-05):**

| Lib | `extends` | `test` target | specs | state |
|---|---|---|---|---|
| `checkout` | `../../../../` ❌ | yes | 0 | **broken** |
| `home` | `../../../../` ❌ | yes | 0 | **broken** |
| `legal-pages` | `../../../../` ❌ | **no `test` target at all** | 0 | **broken + untestable** |
| `services-catalog` | `../../../../` ❌ | yes | 0 | **broken** |
| `gdpr` | `../../../` ✅ | yes | 1 | **fixed in T-0535** |
| `orders` | `../../../` ✅ | yes | 2 | **fixed in T-0535** |

The other nine customer libs, and every partner and admin feature lib, already extend correctly.

**The four files still broken, named in full (PM, 2026-08-05 — re-verified at HEAD after `6bd3b0c6`
fixed `gdpr` and `orders` as part of T-0535):**

```
src/Cleansia.App/libs/cleansia-customer-features/checkout/tsconfig.json          # extends ../../../../ ❌
src/Cleansia.App/libs/cleansia-customer-features/home/tsconfig.json              # extends ../../../../ ❌
src/Cleansia.App/libs/cleansia-customer-features/legal-pages/tsconfig.json       # extends ../../../../ ❌ + no `test` target
src/Cleansia.App/libs/cleansia-customer-features/services-catalog/tsconfig.json  # extends ../../../../ ❌
```

Also touched by AC2: `src/Cleansia.App/libs/cleansia-customer-features/legal-pages/project.json`
(no `"test"` target — the other three have one).

> These paths are written out because a ticket that names no path **cannot be detected as stale by
> anything** (`status/sprint-15.md` §D3): the staleness check resolves path tokens by suffix match
> against `git ls-files`, and `libs/cleansia-customer-features/<lib>/tsconfig.json` with a placeholder
> resolves to nothing. This ticket is now covered by that check.

## Acceptance criteria

- [ ] **AC1** — `checkout`, `home` and `services-catalog` extend `../../../tsconfig.base.json`, and a
      throwaway spec added to each compiles and runs. (The throwaway spec is evidence, not a
      deliverable — it does not land.)
- [ ] **AC2** — `legal-pages` gains a `test` target matching the other customer libs (`@nx/jest:jest`
      + its existing `jest.config.ts`/`tsconfig.spec.json`), and the same probe runs there.
- [ ] **AC3 — the class is closed, not the four instances.** A check asserts that **every**
      `libs/**/tsconfig.json` resolves its `extends` to a file that exists, and that every feature lib
      with a `jest.config.ts` also has a `test` target. Without this the next generated lib
      reintroduces it silently — which is exactly how six of them accumulated.
- [ ] **AC4** — `nx run-many -t test --all` still green, and the number of projects that actually
      execute at least one test is recorded before and after.

## Out of scope

- Writing real tests for these libs. That is per-feature work; this ticket only makes it *possible*.
- `partner-stores` / `admin-stores` / `customer-stores` having no `test` target — **T-0463**, a
  different defect with a different fix.

## Implementation notes

The fix itself is one token per file. **AC3 is the actual value** — prefer a small Jest spec under
`apps/` or a `tools/` script over a README line, and mutation-prove it by breaking one `extends`.

Note `legal-pages` also carries the i18n-absence guard family described in
`patterns-frontend.md` §"Retiring a claim the product does not deliver" — if any of that is meant to
live in the lib rather than in `apps/cleansia.app`, it currently could not run.

## Status log
- 2026-08-05 — draft, filed by frontend from the T-0535 sweep. Not `ready`: AC3 wants a decision on
  where the workspace-wide check lives (a Jest spec vs. a `tools/` script run in CI), which is a
  small architect/PM call rather than an implementer's.
- 2026-08-05 — **pm, additive only: the four remaining files are now written out by full path** (§Context)
  and the ticket gained its first `INDEX.md` row — it had none. State re-verified at HEAD: `gdpr` and
  `orders` extend `../../../` ✅ (fixed in `6bd3b0c6`); `checkout`, `home`, `legal-pages` and
  `services-catalog` still extend `../../../../` ❌, and `legal-pages/project.json` still has no `test`
  target. **Status unchanged (`draft`) and no AC was altered** — the open AC3 decision still gates it.

## Review

### Catalog-edit routing (ADR-0033) — verdict: **inline**

One entry added to `agents/knowledge/patterns-frontend.md`, appended to the existing
`check-nx-project-registration.mjs` block: *"A registered lib is not yet a runnable one — two ways a
green `test` target compiles nothing"*.

- **Test 1 — does it put shipped code in violation?** No. **Sweep:**
  `node agents/tools/check-nx-project-registration.mjs` over the whole workspace — 205 tsconfigs,
  64 jest configs, 64 registered lib projects, 3 rostered apps → **0 violations, 0 known**. The six
  dangling `extends` and the one missing `test` target are fixed in this same change, so the entry's
  baseline is zero by construction.
- **Test 2 — does it narrow open latitude? Floor claimed.** **Searched** `patterns-frontend.md` and
  `consistency.md` for `extends`, `tsconfig`, `jest.config`, `test target`, `No tests found`, `TS5083`.
  Returned: `extends` matches only `extends UnsubscribeControlDirective` (the facade base, a different
  subject); `tsconfig` matches only the `tsconfig.base.json` **alias** rule (an import path naming a
  missing lib) and `tsconfig.app.json` excluding specs from `npm run typecheck`; `jest.config`,
  `test target`, `No tests found` and `TS5083` return **nothing in either file**. No sentence covers
  tsconfig `extends` resolution or the jest-config ⇔ test-target pairing at any level of generality, and
  the entry carves no exception out of the registration rule — it extends it in the same direction.
- **Test 3 — prescriptive claim about an unbuilt stack?** No. Frontend only, built and run here
  (`nx run-many -t test --all`, the three production builds, the checker + its self-test).
- **Price paid:** the entry constrains call sites, so it ships its gate — **Enforced by:**
  `check-nx-project-registration.mjs` NX-6/NX-7 — **T1-CI**.

### Evidence

- **Mutation proof of the `extends` fix:** re-broke `checkout/tsconfig.json` back to `../../../../` →
  `TS5083`, `Tests: 0 total`, suite failed to run. Restored → `8 passed`.
- **Mutation proof of the guard:** the checker stubbed to `process.exit(0)` fails **36 of 40**
  self-test scenarios (the 4 survivors are the must-NOT-fire cases a no-op passes by construction).
- **AC4 counts:** before — `run-many -t test --all` green over **61** projects, of which **12** had a
  `test` target and **zero** spec files (3 of the 4 libs in this ticket; `legal-pages` was not in the run
  at all). After — **62** projects, and the zero-spec list is down to **8**.
