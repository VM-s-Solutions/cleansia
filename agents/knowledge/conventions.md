# Conventions & Quality Bars

> ## ⚠️ FOUR CI GATES WERE REMOVED — 2026-08-11, owner instruction. Any `T1-CI` token naming one of them is now FALSE.
>
> Deleted: `catalog-claims.yml`, `module-boundaries.yml`, `offerability-parity.yml`,
> `nx-project-registration.yml`. **The checker scripts survive** under `agents/tools/` and still run on
> demand — what is gone is the thing that made them able to fail a build.
>
> §*"The price of a law"* below is explicit that **a tier naming a mechanism that cannot fail a build is
> `T2-ADVISORY`**, not `T1-CI`. Every entry citing one of those four workflows therefore overstates its
> enforcement, and those rules are now **conventions a human upholds**, not gates. The tokens are not
> individually rewritten — that would assert a tier nobody has re-decided — so this banner is the
> correction and it applies to all of them at once.
>
> **What the four were catching, so the trade is visible rather than implied:** citation and ADR-status
> rot in `agents/**`; customer→partner module-boundary regressions; offerability-status drift between
> the C# source of truth and eight client literals across three languages; and libraries becoming
> invisible to Nx. Each had a measured, non-zero baseline before its gate existed.
>
> **Retires when:** any workflow file names one of the four checkers again.


The shared "what clean means here" reference, across all stacks. Every developer reads this plus
their stack catalog. The Reviewer enforces it. Where this references concrete .NET / Angular /
Compose patterns, the per-stack catalogs (`patterns-backend.md`, `patterns-frontend.md`,
`patterns-mobile.md`) hold the code samples.

The canonical *architecture* description lives in [`../../docs/architecture/`](../../docs/architecture/)
(`overview.md`, `backend.md`, `frontend.md`, `database.md`, `fiscal-compliance.md`,
`infrastructure.md`, `push-notifications.md`). That is the source of truth for *how the system is
built*; this file is the source of truth for *how we write code in it*. When they conflict, fix one
and note it — they must not drift.

---

## Reuse the real types — do not reinvent (the prime directive)

This codebase has established base types, shared components, and idioms. **Before writing anything,
open the relevant `knowledge/patterns-*.md` and the nearest existing feature of the same kind, and
reuse the exact types named there.** Inventing a parallel base class, result type, table wrapper,
HTTP call, or state container when one already exists is the single most-rejected mistake — the
Reviewer treats it as a hard fail.

- **Backend:** `BusinessResult`/`Error`/`BusinessErrorMessage`, `ICommand`/`IQuery`/handlers,
  `DataRangeRequest`/`PagedData<T>` + `<Entity>Specification`/`<Entity>Sort`, the real
  `*ApiController` + `HandleResult` + `Policy.CanXxx`, `BaseRepository<TEntity>`,
  `IUserSessionProvider`. No new result type, no `ErrorType` enum, no hand-rolled paging.
- **Frontend:** `UnsubscribeControlDirective`, signal state, the generated client wrapper,
  `cleansia-*` components + `cleansia-table`/`TableColumn`/`TableAction`, `SnackbarService`,
  `*cleansiaPermission`, `Policy`. No hand-rolled HTTP, no raw HTML controls, no edited generated files.
- **Mobile:** `@HiltViewModel` + sealed `*UiState`/`ActionState`, `StateFlow`/`SharedFlow`, the
  `@Singleton` repo + `SessionScopedCache` + `networkCall` + `ApiErrorParser` + `SnackbarController`,
  `cz.cleansia.core.ui.components.*` + `CleansiaTheme`. No duplicated `:core` components.

If a genuinely new abstraction is needed, that's an **Architect** decision (an ADR), not an ad-hoc
invention inside a feature. Raise it via the ticket; don't fork the pattern silently.

## One way to do each thing — see `consistency.md`

