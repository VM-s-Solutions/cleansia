---
id: T-0234
title: Bound ChangeOwnPassword current-password guessing (authenticated surface)
status: done
size: S
owner: pm
created: 2026-06-12
updated: 2026-06-15
depends_on: [T-0193]
blocks: []
stories: []
adrs: [0003]
layers: [backend]
security_touching: true
manual_steps: []
sprint: 6
source: T-0193 security-gate note N5 + dev handoff residual (Wave-3 close, 2026-06-12)
---

## Context
T-0193's lockout covers the **login** surfaces only. `ChangeOwnPassword`'s current-password check is
an **authenticated guessing surface** a session-holder (stolen token, unattended browser) can use to
brute the account password — today bounded only by the per-`sub` 30/min rate-limit window
(T-0115 "auth" policy). The T-0193 developer flagged it on the record as an out-of-scope residual
("file separately if wanted") and the security gate filed it as note **N5**. Close the gap by giving
the current-password check the same per-account failure budget the login surfaces got.

No-decision note candidate is WRONG here — this charges a counter on an authenticated path; small
but security_touching, so the security gate runs. The shape, however, is already canonical: the
T-0193 validator-invoked atomic `ExecuteUpdateAsync` counter pattern (see
`agents/knowledge/patterns-backend.md`, "Failure-path counters bypass the UoW deliberately").

## Acceptance criteria
- [ ] **AC1** — Given N consecutive wrong current-password attempts on `ChangeOwnPassword`
  (threshold aligned with `User.MaxFailedLoginAttempts` unless security argues otherwise), When the
  budget is exhausted, Then further attempts are refused with a distinct error key for the window
  duration — the current password is no longer evaluated (no oracle).
- [ ] **AC2** — A successful change (or window expiry) restores a fresh budget.
- [ ] **AC3** — The charge is atomic/race-proof under parallel requests (conditional
  `ExecuteUpdateAsync`, mirroring T-0193) — failing commands never reach the UoW commit.
- [ ] **AC4** — Red-first tests cover budget charge, refusal, expiry, and reset-on-success; i18n key
  for the new error in all 5 locales × affected apps.

## Out of scope
- The login/confirm/reset surfaces (T-0193, shipped). Re-tuning T-0115 windows.
- Admin change-password-for-other-user flows (no current-password check there).

## Implementation notes
Decide whether to reuse `FailedLoginAttempts`/`LockoutEndsAt` (couples the two surfaces — a
change-password sprayer would lock login too; arguably correct) or a dedicated counter pair (needs
an owner **ef-migration** — if chosen, add the manual_step). Security gate settles this at
contract-lock; default recommendation: reuse the existing lockout pair, no new columns.

## Status log
- 2026-06-12 — draft (created by pm at Wave-3 close; from T-0193 security note N5)
- 2026-06-14 — **ready** (PM, Wave-6 intake / Batch **6B**). Dep T-0193✓. The shape is canonical (the
  T-0193 validator-invoked atomic `ExecuteUpdateAsync` counter pattern) → no panel; **security gate** runs
  (charges a counter on an authenticated path) and settles the counter choice at contract-lock. **Default =
  reuse the existing lockout pair → no migration**; ef-migration ONLY if a dedicated counter is chosen
  (flag then). **Lane BusinessErrorMessage + locale-JSONs — runs AFTER T-0262** (6A) which removes a key in
  the same files; this ticket adds a new `auth.*` key ×5 locales. **Lane Auth-surface — before T-0233**
  (6E), same lockout/login authn surface. Red-first tests. Plan: `status/sprint-8.md` §3 Batch 6B.
