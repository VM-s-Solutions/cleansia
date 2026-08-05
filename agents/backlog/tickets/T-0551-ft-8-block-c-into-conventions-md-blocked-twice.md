---
id: T-0551
title: FT-8 — apply ADR-0033's Block C into conventions.md — BLOCKED TWICE, and must NOT be applied as specified
status: blocked
size: XS
owner: architect
created: 2026-08-05
updated: 2026-08-05
depends_on: [T-0549, T-0553]
blocks: []
stories: []
adrs: [0032, 0033]
layers: [architect, docs]
security_touching: false
manual_steps: []
sprint: 15
source: ADR-0033 §Follow-ups **FT-8** (`:571`) + Block C (`:289-354`) · blocked a second time by the
  independent lead pass finding **L3** (ADR-0033 `:977-1000`, `:1061`; `catalog-governance.md:143-154`)
---

> ⛔ **DO NOT DISPATCH, AND DO NOT APPLY BLOCK C AS WRITTEN.** Applied literally it installs a
> contradiction into `agents/knowledge/conventions.md`. Both blockers must clear first. See §Why blocked.

## Context

ADR-0033 **Block C** (`agents/backlog/adr/0033-…md:289-354`) is the catalog text of the routing test:
the three ordered tests, the amended floor, the "inline is not free" clause, and the cross-stack
strength rule — destined for `agents/knowledge/conventions.md` §"Harvest good patterns back into the
catalog", below the "The price of a law" section ADR-0032 already added (`conventions.md:136`).

It has been ready to apply since the ADR was accepted, and it must not be applied yet.

## Why blocked — two independent blockers, and neither is the other

### Blocker 1 — FT-11 (T-0549). The ADR's own M3 sequencing.

Block C's applier note says it *"may **not** be applied before the reviewer's standing checklist names
this check"* (`:291-294`). The reason is CH-2's: a `conventions.md` section aimed at the **author**,
while the **reviewer's** page still holds the superseded instruction, *"changes which rule is quotable
and not which rule is run"*. Until `.claude/agents/reviewer.md` carries reviewer-check 5, applying
Block C makes the disagreement between the two pages worse, not better.

### Blocker 2 — L3 (T-0553's panel). **Block C as specified installs a contradiction.**

This blocker is **new**, was missed by the challenger round, and is the sharper of the two.

`conventions.md:122-127` presents a **disjunction**:

> a *new canonical archetype* **or** anything that changes "the one way to do X" across the codebase →
> this is an **Architect** call

ADR-0033's floor **reverses the first limb**: a first statement of a canonical form, where no sentence
governs the subject at any level of generality, routes **inline** — and a first statement of a
canonical form *is* a new canonical archetype. The ADR's own retro row 7 proves it: T-0379's
`format: date` row routes inline under the floor and was routed to the **Architect** in fact, on the
ground that it *"defines the one way for date-only wire on iOS"*.

**Reversing limb 1 is a defensible architectural choice. The defect is that Block C does not implement
it.** Block C says only *"Insert **after** the existing numbered list"* (`:294-295`) and never amends,
deletes or annotates `:122-127`. Applied as specified, one page would instruct **both**:

- `:125-127` — *"a new canonical archetype … → **Architect**"*
- Block C's floor — *"first statement of a form, catalog silent → **inline**"*

on the same subject, for the same reader. That is exactly the disease **F5** was filed for — a page
carrying two incompatible forms — installed by the edit whose stated purpose is to stop authority
drift. ADR-0033's *"Refines … does not reverse"* header claim is also false as to that limb.

**So the corrected Block C is a decision, not a transcription.** It is being authored by the T-0553
panel. This ticket applies whatever that panel rules; it does not invent it.

## Acceptance criteria

- [ ] **AC0 — both blockers are cleared before anything is written.** Given this ticket is picked up,
      When the PM dispatches it, Then (a) `.claude/agents/reviewer.md` carries reviewer-check 5
      (T-0549 AC1 `done`), and (b) T-0553 has issued a ruling on limb 1 and a corrected Block C text.
      Evidence: both cited in `## Review`. **Neither may be assumed.**
