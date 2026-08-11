# ADR-NNNN (DRAFT — round 2, number NOT allocated) — What makes a catalog sentence *govern* an entry: reach, read at the narrowest scope the sentence's own words support, against the entry's written exhibit

- **Status:** 🔴 **`rejected` — 2026-08-05, lead pass, panel CLOSED. This is not an ADR and no ticket
  may cite it.** It stays on disk as the record of what was tried, alongside round 1's. **The operative
  rule is unchanged: accepted ADR-0033 D1 + M1/M2, and `conventions.md:161-169`'s standing warning that
  *"governs"* is undefined.** See `## Verdict` for the ruling and for the one carve-out the lead
  routed as work (**G3**). The lead also ruled **L1 closed as a definition project** — the re-open path
  is named in §Verdict and is deliberately cheap, but it is not open by default.
  *(Filed `proposed` as the second author round on L1; the first draft is `rejected` at
  `agents/backlog/adr/drafts/NNNN-what-makes-a-catalog-sentence-govern.md`.)*
- **Date:** 2026-08-05 (drafted)
- **Number:** **not allocated on purpose.** Highest on disk today is **0042** (verified by `Glob` over
  `agents/backlog/adr/00*.md` immediately before writing this file). The PM allocates when the panel
  closes; two architects collided on 0041 this sprint by both grepping correctly at the same moment.
- **Refines:** **ADR-0033** D1 test 2 — it supplies the missing predicate and changes nothing else. It
  does **not** re-open test 1, test 3, D2 (cross-stack strength), M1's definition of silence, M2's
  evidence rule and route-by-default, M4's ADR-0032 composition, or the floor. On acceptance a **dated
  appended section** goes on ADR-0033 (`adr/README.md` §1, form 1). **No in-body edit to ADR-0033.**
- **Consumes:** ADR-0032 (a constraining entry names an enforcer and declares a tier).
- **Applies to:** cross-cutting (catalog governance; all stacks)
- **Ticket:** T-0553 (L1). **L3 is CLOSED** (T-0549 AC3 + T-0551 applied the severed block); **AC5 is
  discharged** (ADR-0033's false *"does not reverse"* header claim already carries a dated record-only
  closure). This round owes **AC2** (the definition, worked on T-0473 and T-0349) and **AC4** (F4).
- **Panel:** author = this instance. I did not write ADR-0033, did not challenge it, did not adjudicate
  it, **did not write the round-1 draft, its challenge, or its verdict.** Challenger and lead are
  separate instances (T-0553 AC1).

---

> ## Method declaration, up front
>
> **No `Bash`** (charter limitation, fourth round running). The substitute is the coordinator-generated
> **catalog-edit corpus** — every commit touching `agents/knowledge/*.md` (94), newest first, with full
> diffs. **Every commit hash, date, hunk header and diff line quoted below I read in that corpus
> myself.** I did not take any prior round's attribution on trust; three of them are corrected below
> (§Corpus notes C-1…C-3), one of them in the round-1 draft's favour.
>
> **What is new in this round, and it is the only reason it can do what round 1 could not:** every row
> is scored against the catalog **as of the edit's parent commit**, read off the diff's own `-`/context
> lines, not off a grep of today's tree. That is R-6, and it changes three rows.
>
> **What is still not available:** I could not reconstruct the whole pre-edit file for any commit — only
> the hunks the corpus carries. So every *negative* ("no sentence governed") is **searched, not
> proven**, and §Retro-validation discounts each one by name rather than printing an undiscounted
> headline. That discipline is R-6's second half and it is the thing three consecutive rounds skipped.

---

## Context — what is actually broken, restated from the files

ADR-0033 D1 test 2 fires when **a catalog sentence already governs this entry's subject at any level of
generality** *and* the entry **carves an exception out of it, replaces it, or forbids a form it named**.

That is a conjunction. Amendment M1 defined the **negative** of the first conjunct (silence = *no*
sentence covers X at any level of generality) and never defined the positive. `conventions.md:161-169`
says so on the developer-facing page today, in a standing warning that ends *"quote the candidate
sentence and record both readings in `## Review` rather than settling it by whoever quotes first."*

**The first repair failed, and the failure is instructive.** Round 1 defined `governs` as *"a reader can
name one concrete artifact both sentences reach and rule differently."* Its lead killed it on **V-1**:
the definition is an existential over an unbounded set of *nameable* artifacts, so its negation is a
universal nobody can discharge, and six of its ten rows discharged that negative by exhibiting an
artifact satisfying **both** — a different test. Read as written it flips accepted ADR-0033's retro row
2 (T-0441, `inline` ✅) to *Architect*; read as scored, its only new catch vanishes.

**Two structural diagnoses in that verdict are the design constraints for this round, and I adopt both:**

1. *"The artifact is not the thing being judged — the **reach relation** is, and reach was defined as
   'the scope `S` prescribes for', which is `S`'s subject with a new name."*
2. *"D1 replaced a semantic judgement with a syntactic-sounding one and did not close either of the two
   degrees of freedom it introduced"* — the quantifier and the granularity of reach.

**And one diagnosis I add, because it is why round 1 could not have worked whatever quantifier it
picked.** Round 1 defined `governs` using a **conflict** notion (*verdicts differ*). But conflict is
already the *second* conjunct of test 2 — *carves / replaces / forbids* — and `challenges/0033-floor.md`
CH-1 explicitly cleared *"replaces it, or forbids a form it named"* as the decidable half. So round 1
duplicated the decidable conjunct into the undecidable one, and inherited its unboundedness on the way:
to compare two verdicts you must range over things to have verdicts about, and nothing bounded that
range. **The missing predicate is not a second conflict test. It is a *reach* test, and it must contain
no verdict term at all.** That is the whole content of this ADR.

### Where the indeterminacy actually comes from, on the two cases the ticket names

Both hard cases turn on the **same reader move**, and naming it is what makes them determinate:

| Case | The move that produces the second reading |
|---|---|
| **T-0473** vs `patterns-mobile.md:566-571` | The candidate sentence reads *"a screen with no test seam gets a **source-text scan scoped to the file** — strip comments and `${…}` templates, then **fail on any string literal holding three or more consecutive letters** (separators `·`, `#`, `—` pass; prose never does)"*. To make it reach a **colour-role** assertion you must **drop every clause after the dash**. |
| **T-0432** vs `patterns-mobile.md:249-253` | The candidate sentence reads *"Use `cz.cleansia.core.ui.components.*` … never duplicate a **`:core`** component."* To make it reach an **iOS** entry you must **replace `:core` with "the shared component package on whichever platform"**, on the strength of the `## Shared UI & theme` heading it sits under. |

In both, the second reading is produced by **widening the sentence past its own words** — dropping a
limiting clause, or substituting a broader term for a named one. That is the move. It is visible. It
can be forbidden, and the forbidding is checkable, because the question *"which clause of `S` did your
reading have to drop?"* has a textual answer.

---

## Decision

### D1 — `Governs` is **reach**: `S`'s condition, read at the narrowest scope `S`'s own words support, tested against `E`'s **written exhibit**

> **A catalog sentence `S` — as the catalog stood at the parent commit of this edit — GOVERNS entry `E`
> iff at least one member of `E`'s EXHIBIT falls under `S`'s CONDITION.**
>
> **1. `E`'s exhibit** — the finite, **written-down** list of code artifacts the entry is about:
> **(a)** every file this ticket changed that `E` declares canonical or withdraws a form from, **plus
> (b)** every `file:line` `E` itself cites. It is the ticket's diff plus the entry's own citations, so
> it is a fact about the change, not a characterization of it. **A deleted file counts** — it is in the
> diff. The author records it in `## Review`; test 1's sweep already produces most of it.
>
> **2. `S`'s condition** — the kind of artifact `S` constrains, **read at the narrowest scope `S`'s own
> words support, its prescription included.** A candidate reading that must **drop a clause of `S`** to
> reach `E`'s exhibit, or **substitute a broader term for one `S` names**, has *widened* `S`. **Widening
> is not available.** Position may **narrow** — a sentence under a heading carries that heading's scope
> — and position **never widens**: a heading broader than the sentence leaves the sentence where its
> words put it.
>
> **3. Quantifier: ∃, over `E`'s exhibit.** One member suffices. The negation — *no* member of the
> exhibit falls under `S`'s condition — is a **finite check over a written list**, which is what makes
> it dischargeable at all.
>
> **4. `Governs` carries NO verdict comparison.** Whether `E` conflicts with `S` is the **second**
> conjunct of test 2, **unchanged from accepted ADR-0033**: *carves an exception out of it, replaces it,
> or forbids a form it named.*

Test 2 then reads, unchanged in structure and now decidable end to end:

> **a catalog sentence governs this entry's subject (D1) AND the entry carves an exception out of it,
> replaces it, or forbids a form it named** → **Architect**.

**Why the exhibit, and not the artifact.** Round 1's artifact was unbounded because it was allowed to be
hypothetical, and it had to be, or test 2 would collapse into test 1 (an edit that converts its own
violators in the same change has a zero baseline). D1 gets the same coverage without hypotheticals,
because **the exhibit is the diff**: an edit that withdraws a shipped form necessarily *touched* the
call sites that carried it, so they are in the exhibit even though they no longer carry the form — or
it did not touch them, in which case they are live violations and **test 1 fires first**. Stated as a
property, because it is the load-bearing one:

> **Coverage lemma.** For any entry `E` that withdraws a form `F` which shipped code used, either
> **(i)** the ticket converted those call sites — they are in `E`'s exhibit, so a sentence naming `F`
> governs — or **(ii)** it did not — those call sites are now deviations they were not before, so
> **test 1** fires. **A withdrawal cannot be invisible to both tests.** Worked on the corpus at rows
> **R1** (converted in the same change), **R8** (files deleted in the same change) and **R11** (the
> whole entry replaced).

**Why "narrowest supported", and not "the subject".** R-2 asks for scope defined without the word
*subject*, or a demonstration that the regress terminates. D1 does not use it. It asks two questions,
each of which terminates on text:

- *"Is this file in the exhibit?"* — answered by the diff and by `E`'s own citations. No interpretation.
- *"Does this file fall under `S`'s condition?"* — answered against `S`'s **quoted** words, with the
  tie-break rule that a reading which deletes a clause of `S` loses to one that keeps it.

**The regress does not vanish; it terminates one level down, on a bounded question with a textual
answer.** I say that plainly rather than claim it is closed: the residual is *"does this named file fall
under these quoted words?"*, and where two readings survive **after** the widening rule has been
applied, M2's route-by-default still governs. What changed is that the argument is now conducted over a
named file and a quoted clause, and the losing move has a name.

### D2 — Firing test 2 costs a **quoted condition + an exhibit member + a named disjunct**. The author has three answers, and each can win.

The round-1 verdict **HELD** the reviewer-side burden — *"the best thing in the draft … it ships with
the repair, not before it"* — on the sole ground that its currency was undefined. The currency is now
defined, so it ships here, with the currency substituted and one rider the round-1 challenger was right
about (the search runs against the catalog **as it stood before the edit**).

| Party | What they owe | If they don't |
|---|---|---|
| **Author claiming the floor** | the catalog file(s) + term searched **against the pre-edit catalog**, and what it returned, in `## Review` (**M2, unchanged**) — **plus the exhibit**, as a file list | the floor is not claimed → **route** |
| **Reviewer firing test 2** | **(i)** the sentence, **quoted**, with the clause that carries its condition; **(ii)** the **exhibit member** that falls under it; **(iii)** **which disjunct** the entry does — carves / replaces / forbids-a-named-form | test 2 has not fired on that sentence — say so and move on |
| **Author answering** | any **one** of: **D-a** the condition, as quoted, is not true of any exhibit member — *name the clause the reviewer's reading dropped*; **D-b** that file is not in the exhibit — here is the exhibit; **D-c** granted that `S` governs, the entry carves no exception out of `S`, replaces nothing `S` said, and forbids no form `S` named — it adds a requirement on ground `S` left unaddressed | unresolved → **route** |

**R-4 is discharged by D-c, and D-c is the slot round 1 had no room for.** The round-1 challenger's
CH-B generalized its false positive into a reductio: *catalog sentences are summaries, summaries
under-specify, so every precision-adding refinement has a nameable conflicting artifact — and D2 gave
the author only "compose or concede".* Under D1 a refinement is defeated **structurally**: refining
carves no exception, replaces nothing, and forbids no form the earlier sentence named, so the second
conjunct never fires however broadly the first is read. **That is R-3's first pole, and it is answered
by the conjunct ADR-0033 already accepted, not by new machinery.**

**Route-by-default is preserved and does different work on each side.** On the author's side it prevents
self-certification. On the reviewer's side it means an unresolved substantive disagreement about one
named file still routes — the reviewer never has to win, only to be concrete. What it is no longer is a
one-way ratchet: **D-a, D-b and D-c are each logically capable of ending the challenge**, which is
exactly what round 1's *"compose or concede"* against an existential was not.

---

## Block A — the literal `conventions.md` text this ADR's acceptance installs

**Applier: architect + docs (a follow-up ticket, not this panel — T-0553 §Out of scope).**
**Operation: REPLACE the standing warning at `agents/knowledge/conventions.md:161-169`** (the
`> ⚠️ **"Governs" is not defined yet …**` blockquote, installed by T-0551 as a deletion-plus-pointer)
with the text below. **`:147-159` (test 2 and the floor), `:170-182` (tests 3/4 and the reversal
callout) and `:184-199` are untouched.** Nothing else on the page changes.

```markdown
   **What "governs" means (ADR-NNNN).** A catalog sentence `S` governs your entry `E` when **at least
   one member of your entry's exhibit falls under `S`'s condition** — and nothing more. It is a
   *reach* test; whether you *conflict* with `S` is the sentence above (carves / replaces / forbids).

   - **Your exhibit** is the finite list of files this entry is about: every file **this ticket
     changed** that the entry declares canonical or withdraws a form from, plus every `file:line`
     **the entry itself cites**. A file the ticket **deleted** is in it. Write it in `## Review`
     alongside the catalog sweep — test 1's sweep already produces most of it.
   - **`S`'s condition** is the kind of artifact `S` constrains, read **at the narrowest scope `S`'s
     own words support, its prescription included**. If a reading has to **drop a clause of `S`** or
     **swap a broader term in for one `S` names** to reach your exhibit, it has *widened* `S`, and
     widening is not available. A heading may **narrow** a sentence to its section; a heading never
     **widens** one past its own words.
   - **Search the catalog as it stood BEFORE your edit.** "Already governs" means already. If your own
     hunk deletes or rewrites a sentence, that sentence is a candidate — you are holding it.

   **Firing it costs three things; answering it costs one.** A reviewer who says a sentence governs
   **quotes the sentence** (including the clause that carries its condition), **names the exhibit
   member** that falls under it, and **names which disjunct** the entry does. A sentence alone has not
   fired the test. The author then wins with **any one** of: *that condition is not true of any file in
   the exhibit — here is the clause your reading dropped*; *that file is not in the exhibit — here it
   is*; or *granted it governs, but this entry carves no exception out of it, replaces nothing it said
   and forbids no form it named — it adds a rule on ground it left unaddressed.*
   **Unresolved either way ⇒ route.**
```

## Block B — the one sentence for reviewer-check 5, held by the round-1 verdict for this round

**Applier: architect + docs. Target: `.claude/agents/reviewer.md` step 5 ("Catalog-edit routing"), the
test-2 bullet, which today ends at *"if there is none, the floor is not claimed — route it"*.** Append:

```markdown
   **And the reverse — if YOU are the one saying a sentence governs, you owe three things, not one:**
   quote the sentence *with the clause that carries its condition*, name the **exhibit member** (a file
   this ticket changed or one the entry itself cites) that falls under it, and name **which disjunct**
   the entry does — carves an exception / replaces it / forbids a form it named. A sentence alone has
   not fired test 2. Search the catalog **as it stood before the edit**: a sentence the hunk itself
   deletes or rewrites is the highest-signal candidate there is, and no grep of the merged file can
   see it.
