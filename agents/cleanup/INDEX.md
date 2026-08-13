# Cleanup Track — INDEX

The manifest for the 2026-08 cleanup. **Deliberately separate from `agents/backlog/`** — no ticket,
row or id is shared between the two tracks, so the state of this work is readable without untangling
it from three sprints of feature history.

Owner brief, 2026-08-12: resolve the open analysis findings, archive the feature backlog, make
`docs/` the source of truth, stop documenting inside the code, and walk every flow end to end for
gaps that actually matter.

## The three rules this track exists to obey

1. **One row per ticket, one status.** `agents/backlog/INDEX.md` records a ticket twice — a *filing*
   row and a *close-out* row with independent statuses — and on 2026-08-11 that sent four lanes at 24
   already-shipped tickets. There is no filing row here. This table is the only place a status lives.
2. **Cite the PR, never the branch SHA.** Every PR lands squashed, so a SHA recorded on a feature
   branch stops resolving the moment it merges. ~105 rows in the old backlog cite 18 dead SHAs for
   exactly this reason.
3. **A row is a claim about the past.** Before working a ticket, verify the thing it describes is
   still true in the tree. A `done` row is not evidence; the tree is.

## Convention

Ticket **files** are written at the start of their phase, not up front — writing a spec for P7 today
would be guessing at what the P1 flow walks will find. The row below is the commitment; the file is
the working spec, and it appears when the phase opens. Rows with no file yet are marked `todo`.

**Status:** `todo` · `in_progress` · `blocked` · `done` · `dropped`
**Size:** S (< half a day) · M · L

---

## P1 — Track, baseline, end-to-end analysis *(read-only against the codebase)*

| ID | Title | Size | Status | PR |
|---|---|---|---|---|
| CL-001 | Cleanup track + INDEX scaffolding | S | done | #192 |
| CL-002 | Baseline census — build, checkers, LOC/comment/test counts | S | done | #192 |
| CL-003 | E2E walk — auth & identity | M | done | #192 |
| CL-004 | E2E walk — booking & pricing | M | done | #192 |
| CL-005 | E2E walk — payment & fiscal | M | done | #192 |
| CL-006 | E2E walk — offerability, preferred-cleaner hold, take | M | done | #192 |
| CL-007 | E2E walk — execution & completion | M | done | #192 |
| CL-008 | E2E walk — cancellation, refund, dispute | M | done | #192 |
| CL-009 | E2E walk — pay, pay periods, invoices, payouts | M | done | #192 |
| CL-010 | E2E walk — loyalty, memberships, metered benefits, referrals | M | done | #192 |
| CL-011 | E2E walk — GDPR, retention, audit, admin override | M | done | #192 |
| CL-012 | E2E walk — cross-cutting: tenancy, outbox, idempotency, notifications, rate limiting | M | done | #192 |
| CL-013 | Gap register — triage every finding into fix / accept / not-a-risk | M | done | #192 |

> **P1 complete.** All thirteen flows walked; findings triaged in `gap-register.md` — **2 to fix, 4 accepted-and-recorded, 19 checked and closed**. G-15's mechanism is decided: **seat ordinal** (owner, 2026-08-12).

## P2 — Fix wave

| ID | Title | Size | Status | PR |
|---|---|---|---|---|
| CL-014 | Route the eight analysis findings to their owning phase | S | done | #193 |
| CL-015 | G-01 tenant stamping · G-15 seat ordinal (`MS-1` **cleared** — migration regenerated) | L | done | #193 |
| CL-016 | `C3` — 6 real teardown gaps fixed; 4 were checker false positives → CL-024 | S | done | #193 |
| CL-017 | `E6` — 13 sites converted to `collectAsStateWithLifecycle()` (checker saw 11) | S | done | #193 |
| CL-018 | Build warnings — 3 EF 10 deprecations + 24 xUnit analyser sites | S | done | #193 |

> **P2 complete.** 19 real defects fixed, 7 checker false positives routed to `CL-024`, 3,878 unit
> tests green. `CL-014` shrank on inspection: five of the eight findings are P9's work (README, root
> docs, dead scripts, `CLAUDE.md`) and one duplicated `CL-018`, so it became routing rather than fixes.
> **`MS-1` cleared** — `Initial` regenerated (`20260813085249`), the seat index exists, and the
> concurrency test passes against real Postgres. **DEV needs a database drop**: the migration id changed.

## P3 — Admin `errors.*` → `api.*` consolidation

| ID | Title | Size | Status | PR |
|---|---|---|---|---|
| CL-019 | Two retirement assertions in `error-contract-parity.spec.ts` — no `errors` block, no reader | S | done | #194 |
| CL-020 | Repoint 30 files; merge 5 unique keys (164 of 169 were already duplicates) | M | done | #194 |
| CL-021 | Delete the `errors` block from all five admin locales; correct `CLAUDE.md` | S | done | #194 |

> **P3 complete.** The block was **97% redundant** — 164 of its 169 keys already existed under `api.*`,
> so this removed a duplicate rather than migrating a corpus. It also closed a live gap: `refund.failed`
> is a real `BusinessErrorMessage` that had **no** `api.*` translation, so any admin hitting it through
> the interceptor path saw the generic message. Admin locales: 235 `api` keys, no `errors`. 67 projects'
> tests green, admin app builds.

## P4 — Convention debt to a stated baseline

| ID | Title | Size | Status | PR |
|---|---|---|---|---|
| CL-022 | Backend — 3 `B1` phantoms killed, `GetMyServingCleaners` → `IQuery`; `B3` ×21 + `B1` ×5 declared | M | done | #195 |
| CL-023 | Frontend + mobile — 16 phantoms killed (`: any`, `C3`, hardcoded `Text`); `D2` ×8 + `E1` ×9 declared | M | done | #195 |
| CL-024 | Fix `B10`, `C3`, `B1` and `conv` — 20 phantoms, each guarded by a new self-test | S | done | #195 |

