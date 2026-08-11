---
id: T-0553
title: Architect panel — L1 ("governs" is undefined), L3 (the reversed limb), F4 (the missing trade-off limb) → a new ADR refining ADR-0033 D1
status: in_progress
size: S
owner: architect
created: 2026-08-05
updated: 2026-08-05
depends_on: []
blocks: [T-0549, T-0551]
stories: []
adrs: [0032, 0033]
layers: [architect]
security_touching: false
manual_steps: []
sprint: 15
source: the independent lead pass on ADR-0033 (`0e1af548`) routed **L1**, **L3** and **F4** to a new
  panel and explicitly refused to author the repair itself — *"inventing the repair and ratifying it is
  the defect T-0471 exists to repair, and it binds a second lead too"* (ADR-0033 `:1083-1085`)
---

> **This ticket exists so the panel is visible in the backlog rather than only in the owner's head.**
> The three-instance panel is **running now**. It was filed after it spawned, which is the same defect
> as FT-11 going unfiled — recorded here rather than smoothed over.

## Context

ADR-0033 is `accepted` with three findings routed onward. None of them is a challenge to the accepted
text; together they are **the next decision**, and the lead that found them is barred from authoring it.

### L1 — M1 defines *silence* and never defines *governs*

The floor's amended test 2 fires when *"a catalog sentence **already governs** this entry's subject at
any level of generality"*. **M1 defines silence** (*no sentence covers X at any level of generality*)
and never defines **governs**. The missing operational sentence, named by the lead but deliberately not
adopted, is:

> a sentence **governs** this entry's subject iff, **applied to that subject**, it yields a prescription
> the entry contradicts.

Worked both ways on real hunks, which is what makes this a decision and not a wording nit:

| Case | Files | Verdict |
|---|---|---|
| **T-0349** (address-picker VM) | `agents/knowledge/patterns-mobile.md:1244-1254` vs the governing sentence at `:990` (*"feature/VM import no MapKit"*) | **Determinate — test 2 fires.** Routes to Architect; history agrees (the ticket is `owner: architect`) |
| **T-0473** | `agents/knowledge/patterns-mobile.md:265-276` vs the candidate sentence at `:520-522` | **Indeterminate.** `:520-522` names a *file*-scoped source-text scan; the entry forbids *"a whole-file `contains`"*. One reviewer fires test 2; another correctly sees a prescription for a different subject that yields nothing here |

Until this lands, routing on a general sentence rests on the reviewer's **paraphrase** of it — the same
defect CH-1 removed from the other half of the clause.

### L3 — Block C as specified installs a contradiction into `conventions.md`

