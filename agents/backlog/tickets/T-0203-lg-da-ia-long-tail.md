---
id: T-0203
title: B/C/D-rule deviations + wrong-source ledger + CQRS-violation reads + magic strings + swallowed catches (long tail)
status: draft
size: M
owner: â€”
created: 2026-06-01
updated: 2026-06-01
depends_on: [T-0142]
blocks: []
stories: []
adrs: []
layers: [backend, frontend, mobile]
security_touching: false
manual_steps: []
sprint: 3
source: audit long tail (LG/DA/IA findings)
---

## Context
The Wave-3 long tail: the small, unglamorous deviations the slice audits (loyalty-growth LG-*,
disputes-addresses DA-*, identity-auth IA-*) turned up that no other ticket already owns. Each is a
single named offender that violates a `agents/knowledge/consistency.md` rule (B/C/D archetype), a
`conventions.md` "no magic constants" rule, or quietly corrupts/hides data. They are bundled into one
M ticket because each is a one-to-few-line change in an *unclaimed* file, the cluster shares the
"characterization-test-first, then remove the smell" shape, and grouping keeps the backlog honest
about the residue after the headline canonicalization tickets (T-0001â€¦T-0016) and the
finding-specific tickets (T-0007, T-0148, T-0176, T-0150, T-0142) have taken their slices.

**Dedup done.** Each item below was checked against `INDEX.md`, `TICKET-MAP.md`, and the existing
`tickets/`. The CQRS read (LG-09) is *explicitly carved out as out-of-scope* by both T-0148
(`T-0148-lg-01q-lg-03.md:91`) and T-0176 (`T-0176-lg-05-06f-09.md:112`) and reserved for this long
tail. The B5 fix in T-0007 covers only `CreateMembershipSubscription`'s one occurrence; the other
membership handlers are unclaimed. DA-9 (the `"CZE"`/geo/dispute-length constants) is T-0150 and is
**not** re-done here. The soft-delete aspect of `UnregisterDevice` (`Remove`â†’`Deactivate`) is T-0142
(IA-13 soft-delete) â€” this ticket does **not** touch delete semantics, only the device handlers'
raw-exception/magic-string smell (so it must serialize against T-0142, see notes).

Confirmed live offenders (real code, verified 2026-06-01):

- **LG-02 â€” wrong-source ledger [behavior fix].** `Features/Loyalty/Admin/RevokePointsManually.cs:57`
  passes `source: LoyaltyEarnSource.ManualGrant` for a **revocation**, so every manual revoke is
  recorded in the ledger as a grant â€” the audit trail is corrupted. `LoyaltyEarnSource`
  (`src/Cleansia.Core.Domain/Loyalty/LoyaltyEarnSource.cs`) has `OrderCompleted/OrderCancelled/`
  `Referral/ManualGrant` but **no revoke member**, so the fix adds a `ManualRevoke` value.
- **LG-09 â€” CQRS-violation read.** `Features/Loyalty/Admin/PreviewTierThresholdImpact.cs:21,25` models
  a pure preview (only `.AsNoTracking()` reads, lines 53,67-70) as `ICommand<Response>`, so it runs
  the UoW commit pipeline (write path) for a read. â†’ `IQuery<Response>`/`IQueryHandler` (no commit).
- **LG-05 â€” B5 across the remaining membership handlers.** `new Error(nameof(Command), â€¦)` /
  `nameof(command)` instead of the offending field in `Features/Memberships/`
  `CreateMembershipCheckoutSession.cs`, `CancelMembershipSubscription.cs`, `SwapMembershipPlan.cs`
  (T-0007 already fixes `CreateMembershipSubscription`).
- **LG-13 â€” B1 missing Response.** `Features/Marketing/SendSitewidePromo.cs:40` is `: ICommand` (no
  `Response` record) for an admin send action.
- **LG-14 â€” swallowed catch + no surface [behavior fix].**
  `loyalty-promo-codes/.../promo-codes-list.facade.ts:82-101` `deactivate()` uses
  `catchError(() => of(null))` and shows **nothing** on failure (no `showApiError`, C4) and skips the
  C3 `finalize`.
- **IA-13(handlers) â€” raw exception + hardcoded English.** `Features/Devices/RegisterDevice.cs:35-36`
  and `UnregisterDevice.cs` throw `new UnauthorizedAccessException("User ID not found in claims.")` â€”
  a bare framework exception with a magic English string instead of the canonical
  `BusinessResult.Failure` + `BusinessErrorMessage` path.
