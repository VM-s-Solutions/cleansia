# Conventions & Quality Bars

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
- **Comments explain WHY, never WHAT.** No `// update the user`. No `// TODO(JIRA-x)` — use a ticket.

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

## Owner-only steps (agents flag, never run)

- **EF Core migrations** — flag `manual_step: ef-migration`, describe the schema delta.
- **NSwag client regeneration** — flag `manual_step: nswag-regen` whenever a backend DTO/endpoint
  changes; hold dependent frontend/mobile work until the owner confirms.
- **DB seed edits** (`sql-scripts/insert_seed_data.sql`) — seeds carry tenant/user ids matched to
  dev tooling; don't touch without explicit owner approval.
- **Real secrets** — never in `appsettings*.json`. User-secrets on dev, env vars on prod.
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
