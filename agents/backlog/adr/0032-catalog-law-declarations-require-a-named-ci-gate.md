# ADR-0032 — A catalog "the ONE way" declaration is priced: it must name a CI-blocking gate whose assertion covers the scope the sentence claims. On iOS that gate is a SwiftLint `custom_rule` or an XCTest guard — never the consistency walker

- **Status:** proposed   <!-- proposed | accepted | superseded | rejected -->
- **Date:** 2026-07-30
- **Supersedes:** —
- **Superseded by:** —
- **Refines:** `agents/knowledge/conventions.md` §"Harvest good patterns back into the catalog"
  (steps 2–3) — this ADR does not reverse that rule, it makes its routing test **decidable** and adds
  the price of a law. Consumes ADR-0018 (the iOS design-parity principle established that a standing
  iOS rule is *an ADR + a named reviewable artifact*, not folklore) and `process/enforcement.md`
  (the "deterministic beats diligent" principle and the E9 two-tier precedent).
- **Applies to:** cross-cutting (catalog governance) · ios (the tier mechanics are iOS-specific)
- **Number note:** **0031 is taken** by `0031-nswag-regen-drift-is-guarded-at-regen-time.md`, which
  exists only in T-0439's worktree and has not reached `master`. This ADR is 0032 deliberately; a
  reader on `master` today sees a gap at 0031 until T-0439 merges.
- **Ticket:** none — raised by the PM from the T-0451 reviewer's refusal to ratify a catalog edit
  inline. **Consumers:** two `patterns-mobile.md` hunks applied by their own lane holders (below),
  one `conventions.md` edit, one `enforcement.md` edit, and five ready-to-file tickets (§Follow-ups).

> **This ADR is `proposed`, not `accepted`.** I am its **author**. `process/deliberation.md` requires
> the author and the lead to be different instances, and this is a decision with a real trade-off
> space (four enforcement options, a cost claim about future catalog edits, a rule that binds every
> stack). I do not have standing to adjudicate my own draft. The `## Challenge` / `## Defense` /
> `## Verdict` sections below are **empty and carry a named challenge agenda** for the panel. Nothing
> in §"Exact catalog text" is applied until this ADR is `accepted` — with the one narrow interim
> exception stated in D7, which is labelled as interim and is reversible.

---

## Context

### What happened

T-0451 fixed a verified iOS dark-mode contrast defect (initials at **2.14:1** on a hardcoded-white
avatar disc; fixed by pinning the ink to the light-mode brand blue, 4.10:1). In the same change it
added a `patterns-mobile.md` entry titled **"Ink on a theme-INVARIANT surface — the ONE way
(T-0451)"**. The reviewer **did not dispute the content** — it is well-argued and the fix is verified
to the last digit — but refused to ratify it inline on the ground that declaring "the one way to do X"
is an architect call.

**The reviewer was right, and this is not a new rule — it is already written.**
`agents/knowledge/conventions.md:125-127` says, verbatim:

> a *new canonical archetype* or anything that changes "the one way to do X" across the codebase →
> this is an **Architect** call (it may warrant an ADR and a canonicalization ticket to migrate the
> existing call sites). Raise it via the ticket; don't unilaterally redefine the standard.

So the routing was correct by the standing rule. What is *missing* is (a) a test sharp enough that two
agents deriving it independently land in the same place, and (b) an answer to the question the refusal
exposes underneath.

### The gap underneath: iOS "the ONE way" rules are mechanically unenforced

Verified in this repo, today:

- **`check-consistency.mjs` has no Swift support at all.** `checkMobile` walks `[".kt"]` only
  (`agents/tools/check-consistency.mjs:387`), and `DEFAULTS.mobile` is `["src/cleansia_android"]`
  (`:502`). There is no `ios` stack key.
- **Worse than absent — it reports a false green.** With `--paths=src/cleansia_ios`, all three stack
  checkers run against that root looking for `.cs` / `.ts` / `.kt`, find nothing, and the script
  prints `consistency: OK (0 files scanned, …)` and **exits 0** (`:519-524`). A reviewer scoping the
  checker to an iOS diff gets a **pass**, not a "not covered". That is a defect in the tool
  independent of anything this ADR decides.
- **`.swiftlint.yml` has no `custom_rules:` block** (`src/cleansia_ios/.swiftlint.yml`, read in full)
  — even though `swiftlint lint --strict` **does** run in CI on a pinned 0.65.0
  (`.github/workflows/ios-ci.yml:149-151`), which means a custom rule at default `warning` severity
  would be a hard CI failure today, for the cost of a YAML block.
- Consequently **every** iOS "the ONE way" entry in `patterns-mobile.md` — `CleansiaDangerButton`
  (:233), `SnackbarPill` (:257), `CleansiaConsentCheckbox` + the `CleansiaWeb` no-literal-domain rule
  (:271) — is enforced by nothing but a human reading the diff.

### And one of those entries **overclaims its own enforcer** — verified

`patterns-mobile.md:266-270` states that *"a second literal `cleansia.cz` anywhere in the iOS tree
(Swift, string catalog or plist) is a defect, and `ConsentCatalogTests` pins the markup + the
no-literal-domain rule across both apps × five locales."*

