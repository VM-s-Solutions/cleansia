# iOS app architecture & port strategy — living decision notes

> Companion to the **immutable** ADRs `agents/backlog/adr/0013-ios-app-architecture-and-port-strategy.md`
> (ADR-0013) **+** `0014-ios-deployment-target-ios16-and-state-mechanism.md` (ADR-0014, which **partially
> supersedes** ADR-0013: **iOS-16 floor** + the **`ObservableObject`/`@Published`** state mechanism + the
> iOS-16 MapKit variant; all other ADR-0013 decisions stand). The ADRs are the frozen decisions; this file
> is the *evolving* design notes, trade-off space, and current shape. Update this as the iOS port
> progresses; supersede an ADR for a real contract change. Cross-links: ADR-0011 (the born-canonical
> `ApiResult<T>` Swift contract this consumes), the Mobile API contract audit (security, 2026-06-22), the
> Android parity map (analyst, 2026-06-22), `patterns-mobile.md` (the iOS section), and
> `docs/architecture/*` once iOS lands.

## Current shape (ADR-0013 accepted 2026-06-23; ADR-0014 floor/state revision accepted 2026-06-23)

**Deployment target: iOS 16** (ADR-0014 / Q-IOS-01 answered — old-device reach: iPhone 8/8 Plus/X, 2017+;
iOS 17 was dropped because it excluded those phones). The only load-bearing effect of the floor is the
state mechanism (`@Observable` is iOS-17-only → `ObservableObject`/`@Published`); the maps use the iOS-16
MapKit variant; everything else in ADR-0013 holds unchanged on the floor.

iOS is a **parity port** of the Kotlin/Compose customer + partner apps onto the **same Mobile API
contract** — not a new product. **What "parity port" means visually is fixed by ADR-0018 (the
design-parity principle): same layout/flow/branding as Android, built with NATIVE SwiftUI components, and
iOS convention wins on a genuine component conflict** — see the *Design parity* section below. The
structure mirrors the proven Android `:core`+2-apps shape:

```
src/cleansia_ios/                         (greenfield — created on the first iOS ticket)
  Cleansia.xcworkspace
  CleansiaCore/        (SPM package = the :core parity — Network/Auth/DI/DesignSystem/Components/
                        Snackbar/Location/Push/Format/State)
  CleansiaPartner/     (app target, cz.cleansia.partner — LEAD APP)
  CleansiaCustomer/    (app target, cz.cleansia.customer)
```

| Concern | Decision | Parity to Android |
|---|---|---|
| Deployment target | **iOS 16** (ADR-0014; old-device reach: iPhone 8/X 2017+) | Android `minSdk 26` (2017) |
| Module layout | 1 workspace, `CleansiaCore` **SPM package** (`platforms: [.iOS(.v16)]`) + 2 **app targets** | `:core` + `:partner-app` + `:customer-app` |
| State | **`ObservableObject` + `@Published`** VMs (iOS-16 floor; `@Observable` is iOS-17-only); sealed `UiState`(Loading/Error/Loaded) + `ActionState`(Idle/Submitting/Error) **enums unchanged** | `StateFlow<UiState>` (E1) + `ActionState` (E2) |
| Navigation | `NavigationStack` (iOS 16+, available on the floor) | Compose Navigation |
| DI | initializer injection + a composition root (`AppContainer`); **no framework** | Hilt (replaced) |
| Result contract | `ApiResult<T> = Result<T, ApiError>` in `CleansiaCore` (ADR-0011 D4) | `ApiResult<T>` in `:core` |
| Token store | **Keychain** (Secure-Enclave-backed); refresh token replaced every refresh | `EncryptedSharedPreferences` (`TokenStore.kt`) |
| Auth client | **hand-written** (URLSession); excluded from codegen | hand-written `AuthApi.kt` |
| 401 refresh | `actor SessionRefresher` single-flight; separate no-auth session | `AuthAuthenticator` `synchronized(this)` + `NoAuthOkHttp` |
| Headers | `X-Device-Id` (one source = Device/Register id), `X-Device-Label`, `X-Time-Zone`; no-Bearer-on-anon allow-list — **full contract: `src/cleansia_ios/docs/header-parity-contract.md`** | `AuthInterceptor` + per-request `X-Time-Zone` |
| Codegen | **openapi-generator swift5 + urlsession**, from the **owner-regenerated** shared spec | openapi-generator kotlin |
| Maps | **MapKit by default**, behind a `MapProvider` protocol; Mapbox iOS SDK = scoped fallback. **iOS-16 variant:** `Map(coordinateRegion:annotationItems:)` for pickers; `MKMapView` via `UIViewRepresentable` for the full-bleed map + polygon overlays (the SwiftUI `Map {...}`/`Marker`/`MapPolygon` API is iOS-17-only) | Mapbox (no first-party map on Android) |
| Stripe | `stripe-ios` **PaymentSheet** (customer target only) | Android PaymentSheet |
| Push | APNs token → existing `/api/Device/*` with `Platform="ios"` | FCM → `/api/Device/*` |
| Lead app | **PARTNER** (read-only Dashboard proves the architecture first) | — |
| trusted-device | **omit v1 to match Android** | not sent by Android |
| i18n | 5 locales (en/cs/sk/uk/ru) via **String Catalog** `.xcstrings` | `values-*/strings.xml` |
| **Design parity** (ADR-0018) | **same layout/flow/branding as Android, NATIVE SwiftUI components, iOS-wins-on-conflict** (Gate-DP per screen) | the Android Compose screens are the cited source of truth |

## Design parity — same layout/flow/branding, native components, iOS-wins-on-conflict (ADR-0018)

ADR-0013 said "parity port"; **ADR-0018 fixes what that means visually**, so no screen ticket drifts into
pixel-cloning Material or into an iOS redesign:

- **Held identical to Android (parity, non-negotiable):** the **screen inventory + per-screen content**, the
  **navigation structure + user flow** (partner Take→OnTheWay→Start→Complete, customer Services→WhenWhere→
  Confirm, the same tabs/back-stack), the **branding** (colors/logo/type/spacing/icon-meaning/mascot), and the
  **layout arrangement** (same regions, same field set + order). The Android Compose screens are the source of
  truth — **every iOS screen ticket cites its counterpart**.
- **Upgraded to native iOS (the "component improvements"):** every control is a **native SwiftUI component**
  (no Material re-implementation); the `Cleansia*` shared components are **brand-skins over native controls**
  (the SwiftUI analogue of how `:core` skins Compose's Material controls — here skinning *native iOS* instead);
  platform affordances expected (SF Symbols mapping the Android icon's meaning, swipe-back, haptics, detents,
  pull-to-refresh).
- **Conflict rule: iOS-native WINS on a genuine component conflict** — keep layout/flow/branding identical,
  upgrade the component, **note the divergence** in the ticket. iOS wins on the **component only**, **never**
  on layout/flow/branding (the rule's boundary).

**Canonical Android → iOS-native component mappings (ADR-0018 D3 — the set the reviewer checks against):**

| Android (Compose / Material) | iOS-native | identical (parity) |
|---|---|---|
| customer shell: `HorizontalPager` + floating-pill `CustomBottomBar` + center `BookFab` (`MainShell.kt:363-474`) | ONE shell-level `NavigationStack` + `TabView(selection:)` `.tabViewStyle(.page(indexDisplayMode: .never))` pager (tab roots only) + the custom `CustomerBottomBar` pill/FAB composite via `.safeAreaInset(edge: .bottom)` — **ADR-0022**; the pill+FAB is **BRANDING**, never a component swap | same 4 tabs/order, swipe-between-tabs, pill+FAB signature, ONE back stack, bar hidden on child push |
| partner shell: `HorizontalPager` + `FloatingIslandBottomBar` (4 even slots, no FAB — `MainScaffold.kt:106,144`) | the same ADR-0022 shape, partner pill variant (PM-filed follow-up; recorded interim after the crash fix = stock `TabView` + per-tab `NavigationPath` stacks) | same tabs, same order |
| `ModalBottomSheet` / AnchoredDraggable booking sheet | `.sheet` + `.presentationDetents` | same 3 steps + content + snap intent |
| Material `DatePicker`/`TimePicker` | native `DatePicker` (`.graphical`/`.wheel`/`.compact`) | same field + label + placement |
| Material `TextField` | native `TextField`/`SecureField` | same fields/labels/error strings ×5 |
| Android system-back + Material top-bar back | swipe-back gesture + `NavigationStack` nav-bar back | same back-stack/destination |
| Coil `AsyncImage` | SwiftUI `AsyncImage` (or Kingfisher) | same frame/aspect/placeholder layout |
| Material `Snackbar` | native toast on the same `SnackbarController` bus | same message, one-per-failure |
| Material `AlertDialog` | `.alert` / `.confirmationDialog` | same title/body/actions/destructive semantics |

**Gate-DP** (standing per-screen reviewer gate) checks the §G assertions (AR-DP-1/2/3 **+ the 2026-07-02
ADR-0022 hardening: AR-DP-1a + AR-DP-4**): cite-the-Android-screen layout/flow/branding parity; native-components-only;
conflicts-iOS-native-and-noted-touching-only-the-component; **AR-DP-1a** — every drawable/raw asset the cited
Android screen references has an iOS asset-catalog counterpart (SF-symbol substitution ONLY for Material icon
vectors, NEVER for brand raster/animated art — mascots/logo/wordmark); **AR-DP-4** — a ONE-TIME per-app app-chrome
check (AppIcon + branded launch screen + in-app splash), owned by the app's shell/scaffold ticket because app
chrome lives in no screen's `.kt` citation (the phase/ios-fix1 gate-miss lesson: the gate's citation unit was the
`.kt` screen file, so everything in `res/` and app packaging slipped). Recorded in §G of
`agents/backlog/ios-app-review-checklist.md` + sprint-12 §10.6 + reviewer-check #22; it runs
beside Gate-AR (ADR-0016) and the SwiftLint/SwiftFormat gate on every iOS **screen** ticket (infra tickets
N/A). A **new** control mapping a feature surfaces is folded back into the table above (living-doc note) so the
set converges; a new *rule about the principle itself* would be a superseding ADR.

## The load-bearing seam: auth/session/headers (the part that breaks silently)

The contract iOS must reproduce **exactly** (it is invisible to the generated client):

```
every request ──▶ HeaderAdapter
                    • X-Device-Id   = DeviceIdProvider (ONE source; == Device/Register deviceId)
                    • X-Device-Label= "<model> - iOS <ver>" (ASCII-safe)
                    • X-Time-Zone   = TimeZone.current.identifier   (also on the no-auth session)
                    • Authorization = Bearer <access>  IFF path ∉ anon-allow-list AND token present
                          │
                          ▼ (on 401)
                    actor SessionRefresher (single-flight)
                    • one refresh under N concurrent 401s; queued callers await + retry with fresh token
                    • refresh uses a SEPARATE no-auth URLSession (can't loop)
                    • refresh token REPLACED on every success (single-use / RefreshTokenReused)
                    • on refresh fail → ForcedSignOut(.sessionExpired) + clear session-scoped caches
```

**The `X-Device-Id` invariant** (the one most likely to be missed): the header value **must equal** the
`deviceId` sent to `/api/Device/Register`, because the server's remote-revoke match is
`RefreshToken.DeviceId == Device.DeviceId`. iOS resolves it in **exactly one** `DeviceIdProvider`
(`UIDevice.identifierForVendor` persisted to the Keychain on first launch). A second source silently
breaks "revoke this device from Your Devices."

**Anon allow-list** = the Android 6 (`login`, `register`, `refreshtoken`, `googleauth`, `confirmuseremail`,
`resendconfirmationemail`) **+** the customer host's `[AllowAnonymous]` endpoints (Lookup, CreateOrder,
Quote, Payment, GetPlans, Referral-Validate). Documented as the Phase-0 **header-parity spec** so the dev
doesn't re-derive it.

**Empty-token special case:** an unconfirmed-email login returns **200 with an empty Token** → route to the
confirm-email gate, **not** a session.

### Generated-client authentication — via the Core-spine-backed `RequestBuilderFactory` (ADR-0019)

The hand-written spine above authenticates the **hand-written** `/api/Auth/*` calls. The **generated** business
client (`PartnerDashboardAPI` &c.) does **not** authenticate itself: its APIs are static, read the global
`CleansiaPartnerApiAPI` config, apply **only** the static `customHeaders` (no Bearer, no device/time-zone
headers, no 401-refresh), and every business endpoint is generated `requiresAuthentication: false` — so without
an adapter the calls go out tokenless and 401. **ADR-0019 fixes the one way it authenticates:**