Reuse isn't only about base types; it's about doing **the same operation the same way every time**.
Before writing a paged query, a create/update/delete command, a list page, a form, or a mobile
ViewModel/Screen/Repository, read the canonical form for that archetype in
[`consistency.md`](./consistency.md) and match it. Doing the same operation a *different* way than the
rest of the codebase — even if it "works" — is the spaghetti we are actively removing before PROD, and
the Reviewer treats a new deviation as a hard fail. Known existing deviations are tracked in
[`../backlog/audits/consistency-violations.md`](../backlog/audits/consistency-violations.md).

## Global rules

- **No hardcoded user-facing strings.** Backend → `BusinessErrorMessage` codes (dot notation,
  e.g. `order.invalid_status`). Frontend → `TranslatePipe` keys. Android/iOS → string resources.
  Every backend error key has a matching frontend `errors.*` key in **all 5 locales**
  (en, cs, sk, uk, ru).
- **No `any` (TS) / no `dynamic` (C#).** Use real types, enums, and generics.
- **No magic numbers/strings.** Constants live in a `Policy` class, an enum, or a theme token —
  never inline. Lead-times, surcharge rates, discounts, window durations, max lengths, status codes
  all come from a named home.
- **No inline templates or styles** in Angular; **no XML layouts** in Android (Compose only).
- **CancellationToken propagation** through every async IO path (backend).
- **No dead code.** Delete unreferenced methods/classes; for DB columns, never delete in code —
  flag a migration `manual_step`.
- **Comment discipline — see the dedicated section below.** The default is *no comment*; the code is
  the documentation.

## File length & method length (backend, as a smell test, not a hard cap)

- Handler file < ~200 lines; `Handle()` method < ~80 lines.
- Service file < ~400 lines; service method < ~100 lines.
- Controller file < ~250 lines.
- Validators: any length (declarative).

Over the line usually means too many responsibilities — extract into a domain service, not a bigger
handler.

## Duplication

Extract when the *same* 3+ lines appear in 3+ places **and** genuinely mean the same thing.
Premature unification is worse than duplication: two methods that look the same but must diverge
later become a silent bug when "deduplicated". Confirm intent before merging call sites.

## Comments — write almost none

**The default is no comment. The code is the documentation.** Self-documenting code — clear names,
small methods, real types — replaces the vast majority of comments. A reviewer who sees a comment on
every few lines treats it as a smell, not as diligence.

**Only comment genuinely non-obvious *critical* logic** — the *why* a reader cannot recover from the
code itself:
- a non-obvious ordering/atomicity requirement, a race the code is defending against, or a
  correctness subtlety (e.g. "this UPDATE is conditional so two callers can't both pass");
- a deliberate, surprising deviation from the obvious approach, with the reason;
- a domain/legal/fiscal rule the code encodes but doesn't state (e.g. a rounding or sequence rule).

**Never write:**
- **WHAT comments** — `// update the user`, `// loop over orders`, `// return the result`. If a line
  needs a label to be understood, rename the variable/method instead.
- **Restating the signature** — `// takes an id and returns the user`.
- **Ticket / review / issue numbers in code** — no `// T-0123`, `// PR review #4`, `// AC2`,
  `// TODO(JIRA-x)`, `// fix from sprint 3`. These rot into dangling pointers the moment the tracker
  moves; a future reader cannot resolve them. The *reason* belongs in the comment; the *traceability*
  belongs in the commit message and the ticket, never in a source comment. (A bare `// TODO:` with a
  concrete next action and no tracker id is acceptable only as a short-lived marker.)
- **Section-divider noise** — `// ─── helpers ───`, banners, ASCII art, decorative rules.
- **Commented-out code** — delete it; git remembers.

When you fix or change a line, **delete any now-stale comment on it** rather than leaving it. A
comment that no longer matches the code is worse than none.

> Rationale: comments are unversioned-against-the-code duplication. Every comment is a second thing
> that must be kept true; most add risk (drift) without adding understanding. Spend the effort on the
> name instead.

## Harvest good patterns back into the catalog

The knowledge catalog (`patterns-*.md`, `consistency.md`) is a **living** document, not a fixed
input. When, while building, you discover a genuinely better or more-consistent way to do a recurring
thing — a cleaner idiom, a reusable helper, a safer default that the rest of the codebase would
benefit from — **don't keep it to yourself in one feature:**

1. **Apply it** in the change you're making.
2. **Decide who ratifies it** — run the routing test below. Most edits are yours to make; some are not.
   - **Nothing fires → it is yours.** Edit the relevant `patterns-*.md` / `consistency.md` entry in the
     same change so it becomes the canonical form everyone follows next time, and note it in the
     ticket's `## Review` so the Reviewer sanity-checks it. Price it: an entry that constrains call
     sites names its enforcer and declares a tier (see "The price of a law").
   - **Something fires → it is not yours to ratify.** Raise it via the ticket and let the **Architect**
     rule (it may warrant an ADR and a canonicalization ticket to migrate the existing call sites). The
     content may well be right, and you may still be the one who ends up writing it — what you may not
     do is ratify your own standard-change. Don't write it into the catalog inline.
3. If the new pattern supersedes an old one, mark the old form as a deviation in
   `consistency.md` (and file the canonicalization follow-up) so the codebase converges instead of
   carrying both.

The bar: a pattern earns a catalog entry when it would make **future** changes cheaper or the
codebase **more consistent**, not because it's merely a preference. Reviewer and Architect are the
guardrails against catalog bloat — the same "earns its place" test as any abstraction.

### Who ratifies a catalog edit — the routing test (ADR-0033)

Apply in order. The **first** one that fires routes the edit to the **Architect** — the content may be
right; what you may not do is ratify it for yourself. If none fires, edit inline.

1. **Does the edit put code that exists today in violation?** If any current call site becomes a
   deviation it wasn't before, it needs a `consistency.md` deviation entry and a canonicalization
   ticket — neither of which a developer or a reviewer can file for themselves. **Name the sweep you
   ran** (a grep, a file list) in `## Review`; "no existing violations" with no sweep is not an answer.
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

   > ⚠️ **"Governs" is not defined yet — know that before you lean on this test.** ADR-0033 defines what
   > *silence* is; it never defines what *governs* is. So on a hard case — a general sentence that may or
   > may not reach your subject — the verdict rests on how you and your reviewer each paraphrase that
   > sentence, and two careful readers can differ (worked both ways on real hunks: `patterns-mobile.md:990`
   > vs T-0349 is determinate and fires; `:520-522` vs T-0473 is not). The first repair — *"name one
   > concrete artifact both sentences reach and rule differently"* — was drafted, challenged and
   > **rejected** on 2026-08-05 (`agents/archive/2026-08/adr-deliberation/drafts/NNNN-what-makes-a-catalog-sentence-govern.md`,
   > `rejected`); a second author round is owed on **T-0553**. Until it lands, quote the candidate
   > sentence and record both readings in `## Review` rather than settling it by whoever quotes first.
3. **Does it make a prescriptive claim about a stack this ticket did not build and run?** A rule for a
   stack you never executed is not yours to declare. A **descriptive** cross-stack note is fine from any
   ticket — see "Cross-stack claims" below.
4. Otherwise → **inline.** This covers both a clarification inside an existing rule's scope *and* the
   first statement of a canonical form where nothing governed the subject.

> **This is a deliberate reversal, and it is the one thing to know if you remember this page from
> before.** The old wording sent *"a new canonical archetype"* to the Architect on that ground alone.
> It no longer does: a first statement that obliges no shipped call site and withdraws no governing
> sentence is **yours to write**, in the moment you hold the context — which is the best moment there
> is to write it. What changed is the price, not the permission. *(ADR-0033's header says it "does not
> reverse" this rule; that claim is false as to this limb, and the ADR carries a dated correction
> saying so. The page you are reading is the operative one.)*

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

### A claim about the tree cites the tree — never another artifact

The generalization of the rule above, and it is the one that keeps being broken. **A claim about the
state of the code — a column exists, a path is down, a call site is the only one, a migration is
pending — is cited at `file:line` in the tree, read at the moment of writing.** A ticket status log, a
sprint status section, a living decision page and a prior ADR are all *records of a past reading*.
They were true when written; nothing updates them when the tree moves, and nothing fails when they
drift. Quoting one in the present tense converts somebody's stale sentence into your artifact's load-
bearing fact.

**The discriminator:** does the sentence assert what the tree *is*, or what somebody *decided*? Cite
artifacts for **decisions, rulings and rationale** — that is what they are for, and an owner ruling or
an accepted ADR is authoritative wherever it is quoted. Cite the **tree** for **state**. A ticket's
*"we chose the required-parameter form"* is quotable; its *"the invoice path is down until the
migration is regenerated"* is not — re-open the migration.

**Cost of getting it wrong, measured:** three instances in one sprint — a living decision page, a
sprint status section, and an ADR that took *"the invoice PDF path is down at HEAD"* from a ticket
status log four days stale and hung four sequencing statements plus its only owner-only-migration cost
mitigation on it. In each case the artifact's *conclusion* survived and its *plan* did not, which is
the expensive half.

**Enforced by:** the panel lead's adjudication — `process/deliberation.md` **step 5**, which re-derives
every blocking finding from the tree rather than from either document, and records the divergence when
one appears — **T3-HUMAN**. *(Not mechanizable: "is this citation a code-state claim or a decision
claim?" needs a reader. A checker cannot tell `T-0522:203` quoted for a ruling from the same line
quoted for a column.)*

### A claim about the tree carries its own retirement condition

**The rule above is not this rule, and its enforcer cannot reach this one.** Measured 2026-08-09: six
catalog artifacts asserted the opposite of the tree in the directory every developer agent reads
first. **All six cited the tree, correctly, at the moment of writing.** They were not mis-citations —
one role card's *"NOT YET BUILT, no ticket is cut"* was true for **2 h 11 m** before `d410f002` shipped
the whole thing; two status banners were true until the ADRs they named were accepted the same day; one
`file:line` citation was true until an **unrelated** refactor shortened the file it pointed at. So the
defect is not the act of citing. It is **decay**, and decay has no authoring event to gate.

That is why `deliberation.md` step 5 is the right enforcer for the rule above and is **structurally
incapable** of covering this one: a lead adjudicates a deliberation, and none of the six arose in one —
a card written at acceptance time, an enumerated count, two status banners, a rotted citation, and a
process page. **Widening step 5 to "the lead also re-reads the catalog" would be a rule whose enforcer
is *be careful*, which is precisely what these six already were.** The class is also known: the
`ExpressWaiverResolver` card has carried a **hand-written** "this banner is stale" correction since
2026-08-05, and the class recurred three more times on one branch anyway — including on a page someone
edited **without noticing the sentence above their own edit**.

So the repair is not more diligence. It is to **write the claim in a form that decays loudly** — a form
a machine can evaluate from in-repo text alone, with no type graph and no build.

**Three forms, and they are obligations on the writer:**

1. **A status claim about an ADR quotes that ADR's own status token, and names it as the retirement
   condition.** Not *"ADR-00NN is proposed"* — write *"**ADR-00NN is `accepted`** (`<adr-file>:3`).
   **Retires when:** that status line stops reading `accepted`."* Both sides are then greppable text in
   one repo and a checker can diff them. *(Would have caught: the `MembershipBenefitUsage` card's
   PROPOSED banner over an `accepted` ADR-0035; `patterns-backend.md`'s *"ADR-0039 is `proposed`"* over
   an `accepted` ADR-0039; the `ExpressWaiverResolver` precedent.)*
2. **A "not yet built / not shipped / no ticket yet" claim names the PATH whose existence retires it.**
   *"**Retires when:** `src/…/Foo.cs` exists."* A checker stats it. Writing a role card **before** the
   code is legitimate and often the point — it is what the implementer builds against — so the rule is
   not *"don't write it early"*; it is *"write the trigger that kills the banner"*. *(Would have caught
   the payout-allocator card two hours after it was written.)*
3. **A `file:line` citation must resolve** — the file exists, and it has at least that many lines. A
   checker asserts both. It cannot assert that the cited lines *say* what the entry claims; that stays
   a reader's job, and an entry may not pretend otherwise. *(Would have caught
   "`PromoCodeRedemptionRepository.cs:99-109`" cited from a **65-line** file — the worst variety,
   because the invariant was still true and a reader who checks the citation concludes it is dead.
   That range is **quoted, not asserted**: an exhibit of a dead citation rides inside the `*"…"*`
   convention, which the checker's quoted-span mask skips — write it bare and the example becomes an
   instance of the rule it illustrates.)*

