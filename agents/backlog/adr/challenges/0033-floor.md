# ADR-0033 — Challenger pass (the test-2 floor, and nothing else)

Role: **CHALLENGER**, per T-0471. Scope held to **one item**: the floor on §D1 test 2. Test 1, test 3,
D2's structural-vs-behavioural line and everything ADR-0032 settled are **not** re-opened; where I
believe one of them must move, it is filed at the end as a **separate finding**, not folded into this
round (T-0471 AC2).

Gate 0 discipline: **REFUTED by default.** Every claim below cites a file, a line, a count or a
ticket I read in this worktree. `agents/**` carries no uncommitted changes in this tree per the
session's git status, so the line numbers are `master`'s. Where a claim of the ADR's **held**, it is
named in §"What survived" rather than left silent.

Six findings. Two are blocking (**CH-1**, **CH-2**). One (**CH-5**) is the case T-0471 AC4 demands,
and it lands **weaker than the ticket hoped** — that is reported as the result, not buried.

---

### CH-1 — The floor has exactly one operative clause, and that clause is undecidable in the only direction that matters. The ADR's own founding case is where it splits — BLOCKING

The floor as written:

> Test 2 fires on a **narrowing** — withdrawing a form the catalog previously permitted, or replacing a
> named canonical form — and **not** on the first statement of a canonical form where the catalog was
> silent and no shipped call site becomes a deviation.

**Strip the redundancy first.** The three tests are **ordered**, and the ADR says so: *"three ordered
tests; the first that fires routes"*. So by the time a reader reaches test 2, test 1 has already not
fired — which is precisely "no shipped call site becomes a deviation". The floor's second conjunct is
a restatement of the preceding test; the ADR half-admits it with the parenthetical *"(test 1 did not
fire)"*. It carries no independent work.

**Of the two remaining disjuncts, one is decidable and one is not.** *"Replacing a named canonical
form"* is checkable: find the named form, or there isn't one. *"Withdrawing a form the catalog
previously permitted"* is not, because the catalog **almost never grants permission** — it prescribes.
Permission is inferred from silence, and the floor's other half says silence is what makes an edit
inline. The floor therefore reduces to: *test 2 fires when the catalog previously permitted the
withdrawn form*, where "permitted" means "did not forbid", which means "was silent" — the same
condition the floor uses to route the edit **inline**. Read literally it is a predicate that is true
and false of the same edit.

**This is not a word game — it splits the ADR's own founding case.** Retro-validation row 3 (T-0451)
asserts test 2 fires because the entry *"withdraws `Color.dynamic` ink, which the catalog previously
permitted everywhere"*. I went and found the sentence that did the permitting. It is
`agents/knowledge/patterns-mobile.md:577` — a row in the Android→iOS mapping table:

> `CleansiaColors` in `Core/DesignSystem` — the **same Material slot names** (`primary`/`onPrimary`/
> `surface`/`outline`/`error`…) as `Color.dynamic(light:dark:)` …

That sentence is **descriptive**, it is about the design system's shape, and it says **nothing** about
theme-invariant surfaces — the concept T-0451 introduces. So:

- If **silence = permission** (the reading row 3 needs), then every first statement of a canonical form
  withdraws the standing "permission" to do it any other way, test 2 fires on everything, and **C5 is
  unrepaired** — which is the entire reason this ADR exists.