- The `AppContainer` installs a **custom `RequestBuilderFactory`** into `CleansiaPartnerApiAPI.requestBuilderFactory`
  (per host) whose `RequestBuilder` subclass routes **every** generated request through the **same** spine:
  the `HeaderAdapter` (Bearer-iff-not-anon + `X-Device-Id`/`X-Device-Label`/`X-Time-Zone`) and the
  `actor SessionRefresher` (single-flight 401→refresh→retry). Uses only the generator's `open`/overridable
  points (`requestBuilderFactory`, `execute`, `createURLRequest`) → survives regeneration; lives in
  hand-written code (the generated files are never edited).
- **One token source, no per-call duplication (the invariant):** the app-side generated-client wrapper and
  call sites **never** read `TokenStore`, set a Bearer, write a Bearer into `customHeaders`, or stamp a device
  header — those happen **only** in the `HeaderAdapter` reached through the factory. The generated
  `requiresAuthentication: false` flag is **not** the authority; the injected `AnonymousAllowList` is.
- **First proven on T-0303** (the partner proving vertical) and **copied by every later authed wave**
  (T-0307/0309/0310 partner, T-0312/0313/0314 customer) — install the same factory per host, write no auth
  code. **Reviewer check #13-gen** (composes with #1–#12): generated client authenticates only via the factory;
  no second token source; no per-call header duplication; the allow-list (not the flag) governs.
  **Tests:** TC-IOS-GEN-AUTH (Bearer+device headers present despite the flag), TC-IOS-GEN-401 (one refresh under
  N concurrent generated 401s — the same `SessionRefresher`), TC-IOS-GEN-DEVICEID (one device id).

### Session-presence on the public app surface — `hasValidSession` only (single mutation path = the spine)

The concrete `TokenStore` (Keychain; exposes `save` / `clear` / `current`) is the spine's **mutation** surface
and stays **`internal` to `CleansiaCore`** — it is **not** exposed on the public `AppContainer` protocol. The
**only** session detail the app layer (e.g. a root view deciding login-vs-shell presence) may read is a
**presence-only, read-only** accessor:

```swift
// On the AppContainer protocol + BaseAppContainer; partner/customer containers forward it.
var hasValidSession: Bool { get }   // delegates to the spine: tokenStore.current()?.accessToken present & non-empty
```

- **Why presence-only.** Putting `save()`/`clear()` (or the raw token) on the public surface creates a **second
  place the session can be mutated** — exactly the single-token-source invariant ADR-0013 D4 and ADR-0019 D2
  ("the Bearer is set in exactly one place… the spine") exist to prevent. `hasValidSession` is a pure read; all
  session mutation (login save, refresh-rotate, forced-sign-out clear) stays inside the spine
  (`AuthApiClient` / `SessionRefresher` / `SessionManager`). The app reads presence; it never writes session.
- **Scope guard.** This is the **minimal** presence bool — **not** a richer session abstraction (current-user,
  expiry, refresh-needed). The fuller session/splash gating is **T-0304's SplashGate** concern; do not grow
  `hasValidSession` into it. This is an **application of ADR-0013 D4 / ADR-0019, not a new ADR** (no new
  trade-off — it just keeps the public surface read-only).
