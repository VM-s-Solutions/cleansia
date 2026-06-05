---
id: T-0105
title: Google sign-in verifies Google ID-token claims server-side; remove IsDevelopment bypass
status: draft
size: M
owner: —
created: 2026-06-01
updated: 2026-06-01
depends_on: []
blocks: []
stories: []
adrs: [0001]
layers: [backend]
security_touching: true
manual_steps: []
sprint: 0
source: finding IDA-SEC-01
---

## Context
Audit finding **IDA-SEC-01** (Wave 0, critical — `audits/AUDIT-2026-06-01-execution-plan.md:30-32`,
:110; one of the top-5 "account takeover on the auth surface" risks): Google sign-in **trusts the
client-supplied `Email` and `GoogleId`** and never binds them to the verified Google ID-token. This
is a full account-takeover hole — an attacker can authenticate as any user by posting that user's
email with any string in `GoogleId`/`Token`.

Two concrete defects in `Features/Auth/GoogleAuth.cs`:
1. **Dev bypass that ships to prod.** `Validator.ValidateGoogleUserAsync` (`GoogleAuth.cs:63-66`)
   short-circuits with `return true` whenever `_googleConfig.IsDevelopment` is true. `IGoogleConfig`
   has a single member, `IsDevelopment` (`IGoogleConfig.cs:5`), and `GoogleConfig` **defaults it to
   `true`** (`GoogleConfig.cs:8`) — so a missing/misconfigured `Google:IsDevelopment` setting in any
   environment disables token verification entirely.
2. **No claim binding even when verification runs.** `GoogleJsonWebSignature.ValidateAsync(token, new
   GoogleJsonWebSignature.ValidationSettings())` (`GoogleAuth.cs:68`) is called with **default**
   settings (no `Audience` → the `aud`/client-id is not checked) and its result (`payload`) is
   discarded. The handler then looks the user up and provisions by `command.Email` /
   `command.GoogleId` (`GoogleAuth.cs:95`, `:106`), i.e. the verified `payload.Email` /
   `payload.Subject` are never compared to the client-claimed values.

This finding falls under ADR-0001 (ADR-AUTHZ) systemic theme #2 "fail-open authorization &
client-trusted identity" and its **S1 server-truth-identity** principle (ADR-0001 D5: "don't trust
client identity"). The endpoint is exposed on the Customer and Partner web + mobile hosts
(`Web.Customer/.../AuthController.cs:51`, `Web.Partner/.../AuthController.cs:63`,
`Web.Mobile.Customer/.../AuthController.cs:54`, `Web.Mobile.Partner/.../AuthController.cs:59`).

## Acceptance criteria
- [ ] **AC1 — Email is taken from the verified token, not the request.** Given a valid Google
      ID-token whose `payload.Email` differs from `command.Email`, When `GoogleAuth` is processed,
      Then sign-in resolves/provisions the user against the **token's** email and the request-claimed
      email does not influence which account is returned (no token issued for the attacker-claimed
      address). Proven by a unit test on the validator/handler asserting the mismatch is rejected
      (or the verified email is used).
- [ ] **AC2 — GoogleId is taken from the verified token, not the request.** Given a valid ID-token
      whose `payload.Subject` differs from `command.GoogleId`, When processed, Then the verified
      subject is used (or the request is rejected) and the client-supplied `GoogleId` cannot bind a
      session to an account it does not own. Proven by a unit test.
- [ ] **AC3 — Audience is enforced.** Given an ID-token whose `aud` is not the configured Google
      OAuth client id, When validated, Then validation fails with `BusinessErrorMessage
      .InvalidGoogleUserToken` (`auth.invalid_google_token`). `GoogleJsonWebSignature
      .ValidationSettings.Audience` is set to the configured client id rather than left default.
      Proven by a test with a non-matching audience.
- [ ] **AC4 — The IsDevelopment bypass is gone.** There is no code path in `GoogleAuth.cs` that
      skips `GoogleJsonWebSignature.ValidateAsync` based on `IGoogleConfig.IsDevelopment` (or any
      environment flag). Proven by a test asserting an unverifiable/forged token is rejected even
      when the dev flag would have been set, plus the removal of the `IsDevelopment` branch.
- [ ] **AC5 — Invalid/forged tokens are rejected.** Given a token that fails
      `GoogleJsonWebSignature.ValidateAsync`, When processed, Then validation fails with
      `InvalidGoogleUserToken` and no JWT is issued and no `User`/`Cart` is created.
