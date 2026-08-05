---
id: T-0551
title: FT-8 — apply ADR-0033's Block C into conventions.md — BLOCKED TWICE, and must NOT be applied as specified
status: done
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

> ✅ **CLEARED AND APPLIED 2026-08-05 — and Block C was NOT applied as written.** Both blockers cleared
> (T-0549 AC1 landed reviewer-check 5; T-0553's panel ruled the severance). What landed is the
> **severed** block: the rejected "governs" definition **excised**, accepted ADR-0033's own floor wording
> verbatim in its place, the numbered list **replaced** rather than appended to, CH-G fixed, the Block D
> addendum **held**. The warning below is kept as the record of why. See §Review.

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

- [x] **AC0 — both blockers are cleared before anything is written.** Given this ticket is picked up,
      When the PM dispatches it, Then (a) `.claude/agents/reviewer.md` carries reviewer-check 5
      (T-0549 AC1 `done`), and (b) T-0553 has issued a ruling on limb 1 and a corrected Block C text.
      Evidence: both cited in `## Review`. **Neither may be assumed.**
- [x] **AC1 — the corrected text lands, verbatim from the panel.** ✅ **with ONE declared placement
      deviation, named in §Review "The one deviation" — not absorbed silently.** Given the panel's corrected Block C,
      When it is inserted into `agents/knowledge/conventions.md` §"Harvest good patterns back into the
      catalog", below §"The price of a law" (`:136`), Then it matches the ruled text with no
      applier-side edits. Any deviation is re-routed to the panel, not absorbed here.
- [x] **AC2 — one page, one axis.** Given `conventions.md` after this change, When a reader looks for
      how a catalog edit routes, Then there is exactly **one** routing instruction and no sentence
      contradicts it. Evidence: the `:122-127` region quoted post-change alongside the inserted text,
      in `## Review`. **This is the AC that L3 exists to protect** — a green diff that leaves limb 1
      standing fails this ticket even if AC1 passes.
- [x] **AC3 — the inserted section carries its own enforcement label.** Given ADR-0032 D2 applies to
      this entry as to any other, When Block C lands, Then it carries
      `**Enforced by:** reviewer-check 5 "Catalog-edit routing" (.claude/agents/reviewer.md) — T3-HUMAN`
      as specified at ADR-0033 `:335-337`. The governance rule discharges its own rule.
- [x] **AC4 — the living doc stops saying "not in force".** Given
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
- 2026-08-05 — **`done` by architect.** Both blockers cleared; the **severed** block applied. Filing
  this ticket `blocked` was correct and load-bearing — a `ready` row here would have produced the
  literal application that installs the contradiction.

## Review

### AC0 — both blockers, cited

| Blocker | Cleared by | Evidence |
|---|---|---|
| **1 — FT-11 / M3 sequencing** | **T-0549 AC1**, applied **before** this hunk in the same pass | `.claude/agents/reviewer.md` step 5 is **reviewer-check 5 "Catalog-edit routing"**. The `conventions.md` text was written only after the reviewer's page could check it, which is the whole content of CH-2 |
| **2 — L3 / the corrected text** | **T-0553's panel ruling, 2026-08-05** (five instances, author ≠ challenger ≠ lead) | *"**D4/Block C′ SUSTAINED and SEVERED** to its own round with the D1 paragraph excised, CH-G fixed, and the Block D addendum held"*; and, operative for this ticket: *"This severance is what unblocks them: the `conventions.md` repair proceeds on ADR-0033's accepted content; only the *definition* waits"* |

**Neither was assumed.** Blocker 1 was verified by performing it first; blocker 2 by reading the panel's
§Verdict, whose D1 disposition is **REJECTED** — and nothing from that rejected draft's D1 or D2 was
carried into the page.

### AC1 — what landed, and what was deliberately left out

Applied = **Block C′'s skeleton**, with the four modifications the panel ruled:

| # | Ruling | Applied as |
|---|---|---|
| a | the *"What 'governs' means — the conflicting-instance test"* paragraph is **EXCISED**; accepted ADR-0033's own floor wording stands **verbatim** in its place | test 2 now carries ADR-0033 Block C's floor paragraph word for word (*"Floor: first-statement-of-a-form … A floor claimed with no search is not claimed: route it."*). **A deletion and a quotation — no authorship** |
| b | a **visible pointer** that *"governs"* is under repair | a ⚠️ blockquote inside test 2: what is undefined, the two worked hunks that show it going both ways (`patterns-mobile.md:990`/T-0349 determinate; `:520-522`/T-0473 not), the rejected draft by path with its `rejected` status, and **T-0553** as the owed second round |
| c | **CH-G fixed** — the harvest action goes back **inside** its branch | numbered item 2 is now a two-branch fork: *nothing fires → write it into the catalog in the same change*; *something fires → don't write it inline, raise it*. A developer whose edit routes no longer arrives at an unconditional "write it into the catalog" |
| d | the **Block D addendum (N-B) is HELD** | **not applied**, on either page. The reviewer's firing-side burden (*"quote the sentence and name the artifact"*) appears nowhere, and Block C′'s matching half — *"firing the test costs one artifact"*, the D2 clause the panel **HELD** — was cut from the applied text. Only M2's author-side sweep, which accepted ADR-0033 already carries, survives |

**Also carried, from Block C′ verbatim:** the reversal callout, "Inline is not free", the *Not the test*
paragraph, `**Enforced by:**`, and §"Cross-stack claims (ADR-0033 D2)".
**Heading changed by necessity:** Block C′ titled the section *"(ADR-0033, refined by ADR-NNNN)"*. That
refining ADR is `rejected` and **no number was allocated** (dispatch constraint), so the heading reads
**"(ADR-0033)"** — the only accepted authority the section now rests on. Citing a rejected draft as
refining authority would have been worse than the drift it avoided.

### The one deviation, declared rather than absorbed (AC1's "any deviation is re-routed" clause)

**Block C′'s letter:** replace `:120-130` in place, so the replacement (list **and** the new `###`
subsections) lands where the list was, and `:132-134` — the *"earns its place"* bar — follows it.
**What was applied:** the list replacement landed in place; the two `###` subsections landed **after**
the bar paragraph, immediately before §"The price of a law".

