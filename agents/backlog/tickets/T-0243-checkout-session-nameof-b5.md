---
id: T-0243
title: CreateMembershipCheckoutSession UserNotFound — nameof(Command) → nameof(userId) (B5 consistency)
status: draft
size: XS
owner: —
created: 2026-06-13
updated: 2026-06-13
depends_on: [T-0179]
blocks: []
stories: []
adrs: []
layers: [backend]
security_touching: false
manual_steps: []
sprint: 5
source: T-0179 (LG-07) carried finding — sibling of the B5 smell T-0179 fixed, explicitly out of scope there
---

## Context
Surfaced (not fixed) by **T-0179** (LG-07, unify membership subscribe path). The sibling handler
`CreateMembershipCheckoutSession` constructs its `UserNotFound` failure with `nameof(Command)` instead
of the offending field `nameof(userId)` — the **same B5 smell** (`consistency.md` B5: failure
construction names the command rather than the offending field) that T-0179 fixed in
`CreateMembershipSubscription.cs`, but which T-0179 explicitly scoped **out**. This is consistency
debt, **not a runtime defect**: the error message is correct; only the `Error.Code`/field-name
payload is wrong (it says `"Command"` where it should say `"userId"`).

No-decision note: this is a pure mechanical rename of a `nameof` argument with **no new behavior and
no architectural decision** — it skips the deliberation panel per the PM process. It mirrors a fix
already reviewed and accepted in the sibling handler.

## Acceptance criteria
- [ ] **AC1 (B5 rename)** — In `CreateMembershipCheckoutSession.cs` (~line 45) the `UserNotFound`
  failure's first `Error` arg is changed from `nameof(Command)` to `nameof(userId)` (the offending
  session-derived field). Any other per-field/per-entity error keys in the handler that are already
  correct are left as-is.
- [ ] **AC2 (no functional change)** — The endpoint behavior, the `BusinessErrorMessage.UserNotFound`
  message, the Command/Response shapes, and the OpenAPI contract are unchanged (the change is to a
  runtime error-payload field name, not the generated schema). No nswag-regen expected — confirm at
  review and record the no-regen finding.
- [ ] **AC3 (pin it if practical)** — If practical, add a small validator/handler assertion in
  `Cleansia.Tests` (red-first) that the `UserNotFound` failure carries the correct field name, locking
  B5 against regression — mirroring T-0179's `CreateMembershipSubscriptionContractLockTests`. If a
  test harness for this handler is impractical, record why and rely on the mechanical review.

## Out of scope
- Any other consistency rule on this handler (B8 try/catch was closed by T-0147; do not re-touch).
- Collapsing or re-pointing the two subscribe paths (T-0179 already documented the split).
- New error constants or message-text changes.

## Implementation notes
- Symbol: `CreateMembershipCheckoutSession` handler in
  `src/Cleansia.Core.AppServices/Features/Memberships/CreateMembershipCheckoutSession.cs` (~line 45).
- This is the exact follow-up T-0179 flagged in its status log
  ("the sibling `CreateMembershipCheckoutSession.cs:45` carries the same `nameof(Command)` smell but
  is OUT OF SCOPE per this ticket's B5 fix-site note — flagged for a follow-up, not touched here").
- Comment discipline: no ticket-ID tokens in source (per `conventions.md`).

## Status log
- 2026-06-13 — draft (created by pm; T-0179 carried B5 sibling-finding made a ticket — Wave-5 candidate).

## Review
<!-- reviewer / security / optimizer write verdicts here; PM reconciles before advancing state -->
