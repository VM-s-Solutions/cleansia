---
id: T-0107
title: Admin GDPR delete/export + deactivate — self + last-admin protection
status: done
size: S
owner: —
created: 2026-06-01
updated: 2026-06-15
depends_on: [T-0100]
blocks: []
stories: []
adrs: [0001]
layers: [backend]
security_touching: true
manual_steps: []
sprint: 0
source: finding IDA-SEC-08
pairs_with: T-0126
---

## Context
Source: finding **IDA-SEC-08** (`agents/backlog/audits/AUDIT-2026-06-01-slice-reports.md:539-545`,
`agents/backlog/audits/AUDIT-2026-06-01-findings.json:3614-3627`; execution plan row
`audits/AUDIT-2026-06-01-execution-plan.md:127`). Severity: major · type: authorization / availability.

The admin GDPR data-subject tools and the admin-user deactivate flow have no guard against an admin
destroying admin accounts:

- `AdminDeleteUserAccount.Validator` (`src/Cleansia.Core.AppServices/Features/Gdpr/AdminDeleteUserAccount.cs:15-24`)
  only checks the target `UserId` exists, then the handler hands it to `gdprDeletionService.DeleteUserAccountAsync`
  which irreversibly anonymizes the user (`User.Anonymize()`, `User.cs:198-215`) and deactivates them.
  Nothing stops an admin from GDPR-deleting **another administrator**, **their own** account, or the
  **last remaining** administrator — locking the tenant out of its own admin console with no recovery.
- `AdminExportUserData.Validator` (`src/Cleansia.Core.AppServices/Features/Gdpr/AdminExportUserData.cs:15-24`)
  has the same existence-only check — the GDPR customer-data tool should not target administrators.
- `DeactivateAdminUser.Validator` (`src/Cleansia.Core.AppServices/Features/AdminUsers/DeactivateAdminUser.cs:17-36`)
  already blocks **self**-deactivation (`CannotDeactivateSelf`, line 32-34) and is already admin-scoped,
  but has **no last-admin guard** — the final active admin can be deactivated.

The GDPR delete/export tools are meant for customer/employee data-subject requests; admins are managed
only through the AdminUsers feature. The authorization model in force is **ADR-0001** (`adr/0001-authorization-model.md`).

## Acceptance criteria
- [ ] **AC1** — Given an admin caller and a target whose `Profile == UserProfile.Administrator`, When
  `AdminDeleteUserAccount.Command` is validated, Then validation fails with a stable error code (the GDPR
  delete tool refuses to target administrators) and `IGdprDeletionService.DeleteUserAccountAsync` is
  never invoked.
- [ ] **AC2** — Given an admin caller whose `UserId` equals the target `UserId`, When
  `AdminDeleteUserAccount.Command` is validated, Then validation fails with `CannotDeleteSelf`
  (`admin_user.cannot_delete_self`) and no anonymization occurs.
- [ ] **AC3** — Given an admin caller and a target whose `Profile == UserProfile.Administrator`, When
  `AdminExportUserData.Query` is validated, Then validation fails with the same "cannot target admin"
  code and `IGdprExportService.BuildAsync` is never invoked (no GDPR export row is marked completed).
- [ ] **AC4** — Given the tenant has exactly one **active** administrator, When `DeactivateAdminUser.Command`
  targets that admin, Then validation fails with a new last-admin error code and the user remains active
  (`IsActive == true`, `Auditable.Deactivated(...)` is not called).
- [ ] **AC5** — Given the tenant has two or more active administrators and the target is neither the
  caller nor the last admin, When `DeactivateAdminUser.Command` is validated, Then validation passes and
  the existing happy path is unchanged (regression: `CannotDeactivateSelf` self-guard still fires).