- If **silence ≠ permission** (the floor's own words: *"where the catalog said nothing about X"*), then
  row 3 **flips to inline**, and the T-0451 reviewer refusal that generated ADR-0032 *and* ADR-0033 was
  wrong. Both ADRs call that refusal correct.

The ADR asserts the withdrawal and never says which reading produces it. One of its four retro rows is
carried by an undefined term.

**The obvious rebuttal fails, and here is why.** *"It doesn't matter — if the alternative form is
shipped anywhere, test 1 fires first, so the permission question only decides cases where nothing is
at stake."* That would be a good defense if test 1 were absolute. It is not: row 3's own T1 column
reads **"no (the two heroes are the ones being fixed)"** — an edit that fixes its violators in the same
change does not fire test 1. That exception is exactly the shape a real narrowing takes (you withdraw a
form *and* convert its call sites), so the conjunction never saves the floor on the cases that matter.
The permission question is load-bearing and it is undefined.

**Repair (cheap, and it preserves the table).** Make the **sentence**, not the topic, the unit:

> Test 2 fires when a catalog sentence **already governs the subject of this entry at any level of
> generality** and the entry carves an exception out of it, replaces it, or forbids a form it named.
> It does not fire when **no** sentence covers the subject at any level of generality. "The catalog was
> silent about X" means *no sentence covers X at any level of generality* — **not** *no sentence names X
> specifically*.

I re-derived both contested rows against that wording, from the file, not from the ADR:

- **T-0451 → Architect ✅.** `:577` governs *which token supplies ink* at the general level; the entry
  carves out the theme-invariant sub-case. Narrowing, for a stated reason instead of an assertion.
- **T-0441 → inline ✅.** I looked for a sentence governing *what an Api-adapter request-side test
  asserts*. The nearest is `patterns-mobile.md:167-175`, which governs normalizing a business-key HTTP
  400 at the repository — a different subject. Nothing covers it at any level. Not a narrowing.

The table survives; row 3 stops being circular.

---

### CH-2 — The floor's enforcer asserts the rule the floor replaces. ADR-0032 D3, applied to ADR-0033 — BLOCKING

ADR-0032 is `accepted` and binding, and its D3 is the part **both sides of that panel called the
strongest**: *"the named enforcer's assertion must cover the scope the sentence claims."* Its D2 draws
the line for human tiers: *"**T3-HUMAN requires a named checklist item.** 'The reviewer will notice' is
**not** T3 — an unnamed human enforcer is `(guidance — no gate)`."*

So: what enforces ADR-0033's floor? The answer is §How a reviewer verifies compliance — six steps that
live **inside this ADR**. I went looking for the standing checklist item that carries them.

**1. The reviewer's charter still teaches the abolished axis.** `.claude/agents/reviewer.md:105-110`,
step 5 — the only numbered standing item in the repo that governs a catalog hunk:

> If the change **edits the knowledge catalog** (`patterns-*.md` / `consistency.md`) to harvest a
> newly-discovered pattern … sanity-check it: **a small clarification/example is fine to pass with the
> change; anything that redefines "the one way to do X" is an Architect call**.

That is verbatim the *"is this a small clarification or a new archetype?"* axis ADR-0033 §D1 exists to
replace, and it is what the reviewer will actually run. Nothing in the charter mentions the three
tests, the floor, or an enforcer + tier.

**2. `quality-gates.md` has no catalog-edit item at all.** Gate 1 (`:92-104`) requires *the change* to
conform to conventions and the stack catalog; it says nothing about *editing* them. Grepping the file
for `knowledge/|catalog|patterns-` returns `:93`, `:94`, `:111`, `:129`, `:136`, `:319` — five
references to reading the catalog and one shared-file-lane mention. Zero about ratifying a hunk.

**3. Therefore ADR-0033's floor ships at `(guidance — no gate)` by ADR-0032's own definition** — and
worse than unenforced: the named human enforcer asserts the **superseded** rule, which is the exact
D3 failure mode ADR-0032 was written to close. The `CleansiaWeb` precedent is the same shape: a gate
that exists, runs, and is green while asserting a fraction of its sentence.

**This is not hypothetical — it has already happened once, after ADR-0032 was accepted.**

- `agents/knowledge/patterns-mobile.md:265-276` is a catalog entry tagged **(T-0473)**. It constrains
  code other people write — *"when a screen's styling is a bare argument (`contentColor:` /
  `colorScheme.x`), **hoist it one level further**"* — and it forbids a form: *"a source-text assertion
  scoped to the one block … **not a whole-file `contains`**."* Under ADR-0032 D2 that entry owes
  `**Enforced by:** <enforcer> — <tier>`. **It carries none.**