- [ ] **AC1 — the corrected text lands, verbatim from the panel.** Given the panel's corrected Block C,
      When it is inserted into `agents/knowledge/conventions.md` §"Harvest good patterns back into the
      catalog", below §"The price of a law" (`:136`), Then it matches the ruled text with no
      applier-side edits. Any deviation is re-routed to the panel, not absorbed here.
- [ ] **AC2 — one page, one axis.** Given `conventions.md` after this change, When a reader looks for
      how a catalog edit routes, Then there is exactly **one** routing instruction and no sentence
      contradicts it. Evidence: the `:122-127` region quoted post-change alongside the inserted text,
      in `## Review`. **This is the AC that L3 exists to protect** — a green diff that leaves limb 1
      standing fails this ticket even if AC1 passes.
- [ ] **AC3 — the inserted section carries its own enforcement label.** Given ADR-0032 D2 applies to
      this entry as to any other, When Block C lands, Then it carries
      `**Enforced by:** reviewer-check 5 "Catalog-edit routing" (.claude/agents/reviewer.md) — T3-HUMAN`
      as specified at ADR-0033 `:335-337`. The governance rule discharges its own rule.
- [ ] **AC4 — the living doc stops saying "not in force".** Given
      `agents/architecture/decisions/catalog-governance.md:61-76` and `:111-114`, When this ticket and
      T-0549 are both `done`, Then the ⛔ box is replaced with the in-force statement and the trade-off
      section's L3 paragraph (`:143-154`) is updated to record how limb 1 was resolved.

## Out of scope

- **Writing the corrected Block C** — T-0553's panel.
- **The reviewer's charter and Gate 1** — T-0549.
- **`agents/process/enforcement.md`** — T-0550.
- **Block B** (`patterns-mobile.md`, FT-9) — the iOS lane, via T-0440.

## Implementation notes

**Files this ticket touches:** `agents/knowledge/conventions.md` (§"Harvest good patterns back into the
catalog", insertion below `:136`) and `agents/architecture/decisions/catalog-governance.md` (`:61-76`,
`:111-114`, `:143-154`).

⚠️ **Serialized lane.** `conventions.md` §"Harvest good patterns back into the catalog" is edited by
**T-0549 AC3 first, then this ticket** — never concurrently. Two instances in one section is how a page
acquires two incompatible forms, which is the defect under repair.

Cited line offsets in ADR-0033 drift: the lead pass recorded live citation drift in this worktree that
hit **every** load-bearing offset (`:577`→`:588`, `:985-990`→`:996-1001`, `:1230`→`:1241`). Match on
**quoted text**, not on line numbers.

### Staleness detectability (sprint-15 §D3)

`agents/knowledge/**` and `agents/architecture/**` are **excluded** from the candidate-3 path rule —
deliberately, because those files move constantly (counting them takes the same rule from 11 flags to
29 on this corpus). **So no path-based signal can flag this ticket**, and while it is `blocked` the
candidate-1 rule does not apply either. **This ticket is detectable only by its `depends_on` chain.**
Its state is a function of T-0549 and T-0553 and must be re-derived from those two, by hand, whenever
either moves.

**No-decision note:** this ticket applies a panel ruling verbatim; the decision belongs to T-0553.

## Status log
- 2026-08-05 — **created `blocked` by pm.** Filed from ADR-0033 §Follow-ups FT-8. Filed `blocked`
  rather than `ready` on purpose: the second blocker (L3) means Block C **as specified in the accepted
  ADR** is not safe to apply, and a `ready` row here invites exactly the literal application the lead
  pass warned against. Blocked behind **T-0549** (M3 sequencing) **and T-0553** (the corrected text).

## Review
<!-- reviewer / architect write verdicts here; PM reconciles before advancing state -->
