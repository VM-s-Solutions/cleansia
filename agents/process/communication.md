# Communication Protocol

The team coordinates through **Git-tracked artifacts only**. There is no agent-to-agent chat, no
shared memory, no verbal hand-off. If a decision isn't written to a file, it didn't happen.

This is deliberate: every hand-off is reviewable, every decision is traceable to a commit, and the
whole history of *why* the system looks the way it does is reconstructable. For a platform that is
about to go to production and will be expensive to change later, that traceability is the whole
point.

---

## The channels

| What | Channel (file) | Written by | Read by |
|---|---|---|---|
| A unit of work + its state | `backlog/tickets/T-NNNN-*.md` | PM (state), devs (notes) | everyone on the ticket |
| The backlog at a glance | `backlog/INDEX.md` | PM | PM, owner |
| Requirements / behavior | `backlog/stories/US-*.md` | Analyst | PM, devs, QA |
| Architecture decisions | `backlog/adr/NNNN-*.md` | Architect | everyone |
| How we build (patterns) | `knowledge/*.md` | Architect | every developer |
| Progress for the owner | `backlog/status/sprint-N.md` | PM | owner |
| Blockers & questions | `backlog/questions/open.md` | any agent | owner (answers), PM |
| Test plans & results | `backlog/test-plans/T-NNNN.md` | QA | PM, devs |
| Review verdicts | the ticket's `## Review` section | Reviewer / Security / Optimizer | PM, devs |
| Audit findings | `backlog/audits/*.md` | Analyst / Architect / Reviewers | PM (→ tickets) |

A developer hands off to a reviewer by **finishing the diff and writing an implementation note in
the ticket**. The PM, seeing the ticket in `in_review`, invokes the reviewer, who writes a verdict
in the ticket's `## Review` section. No one messages anyone.

---

## Escalation: how the team asks the owner

When an agent hits something it cannot decide — a business rule it doesn't know, an ambiguous
requirement, a decision with lasting cost — it does **not** guess silently and it does **not** stop
the world. It:

1. Appends a question to [`../backlog/questions/open.md`](../backlog/questions/) using the format
   below.
2. Marks it `blocking: yes` only if work genuinely cannot proceed without the answer; otherwise
   `blocking: no` and the agent proceeds with the **most defensible default**, documenting the
   assumption in the ticket.
3. The PM surfaces open blocking questions to the owner at the next checkpoint.
4. When the owner answers, the answer is moved to `answered.md`, and the decision is locked into
   the relevant artifact (ADR, story AC, or charter) so it never has to be asked again.

```markdown
### Q-0007 — [blocking: yes] Per-employee pay override precedence
- **Raised by:** backend (T-0042)
- **Date:** 2026-06-01
- **Question:** When both a per-employee and a per-service pay config exist, which wins?
- **Why it matters:** Determines the lookup order in PayCalculationService; changing it later
  means recomputing historical invoices.
- **Default taken (if non-blocking):** —
- **Answer:** _(owner fills this in)_
```

> **Escalate up, not sideways.** An agent never asks another agent to make a business decision —
> business decisions go to the owner via `questions/open.md`. Agents *do* defer technical decisions
> to the Architect by leaving a note in the ticket and having the PM invoke the Architect.

---

## Batching status to the owner

The PM is the **only** agent that reports progress to the owner, and it **batches**. The owner is
not pinged mid-ticket. Status surfaces at:

- a **sprint checkpoint** (the PM writes/updates `status/sprint-N.md`), or
- when a ticket has been `blocked` longer than a checkpoint, or
- when `questions/open.md` has an unanswered `blocking: yes` entry.

Everything else stays in the artifacts until the owner asks. This keeps the owner's attention for
the decisions that actually need a human.

---

## Anti-patterns (rejected on sight)

- An agent describing what it "told" or "asked" another agent — there is no such channel.
- A decision that exists only in a chat turn and not in a file.
- A developer silently inventing a business rule instead of raising a question.
- The PM pinging the owner for routine progress instead of batching into the sprint doc.
- Two agents editing the same file concurrently without the PM sequencing them (the PM owns
  ordering to avoid write races; when true parallel file edits are unavoidable, isolate them).