- Its ticket self-classified on the abolished axis, in the same words T-0274 used:
  `T-0473-…md:337-339` — *"That is a **testability clarification, not a redefinition** of the
  destructive law."* Compare `T-0274-fe-error-resolver-dedup.md:133` — *"Small clarification to an
  existing rule, not a new archetype."* The identical self-classification, two sprints apart, on the
  identical failure mode, after both ADRs were drafted.
- **`**Enforced by:**` appears ZERO times in `patterns-mobile.md`.** Repo-wide in `agents/knowledge/`
  it appears **8** times — `patterns-backend.md:638`, `:729`; `patterns-frontend.md:462`;
  `consistency.md:332`, `:395`; `roles/post-commit-effects.md:32`; `roles/order-availability.md:130`;
  and the template in `conventions.md:141`. None in the 1093-line file that holds ~22 laws and that
  ADR-0032's own Block A was written to edit. **ADR-0032's Block A was never applied**: the shipped
  T-0451 entry at `patterns-mobile.md:292-304` has no enforcer line and no residual-scope sentence.

An accepted ADR whose catalog label has zero instances in the file it was written for, and whose next
harvest into that file arrived unlabelled and self-classified under the rule it abolished, is being
routed around, not complied with. ADR-0033 proposes to add a second rule to the same unread page.

**Repair.** Acceptance is worth nothing without a **named** enforcer. ADR-0033 must specify a **Block
D** — the replacement text for `.claude/agents/reviewer.md` step 5, giving the check a name a catalog
entry can cite — and ADR-0033's own Block C must carry
`**Enforced by:** reviewer-check 5 "Catalog-edit routing" — T3-HUMAN`, discharging ADR-0032 on
itself. Until that lands, **Block C must not be applied**: a `conventions.md` section aimed at the
author, with the reviewer still holding the old instruction, changes which rule is *quotable* and not
which rule is *run*.

---

### CH-3 — The evidentiary burden sits on the party without the information, and it is the only test where that is true

Reviewer check **2** (test 1): *"the ticket names what it swept and what it found (a grep, a file
list). **'No existing violations' with no sweep is not an answer.**"* Burden on the **author**, who
holds the diff.

Reviewer check **3** (the floor): *"If the author claims the floor …, **ask for the previously
permitted alternative** the entry withdraws. If one can be named, test 2 fired."* Burden on the
**reviewer**, who must reconstruct what a 1093-line catalog used to permit, from memory, against an
author who asserts silence for free.

That asymmetry has a recorded outcome. T-0274 is in this ADR as the case that proves the old axis
under-routes, and its geometry was exactly this: the author self-classified
(`T-0274-…md:133`), and the reviewer — holding a diff, not the catalog's history — did not catch that
seven shipped `.models.ts` resolvers were being obliged. The floor keeps that geometry and moves it to
a new axis. T-0473 (CH-2) is the same geometry running again, two sprints later.

**Repair.** Symmetry, at the cost of one line: **an author claiming the floor records the catalog
search** — the file(s) and the term — in the ticket's `## Review`, the same shape check 2 already
demands for code. Check 3 becomes *"read the author's search"* (verifiable) instead of *"reconstruct
the catalog's history"* (not). And the **default on missing evidence is `route`, not `inline`**: a
floor claimed with no search is not claimed. The floor should be opt-in with evidence, because the
party holding the information is the author.

---

### CH-4 — Both limbs of the floor look backwards, so it discriminates in proportion to how much code already exists — and it is weakest exactly where "the one way" is being written fastest

Test 1 asks about **shipped** call sites. The floor's operative case is "no shipped call site becomes a
deviation". Both are retrospective. But a first statement of a canonical form binds every **future**
call site, and the floor prices that at zero.

`conventions.md:125-127` — the rule ADR-0033 refines, not reverses — exists for the forward cost:
*"anything that changes 'the one way to do X' across the codebase … **don't unilaterally redefine the
standard**."* On a stack where the code does not exist yet, whoever ships first **is** the standard,
and the floor routes them inline every time. That is not a hypothetical stack:
`agents/knowledge/patterns-mobile.md` is 1093+ lines carrying **22** "the ONE way" lines plus one "The
ONE sanctioned way" (`:191`) and ~20 "Deviations a reviewer rejects" lists (ADR-0032 §Context, lead-
verified) — written largely ahead of, or alongside, the iOS port it governs.

