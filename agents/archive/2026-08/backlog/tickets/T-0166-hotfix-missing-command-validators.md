---
id: T-0166
title: Hotfix — commands missing FluentValidation validators throw at runtime (GDPR delete + 2 notification commands)
status: done
size: S
owner: backend
created: 2026-06-06
depends_on: []
blocks: []
stories: []
adrs: []
layers: [backend]
security_touching: true
manual_steps: []
sprint: 1
source: found during Batch-1B wave-1 verification (pre-existing master bug, surfaced by the new guard test)
---

## Context
`ValidationPipelineBehavior` requires a registered `IValidator` for any request whose type is named
`Command` (or whose declaring type ends in `Command`); without one it throws
`InvalidOperationException` at runtime on the first call. Three commands shipped in master (#72) with
NO validator, so their endpoints were broken in production:

- **`DeleteUserAccount.Command`** (customer self-delete) — reachable via `GdprController` on Web.Customer
  and Web.Mobile.Customer. This is the **GDPR Article-17 "delete my account"** path — a live compliance
  defect.
- **`GetMyNotificationPreferences.Command`** — the notification-prefs GET (lazy-creates the row, hence
  named Command).
- **`UpdateNotificationPreferences.Command`** — the notification-prefs PUT.

Not a Batch-1B regression — all three predate Wave 1 (the Admin GDPR delete and most commands DO have
validators; these were missed). Surfaced while verifying Batch-1B wave-1.

## Fix
- Added a minimal `AbstractValidator<Command>` to each of the three (the commands are parameterless /
  all-bool, so there is nothing to constrain — the empty validator satisfies the pipeline, exactly as
  its own error message suggests).
- **Added a guard test** `EveryCommandHasValidatorTests.Every_Command_Type_Has_A_Concrete_Validator`
  that enumerates every command type matching the pipeline's exact name-based rule and asserts each has
  a concrete validator — so this whole bug class can never silently ship again. (The test correctly
  excludes reads named `Query` even when they implement `ICommand<T>`, matching the pipeline.)

## Acceptance criteria
- [x] All three commands have a validator; their endpoints no longer throw at the pipeline.
- [x] The 3 previously-red GDPR integration tests (`DeleteUserAccountTests`) pass red→green.
- [x] The guard test passes and fails if any future command omits a validator.

## Status log
- 2026-06-06 — found + fixed during Batch-1B wave-1 verification; guard test added; done. Committed with
  the Batch-1B batch (owner: commit all at end).
