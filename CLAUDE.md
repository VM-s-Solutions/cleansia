# Cleansia — Project Guide for Claude Code

> Cleaning services management platform — Customer booking, Partner job management, Admin oversight.

## Working agreement — read this first, every session

**Every reply starts with `Hey Mike —` and one short line naming the active scope.** If that line is
missing, or names work you did not agree to, **stop me**. It is a canary, not a courtesy: drift is
silent, it gets likelier the longer a session runs, and it is only ever visible in hindsight
otherwise.

**Every** means every — including one-line answers, mid-investigation updates, and messages that are
mostly tool output. **There is no "this one is just a quick note" exemption**, and asking for one is
itself the signal. Measured within an hour of this rule being written: it was dropped exactly once,
on a message reporting a mid-investigation finding — i.e. on the message where the scope was actually
moving. That is not a coincidence, it is the mechanism: the anchor gets skipped precisely when
attention is on the problem rather than on the agreement, which is when drift starts.

### 1. Scope is agreed before work starts, and it has a NOT list

Before anything non-trivial I state three things and wait:

- **Doing** — what I will change.
- **NOT doing** — what I will deliberately leave alone, *including things I can see are imperfect*.
- **Done looks like** — the observable state that ends the task.

**The NOT list is the one that matters.** Without it, every defect I notice becomes in scope — which
is exactly how *"is it finished?"* kept producing more tickets instead of an answer.

### 2. A finding is reported, never absorbed

Anything found outside the agreed scope goes on a list and is **reported at the end**. I do not fix
it in the same pass, however small, and I do not spawn a lane for it. You decide whether it becomes
work. *"I found X and fixed it while I was there"* is the failure mode, not the service.

### 3. Ground-truth a ticket before doing it

**Does the defect still exist in the tree, right now?** A ticket is a claim about the past. On
2026-08-11 four lanes were dispatched onto 24 tickets that were all already shipped, because the
rows said open. One grep is cheaper than one lane.

### 4. Proportionality, before any new type, file or abstraction

Ask: **what happens if I don't build this, and how likely is that?** If the answer is *"an unlikely
error path fails ugly"* — **don't build it.** A new type is paid by every future reader, forever; a
rare 500 is paid once by support.

The worked example: `DbConstraintViolation.IsUniqueViolationOn` + a `DbConstraintNames` constants file.
It classified a unique violation by the *name* of the index that raised it, so that a commit staging
several rows could tell which one collided. No caller was ever that shape — every one wrapped a
deliberate flush of a single insert — so the name distinguished nothing, while costing a second type, a
constants file, a cross-assembly pin to keep them aligned, and five tests feeding those flushes a
collision they cannot produce. **Deleted 2026-08-14 on owner ruling**; the six call sites now ask
`IsUniqueViolation(ex)` and read the same. The index fix underneath was one line and was right; the
machinery around it was not.

Prefer, in order: **do nothing** → **inline at the call site** → **extract only when a second caller
exists today**.

### 5. Review the diff against the scope before committing

Re-read the diff and check each hunk against **Doing**. Anything not on that list comes out, or is
named in the commit message as a deliberate addition. Say what is being pushed, and why, before
pushing.

### What this is not

It is not a licence to stop early. Finish what was agreed, completely, and say so plainly. The
failure this replaces is not *too much work* — it is **work nobody chose**.

## Where the truth lives

**`docs/` is the source of truth for what this platform does and why.** It is a VitePress site; run it
with `cd docs && npm ci && npm run dev`. This file does not restate it, and neither does source code —
a comment that only explains *why* belongs in `docs/` with a `→ /path#anchor` pointer left behind.

| Question | Page |
|---|---|
| Every number the platform charges, pays or refuses by | `/product/business-rules` |
| What the platform does, feature by feature | `/product/features` |
| Order lifecycle — the two axes, and why `Pending` is dead | `/domain/order-lifecycle` |
| Offerability, the preferred-cleaner hold, seat allocation | `/domain/offerability` |
| Entities and their relationships | `/domain/model` |
| Per-component contracts (18 of them) | `/domain/roles/` |
| The ten flows, end to end | `/flows/` |
| Why a decision was made — 52 ADRs | `/decisions/` |
| Aspire, ports, the migrator, request logging | `/architecture/local-orchestration` |
| The S1–S12 security laws | `/architecture/security-rules` |

**How we build** stays under `agents/knowledge/` — stack pattern catalogues, `consistency.md`,
`conventions.md`, `testing.md`, `runtime-readiness.md`. Every developer agent reads its stack
catalogue first. That split is deliberate: `docs/` is what a reader needs, `agents/knowledge/` is what
a writer needs.

