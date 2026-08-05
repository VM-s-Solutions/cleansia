# ADR-NNNN round-2 draft ("`governs` = reach over the entry's exhibit") — Challenger pass

Role: **CHALLENGER**, round 2. A fresh instance: I did not write ADR-0033, did not challenge it, did
not adjudicate it, **did not write the round-1 draft, its challenge, or its verdict, and did not write
this round's draft.** Target:
`agents/backlog/adr/drafts/NNNN-what-makes-a-catalog-sentence-govern-round-2.md` (`proposed`).

Gate 0 discipline: **REFUTED by default.** Every claim below cites a commit, a hunk, a file:line or a
ticket that I read myself this pass. Where I could not settle something I say so in §"What I could not
verify" rather than assert it.

> ## Method
>
> **No `Bash`** (charter limitation). Evidence base: the coordinator-generated **catalog-edit corpus**
> (94 commits touching `agents/knowledge/*.md`, full diffs), plus the live tree
> (`agents/knowledge/patterns-mobile.md`, `patterns-backend.md`, `conventions.md`,
> `.claude/agents/reviewer.md`, `agents/backlog/adr/challenges/0033-floor.md`, the ticket directory,
> the ADR directory).
>
> **I did not take the draft's attributions on trust and I re-derived the load-bearing ones.** Three of
> its corpus notes — **C-1** (R1's hunk is a MODIFICATION), **C-3** (the `.medium` grant predates the
> withdrawal by ≥11 days), and **R11**'s full-entry replace — I re-read in the diffs and **all three
> hold**; they are recorded under §"What survived" rather than omitted.
>
> **What this pass adds that the draft did not have:** I read the **live file structure** of
> `patterns-mobile.md` (its `## ` headings and their line ranges), not only the hunks. That is what
> produces CH-C and CH-D, and it is the one input the draft's method could not supply, because a hunk
> shows you a paragraph and never shows you which section it landed in.

**Seven findings. Two blocking (CH-A, CH-B). One blocking-as-a-condition (CH-C).** Three limbs I
attacked hard and **could not break** — the coverage lemma, the ∃-over-a-finite-list quantifier, and
the R6/R7 divergences — are named in §"What survived", with what I tried, because round 1's
credibility problem was manufactured disagreement.

---

### CH-A — R-2 is **not met**. The subject question was not removed; it was moved from `S` to `E`, into the exhibit's own filter — and on this repo's landing model the exhibit is not recoverable from a commit — BLOCKING

The draft's central claim over round 1 and over Alternative A:

> *"**Why "narrowest supported", and not "the subject".** R-2 asks for scope defined without the word
> *subject* … **D1 does not use it.** … *"Is this file in the exhibit?"* — answered by the diff and by
> `E`'s own citations. **No interpretation.**"*

Read limb (a) of its own definition:

> *"**(a)** every file this ticket changed **that `E` declares canonical or withdraws a form from**"*

The clause I have bolded is a filter, and it is not mechanical. *"Which of the files this ticket
changed is this entry about?"* **is** `E`'s subject, stated in different words. The draft asserts
twice that *"the exhibit **is** the diff"* and that it is *"a fact about the change, not a
characterization of it"* — and both statements are true only of the **unfiltered** diff, which is not
what the definition says.

**The filter is load-bearing, so it cannot be waved through.** Drop it and ∃ ranges over the whole
commit; a general sentence then has an exhibit member in almost every harvest and D1 over-fires into
C5's reductio. Keep it, and the discriminating work is done by an author-authored judgment about what
their own entry is "about". That is the round-1 defect's *shape* — a claimed closure that is a
relocation — even though the relocated question is materially cheaper than round 1's.

**And the mechanical fallback is unavailable in this repo, which the corpus proves.** D1 says *"the
**ticket's** diff"*. The unit that lands here is a **phase**, not a ticket (`MEMORY.md` →
*"PR by phase, not per task — batch task PRs onto a phase branch; merge to master per phase"*), and
the corpus shows it on the draft's own rows:

| Row | Landing commit | What the commit carries |
|---|---|---|
| **R12** | `6bd3b0c6` | *"…[**T-0447, T-0535, T-0546**]"* — **three tickets** |
| **R4/R9** | `0e4ede1b` | T-0473 + T-0449 + the T-0448 section, one hunk set |
| **R1/R5** | `04f98937` | *"Phase/hardening 1 (#101)"* — a phase branch |
| **R6/R7/R11** | `f0e39d7e` | *"Feature/payroll invoice paid notify (#127)"* — T-0379 + T-0397 + T-0429 |
| **R5/R6** | `365fd221` | *"Phase/ios fix2 (#107)"* |

For the majority of the corpus **no commit carries a per-ticket diff**, so *"checkable against the
commit"* is false. The draft concedes the consequence in its own Gate 0.5, leg 3, item 6, and this is
the sentence that decides the finding:

> *"Exhibit membership is derived from **the ticket's stated scope** and the entry's own citations,
> **not from reading the files**."*

The ticket's stated scope is prose a developer wrote. **So every one of the 13 rows was scored using
exactly the characterization the definition says it does not rely on.**

**A worked instance from the corpus, not from imagination** — this is attack surface #2, and the
draft's own newest backend row supplies it. **R13 / T-0548** (`97bb7265`) states a *general* law:

> *"**size first, then anything that decodes, parses, hashes, or round-trips the bytes**"*

and scopes its own roster in the next breath:

> *"Closed roster: it gates `ImageFileValidator` and `FileValidator` … **The other base64 intake paths
> are enumerated on T-0548's sweep and are not covered by it.**"*

Those uncovered paths were not changed by the ticket, so they are outside limb (a); they carry no
`file:line` in the entry, so they are outside limb (b). **A sentence that governed one of them could
not be reached, because the author scoped the exhibit to what they converted.**

**And that exposes a composition defect with ADR-0032 that nobody has named.** ADR-0032 D3 obliges an
honest author to *"narrow the sentence (stating the residual) or widen the enforcer"*. Under D1,
**stating the residual mechanically shrinks your exhibit**, which mechanically reduces the set of
sentences that can govern you, which mechanically cheapens your routing verdict. The draft's header
says *"Consumes: ADR-0032"* and never examines the composition in this direction. An ADR whose
compliance is rewarded by a second ADR's reach test is a seam I have to flag.

**What I am not claiming.** The exhibit **is finite** — I tested termination and it holds (see
§"What survived", item 2). The finiteness claim survives; the **objectivity** claim does not, and the
decidability argument, the coverage lemma's *"they are in the exhibit"*, and defence **D-b** all rest
on the objectivity claim.

**Ask:** either concede that the exhibit is a **written characterization with a stated tie-break**
(D1 gives a tie-break for `S` — *narrowest supported, widening unavailable* — and **none** for `E`;
route-by-default is the obvious candidate and the draft never says so), or show me limb (a)'s filter
applied without a judgment on a phase commit. R-2 stands unmet as written.

---

### CH-B — Deleting the verdict term from `governs` moves 100% of the discriminating work onto the second conjunct, and the draft's warrant for that conjunct's decidability is a **mis-citation** — BLOCKING

The draft's diagnosis of round 1 is right and I am not disputing it: `governs` should carry no verdict
term. But it has a consequence the draft states and does not price:

> *"**4. `Governs` carries NO verdict comparison.** Whether `E` conflicts with `S` is the **second**
> conjunct of test 2, **unchanged from accepted ADR-0033**"*

A reach test with no verdict term is a *much weaker* condition than round 1's. So under D1 the first
conjunct fires far more often, and **every** routing verdict is now decided by *carves an exception
out of it / replaces it / forbids a form it named*. The draft's warrant that this is safe:

> *"`challenges/0033-floor.md` CH-1 explicitly cleared *"replaces it, or forbids a form it named"* as
> the decidable half."*

I read CH-1. It does not say that. `challenges/0033-floor.md:32-34`:

> *"**Of the two remaining disjuncts, one is decidable and one is not.** *"**Replacing a named
> canonical form**"* is checkable: find the named form, or there isn't one. *"Withdrawing a form the
> catalog previously permitted"* is not…"*

CH-1 was ruling on the **original** floor's disjuncts (`:22-24`): *withdrawing a form the catalog
previously permitted* / *replacing a named canonical form* / a restatement of test 1. It cleared
**one** of them. The phrase **"carves an exception out of it"** does not appear anywhere in CH-1's
analysis — it enters at `challenges/0033-floor.md:73`, inside **CH-1's own proposed repair text**, and
**nobody has ever argued it decidable.**

**That is the disjunct doing the work.** In the draft's table, *"carves an exception"* is the named
disjunct on **both** positive rows — **R1** (*"that View touch **is allowed**"*) and **R2**
(`onFixedWhite`) — and it is the first of the three things every **D-c** defence must deny (R3, R7,
R9, R12, R13). Six of thirteen rows turn on a term whose decidability is warranted by a citation that
does not cover it.

**Why this is not pedantry.** *"Replaces a named form"* is checkable because it is lexical: the form is
named, or it is not. *"Carves an exception"* is a **semantic** relation between two prescriptions —
the same class of judgment the draft has just spent a whole ADR removing from the first conjunct. D1
therefore does not make test 2 *"decidable end to end"* as it claims; it makes conjunct 1 decidable and
**inherits** an untested conjunct 2, having just increased conjunct 2's load.

**Ask:** either strike the CH-1 citation and say plainly that conjunct 2 is carried forward untested
(which is honest and survivable — it is accepted ADR-0033 content), or score the corpus a second time
recording, per row, *which reader could reasonably deny the disjunct*. Do not present R1 and R2 as
D1's successes while their operative term is warranted by a sentence about a different term.

