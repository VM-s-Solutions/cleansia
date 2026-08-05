# ADR-NNNN (DRAFT — number NOT allocated) — What makes a catalog sentence *govern* an entry: the conflicting-instance test, and the corrected `conventions.md` edit

- **Status:** ⛔ **`rejected` as a single decision — panel closed 2026-08-05. NOT an ADR; no number
  allocated; nothing here may be cited by a ticket.** **D1 (the conflicting-instance test) is REJECTED**
  on a blocking defect the panel found in it (§Verdict, **V-1**: the definition is an existential and its
  own retro table scores six of ten rows with a *compose* test — there is no single reading of D1 under
  which its ten rows come out as recorded). **D2 is HELD** — sound, and meaningless until its currency is
  defined. **D3 is UNBEATEN, NOT SETTLED.** **D4 / Block C′ is SUSTAINED and SEVERED** to its own round,
  with the D1 paragraph excised. **This file stays on disk as the record of what was tried and why it
  failed** — the next author starts from §Verdict's R-1…R-7, not from a blank page.
- **Date:** 2026-08-05 (drafted; challenged; **ruled 2026-08-05 by an independent fifth instance**)
- **Number:** **not allocated on purpose.** ADR-0033's independent lead pass left allocation to the PM,
  and two architects collided on 0041 this sprint by both grepping `adr/` correctly at the same moment.
  Highest on disk today is **0042**. The PM allocates when the panel closes; the file is renamed then.
- **Refines / partially supersedes:** **ADR-0033** — D1 test 2's floor (adds the missing predicate) and
  **Block C** (replaces it). It does **not** re-open ADR-0033's D1 test 1, test 3, D2, M2's evidence
  rule, or Block D. ADR-0033 is `accepted`; on acceptance of this ADR, a **dated appended section**
  (`adr/README.md` §1, form 1 — partial supersede) goes on ADR-0033 pointing here. **No in-body edit to
  ADR-0033 is proposed or permitted** — see D4, which is about a false statement ADR-0033 makes about
  itself and still does not qualify for the erratum lane.
- **Consumes:** ADR-0032 (a constraining entry names an enforcer and declares a tier).
- **Applies to:** cross-cutting (catalog governance; all stacks)
- **Ticket:** the L1/L3/F4 panel routed by ADR-0033's independent lead pass, 2026-08-05.
- **Panel:** author = this instance (did not write ADR-0033, did not challenge it, did not adjudicate
  it). Challenger and lead are separate instances. `deliberation.md` §"The roles".

> ## ⛔ Method declaration, up front, because the last two rounds each got burned by one
>
> **This invocation has NO shell.** The task brief stated I had one; I do not — my tool set is
> `Read` / `Write` / `Edit` / `Glob` / `Grep`, with **no `Bash`**. So, exactly as with the lead pass
> before me: **no `git log -p`, no `git show`, and not one catalog edit was read as a diff.** The
> corpus in §Retro-validation is built from (a) the entry text as it stands in the tree today, (b) the
> ticket that the entry is tagged with, read in full where the routing mattered, and (c) candidate
> governing sentences located by `Grep` over `agents/knowledge/`. That is **strictly more evidence than
> the lead pass had** (it read two entries; I ran ten, and I read the candidate governing sentence in
> each) and **strictly less than the brief asked for**. Every place where a diff would have changed my
> confidence is marked **⚠ diff-blind** in the table and enumerated in §"What this draft could not
> verify". A challenger with a shell should re-run §Retro-validation first; it is the part most likely
> to move.
>
> **One consequence is immediate and it is not cosmetic:** the single case that motivates this whole
> ADR (the lead's Case β) may not be a historical case at all. See §Context, "The founding case has a
> chronology problem".

---

## Context

ADR-0033 D1 test 2 fires when **a catalog sentence already governs the subject of this entry at any
level of generality** and the entry carves an exception out of it, replaces it, or forbids a form it
named. Amendment **M1** defined the *negative* — silence is *no sentence at any level of generality* —
and closed the sub-case dodge with it. **M1 never defined the positive.** Nothing in ADR-0033 says what
makes a sentence *govern*.

The independent lead pass (ADR-0033 §"Ruling 1") demonstrated the consequence on a real entry and
routed it as **L1**: on `patterns-mobile.md:265-276` (T-0473), two reviewers reach opposite verdicts
from the same recorded evidence, and **both are following the ADR to the letter**. One cites
`patterns-mobile.md:520-522` — *"a screen with no test seam gets a **source-text scan scoped to the
file**"* — against the entry's *"not a whole-file `contains`"* and fires test 2. The other reads `:520`
as prescribing for a different subject, finds it yields nothing here, and routes inline.

That lead **nominated** a disambiguator and deliberately did not adopt it:

> *"does the candidate sentence, applied to **this entry's** subject, yield a prescription the entry
> contradicts?"*

This ADR treats that as **one candidate among four** and does not adopt it either — for a reason the
lead did not see from inside its own nomination, given in Alternative A.

### The founding case has a chronology problem, and I cannot close it without a diff

`patterns-mobile.md:517-525` — the entry that holds the candidate governing sentence `:520-522` —
**cites T-0473 inside itself**:

> `:522-524` — *"**plus a call-site pin**, because a resolver test does not cover the call site (**the
> T-0473 rule**): assert the card still calls `orderStatusLabel(…)` …"*

So at least part of that entry **post-dates** T-0473. A sentence can only govern an entry if it was in
the catalog when the entry was written — that is not a rule anyone needs to state, it is what "already
governs" means. Two possibilities, and `Grep` cannot separate them:

1. `:517-522` predates T-0473 and only the `:522-525` clause was added by/after it → the lead's Case β
   is a real historical case and L1's evidence stands as filed.
2. The whole `:517-525` entry post-dates T-0473 → **`:520-522` was never a candidate governing sentence
   for T-0473 at all**, and Case β is not a historical mis-routing. It remains a perfectly valid
   *hypothetical* about the catalog as it reads **today** — a reviewer routing a T-0473-shaped entry
   tomorrow hits exactly the indeterminacy described.

**L1 survives either way** — one reproducible indeterminacy on the current text is a counter-example,
and the lead's own Gate-0.5 declaration already limited the claim to "reconstructed, not diffed". But
the evidence is **weaker than §Ruling 1 presents it**, and a reader should know that before weighing
this ADR's cost. `git log -p -- agents/knowledge/patterns-mobile.md` settles it in one command.
**⚠ diff-blind. Flagged, not papered over.**

### Why "define it later" is not available

`conventions.md:132` sets the bar for *any* catalog entry at "makes the codebase **more consistent**".
Read literally, every entry that earns its place forbids a less-consistent alternative — so an
undefined "governs" resolves, under pressure, toward **fires on everything**, which is C5's reductio
and kills the inline lane; or toward **fires on nothing**, which is the T-0274 under-routing this whole
line of ADRs exists to stop. An undefined predicate in an ordered test does not stay neutral; it drifts
to whichever pole the reader's incentive points at, and the author's incentive points at inline.

---

## Decision

### D1 — "Governs" is the **conflicting-instance test**

> **A catalog sentence `S` governs entry `E` iff a reader can name ONE CONCRETE ARTIFACT — a call site,
> a declaration, a test, a file — such that `S` and `E` both reach it, and `S`'s verdict on it differs
> from `E`'s verdict on it.**
>
> - **"Reach"** = the artifact falls inside the scope `S` prescribes for, and inside the scope `E`
>   prescribes for.
> - **"Verdict"** = *compliant* / *defect* / *this exact required shape*.
> - **The artifact need not exist in the tree.** Whether it exists is **test 1**'s question and test 1
>   has already been asked. It must be **nameable**: point at a file that exists, or write down in one
>   line the shape of a file someone could commit tomorrow.
> - **If no such artifact can be named, `S` does not govern `E`** — however close their vocabulary,
>   however general `S` is, however plausible the paraphrase.

Test 2 then reads, unchanged in structure and now decidable: *a sentence governs this entry's subject
(D1) and the entry carves an exception out of it, replaces it, or forbids a form it named* → **Architect**.

**What this buys that a semantic definition does not.** The disagreement in Case β is not about what
`:520` *says* — both reviewers quote it identically. It is about what its **subject** is. Any
definition phrased as *"applied to this entry's subject…"* (Alternative A) relocates the indeterminacy
from "what does the sentence mean" to "what is the subject", and subject-granularity is exactly the
thing two competent readers pick differently. **An artifact has no granularity problem.** Either you
can point at the file where the two rules disagree, or you cannot.

Worked, on the case that motivated the ADR: `:520-522` prescribes a **file-scoped source-text scan**;
`:265-276` prescribes a **block-scoped source-text assertion, not a whole-file `contains`**. Name the
artifact where they disagree. The obvious candidate is the model test `:524` itself names —
`OrderDetailCardStringsTest` — and on that artifact the two rules **compose**: it carries a file-scoped
literal scan *and* a call-site pin, in one file, satisfying both. No artifact is ruled differently
⇒ **`:520` does not govern ⇒ inline.** Determinate, and determinate against a file a reviewer opens
rather than a paraphrase a reviewer writes.

### D2 — Firing test 2 costs an **artifact**; claiming the floor still costs a **search**. The default stays `route`.

M2 put a catalog sweep on the author and made missing evidence route to the Architect. It is the one
amendment the independent lead sustained without qualification, precisely because it changed what a
reviewer can *do*. **D2 is the symmetric half, on the reviewer's side:**

| Party | What they owe | If they don't |
|---|---|---|
| **Author claiming the floor** | the catalog file(s) + term searched, and what it returned, in `## Review` (**M2, unchanged**) | the floor is not claimed → **route** |
| **Reviewer firing test 2** | the **quoted sentence** *and* the **named artifact** where the two verdicts differ | test 2 has not fired on that sentence — say so and move on |
| **Author answering a named artifact** | either show the two prescriptions **compose** on it (one file satisfies both), or concede | unresolved → **route** |