**And one shape rule: never enumerate a COUNT of tree instances — write a roster with a membership
test.** *"There are exactly two documented exceptions"* was wrong twice, and a wrong number is invisible:
it carries no evidence for the reader to check and nothing fails when the tree gains a third. A roster
is different in kind — every entry is falsifiable by opening one file, a missing entry is discoverable by
grep, and the **test** (which is normative) keeps deciding the next case even while the **roster**
(which is only descriptive) is stale. `consistency.md` §"Post-commit ordering" limb (a) is the worked
example. Deviating form: **any sentence of the shape "there are exactly N …" about code**.

**Enforced by:** `agents/tools/check-catalog-claims.mjs` + `.github/workflows/catalog-claims.yml`
(T-0574) — **`T1-CI`**, blocking, both halves. It shipped `T2-ADVISORY` and was promoted on
`docs/sprint-15-decisions` the moment its own stated condition was met: a full-corpus run reporting
`FAILED: C1 0 · C2 0 · C3 0`. The arc is the lesson — **16** violations before T-0574 changed
anything, **15** once it retired `enforcement.md`'s own *"Specified, NOT yet built"* banner about this
checker, then a sweep that closed the C2 banners and the non-`roles/` citations, and finally the six
that needed a ruling rather than an edit: five `roles/*` citations (a regenerated migration filename,
an extracted `ResolveTimeZone`, two exhibits that *quote* their dead citation on purpose) and the C1
disagreement over ADR-0022, resolved by amending the **ADR's** status line, because the card matched
the tree and the header did not.

