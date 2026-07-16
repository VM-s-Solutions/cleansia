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
| Project-specific rules | `agents/tools/check-consistency.mjs` | the A/B/C/D/E rules in `knowledge/consistency.md` no linter knows | **added — run by Reviewer** |

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

### Baseline (run on 2026-06-01): ~187 pre-existing violations

The checker found **more** real debt than the manual variance analysis did (e.g. 4 membership
commands with `nameof(Command)` error codes, ~50 ViewModel `collectAsState()` calls). These are
tracked in [`../backlog/audits/consistency-violations.md`](../backlog/audits/consistency-violations.md)
and the canonicalization tickets (T-0001…T-0016). **Existing violations do not block unrelated work**
— the gate (below) is **on new/changed code**, not the whole repo, until the baseline is cleared.

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

## When a new rule is needed

A new mechanical check is added **only** when a convention exists in `consistency.md`/`conventions.md`
(or a new ADR) — the checker enforces decisions, it doesn't invent them. Adding a check is itself a
small ticket (`layers: [<stack>]`) and the Architect signs off the rule.