**Naming a candidate sentence does not fire test 2.** That is the operative change, and it is the only
thing standing between the amended floor and a one-way ratchet: today a reviewer can fire test 2 by
asserting any topically-adjacent sentence, and the author has nothing to answer with. Case β is that
ratchet caught in the act.

**Route-by-default is preserved and is doing different work on each side.** On the author's side it
prevents self-certification. On the reviewer's side it means a *substantive* disagreement about one
named file still routes — the reviewer never has to win the argument, only to make it concrete.

### D3 — **F4: no fourth test.** The "trade-off ⇒ ADR" question belongs to the Architect, after routing, not to the developer, before it.

**I accept the framing that F4 belongs in this decision** — it was filed as a candidate fourth limb of
*this* ordered list, closing it elsewhere would leave D1 pointing at an open question about its own
completeness, and a reader deciding "is my list of tests complete?" is deciding one thing. **I reject
its substance**, on evidence I went and gathered rather than on preference. Three grounds:

**(a) The evidence cited for F4 proves the opposite reading of it.** F4's strongest citation is
`T-0397-…md:70` — *"carries a real trade-off — should it be an ADR, not a catalog row? Ruling: no
trade-off survives"*. Read where it sits: that is the **Architect**, asking the question **after** the
edit had already been routed, and answering it. It establishes that "catalog row or ADR?" is a real
question **the Architect answers**; it does not establish a ground on which a **developer or reviewer
routes**. D1's three tests all answer *who ratifies*. F4 answers *what form the record takes*. Those
are different questions and only the first is a routing test.

**(b) Both operationalizations I could construct fail against the record.** I tried to build a trigger
a developer could actually apply, and tested each:

| Candidate trigger for test 4 | Verdict | Why |
|---|---|---|
| *"the entry picks between two forms that both ship in the repo today"* | **subsumed** | if both ship and the entry canonicalizes one, the loser's call sites become deviations — **test 1 has already fired**. A test that never fires alone is not a test. |
| *"the entry states a **cost** of the form it chooses (not merely a defect of the rejected one)"* | **contradicted by the record** | it fires on `patterns-mobile.md:559-561` (T-0448: *"memory-only removes the … question rather than answering it, **at the cost of one small refetch per cold start**"*) which history harvested **inline**; and it does **not** fire on either of ADR-0033's two admitted divergences (T-0397 row 1 and T-0379's `format: date` row are both defect-framed, no stated cost), which history routed to the **Architect**. It gets the record wrong in both directions. |

**(c) It does not close the residual it was hoped to close.** I checked the tempting argument — that a
fourth limb would cover CH-4's greenfield weakness (on a stack still being written, first statements
route inline and whoever ships first sets the form). It does not: a first statement on greenfield
usually has **no competitor to price**, so a trade-off limb is silent exactly there. The accepted answer
to the greenfield residual remains ADR-0032's price attaching to the inline lane plus reviewer-check 5,
and this ADR does not improve it. Stated, not hidden.

**What F4 gets instead of a test.** The question is **recorded where it is actually asked** — in the
Architect's procedure for a routed edit, as one line: *before ratifying a routed catalog entry, ask
whether the rejected forms are **defects** (catalog row) or **live options with real costs on both
sides** (ADR). `T-0397-…md:70` is the worked precedent and its answer was "row".* Applier and file are
in §Follow-ups. **This is the weakest limb of this draft and I am flagging it as such**: a challenger
who can build a trigger that survives table (b) has beaten it, and should.

### D4 — Block C **replaces** `conventions.md:120-130`; it does not append beside it. And yes, this reverses limb 1.

The defect (L3) verified independently in this tree: `conventions.md:125-127` is a **disjunction** —

> *"a **new canonical archetype** **or** anything that changes 'the one way to do X' across the
> codebase → this is an **Architect** call … don't unilaterally redefine the standard."*

— and ADR-0033's floor routes a first statement of a canonical form **inline**. A first statement of a
canonical form *is* a new canonical archetype. ADR-0033's own retro row 7 is the proof: T-0379's
`format: date` row routes inline there and was routed to the Architect in fact, **on the ground that it
"defines the one way for date-only wire on iOS"** — limb 1, used, verbatim. ADR-0033 Block C says
*"Insert after the existing numbered list"* and never touches `:122-127`. Applied literally, one page
would instruct both things.

**Three findings the L3 filing did not carry, which change what the repair has to be:**

1. **Bullet 1 is wrong too, not just bullet 2.** `:122-124` describes the inline lane as *"a **small
   clarification/addition to an existing rule** (a better example, a sharper 'why', a newly observed
   footgun)"*. The floor sends inline a **first statement of a canonical form where no sentence governs
   the subject** — which is not a clarification to an existing rule, because there is no existing rule.
   So the floor does not merely reverse limb 1 of bullet 2; it opens a **third category that neither
   bullet describes**. Appending is not just contradictory, it is incomplete.
2. **ADR-0033's own D1 test 4 mis-cites itself.** It says its inline lane is *"unchanged from
   `conventions.md` step 2, first bullet"*. It is not — for the reason in (1). This is not fatal to
   ADR-0033 (the floor is stated elsewhere, in full) but it is why an editor reading Block C in good
   faith would append rather than replace: Block C's own text tells them nothing is being displaced.
3. **`:128-130` (step 3) survives untouched** and must not be swept up: *"If the new pattern supersedes
   an old one, mark the old form as a deviation in `consistency.md` (and file the canonicalization
   follow-up)"* is consistent with test 1 and is the machinery test 1 exists to trigger.

**The corrected block is §Block C′ below.** It **replaces `conventions.md:120-130` in full** (numbered
items 1–3 of "Harvest good patterns back into the catalog"), leaving `:113-119` (the section preamble)
and `:132-134` (the "earns its place" bar) standing, and inserting nothing between `:185` and `:187`
(`## Naming (canonical)`) — i.e. the replacement lands **in place of** the list, not after the
"price of a law" section as ADR-0033 specified.

**And the header claim is false.** ADR-0033's **Refines:** line says it *"does **not reverse** that
rule"*. It does reverse limb 1 of it. Saying so plainly is a change to what ADR-0033 claims about
itself, so — per the brief and per `adr/README.md` — **it is stated here, plainly:**

> **ADR-0033's floor reverses `conventions.md:125-127`'s first limb.** A new canonical archetype, where
> no sentence governs the subject at any level of generality, is **inline** with an enforcer + tier, not
> an Architect call. That is a deliberate and defensible choice — it is the whole content of the floor,
> it was challenged (CH-4, nominated line 2) and sustained in part on evidence — but ADR-0033's header
> denies making it.

**This does NOT qualify for the erratum lane.** `adr/README.md:16-26` allows an in-body annotation only
for a **transcription error** where "no decision content changes". A characterization of a rule's
relationship to the rule it refines is meaning, not digits — *"the erratum lane is for digits, not
meaning"*. So the correction rides the **dated appended section** (form 1) that this ADR's acceptance
puts on ADR-0033, and nothing in ADR-0033's body is edited. **I flag one honest doubt:** a reviewer
could argue the header claim is so plainly contradicted by the ADR's own retro row 7 that it is closer
to a transcription slip than a decision. I do not think so — the ADR *argues* the claim (§Consequences:
*"`conventions.md:125-127`'s concern is *changing* the one way"*), and an argued claim is meaning.
A challenger is entitled to press this.

---

## Block C′ — the corrected `conventions.md` edit (replaces ADR-0033 Block C)

**Applier: the architect, as the re-scoped FT-8.** `conventions.md` is lane-uncontended.
**Sequencing unchanged from ADR-0033 (M3/CH-2): behind FT-11.** A `conventions.md` section aimed at the
*author*, while the *reviewer* still holds the superseded instruction, changes which rule is quotable
and not which rule is run. **§Follow-ups proposes landing FT-11 and FT-8 in one commit** to close the
window where the two pages disagree in the other direction; the PM owns that call.

**Operation: REPLACE `agents/knowledge/conventions.md` lines 120–130** — numbered items 1, 2 (both
bullets) and 3 of "Harvest good patterns back into the catalog" — with the text below. `:113-119`
(preamble) and `:132-134` (the "earns its place" bar) are **untouched**. Nothing is inserted after the
"The price of a law" section; **ADR-0033's stated insertion point is superseded by this block.**

```markdown
1. **Apply it** in the change you're making.
2. **Decide who ratifies it** — the routing test below. Most edits are yours to make; some are not.
3. **Write it into the catalog** so it becomes the canonical form everyone follows next time, and note
   it in the ticket's `## Review` so the Reviewer sanity-checks it.
4. If the new pattern **supersedes** an old one, mark the old form as a deviation in `consistency.md`
   (and file the canonicalization follow-up) so the codebase converges instead of carrying both.

### Who ratifies a catalog edit — the routing test (ADR-0033, refined by ADR-NNNN)

Apply in order. The **first** one that fires routes the edit to the **Architect** — the content may be
right; what you may not do is ratify it for yourself. If none fires, edit inline.

1. **Does the edit put code that exists today in violation?** If any current call site becomes a
   deviation it wasn't before, it needs a `consistency.md` deviation entry and a canonicalization
   ticket — neither of which a developer or a reviewer can file for themselves. **Name the sweep you
   ran** (a grep, a file list) in `## Review`; "no existing violations" with no sweep is not an answer.
