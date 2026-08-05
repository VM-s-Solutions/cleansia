---
id: T-0550
title: FT-12 — record reviewer-check 5's id in enforcement.md so dropping it is a visible regression
status: ready
size: XS
owner: architect
created: 2026-08-05
updated: 2026-08-05
depends_on: [T-0549]
blocks: []
stories: []
adrs: [0032, 0033]
layers: [architect, docs]
security_touching: false
manual_steps: []
sprint: 15
source: ADR-0033 §Follow-ups **FT-12** (`:570`) · `catalog-governance.md:278`. Never filed as an
  `INDEX.md` row until 2026-08-05 — filed by the PM in the same pass as FT-11/FT-8
---

## Context

ADR-0033's floor is enforced by exactly one thing: **reviewer-check 5 "Catalog-edit routing"**, a
`T3-HUMAN` enforcer living in a charter file (`.claude/agents/reviewer.md`). T-0549 lands it.

A `T3-HUMAN` enforcer that exists in only one place has a specific failure mode the other tiers do not:
**a later charter edit can delete it and nothing goes red.** `agents/process/enforcement.md` is where
the project records what a rule is worth (`:146-178`, §"Enforcement tiers — what a rule is worth
(ADR-0032)"), and today its `T3-HUMAN` bullet (`:161-163`) names Gate-DP §G and "a numbered
reviewer-check" generically — **it names no reviewer-check id at all**. So the enforcement side of the
project cannot tell that reviewer-check 5 is load-bearing, and a charter cleanup that removes it would
read as tidying.

This is small, and it is the difference between an enforcer that can be silently deleted and one that
cannot. It is also the ADR's own follow-up, filed at `:570`, which had no ticket for the entire life of
the accepted ADR.

## Acceptance criteria

- [ ] **AC1 — the id is written down on the enforcement side.** Given `agents/process/enforcement.md`
      §"Enforcement tiers" (`:146-178`), When the `T3-HUMAN` bullet (`:161-163`) is extended to name
      **reviewer-check 5 "Catalog-edit routing"** by id, with its home file (`.claude/agents/reviewer.md`)
      and what it governs (any diff touching `agents/knowledge/*.md`), Then a reader of `enforcement.md`
      alone can tell the check exists and what depends on it.
- [ ] **AC2 — the dependency is stated in the direction that matters.** Given that entry, When it is
      written, Then it states explicitly that **ADR-0033's routing test is `(guidance — no gate)` if
      this check is removed** — so deleting reviewer-check 5 is legible as a regression against an
      accepted ADR rather than as a charter cleanup.
- [ ] **AC3 — no new claim is made.** Given the edit, When reviewed, Then it records an existing
      enforcer and declares no new rule. `enforcement.md:180-185` reserves rule *creation* for the
      Architect via ADR/catalog; this ticket only makes an already-decided enforcer visible.

## Out of scope

- **Writing the check.** That is T-0549 (FT-11). This ticket records it; if T-0549 has not landed there
  is nothing to record, which is why `depends_on: [T-0549]`.
- Any change to the tier definitions themselves, or to the `check-consistency.mjs` / lint tier facts at
  `:159-160` and `:173-178`.
- The `agents/knowledge/conventions.md` §"The price of a law" table (`:136-159`) — a different page with
  its own lane (T-0549 AC3 → T-0551).

## Implementation notes

**Files this ticket touches:** `agents/process/enforcement.md` (§"Enforcement tiers", `:146-178` — the
`T3-HUMAN` bullet at `:161-163`). That is the whole change.

The house model for how a `T3-HUMAN` enforcer is named on this page already exists two bullets up:
ADR-0018's Gate-DP §G of `ios-app-review-checklist.md` + reviewer-check #22. Mirror that shape.

### Staleness detectability (sprint-15 §D3)

`agents/process/**` is **excluded** from the candidate-3 path rule, so **no path-based signal can flag
this ticket**. Candidate 1 (empty `## Review` while `ready`) is the only live detector. Manual
re-verification at dispatch is one command:
`grep -n 'reviewer-check' agents/process/enforcement.md`.

**No-decision note:** mechanical record of an enforcer an accepted ADR already specifies — no panel.

## Status log
- 2026-08-05 — created `ready` by pm. Filed from ADR-0033 §Follow-ups FT-12, which had carried no
  `INDEX.md` row since the ADR was accepted. Sequenced behind T-0549 because the id it records does not
  exist until T-0549 AC1 lands.

## Review
<!-- reviewer / architect write verdicts here; PM reconciles before advancing state -->