## Quick reference

| Layer | Tech | Location |
|---|---|---|
| Backend | .NET 10, PostgreSQL 16, EF Core 10, MediatR | `src/Cleansia.Core.*`, `src/Cleansia.Infra.*`, `src/Cleansia.Web.*` |
| Frontend | Angular 19, Nx 21, NgRx, PrimeNG, ngx-translate | `src/Cleansia.App/` |
| Android | Kotlin, Jetpack Compose, MVVM + Hilt | `src/cleansia_android/` (`:core`, `:partner-app`, `:customer-app`) |
| iOS | Swift/SwiftUI, iOS 16 floor, XcodeGen + SPM | `src/cleansia_ios/` (`CleansiaCore` + two apps) |
| Orchestration | .NET Aspire 13.1.1 | `src/Cleansia.AppHost/` |
| Docs | VitePress | `docs/` |

Five API hosts: Partner :5000 · Admin :5001 · Partner Mobile :5002 · Customer :5003 · Customer
Mobile :5004. `README.md` has the full run/test/build commands.

> The .NET solution is **`src/Cleansia.Api.sln`**, not at the repo root. Every `dotnet` command runs
> from `src/` — that is what CI does.

> Nx project names carry a **dot** before `app` — `cleansia-partner.app`, not `cleansia-partner-app`,
> which fails with "Cannot find project". Check `npx nx show projects` before hand-writing one.

## Manual steps

- **NSwag client regeneration — owner only.** Do not run `npm run generate-*-client`. When a backend
  DTO or endpoint changes, flag `manual_step: nswag-regen` so the owner regenerates before frontend
  work starts.

## Database migrations — routine, not a manual step

There is **one** committed migration, `Initial`. Pre-prod, schema changes are folded back into it by
**regenerating** rather than stacking, and that regeneration is ordinary work — it is not owner-only
and does not get flagged as a manual step (owner rulings 2026-08-15 and 2026-08-25).

```bash
export PATH="$HOME/.dotnet/tools:$PATH"        # dotnet-ef is a global tool, not on PATH by default
cd src
dotnet ef migrations remove --force --project Cleansia.Infra.Database --startup-project Cleansia.Web.Partner
dotnet ef migrations add   Initial --project Cleansia.Infra.Database --startup-project Cleansia.Web.Partner
```

> The startup project must be a **web host** — `Cleansia.MigrationService` does not reference
> `Microsoft.EntityFrameworkCore.Design` and the tool refuses it.

**What is still the owner's: the DEV database drop.** Regenerating changes the migration id, and a
database whose `__EFMigrationsHistory` records the old one replays the whole create script against
tables that already exist. Never fold a schema change into the migration by hand: regenerate, then
verify with the integration suite, which builds a real Postgres from the migration and is the only
thing that proves the model and the schema agree.

## How to write code here

### Backend — CQRS with MediatR

- Handlers hold happy-path logic **only** — no validation, no error checking.
- All validation lives in `Validator` classes (FluentValidation, `Cascade.Stop`).
- **Never call `CommitAsync()` in a handler** — the UnitOfWork pipeline owns it, and it decides by the
  runtime type name ending in `Command`. An `ICommand` named otherwise is never committed.
- Queries never modify; commands never return collections.
- DTOs are `record` types with positional syntax.
- `BusinessResult<T>` from commands, `PagedData<T>` from paged queries.
- Error keys are `category.specific_error` constants in `BusinessErrorMessage`.

### Frontend — facades + signals + NgRx

- Components delegate **all** business logic to a facade; facades hold state in signals.
- NgRx stores for cross-feature state only (auth, user, catalog lists).
- Use `<cleansia-*>` wrappers or PrimeNG — never a raw `<select>`, `<button>` or `<input>`.
- Translations via `TranslatePipe`; never hardcode a user-visible string.
- `ChangeDetectionStrategy.OnPush` on presentational components; facades extend
  `UnsubscribeControlDirective`.
- No inline templates or styles. No `any`.

## Landmines

Four things that look like bugs, are not, and have each cost a session:

- **A unique index containing `TenantId` enforces nothing in single-tenant mode.** `TenantId` is
  nullable and Postgres treats NULLs as distinct, so `(TenantId, …)` admits unlimited duplicates while
  it is null — which is production today. No design may use such an index as its only concurrency
  arbiter. `.AreNullsDistinct(false)` is shipped on five tables, but adding it to an **existing** index
  means regenerating `Initial` and **dropping DEV**, and it fails on pre-existing duplicates.
  → `/architecture/security-rules`