> **P4 complete — and it inverted.** The phase assumed ~58 items of debt to clear; **20 of the 66 were
> the checker being wrong**, not the codebase. Those are fixed and guarded (self-tests 19 → 26, each
> narrowing paired with a "STILL flags" case). One real landmine was fixed: `GetMyServingCleaners` was
> an `ICommand` the UnitOfWork pipeline never commits, because it decides on the type-name suffix.
> The surviving **46 are declared with reasons** in `consistency-baseline.md` — none has a user-visible
> cost, and `B3` (21) and `D2` (8) need an owner ruling because both change working behaviour.

## P5 — Docs platform

| ID | Title | Size | Status | PR |
|---|---|---|---|---|
| CL-025 | VitePress + mermaid; diagrams render, docs build clean | S | done | #196 |
| CL-026 | New IA — Product · Domain · Flows · Decisions, with real landing pages | M | done | #196 |
| CL-027 | `check-docs-refs.mjs` + 10 self-tests, wired as **Docs CI** (`T1-CI`) | M | done | #196 |

> **P5 complete.** Mermaid renders (client-side — a malformed diagram builds clean and breaks only in
> the browser, so diagrams get looked at). The IA gains Product · Domain · Flows · Decisions, each with
> a real landing page rather than a stub. `check-docs-refs.mjs` verifies both halves of a pointer —
> the page resolves AND the `#anchor` matches a heading — with 10 self-tests including two that prove
> it can fail. Wired as **`docs-ci.yml` — "Docs CI"** on owner instruction, joining Backend / Frontend
> / Android / iOS CI. It gates BOTH halves of the reference contract, neither of which was checked on
> a PR before: `code → docs` via the checker, and `docs → docs` via `vitepress build` — which enforces
> `ignoreDeadLinks:false` but previously ran only inside the deactivated, dispatch-only deploy
> workflow. The gate's own self-test runs first and blocks, so a change that defangs it reddens here.

## P6 — ADR migration

| ID | Title | Size | Status | PR |
|---|---|---|---|---|
| CL-028 | Migrate **51** ADRs to `docs/decisions/adr-NNNN.md`; index + supersession graph | L | done | #197 |
| CL-029 | Archive 29 challenges + 7 drafts to `agents/archive/2026-08/adr-deliberation/` | S | done | #197 |

> **P6 complete.** **51** records, not 52 — the earlier count included `README.md`. Each is now
> `adr-NNNN.md`, so `/decisions/adr-0037` resolves and the ~618 citing source files needed no edit.
> 125 stale `agents/backlog/adr/` paths rewritten. Three defects surfaced on the way: a tool-call
> artifact committed inside ADR-0021, my own migration dropping frontmatter from the two YAML-style
> records, and two citations of the migration filename **P2 renamed** — which no gate caught, because
> `check-catalog-claims` runs in no workflow.

## P7 — Content build

| ID | Title | Size | Status | PR |
|---|---|---|---|---|
| CL-030 | Domain model — ERDs per area, generated from the **61** EF configurations | L | done | — |
| CL-031 | Order lifecycle + offerability, both diagrammed | M | done | — |
| CL-032 | Thirteen flow pages — sequence diagrams + edge-case tables, from P1 | L | todo | — |
| CL-033 | Business rules with rationale — booking, cancellation, crew, preferred, pay | L | **partial** — feature list outstanding | — |
| CL-034 | Split `agents/knowledge/` by audience — domain truth publishes, build rules stay | M | todo | — |

> **P7 in progress — splitting across two PRs.** Landed: the domain model (61 entities, ERDs per area,
> generated from the EF configurations rather than described), the two-axis order lifecycle, offerability
> including the take cascade, and the business rules with their rationale. Outstanding: `CL-032` (the
> thirteen flow pages), `CL-034` (the `agents/knowledge/` split), and the product feature list half of
> `CL-033`. Four `L`/`M` tickets is more than one reviewable diff.

## P8 — Comment migration

| ID | Title | Size | Status | PR |
|---|---|---|---|---|
| CL-035 | C# — 4,720 `//` triaged, 9,158 `///` trimmed to 1–2 lines | L | todo | — |
| CL-036 | Android 15,391 · iOS · Angular | L | todo | — |
| CL-037 | Write the rule into `conventions.md` and all 13 agent charters | M | todo | — |

## P9 — Archive & delete

| ID | Title | Size | Status | PR |
|---|---|---|---|---|
| CL-038 | Archive `agents/backlog/` → `agents/archive/2026-08/` (kept in git) | M | todo | — |
| CL-039 | Delete the dead — `_legacy/`, `planning/`, six frozen root docs, ~25 spent wave scripts, the empty `Infra.Scripts` project | M | todo | — |
| CL-040 | Rewrite `README.md` — it currently hands out `Add-Migration` against paths that do not exist | S | todo | — |
| CL-041 | Slim `CLAUDE.md` to the working agreement + pointers into the docs site | M | todo | — |

---

## Out of scope — named, so it stays out

- **No EF migration, no NSwag regen.** Owner-only; a phase that needs one raises a `MANUAL_STEP`.
- **No UI work** on any of the five apps.
- **No re-litigating shipped ADRs** during migration. They move as written. An ADR that contradicts
  the tree is reported as a finding, not silently edited.
- **Not documenting what OpenAPI already generates.**
- `Address.State` stays — it is for US/CA launch.
- `CHANGELOG.md` stays a repo-root file and is deliberately not moved into the site.
- **No attribution to Claude** on any commit, PR or page — see `CLAUDE.md` § *Conventions Summary*.
