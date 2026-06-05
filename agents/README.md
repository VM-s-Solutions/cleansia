# Cleansia Agent Operating System

> A team of specialized AI sub-agents that pick up tasks, analyze them, write user stories,
> implement across backend / frontend / Android / iOS, test, review, harden, and document —
> coordinating entirely through Git-tracked artifacts.

This folder is the **operating system** for the Cleansia engineering team of agents. It is the
single source of truth for *how the team works*. The agents themselves (their system prompts)
live in [`.claude/agents/`](../.claude/agents/); everything they read, produce, and coordinate
through lives here under `agents/`.

If you are a human: start with [`WAY-OF-WORKING.md`](./WAY-OF-WORKING.md).
If you are an agent: read your charter in `.claude/agents/<your-name>.md`, then the process docs
below, then the ticket you were handed.

---

## The mental model in one paragraph

You (the owner) type a request in natural language. The **Orchestrator** (the main Claude Code
session) hands it to the **PM**, who turns it into one or more **tickets** in
[`backlog/tickets/`](./backlog/tickets/). Each ticket has a state machine
(`draft → ready → in_progress → in_review → qa → done`). The PM routes each ticket to the right
**specialist** (analyst, architect, backend, frontend, android, ios, db). A **reviewer** runs
**in parallel** with every developer. **Security** and **QA** gate the merge. Nothing is
verbal — every decision, hand-off, and status change is a file in Git. When the team is blocked,
it writes a question to [`backlog/questions/open.md`](./backlog/questions/) and surfaces it to you.

---

## Roster

The team is defined as **one charter per role** (DRY). The Orchestrator/PM spawns **multiple
concurrent instances** of the same charter when work fans out — e.g. three `backend` agents on
three independent features, each with a `reviewer` running alongside. Concurrency is a *runtime*
decision; the charter is the *definition*.

| Agent | Charter | Owns | One-line role |
|---|---|---|---|
| **Orchestrator** | *(the main session)* | routing | Receives your request, invokes the PM, relays status. The only agent you talk to. |
| **PM** | `pm.md` | `backlog/tickets`, `backlog/status`, ticket state | Owns the backlog & sprint state; sequences work; the only agent that reports progress up. |
| **Analyst** | `analyst.md` | `backlog/stories`, `questions/open.md` | Turns intent into user stories with Given/When/Then acceptance criteria. |
| **Architect** | `architect.md` | `backlog/adr`, `knowledge/*`, `knowledge/roles` | Owns Architecture Decision Records, the pattern catalog, and the responsibility map. |
| **Backend Dev** | `backend.md` | `src/Cleansia.Core.*`, `Cleansia.Web.*`, `Cleansia.Infra.Services`, `Cleansia.Functions` | Implements .NET 10 / CQRS / MediatR features and integrations. |
| **DB Master** | `db.md` | `src/Cleansia.Infra.Database`, migrations, entity configs | Owns the Postgres schema, EF Core configs, migrations, query filters, indexes, seeds. |
| **Frontend Dev** | `frontend.md` | `src/Cleansia.App` (apps + libs) | Implements Angular 19 / Nx / NgRx / PrimeNG across the 3 web apps. |
| **Android Dev** | `android.md` | `src/cleansia_android` | Implements Kotlin / Compose / Hilt across `:core`, `:partner-app`, `:customer-app`. |
| **iOS Dev** | `ios.md` | `src/cleansia_ios` *(to be created)* | Ports the Android apps to Swift / SwiftUI, sharing the backend contract. |
| **QA** | `qa.md` | `backlog/test-plans` | Writes test plans, executes against running apps, adds automated tests, reports defects. |
| **Reviewer** | `reviewer.md` | review verdicts | Gatekeeps every change against the conventions, ADRs, and AC. Runs in parallel with devs. |
| **Security Reviewer** | `security.md` | `backlog/security` | Audits auth, ownership, PII, tenancy, idempotency, secrets, rate-limits. Gates security-touching work. |
| **Optimizer** | `optimizer.md` | optimization reports | Hunts performance & cost: N+1s, bundle size, render churn, slow queries, allocations. |
| **Docs** | `docs.md` | `docs/**`, changelog | Keeps the VitePress site, READMEs, and changelog in sync with shipped behavior. |

### Why one charter per role (and not `backend-1`, `backend-2`)

