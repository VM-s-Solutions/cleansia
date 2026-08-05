---
id: T-0546
title: Four customer feature libs cannot compile a test — a wrong tsconfig `extends` makes their green test target meaningless
status: draft
size: S
owner: frontend
created: 2026-08-05
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

## Review
<!-- reviewer verdict here -->
