# ADR-0033 — Catalog-edit authority: which catalog edits a developer may make inline (the three-test routing rule, with a floor), and at what strength a catalog entry may claim something about a stack the ticket never ran

- **Status:** proposed   <!-- proposed | accepted | superseded | rejected -->
- **Date:** 2026-08-01
- **Supersedes:** —
- **Superseded by:** —
- **Split from:** **ADR-0032**, by that ADR's panel lead, on challenge **C8**
  (`adr/README.md:3` — "one decision per ADR. If you're writing two, split"). ADR-0032's draft carried
  three decisions; two of them are **one** decision and live here, because draft-D6's third routing
  test (*"a prescriptive claim about a stack this ticket did not build and run"*) **is** draft-D7. The
  single decision recorded here is: **what a ticket may write into the catalog by itself, and how
  strong a claim it may make.**
- **Refines:** `agents/knowledge/conventions.md` §"Harvest good patterns back into the catalog"
  (steps 2–3) — it does not reverse that rule, it makes its routing test **decidable** and gives it a
  floor. Consumes **ADR-0032** (the price of a law: a constraining entry names an enforcer and declares
  a tier).
- **Applies to:** cross-cutting (catalog governance; all stacks)
- **Number note:** **0031** is taken by `0031-nswag-regen-drift-is-guarded-at-regen-time.md`, which
  exists only in T-0439's worktree and has not reached `master`; **0032** is the price-of-a-law ADR.
  0033 is allocated by the ADR-0032 panel. A reader on `master` sees a gap at 0031 until T-0439 merges.
- **Ticket:** none — split out of the ADR-0032 panel.

> **Why `proposed` and not `accepted`.** Two of the three parts carry panel consensus already:
> **test 1** was called *objective and unattacked* by the challenger, and **D2 (cross-stack strength)**
> had its structural-vs-behavioural line called *drawn on the right property*. The **floor on test 2**
> (§D1, test 2) is **new text authored by the panel lead** in response to challenge C5, which demanded
> a floor without proposing one. A lead may adjudicate between positions the parties argued; inventing
> the repair and then ratifying it is not adjudication. So this ADR needs **one** challenger round on
> **exactly one item** — the floor — and nothing else is re-opened.
>
> **Nothing regresses meanwhile:** `conventions.md:125-127` already routes "anything that changes 'the
> one way to do X'" to the Architect, unchanged, and ADR-0032 (accepted) already governs what a
> constraining entry must state.

---

## Context

The T-0451 reviewer refused to ratify a `patterns-mobile.md` "the ONE way" entry inline, on the ground
that declaring the one way to do X is an architect call. That routing was **correct** by
`conventions.md:125-127`. But the operative test a reviewer and a developer actually apply today is
`conventions.md` step 2's implicit axis — *"is this a small clarification to an existing rule, or a new
canonical archetype?"* — and that axis is not durable.

**Why "gap vs clarification" does not survive contact.** It measures novelty *relative to the text*,
not cost *imposed on the codebase* — and those come apart in both directions. A gap can be tiny and
additive (a footgun no rule mentions, obliging nobody). A "clarification" can be enormous: sharpening
an existing rule's scope retroactively puts shipped call sites in violation. Two agents applying "is
this new or is this a clarification?" to the same edit will disagree, because the honest answer is
usually "both".

**The verified case that proves it under-routes.** T-0274's inline edit
(`agents/backlog/tickets/T-0274-fe-error-resolver-dedup.md:130-139`) said per-feature
`resolveXxxErrorKey` resolvers *"must delegate … rather than re-implement the walk inline"* and
self-classified as *"Small clarification to an existing rule, not a new archetype."* Its own next
bullet lists **seven** shipped `.models.ts` resolvers that still inline the walk. That edit obliged
seven existing call sites; under the test below it was an Architect call, and the codebase now carries
both forms with no canonicalization ticket — the exact drift `conventions.md` step 3 warns about.
**T-0274 is not re-opened** (it shipped, and the edit was substantively right); it is recorded as the
data point.

