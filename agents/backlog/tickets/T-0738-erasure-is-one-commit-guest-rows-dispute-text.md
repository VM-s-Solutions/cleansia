---
id: T-0738
title: Erasure is one commit, reaches guest rows, and keeps dispute text three years (A1, A7, L3)
status: done
size: M
owner: —
created: 2026-09-14
updated: 2026-09-14
depends_on: []
blocks: [T-0739]
stories: []
adrs: [ADR-0062]
layers: [backend, db]
security_touching: true
manual_steps: []
sprint: 16
---

## Context

Owner rulings 2026-09-14 on ADR-0062's review findings and questions. **A1** "fix it" — the erasure
walk (`GdprDeletionService.AnonymizeUserDataAsync`) committed mid-walk through
`RefreshTokenService.RevokeAllForUserAsync` → `CommitRevokeWithRetryAsync`, so a throw below it left a
half-erased subject with no request on record. **A7** "extend" — reach the customer audit rows of
guest bookings that later became the subject's. **L3** "keep it for 3 years then delete — cleaner
and better for defence" — the dispute text was blanked at erasure.

## Doing

- **One commit.** A non-committing revoke for the erasure path (`StageRevokeAllForUserAsync`) whose
  rows ride the erasure's single UnitOfWork commit; a concurrency conflict on a token row surfaces as
  the erasure's own failure. Logout keeps its committing shape.
- **Guest rows.** Blank IP / DeviceLabel / DeviceId on customer audit rows with `UserId == null` whose
  `ResourceType == "Order"` and `ResourceId ∈` the subject's orders — tracked `Pseudonymise()` calls,
  same commit, the immutability walk still seeing one delete site.
- **Dispute text.** `Dispute.TextRetainedUntil` (DateTimeOffset?) stamped `now + retention.dispute_text.years`
  (`DisputeTextRetentionYears = 3`, floor > 0); the retention sweep gains a task that blanks the
  description and the messages' text once the stamp is past and clears it (`Dispute.Anonymize()`
  reused); roster verdict *RetainedForDefence*; the evidence blobs still deleted at erasure.
- Regenerate `Initial`. No DEV drop.
- Tests: a poisoned final commit leaves the subject un-anonymised with valid tokens and no
  `GdprRequest`; a successful erasure revokes every token; the guest-row case; the dispute text present
  with the stamp set, blanked by the sweep once past and not before; roster; the IL walk.

## NOT

No change to what erasure keeps for orders (the cancellation reason stays). No change to
`DeleteUserAccount`'s blocking rules. No notification. Not the deletion-outcome record (T-0739).

## Done looks like

Tests / IntegrationTests (Gdpr + Auditing + DataRetention) / HostTests green; one `Initial`; the
report names the migration id.

## Status log

- 2026-09-14 — shipped in `fe51557c` (one commit; `TextRetainedUntil` + `retention.dispute_text.years`
  + the `DisputeText` task; `Initial` → `20260914115922`) and `c26aa541` (review: the guest-row walk
  **deleted** — `Order.UserId` is written once at creation from the same session the audit row takes
  its `UserId` from and only ever nulled afterwards, so a customer audit row with no user on an order
  the subject owns cannot exist; `PseudonymiseGuestRowsForOrdersAsync`, its interface entry, its test
  and the immutability walk's second sanctioned caller went with it; the dispute repository's erasure
  read now says what the caller does). **A7 residual stands** — the only lever is matching guest
  orders by e-mail, filed as Q-GDPR-01. Recorded in ADR-0062 D5 as amended.
- 2026-09-15 — Q-GDPR-01 ruled *yes* and shipped as **T-0751** (`6ab64fff` + `ca6dc84e`): the e-mail
  lever, `SubjectOrders`; `PseudonymiseGuestRowsForOrdersAsync` is back as the second sanctioned
  caller, keyed on the e-mail-matched orders this time.
