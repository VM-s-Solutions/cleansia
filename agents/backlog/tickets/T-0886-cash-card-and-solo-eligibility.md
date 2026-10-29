---
id: T-0886
title: Require a usable saved card for single-cleaner cash bookings
size: M
owner: Michael Chaban
created: 2026-10-29
updated: 2026-10-29
depends_on: []
blocks: []
stories: [US-mobile-20261029]
adrs: [ADR-0070]
layers: [backend, frontend, android, ios, docs]
security_touching: true
---

## Context

Mike requested this behavior on 29 October 2026 after testing build18. Current source was inspected before implementation. This batch follows the completed rollout and does not authorize another deployment, reset or upload.

## Acceptance criteria

- [x] **AC1** — A current authenticated customer needs a verified, captured, active, unexpired card in the booking currency plus one server-required cleaner to select or submit cash.
- [x] **AC2** — Enforce creation, recurrence, confirmation/materialization and card-to-cash fallback before financial side effects; retain debt/open-cash limits and no automatic card charging.
- [x] **AC3** — A job requiring multiple cleaners or already staffed by more than one cannot become cash-eligible merely because no cleaner is assigned yet.

## Out of scope

PRO access, unrelated fixes, public release, new automatic charges, and changing a status layout before Mike selects a concept.

## Work record

- 2026-10-29 — Ground-truthed the current implementation and started the requested change. Evidence and exact command receipts are retained in the mobile-refinements-20261029 work report.
- 2026-10-29 — Shared server enforcement is implemented for one-off creation and its factory backstop, recurring create/edit/confirm/materialization, recurring-list eligibility and card-to-cash fallback. The card query is pinned to the authenticated or authorized stored customer and booking currency, including a booking in another operator's market. Captured, active, unexpired cards require a nonblank verified payment-method id; expiry uses UTC. New cash orders expose at most one seat even with legacy spare capacity. Actual crew above one refuses cash collection for either tender. Already booked cash is not converted if its card is later removed or expires.
- 2026-10-29 — Existing receivable and two-open-cash limits remain. Stripe reconciliation still repairs an already settled card payment before fallback refusal; missing-card refusal precedes cancellation of an outstanding intent. No automatic charge, saved-card consent/version change, schema change, new capture step or rollout is part of this implementation.

## Backend evidence and remaining checks

Evidence directory: `/Users/michael/cleansia-work.noindex/mobile-refinements-20261029/`. INDEX.md remains the only ticket-status record. The acceptance criteria stay open pending the final combined-tree checks.

- [x] Solution build passed and full unit suite passed **9,142/9,142**, zero failures/skips. `backend-final-gates.json` and `test-results/final-backend-unit-full.trx` retain the exact commands, counts and unchanged source fingerprint.
- [x] Existing behavioral tests passed for missing/pending/expired/removed/unverified cards on recurring create/edit/confirmation, missing-card materialization skips, owner/currency read flags, UTC expiry boundaries, direct factory refusal before Add/notifications, and card-to-cash refusal before intent cancellation. Named cases include `CustomerCashStandingTests.A_Cash_Schedule_And_Its_Confirmation_Require_A_Usable_Captured_Card`, `OrderFactoryCashBackstopTests.A_Direct_Construction_With_No_Usable_Card_Cannot_Add_Or_Announce_A_Cash_Order` and `MarkCashCollectedHandlerTests.Card_To_Cash_Without_A_Saved_Card_Is_Refused_Before_Closing_The_Payment_Intent`.
- [x] Original full local PostgreSQL and Host runs completed and were retained: **1,038 total / 1,017 passed / 21 failed** and **518 total / 511 passed / 7 failed**. All failures were traced to older setup fixtures missing the card precondition or constructing multi-cleaner cash crews. Corrections change only seven IntegrationTests/HostTests fixture/helper files; all existing assertion lines are byte-identical, including the historical cash crew's two payroll recipients. Production and unit source hashes remain unchanged. See `backend-authoritative-failure-classification.json` and `backend-final-fixture-corrections.json`.
- [x] Cash-guard mutation executed: the existing absent-card row failed at the intended assertion with the bypass, then passed after byte-exact restoration (`backend-cash-mutation.json`).
- [x] Guarded rebuild, affected-class checks and full local PostgreSQL/Host successors passed:1038/518, zero failures (`backend-postgres-fixture-retry.json`). Original failures remain retained.
- [x] Current optimized iOS, Android3154, web1061/scoped lint/all3production builds and documentation build pass. Exact current-source receipts supersede the earlier native snapshots.

- 2026-10-29 — iOS source/native evidence: iOS booking/recurring require a current server-confirmed usable card in quote currency and exactly one quoted cleaner, with fresh reads before cash submission. JWT subject + existing login epoch accepts same-account credential rotation and rejects account/session changes; request sequence rejects stale reads. Deferred POST refusal tests verify immediate pending state, one recovery read to needsSavedCard and no auto-selected payment. Cash/identity/recovery coverage passes in Debug and the 16.4 floor. Full iOS formatter/linter pass and source seal are linked from [the iOS evidence record](../../analysts/mobile-refinements-ios-20261029.md). Root owns final combined-gate/acceptance status.

## Local verification review — 29 October 2026

Server solution build and unit9142/PG1038/Host518 pass. Phone and saved-card guard mutation proofs fail for the intended assertion then pass after byte-exact restoration. Android3154, optimized native suites and web1061 pass; cash rejection clears the choice and refreshes eligibility. Published terms must be aligned before a future deployment; automatic charging and consent are unchanged.

Ready for source review; no push, merge, deployment or upload was performed for this batch.
