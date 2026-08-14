# Catalog Governance — living decision doc

**Topic:** how the `agents/knowledge/*` catalog acquires, states, and enforces its rules.
**ADRs:** [ADR-0032](/decisions/adr-0032)
(`accepted` 2026-08-01 — the price of a law) ·
[ADR-0033](/decisions/adr-0033)
(`accepted` 2026-08-05, amended by the T-0471 panel — catalog-edit authority) ·
[ADR-0018](/decisions/adr-0018) (the T3-HUMAN precedent).
**Process:** [`process/enforcement.md`](../../process/enforcement.md) ·
[`knowledge/conventions.md`](../../knowledge/conventions.md) §"Harvest good patterns back into the catalog".

---

## The problem this area exists to solve

The catalog is a **living** document that any developer may extend while they hold the context. That is
deliberate and worth protecting. It creates two failure modes that are not the same problem and do not
have the same fix:

1. **Authority drift** — a ticket unilaterally redefines "the one way to do X", and the codebase ends
   up carrying two canonical forms with no canonicalization ticket. (Real: T-0274 obliged seven shipped
   `.models.ts` resolvers while self-classifying as a clarification.)
2. **Enforcement opacity** — a reader cannot tell, from an entry, whether anything is watching, or
   whether the named watcher watches what the sentence claims. (Real: the `CleansiaWeb` no-literal-domain
   entry claims the whole iOS tree; `ConsentCatalogTests` asserts **two sentences × five locales**.)

ADR-0033 addresses (1). ADR-0032 addresses (2). They compose on the same hunk and are checked together.

---

## Current shape

### A constraining entry states its enforcement (ADR-0032, accepted)

```
**Enforced by:** <named enforcer> — <tier token>
```

| Tier | Fails the build? | Where it lives |
|---|---|---|
| `T1-CI` | **yes** | a test in a CI job; on iOS a SwiftLint `custom_rules` entry or an XCTest guard in one of the three CI schemes; a `check-consistency.mjs` rule **once that stack's checker step is in its workflow** |
| `T2-ADVISORY` | no | `check-consistency.mjs` **today, on every stack** (verified: zero hits under `.github/`) · **any ESLint rule on the web stack**, because `frontend-ci.yml:72-74` runs lint with `continue-on-error: true` |
| `T3-HUMAN` | no | a **named** standing-checklist item (Gate-DP §G, Gate-AR, a numbered reviewer-check) |
| `(gate pending: <ticket>)` | not yet | the gate is specified; a live violation blocks it per the zero-baseline rule; promotes on the ticket |
| `(guidance — no gate)` | no | nothing is watching, and the entry says so |

**The two rules that carry the weight:**

- **T1-CI is owed only where the rule is mechanizable AND the baseline is zero.** Not because the
  sentence is imperative. Imperative framing buys nothing, so nobody is tempted to launder a law into
  "the canonical form is X".
- **The named enforcer's assertion must cover the scope the sentence claims** — narrow the sentence
  (stating the residual) or widen the enforcer. A tree-walking guard must fail on an empty corpus or a
  missing anchor, or it is not an enforcer.
- **A tier token naming a mechanism that *cannot fail a build* is `T2-ADVISORY`, however it is worded**
  (added by the T-0471 round). Two live cases, both verified: `check-consistency.mjs` is in **zero**
  `.github/` workflows, and the frontend lint step is `continue-on-error: true`. This is the same
  failure shape as D3's overclaim, moved from *scope* to *severity*: a step that reports and never
  reddens has told a reader nothing, exactly as a green gate asserting two sentences had.

### What routes to the Architect (ADR-0033, accepted 2026-08-05) — **IN FORCE from 2026-08-05**

> ✅ **The condition of acceptance is met.** ADR-0033 named Block D — the reviewer-check — as its own
> condition of acceptance and was accepted with that condition unmet; it sat at `(guidance — no gate)`
> for the whole of its acceptance. **T-0549 / T-0550 / T-0551 landed it, 2026-08-05.** State now:
>
> | | State |
> |---|---|
> | `.claude/agents/reviewer.md` step 5 — what a **reviewer** runs | **reviewer-check 5 "Catalog-edit routing"** — ADR-0033 Block D verbatim. The superseded axis is gone from the page (T-0549 AC1) |
> | `agents/process/quality-gates.md` Gate 1 | carries the one-line pointer to reviewer-check 5, so the check is reachable from the gate list and not only from the charter (T-0549 AC2) |
> | `agents/process/enforcement.md` §"Enforcement tiers" | names reviewer-check 5 by id under `T3-HUMAN`, with what it governs and what falls if it is deleted (T-0550) |
> | `agents/knowledge/conventions.md` — what an **author** applies | the superseded numbered list is **replaced**, not appended to; §"Who ratifies a catalog edit — the routing test" carries the three tests + the floor + `**Enforced by:**` (T-0549 AC3 + T-0551) |
> | ADR-0033 itself | carries a dated **record-only closure** recording the landing and correcting the false *"does not reverse"* header claim |
>
> **Tier: `T3-HUMAN`, and that is the honest ceiling.** Nothing in ADR-0033 is mechanically enforced and
> nothing claims to be — `check-consistency.mjs` is in zero `.github/` workflows and `frontend-ci.yml`
> runs lint with `continue-on-error: true`, so no mechanism this rule could reach can fail a build. What
> makes it a tier at all is that the checklist item is **named** and greppable (`reviewer-check 5`).
>
> ⚠️ **In force does not mean settled: the floor's own predicate is undefined, and the panel that tried
> to define it is CLOSED.** **Two** attempts to define *"governs"* were `rejected` by their panels on
> 2026-08-05 — round 1 (the conflicting-instance test) and round 2 (reach over the entry's exhibit) —
> and the lead on round 2 **closed L1 as a definition project** (see **L1** in Open items). So test 2
> routes on the reviewer's **paraphrase** of a general sentence, and that is the settled operative
> state, not a gap awaiting a fix. **What makes it survivable is M2:** an unresolved reading **routes**,
> which is the safe direction and is the same operative outcome either rejected definition would have
> produced on a disputed case. `conventions.md:161-169` says so on the page. **`ab077504` is what the gap costs**: a harvest that
> deleted a named canonical form, deleted a shipped Swift file and rewrote a committed test suite went
> inline under `owner: qa`, one day after this ADR was accepted — and note *that* case needed no new
> definition. **The accepted floor already routed it; nothing was watching. From today something is.**

Three ordered tests; first to fire routes: **(1)** does it put shipped code in violation? **(2)** does
it *narrow* latitude the catalog previously left open? **(3)** does it make a *prescriptive* claim
about a stack the ticket never built and ran? Otherwise: inline, flagged in `## Review`.

**The floor on test 2, in its amended (T-0471) form** — the four sentences that carry the weight:

1. **The unit is a *sentence*, not a topic.** Test 2 fires when a catalog sentence **already governs
   this entry's subject at any level of generality** and the entry carves an exception out of it,
   replaces it, or forbids a form it named.
2. **Silence is defined.** "The catalog was silent about X" = *no sentence covers X at any level of
   generality* — **not** *no sentence names X specifically*. This is what closes the sub-case dodge
   (T-0451's "theme-invariant surfaces" is a sub-case of the Android→iOS mapping row that states
   `CleansiaColors` slots *are* `Color.dynamic(light:dark:)` pairs — **`patterns-mobile.md:588`**, cited
   as `:577` in the ADR before drift; T-0451 adds `onFixedWhite`, a slot deliberately **not** a dynamic
   pair, so it carves an exception out of what that row states. The phrase being new does not make the
   catalog silent).
   > ⚠️ **Known residual (finding L1, lead pass 2026-08-05): M1 defines *silence* and never defines
   > *governs*.** The disambiguator that actually decides a hard case is *"does the candidate sentence,
   > **applied to this entry's subject**, yield a prescription the entry contradicts?"* — and it is **not
   > in the ADR**. Worked both ways on real hunks: **T-0349** (address-picker VM) is determinate and fires
   > — `patterns-mobile.md:990` says *"feature/VM import no MapKit"*, the entry says one such import *"is
   > allowed"*; history routed it to the Architect ✅. **T-0473** (`:265-276`) is **not** determinate —
   > `:520-522` names a *file*-scoped source-text scan and the entry forbids *"a whole-file `contains`"*,
   > so one reviewer fires test 2 while another (correctly) sees a prescription for a different subject
   > that yields nothing here. Until L1 lands, routing on a general sentence rests on the reviewer's
   > **paraphrase** of it — the same defect CH-1 removed from the other half of the clause.
   >
   > 🔴 **The first repair was DRAFTED, CHALLENGED and REJECTED — 2026-08-05, panel closed.**
   > `agents/archive/2026-08/adr-deliberation/drafts/NNNN-what-makes-a-catalog-sentence-govern.md` proposed the **conflicting-instance
   > test** (*`S` governs `E` iff a reader can name one concrete artifact both reach on which their
   > verdicts differ*). **It is `rejected` and is not an ADR.** The blocking defect, found by the lead
   > pass on the draft's own table: the definition is an **existential** (*some* artifact ruled
   > differently) and six of its ten rows were scored with a **compose** test (*an* artifact satisfying
   > both). Read existentially it flips **accepted ADR-0033's own retro row 2** (T-0441, `inline` ✅) to
   > *Architect*; read compositionally its single new catch (T-0449) disappears. **There is no reading of
   > it under which its own validation table comes out as recorded** — which is CH-1's defect (*a
   > predicate true and false of the same edit*) relocated from the word "permitted" onto the word
   > "governs". Two supporting falsifications, both from diffs: its only claimed catch is a **false
   > positive** (the Android artifact it names is excluded by `ProfileViewModel.kt:179-180` +
   > `ProfileViewModelTest.kt:635`, shipped by the governing sentence's **own** ticket), and its
   > corpus method **greps the post-edit tree**, so a sentence an edit **deletes** — the highest-signal
   > instance of test 2 there is — is structurally invisible to it.
   >
   > **So L1 was open and unrepaired after round 1 — and after round 2 it is CLOSED as a definition
   > project (2026-08-05, round-2 lead, V-2).** Routing on a general sentence rests on the reviewer's
   > paraphrase, and that is now the ruled operative state; M2's route-by-default is what makes it safe.
   > What a re-opener must clear is still **R-1…R-7** in that draft's §Verdict; the
   > file stays on disk as the record of what was tried.
   >
   > 🔴 **Round 2 was DRAFTED, CHALLENGED and REJECTED — 2026-08-05, panel CLOSED, and L1 is closed
   > with it.** `agents/archive/2026-08/adr-deliberation/drafts/NNNN-what-makes-a-catalog-sentence-govern-round-2.md` proposed
   > **`governs` = REACH, carrying no verdict term** — *`S` governs `E` iff at least one member of `E`'s
   > **exhibit** (the ticket's diff + the entry's own `file:line` citations) falls under `S`'s
   > **condition**, read at the narrowest scope `S`'s own words support*, ∃ over that finite list, with
   > **widening `S` past its own words unavailable**. **It is `rejected` and is not an ADR.** Two
   > blocking findings: **(CH-A)** the exhibit's limb (a) filter — *"every file this ticket changed
   > **that `E` declares canonical or withdraws a form from**"* — **is** `E`'s subject, so the subject
   > question moved one level rather than dissolving, and the mechanical fallback is unavailable here
   > (this repo lands by **phase**: `6bd3b0c6` carries three tickets; ticket front matter carries no file
   > list — checked on `T-0548`), so all 13 rows were scored from the ticket's *stated scope*, the one
   > input the definition says it does not use; **(CH-B)** its warrant that the conflict conjunct is
   > decidable **mis-cites** `challenges/0033-floor.md` — CH-1 cleared *"replacing a named canonical
   > form"* only, and *"carves an exception out of it"* first appears at `:73` inside CH-1's own repair.
   > **What the lead corrected against the challenger, on evidence:** nothing was smuggled — accepted
   > ADR-0033 declares test 2 **semantic** on its face (`:161`, `:538`; `conventions.md:158`;
   > `reviewer.md:115`) and the disjunct has four determinate applications in the record plus a live one
   > (`challenges/0041-schema.md:124`); and the position rule's error direction is **permissive in both
   > halves**, not one.
   >
   > 🟢 **Case β is RETIRED as a discriminating case — it now dissolves THREE independent ways, and the
   > third decided the lane (round-2 lead, V-2).** (1) **chronology** — the candidate post-dates the
   > entry by a day; (2) **self-citation** — the candidate's paragraph names *"(the T-0473 rule)"*; and
   > (3) **platform, on the sentence's own words** — the candidate is `patterns-mobile.md:563-571`, it
   > names **`${…}`** (Kotlin string templates; Swift interpolates `\(…)`) and **`R`**, it sits under
   > `## Strings & states` (`:507`, *"All user text in `res/values/strings.xml`"*) while iOS proper does
   > not start until `:615`, and at **`:569` the sentence itself** reads *"because a resolver test does
   > not cover the call site **(the T-0473 rule)**"*. Reaching a Swift entry from it requires substituting
   > a broader term for one it names. **Three panel rounds and two lead passes argued over this sentence
   > and none of them read the section it is in.** Do not price L1 off it; do not use it as any
   > definition's validation. ✅ **Chronology (dissolution 1), from diffs.** `:520-522` was introduced by
   > **`2012b014`, 2026-08-02**; the T-0473 entry by **`0e4ede1b`, 2026-08-01**. **The candidate sentence
   > post-dates the entry by one day**, so Case β is **not** a historical mis-routing — it is a live
   > hypothetical about today's text. L1 stands anyway (one reproducible indeterminacy on the current
   > text is a counter-example), but the evidence is weaker than §Ruling 1 presents it, **and worse:
   > `2012b014`'s added paragraph cites *"(the T-0473 rule)"* by name and names `OrderDetailCardStringsTest`
   > in the same sentence.** Reading the *entry* rather than the *sentence* answers Case β, so **it does
   > not discriminate between any two candidate definitions** and must not be used as one's validation.
