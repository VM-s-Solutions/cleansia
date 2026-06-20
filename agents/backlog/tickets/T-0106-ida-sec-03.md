---
id: T-0106
title: Cryptographic email-confirm + password-reset tokens; user-scoped lookup + expiry
status: done
size: M
owner: —
created: 2026-06-01
updated: 2026-06-15
depends_on: []
blocks: []
stories: []
adrs: []
layers: [backend, db]
security_touching: true
manual_steps: [ef-migration]
sprint: 0
source: finding IDA-SEC-03
---

## Context

Audit finding **IDA-SEC-03** (critical, Wave 0 PROD-blocker) —
`agents/backlog/audits/AUDIT-2026-06-01-slice-reports.md:494`, JSON entry
`AUDIT-2026-06-01-findings.json:3543`, plan row `AUDIT-2026-06-01-execution-plan.md:111`.

Both the email-confirmation code and the password-reset code are generated with a non-cryptographic
PRNG and looked up weakly:

- `User.cs:95` (`CreateWithPassword`), `User.cs:114` (`UpdateResetPasswordToken`), `User.cs:175`
  (`UpdateConfirmationCode`) all use `Random.Shared.Next(100000, 999999).ToString()` — a 900k-value,
  predictable, **non-cryptographic** space, valid 15 minutes.
- `UserRepository.GetByConfirmationCodeAsync` (`UserRepository.cs:45-48`) resolves the user by the
  **code alone** — no email/identifier binding — and `ConfirmUserEmail` issues a JWT for whatever
  account holds the matched code (`ConfirmUserEmail.cs:37,56,65-68`). So an attacker need not target a
  specific account: any outstanding 6-digit code confirms and logs in as its owner.
- The reset path (`ChangePassword.cs:56-63`) at least binds to email, but stores and compares the code
  in **plaintext** (`user.ResetPasswordCode == command.Code`) over the same weak 6-digit space, then
  directly resets the password → takeover.

Combined with the broken auth rate limiter (IDA-SEC-02 / BSP-4), brute-forcing 900k guesses inside a
window is feasible. This hardens both flows: CSPRNG-generated, hashed-at-rest, user-scoped (and for
confirm, no longer resolvable by code alone), with enforced expiry. Paired test ticket: **T-0128**
(TC-AUTH-TAKEOVER) — lands in the same merge (TDD).

## Acceptance criteria

- [ ] **AC1 — Tokens are cryptographic.** Given a new registration or a confirmation-code refresh,
      When the code is generated (`User.CreateWithPassword`, `User.UpdateConfirmationCode`,
      `User.UpdateResetPasswordToken`), Then it is produced via a CSPRNG (`RandomNumberGenerator`),
      not `Random.Shared`, with ≥128 bits of entropy; a unit test asserts `Random.Shared` is no longer
      referenced on these paths and that two successive tokens are unpredictable/non-sequential.
- [ ] **AC2 — Stored hashed, never plaintext.** Given a token is issued, When the user row is
      persisted, Then only a hash of the token is stored (the raw token leaves only in the email);
      a test asserts the persisted column value never equals the raw token delivered to the user.
- [ ] **AC3 — Email confirmation is no longer resolvable by code alone.** Given an attacker submits a
      code that is valid for a *different* account, When `ConfirmUserEmail` runs, Then confirmation is
      rejected (no JWT issued) because the lookup is scoped to the account, not the bare code; a test
      proves a code valid for user B does not confirm/log in user A and does not confirm B via the
      old `GetByConfirmationCodeAsync(code)`-only path.
- [ ] **AC4 — Reset lookup is `(email, hashed-token)`.** Given a password-reset attempt, When the
      validator/handler resolves the user (`ChangePassword`), Then it matches on email **and** the
      hash of the supplied code (no plaintext comparison); a test proves a correct code for the wrong
      email and a wrong code for the right email both fail.
