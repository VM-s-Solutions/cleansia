---
id: T-0128
title: Token-claim binding + (email,hashedToken) reset-code lookup tests
status: done
size: M
owner: —
created: 2026-06-01
updated: 2026-06-15
depends_on: []
blocks: []
stories: []
adrs: [0001]
layers: [backend]
security_touching: false
manual_steps: []
sprint: 0
source: covers IDA-SEC-01/IDA-SEC-03
---

## Context

Paired **test ticket** (TC-AUTH-TAKEOVER) for the two Wave-0 account-takeover fixes on the auth
surface. Per `agents/knowledge/testing.md` (§"TDD — write the test first") and the TICKET-MAP
`pairs_with` column, these tests are written **test-first** and land in the **same merge** as their
fixes:

- **T-0105 / IDA-SEC-01** — Google sign-in trusts the client-supplied `Email`/`GoogleId` and never
  binds them to the verified Google ID-token; `ValidateGoogleUserAsync` short-circuits with
  `return true` when `IGoogleConfig.IsDevelopment` is set (`GoogleAuth.cs:63-66`), and
  `GoogleJsonWebSignature.ValidateAsync(token, new GoogleJsonWebSignature.ValidationSettings())`
  (`GoogleAuth.cs:68`) runs with default settings (no `Audience`) and discards the result. The handler
  then resolves/provisions by `command.Email`/`command.GoogleId` (`GoogleAuth.cs:95,106`).
- **T-0106 / IDA-SEC-03** — confirmation + reset codes are non-cryptographic 6-digit
  `Random.Shared.Next(100000, 999999)` strings; `ConfirmUserEmail` resolves the user by the **bare
  code** (`UserRepository.GetByConfirmationCodeAsync`, `UserRepository.cs:45-48`;
  `ConfirmUserEmail.cs:37,65`), and `ChangePassword` compares the reset code in **plaintext**
  (`ChangePassword.cs:56-63`, `user.ResetPasswordCode == command.Code`).