---

### CH-C — Alternative F is defended on a row the table itself excluded, and the *narrowing* half of the position rule is **unsound** (not merely permissive) in the section this test is applied to most. G3 is a **condition**, not a follow-up — BLOCKING as a condition of acceptance

**(i) Ground (i) of Alternative F cites a row the draft excluded for having no history.** Alternative F
rejects position-widening on three grounds, the first being:

> *"(i) it flips **R10** against history"*

But R10's own row reads:

> *"**UNESTABLISHED — no `T-0432-*.md` exists in `agents/backlog/tickets/`. Excluded from the agreement
> count**"* … *"⚪ **determinate; unscored**"*

**Verified:** I globbed `agents/backlog/tickets/T-04*` — there is no `T-0432-*.md` (there are
`T-0427`, `T-0441`, `T-0447`, `T-0448`, `T-0449`, `T-0451`, `T-0473`, `T-0479`, `T-0497`). The
exclusion is correct. **A row with no establishable history cannot be flipped against history.** Ground
(i) is void, and Alternative F is the alternative the author says it is least comfortable rejecting.
That leaves grounds (ii) and (iii), and **(iii) argues for *narrowing*-by-position, not against
widening** — T-0551's judgment call #2 is cited for *"a general sentence parked under a narrower
heading acquires that heading's scope"*, which is the half nobody disputes.

**(ii) The far worse problem: the narrowing half is unsound where it is applied most.** I read the live
heading structure of `agents/knowledge/patterns-mobile.md`:

| Line | Heading | Platform |
|---|---|---|
| `:247` | `## Shared UI & theme` | preamble `:249-253` is **Android** (`cz.cleansia.core.ui.components.*`, *"never duplicate a `:core` component"*) |
| `:501` | `## Navigation — typed routes` | Android (`navigation/Routes.kt`) |
| `:507` | `## Strings & states` | Android (`res/values/strings.xml`, `stringResource(R.string.x)`) |
| `:580` | `## Picking an image… (T-0448)` | Android |
| `:615` | `## iOS — SwiftUI/MVVM parity port` | iOS starts **here** |

So `## Shared UI & theme` runs `:247`–`:500` and hosts **four iOS blockquotes** — T-0432, T-0473,
T-0451, T-0449 — under an Android-worded preamble. D1 says:

> *"Position may **narrow** — a sentence under a heading carries that heading's scope"*

Apply that uniformly inside `:247`–`:500` and every iOS blockquote there is narrowed to Android, which
is false of all four. The rule does not merely leave a *permissive gap* in that section (the draft's
G3 framing); in that section it **returns wrong answers in the narrowing direction too**. The draft
prices only the widening half:

> *"**G3 is resolved permissively by construction.** … That is the right reading of the text and the
> wrong state for the file."*

It is not only permissive. It is unsound, and the unsoundness is inside `patterns-mobile.md` — the file
the routing test is applied to most, and the file that supplies 10 of the 13 rows.

**Ask:** **N-C is not "any time".** Make G3 a **condition of acceptance**: the position rule may not
land while the section it will be applied to has no determinate heading scope. This lane has already
accepted one ADR with its own named condition unmet — `catalog-governance.md:61-66` records that
ADR-0033 *"named Block D … as its own condition of acceptance and was accepted with that condition
unmet; it sat at `(guidance — no gate)` for the whole of its acceptance."* Do not do it twice on the
same clause.

---

### CH-D — R4′, the row that discharges AC2, is scored on the **weakest available ground**; the decisive one is D1's own paradigm move, and it dissolves Case β a third time (which strengthens Alternative H)

The draft scores R4′ — the live-hypothetical form of the case ADR-0033 §Ruling 1 was built on — with a
clause-count argument, and names it as attack surface #3:

> *"I claim `S`'s condition is *"the untranslated-literal guard"* because every clause after the dash
> specifies literals. Argue that … the literal clauses are its *method*, not its scope. If that reads
> better, **R4′ routes** and AC2 is unmet."*

**I am not taking that bait, because it does not matter.** I read `S` in the live file. It is
`patterns-mobile.md:563-571`, and here is what the draft's method could not see:

1. **`S` sits under `## Strings & states` (`:507`)**, whose opening sentence is *"All user text in
   `res/values/strings.xml`, accessed via `stringResource(R.string.x)` (or `appContext.getString` in
   the VM)"*. The iOS section does not begin until `:615`. Under D1's **own** narrowing rule, `S`'s
   condition is an **Android** screen.