- [ ] **AC5 — Expiry strictly enforced and one-shot.** Given a token past
      `ConfirmationCodeExpiresAt` / `ResetPasswordCodeExpiresAt`, When submitted, Then it is rejected;
      and once a code is successfully consumed it is invalidated (cleared) so it cannot be replayed; a
      test covers expired and already-used codes for both flows.
- [ ] **AC6 — Tenant filter respected, not bypassed.** Given the anonymous confirm/reset lookups,
      When implemented, Then they do NOT use `IgnoreQueryFilters()` (which would make a hashed-token
      match cross-tenant — see IDA-SEC-10); a test/assertion confirms the global tenant filter is
      still in force on these repository methods.
- [ ] **AC7 — Migration flagged, not run.** Given the schema changes (token columns become hashes /
      add hash columns), When the work is complete, Then an `ef-migration` MANUAL_STEP is recorded for
      the owner and dependent/frontend-visible work is held until the owner confirms; Claude does not
      run `dotnet ef`.

## Out of scope

- Fixing the auth rate limiter (IDA-SEC-02 / BSP-4) — separate Wave-0 tickets; this ticket assumes the
  limiter is fixed in parallel and does not depend on it.
- Removing code/PII from logs (IDA-SEC-05) — separate ticket; do not regress it, but the log scrub is
  not owned here.
- Multi-tenant tenant resolution on anonymous auth routes (IDA-SEC-10) — out of scope except for the
  AC6 guard that this fix must not introduce an `IgnoreQueryFilters()` cross-tenant leak.
- Changing the user-facing UX (still a code in an email) or moving to a magic-link — keep the existing
  flow shape; only the secret strength, storage, and lookup change.
- Any frontend change — these are server-internal token mechanics; no DTO contract change is expected,
  so no `nswag-regen` (confirm during implementation; flag it if a request/response shape does change).

## Implementation notes

- **TEST-FIRST** per `agents/knowledge/testing.md` (§"TDD — write the test first"). Token generation,
  hashing, and the lookup/expiry branches are pure logic + handler-contract logic → strict
  red→green→refactor; the test (T-0128 / TC-AUTH-TAKEOVER) must predate or land with the
  implementation in the diff, and each AC maps to a case. Gate 6 (Reviewer) enforces this.
- **Governing rules:** no ADR governs token-secret design — this is governed by the S-laws in
  `agents/knowledge/security-rules.md` (S4 DTO/secret-leak: token hashes belong in the never-leak list;
  S5/S6 logging). ADRs 0001/0002/0003 do **not** apply here.
- **Serialization cluster:** none. The touched files — `User.cs`, `UserRepository.cs`,
  `ConfirmUserEmail.cs`, `ChangePassword.cs` (+ EF entity config for the User token columns) — do not
  appear in any cluster in `agents/backlog/TICKET-MAP.md`. Safe to run concurrently with other Wave-0
  tickets, with the standard reviewer-per-developer invariant.
- **Where:**
  - Generation: `src/Cleansia.Core.Domain/Users/User.cs:95` (`CreateWithPassword`),
    `:112-117` (`UpdateResetPasswordToken`), `:173-179` (`UpdateConfirmationCode`); replace
    `Random.Shared.Next(...)` with a CSPRNG helper. Confirm/clear methods: `ConfirmEmail` (`:157-164`),
    `ClearResetPasswordToken` (`:125-130`) — ensure they clear the (now hashed) token on success.
  - Lookup: `src/Cleansia.Infra.Database/Repositories/UserRepository.cs:40-48`
    (`ExistsWithConfirmationCodeAsync`, `GetByConfirmationCodeAsync`) — confirm flow must bind to the
    account, not the bare code; reset stays `GetByEmailAsync` + hashed-code compare.
  - Confirm handler/validator: `src/Cleansia.Core.AppServices/Features/Auth/ConfirmUserEmail.cs:35-68`
    — the `Command(string Code)` currently carries no identifier; if confirm-by-code-alone is
    eliminated by binding the lookup, ensure the handler re-resolves the same user the validator did.
  - Reset validator/handler: `src/Cleansia.Core.AppServices/Features/Users/ChangePassword.cs:56-63,85-92`
    — replace plaintext `user.ResetPasswordCode == command.Code` with a hash comparison; the handler
    already calls `ClearResetPasswordToken()` (keep, applied to the hashed column).
  - EF: the `User` token columns need to hold a hash (length/rename) → **ef-migration MANUAL_STEP**.
