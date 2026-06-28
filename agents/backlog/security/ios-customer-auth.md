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

---

## 2026-06-28 — T-0312 Slice C (iOS customer SOCIAL sign-in: Apple + Google) build-time verification (auth-security gate, PASS)

Branch `phase/ios-phase6`, uncommitted. Scope: the iOS acquisition controllers (`AppleSignInController`,
`GoogleSignInController`, `Nonce`, `CustomerSocialSignInProvider`), the shared Core spine
`socialAuth`/`googleAuth`/`appleAuth` + `SocialAuthClient`/`AppleAuthRequest`/`GoogleAuthRequest`, the
`appleauth` anon allow-list entry, the customer VM social handlers, the SIWA entitlement + GoogleSignIn dep.
Reviewer covers Gate-DP/seam-discipline in parallel; this entry OWNS the auth-security gate. Tests run on
iPhone 17 (simulator, booted) via `xcodebuild test`: `CleansiaCoreTests/SocialAuthSpineTests` 6/6 green;
`CleansiaCustomerTests/CustomerAuthViewModelTests` 27/27 green (incl. TC-IOS-SOCIAL-NONCE
`testAppleNonceFlowRawToBackendHashedToApple` + `testAppleSpinePostsRawNonceNotHashed`).

VERDICT: **PASS** — exploitable as-written: none found.

### CROSS-COMPONENT — iOS↔backend nonce-encoding alignment (highest scrutiny) — PASS / ALIGNED
The iOS encoding MATCHES the T-0343 backend's lowercase-hex expectation; live SIWA will NOT silently fail-closed.
- iOS `request.nonce` = `Nonce.sha256(rawNonce)` (`AppleSignInController.swift:22`), where `Nonce.sha256`
  = `SHA256.hash(Data(input.utf8))` formatted `%02x` per byte and joined → **lowercase hex of SHA256 over
  UTF-8 bytes** (`Nonce.swift:23-26`). NOT raw bytes, NOT base64, NOT uppercase.
- Backend `AppleTokenVerifier.HashNonce` = `Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(rawNonce)))`
  compared to `token.nonce` via `FixedTimeEquals` (`AppleTokenVerifier.cs:115-126, :86-90`). Same algorithm,
  same UTF-8 input, same lowercase-hex output.
- Both sides pin the encoding by a KNOWN-ANSWER test on the SHA256("abc") vector:
  iOS `CustomerAuthViewModelTests.swift:378-379` and backend `AppleTokenVerifierTests` both assert
  `ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad` (canonical lowercase hex). A future
  case/encoding drift on either side breaks a test, not production.
- The credential carries the RAW (un-hashed) nonce to the spine: `AppleSignInController` finishes with
  `AppleCredential(rawNonce: self.rawNonce, …)` where `rawNonce` is the pre-hash value
  (`AppleSignInController.swift:53-58, :17,:21`); the VM forwards `credential.rawNonce` to
  `appleAuth(rawNonce:)` (`CustomerAuthViewModel.swift:281-289`); the spine POSTs it as `AppleAuthRequest.rawNonce`
  (`Auth.swift:159-171`, `AuthRequests.swift:59-64`). `testAppleSpinePostsRawNonceNotHashed` asserts the posted
  body == raw, `!= sha256(raw)`.
- Raw nonce is cryptographically random + single-use: `Nonce.randomRaw` draws each char via
  `SecRandomCopyBytes(kSecRandomDefault, …)` (`Nonce.swift:5-21`), generated fresh per `signIn()` call and
  cleared on `finish` (`AppleSignInController.swift:16-17,:34`). Length 32 asserted by test.
- Field-name alignment confirmed: iOS `AppleAuthRequest{identityToken, rawNonce, firstName, lastName}` ↔
  backend `AppleAuth.Command(IdentityToken, RawNonce, FirstName, LastName)` (`AppleAuth.cs:41-46`);
  iOS `GoogleAuthRequest{token, googleId, email, firstName, lastName}` ↔ `GoogleAuth.Command(Token, GoogleId,
  Email, FirstName, LastName)` (`GoogleAuth.cs:38-44`). The §7.14 D1 contract holds.

