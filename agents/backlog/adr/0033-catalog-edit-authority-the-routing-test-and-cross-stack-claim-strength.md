# ADR-0033 — Catalog-edit authority: which catalog edits a developer may make inline (the three-test routing rule, with a floor), and at what strength a catalog entry may claim something about a stack the ticket never ran

- **Status:** accepted   <!-- proposed | accepted | superseded | rejected -->
- **Date:** 2026-08-01 (drafted `proposed`; the floor challenged and the ADR amended + accepted
  **2026-08-05** by the T-0471 panel — see §Challenge / §Defense / §Verdict)
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
- **Number note (corrected by the T-0471 panel, 2026-08-05):** **0031** is taken by
  `0031-nswag-regen-drift-is-guarded-at-regen-time.md` and **it is on `master`** — T-0439 merged as
  `acf2f0bc` (PR #175). *The draft's claim that it "exists only in T-0439's worktree" and that "a reader
  on `master` sees a gap at 0031" was true when written and is false now; it is corrected in place
  because this ADR was `proposed`, not `accepted`, when the correction was made.* **0032** is the
  price-of-a-law ADR; 0033 was allocated by the ADR-0032 panel. **ADR-0032 carries the same stale note
  at `:23-25` and is `accepted`, so it needs a signed erratum, not an in-body edit
  (`adr/README.md:16-26`) — filed as finding F1 for the PM, deliberately not fixed from inside this
  ticket.**
- **Ticket:** none for the draft — split out of the ADR-0032 panel. **T-0471** is the ticket that ran
  the floor's challenger round and carried this ADR to `accepted`.

> **Why this sat at `proposed` for four days, and what closed it.** Two of the three parts carried
> panel consensus from the ADR-0032 round: **test 1** was called *objective and unattacked* by the
> challenger, and **D2 (cross-stack strength)** had its structural-vs-behavioural line called *drawn on
> the right property*. The **floor on test 2** (§D1, test 2) was **new text authored by the ADR-0032
> panel lead** in response to challenge C5, which demanded a floor without proposing one. *A lead may
> adjudicate between positions the parties argued; inventing the repair and then ratifying it is not
> adjudication.* So the floor — and nothing else — needed one adversarial round.
>
> **That round ran on 2026-08-05 under T-0471.** Six findings were filed
> (`agents/backlog/adr/challenges/0033-floor.md`); two were blocking. The floor **survives in
> direction and is amended in wording**: its trigger is redefined on the *sentence* (M1), it acquires
> a symmetric evidentiary burden with a **route-by-default** on missing evidence (M2), its enforcement
> moves from prose-inside-an-ADR to a **named reviewer-check** (M3), its composition with ADR-0032 is
> stated (M4), and its retro-validation grows from four rows to seven, two of which it routes against
> history (M5). Full trail in §Challenge / §Defense / §Verdict.

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

2. **Does it *narrow* latitude the catalog previously left open — carving an exception out of a
   sentence that already governs this subject, replacing it, or forbidding a form it named?** →
   **Architect**. That is a law, and ADR-0032 prices it. *(The draft asked this as "forbidding an
   alternative the catalog, until now, permitted"; the T-0471 panel replaced that wording — see the
   floor below and Alternative G.)*

   **The floor (challenge C5; amended by the T-0471 panel, 2026-08-05).** Test 2 fires on a
   **narrowing**, not on the **first statement of a canonical form**.

   **The unit is a *sentence*, not a topic (M1).** Test 2 fires when a catalog sentence **already
   governs the subject of this entry at any level of generality**, and the entry carves an exception
   out of it, replaces it, or forbids a form it named. It does **not** fire when **no** sentence covers
   the subject at any level of generality.

   > **"The catalog was silent about X" means _no sentence covers X at any level of generality_ — not
   > _no sentence names X specifically_.** A rule stated about the general case governs its sub-cases,
   > so carving out a sub-case is a narrowing of that rule whether or not the sub-case was ever named.

   *Why the original wording could not stand:* it turned on "a form the catalog previously
   **permitted**", and the catalog almost never permits — it prescribes. Permission was inferable only
   from silence, which is the same condition that routes an edit inline, so the predicate was true and
   false of the same edit. It split this ADR's own retro row 3: T-0451 withdraws `Color.dynamic` ink,
   but the sentence that "permitted" it (`patterns-mobile.md:577`, the Android→iOS mapping row naming
   `CleansiaColors` slots as `Color.dynamic(light:dark:)` pairs) never mentions theme-invariant
   surfaces. Under the amended wording row 3 resolves for a stated reason: `:577` governs *which token
   supplies ink* at the general level, and T-0451 carves out a sub-case → **narrowing → Architect**.
   Row 2 is unaffected: nothing governed *what an Api-adapter request-side test asserts* at any level
   (the nearest sentence, `patterns-mobile.md:167-175`, governs normalizing a business-key 400 at the
   repository — a different subject) → **inline**.

   *The second conjunct of the original wording ("and no shipped call site becomes a deviation") is
   kept as a reader's aid but is **not** a safeguard: the tests are ordered, so test 1 has already not
   fired by the time test 2 is asked. Do not rely on it.*

   **Claiming the floor costs one line of evidence, and the default is `route` (M2).** Test 1's
   evidence is a **code** sweep in the ticket's `## Review` (reviewer check 2). The floor's evidence is
   a **catalog** sweep: name the file(s) and the term searched for a sentence governing this subject,
   and what it returned. **A floor claimed with no search is not claimed — the edit routes to the
   Architect.** The floor is opt-in *with evidence*, because the party holding the information is the
   author; leaving the reviewer to reconstruct what a 1000-line catalog used to permit is the same
   burden inversion that let T-0274 through (`T-0274-fe-error-resolver-dedup.md:133` self-classified;
   the reviewer, holding a diff and not the catalog's history, did not catch seven obliged call sites).

   **The floor does not make the inline lane the cheap lane (M4 — the composition with ADR-0032).** An
   entry that qualifies for the floor has a **zero baseline by construction**: test 1 did not fire.
   Zero baseline is exactly ADR-0032 D2's condition (b). So if the new form is **mechanically
   expressible on its stack**, `T1-CI` is **mandatory** and the inline ticket ships the gate alongside
   the entry. Where the only available mechanizer **cannot fail a build**, the honest token is
   `T2-ADVISORY`, and the entry says so and names what would promote it:

   - `check-consistency.mjs` appears in **zero** files under `.github/` — it can never set a blocking
     exit code, on any stack (re-verified 2026-08-05).
   - `frontend-ci.yml:72-74` runs lint with **`continue-on-error: true`** — an ESLint rule attached
     there can never red a build either.
   - The house model for an honest label is `patterns-frontend.md:462-465`: *"**T2-ADVISORY**, because
     `frontend-ci.yml` runs lint with `continue-on-error: true`; promotes to `T1-CI` with the rest of
     the lint baseline."* Copy that shape. **A tier token naming a mechanism that cannot fail a build
     is `T2-ADVISORY` however the entry is worded.**

   **Why a floor is needed at all.** `conventions.md:132` sets the bar for *any* catalog entry at
   "makes the codebase **more consistent**" — which, read literally, means every entry that earns its
   place forbids some less-consistent alternative. Without the floor, test 2 fires on everything, D1
   collapses into "everything goes to the Architect", the inline lane dies, and the harvest loop
   `conventions.md` deliberately opened closes again.

   **The test is semantic, not lexical (challenge C4).** Imperative wording — "the ONE way", "never
   X", "X is a defect", a closing "Deviations a reviewer rejects:" list — is a **prompt** that should
   make a reviewer look; it is not the trigger. An entry rewritten as "the canonical form is X" that
   nonetheless carves an exception out of a governing sentence fires test 2 all the same. *Under the
   amended floor this is doubly true: rephrasing the **new** entry changes nothing about the **old**
   sentence, which is where the trigger now lives.* There is no wording that
   launders a narrowing past this test. (Under ADR-0032 the incentive to try is gone anyway: every
   entry constraining call sites names an enforcer + tier **whatever its wording**, so imperative
   phrasing costs nothing and buys nothing.)

3. **Does it make a *prescriptive* claim about a stack this ticket did not build and run?** →
   **Architect** (see D2 for what "prescriptive" means and what the alternative is).

4. Otherwise — it explains, exemplifies, or names a footgun **inside an existing rule's existing
   scope**, and no shipped code becomes a deviation → **inline**, flagged in the ticket's `## Review`
   for the Reviewer's sanity-check (unchanged from `conventions.md` step 2, first bullet).

**Retro-validation against seven real cases** (the evidence that the test is sound, not just tidy).
*Rows 1–4 are the draft's; rows 5–7 were found by the T-0471 challenger and added under M5, because
four rows — one of which the floor moves — is thin evidence for a rule that governs every future
catalog edit. Two of the three new rows route **against** what actually happened; that is stated, not
hidden.*

| Case | T1 (obliges existing code?) | T2 (narrows latitude?) | T3 (foreign stack, prescriptive?) | Routes to | Actual ruling |
|---|---|---|---|---|---|
| **T-0446 / SEC-5** — nothing in S1–S11 covers bytes inside a stored artifact served by URL | **YES** — three shipped pipelines (avatar, order photos, dispute evidence) sanitize nothing | yes | no | **Architect + docs** | Architect + docs (T-0460) ✅ **matches** |
| **T-0441** — "assert the GENERATED command, not the app one" | no — it names existing practice (`BookingApiTest`, `UserRepositoryTest` are the cited models); no shipped call site becomes a deviation | **no** — it **adds a test obligation** where **no sentence governed the subject at any level**: the nearest, `patterns-mobile.md:167-175`, governs normalizing a business-key 400 at the repository — a different subject | its Android half, no; its iOS half is **descriptive** (D2) | **inline** | inline ✅ **matches** |
| **T-0451** — "Ink on a theme-INVARIANT surface — the ONE way" | no (the two heroes are the ones being fixed) | **YES** — `patterns-mobile.md:577` already governs *which token supplies ink* (`CleansiaColors` slots = `Color.dynamic` pairs); this carves out the theme-invariant sub-case. *The sub-case being unnamed in the catalog is not silence (M1).* | no | **Architect** | Architect ✅ **matches** |
| **T-0274** — "resolvers must delegate rather than re-implement the walk" | **YES** — seven shipped `.models.ts` resolvers | yes | no | **Architect** | inline ❌ **the recorded divergence** — the old axis under-routed it |
| **T-0397 row 1** — full-bleed header-to-top idiom | no — all **three** call sites already matched (`ProfileTab.swift:23-52`, `SubscribePlusScreen.swift:34-49`, `ProfileHubContent.swift:22-35`, verified in the ratification) | **no** — no sentence governed full-bleed header layout at any level | no — iOS ticket, verified on-simulator | **inline** | Architect (T-0397) ⚠️ **divergence — accepted, see below** |
| **T-0397 row 2** — short entry sheet must NOT use a fixed `.medium` detent | no | **YES** — `patterns-mobile.md:1230` names `.presentationDetents([.medium])` for exactly those promo/referral code dialogs | no | **Architect** | Architect ✅ **matches** |
| **T-0379 scope-add** — "a `format: date` field ridden as plain `Date` is a defect" | no — both generator configs already carried `useCustomDateWithoutTime: true` | **no** — nothing governed date-only wire decoding | no | **inline** | Architect (T-0379) ⚠️ **divergence — accepted, see below** |

It reproduces all three actual rulings the PM flagged as nearly inconsistent, **and** explains the one
that went wrong. Note the floor doing real work in row 2: T-0441 stays inline **because** no sentence
governed its subject at any level, not merely because its wording was additive — under the unfloored
test 2 it would have routed to the Architect, contradicting the actual (correct) ruling. And note rows
2 and 3 are what the amendment turns on: the draft floor could not tell them apart without the word
"permitted", and the amended floor separates them on a sentence a reader can go and open.

**The two divergences, owned rather than discovered later (M5).** Rows 5 and 7 are real historical
catalog rows that three fix-round-6 reviewers and the PM routed to the Architect and that this rule
sends inline. Both were **ratified essentially unchanged** — T-0379's `format: date` row *"RATIFIED
as-is"*; T-0397's header idiom ruled *"no trade-off survives … a row that names the one working shape
plus its defect forms is exactly what the catalog is for"* — so the floor's **substantive** answer
matched the outcome in both, and the rounds it saves are real cost. What it loses is visible in row 5:
that ratification **added content the developer's row did not have** (it folded in the fix-round-8
`.animation(nil, value: topInset)` settle pin, which *"the row predated"*, and corrected the verified
call-site count from two to three). The floor buys throughput and pays for it in verification rounds
that sometimes catch something. That is the trade, stated; §Consequences carries it.

**And rows 5 and 6 are the same harvest ticket.** Same author, same reviewers, ratified in the same
sitting — split by this rule on nothing but whether the catalog happened to have *described* the old
form somewhere (`:1230`). That is a genuine property of any withdrawal-based test and the reason M2's
evidentiary line exists: the author, not the reviewer, is the one who can cheaply answer "is there such
a sentence?".

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

**On acceptance**, and not before — **and Block C additionally waits on Block D** (FT-11 → FT-8), for
the reason CH-2 established: a rule the author's page states and the reviewer's page contradicts is not
in force, it is quotable.

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

**Applier: the architect** (`conventions.md` is lane-uncontended), **as FT-8, sequenced behind FT-11**.
It may **not** be applied before the reviewer's standing checklist names this check (M3): a
`conventions.md` section aimed at the *author*, while the *reviewer* still holds the superseded
instruction, changes which rule is quotable and not which rule is run. Insert after the existing
numbered list, **below** the "The price of a law" section already added by ADR-0032:

```markdown
### Which of those two lanes you are in — the routing test (ADR-0033)

Apply in order. The **first** one that fires routes the edit to the **Architect**; if none fires, edit
inline and flag it in the ticket's `## Review`.

1. **Does the edit put code that exists today in violation?** If any current call site becomes a
   deviation it wasn't before, it needs a `consistency.md` deviation entry and a canonicalization
   ticket — neither of which a developer or a reviewer can file for themselves.
2. **Does it *narrow* latitude the catalog previously left open?** It narrows when **a catalog sentence
   already governs this entry's subject at any level of generality** and the entry carves an exception
   out of it, replaces it, or forbids a form it named. That is a **law**, and laws are priced (see "The
   price of a law").
   **Floor:** first-statement-of-a-form, where **no** sentence covers the subject at any level, is
   **inline**. *"The catalog was silent about X" means no sentence covers X at any level of generality
   — not that no sentence names X specifically.* A rule stated about the general case governs its
   sub-cases; carving out a sub-case narrows it whether or not the sub-case was ever named.
   **Claiming the floor costs one line:** name, in the ticket's `## Review`, the catalog file(s) and the
   term you searched for a governing sentence, and what it returned — the same evidence test 1 already
   demands for code. **A floor claimed with no search is not claimed: route it.**
   The test is **semantic**: "the canonical form is X" narrows exactly as much as "the ONE way is X".
   Imperative wording is a prompt to look, not the trigger.
3. **Does it make a prescriptive claim about a stack this ticket did not build and run?** A rule for a
   stack you never executed is not yours to declare.

**Inline is not free.** An entry that clears the floor has a **zero baseline by construction** (test 1
did not fire), which is exactly the second condition "The price of a law" puts on `T1-CI`. So if the
form is mechanically expressible on its stack, the inline ticket **ships the gate with the entry**.
Where the only mechanizer available cannot fail a build — `check-consistency.mjs` (in **zero**
`.github/` workflows) or an ESLint rule under `frontend-ci.yml`'s `continue-on-error: true` lint step —
the honest token is `T2-ADVISORY` and the entry says what would promote it.
`patterns-frontend.md:462-465` is the model.

*Not* the test: "is this a gap in the rules or a clarification to them?" That measures novelty
relative to the text rather than cost imposed on the codebase, and the two come apart in both
directions — a gap can oblige nobody, and a "clarification" that sharpens an existing rule's scope can
retroactively put dozens of shipped call sites in violation.

**Enforced by:** reviewer-check **5 "Catalog-edit routing"** (`.claude/agents/reviewer.md`, as rewritten
by ADR-0033 Block D) — **T3-HUMAN**. Scope: it fires on any diff touching `agents/knowledge/*.md`; it
does not read the entry's content, only its routing and its enforcement label.

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

### Block D — the named enforcer (added by the T-0471 panel, M3; **this is the condition of acceptance**)

**Applier: the architect, as FT-11 — and FT-8 (Block C) is sequenced BEHIND it.**

Without this block, ADR-0033's floor ships at `(guidance — no gate)` by ADR-0032's own definition
(*"T3-HUMAN requires a **named** checklist item; 'the reviewer will notice' is not T3"*), and worse: the
one named standing item that governs a catalog hunk — `.claude/agents/reviewer.md:105-110`, step 5 —
currently instructs the reviewer to apply the **superseded** axis (*"a small clarification/example is
fine to pass with the change; anything that redefines 'the one way to do X' is an Architect call"*).
An enforcer that asserts the rule this ADR replaces is ADR-0032 **D3**'s exact failure mode, applied to
ADR-0033 itself.

Replace `.claude/agents/reviewer.md` step 5's catalog clause with:

```markdown
5. If you find a **security** concern, mark it and tell the PM to invoke `security`. If a **design**
   concern, tell the PM to invoke `architect`.

   **Reviewer-check 5 — "Catalog-edit routing" (ADR-0033 · ADR-0032).** If the diff touches
   `agents/knowledge/*.md`, run the three ordered tests; the first that fires means the ticket may not
   ratify the edit for itself — flag it for the PM to route to the Architect. The content may be right;
   the question is who ratifies it.
   1. **Does it put code that exists today in violation?** The ticket names the sweep it ran (a grep, a
      file list). "No existing violations" with no sweep is not an answer.
   2. **Does it narrow?** A sentence already governs this subject at any level of generality, and the
      entry carves an exception out of it, replaces it, or forbids a form it named. Semantic, not
      lexical — "the canonical form is X" narrows as much as "the ONE way is X". **If the author claims
      the floor (first statement, catalog silent), read the catalog search they recorded; if there is
      none, the floor is not claimed — route it.**
   3. **Prescriptive about a stack this ticket never built and ran?** A descriptive cross-stack note
      needs a file:line of that stack's code **in the entry** and must impose no obligation.
   4. **The price of a law (ADR-0032).** An entry constraining call sites carries
      `**Enforced by:** <named enforcer> — <tier token>`. A floor-qualifying entry has a zero baseline,
      so a mechanizable rule owes `T1-CI`. **A tier naming a mechanism that cannot fail a build is
      `T2-ADVISORY`** — `check-consistency.mjs` is in zero `.github/` workflows; `frontend-ci.yml` runs
      lint with `continue-on-error: true`. **Open the named enforcer and read what it asserts**: if the
      sentence claims more, the sentence narrows (stating the residual) or the enforcer widens.
   Nothing fires ⇒ inline is correct; sanity-check the content and say so in your verdict.
```

`agents/process/quality-gates.md` **Gate 1** gains one pointer line so the check is reachable from the
gate list, not only from the charter:

```markdown
- **Editing the catalog is not the same as conforming to it.** A diff that touches
  `agents/knowledge/*.md` is routed by **reviewer-check 5 "Catalog-edit routing"** (ADR-0033's three
  tests + ADR-0032's enforcer/tier label), not by this gate.
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

*(G–I were raised in the T-0471 round.)*

**G. Keep the floor's trigger on "a form the catalog previously *permitted*".** *Rejected — this is the
draft's own wording, and it is the thing the round broke (CH-1).* The catalog prescribes; it does not
grant permission. "Previously permitted" is therefore inferable only from silence, which is the same
condition that routes an edit **inline** — so the predicate is true and false of the same edit, and it
splits this ADR's own retro row 3 (`patterns-mobile.md:577` never mentions theme-invariant surfaces).
The conjunction with test 1 does not rescue it: row 3's T1 column is *"no (the two heroes are the ones
being fixed)"*, so an edit that converts its own violators in the same change escapes test 1 — which is
exactly the shape a real narrowing takes.

**H. Define the floor on the *topic* ("has the catalog ever discussed X?") rather than on a governing
sentence.** *Rejected.* It is the more natural reading and it fails in the direction that matters: it
makes every sub-case a fresh topic, so an author can always find a level of description at which the
catalog is silent (T-0451 = "theme-invariant surfaces", a phrase the catalog had never used). The
sentence-level unit is what stops the sub-case dodge, and it is why the amended floor spells out that
silence means *no sentence at any level of generality*.

**I. Accept the floor and leave enforcement to `conventions.md` (Block C) alone.** *Rejected — CH-2,
the round's second blocking finding.* `conventions.md` is read by the **author**; the routing decision
is checked by the **reviewer**, whose standing instruction (`.claude/agents/reviewer.md:105-110`) still
teaches the abolished axis. Evidence that this is not theoretical: after ADR-0032 was accepted, the
next harvest into `patterns-mobile.md` (`:265-276`, T-0473) landed with **no** `**Enforced by:**` label
— which accepted ADR-0032 D2 requires of it — and self-classified as *"a testability clarification, not
a redefinition"*, the same self-classification T-0274 used. `**Enforced by:**` appears **zero** times in
`patterns-mobile.md`. A rule whose only home is a page the checker does not read is folklore with a
citation. Hence **Block D**, and hence FT-8 sequenced behind FT-11.

---

## Consequences

**Cheaper / safer**
- The routing question is decided once, in three ordered tests measured against **seven** real
  historical catalog edits: they **agree with four** actual rulings, **correct a fifth** the old axis
  got wrong (T-0274), and **diverge from two**, which are named in the table rather than left to be
  discovered — instead of the axis being re-derived per ticket by whoever is holding the diff.
- The inline harvest lane **survives** with a stated floor, so a developer who finds a better idiom
  can still write it down in the moment.
- **The routing rule lands where it is applied.** Block D moves the check out of this ADR and into the
  reviewer's own numbered list, so the rule that is *run* and the rule that is *written* are the same
  one. That is what was missing from the accepted ADR-0032 four days after acceptance.
- A cross-stack observation stops being either forbidden or silently binding: it is a **cited,
  labelled, non-obligating** note, or it is an Architect call.

**More expensive (new obligations)**
- A developer must ask "does this narrow something?" rather than "is this new?" — a slightly harder
  question, deliberately, because it is the one that predicts cost.
- Every descriptive cross-stack note carries a **file:line in the entry**, not just in the ticket.
- **Claiming the floor costs a catalog sweep** — one grep line in `## Review`, and the edit routes to
  the Architect without it.
- **An inline entry that is mechanizable owes its gate in the same ticket** (zero baseline by
  construction ⇒ ADR-0032's `T1-CI` condition is met). The inline lane is faster, not cheaper.

**What could go wrong (state it plainly)**
- **The sub-case dodge.** An author names a level of description at which the catalog is silent
  ("theme-invariant surfaces") when a general sentence plainly governs the subject ("which token
  supplies ink"). This is the floor's residual soft edge after M1 narrowed it: the trigger is now a
  *sentence at any level of generality*, and the reviewer reads the author's recorded search rather
  than reconstructing history — but nothing forces the author to search at the right level of
  generality. Mitigated, not eliminated.
- **Test 1 requires knowing the call sites.** "Does any shipped code become a deviation?" is only as
  good as the author's sweep. A grep in the ticket's `## Review` is the expected evidence. Note test 1
  does **not** fire when the edit converts its own violators in the same change (retro row 3) — so on a
  narrowing that ships with its cleanup, the floor is the only thing standing.
- **The floor is retrospective on both limbs, and the newest stack is where it discriminates least
  (CH-4, sustained in part).** It prices the cost to *shipped* code and prices the cost to *future*
  code at zero. On a stack whose code is still being written — iOS, where `patterns-mobile.md` already
  carries 22 "the ONE way" lines — most canonical forms are first statements, so most route inline, and
  whoever ships first sets the form. That is the accepted trade: `conventions.md:125-127`'s concern is
  *changing* the one way, and a first statement changes nothing shipped. What holds the line instead is
  ADR-0032's price attaching to the inline lane (an entry with a zero baseline that is mechanizable
  owes a gate) plus reviewer-check 5 — **not** the routing test.
- **Verification rounds the floor buys out.** Retro rows 5 and 7 are real cases the process routed to
  the Architect and this rule sends inline. Both were ratified in substance — but row 5's ratification
  **added content the row lacked** (the fix-round-8 settle pin) and corrected its call-site count. The
  floor trades some of that for throughput, deliberately. If a pattern of inline first-statements later
  proves to need correction, that is the evidence to revisit — supersede this ADR, do not route around
  it.
- **The enforcer can rot back.** Reviewer-check 5 is a `T3-HUMAN` item in a charter file. If a future
  charter edit drops or reworks it, this ADR silently returns to `(guidance — no gate)` — exactly the
  state the round found it in. FT-12 records the check id in `enforcement.md` so the dependency is
  visible from the enforcement side too.

---

## How a reviewer verifies compliance

On any ticket whose diff touches `agents/knowledge/*.md`:

1. **Run the three tests against the hunk.** If any fires and there is no ADR, the edit is a finding:
   the content may be right, but the ticket may not ratify it — route to the Architect.
2. **Test 1 evidence.** For a hunk that constrains anything, the ticket names what it swept and what it
   found (a grep, a file list). "No existing violations" with no sweep is not an answer.
3. **Test 2 floor — read the author's catalog sweep; do not reconstruct the catalog.** If the author
   claims the floor (first statement of a form, not a narrowing), the ticket's `## Review` names the
   catalog file(s) and the term searched, and what it returned. **No search recorded ⇒ the floor is not
   claimed ⇒ route it.** Where a search *is* recorded, check it was run against the **subject** at a
   general level, not against the entry's own new vocabulary — "the catalog never says
   *theme-invariant*" is not silence if a sentence governs *which token supplies ink*.
4. **Test 2 is semantic.** A hunk with no imperative wording that nonetheless carves an exception out
   of a governing sentence still fires. Do not check for the phrase; check for the withdrawal.
5. **Cross-stack claims.** A descriptive claim carries a **file:line in the entry** and imposes no
   obligation ("so X must…" is prescriptive). A prescriptive claim comes from a ticket that ran that
   stack, or an ADR.
6. **Lane.** The hunk was applied in the ticket's own worktree, touching only its own hunk. No
   `git restore` of a shared catalog file; **no `git stash`**.
7. **Inline does not exempt the entry from ADR-0032 (added by the T-0471 panel).** An inline entry that
   constrains call sites still carries `**Enforced by:** <named enforcer> — <tier token>`. A
   floor-qualifying entry has a **zero baseline**, so if the rule is mechanizable on its stack the tier
   is `T1-CI` and the gate ships with the entry. **A tier token naming a mechanism that cannot fail a
   build is `T2-ADVISORY`, whatever the entry says** — `check-consistency.mjs` is in zero `.github/`
   workflows; `frontend-ci.yml:72-74` runs lint with `continue-on-error: true`. Model:
   `patterns-frontend.md:462-465`.

---

## Roles affected

No new code roles. **Reviewer** gains the seven-point check above as **reviewer-check 5 "Catalog-edit
routing"** — a *named* standing item (Block D / FT-11), not prose living inside an ADR; it composes
with ADR-0032's check on the same hunk and, per FT-11, replaces the superseded clause at
`.claude/agents/reviewer.md:105-110`. **Architect** receives the routed edits. The living companion
`agents/architecture/decisions/catalog-governance.md` carries both rules and the current shape.

---

## Follow-up tickets — specs, not files

| # | Title | Layers / size | Panel? | Sequencing |
|---|---|---|---|---|
| **FT-11** | **Land the named enforcer — Block D.** Rewrite `.claude/agents/reviewer.md` step 5's catalog clause into **reviewer-check 5 "Catalog-edit routing"** (the three tests + the floor's evidence rule + ADR-0032's enforcer/tier check), and add the one-line pointer to `quality-gates.md` Gate 1. **This is the condition of acceptance** (M3): until it lands, the only named standing item governing a catalog hunk teaches the axis this ADR replaces, and the floor is `(guidance — no gate)` by ADR-0032's own definition. | architect + docs, **XS** | no | **first.** FT-8 is sequenced behind it. |
| **FT-12** | **Record the check id in `agents/process/enforcement.md`** so the `T3-HUMAN` enforcer is visible from the enforcement side and a future charter edit that drops reviewer-check 5 is a visible regression rather than a silent one. | architect + docs, **XS** | no | with or after FT-11. |
| **FT-8** | **Apply ADR-0033's catalog text** — Block C into `conventions.md` (below ADR-0032's "price of a law" section, in its amended wording, **including its own `**Enforced by:**` line**). `conventions.md` is lane-uncontended. | architect + docs, **XS** | no | **after FT-11**, not merely after acceptance. |
| **FT-9** | **Block B** — the T-0441 cross-stack sentence gains its file:line citation + descriptive label, applied by T-0440 in the `patterns-mobile.md` lane. | ios, **XS** | no | after acceptance, when the lane reaches T-0440. |
| **FT-10** | **(PM scheduling call, not an architect ruling)** — decide whether the **seven** `.models.ts` resolvers T-0274 left inlining the error-key walk get a canonicalization ticket. This ADR only records that the edit was mis-routed under the old axis; whether to chase the call sites is scheduling. | frontend, **S** | no | PM's call. |

---

## What this ADR does **NOT** decide

- **It does not decide what a constraining entry must state about its enforcement** — that is
  **ADR-0032** (named enforcer + declared tier + coverage).
- **It does not re-open T-0274**, T-0441's reviewer verdict, T-0446's SEC-5 routing, ADR-0018, or
  ADR-0032's accepted amendments.
- **It does not change the Reviewer's authority.** A reviewer may still reject a catalog hunk on
  content; this ADR only fixes *which* hunks a ticket may ratify for itself — and, via Block D, makes
  the check the reviewer runs say so.
- **It does not add a fourth test.** Whether an entry that *prices two competing forms* is a trade-off
  that belongs in an ADR rather than a catalog row (finding **F4**) is a new decision. It is filed, not
  decided.
- **It does not decide whether the seven inline `.models.ts` resolvers get chased** — FT-10, PM's call.
- **It writes no file itself.** Blocks B, C and D are **specifications**: Block C → `conventions.md`
  (FT-8, architect), Block D → `.claude/agents/reviewer.md` + a one-line `quality-gates.md` Gate 1
  pointer (FT-11, architect), Block B → `patterns-mobile.md` (FT-9, applied by T-0440 in that lane).
  No `consistency.md`, `security-rules.md`, `INDEX.md` or `patterns-*.md` edit is made from the
  architect's hand here; lane-held files are specified here and applied by the holder.

---

## Challenge

> **Provenance.** The three numbered lines of attack below were nominated by the **ADR-0032 panel lead**
> (the floor's author) when it declined to ratify its own repair. **CH-1…CH-6** were filed on
> 2026-08-05 by the T-0471 challenger instance; the full pass, with every citation, is
> `agents/backlog/adr/challenges/0033-floor.md`. This section carries the condensed form so the trail
> lives in the artifact (`deliberation.md` §"The output handed to developers").

**Open item — the only thing this ADR needed attacked: the floor on test 2 (§D1 test 2).**

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
the split itself (ADR-0032 verdict C8). *Scope was held: the challenger's one out-of-scope idea
(CH-6/F4) was filed for the PM rather than folded in.*

---

### Filed by the challenger, 2026-08-05

**CH-1 — the floor's only operative clause is undecidable, and it splits this ADR's own retro row 3.
BLOCKING.** The tests are ordered, so "no shipped call site becomes a deviation" is a restatement of
test 1 and carries no independent work — the ADR half-admits it with *"(test 1 did not fire)"*. Of the
two remaining disjuncts, *"replacing a named canonical form"* is checkable and *"withdrawing a form the
catalog previously **permitted**"* is not: the catalog prescribes, it does not permit, so permission is
inferable only from silence — the same condition that routes an edit inline. Concretely, row 3 says
T-0451 *"withdraws `Color.dynamic` ink, which the catalog previously permitted everywhere"*; the
sentence that did the permitting is `patterns-mobile.md:577`, a **descriptive** Android→iOS mapping row
that never mentions theme-invariant surfaces. If silence = permission, every first statement withdraws
one and C5 is unrepaired; if silence ≠ permission (the floor's own words), row 3 flips to inline and
the T-0451 refusal that generated both ADRs was wrong. *"Test 1 catches those cases anyway"* fails:
row 3's own T1 column is *"no (the two heroes are the ones being fixed)"*.

**CH-2 — the floor's enforcer asserts the rule the floor replaces. ADR-0032 D3, applied to ADR-0033.
BLOCKING.** The only *named* standing item governing a catalog hunk is `.claude/agents/reviewer.md`
step 5 (`:105-110`), and it instructs verbatim the abolished axis: *"a small clarification/example is
fine to pass with the change; anything that redefines 'the one way to do X' is an Architect call."*
`quality-gates.md` has **no** catalog-edit item (Gate 1 `:92-104` governs conformance, not ratification).
So by ADR-0032's own line — *"T3-HUMAN requires a **named** checklist item; 'the reviewer will notice'
is not T3"* — this floor ships at `(guidance — no gate)`. It has already been routed around once, after
ADR-0032 was accepted: `patterns-mobile.md:265-276` (T-0473) constrains call sites (*"hoist it one level
further"*, *"not a whole-file `contains`"*) with **no** `**Enforced by:**` label, and its ticket
self-classified at `T-0473-…md:337-339` as *"a testability clarification, not a redefinition"* — the
same words as `T-0274-…md:133`. `**Enforced by:**` occurs **0** times in `patterns-mobile.md` (8
repo-wide in `agents/knowledge/`), and ADR-0032's own Block A was never applied (`:292-304` carries no
label). *A rule whose only home is a page the checker does not read is folklore with a citation.*

**CH-3 — the evidentiary burden is inverted relative to test 1.** Reviewer check 2 puts a **code**
sweep on the author (*"'No existing violations' with no sweep is not an answer"*); check 3 puts the
**catalog** sweep on the reviewer (*"ask for the previously permitted alternative"*), who must
reconstruct what a 1000-line catalog used to permit while the author asserts silence for free. T-0274's
failure had exactly that geometry, and T-0473 ran it again two sprints later.

**CH-4 — both limbs look backwards, so the floor discriminates in proportion to how much code already
exists.** A first statement binds every *future* call site and the floor prices that at zero;
`patterns-mobile.md` carries 22 "the ONE way" lines on the stack still being written. And the
composition with ADR-0032 is unstated: a floor-qualifying entry has a **zero baseline by
construction**, which is ADR-0032 D2's condition (b) — so mechanizable inline entries owe `T1-CI`,
while the mechanisms authors will reach for cannot fail a build (`check-consistency.mjs` in **zero**
`.github/` workflows; `frontend-ci.yml:72-74` lint at `continue-on-error: true`).

**CH-5 — the retro-validation is not fitted, but four rows is thin; three more cases found, two of
which the floor routes against history.** Searched: all 60 tickets mentioning a `patterns-*.md` file;
every backlog file matching the harvest self-classification phrases; and the two tickets that *are*
architect ratifications of a dev harvest — **T-0397** and **T-0379**. T-0397 row 1 (full-bleed header
idiom) and T-0379's `format: date` row both clear test 1 (baselines verified zero in their own
ratifications) and are governed by no catalog sentence ⇒ **inline** here, **Architect** in fact.
T-0397 row 2 (`.medium` detent) fires test 2 — the withdrawn form is nameable at
`patterns-mobile.md:1230` ⇒ **Architect** ✅. Rows 1 and 2 are the *same harvest ticket*, split by
whether the catalog happened to describe the old form. **Honest result: no case exists where the floor
routes inline something a later reader would call plainly wrong** — in both divergences the architect
ratified the substance (*"RATIFIED as-is"*; *"no trade-off survives"*). What row 5 loses is a
verification round that **added content the row lacked** (`T-0397-…md:66-80`).

**CH-6 — no limb for "this may carry a trade-off, so it may be an ADR."** The ground three reviewers
actually used on T-0397. **Filed as a separate finding (F4), not folded in** — AC2.

## Defense

*Written by the author instance carrying this ADR forward (the ADR-0032 panel lead's text is being
defended, not re-authored). One of REBUT / CONCEDE + REVISE / ESCALATE per challenge, per
`deliberation.md` §3.*

**CH-1 — CONCEDE + REVISE.** The challenge is right, and it is right for a reason the draft could not
see from inside itself: the draft used "permitted" as though the catalog issued permissions, and it
does not. I checked the challenger's central citation rather than taking it — `patterns-mobile.md:577`
is a mapping-table row describing `CleansiaColors`, and it contains no sentence about fixed surfaces.
The attempted rebuttal (*conjunction with test 1 saves the floor*) does not survive the ADR's own row-3
T1 column. **Revised (M1):** the trigger is now *"a catalog sentence already governs the subject at any
level of generality"*, with silence defined as *no sentence at any level*. I re-derived rows 2 and 3
from the files, not from the draft: `:577` governs which token supplies ink ⇒ T-0451 narrows ⇒
Architect; nothing at any level governs what an Api-adapter request-side test asserts (the nearest,
`:167-175`, is about normalizing a business-key 400 at the repository) ⇒ T-0441 inline. **The table is
preserved and row 3 stops being circular.** Alternatives **G** and **H** record the two wordings
rejected on the way.

**CH-2 — CONCEDE, and it is the condition of acceptance.** I attempted the rebuttal that ADR-0033's
own §"How a reviewer verifies compliance" *is* the named item, and it fails on ADR-0032 **D3**, which I
am bound by: an enforcer must assert what the sentence claims, and the enforcer a reviewer actually
runs (`reviewer.md:105-110`) asserts the **superseded** axis. The T-0473 evidence closes it — an entry
that landed after ADR-0032 was accepted, in the file ADR-0032 was written for, with no label and the
abolished self-classification. **Revised (M3): Block D**, `FT-11`, `FT-8` sequenced behind it, and this
ADR's own Block C carries `**Enforced by:** reviewer-check 5 — T3-HUMAN`. That also discharges
**T-0471 AC6**: the enforcer + tier obligation is applied *to* ADR-0033's catalog text, so ADR-0032 is
consumed, not amended by side effect.

**CH-3 — CONCEDE + REVISE (M2).** No rebuttal available: the asymmetry is on the page, check 2 vs
check 3. The repair is one grep line, and the important half is the **default**: missing evidence
routes to the Architect rather than falling through to inline. That is Gate 0's "REFUTED by default"
posture applied to a routing question, and it is what stops the floor from being self-certifying.

**CH-4 — SPLIT: rebut the framing, concede the omission.** *Rebutted:* the floor does not **contradict**
test 1 — the tests are conjunctive and consistent, and "a first statement binds future call sites" is
not a defect of the floor but the deliberate content of `conventions.md`'s inline lane: a form that
obliges nothing shipped can be written down by the person holding the context. Routing every first
statement to the Architect is **Alternative B**, already rejected, and the challenger did not argue for
it. *Conceded:* the ADR-0032 composition was genuinely missing, and it is the mechanism that keeps the
inline lane honest — an entry that clears the floor has a zero baseline **by construction**, so a
mechanizable rule owes `T1-CI` in the same ticket, and a mechanism that cannot fail a build is
`T2-ADVISORY` however it is labelled. **Revised (M4)** in D1, in Block C, in reviewer check 7, citing
`patterns-frontend.md:462-465` as the model. The greenfield weakness is real and is now stated in
§Consequences rather than left for a future reader to discover.

**CH-5 — CONCEDE the thinness, REBUT "fitted", and adopt the cases (M5).** The table was not fitted: an
independent instance re-derived rows 2 and 3 from the catalog files and both hold under the amended
wording. But "one row moves" *is* thin, and the challenger did the work the draft owed — three real
historical cases, two of which route against history. Both are added to the table with their divergence
marked, and §Consequences now says plainly what the floor buys out. The challenger's own finding that
**no plainly-wrong case exists** is recorded as the result, not omitted: it is the strongest available
evidence that the floor's direction is right, and it was produced by the party trying to break it.

**CH-6 — ESCALATE-as-filed, not defended.** The author agrees it is a real gap and agrees it is out of
scope for this round. It is F4.

## Verdict

**Consensus: reached, with amendments. Status: `accepted` for the amended decision recorded above.
Zero blocking challenges remain.**

> **Panel composition — stated honestly, because T-0471 AC1 is specific about it.** The floor's
> **author** was the ADR-0032 panel lead (a different instance, 2026-08-01), which declined to ratify
> its own repair. The **challenger** and the **lead** in this round were the **same T-0471 architect
> instance** (2026-08-05): the invocation carried no capability to spawn a separate lead instance.
> **What AC1 exists to prevent is repaired** — the floor was not ratified by the party that invented
> it, and the challenge was written to break it (two blocking findings, both conceded, both changing
> the decision text). **What AC1 literally requires is not fully met** — challenger ≠ lead was not
> achievable here. The mitigations actually used: the challenger pass was written **first**, filed as a
> standalone artifact (`challenges/0033-floor.md`) before any defense existed, under Gate 0
> REFUTED-by-default with every claim cited to a file read in the tree; and the ruling **overrules the
> challenger in part twice** (CH-4's contradiction framing, CH-5's "fitted") on stated evidence, which
> a rubber stamp does not do. **The PM should treat AC1 as SATISFIED-IN-PART and decide whether a
> second-instance re-check is warranted; the amendments are concessions to the challenger's own
> proposals, so a re-check has a narrow surface.** (`deliberation.md` §4 re-check right is preserved:
> a new hole in the *amended* text is a new challenge, not a re-litigation of CH-1…CH-6.)

**The three nominated lines of attack, each ruled (T-0471 AC3 — none closed unruled):**

| # | Nominated line | Disposition | Reason |
|---|---|---|---|
| **1** | *Is "previously permitted" decidable? Is the reviewer's "name the withdrawn form" check enough to close it?* | **SUSTAINED** (via CH-1 + CH-3) | It is not decidable as drafted — the catalog prescribes rather than permits, so "previously permitted" reduces to "was silent", which is the floor's own inline condition; `patterns-mobile.md:577` shows it splitting the ADR's own row 3. And **no**, check 3 was not enough: it asked the reviewer to reconstruct the catalog's history while the author asserted silence for free. **Repaired by M1** (sentence-level trigger + silence defined) **and M2** (author records the catalog sweep; missing evidence routes). |
| **2** | *Does the floor contradict test 1 — can the catalog acquire canonical forms with no Architect involvement?* | **SUSTAINED IN PART** | **Overruled** on contradiction: the tests are conjunctive and consistent, and "a first statement obliges nothing shipped ⇒ inline" is the deliberate content of `conventions.md`'s harvest loop; the alternative is Alternative B, already rejected and not argued for. **Sustained** on the consequence: routing is now purely retrospective, so on the stack where code is thinnest the floor routes nearly everything inline. What holds the line is **not** the routing test but **ADR-0032's price attaching to the inline lane** (M4 — zero baseline by construction ⇒ `T1-CI` owed where mechanizable) and **reviewer-check 5** (M3). Both were missing; both are now in the ADR, and the residual risk is stated in §Consequences. |
| **3** | *Is the retro-validation honest, or fitted? Find a case where the floor gets it wrong.* | **SUSTAINED IN PART** | **Overruled** on "fitted": rows 2 and 3 were re-derived independently from the catalog files and hold under the amended wording. **Sustained** on thinness: four rows, one moving, is not evidence for a rule governing every future catalog edit. The challenger produced **three** further real cases (T-0397 ×2, T-0379's `format: date`), **two of which the floor routes against history** — added as rows 5–7 with the divergence marked (M5). **The requested "plainly wrong" case does not exist**: in both divergences the Architect ratified the substance, so the floor's answer was right and what it costs is a verification round that, in one case, added content the row lacked. Under **T-0471 AC4** that is reported as a **pass with the search named**, not as silence. |

**The additional findings, ruled:**

| # | Disposition | Reason |
|---|---|---|
| **CH-1** | **SUSTAINED — blocking, and it changed the decision text** | The floor's operative clause was undefined and self-contradicting; **M1** redefines the trigger on the *sentence* and defines silence. Rejected wordings recorded as Alternatives **G** and **H** so the next reader sees what was tried. |
| **CH-2** | **SUSTAINED — blocking, and it is the condition of acceptance** | ADR-0032 D2/D3 applied to ADR-0033: the only named standing item (`reviewer.md:105-110`) asserts the superseded axis, so the floor would ship at `(guidance — no gate)`. Verified: `**Enforced by:**` = **0** occurrences in `patterns-mobile.md`; the post-acceptance T-0473 entry (`:265-276`) constrains call sites with no label and the abolished self-classification. **M3** — **Block D**, **FT-11**, and **FT-8 sequenced behind it**. |
| **CH-3** | **SUSTAINED** | Burden inversion vs check 2 is on the page and reproduces T-0274's geometry. **M2** — symmetric catalog sweep, **route-by-default on missing evidence**. |
| **CH-4** | **SUSTAINED IN PART; contradiction framing OVERRULED** | See nominated line 2. **M4** — the ADR-0032 composition is stated, with `T2-ADVISORY` named for mechanisms that cannot fail a build (`check-consistency.mjs`: zero `.github/` hits; `frontend-ci.yml:72-74`: `continue-on-error: true`) and `patterns-frontend.md:462-465` cited as the house model. |
| **CH-5** | **SUSTAINED IN PART; "fitted" OVERRULED** | See nominated line 3. **M5** — table extended to seven rows, divergences owned in-place, and the trade written into §Consequences. |
| **CH-6** | **NOT RULED HERE — filed as F4** | Out of AC2 scope. Adding a fourth test is a new decision, not a repair of the floor; the challenger filed it correctly rather than folding it in. |

**Amendments carried (M1–M6):** **M1** sentence-level trigger + silence defined · **M2** symmetric
catalog-sweep evidence with route-by-default · **M3** Block D, the named `reviewer-check 5`, FT-11
before FT-8, and this ADR's Block C labelled with its own enforcer + tier · **M4** the ADR-0032
composition (floor ⇒ zero baseline ⇒ `T1-CI` owed; non-blocking mechanisms are `T2-ADVISORY`) ·
**M5** retro-validation 4 → 7 rows with both divergences owned · **M6** the stale **Number note**
corrected in place (permissible: this ADR was `proposed`), with ADR-0032's identical stale note routed
to the PM as **F1** because it is `accepted` and needs a signed erratum (`adr/README.md:16-26`).

**What the panel accepted:** D1's three ordered tests, unchanged in structure; the floor, **amended in
wording and in enforcement**; D2 entirely (untouched, carried consensus); the retro-validation as
extended.

**What the panel rejected:** the draft floor's "previously permitted" wording (Alternative G); the
topic-level reading of silence (Alternative H); and leaving enforcement to `conventions.md` alone
(Alternative I).

**Findings routed to the PM — not fixed from inside this ticket** (all detailed in
`challenges/0033-floor.md` §Findings): **F1** ADR-0032's stale Number note needs a signed erratum ·
**F2** ADR-0032's Block A was never applied and `**Enforced by:**` has zero instances in
`patterns-mobile.md` · **F3** `patterns-mobile.md:265-276` (T-0473) carries no enforcer + tier and its
test-1 sweep was never run — **recorded, not re-opened**, on the T-0274 precedent · **F4** CH-6's
missing "carries a trade-off ⇒ ADR" limb · **F5** `patterns-mobile.md:1230` still grants the `.medium`
detent that `:985-990` withdrew.

**Escalations to the owner:** none. Every disagreement resolved on in-repo evidence; nothing here
carries lasting business impact requiring an owner ruling.

**Gate 0.5, applied to a deliberation (T-0471 AC7 — say what this could not verify):**
- **Leg 1 (mutation-prove the test) DOES NOT APPLY.** This ADR's evidence is not an executable
  assertion — it is a routing rule whose subjects are Markdown edits. `quality-gates.md:67-70` scopes
  leg 1 *"by the evidence, not the ticket type"* and directs exactly this case to be declared here
  rather than to have a mutation invented for it. Manufacturing a test that asserts the ADR's own
  prose is what `knowledge/testing.md` calls theatre, and ADR-0032's D-series was written against that
  temptation.
- **Leg 2 (a cached run is not a run) DOES NOT APPLY.** No suite, build or checker was run in this
  round, so there is no green to qualify.
- **Leg 3 — what this round could NOT verify, named:**
  1. **Challenger ≠ lead** was not achievable in this invocation (see the composition note). Declared,
     not papered over.
  2. **F3 is unresolved by design.** Whether `patterns-mobile.md:265-276`'s *"not a whole-file
     `contains`"* clause puts shipped tests in violation was **not** determined. The tree holds **14**
     guard tests that read source as a fixture (`ProfileAvatarBindingTests`,
     `OrderDetailSummaryBindingTests`, `OrderStatusPillPlacementTest.kt`, `BrandIconCatalogTest.kt`,
     `ConsentCatalogTest.kt`, `FixedWhiteContrastTests`, …); each was **not** opened and read for the
     withdrawn shape. That sweep belongs to the ticket that made the claim, and it is the finding.
  3. **The retro-validation is a historical re-read, not a prospective test.** Seven cases is better
     than four and is still a sample; the rule's real evidence arrives as the next catalog edits are
     routed under it. §Consequences names what a pattern of wrongly-inlined first statements would
     look like, and the response is a superseding ADR, not a workaround.
  4. **Line numbers are this worktree's.** `agents/**` carries no uncommitted changes here per the
     session's git status, so they are `master`'s — but `patterns-mobile.md` is a shared-file lane
     with live worktrees, so a cited line may have moved by the time FT-11/FT-8 apply. Cited text, not
     only offsets, is given wherever a citation is load-bearing.

---

## 2026-08-05 — Independent lead adjudication (T-0471 **AC1**)

> **Appended per `adr/README.md` §1** — a dated, attributed, **record-only** section. **The body above is
> not rewritten and no decision content changes here.** Where this pass finds the accepted decision
> *insufficient*, it says so and **routes a new panel**; it does not write the repair. A lead that invents
> the fix and ratifies it reproduces, at one remove, the exact defect T-0471 exists to repair — the
> status block above says so in the ADR's own words, and it binds this instance too.
>
> **Author of this section:** a **third** architect instance, distinct from (a) the floor's author (the
> ADR-0032 panel lead, 2026-08-01) and (b) the T-0471 challenger, which filed
> `adr/challenges/0033-floor.md` and then declined to self-certify AC1 on the ground that it was also
> acting as lead. That objection was correct. **With this pass the panel composition AC1 requires is
> complete: author ≠ challenger ≠ lead, three instances.** **AC1 is SATISFIED.**

**Method note, declared up front (Gate 0.5 leg 3).** This invocation had **no shell**, so no `git log` /
`git show` was available and **no catalog edit was read as a diff**. The two worked cases below are
therefore real catalog edits identified by their **in-tree ticket tags** plus the tickets that produced
them — recoverable and reproducible by any reader, but *reconstructed* hunks, not diffs. Line offsets are
this worktree's and three cited in the body above have **drifted** (see Corrections of fact).

### Ruling 1 — the repaired trigger (M1) and its evidence rule (M2): **SUSTAINED IN DIRECTION, INSUFFICIENT AS WRITTEN**

The parent question was whether *"no sentence at any level of generality"* is **decidable in practice** —
whether two reviewers reach the same verdict on a real hunk. I applied the repaired clause to two.

**Case α — T-0349, `patterns-mobile.md` "The address-picker = one Core VM, app-local Views (the one way,
T-0349 RESOLVED)" (`:1244-1254` this worktree). DETERMINATE. Test 2 fires. Agrees with history.**
The prior governing sentence is nameable and quotable: `patterns-mobile.md:990` — *"The §7.6 D1
minimal-now/additive-later seam — **feature/VM import no MapKit** (#7/#12/#30)"*, itself the catalog form
of ADR-0013 D6 invariant #7. The T-0349 entry carves an explicit exception out of it: *"the **only**
sanctioned feature-layer `import MapKit` is the View's binding to the `MapProvider` protocol's
MapKit-typed signature … **that View touch is allowed**"*. Applying `:990` to the entry's subject yields
*no feature-layer MapKit*; the entry says *this feature-layer MapKit is allowed*. Exception carved ⇒
**Architect**. History: `T-0349-harvest-address-picker-vm-to-core.md` carries `owner: architect`,
`layers: [ios, architect]` and a `## Architect ruling (2026-06-30)` that reasons from the same invariant
(`:97-99`). **This is an eighth retro row, independently produced, and it is the first case on record
where test 2 fires on a *general* sentence rather than on a specifically-named form** — which is
precisely the limb M1 added. It also fires in the shape §Consequences names as the floor's load-bearing
case: test 1 does **not** fire, because the edit converts its own violator (the duplicated partner VM) in
the same change. **The "at any level of generality" limb does real, reproducible work. That is the
strongest evidence in this ADR's file for M1, and it was not available to the round.**

**Case β — T-0473, `patterns-mobile.md:265-276` ("A colour-resolver test does not cover the call site").
NOT DETERMINATE. Two reviewers reach opposite verdicts on the recorded evidence.**
The entry forbids a form: *"the sanctioned fallback is a source-text assertion **scoped to the one block**
… **not a whole-file `contains`**."* A candidate governing sentence exists and is nameable:
`patterns-mobile.md:520-522` — *"a screen with no test seam gets a **source-text scan scoped to the
file**"*.
- **Reviewer A routes it to the Architect:** `:520` governs source-text-fallback scope at the general
  level and names *file* scope; `:265-276` forbids exactly that (*"not a whole-file `contains`"*).
  Exception carved out of a sentence that governs the subject ⇒ test 2 fires. Reviewer A is following
  §"How a reviewer verifies compliance" item 3 to the letter (*"check it was run against the **subject**
  at a general level, not against the entry's own new vocabulary"*).
- **Reviewer B routes it inline:** `:520`'s file-scoped scan is a prescription for a *different* subject
  (an untranslated string literal, where any literal anywhere in the file is the defect). Applied to
  `:265-276`'s subject — *which colour role a screen hands a component* — `:520` yields **no**
  prescription at all: a whole-file scan neither passes nor fails on that question. Nothing is carved out;
  a scope prescription is *added* where none existed ⇒ inline.

**Reviewer B is right, and the ADR does not say why.** The disambiguating rule is *"does the candidate
sentence, applied to **this entry's** subject, yield a prescription that the entry then contradicts?"* —
**and that sentence is nowhere in the ADR.** M1 defines what silence is; it never defines what
**"governs"** is. So the routing verdict is carried by the reviewer's *paraphrase* of the candidate
sentence, and a paraphrase is precisely what CH-1 removed from the *other* half of the clause.

**The same gap is visible in the ADR's own row 3.** M1's load-bearing citation is characterized as *"the
sentence that governs **which token supplies ink**"*. Opened, that sentence
(`patterns-mobile.md:588` this worktree — the Android→iOS mapping row) says `CleansiaColors` carries *"the
**same Material slot names** … as `Color.dynamic(light:dark:)`"*. It does **not** say which token supplies
ink on which surface. Row 3 still fires — T-0451 adds `CleansiaColors.onFixedWhite`, a slot that is
deliberately **not** a dynamic pair, which does carve an exception out of what `:588` states — but a
reader who opens `:588` looking for the ADR's stated reason will not find it. **The trigger moved from an
undefined predicate ("permitted") to a quotable sentence, which is a real repair; the *characterization*
of that sentence is still an assertion, and it is the characterization that routes.**

**Disposition.** M1 is **a strict improvement and it is not sufficient**. The floor is decidable when the
governing sentence, applied to the entry's subject, plainly yields a contradicted prescription (Case α),
and **undecidable when it does not** (Case β) — where the ADR's only interpretive aid pushes reviewers in
one direction only, toward over-firing. **Routed as finding L1**; it needs the missing operational
sentence, and that is a decision, not an erratum.

**M2 (the evidence rule) is SUSTAINED without qualification.** It is the one amendment that changes what a
reviewer can *do* rather than what a reviewer is *told*: an author-recorded catalog sweep is checkable and
"the catalog was silent" is not, and **route-by-default on missing evidence** removes the self-certifying
path. Note the composition, which is not stated above and is favourable: M2's default masks part of L1 —
where "governs" is arguable, the author's recorded sweep is the artifact the reviewer argues *with*, so the
disagreement becomes visible instead of silent. It **narrows** the residual; it does not close it, because
both reviewers in Case β can record the same search and still differ on what it means.

### Ruling 2 — the enforcement amendment (M3 / Block D): **the challenge SUCCEEDED and the amendment does NOT yet fix it**

Re-verified independently in this tree, all four facts:

| Fact | State on 2026-08-05, after the round |
|---|---|
| `.claude/agents/reviewer.md:105-110` | **still the superseded axis, verbatim** — *"a small clarification/example is fine to pass with the change; anything that redefines 'the one way to do X' is an **Architect** call"* |
| `agents/knowledge/conventions.md:122-127` | **also still the superseded axis** — the *author's* page teaches it too, which the round did not measure |
| ADR-0033's Block C in `conventions.md` | **absent** — the section following "The price of a law" is `## Naming (canonical)` |
| `**Enforced by:**` in `patterns-mobile.md` | **0** (7 in `agents/knowledge/` on the strict `Enforced by:` form; 9 counting the two variant forms in `roles/`) |
| **FT-11 / FT-12 / FT-8 as filed tickets** | **none.** `INDEX.md` carries **no row** for any of them |

**Therefore: `reviewer-check 5 "Catalog-edit routing"` does not exist.** By ADR-0032 D2's own line — *"`T3-HUMAN`
requires a **named** checklist item; 'the reviewer will notice' is not T3 — an unnamed human enforcer is
`(guidance — no gate)`"* — **ADR-0033 ships today at `(guidance — no gate)`, which is the exact state CH-2
declared blocking.** §Consequences' claim *"Block D **moves** the check out of this ADR and into the
reviewer's own numbered list, so the rule that is run and the rule that is written are the same one"* is
written in the present tense and is **false today**.

**M3 is a genuine improvement over the draft** — a specification with an applier, a sequencing constraint
and an acceptance condition is strictly more actionable than a paragraph, and the round's diagnosis (the
only named enforcer asserts the *superseded* rule) is the single most valuable thing it produced.
**But on the parent question — does the amendment fix it, or describe a fix? — it describes one.** The
concession to a blocking finding was to write another specification into the same artifact whose
unreachability was the finding, and to make its landing a **follow-up that was never filed**.

**Consequence, ruled:** an ADR may not be `accepted` on a condition of acceptance that is unmet. I cannot
and will not reopen the status — the decision's *direction* survived a real attack and reversing that would
be worse — so this section records the operative state instead:

> **ADR-0033 is `accepted` and NOT IN FORCE.** Its D1 routing test binds nothing until **FT-11** lands.
> Until then `conventions.md:122-127` is what an author applies and `reviewer.md:105-110` is what a
> reviewer runs, and both teach the axis D1 replaces. **FT-11 is not a follow-up; it is the remainder of
> the decision.** Routed as finding **L2** with a request that the PM file it as a ticket, since an
> ADR-internal table row has now demonstrably not caused one to exist.

### Ruling 2b — a defect the round did not find: **Block C as specified installs a contradiction into `conventions.md`**

This follows from Ruling 2's second row and it is blocking-grade.

ADR-0033's header states, under **Refines:** *"it does **not reverse** that rule, it makes its routing test
**decidable** and gives it a floor."* Opened, the rule it refines reads:

> `conventions.md:125-127` — *"a **new canonical archetype** **or** anything that changes 'the one way to
> do X' across the codebase → this is an **Architect** call … don't unilaterally redefine the standard."*

That is a **disjunction**, and the floor **reverses its first limb**: a first statement of a canonical form
where no sentence governs the subject *is* "a new canonical archetype", and the floor routes it **inline**.
The ADR argues past this by quoting only the second limb (§Consequences: *"`conventions.md:125-127`'s
concern is *changing* the one way"*). Its own table proves the reversal: **retro row 7** routes T-0379's
`format: date` row inline where history routed it to the Architect **on the ground that it *"defines the
one way for date-only wire on iOS"***.

Reversing limb 1 is a legitimate architectural choice, argued and evidenced — **the defect is that Block C
does not implement it.** Block C instructs: *"Insert **after** the existing numbered list."* It never
amends, deletes or annotates `:122-127`. So when FT-8 lands as specified, `conventions.md` will instruct,
on one page, both *"a new canonical archetype → Architect"* and *"a first statement of a canonical form →
inline"*. **That is finding F5's disease — a catalog carrying two incompatible forms — shipped into
`conventions.md` by design, in the very edit whose purpose is to stop authority drift.** Routed as **L3**.
The fix is a decision (which limb-1 text survives, and what the residual sentence says), not an erratum.

### Ruling 3 — the two OVERRULED challenges: both re-derived, and **neither is an overrule of the challenger**

§Verdict rests part of its AC1 argument on this: *"the ruling **overrules the challenger in part twice**
(CH-4's contradiction framing, CH-5's 'fitted') on stated evidence, which a rubber stamp does not do."*
Re-derived against `challenges/0033-floor.md`:

**(a) The substance of both overrules is CORRECT.** *Contradiction*: the three tests are **ordered**, so
test 2 is only asked once test 1 has not fired; no edit can receive inconsistent verdicts, and routing
every first statement to the Architect is Alternative B, already rejected. **OVERRULE UPHELD.** *Fitted*:
the draft's row 3 rationale **was** written backwards from a known outcome — CH-1 proved it, since the
sentence said to have "permitted" `Color.dynamic` ink never mentions the subject — but rows 2 and 3 do
hold under the amended wording when re-derived from the files, as I re-derived row 3 above. **OVERRULE
UPHELD**, with the qualification that what was overruled is *"the table is fitted"*, not *"the draft's
stated reasons were fitted"*, and the latter is true.

**(b) Neither position was the challenger's.** `challenges/0033-floor.md` CH-4 is titled *"Both limbs of
the floor look backwards…"* and **never asserts a contradiction**; the §Defense concedes this in passing
(*"the challenger did not argue for it"*) and the §Verdict nonetheless books it as CH-4 partly overruled.
CH-5 is titled *"The retro-validation is **not fitted**, but four rows is thin"* — **the challenger
affirmatively cleared the ADR on that point, unprompted, and is recorded as having been overruled on it.**
Both "overrules" are of the **author's own nominated framings** (nominated lines 2 and 3), not of anything
a challenger argued.

**Ruled: the dispositions stay** — they answer questions the round was obliged to answer (AC3) and the
reasoning is sound. **The attribution is corrected**: CH-4 and CH-5 were **SUSTAINED**, in full, on
everything their author actually claimed. **And the AC1 mitigation that leans on them does not hold** — a
panel does not demonstrate independence by overruling positions nobody took. **AC1 is satisfied by *this*
pass, on composition, not by that argument.** This is the specific reason the challenger's refusal to
self-certify was right, and it is what an independent read was for.

### Corrections of fact (record-only; no decision content)

1. **Citation drift is live in this worktree, and it hit every load-bearing offset.** `:577` → **`:588`**
   (the `CleansiaColors` mapping row, M1's founding citation) · `:985-990` → **`:996-1001`** (the T-0397
   `.medium` withdrawal) · `:1230` → **`:1241`** (the F5 grant). `:265-276` (T-0473) is stable. The round
   pre-declared this risk and quoted its text, which is what made every citation recoverable — the practice
   is vindicated and should continue.
2. **`**Enforced by:**` counts.** **0** in `patterns-mobile.md` (the load-bearing number, confirmed).
   Repo-wide in `agents/knowledge/` the strict `Enforced by:` form returns **7**, not 8; the round's 8
   counted two `roles/` variants that omit the colon (`roles/post-commit-effects.md:32` *"**Enforced by**
   (ADR-0032 D2 …"*, `roles/order-availability.md:130` *"**Enforced by `TC-TAKE-ONE-ERROR`**"*). Both
   counts are defensible; the label is not yet uniform enough to grep for one way, which is itself worth
   knowing before FT-4.
3. **F5 is confirmed and has been fixed.** `patterns-mobile.md:1241` granted
   `.presentationDetents([.medium])` for the promo/referral code dialogs that the architect-ratified rule
   above withdrew. It was also **factually stale**: the shipped form is
   `CodeSheetShell.swift:29` `.fixedSize(horizontal: false, vertical: true)` + `:36`
   `.presentationDetents([.height(contentHeight)])` + `:78` `CodeSheetHeightKey: PreferenceKey`. The
   withdrawal survives; the grant is retracted in place with a dated erratum note.
4. **F1 is confirmed and is wider than filed.** ADR-0032 `:23-25` is stale as reported — **and `:14`
   (*"ADR-0033 is `proposed`, not accepted"*) was made stale by this very round.** ADR-0032's signed
   erratum must cover **both**.

### Findings routed (this section fixes none of them)

| # | Finding | Route |
|---|---|---|
| **L1** | **M1 defines *silence* but never defines *governs*.** The missing operational sentence — *"a sentence governs this entry's subject iff, applied to that subject, it yields a prescription the entry contradicts"* — is what makes Case β determinate. It is **a decision, and it must not be written by a lead**: a **new ADR refining ADR-0033 D1, with its own panel**. Do not allocate a number until the panel spawns. | PM → architect panel |
| **L2** | **FT-11 is the remainder of the decision, not a follow-up, and it has no ticket.** Until it lands ADR-0033 is `(guidance — no gate)` and both the author's and the reviewer's pages teach the superseded axis. File it, FT-12 and FT-8 as real `INDEX.md` rows with FT-8 sequenced behind FT-11. **Widen FT-11's scope**: the round measured only `reviewer.md`; `conventions.md:122-127` teaches the same superseded axis to the author. | PM → architect + docs |
| **L3** | **Block C as specified installs a contradiction into `conventions.md`** — it appends the new routing test and leaves `:122-127`'s "new canonical archetype → Architect" limb standing, which the floor reverses. Also: **ADR-0033's "Refines … does not reverse" header claim is false as to that limb.** Fold into L1's panel; **FT-8 must not be applied as specified.** | PM → architect panel |
| **L4** | **§Verdict's "overrules the challenger in part twice" is not supported** (Ruling 3b). Corrected here, record-only; the AC1 argument it supported is superseded by this section's composition. | closed here |
| **F1** | Confirmed, **widen to two stale statements** (`:23-25` *and* `:14`). Signed erratum on `accepted` ADR-0032. | PM → architect |
| **F2** | Confirmed. `patterns-mobile.md` has **0** `Enforced by:`; the T-0451 entry (`:292-304`) carries neither label nor residual sentence. FT-4 has nothing to build on. | PM → ios lane |
| **F3** | Confirmed as filed, and **its test-1 question is now answered in part**: the candidate governing sentence is `patterns-mobile.md:520-522`, and under Ruling 1 Case β the T-0473 entry **routes inline** — so the mis-routing F3 suspected is *not* established. What stands is the missing enforcer + tier and the unrun sweep. **Recorded, not re-opened** (T-0274 precedent). | PM |
| **F4** | Confirmed real, with better evidence than filed: `T-0397-…md:70` shows the architect ruling *"carries a real trade-off — should it be an ADR, not a catalog row? Ruling: no trade-off survives"*, so the limb is a ground **actually used** and answered. Fold into L1's panel — a fourth test is the same decision as defining "governs". | PM → architect panel |
| **F5** | **FIXED** in this pass (Corrections of fact 3). | closed |

### What this pass could NOT verify (Gate 0.5 leg 3)

1. **No shell.** No `git log`/`git show`; no catalog edit was read as a **diff**. Cases α and β are
   reconstructed from in-tree tagged entries plus their tickets. A diff-based re-run could find a hunk
   whose routing differs from the entry's settled text.
2. **Two cases, not a corpus.** One determinate fire, one indeterminate. That is enough to rule M1
   *insufficient* (one reproducible indeterminacy is a counter-example) and **not** enough to say how often
   Case β's shape occurs.
3. **Case β's verdict is mine.** Reviewer B is right on the reading I give, but the ADR as written does not
   compel it — which is the finding, and it means a reader may reasonably rule Case α/β differently until
   L1 lands.
4. **No behaviour was executed.** Legs 1 and 2 do not apply, for the reasons §Verdict already gives.

**Consensus: reached. Zero blocking challenges remain against the decision as accepted.** The floor's
direction is right, M2 is unqualifiedly sound, and M1 is a real repair with a named residual. **L1, L2 and
L3 are not challenges to the accepted text — they are the next decision, and they belong to a panel this
lead is not entitled to be the author of.** No escalation to the owner: nothing here carries lasting
business impact.