What did **not** get promoted with it, deliberately: **C3B**, the advisory heuristic asking whether
the cited *subject* still appears on the cited lines. It reports ~150 misses and fails nothing. It
cannot decide that the lines *say* what the entry claims — that stays the reader's — and putting a
heuristic in the blocking path is how a gate teaches everyone to route around it.

The gate is not aspirational, and all three forms above are decidable from in-repo text with no
compiler: parse `agents/knowledge/**/*.md` +
`agents/process/**/*.md` for (1) an ADR id adjacent to a quoted status token → read that ADR's
`- **Status:**` line → fail on disagreement; (2) a `Retires when: <path> exists` marker → `fs.existsSync`
→ fail if it exists; (3) every `` `Path.ext:N` `` / `:N-M` citation → file exists **and** has ≥ M lines
→ fail otherwise. It takes the **cross-stack** shape ADR-0032 §D and `enforcement.md` prescribe — a
dependency-free Node script **outside the Nx workspace with its own repo-root workflow**, the
`check-available-status-parity.mjs` / `offerability-parity.yml` mold — because **no stack's CI watches
`agents/`**, so no existing workflow can host it. Like that check, it must fail loudly when its corpus
is empty or an anchor matches nothing; a green run must mean it *read* the pages.

**Why the blocking tier is still `(gate pending:)` and not `T1-CI`:** `enforcement.md`'s zero-baseline
rule. Six instances were fixed on 2026-08-09; the remaining role cards and catalog pages were not
swept then. T-0574 **measured** that baseline instead of guessing at it, and the shape was worth knowing
before the sweep was planned: **7 were C2-FORM** (a bold "not yet built" banner with no `Retires when:`
condition — a one-line mechanical fix per site), **8 were C3** (rotted citations, including two into a
migration filename that no longer exists and one into a file that is gone), and **1 was C1**. The
C2-FORM class is now empty and the C3 class survives only inside `docs/domain/roles/`; the C1 is
untouched by design. Two of
T-0574's four owed items remain: the **sweep**, and **one line extending
reviewer-check 5 "Catalog-edit routing"** (`.claude/agents/reviewer.md`) to re-read the banners and
citations of the *whole file* a hunk touches — not just the hunk — because the sixth instance was a
sentence that survived a pass over its own page.

