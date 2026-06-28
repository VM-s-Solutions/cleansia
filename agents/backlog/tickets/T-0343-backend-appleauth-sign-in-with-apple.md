---
id: T-0343
title: "Backend: AppleAuth (Sign in with Apple) — AppleAuth CQRS + IAppleTokenVerifier + AppleConfig + User.AppleId (mirrors GoogleAuth)"
status: ready
size: M
owner: backend
created: 2026-06-28
updated: 2026-06-28
depends_on: []
blocks: [T-0312]
stories: []
adrs: [0013, 0019]
layers: [backend, db]
security_touching: true
priority: high
manual_steps: [ef-migration-user-appleid, mobile-spec-regen]
sprint: 12
source: Q-IOS-04 ruling (sprint-12 §7.14); resolves the planned SIWA backend endpoint (the former §10 T-0326 placeholder)
---

> **The backend half of iOS Sign in with Apple (Q-IOS-04 / sprint-12 §7.14).** SIWA is net-new (no Android
> reference, no `/api/Auth/AppleAuth` in the committed spec). This mirrors the existing `GoogleAuth` 1:1 and
> ships **fail-closed** behind empty config so it merges with no owner provisioning (live sign-in is gated on
> **T-0344** Apple + **T-0345** Google). Security gate: **PASS-WITH-REQUIREMENTS** (sprint-12 §7.14 / the binding
> rules below). **The single highest-risk surface is the RS256/JWKS verification — it is genuinely net-new
> (GoogleTokenVerifier delegates to a library), so it MUST be a vetted, alg-pinned path, not hand-rolled.**

## Scope (6 work items — mirror the Google pattern at `Features/Auth/GoogleAuth.cs` + `Services/GoogleTokenVerifier.cs`)

1. **`AppleAuth` CQRS** — `src/Cleansia.Core.AppServices/Features/Auth/AppleAuth.cs`: `Command(IdentityToken, RawNonce, FirstName, LastName) : ICommand<JwtTokenResponse>`; a `BaseAuthValidator` with **shape-only** rules (IdentityToken + RawNonce `NotEmpty`, name rules) — **identity is bound from verified claims, never validated/bound client-side**; Handler injects `IAppleTokenVerifier` + `ITokenService` + `ICartRepository` + `IUserRepository` + `IHostAudienceProvider` (same collaborator set as GoogleAuth). Look up by `claims.Email`; run the takeover guard (below); reuse `tokenService.GenerateTokenAsync(...)`; on no-user create only when `claims.EmailVerified` via `User.CreateWithApple` + `Cart.CreateWithUser`.
2. **`IAppleTokenVerifier` + `AppleTokenVerifier`** — `Services/Interfaces/IAppleTokenVerifier.cs` returning `AppleVerifiedClaims(Subject, Email, EmailVerified)`; `Services/AppleTokenVerifier.cs` as the **sole** caller of Apple's JWKS path. DI registration alongside `IGoogleTokenVerifier`.
3. **`AppleConfig`** — `Infra.Common/Configuration/AppleConfig.cs` : `AutoBindConfig(configuration, "Apple")`, `IAppleConfig` with `BundleId` (default empty ⇒ verifier fails closed), mirroring `IGoogleConfig`; register in `Cleansia.Config` `ConfigurationExtensions` next to `GoogleConfig`.
4. **Domain** — `AuthenticationType.Apple = 3`; `User.AppleId` (nullable, `MaxLength 512`, sibling to `GoogleId`, **included in `Anonymize()`**); static `User.CreateWithApple(email, firstName, lastName, appleSub, languageCode?)` setting `AuthenticationType.Apple`, `AppleId`, `IsEmailConfirmed = true` (parity with `CreateWithGoogle`); EF entity config + filtered index on `AppleId` mirroring `GoogleId` (`UserEntityConfiguration.cs:118`).
5. **Endpoint + error + i18n** — `[AllowAnonymous] [HttpPost("AppleAuth")] [EnableRateLimiting("auth")]` on **`src/Cleansia.Web.Mobile.Customer/Controllers/AuthController.cs`** ONLY (mirrors the GoogleAuth action; returns `JwtTokenResponse` in the body; NOT on Partner/Admin/Web hosts). `BusinessErrorMessage.InvalidAppleUserToken = "auth.invalid_apple_token"` + the `errors.auth.invalid_apple_token` key in **all 5 languages** (en/cs/sk/uk/ru).
6. **Unit tests** (stubbed verifier; mirror `GoogleAuthHandlerTests` + `GoogleTokenVerifierTests`) — verified `email`/`sub` win over request fields; forged/null verifier ⇒ `InvalidAppleUserToken` and **creates nothing** (no User/Cart/token); `AuthenticationType != Apple` collision for **BOTH** `Internal` AND `Google` ⇒ `InternalAuthTypeError`; unverified email ⇒ no provision; verifier fails closed on empty `BundleId`; source-guard that aud/iss/nonce are checked and **no `IsDevelopment` bypass** exists. Live-signature/JWKS rejection deferred to the integration suite (the T-0128 precedent).