### Spine — single token-write path, no parallel persist — PASS
- `socialAuth` (`Auth.swift:174-190`) and both `googleAuth`/`appleAuth` route through the SAME
  `resolveEmailGate` (:184) → the SINGLE private `persist` (:200, body :309-318 → `tokenStore.save` :317).
- Exhaustive `tokenStore.save` call sites in the auth module: ONLY `Auth.swift:317` (the gate's persist) and
  `SessionRefresher.swift:71` (pre-existing refresh rotation, not a social path). No second write path.
- The acquisition controllers + provider contain ZERO `tokenStore`/Keychain/`save`/`persist` references
  (grep clean over `Features/Auth/Social/`) — the token enters the spine only via the one gate. Seam
  discipline holds as a security property.

### Anon transport — no Bearer on social routes — PASS (S2-aligned)
- `socialAuth` POSTs with `useNoAuthSession: true` (`Auth.swift:179`); `send` sets `accessToken = nil` when
  `useNoAuthSession` (:284) so no `Authorization` header is stamped even with a stored token. X-Device-Id /
  X-Time-Zone still applied via `headerAdapter` (:285). `testSocialAuthPathsNeverCarryBearerEvenWithStoredToken`
  proves no Bearer + X-Device-Id present.
- `appleauth` added to the anon allow-list (`AnonymousAllowList.swift:20`); `googleauth` already present (:19).
  Backend mirror: `[AllowAnonymous][HttpPost("AppleAuth")]` + class `[EnableRateLimiting("auth")]` on
  `Cleansia.Web.Mobile.Customer/Controllers/AuthController.cs:61-70, :25` (S5 PASS, per-IP partitioned auth window).

### Empty-token / verified-email gate — PASS (S-rule: never a silent .home on unconfirmed)
- `resolveEmailGate` returns `.unverifiedEmail(hasToken:false)` and does NOT persist when `token` is empty
  (`Auth.swift:197-199`); persists then routes `.unverifiedEmail(hasToken:true)` if `isEmailConfirmed != true`
  (:200-203); only `.authenticated` when confirmed (:204). VM maps `.unverifiedEmail → needsEmailConfirm`,
  never `.signedIn`/`.home` (`CustomerAuthViewModel.swift:304-312`).
- Apple verified-user path ⇒ signedIn: `User.CreateWithApple` sets `IsEmailConfirmed = true` (`User.cs:165`),
  so `TokenService.GenerateTokenAsync` returns `Token=<jwt>, IsEmailConfirmed=true` (`TokenService.cs:44-51`)
  ⇒ iOS gate → `.authenticated` → `.signedIn`. `testAppleSuccessAuthenticatedEmitsSignedIn` proves it.
  Google routes per the server response (`testGoogleUnverifiedEmitsNeedsEmailConfirmAndRouterMapsToVerify`).

### `.longLived` refresh-expiry fallback — PASS (not security-load-bearing)
- `socialAuth` passes `refreshLifetime: .longLived` only as a DISPLAY/expiry HINT: `refreshExpiry` uses the
  SERVER's `refreshTokenExpiresAt` when present and falls back to `lifetime.seconds` only when the server omits
  it (`Auth.swift:324-329`). The server value wins; the fallback is a local Date estimate that cannot extend a
  refresh token's server-side validity. The access-token expiry is read from the JWT itself (`accessExpiry`,
  :320-322). No way to widen a session beyond server intent.

### S6 — no token/idToken/nonce/PII logging — PASS
- Grep for `print(|os_log|Logger|NSLog|debugPrint|dump(` across the entire touched iOS tree
  (`CleansiaCore/Sources`, `CleansiaCustomer/Sources`) returns ZERO hits. Nonce, the two controllers, the
  spine, and the VM emit no logs.

