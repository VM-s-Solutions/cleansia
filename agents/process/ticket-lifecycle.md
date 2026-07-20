# Ticket Lifecycle

A **ticket** is the atomic unit of coordinated work. One ticket = one shippable change with its
own acceptance criteria. Tickets live in [`../backlog/tickets/`](../backlog/tickets/) as
`T-NNNN-<slug>.md` and are indexed in [`../backlog/INDEX.md`](../backlog/INDEX.md).

The **PM owns every state transition.** No other agent edits a ticket's `status` field.

---

## State machine

```
        ┌──────────────────────────────────────────────────────────┐
        │                                                          │
draft ──► ready ──► in_progress ──► in_review ──► qa ──► done       │
            │            │              │          │                │
            │            └──────────────┴──────────┴──► blocked ────┘
            │                                              │
            └──────────────────────────────────────────────┘
                              (unblocked → back to prior state)
```

| State | Meaning | Who moves it out |
|---|---|---|
| `draft` | Captured but not yet specced. Needs AC, scope, sizing. | PM (after Analyst/Architect input) |
| `ready` | Passes the **Definition of Ready** (below); `depends_on` satisfied; safe to start. | PM (when picking it up) |
| `in_progress` | A developer instance is implementing it. A reviewer runs alongside. | PM (when work + review converge) |
| `in_review` | Implementation done; reviewer + (if needed) security/optimizer verifying. | PM (when review passes or requests changes) |
| `qa` | Review passed; QA executing the test plan against the running app. | PM (when QA passes) |
| `done` | Merged, verified, status logged. | — terminal |
| `retired` | Cancelled by an explicit supersede/owner decision — the WORK is no longer wanted (e.g. a design amendment killed it). Not a failure state; the ticket records why. | PM or Architect (citing the superseding artifact) — terminal |
| `superseded` | The QUESTION the ticket asked was answered by other shipped work (typical for spikes). Points at what answered it. | PM (citing the superseding ticket) — terminal |
| `blocked` | Cannot proceed: unanswered blocking question, failed dependency, owner decision needed. | PM (when the blocker clears) |

A ticket that fails review or QA does **not** go backwards in the index; it stays in
`in_progress`/`in_review` with the reviewer's change-requests appended, and the same developer
instance fixes it. Only a *dependency failure* or an *owner decision* sends it to `blocked`.

---

## Ticket frontmatter (canonical shape)

```yaml
---
id: T-0042
title: Per-employee pay override admin UI
status: in_progress            # PM owns this field
size: M                        # S | M | L  (L must be split)
owner: frontend                # the charter currently working it
created: 2026-06-01
updated: 2026-06-01
depends_on: [T-0040, T-0041]   # ticket ids that must be `done` first
blocks: [T-0050]               # tickets waiting on this one
stories: [US-admin-0007]       # user stories this satisfies
adrs: [0003, 0012]             # ADRs in force for this work
layers: [backend, db, frontend]  # which stacks it touches → which agents run
security_touching: false       # true → Security gate is mandatory
manual_steps: [ef-migration, nswag-regen]   # owner-only steps this ticket needs
sprint: 5
---
```

The body holds: **Context**, **Acceptance Criteria** (Given/When/Then), **Out of scope**,
**Implementation notes**, and a **Status log** (one line per transition). See
[`../templates/ticket.md`](../templates/ticket.md).

---

## The cross-stack flow

A typical feature ticket touches several layers. The PM sequences the layers and runs review in
parallel:

```
0. DELIBERATION (before any ticket exists) — per agents/process/deliberation.md:
     analyst PANEL  (author + 2-3 challengers + lead)  defends the user story  → consensus
     architect PANEL (author + 2-3 challengers + lead) defends the decision/ADR → consensus
     each panel's owning role updates its living doc (analysts/<domain>.md, architecture/decisions/)
        │  (only a FINALIZED story/ADR becomes a ticket)
        ▼
1. analyst   — (already done in the panel) story is finalized with AC + deliberation trail
2. architect — (already done in the panel) ADR accepted, living decision doc updated
        │
        ▼  (contract locked: entity shape, API DTOs, error codes)
   qa       — drafts the test plan from the AC, in parallel (becomes the developers' TDD target)
3. db        — migration + entity config + repository        ┐
4. backend   — test-first: failing test from AC → handler   │ each developer step
5. frontend  — test-first: facade spec → component + i18n    │ runs with a `reviewer`
6. android   — test-first: viewmodel test → screen + strings │ instance IN PARALLEL
7. ios        — test-first: view-model test → view (parity)   ┘  (red → green → refactor)
        │
        ▼
8. security  — mandatory iff `security_touching: true`
9. optimizer — for perf-sensitive or hot-path changes
10. qa       — execute the test plan against the running app
        │
        ▼
11. PM       — all gates green → merge → status: done → log → pick next
```