- **Sequence:** db (architect/db lock the token-column shape + hashing approach) → backend
  (domain generation + repository lookup + handlers/validators) with reviewer in parallel; security
  gate mandatory (`security_touching: true`); then qa. Hold on owner-confirmed migration before done.
- Do not log the raw or hashed token at any level (preserve S6 / don't regress IDA-SEC-05).

## Owner decision (2026-06-02) — token strength + contract shape (BINDING)
- **Token strength: longer high-entropy token (≥128-bit), NOT a 6-digit code.** Generated via
  `RandomNumberGenerator` as a URL-safe string (≥16 random bytes → base64url/hex). Brute-force-proof on
  its own — does NOT lean on the BSP-4 rate limiter for safety. (Owner picked this over keeping 6 digits.)
- **Stored hashed at rest** (SHA-256 of the raw token; raw never persisted). Columns widen 6 → 64 (hex)
  or appropriate length — drives the ef-migration.
- **Confirm lookup = by HASH of the token alone (no email param).** Because the token is now a 128-bit
  secret, scoping the confirm lookup *by the token's hash* is cryptographically safe and closes AC3 (a
  token valid for B can't confirm A, and a guessed/short code can't confirm anyone). **Keep
  `ConfirmUserEmail.Command(string Code)` — no new field → no contract change → NO nswag.** The validator
  and handler both resolve the same user via the hashed-token lookup; handler re-resolves (or the
  validator stashes) the same user so behavior is consistent.
- **Reset lookup = by (email, HASH of token).** `ChangePassword` keeps `Command(Email, NewPassword, Code)`
  — already binds to email; replace the plaintext `user.ResetPasswordCode == command.Code` with a hash
  compare. No contract change → NO nswag.
- **Email sends the RAW token, DB stores the HASH.** The generator methods (`CreateWithPassword`,
  `UpdateConfirmationCode`, `UpdateResetPasswordToken`) must surface the RAW token to the email-sending
  handler (`Register`, `ResendConfirmationEmail`, `RequestPasswordChange`) while the entity persists only
  the hash. Today those handlers read `userEntity.ConfirmationCode!` / `user.ResetPasswordCode!` AFTER
  generation and pass it to `IEmailService` — if generation hashes in place, the email would send the
  HASH and sign-in would break. So change the domain methods to RETURN the raw token (e.g.
  `string UpdateConfirmationCode()` returns raw, stores hash; same for reset; `CreateWithPassword` exposes
  the raw confirmation token to the caller). Update the 3 email handlers to email the returned RAW value.
- **UX:** still a code in an email, just longer (paste rather than type 6 digits). No magic-link required;
  keeping it as a pasteable code is acceptable. No frontend change needed (the code field already accepts
  a string). Confirm with the reviewer that no frontend input has a 6-char maxlength that would truncate.

## Status log
- 2026-06-01 — draft (created by pm)
- 2026-06-02 — in_progress (backend) — TEST-FIRST per owner-decision (BINDING). Added the canonical
  crypto seam `SecurityTokens` (RandomNumberGenerator 128-bit raw token + SHA-256 hex hash); domain
  generators now return the RAW token and persist only the HASH (`CreateWithPassword` surfaces it via
  the transient `[NotMapped] RawConfirmationToken`, `UpdateConfirmationCode`/`UpdateResetPasswordToken`
  return it); the 3 email handlers email the RAW returned token; confirm lookup is by hash-of-token
  (no email param, contract unchanged); reset compare is `(email, SHA-256(code))`; one-shot clears on
  `ConfirmEmail`/`ClearResetPasswordToken`; AC6 keeps the global tenant filter (no `IgnoreQueryFilters`
  on the token lookups). EF column-length 6→64 + migration are the db agent's MANUAL_STEP (ef-migration).
