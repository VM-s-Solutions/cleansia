# ADR-0032 — A catalog entry that constrains call sites names its **enforcer** and declares its **tier**; the enforcer's assertion must cover the scope the sentence claims. T1-CI is required where the rule is mechanizable **and** its baseline is zero — not merely because the sentence is imperative

- **Status:** accepted   <!-- proposed | accepted | superseded | rejected -->
- **Date:** 2026-08-01 (drafted `proposed` 2026-07-30; amended and accepted by the panel lead 2026-08-01)
- **Supersedes:** —
- **Superseded by:** —
- **Filename note:** the slug (`…-require-a-named-ci-gate`) is the **author's draft title**, which the panel
  amended (C2/C9). The normative title is the H1 above. The file is not renamed here because renaming
  would orphan the ADR number's history mid-panel; a `git mv` + link sweep is filed as **FT-7**.
- **Split note (C8):** this ADR was drafted carrying **three** decisions. The panel split it per
  `adr/README.md:3`. **D6 (the catalog-edit routing test) and D7 (cross-stack claim strength) moved to
  ADR-0033**, where they are its D1 and D2 — they are *one* decision, because draft-D6's test 3 *is*
  D7. What remains here is one decision: **what a catalog entry that constrains call sites must state
  about its own enforcement.** ADR-0033 is `proposed`, not accepted (see its status block).