- [ ] **AC6** — Each new error code added to `BusinessErrorMessage` (last-admin guard; "cannot target
  administrator via GDPR tool") has a corresponding `errors.*` translation key in all 5 locales
  (en, cs, sk, uk, ru) per the i18n rule in `CLAUDE.md`.
- [ ] **AC7** — TEST-FIRST: xUnit validator tests in `Cleansia.Tests` cover AC1–AC5 (one case per
  failure branch + the happy path), authored before the validator changes (commit order / status log
  shows red → green per `agents/knowledge/testing.md`). The hole is closed and the tests prove it.

## Out of scope
- Any admin UI for GDPR / admin-user management — that is the Wave 2 ticket **IA-01/03** (admin GDPR UI),
  not this guardrail fix.
- Changing `IGdprDeletionService` / `IGdprExportService` behaviour or the anonymization fields themselves.
- The partner GDPR self-service flow (`DeleteUserAccount.cs` / `ExportUserData.cs`) and consent endpoints.
- Activate flow (`ActivateAdminUser`) — only deactivate has the availability risk.
- No EF migration and no NSwag regen: command/query shapes are unchanged (validation-only fix).

## Implementation notes
- **Test-first** per `agents/knowledge/testing.md` (validators are pure logic → strict red-green-refactor):
  write the failing `Cleansia.Tests` validator unit tests for each branch first, then add the rules.
- Add guards to **both GDPR validators** (`AdminDeleteUserAccount.Validator`, `AdminExportUserData.Validator`):
  reject when `target.Profile == UserProfile.Administrator`, and reject self-target (caller from
  `IUserSessionProvider.GetUserId()`). These validators currently only call `userRepository.ExistsAsync`
  — they will need to read the target `User` (Profile) to apply the guard; follow the `DeactivateAdminUser.Validator`
  pattern (`userRepository.GetAll().AnyAsync(...)`).
- Add the **last-admin guard** to `DeactivateAdminUser.Validator`: reject when the target is the only
  active administrator in the tenant (count `Profile == UserProfile.Administrator && IsActive` > 1).
  Keep the existing `CannotDeactivateSelf` rule and `Cascade.Stop`.
- Reuse existing constants where they fit (`CannotDeleteSelf` = `admin_user.cannot_delete_self`,
  `CannotDeactivateSelf`, `AdminUserNotFound`); add new `BusinessErrorMessage` constants (dot notation,
  `admin_user.*`) for the last-admin guard and the "cannot target administrator via GDPR tool" case, with
  matching `errors.*` keys in all 5 locale files.
- Entity API: `User.Profile` (`UserProfile.Administrator`), `User.Anonymize()` (`User.cs:198-215`),
  `Auditable.Deactivated(by, on)` sets `IsActive = false` (`Auditable.cs:35-39`).
- **ADR in force:** ADR-0001 (authorization model) — this is a write-path authorization/availability guard.
- **Depends on T-0100** (BSP-1 PolicyBuilder fail-closed + complete map + shared host registration): the
  admin endpoints' `[Permission(...)]` policies resolve correctly only after BSP-1 lands.
- **Pairs with T-0126** (TC-AUTHZ-0 family) — TDD, same merge.
- **Serialization cluster:** none. The GDPR validators (`Features/Gdpr/*`) and the AdminUsers validator
  (`Features/AdminUsers/DeactivateAdminUser.cs`) are not part of any shared-file cluster in
  `agents/backlog/TICKET-MAP.md`; this ticket may run concurrently with other Wave-0 backend tickets that
  do not touch these files (do NOT co-schedule with another ticket editing `BusinessErrorMessage.cs`).

## Status log
- 2026-06-01 00:00 — draft (created by pm)
- 2026-06-02 — in_progress (backend). Test-first: authored 9 xUnit validator tests across
  `Cleansia.Tests/Features/Gdpr/AdminDeleteUserAccountValidatorTests.cs`,
  `.../AdminExportUserDataValidatorTests.cs`, and
  `Cleansia.Tests/Features/AdminUsers/DeactivateAdminUserValidatorTests.cs` BEFORE the guards.
  **RED** confirmed: test build failed for the right reasons — the two GDPR validators were
  `internal` / had no self-target+admin guards, and the new `BusinessErrorMessage` constants
  (`CannotDeactivateLastAdmin`, `CannotTargetAdminViaGdprTool`) did not exist.
- 2026-06-02 — done (backend → ready-for-review). **GREEN**: implemented the 3 validator guards
  (GDPR delete: admin-target + self-target via `IUserSessionProvider`; GDPR export: admin-target;
  DeactivateAdminUser: last-active-admin guard, keeping the existing self-guard + `Cascade.Stop`),
  added the 2 new `admin_user.*` constants, and added matching `errors.admin_user.*` keys (plus the
  mirrored `api.admin_user.*` block) in all 5 admin-app locales (en/cs/sk/uk/ru) with real
  translations. `dotnet build Cleansia.Api.sln -c Debug` → 0 errors; `dotnet test Cleansia.Tests`
  → 179 passed / 0 failed (9 new). Validation-only: no NSwag, no EF migration. Not committed.

- 2026-06-02 — done (reviewer APPROVED + security PASS, both verified against the real code; build 0
  errors, Cleansia.Tests 179 passed/0 failed; all 5 locales verified by orchestrator). NOT committed.

## Review
**Reviewer — APPROVED (2026-06-02).** Verified the hole + fix against the real code (existence-only checks,
`internal` validators confirmed as the baseline; guards added with `Cascade.Stop`). Both GDPR validators
reject admin-target + self-target before any `IGdprDeletionService.DeleteUserAccountAsync` /
`IGdprExportService.BuildAsync` call (single `Cascade.Stop` chain — a false from the guard gates the whole
rule). `DeactivateAdminUser` last-admin guard counts active admins excluding the target and keeps the
self-guard. New constants use `admin_user.*` dot notation; existing `CannotDeleteSelf` reused. Tests
test-first, one per branch + happy path, assert on `BusinessErrorMessage` constants. Validation-only — no
nswag, no ef.

**Security — PASS (2026-06-02).** An admin can no longer irreversibly GDPR-delete/anonymize another admin
or themselves (guard in the validator, destructive service unreachable on reject), nor export admin data;
the tenant can no longer be locked out of its console (last active admin can't be deactivated; multi-admin
path + self-guard intact). Active-admin count is correct (inactive sibling doesn't count). Reject paths
return stable business errors; Anonymize/Deactivated provably not reached (service mock Times.Never /
IsActive unchanged in tests).

**Verification (orchestrator, independent):** `dotnet build Cleansia.Api.sln` = 0 errors; `dotnet test
Cleansia.Tests` = 179 passed / 0 failed. **i18n completeness checked myself** — both new keys
(`cannot_deactivate_last_admin`, `cannot_target_admin_via_gdpr_tool`) present in ALL 5 admin locales
(en/cs/sk/uk/ru), in both the `errors.admin_user.*` and mirrored `api.admin_user.*` blocks; spot-checked
real translations (uk = proper Cyrillic, cs = proper diacritics — not English placeholders). No EF, no
nswag. Not committed.