`ConsentCatalogTests.testConsentSentencesCarryNoLiteralDomain`
(`src/cleansia_ios/CleansiaCore/Tests/CleansiaCoreTests/ConsentCatalogTests.swift:54-64`) iterates
**two catalog keys × five locales** and asserts the *consent sentence* carries no literal domain. It
says nothing about any other string, any Swift source, or any plist. **The rule is tree-wide; the
gate is two sentences.** A reader who trusts the entry believes the tree is covered. It is not.

This is the precise failure mode this ADR exists to stop: a catalog entry that *names* an enforcer,
where the enforcer asserts strictly less than the sentence claims. It is more dangerous than no
enforcer at all, because it stops the reviewer looking.

### The counter-evidence: the price of a real gate is affordable here

T-0451's own developer, inside an **S**-sized ticket, shipped
`CleansiaCore/Tests/CleansiaCoreTests/FixedWhiteContrastTests.swift` — which contains exactly the
guard this ADR would have mandated, and a stronger form than I would have asked for:

- `FixedWhiteContrastTests` pins the **ratio from the hexes** (not a `Color` → `UIColor` roundtrip,
  which is trait-dependent on the iOS-16 floor) and validates its own arithmetic against three WCAG
  reference pairs (`:23-27`) — so the test cannot pass by computing the wrong formula consistently.
- `AvatarDiscBindingTests` (`:35-171`) binds the **two call sites by source**, using the
  `ConsentCatalogTests` `#filePath`-walk-out-of-the-package idiom, brace-matching outward from
  `Text(initials)` to the enclosing block that also holds the `Circle(`, then asserting the `.fill(`
  and `.foregroundColor(` arguments are **both in a measured table** and that their ratio clears 3:1.
- It is **non-vacuous by construction**: a moved file throws on read, and a missing/duplicated
  `Text(initials)` anchor fails `XCTAssertEqual(anchors.count, 1)` (`:92`). It fails loudly on a
  restructure instead of silently finding nothing.
- Its table comment (`:41-44`) encodes the right semantics: *"A fill outside this table is not itself
  a defect — it means the pair is unmeasured, and the ratio below is void until someone measures it."*

Android already pins the same pair for the same reason (`customer-app/.../ProfileTab.kt:279-284`), so
T-0451 **closes a cross-platform divergence rather than opening one** — which is what ADR-0018 D1
requires of a branding decision.

**Conclusion from the evidence:** "unenforced stack" is not a reason to weaken the catalog. It is a
reason to make the catalog *state its enforcement honestly*, and to charge the author of a law the
price of a gate — because on this stack that price is demonstrably one file, written by the person
who already has the context, in an S-sized ticket.

---

## Decision

### D1 — Enforcement is a declared **tier**, not a binary, and the tier is part of the entry

Every catalog entry that constrains code carries one of three tiers. On iOS:

| Tier | Mechanism | Blocks? | Where |
|---|---|---|---|
| **T1-CI** | a SwiftLint `custom_rules` entry, **or** an XCTest guard in a scheme CI runs | **yes** — `swiftlint lint --strict` (`ios-ci.yml:151`) / the three test schemes (`:165-190`) | `.swiftlint.yml` / `CleansiaCoreTests` (or an app test target) |
| **T2-ADVISORY** | `check-consistency.mjs` | no — reviewer-run, non-blocking (`enforcement.md` §E9 precedent) | `agents/tools/check-consistency.mjs` |
| **T3-HUMAN** | the Reviewer reads the catalog and the diff | no | — |

The equivalent tiers on backend/frontend/Android already exist (`dotnet test`/`nx test` = T1-CI,
`check-consistency.mjs` = T2-ADVISORY); this ADR only names them so an entry can cite one.

### D2 — "The ONE way" is a **law**, and a law must name a **T1-CI** gate

An entry that **forbids an alternative form** a competent developer could otherwise reasonably choose
— the "the ONE way" / "never X" / "X is a defect" framing — may use that framing **only if the entry
names, inline, a T1-CI enforcer** (rule id, or test file + test class). No T1-CI enforcer → the entry
is written **descriptively** ("the canonical form is X, because Y") and carries the marker
**`(guidance — no gate)`**.

This is not pedantry about wording. A "the ONE way" sentence tells the next reader *the question is
settled and something is watching*. If nothing is watching, the sentence is worse than silence: it
buys compliance today at the cost of a reviewer who stops looking tomorrow.

**Scope of D2, tightly:** it bites on entries that constrain **call sites** — code other people
write. An entry that merely *describes a Core component's own internals* (e.g. how `SnackbarPill`
renders itself) is not a law over call sites and needs no gate; it needs accurate prose.

### D3 — The named gate's assertion must **cover the scope the sentence claims**

An entry may not claim tree-wide coverage from a gate that asserts a closed roster. Either:

- **narrow the sentence** to what the gate actually asserts, and state the residual scope explicitly
  ("gated on the two heroes; the remaining surfaces are enumerated by <ticket>"), **or**
- **widen the gate**.

A roster-based gate is legitimate — `enforcement.md` already specifies exactly this shape for S11
(`SessionScopedModuleTest` / `SessionScopedCacheRegistryTest`) — but the roster's boundary must be
visible in the entry, because the roster is hand-maintained and a new instance outside it is not
caught.

**Anti-vacuity requirement.** A guard test that walks the tree must **fail** when its corpus is
empty or its anchor is missing (the `XCTUnwrap`/`XCTAssertEqual(count, 1)` shape of
`ConsentCatalogTests:31` and `AvatarDiscBindingTests:92`). A "grep the tree and assert nothing
matches" test that passes when the files have been renamed away is not a gate.

