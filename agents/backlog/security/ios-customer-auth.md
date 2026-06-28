# Security — iOS Customer Auth (Sign in with Apple / Google social login)

Scope: the Customer Mobile API social-auth surface consumed by the native iOS/Android customer apps —
`POST /api/Auth/AppleAuth` (T-0343, net-new) and `POST /api/Auth/GoogleAuth` (existing). The S1
server-truth-identity boundary lives in `AppleTokenVerifier` / `GoogleTokenVerifier`; the account
link/create + takeover guard lives in the `AppleAuth` / `GoogleAuth` handlers.

---

## 2026-06-28 — T-0343 AppleAuth build-time verification (Q-IOS-04 design gate, PASS-WITH-REQUIREMENTS)

Branch `phase/ios-phase6`, uncommitted. Build clean; `dotnet test --filter Apple` = 20/20 green.
This is the build-time verification of the Q-IOS-04 design gate (sprint-12 §7.14). Verdict per binding
requirement below.

VERDICT: **PASS** — all binding requirements met, exploitable as-written: none found.
ACCOUNT-TAKEOVER: **NO** — a forged token cannot mint claims (RS256/aud/iss/nonce all pinned and
fail-closed), and a cross-provider token cannot claim an existing account (the `AuthenticationType != Apple`
guard runs in the handler against the VERIFIED `claims.Email` and covers Internal + Google).

### Finding #1 (HIGH — net-new RS256/JWKS path) — PASS
`Services/AppleTokenVerifier.cs`:
- alg PINNED: `ValidAlgorithms = [SecurityAlgorithms.RsaSha256]` (:67) on `JsonWebTokenHandler.ValidateTokenAsync`
  (:73-74). `alg:none` and HS256/symmetric key-confusion against the public JWKS key are rejected;
  no path trusts the token-header alg. `RequireSignedTokens = true` (:70), `ValidateIssuerSigningKey = true` (:64).
- Keys via `ConfigurationManager<OpenIdConnectConfiguration>` + `OpenIdConnectConfigurationRetriever`
  (:34, :39-41, :55) — cached JWKS, refresh-on-unknown-kid; NOT hand-parsed. Source-guard test
  `AppleTokenVerifierTests.cs:88-104` pins RsaSha256 + ValidAlgorithms + issuer + audience + nonce +
  no `IsDevelopment`.

### Finding #2 (account-takeover) — PASS
`Features/Auth/AppleAuth.cs`:
- Lookup uses `claims.Email` (the VERIFIED email), never `command.*` (:68). Identity bound only from
  verified `sub`/`email` (:99 `User.CreateWithApple(claims.Email, ..., claims.Subject)`).
- `AuthenticationType != Apple` reject runs in the HANDLER (:75-79) → `InternalAuthTypeError`, covering
  BOTH Internal (password) AND Google collisions. Tested `[InlineData(Internal)] [InlineData(Google)]`
  (`AppleAuthHandlerTests.cs:157-181`).
- Provision ONLY on `claims.EmailVerified == true` (:91-95); unverified ⇒ `InvalidAppleUserToken`, no
  User/Cart (`AppleAuthHandlerTests.cs:184-201`).

### Remaining binding requirements — all PASS
- aud == `AppleConfig.BundleId` native bundle id (`AppleTokenVerifier.cs:61`, `IAppleConfig` doc says
  App ID not Services ID); iss == `https://appleid.apple.com` exact (:27, :59); exp/iat via
  `ValidateLifetime`+`RequireExpirationTime` (:68-69).
- Nonce binding: `SHA256(rawNonce)` as lowercase-hex-over-UTF8 (`Convert.ToHexStringLower`, :117-118)
  compared with `CryptographicOperations.FixedTimeEquals` (:87, :121-126) to `token.nonce`. Encoding
  pinned by known-vector test SHA256("abc") (`AppleTokenVerifierTests.cs:72-84`).
- Fail-closed: empty/whitespace `BundleId` ⇒ null BEFORE network (:48-51); ANY failure ⇒ null ⇒
  uniform `InvalidAppleUserToken`. No env bypass (source-guard `DoesNotContain("IsDevelopment")`).
- JWKS URL hardcoded `https://appleid.apple.com/auth/keys` const, no config override, no cross-host
  redirect ⇒ no SSRF (:29-31). Outage fails closed (try/catch ⇒ null, :105-108).
- No token/PII logging: zero ILogger/log calls in `AppleAuth.cs` / `AppleTokenVerifier.cs` (S6 PASS).
- Tenant scoping: `UserRepository.GetByEmailAsync` uses `GetDbSet()` with NO `IgnoreQueryFilters`;
  `User` is `ITenantEntity` with the global tenant filter auto-applied (S8 PASS).
- Response = `JwtTokenResponse` unchanged; no Apple sub/token/nonce/TenantId echoed (S4 PASS).
- Endpoint `[AllowAnonymous] [HttpPost("AppleAuth")]` on `Cleansia.Web.Mobile.Customer/AuthController.cs:61-70`
  ONLY; rate-limited by class-level `[EnableRateLimiting("auth")]` (:25), per-IP fixed-window partition
  for anonymous (`RateLimitPolicies.cs:124-129`) — not a global bucket (S5 PASS).
- `User.AppleId` nullable MaxLength(512), included in `Anonymize()` (`User.cs:43, 302`); filtered index
  mirrors GoogleId (`UserEntityConfiguration.cs`).
- GoogleAuth + GoogleTokenVerifier UNTOUCHED (git status empty); the `email_verified` Google hardening
  is correctly absent (that is T-0346), so it did not leak into this diff.

### Notes (NOT security-gate blockers — referred to the reviewer/parity lane)
- i18n: the diff added `errors.auth.invalid_apple_token` to the ADMIN and PARTNER web apps, but the
  AppleAuth endpoint lives on the Customer Mobile host. The CUSTOMER web app carries neither
  `invalid_google_token` nor `invalid_apple_token` (it never has), and the native iOS/Android customer
  apps localize via their own resource bundles. So the customer-facing string is delivered outside these
  web JSONs. Parity/i18n-completeness call for the reviewer; no auth-security impact.

### Owner-gated (live sign-in, not part of this gate)
EF migration for `User.AppleId`; mobile spec/client regen; `Apple:BundleId=cz.cleansia.customer` +
Apple capability/entitlement (T-0344). Until `Apple:BundleId` is set the verifier fails closed — the
endpoint ships safely dark.
