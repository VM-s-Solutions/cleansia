---
id: T-0693
title: Confirmed can be true with zero assignees — a drop or cover never walks it back
status: todo
size: M
owner: —
created: 2026-09-08
updated: 2026-09-08
depends_on: [T-0691]
blocks: []
stories: []
adrs: [ADR-0057]
layers: [backend]
security_touching: false
manual_steps: []
sprint: 16
---

## Context

ADR-0057 settled that `Confirmed` means **a cleaner took this job**. It also recorded, as a known
consequence, that the status is never walked back: a drop, a cover request or an admin rejection
removes the assignment while the status stays `Confirmed`.

So the shipped meaning is precisely *"a cleaner took this job"* and **not** *"a cleaner is currently
on it"*. Six sweeps already compensate by selecting `Confirmed` **and** `AssignedEmployees.Any()`,
which is the same shape of compensation ADR-0057 was written to remove one layer up.

## Acceptance criteria

- [ ] **AC1** — Decide, and record, whether an order that loses its last assignee returns to `New`.
- [ ] **AC2** — If yes: the transition exists, and `AdminOverrideOrderStatus`'s prohibition on backward
      moves is reconciled with it explicitly rather than bypassed.
- [ ] **AC3** — If yes: the ~eight test files that build a `Confirmed` order with no assignments are
      updated, and the six sweeps drop their now-redundant `AssignedEmployees.Any()` term.
- [ ] **AC4** — If no: the reason is written into ADR-0057's consequences so the next reader does not
      re-open it.

## Out of scope

- The meaning of `Confirmed` itself. Settled by ADR-0057.

## Implementation notes

`AdminOverrideOrderStatus` forbids backward moves for admins — that is its own decision and the reason
this is not a one-liner. Roughly eight test files construct exactly the `Confirmed`-with-no-crew
fixture; adding the invariant without addressing them triples the diff, which is why T-0691 left it.

**This is a decision ticket before it is a code ticket.** Do not start building.

## Status log

- 2026-09-08 — filed from the T-0691 out-of-scope list. Pre-existing behaviour, not introduced by T-0691.