2. **`S`'s own words name Kotlin.** `:567` — *"strip comments and **`${…}` templates**"*. `${…}` is
   Kotlin string-template syntax; Swift interpolates with `\(…)`. `:564` — *"**`R`** cannot see prose
   that never became a resource"*. Its immediate neighbours name `@Composable`, `BuildConfig.DEBUG`,
   `CleansiaNavHost`, `ProfileRowRoutingTest`.

R4′'s exhibit is `OrderDetailFooterStyle.swift` / `OrderDetailFooterTintTest` /
`NotificationsScreenTogglesTest`. To reach a Swift file you must **substitute a broader term for one
`S` names** — read `${…}` as "string interpolation on whichever platform", exactly as T-0432 requires
reading `:core` as "the shared component package on whichever platform". **That is the draft's own
§Context table, row 2, applied to row 1 — and the draft does not apply it.**

**So the verdict holds and the reasoning offered does not carry it.** R4′ is inline for a reason one
sentence long that needs no clause-counting, and the author invited a challenge on the only ground that
could have lost.

**The consequence the draft does not have, and it is the strongest thing in Alternative H's favour.**
Case β — the founding indeterminacy, `catalog-governance.md:104-113`, the reason this ADR exists — now
dissolves **three independent ways**, none of which needs a new definition:

| # | Dissolution | Found by |
|---|---|---|
| 1 | The candidate sentence **post-dates** the entry by a day (`2012b014` 2026-08-02 vs `0e4ede1b` 2026-08-01) | corpus §"Settled: N-F" |
| 2 | The candidate's own paragraph **cites *"(the T-0473 rule)"* by name** as a rule it composes with, and names the composing test in the same sentence | round-1 CH-E |
| 3 | **The candidate is in the Android section and names Kotlin syntax**; reaching a Swift entry needs the forbidden term-substitution | **this pass** |

Three panel rounds, an independent lead pass, and ADR-0033 §Ruling 1 all argued over this sentence and
**none of them read the section it is in**. That is a fact about the strength of the founding evidence,
and it belongs in Alternative H. The draft's Alternative H says *"the case for it is stronger than
round 1 allowed"*; on this evidence it is stronger than **round 2** allows too. I am **not**
recommending H (see §Summary) — but the draft owes it this row.

---

### CH-E — the "9 agree" headline overstates its evidential weight by roughly half, and the independence discount is arithmetically incomplete

The draft is materially more honest than round 1 here — it prints *"Do not read 13 as 13"* and
discounts per row. Two things still stand.

**(a) Four or five of the nine agreements are non-discriminating** — the class round-1 CH-E killed row
3 for, which round 2 never re-applies:

| Row | Why it does not discriminate D1 from any rival, or from no rule at all |
|---|---|
| **R1** | The draft's own C-1 finding is the refutation: the pre-image says *"a duplicated **VM** is a harvest-to-Core candidate — flag, **an Architect call**"*. **The catalog names the routing in the sentence.** I re-read `04f98937` `@@ -632,10 +634,15 @@` and confirm it. Any reader following the catalog routes it; the ticket is `owner: architect` |
| **R11** | Routed by an **owner supersede of ADR-0022** plus an architect-run sweep. The round-1 lead already ruled on this exact commit: *"I am **not** booking this as a mis-routing — it was an architect-run sweep following an owner supersede of ADR-0022, which is correctly routed. It is a **method** datum."* An owner ADR supersede routes by machinery D1 has nothing to do with |
| **R2, R3** | **R-5 *requires* these be reproduced** (*"Reproduce accepted ADR-0033's retro rows 2 and 3"*). A constraint the repair must satisfy is not evidence the repair works |
| **R4** | Settled by chronology alone — the candidate did not exist. *"Already governs" means already* needs no definition |

That leaves **R5, R9, R12, R13** as rows where D1 does work a rival might get wrong — and R13 the
draft itself scores *"inline under **BOTH** readings"*, so the narrowing rule does no work there
either. I verified R13's `S`: `patterns-backend.md:102-104` says *"Validator inherits
`AbstractValidator<Command>`, uses `.Cascade(CascadeMode.Stop)`…"* and says **nothing** about ordering
— D-c holds under either reading. **Honest count: ~3 discriminating agreements, not 9.**

**(b) The independence discount is wrong by one, under the draft's own rule.** It says:

> *"**~11 independent routing events, not 13.** R5 and R6 are one ticket; … R11 folds T-0379 …"*

**R7 and R11 are also one ticket — T-0379.** R7 is *"T-0379 `format: date`"*; R11 is *"T-0379 shell
navigation"*; both rows cite `T-0379-…md`. The draft merges R5/R6 for being one ticket and does not
merge R7/R11. Applying its own rule gives **~10**, not ~11 — and the merged pair **straddles the
agree/diverge line** (R7 ❌, R11 ✅), which is the least comfortable place for a clustering error to sit.

**Ask:** re-state the headline as *"~10 routing events; ~3 discriminating agreements; 3 pre-existing
divergences"*. That is still a result, and it is one a lead can rely on.