2. **Does it *narrow* latitude the catalog previously left open?** It narrows when **a catalog sentence
   already governs this entry's subject** and the entry carves an exception out of it, replaces it, or
   forbids a form it named. That is a **law**, and laws are priced (see "The price of a law").

   **What "governs" means — the conflicting-instance test.** A sentence `S` governs your entry `E` when
   you can name **one concrete artifact** — a call site, a declaration, a test, a file — that **both
   `S` and `E` reach**, and on which **`S`'s verdict differs from `E`'s** (compliant / defect /
   required-shape). The artifact need not exist yet; it must be **nameable**. **If no artifact can be
   named, `S` does not govern `E`**, however close the vocabulary.

   **Floor — the first statement of a canonical form is inline.** Where **no** sentence governs the
   subject, you are adding a rule, not withdrawing one. *"The catalog was silent about X" means no
   sentence governs X — not that no sentence names X specifically.* A rule stated about the general
   case governs its sub-cases, so carving out a sub-case narrows it whether or not the sub-case was
   ever named. Picking a level of description at which the catalog happens to be silent
   ("theme-invariant surfaces") is not silence if you can name the file where the general rule and
   yours disagree.

   **Claiming the floor costs one grep; firing the test costs one artifact.** The **author** records,
   in `## Review`, the catalog file(s) and the term searched and what it returned — **a floor claimed
   with no search is not claimed: route it.** A **reviewer** who says a sentence governs **quotes the
   sentence and names the artifact**; a sentence alone has not fired the test. The author then shows
   the two prescriptions **compose** on that artifact (one file can satisfy both) or concedes.
   **Unresolved either way ⇒ route.**

   The test is **semantic, not lexical**: "the canonical form is X" narrows exactly as much as "the ONE
   way is X". Imperative wording is a prompt to look, not the trigger.
3. **Does it make a prescriptive claim about a stack this ticket did not build and run?** A rule for a
   stack you never executed is not yours to declare. A **descriptive** cross-stack note is fine from any
   ticket — see "Cross-stack claims" below.
4. Otherwise → **inline.** This covers both a clarification inside an existing rule's scope *and* the
   first statement of a canonical form where nothing governed the subject.

> **This is a deliberate reversal, and it is the one thing to know if you remember this page from
> before.** The old wording sent *"a new canonical archetype"* to the Architect on that ground alone.
> It no longer does: a first statement that obliges no shipped call site and withdraws no governing
> sentence is **yours to write**, in the moment you hold the context — which is the best moment there
> is to write it. What changed is the price, not the permission.

**Inline is not free.** An entry that clears the floor has a **zero baseline by construction** (test 1
did not fire), which is exactly the second condition "The price of a law" puts on `T1-CI`. So if the
form is mechanically expressible on its stack, the inline ticket **ships the gate with the entry**.
Where the only mechanizer available cannot fail a build — `check-consistency.mjs` (in **zero**
`.github/` workflows) or an ESLint rule under `frontend-ci.yml`'s `continue-on-error: true` lint step —
the honest token is `T2-ADVISORY` and the entry says what would promote it.
`patterns-frontend.md:462-465` is the model.

*Not* the test: "is this a gap in the rules or a clarification to them?" That measures novelty relative
to the text rather than cost imposed on the codebase, and the two come apart in both directions — a gap
can oblige nobody, and a "clarification" that sharpens an existing rule's scope can retroactively put
dozens of shipped call sites in violation.

**Enforced by:** reviewer-check **5 "Catalog-edit routing"** (`.claude/agents/reviewer.md`) —
**T3-HUMAN**. Scope: it fires on any diff touching `agents/knowledge/*.md`; it does not read the
entry's content, only its routing and its enforcement label.

### Cross-stack claims (ADR-0033 D2)

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

**One addition to reviewer-check 5 (Block D), for FT-11 to carry.** Block D's test 2 bullet currently
ends at *"if there is none, the floor is not claimed — route it"*. Append one sentence:

```markdown
   **And the reverse: if YOU are the one saying a sentence governs, quote it and name the artifact** —
   the call site, declaration, test or file that both sentences reach and rule differently. A sentence
   alone has not fired test 2; that is how a plausible-but-adjacent rule ends up routing an edit that
   withdraws nothing.
```

---

## Retro-validation — ten real catalog entries, run against D1

*Every row: the entry as it stands in the tree, the strongest candidate governing sentence I could find
by `Grep` over `agents/knowledge/`, and the artifact (or its absence). **"Actual"** is from the entry's
ticket where I read it, and from ADR-0033's own table where that is the source.* **⚠ diff-blind** marks
a row where a `git log -p` could change the answer.

| # | Entry | Strongest candidate `S` | Artifact where verdicts differ | Governs? | Routes | Actual | |
|---|---|---|---|---|---|---|---|
| 1 | **T-0441** `:181-191` "assert the GENERATED command" | `:167-175` normalize a business-key 400 at the repository | none — `UserRepositoryTest` is reached by both and **satisfies both** (a real-ProblemDetails repo test *and* generated-client mocking live in one file) | **no** | inline | inline | ✅ |
| 2 | **T-0451** `:292-304` ink on a theme-invariant surface | `:588` — `CleansiaColors` slots are the same Material slot names **as `Color.dynamic(light:dark:)`** | **`CleansiaColors.onFixedWhite`** itself: `:588` says every slot is a dynamic pair; the new slot is deliberately **not** one | **yes** | **Architect** | Architect | ✅ |
| 3 | **T-0473** `:265-276` (the L1 case) | `:520-522` "a screen with no test seam gets a source-text scan **scoped to the file**" | none — **`OrderDetailCardStringsTest`** (`:524`, the model the sentence itself names) carries a file-scoped literal scan **and** a call-site pin in one file; they compose | **no** | inline | inline (F3: recorded, not re-opened) | ✅ **now determinate** |
| 4 | **T-0349** `:1249-1257` address-picker VM | `:990` "the §7.6 D1 … seam — **feature/VM import no MapKit**" | the app-local `AddressPickerView` carrying `import MapKit`: `:990` = defect, entry = *"that View touch **is allowed**"* | **yes** | **Architect** | Architect (`T-0349` is `owner: architect`) | ✅ |
| 5 | **T-0397 row 2** `:996-1001` no fixed `.medium` detent | `:1241` (pre-erratum) "the code dialogs are native `.sheet`s … `.presentationDetents([.medium])`" | **`CodeSheetShell.swift:29,36`** — one says `.medium`, the other says self-sizing | **yes** | **Architect** | Architect | ✅ |
| 6 | **T-0397 row 1** full-bleed header idiom | none nameable | — | **no** | inline | Architect | ⚠️ divergence — **unchanged from ADR-0033 row 5** |
| 7 | **T-0379** `format: date` ridden as plain `Date` | none nameable | — | **no** | inline | Architect | ⚠️ divergence — **unchanged from ADR-0033 row 7** |
| 8 | **T-0527** `:477-492` a server-charged price is never estimated client-side | nearest are `:468-475` (a conditional list is a pure resolver) and `:505-515` (enum ordinal → resource id) — neither reaches a price | none | **no** | inline | inline (`T-0527-…md:286` *"Harvested back into the catalog"*) | ✅ |
| 9 | **T-0449** `:320-329` "the guard is **released by a successful render**" | `:562-565` (T-0448, Android) *"refetch the profile once, **guarded by the `fileName` already retried**"* | an Android `ProfileViewModel` whose retry watermark is set once and **never cleared**: `:562-565` rules it compliant, the entry rules it *"drops the avatar to initials permanently"* | **yes** | **Architect** | inline (`T-0449-…md:316` *"**Harvested** into `patterns-mobile.md`"*) | ❌ **NEW divergence — found by this pass** |
| 10 | **T-0432** `:255-263` iOS `CleansiaDangerButton` | `:249-253` (Android) *"never duplicate a `:core` component"* | none — `:249-253` reaches `cz.cleansia.core.ui.components`, the entry reaches `CleansiaCore/Components`; no artifact is inside both | **no** | inline | inline (the entry is live and priced `(gate pending: FT-5)`) | ✅ ⚠ history not verified from the ticket |

**Score, stated the way the brief asked for it: 10 cases run, 10 determinate.** Every row produced a
verdict backed by a **named artifact** or by a stated, searched absence — including the one case
ADR-0033's own record says is **indeterminate** (row 3). Agreement with history: **7 match**, **2 are
ADR-0033's already-owned divergences** (rows 6, 7 — D1 does not move them and does not claim to), and
**1 is a new divergence D1 creates** (row 9).

### Row 9 is the honest cost, and it is not small

D1 routes T-0449's harvest to the Architect; the ticket harvested it inline. The conflict is real and I
can name it: `:562-565` describes a once-per-`fileName` retry guard with no release, and the T-0449
paragraph makes never-releasing it a defect — *"a 'once per key' guard that is never re-armed drops the
avatar to initials permanently — and every suite stays green"*. Same artifact, opposite verdicts.

Three things follow, and a challenger should press all three:

1. **This is D1 working, not D1 failing.** Row 9 is a *narrowing* that shipped inline: an entry that
   withdrew a form the catalog described, on the other platform, without an Architect. That is precisely
   the class ADR-0033 test 2 exists to catch, and neither the accepted floor nor the lead's nominee
   catches it (both would ask whether `:562-565` "governs the subject", and the subject can be
   paraphrased as *iOS avatar caching* vs *SAS-backed image retry* to taste).
2. **It probably fires test 1 as well, and I could not check.** If any Android `ProfileViewModel`
   lacked `onAvatarLoadSucceeded` when the entry landed, test 1 fires and the routing question never
   reaches test 2. The entry names `Android onAvatarLoadSucceeded` as existing, but a name in an entry
   is not a sweep. **⚠ diff-blind.**