- **Reviewer angle (a #13-gen sibling).** The public `AppContainer` surface exposes **no** `TokenStore`,
  **no** `save`/`clear`, **no** raw token — only `hasValidSession`. A `TokenStore` (or its mutators) reaching the
  app layer is a finding: it forks the mutation path the spine owns.

### Partner router shape — a flat-enum `PartnerRootView` root-switch gated by `.splash` (ADR-0020)

The partner app routes its **top-level audience** (logged-out / resolving / locked / in-shell) with the
**flat-enum `PartnerRootView` root-switch** T-0303 shipped — **not** a path-based `NavigationStack` audience
router. ADR-0020 (accepted 2026-06-26, surfaced by the T-0304 Understand pass) canonicalizes it so the later
partner waves (T-0305/0307/0309/0310) extend one router instead of each re-deciding the shape:

```swift
// PartnerRootView.Route — extended in T-0304 from T-0303's { login, dashboard, verifyEmail }.
enum Route { case splash; case login; case verifyEmail; case registrationLock; case dashboard /* = the shell */ }
_route = State(initialValue: container.hasValidSession ? .splash : .login)   // seed: .splash, NOT .dashboard
// Route.afterLogin: requiresEmailConfirmation ? .verifyEmail : .splash       // verified login BOUNCES through .splash
```

- **The audience is a closed `enum` the root view `switch`es over** (compiler-exhaustive, replace-semantics —
  setting `route` swaps the whole root, no audience back-stack to swipe-back into a stale/logged-out state).
  **`NavigationStack` stays the *intra-audience* push container** (OrderDetail, ProfileSection, the
  onboarding-chain sections) — **not** the audience selector. (Reconciles the `patterns-mobile` `navigation.Routes`
  row, now split-scoped: top-level audience = root-`enum`; intra-audience push = `NavigationStack`+typed route.)
- **`.splash` is the SplashGate decision state** (the `SplashViewModel` parity, `PartnerNavHost.kt:478-509`):
  resolves the registration gate **once** on appear → `.dashboard`-shell vs `.registrationLock` vs `.login`.
  A verified **login bounces through `.splash`** (the Android idiom, `PartnerNavHost.kt:118-124`) so the
  registration gate is re-applied to every fresh login; the T-0303 §7.2 `verifyEmail` gate is **preserved**
  (unverified → `.verifyEmail`). The seed change `.dashboard → .splash` **closes a T-0303 fail-open** (the old
  seed landed an authed-but-incomplete partner straight on the shell).
- **T-0305 seed refinement (refines, not contradicts, D2 — sprint-12 §7.5):** the launch seed is now
  **UNCONDITIONALLY `.splash`** (was `hasValidSession ? .splash : .login`) so the SplashGate is the **sole**
  launch resolver — required for the onboarding-vs-login decision on an un-authed first run. The fail-closed
  registration gate (#24) is **byte-unchanged and no bypass is introduced**: the no-session branch resolves
  only to `.unauthenticated`/`.needsOnboarding` (via `hasSeenOnboarding`), **never** `.authenticated`.
- **Mirror the Android *decision tree*, not its *mechanism*** — Android is a path-based `NavHost`; iOS mirrors
  the `Splash → {shell | lock | login}` tree with a root-`enum` (the same "parity is of behavior, not
  vendor/mechanism" logic ADR-0013 D6 used for MapKit-vs-Mapbox). Rejected: a literal `NavigationStack` audience
  router (discards the working root-switch + §7.2 test; models replace-semantics audience hops as awkward path
  push-then-clear). **Reviewer #23** enforces it; the customer app (T-0312+) copies the *pattern* with its own
  root view + audience states.

### Partner registration gate — fail-closed, between login and the shell (sprint-12 §7.4 Decision 1; SECURITY)

The **standing gate every later partner wave sits behind** (the iOS sibling of the partner-gate rule), a
confirmation of the Android gate (ADR-0013 "mirror the code") — recorded in sprint-12 §7.4, **not** a new ADR:

- **Between login and the authed shell.** The shell (Orders/etc.) is **unreachable** until
  `isRegistrationComplete == true`; ADR-0020 enforces it structurally (`.splash` resolves before `.dashboard`
  renders — no login→shell path bypasses the gate).
- **The predicate is an AND** (`RegistrationLockViewModel.kt:103-109`): `hasCompletedProfile == true &&
  areDocumentsUploaded == true && (contractStatus == .approved(4) || .active(2))`. **Any nil/unknown/other →
  LOCKED.** Availability is **not** a clause (backend always reports it true). `ContractStatus`:
  Pending=1/Active=2/Terminated=3/Approved=4/Rejected=5.
- **Both error paths fail CLOSED:** the **SplashGate** routes a status-API `.failure` → `.registrationLock`,
  **never** the shell (`PartnerNavHost.kt:506`); the **lock VM's `.failure`** preserves the cached status and
  **never** unlocks (`RegistrationLockViewModel.kt:197-211` — the Error branch must not touch `status`). The
  **only** unlock is the success "complete" watermark (`RegistrationLockScreen.kt:112-114`).
- **Reviewer #24** + **TC-IOS-REGLOCK** (sprint-12 §7.4 / §8) enforce it. **Forward-note:** later partner
  waves (T-0307/0309/0310) render *inside* the shell (reached only past this gate) — they must not add a
  second, weaker status check or a permissive nil default that re-opens it.

### Device-local settings — the GENERAL `AppSettingsStore` in `CleansiaCore` (UserDefaults-backed) (sprint-12 §7.5 Decision 1)

**The one way to do device-local settings on iOS** (the `AppSettingsRepository.kt` parity), surfaced by the
T-0305 Understand pass — **not** a new ADR (an application of ADR-0013 D1 + the existing
`CleansiaCore/Validation/EmailValidator.swift` Core-utility precedent):

- A **single, general** `AppSettingsStore` in `CleansiaCore` (a small key/value façade) — **not** a
  single-purpose `OnboardingStateStore` + a separate language helper — so T-0307+ / the customer wave add a
  property here instead of standing up a new store each time. T-0305 needs two: `hasSeenOnboarding`
  (get + `markSeen()`, `AppSettingsRepository.kt:25-33`) and a **resolved language tag** ∈ `{en,cs,sk,uk,ru}`
  (`:24`/`:41-43`): persisted-tag-if-in-set → else `Locale.current.language.languageCode`-if-in-set → else
  `"en"` (the Android `?: "en"` fallback, `RegisterViewModel.kt:89`).
- **Backed by `UserDefaults`, NOT Keychain** — deliberate parity with Android DataStore's **wiped-on-uninstall**
  semantics (`partner_app_settings`, `AppSettingsRepository.kt:13`). The Keychain spine (header-parity §2/§6)
  holds the security-load-bearing device id + the session; onboarding-seen + a UI language preference are not
  secrets and **must** reset on reinstall. A secret reaching `AppSettingsStore` (or a settings value in the
  Keychain) is a reviewer finding (#26a). CRC: `ios-app-settings-store`.

### ConfirmEmail email threading — via the `.verifyEmail(email:)` Route associated value, NOT a `UserProfileStore` (sprint-12 §7.5 Decision 2 — ADR-0020 fold-in)

iOS has **no `UserProfileStore`** (T-0303 used a one-shot `employeeGetCurrentEmployee`, §7.2). The
ConfirmEmail **resend** needs the email; Android reads it from `UserProfileStore`
(`AuthRepository.kt:104-105`/`:182-183`). iOS threads it through the **Route associated value** instead — the
existing ADR-0020 `.verifyEmail` case gains a payload: `case verifyEmail(email: String?)`. This is an
**ADR-0020 living-doc fold-in** (D5: "a future audience state is a new `enum` case under this ADR" — here an
existing case merely carries a payload), lighter than and aligned with "the audience enum carries the state";
**not** a superseding ADR.

- The unverified login already **has** the email — `LoginOutcome.unverifiedEmail(email:hasToken:)` (the
  Android parity, `AuthRepository.kt:99-114`/`:277`); `LoginSuccess`/`Route.afterLogin` currently **drop** it.
  T-0305: `LoginSuccess` carries the email, `afterLogin` seeds `.verifyEmail(email:)`, the verify screen's
  resend uses it. The confirm **code** post needs no email (`confirmEmail(code:)`, `AuthRepository.kt:168-171`).
- **Cold-start-mid-confirm-with-no-email** degrades to Android's blank-email guard: `.verifyEmail(nil)` →
  **disable resend + show `error_generic`** (the `UnverifiedEmail(email="",hasToken=false)` parity,
  `AuthRepository.kt:175-180`); the code entry still works. The `requiresEmailConfirmation==true → .verifyEmail`
  gate (T-0303 §7.2 / ADR-0020 D3) is **preserved** — the associated value is additive. A NEW `UserProfileStore`
  built just to carry the email is a reviewer finding (#26b). The `ios-partner-root-router` CRC is unchanged in
  responsibility — it lands the state; the state now carries a payload it does not interpret.

### Client-side password policy — a Core `PasswordPolicy` + `PasswordRuleList` (sprint-12 §7.5 Decision 4)

The Android register password rule (≥8 && ≥1 letter && ≥1 digit, `RegisterViewModel.kt:37-39`, consumed `:73`)
is extracted to **`CleansiaCore/Validation/PasswordPolicy`** (the same predicate, lifted out of the VM — Android
left the `passwordHas*` getters in `RegisterUiState`; iOS fixes that so partner + customer share one source),
feeding a native-SwiftUI **`PasswordRuleList`** Core component (the parity of the already-shared `:core`
`PasswordRuleList.kt`, used by partner `RegisterScreen` + customer `SignUpScreen.kt:182,200`). **Client-side
UX only — the backend `BaseAuthValidator` is authoritative.** Reused by the customer wave (T-0312+) at the
second call site with no re-decision. A VM-local copy of the predicate or a per-app widget is a finding (#26c).
CRC: `ios-password-policy`. (An application of ADR-0013 D1 + a catalog harvest, not a new ADR.)

### F1 parity deviation — Android partner Register/Forgot hardcode English validation strings; iOS localizes (sprint-12 §7.5 Decision 5)

`RegisterViewModel.kt:64-84` + `ForgotPasswordViewModel.kt:45-52` set validation errors as **raw English
literals** (these VMs don't inject `@ApplicationContext Context`) — they never localize across the 5 locales.
**iOS does it right** (`Localizable.xcstrings` keys ×5, ADR-0013 D11 / reviewer #10), the one intentional
Android-divergence on T-0305 (the `patterns-mobile` Parity rule's "Android is wrong → raise a finding, don't
copy"). Recorded as a `consistency.md` **E8** deviation; the android partner-VM i18n fix is a PM-filed
follow-up (small, mechanical), **not** part of the iOS wave.

### iOS maps behind `MapProvider`/`GeocodingService` — the one way (sprint-12 §7.6 / T-0306)

The first concrete shape of the ADR-0013 D6 / ADR-0014 D6′ map seam, surfaced by the T-0306 Understand pass.
**Four rulings, all APPLYING accepted ADRs — no new ADR.** The Android source the iOS port mirrors:
`partner-app/.../features/profile/AddressPickerScreen.kt` (+ `AddressPickerViewModel.kt`) and
`core/.../location/{ReverseGeocodingService,GeocodedAddress,LocationService,MapStyles}.kt`.

**Decision 1 — `MapProvider` protocol shape: a minimal picker factory NOW, T-0307's full-bleed surface added
ADDITIVELY later.** `MapProvider` (in `CleansiaCore/Location`) ships only what T-0306 needs — a picker-map
factory producing a SwiftUI view bound to a region + a static center-pin overlay (the iOS-16 variant: a
`Map(coordinateRegion:interactionModes:showsUserLocation:annotationItems:)` with `annotationItems: []` + a
SwiftUI overlay pin the map pans under — NOT `Map{Marker}`/`onMapCameraChange`, reviewer #12). T-0307's
full-bleed `OrderDetail` map + 3-snap sheet + the service-area polygon overlay are added **later as an
additive method** on the same protocol (the `MKMapView`-via-`UIViewRepresentable` surface, ADR-0014 D6′) —
**not** designed ahead. Rationale: designing the rich surface now means guessing T-0307's camera/overlay/
gesture needs before they exist; additive protocol methods don't break the picker call site, so deferring
costs nothing and avoids a speculative-shape rewrite. **`GeocodingService` is a clean 1:1 port of
`ReverseGeocodingService.kt`** (`reverseGeocode(lat,lng) → GeocodedAddress?`, `forwardGeocode(query,
countryIsoCodes) → [GeocodedAddress]`) **minus the Mapbox token + the OkHttp/network args** — under the
MapKit default it is `CLGeocoder`/`MKLocalSearch`, no token, no HTTP client. `Coordinate` + `GeocodedAddress`
are plain value types in `CleansiaCore/Location` (the `GeocodedAddress.kt` parity: lat/lng/street/city/
zipCode/country/countryIsoCode/formatted). **The only sanctioned MapKit/CoreLocation consumer is the
`MapKitMapProvider`-produced view + `CLGeocoderGeocodingService`** — feature/VM code imports neither
(reviewer #7 + the new #27 below). Reused by T-0307/0310/0314 by adding the next additive method, not
re-deciding the seam.

**Decision 2 — current-location / permission DEFERRED out of T-0306** (the one flow-touching Gate-DP
divergence). The Android picker auto-centers on the FusedLocation fix + shows a my-location FAB
(`AddressPickerScreen.kt:131-161,272-295` via `LocationService.kt`). iOS **omits** that for T-0306: the
picker centers on the **Prague default** (`DEFAULT_CENTER = 14.4378, 50.0755`, zoom 15 — the
`AddressPickerScreen.kt:90-91` parity) and ships **pan-to-place + search at full parity** (both work without
any location fix). The `LocationProvider` seam (`CLLocationManager`) + the my-location FAB are homed to
**T-0310** (when the picker is wired into the AddressSection and **T-0325** has added the
`NSLocationWhenInUseUsageDescription` plist key — owner ticket; without it the iOS permission prompt never
appears, so building the FAB now would ship a dead control). **This is the recorded Gate-DP divergence**
(ADR-0018 D3): *iOS picker omits current-location pending T-0325; pan/search parity is full; the divergence
touches a deferred affordance, not layout/flow/branding* — architect sign-off, the T-0307 inert-nav/§7.4
contact-support-inert precedent. **Not a new trade-off:** it is the same "defer the affordance whose
dependency isn't live yet, home it, note it" call §7.2/§7.4 already made.

**Decision 3 — geocoding error/throttle + NO `UiState`.** (a) `CLGeocoder` is best-effort: **cancel the
in-flight geocode before re-firing** (`kCLErrorGeocodeCanceled` is expected, swallowed), return `nil`/`[]`
on any error, **never block the confirm or crash** — the `runCatching{}.getOrNull()` /
`.getOrDefault(emptyList())` parity (`ReverseGeocodingService.kt:41,79`). (b) The debounce timings port
**VERBATIM**: **300ms forward** (`AddressPickerScreen.kt:188`), **500ms reverse-on-idle**
(`AddressPickerScreen.kt:171`) — they double as Apple's `CLGeocoder` rate-limit guard. iOS-16 has **no
idle callback**, so reverse-geocode-on-idle is a **VM-owned Combine/`Task` debounce** off the region binding
(not a map callback). (c) **The AddressPicker correctly has NO `UiState<T>`/`ActionState`** — it is an
interactive map with plain `@Published` state (`resolved: GeocodedAddress?`, `lookingUp`, `searchQuery`,
`searchResults`, `searching` — the `remember{mutableStateOf}` set, `AddressPickerScreen.kt:117-122`) + a
**one-shot confirm event** (the `onConfirmed(GeocodedAddress)` callback, not a mutation result). It is
neither a load-fetch screen (E1) nor a mutation screen (E2), so the sealed-state expectation is **correctly
scoped OUT** — a reviewer must NOT flag the absence of `UiState`/`ActionState` here. **Reviewer note #27**
(see sprint-12 §7.6 / §8).

**Decision 4 — NO Mapbox token / security (net reduction in secret-management surface).** Under the MapKit
default there is **ZERO map token, ZERO map SDK dependency, ZERO `Package.swift` change** — MapKit +
CoreLocation are **system frameworks**, no SPM entry. This **contrasts Android's `MAPBOX_ACCESS_TOKEN`
BuildConfig** (`ReverseGeocodingService.kt:21` takes the token; the Hilt module feeds it from BuildConfig).
**No owner provisioning step for maps** (the §7 owner-steps map row already says "Mapbox token ONLY IF
Q-IOS-02 flips the default"). **Net effect: iOS removes a secret Android carries** — one fewer token to
rotate, one fewer leak surface. **Q-IOS-02 stays defaulted "No"** (MapKit standard style; `MapStyles.kt`'s
custom Mapbox Studio style is **NOT ported** — the stock MapKit style is the parity baseline; a hard brand
requirement is the only input that flips it, behind the unchanged seam). Recorded as the no-token **security
note** (sprint-12 §7.6).

**New CRC (added with the T-0306 wiring):** `ios-geocoding-service` — `GeocodingService` (protocol) +
`CLGeocoderGeocodingService` (the default impl in `CleansiaCore/Location`): *responsibility:* forward/reverse
geocode text↔`GeocodedAddress`, best-effort (nil/`[]` on error, cancel-before-refire), no token. *Collaborators:*
`CLGeocoder`/`MKLocalSearch`, `Coordinate`/`GeocodedAddress` value types. *Does NOT know:* the Mapbox token
(there isn't one), the network client (system framework), which feature/VM consumes it, or how the picker
renders. (The `ios-map-provider` CRC from ADR-0013 is unchanged — `MapKitMapProvider` is its default impl.)

**Living-doc note (T-0349, 2026-06-30) — the address-picker VM is a Core type, the View stays app-local.** The
near-duplicate address-picker VM (partner `Profile/AddressPicker` + customer `Booking/WhenWhere/AddressPicker`)
is hoisted into **`CleansiaCore/Location/AddressPickerViewModel`** (public, alongside the `MapProvider`/
`GeocodingService` seam it already depended on) — partner + customer construct the one Core type. Per ADR-0013's
own escape clause this is a **home change, not a contract change** → recorded here + in `patterns-mobile.md`, no
new ADR. The public init is the customer's parameterized shape: `init(geocoding:, reverseDebounce: =
.milliseconds(500), searchDebounce: = .milliseconds(300), searchBias: [String] = ["cz","sk"])` — `searchBias` is
the **only** variation point (a caller-supplied param mapping 1:1 to `GeocodingService.forwardGeocode(query:,
countryIsoCodes:)`, never a country branch inside the VM); the `["cz","sk"]` default keeps partner non-regressing
(its test asserts the default bias unchanged). The **Views stay app-local** (`AddressPickerView` /
`BookingAddressPickerView` — distinct chrome/L10n/navigation); their `import MapKit` is the sanctioned
View-layer touch — binding the `MapProvider` protocol's MapKit-typed `pickerMap(region: Binding<MKCoordinateRegion>,
…)` / `fullBleedMap` signature (the `MapProvider.swift:5` boundary), NOT map/geocode logic. Invariant #7 ("no
feature imports MapKit") is about logic, not the View's binding to the protocol's typed signature. The VM is the
**only** shared piece.

### Partner Profile tab — in-tab `NavigationStack`, lock-owns-its-own-stack, deferred service-area, theme-honoring, sealed-state (sprint-12 §7.7 / T-0310)

The partner **Profile tab** (replacing `PartnerShellView.swift:36`'s `PlaceholderTab`) — the hub + 6 section editors +
the onboarding chain + Devices + Preferences — surfaced by the T-0310 Understand pass. **Five rulings, all APPLYING
accepted ADRs or confirming Android parity — no new ADR** (ADR-0020 owns the nav trade-off; sprint-12 §7.5 D1 the
settings store; §7.6 D2 the defer-the-affordance call; ADR-0018 Gate-DP the divergence form; `patterns-mobile` Parity
the E1 divergence). The Android source the iOS port mirrors: `partner-app/.../features/profile/` (`ProfileScreen.kt`/
`ProfileViewModel.kt`, the `*Section*` set, `OnboardingChainHeader.kt`, `SectionScaffold.kt`, `AddressSectionScreen.kt`)
+ `features/orders/{RegistrationLockViewModel,OnboardingChainViewModel}.kt` + `core/settings/AppSettingsRepository.kt`;
nav inventory `navigation/NavRoutes.kt`. **The device-id/revoke gate (decisions 6–8) is SECURITY-ruled in parallel —
not in this record.**

**D1 — Profile nav = an in-tab `NavigationStack` over a typed `ProfileRoute` enum, INSIDE the `.dashboard` shell's
Profile tab.** This is the **ADR-0020 D2 intra-audience push** applied (the root-`enum` `PartnerRootView` stays the
audience selector; `NavigationStack` + a typed route is the push container WITHIN an audience — ADR-0020 names
"ProfileSection" + "onboarding-chain sections" as exactly this). Android uses one flat `NavHost`; iOS mirrors the
*tree*, not the *mechanism* (per-tab `NavigationStack` = the native tab-local back-stack). `ProfileRoute` is derived
1:1 from `NavRoutes.kt:54-91` minus the audience cases: `.personal/.address/.identification/.bank(onboarding: Bool)`
(the four **gate** sections carry the flag — `NavRoutes.kt:64-67`), `.emergency`, `.documents` (no flag), `.preferenceLanguage`,
`.preferenceTheme`, `.devices` (the route lives here; the screen is SECURITY-ruled). The **AddressPicker is NOT a route**
— it is a `.sheet`/`.fullScreenCover` return-value flow (`onConfirmed(GeocodedAddress)`, the `AddressPickerScreen.kt:96-101`
parity), not a back-stack push. Reviewer #28a.

**D2 — the load-bearing call: the RegistrationLock owns its OWN local `NavigationStack` + onboarding-chain VM and
pushes the SHARED section set over ITSELF.** The lock is a **root audience state** (`PartnerRootView` `.registrationLock`,
NOT in the shell). Android pushes section screens **over** the lock in the same flat NavHost and, on finish,
`popUpTo(NavRoute.RegistrationLock){ inclusive = false }` — the lock stays **underneath** and re-resolves via its own
`ON_RESUME` (`OnboardingChainViewModel.kt:86-121` + `RegistrationLockViewModel.kt:134-136`), so the gate sections are
reachable-from-lock **without unlocking the shell** (the §7.4/#24 fail-closed gate). iOS mirrors the tree: `.registrationLock`
owns its **own** `NavigationStack` (over the same `ProfileRoute` gate cases) + its own chain VM; on chain-finish the
lock re-resolves and **only the success watermark flips the root to `.dashboard`**. **The shell's Profile-tab stack is
never entered from the lock.** **Section screens are SHARED as ONE set of Views/VMs hosted by TWO stacks** (the Profile
tab — maintenance edits, `onboarding == false` → pop on save; and the lock — onboarding, `onboarding == true` → chain
forward), the `onboarding` flag the **single** switch (the Android `NavRoutes.kt:55-67` proof: the lock chain and the
Profile hub resolve to the **same** `NavRoute.ProfilePersonal` &c.). **Rejected: cross-audience routing into the
shell's Profile tab** — it requires rendering the shell the gate exists to keep unreachable (a fail-OPEN hole that
breaks #24). Reviewer #28b + TC-IOS-LOCK-CHAIN (composes with #24). T-0304's inert Fix CTAs are wired here.

**D3 — `ServiceAreaProvider` / the advisory `ServiceAreaRow` is DEFERRED → T-0334.** The 3-state row
(`AddressSectionViewModel.kt:50-55`, `ServiceAreaProvider.kt`) is **advisory-only** — *"only feeds the indicator row —
failures degrade to Unknown rather than blocking save"* (`:165-167`); the save resolves `countryId` independently
(`:213-222`). Porting it is a **Core seam in its own right** (lazy-cached `ServiceAreaProvider` + a per-app
`ServiceAreaDataSource` binding + value types + ISO alpha-2↔alpha-3 reconciliation), and the **same provider backs the
forward-geocode country-bias** which **T-0306 also deferred** — so pulling it into a screen ticket balloons it. T-0310
ships pan/search/save at full parity without it. **Recorded Gate-DP divergence (architect sign-off):** *"Address ships
pan/search/save at parity; the advisory `ServiceAreaRow` is deferred to T-0334; touches a deferred advisory affordance,
not layout/flow/branding, never a save gate."* Reviewer #28 (its absence is NOT a finding).

**D4 — EXTEND the one general `AppSettingsStore` (NOT a second store) + honor theme app-wide now.** sprint-12 §7.5 D1
(reviewer #26a) already ruled the **one** way to do device-local settings: a single general `AppSettingsStore`,
`UserDefaults`-backed. The Android `AppSettingsRepository.kt:22-43` holds theme + language + onboarding-seen in **one**
DataStore with setters; iOS extends its store to match — add `setLanguage(tag)` (making the resolved tag writable) + a
`Theme` enum (System/Light/Dark — the `ThemePreference` parity) + `setTheme`. **Honor theme NOW** via
`.preferredColorScheme(theme.colorScheme)` on the partner root (`.system`→`nil`) — additive root wiring (the
`MainActivity`-collects-and-propagates parity, `AppSettingsRepository.kt:17-19`), avoiding a dead Preferences row.
Both prefs stay non-secret + `UserDefaults` (NOT Keychain — #26a). Reviewer #28c.

**D5 — born sealed-state canonical; Android E1 flag-bags NOT replicated (record-the-finding, iOS-right divergence).**
Android's `ProfileUiState` (`ProfileViewModel.kt:26-36`) + the section `*UiState` (`PersonalSectionViewModel.kt:17-30`)
are **flag-bag `data class`es** (`isLoading`/`isSaving`/`error?`/`isSaved` booleans — the **E1** smell,
`consistency.md:160-163`, partner named "mostly wrong"). iOS is born right: the hub + each section **load** use sealed
`UiState<T>` (`.loading`/`.error(canRetry:)`/`.loaded`), the section **save** uses `ActionState` + a one-shot effect
(NOT the picker case §7.6 D3 — sections DO have both a load and a mutation lifecycle). The section VMs **also** hardcode
English validation strings (`PersonalSectionViewModel.kt:82,91`; `AddressSectionViewModel.kt:201,205,220`) — the same
F1/E8 class as §7.5 D5; iOS localizes ×5. Per the `patterns-mobile` Parity rule ("Android wrong → diverge correctly on
iOS, raise the Android finding"), the android E1 flag-bag + string-literal fix is filed as **T-0337** (independent of
the iOS wave, same shape as F1/T-0333). Reviewer #28d.

**Scope A — current-location FAB + `LocationProvider` seam DEFERRED → T-0335 (gated on T-0325).** T-0306 §7.6 D2 homed
current-location to "T-0310 IF T-0325's plist key exists"; **T-0325 (`NSLocationWhenInUseUsageDescription`) is still
`proposed`** (an open owner manual_step), so building the FAB now ships a **dead control** (the §7.6/T-0331 precedent).
T-0310 ships the AddressPicker pan/search wiring only (Prague default center, full T-0306 parity). The **`LocationProvider`
protocol shape is recorded now** (a `CleansiaCore/Location` protocol — `authorizationStatus`/`requestWhenInUseAuthorization()`/
`currentLocation() -> Coordinate?`, default `CLLocationManagerLocationProvider`, the only CoreLocation consumer besides
the providers — the `LocationService.kt` parity) so T-0335 slots the FAB in additively. **Rejected: build the seam +
FAB behind T-0325 now** — couples a screen ticket to an open owner plist step for an affordance the picker doesn't need
to be usable. Recorded Gate-DP divergence.

**Scope B — "Notifications" DROPPED from T-0310 (not buildable at parity).** The Understand pass found NO Android prefs
surface, NO backend prefs/push-prefs API, NO client. Android "Notifications" (`NavRoutes.kt:51-52`) is an in-app push
**FEED** (the dashboard bell, partner-app-local Room DB), NOT a Profile-tab prefs screen. A "Notifications prefs" screen
has nothing to port (an ADR-0016 hidden/placeholder-feature risk) → **dropped**. The partner Profile hub Preferences
group is **Language + Theme + Devices** (the `ProfileScreen.kt:183-204` parity — no Notifications row). The in-app feed
(mis-homed to T-0310 by T-0303's §7.2 deferral) is a **richer feature** (a local persistence store + bell badge +
push-receipt path depending on T-0311 APNs) → a **separate spike, T-0336**.

**New / updated CRCs (T-0310):** `ios-profile-hub` (the hub View + VM; hosts the Profile tab's `NavigationStack`; does
NOT switch the audience root, does NOT route the lock, does NOT know the device-revoke contract); `ios-profile-section`
(one section View+VM written ONCE, hosted by TWO stacks; reads only the `onboarding` flag for context; does NOT know
which stack hosts it or the deferred `ServiceAreaProvider`); `ios-onboarding-chain` (the chain VM owned by the LOCK's
stack; does NOT know the shell's Profile-tab stack); `ios-app-settings-store` (updated — gains writable language + a
`Theme` enum + setters; still UserDefaults, still the one store; secrets stay in the Keychain spine).

### Partner order work-loop — additive `fullBleedMap`, the non-modal `SnapSheet` (ADR-0021), the pure action machine, ported staleness cache (sprint-12 §7.9 / T-0307)

The partner **order work-loop** — the 3-pane OrdersList, the OrderDetail shell (full-bleed map + the always-present
3-snap sheet), the OnTheWay lifecycle, checklist/notes/issues/timeline — surfaced by the T-0307 Understand pass on
`phase/ios-phase4` (HARD AREA #3; depends_on T-0304✓+T-0306✓). **The order-action OWNERSHIP gate is SECURITY's
(§7.8 / `security/ios-orders.md`, O1–O4) — out of this record.** Five decisions: **four APPLY accepted ADRs (no new
ADR); ONE is a genuine new trade-off → ADR-0021.**

**Decision (a) — the additive `MapProvider.fullBleedMap(coordinate:)` method (no new ADR; §7.6 D1 + ADR-0014 D6′).**
ONE additive method on the existing `MapProvider` seam — `fullBleedMap(coordinate:) -> some View`, implemented
`MKMapView`-via-`UIViewRepresentable` inside `MapKitMapProvider`, rendering **ONE address pin** camera-bottom-padded for
the sheet peek (`MapBackdrop`/`EdgeInsets` parity, `OrderDetailScreen.kt:256-299,273-281`). **NO `overlays:`/`polygon:`
param:** the **key finding** is there is **no service-area polygon data in the partner spec** (`ServiceCityDto` has only
`zipPrefix`, no geometry) and Android renders no polygon — designing overlay support now repeats the speculative shape
§7.6 D1 rejected. Overlay support is additive IF T-0334 ever has geometry. Feature/VM import no MapKit (#7/#12/#30).

**Decision (b) — the non-modal 3-snap sheet on the iOS-16.0 floor → ADR-0021 (the ADR-worthy one).** `.presentationDetents`
ships 16.0 with **only** `.medium`/`.large`; custom `.fraction`/`.height` are 16.4+; the floor is 16.0. The Android
OrderDetail is a **non-modal** `BottomSheetScaffold` (`skipHiddenState=true`, full-bleed map always behind, 0.75 peek,
3-anchor map-focus/peek/expanded — `OrderDetailScreen.kt:172-245`) that a modal `.sheet` cannot model. Ruled among
(i) bump the floor to 16.4 — **rejected** (re-opens the owner's 2017-device reach; and even 16.4 `.sheet` is modal),
(ii) a **custom non-modal `SnapSheet` Core container** (`GeometryReader`+`DragGesture`, 3 snap offsets, 16.0-safe) —
**CHOSEN**, full layout parity + the floor stays 16.0, (iii) a 2-detent native modal — **rejected as the answer**
(an ADR-0018 D1/Gate-DP **layout** divergence), kept only as the explicitly-approved-only fallback. Native
`.sheet`+`.presentationDetents` stays the way for **modal** sheets (the discriminator: modal-over-a-screen vs
non-modal-over-a-live-backdrop). **ADR-0021** records it (extends ADR-0014 D6′ — the sheet half of D6′; refines
ADR-0018 D3 — the missing non-modal-sheet mapping; the floor is NOT amended).

**Decision (c) — the pure shared lifecycle action machine (no new ADR; ADR-0013 "mirror the code" + DRY).** A pure
`OrderPrimaryAction.action(for status: OrderStatus?, isMine: Bool, hasAfterPhotos: Bool) -> OrderPrimaryAction` sealed
enum (`.take/.notifyOnTheWay/.start/.complete/.completeBlocked/.none`), one tested function for the **three** call sites
(detail footer, list inline row, panes) — canonicalizing the Android shape, which **inlines** the `when(status)` inside
the `OrderPrimaryAction` Composable (`OrderPrimaryAction.kt:54-126`) so three iOS sites would each re-inline it. The
table mirrors Android exactly (incl. `_4`+mine+!photos → `.completeBlocked` soft hint, `canComplete` parity; backend
validator is the safety net). It is **presentational** and consumes `isMine`/`hasAfterPhotos` — the ownership trust is
SECURITY §7.8 (O1–O4), not this function.

**Decision (d) — the T-0308 photo precursor seam (confirmed; no new ADR).** T-0307 reserves the Photos slot in the
sheet (rendered **disabled/placeholder**, visibly disabled — not a dead control) + derives `hasAfterPhotos` (feeding
(c)'s `.complete`/`.completeBlocked`); **T-0308 fills photo CAPTURE additively** (camera → JSON base64 →
`SaveOrderPhotosCommand`) with no OrderDetail re-layout — the `PhotosSection.kt` parity + the §7.2/§7.3 inert-now/
additive-later precedent.

**Decision (e) — the list state shape: sealed per-pane state + a refresh-phase enum, PORT the staleness cache (no new
ADR; ADR-0014 D2′ + §7.7 D5 + the T-0310 D5 precedent + the Parity rule).** The Android `OrdersListUiState`
(`OrdersListViewModel.kt:89-120`) is an **E1 flag-bag** (`isInitialLoad`/`isUserRefreshing`/`isBackgroundRefreshing`/
`hasLoadedOnce`/`inFlightActionOrderId`/`error`). iOS is born sealed-state: a sealed per-pane `UiState<[OrderListItem]>`
**plus** an orthogonal `enum RefreshPhase { idle, userRefreshing, backgroundRefreshing }` that preserves the silent-stale
"PTR-only-on-user-pull" behavior (PTR binds `==.userRefreshing`; background refresh is invisible; `.loaded` stays
mounted — no spinner flash). **The per-pane/per-order `Staleness` cache is PORTED** (`OrdersRepository.kt:159-192`:
~30s watermarks + `invalidatePanesFor(mutation)` mapping Take/Notify→Available+Active, Start→Active,
Complete→Active+History, registered in the `SessionScopedCacheRegistry`) — it is **load-bearing for no-flash
resume-after-mutation**; **simplifying to load-on-appear+`.refreshable` is an un-approved behavior divergence**
(the dashboard-cache §7.2/§7.4 deferral does not apply — that had no resume loop; the work-loop's resume IS the cache's
reason to exist). Inline commit = an **iOS-native confirm affordance** (`SlideToCommit`→native button+confirmation /
`swipeActions` — the noted Gate-DP component swap; same actions/in-flight/guard, the §7.8 O4 re-entry guard). Android
E1 fix → T-0337.

**Convention — Code→OrderStatus on the read path.** The read-path DTOs (`OrderItem`/`OrderListItem`.`orderStatus`)
carry the **`Code` envelope** `{type,name,value:Int?}` (the action responses carry the typed `OrderStatus`). Map in
**one** `extension Code { func toOrderStatus() -> OrderStatus? { value.flatMap(OrderStatus.init(rawValue:)) } }` —
`OrderStatus: Int` rawValues 0…6 = the backend ints (0 New·1 Pending·2 Confirmed·3 OnTheWay·4 InProgress·5 Completed·
6 Cancelled, `OrderStatusPill.kt:36-42`). No raw-`Int` `.value` compares; no second mapper.

**Recorded Gate-DP divergences (all component-only):** the `SnapSheet` vs `.sheet` (ADR-0021, #29); `SlideToCommit`→
native confirm (#30); **no service-area polygon** (Android has none either — recorded so the absence isn't flagged).
**New CRCs (T-0307):** `ios-snap-sheet` (the non-modal 3-snap container; knows nothing of order data/MapKit);
`ios-order-primary-action` (the pure action table; does NOT know how `isMine`/`hasAfterPhotos` are trusted —
SECURITY §7.8); `ios-orders-cache` (per-pane/per-order watermarks + mutation→panes; does NOT know the UI refresh-phase
or the token); `ios-order-detail-vm` (sealed `OrderDetailUiState`+`ActionState`+`OrderAction?`; does NOT know the
sheet snap mechanics, MapKit, or photo capture). **Reviewer checks added:** #29 (ADR-0021 sheet), #30 (map+list state),
#31 (action machine + Code→OrderStatus). **Tests:** TC-IOS-SNAP (snap resolver), TC-IOS-ORDER-ACTION (the action table).

### Partner order PHOTOS — the first `UIViewControllerRepresentable`, the 1920/0.7 compression helper, `AsyncImage` read-back, the re-fetched-`hasAfterPhotos` Complete gate (sprint-12 §7.10 / T-0308)

The partner **order photos** surface — camera/library capture → **base64-over-JSON** upload, read-back, delete, and
the After-photo Complete-unblock — surfaced by the T-0308 Understand pass on `phase/ios-phase4` (depends_on T-0307;
fills the disabled Photos placeholder §7.9 (d) reserved). **Four rulings, all APPLYING accepted ADRs — no new ADR**
(ADR-0018 D2/D3 + Gate-DP own the capture seam + the image-loading swap; the Parity rule + ADR-0018 D3 own the
compression divergence; ADR-0013 parity owns the re-fetched-`hasAfterPhotos` gate; ADR-0016 AR-PRIV-4 owns the
in-ticket plist keys). **The photo-upload OWNERSHIP / EXIF-strip gate is SECURITY-ruled in parallel
(`security/ios-orders.md`) — not in this record.** Android source mirrored: `partner-app/.../features/orders/
PhotosSection.kt` + `OrderPhotosViewModel.kt` + `OrderDetailScreen.kt` (`:530-558`) + `data/orders/OrdersRepository.kt`
(`:261-297`).

**D1 — the capture seam = the repo's FIRST `UIViewControllerRepresentable` (`CameraOrLibraryPicker` in
`CleansiaCore/Components`).** A camera-capable `UIImagePickerController` wrapped in a `UIViewControllerRepresentable`
(the *controller* analogue of the ADR-0014 D6′ `MKMapView`/`UIViewRepresentable` *view* seam — both ADR-0018 D2
brand-skin-over-native seams). The single Add tile (the Android single-affordance rail) opens a native
`.confirmationDialog` action sheet → Take Photo (`.camera`) / Choose from Library (`.photoLibrary`). **Rejected:**
PHPicker (library-only — fails the camera requirement); AVFoundation (over-engineered — rebuilds the system camera).
This is the canonical **imperative-UIKit-controller-behind-a-SwiftUI-seam** idiom, harvested to `patterns-mobile`.
**Gate-DP camera-vs-gallery divergence (architect sign-off):** Android's Add tile is **gallery-only**
(`ActivityResultContracts.GetContent("image/*")`, `PhotosSection.kt:146-161,200`); iOS adds **camera + library** (the
ticket's camera requirement) — an iOS ENHANCEMENT that ADDS a source affordance without moving layout/flow/branding.

**Catalog correction (the false precedent guarded against):** the **AddressPicker (T-0306) is pure MapKit/SwiftUI**
(`Map(coordinateRegion:annotationItems:[])` + a SwiftUI overlay pin + `CLGeocoder`/`MKLocalSearch`) — it uses
**neither** a `UIViewControllerRepresentable` **nor** a `UIViewRepresentable`. Any claim that it established a
representable precedent is **FALSE**; `CameraOrLibraryPicker` is genuinely the repo's **first**
`UIViewControllerRepresentable` (the `MKMapView`/`UIViewRepresentable` `fullBleedMap`, T-0307, is the first *view*
representable). The `patterns-mobile` harvest records this so the claim cannot re-enter.

**D2 — the compression target = a PURE Core `ImageCompressor`: 1920px longest-side (aspect-preserved, never upscale)
+ JPEG 0.7 + `image/jpeg`, OFF the main thread.** Android ships **raw camera bytes** uncompressed
(`PhotosSection.kt:155-159`: `readBytes()` → `Base64.encodeToString(bytes, NO_WRAP)`, with a comment that base64 is
slow for multi-MB images), so a 3–8MB image is base64-inflated ~33% over the JSON body + held in memory. iOS **does
it right** — a 1920/0.7 JPEG is ~10–30× smaller, bounding the base64-over-JSON body + memory (the OOM risk on the
2017 floor). **Recorded Parity divergence** (changes pixels, not layout): *Android raw bytes → iOS 1920/0.7 downscale,
a deliberate perf divergence*. It is a **bounded pure helper** → strict TDD, **no optimizer pass** (the architect
ruling suffices; an optimizer pass is for an unbounded hot path, not a single deterministic transform).

**D3 — read-back via `orderGetPhotos → blobUrl` rendered with SwiftUI `AsyncImage`; the Complete gate trusts the
RE-FETCHED `OrderItem.hasAfterPhotos`.** Thumbnails use iOS-16 **`AsyncImage`** (the ADR-0018 D3 table's Coil
`SubcomposeAsyncImage` → `AsyncImage` row — same frame/aspect + loading/broken-image states, **no 3rd-party dep**;
`blobUrl` is a per-fetch SAS URL so disk-cache parity isn't load-bearing — Kingfisher stays the scoped fallback if a
future surface needs it). The Complete footer reads **`order.hasAfterPhotos == true`** (`OrderDetailScreen.kt:558`)
— the server-recomputed flag on the **re-fetched** order, kept live by the post-mutation parent refresh (the
`mutationVersion` → `onContentMutated` parity, `:133`); it does **NOT** short-circuit off
`GetOrderPhotosResponse.afterPhotoCount`. The `OrderPhotosViewModel` mirrors `OrderPhotosViewModel.kt` (sealed
`OrderPhotosUiState`; per-rail `isUploading`/`deletingId`; the parent-refresh bump). Upload = the batch-of-one
`orderSavePhotos`; delete = `orderDeletePhoto(photoId)`. Upload windows port verbatim: `canUploadBefore = status ∈
{_3 OnTheWay, _4 InProgress}`, `canUploadAfter = status == _4 InProgress`; terminal orders render read-only.

**D4 — the two `NS*UsageDescription` plist keys land IN-TICKET in the PARTNER `project.yml` `info.properties`;
Customer carries its own at T-0314.** `NSCameraUsageDescription` + `NSPhotoLibraryUsageDescription` are the same
mechanical XcodeGen `info.properties` add as the existing `API_BASE_URL`/`UIAppFonts` keys
(`CleansiaPartner/project.yml:43-51`) — **not** a deferred owner manual_step; localized ×5 via `InfoPlist.strings`,
describing the real use (AR-PRIV-4 / reviewer #10). ADR-0016 AR-PRIV-4 already anticipates these "for partner photos
(T-0308)" and requires they ship **with the capability, in-ticket** (Gate-AR). **Partner-only now** — pre-adding them
to the Customer `project.yml` declares a capability the Customer app doesn't yet exercise (an AR-PRIV-4
"no purpose string for a capability the app does NOT use" risk); T-0314 adds the Customer keys with its
dispute-evidence capture. The `PrivacyInfo.xcprivacy` photos data-type is declared (AR-PRIV-1). **The owner approves
the WORDING async (non-blocking).**

**Reviewer check added:** #32 (capture seam = Core `UIViewControllerRepresentable`; compression = pure
`ImageCompressor` 1920/0.7; read-back = `AsyncImage`; Complete gate = re-fetched `hasAfterPhotos` not `afterPhotoCount`;
plist in PARTNER `project.yml` in-ticket ×5). **New CRCs (T-0308):** `ios-camera-library-picker` (the first
`UIViewControllerRepresentable`; does NOT know the order/photo-type/upload contract, base64/compression, or who may
upload — SECURITY); `ios-image-compressor` (the pure 1920/0.7 transform; does NOT know base64/transport/threading
policy); `ios-order-photos-vm` (the `OrderPhotosViewModel.kt` parity; does NOT know the representable internals, who
owns the order — SECURITY, or how the Complete footer renders). **Recorded divergences:** camera-vs-gallery
(enhancement), 1920/0.7 (perf), Coil → `AsyncImage` (component). **Tests:** TC-IOS-IMG-COMPRESS (the pure helper),
TC-IOS-PHOTOS-GATE (re-fetched `hasAfterPhotos` flips `.completeBlocked`→`.complete`, NOT off `afterPhotoCount`),
TC-IOS-PHOTOS-UPLOAD (compress → base64 → batch-of-one `orderSavePhotos`).

### Partner earnings/invoices/PeriodPay — the `.invoices` tab in-tab stack, the Core `QuickLookPreview` PDF seam, `EarningsFormat`, reused dashboard stats (sprint-12 §7.12 / T-0309)

The partner **earnings / invoices / PeriodPay** surface — the Earnings summary, the invoices list + detail + PDF, and
the per-period PeriodPay rollup over the generated `PartnerEmployeePayrollAPI` (all four ops on the ADR-0019 spine) —
surfaced by the T-0309 Understand pass on `phase/ios-phase5` (depends_on T-0304✓). It replaces the partner shell's 3rd
tab placeholder (`PartnerShellView` `.invoices` = `PlaceholderTab(ticket:"T-0309")`) and wires the Dashboard's inert
`onOpenEarnings`. **The spec is already regen'd** (it carries `EmployeePayroll_{GetPagedInvoices,GetInvoiceById,
GetPeriodPays,DownloadInvoice}`) — **no owner codegen step**. **Four rulings, all APPLYING accepted ADRs + prior
records — no new ADR** (ADR-0020 + §7.7 D1 own the nav; the §7.10 D1 Core-seam precedent + ADR-0018 D2 own the PDF seam;
the §7.5 D4 / §7.7 D4 Core-utility precedent + the harvest rule own the format helper; ADR-0013 parity + Core/DRY own the
stats source). **The read-scoping / PII gate (own-id-only + the mandatory post-preview PDF cache-cleanup) is
SECURITY-ruled in parallel — sprint-12 §7.11 / `security/ios-earnings.md` (E1–E4) — not in this record.** Android source
mirrored: `partner-app/.../features/{earnings,invoices,payroll}/*.kt` + `data/{invoices,payroll}/*.kt` + nav
`PartnerNavHost.kt:160-289`.

**D1 (a) — the `.invoices` shell tab IS the surface: an in-tab `NavigationStack` over a typed `EarningsRoute` enum,
landing on the Earnings summary.** Android has **two** payroll surfaces — `NavRoute.Earnings` is a **pushed** screen
(`PartnerNavHost.kt:168,260-278`) and `NavRoute.Invoices` is a **bottom-nav tab** (`:279-289`), with Earnings→Invoices a
pop+tab-ordinal hop (`:264-275`). iOS collapses push+tab into the **single committed `.invoices` tab** (T-0304) rooting
an in-tab `NavigationStack` over `enum EarningsRoute { .summary; .invoices; .invoiceDetail(id); .periodPay(payPeriodId,
currencyCode) }` — the **ADR-0020 D4 / §7.7 D1 intra-audience push** (the root `PartnerRootView` enum stays the audience
selector). The tab root is **`.summary`** (Android built `EarningsSummaryScreen` *specifically* to avoid the empty-list
landing — `EarningsSummaryScreen.kt:56-66`); the list is a push off it. The Dashboard's `onOpenEarnings` sets
`ShellModel.selection = .invoices` (the `selectOrders()`/T-0304 `onOpenOrders` parity — a **tab switch, not a push**).
**Recorded Gate-DP divergence** (same class as the T-0304 `MainScaffold`→`TabView` swap): *Android push+tab → iOS single
tab + in-tab stack; same nav structure/content/back-stack order; mechanism is native; not layout/flow/branding.*
**Rejected:** landing the tab on the invoices list (reproduces the empty-landing UX Android removed); modeling the
surface as a pushed screen off the Dashboard tab (an ADR-0020 audience-vs-intra-audience confusion). Reviewer #33a.

**D2 (b) — invoice PDF viewing = a NEW Core `QuickLookPreview` seam (`QLPreviewController` `UIViewControllerRepresentable`
in `CleansiaCore/Components`); NO `FileDownload` seam.** The generated `employeePayrollDownloadInvoice` (a `format:
binary` response) is mapped by the swift5+urlsession generator to a **local file URL already written to the caches dir** —
so the VM holds the URL and surfaces it via a ONE-SHOT event (the §7.10 D3 one-shot, NOT a route); the screen presents
`QuickLookPreview` over it. This is the **second member of the §7.10 D1 `CameraOrLibraryPicker` family** (a system UIKit
controller behind a `CleansiaCore/Components` brand-skin seam, ADR-0018 D2) — **harvested to `patterns-mobile`** as the
canonical "preview a downloaded document" seam (the **customer app T-0314 reuses it** — so it MUST be Core, not
partner-local). The "Open PDF" affordance is **guarded on the DTO's `pdfGenerationFailed`** (the boolean on
`EmployeeInvoiceDto`/`EmployeeInvoiceDetailDto`) — iOS does it *better* than Android (which downloads unconditionally and
relies on a snackbar). **Rejected:** a partner-local representable (duplicated into the customer target); a share-sheet
(`UIActivityViewController` — an export, not an in-app viewer); `SafariView`/`SFSafariViewController` (web URLs, not a
`file://` PDF). **NO `FileDownload` seam** — the generated client IS the download; an orchestration seam would be dead
abstraction. **Recorded Gate-DP divergence:** *Android = VM streams `ResponseBody` → cache → `FileProvider` URI →
`Intent.ACTION_VIEW` (+ no-viewer fallback); iOS = codegen writes the body to disk → VM surfaces the local file URL →
Core `QuickLookPreview`; same in-app PDF viewing, native mechanism, no stream-to-cache/FileProvider/no-viewer branch.*
**SECURITY E4 (delete the PII-bearing PDF from cache on dismiss) is hosted by the `QuickLookPreview` coordinator** — this
record fixes the seam; §7.11 owns the cleanup mandate. Reviewer #33b.

**D3 (c) — money/date via a small Core `EarningsFormat`; do NOT overload `DashboardFormat.money`.** The Android earnings
surface uses **two grouped precisions**: `%,.0f` (whole) for the **earnings headline + breakdown**
(`EarningsSummaryScreen.kt:421`) and `%,.2f` (decimal) for **invoices + PeriodPay** (`InvoiceDetailScreen.kt:625` /
`InvoicesListScreen.kt:478` / PeriodPay). `DashboardFormat.money` is `%.0f` (whole, **un**grouped — the dashboard hero's
own contract) — **neither** earnings format, so overloading it would break or fork it. iOS introduces a Core
**`EarningsFormat`** (the §7.5 D4 `PasswordPolicy` / §7.7 D4 `AppSettingsStore` Core-utility factoring) carrying
`formatMoney` (`%,.2f`) + `formatMoneyWhole` (`%,.0f`) + ISO→local date helpers, reusing the **currency-symbol
resolution** (the `Currency.getInstance(code).getSymbol(Locale)` → raw-`code` fallback, duplicated verbatim across
Earnings + InvoiceDetail + InvoicesList — **≥3 call sites → HARVESTED to Core** as a `NumberFormatter(.currency)`/`Locale`
lookup with the never-crash code fallback). **PeriodPay's `currencyCode` comes from the nav route** (`EarningsRoute.periodPay`),
not the DTO — the `PeriodPaySummary` has no currency (`PeriodPayViewModel.kt:43-44`); a nil code degrades to the
symbol-less number. **Client-side display only** — server amounts/currency authoritative. **Rejected:** per-screen private
`formatMoney`/`currencySymbol`/`formatDate` copies (the Android copy-paste); overloading the dashboard helper. Reviewer #33c.

**D4 (d) — the Earnings summary REUSES `PartnerDashboardClient.getStats` (the `DashboardStatsDto` the Dashboard hero
renders); NOT a payroll-client duplicate / a `GetPeriodPays`-derived summary.** Exact Android parity:
`EarningsSummaryViewModel` injects `DashboardRepository` and calls `getStats(employeeId = null)`
(`EarningsSummaryViewModel.kt:9,23-32,49`) — *"same data the dashboard hero cards already render, just on its own
dedicated surface."* The iOS summary VM is a thin sealed `UiState<DashboardStatsDto>` over the one reused call.
Duplicating the fetch onto the payroll client, or deriving the summary from `GetPeriodPays`, would fork the
source-of-truth for the same numbers — a Core/DRY + parity violation. The `employeeId = null` own-stats read is
server-scoped (the partner-host `[Permission]`-guarded read T-0303 proved); **how the caller-own scope is trusted is
SECURITY §7.11 (E1), composing with the no-`UserProfileStore` fact (§7.5 D2 — the server derives the id, iOS does not
resolve it from a client store).** Reviewer #33d.

**Recorded Gate-DP/Parity divergences (all component/mechanism or "iOS does it right"):** push+tab→single-tab+stack nav
(D1); FileProvider/`ACTION_VIEW`→Core `QuickLookPreview` (D2); the Android `InvoicesListUiState` E1 flag-bag NOT
replicated → iOS sealed per-list `UiState`+`RefreshPhase` (the §7.9 (e) convention; staleness watermark PORTED; Android
fix → **T-0337**); Open-PDF gated on `pdfGenerationFailed` (iOS-does-it-right; Android catch-up = a PM follow-up); the
Android hand-written `PeriodPayApi` Retrofit → iOS GENERATED `employeePayrollGetPeriodPays` (the spec now carries it;
Android catch-up = a PM follow-up). **New CRCs (T-0309):** `ios-quicklook-preview` (the Core `QLPreviewController`
representable — the 2nd §7.10 D1 family member; its coordinator hosts SECURITY's E4; does NOT know the payroll contract /
who may read — SECURITY); `ios-earnings-format` (the pure money/date Core helper; does NOT overload `DashboardFormat.money`);
`ios-earnings-summary-vm` (reuses `PartnerDashboardClient.getStats`; does NOT touch the payroll client / `GetPeriodPays`);
`ios-invoices-vm` (list+detail; surfaces the download URL one-shot, gated on `pdfGenerationFailed`); `ios-periodpay-vm`
(the rollup; formats with the route-threaded `currencyCode`). **Reviewer check added:** #33 (nav / PDF seam / format /
stats source). **Tests:** TC-IOS-EARNINGS-NAV, TC-IOS-PDF-GATE, TC-IOS-EARNINGS-FORMAT (pure), TC-IOS-EARNINGS-STATS
(+ SECURITY §7.11's TC-IOS-EARNINGS-OWNERSHIP + the E4 cleanup test compose with TC-IOS-PDF-GATE).

### Partner APNs push registration — the `PushRegistrar` Core seam, the `@UIApplicationDelegateAdaptor` hook, the session-driven `PushSessionObserver`, the logout-ordered unregister (sprint-12 §7.13 / T-0311)

The partner **APNs push registration** surface — register → an APNs token → the **same `/api/Device/*` contract** the
Android `:core` push uses (`Platform="ios"`, the one `X-Device-Id`) + register-on-login / clear-on-logout, plus a minimal
foreground-banner/tap — surfaced by the T-0311 Understand pass on `phase/ios-phase5` (depends_on T-0302/0303/0310/0331). The
generated `PartnerDeviceAPI` (`deviceRegister(RegisterDeviceCommand{deviceId,deviceToken,platform})`/`deviceUnregister(deviceId)`)
rides the **ADR-0019** spine — T-0311 writes no auth code. **Three rulings, all APPLYING accepted ADRs/records — no new ADR**
(ADR-0013 D8 is the push divergence; ADR-0014 D6′ + ADR-0018 D2 own the Core-seam family; ADR-0019 owns the device-call
transport; the `SessionScopedCacheRegistry` owns the clear). **The registration / logout-clear-ordering SECURITY gate is
ruled in PARALLEL (Gate-SEC) — out of scope here; this record fixes the seam + names WHERE `unregisterDevice()` is invoked
so the ordering has a home.** Android source mirrored: `core/.../notifications/{PushTokenRepository,PushTokenSessionObserver,
DeviceRegistrationClient,PushTokenDataStore}.kt` + `partner-app/.../data/auth/AuthRepository.kt:210-231`.

- **(a) The `PushRegistrar` Core seam + the `@UIApplicationDelegateAdaptor` hook.** A `PushRegistrar` protocol in
  `CleansiaCore/Push` is the **SOLE** consumer of `UNUserNotificationCenter` + `UIApplication.registerForRemoteNotifications`
  — feature/lifecycle code imports **neither** `UserNotifications` **nor** `UIKit` (the seam-family precedent:
  `LocationProvider`/`MapProvider`/`GeocodingService` behind ADR-0014 D6′, `CameraOrLibraryPicker`/`QuickLookPreview` behind
  ADR-0018 D2). It exposes **`requestAuthorization`** (the `POST_NOTIFICATIONS` parity), **`registerForRemoteNotifications`**
  (main-actor), and an **APNs-token stream the AppDelegate feeds** — the structural parity to
  `PushTokenRepository.fcmToken: StateFlow<String?>` (`PushTokenRepository.kt:55`), a hot stream fed out-of-band by the OS
  callback so the registrar never juggles the AppDelegate directly. The APNs-token AppDelegate callbacks are received via a
  **per-app `@UIApplicationDelegateAdaptor`** (the canonical SwiftUI AppDelegate bridge — SwiftUI's `App` exposes no
  `didRegisterForRemoteNotifications` hook; available on the iOS-16 floor) that **only forwards** into the Core registrar.
  Rejected: a bare SwiftUI hook (no such callback exists in `App`/`Scene`); a hand-rolled `UIApplication.shared.delegate`
  (fights the App-lifecycle). The registrar/observer live in Core (shared with the future customer wave); the AppDelegate is
  per-app (the composition-root parity, like installing the `RequestBuilderFactory`/`MapProvider`).
- **(b) The lifecycle-wiring home: a Core `PushSessionObserver`; `unregisterDevice()` from `AuthApiClient.logout()` BEFORE
  the token wipe.** Registration is a **PROPERTY of session×token state, not an event** — the
  `combine(session, token).filterNotNull().distinctUntilChanged() → ensureRegistered` shape verbatim
  (`PushTokenSessionObserver.kt:56-64`), attached once from the App (the `MainActivity.onCreate` parity). This is the exact
  rewrite the Android `:core` did to kill "device wasn't registered" bugs (`PushTokenSessionObserver.kt:12-40`); hooking
  iOS's `afterLogin` would re-introduce that brittleness and miss cold-start-into-an-authed-session (the SplashGate
  re-enters an authed session with **no** `afterLogin`) and token-arrives-after-login. `ensureRegistered` short-circuits on
  the persisted last-registered token (`UserDefaults`, not Keychain — the `PushTokenDataStore` parity; not a secret) and
  persists on success only. **`unregisterDevice()` is invoked from `AuthApiClient.logout()` BEFORE the `TokenStore` wipe**
  (best-effort; the `Device/Unregister` DELETE needs the Bearer — the `AuthRepository.kt:210-225` ordering); the local
  `clear()` is the **`SessionScopedCache`** the registrar's store implements, run by the registry on **both** the user-logout
  and the forced-401 sign-out paths (the `PushTokenRepository.kt:44,65-67` local-only `clear()` parity). **SECURITY rules the
  ordering GATE in parallel; this record only fixes that the invocation home is `logout()` (before the wipe) + the
  `SessionScopedCache` (local).** Rejected: the `afterLogin` + logout-call event hooks (the deleted Android brittleness;
  thickens the App; forks two clear-paths).
- **(c) Minimal foreground/tap; no plist key; skip the rationale; no `UiState`.** Ship a minimal
  `UNUserNotificationCenterDelegate` — `willPresent` (foreground banner) + `didReceive` (tap → the EXISTING order route via a
  thin `PartnerNotificationDeepLink` port). **DEFERRED → T-0336:** the in-app feed, persistence, the dashboard bell badge,
  title/body templates, channels (the §7.7 separate-spike precedent). **No Info.plist purpose string** — APNs requires only
  the **`aps-environment` entitlement** + the runtime `requestAuthorization` (the OS shows its own alert; notifications has
  no plist key, unlike location/camera/photo). **Skip the rationale string** for strict parity (Android requests
  `POST_NOTIFICATIONS` silently); the one optional soft-ask `.xcstrings` key ×5 is the recorded, un-built fallback. **No
  `UiState`/`ActionState`** — fire-and-forget background plumbing; the sealed-state **absence is correct** (the §7.6 D3
  AddressPicker precedent — neither an E1 load-fetch nor an E2 mutation screen).
- **Recorded Gate-DP divergence (ADR-0013 D8):** *Android FCM (`FirebaseMessaging` token + `onNewToken`) → iOS APNs
  (`registerForRemoteNotifications` + the `@UIApplicationDelegateAdaptor` `didRegister…DeviceToken` + `UNUserNotificationCenter`);
  the SAME `Device/Register`/`Device/Unregister` contract, `Platform="ios"`, the one `X-Device-Id`; the mechanism is the
  native platform push transport, the contract + register/clear lifecycle are identical.* (No Firebase-project-migration
  analogue — FCM-specific, correctly not ported.)
- **Owner gate — T-0342 (the end-to-end-delivery gate).** Delivery (a push arriving on a device) needs the owner's APNs
  `.p8` key + the Push capability + provisioning — filed as **T-0342** *(NOT "T-0341", which is the backend status-history
  flaky-test ticket)*. T-0311 ships **code-complete + the `aps-environment` entitlement**; delivery is owner-gated — the
  **T-0325-gates-T-0335** pattern (the location plist key gating the my-location FAB). T-0311's reviewer/security gates verify
  the code seam + the entitlement, not a live push.
- **New CRCs (T-0311):** `ios-push-registrar` (the Core `UNUserNotificationCenter`/`registerForRemoteNotifications` sole
  consumer; APNs-token stream + `ensureRegistered`/`unregisterDevice` over the ADR-0019 spine; does NOT know the access token
  [the spine's], session presence [the observer's], the tap route [the port's], or templates [T-0336]); `ios-push-session-observer`
  (the `combine(session,token)` → `ensureRegistered`; does NOT know APNs/the backend or the logout ordering);
  `ios-push-app-delegate` (the per-app `@UIApplicationDelegateAdaptor`; forwards OS callbacks only; the one allowed
  `UIKit`/`UserNotifications` touch-point); `ios-partner-notification-deep-link` (tap→existing-order-route resolver).
  **Reviewer check added:** #34 (seam / lifecycle / scope-permission). **Tests:** TC-IOS-PUSH-REGISTER, TC-IOS-PUSH-OBSERVER,
  TC-IOS-PUSH-LOGOUT-ORDER (the seam half; the security gate is §7.x SECURITY's), TC-IOS-PUSH-TAP — red-first; no live-delivery
  test (the T-0342 owner-gated proof).

### The iOS-16 shell crash + shell design-parity restructure — ONE shell stack, page-pager, pill bar (phase/ios-fix1, recorded 2026-07-02, **ADR-0022**)

The first on-device pass (the owner's iOS 16 iPhone — the ADR-0014 floor) surfaced a **fatal navigation defect
class + the un-ported shell chrome**. Root cause (empirically reproduced on the iOS 16.4 sim): the outer pathless
`NavigationStack` at `CustomerRootView.swift:17` / `PartnerRootView.swift:17` wrapping the shells' sibling
typed-path `NavigationStack`s — illegal nesting iOS 17+ tolerates and iOS 16 punishes (`comparisonTypeMismatch`
crash on the Plus pushes; yellow-⚠️ missing-destination placeholder pages on Profile pushes). **Four rulings,
ADR-0022 carries the trade-off** (it supersedes the sprint-12 §7.15 D6 shell mapping and corrects the ADR-0018 D3
bottom-bar row — see the amended table above):

- **R1 (customer) — the RESTRUCTURE is approved, not the minimal fix.** ONE shell-level
  `NavigationStack(path:)` owning ALL child routes: the four route enums merge into **one `ShellRoute` enum**
  (deduping `subscribePlus` + order-detail, registered once via `.navigationDestination(for: ShellRoute.self)`);
  the path is a **type-erased `NavigationPath`** (retires all three iOS-16 hazards: sibling typed paths,
  multi-element sets `CustomerShellView.swift:212,333`, `isPresented` mixing `OrderDetailView.swift:53`); the 4
  tab ROOTS (only) sit in `TabView(selection:)` + `.tabViewStyle(.page(indexDisplayMode: .never))` (swipe
  parity); the `CustomerBottomBar` pill/FAB composite (`CustomBottomBar` + `BookFab` port: 64pt pill, 16pt
  margins, radius 32, outline-variant stroke, animated `NavSlot` dots, 72pt center gap, FAB top-center offset
  −12) mounts via **`.safeAreaInset(edge: .bottom)`** — the 88pt-clearance contract; the ad-hoc 40pt spacers +
  the `.overlay`/`offset(y:-28)` FAB are deleted; the outer root stack is deleted. Pushed children **cover the
  whole shell** (bar hidden on push — the Android NavHost-above-shell parity); one back stack, exactly like
  Android (the per-tab-stack loss is conceded and priced in ADR-0022). ADR-0020 (root enum) + ADR-0021 (booking
  stays a modal `.sheet` off the shell root) are **unchanged**. Tab roots stop driving nav-bar chrome
  (`ProfileTab.swift:44`'s `.navigationTitle` → in-content header).
- **R2 (partner) — minimal crash fix ONLY this phase:** delete the outer stack + convert the four typed paths
  (`OrdersListView.swift:35`, `EarningsView.swift:26`, `ProfileView.swift:47`, `RegistrationLockView.swift:38`)
  to `NavigationPath`, topology otherwise as recorded (§7.7 D1 / §7.12 D1). Partner pill/pager parity (Android
  partner IS a `HorizontalPager` + `FloatingIslandBottomBar` — 4 even slots, no FAB) = a **PM-filed follow-up**
  adopting the ADR-0022 shape; the pill is harvested to Core at that second call site, not before (§7.6 D1
  anti-speculation). Until then the partner's stock bar is a *recorded* interim divergence.
- **R3 (D3 table) —** the "bottom `NavigationBar` → `TabView`" row was stale (neither Android shell is a Material
  `NavigationBar`); corrected in the table above. Classification: the floating pill + center FAB is the apps'
  shared **brand signature** (per the `FloatingIslandBottomBar.kt` comment) — ADR-0018 *branding*, non-negotiable
  parity, never an "iOS-wins" component swap. §7.15 D6's contrary call is superseded by ADR-0022.
- **R4 (Gate-DP hardening) —** §G gains **AR-DP-1a** (Android-asset → asset-catalog parity; SF-symbol
  substitution ONLY for Material icon vectors, never brand raster art) + **AR-DP-4** (one-time per-app app-chrome:
  AppIcon + branded `UILaunchScreen` + in-app splash, owned by the shell/scaffold ticket); AR-DP-3's canonical
  mapping list drops "Compose bottom-nav → `TabView`". These close the two gate-miss classes the diagnosis proved
  (everything living in Android `res/` and app-level packaging escaped the per-`.kt`-screen citation unit).

**Slice-A implementation names (the dev contract):** `ShellRoute` (one `Hashable` enum: `.orderDetail(String)`,
`.subscribePlus`, `.membershipSuccess`, `.recurringList`, `.createRecurring(orderId: String?)`,
`.rewardsActivity`, `.disputes`, `.createDispute(orderId: String?)`, `.disputeDetail(String)`, `.addresses`,
`.editProfile`, `.devices`, `.notifications`, `.security`, `.language`, `.appearance`, `.help`,
`.deleteAccount`); `CustomerShellModel` keeps `@Published var selection: CustomerShellTab` (the pager binding —
pill taps set it inside `withAnimation`, the `animateScrollToPage` parity) + gains
`@Published var path = NavigationPath()` (replacing the four arrays; `openOrder` = select `.orders` +
`path = NavigationPath([ShellRoute.orderDetail(id)])`); `CustomerBottomBar(selection:onSelect:onBook:)` is the
app-local composite. **Verify on an iOS 16.x device:** Plus from Home + from Profile, order detail, dispute
create→detail, membership success, order photos, tab swipe, bar-hidden-on-push.

**CRC updates (ADR-0022):** `ios-customer-shell` — `CustomerShellView` + `CustomerShellModel`: *responsibility:*
own the tab selection, the ONE shell `NavigationPath`, and the booking-sheet presentation; *collaborators:*
`ShellRoute` destinations, `CustomerBottomBar`, `BookingSheetView`, the repositories it prefetches; *does NOT
know:* the audience root (CustomerRootView's), auth/session mutation, how child screens render, MapKit/Stripe.
`ios-customer-bottom-bar` — the pill/FAB composite: *responsibility:* render selection + emit
`onSelect`/`onBook`; *does NOT know:* navigation paths, routes, or any business data (a bar that appends to the
path has the wrong responsibility). **Reviewer check #35** = ADR-0022 §"How a reviewer verifies compliance".

- **Deployment target: iOS 16 vs iOS 17 (ADR-0014).** Chose **iOS 16** — the owner prioritised old-device
  reach (iPhone 8/8 Plus/X, 2017+), which iOS 17 (XS/XR, 2018+) excluded. The cost is the state mechanism
  (`@Observable` is iOS-17-only) and a couple of MapKit API variants; both are accepted trade-offs, recorded
  below. iOS 15-or-lower was rejected (loses `NavigationStack`); iOS 16 is the sweet spot (2017 phones +
  `NavigationStack`).
- **State mechanism: `ObservableObject`/`@Published` vs `@Observable` (ADR-0014 D2′).** Forced by the iOS-16
  floor. The sealed `UiState`/`ActionState` **enums and the facade/state parity are unchanged** — only the
  observation *wrapper* changes (`@Published` + `@StateObject`/`@ObservedObject` instead of the macro). The
  accepted cost: more boilerplate (`@Published` per property, the `@StateObject`-vs-`@ObservedObject`
  foot-gun) and whole-view (not per-property) invalidation — ergonomics/minor-perf, not architecture. A
  future iOS-17 bump can adopt `@Observable` VM-by-VM with no change to the enums, views, or parity.
- **Maps: MapKit vs Mapbox (the biggest cross-app call).** Chose MapKit-by-default-behind-a-protocol.
  Parity is of behavior/product, not vendor; iOS has a first-party map Android lacks. Mapbox app-wide =
  paid SDK + token rotation (an owner ops burden) + worse battery for capability iOS already has. The
  `MapProvider` seam makes a forced parity gap (custom style, polygon overlay) migrate **one provider, not
  the app**. Open input that would flip the default: a hard brand requirement for a Mapbox-identical map
  (Q-IOS-02, non-blocking).
- **Lead app: partner vs customer.** Chose partner. Its first authed screen (Dashboard) is **read-only** and
  proves auth/session/headers/codegen/state with **zero** Mapbox/Stripe/Google/photo deps; the customer
  primary flow IS the hardest feature. The honest cost: customer is the **richer `:core` reference** — so
  `CleansiaCore` is **designed from the customer app's mature shape** (D9.1), and partner's app-local
  network duplication is **not** ported. Lead app (proving vertical) ≠ the only input to the shared package.
- **Codegen auth vs hand-written.** Hand-written, for the same three reasons Android hand-writes
  `AuthApi.kt`: no-Bearer-on-anon, the empty-token gate, single-use refresh.
- **Codegen now vs after the owner regen.** After. The committed specs are pre-T-0272 (wrong login schema,
  no `trustedDeviceToken`, leaked `requiredProfile`/`requiredAudience`, **missing** Device-revoke + payroll
  endpoints). The regen is the **one hard blocker**; the foundation that doesn't need the client starts on
  approval.
- **DI framework vs hand-wired.** Hand-wired composition root; the only Hilt subtlety that mattered (the
  refresh-client cycle) is the separate-no-auth-session boundary Android already draws.
- **trusted-device now vs omit.** Omit v1 (no Android reference, optional field; an iOS-only build diverges
  a security path). If wanted, design once for all mobile clients, ship Android + iOS together (Q-IOS-03).

## Current rollout state

| Step | Phase | State (2026-06-23) |
|---|---|---|
| ADR (architecture + strategy) | — | **accepted** (ADR-0013) |
| ADR (iOS-16 floor + `ObservableObject` state + iOS-16 MapKit variant) | — | **accepted** (ADR-0014, partially supersedes ADR-0013 D2 + target) |
| ADR (generated client authenticates via the Core-spine-backed `RequestBuilderFactory`) | — | **accepted** (ADR-0019, refines ADR-0013 D4/D5 — the one way; reviewer #13-gen) |
| ADR (partner router = flat-enum `PartnerRootView` root-switch gated by `.splash`) | — | **accepted** (ADR-0020, refines ADR-0013 D2/D9 — the canonical partner router; reviewer #23; T-0304 builds it) |
| ADR (partner OrderDetail's non-modal 3-snap sheet = a custom `SnapSheet` Core container on the **16.0 floor**; the floor stays 16.0; native `.presentationDetents` = the MODAL-sheet way) | — | **accepted** (ADR-0021, extends ADR-0014 D6′ + refines ADR-0018 D3 — the one way iOS does a non-modal map sheet; reviewer #29; T-0307 Slice C builds it) |
| Partner auth completeness rulings (settings store / ConfirmEmail email via Route assoc-value / PasswordPolicy / PUT+empty-token / F1) | — | **recorded** (sprint-12 §7.5, **no new ADR** — applies ADR-0013/0019/0020 + the header-parity-contract; reviewer #25/#26; T-0305 builds it) |
| Map-seam rulings (minimal `MapProvider` picker factory + additive-later / current-location DEFERRED to T-0310+T-0325 / geocoding best-effort + no-`UiState` / no-Mapbox-token security) | — | **recorded** (sprint-12 §7.6, **no new ADR** — applies ADR-0013 D6 + ADR-0014 D6′ + ADR-0018 Gate-DP; reviewer #27; T-0306 builds it) |
| Partner Profile-tab rulings (in-tab `NavigationStack` over `ProfileRoute` / **lock-owns-its-own-stack** pushing the shared section set, fail-closed / `ServiceAreaRow` DEFERRED→T-0334 / `AppSettingsStore` extended + theme honored / born sealed-state, Android E1 NOT copied→T-0337 / current-location DEFERRED→T-0335 / Notifications DROPPED→T-0336) | — | **recorded** (sprint-12 §7.7, **no new ADR** — applies ADR-0020 + §7.5 D1 + §7.6 D2 + ADR-0018 Gate-DP + the Parity rule; reviewer #28; T-0310 builds it; device-id/revoke gate decisions 6–8 = SECURITY) |
| Partner order work-loop rulings (additive `fullBleedMap(coordinate:)` single-pin no-polygon / the non-modal `SnapSheet` 16.0-floor sheet = **ADR-0021** / the pure shared `OrderPrimaryAction.action(…)` machine / the T-0308 photo precursor seam / sealed per-pane `UiState`+`RefreshPhase` + **PORTED** staleness cache, Android E1 NOT copied→T-0337 / Code→OrderStatus one-mapper convention / SlideToCommit→native + no-polygon Gate-DP divergences) | — | **recorded** (sprint-12 §7.9, **+ ADR-0021** for the sheet — the other four apply ADR-0013 D6/D9 + ADR-0014 D2′/D6′ + §7.6 D1 + §7.7 D5 + the Parity rule; reviewer #29/#30/#31; T-0307 builds it; the **order-action ownership gate = SECURITY** §7.8) |
| Partner order PHOTOS rulings (capture seam = the Core **`CameraOrLibraryPicker` `UIViewControllerRepresentable`** — the repo's FIRST, the canonical UIKit-controller-behind-a-SwiftUI-seam idiom / **`ImageCompressor`** 1920px+JPEG-0.7 pure helper / read-back via SwiftUI **`AsyncImage`** + Complete gate trusts the **re-fetched `OrderItem.hasAfterPhotos`** / the two `NS*UsageDescription` keys in the PARTNER `project.yml` in-ticket ×5, Customer→T-0314; camera-vs-gallery + 1920/0.7 + Coil→`AsyncImage` Gate-DP/Parity divergences) | — | **recorded** (sprint-12 §7.10, **no new ADR** — applies ADR-0018 D2/D3 + ADR-0016 AR-PRIV-4 + ADR-0013 parity + the Parity rule; reviewer #32; T-0308 builds it; the **photo-upload ownership / EXIF gate = SECURITY** `security/ios-orders.md`). **Catalog correction:** the AddressPicker is pure MapKit/SwiftUI — it is NOT a `UIViewControllerRepresentable` precedent (the false claim is guarded against in `patterns-mobile`) |
| Partner earnings/invoices/PeriodPay rulings (the `.invoices` shell tab roots an in-tab `NavigationStack` over a typed `EarningsRoute` enum landing on the Earnings **summary**, `onOpenEarnings` = a tab switch not a push / invoice PDF via the new Core **`QuickLookPreview`** seam — the 2nd §7.10 D1 UIKit-controller-behind-a-seam family member, reused by customer T-0314 — guarded on `pdfGenerationFailed`, **no `FileDownload` seam** / a small Core **`EarningsFormat`** (`%,.2f` + `%,.0f` + ISO dates), `DashboardFormat.money` NOT overloaded, currency-symbol harvested to Core / the Earnings summary **REUSES** `PartnerDashboardClient.getStats`; push+tab→single-tab+stack, FileProvider→QuickLook, Android E1 invoices flag-bag NOT replicated→T-0337, hand-written PeriodPay Retrofit→generated divergences) | — | **recorded** (sprint-12 §7.12, **no new ADR** — applies ADR-0020 + §7.7 D1 + the §7.10 D1 Core-seam precedent + ADR-0018 D2/Gate-DP + the §7.5 D4/§7.7 D4 Core-utility precedent + ADR-0013 parity + Core/DRY; reviewer #33; T-0309 builds it; the **read-scoping / PII gate + the post-preview PDF cache-cleanup = SECURITY** sprint-12 §7.11 / `security/ios-earnings.md`) |
| Partner APNs push registration rulings (a Core **`PushRegistrar`** seam — the SOLE `UNUserNotificationCenter`/`registerForRemoteNotifications` consumer, the next ADR-0014 D6′/ADR-0018 D2 seam-family member — fed by a per-app **`@UIApplicationDelegateAdaptor`** / a Core **`PushSessionObserver`** = the `PushTokenSessionObserver.kt` combine-parity, `unregisterDevice()` from `AuthApiClient.logout()` BEFORE the token wipe + local `clear()` via the `SessionScopedCacheRegistry` / minimal `willPresent`+`didReceive`-tap, in-app feed→T-0336, **no plist key** (only the `aps-environment` entitlement), skip the rationale, **no `UiState`**; the FCM→APNs over the same `Device/*` contract `Platform="ios"` Gate-DP divergence) | 5 | **recorded** (sprint-12 §7.13, **no new ADR** — applies ADR-0013 D8 + ADR-0014 D6′ + ADR-0018 D2 + ADR-0019 + the `SessionScopedCacheRegistry`; reviewer #34; T-0311 builds it; the **registration / logout-clear-ordering gate = SECURITY** Gate-SEC). **Delivery owner-gated → T-0342** (APNs `.p8` + Push capability/provisioning — the T-0325-gates-T-0335 pattern; T-0311 ships code-complete + the entitlement) |
| ADR (iOS shell = ONE shell-level `NavigationStack` + `.page` tab pager + custom pill-bar/FAB composite; outer root stacks DELETED both apps; partner this phase = minimal crash fix, pill/pager parity = PM-filed follow-up; supersedes §7.15 D6's pill→`TabView` swap + corrects the ADR-0018 D3 bottom-bar row; Gate-DP gains AR-DP-1a + AR-DP-4) | — | **accepted** (ADR-0022, 2026-07-02 — the phase/ios-fix1 owner-device iOS-16 defect ruling; reviewer #35) |
| Owner Q-IOS-01 (deployment target) | — | **ANSWERED — iOS 16** (old-device reach) |
| Owner **mobile-spec regen** (the one hard blocker) | pre-Phase-2 | **PENDING — owner-only** (`manual_step: mobile-spec-regen`) |
| Workspace + `CleansiaCore` skeleton + design tokens + DI + snackbar/error center | 0 | planned (runnable on approval) |
| Auth/session/header middleware (hand-written) | 0 | planned (runnable on approval) |
| Swift codegen toolchain wired | 0 | planned — **held on the regen** for first real generation |
| Partner lead vertical: login → Dashboard (read-only) | 1 | planned — needs the generated partner client (held on regen) |
| Parity feature waves (ordered by complexity; 3 hard areas) | 2+ | planned |

## Open questions / future evolution

- **Q-IOS-01** (deployment target) — **ANSWERED: iOS 16** (ADR-0014; old-device reach iPhone 8/X 2017+).
  Forced `@Observable` → `ObservableObject` (D2′) + the iOS-16 MapKit variant (D6′); no longer open.
- **Q-IOS-02** (Mapbox brand requirement) — default **No** → MapKit. A "yes" flips the default provider
  behind the unchanged `MapProvider` seam.
- **Q-IOS-03** (trusted-device for mobile) — default **omit v1**; if adopted, one design across Android + iOS.
- **Future iOS-17 floor bump** — adopt `@Observable` + the SwiftUI MapKit API; mechanical per ADR-0014 D2′
  (enums + facade + views' switch unchanged). A new ADR if/when taken; not planned now.
- **Mapbox provider** — if a parity gap forces it, add a `MapboxMapProvider` behind `MapProvider` (a
  living-doc note + a scoped dependency, not an ADR — unless it changes the default app-wide, then supersede).
- **iOS-bespoke `ApiResult`** — allowed only if a future need demands extra combinators, shape-compatible
  with ADR-0011 D4's `ApiError` case set (recorded against ADR-0011, not forked).
- When the first iOS generated client is produced, confirm the Swift `ApiError` case set still matches the
  Kotlin one one-to-one (ADR-0011 invariant #2 — a per-client case is drift).

## Apple App Review compliance + the quality bar (ADR-0016, accepted 2026-06-23)

> Companion section for **ADR-0016** (`agents/backlog/adr/0016-apple-app-review-compliance-and-ios-quality-bar.md`).
> The iOS apps are held to a **submission-passing** bar (higher than the rest of the platform). Tickets
> T-0323…T-0329 in `status/sprint-12.md §10`; the pre-submission audit artifact is
> `agents/backlog/ios-app-review-checklist.md`.

**The myth, corrected (so it never re-enters the record):** there is **NO "AI-written-code detector"** in App
Review and review **cannot brick/disable hardware** — both **FALSE**. The real risk is **rejection vs the
published App Store Review Guidelines** + account-level consequences for **concealed functionality / abuse**.
The bar is engineered for the **knowable, reviewer-verifiable** checklist.

**The bar in one table:**

| Area | The bar | Where verified |
|---|---|---|
| Code quality | **STRICT SwiftLint + SwiftFormat, BLOCKING** the iOS CI (`force_unwrapping`/`force_try`/`force_cast` = error; generated dir excluded) — the delta from FE's non-blocking lint (greenfield = no debt to grandfather) | T-0323; check #14 |
| Privacy manifest | `PrivacyInfo.xcprivacy` per target: required-reason APIs, data types, **tracking=false** | T-0324; check #15 |
| Tracking / ATT | **No tracking, no ATT prompt** — the apps operate the service, they don't track for ads | T-0324; check #15 |
| Purpose strings | location (MapKit pickers), camera + photo library (partner photos T-0308 + customer dispute T-0314), localized ×5; **push = `aps-environment` entitlement + runtime request, NOT an Info.plist key** (T-0311 ships the entitlement; the live-delivery `.p8`/capability/provisioning = owner T-0342 — sprint-12 §7.13) | T-0325; check #16 |
| Account deletion | **In-app (5.1.1(v))** — customer Settings → Delete reaches the `GdprDeletionService` account+data deletion | T-0327; check #17 |
| Sign in with Apple | **REQUIRED on the customer app (4.8)** because it offers Google Sign-In; partner app = none | T-0326; check #18; Q-IOS-04 (mechanism) |
| Payments | **External Stripe is ALLOWED, IAP NOT required (3.1.3/3.1.5)** — cleaning is a real-world service; documented so a reviewer doesn't wrongly demand IAP | T-0328; check #19 |
| Standard floor | no private API / no hidden feature / complete metadata + demo account / functional + crash-free / no placeholder | T-0329; check #20 |
| HIG / a11y | VoiceOver, Dynamic Type, contrast, hit targets, 5-locale completeness | per-ticket; check #20 |

**Gate-AR (continuous):** on **every** iOS ticket, the reviewer + ios charters check the blocking lint is
green, any new capability carries its **purpose string + manifest entry + locale strings in-ticket** (not
deferred), no hidden feature / private API / placeholder, and VoiceOver/Dynamic Type on new controls — so the
pre-submission audit **confirms** an already-compliant app rather than retrofitting one.

**Per-app difference (recorded so the bar isn't over-applied):** SIWA (4.8), in-app account deletion (5.1.1(v)),
and Google Sign-In are **customer-only**; the **partner** app has no social login and no account-creation-tied
delete obligation beyond the standard floor. Both apps carry the privacy-manifest / purpose-string / lint /
standard-floor items.

**The one open input:** **Q-IOS-04** (SIWA backend mechanism — likely a backend `appleauth` endpoint + a
spec-regen, mirroring `googleauth`). It gates **only** the SIWA ticket (T-0326); the rest of the iOS + the
compliance plan proceeds. Watch-items: `SubscribePlus` must stay a real-world-service benefit (not a digital
good → IAP); adding any tracking SDK flips AR-PRIV-3 (tracking + ATT become mandatory).
