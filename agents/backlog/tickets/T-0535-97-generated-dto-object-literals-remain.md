---
id: T-0535
title: 97 object literals over generated command types remain, and the ratchet that would stop the next one is advisory
status: ready
size: M
owner: frontend
created: 2026-08-04
updated: 2026-08-04
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

## Review
<!-- reviewer writes the verdict here; PM reconciles before advancing state -->