- 2026-06-14 — **review** (backend). Implemented the **default: reuse the existing lockout pair**
  `FailedLoginAttempts`/`LockoutEndsAt` → **no migration, no new EF config, no schema change** (ticket
  confirms this is allowed and is the security-gate default). **No new `BusinessErrorMessage` constant
  and no new i18n key** — the distinct exhausted-window key is the pre-existing `AccountLocked`
  (`auth.account_locked`), and the wrong-attempt key stays `CurrentPasswordInvalid`; both already exist
  in all 5 locales of the admin app (the only host exposing `ChangeOwnPassword`). So the AC4 i18n
  requirement is satisfied with zero new strings, and the T-0262/BusinessErrorMessage-lane ordering is
  moot (nothing added there).
  - **Mechanism** (mirrors T-0193 exactly): new atomic by-id charge `IUserRepository
    .RecordFailedCurrentPasswordAttemptAsync(userId, now)` — a conditional `ExecuteUpdateAsync`
    (`WHERE Id = @id AND (LockoutEndsAt IS NULL OR LockoutEndsAt <= now)`) charging the SAME lockout
    pair, byte-for-byte the same threshold/window logic as `RecordFailedLoginAsync` (only the key
    changes email→id). Sharing the budget is the deliberate "a change-password sprayer also bounds
    login" coupling (S7a, race-proof; runs outside the UoW so the **failing command never commits** but
    the counter still lands).
  - **Handler** (`ChangeOwnPassword`): lockout gate **before** the password compare → `AccountLocked`
    (current password no longer evaluated = no oracle, AC1); wrong password → atomic charge then
    `CurrentPasswordInvalid` (AC1/AC3); success → `user.ResetLoginThrottle()` (AC2 fresh budget); window
    expiry restore is inherent in `IsLockedOut(now)`.
  - **TDD red→green**: added 3 unit tests to `ChangeOwnPasswordTests` (charge / lockout-refusal-no-oracle
    / reset-on-success). Captured RED: `Failed: 3, Passed: 8`. After the repo+handler change: GREEN
    `Passed: 11`. Broader Auth + lockout + ChangePassword suites unchanged green (`212/212`),
    FrozenPermissionMap `3/3`. Added integration test `ChangeOwnPasswordLockoutTests` (3 cases: (N+1)-th
    refusal end-to-end against Postgres, sub-cap charge, reset-on-success) — compiles clean; runtime
    verification deferred to the orchestrator's clean run (this concurrent tree's `Infra.Database` is
    blocked by an UNRELATED in-flight lane editing `ServiceRepository.cs`/`CatalogReferenceJson`, not by
    this change — reported, not touched).
  - **No MANUAL_STEPs**: no schema change (no ef-migration), no Response/DTO/endpoint shape change (no
    nswag-regen), no new locale strings.
- 2026-06-14 — **review-fix** (backend). Addressed two review findings; no production code touched.
  - **Finding 1 (broken integration assertions)**: `ChangeOwnPasswordLockoutTests` asserted
    `Assert.IsAssignableFrom<IValidationResult>(result)` (copied from `AccountLockoutTests`). Verified
    against source: in `ChangeOwnPassword` the `AccountLocked`/`CurrentPasswordInvalid` errors are
    returned by the **handler** as plain `BusinessResult.Failure<Response>(...)`
    (`ChangeOwnPassword.cs:56,63`); `BusinessResult<T>` → `BusinessResult` does **not** implement
    `IValidationResult` (only `ValidationResult<T>` does, and only the validator path is wrapped per
    `ValidationPipelineBehavior.cs:58-73`). The cast therefore throws at runtime — the dev never ran the
    integration suite. Replaced both with `Assert.Equal(BusinessErrorMessage.X, result.Error!.Message)`
    (the same form the unit tests already use correctly) and dropped the now-unused
    `Cleansia.Infra.Common.Validations` import. The third case (`A_Successful_Change…`) never used the
    cast — untouched.
  - **Finding 2 (comment discipline)**: removed the forbidden `// AC1`/`// AC1`/`// AC2` prefixes at
    `ChangeOwnPasswordTests.cs` (kept the plain behavior descriptions). The pre-existing `// AC4`
    (out of scope) was left untouched.
  - **Verification**: `Cleansia.Tests` builds clean (0 warn); `ChangeOwnPasswordTests` 11/11 green, full
    `Features.Auth` unit suite 106/106 green. `Cleansia.IntegrationTests` **compiles clean** with the
    fix. Integration **runtime** could NOT be verified in this shared tree: ALL integration tests
    (including the unmodified `AccountLockoutTests`) fail at fixture setup
    (`BaseIntegrationTest.ApplyMigrationsAsync` → `PendingModelChangesWarning`) because a concurrent lane
    edited an EF entity config without its migration — an UNRELATED tree-wide condition, reported not
    touched. The cast bug fixed here was a guaranteed assertion-phase throw even against a healthy DB;
    with the fix the cases will pass once the orchestrator's isolated clean run can initialize the DB.

## Review
<!-- reviewer / security / optimizer write verdicts here; PM reconciles before advancing state -->