- 2026-06-02 — RED captured: with a weak/plaintext `SecurityTokens` stub, the contract tests fail for
  the right reasons (7 fail: non-crypto/collide, hash==raw, stored==raw on all 3 generators,
  `RandomNumberGenerator` source-absent, and the AC4 plaintext-compare guard wrongly passing).
- 2026-06-02 — GREEN: `dotnet build Cleansia.Api.sln -c Debug` = 0 errors / 0 new warnings;
  `dotnet test Cleansia.Tests` = 170 passed / 0 failed (22 of them T-0106). Not committed; no nswag,
  no `dotnet ef` run. Held on owner-confirmed migration (AC7) before done.
- 2026-06-02 — db agent — EF column shape locked. `UserEntityConfiguration` sets
  `builder.Property(u => u.ResetPasswordCode).HasMaxLength(64)` and
  `builder.Property(u => u.ConfirmationCode).HasMaxLength(64)` (was `HasMaxLength(6)` on both); both stay
  nullable; no value converter (a stored SHA-256 hex hash is a plain `string`, unlike `Password` which
  keeps its `PasswordConverter`). 64 = length of `SecurityTokens.Hash` output (`Convert.ToHexString(SHA256)
  .ToLowerInvariant()` = 64 lowercase hex chars). AC6 verified: `User` is `ITenantEntity`, the global
  tenant filter in `CleansiaDbContext.ApplyTenantQueryFilters` still applies; no `IgnoreQueryFilters()`
  added — the confirm/reset lookups (`UserRepository.GetByConfirmationCodeAsync` /
  `ExistsWithConfirmationCodeAsync`) hash-and-match inside the filter; the only `IgnoreQueryFilters` in
  `UserRepository` is the unrelated `GetByIdIgnoringTenantAsync`, which these flows do NOT use. Re-ran
  `dotnet build src/Cleansia.Api.sln -c Debug` = **0 warnings / 0 errors**. `dotnet ef` NOT run (AC7).

## MANUAL_STEP (owner runs) — ef-migration

**Why:** the `User.ConfirmationCode` and `User.ResetPasswordCode` columns change from a 6-char plaintext
code to a 64-char SHA-256 hex hash. The EF model is already widened (`HasMaxLength(6)` → `HasMaxLength(64)`
in `UserEntityConfiguration`); a migration must be generated + applied to widen the two DB columns.

**Command (run from the repo root; the `DesignTimeCleansiaDbContextFactory` in `Cleansia.Infra.Database`
makes the project self-sufficient — no separate web startup project is required, so `--project` and
`--startup-project` are the same):**

```bash
dotnet ef migrations add HashAuthTokens \
  --project src/Cleansia.Infra.Database/Cleansia.Infra.Database.csproj \
  --startup-project src/Cleansia.Infra.Database/Cleansia.Infra.Database.csproj \
  --context CleansiaDbContext \
  --output-dir Migrations
```

(PowerShell one-liner equivalent — backslash line-continuation is bash; on PowerShell use a single line:)

```powershell
dotnet ef migrations add HashAuthTokens --project src/Cleansia.Infra.Database/Cleansia.Infra.Database.csproj --startup-project src/Cleansia.Infra.Database/Cleansia.Infra.Database.csproj --context CleansiaDbContext --output-dir Migrations
```

Then apply it (Aspire normally runs `Database.Migrate()` on startup; to apply manually):