- **IA-19 â€” audit-actor/reason magic strings.** `Features/Gdpr/DeleteUserAccount.cs:24`
  (`"GDPR_DELETION"`) and `AdminDeleteUserAccount.cs:37` (`"GDPR_ADMIN_DELETION"`, `"admin"`) inline
  string literals for the deactivation reason/actor.
- **DA-12 â€” empty/swallowing catch.** `Features/Disputes/UploadDisputeEvidence.cs:109-115` â€” a
  completely empty `catch { }` hides SAS-URI generation failure with no logging (the evidence row is
  saved but the returned URL is silently null).

## Acceptance criteria
> Wave-3 rule: most items are refactors â€” **behavior unchanged (characterization test green) + the
> smell removed + `check-consistency.mjs` clean for the touched area**. The two items marked
> *[behavior fix]* deliberately change observable behavior; their tests assert the **new** correct
> behavior, with the old behavior pinned first to prove the bug.

- [ ] **AC1 (LG-02, behavior fix)** â€” Given a characterization test pinning that today's revoke writes
  a `ManualGrant` ledger row, When `RevokePointsManually.Handler` runs after the fix, Then a new
  `LoyaltyEarnSource.ManualRevoke` value exists and the revoke records its source as `ManualRevoke`;
  the test asserting `ManualGrant` is updated to assert `ManualRevoke`. No other ledger field changes.
- [ ] **AC2 (LG-09)** â€” Given a characterization test capturing the current `Impacts` output for a
  fixed account set, When `PreviewTierThresholdImpact` is converted from `ICommand<Response>` to
  `IQuery<Response>` (+ `IQueryHandler`), Then the same `Impacts` are returned, no UoW commit runs for
  this path, and `check-consistency.mjs backend` is clean for the file. Behavior identical.
- [ ] **AC3 (LG-05)** â€” Given each membership handler that returns a `BusinessResult.Failure`, When the
  three remaining handlers (`CreateMembershipCheckoutSession`, `CancelMembershipSubscription`,
  `SwapMembershipPlan`) are corrected, Then every `new Error(...)` first arg is
  `nameof(command.<Field>)` (B5), the `check-consistency.mjs` B5 scan reports **zero** hits for those
  files, and a unit test asserts each `Error.Code` is the field name. Returned `BusinessErrorMessage`
  values are unchanged.
- [ ] **AC4 (LG-13)** â€” Given `SendSitewidePromo`, When it is given a `Response` record (B1) and the
  admin controller returns it, Then the command is `ICommand<Response>`; behavior (the enqueued send)
  is unchanged, pinned by a handler unit test. *(NSwag drift â€” see manual_steps note.)*
- [ ] **AC5 (LG-14, behavior fix)** â€” Given a facade spec that today asserts a silent no-op on a failed
  `deactivate`, When the facade is fixed, Then a failed `deactivate` calls
  `SnackbarService.showApiError` (C4) and the pipe uses `takeUntil â†’ catchError(() => of(null)) â†’
  finalize` (C3); the spec asserts the error surfaces and loading resets. The success path is
  unchanged.
- [ ] **AC6 (IA-13 handlers)** â€” Given a characterization test pinning that the device handlers throw
  on a missing session, When `RegisterDevice`/`UnregisterDevice` are fixed, Then the missing-session
  case returns a `BusinessResult.Failure` with a `BusinessErrorMessage` key (no raw
  `UnauthorizedAccessException`, no inline English), and a unit test asserts the `Error.Code`. The
  happy path is unchanged.
- [ ] **AC7 (IA-19)** â€” Given the GDPR deletion handlers, When `"GDPR_DELETION"` / `"GDPR_ADMIN_DELETION"`
  / `"admin"` are replaced by named constants (one static home, e.g. `GdprAuditReasons.*`), Then the
  *string values written to the deactivation reason are byte-identical* (characterization test on the
  persisted reason) and the bare literals are gone from both handlers.
- [ ] **AC8 (DA-12)** â€” Given `UploadDisputeEvidence`, When SAS-URI generation throws, Then the empty
  `catch { }` is replaced by a narrow `catch` that **logs** the failure (ILogger) and still returns the
  evidence row with a null `BlobUrl` (behavior preserved: upload succeeds, URL null), pinned by a test
  that forces the SAS path to throw and asserts the log + non-failing result.
- [ ] **AC9** â€” `node agents/tools/check-consistency.mjs --paths=<the touched dirs>` is clean for every
  touched backend/frontend area; `dotnet build Cleansia.Api.sln` succeeds; `npx nx lint` + `npx nx test`
  pass for the `loyalty-promo-codes` lib; every new/changed test is written **test-first** (red before
  green) per `agents/knowledge/testing.md`, visible in the diff/status log.

