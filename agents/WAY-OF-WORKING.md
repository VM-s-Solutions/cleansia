# Cleansia — The Way of Working

> Read this once and you'll understand the whole team. It's the human-facing companion to
> [`README.md`](./README.md) (the roster) and the `process/` docs (the rules).

You built Cleansia over a year and it grew large: 5 .NET APIs, 3 Angular apps, 2 Android apps, iOS
on the way. You can't hold all of it in your head anymore, and you want to *delegate by typing* —
hand a task to a team of specialized agents that analyze it, spec it, build it, review it, test it,
harden it, and document it, coordinating with each other and tracking status — without you having to
micromanage. This document is how that team works.

---

## 1. The one-screen picture

```
  YOU ──"natural language request"──►  ORCHESTRATOR (the main Claude session)
                                            │ hands the request to the PM
                                            ▼
                                          ┌────┐
                                          │ PM │  owns the backlog + state, sequences everything
                                          └─┬──┘
            ┌──────────────┬───────────────┼───────────────┬──────────────┐
            ▼              ▼                ▼               ▼              ▼
         ANALYST       ARCHITECT      DB → BACKEND      FRONTEND       ANDROID / iOS
       (user stories) (ADRs+patterns) (schema, CQRS)  (Angular)       (Compose / SwiftUI)
            │              │                │               │              │
            └──────────────┴───────┬────────┴───────────────┴──────────────┘
                                   │  ◄── a REVIEWER runs IN PARALLEL with every developer
                                   ▼
                         SECURITY · OPTIMIZER · QA   (the merge gates)
                                   │
                                   ▼
                         PM merges ──► ticket: done ──► picks the next
```

Everything they say to each other is a **file in Git**. There is no hidden chat. If a decision isn't
written down, it didn't happen. That's what makes a platform this size safe to change with a team of
agents.

---

## 2. The team (and why it's shaped this way)

| Agent | What it does for you |
|---|---|
| **Orchestrator** | The session you type into. Relays your request to the PM and reports back. The only one you talk to. |
| **PM** | Turns your request into tickets, decides the order, spawns the right specialists + a reviewer alongside each, runs the gates, tells you progress. The conductor. |
| **Analyst** | Writes the user story with crisp Given/When/Then criteria so "build X" can't be misinterpreted. Also finds *missing* functionality during audits. |
| **Architect** | Makes the decisions that are expensive to undo (ADRs) and owns the pattern catalog so everyone builds the same way. |
| **DB Master** | Owns the Postgres schema, EF configs, indexes, query filters. Describes migrations — **you** run them. |
| **Backend Dev** | The .NET / CQRS / MediatR features, the 5 APIs, integrations (Stripe/SendGrid/Firebase), fiscal flow, Functions. |
| **Frontend Dev** | The 3 Angular apps — components, facades, NgRx, PrimeNG, 5-language i18n. |
| **Android Dev** | The Kotlin/Compose apps. The reference the iOS apps copy. |
| **iOS Dev** | Ports the Android apps to SwiftUI, 1:1, against the same backend. (Ready now; activates on your first iOS ticket.) |
| **QA** | Writes and runs test plans; adds automated tests for money/state logic; finds regressions. |
| **Reviewer** | Gatekeeps every change — runs **in parallel** with the developer, exactly as you asked. |
| **Security Reviewer** | Hunts auth/ownership/PII/tenancy/idempotency holes against the S1–S10 laws. |
| **Optimizer** | Hunts N+1s, slow queries, bundle bloat, render churn. |
| **Docs** | Keeps the VitePress site + changelog honest. |

**Why one charter per role (not `backend-1`, `backend-2`):** you wanted multiple developers of a
role working in parallel so each can focus. We get that by **spawning multiple live instances of the
same charter at runtime** — the PM can run `backend #1` on one feature and `backend #2` on another at
the same time, each with its own reviewer. But the *definition* stays in one file, so when a CQRS
rule changes you edit it once and every instance gets it. Copies would silently drift; instances
don't.

**Why a reviewer runs in parallel with every developer:** you asked for this specifically. The
developer writes the change while a reviewer instance reads the same ticket and diff and writes a
verdict concurrently. The PM merges the two before the ticket advances. Review is a companion, not a
bottleneck.

---

## 3. How a request becomes shipped code

You type, for example:

> "Add the admin UI for per-employee pay overrides, and make sure cancelling an order can't be done
> by the wrong user."

Here's what happens — every step is a file you can open:

1. **PM** reads the backlog, splits this into tickets:
   `T-0101 per-employee pay override admin UI` and `T-0102 audit & fix order-cancel ownership`.
   Each gets a file in `backlog/tickets/` and a row in `backlog/INDEX.md`.
2. For T-0101, behavior is slightly fuzzy → **Analyst** writes `US-admin-0007` with exact AC. The
   pay-override layering touches a seam → **Architect** confirms/writes an ADR so it composes over
   per-service config without recomputing history.
3. **DB Master** designs the schema delta for the override (flags `manual_step: ef-migration` — you
   run it). **Backend Dev** writes the command/query/validator/handler + DTO (flags
   `manual_step: nswag-regen` — you regenerate the client). A **Reviewer** instance reviews each in
   parallel.
4. Once the contract is locked and you've regenerated the client, **Frontend Dev** builds the admin
   tab + 5-locale i18n, with a Reviewer alongside.
5. T-0102 is `security_touching` → **Security Reviewer** walks S1–S3 across `CancelOrder`, names the
   exact hole if any ("partner X can cancel customer Y's order — no ownership check at line N"),
   Backend Dev fixes it, Security re-verifies.
6. **QA** writes and runs the test plans (including the cross-user cancel attempt → must be rejected).
7. **PM** confirms every gate is green, marks the tickets `done`, updates the sprint status, and —
   because there were manual steps — has already flagged them to you. Nothing is committed/pushed
   unless you ask.

You watched none of the mechanics. You read `backlog/status/sprint-N.md` when you want the summary,
and `backlog/questions/open.md` if the team needed a decision from you.

---

## 4. When the team needs *you*

The team is autonomous but not reckless. It escalates a decision to you (and only you) when it
genuinely can't be derived from the code, the docs, or a sensible default — by writing a question to
[`backlog/questions/open.md`](./backlog/questions/). Blocking questions surface at the next
checkpoint; non-blocking ones proceed on a documented default. When you answer, the decision is
locked into an ADR/story/charter so it's never asked again.

You're also the only one who runs the two **owner-only** steps (per your `CLAUDE.md`): **EF
migrations** and **NSwag client regeneration**. The agents detect when these are needed, describe the
exact delta, flag them on the ticket, and hold dependent work until you confirm.

---

## 5. The quality bar (because you're going to PROD)

You said it plainly: not in production yet, so fix things *now*, for the long game, not with
throwaway patches. That's baked in:

- **`knowledge/conventions.md`** sets a "would I run this unattended in production with real
  customers and real money" bar, and forbids temporary workarounds, hardcoded strings, `any`/`dynamic`,
  and magic numbers.
- **`knowledge/security-rules.md`** carries the S1–S10 laws — derived from the real security
  regression this codebase already had — and the Security Reviewer enforces them.
- **`process/quality-gates.md`** is the checklist a change clears before `done`: conventions, AC
  evidence, security, architecture seams, performance, tests, contract/docs parity.
- The **Optimizer** keeps the running cost down (N+1s, indexes, bundle size) so scale doesn't hurt.

The harvested seniority from your old setup — the 684-line backend guide, the security rules, the
Angular/Compose conventions — wasn't thrown away. It was folded into the `knowledge/` catalog, which
every developer agent reads first. The old YAML system is archived under `agents/_legacy/` for
history.

---

## 6. The first real job (waiting for your go)

Once you've approved this setup, the first job is the one you named: **a full audit of the codebase**
to find what's missing, half-built, spaghetti, hardcoded, insecure, or slow — before PROD. The PM
will fan this out **wide and in parallel**: one analyst per subsystem looking for functional gaps,
the Reviewer/Security/Optimizer sweeping their dimensions, each writing ranked findings to
`backlog/audits/`. The PM then converts every finding into a prioritized ticket in `INDEX.md`, and
the build-fix loop above takes over. You'll get one audit report and a ready-to-execute backlog,
not a wall of raw output.

---

## 7. How you drive it day to day

- **To start work:** just describe what you want, in plain language. The Orchestrator hands it to the
  PM. (Optionally, "have the PM ..." to be explicit.)
- **To check status:** read `backlog/status/sprint-N.md` and `backlog/INDEX.md`, or ask the PM.
- **To answer the team:** edit `backlog/questions/open.md`.
- **To change how the team works:** edit a charter in `.claude/agents/` or a `process/` doc — it
  takes effect on the next invocation, and the change is reviewable in Git like any code.

That's the whole system. Approve it, tweak any charter you'd run differently, or tell the PM to begin
the audit.
