# Security — iOS Customer Auth (Sign in with Apple / Google social login)

Scope: the Customer Mobile API social-auth surface consumed by the native iOS/Android customer apps —
`POST /api/Auth/AppleAuth` (T-0343, net-new) and `POST /api/Auth/GoogleAuth` (existing). The S1
server-truth-identity boundary lives in `AppleTokenVerifier` / `GoogleTokenVerifier`; the account
link/create + takeover guard lives in the `AppleAuth` / `GoogleAuth` handlers.

---

## 2026-06-28 — T-0312 Slice B (iOS customer email-auth core) build-time verification (auth-security gate, PASS)

Branch `phase/ios-phase6`, uncommitted. Scope: the shared Core auth-spine change (`CleansiaCore/Auth/Auth.swift`)
that adds the customer email/password register route, plus the customer VM/views/routing. Reviewer covers
Gate-DP/parity in parallel; this entry OWNS the auth-security gate. Tests run on iPhone 17 (simulator, booted):
`AuthApiClientTests` 16/16 green (Core spine, via `xcodebuild test -scheme CleansiaCore -destination
'…name=iPhone 17'`); `CleansiaCustomerTests` 32/32 green (VM + routing). Verdict per rule below.

VERDICT: **PASS** — exploitable as-written: none found. The shared-spine Register change is **non-regressing**
and has **no parallel auth write path**; the empty-token gate cannot yield a session without a verified token;
the anon auth routes carry no Bearer; no token/PII logging.

### Shared-spine Register change (highest scrutiny) — PASS
- **Partner path UNCHANGED.** `RegisterEndpoint` enum: `.employee → api/Auth/RegisterEmployee`,
  `.customer → api/Auth/Register` (`Auth.swift:66-76`). `init` defaults `registerEndpoint = .employee`
  (`Auth.swift:97`); `PartnerAuthSpine.make` does NOT pass it (`CleansiaPartner/Sources/PartnerClients.swift:24-31`)
  → partner still hits `RegisterEmployee` with the `.partner` allow-list. The `RegisterEmployeeRequest →
  RegisterRequest` rename is serialization-identical (same 5 fields, same casing, same `Encodable`;
  `Auth.swift:39-45`) — no wire drift on the partner body. Zero lingering `RegisterEmployeeRequest` refs
  (grep clean). Tests: `testDefaultRegisterEndpointStaysEmployeeForPartnerByteEquivalence`,
  `testCustomerRegisterTargetsRegisterPathNotRegisterEmployee` (asserts path `/api/Auth/Register` + NIL
  Authorization), `testRegisterTargetsRegisterEmployeePath`.
- **NO parallel auth write path.** Customer `register()` routes through the SAME `post()→send()→headerAdapter.apply`
  transport as login/confirm (`Auth.swift:138-153`), returns `ApiResult<Bool>`, and **never** calls
  `resolveEmailGate`/`persist`. The single token-persist mutation `tokenStore.save` has exactly two call
  sites — `persist()` (`Auth.swift:315`, reached only via `resolveEmailGate` ← login `:130` / confirm `:166`)
  and the spine's `SessionRefresher.swift:71` (refresh rotation) — both pre-existing; the diff added neither.
  Register cannot mint or persist a session.
- **Register returns Bool, never persists.** `RegistrationAuthClient.register → ApiResult<Bool>`
  (`ContainerSeams.swift:12-20`); the VM maps success → `.needsEmailConfirm(email:)` only
  (`CustomerAuthViewModel.swift:168-173`).