**The second half of the same question.** T-0441 (an Android ticket) wrote into `patterns-mobile.md`
that *"iOS mirrors this — its generated models have the same all-optional shape."* The reviewer let it
stand. Was that right? It is a claim about a stack the ticket never built or ran. The answer turns on
the **kind** of claim, not on the stack — which is why it belongs in the same decision as the routing
test rather than beside it.

---

## Decision

### D1 — The routing test: three ordered tests; the first that fires routes the edit to the Architect

Replaces "gap in the ruleset vs clarification to an existing pattern" as the operative test.

1. **Does the edit put any code that exists today in violation?** (After this edit, is a current call
   site a deviation that wasn't one before?) → **Architect**. It implies a `consistency.md` deviation
   entry and a canonicalization ticket, neither of which a developer or a reviewer can file for
   themselves.

2. **Does it *narrow* latitude the catalog previously left open — forbidding an alternative the
   catalog, until now, permitted?** → **Architect**. That is a law, and ADR-0032 prices it.

   **The floor (this is the new text; challenge C5).** Test 2 fires on a **narrowing**, not on the
   **first statement of a canonical form**. Writing down "here is how we do X" where the catalog said
   nothing about X, and where no shipped call site becomes a deviation (test 1 did not fire), is
   **inline** — it removes no option anyone was entitled to rely on. Test 2 fires when the entry
   *withdraws* a form the catalog previously allowed, or replaces a named canonical form with a
   different one.

   **Why a floor is needed at all.** `conventions.md:132` sets the bar for *any* catalog entry at
   "makes the codebase **more consistent**" — which, read literally, means every entry that earns its
   place forbids some less-consistent alternative. Without the floor, test 2 fires on everything, D1
   collapses into "everything goes to the Architect", the inline lane dies, and the harvest loop
   `conventions.md` deliberately opened closes again.

   **The test is semantic, not lexical (challenge C4).** Imperative wording — "the ONE way", "never
   X", "X is a defect", a closing "Deviations a reviewer rejects:" list — is a **prompt** that should
   make a reviewer look; it is not the trigger. An entry rewritten as "the canonical form is X" that
   nonetheless withdraws a permitted alternative fires test 2 all the same. There is no wording that
   launders a narrowing past this test. (Under ADR-0032 the incentive to try is gone anyway: every
   entry constraining call sites names an enforcer + tier **whatever its wording**, so imperative
   phrasing costs nothing and buys nothing.)

3. **Does it make a *prescriptive* claim about a stack this ticket did not build and run?** →
   **Architect** (see D2 for what "prescriptive" means and what the alternative is).

4. Otherwise — it explains, exemplifies, or names a footgun **inside an existing rule's existing
   scope**, and no shipped code becomes a deviation → **inline**, flagged in the ticket's `## Review`
   for the Reviewer's sanity-check (unchanged from `conventions.md` step 2, first bullet).

**Retro-validation against the four real cases** (the evidence that the test is sound, not just tidy):

| Case | T1 (obliges existing code?) | T2 (narrows latitude?) | T3 (foreign stack, prescriptive?) | Routes to | Actual ruling |
|---|---|---|---|---|---|
| **T-0446 / SEC-5** — nothing in S1–S11 covers bytes inside a stored artifact served by URL | **YES** — three shipped pipelines (avatar, order photos, dispute evidence) sanitize nothing | yes | no | **Architect + docs** | Architect + docs (T-0460) ✅ **matches** |
| **T-0441** — "assert the GENERATED command, not the app one" | no — it names existing practice (`BookingApiTest`, `UserRepositoryTest` are the cited models); no shipped call site becomes a deviation | no — it **adds a test obligation** where the catalog said nothing; it withdraws no permitted form | its Android half, no; its iOS half is **descriptive** (D2) | **inline** | inline ✅ **matches** |
| **T-0451** — "Ink on a theme-INVARIANT surface — the ONE way" | no (the two heroes are the ones being fixed) | **YES** — it withdraws `Color.dynamic` ink, which the catalog previously permitted everywhere | no | **Architect** | Architect ✅ **matches** |
| **T-0274** — "resolvers must delegate rather than re-implement the walk" | **YES** — seven shipped `.models.ts` resolvers | yes | no | **Architect** | inline ❌ **the recorded divergence** — the old axis under-routed it |

It reproduces all three actual rulings the PM flagged as nearly inconsistent, **and** explains the one
that went wrong. Note the floor doing real work in row 2: T-0441 stays inline **because** it added an
obligation where none existed rather than withdrawing a permitted form — under the unfloored test 2 it
would have routed to the Architect, contradicting the actual (correct) ruling.

### D2 — Cross-stack claims in a catalog entry: permitted, at exactly two strengths

A catalog entry **may** make a claim about a stack the ticket did not build. The strength must be
legible from the sentence:

- **Descriptive** ("iOS mirrors this — …", "the same shape exists on X"): permitted from **any**
  ticket. Requires (a) a **file:line citation** of the other stack's code **in the entry itself**, not
  only in the ticket's `## Review`, and (b) that it imposes **no obligation** on the other stack (no
  "so iOS must…"). It tells the next reader where to look; it does not bind them. Label it:
  *"Cross-stack note (descriptive — not a rule for X)"*.