A CQRS rule changes once → it changes in one file. Named duplicates drift: `backend-1.md` and
`backend-2.md` slowly disagree, and the reviewer can't tell which is canonical. We get parallelism
from **spawning N instances of the one charter at runtime**, not from copying the charter N times.
Where a role genuinely splits by domain (e.g. Android customer-app vs partner-app), the charter
documents both surfaces and the PM scopes each instance to one.

---

## Folder map

```
agents/
├── README.md                 # this file — the roster & map
├── WAY-OF-WORKING.md         # human-facing guide to the whole flow (read this first)
├── process/
│   ├── ticket-lifecycle.md   # state machine + Definition of Ready + deliberation stage + parallelism
│   ├── deliberation.md       # DEFENSE PANELS: author defends story/ADR vs challengers → lead → consensus
│   ├── documentation.md      # role-owned living docs (analysts/architects/devs) + Mermaid conventions
│   ├── quality-gates.md      # the 8 gates a change passes before "done"
│   ├── enforcement.md        # mechanical checks: editorconfig + check-consistency.mjs + CI rollout
│   ├── communication.md      # artifact-based protocol; escalation; no agent chat
│   └── routing.md            # how the PM decides which agent gets the work
├── analysts/                 # analyst living docs: business logic + Mermaid diagrams, per domain
├── architecture/decisions/   # architect living decision docs (immutable ADRs stay in backlog/adr/)
├── knowledge/                # the canonical "how we build" catalog (agents read this first)
│   ├── patterns-backend.md   # CQRS, validators, BusinessResult, repos, mappers (REAL types)
│   ├── patterns-frontend.md  # facades, signals, NgRx, PrimeNG, i18n (REAL types)
│   ├── patterns-mobile.md    # Compose, Hilt, MVVM, StateFlow (Android + iOS parity)
│   ├── consistency.md        # ONE way to do each archetype (paged query, command, list, form, VM)
│   ├── security-rules.md     # S1–S10 non-negotiable security laws (real-incident derived)
│   ├── testing.md            # what must be tested + the must-cover list (pay, lifecycle, authz…)
│   ├── runtime-readiness.md  # observability + graceful degradation when a dependency is down
│   ├── conventions.md        # naming, file layout, quality bars, owner-only steps
│   └── roles/                # responsibility map (CRC cards) per aggregate/service
├── tools/
│   └── check-consistency.mjs # mechanical checker for the project-specific A/B/C/D/E rules
├── backlog/
│   ├── INDEX.md              # the manifest — every ticket, one row, current state
│   ├── tickets/              # T-NNNN-*.md — one file per unit of work
│   ├── stories/              # US-<persona>-NNNN-*.md — user stories with AC
│   ├── adr/                  # NNNN-*.md — immutable architecture decisions
│   ├── status/               # sprint-N.md — progress reports for the owner
│   ├── questions/            # open.md / answered.md — the escalation inbox
│   ├── audits/               # findings from codebase audits (the first real job)
│   ├── test-plans/           # T-NNNN.md — QA test plans & results
│   └── security/             # audit checklists & findings
├── templates/                # ticket / story / adr / audit / test-plan templates
└── _legacy/                  # the archived old /plan+/execute YAML system (kept for history)
```

> **Why `agents/` and not `docs/`?** `docs/` is the *published* VitePress site for the product.
> The agent backlog churns constantly and is internal machinery — mixing it into the public docs
> would pollute the site and couple our process to a deploy artifact. The canonical *architecture*
> knowledge already lives in `docs/architecture/*.md`; our `knowledge/` catalog **references** it
> rather than duplicating it (one source of truth).

---

## How an agent is invoked

The Orchestrator or PM invokes a sub-agent via the `Agent` tool with `subagent_type` matching the
charter's frontmatter `name`. The charter is loaded as that agent's system prompt. The agent then
reads, in order:

1. Its own charter (`.claude/agents/<name>.md`)
2. The relevant `knowledge/*` catalog for its stack
3. `CLAUDE.md` (project guardrails)
4. The ticket it was handed (and any ADRs / stories it links)

Communication is **artifact-based** — agents never chat with each other. See
[`process/communication.md`](./process/communication.md).

---

## Modifying the team

Edit a charter or a process doc; the change takes effect on the next invocation. Everything is in
Git, so every change to *how the team works* is reviewable like code. If you rename a charter, the
PM is responsible for updating every reference in `process/` and `backlog/`.