```

---

## Retro-validation — 13 real catalog edits, scored under one declared quantifier, against the pre-edit catalog

**Method, stated so it can be attacked (R-6).** For each row: the entry's **landing commit and hunk
shape** (insertion / modification / replacement); the candidate `S` **as it stood at the parent
commit**, taken from the hunk's own `-`/context lines or from an earlier commit in the corpus; `S`'s
condition read at narrowest supported scope; the exhibit member (or the finite absence); the disjunct;
the verdict; and the **routing event** (ticket front matter + recorded ruling), not the entry's present
text. **Quantifier: ∃ over the exhibit, in every row.**

| # | Entry · commit · hunk shape | Candidate `S` (pre-edit) | `S`'s condition, narrowest supported | Exhibit member under it | Governs | Disjunct | Routes | Routing event | |
|---|---|---|---|---|---|---|---|---|---|
| **R1** | **T-0349** · `04f98937` 2026-06-30 · **MODIFICATION** ×4 hunks | the same paragraph's pre-image, deleted by this edit: *"**Deviations a reviewer rejects:** a feature/VM `import MapKit`/`CoreLocation` (the §7.6 seam — the picker file is the only sanctioned consumer)"*; independently `:1036` *"feature/VM import no MapKit"*, a **context** line here and introduced `76fc48ab` 2026-06-27 | a feature-layer/VM file importing MapKit or CoreLocation | app-local `AddressPickerView.swift` / `BookingAddressPickerView.swift`, both now carrying `import MapKit` | **yes** | **carves an exception** — *"that View touch **is allowed**"*, and it rewrites `S`'s clause to *"for map/geocode **logic**"* | **Architect** | **Architect** — `owner: architect`, `layers: [ios, architect]`, `## Architect ruling (2026-06-30)` | ✅ |
| **R2** | **T-0451** · `1c8fdd00` 2026-08-01 · insertion | the parity-table `CleansiaColors` row, `c1009c63` 2026-06-25 (5 weeks earlier): *"`CleansiaColors` … the **same Material slot names** … **as `Color.dynamic(light:dark:)`**"* | a `CleansiaColors` slot | **`CleansiaColors.onFixedWhite`** — a new slot, deliberately not a dynamic pair | **yes** | **carves an exception** | **Architect** | **Architect** — the reviewer **refused to ratify inline** (ADR-0033 `:51`); that refusal generated ADR-0032 and ADR-0033. *(Ticket is `owner: ios` and the text landed in the ios commit — recorded, C-2)* | ✅ |
| **R3** | **T-0441** · `1d85b35f` 2026-08-01 · insertion | `:167-175` *"'Nothing recorded yet' is a repository concern … Normalize it **once, at the repository**"*; and the two-layer Api-adapter paragraph `:140-165` (`raw.mapBody { it.toAppDto() }`) | (a) a single-resource GET answering **HTTP 400 with a business key**, and the repository that reads it; (b) the **response**-direction adapter mapping wire→app | none of `BookingApi.toWire()`, `BookingApiTest`, the app/generated command DTOs answers a business-key 400 or maps a response | **no** (**D-a**) | — (and **D-c** independently: no exception, no replacement, no named form forbidden) | **inline** | **inline** — `## Review`: *"Dev harvest note (android, F1 close-out)"*; **and one cross-stack sentence routed to the Architect separately** (`:305-307`) — that is **test 3**, not test 2 | ✅ **R-5** |
| **R4** | **T-0473** · `0e4ede1b` 2026-08-01 · insertion | `:566-571` — **did not exist**: introduced `2012b014` 2026-08-02, one day *later* | — | — | **no** (nothing to read) | — | **inline** | **inline** (F3, already booked) | ✅ |
| **R4′** | **T-0473 as a live hypothetical on today's text** — the AC2 question | `:566-571` *"a screen with no test seam gets a **source-text scan scoped to the file** — strip comments and `${…}` templates, then **fail on any string literal holding three or more consecutive letters**"* | **the untranslated-literal guard** for a seamless screen — every clause after the dash specifies literals | `OrderDetailFooterStyle.swift`, `OrderDetailFooterTintTest`, `NotificationsScreenTogglesTest` are **tint/role** guards. Reaching them requires **deleting `S`'s literal clauses** ⇒ widening ⇒ unavailable | **no** (**D-a**) | — | **inline** | — (hypothetical) | ✅ **determinate** |
| **R5** | **T-0397 `.medium`** · `365fd221` 2026-07-11 · insertion into an existing bullet; ratification append `f0e39d7e` 2026-07-20 | *"The code dialogs are native `.sheet`+`.presentationDetents([.medium])` owning local input+FSM"* — **a context line in `04f98937`'s `@@ -632,10 +634,15 @@` (2026-06-30)**, so it predates by ≥11 days. **This settles the one date relation three prior rounds left open** | the promo/referral code dialogs | **`CodeSheetShell.swift:29,36`** — the sheet this ticket changed | **yes** | **replaces it / forbids a form it named** (`.medium`) | **Architect** | **Architect** — T-0397 `owner: architect`; entry signed *(Architect-ratified T-0397, 2026-07-19)* | ✅ |
| **R6** | **T-0397 full-bleed header** · `365fd221` 2026-07-11 **insertion** of a parity-table row; `f0e39d7e` 2026-07-20 **MODIFICATION** appending the fix-round-8 pin + signature — **two parties, two dates** | none **found** in the corpus's `patterns-mobile.md` hunks up to the parent | — | — | **no** (**searched, not proven — discounted**) | — | **inline** | **Architect** (T-0397) | ❌ **divergence — pre-existing** (ADR-0033 retro row 5). D1 does not move it and does not claim to |
| **R7** | **T-0379 `format: date`** · `e97b14e7` 2026-07-05 · insertion; **never modified since** | the same table's generated-client row, a **context** line here: *"config in `cleansia_ios/openapi/openapi-generator-config.*.yaml` … Generated output is **gitignored + never hand-edited** (change the spec or config, regenerate)"* | the generated Swift output and the two generator config files | **`openapi-generator-config.*.yaml`** — *"Both app configs carry the flag"* | **yes** | **none** — the entry **adds** `useCustomDateWithoutTime: true` and declares a defect; it carves no exception, replaces nothing, forbids no form `S` named (**D-c**) | **inline** | **Architect** — routed **2026-07-04** at a fix-round-3 review (`T-0379-…md:115-118`), ratified as-is 07-19 | ❌ **divergence — pre-existing** (ADR-0033 retro row 7) |
| **R8** | **T-0527** · `ab077504` 2026-08-04 · insert ×2 + **REPLACE** (`@@ -1272,9 +1315,9 @@`) | **the clause this very hunk deletes**: *"Cancel is a modal `.sheet` previewing the fee/refund via a pure TDD'd **`CancellationFeePreview`** (oops≤15m/free≥24h/half 4–24h/full<4h, **the `CancelOrderSheet.kt` tiers**; server recomputes authoritatively)"* | the customer cancel sheet and its fee preview — `S` **names two exhibit members by name** | **`CancellationFeePreview.swift`** (deleted), **`CancelOrderSheet.kt`** (ladder deleted), `CancellationFeePreviewTests` (rewritten) | **yes** | **replaces it, and forbids a form it named** — the entry's own replacement text: *"the client-side tier ladder both platforms shipped **is deleted**"* | **Architect** | **inline** — `owner: qa`, `adrs: []`, `:286` *"Harvested back into the catalog"* | ❌ **divergence — and the CORRECT one.** This is **G1**, the mis-routing the panel already booked; the **accepted** floor routes it too. A definition that agreed with history here would be wrong |
| **R9** | **T-0449** · `0e4ede1b` 2026-08-01 + `4f81dce7` 2026-08-05 · insertions | the T-0448 Android paragraph: *"refetch the profile once, **guarded by the `fileName` already retried**"* — same commit as the 08-01 hunk; predates the 08-05 append | an **Android** Coil-rendered SAS avatar in the customer profile composable — every clause is Android (`Coil`, `diskCachePolicy(CachePolicy.DISABLED)`, *"the composable"*) | none: the exhibit is `CachedRemoteImage`, `RemoteImageCache`, `ProfileViewModel.avatarLoadSucceeded` — Swift | **no** (**D-a**) | — (and **D-c** independently: the entry adds a *release* rule; nothing is carved, replaced or un-named) | **inline** | **inline** — `owner: ios`, `adrs: []`, `:316` *"Harvested into `patterns-mobile.md`"* | ✅ **R-3, second pole** |
| **R10** | **T-0432** · `4d8b3978` 2026-07-22 · insertion, **immediately below the `## Shared UI & theme` preamble** — the **R-2 test** | `:249-253` *"Use `cz.cleansia.core.ui.components.*` … never duplicate a **`:core`** component."* | a **`:core`** component — `cz.cleansia.core.ui.components`. The same file's parity table maps *"`cz.cleansia.core.ui.components.*` Composables"* **onto** *"`View`s in `Core/Components`"*, so the catalog itself treats them as two things joined by a mapping. The `## Shared UI & theme` heading is **broader** than `S`'s words, so it cannot widen them | none — `Core/Components/CleansiaButton.swift`, the two customer call sites, and the cited `ProfileHubContent.swift:298` are all Swift | **no** (**D-a**) | — | **inline** — *and see N-E: in a live application this row **never reaches test 2**, because **test 1** fires on `ProfileHubContent.swift:298`. The scoring here is the counterfactual R-2 asked for* | **UNESTABLISHED — no `T-0432-*.md` exists in `agents/backlog/tickets/`. Excluded from the agreement count** | ⚪ **determinate; unscored** |
| **R11** | **T-0379 shell navigation** · `f0e39d7e` 2026-07-20 · `@@ -342,20 +354,24 @@` **FULL-ENTRY REPLACE** — invisible to any grep of today's tree | the deleted clause: *"**Deviations a reviewer rejects (#35):** … **a shell bar built as a stock `TabView`/`.tabItem` bar** (the pill+FAB is BRANDING per ADR-0018/ADR-0022, not a component swap)"* | the signed-in shell bar on either iOS app | `CustomerShellView.swift`, the partner shell (T-0429), `BookFabMetrics` | **yes** | **replaces it and un-forbids the form it named** — *"The shell bar on BOTH apps **is the stock `TabView` + `.tabItem`**"* | **Architect** | **Architect** — T-0379 `owner: architect`, title *"a 'one way' **redefinition**"*; entry signed *"stale pill mandate swept T-0379"*; substantively an **owner** supersede of ADR-0022 | ✅ **new row, from a deletion** |
| **R12** | **T-0447/T-0535/T-0546** · `6bd3b0c6` 2026-08-05 · `patterns-frontend.md` **MODIFICATION + insertion** | the sentence **this hunk rewrites**: *"Cleared so far: all of `libs/core`, `libs/data-access`, and `libs/cleansia-customer-features/order-wizard`. **Never delete a scope from that list to make a new literal compile** — convert the call site instead."* | the cleared-scope list and the lint scopes on it | the converted libs' `eslint.config.mjs` files + the workspace-relative glob | **yes** | **none** — the entry **extends** the cleared list and adds a pin-before-you-convert rule; *"never delete a scope"* survives verbatim (**D-c**) | **inline** | **inline** — T-0447 `owner: frontend`, `adrs: []` | ✅ **new row; non-mobile; a MODIFICATION that correctly does not route** |
| **R13** | **T-0548** · `97bb7265` 2026-08-05 · `patterns-backend.md` insertion | `:102` *"Validator inherits `AbstractValidator<Command>`, uses `.Cascade(CascadeMode.Stop)`, injects repos … maps every rule to a `BusinessErrorMessage.X` constant"* | a Command validator (narrowest) — the exhibit's validators are `AbstractValidator<BlobFileDto>`. **Scored under the wider reading too**, deliberately | wider reading: `ImageFileValidator`, `FileValidator` | **yes**, under the wider reading | **none** under either reading — `S` says nothing about **ordering**; the entry adds an ordering rule (**D-c**) | **inline** | **inline** — T-0548 `owner: backend`, `adrs: []`; entry ships `**Enforced by:** ImageFileValidatorTests — T1-CI` | ✅ **new row; backend; inline under BOTH readings of the condition** |

### The score, discounted — because a stated limitation that does not move the number is a disclaimer, not a method (R-6)

**13 rows. One quantifier throughout. 13 determinate. 12 with an establishable routing event.**
**9 agree · 3 diverge · 1 unscored.**

**All three divergences are pre-existing and named. This definition creates none, and it flips no row of
the ADR it refines** (R-5: ADR-0033's retro rows 2 and 3 reproduced at **R3** and **R2**):

- **R6, R7** — ADR-0033's own two admitted divergences, the floor's backward-looking trade. Unmoved.
- **R8** — the mis-routing the panel already booked as **G1**, where the **accepted** floor routes and
  nothing ran. Agreeing with history here would be the error.

**Now the discounts, per row, in the direction they cut:**

| Discount | Rows affected | Direction |
|---|---|---|
| **Negatives are searched, not proven.** I read the corpus's hunks for ~13 of 94 commits; I could not reconstruct any whole pre-edit file. A missed governing sentence can only *add* firings | **R6** (whole verdict rests on it), **R3** (partially — I named and defeated the two nearest candidates by condition **and** by D-c), **R4** (the historical negative is *proven*; "no other sentence governed" is not) | Against R6's *"inline"* — which is already a divergence, so the discount can only **improve** the headline. **Against R3 and R4**, where a missed sentence could convert an agreement into a divergence. R3/R4 also carry a **D-c** defeater, which a missed sentence would have to overcome as well |
| **Low-discount negatives.** R9, R12, R13 rest on a **positive** reading of `S` plus D-c, not on absence | R9, R12, R13 | A missed sentence would have to *also* be conflicted-with. Low |
| **~11 independent routing events, not 13.** R5 and R6 are one ticket; R6 is two parties on two dates; R11 folds T-0379 (architect) + T-0429 (ios) + an owner supersede | all | **Do not read 13 as 13** |
| **Two compound "actual"s.** R2's routing event is a reviewer refusal recorded in an ADR, not the ticket's front matter; R11's is three events | R2, R11 | Stated |
| **R10 has no ticket file.** Its routing is unestablished and it is **excluded from the count**, not scored as an agreement | R10 | Stated |
| **No rate.** 13 of 94 commits read; the corpus holds 41 touching `patterns-mobile.md` alone | all | **This is a set of cases, not a measurement.** N-F remains the measurement |

**One asymmetry worth stating rather than burying:** the corpus's blind spot (a governing sentence
deleted or rewritten by an earlier commit) is a **retro-scoring** problem, not a defect in the rule as
applied. An author applying D1 searches *the catalog they are editing*, which **is** the pre-edit
catalog; and where their own hunk deletes or rewrites a sentence, they are holding it — R8 and R11 are
both cases where the highest-signal candidate is in the author's own diff. **The method that could not
see them is the audit's, not the developer's.**

