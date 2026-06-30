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

---

## 2026-06-29 — T-0313 Slice D Gate-SEC verification (cash submit + T-0332 dual-use Bearer carve-out) — VERDICT: PASS

Scope: the UNCOMMITTED Slice D on `phase/ios-phase7` (`git diff`, not yet committed) — the Core
`HeaderAdapter`/`AnonymousAllowList` dual-use carve-out (the T-0332 resolution) + the ConfirmStep cash submit
+ the double-submit guard. Verified against §7.16 Gate-SEC ACs, the T-0332 ruling (sprint-12 §7.16 Decision 2),
and S1–S10. Gate-DP/parity is the parallel reviewer's; this is the payment/auth-security gate only.

### The T-0332 dual-use Bearer carve-out — PASS (4/4)
- **Core change** (the load-bearing files):
  `CleansiaCore/Sources/CleansiaCore/Auth/AnonymousAllowList.swift:5-20,49-59` adds `dualUsePaths` +
  `isDualUse(path:)`; customer dual-use = exactly the 3 booking endpoints `/api/order/quote`,
  `/api/order/createorder`, `/api/payment/createorder` (lines 49-53); partner dual-use = `[]` (default,
  `partner` ctor line 55). `HeaderAdapter.swift:34-40` `shouldAttachBearer` = `isDualUse(path) || !isAnonymous(path)`,
  attached only `if let accessToken, !accessToken.isEmpty` (line 29).
- **AC1 dual-use Bearer IFF token exists — PASS.** Signed-in dual-use → Bearer; tokenless guest → no Bearer
  (the genuine guest booking path survives at the transport layer). `HeaderAdapter.swift:29,34-40`. Tests:
  `AuthSpineTests.testSignedInDualUseBookingPathsCarryBearer` / `testGuestDualUseBookingPathsStayTokenless`.
- **AC2 pure-anon NEVER carries the Bearer even signed-in — PASS (critical regression check).**
  `dualUsePaths` is EXACTLY the 3 booking endpoints; no pure-anon path (login/register/confirmuseremail/
  forgotpassword/googleauth/appleauth/`*GetOverview`/`Order/Lookup`/`Referral/Validate`) is a `contains`-substring
  of any dual-use entry, so `isDualUse` is false for every pure-anon path and `shouldAttachBearer` falls to
  `!isAnonymous`=false. Exhaustive substring sweep over the full generated customer endpoint inventory confirms
  no collision (notably `/api/payment/createpaymentintent` does NOT contain `/api/payment/createorder`). Tests:
  `AuthSpineTests.testSignedInPureAnonPathsStayTokenless` / `testSignedInGuestReadPathsStayTokenless`;
  `AnonymousAllowListTests.testPureAnonPathsAreNotDualUse`.
- **AC3 CreatePaymentIntent always authed — PASS.** `/api/Payment/CreatePaymentIntent` is in NO anon set and NOT
  dual-use → `shouldAttachBearer`=true always (a guest cannot complete an in-app card booking). Tests:
  `AnonymousAllowListTests.testPaymentCreateIntentIsNeverAnonymousOrDualUse`;
  `AuthSpineTests.testCreatePaymentIntentAlwaysCarriesBearer`.
- **AC4 partner non-regression — PASS.** Partner uses `.partner` (empty dual-use, `PartnerClients.swift:22`);
  `shouldAttachBearer` reduces to the prior `!isAnonymous` — byte-equivalent behavior. The `customerGuestBooking`
  entries STAY (additive; `AnonymousAllowList.swift:36-47`), not deleted (T-0332 out-of-scope honored).
  `testPartnerHasNoDualUsePaths` + `testPartnerBookingPathsAreNotAnonymousSoTheyCarryBearer`; full partner suite
  **366 green** on iPhone 17. X-Device-Id/X-Time-Zone still stamp unconditionally (`HeaderAdapter.swift:25-27`).

### Price / submit ACs (§7.16 Decision 3 + the Gate-SEC iOS ACs) — PASS
- **Server-authoritative price echo — PASS. Can a client cause a lower charge? NO.**
  `BookingOrderCommandFactory.swift:45` sends `totalPrice: resolved.quote.totalPrice` — the RAW quote totalPrice
  echoed VERBATIM, never a discounted/`finalTotal` value. `BookingCodeStates.swift:43-45` maps it straight from
  `QuoteOrderResponse.totalPrice`. The display-only discount math (`ConfirmStep.finalTotal`,
  `BookingSheetView.totalDisplay`) never feeds the command. Same `cleaningDate` to Quote and Create
  (`BookingViewModel+Submit.swift:18,49` + `BookingViewModel.swift:306` both read `selectedInstant`); a mismatch
  is rejected server-side by `PriceMatchesAsync` → `TotalPriceNotMatch` (no silent re-price). Tests:
  `testPriceEchoesQuotedRawTotalVerbatim` (1899 RAW echoed despite 201 tier discount),
  `testSameCleaningDateSentToQuoteAndCreate`.
- **Double-submit / double-order — PASS.** `BookingViewModel+Submit.swift:6-8`
  `guard !submitState.isSubmitting else { return .failed }` then `submitState = .submitting`, all synchronous on
  the `@MainActor` before the first `await` (line 12) — atomic single-in-flight guard; CreateOrder has no server
  idempotency so this is the sole guard. Test: `testDoubleSubmitYieldsSingleCreateCall` (2 concurrent → 1 create,
  1 success + 1 failed).
- **Cash path / no leaked secret — PASS.** Cash → `.success(orderId, confirmationCode)`; card branch is a Slice-E
  placeholder → `.cardPending(orderId, confirmationCode)` with NO secret. The slice DELETES `PaymentSheetParams`
  (clientSecret/ephemeralKey/customerId) from `BookingSubmitOutcome` (diff) — no Stripe/client_secret in Slice D.
  `CreatedOrder` carries only id+confirmationCode (`OrderCreateClient.swift:5-8`). grep for
  stripe/client_secret/payment_intent across the booking tree: NONE.