3. **Claiming the floor costs one grep, and the default is route.** The author records the catalog
   file(s) + term searched in `## Review`, symmetric with test 1's code sweep. **A floor claimed with
   no search is not claimed.**
4. **Inline is not free.** A floor-qualifying entry has a **zero baseline by construction**, which is
   ADR-0032's second `T1-CI` condition — so a mechanizable rule owes its gate in the same ticket, and a
   mechanism that cannot fail a build is `T2-ADVISORY` however it is labelled.

**Enforced by:** reviewer-check **5 "Catalog-edit routing"** (`.claude/agents/reviewer.md` step 5) —
**T3-HUMAN**, landed 2026-08-05 (T-0549). Recorded by id in `process/enforcement.md` (T-0550) so a
charter edit that drops it reads as a regression against an accepted ADR rather than as tidying, and
pointed at from `quality-gates.md` Gate 1 so a reviewer arriving with an `agents/knowledge/*.md` diff
reaches it without knowing the charter. **The author's page, the reviewer's page and five of the six
charters that teach the harvest loop now teach one axis and it is ADR-0033's** — the four developer
charters and `architect.md` were brought over on 2026-08-05 (L5).

⚠️ **Verify with the multiline form, not the flat one.** `grep -rn 'one way to do X'` is a *lexical*
probe for a rule whose whole repair was to stop being lexical, and it misses any site where the phrase
wraps across a line break — which is exactly how `.claude/agents/db.md:62-65` survived four rounds of
counting. Use:

```
rg -U 'one\s+way\s+to\s+do\s+X' .claude/ agents/knowledge/ agents/process/   # or: rg 'way to do X'
```

Expected: no hit that *instructs* routing. Hits inside `docs/decisions/` are legitimate — an
accepted ADR records what was decided then, and ADR-0033 must quote the rule it reverses.

---

## The trade-off space (why the shape is this shape)

**The axis that was argued: how hard should a "the ONE way" declaration be to make?**

| Position | Cost per law | What it gets wrong |
|---|---|---|
| Nothing — write what you like | zero | the status quo; ~22 iOS laws, ~3 naming any enforcer, one of those three over-claiming |
| **Name an enforcer + declare a tier** ← **chosen** | one line | nothing found; it is the cheapest thing that makes the difference visible |
| Require a CI-blocking gate | one test file (~190 lines, unamortized) | contradicts ADR-0018 (unmechanizable laws exist and are load-bearing); collides with the zero-baseline rule on exactly the entries with a live violation; the governance rule could not discharge itself |
| Downgrade unenforced stacks to guidance | zero | concedes the catalog's job on the stack with two shipping apps; iOS is not unenforced, it is T3-HUMAN-enforced |

**The alternative that nearly ate the decision, and why it did not.** The consistency checker's loud
`NOT RUN` banner for a zero-file scope delivers *stack-level* coverage visibility to every reviewer, at
the point of use, at zero marginal cost per law — strictly cheaper than anything a rule could charge.
It is ratified, not re-decided. But it answers *"does this tool read this stack?"*, never *"does this
entry's enforcer assert what the sentence claims?"* The `CleansiaWeb` case is the proof: that gate
exists, runs in CI, and is **green** while asserting a fraction of its sentence. Stack-level tool
honesty and entry-level enforcement honesty are orthogonal, and they compose.

**The distinction that keeps the harvest loop open.** `conventions.md:132` sets the bar for *any*
catalog entry at "makes the codebase more consistent" — which is, literally read, forbidding the
inconsistent alternative. So "does it forbid something?" fires on everything. ADR-0033's floor is the
repair: **adding** a canonical form where **no sentence governed the subject at any level** is inline;
**carving an exception out of a sentence that did** is a law.

**The floor REVERSES the old first limb — RESOLVED 2026-08-05 by replacing the list, not appending to it
(finding L3).** The rule ADR-0033 refines read *"a **new canonical archetype** **or** anything that
changes 'the one way to do X' … → **Architect** call"* — a **disjunction**. The floor routes a first
statement of a canonical form **inline**, and a first statement of a canonical form *is* a new canonical
archetype. ADR-0033's own retro row 7 proves it: T-0379's `format: date` row routes inline here and was
routed to the Architect in fact, on the ground that it *"defines the one way for date-only wire on iOS"*.
The ADR argued past this by quoting only the second limb, and its Block C said *"insert **after** the
existing numbered list"* — which, applied literally, would have left the reversed limb standing and put
two incompatible routing instructions on one page: F5's disease installed by the edit whose purpose is to
stop authority drift.