### Corpus notes — three prior attributions corrected from the diffs

- **C-1 — R1's hunk shape was wrong in every prior round.** T-0349 was scored as an insertion at
  `:1244-1254`. `04f98937` carries **four** hunks and the load-bearing one is a **modification** that
  rewrites the pre-existing *"Deviations a reviewer rejects"* clause. The pre-image of that clause is
  the strongest candidate `S` in the whole corpus — and the pre-image also says a duplicated picker VM
  is *"a harvest-to-Core candidate — flag, **an Architect call**"*. **The catalog routed this edit
  itself, in the sentence the edit rewrote.**
- **C-2 — R2's "actual" is not its ticket's front matter.** `T-0451-…md` is `owner: ios`, `adrs: []`,
  and the entry landed in T-0451's own commit `1c8fdd00`. The routing event is the **reviewer's refusal
  to ratify inline**, recorded at ADR-0033 `:51`. Both facts are true and prior rounds printed only the
  second. R-6 asks for the routing event; I give it, and flag the tension.
- **C-3 — R5's chronology, open across three rounds, is settled.** The `.medium` grant is a **context**
  line in **`04f98937`'s `@@ -632,10 +634,15 @@` (2026-06-30)** — the same hunk that carries R1's
  pre-image, which is how I found it; the withdrawal landed `365fd221` (2026-07-11). **The grant
  predates the withdrawal by ≥11 days.** *(Corrected inside this draft: I first attributed the hunk to
  `e97b14e7` 2026-07-05, the next commit down in the corpus. Recorded rather than silently fixed,
  because a mis-read hunk boundary is exactly the class of error this round exists to stop.)*

---

## Alternatives considered

**A. The lead's original nominee — *"does `S`, applied to this entry's subject, yield a prescription the
entry contradicts?"*** *Rejected, and for a different reason than round 1 gave.* Round 1 called it
circular. That is half right and it is not the fatal half. The fatal half is that it fails **R-1** and
**R-2** simultaneously: it has **no quantifier** (it ranges over an unbounded "subject") and its scope
term **is** the undefined one. D1 keeps its correct intuition — a governing sentence must actually reach
this case — and replaces both open variables: the unbounded subject becomes `E`'s **written exhibit**,
and *"yields a prescription the entry contradicts"* is **dropped entirely**, so conflict stays where
ADR-0033 put it and `governs` becomes a pure reach test.

**B. The rejected conflicting-instance test (round 1's D1).** *Re-tested; re-fails.* Recorded so it is
not re-proposed. Its irreparable defect is structural, not evidentiary: it quantified over *nameable*
artifacts — unbounded, so its negation is undischargeable — **and** it folded the conflict conjunct into
the reach relation, which is why substituting the correct negation flipped ADR-0033's own retro row 2.
D1 fixes both by bounding the quantifier to the diff and by deleting the verdict term.

**C. Define `governs` lexically — *"`S` names `E`'s subject term"*.** *Rejected.* It is ADR-0033's
Alternative H (the topic-level reading) wearing a hat, already rejected by an accepted ADR. Re-tested on
**R2**: T-0451 escapes as *"theme-invariant surfaces"*, a phrase the catalog had never used, while its
governing sentence predates it by five weeks. Not re-litigated.

**D. Exhibit = all code `S` would reach, not just this ticket's.** *Rejected.* It re-opens the
unbounded quantifier round 1 died on, and it makes a broad sentence govern essentially every entry —
C5's reductio returning through a different door. **The bound is what makes the negative dischargeable**,
and it is not arbitrary: the entry's claim is about the code it was harvested from, and that code is a
fact recorded in a diff.

**E. Require the exhibit member to survive the edit.** *Rejected.* R1, R8 and R11 all convert, delete or
replace their own exhibit members in the same change; requiring survival makes test 2 a strict subset of
test 1 and misses exactly the withdrawals it exists to catch. Round 1's Alternative D reached the same
conclusion and paid for it with hypotheticals; D1 gets it for free, because **a deleted file is in the
diff and a hypothetical file is not**.

**F. Position widens — read a section preamble as scoping everything under its heading.** *Rejected,
and this is the alternative I am least comfortable rejecting, so the cost is stated.* Three grounds:
(i) it flips **R10** against history; (ii) it makes *what code is legal* change when a paragraph is
**moved**, with no sentence edited and no diff a reviewer would read as a rule change; (iii) it
contradicts the applied T-0551 judgment call #2, which deliberately kept a *general* sentence **above**
new `###` subsections precisely because *"a general sentence parked under a narrower heading acquires
that heading's scope"* — i.e. narrowing-by-position is already treated in this repo as real and
widening is not. **The cost:** `patterns-mobile.md`'s `## Shared UI & theme` preamble does **not** bind
the four iOS entries under it until someone writes that it does. That is a real permissive gap, it is
**G3**, and D1 resolves it in the permissive direction — which is an argument for fixing G3 promptly,
not an argument against D1. A challenger who wants (ii) answered differently should attack here.

**G. `∀` over the exhibit — `S` governs only if *every* exhibit member falls under its condition.**
*Rejected.* A cross-platform harvest (R8 touches iOS and Android; R11 touches two apps) escapes the
moment one member sits outside `S`'s condition, which is the common case. `∃` is right, and its negation
stays checkable **because the exhibit is finite** — which is the whole reason the quantifier is
survivable here and was not in round 1.

**H. Do nothing — leave `governs` undefined and lean on route-by-default.** *Rejected, with sympathy,
and I record that the case for it is stronger than round 1 allowed.* Round 1 argued the enforcer's
landing made the gap urgent. It landed (`c717091d`; T-0549/0550/0551), **and this ADR blocks nothing** —
the ticket says so, and the routing rule works today. But `conventions.md:161-169` now instructs a
developer, in the operative page, that the predicate they are applying is undefined and that they should
*"record both readings"* — an instruction to log a disagreement rather than resolve one. **That is a
stable, honest state and it is survivable indefinitely**; what it costs is one recorded indeterminacy
per hard case and a reviewer who cannot be answered. Whether that is worth Block A's cost is the lead's
call, and a lead who rules *"no definition, keep the warning"* is not making an error.

---

## F4 — answered as a consequence of D1, not as a second decision

**T-0553 AC4 asks for a fourth test added with its text or rejected with the reason.** The round-1
verdict left it *"UNBEATEN, NOT SETTLED — re-opens automatically when the corpus is rebuilt."* The corpus
is rebuilt. **My answer: no fourth test**, and the reason is now stronger than *"eight triggers died."*

1. **The target was re-specified, and D1 closes it.** The round-1 lead ruled that rows 6/7 are *"the
   wrong prize"* and that the case a fourth test should be measured against is **R8** (T-0527: a named
   canonical form deleted from the catalog, a shipped Swift file deleted, a committed suite rewritten,
   `owner: qa`, inline) — adding *"a fourth test does not reach it; the accepted floor already routes it
   and nothing ran."* Under D1, **R8 routes on test 2**, from `S`'s own deleted text, with no
   interpretation required. **F4's re-specified target is caught by the limb it was proposed to
   supplement.**
2. **What remains in F4's scope is rows R6 and R7 alone** — two first statements where no sentence was
   *found* to govern. Eleven triggers have now been built against that target across three rounds (two
   by the round-1 author, six by its challenger, two by its lead, and I built one more: *"the entry
   ratifies a form whose losing alternative is documented as shipping elsewhere in the catalog"* —
   fires on R6, **not** on R7, dead). Every one either misses a prize, over-fires on a row both the rule
   and history send inline, or reduces to the location/wording trigger accepted ADR-0033 already closed.
   **The record now holds eleven; I am not manufacturing a twelfth to avoid saying so.**
