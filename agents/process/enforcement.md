# Enforcement — Making Rules Mechanical, Not Advisory

A rule in a Markdown file is a strong suggestion; a build that fails is a law. This document is the
plan and the current state for turning the team's conventions into **machine-checked gates** so
consistency survives even when an agent (or human) doesn't read carefully. The principle:
**deterministic beats diligent.** Anything a tool can check, a tool should check.

## What's mechanical today

| Layer | Tool | Covers | Status |
|---|---|---|---|
| Build correctness | `dotnet build` + `dotnet test` (CI: `backend-ci.yml`) | compile, unit/integration tests | **live in CI** |
| Frontend build | `nx build` (CI: `frontend-ci.yml`) | the 3 apps compile | **live in CI** |
| Formatting/style (C#) | `/.editorconfig` (root) | file-scoped namespaces, braces, unused usings, nullability warnings | **added — surfaces as warnings** |
| Formatting/style (TS) | `src/Cleansia.App/.editorconfig` + ESLint (`eslint.config.mjs`) | TS formatting + lint | **present** |
| Project-specific rules | `agents/tools/check-consistency.mjs` | the A/B/C/D/E rules in `knowledge/consistency.md` no linter knows | **added — run by Reviewer** (in **no** CI workflow yet — verified: zero hits under `.github/`) |
| Cross-stack offerability parity (ADR-0037 D7 layer 2) | `agents/tools/check-available-status-parity.mjs` (CI: `offerability-parity.yml`) | the canonical C# `OrderAvailability.OfferableStatuses` vs all 8 partner-client status literals — **query literals AND take-button gates** — across TS, Kotlin **and Swift** | **live in CI — T1-CI** (its own repo-root workflow; baseline now empty — all 8 surfaces gated strictly) |
| iOS (Swift) | `swiftformat --lint` + `swiftlint lint --strict` (pinned 0.60.1 / 0.65.0) + 3 XCTest schemes (CI: `ios-ci.yml`) | formatting, lint, and whatever the guard tests assert. **`check-consistency.mjs` covers NO Swift** — its walker globs `.cs`/`.ts`/`.kt` only | **live in CI — T1-CI** (lint + tests). **Project-specific rules exist**, declared under `custom_rules:` in `src/cleansia_ios/.swiftlint.yml` — each `severity: error` and therefore CI-blocking under `--strict`. **Read the file for the roster; it is not enumerated here.** This sentence said "two" for one afternoon: it was corrected from "none" at 17:05 and a third rule landed at 18:32, which is the decay class `conventions.md` §"A claim about the tree carries its own retirement condition" exists to stop — and its own shape rule, *never enumerate a count of tree instances*, is what this sentence now obeys. ⚠️ **A `custom_rule` only reaches what `included:` lints** (`:1-5`): `CleansiaCore/Sources`, `CleansiaCore/Tests`, `CleansiaPartner/Sources`, `CleansiaCustomer/Sources` — so `CleansiaCustomer/LiveActivity/` and **both apps' `Tests/`** are outside every one of them |

## The consistency checker — `agents/tools/check-consistency.mjs`

Dependency-free Node (runs on the Windows dev box **and** ubuntu CI — the repo already uses Node 22).
It line-scans source for the project-specific rules that ESLint/analyzers can't express:

- **Backend:** A1 (paged query inherits `DataRangeRequest`), A5 (no hand-built `PagedData`), B1
  (no raw-scalar command return), B3 (validator inherits `AbstractValidator`), B5 (`Error` code is a
  field name, not `nameof(Command)`), B10 (no direct `dispute.Close/Escalate/Resolve` outside the
  T-0172 transition-guard allowlist), and a `dynamic` ban.
- **Frontend:** C1 (facade extends `UnsubscribeControlDirective`, no `DestroyRef`), C2 (no
  `BehaviorSubject`), C3 (`.subscribe()` has `takeUntil(this.destroyed$)`), C7 (component is OnPush),
  D2 (forms use `fb.nonNullable.group`), and an `any` ban.
- **Mobile:** E1 (no flag-bag `data class …UiState`), E3 (`@HiltViewModel`), E5 (repo returns
  `ApiResult<T>`, not a nullable body), E6 (ViewModel flows use `collectAsStateWithLifecycle`), a
  hardcoded-`Text("…")` ban, and **E9 (WARN-only)** — a `@Singleton` cache holder not in the
  `SessionScopedCache` wipe set (S11; see below).

```bash
node agents/tools/check-consistency.mjs              # all stacks; exit 1 on any violation
node agents/tools/check-consistency.mjs backend      # one stack
node agents/tools/check-consistency.mjs --warn       # report but exit 0 (use during rollout)
node agents/tools/check-consistency.mjs --paths=src/Cleansia.Core.AppServices/Features/Orders   # scope to a diff
```

The checks are **heuristic and line-based** — a clean run is *necessary, not sufficient*; the
Reviewer still reads the diff. They are intentionally tuned to minimize false positives (e.g. E6
only flags *ViewModel* flows, not a sheet's local `mutableStateOf`; B1 allows a bare `ICommand` for
operations with nothing to return and only flags raw-scalar returns).

### E9 — session-wipe-set membership (S11) — WARN-only, plus a specified hard gate

The `SessionScopedCache` wipe rule (`security-rules.md` **S11** / `consistency.md` **E9**) recurred 5+
times (`PushTokenRepository`, `NotificationFeedCache`, `UserProfileStore`, customer `UserRepository`,
and the T-0416 Dashboard/Orders/Invoices/Profile/OrderChecklist/NotificationPreferences stragglers)
with no mechanical guard. A *full* static check ("is this `@Singleton` per-user AND not in the
multibinding?") needs Kotlin/Swift **type-graph resolution** — cross-file constructor-injection and
supertype analysis — which this dependency-free line-scanner cannot do. So E9 is deliberately **two
layers**:

- **Live now — E9 warn-only advisory** (`check-consistency.mjs`, mobile): flags a `@Singleton` whose
  body declares a cache field (`MutableStateFlow<` / `DataStore<` / `Staleness()`) but whose class
  declaration does **not** name `SessionScopedCache`, cross-checked against a reason-annotated allowlist
  (`SESSION_WIPE_ALLOW` — mirrors the `consistency.md` E9 table; **keep them in sync**). It prints under
  a `consistency: N advisory warning(s) (non-blocking)` header and **never sets the exit code**, because
  it has known blind spots (a Room-DAO- or other-backed per-user cache with no matching field regex slips
  past). It is a *prompt for the Reviewer*, not a gate. Covered by `check-consistency.test.mjs` (E9 cases).
- **Specified, NOT yet built — the hard gate:** a **roster-equality assertion test**
  (`SessionScopedModuleTest` per Android app, `SessionScopedCacheRegistryTest` on iOS) asserting the
  production wipe set **equals** a hardcoded expected roster, so a forgotten new per-user repo fails a
  real test. Today's `AuthRepositoryTest` / `PushLogoutClearsTests` only exercise `clearAll()` with an
  *injected* set — they do not check the real multibinding's membership. **Follow-up ticket to file:**
  *"Add SessionScopedCache roster-equality tests (Android per-app + iOS) — the S11/E9 hard gate"*
  (`layers: [mobile]`, small; architect-signed rule already in place).

### S12 — user-artifact content (upload intake) — mechanically checkable **in parts**, and only in parts

`security-rules.md` **S12** (ADR-0043) is the answer to T-0460 AC5, recorded here so nobody has to
infer it: **the rule is partly mechanical and partly not, and it must never be labelled `T1-CI`
wholesale.** The authoritative per-clause table is in S12 itself; the summary is:

- **Enforced today (`T1-CI`, `Cleansia.Tests`, a named step of `backend-ci.yml:69-71`)** — the served
  type is a closed set on the read path (`ServedContentTypeTests`, `SasResponseHeaderOverrideTests`,
  `EmployeeDocumentDownloadContentTypeTests`, `EmployeeDocumentDownloadDispositionTests`); the intake
  roster **enumerates** every upload route (`UploadIntakeRosterTests`, count-asserted first); the scrub
  removes metadata from the bytes actually handed to the blob client (three per-pipeline suites); the
  scrub dispatches on bytes and reports honestly (`ImageMetadataDispatchTests`); orientation degrades
  without guessing (`JpegMetadataScrubTests`); the avatar exemption is honoured
  (`UpdateCurrentUserAvatarScrubExemptionTests`).
- **Specified, not built — `(gate pending: T-0458)`** — accepted-set ⊆ servable-set; the roster's
  `audience` / `scrub` columns; the decoder **package** denylist; the decoder **call-site** scan.
  ⚠️ **The roster's annotation is enforced by nothing today.** `UploadIntakeRosterTests.cs:66-68`
  splits each row on `" — "` and compares index `[0]`, so everything after the dash is asserted
  nowhere. Adding two columns without changing that assertion adds a string no test reads — the
  `T1-CI` claim would be false the day it is written.
  ⚠️ **And the replacement gate must not pass vacuously.** `Assert.False(result.IsValid)` is green on
  any un-stubbed constructor dependency, so a per-intake refusal theory owes (a) an assertion on the
  **identity** of the failure (that route's error code / the file property) and (b) a **positive
  control** per case — the same command with an accepted payload validating clean.
- **No mechanism at all — `(guidance — no gate)`** — the avatar exemption's *expiry* (an avatar URL
  appearing on a cross-user DTO). A wire-surface assertion in the `PayoutDtoSurfaceTests` shape would
  close it; **that ticket is owed** and is named in S12 rather than left implicit.

**If T-0458 cannot build the call-site scan, that clause is re-declared `T2-ADVISORY` with a named
reviewer check** — per ADR-0043 §B.6, it is not left carrying a `(gate pending: …)` token forever.

### Baseline (run on 2026-06-01): ~187 pre-existing violations

The checker found **more** real debt than the manual variance analysis did (e.g. 4 membership
commands with `nameof(Command)` error codes, ~50 ViewModel `collectAsState()` calls). These are
tracked in [`../backlog/audits/consistency-violations.md`](../backlog/audits/consistency-violations.md)
and the canonicalization tickets (T-0001…T-0016). **Existing violations do not block unrelated work**
— the gate (below) is **on new/changed code**, not the whole repo, until the baseline is cleared.

## The offerability parity check — `agents/tools/check-available-status-parity.mjs`

ADR-0037 D7 layer 2. It parses the canonical C# rule (`OrderAvailability.OfferableStatuses`, plus the
`DEAD` annotation on `OrderStatus.Pending`) and asserts the **eight** partner-client literals agree —
four on web, two on Android, two on iOS, and **half of them are BUTTON gates, not query literals**
(a query-only check goes green while the detail page hides Take for the whole `New`+Cash pipeline —
that is a live defect, ADR-0037 D0 row 10).

```bash
node agents/tools/check-available-status-parity.mjs             # strict — any divergence exits 1
node agents/tools/check-available-status-parity.mjs --baseline  # what CI runs (see below)
node agents/tools/check-available-status-parity.test.mjs        # the guard's own acceptance test
```

Three properties make it a real T1-CI gate rather than the "test with no trigger" the panel rejected:

- **It is outside the Nx workspace and has its own repo-root workflow** (`offerability-parity.yml`,
  triggered on `Cleansia.Core.Domain` + `Cleansia.App` + both mobile trees). `frontend-ci`'s
  `nx affected -t test` selects **zero** projects on a Kotlin/Swift/C#-only diff, and even when
  selected Nx would serve a **cached green** because those trees are not declarable inputs;
  `backend-ci` excludes both mobile trees. Being uncacheable is structural here, not configured.
- **A moved or renamed surface is a hard `P0` failure, never a silent pass.** Every surface is
  anchored, and an anchor that matches nothing — or matches but yields zero status tokens — fails.
  A green run means the tool *read* all ten files.
- **Its acceptance test runs in CI.** `check-available-status-parity.test.mjs` copies the ten files to
  a throwaway root, mutates one literal, and asserts red — including one scenario that widens the
  **canonical C#** floor and asserts the mobile clients go red, which is what proves the check parses
  the domain rule instead of carrying its own copy of the answer.

**The baseline is empty, and that is what "it self-invalidates" bought.** It held four entries —
ADR-0037 D4 rows 5/9/10/11, the partner-web half of T-0530 — each pinning a surface by its **exact**
divergent set. An entry matches only that exact set, so a baselined surface that drifts further **or
that gets fixed** both turn CI red; the four were therefore deleted in the same change that fixed the
four surfaces, which is the only exit an entry has. All eight surfaces are now gated strictly. The
summary line always prints the count; the tool never prints a bare `OK`.

## The catalog-claim liveness check — `agents/tools/check-catalog-claims.mjs`

**Specified, NOT yet built.** Rule: `conventions.md` §*"A claim about the tree carries its own
retirement condition"*; deviating forms: `consistency.md` §*"Catalog claims about the tree"*.
Tier today: **`(gate pending: catalog-claim-liveness checker — ticket owed)`** → **`T1-CI`** on landing.

**The failure it closes is decay, not mis-citation.** Every one of the six measured instances cited the
tree correctly at the moment of writing; each became false when the tree moved and **nothing anywhere
went red**. Two were falsified the same day they were written. So no writing-time human gate can close
this — the artifact is correct when the gate would run.

**Three checks, all decidable from in-repo text, no compiler and no type graph:**

1. **ADR status agreement.** For each ADR id appearing in `agents/knowledge/**/*.md` +
   `agents/process/**/*.md` adjacent to a quoted status token, read that ADR's `- **Status:**` line and
   fail on disagreement. *(The two banner instances; `agents/backlog/adr/*.md` filenames are not stable
   — resolve by the `NNNN-` prefix, not by full name.)*
2. **`Retires when: <path> exists` markers.** `fs.existsSync` the path; fail if it exists, because the
   banner it guards is then false. *(The payout-allocator card.)*
3. **Citation resolution.** Every `` `Path.ext:N` `` / `:N-M` citation in those trees: the file exists
   **and** has ≥ `M` lines. It cannot check that the lines *say* what is claimed — that residue is the
   reader's, permanently, and the entry must not pretend otherwise. *(The 65-line file cited at `:99-109`.)*

**Shape — cross-stack, its own repo-root workflow.** No stack's CI watches `agents/`, so no existing
workflow can host this: `backend-ci` / `frontend-ci` / `ios-ci` / `android-ci` all trigger on `src/`
subtrees, and `nx affected` selects zero projects for a markdown-only diff. Same structural argument
ADR-0037 D7 recorded for `check-available-status-parity.mjs`, and the same answer — a dependency-free
Node script **outside the Nx workspace** with its own workflow, triggered on `agents/**` **and** on the
`src/` trees the citations point into (a citation rots when the *cited* file changes, not when the
catalog does — that trigger is the whole check, not a nicety).

**Anti-vacuity (ADR-0032 D3).** A moved or renamed page must be a hard failure, never a silent pass:
assert the walker enumerated a non-trivial page count and that each check matched a **known-good
fixture** embedded in its own test. A run that finds zero citations because a glob broke must be red.

**The ticket owes four things**, and the last is not optional:
1. the checker + its own acceptance test (mutate one banner / one citation, assert red);
2. the sweep that drives the baseline to zero across the remaining role cards and catalog pages
   — six were fixed 2026-08-09, the rest are **unmeasured**;
3. the repo-root workflow;
4. **one line extending reviewer-check 5 "Catalog-edit routing"** to re-read the banners and citations
   of the **whole file** a hunk touches, not just the hunk — the sixth instance was a false sentence
   that survived a pass over its own page.

Until it lands, **nothing enforces the rule**, and the catalog says so rather than implying a reviewer
will notice: an unnamed human enforcer is `(guidance — no gate)` by this document's own tier table.

## How the gate works (Reviewer + PM)

- For any ticket touching code, the **Reviewer runs `check-consistency.mjs` scoped to the changed
  area** (`--paths=`) and treats a **new** violation as a hard fail (it names the rule). A
  *pre-existing* violation the change merely sits near is noted, not blocked (unless the ticket *is*
  the canonicalization ticket for it).
- The **PM does not mark a ticket `done`** until: `dotnet build` + `dotnet test` pass (backend
  touched), `nx build`/`nx lint`/`nx test` pass (frontend touched), and the consistency checker is
  clean for the changed area. See `quality-gates.md` Gate 8.

## Rollout plan (graduate to fully automatic)

1. **Now:** checker + editorconfig added; Reviewer runs the checker per change; baseline recorded.
2. **As canonicalization tickets land (T-0001…T-0016):** the baseline count drops toward zero.
3. **When a stack's baseline hits zero:** add `node agents/tools/check-consistency.mjs <stack>` as a
   **required step in that stack's CI workflow** (`backend-ci.yml` / `frontend-ci.yml`), and add
   `nx lint` + `nx test --affected` to `frontend-ci.yml` (currently it only builds).
4. **C# analyzers:** introduce a `src/Directory.Build.props` enabling `EnableNETAnalyzers` +
   `AnalysisLevel=latest`, with `TreatWarningsAsErrors` switched on **per selected rule id** (not
   globally) as each is driven to zero. The `.editorconfig` already sets the target severities; this
   step makes them fail the build. Sequence it so the build never breaks on day one.
5. **Android:** add **detekt** (no static analysis exists today) with a ruleset mirroring E1/E3/E5/E6,
   wired into the Gradle build.

> **Rule of thumb:** a check only becomes *blocking in CI* once its baseline is zero for that stack —
> otherwise CI is red for reasons unrelated to the current change, and people learn to ignore it. Add
> enforcement behind the cleanup, never in front of it.

## Enforcement tiers — what a rule is worth (ADR-0032)

A catalog entry that constrains call sites names its enforcer and declares one of these
(`knowledge/conventions.md` §"The price of a law"). **The tier is a property of *where the check runs*,
not of *which tool* runs it** — a `check-consistency.mjs` rule promoted into a stack's CI workflow
(Rollout step 3 below) is T1-CI from that day.

- **T1-CI** — fails a CI job on the offending change. Backend/frontend/Android: a test in a CI job.
  iOS: a SwiftLint `custom_rules` entry, or an XCTest guard in one of the three schemes CI runs.
  **Cross-stack** (a rule no single stack's CI can see): a plain Node script outside the Nx workspace
  with **its own repo-root workflow** triggered on every tree it reads — the
  `check-available-status-parity.mjs` / `offerability-parity.yml` shape. Do not reach for a Jest spec:
  ADR-0037 D7 records why one cannot work here (`nx affected` selects nothing, and Nx caches a green).
- **T2-ADVISORY** — reports, never sets the exit code. `check-consistency.mjs` sits here today on
  **every** stack (it is in no `.github/` workflow), including its warn-only rules (E9).
- **T3-HUMAN** — a **named** item in a standing checklist the Reviewer runs (Gate-DP §G of
  `ios-app-review-checklist.md`, Gate-AR, a numbered reviewer-check). An **unnamed** human enforcer
  ("someone will notice") is not T3 — it is `(guidance — no gate)`.
  - **The named T3-HUMAN enforcers, by id.** A T3-HUMAN enforcer lives in exactly one file and nothing
    goes red when it is deleted, so removing one has to be legible *here* as a regression against an
    accepted ADR rather than as tidying. This list records existing enforcers; it declares no new rule.
    - **Gate-DP §G** of `agents/backlog/ios-app-review-checklist.md` (+ reviewer-check #22) — ADR-0018's
      design-parity gate, on every iOS screen/feature ticket.
    - ⚠️ **There is no named T3-HUMAN enforcer for catalog-claim *decay*, and one cannot be invented.**
      Measured 2026-08-09: six `agents/knowledge/` + `agents/process/` artifacts asserted the opposite
      of the tree, and **all six cited the tree correctly when written** — a card true for 2 h 11 m, two
      banners outlived by same-day ADR acceptances, a citation rotted by an unrelated refactor, a count
      wrong twice, and this table's own row 18. None arose in a deliberation, so
      `conventions.md:239-243`'s enforcer (the panel lead, `deliberation.md` step 5) structurally cannot
      see them, and widening it to *"the lead also re-reads the catalog"* would be an enforcer named
      **be careful** — which is what the six already had. The answer is mechanical and specified as a
      new `T1-CI` gate: `conventions.md` §*"A claim about the tree carries its own retirement
      condition"* + `consistency.md` §*"Catalog claims about the tree"*, enforced by
      `agents/tools/check-catalog-claims.mjs`, **`(gate pending: catalog-claim-liveness checker —
      ticket owed)`**. See §*"The catalog-claim liveness check"* below for why it takes the cross-stack
      repo-root-workflow shape and what its ticket owes.
    - **reviewer-check 5 "Catalog-edit routing"** — `.claude/agents/reviewer.md`, step 5. Governs **any
      diff touching `agents/knowledge/*.md`**: it runs ADR-0033's three ordered routing tests (does the
      edit put shipped code in violation / does it narrow a governing sentence, with the floor's
      recorded-catalog-sweep rule / is it prescriptive about a stack the ticket never ran) plus
      ADR-0032's enforcer + tier check on the hunk. **ADR-0033 named this check as its own condition of
      acceptance: delete it and ADR-0033's routing test is `(guidance — no gate)`** by the line directly
      above — the rule stops binding the day the check disappears, and no build notices. Specified at
      ADR-0033 §Block D; this entry is its §Follow-ups **FT-12**.
- **`(gate pending: <ticket>)`** — the gate is specified and ticketed, but its baseline is non-zero, so
  the "rule of thumb" below forbids blocking on it yet. It promotes to T1-CI when the ticket lands.
- **`(guidance — no gate)`** — nobody enforces it.

**T1-CI is required only where the rule is mechanically expressible AND its baseline is zero.** An
unmechanizable rule is not thereby demoted to advice: ADR-0018's design-parity gate cannot be asserted
by any tool and is a load-bearing law at T3-HUMAN. Adding Swift to the consistency walker would land at
**T2-ADVISORY** — a legitimate declared tier, just not a CI gate.

**Zero-file scopes report `NOT RUN`, never `OK` (ADR-0032 D5).** A `--paths=` scope that matches zero
files used to print `consistency: OK (0 files scanned)` and exit 0 — so `--paths=src/cleansia_ios` read
as a **pass** for a stack the tool cannot parse. The fix (a loud `NOT RUN` banner) is ratified; it
landed on `fix/tooling-false-green-and-broken-docs` and reaches `master` with that branch. A green
banner tells you the tool *read* the stack; it can never tell you whether a named enforcer asserts what
its catalog sentence claims — that is the reviewer's read-the-enforcer check.

## When a new rule is needed

A new mechanical check is added **only** when a convention exists in `consistency.md`/`conventions.md`
(or a new ADR) — the checker enforces decisions, it doesn't invent them. Adding a check is itself a
small ticket (`layers: [<stack>]`) and the Architect signs off the rule.