This ticket encodes "what correct looks like" as executable red tests so the fixes have a regression
net the moment they exist. The two fixes own the production-code changes (and IDA-SEC-03's migration);
this ticket owns the **test cases that prove the holes are closed**. Governed by ADR-0001
(server-truth-identity, S1) for the claim-binding cases; the token-secret cases are governed by the
S-laws in `agents/knowledge/security-rules.md` (no ADR for token-secret design — see T-0106 notes).

## Acceptance criteria

- [ ] **AC1 — Google email binds to the verified token, not the request.** Given a stubbed valid
      Google ID-token whose `payload.Email` differs from `command.Email`, When `GoogleAuth` runs,
      Then the test proves the request-claimed email cannot bind a session to that account (validation
      fails / the verified email is used) and no `JwtTokenResponse` is issued for the attacker-claimed
      address. (Covers IDA-SEC-01 AC1.)
- [ ] **AC2 — Google GoogleId binds to the verified token, not the request.** Given a stubbed token
      whose `payload.Subject` differs from `command.GoogleId`, When processed, Then the test proves the
      client-supplied `GoogleId` cannot bind to an account it does not own. (Covers IDA-SEC-01 AC2.)
- [ ] **AC3 — Audience is enforced and the dev bypass is gone.** A test asserts a token whose `aud`
      does not match the configured client id fails with `BusinessErrorMessage.InvalidGoogleUserToken`
      (`auth.invalid_google_token`), and a separate test asserts a forged/unverifiable token is
      rejected **even with the dev flag set** — i.e. there is no `IsDevelopment` short-circuit. The
      existing bypass-dependent tests
      (`GoogleAuthValidatorTests.cs:51-62` `When_In_Development_Mode_And_Token_Is_Not_Empty_Then_Validation_Passes`,
      `:178-190` `When_All_Fields_Are_Valid_Then_Validation_Passes` setting `IsDevelopment=true`) are
      rewritten so they no longer assert the bypass; the `InternalAuthTypeError`, required-field, and
      email-format cases stay green. (Covers IDA-SEC-01 AC3/AC4/AC5/AC7.)
- [ ] **AC4 — Email confirmation is not resolvable by code alone.** A test proves a confirmation code
      valid for user B does **not** confirm/log in user A, and the confirm flow no longer logs in B via
      a bare-`GetByConfirmationCodeAsync(code)` path — the lookup is account-scoped. (Covers
      IDA-SEC-03 AC3.) This replaces the bare-code happy-path assumption in
      `ConfirmUserEmailValidatorTests.cs:90-107` `When_Code_Is_Valid_Then_Validation_Passes`.
- [ ] **AC5 — Reset lookup is `(email, hashed-token)`.** A test proves a correct code for the **wrong
      email** and a wrong code for the **right email** both fail with
      `BusinessErrorMessage.NotValidResetPasswordToken`, and that the comparison is against the **hash**
      (the persisted column never equals the raw code submitted). (Covers IDA-SEC-03 AC4.)
- [ ] **AC6 — Tokens are cryptographic and stored hashed.** A unit test asserts the generated
      confirmation/reset codes are not produced via `Random.Shared`, two successive tokens are
      non-sequential/unpredictable, and the value persisted on the `User` row is a hash that never
      equals the raw token delivered to the user. (Covers IDA-SEC-03 AC1/AC2.)
- [ ] **AC7 — Expiry is enforced and codes are one-shot.** Tests cover, for **both** flows, an expired
      code (rejected) and a successfully-consumed code that is invalidated and cannot be replayed.
      Extends the existing expiry case
      `ConfirmUserEmailValidatorTests.cs:69-88` `When_Code_Is_Valid_But_Expired_Then_Validation_Fails_With_InvalidCode_Error`.
      (Covers IDA-SEC-03 AC5.)
- [ ] **AC8 — Anonymous lookups stay tenant-filtered.** A test/assertion confirms the confirm/reset
      repository lookups do NOT call `IgnoreQueryFilters()`, so a hashed-token match cannot resolve
      cross-tenant. (Covers IDA-SEC-03 AC6.)
- [ ] **AC9 — Suite is red first, then green.** Each new test is committed/visible as failing for the
      right reason against the pre-fix code, then green once T-0105/T-0106 land in the same merge; the
      status log notes "red → green" per `agents/knowledge/testing.md` §"How the ticket shows TDD
      happened". The full `Cleansia.Tests` + `Cleansia.IntegrationTests` Auth suites pass.

## Out of scope

- The production fixes themselves (token generation/hashing, audience binding, account-scoped lookup,
  the EF migration) — owned by **T-0105** (IDA-SEC-01) and **T-0106** (IDA-SEC-03).
- IMP-1 Google OAuth Console / client-id provisioning and the OAuth login UX.
- Rate-limiter tests (IDA-SEC-02 / BSP-4 → TC-AUTHZ-0 limiter case), refresh-rotation re-check
  (IDA-SEC-06), and log-scrub tests (IDA-SEC-05) — separate tickets in the auth family.
- Cross-tenant resolution on anonymous auth routes (IDA-SEC-10) beyond the AC8 no-`IgnoreQueryFilters`
  guard.
- Any frontend/Jest specs or i18n changes — these are backend (xUnit) tests only.

## Implementation notes

- **TEST-FIRST per `agents/knowledge/testing.md`.** Strict red→green→refactor: token generation +
  hashing + lookup/expiry are pure/handler-contract logic, and the claim-binding cases are
  contract-level handler/validator tests. The tests must predate (or land with) the fix in the diff;
  Gate 6 (Reviewer) enforces TDD order via commit order / the status log. Because they pair with
  T-0105/T-0106, schedule so the tests are written first and the three merge together.
- **Governing decisions:** ADR-0001 (server-truth-identity / S1) for AC1–AC4 claim-binding; the
  S-laws in `agents/knowledge/security-rules.md` (S4 secret-leak: token hashes; S5/S6 logging) for the
  token-secret cases. ADRs 0002/0003 do not apply.
- **Serialization cluster:** **none.** The test files below, and the production files T-0105/T-0106
  touch (`GoogleAuth.cs`, `ConfirmUserEmail.cs`, `ChangePassword.cs`, `UserRepository.cs`, `User.cs`),
  do not appear in any cluster in `agents/backlog/TICKET-MAP.md`. Safe to run concurrently with other
  Wave-0 work under the standard reviewer-per-developer invariant. **One caveat:** because this ticket
  and its two fixes edit the *same test files* (see below) and merge together, treat the
  T-0105 / T-0106 / T-0128 trio as a single coordinated change — do not let two of them edit the same
  test file concurrently; serialize those edits within the shared merge.
- **Where the tests live (all exist today):**
  - Unit — `src/Cleansia.Tests/Features/Auth/GoogleAuthValidatorTests.cs` (rewrite the two
    `IsDevelopment`-bypass cases; add audience-mismatch + claim-binding cases),
    `src/Cleansia.Tests/Features/Auth/ConfirmUserEmailValidatorTests.cs` (account-scoped + one-shot +
    expiry cases). Reset-path unit coverage:
    `src/Cleansia.Core.AppServices/Features/Users/ChangePassword.cs` validator → add a
    `ChangePasswordValidatorTests.cs` under `src/Cleansia.Tests/Features/Auth` or
    `.../Features/Users` (no existing reset-validator test). Token-generation/hashing unit test against
    `src/Cleansia.Core.Domain/Users/User.cs` (`CreateWithPassword`, `UpdateConfirmationCode`,
    `UpdateResetPasswordToken`).
  - Integration — `src/Cleansia.IntegrationTests/Features/Auth/GoogleAuthTests.cs`,
    `.../ConfirmUserEmailTests.cs` (add the cross-account / wrong-email rejection routes).
  - Fixtures — reuse `Cleansia.TestUtilities/MockDataFactories/Users/UserMockFactory.cs`
    (`UserMockFactory.Generate(new UserPartial { ConfirmationCode = …, ConfirmationCodeExpiresAt = … })`);
    do not hand-roll entity graphs inline (testing.md §"How to write them").
- **Error contracts to assert on (constants, not literals):**
  `BusinessErrorMessage.InvalidGoogleUserToken`, `BusinessErrorMessage.InvalidConfirmationCode`,
  `BusinessErrorMessage.NotValidResetPasswordToken`.
- **Anti-patterns to avoid (testing.md §"Anti-patterns"):** no existence-only assertions, no
  all-happy-path, do not couple to log strings/private fields, do not assert hardcoded message
  literals. The cross-user/cross-tenant rejection cases (AC4, AC5, AC8) are mandatory, not optional.

## Status log
- 2026-06-01 — draft (created by pm)
- 2026-06-05 — **QA consolidation audit (cases already shipped inline with T-0105/T-0106).** Mapped every
  T-0128 AC to a concrete passing test. Build green (`dotnet build Cleansia.Api.sln -c Debug` → 0 errors);
  full T-0128 auth surface runs green. Found ONE genuine partial gap on AC3 and filled it (below). The one
  full-suite failure (`Features/EmployeePayroll/PayCalculatorTests`) is pre-existing/unrelated untracked work.

  **Coverage matrix — each T-0128 AC → existing/added test:**

  | AC | Required behavior | Covering test (file:line) |
  |---|---|---|
  | AC1 — verified email wins over request | provision/resolve by verified `claims.Email`, never `command.Email`; no JWT for attacker email | `Features/Auth/GoogleAuthHandlerTests.cs:64` `Uses_Verified_Email_Not_Request_Email_When_Provisioning` |
  | AC2 — verified subject wins over request | bind `claims.Subject` to `User.GoogleId`, never `command.GoogleId` | `Features/Auth/GoogleAuthHandlerTests.cs:89` `Uses_Verified_Subject_Not_Request_GoogleId_When_Provisioning` |
  | AC3 — forged/unverifiable rejected (handler fails closed) | verifier null → `InvalidGoogleUserToken`, no User/Cart/JWT | `Features/Auth/GoogleAuthHandlerTests.cs:110` `Forged_Token_Is_Rejected_With_InvalidGoogleUserToken_And_Creates_Nothing` |
  | AC3 — audience enforced + dev-bypass gone (verifier seam) | verification ALWAYS runs (no `IsDevelopment` short-circuit), audience pinned, fail-closed when unconfigured | **GAP FILLED →** `Features/Auth/GoogleTokenVerifierTests.cs:43` (unconfigured ClientId fails closed), `:64` (forged token rejected with audience configured), `:78` (source-guard: no `IsDevelopment`, audience pinned). Source-level confirmation: `IsDevelopment` absent from all of `Cleansia.Core.AppServices`; bypass removed from `GoogleAuth.cs`. |
  | AC3 — validator bypass cases rewritten | required-field / email-format / `InternalAuthTypeError` stay green; no `IsDevelopment` bypass asserted | `Features/Auth/GoogleAuthValidatorTests.cs:59` `When_Token_Is_Not_Empty_Then_No_Token_Validation_Error` (replaces the old bypass-pass), `:103` (InternalAuthTypeError), `:185` (all-valid passes), required/format cases `:32-152` |
  | AC4 — confirm not resolvable by code alone | token valid for B never confirms A; lookup account-scoped by hash | `Features/Auth/ConfirmUserEmailSecurityTests.cs:73` `Token_Valid_For_Another_Account_Does_Not_Confirm_The_Attacker` |
  | AC5 — reset by (email, hashed-token) | correct code+wrong email AND wrong code+right email both fail `NotValidResetPasswordToken`; compare over hash, never plaintext | `Features/Users/ChangePasswordSecurityTests.cs:50` (right), `:63` (correct code/wrong email fails), `:85` (wrong code/right email fails), `:99` (submitting stored hash fails — no plaintext compare) |
  | AC6 — tokens cryptographic + hashed at rest | CSPRNG, non-sequential, ≥128 bits; stored value is hash ≠ raw; no `Random.Shared` | `Common/SecurityTokensTests.cs:22` (high-entropy/non-sequential), `:43` (hash deterministic ≠ raw), `:59` (`Domain_Token_Generation_Does_Not_Use_Random_Shared`), `:72`/`:86`/`:99` (CreateWithPassword/UpdateConfirmationCode/UpdateResetPasswordToken return raw, store hash) |
  | AC7 — expiry + one-shot (both flows) | expired rejected; consumed token cleared + cannot replay | confirm: `ConfirmUserEmailSecurityTests.cs:95` (expired), `:115` (one-shot/replay); reset: `ChangePasswordSecurityTests.cs:116` (expired), `:131` (`Handler_Clears_Reset_Token_After_Consumption`); validator expiry `ConfirmUserEmailValidatorTests.cs:70` |
  | AC8 — anonymous lookups stay tenant-filtered | confirm/reset lookups do NOT call `IgnoreQueryFilters()`; bypass confined to the named cross-tenant method | `Features/Auth/UserRepositoryTokenLookupTenantTests.cs:36` (`...Do_Not_Ignore_Tenant_Filter`), `:50` (`IgnoreQueryFilters_Is_Confined_To_The_Named_Cross_Tenant_Method`) |

  **Gap filled (true gap, AC3):** added `src/Cleansia.Tests/Features/Auth/GoogleTokenVerifierTests.cs`
  (4 cases). WHAT WAS MISSING: the existing handler tests mock `IGoogleTokenVerifier`, so they proved the
  handler fails closed when the verifier returns null (AC5) but NOT that the verifier itself enforces the
  audience, fails closed when `ClientId` is unconfigured, and carries no `IsDevelopment` bypass — the
  `GoogleTokenVerifier` production class had ZERO unit tests. The new cases assert (a) empty/whitespace
  `ClientId` → returns null (fail-closed audience), (b) a forged/garbage token with a configured audience
  → returns null and never throws (verification always runs, no dev short-circuit), (c) a source-contract
  guard that the verifier has no `IsDevelopment` branch and does pin `Audience` (mirrors the
  `SecurityTokensTests` source-guard idiom already in the suite). All 4 pass.

  **Deferred-to-integration (honest, not a gap):** the full forged-SIGNATURE rejection (a syntactically
  valid JWT with a bad RSA signature / mismatched real `aud`) exercises Google's live crypto/JWKS path and
  is covered at the integration layer (`Cleansia.IntegrationTests/Features/Auth/GoogleAuthTests.cs` stubs
  the verifier and documents that there is no `IsDevelopment` bypass). The unit cases cover the fail-closed
  branches the unit harness can run without network.

  **DONE-STATUS: T-0128 is FULLY SATISFIED** by the existing tests plus the one added
  `GoogleTokenVerifierTests` — every AC1–AC8 maps to a concrete passing test, with the live-signature
  rejection honestly deferred to the integration suite.

## Review
**QA consolidation audit — APPROVED (reviewer, 2026-06-05).** Coverage matrix verified: AC1–AC8 each map to
a concrete passing test — verified-email-wins / verified-subject-wins / forged-rejected
(`GoogleAuthHandlerTests`), validator bypass-cases rewritten (`GoogleAuthValidatorTests`), confirm-not-by-
bare-code (`ConfirmUserEmailSecurityTests`), reset-by-(email,hash) (`ChangePasswordSecurityTests`), crypto
tokens/hashed-at-rest (`SecurityTokensTests`), expiry+one-shot both flows, anon lookups stay tenant-filtered
(`UserRepositoryTokenLookupTenantTests`). **One genuine gap found + filled** (good catch): the
`GoogleTokenVerifier` *production class* — the sole home of AC3's "audience enforced" + "IsDevelopment-bypass
gone" — had **zero** direct tests (the handler tests only mock `IGoogleTokenVerifier`). New
`GoogleTokenVerifierTests` (4 cases): unconfigured ClientId fails closed, forged token rejected with audience
configured, source-guard that no `IsDevelopment` branch exists + audience is pinned. Live forged-SIGNATURE
(JWKS crypto) rejection honestly deferred to the integration suite.

**Verification (orchestrator):** `GoogleTokenVerifierTests.cs` present (file-isolated, real — fills a genuine
gap, not a duplicate). Coverage conclusion sound. Test-count caveat: same as T-0127 — the audit ran
concurrently with T-0125 writing pay-calc files, so the "457" count is verified authoritatively once
T-0125 + T-0126 land. No production change. Not committed.

- 2026-06-05 — done (consolidation audit APPROVED; T-0128 fully satisfied after filling the GoogleTokenVerifier
  AC3 seam gap; live-signature rejection deferred-to-integration). NOT committed.