## Out of scope
- DA-9's `"CZE"` / geo-bounds / dispute-length constants (T-0150); the tier-threshold dead-config and
  grant/revoke `Reason` persistence (T-0148); the admin referral intervention LG-05/06/09 surface
  (T-0176 â€” a *different* LG-05/09 numbering); `CreateMembershipSubscription`'s B5 + Stripe try/catch
  (T-0007/T-0008/T-0147); the membership-subscribe path unification (T-0179).
- **Delete semantics** of `UnregisterDevice` (`Remove`â†’`Deactivate`) â€” that is T-0142 (IA-13
  soft-delete). This ticket touches the *exception/magic-string* smell only.
- Adding the missing `errors.*` dispute/address translations (DA-7) and the disputes-list
  archetype rebuild (DA-6).
- The customer-app flag-bag/`ActionState` migrations (T-0013/T-0014/LG-08 mobile) and any mobile work
  beyond confirming `check-consistency.mjs mobile` stays clean â€” no mobile source is edited here unless
  a cited mobile offender surfaces (none in this bundle; `layers: [..., mobile]` is for the consistency
  gate run, not new edits).
- The transactional-outbox / idempotency reworks the membership handlers also need (Wave-0/1) â€” this
  ticket fixes only the B5/B1 archetype smells on those files.

## Implementation notes
- **Canonical patterns:** B5 â†’ `consistency.md` Â§B5 (`new Error(nameof(command.<Field>), Businessâ€¦)`);
  B1 â†’ Â§B1 (every command returns a `Response` record); LG-09 read â†’ the CQRS split in
  `patterns-backend.md` (Query/QueryHandler, never the commit pipeline) and `consistency.md` Â§A intro;
  LG-14 â†’ Â§C3 (the exact `takeUntil â†’ catchError â†’ finalize` pipe) + Â§C4 (errors via `SnackbarService`);
  IA-13/IA-19/DA-12 â†’ `conventions.md` "no magic strings â€” constants live in a Policy/enum/const" and
  the `BusinessResult.Failure` + `BusinessErrorMessage` error contract (no raw exceptions, no empty
  catches, S6 "no silent swallow â€” log it").
- **TEST-FIRST** per `agents/knowledge/testing.md`: for every refactor item write the
  **characterization test first** (pin current behavior â€” the `ManualGrant` row, the current `Impacts`,
  the current persisted GDPR reason string, the current throw, the current silent no-op), confirm it
  passes/red-for-the-right-reason, then make the change with the test green. The two *[behavior fix]*
  items (AC1, AC5) flip their assertion to the corrected behavior in the same diff. No after-the-fact
  tests for the logic items.
- **Serialization (TICKET-MAP shared-file map):** none of these files is in a TICKET-MAP cluster, so
  the bundle can run concurrently â€” **except**: it edits `RegisterDevice.cs`/`UnregisterDevice.cs`
  (also touched by **T-0142** IA-13 soft-delete) â†’ **serialize against T-0142** (do not run both at
  once); `RevokePointsManually.cs` is referenced by T-0112/T-0148/T-0176 but each touches a *different*
  region (idempotency `requestId`, `Reason` threading, referral wiring) â€” sequence after those land to
  avoid a merge on the same handler. The membership handlers, `SendSitewidePromo.cs`,
  `PreviewTierThresholdImpact.cs`, `UploadDisputeEvidence.cs`, the GDPR handlers, and the promo facade
  are otherwise collision-free.
- **Manual step note (not a frontmatter `manual_steps` â€” flag to owner if it surfaces):** AC4
  (`SendSitewidePromo` gains a `Response`) and AC6/AC3 (device/membership handler error shapes) change
  backend DTOs/contracts that the FE/mobile NSwag clients consume. If the affected endpoints are
  client-consumed, this needs **`nswag-regen`** â€” confirm at review and add `manual_steps:
  [nswag-regen]` if so (kept off the frontmatter until the diff confirms a wire-shape change reaches a
  generated client; `SendSitewidePromo`/devices are admin/mobile actions â€” verify).
- **Enum addition (AC1):** adding `LoyaltyEarnSource.ManualRevoke = 5` is a new int enum value; existing
  rows are unaffected and it is stored as the int â€” **no EF migration**. Confirm at review that the enum
  is `[SwaggerEnumAsInt]` (it is) so the new value flows to clients on the next regen.
- Cited lines verified against live code on 2026-06-01 (see Context).

## Status log
- 2026-06-01 â€” draft (created by pm)

## Review
<!-- reviewer / security / optimizer write verdicts here; PM reconciles before advancing state -->