**And the composition with ADR-0032 is left unstated, which AC6 does not permit.** Work it out: an
entry qualifies for the floor **only if test 1 did not fire** — i.e. its baseline is **zero by
construction**. Zero baseline is precisely ADR-0032 D2 condition (b). So **every floor-qualifying entry
satisfies the harder half of the T1-CI test already**, and if the rule is mechanically expressible on
its stack, `T1-CI` is **mandatory** — inside a ticket with no Architect involvement. Neither ADR says
this anywhere, and it is the single most consequential thing about the inline lane.

It also has a trap the tree already fell into. On the stacks where the natural mechanizer cannot fail a
build, a `T1-CI` label would be a lie:

- `check-consistency.mjs` appears in **zero** files under `.github/` (I re-verified: the grep returns
  nothing). It cannot set a blocking exit code on any stack.
- `frontend-ci.yml:72-74` runs lint with **`continue-on-error: true`** — an ESLint rule attached there
  can never red a build either.

Someone has already got this right, and it is the model the ADR should cite:
`patterns-frontend.md:462-465` labels its `no-restricted-syntax` rule
**`T2-ADVISORY`, "because `frontend-ci.yml` runs lint with `continue-on-error: true`; promotes to
`T1-CI` with the rest of the lint baseline."** That is what an honest inline entry looks like. ADR-0033
should say so, or the inline lane becomes the lane where laws are made without gates *and* without
tiers.

---

### CH-5 — The retro-validation is **not fitted**, but four rows is thin and I found three more. Two of them the floor routes against history — reported honestly, because they land softer than T-0471 hoped

T-0471 AC4 demands a **case** or a plain statement of what was searched. Both, then.

**What I searched.** Every `agents/backlog/tickets/*.md` mentioning `patterns-{mobile,frontend,backend}.md`
(60 files); every backlog file matching `clarification to an existing rule|not a new archetype|new
canonical archetype|Harvest good patterns` (returns T-0274 `:133`, ADR-0021 `:98`, the two ADRs, and
T-0473); and the two tickets whose titles are literally architect ratifications of a catalog harvest —
**T-0397** and **T-0379**. Those two are the population this question wants: real, historical, dev-
authored catalog rows that the process actually routed to the Architect.

**Case A — T-0397 row 1 (full-bleed header-to-top idiom).** The floor routes it **inline**; history
routed it to the **Architect**.
- Test 1: does not fire. The architect's own ratification (`T-0397-…md:66-80`) verified **all three**
  call sites already matched — `ProfileTab.swift:23-52`, `SubscribePlusScreen.swift:34-49`,
  `ProfileHubContent.swift:22-35`. Baseline zero.
- Test 2 under the amended floor: no catalog sentence governed full-bleed header layout at any level of
  generality. Not a narrowing.
- Test 3: an iOS ticket, verified on-simulator. Does not fire.
- ⇒ **inline.** What actually happened: **three** fix-round-6 reviewers flagged it (`T-0397` front
  matter, `source:`), the PM filed an architect ticket, and the round **changed the artifact** — it
  folded in the fix-round-8 `.animation(nil, value: topInset)` settle pin *"(the row predated it)"* and
  corrected the verified call-site count from two to three.

**Case B — T-0379 scope addition (`format: date` ridden as plain `Date` is a defect).** Same shape.
Test 1 does not fire — both generator configs already carry `useCustomDateWithoutTime: true`
(`openapi-generator-config.{partner,customer}.yaml:21`, verified in the ratification). Nothing in the
catalog governed date-only wire decoding. ⇒ **inline** under the floor; routed to the Architect in
fact, on the explicit ground that it *"defines the one way for date-only wire on iOS"*
(`T-0379-…md:126-128`).