- [ ] **AC6 — Existing legitimate flow still works.** Given a valid ID-token matching the configured
      audience, When a known active Google user signs in, Then a `JwtTokenResponse` is returned; and
      for an unknown email a new `User.CreateWithGoogle(...)` + `Cart` are provisioned from the
      verified claims (`GoogleAuth.cs:96-111` behavior preserved on the happy path).
- [ ] **AC7 — Existing validator tests reconciled.** `GoogleAuthValidatorTests` is updated so the
      tests that relied on the `IsDevelopment=true` bypass
      (`GoogleAuthValidatorTests.cs:51-62`, `:178-190`) no longer assert the bypass; the
      `InternalAuthTypeError` / required-field / email-format cases remain green.

## Out of scope
- IMP-1 Google OAuth Console / client-id provisioning and the full OAuth login UX (owner-provided
  Google Cloud project; tracked separately as IMP-1).
- The reset-code / email-confirmation token hardening (`IDA-SEC-03`) and refresh re-check
  (`IDA-SEC-06`) — separate tickets in the same auth family.
- Any change to JWT issuance / audience map (ADR-0001 D5 is the contract; not edited here).
- The `IsDevelopment` flags on the host startup pipelines (`CleansiaStartupBase.cs:161`,
  host `ServiceExtensions.cs:40`) — unrelated to the Google token path.

## Implementation notes
- **TEST-FIRST per `knowledge/testing.md`.** This is auth/identity logic in a validator + handler —
  the "test-first at the contract" rule applies and the Reviewer expects the failing test to predate
  the fix (visible in commit order / status log). Paired test ticket is **TC-AUTH-TAKEOVER**
  (`pairs_with: T-0128`): token-claim binding for IDA-SEC-01 lands in the **same merge** as this fix.
- **Governing decision:** ADR-0001 (ADR-AUTHZ), S1 server-truth-identity / D5 "don't trust client
  identity." TICKET-MAP lists no ADR for IDA-SEC-01 directly; it is governed by that S1 principle.
- **Not in a serialization cluster.** `GoogleAuth.cs` is not in the TICKET-MAP shared-file
  serialization map; this ticket may run concurrently with other Wave-0 work (no shared-file
  collision). It has no `depends_on`.
- Primary file: `src/Cleansia.Core.AppServices/Features/Auth/GoogleAuth.cs`
  (validator `:59-75`, command `:78-84`, handler `:86-112`).
- Verified-claim source of truth: the `GoogleJsonWebSignature.Payload` returned by
  `ValidateAsync` — use `payload.Subject` (the `sub`/GoogleId) and `payload.Email`. Set
  `ValidationSettings.Audience = [configured client id]`.
- Config: `IGoogleConfig` (`Cleansia.Infra.Common/Configuration/Interfaces/IGoogleConfig.cs`) and
  `GoogleConfig` (`.../Configuration/GoogleConfig.cs`, bound from the `"Google"` section). A
  client-id member will be needed for the audience check; removing the `IsDevelopment` member or
  retiring its use is part of AC4 (coordinate so nothing else reads it — it is referenced only by
  `GoogleAuth.cs` and the tests).
- Error code already exists: `BusinessErrorMessage.InvalidGoogleUserToken = "auth.invalid_google_token"`
  (`BusinessErrorMessage.cs:9`). It has a frontend i18n key under `errors.auth.*` already (no new
  key). If a new error key is introduced, add it across all 5 locales per CLAUDE.md.
- No DTO/endpoint shape change is required (the `Command` fields stay; the fix changes which values
  are *trusted*), so **no nswag-regen** unless the team chooses to drop client-supplied
  `Email`/`GoogleId` from the request contract — if so, flag `nswag-regen` then.
- Tests to update/add: `src/Cleansia.Tests/Features/Auth/GoogleAuthValidatorTests.cs`; existing
  integration coverage `src/Cleansia.IntegrationTests/Features/Auth/GoogleAuthTests.cs`.

## Status log
- 2026-06-01 — draft (created by pm)
- 2026-06-02 — in_progress (backend). TEST-FIRST: wrote failing handler tests
  (`src/Cleansia.Tests/Features/Auth/GoogleAuthHandlerTests.cs`) + reconciled validator tests
  (`GoogleAuthValidatorTests.cs`) — RED confirmed (CS0122 `GoogleAuth.Handler` inaccessible + CS0246
  `IGoogleTokenVerifier` not found), the correct pre-fix failure.
