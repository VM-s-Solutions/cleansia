---
id: T-0559
title: Finish the generated-DTO literal sweep — 46 left in 9 admin libs, 3 with no specs at all — and rule on the ratchet's `(Command|Request|Dto|Query)$` blind spot
status: draft
size: M
owner: frontend
created: 2026-08-05
updated: 2026-08-05
depends_on: [T-0535]
blocks: []
stories: []
adrs: [0031]
layers: [frontend, architect]
security_touching: false
manual_steps: []
sprint: 15
source: reported by the frontend lane while executing **T-0535** (`6bd3b0c6`, 51 literals converted).
  This is T-0535's **remainder plus its one architect question**, filed separately because T-0535 is a
  live lane and its ticket file must not be edited underneath it
---

## Context

**Dedup, stated first because this ticket looks like a duplicate and is not.** `T-0535` ("97 object
literals over generated command types remain, and the ratchet that would stop the next one is
advisory") is `ready`, `owner: frontend`, and **in flight** — `6bd3b0c6` converted 51 literals and wired
the ratchet into the root config plus a per-lib `eslint.config.mjs` across the admin feature libs. This
ticket carries what that lane reported as **left over**, so the remainder is visible without editing a
ticket another instance is holding.

### Half 1 — the remainder (reported by the lane, to be re-counted at dispatch)

**46 literals remain in 9 admin libs.** The real cost is not the conversion: **three of those libs have
no spec files at all**, so pinning their behaviour before changing it means writing the first specs
those libs have ever had. That is why this is `M` and not `S`, and why it did not simply continue
inside T-0535.

### Half 2 — the blind spot, which is an Architect call

The ratchet's selector is:

```js
"NewExpression[callee.name=/(Command|Request|Dto|Query)$/][arguments.0.type='ObjectExpression']"
```

(`src/Cleansia.App/eslint.generated-dto.config.mjs:24`.) It matches **four suffixes only**. Several
generated types are identically hazardous — the same all-optional generated shape, the same silent
drift when a property is renamed at regen time — and are **invisible to the rule** because their names
end differently.

Widening the regex is not a free win: a broader pattern starts matching **hand-written** classes, where
an object-literal constructor argument is ordinary and correct. So the choice is between a rule that
under-matches (today) and one that produces false positives on hand-written code — **a trade-off, and
therefore an Architect call**, not an implementer's tweak.

## Acceptance criteria

- [ ] **AC1 — the count is re-run and recorded, not inherited.** Given the reported "46 in 9 admin
      libs", When this ticket starts, Then the sweep is re-run against the tree at that moment and both
      numbers are recorded here with the command used. **The figure above is the reporting lane's, on
      its tree**; three reconciliation passes this sprint each found an inherited number to be wrong.
- [ ] **AC2 — the three spec-less libs are named before any conversion.** Given the 9 libs, When AC1
      runs, Then the libs with **zero** spec files are named explicitly with their paths, because they
      are the actual cost of this ticket and the reason for its size.
- [ ] **AC3 — behaviour is pinned before it is changed.** Given a lib with no specs, When its literals
      are converted, Then a spec exists first that fails if the conversion changes behaviour. A
      conversion in an untested lib with no new test is not evidence of anything.
- [ ] **AC4 — the remaining literals are converted.** Given the re-counted set, When the work lands,
      Then the count is **zero** in those libs and the ratchet is enabled for each of them.
- [ ] **AC5 — the regex question is ruled by the Architect, with evidence.** Given
      `eslint.generated-dto.config.mjs:24`, When the architect rules, Then the ruling names (a) which
      generated types the current four suffixes miss, with file paths; (b) what a widened pattern would
      start matching in hand-written code, measured rather than assumed; and (c) the decision — widen,
      keep and state the residual, or replace name-matching with a different discriminator (e.g. import
      provenance from the generated client). **A "we should widen it someday" note fails this AC.**
- [ ] **AC6 — whatever survives states its tier** (ADR-0032). Given the rule after AC5, When it is
      recorded in `agents/knowledge/patterns-frontend.md`, Then it carries
      `**Enforced by:** … — <tier>`, and the tier is honest: `frontend-ci.yml:72-74` runs lint with
      `continue-on-error: true`, so **any ESLint rule is `T2-ADVISORY` on this stack** however it is
      worded, with a statement of what would promote it. `patterns-frontend.md:462-465` is the house
      model.
- [ ] **AC7 — the catalog edit is routed, not self-ratified.** Given AC6 touches
      `agents/knowledge/patterns-frontend.md`, When the entry is written, Then its routing follows
      whatever ADR-0033's test resolves to at that time (see **T-0549** / **T-0551**), and the ticket's
      `## Review` records the catalog search that justifies the routing claim.

## Out of scope

- **The 51 literals already converted in `6bd3b0c6`**, and anything else T-0535 is currently holding.
  **Do not edit `agents/backlog/tickets/T-0535-…md`** — it belongs to a live lane.
- The four broken customer-lib tsconfigs — **T-0546**, a different defect with a different fix.
- Widening the ratchet's *glob scope* to stacks it does not read today; this ticket is about the
  **selector**, and any scope change is stated as its own decision under AC5.

## Implementation notes

**Files this ticket touches:**
- `src/Cleansia.App/eslint.generated-dto.config.mjs` — `:24`, the selector (AC5).
- `src/Cleansia.App/eslint.config.mjs` — the glob list that scopes the rule.
- `src/Cleansia.App/libs/cleansia-admin-features/*/eslint.config.mjs` — per-lib enablement for the
  remaining libs (AC4).
- `src/Cleansia.App/libs/cleansia-admin-features/*/src/**` — the conversions and the new specs
  (exact libs named by AC1/AC2).
- `agents/knowledge/patterns-frontend.md` — the entry + its enforcer/tier (AC6/AC7).

**Sequencing.** AC5 is an architect ruling and can run **in parallel** with AC1–AC4; the conversion does
not wait on it. Do not let the reverse happen — the sweep stalling behind a regex decision is how 97
literals became a standing number in the first place.

### Staleness detectability (sprint-15 §D3)

Names **product paths under `src/`** — `eslint.generated-dto.config.mjs`, `eslint.config.mjs` and the
admin lib trees — so the candidate-3 path rule covers this ticket, which matters because the same lane
is actively committing in those directories. `agents/knowledge/**` is excluded from that rule, so the
AC6/AC7 half is invisible to it and must be re-checked by hand.

## Status log
- 2026-08-05 — created **`draft`** by pm. Not `ready`: **AC5 is an architect ruling that does not exist
  yet** (Definition of Ready item 7 — the canonical form is not identified until the regex question is
  answered), and AC1/AC2 must re-derive the count and the spec-less lib list before anyone is dispatched
  into nine libraries. Filed as T-0535's remainder rather than as an edit to T-0535, whose file is held
  by a live lane.

## Review
<!-- reviewer / architect write verdicts here; PM reconciles before advancing state -->
