---
id: T-0549
title: FT-11 — land ADR-0033's named enforcer (reviewer-check 5) and stop BOTH pages teaching the superseded routing axis
status: ready
size: XS
owner: architect
created: 2026-08-05
updated: 2026-08-05
depends_on: [T-0553]
blocks: [T-0551]
stories: []
adrs: [0032, 0033]
layers: [architect, docs]
security_touching: false
manual_steps: []
sprint: 15
source: ADR-0033 §Follow-ups FT-11 (`0e1af548` M3) — **the ADR's own condition of acceptance**, widened
  by the independent lead pass finding **L2** (ADR-0033 `:1060`, `catalog-governance.md:275`). Filed by
  the PM 2026-08-05. **The fact that it had no ticket for the whole of its acceptance is the finding**
---

> ⚠️ **`depends_on: [T-0553]` applies to AC3 ONLY.** AC1 and AC2 are unblocked, are the condition of
> acceptance, and are dispatched **now**. Do not hold them behind the panel. See §Sequencing.

## Context

**ADR-0033 is `accepted` and NOT IN FORCE.** It named Block D — the named reviewer-check — as *its own
condition of acceptance*, and was accepted with that condition unmet. Its §Consequences claims in the
present tense that Block D *"moves the check out of this ADR"*. That claim is false at HEAD.

Re-verified by the PM before filing (2026-08-05, tree at `0e1af548`):

| | State at HEAD |
|---|---|
| `.claude/agents/reviewer.md:105-110` — what a **reviewer** actually runs | still the **superseded** axis, verbatim: *"a small clarification/example is fine to pass with the change; anything that redefines 'the one way to do X' is an **Architect** call"* |
| `agents/knowledge/conventions.md:122-127` — what an **author** actually applies | still the superseded axis: limb 1 *"a **new canonical archetype**"* **or** limb 2 *"anything that changes 'the one way to do X' … → **Architect** call"* |
| `agents/process/quality-gates.md` Gate 1 (`:92`) | no catalog-edit pointer |
| `agents/process/enforcement.md` | no reviewer-check 5 (that is **T-0550 / FT-12**) |
| FT-11 / FT-12 / FT-8 as `INDEX.md` rows | **none existed until this filing** |

By ADR-0032 D2's own line — *"T3-HUMAN requires a **named** checklist item; 'the reviewer will notice'
is not T3"* — ADR-0033 is `(guidance — no gate)` today. Its three routing tests bind nothing.
**FT-11 is not a follow-up; it is the remainder of the decision.**

**Why the scope is two pages, not one (finding L2).** The T-0471 challenger round measured only the
reviewer's page. The independent lead pass found the second site: `conventions.md:122-127` teaches the
same superseded axis to the *author*. Fixing one page leaves the rule **half-taught**, which is worse
than leaving it wholly untaught, because it looks done — the reviewer would run the new test while the
author's page still says the old one, or vice versa, and a disagreement between the two pages is
resolved by whoever quotes first.

**Not hypothetical.** `patterns-mobile.md:265-276` (T-0473) was harvested **after** ADR-0032 was
accepted, constrains call sites (*"hoist it one level further"*, *"not a whole-file `contains`"*),
carries no `**Enforced by:**` label, and self-classified as *"a testability clarification, not a
redefinition"* — the same words T-0274 used two sprints earlier for the same failure.

## Acceptance criteria

- [ ] **AC1 — the reviewer's page carries the named check.** Given `.claude/agents/reviewer.md`, When
      step 5's catalog clause (`:105-110`) is replaced with **reviewer-check 5 "Catalog-edit routing"**
      exactly as specified in ADR-0033 **Block D** (`agents/backlog/adr/0033-…md:368-394` — the three
      ordered tests + the floor's evidence rule + ADR-0032's enforcer/tier check), Then no sentence
      teaching the superseded axis survives on that page. Evidence: the diff, plus a grep for
      `redefines "the one way to do X"` in `.claude/agents/` returning **zero** hits.
- [ ] **AC2 — the check is reachable from the gate list, not only from the charter.** Given
      `agents/process/quality-gates.md` **Gate 1** (`:92`), When the one-line pointer specified at
      ADR-0033 `:399-403` is added, Then a reader arriving at Gate 1 with a `agents/knowledge/*.md`
      diff is routed to reviewer-check 5.
- [ ] **AC3 — the author's page stops teaching the superseded axis** (`agents/knowledge/conventions.md:122-127`).
      **HELD until T-0553 rules.** Given the panel's ruling on **which limb-1 text survives** (L3 — the
      floor *reverses* limb 1 and ADR-0033 never amends it), When `:122-127` is rewritten to the ruled
      text, Then `conventions.md` and `.claude/agents/reviewer.md` teach **one** routing axis and it is
      ADR-0033's. Evidence: both pages quoted side by side in `## Review`.