---

### CH-F — Block B hands the reviewer the **burden** and not the **predicate**. Landing it in one commit with Block A fixes timing, not content (minor→blocking on the drafted text)

**Verified** at `.claude/agents/reviewer.md:114-118` — reviewer-check 5's test-2 bullet reads today:

> *"**Does it narrow?** A sentence already governs this subject at any level of generality, and the
> entry carves an exception out of it, replaces it, or forbids a form it named. Semantic, not
> lexical … **If the author claims the floor …; if there is none, the floor is not claimed — route
> it.**"*

Block B appends the three-part firing burden. It does **not** carry the definition. So after N-A + N-B:
`conventions.md` teaches a 24-line reach-over-exhibit predicate, and `reviewer.md` states test 2 with
`governs` **still undefined** — and without even the `:161-169` warning, which lives only on the
author's page. N-B's sequencing note handles the wrong axis:

> *"**with N-A**, one commit — the two pages must not disagree in either direction"*

**One commit fixes when they land, not what they say.** This is the L5 finding
(`catalog-governance.md:426`) one turn later: *"five paraphrases of one rule is how this drifted in the
first place"*, whose fix was to make the other pages **quote** `conventions.md` rather than paraphrase
it. Block B is a burden without its predicate — the reviewer will fire test 2 on a paraphrase and the
author will answer with D-a/D-b/D-c.

**Ask:** Block B carries the one-sentence predicate (or an explicit pointer to the `conventions.md`
section by its greppable title), not only the burden.

---

### CH-G — Block A lands **beside** unamended M1 text, so `conventions.md` will teach two rules about the same question. That is L3's disease at one nesting level down — the shape CH-G caught in round 1 (minor, blocking on the drafted text)

Block A's operation:

> *"**REPLACE the standing warning at `agents/knowledge/conventions.md:161-169`** … **`:147-159` (test 2
> and the floor) … are untouched.** Nothing else on the page changes."*

**Verified** what stays. `conventions.md:153-154`:

> *"A rule stated about the general case **governs its sub-cases**; carving out a sub-case narrows it
> **whether or not the sub-case was ever named**."*

Block A's new text, four lines below it:

> *"If a reading has to **drop a clause of `S`** or **swap a broader term in for one `S` names** to
> reach your exhibit, it has *widened* `S`, and widening is not available."*