### D4 — On iOS, the gate is SwiftLint `custom_rules` **or** an XCTest guard, chosen by rule *shape*

- **Single-line token/literal ban, scoped by path** → **SwiftLint `custom_rules`**. Cheapest real
  enforcement in the repo: it already runs, already blocks, `included`/`excluded` handles the
  "everywhere except the one file that owns it" shape, and `match_kinds` keeps comments out. The
  `CleansiaWeb` no-literal-domain rule is the textbook case for Swift sources.
- **Anything relational, cross-file, computed, or over non-Swift files** (`.xcstrings`, plists) →
  **an XCTest guard**, using the `#filePath`-walk-out-of-the-package idiom
  (`ConsentCatalogTests:16-22`, `AvatarDiscBindingTests:106-112`). SwiftLint regex cannot express "the
  fill three lines up pairs with this foreground colour at ≥ 3:1", and SwiftLint does not lint
  `.xcstrings` at all.

Most real laws need both: a `custom_rule` for the Swift half and a guard test for the rest.

### D5 — `check-consistency.mjs` is **not** the answer for iOS laws (but its false green is a bug)

Adding `.swift` to the walker is **rejected as the mechanism for D2** and **not adopted now** —
see §Alternatives for why. Two things are decided about the tool regardless:

1. **A `--paths=` scope that matches zero files must not print `OK`.** It must report
   `scope matched 0 files` and exit non-zero (or, at minimum, print a loud non-OK banner). Today a
   reviewer scoping to `src/cleansia_ios` gets a green pass for a stack the tool cannot read. Filed
   as FT-1; it is a tool bug, not a decision, and does not wait for this panel.
2. **`enforcement.md`'s "What's mechanical today" table gains an iOS row** stating plainly that
   `check-consistency.mjs` covers **no** Swift, so nobody infers coverage from the table's silence.

If a future ticket adds Swift to the walker, it lands at **T2-ADVISORY** and cannot discharge D2.

### D6 — The routing test (which catalog edits a developer may make inline)

Replaces "gap in the ruleset vs clarification to an existing pattern" as the operative test. Apply in
order; the **first** one that fires routes to the Architect:

1. **Does the edit put any code that exists today in violation?** (After this edit, is a current call
   site a deviation that wasn't one before?) → **Architect**. It implies a `consistency.md` deviation
   entry and a canonicalization ticket, neither of which a developer or reviewer can file for
   themselves.
2. **Does it forbid an alternative a competent developer could otherwise reasonably choose?**
   ("the ONE way", "never", "is a defect") → **Architect**. That is a law, and D2 prices it.
3. **Does it make a prescriptive claim about a stack this ticket did not build and run?** → **Architect**
   (see D7-X below).
4. Otherwise — it explains, exemplifies, or names a footgun **inside an existing rule's existing
   scope**, and no shipped code becomes a deviation → **inline**, flagged in the ticket's `## Review`
   for the Reviewer's sanity-check (unchanged from `conventions.md` step 2, first bullet).

**Why the reviewer's "gap vs clarification" axis is not durable.** It measures novelty *relative to
the text*, not cost *imposed on the codebase* — and those come apart in both directions. A gap can be
tiny and additive (a footgun no rule mentions, obliging nobody). A "clarification" can be enormous:
sharpening an existing rule's scope retroactively puts shipped call sites in violation. Two agents
applying "is this new or is this a clarification?" to the same edit will disagree, because the honest
answer to that question is usually "both".

**Retro-validation against the three real cases** (this is the evidence that the test is sound, not
just tidy):

| Case | Test 1 (obliges existing code?) | Test 2 (declares a law?) | Test 3 (foreign stack?) | Routes to | Actual ruling |
|---|---|---|---|---|---|
| **T-0446 / SEC-5** — nothing in S1–S11 covers bytes inside a stored artifact served by URL | **YES** — three shipped pipelines (avatar, order photos, dispute evidence) sanitize nothing | yes | no | **Architect + docs** | Architect + docs (T-0460) ✅ **matches** |
| **T-0441** — "assert the GENERATED command, not the app one" | no — it names existing practice (`BookingApiTest`, `UserRepositoryTest` are the cited models); no shipped call site becomes a deviation | no — it adds a test obligation, it does not ban a construct | its **Android** half, no | **inline** | inline ✅ **matches** |
| **T-0451** — "Ink on a theme-INVARIANT surface — the ONE way" | no (the two heroes are the ones being fixed) | **YES** | no | **Architect** | Architect ✅ **matches** |

It reproduces all three actual rulings, including the two the PM flagged as nearly inconsistent —
which is the validation the test needed.

**One honest divergence, recorded not re-opened.** T-0274's inline edit
(`agents/backlog/tickets/T-0274-fe-error-resolver-dedup.md:130-139`) said per-feature
`resolveXxxErrorKey` resolvers *"must delegate ... rather than re-implement the walk inline"* and was
self-classified as *"Small clarification to an existing rule, not a new archetype."* Its own next
bullet then lists **seven** shipped `.models.ts` resolvers that still inline the walk. Test 1 fires:
under D6 that was an Architect call, and the codebase is now carrying both forms with no
canonicalization ticket — the exact drift `conventions.md` step 3 warns about. **I am not re-opening
T-0274** (it shipped, and the edit was substantively right); I record it as the third data point that
the old axis under-routes. Whether to chase the seven call sites is the PM's scheduling call, not
mine.

### D7 — Cross-stack claims in a catalog entry: permitted, at exactly two strengths

A catalog entry **may** make a claim about a stack the ticket did not build. The strength must be
legible from the sentence:

- **Descriptive** ("iOS mirrors this — …", "the same shape exists on X"): permitted from **any**
  ticket. Requires (a) a **file:line citation** of the other stack's code **in the entry itself**, not
  only in the ticket's `## Review`, and (b) that it imposes **no obligation** on the other stack (no
  "so iOS must…"). It tells the next reader where to look; it does not bind them.
