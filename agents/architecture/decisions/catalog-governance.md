# Catalog Governance — living decision doc

**Topic:** how the `agents/knowledge/*` catalog acquires, states, and enforces its rules.
**ADRs:** [ADR-0032](../../backlog/adr/0032-catalog-law-declarations-require-a-named-ci-gate.md)
(`accepted` 2026-08-01 — the price of a law) ·
[ADR-0033](../../backlog/adr/0033-catalog-edit-authority-the-routing-test-and-cross-stack-claim-strength.md)
(`proposed` — catalog-edit authority) · [ADR-0018](../../backlog/adr/0018-ios-design-parity-principle.md)
(the T3-HUMAN precedent).
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
| `T2-ADVISORY` | no | `check-consistency.mjs` **today, on every stack** (verified: zero hits under `.github/`) |
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

### What routes to the Architect (ADR-0033, proposed — not yet in force)

Three ordered tests; first to fire routes: **(1)** does it put shipped code in violation? **(2)** does
it *narrow* latitude the catalog previously left open? **(3)** does it make a *prescriptive* claim
about a stack the ticket never built and ran? Otherwise: inline, flagged in `## Review`.

Until ADR-0033 is accepted, `conventions.md:125-127` governs routing unchanged.

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
repair: **adding** a canonical form where the catalog was silent is inline; **withdrawing** a form the
catalog permitted is a law. That floor is the one item still awaiting an adversarial round.

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
  **no** `.github/` workflow.
- The XCTest guard idiom (`#filePath` walk out of the package) is duplicated per guard with no shared
  harness — FT-6 extracts it so the third guard costs less than the second.

---

## Open items

| Item | Owner | Where |
|---|---|---|
| The floor on ADR-0033's test 2 needs one adversarial round | architect panel | ADR-0033 §Challenge |
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