- 2026-06-02 — done (backend). Introduced `IGoogleTokenVerifier` + `GoogleVerifiedClaims` record
  (`Services/Interfaces/IGoogleTokenVerifier.cs`) and the sole adapter `GoogleTokenVerifier`
  (`Services/GoogleTokenVerifier.cs`, audience pinned to `IGoogleConfig.ClientId`, always verifies, null
  on failure), registered `AddScoped<IGoogleTokenVerifier, GoogleTokenVerifier>()` next to `ITokenService`.
  Added `IGoogleConfig.ClientId` (+ `GoogleConfig.ClientId = string.Empty`) and DELETED `IsDevelopment`
  (AC4 — bypass cannot return). Rewrote `GoogleAuth.cs`: validator dropped `IGoogleConfig` + the
  `ValidateGoogleUserAsync` bypass (shape rules only); handler now verifies first and binds
  `claims.Email`/`claims.Subject` from the VERIFIED token (made `Handler` public for unit testing, matching
  `GetOrderAnalytics.Handler`). GREEN: `Cleansia.Tests` 153/153 pass (18 GoogleAuth). Solution build
  0 errors / 0 warnings (`src/Cleansia.Api.sln`, includes the reconciled `Cleansia.IntegrationTests`
  GoogleAuthTests that now stubs `IGoogleTokenVerifier`). No EF, no nswag, no real client id committed.
  Owner dependency: IMP-1 must supply the real `Google:ClientId` for live OAuth (verifier fails closed until then).

- 2026-06-02 — done (reviewer APPROVED + security PASS, both verified against the real code; build 0
  errors, Cleansia.Tests 153 passed/0 failed — independently re-verified by orchestrator). NOT committed.

## Review
**Reviewer — APPROVED (2026-06-02).** Verified every load-bearing claim against the real code on disk
(handler, verifier interface + adapter, DI registration, `IGoogleConfig`/`GoogleConfig`, both test
files); re-ran build + tests; did not trust the dev report. Handler binds identity from VERIFIED
`claims.Email`/`claims.Subject` (`GoogleAuth.cs:82`,`:95`), never the client's `command.Email`/
`command.GoogleId`; `FirstName`/`LastName` still from the command with a documented rationale (Google
ID-token may carry no name). The `IGoogleTokenVerifier` seam is placed/namespaced like `ITokenService`
(`Services/Interfaces` + `Services/`), registered `AddScoped` beside the sibling. `GoogleTokenVerifier`
is the ONLY caller of `GoogleJsonWebSignature.ValidateAsync`, pins `Audience = ClientId`, has no env
bypass, fails closed (null). Validator no longer takes `IGoogleConfig` and keeps only shape rules.
Tests written test-first (RED = CS0122 + CS0246), assert on the `InvalidGoogleUserToken` constant,
cover AC1/AC2/AC5/AC6; AC7 reconciles the old `IsDevelopment`-bypass tests; `Add` Times.Never on reject.
No nswag/ef, no real client-id committed, not committed.

**Security — PASS (2026-06-02).** The account-takeover hole is closed at the binding point: a request
whose `command.Email` differs from the verified `payload.Email` cannot yield a JWT for the
attacker-claimed address, and `command.GoogleId` can never bind a session (`User.CreateWithGoogle` uses
`claims.Subject`) — S1 server-truth-identity / ADR-0001 D5. The `IsDevelopment` `return true` bypass is
GONE (deleted from `IGoogleConfig`/`GoogleConfig`; solution-wide grep shows only standard
`IHostEnvironment.IsDevelopment()` runtime checks remain — out of scope). Audience pinned to the
configured client id (rejects tokens minted for another OAuth client). Fails closed: any
failure/expired/wrong-audience/unconfigured-ClientId → null → uniform `InvalidGoogleUserToken`, no
enumeration leak (S4), no User/Cart/JWT created on reject (`Add` Times.Never). No real client secret
committed; empty default still fails closed. Claim-binding test proves it, not prose (D5).

**Verification (orchestrator, independent):** read the new verifier + interface + handler line-by-line
— handler uses `claims.Email`/`claims.Subject` for both lookup and provisioning; verifier fails closed
on empty `ClientId` (extra guard, lines 21-24) and on missing subject/email (line 34). Solution-wide
grep for `IsDevelopment` confirms only standard ASP.NET runtime checks remain (the `IGoogleConfig`
config-flag bypass is fully eradicated). `dotnet build Cleansia.Api.sln` = 0 errors; `dotnet test
Cleansia.Tests` = 153 passed / 0 failed. No EF, no nswag. ⚠️ owner dependency: IMP-1 must supply the
real `Google:ClientId` for live OAuth (verifier fails closed until then). Not committed.