- **Prescriptive** (a rule the other stack must follow): **Architect**, and it must be written from —
  or ratified from — a ticket that **built and ran** that stack, or from an ADR. Under D2 a law's
  price is a gate *on that stack*, and a ticket that never ran that stack's build cannot pay it.

**The evidence standard that separates them: structural claims may be verified by reading; behavioural
claims require execution.** "Its generated models have the same all-optional shape" is structural —
you can read `CreateOrderCommand.swift:15-32` and see it. "The same mutation would leave the iOS suite
green" is behavioural — it requires running the iOS suite.

Applied to the flagged case: T-0441's sentence *"iOS mirrors this — its generated models have the same
all-optional shape"* is **structural, verified true, and carries no obligation**. The reviewer's call
to let it stand as descriptive was **correct**. The only thing missing is the file:line in the entry
(Block B below). Promotion to a prescriptive iOS test rule waits for T-0440, exactly as T-0441's
`## Review` already routed it.

### D7-interim — Disposition of the T-0451 hunk *pending* the panel

The hunk **stands**, revised to Block A. This is an **interim** disposition, labelled as such, and it
is reversible:

- The revision only **narrows an overclaim** (the general law is stated tree-wide; the gate binds two
  heroes) and **names the gate the entry already ships**. It adds no new obligation and no new work.
- The entry already satisfies D2 and D4 — `FixedWhiteContrastTests` + `AvatarDiscBindingTests` are a
  T1-CI XCTest guard in a scheme CI runs — so under this ADR's own rule it **earns** its "the ONE way"
  framing today. It is not a fait accompli presented to the panel; it is the one case where the rule
  and the artifact already agree.
- If the panel rejects D2, Block A survives anyway as a strictly more accurate entry.

---

## Exact catalog text, and who applies it

**Lane discipline (binding on all four blocks).** `agents/knowledge/patterns-*.md` is a shared-file
lane as of the PM's 2026-07-30 ruling (`agents/backlog/INDEX.md:177`; the durable
`shared-file-lanes.md` row is routed through T-0456). `patterns-mobile.md` currently carries
**uncommitted edits from two live worktrees** (T-0441's and T-0451's). **The architect does not edit
it.** Each block below is applied by the ticket that owns the hunk, in that ticket's worktree,
touching **only its own hunk** (`shared-file-lanes.md` rule 2). Nobody runs `git restore` on it
(rule 3). Nobody runs `git stash` — the stash is repo-global across worktrees.

### Block A — replaces the T-0451 entry in `agents/knowledge/patterns-mobile.md` (currently at :243-255 of the T-0451 worktree copy)

**Applier: the T-0451 iOS developer, in `wt-t0451`, replacing their own hunk.** Interim per D7-interim
— may be applied before this ADR is accepted, because it only narrows and cites.

```markdown
> **Ink on a theme-INVARIANT surface — the ONE way (T-0451):** a `Color.dynamic` token is right almost
> everywhere and wrong wherever the surface beneath it refuses to adapt. Both profile-hero avatar discs
> are a fixed `Color.white` in **both** schemes, so `CleansiaColors.primary` resolved to sky400 on them
> and measured **2.14:1** — under the WCAG **3:1** large-text floor — while light mode stayed fine at
> 4.10:1, which is why it survived review. **Pin the light-mode value; do not make the surface adaptive:**
> the disc is a deliberate cut-out in the brand gradient and Android's customer hero pins `Sky600` for
> the same reason (`ProfileTab.kt`), so adapting the disc would open a fresh divergence while closing a
> defect. Core owns the pinned pair — **`CleansiaColors.onFixedWhite`**, derived from the internal
> `fixedWhiteHex`/`onFixedWhiteHex` so the **ratio** is pinned from the hexes (a `Color` → `UIColor`
> roundtrip is trait-dependent on the iOS-16 floor — the `BrandGradientTests` rule). **Putting ink on a
> fixed surface means measuring the pair and adding it to the table:** a fill or ink outside
> `AvatarDiscBindingTests`' `discFills`/`discInks` is not styled wrong, it is *unmeasured*, and the test
> fails until someone measures it. The generalizable law — **an adaptive foreground over a hardcoded
> background is a contrast defect until someone measures it**, the same shape as the `onError`-on-`error`
> collapse above, and neither is visible in the theme the author develops in — is stated for the whole
> tree but **gated only on the two heroes**; the remaining theme-invariant surfaces are enumerated by the
> contrast-sweep ticket.
> **Gate (T1-CI):** `CleansiaCore/Tests/CleansiaCoreTests/FixedWhiteContrastTests.swift` —
> `AvatarDiscBindingTests` binds `ProfileTab.swift` + `ProfileHubContent.swift` **by source** (brace-matched
> outward from `Text(initials)`, so it fails loudly on a restructure rather than drifting), and
> `FixedWhiteContrastTests` validates its own WCAG arithmetic against three reference pairs.
```