**Case C — T-0397 row 2 (a short entry sheet must NOT use a fixed `.medium` detent).** The floor gets
this **right**, and the contrast is the finding. The withdrawn form is nameable:
`patterns-mobile.md:1230` says *"The code dialogs are native `.sheet`+`.presentationDetents([.medium])`"*
— the promo/referral code dialogs, exactly the sheets row 2 forbids `.medium` on. Test 2 fires ⇒
**Architect** ✅.

**So one harvest ticket splits.** Same author, same evidence quality, same reviewers, ratified in the
same sitting — and the floor sends row 1 inline and row 2 to the Architect, on nothing but whether the
catalog happened to have *described* the old form somewhere. That is a real property of the rule, and
the ADR should own it rather than discover it later.

**Now the honest part, which is a concession.** Neither Case A nor Case B is a case where the floor
routes something **dangerous** inline. In A the architect's ruling explicitly asked *"should this be an
ADR, not a catalog row?"* and answered **no** — *"no trade-off survives … a row that names the one
working shape plus its defect forms is exactly what the catalog is for"* (`T-0397-…md:70-72`). In B the
row was **"RATIFIED as-is"**. So the floor's *substantive* answer matched the outcome in both, and the
rounds it would have saved cost real tokens. What the floor loses in A is a **verification round that
added content the developer's row did not have** — and it loses it silently, because after the floor a
reviewer has no ground on which to route: nothing was withdrawn.

**I could not find a case where the floor routes inline something that a later reader would call
plainly wrong.** That is a pass of AC4 by its own terms and it is a genuine result: it means the floor
is **correct in substance and thin in safety margin**, not that it is broken. Which is why CH-5 asks
for a table extension and an honest §Consequences line, and **not** for the floor to be replaced.