- **S6 no PII/token/secret logging — PASS.** grep `print|os_log|Logger|NSLog|debugPrint|dump` across all changed
  AND new slice files: ZERO matches. Only token persistence is `CustomerBookingTokenStore.shared` =
  `KeychainTokenStore("cz.cleansia.customer.tokens")`, the SAME service as `CustomerClients.swift:15` (single
  canonical token source; no second store, no disk write of secrets).
- **Order binds to the right user / no guest leak — PASS.** In-app `submit()` requires a token
  (`BookingViewModel+Submit.swift:10` → `.failed` for a guest; `testGuestWithNoTokenFailsWithoutCreatingOrder`,
  create callCount 0); a signed-in CreateOrder carries the Bearer (dual-use) so the server binds `order.UserId`
  from the JWT (S1). Contact name/email/phone come from the caller's OWN self-scoped `userGetCurrentUser`
  (`ProfileClient.swift:27`), display/contact data only — not an identity claim; cannot create under another user.

### Explicit verdicts
- (a) Does any pure-anon path now wrongly carry the Bearer? **NO.** dualUsePaths is exactly the 3 booking
  endpoints; no pure-anon path collides by substring; verified by code + the negative-control tests.
- (b) Can a client lower the charge? **NO.** RAW quote totalPrice echoed verbatim; server `PriceMatchesAsync`
  re-validates; the charge reads `order.TotalPrice` server-side.
- (c) Does the carve-out preserve the guest path + partner non-regression? **YES.** Guest tokenless path survives
  at the transport layer; `customerGuestBooking` intact; partner empty-dual-use → byte-unchanged, 366 green.

### Residual (NON-blocking, NOT a security finding — cross-gate note for the parallel Gate-DP reviewer)
- **Cash-order success copy says "Booking confirmed!"** (`Localizable.xcstrings` `booking_success_title` =
  "Booking confirmed!", `booking_success_subtitle` = "Your cleaning is booked. Sit back and relax."). The §7.16
  Decision 4 architect ruling (sprint-12:3557) corrects CLAUDE.md: a one-off cash order is NOT auto-confirmed at
  create — it stays Pending+New until a cleaner takes it; "iOS must not show 'Confirmed' for a fresh cash booking."
  This is a COPY/UX accuracy nit (the task brief's "cash creates a Confirmed order" premise is itself contradicted
  by the ruling), NOT a security leak — the success OUTCOME carries no secret (only the confirmationCode, the
  by-design shared-secret lookup token). Owned by Gate-DP/parity; flagged here for awareness, not a Gate-SEC block.

### Tests run on iPhone 17 (sim already booted; no erase needed)
- CleansiaCore (swift/xcodebuild, iOS sim): AnonymousAllowListTests 12 + HeaderAdapterTests 11 = **23 PASS** —
  the dual-use Bearer matrix (signed-in→Bearer, guest→none, pure-anon→never-with-token, CreatePaymentIntent→always).
- CleansiaCustomer BookingSubmitTests: **20 PASS** — price-echo-raw-verbatim, same-cleaningDate, double-submit→one,
  guest-no-order, card-no-Stripe.
- CleansiaPartner full suite: **366 PASS** — non-regression confirmed.

**VERDICT: PASS.** Slice D is exploitable-as-written: NONE found. The dual-use carve-out is correct and minimal;
no pure-anon path leaks the Bearer; no client-lowerable charge; guest path + partner behavior preserved. The one
residual ("Booking confirmed!" cash copy) is a Gate-DP parity nit, not a security gate failure.

---

## 2026-06-29 — T-0313 Slice E (customer card branch + Stripe PaymentSheet) — Gate-SEC verification — PASS

Verified the UNCOMMITTED Slice E on `phase/ios-phase7` (working-tree diff + 6 untracked files) against the
§7.16 Gate-SEC ACs and S1–S10. Read the actual code; ran the security tests on iPhone 17 (sim already booted).
I OWN the payment-completion security gate; Gate-DP/parity covered in parallel by the reviewer. T-0347
(single-charge-surface) is already on HEAD — re-confirmed Slice E adds no second surface.

New/changed files in scope:
- `CleansiaCustomer/Sources/Features/Booking/Payment/StripePaymentController.swift` (new) — sole Stripe importer
- `CleansiaCustomer/Sources/Features/Booking/Payment/PaymentSheetPresenting.swift` (new) — protocol + redacted DTO
- `CleansiaCustomer/Sources/Features/Booking/Payment/BookingCardResultResolver.swift` (new) — pure result→nav mapper
- `CleansiaCustomer/Sources/Features/Booking/Submit/PaymentIntentClient.swift` (new) — CreatePaymentIntent client
- `CleansiaCustomer/Sources/StripeConfig.swift` (new) — fail-closed publishable-key gate
- `BookingViewModel{,+Submit}.swift`, `BookingSubmitOutcome.swift`, `BookingSheetView.swift`,
  `CustomerShellView.swift`, `CleansiaCustomerApp.swift`, `project.yml`, `Info.plist`, `Package.resolved`,
  `Localizable.xcstrings` (modified)

### Per-AC verdict (file:line)
- **AC1 — `.completed` is UX-only; webhook is sole paid authority — PASS.** `StripePaymentController.present`
  (`StripePaymentController.swift:37-47`) returns ONLY the typed `PaymentSheetOutcome`; `.completed` maps via
  `BookingCardResultResolver.resolve` (`BookingCardResultResolver.swift:11-12`) to `.navigateToSuccess(confirmationCode:)`
  — pure navigation. `BookingSheetView.presentPaymentSheet` (`BookingSheetView.swift:70-76`) only sets local
  `successCode`; no order-mutation/confirm/markpaid API anywhere in the completion path (grep across Payment/ +
  Submit + SheetView: only `confirmationCode`/UI-`onConfirm`). The intent response DTO carries NO `status`/`paid`
  field (`CreatePaymentIntentResponse.swift:13-19`) and the app's `PaymentIntentDetails`
  (`PaymentIntentClient.swift:5-9`) drops even `paymentIntentId` — the client physically cannot infer paid state.
  Success screen shows confirmation code + status-accurate copy: `booking_success_title`="Booking received",
  `booking_success_subtitle`="…we'll confirm it shortly" (HEAD baseline; NOT "Confirmed"/"Paid"). This RESOLVES
  the Slice-D residual ("Booking confirmed!" cash copy) — the copy was corrected on HEAD before this slice.