### Block B — replaces the closing sentence of the T-0441 entry in `agents/knowledge/patterns-mobile.md` (currently `:180-181` of the T-0441 worktree copy)

**Applier: the T-0440 developer, when `patterns-mobile.md` reaches it in the lane**
(`INDEX.md:177` live lane: T-0441 ✅ → T-0440). T-0440 is the iOS-side ticket, so it is the ticket
that will have *run* the stack — which is what D7 requires for anything stronger than a citation, and
it is where T-0441's `## Review` already routed the promotion decision. **T-0440's standing
instruction not to re-harvest is unchanged: this is a two-line edit to an existing sentence,
specified here by the architect, not a harvest.**

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

**Applier: the ticket filed as FT-5 (architect or docs).** `conventions.md` is **not** currently
lane-contended (the contended catalog files this sprint are `consistency.md` → T-0454 → T-0461,
`security-rules.md` → T-0460, and the `patterns-*` family). Deliberately **no `consistency.md` edit**
is required by this ADR — the normative text lives in one uncontended file, and `patterns-mobile.md`
already instructs every mobile agent to read `conventions.md` (`:5`).

Insert after the existing numbered list (i.e. after step 3, before the "The bar:" paragraph):

```markdown
### Which of those two lanes you are in — the routing test (ADR-0032)

Apply in order. The **first** one that fires routes the edit to the **Architect**; if none fires, edit
inline and flag it in the ticket's `## Review`.

1. **Does the edit put code that exists today in violation?** If any current call site becomes a
   deviation it wasn't before, it needs a `consistency.md` deviation entry and a canonicalization
   ticket — neither of which a developer or a reviewer can file for themselves.
2. **Does it forbid an alternative a competent developer could otherwise reasonably choose?** The
   "the ONE way" / "never X" / "X is a defect" framing is a **law**, and laws are priced (below).
3. **Does it make a prescriptive claim about a stack this ticket did not build and run?** A rule for
   a stack you never executed is not yours to declare.

*Not* the test: "is this a gap in the rules or a clarification to them?" That measures novelty
relative to the text rather than cost imposed on the codebase, and the two come apart in both
directions — a gap can oblige nobody, and a "clarification" that sharpens an existing rule's scope can
retroactively put dozens of shipped call sites in violation.

### The price of a law: "the ONE way" must name a gate that covers what it claims (ADR-0032)