A developer reading `:147`–`:185` straight through gets *"a general rule reaches sub-cases it never
named"* immediately above *"you may not restate a sentence more generally to reach your case"*, with
**nothing on the page saying how they compose**. They are reconcilable — M1's sub-cases are sub-cases
of `S`'s condition **as `S` words it** — but that sentence is not written anywhere, and
`catalog-governance.md:97-103` shows M1 being applied by *re-describing the subject at a different
level* (T-0451's *"theme-invariant surfaces"* as a sub-case of the `CleansiaColors` mapping row).

The draft insists it *"does not re-open … M1's definition of silence"*. It does not re-open the
**negative** (silence). It unavoidably constrains the **positive** limb of the same sentence. Say so on
the page, or the page teaches two rules — which is exactly what T-0549 AC3 + T-0551 were run to stop.

---

### Minor findings — named so silence on them does not read as assent

1. **Limb (b) is applied in two readings inside one table.** D1 says the exhibit includes *"every
   `file:line` `E` itself cites"*. On **R10** the draft lists *"the cited `ProfileHubContent.swift:298`"*
   — but the entry (`4d8b3978`) cites only *"partner `ProfileHubContent`'s hand-rolled copy is the
   remaining convergence target"*, **no line**; the `:298` comes from `catalog-governance.md:371`. On
   **R1** it lists `AddressPickerView.swift` / `BookingAddressPickerView.swift`, which `04f98937` names
   as *"(`AddressPickerView` / `BookingAddressPickerView` — distinct chrome/L10n/navigation)"*, **no
   lines**. Strictly, bare filenames are outside limb (b); loosely, they are in, and limb (b) balloons
   (parity-table rows name dozens of files without lines). **No verdict flips either way** — I checked
   R1 and R10 under both readings. But round 1 was rejected for using two readings of its own term, and
   one word ("cited by name or by `file:line`") closes it.
2. **"The parent commit of **this edit**" is singular; four rows land across two commits.** R5, R6, R9
   and R11 each do. The draft flags it as a discount for R6 (*"two parties, two dates"*) and for R9
   **silently picks the later parent** — under the earlier one (`0e4ede1b`) the candidate `S` (the
   T-0448 paragraph) landed in the **same commit** and did not exist at the parent, making the row
   vacuous like R4. Same verdict either way, so nothing moves; the rule still needs one clause.
3. **Block A is 24 lines, not "~20"** (draft lines 194-217), against a 9-line warning. The
   §Consequences cost line understates by a third. Trivial, but it is the cost line a lead weighs
   Alternative H against.

---

## What survived — attacked and not broken

Named with what I tried (`deliberation.md`: *"a challenger that finds nothing says so explicitly and
names what they checked"*).

1. **The coverage lemma holds, and I tried to break it on the draft's own newest row.** R12
   (`6bd3b0c6`) is a hunk that **literally rewrites the sentence it cites** — `patterns-frontend.md`
   `@@ -468,8 +468,41 @@` deletes *"Cleared so far: all of `libs/core`, `libs/data-access`, and
   `libs/cleansia-customer-features/order-wizard`. **Never delete a scope from that list…**"* and
   re-adds it with a longer list. I pressed *"replaces it"* on its face: a hunk that deletes and re-adds
   the sentence has, textually, replaced it. **The draft's D-c reading is the better one** — the rule
   (*"never delete a scope"*) survives verbatim, the enumeration is a factual inventory being extended,
   and extending a cleared list is strictly more constraint, not an exception carved. R12 stays inline.
   The lemma is not dented.
2. **The exhibit terminates.** I ran the three regress candidates the brief names. *An entry citing a
   file that cites another:* limb (b) collects `file:line`s, not entries — there is no transitive
   closure and no recursion step to unroll. *A diff touching a shared file:* the catalog file itself is
   in the diff and is excluded by limb (a)'s filter (nobody declares `patterns-mobile.md` canonical) —
   which is CH-A's point, but not a termination failure. *An empty diff:* exhibit = limb (b) alone,
   still a finite written list. **∃ over a finite written list is a real closure of round 1's
   quantifier, and R-1 is met.** My attack on the exhibit is its objectivity (CH-A), not its finiteness.
3. **I found the "sentence with no limiting clauses" the draft says no row tests — and neither
   divergence moves.** `patterns-mobile.md` carries, as a **context** line in `60fa795c`'s hunk (so it
   predates 2026-06-26 and every row in the corpus):

   > *"**Parity rule:** reproduce the Android feature's states, empty/loading/error handling, and API
   > calls exactly. **A behavior difference is a bug unless the ticket calls for it.** If the Android
   > behavior is itself wrong, raise a finding — don't silently diverge on iOS only."*

   Sentence 2 has **no limiting clause at all** and reaches every iOS entry's exhibit. So D1's
   §Consequences worry (*"no row in this corpus tests it"*) is testable, and I tested it on the two
   divergences the draft cannot move:
   - **R6** (T-0397 full-bleed header): governs. Disjunct? The entry says the Profile hero is *"an
     **owner-directed** edge-to-edge deviation from Android's breathing-room treatment"* — which is
     `S`'s **own** exception, *"unless the ticket calls for it"*. **D-c defeats. Stays inline.**
   - **R7** (T-0379 `format: date`): governs. The entry sets `useCustomDateWithoutTime: true` so the
     Swift wire form **matches** Android's `"yyyy-MM-dd"` — it preserves behavioural parity by changing
     a mechanism. Nothing carved, replaced or un-named. **D-c defeats. Stays inline.**

   **So R6 and R7 remain divergent for the floor's backward-looking reason, not for want of a
   governing sentence** — and the draft's discount was right that a missed sentence on R6 *"can only
   improve the headline"*. I attacked the negatives where they could hurt (R3, R9, R12, R13) and could
   not convert one into a divergence: each carries an independent **D-c**, and D-c is the one defence a
   missed sentence cannot defeat.
4. **C-1, C-3 and R11 re-verified from the diffs, against the possibility that a fourth round would
   inherit an error.** All three hold.
   - **C-1:** `04f98937` `@@ -632,10 +634,15 @@` is a **modification**, and its `-` lines carry
     *"**Deviations a reviewer rejects:** a feature/VM `import MapKit`/`CoreLocation` … a duplicated
     **VM** is a harvest-to-Core candidate — flag, an Architect call"*. The draft's correction of three
     prior rounds is right.
   - **C-3:** the `.medium` grant — *"The code dialogs are native `.sheet`+`.presentationDetents([.medium])`"* —
     is a **context** line in that same hunk (2026-06-30); the withdrawal landed `365fd221`
     `@@ -429,6 +449,11 @@` (2026-07-11). **≥11 days.** A chronology open across three rounds is
     correctly settled.
   - **R11:** `f0e39d7e` `@@ -342,20 +354,24 @@` is a full-entry replace whose `-` lines name *"a shell
     bar built as a stock `TabView`/`.tabItem` bar"* as a rejected form and whose `+` lines install
     *"**The shell bar on BOTH apps is the stock `TabView` + `.tabItem`**"*. Accurate.
5. **R2's chronology is sound by a route independent of `c1009c63`.** The `CleansiaColors` /
   `Color.dynamic(light:dark:)` mapping row appears as a `-`/`+` pair in `f0e39d7e` (2026-07-20), so it
   existed before the T-0451 entry (`1c8fdd00`, 2026-08-01). R-5's first half is genuinely discharged.
6. **The arithmetic is clean and the procedural discipline is right.** 9 + 3 + 1 = 13; 12 scored, R10
   excluded. **No number allocated** — I globbed `agents/backlog/adr/00*.md` and the highest on disk is
   **0042**, as the draft states. No `T-0432-*.md` exists, as the draft states. `reviewer.md:114-118` is
   as the draft describes. **After round 1, none of this could be assumed; all of it checks out.**
7. **R-3 is met.** T-0449 (must not route) and T-0527 (must route) come out right for independently
   checkable reasons — R9 on `S`'s Android vocabulary plus D-c, R8 on the sentence the hunk deletes.
   This is the pair round 1 could not discriminate, and it is the best result in the draft.

---

## Findings filed for the PM (not part of this round's verdict)

| # | Finding | Why it is not in the round |
|---|---|---|
| **G3′** | **G3 is worse than filed.** `patterns-mobile.md` §"Shared UI & theme" (`:247`–`:500`) does not merely leave the Android preamble un-scoped — it makes *any* heading-based scoping rule return **wrong** answers inside it, in both directions, because four iOS blockquotes live under an Android-worded preamble and the iOS section does not start until `:615`. Whatever the panel rules on D1, this is a live defect in the file the routing test is applied to most | structural catalog edit, `patterns-mobile.md` lane — but see CH-C: it should gate the ADR, not trail it |
| **G5** | **ADR-0032 D3 and the proposed D1 compose backwards.** ADR-0032 obliges an honest author to state the residual when the enforcer is narrower than the sentence (`patterns-backend.md:1233-1237` / T-0548 is the model). Under D1 that **shrinks the author's exhibit**, so ADR-0032 compliance mechanically cheapens the author's own routing verdict. Nobody has priced this direction | a composition question between two ADRs; it belongs in the draft's §Alternatives or a follow-up ADR, not in a challenge |
| **G6** | **Case β should be retired from the record as a discriminating case.** It now fails three independent ways (chronology; self-citation; **platform scope — this pass**). `catalog-governance.md:104-113` and ADR-0033 §Ruling 1 still present it as the worked hard case; anyone pricing L1 off it is pricing off a case that answers itself | a documentation correction to the living decision doc, owned by whoever finalizes this panel |

---

## What I could not verify (Gate 0.5 leg 3)

1. **No `Bash`.** I read the coordinator-generated corpus, not `git`. Commit hashes, dates and hunk
   headers are read off its diff blocks; I did not re-derive them.
2. **I did not reconstruct any whole pre-edit catalog file either**, so my *positive* find in
   §"What survived" item 3 (the Parity rule governs R6 and R7) is **one** sentence I located, not the
   set. The direction is unchanged: a missed sentence can only add firings, and every row I could not
   flip carries an independent **D-c**.
3. **I did not open any Swift/Kotlin/C# source for exhibit membership**, and CH-A's core claim does not
   need it — it is a claim about what the *definition* says and what the *commits* carry.
4. **CH-A's phase-commit claim rests on commit messages and the repo's stated PR model**, not on
   per-ticket file lists. I did not open the six ticket files to enumerate their scopes; if any of them
   pins an exact file list, that strengthens the "written characterization" reading and does not rescue
   the "checkable against the commit" one.
5. **CH-D's platform-scope reading of `S` is mine.** It is grounded in the live heading structure
   (`:507` / `:615`) and in `S`'s own `${…}` / `R` terms, both quoted. A reader who holds that a section
   heading in this file scopes nothing (which is G3′) loses limb 1 of it and keeps limb 2 — the
   term-substitution limb, which is D1's own paradigm move and does not depend on position.
6. **I did not re-score all 13 rows end to end under D1.** I scored R1, R2, R4′, R6, R7, R10, R12, R13
   myself and spot-checked R3, R5, R9, R11. No verdict I checked moves; CH-E is about *weight*, not
   about a wrong cell.
7. **Line numbers are this worktree's**, and `patterns-mobile.md` / `conventions.md` are live shared
   lanes. Every load-bearing citation quotes its text or its hunk header.

---

## Summary for the lead

| # | Claim | Ask |
|---|---|---|
| **CH-A** | **R-2 is not met.** Limb (a)'s filter — *"that `E` declares canonical or withdraws a form from"* — **is** `E`'s subject; the draft asserts *"the exhibit is the diff"* and *"no interpretation"*, which is true only of the unfiltered diff. And *"the ticket's diff"* is not recoverable from this repo's landing unit: `6bd3b0c6` carries three tickets, `04f98937`/`365fd221`/`f0e39d7e` are phase commits. The draft's own Gate 0.5 item 6 concedes it scored **every row** from *"the ticket's **stated scope**"*. Worked from the corpus at T-0548 (R13), which scopes its own roster and thereby its own exhibit | **BLOCKING** — concede the exhibit is a written characterization and give `E` the tie-break `S` already has (route-by-default), or show limb (a) applied without judgment on a phase commit |
| **CH-B** | **The conjunct that now does all the work is warranted by a mis-citation.** Deleting the verdict term makes conjunct 1 fire easily, so *carves/replaces/forbids* decides everything. `challenges/0033-floor.md:32-34` cleared **"replacing a named canonical form"** only; **"carves an exception out of it"** first appears at `:73`, inside CH-1's *repair*, and has never been argued decidable — and it is the named disjunct on **both** positive rows (R1, R2) and the first denial in every **D-c** | **BLOCKING** — strike the citation and carry conjunct 2 forward as untested (honest, survivable), or test it per row |
| **CH-C** | **Alternative F ground (i) cites R10 "against history" while the table excludes R10 for having no establishable history** (verified: no `T-0432-*.md`). Worse, the *narrowing* half of the position rule is **unsound** in `## Shared UI & theme` (`:247`–`:500`), which hosts four iOS blockquotes under an Android preamble and where iOS proper starts at `:615` | **BLOCKING AS A CONDITION** — strike ground (i); make **N-C / G3** a condition of acceptance, not "any time". `catalog-governance.md:61-66` records what happened last time this lane accepted an ADR with its condition unmet |
| **CH-D** | **R4′ — the row that discharges AC2 — is scored on the weakest available ground.** `S` (`:563-571`) sits under `## Strings & states` (`:507`, `res/values/strings.xml` / `stringResource`) and names `${…}` (Kotlin) and `R`. Reaching a Swift exhibit needs the **term-substitution** D1 forbids — the draft's own §Context row 2, unapplied to row 1. Verdict holds; reasoning does not carry it. **Consequence: Case β now dissolves three independent ways**, which is the strongest available argument for Alternative H and the draft does not have it | re-score R4′ on the term/position ground; add the third dissolution to Alternative H so a lead prices it honestly |
| **CH-E** | **"9 agree" overstates weight by ~half.** R1 (the pre-image **names its own routing** — *"an Architect call"*), R11 (an **owner supersede of ADR-0022**; the round-1 lead already declined to book it) route by machinery outside D1; R2/R3 are reproductions **R-5 requires**; R4 is settled by chronology. ~3 discriminating agreements. And the independence discount is short by one: **R7 and R11 are both T-0379**, so ~10 events, not ~11 — and that pair straddles agree/diverge | restate the headline; it is still a result |
| **CH-F** | **Block B gives the reviewer the burden, not the predicate.** `reviewer.md:114-118` will still state test 2 with `governs` undefined and without the `:161-169` warning. One commit fixes timing, not content — the L5 paraphrase finding one turn later | Block B carries the predicate or quotes the `conventions.md` section |
| **CH-G** | **Block A lands beside unamended `:153-154`** (*"a rule stated about the general case governs its sub-cases … whether or not the sub-case was ever named"*), four lines from *"widening is not available"*, with nothing stating the composition. L3's disease one nesting level down | one sentence on the page: M1's sub-cases are sub-cases of `S`'s condition **as `S` words it** |
| **Minor** | limb (b) read two ways inside one table (bare filename vs `file:line`) — no verdict flips, but it is round 1's fatal shape in miniature; *"the parent commit of **this** edit"* is undefined for the four rows that land across two commits, and R9 silently takes the later one; Block A is 24 lines, not ~20 | one clause each |

**My recommendation, plainly.** This is **not** round 1. R-1 (quantifier declared and used), R-3 (the
T-0449/T-0527 pair discriminated), R-4 (three defeaters, not "compose or concede"), R-5 (rows 2 and 3
reproduced), R-6 (parent-commit scoring, discounts that name their direction) are met, and its
diagnosis of round 1 — *`governs` must carry no verdict term* — is correct and is the reason it gets
further. **I do not recommend "reject and keep the warning."** Alternative H is stronger than the draft
allows (CH-D), but a warning that instructs a developer to *"record both readings"* is an instruction to
log a disagreement, and the definition catches R8/G1 on test 2 from the sentence its own hunk deletes,
which is the case this whole lane exists for.

**I do recommend the lead refuse it as filed.** CH-A and CH-B are answerable — CH-A by conceding
the characterization and naming route-by-default as `E`'s tie-break, CH-B by striking one citation —
and both are things the author must say **in the ADR**, because a decision that claims a closure it did
not achieve is the specific failure this panel rejected once already. CH-C should become a condition of
acceptance rather than a follow-up. **Answered on those three, this should be accepted**; the remaining
findings are drafting.