- **AC2 — no secret logging/persistence (S6) — PASS.** grep `print|os_log|Logger|NSLog|debugPrint` across ALL
  payment files: ZERO. `PaymentSheetPresentation` is `CustomStringConvertible`+`CustomDebugStringConvertible`
  with `description`/`debugDescription` = `"PaymentSheetPresentation(merchant: …, secrets: <redacted>)"`
  (`PaymentSheetPresenting.swift:9-15`) — a logged/printed value cannot leak the secret (test-proven, below).
  No Keychain/UserDefaults/`.set` in any payment file — secrets are in-memory only.
- **AC3 — client_secret straight to the sheet — PASS.** Arrives only over the authed HTTPS
  `paymentCreatePaymentIntent` response (`PaymentIntentClient.swift:17-21`), flows
  intent→`PaymentSheetPresentation`→`PaymentSheet(paymentIntentClientSecret:)` (`StripePaymentController.swift:31-34`);
  not re-used, not stored.
- **AC4 — fail-closed on empty/`$(...)` key — PASS.** `StripeConfig.publishableKey` strips the `$(` placeholder to ""
  (`StripeConfig.swift:4-7`); `isCardPaymentAvailable` is false when unconfigured (`:9-16`). `StripeLaunch.applyPublishableKey`
  early-returns and does NOT set `STPAPIClient.shared.publishableKey` when unavailable (`StripePaymentController.swift:9-12`).
  Two-gate defense: card option hidden in UI (`ConfirmStep.swift:173`) AND the submit card-branch requires
  `… , isCardPaymentAvailable` (`BookingViewModel+Submit.swift:42`) so `createPaymentIntent`/`.cardPending` is
  unreachable with an unconfigured key → sheet never presented, cash path fully works.
- **AC5 — no Stripe SECRET key — PASS.** grep `sk_live|sk_test|secretKey|STRIPE_SECRET`: NONE. Only the publishable
  slot ships: `STRIPE_PUBLISHABLE_KEY: $(STRIPE_PUBLISHABLE_KEY)` (Info.plist) default-empty in project.yml with an
  explicit "pk_ only — NEVER a secret key" comment.
- **AC6 — sole-importer invariant — PASS.** `import StripeCore`/`import StripePaymentSheet` appears ONLY in
  `StripePaymentController.swift:4-5` (grep across all Sources/Tests). The "PaymentSheet(" hits in
  `BookingSheetView.swift` are the local `PaymentSheetPresenting` protocol / `PaymentSheetPresentation` struct, not
  the SDK. No Stripe in CleansiaCore / CleansiaPartner / Packages. `project.yml` adds the `StripePaymentSheet`
  product as a dependency of the CleansiaCustomer target ONLY — customer-only dep.
- **AC7 — `.canceled`/`.failed` leave the order Pending — PASS.** Resolver maps both to a `.snackbar(...)`
  (`BookingCardResultResolver.swift:13-16`); the VM performs NO order-cancel/mutation — backend
  `CleanupStalePendingOrders` sweeps the stale Pending. No client-driven order-state change.
- **AC8 — single charge surface (with T-0347) — PASS.** The order card path uses CreateOrder + CreatePaymentIntent +
  PaymentSheet only. NO Checkout-session call in Sources (grep `checkout`: none). `membershipCreateCheckoutSession`
  exists only on the separate `CustomerMembershipAPI` (subscription flow) and is NOT called by the booking VM/views.
  Legacy `CreateOrderResponse.stripeSessionId` is NOT surfaced — `CreatedOrder` is `{id, confirmationCode}` only
  (`OrderCreateClient.swift:5-8,19-24`). No second surface introduced.

### Explicit verdicts demanded by the brief
- (a) Can a client appear-paid without a real capture? **NO.** `.completed` is navigation-only; no order-mutation API
  in the completion path; the intent DTO carries no paid/status field; the webhook (HandlePaymentNotification) is the
  sole paid-status authority. Success copy is "Booking received / we'll confirm it shortly", not "Confirmed".
- (b) Can the client_secret/ephemeralKey/stripeCustomerId leak via log/persist? **NO.** No print/os_log/Logger/NSLog/
  debugPrint in any payment file; the presentation's description/debugDescription redact ("secrets: <redacted>",
  test-proven); no Keychain/UserDefaults/disk write — in-memory only, handed straight to the sheet.
- (c) Is the card path fail-closed when unconfigured + single-surface? **YES.** Empty/`$(...)` key ⇒ STPAPIClient not
  initialized, card option hidden, submit card-branch unreachable, sheet never presented, cash unaffected; exactly one
  charge surface (PaymentIntent/PaymentSheet), no Checkout session client-side.

### Tests run on iPhone 17 (sim id 04753F32… already booted; xcodegen-generated proj; Stripe 25.17.0 resolved)
`xcodebuild test -workspace Cleansia.xcworkspace -scheme CleansiaCustomer` — **16/16 PASS, 0 failures**:
- `.completed`-doesn't-flip-state: `BookingCardResultTests.testCompletedNavigatesToSuccessWithoutFlippingOrderState`,
  `testNonCompletedOutcomesNeverNavigateToSuccess` — PASS
- fail-closed (empty key → no sheet): `BookingCardSubmitTests.testFailClosedSubmitNeverReachesPaymentSheet`,
  `testCardUnavailableNeverCallsPaymentIntent`, `testDefaultCardAvailabilityIsFailClosedUnderEmptyKey`,
  `StripeConfigTests.testEmptyKeyIsUnconfigured` / `testPlaceholderKeyIsTreatedAsUnconfigured` — PASS
- secret-redaction: `PaymentSheetSecretRedactionTests.testPresentationDescriptionNeverLeaksSecrets` — PASS
- canceled/failed-leave-Pending: `BookingCardResultTests.testCanceledShowsSnackbarAndLeavesOrderPending`,
  `testFailedShowsSnackbarAndLeavesOrderPending` — PASS
- supporting: `testCardSubmitCreatesOrderThenPaymentIntent`, `testEmptyClientSecretFails`,
  `testPaymentIntentFailureLeavesCardPendingUnreached`, `testCashSubmitNeverCallsPaymentIntent`,
  `testIsCardPaymentAvailableReflectsInjectedFlag`, `testPublishableKeyIsConfigured` — PASS