**How it was resolved** (T-0549 AC3 + T-0551, applying the T-0553 panel's severed Block C):

| | |
|---|---|
| Operation | **REPLACE** the numbered list, not append beside it. The old bullets are gone; nothing on the page instructs the old axis |
| Limb 1 (*new canonical archetype → Architect*) | **reversed, and the page says so** — a standing callout tells a reader who remembers the old wording exactly what changed and that *"what changed is the price, not the permission"* |
| Limb 2 (*changes "the one way" → Architect*) | **survives** as test 2 (narrowing a governing sentence) |
| The **third category** neither old bullet described | named: old bullet 1 scoped the inline lane to *"a **small** clarification/addition to an **existing** rule"*, which excludes the first statement the floor sends inline. Test 4 now says the inline lane covers **both** — which also repairs ADR-0033 D1 test 4's self-miscitation (*"unchanged from step 2, first bullet"* — it was not) |
| Step 3 (supersession → `consistency.md` deviation) | **untouched**, and the harvest action was kept **inside** its branch (CH-G): a developer whose edit routes to the Architect no longer arrives at an unconditional *"write it into the catalog"* |
| ADR-0033's *"does not reverse"* header claim | **corrected on the ADR**, via a dated record-only closure — meaning, not digits, so not the erratum lane (`adr/README.md:16-29`). The denial is **not** propagated into the developer-facing page |

**Where the floor's first wording broke, and why the current one is different (T-0471, 2026-08-05).**
The draft floor turned on *"a form the catalog previously **permitted**"*. The catalog does not permit;
it prescribes — so "permitted" was inferable only from silence, which is the *same* condition that
routes an edit **inline**. The predicate was true and false of the same edit, and it split the ADR's
own founding case: retro row 3 claims T-0451 withdrew `Color.dynamic` ink, but the sentence that
"permitted" it (`patterns-mobile.md:588`; cited as `:577` before drift) is a descriptive mapping row
that never mentions theme-invariant surfaces. Read one way, C5 was unrepaired; read the other, the T-0451 refusal that
generated both ADRs was wrong. The repair moved the unit from the *topic* to the **sentence** — and the
topic-level reading was explicitly rejected, because a sub-case is always a fresh topic if you are
allowed to choose the vocabulary.

**The trade the floor makes, stated because it is real.** Both limbs look **backwards**: it prices the
cost to shipped code and prices the cost to future code at zero. So its discriminating power is
proportional to how much code already exists, and it is weakest on the newest stack — exactly where
"the one way" is being written fastest. Two historical cases (T-0397's header idiom, T-0379's
`format: date` row) are first statements the process routed to the Architect and this rule sends
inline; both were ratified in substance, but one of those rounds **added content the row lacked**. What
holds the line is not the routing test — it is ADR-0032's price attaching to the inline lane, plus a
reviewer who is actually told to look.

> ⚠️ **Read those two cases with the 2026-08-05 caveat.** Their *"no sentence governed"* column was
> produced by grepping the **post-edit** tree, which is the error that falsified retro row 8 — so it is a
> one-sided under-estimate in both. And they are **two routing decisions, ruled on one day by one
> architect** (T-0397 on 2026-07-19; T-0379's `format: date` item routed **2026-07-04** at a fix-round-3
> review and ratified *as-is* on 07-19), landed by one commit, `f0e39d7e` 2026-07-20. They remain the
> best-known cases of the floor's backward-looking trade **and they are not two independent
> confirmations of it.**

| Position on the floor | What it costs | Why not |
|---|---|---|
| No floor (C5's unfloored test 2) | everything routes | the inline lane dies; `conventions.md`'s harvest loop closes |
| Floor on "previously **permitted**" | nothing | undecidable — the catalog prescribes, so "permitted" collapses into "silent" |
| Floor on the **topic** ("has the catalog discussed X?") | nothing | every sub-case is a fresh topic if you pick the vocabulary; T-0451 escapes as "theme-invariant surfaces" |
| **Floor on a governing *sentence* at any level of generality** ← **chosen, and still the operative rule** | one grep, recorded | **two** residuals: the author may search at the wrong level of generality (mitigated by route-by-default), and **"governs" is itself undefined** (finding **L1 — CLOSED as a definition project 2026-08-05**, after **two** rejected repair rounds; the residual is ruled survivable because an unresolved reading routes) |
| …with **"governs" = a nameable conflicting artifact** ← **REJECTED 2026-08-05** | one grep **+ one artifact when a reviewer objects** | It moves the residual from *"what is the subject?"* to *"can an artifact be named?"* — narrower and checkable, **and it does not close either degree of freedom it opens.** (1) **Quantifier:** stated as an existential (*one* artifact ruled differently ⇒ governs), scored as a universal (*an* artifact satisfying both ⇒ does not govern). Read as stated it flips ADR-0033's retro row 2 (T-0441 `inline` ✅) to *Architect*; read as scored, its only new catch vanishes. (2) **Granularity:** "reach" is defined as *"inside the scope `S` prescribes for"* — which **is** `S`'s subject, the very thing Alternative A was rejected for relocating. Demonstrated inside its own table (`patterns-mobile.md:249-253` read as an Android rule vs as the `## Shared UI & theme` preamble it was inserted under, `4d8b3978`) |
| …with **"governs" = REACH: one member of the entry's finite exhibit falls under `S`'s condition, read at `S`'s narrowest supported scope** ← **REJECTED 2026-08-05 (round 2)** | one grep against the **pre-edit** catalog **+ the exhibit written as a file list**; firing it costs a quoted condition + an exhibit member + a named disjunct | `drafts/…-round-2.md`, `rejected`. It **did** close round 1's two degrees of freedom — ∃ over a finite written list (R-1), and `S` read at its own words with **widening unavailable** — and it discriminated R-3's pole pair. **Why it fell:** the exhibit's limb (a) filter (*"…that `E` declares canonical or withdraws a form from"*) **is** `E`'s subject, and *"the ticket's diff"* is not recoverable in a repo that lands by **phase**, so R-2's first limb was claimed and unmet (R-2's *second* limb — concede it and say who decides — was open, sanctioned, and unused); and its warrant that the conflict conjunct is decidable **mis-cites** the challenge it quotes. Two ideas survive as **unratified assets, citable by nothing**: the **coverage lemma** (a withdrawal either converted its violators — they are in the exhibit — or did not, and **test 1** fires; it cannot hide from both) and the **widening rule** (a reading that drops a clause of `S`, or swaps a broader term for one `S` names, has widened it), which is what dissolved Case β a third time |
| …with **"governs" defined so that R-1…R-7 are met** ← **CLOSED, not open** | unknown | Two rounds tried and both are `rejected`. The round-2 lead **closed L1 as a definition project (V-2)**: after Case β's third dissolution, **the record contains no evidenced case in which a defined `governs` would have changed a routing outcome** — on a disputed case M2 routes either way, `ab077504`/G1 routes under the **accepted** floor, and the one live indeterminacy left is **G3**, a catalog-file defect with an XS fix. The bar (R-1…R-7, rejected round-1 draft §Verdict) stands unchanged **for whoever reopens it**, and the reopen triggers are named in the round-2 §Verdict — not a third draft, but **one author Defense pass** on round 2 |
| Route every first statement (Alternative B) | the harvest loop | rejected in the original panel; nobody argued for it in the re-check |

**The eighth retro case, produced by the lead pass — the first one where the "any level of generality"
limb does the work.** ADR-0033's table has seven rows; in six of them test 2 fires (or not) on a
sentence that **names the specific form**. **T-0349** (`patterns-mobile.md:1244-1254`, "the address-picker
= one Core VM, app-local Views — the one way") is the case M1 was written for and did not have:

| | |
|---|---|
| Test 1 | **does not fire** — the edit converts its own violator (the duplicated partner picker VM) in the same change. This is retro row 3's shape: *the floor is the only thing standing.* |
| Test 2 | **FIRES.** Governing sentence, quotable and general: `patterns-mobile.md:990` — *"feature/VM import no MapKit"* (the catalog form of ADR-0013 D6 invariant #7). The entry carves an exception: *"the **only** sanctioned feature-layer `import MapKit` is the View's binding … that View touch **is allowed**"*. |
| Routes to | **Architect** |
| Actual ruling | **Architect** ✅ — `T-0349-…md` is `owner: architect`, `layers: [ios, architect]`, with a `## Architect ruling (2026-06-30)` reasoning from the same invariant (`:97-99`) |

**Why it matters:** it is direct evidence that a *general* sentence can carry a determinate test-2 verdict,
which is the whole content of M1 and was previously supported only by row 3 — the row CH-1 showed was
carried by an assertion. It is also the counterweight to L1: the limb works when the governing sentence,
applied to the entry's subject, yields a prescription the entry contradicts. When it does not (T-0473),
the limb goes indeterminate. Same test, two outcomes, and the ADR does not name the difference.

> ✅ **Chronology confirmed from diffs (2026-08-05), and this is now the only load-bearing row in the
> corpus nobody has dented.** `:990` was introduced by **`76fc48ab` 2026-06-27**; the T-0349 entry by
> **`04f98937` 2026-06-30** — the governing sentence predates the entry by three days, so the row is
> immune to the post-edit-tree error. Two challenger passes and two lead passes attacked it and none
> moved it. **Any candidate definition of "governs" must keep this row firing**, and it is the natural
> positive control for the rebuilt corpus.

**The ten-case corpus (L1 draft, 2026-08-05) is WITHDRAWN — it must be rebuilt, not patched.** It was the
first pass to run a candidate definition rather than re-read the record, and that was the right move. It
does not survive the diffs, for three reasons in ascending order of force:

1. **It was not scored under the definition it validates.** Six of ten rows return *"does not govern"*,
   and rows 1 and 3 establish that by naming an artifact that **satisfies both** sentences — which is not
   the negation of *"some artifact is ruled differently"*. Every row must be re-scored under whatever
   predicate replaces D1; **no verdict transfers.**
2. **The search ran against the wrong snapshot.** Candidate sentences were located by `Grep` over
   `agents/knowledge/` **as the tree stands today** — i.e. *after* each entry landed. That error is
   one-sided: it can only *miss* governing sentences, never invent them, so every *"none nameable ⇒
   inline"* is an under-estimate of test 2's firing rate. **Row 8 (T-0527) is falsified by it**:
   `ab077504` carries **three** hunks, and the third (`@@ -1272,9 +1315,9 @@`) *deletes* the sentence
   that named `CancellationFeePreview` and the client-side tier ladder as the shipped canonical form. The
   accepted floor's decidable disjunct — *"replaces it, or forbids a form it named"* — fires on it
   without needing any new definition. It shipped **inline**, `owner: qa`, `adrs: []`, and it deleted a
   shipped Swift file and rewrote a committed test suite on the way.
3. **The blindness is wider than deletions: it hides the pre-edit text of any *modified* sentence.**
   `f0e39d7e`'s `@@ -313,12 +313,12 @@` shows retro row 6's "entry" is a **modification** — the
   developer's cell landed `365fd221` 2026-07-11 and the T-0397 ratification **appended** the
   fix-round-8 settle pin plus its signature. Today's tree shows only the merged result, so a
   two-party, two-date artifact reads as one entry.

**Per-row state after the diff audit** (rows 2, 4, 5 are positives and survive — grep's error is
one-sided; they may be carried into the rebuild without re-derivation):

| Row | Landing commit(s) | Hunk shape | State |
|---|---|---|---|
| 1 T-0441 | `1d85b35f` 2026-08-01 | insertion | **flips** under D1's literal text |
| 2 T-0451 | `1c8fdd00` 2026-08-01 | insertion | **stands** (positive) |
| 3 T-0473 | `0e4ede1b` 2026-08-01 | insertion | **vacuous as history** — candidate post-dates it (`2012b014`) |
| 4 T-0349 | `04f98937` 2026-06-30 (`:990` from `76fc48ab` 2026-06-27) | ~~insertion~~ → **MODIFICATION ×4 hunks** (corrected 2026-08-05, round 2) | **stands** — the strongest row; unattacked successfully by anyone. **And it was mis-classified by every prior round:** the load-bearing hunk **rewrites** the pre-existing *"Deviations a reviewer rejects: a feature/VM `import MapKit`/`CoreLocation` (the §7.6 seam — the picker file is the only sanctioned consumer)"* clause into *"…for map/geocode **logic**"*. The pre-image is the strongest candidate `S` in the corpus **and it names its own routing** — *"a duplicated **VM** is a harvest-to-Core candidate — flag, **an Architect call**"*. The catalog routed this edit in the sentence the edit rewrote |
| 5 T-0397 `.medium` | `365fd221` 2026-07-11 → `f0e39d7e` 2026-07-20 | insertion + append | **stands. ✅ Date relation SETTLED 2026-08-05 (round 2), after three rounds open:** the `.medium` **grant** is a *context* line in **`04f98937`'s `@@ -632,10 +634,15 @@` (2026-06-30)** — the same hunk that carries row 4's pre-image — so it predates the withdrawal by **≥11 days** |
| 6 T-0397 header | `365fd221` 2026-07-11 → `f0e39d7e` 2026-07-20 | **modification** | unfalsified, **unreliable** |
| 7 T-0379 `format: date` | `e97b14e7` 2026-07-05, **never modified since** | insertion | unfalsified, **unreliable** |
| 8 T-0527 | `ab077504` 2026-08-04 | insert ×2 + **REPLACE** | **FALSIFIED** |
| 9 T-0449 | `0e4ede1b` + `4f81dce7` | insertions | **FALSIFIED** — see below |
| 10 T-0432 | `4d8b3978` 2026-07-22 | insertion under the Android preamble | **contested** (scope) |

**Row 9 — the one new divergence the draft claimed to have found — is a FALSE POSITIVE, and it is the
most useful row in the corpus for the opposite reason.**

| | **T-0449** `patterns-mobile.md:319-329` — *"the guard is released by a successful render"* |
|---|---|
| Claimed governing sentence | `:562-565` (T-0448, **Android**) — *"refetch the profile once, **guarded by the `fileName` already retried**"* |
| Claimed artifact | an Android `ProfileViewModel` whose retry watermark is set once and **never cleared** |
| Why it fails | **That artifact was shipped against and tested against by the sentence's own ticket.** `ProfileViewModel.kt:179-180` — `fun onAvatarLoadSucceeded() { avatarRetriedFor = null }`; pinned by `ProfileViewModelTest.kt:635` *"a successful load restores the retry budget"*. `4f81dce7`'s own text cites Android's method **as the existing model**. Nothing was withdrawn from Android; `:562-565` is an **incomplete summary** of shipped behaviour and the 2026-08-05 append **completed the description** |
| What it actually proves | The two readings of D1 give **opposite answers on this row**: existential ⇒ fires (the hypothetical VM is nameable) ⇒ diverges from history; compose ⇒ does not fire (`ProfileViewModel.kt` satisfies both) ⇒ agrees. **Same row, same evidence, opposite verdicts** — the quantifier is doing all the work |

**Two things that survive from it.** `:562-565` really is a **stale summary of its own shipped code** —
`4f81dce7` corrected the iOS paragraph and left the Android one standing, which is F5's disease (two
forms on one page) live today; and the same entry independently fires **test 3** — filed as **N1**,
re-scoped (see Open items). **Whether the class is a *case* or a *rate* is still unmeasured**: 41 commits
touch `patterns-mobile.md` and the 2026-08-05 pass read the diffs of eight. What is established is a
**direction**, not a number.

---

## Current tier census (iOS — the corpus ADR-0032's FT-4 triages)

Verified on `master`, 2026-08-01, in `agents/knowledge/patterns-mobile.md` (1093 lines):

| Measure | Count |
|---|---|
| lines matching "the ONE way" | **22** (+1 "The ONE sanctioned way", `:191`) |
| entries closing with a "Deviations a reviewer rejects:" list | **~20** |
| occurrences of the string `Tests` in the entire file | **4** (`:205`, `:269`, `:348`, `:517`) |

So: **~22 iOS laws, ~3 naming any enforcer.** FT-4 labels the corpus (a labelling sweep, not a
gate-writing sweep — which is what makes it affordable). Expected distribution: mostly `T3-HUMAN`
against a named reviewer-check, a few `T1-CI`, and `(gate pending:)` where a live violation stands.

**Known live cases:**

| Entry | Status | Note |
|---|---|---|
| `CleansiaDangerButton` (`:233`) | `(gate pending: FT-5)` | partner `ProfileHubContent.swift:298-320` (`LogoutRow`) hand-rolls the component — a non-zero baseline, so `enforcement.md:104-106` forbids gating it today |
| `CleansiaWeb` no-literal-domain (`:266-270`) | **overclaims** → FT-2 | sentence is tree-wide; `ConsentCatalogTests:54-64` asserts 2 keys × 5 locales. Baseline for the real rule is **zero** (one literal, `CleansiaWeb.swift:8`), so a `custom_rule` can be T1-CI on day one |
| `SnackbarPill` (`:243`) | likely **not a law** | component-internals prose; needs accuracy, not an enforcer |
| Ink on a theme-invariant surface (T-0451) | `T1-CI`, roster of 2 | `FixedWhiteContrastTests` + `AvatarDiscBindingTests`; residual enumerated by FT-3 |

**Tooling scope facts a tier claim must respect:**

- `.swiftlint.yml:1-5` lints `CleansiaCore/Sources`, `CleansiaCore/Tests`, `CleansiaPartner/Sources`,
  `CleansiaCustomer/Sources` — **not** `CleansiaCustomer/LiveActivity/` (1 file) or either app's
  `Tests/` (65+ files). A `custom_rule` claiming "the iOS tree" must widen `included:` or state the
  residual.
- `check-consistency.mjs` walks `.cs`/`.ts`/`.kt` only (`:387`, `:502`); no `ios` stack key; appears in
  **no** `.github/` workflow. **It can never set a blocking exit code on any stack** — re-verified
  2026-08-05.
- **`frontend-ci.yml:72-74` runs lint with `continue-on-error: true`** — so an ESLint rule is
  `T2-ADVISORY` on the web stack too, whatever the entry says. The house model for saying so honestly
  is `patterns-frontend.md:462-465`: *"**T2-ADVISORY**, because `frontend-ci.yml` runs lint with
  `continue-on-error: true`; promotes to `T1-CI` with the rest of the lint baseline."*
- **The general rule this yields (ADR-0033 reviewer-check 7): a tier token naming a mechanism that
  cannot fail a build is `T2-ADVISORY`, however the entry is worded.** The two live examples are the
  checker in zero workflows and the non-blocking lint step; assume there will be others.
- The XCTest guard idiom (`#filePath` walk out of the package) is duplicated per guard with no shared
  harness — FT-6 extracts it so the third guard costs less than the second.

### The gap between an accepted rule and an applied one (measured 2026-08-05)

The T-0471 round measured how far ADR-0032 had actually travelled four days after acceptance. This is
the number to re-measure, not to trust:

| Measure | Count |
|---|---|
| `Enforced by:` in `agents/knowledge/` — **strict** form (colon straight after `by`) | **10** *(re-measured 2026-08-05 by the L5 pass; was **9** at the close of T-0551, **7** at the lead pass)* — `consistency.md:343`, `:436`; `patterns-frontend.md:462`, `:579`; `patterns-backend.md:638`, `:729`, `:1229`, **`:1269`**; `conventions.md:197` (this rule) + the template at `:222`. **The tenth is another lane again**: `patterns-backend.md:1267-1270`, T-0556's *"the declared content type is a HINT; the bytes are the evidence"*, `T1-CI` against `SaveMyDocumentsHandlerTests` + `DocumentFileValidatorTests` and scoped honestly to *"`SaveMyDocuments` only"* — a model of ADR-0032 D3 (the enforcer's assertion covers the scope the sentence claims). **Three measurements, three numbers, and every increment came from a lane this doc was not tracking** — 7 → 9 → 10. The practice is spreading, which is ADR-0032's point; the corollary is that **a count here is a timestamp, not a fact.** Re-run it; never cite it |
| …counting the `roles/` **variant** spellings that omit the colon | **+2** — `roles/post-commit-effects.md:32` (*"**Enforced by** (ADR-0032 D2 …"*), `roles/order-availability.md:130` (*"**Enforced by `TC-TAKE-ONE-ERROR`**"*). **The label is still not uniform enough to grep one way — know this before FT-4 counts anything.** |
| `**Enforced by:**` in `patterns-mobile.md` — the ~22-law file ADR-0032's Block A and FT-4 target | **0** — **unchanged, and deliberately so.** This lane added the label to exactly one entry: the governance rule itself, which is the rule discharging its own rule. Nothing here implies the label belongs on the iOS corpus yet — that is **FT-4**, a labelling sweep in the iOS lane, and it still has nothing to build on |
| Catalog entries added to `patterns-mobile.md` *after* ADR-0032 was accepted, carrying no enforcer + tier | at least **1** — `:265-276` (T-0473), which constrains call sites (*"hoist it one level further"*) and forbids a form (*"not a whole-file `contains`"*) |
| Pages that still teach the **superseded** routing axis | **1** *(was 7 — 2 fixed by T-0549/T-0551, 5 by the L5 pass)*. The one that remains is **`.claude/agents/db.md:62-65`**, and it is **invisible to the grep this doc has been re-running**: the phrase wraps across the line break (`…redefining "the one` / `way to do X" is an Architect call.`), so `grep -rn 'one way to do X'` returns **zero** for it. It surfaced only on widening to `way to do X`. **Fourth consecutive round in which the count grew, and the first in which whitespace hid the growth** — re-verify with the multiline form above, not the flat one |

The T-0473 entry's ticket self-classified as *"a testability clarification, not a redefinition"*
(`T-0473-…md:337-339`) — the same self-classification as T-0274 (`:133`), two sprints later, on the
same failure mode. **That was the empirical case for FT-11**, and it is now answered: a governance rule
whose only home is a page the checker does not read is folklore with a citation, so the rule was moved
onto the page the reviewer actually runs. **What that does not buy is detection strength.** The check is
`T3-HUMAN`; it fires only when a reviewer reads the diff and remembers to run it. The measurement worth
repeating next sprint is not *"does the check exist"* — it does — but *"did the next `agents/knowledge/`
hunk after 2026-08-05 arrive with a recorded catalog sweep and an enforcer label?"*

---

## Open items

| Item | Owner | Where |
|---|---|---|
| ~~The floor on ADR-0033's test 2 needs one adversarial round~~ — **CLOSED 2026-08-05** (T-0471): challenged, amended M1–M6, `accepted` | architect panel | ADR-0033 §Verdict · `adr/challenges/0033-floor.md` |
| ~~AC1 — the round needs a lead distinct from the challenger~~ — **CLOSED 2026-08-05**: third instance ran it; ADR-0033 §"Independent lead adjudication" | architect | ADR-0033, appended section |
| 🟢 **L1 — CLOSED 2026-08-05 as a definition project, after TWO rejected rounds. `governs` stays undefined, and that is now the RULED operative state, not a gap awaiting a fix.** Round 1 (the conflicting-instance test, `adr/drafts/NNNN-what-makes-a-catalog-sentence-govern.md`) fell on **V-1**: it states an existential and scores six of ten rows with a compose test, so read as stated it flips ADR-0033's retro row 2 (T-0441 `inline` ✅) to *Architect*, and read as scored its only new catch (T-0449) is a false positive. Round 2 (`adr/drafts/…-round-2.md`, `governs` = **reach** over the entry's exhibit) got materially further — **R-1, R-3, R-4, R-5, R-6 met**, corpus rebuilt against **parent commits** with per-row discounts naming their direction — and fell on two blocking findings: **CH-A**, limb (a)'s filter (*"every file this ticket changed **that `E` declares canonical or withdraws a form from**"*) **is** `E`'s subject, and *"the ticket's diff"* is not recoverable in a repo that lands by **phase** (`6bd3b0c6` carries three tickets; ticket front matter carries no file list — checked on `T-0548`), so all 13 rows were scored from the ticket's *stated scope*, the one input the definition says it does not use, and **R-2's first limb was claimed and unmet** (its second limb — concede it and say who decides — was open, sanctioned and unused); and **CH-B**, the warrant that the conflict conjunct is decidable **mis-cites** `challenges/0033-floor.md` (CH-1 cleared *"replacing a named canonical form"* only; *"carves an exception out of it"* first appears at `:73`, inside CH-1's own repair). **The closure ruling is V-2 and it is about marginal value, not draft quality:** after Case β's third dissolution, **the record holds no evidenced case in which a defined `governs` would have changed a routing outcome** — a disputed reading **routes** under M2 either way, `ab077504`/G1 routes under the **accepted** floor (three documents say so, including the round-2 draft's own R8 row), and the only live indeterminacy left is **G3**, a catalog-file defect with an XS fix. **Reopen triggers, and nothing else:** (1) one recorded routing disagreement that route-by-default resolves wrongly, or (2) **N-F** run to completion surfacing one. On either, the next step is **one author Defense pass on the round-2 draft** — five named repairs in its §Verdict — not a third draft from a blank page. **Both drafts stay on disk as the record; neither is an ADR; no ticket may cite either.** | closed (architect panel) · reopen → PM | round-2 draft §Verdict (V-2) · round-1 draft §Verdict (R-1…R-7) · ADR-0033 §Ruling 1 |
| 🟡 **L5 — APPLIED 2026-08-05 on the five charters it named. ONE residual site remains, and it is a new class of miss.** All five now teach ADR-0033's axis by **quoting** `conventions.md` §"Who ratifies a catalog edit" instead of paraphrasing it (five paraphrases of one rule is how this drifted in the first place), and each names the enforcer by its greppable title **`Catalog-edit routing`** — not "reviewer-check 5", which markdown emphasis breaks as a literal. `backend.md:77-91` / `frontend.md:67-80` / `android.md:67-80` / `ios.md:60-73` carry **one identical block**, and it states the axis change **in both directions** rather than merely dropping the old trigger: *more* passes inline (the lane now *"covers both a clarification inside an existing rule's scope **and** the first statement of a canonical form where nothing governed the subject"*) and *fewer* narrowings do (semantic, not lexical — *"the canonical form is X" narrows exactly as much as "the ONE way is X"*), closed by the price, so the deletion left no permissive vacuum. **`architect.md:85-105` was rewritten, not search-and-replaced**: the paragraph described what the Architect does *on receipt*, so the reversed limb was replaced with the three-test **intake condition**, an explicit *"novelty alone does not route it to you … you gate the narrowing, not the novelty"*, and a per-test on-receipt procedure (test 1 → ratify + `consistency.md` deviation + canonicalization ticket; test 2 → it is a law, so the entry carries `**Enforced by:** <named enforcer> — <tier token>`; test 3 → an ADR or a ticket that built and ran that stack, else downgrade to a *descriptive* note with a file:line citation in the entry). **RESIDUAL — `.claude/agents/db.md:62-65` still teaches the superseded axis**, the same bullet the four developer charters carried, and the lane was authorised for five charters only so it was **not touched**. **The finding is the method, for the fourth round running:** the inventory command L5 itself prescribed is **lexical**, and `grep -rn 'one way to do X'` **cannot see `db.md`** because the phrase wraps across the line break. A rule whose repair was to stop being lexical was being audited lexically. The sweep that actually enumerates: `rg -U 'one\s+way\s+to\s+do\s+X' .claude/ agents/knowledge/ agents/process/`, or the shorter `rg 'way to do X'`. Verified this pass: both forms are **zero** across the three instruction-carrying trees except `db.md` | PM → db charter (one XS hunk; the shared block is already drafted on the other four — copy it, adjusting only the catalog file name to `consistency.md`) | this pass · T-0549 AC1 evidence line |
| 🟢 **L2 — CLOSED 2026-08-05. FT-11 landed and ADR-0033 is in force.** `T-0549` (both pages + Gate 1) · `T-0550` (FT-12, the check id in `enforcement.md`) · `T-0551` (FT-8, the severed Block C) all applied in one serialized pass over the shared `conventions.md` section. `T-0552` (F1 erratum) is **still open** — a different lane. **What is closed is the enforcer's existence, not its strength**: reviewer-check 5 is `T3-HUMAN`, so the next measurement is whether the first post-2026-08-05 `agents/knowledge/` hunk arrives with a recorded catalog sweep and an enforcer label | architect + docs | ADR-0033 §"Record-only closure" |
| 🔴 **G1 — a catalog harvest deleted a named canonical form INLINE, under `owner: qa`, with no Architect (T-0527, `ab077504` `@@ -1272,9 +1315,9 @@`).** It removed the sentence naming `CancellationFeePreview` + the client-side tier ladder as the shipped form, deleted `CancellationFeePreview.swift`, deleted `CancelOrderSheet.kt`'s ladder and **rewrote a committed test suite that pinned the old schedule**. Under the **accepted** floor this fires test 2's *decidable* disjunct — *"replaces it, or forbids a form it named"* — the half `challenges/0033-floor.md` CH-1 explicitly cleared as checkable. **No new definition is needed to catch it.** **Recorded, not re-opened** (T-0274/T-0473 precedent — the substance is right, only the routing was not taken); it post-dates ADR-0033's acceptance by a day, **which is the point: this is the sharpest evidence in the record for L2** | PM (record) → the L2 lane | corpus `ab077504` · challenge CH-A |
| 🔴 **G3 — PROMOTED 2026-08-05 to this lane's one real deliverable, and it is TWICE the size previously recorded.** `patterns-mobile.md` §"Shared UI & theme" runs **`:247`–`:500`** (next heading `## Navigation — typed routes` at `:501`) and hosts **eight** iOS entries under an Android-worded preamble (`:249-253`, *"Use `cz.cleansia.core.ui.components.*` … never duplicate a `:core` component"*): `:255`, `:366`, `:386`, `:456`, `:463`, `:470` say **iOS** in their own titles, and `:292` (T-0451) / `:306` (T-0449) are iOS by content. **iOS proper starts at `:615`, not `:569`.** *(Both prior figures in this doc — "four entries", "`:247`–`:455`", "`:569`" — were stale; corrected by the round-2 lead from the live file.)* `4d8b3978` inserted the first iOS blockquote **immediately below** the preamble. So one sentence has two defensible scopes in the file the routing test is applied to most — and after **L1's closure this is the ONLY evidenced live indeterminacy in the record** (Case β dissolves three ways; G1 routes under the accepted floor). **It is also not fixable by any definition of `governs`:** the round-2 draft's answer to it was a *position* rule (*"a sentence under a heading carries that heading's scope"*) which is **inoperative as written** — every heading in this file except `## iOS — SwiftUI/MVVM parity port` is platform-neutral, the file's own title (`:1`) frames it as *"the catalog for **both** mobile platforms"*, and the Android scope of `:249-253` is carried by the sentence's **own words** (`cz.cleansia.core.ui.components.*`, `` `:core` ``), not by a heading. **One clause of scope on `:249-253`, or a heading, retires G3 and retires the T-0432/R10 case with it.** Size XS | **PM → ios lane (structural catalog edit) — the lane's deliverable** | round-2 §Verdict CH-C · round-1 challenge CH-C |
| 🟢 **L3 — CLOSED 2026-08-05, applied as ruled (T-0549 AC3 + T-0551).** The list was **replaced, not appended to**; limb 1's reversal is stated in the developer-facing page instead of denied in the ADR header; bullet 1's *"small clarification to an **existing** rule"* scope and the third category it excluded are both repaired in test 4; `:128-130` (supersession) survived untouched; **CH-G fixed** — the harvest action is back **inside** its branch. The D1 *"what governs means"* paragraph was **EXCISED** and **accepted ADR-0033's floor wording stands verbatim** in its place (a deletion + a quotation, not authorship), with a visible pointer that *"governs"* is under repair. **Two things stayed held, on purpose:** the **Block D reviewer-side addendum** (*"if you say a sentence governs, quote it and name the artifact"*) — empty until "artifact ruled differently" has one reading — and any definition of *"governs"* itself. The header correction rode a **dated record-only closure** on ADR-0033, not the erratum lane (`adr/README.md:16-29` — meaning, not digits). **No ADR number was allocated**: what the panel called "its own small ADR" landed as an application of already-accepted ADR-0033 content plus two deletions, so there was no new decision to number — if a later reader disagrees, the missing artifact is an ADR, not a re-edit | architect (closed) · PM (number call, if any) | the rejected draft §D4 / §Block C′ · T-0551 · ADR-0033 §"Record-only closure" |
| ~~**FT-11 — land the named enforcer (Block D)**~~ — **DONE 2026-08-05 (T-0549).** reviewer-check 5 in `.claude/agents/reviewer.md` step 5 (Block D verbatim) + the Gate 1 pointer + the author's page. **This was the one charter edit in the lane; it changes how every future review behaves** | closed | ADR-0033 §Block D / §"Record-only closure" |
| ~~**FT-12 — record the check id in `enforcement.md`**~~ — **DONE 2026-08-05 (T-0550).** Named by id under the `T3-HUMAN` bullet, with its home file, what it governs, and the sentence that makes deleting it a regression rather than a cleanup | closed | ADR-0033 §Follow-ups |
| ~~**FT-8 — Block C into `conventions.md`**~~ — **DONE 2026-08-05 (T-0551)**, as the **severed** block: the rejected D1 definition excised, ADR-0033's accepted floor wording verbatim in its place, the numbered list replaced rather than appended to, CH-G fixed, the Block D addendum held | closed | ADR-0033 §Block C · L3 row above |
| 🟢 **F1 — CLOSED 2026-08-05 (T-0552), and the instrument is NOT the one the finding named.** Both statements are stale (`:14` — *"ADR-0033 is `proposed`, not accepted"*, made stale by the T-0471 round itself; `:23-25` — 0031 *is* on `master`, `acf2f0bc`/PR #175) and both were **true when written**, so neither is a transcription error and the **erratum lane is unavailable** (`adr/README.md:16-29` — *"for digits, not meaning"*). Landed as a **dated record-only closure** appended to ADR-0032, plus two bracketed dated **pointer** annotations (the ADR-0031 V9 form, signed by the closure) at the two header-block lines a reader actually consults. **Status stays `accepted`; no clause altered.** The rule the closure states, because it has now been drawn twice in four days on this pair: *an erratum corrects the ADR against its own source; a closure records that the world moved.* **The third occurrence of the same phrase — the `## Verdict` table's **C5** row (cited as `:624` before the annotations shifted it) — was deliberately left alone**: it pins what the panel ruled, and ADR-0031 §A's rule (*"leave citations that pin what was ruled on"*) forbids touching it. FT-7 (rename the file to the amended title) is untouched and still unfiled | closed | ADR-0032 §"2026-08-05 record-only closure" |
| **F2 — ADR-0032's Block A was never applied**; `**Enforced by:**` has 0 instances in `patterns-mobile.md`, so FT-4 has nothing to build on | PM → ios lane | ADR-0032 §Block A |
| **F3 — `patterns-mobile.md:265-276` (T-0473)** carries no enforcer + tier. **Its test-2 question is now closed on a stronger ground than the lead pass had:** the candidate governing sentence `:520-522` **did not exist** when the entry landed (`2012b014` 2026-08-02 vs `0e4ede1b` 2026-08-01), so nothing governed the subject and the entry **routes inline** — the suspected mis-routing is not merely unestablished, it is refuted. What stands is the **missing enforcer + tier** and the **unrun test-1 sweep** (14 in-tree guard tests read source as a fixture and none was opened for the withdrawn shape). **Recorded, not re-opened** — T-0274 precedent | PM | ADR-0033 §Ruling 1, Case β · corpus `2012b014` |
| 🟡 **F4 — no "carries a trade-off ⇒ ADR" limb** in the routing test. **Ruled 2026-08-05: UNBEATEN, NOT SETTLED — no fourth test today, and the question re-opens automatically when the corpus is rebuilt.** **Eight triggers built and killed** and the record must keep them so nobody re-derives them: *two by the author* (*"two live forms that both ship"* — subsumed by test 1; *"the entry states a cost of the form it chooses"* — fires on `:559-561`, harvested inline); *six by the challenger* (T-α correct-but-rejected-form · T-β build/tooling config · T-γ ≥2 recorded fix-rounds · T-δ closed set of ≥2 lockstep call sites · T-ε deviates from the other platform's shipped form · **T-η adds an iOS-obligation cell to the parity table** — the only one that hits the stated target, and still a **location** trigger, i.e. the wording trigger accepted ADR-0033 already closed, over-firing on most of `:578`–`:715`); *two by the lead* (*"the entry ratifies an **owner**-directed form"* — misses row 7, and an owner ruling routes by existing machinery; *"the cost is borne by someone other than the codebase"* — fires on the negative control immediately). **Two riders, both against the draft:** ground **(a)** (`T-0397-…md:70` shows the Architect asking it *after* routing) is **STRUCK** — a censored-sample inference, since Architect rulings are the only place a routed decision is written down at all; and **the prize is mis-specified** — rows 6 and 7 are two decisions ruled by one architect on one day and their "governs?" column came from the invalidated procedure. **The case a fourth test should be measured against is row 8** (T-0527: a named canonical form deleted from the catalog, a shipped file deleted, a committed suite rewritten, `owner: qa`, inline) — **and a fourth test does not reach it; the accepted floor already routes it and nothing ran.** That is **L2**'s evidence, not F4's. **🟡 RE-OPENED IN THE ROUND-2 DRAFT AND RULED MOOT BY ITS LEAD (2026-08-05).** The draft answered F4 *as a consequence of its D1* — under the proposed definition row 8 routes on test 2 from the sentence its own hunk deletes, so *"F4's re-specified target is caught by the limb it was proposed to supplement"*. **D1 is `rejected`, so the consequence has no premise and the lead did not rule on it.** What is banked: an **eleventh** trigger built and killed against rows 6/7 (*"the entry ratifies a form whose losing alternative is documented as shipping elsewhere in the catalog"* — fires on row 6, not on row 7). Ground (a) stays **struck**; `T-0397-…md:70` is re-read as evidence for an on-receipt **procedure** (follow-up N-D), not a routing test. **Disposition unchanged from the round-1 verdict: UNBEATEN, NOT SETTLED. Eleven triggers on the record — do not re-derive them** | architect (dormant with L1) | round-2 draft §F4 + §Verdict · the rejected draft §D3 · challenge §D3 |
| 🟡 **N1 — RE-SCOPED 2026-08-05 (it was filed too wide).** Two claims, two different repairs. The `Android onAvatarLoadSucceeded` citation is **structural and verifiable by reading** (`ProfileViewModel.kt:179-181`, wired at `CleansiaNavHost.kt:430` / `MainShell.kt:302` / `EditProfileScreen.kt:148`, pinned by `ProfileViewModelTest.kt:635`) — under ADR-0033 D2's own line (*can the next reader verify this by reading what is in the repo?*) that is **descriptive**, missing only its file:line, i.e. **Block B's two-line shape**, not a routing failure. What survives as **prescriptive** is the narrower clause *"**Both platforms** plumb the pair through **every** surface that draws the disc"* — a forward obligation on Android written from an iOS ticket. **N1 = that clause only.** Riding with it: **G2 — `patterns-mobile.md:562-565` (Android, T-0448) is a stale summary of its own shipped code.** `4f81dce7` corrected the **iOS** paragraph (`:319-329`) and left the Android one standing, so the page carries two descriptions of one behaviour — F5's disease, current. **Both recorded, not re-opened** (T-0274/T-0473 precedent) | PM → ios/android lane, with FT-9 | the rejected draft §Retro row 9 / N-E · challenge CH-F, G2 |
| ~~**F5 — `patterns-mobile.md` still grants the `.medium` detent**~~ — **FIXED 2026-08-05** by the lead pass. Confirmed real at `:1241` (cited `:1230` before drift) against the withdrawal at `:996-1001` (cited `:985-990`), **and factually stale**: shipped is `CodeSheetShell.swift:29` `.fixedSize(horizontal:false, vertical:true)` + `:36` `.presentationDetents([.height(contentHeight)])` + `:78` `CodeSheetHeightKey: PreferenceKey`. The withdrawal survives; the grant is retracted with a dated in-place erratum note | closed | `patterns-mobile.md:1241` |
| FT-1 verify the `NOT RUN` banner on merge of `fix/tooling-false-green-and-broken-docs` | tooling | ADR-0032 §Follow-ups |
| 🟡 **FT-2 — the `custom_rules` bootstrap half is DONE; the `included:` half is not.** Verified 2026-08-09 against the file: `src/cleansia_ios/.swiftlint.yml:27` declares `custom_rules:` with **two** entries — `combine_assign_retains_target` (`:28-36`) and `calendar_day_needs_greenwich` (`:38-46`), both `severity: error`, therefore CI-blocking under `--strict`. So the block exists and the *"iOS has no project-specific rules"* premise is retired (`enforcement.md:18` corrected the same day; **ADR-0032:96 says otherwise and stays untouched** — an accepted ADR is a record of a past reading). **What remains:** `included:` is still the four-entry roster at `:1-5`, so **no `custom_rule` reaches `CleansiaCustomer/LiveActivity/` or either app's `Tests/`** — any entry claiming tree-wide scope for a `custom_rule` overclaims by that much | ios (residual: `included:` only) | ADR-0032 §Follow-ups |
| FT-4 tier-label the ~22-entry iOS corpus, in lane slices | ios + architect | ADR-0032 §Follow-ups |
| FT-5 canonicalize `LogoutRow`, then promote the tier | ios | ADR-0032 §Follow-ups |
| FT-6 shared test-tree-root helper | ios | ADR-0032 §Follow-ups |
| FT-7 rename ADR-0032's file to match its amended title | docs | ADR-0032 §Follow-ups |

---

## Deliberation history

- **2026-07-30** — ADR-0032 drafted `proposed` by the author instance, carrying three decisions and an
  empty `## Challenge`.
- **2026-07-31/08-01** — a challenger filed **C1–C11**. No `## Defense` was filed.
- **2026-08-01** — the lead adjudicated on independently re-verified evidence: **C2** (the ADR-0018
  precedent contradicts a CI-only rule) and **C9** (the ADR could not discharge its own rule) together
  forced the amendment from *"a law must name a T1-CI gate"* to *"a law must name an enforcer at a
  declared tier"*. **C1** (corpus ~5× larger than stated), **C3** (`(gate pending:)`), **C6**
  (SwiftLint's real scope), **C10** and **C11** (premise expiry + Alternative G) were sustained and
  folded in. **C8** was sustained in part — split into two ADRs, not three. Five findings were
  **overruled** with evidence: C7's seam framing, C8's "three decisions", C11's subsumption claim,
  C1's "grandfathered forever", C3's universality. Full trail in ADR-0032 §Verdict.
- **2026-08-05 (T-0471)** — the one round ADR-0033 was held open for. The floor's author (the ADR-0032
  lead) had nominated three lines of attack and declined to rule on its own repair. Six findings filed
  (`adr/challenges/0033-floor.md`), two blocking. **CH-1** — "previously permitted" is undecidable
  because the catalog prescribes rather than permits, and it split the ADR's own retro row 3 — and
  **CH-2** — the floor's enforcer (`reviewer.md:105-110`) asserts the axis the floor *replaces*, which
  is ADR-0032 **D3** applied to ADR-0033, with T-0473 as the already-happened proof — together forced
  the amendment. **CH-3** (burden inverted vs test 1) and **CH-4**'s omission (the ADR-0032
  composition) were sustained; **CH-4**'s "contradicts test 1" framing and **CH-5**'s "fitted" were
  **overruled** on evidence. **CH-5** extended the retro-validation from four rows to seven and
  reported, as a result rather than a silence, that **no case exists where the floor routes inline
  something plainly wrong** — the floor is correct in direction and thin in safety margin.
  Amendments **M1–M6**; `accepted`. **Panel composition is declared in §Verdict: challenger and lead
  were the same instance** (no spawn capability in that invocation), distinct from the floor's author —
  AC1 **SATISFIED-IN-PART**, for the PM to decide whether a second-instance re-check is warranted.
- **2026-08-05 (T-0471, later the same day) — the independent lead pass.** A **third** instance ran the
  adjudication the challenger declined to self-certify. **AC1 is now SATISFIED on composition** (author ≠
  challenger ≠ lead). Rulings: **M2 sustained without qualification** — it is the one amendment that
  changes what a reviewer can *do*, and route-by-default removes the self-certifying path. **M1 sustained
  in direction, insufficient as written** — proved determinate on a real hunk the round did not have
  (**T-0349**, an eighth retro case that fires on a *general* sentence and agrees with history) and
  **indeterminate** on another (**T-0473**), because "governs" is never defined (**L1**). **M3 ruled a
  described fix, not a fix** — the four facts CH-2 turned on are all still true, FT-11/12/8 have no
  tickets, so ADR-0033 ships at `(guidance — no gate)`: **the challenge succeeded and the amendment did
  not** (**L2**). New blocking-grade defect the round missed: **Block C appends the new test and leaves
  `conventions.md:125`'s reversed limb standing**, so applying it as specified would put two incompatible
  routing instructions on one page — and ADR-0033's *"refines, does not reverse"* header claim is false as
  to that limb (**L3**). The two **OVERRULED** dispositions were re-derived: **both are substantively
  right and neither is an overrule of the challenger** — `challenges/0033-floor.md` CH-4 never claims a
  contradiction and CH-5 is titled *"the retro-validation is **not** fitted"*. Both were overrules of the
  *author's own nominated framings*, so **§Verdict's "overrules the challenger in part twice" does not
  support the AC1 argument it was offered for** — which is exactly why the composition fix was needed.
  **F5 fixed in the same pass.** L1/L3 are routed to a **new panel**, not written by this lead: inventing
  the repair and ratifying it is the defect T-0471 exists to repair, and it binds a second lead too.
- **2026-08-05 (later still) — the L1/L3/F4 panel opens; AUTHOR'S DRAFT filed, nothing ratified.**
  A fourth instance (did not write ADR-0033, did not challenge it, did not adjudicate it) filed
  `adr/drafts/NNNN-what-makes-a-catalog-sentence-govern.md`. **Number deliberately unallocated** — the
  PM allocates when the panel closes; highest on disk is 0042 and two architects collided on 0041 this
  sprint by grepping correctly at the same moment. What it proposes: **D1** the conflicting-instance
  test for *"governs"*; **D2** the reviewer's symmetric burden (firing test 2 costs a **named artifact**,
  not a named sentence — the completion of M2 on the other side, and what stops test 2 becoming a
  one-way ratchet); **D3** F4 answered as *no fourth test*, with the trade-off question relocated to the
  Architect's post-routing procedure; **D4/Block C′** the corrected `conventions.md` edit — **replace
  `:120-130`, do not append** — with the reversal of limb 1 stated in the developer-facing page instead
  of denied in an ADR header. Evidence: **10 real entries run, 10 determinate**, 7 agreeing with history.
  **Declared limitation, and it is the same one that hobbled the lead pass: no shell.** The brief
  asserted one was available; the invocation had `Read`/`Write`/`Edit`/`Glob`/`Grep` and no `Bash`, so
  **again no catalog edit was read as a diff** — every case is reconstructed from entry text + ticket +
  grep. Follow-up **N-F** is the one-command measurement (`git log -p` over the catalog files) that
  converts ten cases into a rate **and** settles whether Case β is historical or hypothetical. Challenger
  and lead rounds pending; **`deliberation.md` §6 — nothing is finalized and no ticket may cite this yet.**
- **2026-08-05 (T-0553) — the challenger and lead rounds ran, WITH the diffs, and the draft is `rejected`.**
  N-F was run for the panel: a corpus of every commit touching `agents/knowledge/*.md`, with full diffs.
  It is the evidence four prior instances declared they lacked, and **it changed verdicts** — which is the
  meta-finding of this whole sequence. **Composition: five distinct instances**, author ≠ challenger ≠
  lead (**T-0553 AC1 SATISFIED**). **No `## Defense` was filed** — the author round did not run before the
  ruling; recorded, and immaterial to the two falsifications, since no defense restores a deleted sentence
  or conjures an absent artifact.
  **Three blocking findings from the challenger, all SUSTAINED:** **CH-A** — retro row 8 is wrong and the
  method is blind in one direction (`ab077504`'s third hunk deletes the governing sentence; the draft
  scored one hunk); **CH-B** — row 9, the only claimed catch, is a **false positive**
  (`ProfileViewModel.kt:179-180` + `ProfileViewModelTest.kt:635`, shipped by the governing sentence's own
  ticket); **CH-C** — *"reach"* re-imports the subject-granularity problem Alternative A was rejected for,
  demonstrated on the draft's own row 10. **CH-D** sustained in part and **corrected against the
  challenger** on evidence (rows 6 and 7 are **two** routing decisions — row 7 was routed 2026-07-04 as a
  fix-round-3 scope addition, `T-0379-…md:115-118`, and ratified *as-is* on 07-19 — not one sitting).
  **CH-E**, **CH-F**, **CH-G** sustained.
  **The ruling turns on a fourth defect the lead found in the draft's own table (V-1): D1 states an
  existential and scores six of ten rows with a compose test.** Read as stated it flips accepted
  ADR-0033's retro row 2 (T-0441 `inline` ✅) to *Architect*; read as scored, row 9 disappears. **No
  reading of D1 produces the table it is validated by** — CH-1's defect (*a predicate true and false of
  the same edit*) relocated from "permitted" onto "governs". **That settles the question the panel was
  convened on: the definition is WRONG, not merely under-evidenced** — the flip lands on a row whose
  commit is a clean insertion with a genuinely predating candidate sentence, i.e. a row CH-A's blindness
  does not touch, so more corpus cannot fix it.
  **Dispositions:** **D1 REJECTED** · **D2 HELD** (sound, and empty until its currency is defined; the
  challenger tried to break it and could not) · **D3 UNBEATEN, NOT SETTLED** (eight triggers built and
  killed; ground (a) struck as a censored-sample inference; re-opens with the rebuilt corpus) ·
  **D4/Block C′ SUSTAINED and SEVERED** to its own round with the D1 paragraph excised, CH-G fixed, and
  the Block D addendum held. **The corpus must be REBUILT, not repaired** — the search step ran against
  the post-edit tree and the scoring step used a different predicate; rows 2, 4, 5 survive as positives.
  **The bar for the next author is R-1…R-7** in the rejected draft's §Verdict. **A pattern named rather
  than repeated: three rounds running have declared a limitation and then printed an undiscounted headline
  number** — R-6 makes discounting a condition of reporting a score.
- **2026-08-05 (T-0549 → T-0550 → T-0551) — the enforcement lands; ADR-0033 goes from `(guidance — no
  gate)` to `T3-HUMAN` in force.** One architect, one serialized pass over the shared `conventions.md`
  section, no panel: AC1/AC2/FT-12 apply text an accepted ADR already ratified, and the `conventions.md`
  repair applies the T-0553 panel's severance ruling. **Nothing from the rejected "governs" draft was
  carried in.** Four judgment calls worth keeping in the record because they were not transcription:
  (1) the numbered list was **replaced**, and the **reviewer's page was fixed first** (the ADR's own M3
  order), so the window in which the two pages disagreed ran in the safe direction and never carried two
  axes on one page; (2) the *"earns its place"* bar was left **above** the new `###` subsections rather
  than following them as Block C′'s letter implies — a general sentence parked under a narrower heading
  acquires that heading's scope, which is finding **G3**'s live disease and would have been indefensible
  to install in this of all edits; (3) ADR-0033's false *"does not reverse"* header claim was corrected
  by a **dated record-only closure**, and the denial was **not** propagated into the developer-facing
  page; (4) **no ADR number was allocated** — after the severance what remained was accepted ADR-0033's
  own content plus two deletions. **The honest limit of all of it: this is a human-tier enforcer and it
  cannot go red.** `check-consistency.mjs` is in zero workflows and the frontend lint step is
  `continue-on-error: true`, so no mechanical option existed to reach for. What the lane bought is a
  **named, greppable** standing item where there had been an unnamed one teaching the superseded rule.
- **2026-08-05 (L5) — the charters catch up with the rule; the audit method does not.** Five charters
  (`architect`, `backend`, `frontend`, `android`, `ios`) were brought onto ADR-0033's axis by quoting
  the ratified `conventions.md` text rather than restating it, each pointing at **`Catalog-edit
  routing`** by title. `architect.md` needed the largest change and the least mechanical one: it
  carried limb 1 — the limb the floor **reverses** — inside a paragraph describing what the Architect
  does *on receipt*, so the repair was a restated intake condition plus a per-test procedure, not a
  deleted clause. **The round's real output is a correction to how this doc measures.** Three previous
  rounds each ran `grep -rn 'one way to do X'`, got the number they expected, and declared the axis
  retired; this round ran it, got **zero on all three trees**, and then widened to `way to do X` and
  found **`.claude/agents/db.md:62-65`** — the same superseded bullet, hidden from four rounds of
  counting by a **line wrap**. The lexical probe was auditing the rule whose entire repair was to stop
  being lexical. Two standing consequences: the verification command in this doc is now the
  **multiline** form, and the *"pages that still teach the superseded axis"* row reads **1**, not 0 —
  because a residual named is worth more than a zero that is wrong. `db.md` was left untouched on
  purpose: the lane was authorised for five charters, and quietly widening a scope is the same class of
  move as ratifying your own standard-change.
- **2026-08-05 (T-0552) — F1 closed, and the erratum lane was refused for the second time this sprint.**
  ADR-0032's two stale header-block statements landed as a **dated record-only closure** plus two signed
  pointer annotations, not as an erratum: both sentences were **true when written**, and
  `adr/README.md:16-29` opens the in-body lane only for a value **mis-copied from the ADR's own cited
  source**. Same call as L3's *"meaning, not digits"* on ADR-0033's header claim, and the closure now
  states it as a reusable line — *an erratum corrects the ADR against its own source; a closure records
  that the world moved.* **No decision content changed and the status stayed `accepted`**, which is what
  kept this out of a panel. One thing the finding did not name and the closure does: the identical phrase
  occurs a **third** time inside ADR-0032's `## Verdict` **C5** row, and was deliberately left
  standing — a verdict row pins what was ruled, and ADR-0031 §A already settled that such citations are
  not corrected. **The discriminator is the sentence's job, not its wording**: `:14` and `:23-25` are
  forward-looking pointers (*"see its status block"*, *"until T-0439 merges"*), `:624` is a finding.
- **2026-08-05 (T-0553, round 2) — the second author round on L1 files a draft. `proposed`; nothing
  ratified; no ticket may cite it.** A **sixth** instance (did not write ADR-0033, did not challenge it,
  did not adjudicate it, did not write round 1, its challenge or its verdict) filed
  `adr/drafts/NNNN-what-makes-a-catalog-sentence-govern-round-2.md` against **R-1…R-7**.
  **The proposal, and the diagnosis it turns on:** *`governs` is **reach**, and it carries no verdict
  term.* `S` governs `E` iff **at least one member of `E`'s exhibit falls under `S`'s condition** —
  exhibit = **the ticket's diff plus the entry's own `file:line` citations**, a finite written list, so
  the quantifier is **∃ over a bounded set** and its negation is a check rather than an argument;
  condition = **`S`'s own words at their narrowest supported scope**, with **widening unavailable** (a
  reading that drops a clause of `S` or swaps a broader term for one `S` names loses; a heading may
  narrow a sentence, never widen it). The *carves / replaces / forbids* conjunct is untouched. **Why
  round 1 could not have worked whatever quantifier it chose:** it defined `governs` with a **conflict**
  notion (*verdicts differ*), duplicating the conjunct `challenges/0033-floor.md` CH-1 had already
  cleared as decidable — and inheriting its unboundedness, because comparing verdicts requires ranging
  over things to have verdicts about.
  **Two properties the draft leans on.** (1) **Coverage lemma** — a withdrawal either converted its
  violators, so they are in the exhibit, or did not, so **test 1** fires; it cannot hide from both. That
  is how the definition reaches the case round 1 needed hypotheticals for. (2) **The blind spot is the
  audit's, not the developer's** — an author searches the catalog they are editing, which *is* the
  pre-edit catalog, and where their own hunk deletes a sentence they are holding it (`ab077504`,
  `f0e39d7e`).
  **Corpus: 13 rows, every one scored against its parent commit, one quantifier throughout — 13
  determinate, 12 with an establishable routing event, 9 agree, 3 diverge, and all three divergences are
  pre-existing** (ADR-0033's own rows 5 and 7, plus T-0527/**G1** where agreeing with history would be
  the error). **0 new divergences; ADR-0033's retro rows 2 and 3 reproduced (R-5).** Four rows are new:
  **`f0e39d7e`'s full-entry REPLACE of the iOS shell-navigation entry** (it deletes the clause forbidding
  a stock `TabView` bar and installs the opposite mandate → routes; T-0379 `owner: architect` ✅ — a
  positive read straight off a deletion), a frontend **modification** that correctly does not route
  (`6bd3b0c6`), a backend row that is inline under **both** readings of its candidate's condition
  (`97bb7265`), and T-0473 scored twice (historically vacuous; determinate as a live hypothetical).
  **Three prior attributions corrected from the diffs:** T-0349 is a **modification**, not an insertion,
  and its strongest candidate `S` is the clause the edit rewrote — which **named its own routing**
  (*"a duplicated **VM** is a harvest-to-Core candidate — flag, an **Architect call**"*); T-0451's
  routing event is the reviewer's refusal (ADR-0033 `:51`), not its ticket's `owner: ios` front matter;
  and **row 5's chronology, open across three rounds, is settled** — the `.medium` grant is a context
  line in `04f98937` (2026-06-30), eleven days before the withdrawal, in the *same hunk* that carries
  row 4's pre-image.
  **The draft names its own soft spots rather than burying them:** *"position never widens"* is an
  interpretive convention and the only rule doing the work on the T-0432 row, and its price is that
  **G3 is resolved permissively by construction**; the exhibit is only as honest as the diff's
  characterization; five of the nine agreements rest on a searched negative over 13 of 94 commits, and
  **no rate is claimed anywhere**. **F4 is answered as a consequence, not as a second decision** — the
  target the round-1 lead re-specified for it (row 8) is caught by test 2 under the new definition — and
  the draft invites the lead to sever it. **Challenger and lead rounds pending.**