```bash
dotnet ef database update --project src/Cleansia.Infra.Database/Cleansia.Infra.Database.csproj --startup-project src/Cleansia.Infra.Database/Cleansia.Infra.Database.csproj --context CleansiaDbContext
```

**Suggested migration name:** `HashAuthTokens`.

**ℹ️ MIGRATION-FOLDER STATE — resolved with owner (2026-06-02).** The `Migrations/` folder is empty on
disk (the 11 committed files show as unstaged deletions vs HEAD). This is **intentional — the OWNER is
regenerating the initial migration**, not a stray agent artifact (orchestrator's earlier "stray deletion"
flag was a false positive; confirmed benign by the owner). Two valid paths, owner's choice:

- **(A) Subsume into the regenerated Initial (simplest).** Because the T-0106 entity-config change is
  already in `UserEntityConfiguration` (`HasMaxLength(64)` on both token columns), a freshly-regenerated
  Initial migration is built from the current model and **already contains the 64-char columns** — so
  **no separate `HashAuthTokens` migration is needed**; T-0106's schema change is baked into the new
  Initial. Nothing further to run for this ticket.
- **(B) Incremental on a restored chain.** If the owner instead restores the old chain
  (`git checkout -- src/Cleansia.Infra.Database/Migrations/`), then `dotnet ef migrations add HashAuthTokens`
  emits a slim two-column `AlterColumn` (6 → 64) appended to the chain.

Either way the **code is correct and unchanged**; only the schema-delivery mechanism differs.

If path (B): `dotnet ef migrations add HashAuthTokens` produces a **slim two-column `AlterColumn`** (6→64).

**What the generated migration will contain:** two `AlterColumn<string>` operations on table `Users` —
`ConfirmationCode` and `ResetPasswordCode` — changing `character varying(6)` → `character varying(64)`,
both still `nullable: true`, with the inverse `Down` reverting `64 → 6`. No new columns, no index changes,
no data backfill, no value converter. (Hand-SQL equivalent, only if you choose to skip EF entirely:
`ALTER TABLE "Users" ALTER COLUMN "ConfirmationCode" TYPE varchar(64), ALTER COLUMN "ResetPasswordCode" TYPE varchar(64);`)

**DATA NOTE (acceptable, pre-PROD):** any confirmation/reset codes outstanding at deploy time were
written under the old 6-digit plaintext scheme and will NOT match the new hashed lookup, so they become
invalid the moment the new code ships. This is safe to ignore: the tokens have a 15-minute TTL, and this
is pre-PROD — affected users simply re-request (resend confirmation / re-initiate password reset). No
backfill or migration of existing code values is needed or possible (we can't reverse a plaintext code
into the new hashed form, nor would we want to).

## Review
<!-- reviewer / security / optimizer write verdicts here; PM reconciles before advancing state -->

### REVIEW — reviewer (2026-06-02) — T-0106 (IDA-SEC-03)

Verified every claim against the real code on disk; did not trust the dev/db reports.

- **Gate 1 Conventions:** PASS. No `dynamic`; reuses existing `BusinessErrorMessage.InvalidConfirmationCode`
  (`auth.invalid_confirmation_code`) + `NotValidResetPasswordToken` (`auth.invalid_reset_token`) — no new
  keys. `SecurityTokens` placed in `Core.Domain/Common` (Domain may reference `System.Security.Cryptography`
  — build confirms). `check-consistency` = 55 violations, all pre-existing baseline; the only ones in
  T-0106-adjacent files (`Register.cs:17` B3, `Register.cs:62` / `ResendConfirmationEmail.cs:47` B1) are on
  the validator base + the `Command:ICommand<bool>` return — both UNCHANGED by this ticket (owner forbade
  contract changes). No NEW violation introduced.