### Fail-safe / no insecure default — PASS
- Google `.notConfigured` when `clientID`/`serverClientID` empty (`GoogleSignInController.swift:19-21`);
  `AppConfig.infoString` returns `""` for an unsubstituted `$(...)` placeholder (`AppConfig.swift:22-25`), so an
  un-provisioned build degrades to `.notConfigured` (VM shows an error, NEVER calls the spine —
  `testSocialNotConfiguredShowsErrorAndDoesNotCallSpine`). Apple capability-absent stub returns `.notConfigured`
  (`AppleSignInController.swift:83-92`). Neither crashes nor bypasses a check.
- Entitlement is `com.apple.developer.applesignin = [Default]` ONLY (`CleansiaCustomer.entitlements`); no ATT /
  tracking keys added. The Google URL scheme is `$(GID_REVERSED_CLIENT_ID)` resolving to `""` until owner
  provisioning at T-0345 (`Info.plist` diff, `project.yml:GID_* = ""`) — an inert placeholder.

### Owner-gated (live sign-in, not part of this gate)
`GID_CLIENT_ID` / `GID_SERVER_CLIENT_ID` / `GID_REVERSED_CLIENT_ID` Google iOS OAuth client (T-0345);
`Apple:BundleId` backend audience (T-0344) — until set, the backend verifier fails closed. The endpoints ship
safely dark. Mobile spec/client regen remains the owner's manual step.

---

## 2026-06-28 — T-0347 money-safety verification (Gate-SEC HIGH double-capture; sprint-12 §7.16) — PASS

Branch `phase/ios-phase7`, uncommitted. Scope OWNED here: the money-safety property — **exactly one
capturable Stripe charge surface per card order, per channel**. Reviewer covers conventions/web-non-regression
in parallel. Tests: `dotnet test src/Cleansia.Tests --filter "OrderPaymentDispatcher|CreateOrder|OrderChannel"`
→ **31/31 PASS** (incl. the new mobile-null / web-session / factory-never-invoked / DI-wiring cases).

**VERDICT: PASS.** The double-capture surface the §7.16 gate flagged is closed. Can a single card order now
present two capturable surfaces? **NO for mobile, NO for web** — see the per-point trace.

### Property trace (file:line)
1. **Mobile card ⇒ EXACTLY the PaymentIntent — PASS.** `OrderPaymentDispatcher.DispatchAsync`
   `OrderPaymentDispatcher.cs:36-39`: on `channelProvider.Channel == OrderChannel.Mobile` the Card branch
   returns `OrderPaymentDispatchResult.Ok(null)` **before** the `try` — it never calls
   `stripeClientFactory.CreateClient()` (line 43) and never mints a Checkout Session. The order's only
   capturable surface is the PaymentSheet PaymentIntent minted by `CreatePaymentIntent.cs:107`. Asserted by
   `OrderPaymentDispatcherTests.MobileCard_DoesNotCreateStripeSession_*` and `MobileCard_NeverInvokesStripeClientFactory`.
2. **Web card ⇒ EXACTLY the Checkout Session (byte-non-regressing) — PASS.** The Web channel falls through
   to the original `try` (`OrderPaymentDispatcher.cs:41-45`), identical to HEAD (diff is purely the additive
   Mobile early-return + the injected `IOrderChannelProvider` ctor param). Web `CreateOrder` does not call
   `CreatePaymentIntent` (the dispatcher is the only Session/Intent mint in the CreateOrder path;
   `CreateOrder.cs:275`). Asserted by `WebCard_CreatesStripeSession_*` and
   `CreateOrderHandlerCharacterizationTests.WebChannel_CardPath_StillCreatesCheckoutSession`.
3. **Channel is HOST-DERIVED, not client-supplied — PASS.** `CreateOrder.Command` has **no** `Channel` field
   (`CreateOrder.cs:178-196`). The value comes only from the per-host DI registration of `IOrderChannelProvider`
   (`Web.Customer/Extensions/ServiceExtensions.cs:24` = Web; `Web.Mobile.Customer/Extensions/ServiceExtensions.cs:25`
   = Mobile). No request/route/query/JWT path reaches it — a client cannot spoof the channel to force or skip
   a surface.