3. **The same entry independently fires test 3, today, unfixed.** T-0449 is an iOS ticket; the entry
   says *"**Both platforms** plumb the pair through **every** surface that draws the disc"* — an
   obligation on Android — and cites `Android onAvatarLoadSucceeded` with **no file:line**. Under
   ADR-0033 D2 that is a prescriptive cross-stack claim from a ticket that did not run that stack, and
   it is a **second live instance** of exactly what Block B was written to fix on the T-0441 sentence.
   **Filed as finding N1**; not repaired here, because this ADR writes no `patterns-mobile.md` hunk.

### What D1 does *not* fix

Rows 6 and 7 stay divergent. D1 is a definition of "governs"; where **no** sentence governs, there is
nothing to define, and the floor sends a first statement inline by design. Anyone who wants rows 6 and
7 to route must argue for a **fourth test** — which is F4, which D3 rejects on the evidence in table
(b). **If a challenger beats D3, rows 6 and 7 are the prize.** I want that argued rather than assumed.

---

## Alternatives considered

**A. The lead's nominee — *"does the candidate sentence, applied to **this entry's** subject, yield a
prescription the entry contradicts?"*** *Rejected as the operative sentence; absorbed as an
explanation.* It is right about the shape of the question and it does not close it. Two reasons:

1. **It relocates the indeterminacy instead of removing it.** In Case β both reviewers quote `:520`
   identically; they differ on whether the entry's subject is *"how a source-text assertion is scoped"*
   or *"which colour role a screen hands a component"*. A definition that begins *"applied to this
   entry's subject"* hands the disagreement its own premise. The lead diagnosed the disease precisely —
   *"the routing verdict is carried by the reviewer's **paraphrase**"* — and the prescription still
   requires a paraphrase, of the subject rather than of the sentence.
2. **It is circular against the trigger it feeds.** Test 2 already fires on *carves an exception /
   replaces / forbids*, which is contradiction. Defining "governs" as "yields a prescription the entry
   **contradicts**" collapses test 2 to "the entry contradicts a prior sentence" — which is not wrong,
   but it deletes the distinction between *narrowing* and *conflicting* that M1 spent a whole round
   installing, and it gives a reviewer nothing new to check.

