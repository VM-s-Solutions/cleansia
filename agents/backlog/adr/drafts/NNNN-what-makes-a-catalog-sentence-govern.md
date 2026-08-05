# ADR-NNNN (DRAFT — number NOT allocated) — What makes a catalog sentence *govern* an entry: the conflicting-instance test, and the corrected `conventions.md` edit

- **Status:** `proposed` — **author's draft, not ratified.** Written to be attacked.
- **Date:** 2026-08-05
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

*Empty — the lead instance rules here. **This author does not ratify its own draft**, which is the
defect T-0471 exists to repair and which binds a third round exactly as it bound the first two.*

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
