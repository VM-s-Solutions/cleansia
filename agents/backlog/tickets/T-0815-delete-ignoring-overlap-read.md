---
id: T-0815
title: Delete the orphaned tenant-ignoring overlap read and re-point S8
size: S
owner: pm
created: 2026-10-11
updated: 2026-10-11
depends_on: [T-0808]
blocks: []
stories: []
adrs: []
layers: [backend, docs]
security_touching: true
---

## Context

Mike ruled "Delete" (Mike's rulings of 2026-10-11 (`/Users/michael/.codex/scratchpads/cleansia-wave-f-2026-10-11/raw/owner-decisions-2026-10-11.md`)). Since B005, `HasOverlappingOrderIgnoringTenantAsync` has no production caller: a tenant-escaping read one IntelliSense entry from a request handler, kept for nobody, the shape ADR-0039 D3.2 struck [BE-23, D3 lane].

## Doing

- Remove the method from `IOrderRepository` and `OrderRepository`; fold the private overload that served both wrappers into the scoped `HasOverlappingOrderAsync`.
- Re-point the S8 worked example in `docs/architecture/security-rules.md` to the live pair: the scoped `HasOverlappingOrderAsync` and the tenant-ignoring batch `GetOverlappedCandidateIdsIgnoringTenantAsync`, both over `LiveCommitmentsInWindow`.
- Move the deleted method's tenancy pins to the batch method. Give `LiveCommitmentWindowsAgreementTests` an oracle that needs no deleted code: the scoped gate asked once per company, plus the literal expected table.
- Fix the `HasOverlappingOrderStatusTests` class doc, and update the dispatch decision doc.

## NOT doing

- Editing ADR-0039 (accepted ADRs are not edited in place).
- Any change to the batch method's behaviour.

## Acceptance criteria (done looks like)

- [ ] **AC1** — No reference to the deleted method remains in `src/` or the living docs.
- [ ] **AC2** — Every assertion the deleted method's tests made has an equivalent on a live method, shown by a mutation check.
- [ ] **AC3** — The unit, integration and host suites pass.

## Work record

- 2026-10-11 — Filed from the post-Wave E ground truth and Mike's rulings.

## Review

Pending.
