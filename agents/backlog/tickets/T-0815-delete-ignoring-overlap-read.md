---
id: T-0815
title: Delete the orphaned tenant-ignoring overlap read and re-point S8
size: S
owner: pm
created: 2026-10-11
updated: 2026-10-12
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

- [x] **AC1** — No reference to the deleted method remains in `src/` or the living docs.
- [x] **AC2** — Every assertion the deleted method's tests made has an equivalent on a live method, shown by a mutation check.
- [x] **AC3** — The unit, integration and host suites pass.

## Work record

- 2026-10-11 — Filed from the post-Wave E ground truth and Mike's rulings.
- 2026-10-11 — Implemented by the backend lane. ADR-0039 is not edited.
  - `HasOverlappingOrderIgnoringTenantAsync` is gone from `IOrderRepository` and `OrderRepository`.
  - The private overload behind both wrappers is folded into the scoped `HasOverlappingOrderAsync`. It is the same expression tree, and `OrderStatusSetPredicatePlanTests` stays green.
  - Test changes:
    - the agreement test's single-window oracle is now the scoped gate, asked under each company, beside the unchanged literal table;
    - the status theory's batch oracle is now `ProbeSlotAsync`;
    - the tenancy test that duplicated the batch theory's null row is deleted;
    - both digest suites now count `HasOverlappingOrderAsync`, and they still assert that no single-window probe runs.
  - S8's worked example now points at the live pair, both over `LiveCommitmentsInWindow`, and the dispatch decision doc records the deletion.
  - AC2 was shown by mutation in a scratch copy, each mutation restored afterwards:
    - reading the batch through `GetDbSet()` turns the batch-form tenancy theory and the PostgreSQL agreement test RED;
    - limiting the agreement oracle to one company is RED;
    - changing the SQL end term from `>` to `>=` is RED, because the touch-end rows appear;
    - making the digest probe each candidate turns 4 tests RED;
    - dropping the scan floor is RED on the re-pointed oracles;
    - dropping `Confirmed` from the status set is RED on the re-pointed oracles.
  - Fix pass: the comment at `HasOverlappingOrderTenancyAndScanFloorTests.cs:85` names what remains, "the sweep needs a tenant-ignoring form (now the batch, …)".
- 2026-10-12 — Committed as `5ce5048`, which builds on its own. The suites are on T-0814's line. The re-review's runs are the ones made on a tree byte-identical to this branch's head.
- 2026-10-12 — Shipped in [#322](https://github.com/VM-s-Solutions/cleansia/pull/322). Exact-head CI on `1136e16` passed all 9 check runs with no re-run: Backend, Frontend and e2e-smoke, Android, iOS, Docs, iOS Symbols, booking-policy parity and Secret scan. Merged normally with the SHA guard as `6840882`. No deploy ran. Evidence: `raw/ci/p1-pr322-ci-merge-closure-001.json` (sha256 `d31e29ba…`) in the off-tree Wave F folder.

## Review

- **AC1 reads as "no live, present-tense reference".**
  - `git grep HasOverlappingOrderIgnoringTenant -- src .github` returns nothing.
  - Dated history still names the method in S8's parenthetical and in `preferred-cleaner-dispatch.md`, including the struck A0 row, as the ground truth planned.
  - The correctness review asked for this reading to be recorded, rather than for the AC to be ticked on its literal wording.
- **Correctness: approve after the fix pass. Security: approve-with-notes.**
- **Both lanes asked one question; the security review answered it.** Do `TakeOrder` and `AdminReassignOrder`, on the scoped gate, miss a commitment the cleaner holds under another company? No, as written:
  - `TakeOrder` runs under the cleaner's own claim.
  - `AdminReassignOrder` reads the employee scoped.
  - `ApproveEmployee` pins a cleaner's company to their work country's operator, so all of a cleaner's commitments sit in their own company.
- **Follow-up, not folded in (S8, latent).** `GetBusyEmployeeIdsInWindowAsync` reads tenant-ignoring under a name that sounds scoped. Every caller's ids are server-derived today. Rename it, or add it to the T-0606 roster. Reported for a ticket of its own.