4. **Safe default + override wiring — PASS.** Shared `Cleansia.Config` uses
   `TryAddSingleton<IOrderChannelProvider>(Web)` (`Cleansia.Config/Services/ServiceExtensions.cs:118`): a host
   that forgets to register gets **Web = Checkout Session = the pre-existing behavior**, never a state where both
   surfaces fire. The override is robust regardless of registration order: each host's explicit `AddSingleton(...)`
   runs in `AddServices` **before** `.AddCoreBindings → Config.AddServices`, so the later `TryAddSingleton`
   is a no-op (Try* never overwrites an existing registration) and exactly one registration exists. Verified by
   `OrderChannelWiringTests` (shared default = Web; mobile override resolves Mobile; web resolves Web). NB: the
   wiring-test comment says "last registration … resolves"; the actual mechanism is Try*-no-op, but the resolved
   value asserted is correct either way.
5. **Webhook still confirms the mobile PaymentIntent — PASS.** `HandlePaymentNotification.cs:219` routes
   `payment_intent.succeeded` into `HandleCompletedSession` → `PaymentStatus.Paid` + `OrderStatus.Confirmed`
   (lines 251-252); `ExtractOrderId` reads OrderId off both Session and PaymentIntent (lines 95-107). Unaffected
   by T-0347 — the single mobile surface still drives the order paid.
6. **No idempotency weakening, no secret logging — PASS.** The ProcessedStripeEvents unique-index idempotency
   gate (`HandlePaymentNotification.cs:148-163`) and the Paid/Refunded terminal-state skips
   (`HandleCompletedSession` 245-249, `HandleExpiredSession` 292) are untouched. The dispatcher logs only the
   `StripeException` message (`OrderPaymentDispatcher.cs:53`), no secrets; `CreatePaymentIntent` returns
   `ClientSecret`/`EphemeralKey` in the response body but never logs them. No new endpoint/attack surface
   (no controller/DTO change → no NSwag/mobile-spec regen; no EF migration — S9 clean).

### Residual finding (LATENT, out of T-0347 scope — file as a follow-up, NOT a blocker)
- **Mobile-paid card orders are not refundable via any code path.** The only refund implementation is
  `RefundCheckoutSessionAsync(order.StripeSessionId, …)` (`RefundService.cs:113`, resolves session→PaymentIntent
  in `StripeClient.cs:75-101`); both `RefundService.cs:44-48` and `AdminRefundOrder.cs:79-86` short-circuit to
  `RefundOrderNotRefundable` when `order.StripeSessionId` is empty. Post-T-0347 a mobile card order has an empty
  `StripeSessionId` (paid on `StripePaymentIntentId`), so admin refund / dispute refund of a mobile order fails.
  There is **no** `RefundPaymentIntent` path. This is **latent, not a live customer breach and not a
  double-capture**: (a) refund endpoints are Admin-host-only (`Web.Admin` `AdminOrderController` /
  `AdminRefundController`) — not exposed to the customer mobile/web hosts; (b) it is a refund-COVERAGE gap, the
  opposite of the double-CAPTURE defect this ticket closes; (c) it was effectively broken pre-T-0347 too — the
  old double-surface set a `StripeSessionId`, but that session was never charged (mobile pays the PaymentIntent),
  so `RefundCheckoutSessionAsync` would have thrown `"…has no PaymentIntent — likely unpaid"` (`StripeClient.cs:85`).
  T-0347 changes the failure mode (guard reject vs. throw), not the outcome. **Recommend a follow-up ticket:**
  "Add a PaymentIntent refund path for mobile-paid (PaymentSheet) card orders" — required before mobile card
  goes live with real refunds, but does not gate the T-0347 double-capture fix.