**Bonus, found while verifying Case C, and it belongs to nobody's ticket:** the catalog currently
carries **both** forms. `patterns-mobile.md:985-990` is the T-0397-ratified rule (*"must NOT use a fixed
`.medium` detent"*, with the architect signature), and `:1230` still says those same code dialogs
**are** `.presentationDetents([.medium])`. The narrowing was applied to the new sentence and never
retracted from the old one. Today, a reviewer running check 3 on a `.medium` edit finds a live
contradiction; a developer reading `:1230` finds permission. Filed below.

---

### CH-6 — The tests have no limb for "this may carry a trade-off, so it may be an ADR" — filed as a SEPARATE finding, not folded into this round (AC2)

The actual ground three reviewers used on T-0397 was *"new 'one way to do X' catalog rows need
Architect sign-off"*, and the architect's ruling spent its first paragraph on *"does this carry a real
trade-off — should it be an ADR rather than a catalog row?"* (`T-0397-…md:68-72`). ADR-0033's three
tests cannot ask that: they ask about obligation, latitude and stack provenance. A test 4 —
*"does the entry name two or more competing forms and price them? then it is a trade-off, and
`adr/README.md` says a trade-off is an ADR"* — is arguably the missing limb.

**I am not folding this into the round.** T-0471 AC2 restricts it to the floor, and adding a fourth
test is a new decision, not a repair of this one. It is filed for the PM.

---

## What survived — the parts of the floor I attacked and could not break

Silence is not assent, so: named, not omitted.

1. **The floor is necessary.** I re-read `conventions.md:132` — *"a pattern earns a catalog entry when
   it would make future changes cheaper or the codebase more consistent"* — and C5's reductio holds
   exactly as stated: without a floor, every entry that earns its place forbids the less-consistent
   alternative, test 2 fires on all of them, and the inline lane dies. **Some** floor is mandatory. The
   attack is on its wording, never on its existence.
2. **The floor's direction is right.** *Adding* a form obliges nobody today; *withdrawing* one converts
   shipped code. That is the axis that predicts cost, and it is a strict improvement on "is this new or
   is this a clarification?", which measures novelty relative to the text. I could not construct a
   better axis.
3. **The semantic-not-lexical rule (challenge C4's fix) holds.** I looked for a wording that launders a
   narrowing past the amended floor and could not build one: once the trigger is "a sentence already
   governs this subject", rephrasing the *new* entry changes nothing about the *old* sentence.
4. **Retro row 2 (T-0441) is real, not fitted.** I re-derived it from the file rather than the ADR: the
   nearest governing sentence (`patterns-mobile.md:167-175`) is about a different subject, so the floor
   does genuine work there — it is what keeps T-0441 inline, and the unfloored test 2 would have routed
   it wrongly, exactly as the ADR claims.
5. **The `(test 1 did not fire)` conjunct is redundant but harmless.** I checked whether removing it
   changes any routing under the ordered tests: it does not. Keep it as a reader's aid; it is not a
   defect, only not a safeguard.

---

## Findings filed for the PM (NOT part of this round's verdict)

| # | Finding | Why it is not in the round |
|---|---|---|
| **F1** | **ADR-0032's "Number note" (`:23-25`) is false.** It says 0031 *"exists only in T-0439's worktree and has not reached `master`"*; `agents/backlog/adr/0031-nswag-regen-drift-is-guarded-at-regen-time.md` is on disk. ADR-0032 is `accepted`, so `adr/README.md:16-26` requires a **signed erratum**, not an in-body edit. | Editing an accepted ADR from inside another ticket is the process violation T-0379 ratified the erratum lane to prevent. |
| **F2** | **ADR-0032's Block A was never applied and its label has zero instances in its target file.** `patterns-mobile.md` contains **0** occurrences of `**Enforced by:**` (8 repo-wide in `agents/knowledge/`); the shipped T-0451 entry at `:292-304` carries neither the enforcer line nor the residual-scope sentence Block A specifies. FT-4 has nothing to build on. | ADR-0032's follow-ups are its own business (T-0471 §Out of scope). Shares a fix with **FT-11**. |
| **F3** | **`patterns-mobile.md:265-276` (T-0473) constrains call sites with no enforcer + tier**, contrary to accepted ADR-0032 D2, and forbids a form (*"not a whole-file `contains`"*) with **no test-1 sweep on record**. The tree holds **14** guard tests that read source as a fixture (`ProfileAvatarBindingTests`, `OrderDetailSummaryBindingTests`, `OrderStatusPillPlacementTest.kt`, `BrandIconCatalogTest.kt`, `ConsentCatalogTest.kt`, `FixedWhiteContrastTests`, …); **I did not audit each for the withdrawn whole-file shape**, so whether test 1 fires on that entry is **unresolved** — which is the finding. | T-0274's precedent: a mis-routed edit is **recorded, not re-opened**. Re-read it after FT-11 lands. |
| **F4** | **CH-6** — the missing "carries a trade-off ⇒ ADR" limb. | New decision, out of AC2 scope. |
| **F5** | **`patterns-mobile.md:1230` still grants what `:985-990` withdrew** (`.presentationDetents([.medium])` for the promo/referral code dialogs). A one-sentence retraction in the `patterns-mobile.md` lane. | T-0471 §Out of scope: this round writes no catalog entry. |

---

## Summary for the lead

| # | Claim | Ask |
|---|---|---|
| **CH-1** | The floor's only operative clause is undecidable, and it splits the ADR's own row 3 | **BLOCKING** — redefine the trigger on the *sentence*, define silence |
| **CH-2** | The floor's enforcer is a charter step that teaches the rule the floor replaces; already routed around once (T-0473), and ADR-0032's label has 0 instances in its target file | **BLOCKING** — Block D + a named check + ADR-0033 labels itself; FT-8 sequenced behind it |
| **CH-3** | Burden of proof inverted vs test 1; T-0274's geometry preserved | symmetric catalog sweep; default = route |
| **CH-4** | Purely retrospective; weakest on the newest stack; the ADR-0032 composition (floor ⇒ zero baseline ⇒ T1-CI owed) is unstated, and `T1-CI` is unavailable via `check-consistency.mjs` (0 workflows) or ESLint (`continue-on-error: true`) | state the composition; cite `patterns-frontend.md:462-465` as the model |
| **CH-5** | Not fitted, but 4 rows is thin: 3 more cases found, 2 route against history, **no plainly-wrong case exists** | extend the table to 7 rows; state the residual risk plainly |
| **CH-6** | No "trade-off ⇒ ADR" limb | **separate finding (F4)** |