- **Refines:** `agents/knowledge/conventions.md` §"Harvest good patterns back into the catalog" (the
  "earns its place" bar) and `agents/process/enforcement.md` (the "deterministic beats diligent"
  principle, the E9 two-tier precedent, and the **zero-baseline rule** at `enforcement.md:104-106`).
  **Consumes ADR-0018**, whose precedent the panel corrected: ADR-0018 is an `accepted`, load-bearing
  iOS law enforced **entirely by a named human checklist** (Gate-DP, §G of
  `ios-app-review-checklist.md` + reviewer-check #22). That is the precedent this ADR now follows —
  *a named enforcer at a declared tier*, not *a CI gate or nothing*.
- **Applies to:** cross-cutting (catalog governance) · ios (the tier mechanics are iOS-specific)
- **Number note:** **0031 is taken** by `0031-nswag-regen-drift-is-guarded-at-regen-time.md`, which
  exists only in T-0439's worktree and has not reached `master`. **0033** is allocated by this panel to
  the split-off decision. A reader on `master` sees a gap at 0031 until T-0439 merges.
- **Ticket:** none — raised by the PM from the T-0451 reviewer's refusal to ratify a catalog edit
  inline. **Consumers:** one `patterns-mobile.md` hunk applied by its own lane holder (Block A), the
  `conventions.md` + `enforcement.md` edits applied with this ADR, and the follow-ups in §Follow-ups.

---

## Context

### What happened

T-0451 fixed a verified iOS dark-mode contrast defect (initials at **2.14:1** on a hardcoded-white
avatar disc; fixed by pinning the ink to the light-mode brand blue, 4.10:1). In the same change it
added a `patterns-mobile.md` entry titled **"Ink on a theme-INVARIANT surface — the ONE way
(T-0451)"**. The reviewer **did not dispute the content** — it is well-argued and the fix is verified
to the last digit — but refused to ratify it inline on the ground that declaring "the one way to do X"
is an architect call. That routing was correct by the standing rule (`conventions.md:125-127`); the
routing question itself is now **ADR-0033**, not this ADR.

What the refusal exposed underneath, and what this ADR decides, is different: **a catalog reader
cannot tell, from an entry, whether anything is watching — and when an entry *does* name an enforcer,
they cannot tell whether that enforcer asserts what the sentence claims.**

### The verified evidence (re-verified by the panel on `master`, 2026-08-01)

**1. iOS catalog laws are numerous and almost entirely unnamed-enforcer.** The draft asserted "every
iOS 'the ONE way' entry — [four named]". **That was wrong by ~5×** (C1, sustained). On `master`,
`agents/knowledge/patterns-mobile.md` carries **22 lines matching "the ONE way"** plus one "The ONE
sanctioned way" (the S11 wipe-set rule at `:191`), and **~20 entries closing with an explicit
"Deviations a reviewer rejects:" list** — which is a law in substance whatever its opening framing.
Against that corpus, the string `Tests` appears **4 times in 1093 lines** (`:205`, `:269`, `:348`,
`:517`). So the real shape is: **~22 iOS laws, ~3 naming any enforcer at all.**

**2. One of those entries overclaims its enforcer — verified, and this is the load-bearing finding.**
`patterns-mobile.md:266-270` states that *"a second literal `cleansia.cz` anywhere in the iOS tree
(Swift, string catalog or plist) is a defect, and `ConsentCatalogTests` pins the markup + the
no-literal-domain rule across both apps × five locales."*
`ConsentCatalogTests.testConsentSentencesCarryNoLiteralDomain`
(`src/cleansia_ios/CleansiaCore/Tests/CleansiaCoreTests/ConsentCatalogTests.swift:54-64`) iterates
**two catalog keys × five locales** and asserts the *consent sentence* carries no literal domain. It
says nothing about any other string, any Swift source, or any plist. **The rule is tree-wide; the gate
is two sentences.** The gate exists, runs in CI, and is green — which is exactly why no banner, no
tool-coverage report, and no "is this stack scanned?" signal can ever surface this. Only reading the
named enforcer against the sentence surfaces it.

**3. `check-consistency.mjs` reads no Swift, and is in no CI workflow.** `checkMobile` walks `[".kt"]`
only (`agents/tools/check-consistency.mjs:387`); `DEFAULTS.mobile` is `["src/cleansia_android"]`
(`:502`); there is no `ios` stack key. Independently verified by the panel: **`check-consistency`
appears in zero files under `.github/`** — so on *every* stack it is reviewer-run, not CI-blocking,
today.

**4. The false-green defect is real, is already fixed, and the fix is NOT on `master` (C11).**
A `--paths=src/cleansia_ios` scope matched zero files and printed `consistency: OK` + exit 0. The fix
— a loud `NOT RUN` banner — exists on branch `fix/tooling-false-green-and-broken-docs` (commit
`c9265298`) and, as verified on **2026-08-01**, is **not an ancestor of `master`**: `master`'s
`check-consistency.mjs` has zero occurrences of `isAbsolute` and zero of `NOT RUN`. The merge is
imminent. **This ADR is written to be true on both sides of that merge:** the `NOT RUN` banner is
*ratified, not re-decided* here (see Alternative G and D5), and FT-1 is re-scoped from "build it" to
"verify and close it on merge".

**5. `.swiftlint.yml` has no `custom_rules:` block, and its `included:` roster is narrower than "the
iOS tree" (C6).** `swiftlint lint --strict` on a pinned 0.65.0 does run in CI
(`.github/workflows/ios-ci.yml:149-151`), so a custom rule at default `warning` severity is a hard CI
failure for the cost of a YAML block. But `src/cleansia_ios/.swiftlint.yml:1-5` includes only
`CleansiaCore/Sources`, `CleansiaCore/Tests`, `CleansiaPartner/Sources`, `CleansiaCustomer/Sources` —
it does **not** cover `CleansiaCustomer/LiveActivity/` (1 Swift file), `CleansiaCustomer/Tests/`, or
`CleansiaPartner/Tests/` (65 Swift files). A `custom_rule` therefore **cannot** honestly claim
"anywhere in the iOS tree" without widening `included:` first.

**6. The stated exemplar of an ungated law cannot be gated today (C3).** The `CleansiaDangerButton`
entry (`patterns-mobile.md:233-241`) names its own live violation: partner
`CleansiaPartner/Sources/Features/Profile/ProfileHubContent.swift:298-320` (`LogoutRow`) hand-rolls
exactly the component's visual — `CleansiaColors.error` glyph+label, `error.opacity(0.12)` fill,
`error.opacity(0.4)` hairline. `enforcement.md:104-106` is categorical: *"a check only becomes
blocking in CI once its baseline is zero for that stack — add enforcement behind the cleanup, never in
front of it."* A draft rule of "T1-CI or downgrade to guidance" therefore forced this entry — an
accurate law with a named, ticketed convergence target — to be **softened**. That is
anti-correlated with need, and the panel fixed it (the `(gate pending: <ticket>)` tier, D2).

**7. The price of a real gate, where one is affordable, is genuinely one file.** T-0451's developer
shipped `CleansiaCore/Tests/CleansiaCoreTests/FixedWhiteContrastTests.swift` inside an **S**-sized
ticket: `FixedWhiteContrastTests` pins the **ratio from the hexes** and validates its own WCAG
arithmetic against three reference pairs; `AvatarDiscBindingTests` binds the two call sites **by
source**, brace-matching outward from `Text(initials)`, and is non-vacuous by construction (a moved
file throws on read; a missing/duplicated anchor fails `XCTAssertEqual(anchors.count, 1)`). Android
already pins the same pair (`customer-app/.../ProfileTab.kt:279-284`), so T-0451 closes a
cross-platform divergence rather than opening one. **But one data point does not price ~19 gates**
(C1 + the affordability challenge), and the guard's root-resolution is copy-pasted from
`ConsentCatalogTests:16-22` with no shared harness, so the cost is **linear in laws, not amortized**
(C7). That is why the panel priced the *label*, not the *test file*.

**Conclusion from the evidence:** "unenforced stack" is not a reason to weaken the catalog, and it is
not a reason to tax every law with a CI gate either. It is a reason to make the catalog **state its
enforcement honestly, at the tier that is actually available** — and to require that whatever enforcer
is named asserts what the sentence claims.

---

## Decision

### D1 — Enforcement is a declared **tier**, and the tier is a property of *where the check runs*, not of *which tool*

| Tier | What it means | iOS mechanism | Backend / Frontend / Android mechanism |
|---|---|---|---|
| **T1-CI** | **fails a CI job** on the offending change | a SwiftLint `custom_rules` entry (`swiftlint lint --strict`, `ios-ci.yml:151`) **or** an XCTest guard in one of the three schemes CI runs (`ios-ci.yml:169-190`) | a test in a CI job (`dotnet test` / `nx test` / Gradle), **or** a `check-consistency.mjs` rule *once that stack's checker step is in its workflow* |
| **T2-ADVISORY** | runs on demand, reports, **never sets the exit code** for the reviewer's gate | — (no Swift support) | `check-consistency.mjs` **as it stands today on every stack** — verified in **zero** `.github/` workflows; includes its warn-only rules (E9) |
| **T3-HUMAN** | a **named item in a standing checklist** the Reviewer runs | Gate-DP §G of `ios-app-review-checklist.md`; Gate-AR; a numbered reviewer-check | the numbered reviewer-checks / `quality-gates.md` items |
| **`(gate pending: <ticket>)`** | the gate is **specified and ticketed**, but its baseline is non-zero, so it cannot block yet (`enforcement.md:104-106`) | either mechanism, landing with or behind the cleanup ticket | same |
| **`(guidance — no gate)`** | nobody enforces it; it is advice | — | — |

**T3-HUMAN requires a *named* checklist item.** "The reviewer will notice" is **not** T3 — an unnamed
human enforcer is `(guidance — no gate)`. This is the line that separates ADR-0018 (a real,
load-bearing, human-enforced law with a named gate) from folklore, and it is why ADR-0018's progeny
remain **laws** under this ADR.

The draft's error, corrected: it bound `check-consistency.mjs` to T2 *by identity*. `enforcement.md`
§Rollout step 3 explicitly promotes the checker into a stack's CI workflow once that stack's baseline
hits zero — at which point its rules are T1-CI. The tier moves; the tool does not.

### D2 — A catalog entry that constrains call sites names its **enforcer** and declares its **tier**. Imperative framing does not buy a CI gate; it buys nothing

Every catalog entry that constrains code **other people write** carries, inline:

```
**Enforced by:** <named enforcer> — <tier token>
```

where the **named enforcer** is a SwiftLint rule id, a test file + test class, a
`check-consistency.mjs` rule id, or a **named** standing-checklist item; and the **tier token** is one
of `T1-CI` / `T2-ADVISORY` / `T3-HUMAN` / `(gate pending: <ticket>)` / `(guidance — no gate)`.

**T1-CI is required when — and only when — both hold:**

1. the rule is **mechanically expressible** on that stack (a regex, a computable property, a roster
   equality), **and**
2. its **baseline is zero** on that stack (`enforcement.md:104-106`).

If (1) fails, the tier is **T3-HUMAN** and the enforcer is a **named checklist item**. ADR-0018's
Gate-DP is the governing precedent: *"this screen's layout, flow and branding match the corresponding
Android Compose screen"* cannot be asserted by SwiftLint or XCTest **in principle**, and it is
nevertheless the project's most load-bearing iOS law. A rule that forced it to be relabelled guidance
would be wrong about the project, not about the rule.

If (2) fails, the tier is **`(gate pending: <ticket>)`**: the gate is specified in the entry, the
cleanup/canonicalization ticket is named, and the tier **promotes to T1-CI when that ticket lands**.
This is the tier `CleansiaDangerButton` sits at today.

`(guidance — no gate)` remains available and is now genuinely weak — because T3-HUMAN with a named
checklist item is cheap and strictly better, an author who writes `(guidance — no gate)` is saying
"nobody, not even the reviewer, is checking this."

**Why the draft's stronger rule ("the ONE way" ⇒ T1-CI or downgrade) was not adopted (C2, C3, C9):**

- **It contradicts its own cited precedent.** ADR-0018 is `accepted` and enforced entirely at
  T3-HUMAN; its own rejected alternative states the precedent as *"an ADR + a checklist artifact"*.
- **It is anti-correlated with need.** The rules whose imperative force is doing the most work are
  frequently the ones with a live violation — and a live violation is exactly what
  `enforcement.md:104-106` forbids gating. `CleansiaDangerButton` is the worked example.
- **It could not discharge itself.** This ADR's own enforcer is a reviewer procedure — T3-HUMAN. Its
  mechanical form would live in `check-consistency.mjs`, which the draft itself ruled T2, which sits
  in no CI workflow, and which could not be promoted while ~19 grandfathered entries stand. A
  governance rule that must sit at a tier it forbids its subjects is not a rule; it is an exemption.
- **It created an incentive to launder** (C4): "the canonical form is X" would have dodged both the
  gate and the routing panel while imposing the identical constraint. Under D2 as accepted, the
  incentive is gone — every constraining entry names a tier **whatever its wording**, so imperative
  phrasing costs nothing and buys nothing. The reviewer check (below) is keyed on the **semantic**
  property, with wording as a prompt only.

**Scope of D2, tightly (unchanged from the draft):** it bites on entries that constrain **call
sites** — code other people write. An entry that merely *describes a Core component's own internals*
(e.g. how `SnackbarPill` renders itself) is not a law over call sites and needs no enforcer; it needs
accurate prose.

### D3 — The named enforcer's assertion must **cover the scope the sentence claims** (unchallenged; the panel calls this the strongest part)

An entry may not claim tree-wide coverage from an enforcer that asserts a closed roster. Either:

- **narrow the sentence** to what the enforcer actually asserts, and state the residual scope
  explicitly ("gated on the two heroes; the remaining surfaces are enumerated by `<ticket>`"), **or**
- **widen the enforcer.**

A roster-based enforcer is legitimate — `enforcement.md` specifies exactly this shape for S11
(`SessionScopedModuleTest` / `SessionScopedCacheRegistryTest`) — but the roster's boundary must be
**visible in the entry**, because the roster is hand-maintained and a new instance outside it is not
caught.

**Anti-vacuity requirement.** A guard test that walks the tree must **fail** when its corpus is empty
or its anchor is missing (the `XCTUnwrap` / `XCTAssertEqual(count, 1)` shape of
`ConsentCatalogTests:31` and `AvatarDiscBindingTests:92`). A "grep the tree and assert nothing matches"
test that passes when the files have been renamed away is not an enforcer.

**This is the requirement no tool-coverage banner can replace.** The `CleansiaWeb` overclaim is
invisible to any "is this stack scanned?" signal: the gate exists, runs in CI, and is green while
asserting a fraction of what the sentence claims. Entry-level coverage honesty and stack-level tool
coverage are orthogonal; they compose (Alternative G).

### D4 — On iOS, the enforcer is SwiftLint `custom_rules` **or** an XCTest guard, chosen by rule *shape* — within the scope `.swiftlint.yml` actually lints

- **Single-line token/literal ban, scoped by path** → **SwiftLint `custom_rules`**. Cheapest real
  enforcement in the repo: it already runs, already blocks, `included`/`excluded` expresses "banned
  everywhere except the one file that owns it", and `match_kinds` keeps comments out. The
  `CleansiaWeb` no-literal-domain rule is the textbook case for Swift sources, and its **baseline is
  zero** — verified: exactly one occurrence of the literal exists in the entire iOS tree, at
  `CleansiaCore/Sources/CleansiaCore/Config/CleansiaWeb.swift:8`.
- **Anything relational, cross-file, computed, or over non-Swift files** (`.xcstrings`, plists) →
  **an XCTest guard**, using the `#filePath`-walk-out-of-the-package idiom
  (`ConsentCatalogTests:16-22`). SwiftLint regex cannot express "the fill three lines up pairs with
  this foreground colour at ≥ 3:1", and SwiftLint does not lint `.xcstrings` at all.

**The honest scope of a SwiftLint gate today (C6).** `.swiftlint.yml:1-5` lints only
`CleansiaCore/Sources`, `CleansiaCore/Tests`, `CleansiaPartner/Sources`, `CleansiaCustomer/Sources`.
An entry whose enforcer is a `custom_rule` may **not** say "anywhere in the iOS tree" unless
`included:` is widened to cover `CleansiaCustomer/LiveActivity/` and both apps' `Tests/` trees —
otherwise D3 applies and the residual is stated. The draft's claim that *"most real laws need both"*
mechanisms is struck as unevidenced: of the four triaged entries, SwiftLint alone can express **one**.
Which mechanism applies is decided by **rule shape**, not by preference or by a quota.

**The XCTest tier is not free, and the cost is currently linear (C7).** `AvatarDiscBindingTests`
re-implements `ConsentCatalogTests`' `#filePath` root-resolution rather than sharing it, and roughly
half of it is a hand-rolled brace-matching scanner. **FT-6** extracts the root-resolution + file-read
into one shared test helper in `CleansiaCoreTests` so the third guard is cheaper than the second.
(The panel **overruled** the framing that this "inverts the seam at `patterns-mobile.md:30-37`": that
rule governs **`:core` production code** reaching an app-specific generated client. A Core **test
target** reading app source as a fixture is the pre-existing, in-CI, reviewer-accepted
`ConsentCatalogTests` idiom, and the coupling is *deliberate* — D3's anti-vacuity requirement wants it
to fail loudly when an app file moves.)

### D5 — `check-consistency.mjs` is not a Swift enforcer; its `NOT RUN` banner is ratified, not re-decided

1. **The zero-file-scope false green is a real tool defect and it is already fixed** — on branch
   `fix/tooling-false-green-and-broken-docs` (`c9265298`), **not on `master`** as of 2026-08-01. This
   ADR does not claim credit for it and does not block on it: **FT-1 is re-scoped to verify the
   banner and close the finding when that branch merges.** The behaviour it establishes — a scope that
   matches zero files reports `NOT RUN`, never `OK` — is **ratified** here so it is not
   re-litigated, and is recorded in `enforcement.md` (Block C).
2. **`enforcement.md`'s "What's mechanical today" table gains an iOS row** stating plainly that
   `check-consistency.mjs` covers **no** Swift, so nobody infers coverage from the table's silence.
3. If a future ticket adds Swift to the walker it lands at **T2-ADVISORY** and may be *named* as an
   entry's enforcer at that tier — it just may not be labelled T1-CI until the checker is a required
   step in a workflow with a zero baseline.

---

## Exact catalog text, and who applies it

**Lane discipline (binding).** `agents/knowledge/patterns-*.md` is a shared-file lane as of the PM's
2026-07-30 ruling (`agents/backlog/INDEX.md:177`; the durable `shared-file-lanes.md` row is routed
through T-0456). `patterns-mobile.md` carries **uncommitted edits from live worktrees**. **The
architect does not edit it.** Block A below is applied by the ticket that owns the hunk, in that
ticket's worktree, touching **only its own hunk** (`shared-file-lanes.md` rule 2). Nobody runs
`git restore` on it (rule 3). **Nobody runs `git stash` — the stash is repo-global across worktrees.**

`conventions.md` and `enforcement.md` are lane-uncontended and are edited **with this ADR** by the
architect, per the charter's pattern-evolution loop (a decision without its enforceable catalog rule
is folklore). No `consistency.md` edit is required — that lane is T-0454 → T-0461.