### Residual (NON-blocking)
- `CreateOrderResponse.stripeSessionId` (generated DTO, `CreateOrderResponse.swift:17`) is a dead legacy field from the
  Checkout-session era. NOT a live leak — the iOS `CreatedOrder` never reads it. Cleanup nit for the backend DTO
  (drop it next NSwag regen once no client references it, per S9), not a Gate-SEC block.

**VERDICT: PASS.** Slice E is exploitable-as-written: NONE found. Webhook remains the sole paid-status authority;
`.completed` is UX-only; no secret logged/persisted; the card path is fail-closed when unconfigured and uses exactly
one charge surface. This completes the T-0313 payment security gate.

---

## 2026-06-30 — T-0314 Slice C (Membership/Plus + Recurring + ConfirmRecurring) — Gate-SEC §7.17 R5–R9

Branch `phase/ios-phase8`, UNCOMMITTED working tree. Scope OWNED: membership/recurring money-path
security gate (extends the Core Stripe seam). Reviewer covers Gate-DP/parity in parallel.

### Seam extension (PaymentSheetPresenting / StripePaymentController + PaymentIntentKind)
- **Single importer — PASS.** `grep "import StripePaymentSheet"` → exactly one hit:
  `StripePaymentController.swift:5`. `StripeCore` (`:4`) is also confined to this file (pre-existing,
  only sets the publishable key). No second SDK importer added.
- **Secret redaction — PASS.** `PaymentSheetPresentation.description` (`PaymentSheetPresenting.swift:29-31`)
  renders `"…secrets: <redacted>"`; `debugDescription` (`:33-35`) delegates to it. The new `intentKind`
  is logged (non-secret); `clientSecret`/`ephemeralKey`/`stripeCustomerId` never appear. Test:
  `MembershipViewModelTests.testSecretsNeverAppearInSetupIntentPresentationDescription` — PASS.
- **Setup-intent path adds no log/persist — PASS.** The `.setup` branch (`StripePaymentController.swift:37-41`)
  only constructs `PaymentSheet(setupIntentClientSecret:)`. No print/os_log/Logger/NSLog/debugPrint and no
  UserDefaults/Keychain/file write in any Membership/Recurring/Payment source file (grep, 0 hits).

### Membership two-phase (R5–R9), file:line
- **R5/R9 webhook-authority — PASS.** Authoritative published state `MembershipRepository.current`
  (drives `hasMembership`) is mutated ONLY by `refresh()` → `client.getMine()` → `membershipGetMine`
  (`MembershipRepository.swift:22-31`). `confirmSubscribe` (`MembershipViewModel.swift:81-101`) calls
  `repository.refresh()` on success (`:95`) and NEVER writes `current`/`hasMembership` from the
  subscribe/SDK result. The `.subscribed(membershipId:)` return is a navigation signal off the server
  `membershipId`, not a client-set active flag. `.canceled`/`.failed` → no mutation
  (`SubscribePlusScreen.swift:99-109`; failure branch `MembershipViewModel.swift:97-99`). Tests:
  `testConfirmSubscribeRereadsMembershipAfterCompleted` (asserts `mineCallCount==1`),
  `testConfirmSubscribeFailureReturnsFailedAndNoMutation` (`repo.current` stays nil) — PASS.
- **R6 no-secret-logging — PASS.** grep print/os_log/Logger/NSLog/debugPrint/UserDefaults/FileManager
  across Membership+Recurring+Payment sources → 0 hits.
- **R7 fail-closed — PASS.** `canSubscribe == isCardPaymentAvailable` (`MembershipViewModel.swift:35-37`).
  `startSubscribe` first line `guard canSubscribe else { return .failed }` (`:51`) returns BEFORE
  `repository.subscribePhase1` — no SetupIntent requested. CTA bar renders only `if vm.canSubscribe`
  (`SubscribePlusScreen.swift:47`) — hidden when key empty. `StripeConfig.isConfigured` treats empty
  and `$(...)` placeholder as unconfigured (`StripeConfig.swift:13-16`). Tests:
  `testFailClosedStartSubscribeUnreachableUnderEmptyKey` (asserts `subscribeCalls.isEmpty`),
  `testCtaHiddenWhenCardUnavailable` — PASS.
- **R8 idempotency replay — PASS.** ONE token minted at Phase-1 (`MembershipViewModel.swift:56-57`),
  REPLAYED at Phase-2 (`:86-87` reuses `subscribeIdempotencyToken`). A fresh `startSubscribe` mints a new
  token (`:56`). Tests: `testConfirmSubscribeReplaysTheSameIdempotencyTokenAcrossBothPhases` (Phase1.token
  == Phase2.token, confirmed=false/true), `testFreshSubscribeAttemptMintsANewToken` (tokens differ) — PASS.
- **R9 own-only — PASS.** `getMine`/`cancel` parameterless; `subscribe` sends planCode+confirmed+token only;
  `swapPlan` sends newPlanCode only (`MembershipManagementClient.swift:5-12,29-58`). No client userId.
  In-app cancel reachable: `MembershipManagementCard.ActiveCard` Cancel button (`:188-195`) → `vm.cancel()`,
  card mounted on HomeTab.

### ConfirmRecurring path, file:line
- **Two-phase branch — PASS.** `OrderDetailViewModel.confirmRecurring` (`:215-241`): cash response
  (nil/empty `clientSecret`, `RecurringConfirmation.needsPayment==false`, `OrderClient.swift:18-19`) ⇒
  snackbar + `repository.refresh()` + `fetch(initial:false)` — re-read, never client-infer paid. Card
  response (non-empty secret) ⇒ emits `PaymentSheetPresentation(intentKind: .payment)` for the view.
- **`.completed` never flips order state — PASS.** `notifyRecurringPaymentResult(.completed)` (`:243-253`)
  ONLY does `repository.refresh()` + `fetch(initial:false)` — re-reads the order; no client status mutation.
  `.canceled` → no-op; `.failed` → snackbar only. View wiring `OrderDetailView.swift:47-52` presents the
  sheet then calls `notifyRecurringPaymentResult`. Test: `testRecurringCardPaymentCompletedRefetches`
  (asserts detail re-fetch) — PASS.
