# Catalog Governance — living decision doc

**Topic:** how the `agents/knowledge/*` catalog acquires, states, and enforces its rules.
**ADRs:** [ADR-0032](../../backlog/adr/0032-catalog-law-declarations-require-a-named-ci-gate.md)
(`accepted` 2026-08-01 — the price of a law) ·
[ADR-0033](../../backlog/adr/0033-catalog-edit-authority-the-routing-test-and-cross-stack-claim-strength.md)
(`accepted` 2026-08-05, amended by the T-0471 panel — catalog-edit authority) ·
[ADR-0018](../../backlog/adr/0018-ios-design-parity-principle.md) (the T3-HUMAN precedent).
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

### What routes to the Architect (ADR-0033, accepted 2026-08-05)

Three ordered tests; first to fire routes: **(1)** does it put shipped code in violation? **(2)** does
it *narrow* latitude the catalog previously left open? **(3)** does it make a *prescriptive* claim
about a stack the ticket never built and ran? Otherwise: inline, flagged in `## Review`.

**The floor on test 2, in its amended (T-0471) form** — the four sentences that carry the weight:

1. **The unit is a *sentence*, not a topic.** Test 2 fires when a catalog sentence **already governs
   this entry's subject at any level of generality** and the entry carves an exception out of it,
   replaces it, or forbids a form it named.
2. **Silence is defined.** "The catalog was silent about X" = *no sentence covers X at any level of
   generality* — **not** *no sentence names X specifically*. This is what closes the sub-case dodge
   (T-0451's "theme-invariant surfaces" is a sub-case of `patterns-mobile.md:577`'s rule about which
   token supplies ink; the phrase being new does not make the catalog silent).
3. **Claiming the floor costs one grep, and the default is route.** The author records the catalog
   file(s) + term searched in `## Review`, symmetric with test 1's code sweep. **A floor claimed with
   no search is not claimed.**
4. **Inline is not free.** A floor-qualifying entry has a **zero baseline by construction**, which is
   ADR-0032's second `T1-CI` condition — so a mechanizable rule owes its gate in the same ticket, and a
   mechanism that cannot fail a build is `T2-ADVISORY` however it is labelled.

**Enforced by:** reviewer-check **5 "Catalog-edit routing"** (`.claude/agents/reviewer.md`) —
**T3-HUMAN**. ⚠️ **Not yet landed: FT-11.** Until it does, the reviewer's charter still teaches the
superseded axis, so **FT-8 (the `conventions.md` text) is sequenced behind FT-11** and
`conventions.md:125-127` remains what a reviewer actually applies.

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

**Where the floor's first wording broke, and why the current one is different (T-0471, 2026-08-05).**
The draft floor turned on *"a form the catalog previously **permitted**"*. The catalog does not permit;
it prescribes — so "permitted" was inferable only from silence, which is the *same* condition that
routes an edit **inline**. The predicate was true and false of the same edit, and it split the ADR's
own founding case: retro row 3 claims T-0451 withdrew `Color.dynamic` ink, but the sentence that
"permitted" it (`patterns-mobile.md:577`) is a descriptive mapping row that never mentions
theme-invariant surfaces. Read one way, C5 was unrepaired; read the other, the T-0451 refusal that
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