**What enforces this today, exactly:** the workflow runs on every change to `agents/**`, `src/**` and
`docs/**` — the cited trees, not just the citing ones, because a citation rots when the *cited* file
moves. Both halves are hard gates now. `--warn` still exists and still means *advisory about the
catalog's debt, never about whether the instrument ran* — a reach failure (an empty corpus, a broken
parser, a floor breached) exits 1 under it too — but the workflow no longer passes it, and reaching
for it to turn a red build green would reinstate exactly the debt this tool retired. What it cannot
decide — whether the cited lines *say* what the entry claims — stays the reader's, and the entry does
not pretend otherwise.

**Three alternatives were weighed and rejected** — do not re-derive them:
- **"A role card may not be written before the thing exists."** Rejected. The payout card was *useful*
  before the code was: it is what the implementer built against, and ADR-0046's panel produced it.
  Banning it pushes design intent out of the catalog into an ADR nobody re-reads. It is also
  unenforceable — nothing can tell a card written two hours early from one written two hours late. What
  survives from it is form 2: early authorship is safe **once it names its own trigger**.
- **A periodic sweep with a named owner.** Rejected as the primary answer: the *cadence* is the thing
  that decays, and nothing goes red when a sweep is skipped. The evidence is on the record — one card
  was hand-patched on 2026-08-05 and the class recurred three times on the next branch. Kept only as
  the fallback **if the checker cannot be built**, and then it needs a named owner and an **event**
  trigger (every ADR status transition), never a calendar.