- [ ] **AC4 — the "not in force" box tells the truth at every intermediate state.** Given
      `agents/architecture/decisions/catalog-governance.md:61-76` (the ⛔ box) and `:111-114`, When AC1+AC2
      land but AC3 has not, Then the box is **updated, not deleted** — it must state that the reviewer's
      page is fixed and the author's page is not, so nobody reads a half-landed FT-11 as
      "ADR-0033 in force". When AC3 lands, the box is replaced by the in-force statement.
- [ ] **AC5 — no ticket claims more than it did.** Given this ticket is closed, When the PM writes the
      `INDEX.md` row, Then the row states which of the two pages were fixed. A row saying
      "the superseded axis is gone" while `conventions.md:122-127` still teaches it is the exact
      failure this ticket exists to end.

## Out of scope

- **Deciding what limb 1 becomes.** That is T-0553's panel (L1/L3/F4). This ticket *applies* the ruling.
- **Inserting Block C into `conventions.md`** — that is **T-0551 (FT-8)**, sequenced strictly after
  AC3, over the same section of the same file.
- **Recording the check id in `agents/process/enforcement.md`** — that is **T-0550 (FT-12)**.
- **Re-opening ADR-0033's status.** It stays `accepted`; the operative state lives in the living doc.
- **Tier-labelling `patterns-mobile.md`** (F2 / FT-4) — a different lane.

## Implementation notes

**Files this ticket touches — the complete list, named on purpose:**

| File | Hunk | AC |
|---|---|---|
| `.claude/agents/reviewer.md` | step 5, `:105-110` | AC1 |
| `agents/process/quality-gates.md` | Gate 1, `:92` | AC2 |
| `agents/knowledge/conventions.md` | `:122-127` | **AC3, held** |
| `agents/architecture/decisions/catalog-governance.md` | `:61-76`, `:111-114` | AC4 |

⚠️ **The PM is forbidden from editing `.claude/agents/*.md` and did not.** This ticket *specifies* that
edit; the architect performs it. That separation is why the finding sat unfiled — the round that found
it could not perform the fix either, and nobody carried the obligation forward.

### Sequencing — and why this ticket is `ready` rather than `blocked`

1. **AC1 + AC2 now.** They are unblocked, and they are the condition of acceptance: until reviewer-check
   5 exists, the only named standing item governing a catalog hunk asserts the axis ADR-0033 replaces
   (ADR-0032 **D3**'s exact failure mode, applied to ADR-0033 itself). Blocking them behind the panel
   would keep ADR-0033 out of force for the panel's whole duration, which is the status quo this ticket
   exists to end.
2. **AC3 after T-0553.** The floor reverses limb 1 of `:122-127`; what replaces it is a decision.
3. **Then T-0551 (FT-8)** inserts Block C *after* the repaired list. **`conventions.md` §"Harvest good
   patterns back into the catalog" is a serialized lane: T-0549 AC3 → T-0551.** Never concurrent —
   two instances in that section is how a page ends up carrying two incompatible forms, which is
   precisely the disease (F5) this whole ADR pair exists to stop.

### Staleness detectability (sprint-15 §D3)

Every path above is **excluded** from the candidate-3 path rule (`agents/knowledge/**`,
`agents/process/**`, `agents/architecture/**` are excluded; `.claude/agents/**` is not a product path).
**So no path-based signal can ever flag this ticket as stale.** The only live detector is candidate 1
— *a `ready` ticket with a written `## Review` is a lie* — and this ticket's `## Review` is empty by
design until work starts. **The PM must re-verify the four hunks above against the tree by hand at
dispatch.** That re-verification is one command:
`grep -n 'the one way to do X' .claude/agents/reviewer.md agents/knowledge/conventions.md`.

**No-decision note:** AC1, AC2 and AC4 apply text already ratified by an accepted ADR — no panel.
AC3 carries the decision and is gated on one.

## Status log
- 2026-08-05 — **created `ready` by pm.** Filed from ADR-0033 §Follow-ups FT-11 + lead-pass finding L2,
  eleven hours after the ADR that depends on it was accepted, with **no** prior `INDEX.md` row. Scope
  **widened** to `agents/knowledge/conventions.md:122-127` per L2 — the round measured only the
  reviewer's page. `depends_on: [T-0553]` is **AC3-only** and is annotated as such in the frontmatter
  banner; AC1/AC2 dispatch immediately.

## Review
<!-- reviewer / architect write verdicts here; PM reconciles before advancing state -->