- **Gate 2 AC (1–6):** PASS, each mapped to a passing test (verified file:line below).
  - AC1 crypto/non-seq, no Random.Shared: `SecurityTokens.Generate()` uses
    `RandomNumberGenerator.GetBytes(16)` (128-bit) (`SecurityTokens.cs:31-38`); `Random.Shared.Next` grep =
    0 hits in `src` outside test-assert/doc strings. Tests `SecurityTokensTests.cs:22,59`.
  - AC2 hash-at-rest != raw: generators store `SecurityTokens.Hash(raw)` and surface raw
    (`User.cs:107-117,141-144,207-214`). Tests `SecurityTokensTests.cs:43,72,86,99`; integration
    `RegisterTests.cs:57-59` asserts persisted col is 64-hex, not the raw.
  - AC3 confirm by hash, no bare-code path: `UserRepository.GetByConfirmationCodeAsync/ExistsWith…` hash
    the incoming token and match `ConfirmationCode == hash` (`UserRepository.cs:44-54`); `Command(string Code)`
    unchanged (no nswag). Test `ConfirmUserEmailSecurityTests.cs:73`.
  - AC4 reset by (email, hash), no plaintext compare: `ChangePassword.cs:61-67` loads by email and compares
    `user.ResetPasswordCode == SecurityTokens.Hash(command.Code)`; the old `== command.Code` plaintext is
    gone. Tests `ChangePasswordSecurityTests.cs:50,63,85,99`.
  - AC5 expiry + one-shot both flows: confirm expiry `ConfirmUserEmail.cs:48`, clear on `ConfirmEmail()`
    (`User.cs:185-194`); reset expiry `ChangePassword.cs:66-67`, clear on `ClearResetPasswordToken()`
    (`User.cs:153-158`). Tests `ConfirmUserEmailSecurityTests.cs:95,115`, `ChangePasswordSecurityTests.cs:116,131`.
  - AC6 tenant filter intact: only `IgnoreQueryFilters()` in `UserRepository.cs` is line 78
    (`GetByIdIgnoringTenantAsync`); the token lookups don't use it. Test `UserRepositoryTokenLookupTenantTests.cs:36,50`.
- **Gate 3 Security (mandatory):** PASS. S4 never-leak: raw token never persisted (`[NotMapped] RawConfirmationToken`
  + returned-string generators); only the hash is stored. S6: no log call references any token value —
  `ConfirmUserEmail.cs:44` logs a static string, `:50` logs only `user.Id`; grep of the touched feature dirs
  shows no token in any Log* call. S8 tenant filter preserved (AC6). IDA-SEC-05 not regressed.
- **Gate 6 Tests, test-first:** PASS. Status log records red→green (7 fail under a weak/plaintext stub),
  4 new test files cover AC1-AC6, all assert on `BusinessErrorMessage` constants. Existing 6-digit-assuming
  tests reconciled: `ConfirmUserEmailValidatorTests` + `UserMockFactory` mock at the repo seam (no plaintext-6
  assumption survives — correct, needed no edit); integration `ConfirmUserEmailTests` submits `RawConfirmationToken`;
  `RegisterTests` asserts the persisted hash shape. The three confirm-validator unit tests
  (`ConfirmUserEmailValidatorTests.cs:70-107`) set raw==stored in the mock — acceptable for a validator unit
  (the mock returns by arg); production hashing is covered by `ConfirmUserEmailSecurityTests`.
- **Gate 7 Contract & docs parity:** PASS for contracts — `ConfirmUserEmail.Command(string Code)` and
  `ChangePassword.Command(Email,NewPassword,Code)` unchanged → no nswag (correct). EF-migration MANUAL_STEP
  recorded (AC7); `dotnet ef` NOT run.
- **Gate 8 Mechanical:** PASS. `dotnet build src/Cleansia.Api.sln -c Debug` = 0 errors / 107 pre-existing
  warnings (none from T-0106). `dotnet test src/Cleansia.Tests` = 170 passed / 0 failed / 0 skipped. Consistency:
  no new violation.