- **2026-08-05 (T-0553, round 2) — the challenger and lead rounds ran. The draft is `rejected`, and the
  lead CLOSED L1 as a definition project.** Composition: seven distinct instances across the sequence,
  author ≠ challenger ≠ lead for the second round running (**AC1 SATISFIED**). **No `## Defense` was
  filed, for the third consecutive panel in this lane** — `deliberation.md` step 3 has never executed
  here; recorded as a process finding, and immaterial to the ruling.
  **The challenger filed seven findings, two blocking, and explicitly declined the cheap exit**
  (*"I do not recommend 'reject and keep the warning'"*). Both blocking findings were **sustained**:
  **CH-A** — limb (a)'s filter *"that `E` declares canonical or withdraws a form from"* **is** `E`'s
  subject, so the question moved one level rather than dissolving; and *"the ticket's diff"* is not
  recoverable in a repo that lands by **phase**, so **all 13 rows were scored from the ticket's *stated
  scope***, which the draft's own Gate 0.5 concedes. **CH-B** — the warrant that the conflict conjunct
  is decidable mis-cites `challenges/0033-floor.md` (CH-1 cleared *"replacing a named canonical form"*
  only). **CH-C sustained on substance with its remedy REFUSED**: fencing an ADR with a condition on an
  unfiled ticket in another lane is structurally what failed with Block D — the challenger's own cited
  precedent — and the position rule **carries no row**, so it is a defect in the rule, not something to
  fence. **CH-D, CH-E, CH-F, CH-G sustained** as drafting or weight (~3 discriminating agreements, not
  9; R7 and R11 are both T-0379).
  **Three corrections the lead made against the challenger, on evidence** — the pattern this lane has
  been trying to establish since round 1's credibility failure: (1) nothing undecidable was smuggled in
  — **accepted ADR-0033 declares test 2 semantic on its face** (`:161`, `:538`; `conventions.md:158`;
  `reviewer.md:115`) and *"carves an exception"* has four determinate applications in the record plus a
  live one (`challenges/0041-schema.md:124`); (2) the position rule's error direction is **permissive in
  both halves** (reach is monotone in the condition, so wrongly narrowing can only reduce firing), so
  *"unsound, not merely permissive"* is right as a soundness claim and wrong as a consequence claim; and
  (3) the challenger's single stated reason for declining Alternative H — *"the definition catches
  R8/G1 on test 2"* — **is contradicted by its own target's R8 row**, which says the accepted floor
  routes it too. Against its own interest, the lead also **enlarged CH-C**: the section hosts **eight**
  iOS entries, not four, and iOS proper starts at `:615`, not `:569`.
  **The ruling that closes the lane is V-2, and it is a marginal-value judgment stated as one.** After
  the challenger's third dissolution of **Case β** — the candidate sentence names `${…}` and `R`, sits
  under `## Strings & states`, and at `:569` cites *"(the T-0473 rule)"* in its own text — **the record
  contains no evidenced case in which a defined `governs` would have changed a routing outcome.** On a
  disputed case M2 routes either way; `ab077504`/G1 routes under the **accepted** floor; the one live
  indeterminacy left is **G3**, a catalog-file defect with an XS fix in the ios lane. Three architect
  rounds have gone into a predicate that has never moved a routing decision, and this ADR blocks
  nothing — the routing rule has been in force since the morning of 2026-08-05. **The lead recorded
  that it nearly ruled the other way** (the Defense step has never run, and the challenger recommended
  acceptance-after-answers), and named a deliberately cheap reopen path: **one author Defense pass**, on
  two named triggers only.
  **What survives, unratified and citable by nothing:** the **coverage lemma**, the **widening rule**
  (which is what dissolved Case β a third time), and the **firing-side burden** — held for a third time,
  now with CH-F's finding attached that a burden shipped without its predicate is a paraphrase
  generator. **What the lane emits as work: G3, promoted to its one real deliverable.**