- **CREATE does not charge a client-trusted price — PASS.** `CreateRecurringInput`
  (`RecurringModels.swift:26-37`) and `CreateRecurringBookingCommand` (`RecurringBookingClient.swift:21-33`)
  carry frequency/rooms/services/packages/paymentType — NO price/amount field. Template is server-priced;
  the generated order confirms (and prices) later.

### Explicit verdicts demanded by the brief
- (a) Can a client appear-subscribed/paid without a real capture? **NO.** Membership active-state and order
  paid-state are mutated only by server re-reads (`membershipGetMine` / order GET); the SDK `.completed` is
  UX-only; subscribe/confirm DTOs set no client-trusted paid flag. Webhook remains the sole authority.
- (b) Can secrets leak via log/persist? **NO.** No logging/persistence anywhere in the money-path files;
  description/debugDescription redact (test-proven); secrets are in-memory, handed straight to the sheet.
- (c) Is the seam extension still single-importer + fail-closed? **YES.** Exactly one StripePaymentSheet
  importer (`StripePaymentController.swift`); empty/placeholder key ⇒ CTA hidden AND subscribe branch
  unreachable (no SetupIntent requested), sheet never presented.

### Tests on iPhone 17 (sim 04753F32… booted; workspace Cleansia, Stripe 25.17.0)
`xcodebuild test -scheme CleansiaCustomer -destination 'platform=iOS Simulator,name=iPhone 17'`
restricted to MembershipViewModelTests + CreateRecurringViewModelTests + RecurringBookingsViewModelTests
+ OrderDetailViewModelTests — **50/50 PASS, 0 failures.** Covers two-phase, idempotency-replay,
fail-closed, webhook-re-read, secret-redaction, ConfirmRecurring cash/card/completed.

### Residual
- NON-blocking nit: `OrderRecurringConfirm.needsConfirmation` (`OrderDetailView.swift:165-170`) hard-codes
  `paymentStatus?.value == 1` (Pending). This is a UX visibility gate for the confirm button, NOT a
  security boundary — the real confirm authority is server-side `orderConfirmRecurring`, which re-validates
  ownership and state. No security impact.

**VERDICT: PASS.** T-0314 Slice C is exploitable-as-written: NONE found. Webhook/server re-read is the sole
authority for membership-active and order-paid state; `.completed` (SetupIntent and PaymentIntent) is UX-only;
one idempotency token spans both subscribe phases; the card/subscribe path is fail-closed when unconfigured;
secrets are redacted and never logged/persisted; the Stripe seam stays single-importer. R5–R9 all PASS.

---

## T-0314 Slice D — Customer Disputes + multipart evidence upload (Gate-SEC R10–R12 + T-0308 photo-privacy) — 2026-06-30

Scope: the customer's FIRST capture surface (camera/library/PDF) feeding a multipart evidence upload.
Reviewer owns Gate-DP/parity in parallel; THIS note owns the file-upload + capture-surface security gate.
Verified against UNCOMMITTED working tree on `phase/ios-phase8`. No code edited.

### R10 own-dispute scoping — PASS
- SERVER-enforced (DisputeNotOwnedByUser); the client adds NO ownership check that could be bypassed.
  `DisputeClient.getById` sends only `disputeId` (`DisputeClient.swift:21-31`); list/create/addMessage/upload
  carry no client userId. `DisputeRepository` does NOT cache details — `getById` always hits the network
  (`DisputeRepository.swift:64-66`), so no stale cross-user detail can be served.
- Cache wipe on sign-out / forced-401: `DisputeRepository: SessionScopedCache` (`DisputeRepository.swift:13`,
  `clear()` :80-84) is REGISTERED (`CustomerAppContainer.swift:125`). `clearAll()` runs on logout
  (`Auth.swift:218`) and on forced-401/failed-refresh (`SessionRefresher.forceSignOut:75-79` → :76 clears
  caches BEFORE token clear). Registry holds weak refs and awaits each `clear()` (`SessionScopedCache.swift:19-31`).
- Bearer rides every dispute call (ADR-0019 spine): the generated builder sets `requiresAuthentication:false`
  (`CustomerDisputeAPI.swift:233`) but auth is attached at the URLSession layer by `HeaderAdapter`
  (`HeaderAdapter.swift:29-30`) for any path NOT on the anon allow-list. `/api/Dispute/*` is absent from
  `AnonymousAllowList.customer` (`AnonymousAllowList.swift:42-63`); customer wires `HeaderAdapter(anonymousAllowList: .customer)`
  (`CustomerClients.swift:17-19`). So all dispute reads/writes carry the caller's Bearer.

### R11 client file validation FAIL-CLOSED, mirroring the backend — PASS
- `EvidenceFileValidator.validate` (`EvidenceFileValidator.swift:14-22`): size > 10 MiB → `.tooLarge`;
  contentType not in {image/jpeg, image/png, image/webp, application/pdf} (lowercased) → `.unsupportedType`.
  Empty/unknown type fails closed (the set lookup misses). Limit == backend MaxFileSize:
  `maxEvidenceBytes = 10*1024*1024` (`DisputeFormConstants.swift:12-18`).
- Runs on the FINAL bytes BEFORE the temp file is written AND before the network call:
  image path validates AFTER ImageCompressor.encode and BEFORE `write` (`EvidencePreparer.swift:44-50`);
  PDF path validates BEFORE `write` (`:57-61`). `write` is the only place a temp file is created (`:64-77`).
- Cannot be bypassed: the VM's ONLY upload path is `uploadEvidence` → `uploadOne` → `prepare` → EvidencePreparer
  (`DisputeDetailViewModel.swift:75-118`); a rejection returns before any `repository.uploadEvidence` call.
  Tests: EvidenceFileValidatorTests (7/7 incl. empty-type fail-closed, over-by-1, case-insensitive,
  oversize-wins-over-wrong-type); EvidencePreparerTests `testPreparePdfRejectsOversizeBeforeWritingFile`
  (temp dir empty after rejection); DisputeDetailViewModelTests `testUploadEvidenceRejectsOversizePdfBeforeCall`
  (uploadCallCount == 0), `testUploadEvidenceMixedValidAndInvalidUploadsOnlyValid`. ALL PASS.