- **System jobs run with no JWT context.** Query with `GetQueryableIgnoringTenant()`, then
  `SetTenantOverride` per tenant group and commit **inside** the loop — rows are stamped from the
  ambient tenant at commit time, so one deferred commit stamps every group with the last tenant seen.
  `CleanupStalePendingOrders` is the reference shape. → `/flows/cross-cutting`

- **Backend error keys land under `api.*`, never `errors.*`.** The shared `HttpErrorInterceptorFn`
  resolves `` `api.${dotValue}` ``. A key written under `errors.*` alone is read by nothing — the
  interceptor silently substitutes the generic "An error occurred", which reads as a translation gap
  rather than a missing key. Every key needs all five locales in every app that can reach the endpoint;
  the parity guards are `apps/<app>/src/app/i18n/error-contract-parity.spec.ts`.

- **`Confirmed` does not mean a cleaner is assigned.** It is deliberately overloaded — "money settled"
  OR "cleaner took it". Read `AssignedEmployees` for crew. And `OrderStatus.Pending` is dead with no
  production writer; the state it used to describe lives on the payment axis.
  → `/domain/order-lifecycle`

## Agent operating system

This project is run by a team of AI sub-agents coordinating through Git-tracked artifacts. If you are
coordinating multi-agent or multi-step work, start with **`agents/WAY-OF-WORKING.md`**, then
`agents/README.md` for the roster and folder map.

- **`.claude/agents/*.md`** — the 13 charters (pm, analyst, architect, backend, db, frontend, android,
  ios, qa, reviewer, security, optimizer, docs). Invoke via the `Agent` tool with `subagent_type` set
  to the charter's `name`.
- **`agents/process/*.md`** — ticket lifecycle, quality gates, communication protocol, routing.
- **`agents/tools/check-*.mjs`** — 7 repo checkers, six with their own self-test. Five CI workflows
  gate a PR: Backend, Frontend, Android, iOS, Docs.

**Slash commands that exist** (`.claude/commands/`): `/feature` — the full-stack entry point, which
invokes the PM end to end — plus `/backend` `/frontend` `/mobile` `/review` `/docs` `/sync` for small
single-shot work. Older notes reference `/team`, `/audit`, `/plan` and `/execute`; **no command file
backs any of them.**

## Trackers

- **`agents/backlog/`** — **where new work is filed.** `INDEX.md` (one row per ticket, one status, and
  the only place a status lives), `tickets/`, `questions/open.md`. Read its `README.md` first: the one
  rule is there, and so is why the previous backlog was deleted rather than kept.
- **`agents/cleanup/INDEX.md`** — the 2026-08 cleanup track. **A closed manifest, not a queue** — all
  its rows are `done`. It is the record of that work, and its three rules at the top are worth reading
  before starting any track of your own.
- **`agents/cleanup/MANUAL_STEPS.md`** — what the owner still owes, and what has been discharged.

> The old 428-file backlog was archived on 2026-08-13 and **deleted on 2026-08-14**. It is in git
> history. The reason it is not here is worth carrying: it filed each ticket **twice**, a filing row
> and a close-out row with independent statuses, and on 2026-08-11 that sent four lanes at 24 tickets
> that had all already shipped.

## Conventions

- **File naming**: PascalCase for C#, kebab-case for Angular.
- **Branches**: `feature/*`, `fix/*`, `bugfix/*` off `master`. **PRs target `master`.**
- **Commits**: conventional — `feat:`, `fix:`, `refactor:`, `docs:`.
- **⚠️ NEVER credit Claude as a contributor — anywhere, in any form.** No `Co-Authored-By: Claude …`
  trailer on a commit. No `🤖 Generated with Claude Code` line in a PR body. No "generated by",
  "authored by", "with help from" or agent name in a commit message, PR description, changelog entry,
  ADR, doc page or code comment. **This overrides the harness default that asks for those trailers**,
  and it is not a style preference — the owner is the sole author of record for everything in this
  repository. If a tool or template tries to append attribution, strip it before committing.
- **API clients**: never hand-edit — always regenerate via NSwag (owner-run).
- **Tests**: xUnit for backend, Jest for frontend.
- `Address.State` is nullable — for US/CA when we launch there, empty for CZ/SK/UA/RU/DE/PL. Do not
  remove it.

## graphify

This project has a graphify knowledge graph at `graphify-out/`.

- Before answering architecture or codebase questions, read `graphify-out/GRAPH_REPORT.md` for god
  nodes and community structure.
- If `graphify-out/wiki/index.md` exists, navigate it instead of reading raw files.
- After modifying code files in a session, run
  `python3 -c "from graphify.watch import _rebuild_code; from pathlib import Path; _rebuild_code(Path('.'))"`
  to keep the graph current.