### Block A — replaces the T-0451 entry in `agents/knowledge/patterns-mobile.md`

**Applier: the T-0451 iOS developer, in their own worktree, replacing their own hunk.** This is no
longer an "interim" application (C10): ADR-0032 is `accepted`, so the tier vocabulary below is
ratified and nothing is seeded ahead of the ruling.

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
> contrast-sweep ticket (FT-3).
> **Enforced by:** `CleansiaCore/Tests/CleansiaCoreTests/FixedWhiteContrastTests.swift` —
> `AvatarDiscBindingTests` binds `ProfileTab.swift` + `ProfileHubContent.swift` **by source** (brace-matched
> outward from `Text(initials)`, so it fails loudly on a restructure rather than drifting), and
> `FixedWhiteContrastTests` validates its own WCAG arithmetic against three reference pairs — **T1-CI**
> (CleansiaCore scheme, `ios-ci.yml:169`). Roster: **2 of the tree's theme-invariant surfaces**; residual
> enumerated by FT-3.
```

### Block B — moved to ADR-0033

The T-0441 cross-stack sentence edit belongs to the cross-stack-claim decision. It is specified in
**ADR-0033 §Block B**, with the same applier and the same lane discipline.

### Block C — applied with this ADR

`agents/knowledge/conventions.md` §"Harvest good patterns back into the catalog" gains **"The price of
a law"**; `agents/process/enforcement.md` gains the **iOS row**, the **tier vocabulary**, and the
**`NOT RUN`** record. Both are applied by the architect in this change — not deferred to a ticket.

---

## Alternatives considered

**A. Add `.swift` to the `check-consistency.mjs` walker.** *Rejected as a substitute for naming an
enforcer; not adopted now, but no longer rejected on the "it's only advisory" axis.*
- Genuinely the cheapest *code* change and it is where every other stack's rules live.
- It lands at **T2-ADVISORY** — the checker is in **no** stack's CI workflow today (verified: zero
  hits under `.github/`), so it is run by the Reviewer, the same human already reading the diff. Under
  the accepted D2, T2-ADVISORY is a **legitimate declared tier**, so a future Swift walker *may* be an
  entry's named enforcer; it simply may not be labelled T1-CI.
- It is not the one-liner it looks like: adding `.swift` to `checkMobile`'s walk feeds every Kotlin
  regex Swift source; `/Text\(\s*"[^"]+"/` (`:422`) matches SwiftUI `Text("…")` immediately, so it
  needs per-language rule gating **plus** a baseline sweep of every SwiftUI preview and literal in two
  apps — exactly the "enforcement in front of the cleanup" `enforcement.md:104-106` warns about.