### R12 no path traversal — PASS
- Blob name is SERVER-controlled (`{disputeId}/{Guid}{ext}`); the client contributes only the extension.
- Temp file is UUID-named with a fixed prefix and a fixed ext: `dispute-evidence-<UUID>.jpg|.pdf`
  (`EvidencePreparer.swift:70`). No user-supplied filename enters the path.
- The multipart `filename` is `fileURL.lastPathComponent` (`URLSessionImplementations.swift:531,542`) = the
  UUID temp name, NOT the picked source's display name. The camera/library picker returns a bare `UIImage`
  (`CameraOrLibraryPicker.swift:69` reads `info[.originalImage]`) — no source URL/name travels; the PDF is
  read as raw `Data` (`DisputeDetailView.swift:193`) — the security-scoped source URL is dropped after read.

### EXIF/GPS strip (T-0308 photo-privacy) — PASS
- Image evidence goes through `ImageCompressor.encode` (`EvidencePreparer.swift:39`), which re-renders into a
  fresh CGBitmapContext (no source metadata) and re-encodes via ImageIO with an empty-but-quality properties
  dict (`ImageCompressor.swift:47-86`) — explicit EXIF/GPS strip by construction. PDFs pass through untouched
  (no EXIF concern). PROVEN: ImageCompressorTests `testEncodedOutputHasNoGpsOrExifMetadata` (GPS-tagged source
  → no GPS dict in output) and EvidencePreparerTests `testPreparedImageHasNoGpsMetadata` (strip survives the
  temp-file write) — both PASS on iPhone 17.

### No secret in multipart / no secret logging / temp cleanup — PASS
- Multipart body carries only `disputeId` + `file` (bytes + UUID filename + mimetype) — no token/secret
  (`CustomerDisputeAPI.swift:215-218`; Bearer is a header, not a body part).
- ZERO logging in the whole Disputes feature and in ImageCompressor (grep for print/os_log/NSLog/Logger/
  debugPrint → none). No token/secret/file-path is logged.
- Temp file is cleaned up on BOTH success and failure: `defer { prepared.cleanUp() }`
  (`DisputeDetailViewModel.swift:99`); `cleanUp()` removes the temp file (`EvidencePreparer.swift:15-17`).
  Tests: `testUploadEvidenceUploadsTempPdfWithCorrectExtensionAndCleansUp` (file gone after upload),
  EvidencePreparerTests temp-cleanup assertions. PASS.

### New capture surface (privacy) — PASS
- `NSCameraUsageDescription` + `NSPhotoLibraryUsageDescription` present in BOTH Info.plist (diff) and
  project.yml (XcodeGen source of truth) — customer's first capture surface.
- `PrivacyInfo.xcprivacy` added and wired into the target sources (`project.yml`); declares
  `NSPrivacyCollectedDataTypePhotosorVideos`, Linked=true, Tracking=false, purpose=AppFunctionality —
  ACCURATE for the actual capture (photo/PDF evidence used only for app functionality; no tracking).
  Matches ADR-0016 AR-PRIV: manifest matches the real capture.
- PDF preview uses the server SAS URL (`DisputeDetailView.openPdf` → `evidence.blobURL`, :142-148) and
  presents `QuickLookPreview(url:, deleteOnDismiss:false)` (:153). `deleteOnDismiss:false` is CORRECT for a
  REMOTE URL — and `QuickLookPreview.removeFile` is `url.isFileURL`-guarded (`QuickLookPreview.swift:72-75`)
  so it could not delete a remote URL anyway. No local PII PDF is left undeleted (the locally-prepared
  upload temp is the only on-disk PII, and it is `defer`-cleaned post-upload). The in-place fullscreen image
  preview likewise points at the remote `blobURL` (:134-139), not a local file.

### Explicit verdicts demanded by the brief
- (a) Can a client upload an oversize / disallowed-type / path-traversing file? **NO.** Fail-closed validation
  runs on the final bytes before any temp write and before the network call; the VM has no bypass path; the
  blob name is server-controlled and the temp/multipart filename is a UUID — no user filename in the path.
- (b) Does image evidence leak EXIF/GPS or any secret? **NO.** Images are re-rendered + re-encoded metadata-free
  (test-proven, strip survives the temp write); the multipart body carries no secret; nothing in the feature
  logs the token/secret/file path; temp files are cleaned on success and failure.
- (c) Is the privacy manifest accurate for the new capture surface? **YES.** PhotosorVideos / Linked / no-Tracking
  / AppFunctionality matches the camera+library+PDF evidence capture; the two usage-description strings are
  present in both Info.plist and project.yml.

### Tests on iPhone 17 (sim 04753F32… booted; workspace Cleansia; project regenerated via xcodegen)
- `CleansiaCustomer`: EvidenceFileValidatorTests + EvidencePreparerTests + DisputeDetailViewModelTests —
  **25/25 PASS, 0 failures** (validator oversize/wrong-type/empty-fail-closed; preparer EXIF-strip +
  validate-before-write + temp-cleanup; upload per-file sequential + cleanup + reject-before-call + mixed).
- `CleansiaCore`: ImageCompressorTests/testEncodedOutputHasNoGpsOrExifMetadata — **PASS** (EXIF/GPS strip proof).

### Residual
- NON-blocking observation: `requiresAuthentication:false` on the generated dispute builders is the NSwag
  default and is harmless here ONLY because the URLSession-level `HeaderAdapter` attaches the Bearer for all
  non-anon paths. This is the established spine for every customer endpoint, not a Slice-D regression. No action.

**VERDICT: PASS.** T-0314 Slice D is exploitable-as-written: NONE found. Own-dispute scoping is server-enforced
with no client bypass and a session-scoped cache that wipes on sign-out/401; client file validation is
fail-closed on the final bytes before write/network and cannot be bypassed; the blob/temp/multipart name is
server-/UUID-controlled (no path traversal, no user filename); image evidence is EXIF/GPS-stripped by
construction (test-proven); no secret travels in the multipart body and nothing in the feature is logged;
temp files are cleaned on success and failure; the privacy manifest + usage strings accurately describe the
new capture surface. R10–R12 + T-0308 all PASS.