| Position on the floor | What it costs | Why not |
|---|---|---|
| No floor (C5's unfloored test 2) | everything routes | the inline lane dies; `conventions.md`'s harvest loop closes |
| Floor on "previously **permitted**" | nothing | undecidable — the catalog prescribes, so "permitted" collapses into "silent" |
| Floor on the **topic** ("has the catalog discussed X?") | nothing | every sub-case is a fresh topic if you pick the vocabulary; T-0451 escapes as "theme-invariant surfaces" |
| **Floor on a governing *sentence* at any level of generality** ← **chosen** | one grep, recorded | residual: the author may search at the wrong level of generality. Mitigated by route-by-default, not eliminated |
| Route every first statement (Alternative B) | the harvest loop | rejected in the original panel; nobody argued for it in the re-check |

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
| `**Enforced by:**` in `agents/knowledge/` | **8** — `patterns-backend.md:638`, `:729`; `patterns-frontend.md:462`; `consistency.md:332`, `:395`; `roles/post-commit-effects.md:32`; `roles/order-availability.md:130`; + the template at `conventions.md:141` |
| `**Enforced by:**` in `patterns-mobile.md` — the ~22-law file ADR-0032's Block A and FT-4 target | **0** |
| Catalog entries added to `patterns-mobile.md` *after* ADR-0032 was accepted, carrying no enforcer + tier | at least **1** — `:265-276` (T-0473), which constrains call sites (*"hoist it one level further"*) and forbids a form (*"not a whole-file `contains`"*) |

The T-0473 entry's ticket self-classified as *"a testability clarification, not a redefinition"*
(`T-0473-…md:337-339`) — the same self-classification as T-0274 (`:133`), two sprints later, on the
same failure mode. **That is the empirical case for FT-11:** a governance rule whose only home is a
page the checker does not read is folklore with a citation. The reviewer's charter
(`.claude/agents/reviewer.md:105-110`) is what a reviewer actually runs, and it still teaches the axis
ADR-0033 replaces.

---

## Open items

| Item | Owner | Where |
|---|---|---|
| ~~The floor on ADR-0033's test 2 needs one adversarial round~~ — **CLOSED 2026-08-05** (T-0471): challenged, amended M1–M6, `accepted` | architect panel | ADR-0033 §Verdict · `adr/challenges/0033-floor.md` |
| **FT-11 — land the named enforcer (Block D):** reviewer-check 5 "Catalog-edit routing" in `.claude/agents/reviewer.md` + a Gate 1 pointer. **Blocks FT-8.** Until it lands, ADR-0033 is `(guidance — no gate)` in fact | architect + docs | ADR-0033 §Block D / §Follow-ups |
| **FT-12 — record the check id in `enforcement.md`** so dropping reviewer-check 5 is a visible regression | architect + docs | ADR-0033 §Follow-ups |
| **FT-8 — Block C into `conventions.md`** (amended floor wording + its own `Enforced by:` line) — **sequenced behind FT-11** | architect + docs | ADR-0033 §Block C |
| **F1 — ADR-0032's Number note is false** (0031 *is* on `master`, `acf2f0bc`/PR #175). `accepted` ⇒ needs a **signed erratum**, not an in-body edit (`adr/README.md:16-26`) | PM → architect | ADR-0032 `:23-25` |
| **F2 — ADR-0032's Block A was never applied**; `**Enforced by:**` has 0 instances in `patterns-mobile.md`, so FT-4 has nothing to build on | PM → ios lane | ADR-0032 §Block A |
| **F3 — `patterns-mobile.md:265-276` (T-0473)** carries no enforcer + tier and its test-1 sweep was never run (14 source-reading guard tests exist; none audited for the withdrawn whole-file shape). **Recorded, not re-opened** — T-0274 precedent | PM | `adr/challenges/0033-floor.md` §Findings |
| **F4 — no "carries a trade-off ⇒ ADR" limb** in the routing test (the ground three reviewers used on T-0397) | PM → architect | CH-6, deliberately out of T-0471's scope |
| **F5 — `patterns-mobile.md:1230` still grants the `.medium` detent** that the T-0397-ratified `:985-990` withdrew | PM → `patterns-mobile.md` lane | found while verifying retro row 6 |
| FT-1 verify the `NOT RUN` banner on merge of `fix/tooling-false-green-and-broken-docs` | tooling | ADR-0032 §Follow-ups |
| FT-2 `custom_rules` bootstrap + `CleansiaWeb` overclaim + widen `included:` | ios | ADR-0032 §Follow-ups |
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