**Why, and it is this lane's own subject matter.** The bar sentence — *"a pattern earns a catalog entry
when it would make future changes cheaper"* — governs **every** catalog entry. Placed under
`### Cross-stack claims`, a reader takes its scope from that heading. That is finding **G3** exactly:
*"§'Shared UI & theme' hosts four iOS entries under an Android-worded preamble … one sentence has two
defensible scopes, in the file the routing test is applied to most"*. Installing the same defect in the
edit whose purpose is to stop a page carrying two readings would be indefensible.

**No word of the panel's ruled text changed, and no surviving line was rewritten or reordered relative
to any other surviving line** — the new material was inserted three lines lower than the letter implies.
**If the panel disagrees, this is the hunk to re-route**; it is named here rather than absorbed, per
AC1's instruction.

### AC2 — one page, one axis (the AC L3 exists to protect)

The `:122-127` region **no longer exists to quote**: the numbered list was **replaced**, not appended
to. What stands in its place branches on the routing test's *outcome*, never on "small clarification vs
new canonical archetype":

- item 2, branch 1 — *"**Nothing fires → it is yours.** Edit the relevant `patterns-*.md` /
  `consistency.md` entry in the same change …"*
- item 2, branch 2 — *"**Something fires → it is not yours to ratify.** Raise it via the ticket and let
  the **Architect** rule …"*

and the routing test's item 4 closes the third category the old bullets left out: *"Otherwise → inline.
This covers both a clarification inside an existing rule's scope **and** the first statement of a
canonical form where nothing governed the subject."*

**Limb 1's reversal is stated, not denied** — the standing callout says so in the developer's own page:
*"The old wording sent 'a new canonical archetype' to the Architect on that ground alone. It no longer
does … What changed is the price, not the permission."* ADR-0033's header claims the opposite; the
callout says the ADR carries a dated correction, and it does. **The denial was not propagated into the
page a developer reads.**

`grep -n 'one way to do X' agents/knowledge/conventions.md` → **zero**. No sentence on the page
contradicts the routing test.

### AC3 — the governance rule discharges its own rule

`**Enforced by:** reviewer-check **5 "Catalog-edit routing"** (`.claude/agents/reviewer.md`) —
**T3-HUMAN**`, with its scope stated (*fires on any diff touching `agents/knowledge/*.md`; reads
routing and enforcement label, not content*) — as specified at ADR-0033 `:335-337`.

This is the **only** `**Enforced by:**` label this lane added. Repo count in `agents/knowledge/` goes
**8 → 9** (strict form); `patterns-mobile.md` stays at **0**, unchanged and deliberately so — labelling
the iOS corpus is **FT-4**, a different lane. Nothing in this work implies the label belongs anywhere
new.

### AC4 — the living doc

`catalog-governance.md`: the ⛔ box → an in-force statement with a per-file landing table and the tier
stated as the honest ceiling; the L3 trade-off paragraph → a **resolution table** recording how limb 1
was resolved (replace-not-append, limb 1 reversed and said so, limb 2 survives as test 2, the third
category named, step 3 untouched, CH-G fixed, header corrected by dated closure); L2/L3 and
FT-11/FT-12/FT-8 rows closed; the "gap" table re-measured; a deliberation-history entry recording the
four judgment calls.

### Cited offsets: matched on quoted text, not line numbers

Per §Implementation notes' drift warning, every anchor was located by its **quoted text**. Confirmed
drift in this tree: the living doc's own `:143-154` / `:61-76` / `:111-114` pointers had all moved, and
`conventions.md:132-134` (the bar) is now `:134-136`. No edit was made on a line number.