## Binding security requirements (PASS-WITH-REQUIREMENTS — sprint-12 §7.14)

- **RS256 PINNED.** Verify the signature against the JWKS key whose `kid` matches the token header; **pin `ValidAlgorithms = ["RS256"]`** — reject `alg:none` and HS256/symmetric key-confusion. **Use a vetted handler** (Microsoft.IdentityModel `JsonWebTokenHandler` + `ConfigurationManager`/`OpenIdConnectConfigurationRetriever` for cached JWKS) — do **not** hand-parse JWKs. *(HIGH — this is where a takeover would re-enter.)*
- **aud == `AppleConfig.BundleId`** (native bundle id, NOT a Services ID); **iss == `https://appleid.apple.com`** exactly; **exp/iat** validated.
- **Nonce binding server-side:** `SHA256(rawNonce)` (encoding/case matching Apple's representation) `== token.nonce`; cover the encoding with a known-vector test (a hex/base64 mismatch is a silent fail-closed).
- **Fail-closed** identical to Google: empty/whitespace `BundleId` ⇒ null; ANY failure ⇒ null ⇒ uniform `InvalidAppleUserToken`. **No env bypass.**
- **JWKS** = hardcoded `https://appleid.apple.com/auth/keys` (HTTPS, no config override, no cross-host redirect ⇒ no SSRF), cached, refresh on unknown `kid`, outage ⇒ fail-closed.
- **Takeover guard in the HANDLER against `claims.Email`** (never the validator/`command.Email`): existing user with `AuthenticationType != Apple` ⇒ `InternalAuthTypeError` (covers Internal + Google). Mirror `GoogleAuth.cs:65-76`.
- **Provision only when `claims.EmailVerified == true`.** (This is stricter than today's Google flow → see **T-0346**.)
- **No token/PII logging** (S6): the Apple `identityToken`, raw nonce, Google `idToken`, decoded email/sub never logged at Information+. **Tenant scoping** (S8): standard tenant-filtered `GetByEmailAsync`, **no `IgnoreQueryFilters`**. **Response** stays `JwtTokenResponse` unchanged (no Apple sub/token/nonce/TenantId echoed).

## Done when
- [ ] `AppleAuth` + `AppleTokenVerifier` + `AppleConfig` + `User.CreateWithApple`/`AppleId`/`AuthenticationType.Apple` land, mirroring Google; `dotnet build` clean.
- [ ] The binding security requirements above hold; the unit suite (stubbed verifier) is green; reviewer **APPROVE** + security **PASS**.
- [ ] `[AllowAnonymous] POST /api/Auth/AppleAuth` exists on the Customer Mobile host only, rate-limited, returning `JwtTokenResponse`.

## MANUAL_STEPs (owner)
- **EF migration** for the new `User.AppleId` column (Claude does not run migrations).
- **Spec + client regen** of `customer-mobile-api.json` + the iOS/Android generated clients after the endpoint + DTOs land (unblocks the iOS T-0312 Apple call via the generated `CleansiaCustomerApi`).
- Live sign-in is gated on **T-0344** (Apple capability + `Apple:BundleId`) + **T-0345** (Google client ids + `Google:ClientId`).

## Status log
- 2026-06-28 — filed from the Q-IOS-04 ruling (sprint-12 §7.14). Backend-buildable now (fail-closed, stubbed-verifier tests); ships ahead of the owner provisioning (T-0344/T-0345) and the owner regen.