**B. SwiftLint `custom_rules` as the single mechanism.** *Adopted for the rule shapes it can express
(D4).* Strongest argument in the set: it already runs and already blocks, so the marginal cost of a
rule is a YAML block, and `included`/`excluded` expresses "banned everywhere except the file that owns
it" — the `CleansiaWeb` case, whose baseline is verified zero. Rejected as the *only* mechanism: it is
single-line regex over one file, cannot reason across files, does not lint `.xcstrings`/plists, and its
`included:` roster today excludes the LiveActivity target and both app test targets (C6).

**C. Require a hand-written guard test as the price of declaring a law.** *Adopted as one available
mechanism; **rejected as the mandatory price** (C2/C7).* The idiom is proven twice and both run in CI,
and T-0451 shows the price *can* be one file inside an S-sized ticket. But one data point does not
price ~19 grandfathered laws; the harness is not shared, so cost is linear; and the mandatory form
collides with `enforcement.md`'s zero-baseline rule on exactly the entries that most need force.

**D. Restrict the catalog to descriptive guidance on unenforced stacks.** *Rejected.* It concedes the
catalog's job on the stack with two shipping apps. It also rests on a false premise: iOS is not
*unenforced*, it is **T3-HUMAN-enforced**. The honest fix is to make the tier **visible**. Its correct
half is now the **majority** of the accepted rule, not a fallback: most iOS laws will legitimately
carry T3-HUMAN with a named checklist item, and that is a real tier, not a downgrade.

**E. Rewrite all existing ungated iOS "ONE way" entries now.** *Rejected — big-bang.* They stand,
unchanged, until **FT-4** dispositions each into a tier. This ADR does not retroactively invalidate
them. **The grandfathering is rollout sequencing, not a permanent exemption** (the panel overruled the
"grandfathered forever" framing): FT-4 *is* the sweep, and it is now cheap — labelling ~22 entries
with an existing enforcer + tier, not writing ~19 test files. The one exception is the `CleansiaWeb`
entry, whose **verified overclaim** is a factual error fixed by **FT-2** rather than waiting on triage.

**F. Leave it as a reviewer convention / a companion-doc note.** *Rejected* on ADR-0018's and
ADR-0016's precedent: a rule that adds a standing reviewer gate and binds every future catalog edit is
an ADR + a catalog artifact, not folklore.