`agents/knowledge/conventions.md:122-127` is a **disjunction** (*"a **new canonical archetype** **or**
anything that changes 'the one way to do X'"* → Architect). ADR-0033's floor **reverses the first
limb** — a first statement of a canonical form routes inline, and a first statement of a canonical form
*is* a new canonical archetype. Block C says only *"insert after the existing numbered list"*
(ADR-0033 `:294-295`) and never amends `:122-127`. Applied literally, one page instructs both.

Retro row 7 is the proof the reversal is real: T-0379's `format: date` row routes **inline** under the
floor and was routed to the **Architect** in fact, *"because it defines the one way for date-only wire
on iOS"*. ADR-0033's *"Refines … does not reverse"* header claim is **false as to that limb**.

### F4 — no "carries a trade-off ⇒ ADR" limb in the routing test

The three tests price *cost imposed on code*. They have no limb for an entry that **prices two
competing forms** — where the question is not who is obliged but whether the trade-off belongs in an
ADR rather than a catalog row. Confirmed real with a ground **actually used and answered**:
`agents/backlog/tickets/T-0397-…md:70` records the architect ruling *"carries a real trade-off — should
it be an ADR, not a catalog row? Ruling: no trade-off survives"*.

Folded into this panel deliberately: **a fourth test is the same decision as defining "governs"** —
both answer "what makes an edit the Architect's rather than the author's".

## Acceptance criteria

- [ ] **AC1 — composition, declared in the ADR itself.** Given the panel, When it runs, Then **author ≠
      challenger ≠ lead** as three distinct instances (`agents/process/deliberation.md:29-30`), and
      §Verdict **states the composition explicitly**. The lead of the ADR-0033 pass may not be the
      author here. *T-0471 had to be re-run for exactly this reason — the challenger correctly refused
      to self-certify — so a claim of independence that rests on anything other than composition does
      not satisfy this AC.*
- [ ] **AC2 — "governs" is defined operationally, and the definition is worked on both cases.** Given
      the new ADR, When it defines `governs`, Then it is applied in the text to **T-0473**
      (`patterns-mobile.md:265-276` vs `:520-522`) and **T-0349** (`:1244-1254` vs `:990`), each
      re-derived from the files rather than from ADR-0033's summary, and T-0473 comes out
      **determinate**. A definition that leaves the reproducible indeterminacy standing fails.
- [ ] **AC3 — limb 1 is ruled, and the ruling is emitted as literal insertable text.** Given
      `conventions.md:122-127`, When the panel rules, Then it states **which limb-1 text survives** and
      what the residual sentence says, and emits (a) the replacement text for `:122-127` and (b) the
      **corrected Block C**, both as literal markdown. **These two outputs are the direct inputs to
      T-0549 AC3 and T-0551 AC1** — a ruling that stops at "limb 1 should be amended" does not unblock
      either ticket.
- [ ] **AC4 — F4 is decided, not deferred.** Given the routing test, When the panel rules, Then a fourth
      test is either **added with its text** or **rejected with the reason**, and the ruling addresses
      the `T-0397-…md:70` evidence directly (a ground used and answered under the old axis).
- [ ] **AC5 — ADR-0033's false header claim is corrected through a sanctioned instrument.** Given that
      ADR-0033 is `accepted`, When the *"Refines … does not reverse"* claim is corrected as to limb 1,
      Then it is done by the new refining ADR or by a dated appended section on ADR-0033
      (`agents/backlog/adr/README.md:7-29`) — **never a silent in-body edit**.
- [ ] **AC6 — the ADR number is allocated at spawn and collision-checked at write time.** Given
      `agents/backlog/adr/`, When the number is taken, Then it is verified free by grep **immediately
      before the file is written**, not earlier. **At HEAD the highest is 0042** (`proposed`, with
      `T-0547` reserved for its refactor). *Two architects collided on an ADR number this sprint by both
      grepping correctly at the same moment* — the check must be adjacent to the write.
- [ ] **AC7 — the deliberation trail is on disk.** Given the panel, When it completes, Then the
      challenge file exists at `agents/backlog/adr/challenges/<NNNN>-<topic>.md` (the shape of
      `challenges/0033-floor.md`), the new ADR carries `## Challenge` / `## Defense` / `## Verdict`, and
      consensus is recorded as *zero blocking challenges remain*.
- [ ] **AC8 — the living doc records the outcome.** Given
      `agents/architecture/decisions/catalog-governance.md`, When the panel closes, Then its **L1 / L3 /
      F4 open-item rows (`:274`, `:276`, `:283`) are closed with the ruling**, the L1 residual warning
      inside the floor's clause 2 (`:94-103`) is resolved or restated, and the trade-off-space paragraph
      (`:143-154`) records how limb 1 was settled.

## Out of scope

- **Re-opening ADR-0033's status or its accepted amendments M1–M6.** L1/L3/F4 are the next decision, not
  challenges to the accepted text (ADR-0033 `:1082-1085`).
- **Applying anything.** The panel emits text; **T-0549 AC3** and **T-0551** apply it. The panel writes
  no `agents/knowledge/**` or `.claude/agents/**` file.
- **Landing reviewer-check 5** — T-0549, which is unblocked and goes first regardless of this panel.
- **F2** (`patterns-mobile.md` has zero `**Enforced by:**` labels) and **F3** (the T-0473 entry's missing
  enforcer + tier) — recorded, not re-opened, and not this panel's.

## Implementation notes

**Files this ticket produces or touches:**
- `agents/backlog/adr/<NNNN>-<slug>.md` — the new ADR (number allocated per AC6).
- `agents/backlog/adr/challenges/<NNNN>-<topic>.md` — the challenge trail.
- `agents/architecture/decisions/catalog-governance.md` — `:94-103`, `:143-154`, `:274`, `:276`, `:283`.
- Possibly a dated appended section on
  `agents/backlog/adr/0033-catalog-edit-authority-the-routing-test-and-cross-stack-claim-strength.md`
  (AC5) — appended only, never an in-body rewrite.

**Citation drift is live in this worktree.** The lead pass recorded that it hit **every** load-bearing
offset (`:577`→`:588`, `:985-990`→`:996-1001`, `:1230`→`:1241`). Quote the text you rely on; do not
trust a line number you did not just re-read.

**What the previous pass could not verify, so this one should not inherit it** (ADR-0033 `:1069-1080`):
no `git log`/`git show` was available, so **no catalog edit was ever read as a diff** — cases α and β
were reconstructed from in-tree entries plus their tickets. A diff-based re-run may find a hunk whose
routing differs from its settled text. Two cases is also not a corpus.

### Staleness detectability (sprint-15 §D3)

Every path above is **excluded** from the candidate-3 path rule (`agents/architecture/**`) or is not a
product path (`agents/backlog/adr/**`). **No path-based signal can flag this ticket.** While it is
`in_progress` the candidate-1 rule does not apply either. Its true state is legible only from whether
the new ADR file exists and whether `catalog-governance.md`'s L1/L3/F4 rows are still red — check both
by hand at each checkpoint.

## Status log
- 2026-08-05 — **created `in_progress` by pm**, after the panel had already spawned. Filed because the
  work was visible only to the owner: an in-flight three-instance panel that blocks two filed tickets
  (T-0549 AC3, T-0551) and appears in no `INDEX.md` row is the same failure as FT-11's — the ticket is
  the only thing that makes a hand-off traceable.

## Review
<!-- reviewer / architect write verdicts here; PM reconciles before advancing state -->