---

## T-0314 Slice F — Profile + GDPR delete + Devices + notification-prefs — Gate-SEC verification (2026-06-30)

Branch `phase/ios-phase8`, UNCOMMITTED working tree (untracked `Features/Profile/`, `ProblemDetailsError.swift`,
`L10n+Profile.swift`, `L10n+Devices.swift`, tests; modified shell/container/L10n/xcstrings). Verified against
§7.17 Gate-SEC (R1-R4 GDPR, R13 Devices, R14 prefs) + S1-S10. I own the GDPR-delete + Devices gate; Gate-DP/parity
covered in parallel by the reviewer. Read the actual code; ran the tests on iPhone 17 (sim 04753F32… booted).

### R2 — signOutLocal-not-logout on success — PASS
- `DeleteAccountViewModel.confirmDelete()` `.success:` calls `authClient.signOutLocal()`
  (`DeleteAccountViewModel.swift:27`), NEVER `logout()`. `signOutLocal()` = `tokenStore.clear()` +
  `await sessionScopedCaches.clearAll()` with NO server call, NO preLogout push-unregister, NO Apple revoke
  (`Auth.swift:216-219`). Contrast `logout()` (`Auth.swift:207-214`) which DOES hit `api/Auth/Logout` + preLogout —
  correctly NOT used (the account is already gone server-side; there is no live token to log out with).
- Production wiring passes the real spine: `DeleteAccountView(authClient: container.authClient, …)`
  (`CustomerShellView.swift:264`); `container.authClient` → the `AuthApiClient` spine (`CustomerAppContainer.swift:25-27`,
  `makeAuthSpine: { _ in authStack.spine }` :130). `AuthApiClient` conforms `AuthSpine → AuthClient`, so
  `signOutLocal()` is exactly `Auth.swift:216`.
- Test: `testSuccessCallsSignOutLocalNotLogout` asserts `signOutLocalCount == 1 && logoutCount == 0`
  (`DeleteAccountViewModelTests.swift:22-30`); `FakeAuthClient` counts the two separately
  (`ProfileFakes.swift:17-28`). PASS.

### R1 — branch-on-result + blocked-stays-signed-in + no-resurrect — PASS
- The VM switches `ApiResult`: ONLY HTTP-2xx (no thrown error) reaches `.success`; a BLOCKED 4xx throws
  `ErrorResponse.error`, caught by `apiResult(mapError: ProblemDetailsError.map)` → `.failure`
  (`GdprDeleteClient.swift:24-30`, `SafeApiCall.swift:7-13`). A blocked response CANNOT be mis-mapped to a false
  success — the only success path is a 2xx body.
- Code extraction is correct and unspoofable: backend sets `ProblemDetails.Type = error.Code`
  (`CleansiaApiController.cs:92`); iOS `ProblemDetailsError.map` decodes `body.type → ApiError.code`
  (`ProblemDetailsError.swift:10-12`). The 3 codes match the backend constants exactly:
  `gdpr.deletion_blocked_by_order` / `_by_invoice` / `_already_pending`
  (`GdprDeleteClient.swift:11-17` ↔ `BusinessErrorMessage.cs:317-319`, emitted at `GdprDeletionService.cs:54,59,66`).
- Each blocked path shows the localized error and LEAVES the session intact — `.failure:` only snackbars +
  sets `deleteState = .error`, NO `signOutLocal`, NO route-to-login (`DeleteAccountViewModel.swift:30-34`).
  All 3 blocked codes + a generic 500 are localized in 5 languages (`error_gdpr_deletion_*` keys present in
  en/cs/sk/uk/ru). Tests: `testBlockedByOrderShowsErrorAndStaysSignedIn` / `…ByInvoice…` / `…AlreadyPending…` /
  `testGenericFailureStaysSignedIn` all assert `signOutLocalCount == 0` (and order-test also `logoutCount == 0`,
  `accountDeleted` NOT emitted) (`DeleteAccountViewModelTests.swift:46-92`). A "wipe-on-any-response" stranding
  bug is NOT present. PASS.
- No-resurrect: on success `signOutLocal` wipes token + ALL session caches — `clearAll()` iterates every
  registered cache (`SessionScopedCache.swift:19-31`); `CustomerAppContainer.init` registers all 8 session
  repos (order/loyalty/referral/membership/recurring/dispute/savedAddress/userProfile)
  (`CustomerAppContainer.swift:133-140`). Re-login is a fresh session; no pre-delete identity replayed. PASS.

### R3 — SIWA note shown + Apple revoke owner-deferred — PASS
- `DeleteAccountView` renders `appleNote` via `L10n.DeleteAccount.appleRevokeNote`
  (`DeleteAccountView.swift:100-112`, key `delete_account_apple_note`), localized ×5 (en/cs/sk/uk/ru) with the
  required "remove Cleansia in Settings → Apple ID → Sign in with Apple" guidance (5.1.1(v)).
- NO Apple `/auth/revoke` is attempted anywhere — grep of the whole customer module finds only the localized
  note + its render site; nothing holds an Apple token to revoke. `/auth/revoke` stays owner-deferred (§7.14 D4). PASS.

### R4 — no client-side delete logic / no client flag — PASS
- The VM only calls `client.deleteMyAccount()` → `CustomerGdprAPI.gdprDeleteMyAccount()` (no args, no client id,
  no flag) and reacts to the result (`GdprDeleteClient.swift:25-29`, `DeleteAccountViewModel.swift:22-35`).
  Server (JWT subject) owns the deletion decision. PASS.

### R13 — Devices (T-0310 D6-8 verbatim) — PASS
- The ONE id: `LiveCustomerDevicesClient(deviceIdProvider: authStack.deviceIdProvider)`
  (`CustomerAppContainer.swift:125`) is the SAME `DeviceIdProvider` instance (service `cz.cleansia.customer.device`)
  passed to the `HeaderAdapter` that stamps `X-Device-Id` (`CustomerClients.swift:16-20`; `HeaderAdapter.swift:25,42-44`).
  `client.currentDeviceId` returns `deviceIdProvider.deviceId` (`CustomerDevicesClient.swift:26-28`). NO second
  UUID / `identifierForVendor` source — `DeviceIdProvider.deviceId` is keychain-backed single source
  (`DeviceIdProvider.swift:31-52`). Same id is sent as the `currentDeviceId:` query param to `deviceMine`
  (`CustomerDevicesClient.swift:32`). Test `testLoadSendsTheOneDeviceIdAsCurrentDeviceId` proves the provider id
  is what is sent (`CustomerDevicesViewModelTests.swift:39-45`).