### Anon allow-list / no-Bearer discipline (S2/S1) — PASS
- `/api/auth/register` is in `sharedAuth` (`AnonymousAllowList.swift:18`) → present in BOTH `.partner` and
  `.customer` lists (`:41-42`); the customer host installs `.customer` (`CustomerClients.swift:11-14`). So
  `api/Auth/Register` is anon for the customer host. Belt-and-suspenders: register also forces
  `useNoAuthSession: true` (`Auth.swift:152`) → `accessToken = nil` passed to the adapter regardless of the
  allow-list. Login/register/confirmuseremail/resendconfirmationemail/forgotpassword/refreshtoken all anon;
  Logout stays AUTHED (not in the list; rides the live Bearer in `logout()` `Auth.swift:205-212`).
  `appleauth`/`googleauth` are Slice C / pre-existing — `googleauth` already anon (`:19`), `appleauth` N/A here.
  Tests: `testAnonAuthPathsNeverCarryBearer`, `testConfirmEmailDoesNotAttachBearerEvenWithStoredToken`.
- **X-Device-Id / X-Time-Zone invariants preserved** for the customer host: stamped unconditionally in
  `HeaderAdapter.apply` (`HeaderAdapter.swift:24-32`), independent of the Bearer branch; the spine change did
  not touch the adapter. Tests assert `X-Device-Id`/`X-Time-Zone` present on anon paths (`AuthApiClientTests:231-232,254`).

### Empty-token gate / no silent session (ADR-0019, the load-bearing invariant) — PASS
- `resolveEmailGate` (`Auth.swift:190-203`): empty/missing token ⇒ `.unverifiedEmail(hasToken:false)` BEFORE
  any persist; `persist` runs only with a non-empty token; `isEmailConfirmed != true` ⇒ `.unverifiedEmail
  (hasToken:true)` — `.authenticated` is returned ONLY for a persisted, confirmed token.
- VM/routing: `signUp` → `.needsEmailConfirm` always; `signIn`/`confirmEmail` map `.unverifiedEmail` →
  `.needsEmailConfirm` (confirm shows an error, never a session); only `.authenticated` → `.signedIn`
  (`CustomerAuthViewModel.swift:188-199,248-260`). `Route.afterAuth(.signedIn)=.home` is the ONLY route to
  `.home` from auth, and the splash `.home` is gated by `hasValidSession` = non-empty persisted access token
  (`AppContainer.swift:106-109`). No unconfirmed/empty-token path reaches `.home`. Tests:
  `testLoginWithEmptyTokenIsUnverifiedNoToken` (asserts `store.current()==nil`),
  `testConfirmEmailEmptyTokenIsUnverifiedNoTokenAndStoresNothing`,
  `testSignInEmptyTokenUnverifiedRoutesToVerifyNotError` (asserts route == `.verifyEmail`, not `.home`).

### Event-driven VM / no PII or token leak (S4/S6) — PASS
- `AuthOutcome` (`AuthOutcome.swift`) = `signedIn | needsEmailConfirm(email) | passwordReset` — carries no
  token/refresh/secret. The `email` in `needsEmailConfirm` is the user's own entered email (from the sign-in/
  sign-up form or the confirmed DTO email; `CustomerAuthViewModel.swift:170,255`) — no PII beyond self.
- **No logging** of token/password/auth response anywhere in `CleansiaCore/Auth/*` or `CleansiaCustomer/.../Auth/*`
  (grep for `print|os_log|Logger|NSLog|debugPrint` = none). Password fields use `CleansiaTextField(isPassword:true)`
  → `SecureField` (`Components/CleansiaTextField.swift:102-103`); the password lives only in transient form
  state (`SignUpFormState`/`SignInFormState`), passed to the request and not retained beyond it.

### Tenant scoping / no new bypass (ADR-0019) — PASS
No new endpoint or transport added; register reuses the one spine. The empty-token gate, the single-flight
refresh (`SessionRefresher`), the single token source, and the anon allow-list are all unchanged in shape —
the diff only adds a destination path enum + a constructor parameter. Customer register cannot mint a session
without the verified-token (login/confirm → `resolveEmailGate` → `persist`) path.

### Owner-gated (NOT part of this build-time gate)
The customer email-register backend route (`POST /api/Auth/Register` on the Customer Mobile host) and its rate
limit (`[EnableRateLimiting("auth")]`, S5) are backend concerns verified separately; the customer mobile
spec/client regen is owner-gated. iOS Slice B is client-only and ships safely behind the verified-token gate.

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
