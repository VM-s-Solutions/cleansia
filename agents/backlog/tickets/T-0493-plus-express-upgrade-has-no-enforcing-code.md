---
id: T-0493
title: Plus advertises an express upgrade and there is no code that enforces it
status: draft
size: M
owner: backend
created: 2026-08-02
updated: 2026-08-02
depends_on: [T-0491]
blocks: []
stories: []
adrs: []
layers: [backend]
security_touching: false
manual_steps: []
sprint: 15
---

## Context

**Source: the Cleansia Plus audit (2026-08-02).** *"Express upgrade has no enforcing code at all."*

**Status of this claim: RELAYED from the investigation, traced by it to file:line, NOT re-verified by
the PM.** Labelled as such deliberately — sprint-14 §2.12 records what happens when the PM stamps a
relayed claim as verified. The first thing this ticket's developer does is re-establish it (AC1).

### Why this is not a simple "add the check"

**Nobody has defined what express means.** The three candidate meanings produce three entirely
different builds:

| Reading | What enforcement looks like | Size |
|---|---|---|
| A **price** benefit — Plus members do not pay the express surcharge | one condition in the surcharge calculation | `S` |
| A **scheduling** benefit — a Plus member's order jumps the assignment queue | a change to how orders reach cleaners; there may be no queue to jump | `M`–`L` |
| A **guarantee** — a committed arrival window | an SLA with a failure path (what happens when it is missed?) | `L`, and it is a new product |

**T-0491 AC5 is required to produce this specification**, which is why this ticket is
`depends_on: [T-0491]` and why its acceptance criteria below are deliberately shaped around "the
ruling", not around an invented mechanism.

**One adjacent fact that is separately confirmed and separately ticketed:** there is a **currency bug
in the express surcharge** — **T-0496**. Its existence proves an express surcharge *path* exists,
which is evidence for reading A but is not proof of it.

## Acceptance criteria

- [ ] **AC1 — RE-ESTABLISH the finding before fixing it.** Search the backend for every reference to
      express/priority/rush on the order path, and state in `## Review`: what express code exists,
      what it does, and where a membership check would have to go. **If the finding is wrong and
      enforcement does exist somewhere, say so and close this ticket** — that is a successful
      outcome, not a failure. Evidence: the file:line inventory.
- [ ] **AC2 — the implementation matches T-0491 AC5's specification of "express", exactly.** The
      diff cites the ruling. **If the ruling lands on reading B or C, STOP: this ticket is
      mis-sized** and the correct output is a re-file at the right size with the panel's design, not
      an `M` that quietly becomes an `L`. Evidence: the ruling reference, or the re-file.
- [ ] **AC3 — the enforcement is SERVER-SIDE.** Whatever express means, the check that decides
      whether a customer gets it lives in a handler or validator, not in a client. **T-0494 exists
      because exactly this mistake was already made once on the recurring perk** — a client-side gate
      that a direct API call walks past. Evidence: the check at file:line, plus an integration or
      host test that calls the endpoint **without** an active membership and is refused.
- [ ] **AC4 — a non-member is refused and a member is granted, both proved by test.** Two cases
      minimum, both executed. Evidence: the tests plus the run.
- [ ] **AC5 — a lapsed/cancelled membership is treated as a non-member.** `UserMembership` carries a
      `MembershipStatus` (`Core.Domain/Memberships/MembershipStatus.cs`); the check must read the
      status, not merely the existence of a row. **A perk that survives cancellation is a revenue
      leak with the same shape as the defect being fixed.** Evidence: a third test case.
- [ ] **AC6 — the customer-facing copy and the enforcement agree.** After this lands, quote the perk
      copy (T-0491 AC1's table) and state that the code now does what it says. If they still
      disagree, the copy change is named as a client ticket, not done here. Evidence: the statement.
- [ ] **AC7 — a test that goes red against the pre-fix code (Gate 0.5 leg 1).** The verifier re-runs
      it **un-cached** and states what it could not verify.
- [ ] **AC8 (Gate 0.5)** — `Cleansia.Tests` / `Cleansia.IntegrationTests` / `Cleansia.HostTests` run
      **locally**, baselines **2295 / 108 / 75**.

## Out of scope

- **Building a scheduling queue or an arrival-window SLA.** If T-0491 rules that way, AC2 requires a
  re-file. This ticket does not become that build in place.
- **The express-surcharge currency bug** — **T-0496**, which is mechanical and has **no dependency on
  T-0491**. It should ship first and independently; it is a correctness defect regardless of what
  express means.
- **The other perks** — T-0492, T-0494, T-0495.
- **Any client.** If the ruling needs a UI change, it is named, not built here.

## Implementation notes

**No panel of its own — T-0491 is the panel**, and AC5 of that story is this ticket's specification.

**Gate 6.5 applies** if the ruling touches the money path (reading A changes what a customer is
charged). Flag at dispatch once the ruling is known.

**Read first:** `Core.Domain/Memberships/UserMembership.cs` + `MembershipStatus.cs`, and however
T-0494 ends up expressing "does this user have an active Plus membership" — **the two tickets should
use the same predicate, not two.** If T-0494 lands first, reuse its helper and say so; if this lands
first, write the predicate somewhere T-0494 can reuse it. Recorded on both tickets.

## Status log
- 2026-08-02 — **draft (created by pm from the Cleansia Plus audit).** **Filed with the finding
  explicitly marked RELAYED, not PM-verified** — AC1 re-establishes it before any fix, and a
  "the finding was wrong" outcome closes the ticket successfully. `depends_on: [T-0491]` because
  "express" has three plausible meanings spanning `S` to a new product, and AC2 forces a re-file
  rather than letting an `M` grow into an `L` in flight.

## Review