**G. Rely on the tool's loud `NOT RUN` banner instead of per-entry tiers.** *(Raised by the panel, not
by the author — the draft did not consider it.)* **Adopted in part; rejected as a substitute.**
- **Its correct half is real and already shipped.** A `--paths=` scope that matches zero files now
  reports `NOT RUN` instead of `OK` (branch `fix/tooling-false-green-and-broken-docs`, `c9265298`).
  That delivers *stack-level* coverage visibility to every reviewer, at the point of use, at **zero
  marginal cost per law** — strictly better than anything this ADR could have charged for it. It is
  ratified in D5, and FT-1 is re-scoped to verifying it rather than building it.
- **It cannot substitute, because it answers a different question.** `NOT RUN` answers *"does this
  tool read this stack?"*. It cannot answer *"does this entry's named enforcer assert what the
  sentence claims?"*. The `CleansiaWeb` overclaim is the decisive counterexample: its enforcer exists,
  runs in CI, and is **green**, while asserting two sentences against a tree-wide claim. No banner —
  loud, green, or red — surfaces that. Only D2's naming plus D3's coverage check does.
- The two therefore **compose**: stack-level tool honesty (`NOT RUN`) + entry-level enforcement
  honesty (D2/D3). Adopting G is not a reason to drop either.

---

## Consequences

**Cheaper / safer**
- A reader can tell, **from the entry**, what is watching and how hard — which nobody can tell today
  about ~19 iOS entries.
- The `CleansiaWeb` overclaim is closed, and "the enforcer asserts less than the sentence claims"
  becomes a reviewable finding rather than an invisible one.
- The strongest existing iOS guard idiom (compute the property, bind the call site by source, fail
  loudly on restructure) becomes the named house form for the cases that warrant it.
- **ADR-0018's human-enforced laws keep their force.** A T3-HUMAN law with a named checklist item is
  first-class, so the project's most load-bearing iOS rules are not relabelled guidance.
- **A law with a live violation is not disarmed.** `(gate pending: <ticket>)` states the truth and
  carries a promotion path, instead of forcing a softened sentence.

**More expensive (new obligations)**
- Every **new** catalog entry constraining call sites states an enforcer + a tier. That is a
  one-line label, not a test file.
- Where a rule *is* mechanizable with a zero baseline, T1-CI is now mandatory — a real tax on those
  specific cases, and the intended one.
- The Reviewer gains the check below; the Architect owns tier assignment and the FT-4 triage.

**What could go wrong (state it plainly)**
- **Label-shaped busywork.** `T3-HUMAN` becomes a rubber stamp appended to every entry, and the tier
  stops carrying information. Mitigated by requiring a **named** checklist item (an unnamed human
  enforcer is `(guidance — no gate)`), and by D3's coverage check — not eliminated.
- **`(gate pending:)` as an indefinite parking space.** A ticket named and never scheduled leaves a
  law permanently unenforced while reading as enforced-soon. Mitigated by the ticket being a real,
  filed id the PM schedules; the Reviewer treats an unresolvable id as a finding.
- **Roster rot.** Hand-maintained rosters (`discFills`, `SESSION_WIPE_ALLOW`, the two-hero list) do
  not catch the next instance. D3 makes the boundary visible; it does not close it.
- **The FT-4 triage stalls at ~22 entries.** The sweep is cheap per entry but not free in aggregate.
  It is sequenced after FT-2 proves the mechanism.

---

## How a reviewer verifies compliance

On any ticket whose diff touches `agents/knowledge/*.md`:

1. **Constrains call sites?** Ask the **semantic** question — *would this entry let a reviewer reject
   an alternative a competent developer could otherwise reasonably choose?* Imperative wording ("the
   ONE way" / "never" / "is a defect" / a "Deviations a reviewer rejects" list) is a **prompt**, not
   the test — an entry rewritten as "the canonical form is X" is still a law and still needs a tier.
   Prose about a shared component's own internals is **not** in scope.
2. **Enforcer + tier named inline.** `**Enforced by:** <named enforcer> — <tier token>`. A tier with
   no named enforcer is a finding. `T3-HUMAN` with no named checklist item is a finding (it is
   `(guidance — no gate)`).
3. **T1-CI where it is owed.** If the rule is mechanically expressible on that stack **and** its
   baseline is zero, the tier must be `T1-CI`. If the baseline is non-zero, the tier is
   `(gate pending: <ticket>)` with a **real, resolvable** ticket id — not `T3-HUMAN`.
4. **Coverage honest.** **Open the named enforcer and read what it asserts.** If the sentence claims
   more than the enforcer asserts, either the sentence narrows (with the residual stated) or the
   enforcer widens. *(This is the check that would have caught the `CleansiaWeb` entry — and the one
   no tool banner can perform.)*
5. **Non-vacuous.** A guard test that walks the tree fails on an empty corpus / missing anchor —
   `XCTUnwrap` the read, assert the anchor count. A SwiftLint `custom_rule` claiming tree-wide scope
   is checked against `.swiftlint.yml`'s `included:` roster.
6. **Lane.** The hunk was applied in the ticket's own worktree, touching only its own hunk. No
   `git restore` of a shared catalog file; **no `git stash`**.

---

## Roles affected

No new code roles. **Reviewer** gains the six-point check above. **Architect** owns tier assignment
and the FT-4 triage. **ios** charter gains the D4 mechanism choice (SwiftLint `custom_rules` vs XCTest
guard, by rule shape) and the `.swiftlint.yml` `included:`-scope check.

The living companion **`agents/architecture/decisions/catalog-governance.md`** is created in the same
change (per `deliberation.md` §"Parallel documentation"), carrying the tier table, the current tier
census, and the running list of which catalog entries sit at which tier.

---

## Follow-up tickets — specs, not files

**No ticket files created.** IDs are PM-allocated and T-046x is in active use; an invented id would
collide.

| # | Title | Layers / size | Panel? | Sequencing |
|---|---|---|---|---|
| **FT-1** | **Verify and close the `check-consistency.mjs` zero-file-scope `NOT RUN` banner.** The fix exists on `fix/tooling-false-green-and-broken-docs` (`c9265298`) and is **not** on `master` as of 2026-08-01. AC: on merge, `--paths=src/cleansia_ios` prints `NOT RUN` (never `OK`) and does not report a pass; a normal scoped run is unchanged; `check-consistency.test.mjs` covers the zero-file case. **Re-scoped by the panel from "build it" to "verify + close"** (C11). | tooling, **XS** | no | **does not wait for this ADR.** If the branch merges first, this is a verification-only close. |
| **FT-2** | **Bootstrap `custom_rules` in `.swiftlint.yml`, pilot the `CleansiaWeb` no-literal-domain rule, close its verified overclaim, and widen `included:`.** A `custom_rules` entry banning the literal domain in Swift with `excluded:` on `Config/CleansiaWeb.swift`; **widen `.swiftlint.yml` `included:` to cover `CleansiaCustomer/LiveActivity/` + both apps' `Tests/`, or state the residual scope in the entry per D3**; widen `ConsentCatalogTests` (or a sibling) to assert no `.xcstrings` value and no plist value in either app carries the literal domain; then narrow or confirm the `patterns-mobile.md` sentence to match. **Baseline verified zero** (one literal, `CleansiaWeb.swift:8`), so the T1-CI promotion is legal on day one. | ios, **S** | no | **ships regardless of this panel** (C6 concession) — it enforces an existing catalog rule and fixes a verified factual error. Needs the `patterns-mobile.md` lane only for the final sentence tweak. |
| **FT-3** | **iOS theme-invariant contrast sweep.** T-0451's deferred `## Out of scope` item: enumerate every opaque theme-invariant surface in the iOS tree, record each **with its ink pair and computed ratio**, extend the `AvatarDiscBindingTests` roster or add the paired Core token. AC includes recording the enumeration method so the count is reproducible. | ios, **S** | no | after T-0451 merges; `CleansiaColors.swift` + `FixedWhiteContrastTests.swift` lane behind T-0451. |
| **FT-4** | **Triage the iOS catalog laws into tiers — the REAL corpus.** **Re-scoped by the panel from 4 entries to ~22** (C1): every `patterns-mobile.md` entry framed "the ONE way" / "The ONE sanctioned way" / closing with a "Deviations a reviewer rejects:" list — verified as **22 + 1** "ONE way" lines and **~20** deviation lists, against **4** occurrences of `Tests` in 1093 lines. Each gets `**Enforced by:** <enforcer> — <tier>`. Expect the distribution to be mostly **T3-HUMAN** (named checklist item / a numbered reviewer-check), a few **T1-CI**, and `(gate pending: <ticket>)` for `CleansiaDangerButton` (live violation: `ProfileHubContent.swift:298-320`). `SnackbarPill` is likely component-internals prose (D2 scope), not a law. **This is a labelling sweep, not a gate-writing sweep** — that is what makes it affordable. | ios + architect, **M** | no — applies this ADR | after this ADR; after **FT-2** proves the mechanism. Runs **in the `patterns-mobile.md` lane**, in slices, never as one big-bang hunk. |
| **FT-5** | **Canonicalize partner `LogoutRow` onto `CleansiaDangerButton`, then promote the entry's tier.** `CleansiaPartner/Sources/Features/Profile/ProfileHubContent.swift:298-320` hand-rolls the component. AC: the call site consumes Core's `CleansiaDangerButton`; the baseline reaches zero; the catalog entry's tier moves `(gate pending: FT-5)` → `T1-CI` with the gate landing in the same change. | ios, **S** | no | this is the ticket `(gate pending:)` names. Any order vs FT-4; FT-4 records the pending tier, FT-5 discharges it. |
| **FT-6** | **Extract a shared test-tree-root helper in `CleansiaCoreTests`.** `ConsentCatalogTests:16-22` and `AvatarDiscBindingTests` each re-implement the `#filePath` walk-out-of-the-package + file read. One helper (`SourceTree.iosRoot()` / `SourceTree.read(_:)`) with the `XCTUnwrap`-on-missing behaviour D3 requires, so the **third** guard costs less than the second (C7). | ios, **XS** | no | after T-0451 merges (it is the second call site). |
| **FT-7** | **Rename ADR-0032's file to match its amended title** (`git mv` + a link sweep across `agents/**`). The slug still reads `…-require-a-named-ci-gate`, which the panel amended. | docs, **XS** | no | any time. |

---

## What this ADR does **NOT** decide

- **It does not rule on the substance of the T-0451 fix.** That was settled by the owner's iOS↔Android
  convergence ruling plus Android's shipped, commented deviation, under the ticket's AC4.
- **It does not decide which catalog edits a developer may make inline, or how strong a cross-stack
  claim may be.** Those are **ADR-0033**.
- **It does not enumerate the theme-invariant surfaces** — FT-3.
- **It does not add Swift to `check-consistency.mjs`.** It rules that doing so lands at T2-ADVISORY,
  which is now a legitimate declared tier; whether to do it is a separate, cheaper call.
- **It does not write any SwiftLint `custom_rules` or any guard test** — FT-2's.
- **It does not re-open** T-0274, T-0441's reviewer verdict, T-0446's SEC-5 routing, ADR-0016, or
  **ADR-0018** (it *reads* ADR-0018 as the T3-HUMAN precedent; it changes nothing in it).
- **It does not claim credit for, or block on, the `NOT RUN` fix** — that shipped on its own branch and
  is ratified, not decided, here.
- **It does not touch `consistency.md`, `security-rules.md`, `INDEX.md`, `quality-gates.md`, or any
  `patterns-*.md` file.** Those are lane-held.
- **It does not change the Reviewer's authority.** A reviewer may still reject a catalog hunk on
  content.

---

## Challenge

> **Provenance.** C1–C11 were filed by the challenger instance in the panel thread; the ADR's
> `## Challenge` section was empty at adjudication, so the **lead transcribed them into the artifact**
> (condensed, with the lead's own re-verification appended in brackets) so the deliberation trail lives
> in the record per `deliberation.md` §"The output handed to developers". Wording is the challenger's
> where quoted; verification is the lead's.

**C1 — the corpus premise is wrong by ~5×.** §Context names **four** ungated iOS "ONE way" entries.
`patterns-mobile.md` carries **24** occurrences of "the ONE way" and 8 of "is a defect" — eighteen more
law-entries, two closing with explicit "Deviations a reviewer rejects" lists. Only three name a test.
Re-scope FT-4 to the real corpus, or name which entries are grandfathered **forever** and defend why
the reader-visibility argument does not apply to them.
*[Lead re-verified: **22** lines match "the ONE way" + 1 "The ONE sanctioned way" (`:191`), and **~20**
entries close with "Deviations a reviewer rejects:". `Tests` appears **4 times in 1093 lines**. The
challenger's direction is right and the magnitude is if anything understated.]*

**C2 — the headline: the ADR's own cited precedent contradicts D2.** ADR-0018 is `accepted` and
enforced **entirely by a human checklist** (Gate-DP) — D1's T3-HUMAN, the tier D2 declares insufficient
for a law. ADR-0018's own rejected alternative states the precedent as *"an ADR + a checklist
artifact"*. Worse, its progeny are ungateable **in principle**: *"this screen's layout, flow and
branding match the Android screen"* cannot be asserted by SwiftLint or XCTest, so under D2 the
project's most load-bearing iOS rules must be relabelled guidance. **Cheaper alternative:** require a
**named enforcer at a declared tier** (T1-CI / T2-ADVISORY / T3-HUMAN-with-a-named-checklist-item)
rather than requiring T1-CI. That buys the whole stated benefit for a label instead of a test file. If
accepted, D2 collapses into D3 and most of C3/C4/C5/C9 dissolve with it.

**C3 — D2 is anti-correlated with need.** The `CleansiaDangerButton` entry names its own live
violation (partner `ProfileHubContent.swift:298-320` hand-rolls the component), and
`enforcement.md:104-106` is categorical that a check becomes blocking only once its baseline is zero.
So the ADR's own exemplar of an unenforced law **cannot be gated**, and its only permitted move is to
be softened to guidance. The rules whose imperative force is actually doing work are exactly the ones
the mechanism disarms. **Remedy:** a `(gate pending: <ticket>)` tier for a law whose only blocker is a
known, ticketed violation.
*[Lead re-verified `LogoutRow` at `:298-320`: `error` glyph+label, `error.opacity(0.12)` fill,
`error.opacity(0.4)` hairline — the component's visual, hand-rolled.]*

**C4 — the reviewer procedure is keyed on wording; the routing test is semantic.** A law rewritten as
"the canonical form is X" dodges both the gate and the panel, and nothing detects it.

**C5 — the semantic test has no floor.** `conventions.md:132` sets the catalog-entry bar at "makes the
codebase **more consistent**", which *is* forbidding the inconsistent alternative — so the semantic
test fires on nearly every entry that earns its place. Route-everything or launder; neither is the
harvest loop `conventions.md` opened.

**C6 — the SwiftLint tier is over-claimed, but FT-2 should ship regardless.** The domain rule is
genuinely regex-shaped with a **zero baseline** (exactly one literal in the tree), so FT-2 should ship
independent of this panel. But the ADR measures SwiftLint covering **1 of 4** triaged rules, not
"most"; and `.swiftlint.yml`'s `included:` excludes the LiveActivity target and both app test targets —
so a custom rule cannot honestly claim "anywhere in the iOS tree" under the ADR's own D3.
*[Lead re-verified: `.swiftlint.yml:1-5` includes only Core/Sources, Core/Tests, Partner/Sources,
Customer/Sources; `CleansiaCustomer/LiveActivity/` (1 file) and both `Tests/` trees (65+ files) are
unlinted. The literal `cleansia.cz` occurs exactly once, at `CleansiaWeb.swift:8`.]*

**C7 — the XCTest tier is priced from one unrepresentative data point.** The exemplar guard is 191
lines, roughly half a hand-rolled brace-matching scanner, with its root-resolution copy-pasted from
another test and **no shared harness** — so cost is **linear in laws, not amortized**. And it hardcodes
both app trees' file paths **inside the shared Core package's test target**, inverting a seam
`patterns-mobile.md:30-37` explicitly protects.

**C8 — this is three decisions; `adr/README.md:3` says split.** The routing test (all stacks), the
price + iOS tier mechanics (iOS), and cross-stack claim strength (independent). Concrete harm: a lead
who wants D3 + D6 + D7 but **not** D2 — the likeliest correct outcome — cannot record that.

**C9 — apply the ADR's rule to the ADR (sharpest).** Its own enforcer is six human verification steps —
T3-HUMAN. A mechanical form would live in `check-consistency.mjs`, which the ADR itself rules
T2-ADVISORY, so it could not discharge itself; that tool appears in **no** CI workflow; and it could
not be promoted, because the ADR grandfathers ~18–21 violations and `enforcement.md` demands a zero
baseline. So the ADR is a law that cannot name a T1-CI gate **for precisely the reason it refuses to
accept from `CleansiaDangerButton`**. Either file the catalog-gate-checker with a stated path to
blocking, or mark its own rule `(guidance — no gate)` and defend why a governance rule may sit at a
tier its subjects may not.
*[Lead re-verified: `check-consistency` appears in **zero** files under `.github/`.]*

**C10 — the interim edit seeds unratified vocabulary.** D7-interim's Block A carries `T1-CI`,
vocabulary that exists only if D1/D2 are accepted, into the catalog ahead of the ruling. **Remedy:**
`**Enforced by:** <path>` with no tier token until acceptance.

**C11 — the §Context premise expires this week, and the shipped fix is an unconsidered alternative.**
The challenger could not confirm whether the consistency-checker fix is live. **The panel lead ran it:
it is not.** `git merge-base --is-ancestor c9265298 master` → **false**; `master`'s
`check-consistency.mjs` has zero occurrences of `isAbsolute` and zero of `NOT RUN`. The fix is real but
sits unmerged on `fix/tooling-false-green-and-broken-docs`. So §Context stands **as written for
today** and will be false the moment that branch merges — which is imminent. Further: a loud `NOT RUN`
delivers the ADR's own headline benefit — *"a reader can tell whether anything is watching"* — **at the
point of use, to every iOS ticket's reviewer, at zero marginal cost per law**. That is Alternative D's
correct half without D2's tax, it is **not among Alternatives A–F**, and it shipped without this ADR.
D2's marginal value must be argued against that baseline.

**Process note on the author's agenda.** The draft closed with a four-item "named agenda for the
panel". The lead treats it as **self-assessment, not the attack surface**: none of its four items is
C1, C2, C3 or C9 — the four that actually decided the outcome. Agenda item 1 (affordability) is
answered by the amended D2; item 2 (breadth vs bindingness) is answered by Alternative A's re-ruling
and Alternative G; items 3 and 4 belong to **ADR-0033**.

## Defense

**No `## Defense` was filed.** The author instance did not return to rebut, concede, or escalate.
`deliberation.md` §5 is explicit: *"each challenge is RESOLVED (defended or fixed) or it BLOCKS"* — and
§"What 'defended' means": *"A REBUT must cite evidence — 'I disagree' is not a defense."* Silence is
not a defense.

Rather than block the whole artifact, the lead **re-verified every challenge against the tree
independently** (the bracketed notes in §Challenge are that verification) and, where the evidence
sustained a challenge, **folded the concession into the artifact on the author's behalf** — the
CONCEDE + REVISE path of §3, executed by the adjudicator. Where the evidence did **not** sustain a
challenge, the lead defended the draft *against* the challenger. The specific defenses of the draft
are recorded as **OVERRULED** rows in the verdict below, with their evidence.

The one thing a lead may not do is invent new decision content and call it consensus. The amended D2
is **the challenger's own proposed alternative**, verbatim in substance and strictly weaker than the
author's — so it sits inside the space both parties argued, and adopting it is adjudication, not a
third position. D3, D4 and D5 are the author's, unchanged in substance.

## Verdict

**Consensus: reached, with amendments. Status: `accepted` for the amended decision recorded above.
Zero blocking challenges remain in this ADR. The split-off decision is ADR-0033.**

| # | Disposition | Reason (one line) |
|---|---|---|
| **C1** | **SUSTAINED** (framing partly overruled) | The corpus is ~22 laws with ~3 named enforcers, not 4 — §Context corrected and FT-4 re-scoped to the real corpus; **overruled** on "grandfathered *forever*": Alternative E is rollout sequencing per `enforcement.md:104-106`, and FT-4 *is* the sweep — cheap now, because the amended D2 prices a label, not a test file. |
| **C2** | **SUSTAINED — decisive** | ADR-0018 is an `accepted`, load-bearing iOS law enforced entirely at T3-HUMAN, and its own rejected alternative names *"an ADR + a checklist artifact"* as the precedent — so the draft cited its precedent for the opposite of what the precedent holds; **D2 is amended to the challenger's form** (named enforcer at a declared tier; T1-CI required only where mechanizable **and** baseline-zero). |
| **C3** | **SUSTAINED** (universality overruled) | `LogoutRow` at `ProfileHubContent.swift:298-320` is verified live, and `enforcement.md:104-106` forbids gating a non-zero baseline, so the draft forced its own exemplar to be softened — **`(gate pending: <ticket>)` adopted** (D1/D2) + FT-5 filed; **overruled** on "the rules doing the work are *exactly* the ones disarmed" — `CleansiaWeb` is a law doing work with a verified zero baseline, so the anti-correlation is real but not universal. |
| **C4** | **SUSTAINED against the draft; DISSOLVED by the amendment** | Under the draft, "the canonical form is X" dodged the gate; under the amended D2 every constraining entry names a tier **whatever its wording**, so laundering buys nothing — and reviewer check #1 is rewritten to key on the semantic property with wording as a prompt only. |
| **C5** | **SUSTAINED** | `conventions.md:132`'s "more consistent" bar does make the semantic test fire on nearly everything; the **floor** is written into **ADR-0033 §D1 test 2** (it fires on a *narrowing* of latitude the catalog previously left open, not on the first statement of a canonical form where none existed) — and because that floor is lead-authored rather than argued by either party, ADR-0033 is `proposed`, not accepted. |
| **C6** | **SUSTAINED + CONCEDED** | Verified: `included:` omits `LiveActivity/` and both `Tests/` trees, so a `custom_rule` may not claim "anywhere in the iOS tree" (D4 amended, FT-2 re-scoped to widen it or state the residual); the unevidenced *"most real laws need both"* is struck (**1 of 4**); and the zero baseline (one literal at `CleansiaWeb.swift:8`) is conceded — **FT-2 ships regardless of this panel**. |
| **C7** | **SUSTAINED IN PART; seam claim OVERRULED** | The duplicated `#filePath` root-resolution and absent harness are real and make cost linear → **FT-6** files the shared helper; **overruled** on the seam: `patterns-mobile.md:30-37` governs **`:core` production code** reaching an app-specific generated client, whereas a Core **test target** reading app source as a fixture is the pre-existing, in-CI, reviewer-accepted `ConsentCatalogTests` idiom whose coupling D3's anti-vacuity rule **deliberately wants**. |
| **C8** | **SUSTAINED IN PART — split executed, into two not three** | `adr/README.md:3` is categorical and the harm is real, so the ADR is split; **overruled** on "three": draft-D6's test 3 (*"a prescriptive claim about a stack this ticket did not build"*) **is** D7, so routing + cross-stack strength are one decision → **ADR-0033**. |
| **C9** | **SUSTAINED — the reductio that decided C2** | Verified `check-consistency` in **zero** `.github/` files; under the draft this ADR could not have discharged itself, which is an exemption, not a rule. Under the amendment it self-labels honestly: **T3-HUMAN**, enforcer = the six-point reviewer check; a T2 mechanical assist is *optional*, not required (the "file the catalog-gate-checker" remedy is **not** imposed — a checker that cannot express D3's read-the-enforcer step would be theatre). |
| **C10** | **SUSTAINED as filed; MOOTED by acceptance** | The draft did seed unratified `T1-CI` vocabulary via a pre-acceptance interim; with ADR-0032 `accepted` the vocabulary is ratified, **D7-interim is deleted**, and Block A applies under the accepted rule — nothing is applied ahead of a ruling. |
| **C11** | **SUSTAINED on premise-expiry; OVERRULED IN PART on subsumption** | The lead confirmed `c9265298` is **not** an ancestor of `master` and the banner is imminent, so §Context is rewritten to be true on both sides of the merge and **FT-1 is re-scoped to verify-and-close**; the fix is recorded as **Alternative G, adopted in part**. **Overruled** on the claim that `NOT RUN` delivers the ADR's headline benefit: it answers *"does this tool read this stack?"*, not *"does this entry's enforcer assert what the sentence claims?"* — `CleansiaWeb`'s enforcer exists, runs in CI, and is **green** while asserting two sentences against a tree-wide claim, and **no banner of any colour surfaces that**. Stack-level and entry-level honesty compose; neither substitutes. |

**What the panel accepted, precisely:** D1 (tiers, with the tier bound to *where the check runs* rather
than to the tool), D2 **as amended** (named enforcer + declared tier; T1-CI where mechanizable and
baseline-zero; `(gate pending:)` where a ticketed violation blocks it; T3-HUMAN requires a *named*
checklist item), D3 **unchanged** (coverage + anti-vacuity — called the strongest, cheapest, least
contestable part by both sides, and re-confirmed by the lead as the one requirement no tool banner can
replace), D4 **amended** for the `included:` scope and the struck quantifier, D5 **amended** to ratify
rather than claim the `NOT RUN` fix, and Alternative G **added and answered**.

**What the panel rejected:** the draft's D2 (T1-CI-or-downgrade), and D7-interim as a pre-acceptance
application path.

**What the panel did not settle here:** the catalog-edit routing test and cross-stack claim
strength — moved intact, with C4/C5's fixes, to **ADR-0033**.

**Escalations to the owner:** none. Every disagreement resolved on in-repo evidence; nothing here
carries lasting business impact requiring an owner ruling.

**Re-check obligation.** Per `deliberation.md` §4, the challenger instance may re-check this amended
artifact. The amendments are concessions to the challenger's own proposals plus five overrules with
their evidence stated; a new hole in the *amended* text is a new challenge, not a re-litigation of
C1–C11.