**Two items flagged for the PM (NOT T-0106 code defects — they do not block this ticket's correctness):**
  1. **The db agent's MANUAL_STEP rests on a FALSE premise.** It states the project has "no checked-in EF
     migrations / the `Migrations/` folder is empty." Reality: HEAD (`1d154849`) contains 5 migrations + a
     model snapshot (`git ls-tree HEAD src/Cleansia.Infra.Database/Migrations/`), but the working tree has
     them as **unstaged deletions** (folder empty on disk) — a parallel-agent / checkout artifact, not
     T-0106's doing. So the correct ef-migration is a slim `AlterColumn` 6→64 appended to the existing chain
     (NOT a baseline, NOT the hand-SQL fallback the db note recommends). The owner must first restore the
     deleted migrations (`git checkout -- src/Cleansia.Infra.Database/Migrations/`) before generating
     `HashAuthTokens`. The db note's "owner picks based on DB state" hedge is wrong here; the answer is
     determinable: append a two-column alter.
  2. The dev/db reports both characterized the index as "clean except T-0100's 3 staged Policy files."
     Actual working tree has extensive parallel-agent changes (GoogleAuth, Dashboard, Disputes, Auth policies,
     the migration deletions). Not T-0106's concern, but the report's index description is inaccurate.

Verdict: **APPROVED.** The T-0106 implementation exactly matches the BINDING owner-decision and all 7 ACs are
green with test evidence; build + unit tests pass; no token is logged; tenant filter intact; no contract
change. The migration-folder deletion (item 1) is an environment/parallel-work hazard the PM must resolve
before the owner runs the ef-migration — it does not change the verdict on the code under review.

### SECURITY — security (2026-06-02) — T-0106 (IDA-SEC-03): VERDICT: PASS
The weak-token account-takeover is closed: tokens are CSPRNG ≥128-bit (`SecurityTokens.Generate` via
`RandomNumberGenerator.GetBytes(16)`), no longer brute-forceable in the old 900k space; only the SHA-256
**hash** is persisted (raw exists transiently to be emailed — S4 never-leak); confirm is resolved by the
token's hash (the bare-code JWT-issue path is gone — a token for B can't confirm A, a short guess can't
match a 128-bit hash); reset binds `(email, hash)` with no plaintext compare; expiry strict + one-shot
clear on success (no replay); no `IgnoreQueryFilters` on the token lookups (tenant filter intact, S8); no
token value in any log (S6 / IDA-SEC-05 not regressed). Tests prove (3)(4)(5), not prose.

### Verification (orchestrator, independent) — 2026-06-02
Read the security-critical paths myself rather than trusting the reports:
- `Random.Shared` grep across `src` = 0 production hits (only test-assert strings + doc comments).
- `SecurityTokens` is the single canonical seam (CSPRNG 128-bit + SHA-256 hex 64); generators store the
  hash, surface/return the raw (`User.cs:103-145`).
- Both email handlers send the **raw** token (`Register.cs:75-90` `rawConfirmationToken`,
  `RequestPasswordChange.cs:40-45` `rawResetToken`) — never the persisted hash.
- Repo hashes the incoming token and matches inside the tenant filter, no `IgnoreQueryFilters`
  (`UserRepository.cs:41-54`).
- `dotnet build Cleansia.Api.sln` = 0 errors; `dotnet test Cleansia.Tests` = **170 passed / 0 failed**.

**Reviewer APPROVED + Security PASS. Code half DONE.** ⚠️ Two owner actions remain before the schema is
live (both flagged, neither blocks the code being correct):
1. **Restore the deleted migrations** — `git checkout -- src/Cleansia.Infra.Database/Migrations/` (all 11
   files are deleted from the working tree but intact in HEAD; a stray multi-agent-session artifact, NOT
   T-0106's change). Orchestrator did not touch git/migrations (owner-only).
2. **Run the ef-migration** `HashAuthTokens` (the corrected MANUAL_STEP above) once the chain is restored.

Not committed. No nswag. `dotnet ef` not run.