- **"This is T3-HUMAN and the enforcer is the next reader."** Rejected for forms 1–3, which a machine
  decides. Kept, explicitly, for the residue: **whether the cited lines say what the entry claims** is
  not mechanizable and stays with the reader — which is exactly why the mechanical half must be
  automated, so the reader's attention is spent on the half only they can do.

### The price of a law — a constraining entry names its enforcer and declares its tier (ADR-0032)

An entry that constrains **call sites** — code other people write — states, inline:

```
**Enforced by:** <named enforcer> — <tier token>
```

The **named enforcer** is a SwiftLint rule id, a test file + test class, a `check-consistency.mjs`
rule id, or a **named** standing-checklist item (e.g. Gate-DP §G of `ios-app-review-checklist.md`).
The **tier token** is one of:

| Token | Means |
|---|---|
| `T1-CI` | fails a CI job on the offending change |
| `T2-ADVISORY` | runs on demand, reports, never sets the exit code (`check-consistency.mjs` today, on every stack) |
| `T3-HUMAN` | a **named** item in a standing checklist the Reviewer runs |
| `(gate pending: <ticket>)` | the gate is specified and ticketed, but a live violation blocks it (`enforcement.md`'s zero-baseline rule); promotes to `T1-CI` when the ticket lands |
| `(guidance — no gate)` | nobody enforces it; it is advice |

**`T1-CI` is required when — and only when — both hold:** (a) the rule is mechanically expressible on
that stack, **and** (b) its baseline on that stack is **zero**. If (a) fails, the tier is `T3-HUMAN`
with a named checklist item — ADR-0018's Gate-DP ("this screen's layout, flow and branding match the
Android screen") is unmechanizable *in principle* and is nonetheless a real, load-bearing law. If (b)
fails, the tier is `(gate pending: <ticket>)`. **Imperative framing does not buy a CI gate and does not
require one** — it buys nothing, because every constraining entry declares a tier whatever its wording.

- **`T3-HUMAN` needs a *named* checklist item.** "The reviewer will notice" is not T3; an unnamed human
  enforcer is `(guidance — no gate)`.
- **The named enforcer's assertion must cover the scope the sentence claims.** A closed-roster
  enforcer is legitimate, but say so in the entry ("gated on the two heroes; the rest are enumerated by
  `<ticket>`"). A hand-maintained roster does not catch the next instance, and the reader must see that
  boundary. *The failure this closes is real and was verified: an entry claiming "a second literal
  domain anywhere in the iOS tree is a defect" named a test that asserts **two sentences**.*
- **A guard test that walks the tree must fail when its corpus is empty or its anchor is missing**
  (`XCTUnwrap` the file read; assert the anchor count). A test that passes because the files were
  renamed away is not an enforcer.
- **An entry describing a shared component's own internals is not a law over call sites** and needs no
  enforcer — it needs accurate prose.
- **Per stack:** backend / frontend / Android — a unit/integration test in a CI job is `T1-CI`; a
  `check-consistency.mjs` rule is `T2-ADVISORY` until that stack's checker step is in its CI workflow.
  **iOS** — a SwiftLint `custom_rules` entry (`swiftlint lint --strict` blocks in `ios-ci.yml`) for a
  single-line token/literal ban, or an **XCTest guard** for anything relational, computed, or over
  non-Swift files (`.xcstrings`, plists), using the `#filePath` walk-out-of-the-package idiom of
  `ConsentCatalogTests`. A `custom_rule` may only claim the scope `.swiftlint.yml`'s `included:`
  actually lints. `check-consistency.mjs` **cannot read Swift at all**.

Why it matters: an entry that reads as settled while nothing is watching buys compliance today at the
cost of a reviewer who stops looking tomorrow. Naming the tier costs a line and makes the difference
visible.

## Naming (canonical)

| Thing | Backend (C#) | Frontend (Angular) | Mobile |
|---|---|---|---|
| Files | PascalCase | kebab-case | PascalCase (Kotlin/Swift) |
| Command | `CreateOrder.cs` (static class; inner record ends `Command`) | — | — |
| Query | `GetMyOrders.cs` (inner record ends `Query`) | — | — |
| DTO | `OrderDto` / `OrderListItemDto` / `OrderDetailDto` (record) | mirrored TS interface (generated) | data class / struct |
| Repo | `IOrderRepository` / `OrderRepository` | — | — |
| Service | `IOrderService` / `OrderService` | `OrderFacade` | `OrdersRepository` |
| Component/Screen | controller | `order-list.component.ts` | `OrdersScreen` / `OrdersView` |
| State | — | NgRx store / signals | `OrdersUiState` (StateFlow) / `@Published` |

> **Critical naming trap (backend):** the `UnitOfWorkPipelineBehavior` commits only when
> `request.GetType().Name.EndsWith("Command")`. Misname a command record (e.g. `.Request`) and the
> row is **silently not saved**. Always end command record types with `Command`.

### Deployment / infra naming (ADR-0015 + ADR-0017)

Azure resource names are **immutable** — getting the seam in at clean-slate is free; retrofitting it
later forces a recreate of live resources. So from day one:

- **A region token in every resource / RG / Key Vault name.** Names carry `weu` (the default region):
  `api-cleansia-<audience>-weu-dev`, `web-cleansia-customer-weu-dev`, `rg-cleansia-weu-dev`,
  `pg-cleansia-weu-dev`, `kv-cleansia-weu-dev`, … A name **without** a region token is a finding — it
  cannot coexist with a second region without a rename. A second region is then a new token *value*
  (`eus`, `neu`, …), not a rename of the live `weu` resources.
- **`region` is a Bicep parameter** (default `weu`), threaded through every module that emits a name or
  an Azure `location` — the modules are otherwise unchanged (no per-region forks). `env` (`dev`/`prod`)
  is the other axis; they compose. Param files are named **`<region>.<stage>.bicepparam`**
  (e.g. `weu.dev.bicepparam`).
- **GitHub Environments are `<stage>-<region>`** — `dev-weu` (auto on merge) / `prod-weu` (protected:
  required reviewers + manual approval), never bare `dev` / `prod`. A second region is `dev-eus` /
  `prod-eus`, additive. Each Environment carries its own per-region secrets + protection.
- **The deploy workflows fan out via a one-element `strategy.matrix.region: [weu]`** — a no-op today,
  but adding a region becomes a one-line list change, not a workflow restructure.

The litmus test: *would adding a second region rename/recreate any live `weu` resource, restructure the
workflow, or change the tenancy filter?* The answer must be **no** — only a new param value + a matrix
entry + an owner `HomeRegion` column-migration. Region is INFRA/config; tenancy stays the unchanged
app-level `TenantId` filter (see [`patterns-backend.md`](./patterns-backend.md)).

## Owner-only steps (agents flag, never run)

- **EF Core migrations** — flag `manual_step: ef-migration`, describe the schema delta.
- **NSwag client regeneration** — flag `manual_step: nswag-regen` whenever a backend DTO/endpoint
  changes; hold dependent frontend/mobile work until the owner confirms.
- **DB seed edits** (`sql-scripts/insert_seed_data.sql`) — seeds carry tenant/user ids matched to
  dev tooling; don't touch without explicit owner approval.
- **Real secrets** — never in `appsettings*.json`, **and never in Bicep, a `.bicepparam`, or a workflow
  YAML** (ADR-0015). Infra-as-code carries Key Vault secret **names** + reference URIs
  (`@Microsoft.KeyVault(SecretUri=...)`) only; the **values** are owner/CI-populated into Key Vault (the
  Postgres admin *password* and every KV-backed secret are supplied at deploy time via `getSecret` / a
  CI secret, never a literal). The Postgres admin *login name* and SKUs/regions are non-secret and may
  sit in the param file. A literal secret in any of these is a blocking finding. User-secrets on dev,
  env vars / Key Vault on prod.
- **Committing / pushing** — leave changes uncommitted unless the owner explicitly asks.

## Localization (5 languages)

Files: `apps/<app>/src/assets/i18n/{en,cs,sk,uk,ru}.json`. Adding a key means adding it to all
five. A wording decision (tone, formality) with business impact goes to the owner via
`questions/open.md` — the developer adds a placeholder and flags it; it is not invented silently.

## The "production-ready, long-term" bar

This is the bar for every change, because the platform is going live and will be costly to change:
- Solve the root cause, not a symptom. No "temporary" workarounds that become permanent.
- Prefer the design that makes the *next* change cheap (preserve seams, adapters, config-driven
  variation) over the one that's shortest today.
- If a change reveals a deeper structural problem, raise it as an audit finding / ticket rather than
  papering over it.
- "It works on the happy path" is not done. Empty, loading, error, and edge states are part of the
  work.
- **Develop test-first (TDD).** Write the failing test from the AC, make it pass minimally, refactor.
  Strict for pure logic (pricing, pay, validators, state machines); test the facade/ViewModel logic
  first for UI. After-the-fact tests on pure logic are rejected. Full rules: `testing.md`.
