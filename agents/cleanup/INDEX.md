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
| CL-014 | Route the eight analysis findings to their owning phase | S | done | — |
| CL-015 | G-01 tenant stamping · G-15 seat ordinal (**`MS-1` open**: EF migration) | L | done | — |
| CL-016 | `C3` — 6 real teardown gaps fixed; 4 were checker false positives → CL-024 | S | done | — |
| CL-017 | `E6` — 13 sites converted to `collectAsStateWithLifecycle()` (checker saw 11) | S | done | — |
| CL-018 | Build warnings — 3 EF 10 deprecations + 24 xUnit analyser sites | S | done | — |

> **P2 complete.** 19 real defects fixed, 7 checker false positives routed to `CL-024`, 3,878 unit
> tests green. `CL-014` shrank on inspection: five of the eight findings are P9's work (README, root
> docs, dead scripts, `CLAUDE.md`) and one duplicated `CL-018`, so it became routing rather than fixes.
> **`MS-1` is open** — the seat index is inert until the migration is regenerated.

## P3 — Admin `errors.*` → `api.*` consolidation

| ID | Title | Size | Status | PR |
|---|---|---|---|---|
| CL-019 | Extend `error-contract-parity.spec.ts` to guard the migration before it starts | S | todo | — |
| CL-020 | Repoint 23 `ERROR_KEY_MAP`s and merge ~759 tokens × 5 locales into `api` | L | todo | — |
| CL-021 | Remove the legacy `errors.*` block from the admin locales | S | todo | — |

## P4 — Convention debt to a stated baseline

| ID | Title | Size | Status | PR |
|---|---|---|---|---|
| CL-022 | Backend — `B3` ×21 validator base, `B1` ×8 response records | M | todo | — |
| CL-023 | Frontend + mobile — `D2` ×8, `E1` ×9, `: any`, hardcoded strings | M | todo | — |
| CL-024 | Fix the `B10` regex — it fires on `TimeZoneResolution.Resolve(...)`, which touches no Dispute | S | todo | — |

## P5 — Docs platform

| ID | Title | Size | Status | PR |
|---|---|---|---|---|
| CL-025 | VitePress: mermaid + diagram tooling | S | todo | — |
| CL-026 | Information architecture, nav and sidebar for the new sections | M | todo | — |
| CL-027 | The code→docs reference convention + `check-docs-refs.mjs` gate | M | todo | — |

## P6 — ADR migration

| ID | Title | Size | Status | PR |
|---|---|---|---|---|
| CL-028 | Migrate 52 ADRs to `docs/decisions/` with stable `ADR-NNNN` anchors | L | todo | — |
| CL-029 | Archive the deliberation artifacts (`challenges/`, `drafts/`) — argument, not decision | S | todo | — |

## P7 — Content build

| ID | Title | Size | Status | PR |
|---|---|---|---|---|
| CL-030 | Domain model — ERD from the 65 EF configurations + entity reference | L | todo | — |
| CL-031 | Order lifecycle — the two-axis state machine as a diagram | M | todo | — |
| CL-032 | Thirteen flow pages — sequence diagrams + edge-case tables, from P1 | L | todo | — |
| CL-033 | Product — feature list + business-rule rationale (pricing, cancellation, pay, membership, loyalty) | L | todo | — |
| CL-034 | Split `agents/knowledge/` by audience — domain truth publishes, build rules stay | M | todo | — |

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