An entry that forbids an alternative may use the imperative framing **only if it names, inline, a
CI-blocking enforcer** — a rule id, or a test file + test class — and **only if that enforcer's
assertion covers the scope the sentence claims**. Otherwise write it descriptively ("the canonical
form is X, because Y") and mark it **`(guidance — no gate)`**.

- A gate that asserts a **closed roster** is legitimate, but say so in the entry ("gated on the two
  heroes; the rest are enumerated by <ticket>"). A hand-maintained roster does not catch the next
  instance outside it, and the reader must be able to see that boundary.
- A guard test that walks the tree must **fail when its corpus is empty or its anchor is missing**
  (`XCTUnwrap` the file read; assert the anchor count). A test that passes because the files were
  renamed away is not a gate.
- **An entry describing a shared component's own internals is not a law over call sites** and needs no
  gate — it needs accurate prose.
- **Per stack:** backend/frontend/Android — a unit/integration test in a CI job, or a
  `check-consistency.mjs` rule *that is in that stack's CI workflow*. **iOS** — a SwiftLint
  `custom_rules` entry (`swiftlint lint --strict` blocks in `ios-ci.yml`) for a single-line
  token/literal ban, or an **XCTest guard** for anything relational, computed, or over non-Swift files
  (`.xcstrings`, plists), using the `#filePath` walk-out-of-the-package idiom of `ConsentCatalogTests`
  / `AvatarDiscBindingTests`. `check-consistency.mjs` **cannot read Swift at all** and cannot discharge
  this requirement.

Why it matters: an ungated "the ONE way" is worse than silence. It tells the next reader the question
is settled and something is watching — so they stop looking — while nothing is.

### Cross-stack claims (ADR-0032)

A catalog entry may reference a stack the ticket did not build, at exactly two strengths, and the
strength must be legible from the sentence:

- **Descriptive** — permitted from any ticket. Needs a **file:line citation of that stack's code in the
  entry itself** (not only in the ticket's `## Review`), and must impose **no** obligation on that
  stack. Label it: *"Cross-stack note (descriptive — not a rule for X)"*.
- **Prescriptive** — Architect, and it must come from a ticket that **built and ran** that stack (or
  from an ADR). A law's price is a gate on that stack; a ticket that never ran that stack's build
  cannot pay it.

The line between them is the evidence: **structural claims may be verified by reading** ("every
property on the generated model is optional"); **behavioural claims require execution** ("the same
mutation leaves that suite green").
```

### Block D — `agents/process/enforcement.md`

**Applier: the ticket filed as FT-5 (architect or docs).** `enforcement.md` is uncontended.
Add the iOS row to the "What's mechanical today" table and a short section after it.

Table row to add:

```markdown
| iOS (Swift) | `swiftformat --lint` + `swiftlint lint --strict` (pinned 0.60.1 / 0.65.0) + 3 XCTest schemes (CI: `ios-ci.yml`) | formatting, lint, and whatever the guard tests assert. **`check-consistency.mjs` covers NO Swift** — its walker globs `.cs`/`.ts`/`.kt` only | **live in CI** (lint + tests); **no project-specific rules yet** — `.swiftlint.yml` has no `custom_rules:` |
```

Section to add after the table:

```markdown
## Enforcement tiers — what a rule is worth (ADR-0032)

- **T1-CI** — fails a CI job on the offending change. Backend/frontend/Android: a test in a CI job.
  iOS: a SwiftLint `custom_rules` entry, or an XCTest guard in one of the three schemes CI runs.
- **T2-ADVISORY** — `check-consistency.mjs`: reviewer-run, non-blocking (the E9 precedent above).
- **T3-HUMAN** — the Reviewer reading the catalog against the diff. Nothing else.

**Only T1-CI discharges a "the ONE way" declaration** (`conventions.md` §"The price of a law"). Adding
Swift to the consistency walker would land at **T2-ADVISORY** and would not discharge it — which is
why it is not the answer to the iOS enforcement gap, even though it is the cheapest change.

**Known tool defect (ADR-0032 D5):** a `--paths=` scope that matches zero files currently prints
`consistency: OK (0 files scanned)` and exits 0 — so `--paths=src/cleansia_ios` reads as a **pass** for
a stack the tool cannot parse. A zero-file scope must report `scope matched 0 files` and not report OK.
```

---

## Alternatives considered

**A. Add `.swift` to the `check-consistency.mjs` walker.** *Rejected as the answer to D2; not adopted
now.*
- It is genuinely the cheapest *code* change and it is where every other stack's rules live, which is
  the honest argument for it.
- But it lands at **T2-ADVISORY**: the checker is **in no stack's CI workflow** today
  (`enforcement.md` §Rollout step 3 gates CI entry on a zero baseline, which no stack has reached). It
  is run by the Reviewer — the same human who is already reading the catalog and the diff. So it buys
  a *second copy of the enforcement we already have*, and cannot discharge a law.
- It is also not the one-liner it looks like. Adding `.swift` to `checkMobile`'s walk feeds every
  Kotlin regex Swift source; `/Text\(\s*"[^"]+"/` (`:422`) matches SwiftUI `Text("…")` immediately, so
  the change requires per-language rule gating **plus** a baseline sweep of every SwiftUI preview and
  literal in two apps — i.e. exactly the "add enforcement behind the cleanup, never in front of it"
  situation `enforcement.md` warns about.
- What we take from it: the **false-green** defect (D5.1) is real and is filed regardless. A future
  Swift walker is welcome as a T2 advisory; it just cannot pay for a law.

**B. SwiftLint `custom_rules` as the single mechanism.** *Adopted, but only for the rule shapes it can
express (D4).*
- Strongest argument in the set: it **already runs and already blocks** (`swiftlint lint --strict`,
  pinned 0.65.0, `ios-ci.yml:149-151`), so the marginal cost of a rule is a YAML block. `included`/
  `excluded` expresses "banned everywhere except the file that owns it" exactly — which is the
  `CleansiaWeb` domain rule.
- Rejected as the *only* mechanism because it is single-line regex over one file: it cannot express
  "this fill pairs with that foreground at ≥ 3:1", cannot reason across files, and does not lint
  `.xcstrings` or plists at all — so it cannot cover the tree-wide half of the very rule it is best
  at. Regex rules also have no reason-annotated allowlist, only path excludes.
- Cost accepted: pinned-tool discipline (a SwiftLint bump can change `match_kinds` behaviour), and
  regex false positives, which must be driven to zero before the rule is added — same "behind the
  cleanup" discipline.

**C. Require a hand-written guard test as the price of declaring a law.** *Adopted as the default.*
- The repo has the idiom and it is proven twice: `ConsentCatalogTests:16-22` walks out of the package
  by `#filePath` and asserts facts about files in both apps by relative path;
  `AvatarDiscBindingTests` does the same and additionally **computes** the property (a contrast ratio)
  rather than matching a spelling. Both run in CI.
- The decisive evidence is affordability: T-0451's dev produced the stronger of the two inside an
  **S**-sized ticket. The price of a law on this stack is one file, written by the person who already
  holds the context — which is also *when* it is cheapest to write.
- Cost accepted: bespoke per rule; the roster is hand-maintained (D3 makes the boundary visible);
  a naive tree-walk can pass vacuously (D3's anti-vacuity requirement closes that).
- Rejected as *sole* mechanism: a token ban that SwiftLint can express in three YAML lines should not
  cost a test file.

**D. Restrict the catalog to descriptive guidance on unenforced stacks.** *Rejected.*
- It concedes the catalog's entire job — "one way to do each thing" — on the stack with two shipping
  apps, and leaves the next developer building a profile hero with no binding rule and the reviewer
  with nothing to cite.
- It also rests on a false premise. iOS is not *unenforced*; it is **T3-HUMAN-enforced** — the
  Reviewer reads the catalog and the diff, which is weaker than CI and much stronger than nothing.
  The honest fix is to make the tier **visible**, not to delete the rules.
- What we take from it: its correct half is D2's fallback — where nobody will pay for a gate, the
  entry is written descriptively and marked `(guidance — no gate)` rather than pretending to be law.

**E. Grandfather the four existing ungated iOS "ONE way" entries by rewriting them all now.**
*Rejected — big-bang.* They stand, unchanged, until the triage ticket (FT-4) dispositions each into
**gate** or **downgrade**. This ADR does not retroactively invalidate them. The one exception is the
`CleansiaWeb` entry, whose **verified overclaim** (tree-wide sentence, two-sentence gate) is a factual
error and is fixed by FT-2 rather than waiting on triage.

**F. Leave it as a reviewer convention / a companion-doc note.** *Rejected* on ADR-0018's precedent:
a rule that adds a standing gate and binds every future catalog edit is an ADR + a catalog artifact,
not folklore. Two agents already nearly ruled inconsistently on the routing question deriving it
fresh — which is the definition of a rule that needs to be written down once.

---

## Consequences

**Cheaper / safer**
- A reader can tell, **from the entry**, whether anything is watching — which is exactly what nobody
  can tell today about five iOS entries.
- The routing question is decided once, in three ordered tests that reproduce all three real rulings,
  instead of being re-derived per ticket by whoever is holding the diff.
- The strongest existing iOS guard idiom (compute the property, bind the call site by source, fail
  loudly on restructure) becomes the named house form instead of one dev's good instinct.
- The `CleansiaWeb` overclaim is closed, and the class of "enforcer asserts less than the sentence
  claims" becomes a reviewable finding rather than an invisible one.

**More expensive (new obligations)**
- **Declaring a law now costs a gate.** That is the point, and it is a real tax on iOS tickets that
  want to canonicalize something. The escape hatch is honest: write it descriptively and mark it
  `(guidance — no gate)`.
- Every *new* catalog entry that constrains call sites must state a tier. Existing entries are
  grandfathered (Alternative E) — no sweep, no big-bang.
- The Reviewer gains one check (below) and the Architect gains a triage ticket.

**What could go wrong (state it plainly)**
- **Gate-shaped busywork.** A dev under time pressure writes a weak test to buy the imperative wording.
  Mitigated by D3's coverage + anti-vacuity requirements, and by the reviewer check — but not
  eliminated. This is the most likely failure mode and the panel should push on it.
- **Roster rot.** Hand-maintained rosters (`discFills`, `SESSION_WIPE_ALLOW`, the two-hero list) do not
  catch the next instance. D3 makes the boundary visible; it does not close it.

---

## How a reviewer verifies compliance

On any ticket whose diff touches `agents/knowledge/*.md`:

1. **Routing.** Run D6's three tests against the hunk. If any fires and there is no ADR, the edit is a
   finding: the content may be right, but the ticket may not ratify it — route to the Architect.
2. **Tier named.** Any hunk using "the ONE way" / "never" / "is a defect" over **call sites** names an
   inline T1-CI enforcer (rule id, or test file + class). If not, it must be rewritten descriptively
   with `(guidance — no gate)`.
3. **Coverage honest.** **Open the named enforcer and read what it asserts.** If the sentence claims
   more than the gate asserts, either the sentence narrows or the gate widens. (This check is what
   would have caught the `CleansiaWeb` entry.)
4. **Non-vacuous.** A guard test that walks the tree fails on an empty corpus / missing anchor —
   `XCTUnwrap` the read, assert the anchor count.
5. **Cross-stack claims.** Descriptive claims carry a **file:line in the entry** and impose no
   obligation; prescriptive claims about another stack come from a ticket that ran that stack, or an
   ADR.
6. **Lane.** The hunk was applied in the ticket's own worktree, touching only its own hunk. No
   `git restore` of a shared catalog file; no `git stash`.

---

## Roles affected

No new code roles. **Reviewer** gains the six-point check above. **Architect** owns the tier
assignment and the FT-4 triage. **ios** charter gains the D4 mechanism choice (SwiftLint
`custom_rules` vs XCTest guard, by rule shape).

**On acceptance**, the living companion `agents/architecture/decisions/catalog-governance.md` is
created in the same change (per `deliberation.md` §"Parallel documentation") carrying the tier table,
the routing test, and the running list of which catalog entries sit at which tier. It is deliberately
**not** created now: a living "current shape" doc for a `proposed` decision would read as settled.

---

## Follow-up tickets — specs, not files

**I have not created ticket files.** IDs are PM-allocated and T-046x is in active use (…T-0468); an
invented id would collide. Each spec below is ready to file as-is.

| # | Title | Layers / size | Panel? | Sequencing |
|---|---|---|---|---|
| **FT-1** | **`check-consistency.mjs`: a `--paths=` scope matching zero files must not report OK** — report `scope matched 0 files` and exit non-zero. AC: `--paths=src/cleansia_ios` no longer prints `OK`; a normal scoped run is unchanged; a case added to `check-consistency.test.mjs`. | tooling, **XS** | **no** — a tool bug, not a decision. **Does not wait for this panel.** | `check-consistency.mjs` lane is **T-0454 → T-0461** (`INDEX.md:170`). Fold into T-0454 if it has not dispatched; otherwise sequence **behind T-0461**. |
| **FT-2** | **Bootstrap `custom_rules` in `.swiftlint.yml`, pilot the `CleansiaWeb` no-literal-domain rule, and close its verified overclaim** — a `custom_rules` entry banning the literal domain in Swift sources with `excluded:` on `Config/CleansiaWeb.swift`; widen `ConsentCatalogTests` (or a sibling) to assert **no** `.xcstrings` value and no plist value in either app carries the literal domain; then narrow or confirm the `patterns-mobile.md` sentence to match. | ios, **S** | no — enforcement of an existing catalog rule | needs the `patterns-mobile.md` lane for the final sentence tweak; the code+test half is lane-free. |
| **FT-3** | **iOS theme-invariant contrast sweep** — T-0451's explicitly deferred `## Out of scope` item. Enumerate every opaque theme-invariant surface in the iOS tree (the PM reports **three**; `AvatarDiscBindingTests` binds **two**), record the third **with its ink pair and computed ratio**, and either extend the roster or add the paired Core token. AC includes recording the enumeration method so the count is reproducible. | ios, **S** | no | after T-0451 merges; **`CleansiaColors.swift` + `FixedWhiteContrastTests.swift` lane** behind T-0451. |
| **FT-4** | **Triage the four grandfathered iOS "ONE way" entries into gate-or-downgrade** — `CleansiaDangerButton`, `SnackbarPill`, `CleansiaConsentCheckbox`, `CleansiaWeb`. Each gets a T1-CI gate, or is rewritten descriptively with `(guidance — no gate)`. Note two known facts going in: the `CleansiaDangerButton` entry names its own **live violation** (partner `ProfileHubContent`'s hand-rolled copy) with no gate; `SnackbarPill` is likely a *component-internals* entry that needs prose, not a gate (D2 scope). | ios + architect, **M** | no — applies this ADR | **after** this ADR is accepted; **after** FT-2 (which resolves `CleansiaWeb` and proves the mechanism). |
| **FT-5** | **Apply ADR-0032's catalog + process text** — Block C into `conventions.md`, Block D into `enforcement.md`. Both files are lane-uncontended. **No `consistency.md` edit** (deliberately — that lane is T-0454 → T-0461). | architect + docs, **S** | no — the ADR is the decision | **after** this ADR is accepted. |

---

## What this ADR does **NOT** decide

- **It does not rule on the substance of the T-0451 fix.** Pinning the light-mode value versus making
  the surface adaptive was settled by the owner's iOS↔Android convergence ruling plus Android's
  shipped, commented deviation, and recorded under the ticket's AC4. I ratify the *entry*, not the
  colour.
- **It does not enumerate the theme-invariant surfaces.** The "exactly three" figure is the PM's,
  from a structural sweep I did not run and cannot reproduce from the catalog. I record it as reported
  and route the enumeration to FT-3. (For what it is worth: the splash gradient
  `CleansiaColors.splashGradientStart/End:57-60` is theme-invariant but *dark*, and already has
  `BrandGradientTests` — so it is probably not the third.)
- **It does not add Swift to `check-consistency.mjs`.** It rules that doing so would be T2-ADVISORY
  and could not discharge a law; whether to do it anyway is a separate, cheaper call.
- **It does not write any SwiftLint `custom_rules` or any guard test.** Those are FT-2's.
- **It does not re-open T-0274**, T-0441's reviewer verdict, T-0446's SEC-5 routing, or ADR-0018.
- **It does not touch `consistency.md`, `security-rules.md`, `INDEX.md`, `quality-gates.md`, or any
  `patterns-*.md` file.** Those are lane-held by other tickets.
- **It does not decide whether the seven `.models.ts` resolvers T-0274 left inlining the walk get a
  canonicalization ticket.** That is the PM's scheduling call; D6 only records that the edit was
  mis-routed.
- **It does not change the Reviewer's authority.** A reviewer may still reject a catalog hunk on
  content; this ADR only fixes *which* hunks they may ratify.

---

## Challenge

<!-- CHALLENGERS: empty by design. The author has no standing to fill this in. Agenda below. -->

**Named agenda for the panel — the four places this draft is weakest, stated by the author:**

1. **Does D2 price laws out of existence?** If a gate costs more than an S-sized ticket can bear, the
   predictable outcome is not "better gates" but **fewer canonical rules** — devs will write
   `(guidance — no gate)` reflexively, and the catalog softens everywhere. The counter-evidence is a
   single data point (T-0451's dev). Is one data point enough to tax every future law? Attack the
   affordability claim.
2. **Is Alternative A dismissed too fast?** A T2 advisory that fires on *every* reviewer run may in
   practice catch more real drift than a T1 gate that only exists for rules someone bothered to gate.
   "It's only advisory" may be the wrong axis if advisory coverage is broad and gate coverage is
   sparse. Make the case for breadth over bindingness.
3. **Does D6 test 1 over-route?** "Puts existing code in violation" is broad — a sharpened example or
   a newly-named footgun often technically implicates shipped call sites. If test 1 fires on most
   real edits, D6 collapses into "everything goes to the Architect", the inline lane dies, and the
   harvest loop `conventions.md` deliberately opened closes again. Where is the floor?
4. **Is the "structural vs behavioural" line in D7 stable?** "Every property is optional" is
   structural today because the models are committed. If iOS's generated client ever stops being
   committed, the same claim becomes unverifiable-by-reading and the rule silently changes strength.
   Is the line drawn on the right property?

Also worth attacking: whether `(guidance — no gate)` is a marker anyone will actually write, or
whether it will be quietly omitted; and whether D7-interim is a fait accompli dressed as an interim.

## Defense

<!-- AUTHOR: to be written after challenges land. REBUT with evidence / CONCEDE + REVISE / ESCALATE. -->

## Verdict

<!-- LEAD (a different architect instance): not yet convened. This ADR is `proposed`. -->

**Not accepted. No verdict has been issued and none is claimed here.**