- Current device hides revoke: server flags `isCurrent`; VM `isCurrentDevice` compares
  `device.deviceId == client.currentDeviceId` with `device.isCurrent` fallback (`CustomerDevicesViewModel.swift:52-57`).
- Defensive self-revoke → sign-out: revoking the current row emits `signedOut` (`…ViewModel.swift:41-42`); tests
  `testSelfRevokeEmitsSignedOut` + `testSelfRevokeByIsCurrentFlagWhenDeviceIdMissing`
  (`CustomerDevicesViewModelTests.swift:79-115`).
- Server-scoped own-device-only revoke (no client check): `RevokeDevice.Handler` derives `userId` from
  `IUserSessionProvider` then `GetByIdAndUserAsync(DeviceRowId, userId)` → null ⇒ `DeviceNotFound` (S3 no-leak),
  then deactivate + `RevokeByDeviceAsync` (`RevokeDevice.cs:33-46`); controller `[Permission(Policy.Authenticated)]`
  + `[EnableRateLimiting("auth")]` (`DeviceController.cs:76-88`). `GetMyDevices` scopes `GetByUserIdAsync(userId)`;
  the `CurrentDeviceId` param only flags `IsCurrent` (`GetMyDevices.cs:19-25`). `DeviceDto` leaks no
  UserId/TenantId/token (`DeviceDto.cs:3-9`) — S4 clean. Other-device revoke removes the row and STAYS signed in
  (`testRevokeOtherRemovesRowAndStaysSignedIn`); failure keeps the list + snackbars (`testRevokeFailureKeepsList…`). PASS.

### R14 — Notification preferences own-only — PASS
- `notificationPreferencesGetMine()` / `notificationPreferencesUpdate(command:)` carry NO client-supplied id;
  `toCommand()` is the 11 booleans only (`NotificationPreferences.swift:65-77, 98-114`). Server scopes to the JWT
  subject. Optimistic apply + 300ms debounce + replace-all + rollback-on-failure is sound
  (`NotificationPreferencesViewModel.swift:20-25, 39-58`); tests coalesce/separate/revert all pass. PASS.

### Cross-cutting S-rules on these paths
- S6 (no PII/secret logging): grep of `Features/Profile/`, `ProblemDetailsError.swift`, and `Auth.swift` for
  print/os_log/NSLog/Logger/debugPrint → NONE. `ProblemDetailsError.map` decodes only `type`+`detail` (the error
  code + a server message), no PII/secret. PASS.
- Change-password (Security) flow: `userRequestPasswordChange` (email+language) → `userChangePassword`
  (email+code+newPassword) (`ChangePasswordClient.swift:11-25`); VM gates on `PasswordPolicy.isValid` +
  `passwordsMatch` BEFORE the network call (`SecurityViewModel.swift:43-50`); no secret is logged; the new
  password rides the HTTPS body, not a query/log. Reset-by-emailed-code is the established public-ish flow. PASS.
- S1/S2/S3/S4/S5 on the backend device endpoints: all satisfied (see R13). S7/S8/S9/S10 N/A to this iOS slice
  (no new side-effecting command, entity, migration, or soft-delete query introduced client-side; the GDPR
  deletion idempotency/blocking is enforced server-side in `GdprDeletionService`, out of Slice-F scope).

### Tests on iPhone 17 (sim 04753F32-7518-4747-8A6E-D030CAD7A42E booted; workspace Cleansia)
`xcodebuild test -scheme CleansiaCustomer` on DeleteAccountViewModelTests + CustomerDevicesViewModelTests +
NotificationPreferencesViewModelTests + SecurityViewModelTests + ProfileViewModelTests:
**34/34 PASS, 0 failures.** Confirms success→signOutLocal-not-logout; the 3 blocked + generic→stay-signed-in;
the-ONE-id sent; self-revoke→sign-out (both deviceId and isCurrent-fallback); hide-on-current; prefs
optimistic+debounce+rollback; password-policy gate.

### Explicit GDPR verdict demanded by the brief
- (a) Does success do a COMPLETE local wipe via `signOutLocal` (not `logout`)? **YES** — token cleared + all 8
  session caches cleared; no server logout; test-proven `signOutLocalCount==1, logoutCount==0`.
- (b) Does a BLOCKED delete leave the user signed in (never strand them)? **YES** — only a 2xx reaches success;
  all 3 blocked codes + generic failure hit `.failure`, which never wipes/redirects; test-proven `signOutLocalCount==0`.
- (c) Is the SIWA-revoke correctly deferred + the note shown? **YES** — no Apple `/auth/revoke` attempted
  anywhere; the localized "remove Cleansia in Settings → Apple ID → Sign in with Apple" note is shown, ×5 languages.

### Devices D6-8 verdict
**PASS** — single keychain-backed `DeviceIdProvider` instance shared between `HeaderAdapter`(X-Device-Id) and the
devices client (no second UUID/identifierForVendor); current device hides revoke; defensive self-revoke→sign-out;
server-scoped own-device-only revoke with NotFound-on-non-owned (S3).

### Residual
- NONE blocking. Observation (non-blocking): `NotificationPreferences.toDomain()` defaults missing booleans to
  `true` except `promo` → `false` (`NotificationPreferences.swift:80-95`) — a safe, privacy-preserving default
  (opt-out marketing by default), not a security issue.

**VERDICT: PASS.** T-0314 Slice F is exploitable-as-written: NONE found. The load-bearing GDPR delete is correct
on all three axes (complete-wipe-via-signOutLocal on success; blocked-stays-signed-in with unspoofable code
extraction; SIWA note shown + Apple revoke owner-deferred). Devices D6-8 and prefs R14 PASS. No secret/PII logged
on any path. This completes the T-0314 customer security gate.