- **Prescriptive** (a rule the other stack must follow): **Architect**, and it must be written from —
  or ratified from — a ticket that **built and ran** that stack, or from an ADR.

**The evidence standard that separates them: structural claims may be verified by reading;
behavioural claims require execution.** *"Its generated models have the same all-optional shape"* is
structural — you can read `CreateOrderCommand.swift:15-32` and see it. *"The same mutation would leave
the iOS suite green"* is behavioural — it requires running the iOS suite.

**Two independent reasons a prescriptive claim needs the stack to have been run** (the second matters
because the first weakened under ADR-0032's amendment):
1. **Evidence.** A behavioural claim about a stack you never executed is unverified by construction.
2. **Price.** ADR-0032 requires a constraining entry to name an enforcer on **that** stack at a
   declared tier. A ticket that never ran that stack's build cannot honestly name — let alone verify
   the coverage of — an enforcer there. (Under ADR-0032 as amended the enforcer may be a T3-HUMAN
   checklist item, which is cheaper than a gate; reason 1 is therefore the load-bearing one.)

**Applied to the flagged case:** T-0441's sentence *"iOS mirrors this — its generated models have the
same all-optional shape"* is **structural, verified true, and carries no obligation**. The reviewer's
call to let it stand as descriptive was **correct**. The only thing missing is the file:line in the
entry (Block B).

**The stability question the author raised, answered.** "Every property is optional" is structural
*because the generated models are committed*. If a stack's generated client ever stopped being
committed, the same sentence would become unverifiable-by-reading — and the line would correctly
re-classify it as behavioural, because the reader can no longer check it without running codegen. The
line is drawn on **"can the next reader verify this by reading what is in the repo?"**, which is the
property that actually matters to a catalog reader, and it *should* move when that property moves.
That is the line behaving correctly, not silently changing strength.

---

## Exact catalog text, and who applies it

**On acceptance**, and not before.

### Block B — replaces the closing sentence of the T-0441 entry in `agents/knowledge/patterns-mobile.md`

**Applier: the T-0440 developer, when `patterns-mobile.md` reaches it in the lane**
(`INDEX.md:177` live lane: T-0441 ✅ → T-0440). T-0440 is the iOS-side ticket, so it is the ticket that
will have *run* the stack — which is what D2 requires for anything stronger than a citation, and it is
where T-0441's `## Review` already routed the promotion decision. **T-0440's standing instruction not
to re-harvest is unchanged: this is a two-line edit to an existing sentence, specified here by the
architect, not a harvest.** Lane discipline per `shared-file-lanes.md` rules 2–3: own hunk only, no
`git restore`, **no `git stash`** (the stash is repo-global across worktrees).

Replace:

```markdown
> by deleting a mapper line: if nothing goes red, the test is one hop short. iOS mirrors this — its
> generated models have the same all-optional shape.
```

with:

```markdown
> by deleting a mapper line: if nothing goes red, the test is one hop short.
> **Cross-stack note (descriptive — not a rule for iOS):** iOS's generated models carry the same
> all-optional shape (`CleansiaCustomerApi/Models/CreateOrderCommand.swift:15-32`, every property
> optional), so the same blind spot exists there. That is a *structural* observation made from an
> Android ticket; turning it into an iOS test obligation takes an iOS ticket that ran the iOS build.
```

### Block C — `agents/knowledge/conventions.md`, §"Harvest good patterns back into the catalog"

**Applier: the architect, on acceptance** (`conventions.md` is lane-uncontended). Insert after the
existing numbered list, **below** the "The price of a law" section already added by ADR-0032:

```markdown
### Which of those two lanes you are in — the routing test (ADR-0033)

Apply in order. The **first** one that fires routes the edit to the **Architect**; if none fires, edit
inline and flag it in the ticket's `## Review`.

1. **Does the edit put code that exists today in violation?** If any current call site becomes a
   deviation it wasn't before, it needs a `consistency.md` deviation entry and a canonicalization
   ticket — neither of which a developer or a reviewer can file for themselves.
2. **Does it *narrow* latitude the catalog previously left open?** Withdrawing a form the catalog
   permitted, or replacing a named canonical form with a different one, is a **law** — and laws are
   priced (see "The price of a law"). **Floor:** writing down a canonical form where the catalog said
   nothing, and where no shipped call site becomes a deviation, is **not** a narrowing — it is inline.
   The test is **semantic**: "the canonical form is X" narrows exactly as much as "the ONE way is X".
   Imperative wording is a prompt to look, not the trigger.
3. **Does it make a prescriptive claim about a stack this ticket did not build and run?** A rule for a
   stack you never executed is not yours to declare.

*Not* the test: "is this a gap in the rules or a clarification to them?" That measures novelty
relative to the text rather than cost imposed on the codebase, and the two come apart in both
directions — a gap can oblige nobody, and a "clarification" that sharpens an existing rule's scope can
retroactively put dozens of shipped call sites in violation.

### Cross-stack claims (ADR-0033)

A catalog entry may reference a stack the ticket did not build, at exactly two strengths, and the
strength must be legible from the sentence:

- **Descriptive** — permitted from any ticket. Needs a **file:line citation of that stack's code in the
  entry itself** (not only in the ticket's `## Review`), and must impose **no** obligation on that
  stack. Label it: *"Cross-stack note (descriptive — not a rule for X)"*.
- **Prescriptive** — Architect, and it must come from a ticket that **built and ran** that stack (or
  from an ADR).

The line between them is the evidence: **structural claims may be verified by reading** ("every
property on the generated model is optional"); **behavioural claims require execution** ("the same
mutation leaves that suite green"). The operative question is *can the next reader verify this by
reading what is in the repo?* — if that stops being true for a claim, the claim has become behavioural.
```

---

## Alternatives considered

**A. Keep "gap vs clarification" (the status quo axis).** *Rejected.* It measures the wrong thing
(novelty relative to the text, not cost imposed on the codebase), and it demonstrably under-routes:
T-0274 self-classified as a clarification while obliging seven shipped call sites.

**B. Route every catalog edit to the Architect.** *Rejected.* It closes the harvest loop
`conventions.md` deliberately opened, makes the catalog a bottleneck, and loses the single best moment
to write a rule — while the developer still holds the context. The inline lane exists on purpose.

**C. Let the developer decide and have the Reviewer catch it.** *Rejected.* This is the status quo, and
two agents deriving the axis fresh already nearly ruled inconsistently on the same question — the
definition of a rule that needs writing down once.

**D. Trigger on wording alone ("the ONE way" / "never" / "is a defect").** *Rejected — it is exactly
what challenge C4 attacks.* A mechanical wording trigger is cheap to check and trivial to launder:
"the canonical form is X" imposes the identical constraint and dodges it. Wording is kept as a
**prompt** for the reviewer; the test is the semantic one. (What made this affordable to reject is
ADR-0032: because *every* constraining entry declares a tier regardless of wording, nothing is gained
by softening the phrasing.)

**E. Unfloored test 2 ("forbids an alternative a competent developer could reasonably choose").**
*Rejected — it is the author's original wording, and it has no floor.* `conventions.md:132` sets the
catalog-entry bar at "makes the codebase more consistent", which *is* forbidding the inconsistent
alternative, so the unfloored test fires on nearly every entry that earns its place. Retro-validation
row 2 shows it concretely: it would route T-0441 to the Architect, contradicting the correct ruling.

**F. Fold cross-stack strength into a separate ADR.** *Rejected.* Draft-D6's test 3 **is** draft-D7 —
splitting them would leave test 3 pointing at an ADR whose only content is the definition of its own
trigger. One decision: what a ticket may write into the catalog, and how strongly.

---

## Consequences

**Cheaper / safer**
- The routing question is decided once, in three ordered tests that reproduce all three correct
  rulings and explain the one that went wrong — instead of being re-derived per ticket by whoever is
  holding the diff.
- The inline harvest lane **survives** with a stated floor, so a developer who finds a better idiom
  can still write it down in the moment.
- A cross-stack observation stops being either forbidden or silently binding: it is a **cited,
  labelled, non-obligating** note, or it is an Architect call.

**More expensive (new obligations)**
- A developer must ask "does this narrow something?" rather than "is this new?" — a slightly harder
  question, deliberately, because it is the one that predicts cost.
- Every descriptive cross-stack note carries a **file:line in the entry**, not just in the ticket.

**What could go wrong (state it plainly)**
- **"Nothing was permitted before" as an escape hatch.** An author can claim the catalog was silent on
  X when a nearby rule plainly covered X, and keep a narrowing inline. The Reviewer's check is to name
  the *previously permitted form* the edit withdraws; if one exists, test 2 fired. Mitigated, not
  eliminated — this is the floor's own soft edge and the item the re-check round should press on.
- **Test 1 requires knowing the call sites.** "Does any shipped code become a deviation?" is only as
  good as the author's sweep. A grep in the ticket's `## Review` is the expected evidence.

---

## How a reviewer verifies compliance

On any ticket whose diff touches `agents/knowledge/*.md`:

1. **Run the three tests against the hunk.** If any fires and there is no ADR, the edit is a finding:
   the content may be right, but the ticket may not ratify it — route to the Architect.
2. **Test 1 evidence.** For a hunk that constrains anything, the ticket names what it swept and what it
   found (a grep, a file list). "No existing violations" with no sweep is not an answer.
3. **Test 2 floor.** If the author claims the floor (first statement of a form, not a narrowing), ask
   for the **previously permitted alternative** the entry withdraws. If one can be named, test 2 fired.
4. **Test 2 is semantic.** A hunk with no imperative wording that nonetheless withdraws a permitted
   form still fires. Do not check for the phrase; check for the withdrawal.
5. **Cross-stack claims.** A descriptive claim carries a **file:line in the entry** and imposes no
   obligation ("so X must…" is prescriptive). A prescriptive claim comes from a ticket that ran that
   stack, or an ADR.
6. **Lane.** The hunk was applied in the ticket's own worktree, touching only its own hunk. No
   `git restore` of a shared catalog file; **no `git stash`**.

---

## Roles affected

No new code roles. **Reviewer** gains the six-point check above (it composes with ADR-0032's check on
the same hunk). **Architect** receives the routed edits. The living companion
`agents/architecture/decisions/catalog-governance.md` carries both rules and the current shape.

---

## Follow-up tickets — specs, not files

| # | Title | Layers / size | Panel? | Sequencing |
|---|---|---|---|---|
| **FT-8** | **Apply ADR-0033's catalog text** — Block C into `conventions.md` (below ADR-0032's "price of a law" section). `conventions.md` is lane-uncontended. | architect + docs, **XS** | no | **after** this ADR is accepted. |
| **FT-9** | **Block B** — the T-0441 cross-stack sentence gains its file:line citation + descriptive label, applied by T-0440 in the `patterns-mobile.md` lane. | ios, **XS** | no | after acceptance, when the lane reaches T-0440. |
| **FT-10** | **(PM scheduling call, not an architect ruling)** — decide whether the **seven** `.models.ts` resolvers T-0274 left inlining the error-key walk get a canonicalization ticket. This ADR only records that the edit was mis-routed under the old axis; whether to chase the call sites is scheduling. | frontend, **S** | no | PM's call. |

---

## What this ADR does **NOT** decide

- **It does not decide what a constraining entry must state about its enforcement** — that is
  **ADR-0032** (named enforcer + declared tier + coverage).
- **It does not re-open T-0274**, T-0441's reviewer verdict, T-0446's SEC-5 routing, ADR-0018, or
  ADR-0032's accepted amendments.
- **It does not change the Reviewer's authority.** A reviewer may still reject a catalog hunk on
  content; this ADR only fixes *which* hunks a ticket may ratify for itself.
- **It does not touch `consistency.md`, `security-rules.md`, `INDEX.md`, `quality-gates.md`, or any
  `patterns-*.md` file** from the architect's hand. Lane-held; specified here, applied by the holder.

---

## Challenge

<!-- CHALLENGER: one round, on ONE item. -->

**Open item — the only thing this ADR needs attacked: the floor on test 2 (§D1 test 2).**

It is lead-authored text, written in response to challenge C5 against ADR-0032's draft ("the semantic
test fires on nearly everything — where is the floor?"). C5 named the problem and did not propose the
repair; the repair below is therefore **unchallenged**, not consensus:

> Test 2 fires on a **narrowing** — withdrawing a form the catalog previously permitted, or replacing a
> named canonical form — and **not** on the first statement of a canonical form where the catalog was
> silent and no shipped call site becomes a deviation.

The three lines of attack the lead considers most likely to land:

1. **Is "previously permitted" decidable?** The catalog's silence is not the same as permission. If
   "the catalog said nothing about X" is always arguable, the floor is an escape hatch, and test 2 is
   as under-routing as the "clarification" axis it replaces. (§Consequences names this as the floor's
   soft edge; is the reviewer's "name the withdrawn form" check enough to close it?)
2. **Does the floor contradict test 1?** If a first-statement-of-a-form is inline whenever no shipped
   call site violates it, then the catalog can acquire canonical forms with no Architect involvement at
   all — which is arguably the *right* answer (nothing is obliged, the harvest loop stays open) or
   arguably how "the one way to do X" gets redefined by whoever ships first, which
   `conventions.md:125-127` exists to prevent.
3. **Is the retro-validation honest, or fitted?** Row 2 (T-0441) is the only row the floor changes. One
   row is thin evidence for a floor that governs every future catalog edit. Is there a real case where
   the floor gets it *wrong* — a first-statement-of-a-form that plainly should have been an Architect
   call?

**Explicitly NOT open (carried consensus in the ADR-0032 panel; do not re-litigate):** test 1 ("puts
existing code in violation") — called objective and unattacked; **D2**'s structural-vs-behavioural line
— called drawn on the right property; the rejection of the wording-only trigger (Alternative D); and
the split itself (ADR-0032 verdict C8).

## Defense

<!-- AUTHOR (the architect who takes this ADR forward): to be written after the round. -->

## Verdict

<!-- LEAD (a different instance): not yet convened. This ADR is `proposed`. -->

**Not accepted.** Two of three parts carry consensus from the ADR-0032 panel; the floor on test 2 has
had exactly zero adversarial rounds and is the sole reason this is not `accepted` today. Until it is,
`conventions.md:125-127` governs routing unchanged and **Block B / Block C are not applied**.
