---
id: T-0905
title: Decide pay when a team job's seat never fills (analysis filed)
size: S
owner: —
created: 2026-10-31
updated: 2026-10-31
depends_on: []
blocks: []
stories: []
adrs: []
layers: [analyst]
security_touching: false
---

## Context

Mike, 31 October 2026: *"if the seat never fills then there is a need to recalculate a salary for each
cleaner, analýze of what needs to be done"*, and *"the cleaner who joins late get the same, we don't resolve
the work internally between each cleaner"*. Today an empty seat's share is paid to nobody
(`CalculateOrderPay.cs`, and the framework contract says so). The analysis of what a change would take is
`agents/analysts/unfilled-team-seat-pay-20261031.md`.

## Acceptance criteria

- [ ] **AC1** — Mike answers the nine questions in §A.7 of the analysis (each carries a default; the
  first is which option, default A: divide by the cleaners on the job).
- [ ] **AC2** — If he decides to change it: an ADR, the contract and business-rule texts, and the pay code, as
  the analysis lists — filed as its own build ticket.

## Out of scope

Any code, contract or legal-seed change before AC1.