3. **`T-0397-…md:70` reads as a *procedure* question, not a *test* question, and ground (a) stays
   struck.** The round-1 challenger was right that *"the Architect asks it after routing"* is a
   censored-sample inference — Architect rulings are the only place a routed decision is written down.
   So the citation proves neither direction about developers. What it **does** establish is that
   *"catalog row or ADR?"* is a real question with a worked precedent and an answer (*"no trade-off
   survives"*), asked **on receipt**. That belongs in the Architect's on-receipt procedure — carried
   forward as **N-D**, which nobody attacked in round 1.
4. **R6 and R7's residual is the floor's, not a missing limb's.** Both are edits that oblige no shipped
   call site and withdraw no governing sentence, and the floor sends those inline **by design**. Moving
   them requires an ADR that prices *future* code, which is a different decision from *"who ratifies
   this edit"*. Nobody has proposed one.

**I present this as a consequence of D1 rather than a second decision, because D1 is what changes the
answer.** If the lead judges it a second decision, **sever it** — nothing in D1 depends on it, and the
severance costs one section.

---

## Consequences

**Cheaper / safer**

- **The predicate is decidable, and the residual argument has a shape.** *"Which clause of `S` did your
  reading drop?"* and *"is this file in the exhibit?"* both have textual answers. The two cases the
  ticket names (**R4′**, **R1**) come out determinate, and so does CH-C's row (**R10**).
- **Test 2 stops being assertable, and it stops being unanswerable.** Firing it costs three things;
  answering it costs one of three, each of which can win. The ratchet is broken in both directions.
- **The refinement-vs-withdrawal line is drawn by the conjunct ADR-0033 already accepted** (D-c), not by
  new machinery. R-3's two poles (**R9** must not route, **R8** must) come out right for different,
  independently checkable reasons.
- **Withdrawals cannot hide.** The coverage lemma: convert your violators and they are in the exhibit;
  don't and test 1 fires. **R8** and **R11** are the two cases in the corpus where the governing sentence
  is inside the author's own hunk — and those are precisely the ones a post-hoc grep can never find.
- **The definition works on a modification and a deletion**, which no prior round's corpus contained a
  reliable instance of (**R1**, **R8**, **R11**, **R12**).

**More expensive (new obligations)**

- **The author writes the exhibit down.** A file list in `## Review`, on top of M2's catalog sweep. Test
  1 already produces most of it, but "most" is not "all", and this is a real per-edit cost on the
  cheapest lane in the process. **This is the first thing a challenger should price.**
- **The reviewer owes three things instead of one.** Some edits a reviewer distrusts will go inline
  because they cannot name the exhibit member.
- **One more concept on the developer-facing page** (Block A is ~20 lines where the warning was ~9).

**What could go wrong — plainly**

- **The residual moved down one level; it did not vanish.** *"Does this named file fall under these
  quoted words?"* can still split two careful readers **after** the widening rule is applied. M2's
  route-by-default is what catches that, and it is unchanged. **I am not claiming closure, only
  bounding.**
- **A sentence with no limiting clauses has a condition as broad as its words.** *"Never duplicate a
  `:core` component"* would reach a very large exhibit set on its own platform, and D1 would make it
  govern often. Whether that is correct (broad rules deserve defence before being carved) or
  over-firing, **one case does not settle**, and no row in this corpus tests it on the platform the
  sentence does cover. Stated, not waved away.
- **The exhibit can be narrowed by an author who mischaracterizes what the entry is about.** The
  mitigation is that the exhibit is the **diff plus the entry's own citations**, both checkable against
  the commit — but an author who writes a general sentence about a deliberately narrow diff has made the
  reviewer's job harder. **This is D1's cheapest attack and I do not claim it is closed.**
- **G3 is resolved permissively by construction.** Four iOS entries sit under an Android-worded preamble
  and D1 says the preamble does not reach them. That is the right reading of the text and the wrong
  state for the file. **G3 becomes more urgent under D1, not less.**
- **The greenfield residual is untouched.** CH-4's finding stands exactly as ADR-0033 recorded it: on a
  stack still being written, first statements route inline and whoever ships first sets the form. What
  holds that line remains ADR-0032's price plus reviewer-check 5.
- **R6 and R7 stay divergent**, and F4 declines the limb that was hoped to move them.

---

## How a reviewer verifies compliance

Adds to ADR-0033's list; replaces none of it.

1. **Firing side.** Routing an edit on the ground that a sentence governs? **Quote the sentence with the
   clause that carries its condition**, **name the exhibit member**, **name the disjunct**. Missing any
   of the three ⇒ test 2 has not fired on that sentence.
2. **The pre-edit snapshot.** The search runs against the catalog **as of the parent commit**. If the
   hunk itself deletes or rewrites a sentence, that sentence is a candidate — check the `-` lines before
   anything else. A grep of the merged file cannot see them.
3. **Floor side (M2, unchanged).** The author records the catalog file(s) + term searched, **plus the
   exhibit as a file list**. No search ⇒ the floor is not claimed ⇒ route.
4. **The widening challenge.** When an author answers **D-a**, they must name the clause the reviewer's
   reading dropped, or the term it swapped. *"That sentence is about something else"* with no clause
   named is not an answer.
5. **The refinement answer (D-c) is legitimate and is not a dodge** — but it must name all three:
   *no exception carved, nothing replaced, no named form forbidden.* Granting reach and denying conflict
   is a **stronger** claim than denying reach, and it is checkable against `S`'s text.
6. **Unresolved either way ⇒ route** (M2, unchanged).
7. Everything in ADR-0033 §"How a reviewer verifies compliance" items 1–7 still applies.

---

## Roles affected

No new code roles. **Reviewer** — reviewer-check 5 gains Block B. **Architect** — gains N-D's one line of
on-receipt procedure. The living companion `agents/architecture/decisions/catalog-governance.md` carries
the current shape and is updated when this is accepted; this round records only that the draft exists
and what it proposes.

---

## Follow-up tickets — specs, not files

> 🔴 **Lead ruling (2026-08-05): this draft is `rejected`, so N-A and N-B are DEAD — they land text from
> a rejected decision and no ticket may be filed from them.** Of this table, exactly one item survives
> and it was promoted: **N-C (G3)**, now the lane's one deliverable, at **twice** the size recorded here
> — §"Shared UI & theme" runs `:247`–`:500` and hosts **eight** iOS entries, and iOS proper starts at
> `:615`. **N-D, N-E and N-F are unchanged and unfiled**; N-F is the only thing that could reopen L1 on
> evidence. See §Verdict.

| # | Title | Layers / size | Sequencing |
|---|---|---|---|
| **N-A** | **Block A into `conventions.md`** — REPLACE the `:161-169` standing warning with the definition. Nothing else on the page changes | architect + docs, **XS** | on acceptance |
| **N-B** | **Block B into reviewer-check 5** (`.claude/agents/reviewer.md` step 5, test-2 bullet) — the firing-side burden, held by the round-1 verdict for this round | architect + docs, **XS** | **with N-A**, one commit — the two pages must not disagree in either direction |
| **N-C** | **G3 — scope `patterns-mobile.md` §"Shared UI & theme"** (`:247-455`): the `:249-253` preamble is Android-worded and hosts four iOS entries. One clause of scope, or a heading. **D1 makes this more urgent**, since the permissive reading now wins by rule | ios lane, **XS** | any time |
| **N-D** | **The catalog-row-vs-ADR question into the Architect's on-receipt procedure** — *"before ratifying a routed catalog entry, ask whether the rejected forms are **defects** (row) or **live options with real costs on both sides** (ADR); `T-0397-…md:70` is the worked precedent and its answer was 'row'."* Carried unchanged from the round-1 draft (unattacked). Target file is `architect.md` or `process/` — **PM's call** | architect + docs, **XS** | after acceptance |
| **N-E** | **T-0432 has no ticket file** in `agents/backlog/tickets/`, so its routing event is unrecoverable — and separately, **test 1 fires on it today**: `ProfileHubContent.swift:298` (`LogoutRow`) became a deviation it was not before, which is why `catalog-governance.md` §"Known live cases" books `CleansiaDangerButton` at `(gate pending: FT-5)` — *"a non-zero baseline, so `enforcement.md:104-106` forbids gating it today"*. **So R10 never reaches test 2 in a live application; my scoring of it is the counterfactual R-2 asked for, not a routing claim.** **Recorded, not re-opened** (T-0274/T-0473 precedent) | PM (record) | — |
| **N-F** | **The rate, re-specified.** For each hunk that adds a constraining sentence: score it under D1 against the catalog **at the parent commit**, classify the hunk insertion/modification/replacement, and source the routing event from the ticket. 94 commits; I read 13. **This can only strengthen or break the ADR** | architect, **S** | any time |

---

## What this ADR does **NOT** decide

- **It does not re-open** ADR-0033 D1 tests 1, 3 or 4, D2 (cross-stack strength), M1's silence, M2's
  evidence rule or route-by-default, M4, Block B or Block D's existing content.
- **It does not change ADR-0033's status.** ADR-0033 stays `accepted`; this is a refinement of test 2's
  first conjunct, recorded as a dated appended section on it.
- **It does not add a fourth routing test**, and it does not move ADR-0033's retro rows 5 and 7.
- **It does not re-open L3 or AC5** — both closed 2026-08-05 (T-0549 AC3, T-0551, and the record-only
  closure on ADR-0033).
- **It does not fix G3, G2, N1, F2 or F3** — recorded, routed, untouched.
- **It writes no `agents/knowledge/**` or `.claude/agents/**` file.** Blocks A and B are specifications
  with named appliers.

---

## Challenge

*Empty by construction — this is the author's draft. The challenger instance files here.*

**The five places I would attack first, named so silence on them reads as a choice**
(`deliberation.md`: *"a challenger that finds nothing says so explicitly and names what they checked"*):

1. **Alternative F is the soft spot.** *"Position never widens"* is an interpretive convention, and it
   is the single rule doing the work in **R10**. Its cost is a real permissive gap (G3). Argue that a
   section preamble does scope its section — and if you win, **R10 flips against history** and D1 owes
   an answer for the `## Shared UI & theme` case that does not come from a heading.
2. **The exhibit is only as honest as the diff's characterization.** Build an entry whose sentence is
   general and whose exhibit is narrow — a real narrowing that escapes because the author scoped their
   own change. If you can build it from the corpus rather than from imagination, that is the strongest
   available attack.
3. **R4′ is the case the whole ADR is aimed at, and I scored it on a clause-count argument.** I claim
   `S`'s condition is *"the untranslated-literal guard"* because every clause after the dash specifies
   literals. Argue that *"a screen with no test seam gets a source-text scan scoped to the file"* is one
   sentence with one condition and that the literal clauses are its *method*, not its scope. If that
   reads better, **R4′ routes** and AC2 is unmet.
4. **The negatives.** R3, R6, R9, R12 and R13 rest on searched absences over hunks I read, not over
   reconstructed pre-edit files. **N-F is the answer and I could not run it.** Any row you can flip with
   a sentence I missed changes the score.
5. **Block A costs 20 lines on the page a developer actually reads**, replacing a 9-line warning that is
   honest about its own gap. Argue Alternative H: keeping the warning is cheaper than teaching the
   definition, and this ADR blocks nothing.

**Explicitly NOT open (carried by accepted ADR-0033; do not re-litigate):** test 1; test 3; the floor;
M1's definition of silence; M2's evidence rule and route-by-default; D2's structural-vs-behavioural
line; the rejection of the wording-only trigger; the rejection of the topic-level reading of silence;
L3 and the limb-1 reversal (closed and applied).

## Defense

*Empty. **No `## Defense` was filed** — the author round did not run before this ruling, for the third
consecutive panel in this lane (`deliberation.md` step 3 has never executed here; ADR-0032, round 1 and
round 2 all closed on undefended artifacts). Recorded as a process finding in §Verdict, because it is
part of what the lane's cost is being measured against. It is **not** the ground of the ruling: no
defense restores a citation that does not say what it is quoted for, and none makes a phase commit
carry a per-ticket diff.*

## Verdict

> **Lead: a seventh instance.** Did not write ADR-0032 or ADR-0033, did not challenge either, did not
> write ADR-0033's independent lead pass, **did not write the round-1 draft, its challenge or its
> verdict, and did not write this round's draft or its challenge.** `deliberation.md` §"The roles".
> **T-0553 AC1 SATISFIED on composition** (author ≠ challenger ≠ lead, for the second round running).
>
> **Method.** No `Bash`. Evidence: the live tree (`agents/knowledge/patterns-mobile.md`,
> `patterns-backend.md`, `conventions.md`, `.claude/agents/reviewer.md`,
> `adr/challenges/0033-floor.md`, `adr/0033-…md`, `adr/challenges/0041-schema.md`, the ticket
> directory) and the coordinator-generated catalog-edit corpus. **I re-derived every load-bearing claim
> I rule on and I ran five checks neither party ran.** Three of them changed a finding's shape; two
> corrected the challenger. All are named below rather than folded in silently.

**Consensus: NOT reached. The draft is `rejected` as filed.** Two blocking findings stand (**CH-A**,
**CH-B**), a third is sustained on substance with its remedy refused (**CH-C**), and four more are
sustained as drafting or weight.

**And a second ruling, which is the one that matters more: L1 is CLOSED as a definition project.** Not
because the draft is bad — it is materially better than round 1 and the challenger is right about that
— but because **this round's own evidence dissolved the case for continuing.** The finding is **V-2**
below. A cheap, named re-open path is recorded at the end; it is not open by default.

| # | Disposition | One line |
|---|---|---|
| **V-2** *(new, this pass)* | **DECIDES THE LANE** | After CH-D, **the record contains no evidenced case in which a defined `governs` would have changed a routing outcome.** Case β dissolves three independent ways; G1/T-0527 routes under the **accepted** floor; the one live indeterminacy left is **G3**, a defect in a catalog file with an XS fix in the ios lane. |
| **CH-A** | **SUSTAINED — BLOCKING** | The subject question moved from `S` to `E`; it did not dissolve. And `"the ticket's diff"` is not mechanically recoverable here — I confirmed the second half of this myself. |
| **CH-B** | **SUSTAINED — BLOCKING on the citation. Its substantive alarm CORRECTED as overstated.** | The warrant is a mis-citation and must not ship. But nothing was smuggled: accepted ADR-0033 declares test 2 **semantic** on its face, and the disjunct has four determinate applications in the record. |
| **CH-C** | **SUSTAINED on substance — worse than filed. REMEDY REFUSED.** | Verified and enlarged: **eight** iOS entries under that preamble, not four. But the mechanism is misdiagnosed, the error direction is permissive both ways, and the position rule **carries no row** — so this is a defect *in the rule*, not something to fence with a condition on another lane's ticket. |
| **CH-D** | **SUSTAINED on limb 2** (limb 1 falls with CH-C) | Verified independently. Its consequence — not its finding — is **V-2**. |
| **CH-E** | **SUSTAINED as to weight; arithmetic stands** | ~3 discriminating rows, not 9. The R7/R11 clustering error is real, from the draft's own row labels. |
| **CH-F** | **SUSTAINED** | Verified `reviewer.md:114-118`. |
| **CH-G** | **SUSTAINED, and sharpened against the draft's premise** | `conventions.md:153-154` is not "M1's negative" — it is a **positive** limb of `governs` already on the operative page. |
| **G5** (the composition defect) | **SUSTAINED as an instance of CH-A, not as an independent defect — and corrected on evidence** | The mismatch is real and worked from the corpus. It is not a demonstrated pathology: the one case in the record resolved by **widening the enforcer** one ticket later. |
| **F4** (§F4, no fourth test) | **MOOT — not ruled** | It is offered as a consequence of D1. D1 is rejected, so the consequence has no premise. F4's disposition stays exactly where the round-1 verdict left it: **UNBEATEN, NOT SETTLED**. The eleventh trigger, built and killed here, is banked in the record. |

---

### V-2 — the finding that decides the lane: **the motivating evidence dissolved during this round**

L1 exists because ADR-0033's operative predicate has an undefined term. That is a true structural
observation and it has never been in dispute. What *was* in dispute is what it costs — and the panel's
own work has now answered that, in the direction nobody was arguing for.

**The two cases T-0553 AC2 names are the whole evidenced case for L1.** Track what happened to them:

| Case | State entering this round | State leaving it |
|---|---|---|
| **T-0473 / Case β** — the founding indeterminacy (`catalog-governance.md:104-113`, ADR-0033 §Ruling 1) | already dissolving twice over: the candidate sentence **post-dates** the entry by a day (`2012b014` vs `0e4ede1b`), and its paragraph **cites *"(the T-0473 rule)"* by name** | **dissolved a third time, on the sentence's own words.** I read it in the live file: `patterns-mobile.md:563-571` names **`${…}`** (Kotlin string templates; Swift interpolates `\(…)`) and **`R`**, and at `:569` the sentence *itself* — not merely its paragraph — reads *"because a resolver test does not cover the call site **(the T-0473 rule)**"*. It names the rule it is alleged to narrow, as a rule it composes with. |
| **T-0432 / R10** — the `:core` case | contested on scope (round-1 CH-C) | **still live, and it is now the only one.** But it is **G3**: a section preamble whose scope is undefined. Its fix is one clause of scope in `patterns-mobile.md`, in the ios lane, size XS. |

**And the strongest case in the whole record for this lane does not need a definition.** `G1`/T-0527
(`ab077504`) — a harvest that deleted a named canonical form, deleted a shipped Swift file and rewrote
a committed suite, `owner: qa`, inline. Three separate documents say the accepted floor already routes
it: `catalog-governance.md:428` (*"**No new definition is needed to catch it.**"*), the round-1 verdict,
and **this draft's own R8 row** (*"the **accepted** floor routes it too"*). What failed on G1 was that
nothing was watching — **L2**, which landed 2026-08-05.

**So the ledger, stated plainly.** In a corpus of 13 scored edits plus every case four prior passes
put in the record, there is **not one** where a defined `governs` would have changed a routing outcome.
On a *disputed* case, D1 and the status quo produce the same operative result — route — because M2's
route-by-default is accepted and D1's own Block A ends *"Unresolved either way ⇒ route."* On an
*undisputed-but-wrong* case, the only evidenced instance is Case β, and Case β answers itself three
ways.

**I want to state the counter-argument at full strength rather than as a footnote**, because it is
good: absence of evidence over 13 of 94 commits is not evidence of absence (**N-F**), and a rule in
force with an undefined operative term will eventually bite whether or not it has bitten. Both true.
What decides it against another cycle is the **direction** of the residual, which both parties agree
on: a missed governing sentence can only *add* firings, and an unresolved reading routes. The
undefined term therefore costs **argument-time and over-routing**, not safety — and `conventions.md`
says so on the page, honestly, today. That is a state a platform going to production can carry
indefinitely; three architect-rounds against it is not.

**This is a ruling about marginal value, and I am making it as one, not as a verdict on the draft's
quality.** The draft's own Alternative H says a lead who rules this way *"is not making an error"*; the
challenger says the exit should not be taken and gives one reason — *"the definition catches R8/G1 on
test 2"*. **That reason does not hold, and the draft it is defending is where I checked it.** An honest
refusal of the cheap exit is evidence and I weighed it as such; it is not load-bearing when its single
stated ground is contradicted by its own target.

---

### CH-A — SUSTAINED, BLOCKING. And one correction *in the author's favour* that changes what the repair is

**The finding stands, on two independent legs.**

*Leg 1 — limb (a)'s filter is a subject judgement.* D1 says the exhibit is *"every file this ticket
changed **that `E` declares canonical or withdraws a form from**"*. Answering that requires deciding
what form `E` declares canonical and which changed files carry it. That is *"what is this entry
about?"* — `E`'s subject, one level over from `S`'s. The draft asserts the opposite twice (*"the exhibit
**is** the diff"*, *"a fact about the change, not a characterization of it"*, *"No interpretation"*),
and both are true only of the **unfiltered** diff, which is not what D1 says. Drop the filter and ∃
ranges over the whole commit and D1 over-fires into C5's reductio; keep it and it is load-bearing.
The challenger is right, and its own concession is right too: the relocated question is **materially
cheaper** than round 1's, because it ranges over a written finite list rather than over nameable
artifacts.

*Leg 2 — the mechanical fallback is unavailable in this repo.* Verified, and **I ran the check the
challenger declared it had not** (its §"What I could not verify" item 4): `T-0548-…md`'s front matter
carries `owner`, `adrs`, `layers`, `security_touching`, `manual_steps` — **no file list**. So neither
end supplies the diff mechanically: the commit is a phase (`6bd3b0c6` carries T-0447 + T-0535 +
T-0546; `04f98937`, `365fd221`, `f0e39d7e` are phase branches), and the ticket carries prose. The
draft's own Gate 0.5 leg 3 item 6 concedes the consequence — *"Exhibit membership is derived from **the
ticket's stated scope** … not from reading the files"* — which means **every one of the 13 rows was
scored with exactly the input the definition says it does not use.**

**The correction, and it matters for what happens next.** R-2 was written as a **disjunction**:
*"Define scope without the word 'subject', **or concede it uses it and say who decides**."* The
draft claimed the first limb and did not meet it. **The second limb is open, sanctioned by the
previous lead, and cheap** — and both parties talked past it. So CH-A is a fatal **overclaim**, not a
fatal **rule**: the reach test survives the concession; what does not survive is the draft's headline
claim over Alternative A. An ADR that claims a closure it did not achieve is the specific thing this
panel rejected once already, so it cannot be accepted while the claim stands — but the distance
between "rejected" and "acceptable" here is a paragraph of honesty, not a new mechanism.

**I did not write that paragraph, and I will not.** Inventing the repair and then ratifying it is what
bound six consecutive instances in this lane, and it binds a second lead exactly as it bound the first.

---

### CH-B — SUSTAINED as to the citation (BLOCKING). Its substantive alarm is **overstated**, and I correct it on evidence

**The mis-citation is real and I verified it myself.** `challenges/0033-floor.md:32-34` reads *"Of the
two remaining disjuncts, one is decidable and one is not. **"Replacing a named canonical form"** is
checkable … "Withdrawing a form the catalog previously permitted" is not"*. The phrase **"carves an
exception out of it"** first occurs at `:73` — inside CH-1's own **repair text** — and CH-1 never
analyses it. The draft says CH-1 *"explicitly cleared"* it. It did not. In a decision whose entire
subject is not settling a reading by whoever quotes first, a load-bearing warrant that misreports its
source is disqualifying on its own. **Blocking.**

**Now the correction, and it runs against the challenger.** Its framing — that D1 *"inherits an
untested conjunct 2"* whose semantic character is a hidden import — does not survive the record:

- **Accepted ADR-0033 declares test 2 semantic on its face.** `adr/0033-…md:161` and `:538`: *"**Test 2
  is semantic.** A hunk with no imperative wording that nonetheless carves an exception out of a
  governing sentence fires test 2 all the same."* Both operative pages repeat it —
  `conventions.md:158` (*"The test is **semantic**"*) and `reviewer.md:115` (*"Semantic, not
  lexical"*). Nothing was smuggled; the draft inherited a property its parent ADR advertises.
- **The disjunct has four determinate applications in the record and one live one.** CH-1's own
  re-derivations at `:80-84` (T-0451 → Architect ✅, T-0441 → inline ✅); ADR-0033 retro rows `:118`
  and `:185`; the **T-0349** row at `:881`, which four passes across three rounds have attacked and
  none has dented; and — today, in another lane — `adr/challenges/0041-schema.md:124` uses
  *"carves an exception out of a sentence that already governs"* as an operative test. **What is
  missing is an argument for decidability, not a record of determinate application.**
- **And the load-transfer claim is measured against the wrong baseline.** *"Conjunct 1 now fires far
  more often"* is true relative to **round 1**. Relative to the **operative** baseline — a reviewer's
  paraphrase, unconstrained — D1's widening rule constrains conjunct 1 in the **restrictive**
  direction. Three of the draft's rows (R4′, R9, R10) turn on refusing a widened reading, which is
  precisely a *reduction* in firing.

**Ruled:** strike the citation; the claim *"decidable end to end"* falls with it and must be restated
as *"conjunct 1 decidable; conjunct 2 is accepted content carried forward untested."* The challenger's
own Ask says exactly this and it is the right one — its reasoning for why it matters is what I am
correcting, not its remedy.

---

### CH-C — SUSTAINED, and **worse than filed**. The mechanism is misdiagnosed, the direction is misstated, and the remedy is **REFUSED**

**Ground (i) of Alternative F is void.** Verified independently: no `T-0432-*.md` exists. A row the
table itself excludes for having no establishable history cannot be *"flipped against history"*. The
author's least-comfortable rejection rests on two grounds, not three.

**The section is worse than the challenger filed it.** I read the live heading structure myself:
`## Shared UI & theme` `:247` → `## Navigation — typed routes` `:501`, so the section runs `:247`–`:500`
(the challenger's range; the living doc's `:247`–`:455` and *"iOS starts at `:569`"* are both stale —
iOS proper starts at **`:615`**). Inside it I count **eight** iOS entries, not four: `:255`, `:366`,
`:386`, `:456`, `:463`, `:470` say **iOS** in their own titles, and `:292` (T-0451) and `:306` (T-0449)
are iOS by content (`Color.dynamic`, `AsyncImage`). **G3 is roughly twice the size the record carries.**

**But the mechanism is misdiagnosed, and that changes the remedy.** D1's rule is *"a sentence under a
heading carries **that heading's scope**"*. The heading is **`## Shared UI & theme`** — platform-neutral.
So is `## Strings & states` (`:507`), `## Navigation — typed routes` (`:501`) and `## Picking an image…`
(`:580`); the file's own title (`:1`) frames it as *"Mobile Patterns (Android … — iOS …) … The catalog
for **both** mobile platforms"*. **The Android-ness of `:249-253` is carried by the sentence's own
words** — `cz.cleansia.core.ui.components.*`, `` `:core` `` — **not by any heading.** Two consequences:

1. **The position rule as drafted is not merely unsound; it is inoperative.** It never says what *"a
   heading's scope"* is, and the only reading under which CH-C's unsoundness bites — heading *plus its
   preamble paragraph* — is a reading D1 does not state.
2. **Both halves of the rule err in the same, permissive direction.** Narrowing shrinks `S`'s
   condition, and reach is monotone in the condition, so wrongly narrowing can only **reduce** firing —
   exactly like refusing to widen. *"Unsound, not merely permissive"* is right as a soundness claim and
   **wrong as a consequence claim**, and the draft's G3 pricing is not as incomplete as the challenge
   says.

**And the finding that decides the remedy: the position rule carries no row.** R10 — the row Alternative
F is defended on — is decided by the **word-level** rule: reaching a Swift file from
`cz.cleansia.core.ui.components.*` requires substituting a broader term for one `S` names, which D1
forbids without any appeal to position. So the position clause is a rule that (a) decides nothing in
the corpus, (b) is undefined as written, and (c) returns wrong answers in the section supplying most of
the corpus.

**Remedy refused, on the challenger's own precedent.** Making acceptance conditional on **N-C/G3** — a
ticket in the **ios** lane, unfiled — is structurally the same move as ADR-0033 naming Block D as its
condition of acceptance and shipping at `(guidance — no gate)` for the whole of its acceptance
(`catalog-governance.md:61-66`). The challenger cites that precedent to argue *"do not do it twice on
the same clause"*, and it is right — which is an argument against conditions, not for one. **A rule
that returns wrong answers is fixed in the rule.** The author owes an answer here and there are at
least three (define *"a heading's scope"*; restrict the clause to headings whose own words carry the
scope; drop the clause, since no row needs it). **I name the option space; I do not choose from it.**

**G3 itself is real, live, and independent of this ADR** — it is the one surviving evidenced
indeterminacy in the record (**V-2**). It is routed as work below, at its true size.

---

### CH-D — SUSTAINED on limb 2. Limb 1 falls with CH-C, and I do not rest on it

Verified in the live file: `S` = `patterns-mobile.md:563-571` names **`${…}`** and **`R`**; its
immediate neighbours name `@Composable`, `BuildConfig.DEBUG`, `CleansiaNavHost`, `ProfileRowRoutingTest`
and `res/values/strings.xml`. Reaching `OrderDetailFooterStyle.swift` requires the **term substitution**
D1 forbids — which is the draft's own §Context table row 2 applied to row 1, and the draft does not
apply it. **The R4′ verdict holds; the clause-count reasoning offered for it does not carry it**, and
the author invited attack on the only ground that could have lost.

Limb 1 (*`S` is under `## Strings & states`, therefore Android*) is subject to CH-C exactly: that
heading is platform-neutral and the Android framing sits in its preamble. **Sustained on limb 2 alone**,
which needs no position rule and is the stronger ground the challenger itself identifies.

**One strengthening I add from my own read**, because it is sharper than either party's version:
dissolution #2 lives in **the candidate sentence itself**, not merely its paragraph — `:569` reads
*"**plus a call-site pin**, because a resolver test does not cover the call site **(the T-0473 rule)**"*.
The sentence alleged to narrow T-0473 names the T-0473 rule as a rule it composes with, in the same
breath. That is what feeds **V-2**.

---

### CH-E — SUSTAINED as to weight. The arithmetic is clean and the draft's discounting is a real advance

The draft is materially more honest than round 1 — it prints *"Do not read 13 as 13"*, discounts per
row **in the direction each cut**, and claims no rate anywhere. That is R-6 discharged and it should be
banked. What it does not license is the headline.

| Row | Why it does not discriminate | Ruled |
|---|---|---|
| **R1** | the pre-image **names its own routing** — *"a duplicated **VM** is a harvest-to-Core candidate — flag, **an Architect call**"*. The draft's own C-1 finding is the refutation of its own row | sustained |
| **R11** | routed by an **owner supersede of ADR-0022** + an architect sweep. **The round-1 lead already classified this exact commit as a *method* datum and expressly declined to book it as a routing datum.** Re-presenting it as a new agreement is the more serious half of this finding | sustained |
| **R2, R3** | **R-5 *requires* them.** Passing a required constraint is not nothing — round 1 failed it — but it is not evidence of discriminating power | sustained |
| **R4** | chronology alone | sustained |
| **R13** | *"inline under **BOTH** readings"*, by the draft's own scoring | sustained |

That leaves **R5, R9, R12** as rows where D1's own machinery does work a rival might get wrong (R4′
too, as a hypothetical, and on CH-D's ground rather than the draft's). **~3 discriminating agreements,
not 9.** And the clustering discount is short by one under the draft's own rule: **R7 and R11 are both
T-0379** — visible in the draft's own row labels — so ~10 routing events, not ~11, and the merged pair
straddles the agree/diverge line. Non-blocking; it is presentation, and it would have been a one-line
fix.

---

### CH-F, CH-G — SUSTAINED. CH-G corrects a premise of the draft, not just its Block A

**CH-F verified** at `reviewer.md:114-118`: the test-2 bullet states *"A sentence already governs this
subject at any level of generality"* with `governs` undefined, and the `:161-169` warning lives only on
the author's page. Block B appends a burden and no predicate. *"One commit"* fixes when they land, not
what they say — the **L5** finding one turn later.

**CH-G verified** at `conventions.md:153-154`, and it is sharper than filed. That sentence reads *"A
rule stated about the general case **governs** its sub-cases; carving out a sub-case narrows it whether
or not the sub-case was ever named."* **That is a *positive* limb of `governs`, on the operative page,
today.** So the draft's founding premise — *"M1 defined the **negative** … and never defined the
positive"* — is not accurate: M1 states one positive **sufficient condition**. D1 is compatible with it
(a sub-case of `S`'s condition-as-worded falls under that condition), but Block A would land *"widening
is not available"* a few lines under *"governs its sub-cases … whether or not the sub-case was ever
named"* **with nothing on the page saying how they compose** — and `catalog-governance.md:97-103`
records M1 being applied by re-describing a subject at a different level. Sustained.

---

### G5 / the composition defect — SUSTAINED as an instance of CH-A, and corrected on evidence

The mismatch is real and it is worth stating precisely, because the challenger's framing (*"two rules
pulling opposite ways"*) is not quite what is wrong. ADR-0032 D3 and D1 do not contradict each other.
The defect is a **measurement mismatch**: an entry's *constraining force* is keyed to **its sentence's
scope**, while its exhibit under D1 is keyed to **the files it converted**. So an entry can assert
broadly and exhibit narrowly, and D1 measures reach against the narrow one. That is the draft's own
named cheapest attack — and the challenger's contribution is that it is **not hypothetical**: T-0548 as
it landed (`97bb7265`) states *"size first, then anything that decodes, parses, hashes, or round-trips
the bytes"* and scopes itself *"Closed roster: it gates `ImageFileValidator` and `FileValidator` … The
other base64 intake paths … are **not** covered by it."* I verified that text in the corpus.

**Correction, from the live tree.** `patterns-backend.md:1229-1234` shows the same entry one ticket
later: **`Enforced by: … over the surface `Base64UploadIntakeRosterTests` enumerates — `T1-CI`**, and
*"Every base64 intake on every host runs the shared `BlobFileSize` predicate."* The author took ADR-0032
D3's **widen-the-enforcer** branch, not the narrow-the-claim one. **So the incentive gradient exists in
the text and the one worked case in the record resolved against it.** G5 is a seam to examine, not a
demonstrated pathology — and it belongs in the ADR, whose header says *"Consumes: ADR-0032"* and never
looks in this direction.

---

### What I tried to break, and could not

`deliberation.md`: silence is not assent. Round 1's credibility problem was overruling only its own
framings, so this is named for checking.

1. **The coverage lemma.** I attacked it where the challenger did not: on **R12**, whose hunk deletes
   and re-adds the sentence it cites, and on the asymmetry claim (*"the blind spot is the audit's, not
   the developer's"*). Both hold. An author searching the catalog they are editing **is** searching the
   pre-edit catalog, and where their hunk deletes a sentence they are holding it. **This is the best
   idea in the draft and nothing in this round touched it.**
2. **∃ over a finite written list.** R-1 is met and the negation is a bounded check. My attack was on
   the list's *objectivity* (CH-A), never on its finiteness, and the challenger's termination tests
   hold.
3. **The widening rule.** I tried to find a row where refusing to widen gives the wrong answer and
   could not — and it is the rule CH-D used to dissolve Case β, which is the single most consequential
   analytic result of this round. **It is independent of the exhibit (it constrains `S`, not `E`) and
   independent of the position clause.** Recorded as an asset, unratified.
4. **The challenger's reason for declining Alternative H.** I tried to make *"the definition catches
   R8/G1 on test 2, which is the case this whole lane exists for"* stand, and it does not — the
   draft's own R8 row, the living doc `:428` and the round-1 verdict all say the **accepted** floor
   routes it. Corrected against the party that filed it, against its own interest.
5. **CH-B's substantive alarm.** I went looking for evidence that *"carves an exception"* is
   undecidable in practice and found the opposite: four determinate applications plus a live one in
   `challenges/0041-schema.md:124`. Corrected against the challenger.
6. **CH-C's "unsound, not merely permissive".** I checked the monotonicity and it fails — both halves
   err permissively. Corrected against the challenger, and then found the section is twice the size it
   filed, which cuts the other way.
7. **My own ruling.** I built the case for *return to author* — the Defense step has never run in this
   lane, the challenger recommends acceptance-after-answers, and the repairs are two concessions and a
   clause — and it is a genuinely close call. What kills it is **V-2**, and V-2 is not my framing: it
   is the challenger's CH-D composed with the draft's own R8 row. **I am recording that I nearly ruled
   the other way.**

---

### Routing — what happens to the lane

**L1 is CLOSED as a definition project.** The operative rule is unchanged and stated on the operative
page: accepted ADR-0033 D1 + M1/M2, with `conventions.md:161-169`'s standing warning. Under it, a hard
case yields a recorded disagreement and **routes** (M2, accepted) — the safe direction, and the same
operative outcome D1 would have produced on a disputed case.

| What | Where it goes |
|---|---|
| **G3 — `patterns-mobile.md` §"Shared UI & theme" (`:247`–`:500`) hosts EIGHT iOS entries under an Android-worded preamble** | **PM → ios lane, XS, promoted from "any time" to the lane's one real deliverable.** This is the only surviving evidenced indeterminacy in the record. One clause of scope on `:249-253`, or a heading, retires it — and retires R10 with it. The living doc's `:247`–`:455` range and *"iOS starts at `:569`"* are stale; correct ranges are `:247`–`:500` and `:615` |
| **The firing-side burden (D2 / Block B)** — quote the sentence, name a file it reaches, name the disjunct | **Recorded as an available, UNRATIFIED asset. Nothing may cite it.** Held twice now, for a currency it may not actually need. It is **not** landed here: CH-F is sustained (a burden without its predicate is a paraphrase generator), and it is not accepted-ADR content, so it cannot ride a charter edit the way T-0549/T-0551 did |
| **The widening rule** — *a reading that drops a clause of `S`, or swaps a broader term for one `S` names, has widened it* | **Recorded as an available, UNRATIFIED asset. Nothing may cite it.** It proved itself as an analytic tool this round (CH-D). It is folklore until an ADR carries it, and this lane knows what folklore-with-a-citation costs |
| **F4** | Unchanged: **UNBEATEN, NOT SETTLED**. Its round-2 answer is a consequence of a rejected premise. The eleventh trigger is banked |
| **N-D, N-E, N-F** | Unchanged and unfiled. N-F remains the only thing that could reopen this on evidence |

**The re-open path, named so the door is not welded shut.** L1 reopens on **either** of two triggers,
and on nothing else: (1) **a real routing disagreement that route-by-default resolves wrongly** — one
recorded case where a reviewer fired test 2 on a widened reading and the author had no answer, or the
reverse; or (2) **N-F run to completion**, if it surfaces such a case. On either trigger the cheapest
next step is **not** a third author round from scratch: it is **one author Defense pass on this draft**,
which must (a) take R-2's second limb explicitly — concede that the exhibit is a written
characterization and say who decides when it is disputed; (b) strike the CH-1 citation and restate
*"decidable end to end"* as *"conjunct 1 decidable, conjunct 2 carried forward untested"*; (c) answer
CH-C on the position clause; (d) restate the headline per CH-E; (e) fix Block A/Block B per CH-G/CH-F.
Everything else in the draft survives on its merits.

**Escalation to the owner: none blocking.** No disagreement here carries lasting business impact and
every one was resolved on in-repo evidence. The one thing the owner or PM may want to overrule is the
**appetite** judgment in V-2 — three architect-rounds have gone into a predicate that has never changed
a routing outcome in the record, and I am ruling that a fourth is not owed. If the PM disagrees, the
Defense-pass path above is one instance, not three.

**Process finding, for the record.** `deliberation.md` step 3 — *the author defends* — has **never run
in this lane**: ADR-0032, round 1 and round 2 all closed on undefended artifacts, each time recorded
and each time immaterial to the ruling. Three occurrences is a pattern, not an accident, and it is
worth the PM knowing that this panel's shape in practice is *draft → attack → adjudicate*, with the
step that is supposed to convert an attack into a revision missing. That is why "return to author" was
a live option here and why the re-open path above is written as a Defense pass rather than a new draft.

---

## Gate 0.5, applied to a deliberation — what this draft could NOT verify

**Leg 1 (mutation-prove the test): DOES NOT APPLY.** The evidence is a routing rule whose subjects are
Markdown edits; `quality-gates.md:67-70` scopes leg 1 by the evidence and directs this case to be
declared. **Leg 2 (a cached run is not a run): DOES NOT APPLY** — no suite, build or checker was run.

**Leg 3 — named:**

1. **No `Bash`.** I read the coordinator-generated corpus, not `git`. Commit hashes, dates and hunk
   headers are read off its diff blocks; I did not re-derive them.
2. **No whole pre-edit file was reconstructed.** The corpus carries hunks, not snapshots, so every
   *"no sentence governed"* is searched over the hunks I read plus today's tree — **not proven**.
   Discounted per row in §"The score, discounted"; **R6's verdict rests entirely on such a negative.**
3. **13 of 94 commits read.** No rate is claimed anywhere in this draft. N-F is the measurement.
4. **R10's routing event is unrecoverable** — no `T-0432-*.md` exists. It is excluded from the count.
5. **R2's routing event is compound** (ticket `owner: ios`; refusal recorded in ADR-0033 `:51`) and
   **R11's is three events** (T-0379 architect, T-0429 ios, an owner supersede of ADR-0022). Both are
   scored on the recorded ruling, and both are flagged.
6. **I did not open the iOS/Android/Swift/Kotlin source for any row.** Exhibit membership is derived
   from the ticket's stated scope and the entry's own citations, not from reading the files. For **R9**
   I relied on the round-1 challenger's verified `ProfileViewModel.kt:179-180` /
   `ProfileViewModelTest.kt:635` citations only as corroboration — **D1's verdict on R9 comes from `S`'s
   Android vocabulary and from D-c**, neither of which needs that code.
7. **Line numbers are this worktree's**, and `patterns-mobile.md` is a live shared-file lane. Every
   load-bearing citation quotes its text or its hunk header.