D1 keeps its content (a governing sentence must actually *reach* the entry's case) and pays for it in
the currency this project has already shown works: a named, openable artifact. **D1's clause
*"`S`'s verdict differs from `E`'s verdict on that artifact"* is Alternative A, instantiated.**

**B. Define nothing; make it purely procedural — "the floor holds unless the reviewer objects; on any
disagreement, route."** *Rejected.* M2 already installed route-by-default and it is sound, but a pure
default with no predicate converts every plausible-but-adjacent sentence into a successful objection.
Case β *is* that: Reviewer A names a sentence, cannot be answered, and the edit routes. Applied
generally it restores C5's reductio from the other side — the inline lane dies not because test 2 is
too broad but because nobody can refute an assertion. **A default needs something to default *from*.**

**C. Define "governs" lexically — "`S` governs `E` iff `S` names `E`'s subject term."** *Rejected — it is
Alternative H of ADR-0033 (the topic-level reading) wearing a different hat, and it was already
rejected there by an accepted ADR.* Every sub-case is a fresh term if you may choose the vocabulary;
T-0451 escapes as "theme-invariant surfaces". Re-recorded here only so the next reader sees it was
re-tested and re-fails.

**D. Require the artifact to *exist in the tree*.** *Rejected.* It makes test 2 a strict subset of test
1, which is ordered ahead of it — test 2 would then never fire alone. Worse, it fails on the exact
shape a real narrowing takes: an edit that **converts its own violators in the same change** (ADR-0033
retro row 3; Case α/row 4 here) has a zero baseline *and* is a genuine withdrawal. The artifact must be
allowed to be writable-but-unwritten. **The cost is stated in §Consequences: that is where the residual
judgment now lives.**

**E. Fold F4 in as a fourth test.** *Rejected on evidence — see D3(b).* Both constructible triggers
fail: "two live forms" is subsumed by test 1; "the entry states a cost" is contradicted by the record in
both directions. **This is the alternative most likely to be re-argued and it deserves to be.**

**F. Split L1 and F4 into two ADRs.** *Rejected, but narrowly.* `adr/README.md` ("one decision per ADR")
points this way, and there is a real cost to bundling: a challenger who wants to kill D3 can hold up
D1, which is the blocking half. I keep them together because **F4 was filed as a question about *this
list's completeness***, and a reader asking "are three tests enough?" is asking one question with the
reader asking "when does test 2 fire?". Answering only the second leaves D1 ratified next to an open
question about whether the list it belongs to is the right list. **If the lead disagrees, the clean
split is: D1+D2+D4 here, D3 to its own round** — D1 does not depend on D3, and this draft is written so
that excision costs one section.

**G. Do nothing; let reviewer-check 5 land (FT-11) and see whether the indeterminacy bites in practice.**
*Rejected, with sympathy.* It is the cheapest option and the evidence base for L1 is thinner than the
lead pass presents it (§Context, the chronology problem). But FT-11 lands a check that instructs a
reviewer to *run test 2*, and test 2's predicate is the undefined one — so the deliberate act of making
the rule enforceable is exactly the moment the undefined predicate starts routing real work. Shipping
an enforcer for an undefined test is the ADR-0032 D3 failure mode in a new costume.

---

## Consequences

**Cheaper / safer**
- **The one recorded indeterminacy becomes determinate** (retro row 3), and for a reason a reviewer can
  check by opening a file rather than by out-arguing a colleague.
- **Test 2 stops being assertable.** Firing it costs an artifact, which is the same trade M2 made on the
  author's side and which the independent lead sustained without qualification. The floor is no longer
  a one-way ratchet in either direction.
- **A real narrowing that shipped inline is now catchable** — retro row 9 is one, found by running the
  definition rather than by reading tickets.
- **`conventions.md` stops teaching two rules at once.** Block C′ replaces rather than appends, so the
  page a developer reads and the check a reviewer runs assert the same thing.
- **The reversal is stated where developers read it**, in the callout inside Block C′, instead of being
  denied in an ADR header and discovered by the third reader.

**More expensive (new obligations)**
- A reviewer who wants to route an edit must **name the artifact**, not just the sentence. That is real
  work and it will sometimes mean an edit the reviewer distrusts goes inline.
- An author claiming the floor still owes the M2 grep, **and** now owes an answer on the artifact when
  one is named.
- The Architect gains one line of procedure on every routed edit (D3's catalog-row-vs-ADR question).

**What could go wrong — plainly**
- **The residual judgment moved; it did not vanish.** It now sits in *"is the artifact nameable?"* An
  author who cannot imagine the conflicting file writes "none nameable" and routes inline. That is a
  narrower and more checkable failure than "what is the subject" — a reviewer can supply the artifact
  the author missed, which they could not do with a paraphrase — but it is not zero. **This is D1's soft
  edge and it is the first place a challenger should push.**
- **Sentences that are vague enough to reach everything.** A catalog sentence like *"never duplicate a
  `:core` component"* reaches a very large artifact set, so a conflicting instance is easy to find and
  test 2 fires often against it. Whether that is correct (broad rules deserve to be defended before
  being carved) or over-firing, I do not know. **No case in my corpus tested it; I am not claiming it is
  fine.**
- **Row 9's class may be common.** If cross-platform harvests routinely narrow the *other* platform's
  shipped entry, D1 routes a lot of mobile harvests to the Architect and the inline lane narrows on the
  stack that harvests most. One case is not a rate. **The measurement a shell would give — how many of
  the 41 tickets touching `patterns-mobile.md` carry a hunk that conflicts with a prior entry — is the
  number that decides whether this is a repair or a tax, and I could not take it.**
- **The greenfield residual is untouched.** CH-4's finding stands exactly as ADR-0033 recorded it: on a
  stack still being written, first statements route inline and whoever ships first sets the form. D1
  does not improve it and D3 declines the limb that was hoped to. What holds that line remains
  ADR-0032's price plus reviewer-check 5.
- **D3 could simply be wrong.** I killed both triggers I could build; someone may build a third. The
  cost of being wrong here is the two divergent rows staying divergent, which is the status quo.

---

## How a reviewer verifies compliance

Adds to ADR-0033's list; does not replace it.

1. **Test 2, firing side.** If you are routing an edit on the ground that a sentence governs it:
   **quote the sentence** and **name the artifact** — the call site, declaration, test or file that both
   sentences reach and rule differently. If you cannot name one, test 2 has not fired on that sentence.
2. **Test 2, floor side (M2, unchanged).** The author records the catalog file(s) + term searched.
   No search ⇒ the floor is not claimed ⇒ route.
3. **The compose answer.** Where an artifact is named, the author may answer that the two prescriptions
   **compose** on it — one file can satisfy both, and they say which file. If that is contested and
   unresolved, **route**.
4. **The artifact may be hypothetical, but it must be concrete.** "Some future call site" is not an
   artifact. "A Compose screen that has no test harness and hands a bare `contentColor:`" is.
5. **Catalog page parity.** After FT-8 lands, `conventions.md` carries the routing test **once**. A diff
   that re-adds a "new canonical archetype → Architect" bullet beside it is the L3 defect returning.
6. Everything in ADR-0033 §"How a reviewer verifies compliance" items 1–7 still applies, unchanged.

---

## Roles affected

No new code roles. **Reviewer** — reviewer-check 5 gains one sentence (§Block C′, the Block D addendum).
**Architect** — gains one line of procedure on a routed edit (D3). The living companion
`agents/architecture/decisions/catalog-governance.md` carries the current shape and is updated when this
is accepted.

---

## Follow-up tickets — specs, not files

| # | Title | Layers / size | Sequencing |
|---|---|---|---|
| **N-A** | **Re-scope FT-8 to Block C′** — REPLACE `conventions.md:120-130`, do not append. **Supersedes ADR-0033's Block C.** Carries its own `**Enforced by:**` line. | architect + docs, **XS** | behind FT-11 |
| **N-B** | **One sentence into Block D / FT-11** — the reviewer's firing-side burden (quote the sentence *and* name the artifact). | architect + docs, **XS** | **with FT-11** |
| **N-C** | **PM scheduling call: land FT-11 and FT-8 in one commit.** Different files, no lane contention, both XS. It removes the window in which the reviewer's page and the author's page disagree in the *new* direction, and it removes one of the two ways the "FT-11 has no ticket" failure repeats. Not an architect ruling. | PM | before either lands |
| **N-D** | **D3's line into the Architect's routed-edit procedure** — *"before ratifying a routed catalog entry, ask whether the rejected forms are defects (row) or live options with costs on both sides (ADR); `T-0397-…md:70` is the worked precedent, answer 'row'."* Target file is the architect charter or `process/` — **PM/lead's call which**, since I may not edit `.claude/agents/` from this draft. | architect + docs, **XS** | after acceptance |
| **N-E** | **N1 — `patterns-mobile.md:320-329` (T-0449) carries a prescriptive cross-stack claim with no file:line** (*"Both platforms plumb the pair through every surface"*; `Android onAvatarLoadSucceeded` uncited). Same defect Block B fixes on the T-0441 sentence, second instance. **Recorded, not re-opened** (T-0274/T-0473 precedent) — but it is evidence that ADR-0033 D2 is as unenforced as ADR-0032's label was. | ios/android lane | with FT-9 if convenient |
| **N-F** | **The measurement this round could not take.** With a shell: `git log -p -- agents/knowledge/patterns-*.md agents/knowledge/consistency.md`, and for each hunk that adds a constraining sentence, record whether a conflicting instance against a prior sentence is nameable. That converts §Retro-validation from ten reconstructed cases into a rate. **It also settles §Context's chronology question in one command.** | architect, **S** | any time; it can only strengthen or break this ADR |

---

## What this ADR does **NOT** decide

- **It does not re-open** ADR-0033 D1 tests 1 or 3, D2, M2, M4, Block B, or Block D's existing content.
- **It does not change ADR-0033's status.** ADR-0033 stays `accepted`; this is a partial supersede of
  its Block C plus a refinement of test 2's predicate, recorded as a dated appended section on it.
- **It does not fix L2.** FT-11 still has no ticket and ADR-0033 is still `(guidance — no gate)` in fact.
  That is a PM filing action, not a decision, and it is unchanged by this ADR — **and this ADR is
  worthless until it lands**, since it refines the predicate of a test nobody is instructed to run.
- **It does not add a fourth routing test** (D3) and it does not re-open ADR-0033's retro rows 5 and 7,
  which stay divergent.
- **It writes no catalog file.** Block C′ is a specification; the applier is named.
- **It does not repair `patterns-mobile.md:320-329`** (N1/N-E) — recorded, routed, not touched.

---

## Challenge

*Empty by construction — this is the author's draft. The challenger instance files here.*

**The four places I would attack first if I were the challenger, named so silence on them reads as a
choice** (`deliberation.md`: *"a challenger that finds nothing says so explicitly and names what they
checked"*):

1. **D3 (no fourth test) is the weakest limb.** I killed two triggers; build a third. If it survives
   table (b) — fires on retro rows 6 and 7, does *not* fire on `patterns-mobile.md:559-561` — it beats
   D3 outright and the two standing divergences close.
2. **"Nameable but not existing" is D1's soft edge.** Show me a real entry where the conflicting
   artifact is easy to imagine for one reviewer and invisible to another, and D1 is Case β again one
   level down.
3. **The corpus is ten reconstructed cases with no diffs, and one of them (row 3) rests on a sentence
   whose chronology I could not establish.** A challenger with a shell should run N-F before defending
   or attacking anything else in §Retro-validation.
4. **Row 9 may be a rate, not a case.** If cross-platform harvests routinely conflict with the other
   platform's entry, D1 taxes the stack that harvests most. I could not measure it; if you can, that
   number is the real verdict on this ADR.

**Explicitly NOT open (carried by accepted ADR-0033; do not re-litigate):** test 1; test 3; D2's
structural-vs-behavioural line; M2's evidence rule and its route-by-default; M4's ADR-0032 composition;
the rejection of a wording-only trigger; the rejection of the topic-level reading of silence.

## Defense

*Empty — awaiting the challenger round.*

## Verdict

> **Lead: a fifth instance** — did not write ADR-0033, did not run its challenger round, did not write
> its independent lead pass, did not author this draft, did not write the challenge ruled on below.
> `deliberation.md` §"The roles". **AC1 (T-0553) is SATISFIED on composition: author ≠ challenger ≠ lead.**
>
> **Method.** No `Bash` (charter limitation). The substitute is the coordinator-generated
> **catalog-edit corpus** — every commit touching `agents/knowledge/*.md` with full diffs — which is the
> evidence all four prior instances declared they lacked. **Every commit, hunk header and diff line cited
> below I read in that corpus myself**; I did not take the challenger's attributions on trust, and one of
> them is corrected against the challenger (CH-D).
>
> **No `## Defense` was filed** — the author round did not run before this ruling. That is recorded, not
> smoothed over (the ADR-0032 panel has the same precedent). It changes nothing for CH-A and CH-B: a
> defense cannot restore a sentence a diff shows deleted, nor conjure an artifact a shipped test excludes.
> It *does* mean the author is entitled to answer the **new** finding below (V-1) in its own round.

**Consensus: NOT reached. Three blocking challenges stand, and a fourth defect — worse than any of
them — was found by this pass. D1 is REJECTED. The draft does not become an ADR as filed.**

| # | Disposition | One line |
|---|---|---|
| **V-1** *(new, this pass)* | **BLOCKING — decisive** | D1 is an **existential** over artifacts; the retro table scores six of ten rows with a **compose** (universal) test. There is **no single reading of D1 under which its own ten rows come out as recorded.** |
| **CH-A** | **SUSTAINED — blocking** | Row 8 falsified from the diff. The method searched the **post-edit** tree; **six** of ten rows (not four) return a negative produced that way. |
| **CH-B** | **SUSTAINED — blocking, in a stronger form than filed** | Row 9, D1's only claimed catch, is a false positive — *and* it is the row that proves the two readings of D1 give opposite answers. |
| **CH-C** | **SUSTAINED — blocking** | "Reach" is the subject question. Verified from the diff that placed the row-10 entry under the Android preamble. |
| **CH-D** | **SUSTAINED IN PART; one claim corrected against the challenger** | Clustering is real (rows 3/9 = one commit, verified). *"Rows 6 and 7 are one architect sitting"* is **wrong**: two tickets, and row 7 was routed **2026-07-04**, sixteen days before it was ratified. |
| **CH-E** | **SUSTAINED** | Verified: `2012b014`'s added paragraph names *"(the T-0473 rule)"* and `OrderDetailCardStringsTest` in one breath. Row 3 is non-historical **and** non-discriminating. |
| **CH-F** | **SUSTAINED** (correction) | N1/N-E re-scoped to the *"every surface that draws the disc"* clause. |
| **CH-G** | **SUSTAINED** (drafting) | Verified against `conventions.md:120-130`: today the action sits **inside** each branch; Block C′ hoists it out. |
| **D3** | **UNBEATEN, NOT SETTLED** | Eight triggers built and killed (two by the author, six by the challenger, two more by me). Ground (a) struck as a censored-sample inference. It re-opens automatically when the corpus is rebuilt. |
| **D4 / Block C′** | **FINDING SUSTAINED — SEVERED and carried forward; the drafted text BLOCKED** | Every fact re-verified in this tree. It does not depend on D1 and must not die with it. |

---

### V-1 — the finding that decides this round: **D1 and its retro-validation are not the same test**

D1 states an **existential**:

> *"`S` governs `E` **iff a reader can name ONE CONCRETE ARTIFACT** … such that `S` and `E` both reach
> it, and `S`'s verdict on it **differs** from `E`'s. … **If no such artifact can be named**, `S` does not
> govern `E`."*

Its negation is universal: *every* artifact both reach is ruled the same. Exhibiting **one** artifact that
satisfies both does not establish it. Now read how the table discharges its negatives:

- **Row 1** — *"none — `UserRepositoryTest` is reached by both and **satisfies both**"*
- **Row 3** — *"none — `OrderDetailCardStringsTest` … carries a file-scoped literal scan **and** a
  call-site pin in one file; **they compose**"*

Both answer *"∃ an artifact satisfying both"*. That is not the negation of *"∃ an artifact ruled
differently"*; the two are compatible. **Rows 1 and 3 are scored under a test D1 does not state.** And the
substitution is not cosmetic — it flips them:

> On **row 1**, a repository test that normalizes a business-key 400 at the repository (`:167-175`
> satisfied) *and* asserts the **app** command on the request side (T-0441's `:181-191` violated) is
> nameable in one line. Verdicts differ ⇒ under D1 as written, `:167-175` **governs** ⇒ **Architect**.
> **Accepted ADR-0033's retro row 2 rules that same edit `inline` and calls it a ✅ match with history.**

So D1 as written contradicts an accepted ADR's own worked case, on the row this draft reproduces as
agreeing with it. That is not thin evidence. That is a wrong predicate.

**And the escape is closed on both sides.** Suppose we rescue D1 by reading "reach" narrowly — `:167-175`
prescribes about *repository error normalization*, so it does not reach a *request-side assertion* even in
a file it also reaches. Then rows 1 and 3 hold — and **row 9 dies**, because on the conduct the T-0448
sentence actually summarizes, `ProfileViewModel.kt` both retries-guarded and releases, so the two
prescriptions compose and nothing is carved. Row 9 is the draft's **only** claimed catch.

| Reading of D1 | Rows 1, 3 (the negatives that agree with history) | Row 9 (the only new catch) | Cost |
|---|---|---|---|
| **Existential + file-scoped** (D1's literal text) | **flip to Architect** — contradicts accepted ADR-0033 | fires | C5's reductio: any summary sentence yields a differing instance |
| **Compose + conduct-scoped** (the table's actual practice) | hold | **does not fire** | the draft's sole positive result disappears |

**Ruled: this is the same defect M1 removed from the word "permitted", relocated onto the word "governs"
— a predicate that is true and false of the same edit.** The T-0471 challenger's CH-1 killed the floor's
first wording for exactly this ("the predicate was true and false of the same edit"). A repair that
reproduces it one word to the right cannot be accepted.

**This finding is independent of CH-A and CH-B.** It survives a perfect corpus. Even if every row had
been derived from the pre-edit catalog and row 9's artifact existed, the ten rows would still not be
evidence *for D1*, because they were not produced *by* D1.

---

### The question the panel was convened to answer: **is the definition wrong, or was only its evidence bad?**

Two readings were put to me. **The first is right, and the second is refuted — but neither is quite the
diagnosis.**

**Reading 2 ("sound definition, bad evidence") is refuted by V-1.** Losing a false positive would leave a
rule under-evidenced. Discovering that the rule, applied literally, returns *the opposite verdict* on
rows the author scored as matches — on a row whose commit is a clean insertion with a genuinely
predating candidate sentence, i.e. a row **CH-A's blindness does not touch** — is a refutation of the
rule, not of its sampling. More corpus cannot fix a predicate that has two readings and needs both.

**Reading 1 ("it fires on every precision-adding refinement") is correct and incomplete.** CH-B's
mechanism is real: catalog sentences are summaries, summaries under-specify, so a refinement always has a
nameable differing instance. But CH-B's remedy — carve out refinements — would not save D1, because
**row 1 is not a refinement of anything.** `:167-175` and T-0441's entry are different subjects; the flip
there comes from the quantifier and the scope of "reach", not from refinement. Excluding refinements
would leave D1 firing on unrelated sentences that happen to share a file.

**The third reading, which is the one I rule:** the failure is neither the evidence nor the refinement
case. It is that **D1 replaced a semantic judgement with a syntactic-sounding one and did not close either
of the two degrees of freedom it introduced** — the quantifier (∃ vs ∀) and the granularity of "reach"
(file vs conduct). The draft's central claim over Alternative A is *"an artifact has no granularity
problem"*. True, and irrelevant: **the artifact is not the thing being judged — the reach relation is**,
and reach is defined as *"inside the scope `S` prescribes for"*, which is `S`'s subject with a new name.
CH-C proved that inside the draft's own table; V-1 proves the quantifier half. Alternative A was rejected
for relocating the indeterminacy; **D1 relocates it twice and hides it in a clause that reads like
plumbing.**

**What would have distinguished the two readings, stated so the next round does not re-derive it:** run
the definition *as written* against the rows scored negative. If D1's literal text reproduces the table,
the rule is sound and the corpus is thin. If it does not, the rule is wrong. I ran it. It does not.

---

### CH-A — SUSTAINED. And the corpus question, answered from the diffs.

**Verified myself**, not taken from the challenge: `ab077504` (2026-08-04) carries **three** hunks in
`patterns-mobile.md`. The third, `@@ -1272,9 +1315,9 @@`, deletes

```diff
-  Cancel is a modal `.sheet` previewing the fee/refund via a pure TDD'd
-  `CancellationFeePreview` (oops≤15m/free≥24h/half 4–24h/full<4h, the `CancelOrderSheet.kt` tiers; server recomputes
-  authoritatively).
+  Cancel is a modal `.sheet` rendering the **server's** quote … the client-side tier ladder both platforms shipped is deleted
```

The edit's **own replacement text** says the shipped ladder *"is deleted"*. Under the **accepted** floor's
decidable disjunct — *"replaces it, or forbids a form it named"* — this fires without needing D1 at all.
Routing in fact: `owner: qa`, `adrs: []`, harvested inline. **Row 8 is an Architect ⇄ inline divergence,
not a ✅ match.**

**I tried to break it.** The best available defense is that the deleted clause is a *Gate-DP inventory
line* describing what the customer read cluster shipped, not a prescription — descriptive text does not
govern. It fails three ways: (i) it **names a canonical form** (`CancellationFeePreview`) and its tier
ladder, which is the disjunct's exact trigger; (ii) the replacement characterizes the change as a
deletion of a shipped form, in the author's own words; (iii) T-0527 deleted `CancellationFeePreview.swift`
and **rewrote a committed test suite that pinned the old schedule**. A sentence whose withdrawal requires
deleting a file and rewriting its tests is not a description.

**How many of the ten rows are affected — the number the brief asked for.** The challenger said four
(rows 6, 7, 8, 10, quoting the draft's own Gate-0.5 item 5). **It is six.** Rows **1** and **3** also
return "does not govern", and they were located by the identical grep of today's tree; the draft excluded
them from its own list because it named a candidate sentence, which is not the same as having searched the
right snapshot. Verified per row, from the diffs:

| Row | Entry / commit | Hunk shape | Negative? | State after this pass |
|---|---|---|---|---|
| 1 | T-0441 · `1d85b35f` 2026-08-01 | pure insertion, one hunk | **yes** (compose) | **flips under D1's literal text** (V-1). Not falsified by CH-A; falsified by the definition |
| 2 | T-0451 · `1c8fdd00` 2026-08-01 | pure insertion | no (positive) | **stands** — grep can only miss, never invent |
| 3 | T-0473 · `0e4ede1b` 2026-08-01 | pure insertion | **yes** (compose) | **vacuous as history** — the candidate `:520-522` landed `2012b014` 2026-08-02, a day later (CH-E) |
| 4 | T-0349 · `04f98937` 2026-06-30 | insertion; `:990` from `76fc48ab` 2026-06-27 | no (positive) | **stands.** The strongest row in the corpus; I did not dent it either |
| 5 | T-0397 `.medium` · added `365fd221` 2026-07-11, ratified `f0e39d7e` 2026-07-20 | insertion + append | no (positive) | **stands** |
| 6 | T-0397 header · added `365fd221` 2026-07-11, ratified `f0e39d7e` | **modification** of a pre-existing cell | **yes** | **unfalsified, unreliable** — see below |
| 7 | T-0379 `format: date` · `e97b14e7` 2026-07-05 | insertion, **never touched since** | **yes** | **unfalsified, unreliable** |
| 8 | T-0527 · `ab077504` 2026-08-04 | **insert ×2 + REPLACE** | **yes** | **FALSIFIED** |
| 9 | T-0449 · `0e4ede1b` + `4f81dce7` 2026-08-05 | pure insertions | no (positive) | **FALSIFIED** (CH-B), and it is V-1's proof case |
| 10 | T-0432 · `4d8b3978` 2026-07-22 | insertion under the Android preamble | **yes** | **contested** (CH-C) |

**And the blindness is wider than "deletions".** `f0e39d7e`'s `@@ -313,12 +313,12 @@` shows that row 6's
"entry" is a **modification**: the developer's cell landed 2026-07-11 (`365fd221`) and the T-0397
ratification **appended** the fix-round-8 settle pin plus its signature. Today's tree shows only the
merged result, so the draft read a two-party, two-date artifact as one entry. Grepping the post-edit tree
cannot see **the pre-edit text of any modified sentence**, not merely deleted ones. That is a strictly
larger hole than CH-A filed, and it is the hole rows 6 and 7 sit in.

**A second, independent instance of CH-A's class, found in the same commit as rows 5/6/7.** `f0e39d7e`
`@@ -342,20 +354,24 @@` **replaces the whole** *"iOS shell navigation — the ONE way (ADR-0022)"* entry,
deleting a *"Deviations a reviewer rejects"* clause that named **"a shell bar built as a stock
`TabView`/`.tabItem` bar"** as a rejection, and installing the opposite mandate. Invisible to any grep of
today's tree. **I am not booking this as a mis-routing** — `T-0379-…md:94-100` shows it was an
architect-run sweep following an *owner* supersede of ADR-0022, which is correctly routed. It is a
**method** datum: the highest-signal instances of test 2 are exactly the ones this procedure erases.

**Ruled: the corpus must be REBUILT, not repaired.** Three reasons, in order of force:
1. **V-1** — the scoring test was not D1. Every row must be re-scored under whatever predicate replaces
   it, so no row's verdict transfers.
2. **CH-A** — the search step ran against the wrong snapshot. Six of ten negatives must be re-derived
   against the catalog **as of each hunk's parent commit**.
3. **The modified-hunk hole** — the *entry text* itself is wrong for any row whose hunk was a
   modification. That is a change to the generating procedure, not a correction to an output.

What survives verification and may be carried into the rebuild without re-derivation: **rows 2, 4, 5** as
positives (grep's error is one-sided), and CH-D's chronologies, which I re-checked and confirm.

**The pattern worth naming, because the brief asked for it.** The draft's Gate-0.5 leg 3 item 5 states
this limitation *verbatim* — *"A sentence deleted since would not appear"* — and then the table prints
*"10 cases run, 10 determinate"* with no discount. **Declaring a limitation is not weighting it.** This is
now the third round in a row where a declared blind spot was allowed to stand next to an undiscounted
headline number (T-0471's four-row table; the lead pass's two reconstructed cases; this table's ten). A
declaration that does not move the conclusion is a disclaimer, not a method. **I am adding it to the
repair's acceptance bar (R-6 below): a stated limitation must appear in the score, or the score is not
reported.**

---

### CH-B — SUSTAINED, and enlarged into V-1's proof case

The coordinator verified `ProfileViewModel.kt:179-180` (`fun onAvatarLoadSucceeded() { avatarRetriedFor =
null }`) and `ProfileViewModelTest.kt:635` (*"a successful load restores the retry budget"*); I take those
as established and did not re-run them. **What I add from the diffs:** `4f81dce7`'s appended text cites
Android's method **as an existing model** — *"(`ProfileViewModel.avatarLoadSucceeded` / Android
`onAvatarLoadSucceeded`)"* — so the entry itself asserts that nothing was withdrawn from Android. And
`4f81dce7` is a **single hunk** that never touches `:562-565`, confirming the challenger's G2: the Android
paragraph is a stale summary of its own shipped code, not a rule the iOS entry narrowed.

**I tried to break it** with the draft's own Alternative D reasoning: the artifact *need not exist*, so a
never-clearing VM is nameable whether or not Android shipped one, and row 9 fires regardless. That defense
is available — **and it is precisely the existential reading**, which V-1 shows flips rows 1 and 3 against
accepted ADR-0033. It saves row 9 only by paying C5's price in full. Not a defense; a demonstration.

**Row 9's real value is diagnostic, and the draft had it exactly backwards.** §Consequences says *"Row 9
is D1 working, not D1 failing."* Ruled: **row 9 is D1 being two tests at once.** It is the one row where
the existential and compose readings give opposite answers, on evidence both readings can cite. That is
what makes it the best row in the corpus — as a counter-example.

---

### CH-C — SUSTAINED

Verified from the diff rather than from the challenge: `4d8b3978` (2026-07-22) inserted the T-0432
blockquote **immediately below** the context line *"Never style raw components one-off; **never duplicate
a `:core` component**"*, and the same entry names its own conflicting artifact — *"partner
`ProfileHubContent`'s hand-rolled copy is the **remaining convergence target**"* — which
`catalog-governance.md:264` already books as `(gate pending: FT-5)`.

**I tried to break it.** The narrow reading is genuinely available: `:249-253` opens *"Use
`cz.cleansia.core.ui.components.*`"* and names Android packages, so a reader can scope it to Android and
name nothing. **That does not defeat the finding**, because the finding is that **two competent readings
exist on today's text** — which is the identical standard ADR-0033's independent lead pass used to
establish L1 (Case β: *"Reviewer B is right, and the ADR does not compel it"*). A draft may not claim that
standard for the defect it repairs and refuse it for the defect it introduces.

The second instance the challenger flagged as *"arguable, not established"* (`:468-475` vs T-0527's
resolver) I leave **unruled** — it is not needed, and it is arguable in the way the challenger says.

---

### CH-D — SUSTAINED IN PART; **one claim corrected against the challenger**

**Confirmed from the corpus:** rows 3 and 9 **and** row 9's governing sentence (T-0448's `:562-565`) are
**one commit**, `0e4ede1b` 2026-08-01, three tickets' harvests in one hunk set. Rows 1, 2, 4 chronologies
sound, as the challenger reported.

**Corrected — the challenger overstated, and it happens to be in the draft's favour:** *"both 'unmoved
divergences' come from one architect sitting"* is **wrong**. Rows 6 and 7 are **two tickets**, and row 7's
routing decision was taken **2026-07-04**, at a fix-round-3 review — *"Scope addition (2026-07-04) — the
`format: date` row"*, `T-0379-…md:115-118`, `:124-129` — sixteen days before the 2026-07-19 ruling that
`:135-136` records as *"ratified as-is"*. The catalog cell itself landed **2026-07-05** (`e97b14e7`) and
**no commit touching `agents/knowledge/` has modified it since** — which is what "ratified as-is" looks
like in a diff. Rows 6 and 7 are therefore **two independent routing decisions ruled on one day**, not one
event.

**Defensible count:** the ten rows are **at most eight** independent routing decisions across seven
commits, and plausibly seven (rows 5+6 are one ticket; rows 3+9 are one commit). CH-D's *"about six"* is
directionally right and slightly overstated. Both figures are far below ten, and the draft should stop
presenting ten rows as ten cases.

---

### CH-E, CH-F, CH-G

**CH-E — SUSTAINED.** Verified in the corpus: `2012b014`'s added paragraph reads *"…**plus a call-site
pin, because a resolver test does not cover the call site (the T-0473 rule)**… `OrderDetailCardStringsTest`
is the model"*. The candidate governing sentence and the entry it allegedly conflicts with were written by
one hand, in one paragraph, with the earlier rule cited **by name as one it composes with**, and the
composing artifact named in the same sentence. So D1's headline consequence — *"the one recorded
indeterminacy becomes determinate"* — is carried by a case that is not historical, is self-answering on
its own text, and **does not discriminate D1 from Alternative A**. Ruled: the draft may not count row 3 as
a result.

**CH-F — SUSTAINED.** N1/N-E is re-scoped to the *"Both platforms plumb the pair through **every** surface
that draws the disc"* clause. The `Android onAvatarLoadSucceeded` citation is structural and
verifiable-by-reading under accepted ADR-0033 D2 — it needs a file:line (Block B's two-line repair), not a
routing. **PM: N-E's spec changes; the finding does not disappear.**

**CH-G — SUSTAINED.** Verified against `conventions.md:120-130` in this tree: bullet 1 (`:122-124`) puts
the action *inside* the branch (*"the developer edits the relevant `patterns-*.md` … in the same change"*)
and bullet 2 (`:125-127`) puts the opposite action inside its own (*"Raise it via the ticket; don't
unilaterally redefine the standard"*). Block C′ hoists *"Write it into the catalog"* to an unconditional
step 3. A developer whose edit fires test 1 still arrives there. **This is L3's disease one nesting level
down, inside the block written to cure L3** — it must be fixed before the block lands.

---

### D3 (no fourth test) — **UNBEATEN, NOT SETTLED**

The challenger built six triggers (T-α…T-η) and killed all six, on top of the author's two. **I tried two
more, and both died:**

| Trigger | Row 6 | Row 7 | `:559-561` | Why dead |
|---|---|---|---|---|
| *the entry ratifies a form the **owner** directed* | fires (*"owner-directed edge-to-edge deviation"*) | **no** | no | misses row 7; and an owner ruling already routes by existing machinery — it is an authority trigger, not a trade-off one |
| *the entry states a cost borne by someone other than the codebase* (cold start, wire bytes, build time) | no | no | **fires** (*"one small refetch per cold start"*) | fails the negative control immediately — the same way the author's own second trigger did |

**Eight constructions, eight failures.** That is a real result and I record it so the next round does not
re-derive them.

**But it does not settle the limb, and the draft should not claim it does.** The target every one of the
eight was tested against is *"fires on rows 6 and 7, not on `:559-561`"* — and rows 6 and 7 are now known
to be (a) two decisions ruled by the same architect on one day, and (b) rows whose "governs?" column was
produced by a procedure this pass has invalidated. **A trigger tested against a corpus that has been
falsified cannot settle anything.** D3 survives as the presumption — *no fourth test today* — and
**re-opens automatically when the corpus is rebuilt**, against a target re-derived from it.

**Two riders, both against the draft:**
1. **Ground (a) is STRUCK as evidence.** The challenger is right that *"`T-0397-…md:70` shows the
   Architect asking the question after routing"* is a censored-sample inference: Architect rulings are
   the only place a routed decision gets written down at all, so the absence of a developer asking it
   proves nothing. The conclusion may hold; **(b) is what carries it**, and (b) now rests on a corpus
   under rebuild.
2. **The prize is mis-specified.** The challenger is right that the case a fourth test should be measured
   against is **CH-A/row 8** — an `owner: qa` ticket that deleted a named canonical form from the catalog,
   deleted a shipped file on one platform and rewrote a committed test suite, inline. A fourth test does
   not reach it; **the accepted floor already routes it and nothing ran.** That is L2's evidence, not
   F4's.

---

### D4 / Block C′ — **the finding SUSTAINED and SEVERED; the drafted text BLOCKED**

Every fact re-verified in this tree, independently of the challenger: `conventions.md:120-130` are items
1–3; `:122-124` scopes the inline lane to *"a **small** clarification/addition to an **existing** rule"*;
`:125-127` is the disjunction whose **first limb the accepted floor reverses**; `:128-130` is the
supersession step and is consistent with test 1; `:132-134` is the "earns its place" bar. **REPLACE, not
append, is correct.** The three findings the L3 filing did not carry are all real, including that ADR-0033
D1 test 4 mis-cites itself as *"unchanged from `conventions.md` step 2, first bullet"* — which is exactly
why a good-faith editor would append. And the erratum-lane ruling is right: *"Refines … does not reverse"*
is an **argued** claim, so it rides a dated appended section, not `adr/README.md:16-26`'s digits lane.
**I pressed the erratum question, as the draft invited, and I concede it.**

**Ruled: D4 does not depend on D1 and must not die with it.** `adr/README.md`'s *"one decision per ADR"*
points at the split, and the draft's **Alternative F** — which contemplated it and rejected it *narrowly*,
solely on the argument that F4 belongs with D1 — loses its only ground now that D1 falls. The draft's own
words: *"If the lead disagrees, the clean split is: D1+D2+D4 here, D3 to its own round … this draft is
written so that excision costs one section."* **I disagree, and the split is the other way.**

**The severance, precisely:**
- **D4's finding + Block C′'s skeleton go forward** as their own small ADR, in a fresh author round.
- **The `**What "governs" means — the conflicting-instance test**` paragraph inside Block C′ routing test
  2 is EXCISED.** In its place stands **accepted ADR-0033's own floor wording, verbatim and unaltered** —
  which the draft already reproduces two paragraphs later. That is a **deletion**, not new text: I am not
  writing the repair, and I may not.
- **The block carries a visible pointer** that *"governs"* is under repair and cite the successor panel,
  so the page does not present an undecided predicate as settled. An undefined term in an accepted rule is
  survivable; an undefined term dressed as a definition is what this round rejected.
- **CH-G is fixed** in the same pass: step 3's action goes back inside its branch.
- **The Block D addendum (N-B)** — the reviewer's firing-side burden — **is HELD.** It is the operative
  half of D2, it is the best thing in the draft, and the challenger could not break it. But *"name the
  artifact"* is only meaningful once "artifact ruled differently" has one reading. **It ships with the
  repair, not before it.** One rider the challenger is right about and I adopt: whatever search that
  burden obliges must be run against the catalog **as it stood before the edit**.

**Operational consequence, stated because two tickets hang on it.** `T-0553` AC3 asks for literal
insertable text and blocks **T-0549 AC3** and **T-0551**. This severance is what unblocks them: the
`conventions.md` repair proceeds on ADR-0033's accepted content; only the *definition* waits.
**T-0549 AC1/AC2 were never blocked and should not wait for any of this.**

---

### What a repair must satisfy — routed, not written

**I do not write the replacement.** That rule bound both my predecessors, it is the whole reason this
panel exists, and it binds me. What I am entitled to do is set the bar the next author must clear:

| # | The repair must… | Why, in one line |
|---|---|---|
| **R-1** | **Declare its quantifier** — ∃ (some artifact is ruled differently ⇒ governs) or ∀ (all artifacts both reach are ruled alike ⇒ does not govern) — and **use the same one in every worked row** | V-1: the draft used both and got opposite answers |
| **R-2** | **Define scope without the word "subject"**, or concede it uses it and say **who decides**. It must return a determinate verdict on **CH-C's row-10 artifact** (`ProfileHubContent.swift:298` under `:249-253`) from the definition, not from the reader's choice of reading | CH-C: "reach" is the subject question wearing plumbing |
| **R-3** | **Discriminate the pair the corpus now supplies**: **T-0449/T-0448** (a refinement completing a summary of already-shipped, already-tested behaviour — must NOT route) vs **T-0527** (a withdrawal of a named canonical form that deleted a file and rewrote its tests — must route). Both are in the record **with diffs**. A definition that treats them alike must say so and defend it | CH-B + CH-A: these are the two poles, and they are now evidenced rather than hypothesized |
| **R-4** | **Give the author an answer logically capable of defeating the trigger.** D2's *"compose or concede"* cannot refute an existential | otherwise the burden is a one-way ratchet toward the reviewer — the mirror of the defect D2 was written to cure |
| **R-5** | **Reproduce accepted ADR-0033's retro rows 2 and 3** (T-0441 inline ✅, T-0451 Architect ✅). A definition that flips a ✅ row of the ADR it refines has superseded that ADR and must say so | V-1: D1 flips row 2 silently |
| **R-6** | **Rebuild the corpus**, not patch it: search the catalog **as of each hunk's parent commit**; classify every hunk **insertion / modification / deletion** and quote the **pre-edit** text of modifications; source the "actual" column from the **routing event** (ticket owner + ratification date), not from the entry's present text. **Every declared limitation is discounted in the reported score, or no score is reported** | CH-A, the modified-hunk hole, and three rounds of undiscounted headlines |
| **R-7** | **Not be ratified by its author**, and its challenger must have the corpus | `deliberation.md`; and this round is the proof that diffs change verdicts |

**Routing.** L1 returns to **T-0553** for a **second author round** — a new author instance, this draft
retained on disk as the record of what was tried and why it failed. It is not a new ticket: T-0553's AC2
is unmet and its AC3/AC4 are answered by this ruling. **L3 becomes its own small ADR** with the severed
Block C″. **F4 stays open** and re-opens against the rebuilt corpus.

**Escalation to the owner: none.** Every disagreement resolved on in-repo evidence; nothing here carries
lasting business impact. The one thing the owner may care about is **schedule**: this is the second panel
on the same clause, and the operative state below says plainly that ADR-0033's routing test still binds
nothing while it runs.

---

### What I tried to break, and could not

`deliberation.md`: silence is not assent, and the first round's credibility problem was overruling its own
framings. Named, so a reader can check whether I did the same.

1. **D2's reviewer-side burden (firing test 2 costs an artifact, not a sentence).** I looked for a way to
   make it cost more than it buys and could not. It is M2 completed on the other side and it converts a
   plausible-but-adjacent assertion into something an author can answer. **It is the best thing in the
   draft and it is held only because its currency is undefined**, not because it is wrong.
2. **Alternative D's rejection (the artifact need not exist in the tree).** I argued for requiring
   existence and the draft's reasoning survives: row 8's artifact was converted **in the same change**, so
   an exists-in-tree requirement would make test 2 a strict subset of test 1 and miss the very case that
   falsifies row 8.
3. **Alternative C's re-rejection (the lexical/topic reading).** Re-tested on row 2 and it re-fails:
   T-0451 escapes as *"theme-invariant surfaces"*, and its governing sentence predates it by five weeks
   (`c1009c63` 2026-06-25 → `1c8fdd00` 2026-08-01). Not re-litigated.
4. **Row 4 (T-0349).** I attacked the strongest row and did not dent it: `:990` from `76fc48ab`
   2026-06-27, the entry from `04f98937` 2026-06-30, ticket `owner: architect`. It remains the one case
   where a *general* sentence carries a determinate verdict, and any repair should keep it.
5. **CH-D's clustering, in the challenger's favour.** I tried to confirm *"rows 6 and 7 are one architect
   sitting"* and **could not** — the ticket shows row 7 routed sixteen days earlier. Corrected against the
   party that filed it.
6. **A ninth and tenth trigger for F4.** Both died (table above). I am not manufacturing a survivor to
   avoid ruling D3 unbeaten.
7. **Row 6.** I built an argument that it flips under D1 (the fix-round-8 pin narrows the pre-existing
   cell) and then **killed my own argument**: the diff shows the append was written **by the architect, in
   the ratification** (`f0e39d7e` carries the pin and the signature in one hunk), so no routing question
   arises. **Recorded because I nearly filed it** — that is the exact failure mode this panel was warned
   about, and the diff is what stopped it.

### Gate 0.5, applied to this adjudication — what I could NOT verify

**Legs 1 and 2 do not apply** (no executable assertion; no suite, build or checker run) — same scoping as
`quality-gates.md:67-70`. **Leg 3, named:**

1. **No `Bash`.** I read a coordinator-generated corpus, not `git` directly. Commit hashes, dates and hunk
   headers are read off its diff blocks; I did not re-derive them. **This is the first round in this
   sequence with diffs at all, and it changed two verdicts — treat that as the finding.**
2. **CH-B's Kotlin/Swift file citations I took as established** (coordinator-verified) rather than
   re-opening `ProfileViewModel.kt` / `ProfileViewModelTest.kt`. My independent grounding for CH-B is the
   `4f81dce7` diff, which is weaker but sufficient: the entry names Android's method as an existing model.
3. **Row 5's chronology remains open**, as the challenger declared: I did not establish when
   `patterns-mobile.md:1241`'s `.medium` grant was introduced relative to the `:996-1001` withdrawal. It
   is a positive row, so the blindness does not touch it, but its date relation is unestablished by
   anybody across three rounds.
4. **I did not re-score the rebuilt corpus.** V-1 is demonstrated on rows 1, 3 and 9; I did not run a
   corrected definition over all ten, because there is no corrected definition and writing one is not
   mine. **R-6 is the work, and it is the next author's.**
5. **The rate is still unmeasured.** 41 commits touch `patterns-mobile.md`; I read the diffs of eight. What
   CH-A establishes and I confirm is a **direction** — the draft's negatives are one-sided under-estimates
   — not a number.
6. **Line numbers are this worktree's**, and `patterns-mobile.md` is a live shared-file lane. Every
   load-bearing citation quotes its text or its hunk header.

---

## Gate 0.5, applied to a deliberation — what this draft could NOT verify

**Leg 1 (mutation-prove the test): DOES NOT APPLY.** The evidence is a routing rule whose subjects are
Markdown edits; `quality-gates.md:67-70` scopes leg 1 by the evidence and directs this case to be
declared rather than to have a mutation invented for it.

**Leg 2 (a cached run is not a run): DOES NOT APPLY.** No suite, build or checker was run.

**Leg 3 — named:**

1. **NO SHELL.** Declared at the top and repeated here because the brief asserted otherwise. No
   `git log -p`, no `git show`, **no catalog edit read as a diff**. Ten cases reconstructed from
   in-tree entry text + their tickets + `Grep`.
2. **The chronology of `patterns-mobile.md:517-525` is unresolved**, and with it whether the lead's
   Case β is a historical mis-routing or only a live hypothetical. L1 survives either way; its evidence
   is weaker than filed. **One `git log -p` settles it.**
3. **Retro row 9's test-1 column is unknown.** Whether any Android `ProfileViewModel` lacked
   `onAvatarLoadSucceeded` when the T-0449 entry landed was not determined; if it did, test 1 fires and
   row 9's test-2 verdict is moot.
4. **Retro row 10's history was not verified from T-0432's ticket** — inferred from the entry being
   live and priced `(gate pending: FT-5)` in `catalog-governance.md`.
5. **Negative claims ("none nameable", rows 6/7/8/10) are searched, not proven.** I searched today's
   `agents/knowledge/`, not the catalog as it stood when each entry landed. A sentence deleted since
   would not appear.
6. **No rate, only cases.** Ten entries out of 41 tickets touching `patterns-mobile.md` alone. Ten
   determinate outcomes is enough to answer *"is the predicate decidable on real hunks?"* — the L1
   question — and is **not** enough to say how often D1 changes a routing. N-F is the measurement.
7. **Line numbers are this worktree's**, and `patterns-mobile.md` is a live shared-file lane. Every
   load-bearing citation quotes its text, because the last two rounds both had offsets drift under them.