### Parallelism rules

- **Reviewer always runs in parallel with the developer**, not after. The developer produces the
  change; the reviewer instance reads the same ticket + diff and produces a verdict concurrently.
  The PM merges the two before transitioning state. (This is the behavior you specifically asked
  for: review happens *alongside* implementation, not as a serial gate.)
- **Backend and frontend may run in parallel** once the API contract is locked (the ADR / ticket
  fixes the DTO shape). Until the contract is locked, frontend waits.
- **Android and iOS run in parallel** with each other — same contract, two platforms.
- **DB must finish before backend** when a migration changes the shape backend code compiles
  against.
- **Independent tickets fan out freely** — the PM may have `backend #1` on T-0042 and `backend #2`
  on T-0048 at the same time, each paired with its own reviewer.
- **L10n/i18n** can proceed any time after AC are fixed.

---

## Definition of Ready (a ticket can't go `ready` without this)

A `draft` only becomes `ready` when **all** hold — this stops half-specced tickets from wasting a
developer's run and stops the backlog from rotting as it grows:

1. **Not a duplicate.** The PM searched `INDEX.md` and `backlog/audits/` first; this isn't already
   captured by an open ticket or an audit finding. (If it overlaps one, merge instead of forking.)
2. **AC are present and observable** (Given/When/Then, verifiable outcomes — not "make it nicer").
3. **Sized** S/M/L, and any `L` is **split** before it goes ready.
4. **Dependencies known** (`depends_on` listed and either `done` or themselves tracked).
5. **`manual_steps` assessed** — does it need an EF migration or NSwag regen? If so they're listed and
   the owner is flagged.
6. **`security_touching` and `layers` set** so the PM routes and gates correctly.
7. **The canonical archetype is identified** (which `consistency.md` rule set applies), so the
   developer mirrors the right existing feature.

A ticket failing any of these stays `draft`; the PM completes it (invoking `analyst`/`architect` as
needed) before promoting it.

## Sizing

| Size | Effort | Files | ADR? | Rule |
|---|---|---|---|---|
| **S** | < ~2h | 1–3 | no | Single concern. May skip the analyst/architect steps. |
| **M** | ~half day | several, often cross-layer | maybe | The default. Full flow. |
| **L** | > ~1 day | many, multiple layers, new patterns | likely | **Must be split** into S/M tickets before going `ready`. |

If a ticket is discovered mid-flight to be an `L`, the developer stops, writes a note in the
status log, and the PM splits it. We never let an `L` run as one ticket — it destroys traceability
and review quality.

---

## "Done" means

A ticket is `done` only when **all** of these hold:

1. AC each have verifiable evidence (a test, a screenshot, a log line, or a reviewer confirmation).
2. The reviewer approved (and security/optimizer approved if they were in scope).
3. QA executed the test plan and recorded the result.
4. Any `manual_steps` are flagged to the owner (the agents do **not** run migrations or NSwag regen).
5. The `INDEX.md` row and the sprint status doc are updated, and the status log has a line for the
   final transition.

Anything short of this stays out of `done`. We do not mark work complete on hope.

### When the in-workflow gate did not run (hand-gating)

A final-report (StructuredOutput) failure can kill a ticket's in-workflow reviewer lane while the work
itself landed fine on disk — observed three times across two waves (see `quality-gates.md` §"A
final-report failure ≠ a work failure"). Such a ticket may still reach `done`, but ONLY when both hold:

1. The ticket's `## Review` carries a **MANUAL-GATE block** recording the concrete evidence the
   orchestrator inspected by hand: the files it read, the commands it ran itself (with exit codes and
   pass/fail counts), and which AC each piece of evidence covers. "The work looked fine" is not a
   MANUAL-GATE block — it is the narration Gate 8 forbids.
2. The `INDEX.md` row carries a **manual-gate provenance marker** (e.g. `done (manual-gate)`), so
   nobody later mistakes a hand-gated ticket for one whose reviewer lane actually ran.

A ticket with neither is not `done` — it is `in_review` with a dead reviewer lane, and the PM re-runs
the gate or hand-gates it properly.
