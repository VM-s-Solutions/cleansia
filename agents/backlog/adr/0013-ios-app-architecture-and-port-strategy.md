# ADR-0013 — iOS app architecture & port strategy: a parity port of the Android customer + partner apps onto a shared Swift package, partner-first, hand-written auth/session/header middleware, MapKit-by-default

- **Status:** accepted   <!-- proposed | accepted | superseded | rejected -->
- **Date:** 2026-06-23
- **Supersedes:** —
- **Superseded by:** ADR-0014 **partially** (2026-06-23) — only **D2** (the view-model observation
  mechanism: `@Observable` → `ObservableObject`/`@Published`) and the **deployment-target assumption**
  (Q-IOS-01: iOS 17 → **iOS 16**, for old-device reach). **All other decisions here (D1, D3–D12) remain in
  force.** Read this ADR *with* ADR-0014's D2′/D6′ overrides applied. The immutable text below is left
  unedited per the ADR rule; the floor-affected parts are superseded, not deleted.
- **Refined by:** ADR-0018 (2026-06-23) — the **iOS design-parity principle** makes the *visual/UX* meaning
  of this ADR's "parity port" explicit and reviewable: same layout/flow/branding as the Android Compose apps,
  built with **native SwiftUI components**, and **iOS convention wins on a genuine component conflict** (a
  standing **Gate-DP** on every iOS screen ticket). A refinement of the parity *definition*, not a change to
  any architecture decision here.
- **Applies to:** ios | mobile (cross-client contract) | cross-cutting
- **Extends:** ADR-0011 (the born-canonical iOS `ApiResult<T> = Result<T, ApiError>` repository contract, D4 — this ADR consumes it, does not re-decide it). Mirrors the mobile API contract audit (security, 2026-06-22) and the Android parity map (analyst, 2026-06-22) as its evidence base.
- **Ticket:** IOS-ADR (this ADR) · **Consumers:** the Phase-0 foundation tickets (workspace + shared package + codegen toolchain + auth/session/header middleware + DI + error center), then the partner Phase-1 vertical, then the parity feature waves (see the ticketed plan in `status/sprint-12.md`).

> This ADR freezes the **architecture and the port strategy** for the iOS apps: the workspace/package
> layout, state management, DI, the load-bearing auth/session/header layer, the Swift codegen toolchain
> and its hard dependency on an owner spec-regen, the Mapbox-vs-MapKit decision, Stripe, push, the **lead
> app** call, the trusted-device scope, and i18n. It ships **no Swift code** — every concrete artifact is
> a consumer ticket in the Phase plan. iOS is a **parity port** of the existing Kotlin/Compose apps onto
> the **same Mobile API contract**; it does not invent product behavior. Once `accepted` it is immutable —
> supersede, never edit.

> **Owner decisions this ADR is built to (the owner's standing instructions for this planning):**
> (a) **PLAN FIRST** — no iOS code, no commits; this is the design + the proposed backlog.
> (b) **THE ARCHITECT DECIDES THE LEAD APP** — this ADR makes that call (D9: **partner-first**) and owns it.
> (c) The mobile-spec regen is **owner-only** (CLAUDE.md "Manual Steps") — flagged as the one hard blocker.

---

## Context

iOS is the next client. There is **zero iOS code today** — `Glob src/cleansia_ios/**` returns nothing —
so this is greenfield with no migration cost, and the cheapest possible moment to set the contracts (the
same logic ADR-0011 D4 used to fix the Swift `ApiResult` shape before any repo existed). Two read-only
investigations done 2026-06-22 are the evidence base and are not re-derived here: the **Mobile API
contract audit** (security) and the **Android parity map** (analyst). The decision is grounded in the real
Android `:core` auth/network stack, which iOS mirrors one-to-one.

### What iOS must mirror (the load-bearing facts, traced)

**Two mobile hosts, JWT-claim multi-tenancy, body-token auth.** Partner = `Cleansia.Web.Mobile` (:5002),
customer = `Cleansia.Web.Mobile.Customer`. Auth is **body-token, never cookie** (`csrfToken` is null on
mobile); login dispatches `MobileLogin` / `MobilePartnerLogin` (the *mobile* commands, not the web
`Login`/`PartnerLogin`). Multi-tenancy is driven by the `tenant_id` JWT claim — **no tenant header** — so
iOS gets tenant-scoping for free and never hand-rolls it (the Cleansia multi-tenancy seam holds for iOS
with zero new code). Refresh is **single-use with theft detection** (`RefreshTokenReused`): the client
**must replace its stored refresh token on every refresh** or it self-revokes. Unconfirmed-email login
returns **200 with an empty Token** (a special case the gate must handle).

**The Android auth stack iOS mirrors one-to-one** (all in `:core`, package `cz.cleansia.core.auth`):
- `AuthInterceptor.kt` — attaches `Authorization: Bearer <access>` **only on non-anon paths**; the
  anon allow-list is explicit (`AuthInterceptor.kt:62-69`: `login`, `register`, `refreshtoken`,
  `googleauth`, `confirmuseremail`, `resendconfirmationemail`). It also stamps `X-Device-Label` and
  `X-Device-Id` on **every** request.
- `DeviceIdProvider.kt:25-33` — **THE single source** of the stable per-install device id. The hard
  invariant (`:14-22`): the `X-Device-Id` header value **must equal** the `deviceId` sent to
  `/api/Device/Register`, because the server's remote-revoke match is `RefreshToken.DeviceId ==
  Device.DeviceId`. A second id source silently breaks remote session-kill. **No generated client
  captures this** — it is the invisible out-of-band contract.
- `X-Time-Zone` — a separate per-request interceptor (customer `AuthModule.kt:85-100`,
  `java.util.TimeZone.getDefault().id`, e.g. "Europe/Prague") on **both** the auth and no-auth clients;
  server-side day/week/month math depends on it.
- `AuthAuthenticator.kt:37-112` — the **single-flight 401-refresh-retry**: `synchronized(this)`
  serialises concurrent 401s, a second caller sees the already-refreshed token, refresh-token-expired or
  a rejected refresh emits `ForcedSignOutReason.SessionExpired` and clears every `SessionScopedCache`.
  It uses a **separate no-auth client** (`refreshClient`) so a 401 on refresh can't loop.
- `TokenStore.kt:41-110` — **EncryptedSharedPreferences** (Android Keystore-backed) holding
  access/refresh tokens + their expiries, exposed as a reactive flow.

**`trustedDeviceToken` is net-new for iOS with no Android reference.** It lives **only** on the two
mobile login commands (`MobileLogin`/`MobilePartnerLogin`), is **optional (= null)**, and **Android's
hand-written `LoginRequest` does NOT send it** (contract audit). So there is no Android behavior to port —
iOS would be *adding* a feature, not matching one.

**The codegen toolchain Android uses (the pattern iOS mirrors).** Android runs the **openapi-generator
gradle plugin** (`partner-app/build.gradle.kts:10` `alias(libs.plugins.openapi.generator)`;
`customer-app/build.gradle.kts` `openApiGenerate { generatorName.set("kotlin"); inputSpec.set(".../openapi/customer-mobile-api.json") }`) with `compileKotlin.dependsOn("openApiGenerate")` so the client is
regenerated from the committed on-disk spec on every build. **Auth is excluded from codegen and
hand-written** (Android's `AuthApi.kt`) precisely because of the no-Bearer-on-anon rule and the empty-token
special case. iOS must do the same: codegen the business clients, hand-write auth.

**The committed specs are STALE and are a hard blocker for codegen** (contract audit, the must-fix). The
on-disk specs (`src/cleansia_android/openapi/{partner,customer}-mobile-api.json`, last regen **2026-05-31**)
are **pre-T-0272**: the login schema is the *web* command, `trustedDeviceToken` is absent, refresh still
exposes the now-`[JsonIgnore]` `requiredProfile`/`requiredAudience`, and `Device/Mine` + `Device/{id}`
revoke + `EmployeePayroll/GetPeriodPays` are **missing**. **Regen is owner-only** (CLAUDE.md). iOS codegen
**must not run against the 2026-05-31 specs** — that is the one true blocker for everything downstream of
the foundation.

**Push is already iOS-ready.** `RegisterDevice` accepts `Platform "android"|"ios"`; iOS sends the APNs
token + `"ios"` to the existing `/api/Device/*` endpoints. No backend change.

**The three effort-dominating areas** (analyst): (1) the **customer booking wizard + Stripe PaymentSheet**
(the Bolt-style 3-step anchored sheet, cash→success vs card→Stripe, client-side pricing — the hardest
feature); (2) **Mapbox across BOTH apps** (partner `OrderDetail` full-bleed map + 3-snap bottom sheet, both
address pickers) — the single biggest cross-app decision; (3) the **partner order work-loop + photo upload
+ standing up the Swift codegen toolchain** (foundational, blocks all verticals). Photo upload is split:
**partner = JSON base64**, **customer dispute = multipart `IFormFile`**.

**The real partner order lifecycle is richer than CLAUDE.md.** There is an **`OnTheWay`** state between
`Confirmed` and `InProgress` (Take → NotifyOnTheWay → Start → Complete). **iOS mirrors the CODE, not the
6-state doc** (analyst note).

This is **one decision** — "the iOS app architecture & port strategy" — because the parts are inseparable:
the package layout determines where the auth/session/header middleware lives; the auth layer's no-Bearer
allow-list and `X-Device-Id` invariant are coupled to the codegen boundary (auth is hand-written *because*
of them); the lead-app call determines which vertical proves the architecture first; and the codegen
toolchain has a single hard blocker (the spec regen) that gates every feature wave. Splitting it would let
the package be decided without the auth layout, or the lead app chosen without the architecture it must
prove. The *implementation* is split into the Phase tickets.

---

## Decision

> **Contract principle.** iOS is a **parity port** of the Kotlin/Compose customer + partner apps onto the
> **same Mobile API contract**, structured as a **shared Swift package (`CleansiaCore`) + two app targets**
> in one Xcode workspace — the exact `:core` / `:partner-app` / `:customer-app` shape. State is
> **`@Observable` view models** translating the sealed `UiState`/`ActionState` one-to-one; DI is
> **plain initializer injection + a composition root** (no framework). The **auth/session/header layer is
> hand-written** and reproduces the Android contract exactly: a no-auth refresh client, a **single-flight
> 401-refresh** actor, a **Keychain** token store, and an out-of-band header layer enforcing
> **`X-Device-Id` == the `Device/Register` deviceId**, `X-Device-Label`, `X-Time-Zone`, and the
> **no-Bearer-on-anon allow-list**. Business clients are **codegen'd by openapi-generator (swift5 +
> URLSession)** from the **owner-regenerated** specs (the one hard blocker); auth is excluded from codegen.
> **Maps default to MapKit** (Apple-native, free, no SDK/token) with a documented fallback to the Mapbox
> iOS SDK only if a parity gap forces it. Stripe uses the **`stripe-ios` PaymentSheet**. The **lead app is
> PARTNER**. `trustedDeviceToken` is **omitted from iOS v1 to match Android**. i18n is the **5 locales via
> a String Catalog**.

### D1 — Workspace & package structure: one workspace, a shared SPM package, two app targets

Mirror the proven Android multi-module shape (`:core` + `:partner-app` + `:customer-app`) in Swift idiom:

```
src/cleansia_ios/
├── Cleansia.xcworkspace                 # the workspace (opens everything)
├── CleansiaCore/                        # a Swift Package (SPM) — the `:core` equivalent
│   ├── Package.swift
│   └── Sources/CleansiaCore/
│       ├── Network/      (ApiResult, ApiError — ADR-0011 D4; the generated-client host; SafeApiCall)
│       ├── Auth/         (TokenStore=Keychain, AuthClient, RefreshSession, AuthInterceptor/headers,
│       │                  SingleFlightRefresh actor, DeviceIdProvider, SessionManager, ForcedSignOut)
│       ├── DI/           (the composition root / AppContainer protocol)
│       ├── DesignSystem/ (design tokens: colors/spacing/shape/type — the SemanticColors/Spacing parity)
│       ├── Components/   (CleansiaButton/TextField/Dropdown/Dialog/… SwiftUI parity wrappers)
│       ├── Snackbar/     (the global SnackbarController bus + host)
│       ├── Location/     (the map abstraction seam — D6; geocoding; service-area)
│       ├── Push/         (APNs registration → DeviceRegistrationClient, the `:core` push-token parity)
│       ├── Format/       (Order/Dispute formatters — the hoisted-to-:core parity, T-0277/T-0278)
│       └── State/        (the UiState/ActionState protocols + helpers — D2)
├── CleansiaPartner/                     # the partner app target (bundle cz.cleansia.partner)
│   └── (Features/, generated client, app-local localizer, composition root wiring)
└── CleansiaCustomer/                    # the customer app target (bundle cz.cleansia.customer)
    └── (Features/, generated client, app-local localizer, composition root wiring)
```

- **SPM package for the shared code, Xcode app targets for the apps.** `CleansiaCore` is a **local SPM
  package** the two app targets depend on — the direct analogue of `implementation(project(":core"))`. SPM
  (not a framework target, not CocoaPods) is the modern Apple-native dependency mechanism, gives a clean
  compile-time module boundary (the same seam `:core` enforces), and lets `CleansiaCore` declare its own
  external deps (the generated client lib, Stripe, optionally Mapbox) in `Package.swift`.
- **The generated client lives per-app** (each app's `Features/.../Generated/`), exactly as Android
  generates `cz.cleansia.partner.api.*` / `cz.cleansia.customer.api.*` per app — the two hosts have
  different controller sets, so two generated clients. The **shared** network/auth/result types live in
  `CleansiaCore` (one definition, like ADR-0011's `:core` move).
- **The app-local error localizer stays per-app** (the `ApiErrorTranslator`/`ApiErrorParser` parity, E3 /
  ADR-0011 D2) — it depends on each app's `String Catalog`, so it is *not* in `CleansiaCore`.
- **Why not one app with two targets sharing a single feature tree, or a monorepo of separate packages
  per feature:** partner and customer are genuinely separate apps with separate bundle ids, separate
  store listings, and largely disjoint feature trees (the parity map confirms only `:core`-level overlap).
  A single shared feature tree would couple them; per-feature packages are SPM-wiring overhead at this size
  (the same reasoning ADR-0011 used to reject a `:network` module). The `:core`+2-apps shape is the one
  Android proved.

### D2 — State management: `@Observable` view models; sealed `UiState`/`ActionState` translated one-to-one

iOS uses **Observation (`@Observable`, iOS 17+) view models** as the MVVM parity of Android's
`ViewModel` + `StateFlow<UiState>`. The two Android state archetypes (E1/E2, the consistency catalog's
sealed states) translate to Swift enums **one-to-one**:

```swift
// E1 parity — the three explicit screen states (Loading/Error/Loaded), a Swift enum with an associated value.
enum UiState<T> { case loading; case error(ApiError); case loaded(T) }

// E2 parity — the one-shot action state (Idle/Submitting/Error). Surfaced as a published property
// the view observes; success is signalled via a one-shot effect (the SnackbarController bus / a
// transient published event), the SharedFlow analogue.
enum ActionState { case idle; case submitting; case error(ApiError) }
```

- A view model is `@Observable`, exposes a `private(set) var state: UiState<T>` and (for mutating screens)
  a `var action: ActionState`; the SwiftUI `View` switches over the enum to render the **three explicit
  data states** (the same rule the frontend catalog mandates). This is the direct, idiomatic parity of
  `StateFlow<UiState>` / `collectAsStateWithLifecycle()` — no third-party state library (no TCA, no
  Redux) is introduced, matching Android's "no extra state framework beyond the platform primitive."
- **Why `@Observable` over `ObservableObject`/`@Published`:** Observation is Apple's current recommendation
  (iOS 17+), gives finer-grained view updates, and is the closest semantic match to `StateFlow`'s
  single-source-of-truth-with-fine-grained-emission. (iOS 17 as the deployment floor is recorded as the
  one product-adjacent assumption — see Open questions Q-IOS-01; it is the same "modern platform floor"
  posture as Android's `minSdk 26`.)

### D3 — DI: plain initializer injection + a composition root (no Hilt analogue framework)

Replace Hilt with **constructor/initializer injection wired by a composition root** (an `AppContainer`
assembled at app launch), not a DI framework:

- Each view model and repository takes its collaborators as **initializer parameters** (protocols, so they
  are test-substitutable). A single `AppContainer` per app builds the object graph at launch — the
  composition-root pattern. This is the standard Swift answer to Hilt and avoids a heavy runtime DI
  dependency for an app of this size.
- **The one DI subtlety to preserve from Android: the refresh-client cycle.** Android breaks the cycle
  (the `AuthAuthenticator` needs the refresh client, which needs the `URLSession`/client the authenticator
  is installed into) with a lazy `refreshClient: () -> RefreshClient` provider. iOS reproduces this with a
  **separate no-auth `URLSession`/client** for refresh (D4) injected lazily — the same explicit boundary
  the `NoAuthOkHttp` qualifier draws on Android. The composition root wires it; no framework needed.
- **Handlers/VMs depend on a small number of collaborators** (the RDD rule) — a VM wiring 8 services is a
  smell to catch in review, same as backend.

### D4 — Auth/session/header layer (the load-bearing decision): hand-written, exact Android-contract parity

This is the part that breaks silently if it is approximated, so it is specified explicitly. iOS reproduces
the Android `:core` auth contract one-to-one, **hand-written** (excluded from codegen):

**D4.1 — Token store on the Keychain.** The `TokenStore` parity stores access/refresh tokens + their
expiries in the **iOS Keychain** (`kSecClassGenericPassword`, `kSecAttrAccessibleAfterFirstUnlock` so a
background refresh works), exposed as an `@Observable`/`AsyncStream` for session-loss reactivity. This is
the Keychain analogue of `EncryptedSharedPreferences` — Keychain is hardware-backed (Secure Enclave) and
is the canonical iOS secret store. **On every refresh the stored refresh token is replaced** (the
single-use/theft-detection contract — `RefreshTokenReused` self-revokes otherwise).

**D4.2 — Hand-written auth client + a separate no-auth refresh session.** Auth endpoints
(`/api/Auth/login` → `MobileLogin`/`MobilePartnerLogin`, register, refresh, confirm-email,
resend-confirmation, and customer Google-auth) are **hand-written** against `URLSession`, **not** codegen'd,
because (a) the no-Bearer-on-anon rule, (b) the empty-token unconfirmed-email special case (200 + empty
`Token` → route to the confirm-email gate, not a session), (c) refresh is single-use and the response shape
matters. The **refresh call uses a dedicated no-auth `URLSession`** (no auth header injection, no
401-interceptor) so a 401 on refresh cannot loop — the `NoAuthOkHttp` parity.

**D4.3 — Single-flight 401-refresh.** A **`actor SessionRefresher`** serialises concurrent 401s (the
Swift-concurrency equivalent of `synchronized(this)`): the first 401 triggers one refresh; concurrent
callers `await` the same in-flight `Task` and retry with the fresh token; a second caller whose request
carried the now-stale access token retries with the already-rotated token without a network call (the
`requestAccess != currentTokens.accessToken` short-circuit). A refresh-token-expired or server-rejected
refresh **emits `ForcedSignOut(.sessionExpired)`** via the `SessionManager` parity and **clears every
session-scoped cache** (the `SessionScopedCache` multibinding parity — a registry of caches the container
holds). This is implemented as a `URLSession` retry layer (a custom delegate or an explicit
"try → on 401 refresh-once → retry" wrapper in the `CleansiaCore` network layer), since `URLSession` has no
built-in `Authenticator` analogue — the behavior, not the mechanism, is the contract.

**D4.4 — The out-of-band header layer (the invisible contract no generated client captures).** A request
adapter in `CleansiaCore` stamps, on **every** request:
- **`X-Device-Id`** = the value from a **single** `DeviceIdProvider` (the `DeviceIdProvider.kt` parity).
  On iOS the stable per-install id is **`UIDevice.identifierForVendor` persisted to the Keychain on first
  launch** (so it survives reinstall within the vendor scope and never regenerates), and it is the **same**
  value sent as `deviceId` to `/api/Device/Register`. The invariant `X-Device-Id == Device/Register
  deviceId` is enforced by **resolving the id in exactly one place** — a second source is a blocking
  finding (the `DeviceIdProvider` "does NOT know two ids" CRC). Breaking it silently kills remote
  device-revoke.
- **`X-Device-Label`** = an ASCII-safe `"<model> - iOS <version>"` (HTTP header values reject non-ASCII —
  the same filter Android applies).
- **`X-Time-Zone`** = `TimeZone.current.identifier` (e.g. "Europe/Prague") — on **both** the auth and
  no-auth sessions (server date math depends on it).
- **The no-Bearer-on-anon allow-list** = the exact Android list (`login`, `register`, `refreshtoken`,
  `googleauth`, `confirmuseremail`, `resendconfirmationemail`) **plus** the customer host's additional
  `[AllowAnonymous]` endpoints the contract audit enumerated (Lookup / CreateOrder / Quote / Payment /
  GetPlans / Referral-Validate). The Bearer is attached **iff** the path is not on the allow-list and a
  non-expired access token is present. This allow-list is the **single source of "is this anonymous"** and
  is documented in the iOS header-parity spec (a Phase-0 deliverable) so the dev does not re-derive it.

**D4.5 — Multi-tenancy is free.** Because tenant scoping is JWT-claim-driven with **no tenant header**, iOS
sends nothing extra — the Cleansia multi-tenancy seam holds with zero iOS code. (Recorded so a future dev
does not invent a tenant header.)

### D5 — Swift API client codegen: openapi-generator (swift5 + URLSession), from the OWNER-REGENERATED specs

- **Generator: `openapi-generator`, `swift5` library, `urlsession` networking.** This mirrors the Android
  toolchain (the same generator family already in the repo, `generatorName=kotlin`) — one generator family
  across both mobile platforms, generating against the **same** mobile-API specs. `urlsession` (not
  Alamofire) keeps the dependency surface minimal and matches D4's `URLSession` choice. The generated
  client is **business endpoints only**; **auth is hand-written** (D4.2), excluding the auth paths from the
  generated surface exactly as Android does.
- **From WHICH specs — the hard blocker.** Codegen runs against
  `src/cleansia_android/openapi/{partner,customer}-mobile-api.json` (the canonical committed mobile specs,
  shared with Android — there is **one** spec per host, not an iOS copy). These specs are **stale**
  (2026-05-31, pre-T-0272). **The owner MUST regen both mobile specs before any iOS codegen runs.** This is
  flagged **`manual_step: mobile-spec-regen (owner-only)`** and is the **single true blocker** in the plan:
  the foundation tickets that don't depend on the generated client can start on approval; **every ticket
  that touches a generated client is BLOCKED on the regen.**
- **Drift discipline (mirror the web NSwag rule).** The generated Swift client is **never hand-edited**
  (the exact CLAUDE.md rule for the NSwag clients). Codegen is **wired into the Xcode build / an SPM plugin
  or a checked-in script** so it regenerates from the on-disk spec (the `dependsOn("openApiGenerate")`
  parity), and the iOS spec source is the **same** committed mobile spec the owner regenerates — so iOS and
  Android can never drift from each other or from the backend. A backend DTO/endpoint change → **one**
  owner spec-regen feeds web (NSwag), Android (openapi-generator kotlin), **and** iOS (openapi-generator
  swift5) — `manual_step: mobile-spec-regen` is the iOS analogue of `nswag-regen`.

### D6 — Maps: **MapKit by default**, Mapbox iOS SDK as a documented fallback only if parity forces it

This is the biggest cross-app cost driver (partner `OrderDetail` full-bleed map + 3-snap sheet, both
address pickers). **Decision: default to MapKit; isolate maps behind a `CleansiaCore` protocol so the
choice is reversible per-surface without touching features.**

- **MapKit (the default).** Apple-native `Map` (SwiftUI), `MKLocalSearch`/`CLGeocoder` for
  geocoding/reverse-geocoding, `MKMapView` for the full-bleed partner map. **Zero SDK dependency, zero map
  token, zero per-render cost, best battery/perf, native look.** The Android apps use Mapbox because
  Android has no first-party map of MapKit's quality; **iOS does**, so the Android Mapbox choice does not
  automatically port. The Mapbox token-rotation ops burden (a standing owner item in the backlog) does not
  extend to iOS under MapKit.
- **The seam that makes this safe (and reversible).** All map/geocode use goes through a **`MapProvider` /
  `GeocodingService` protocol in `CleansiaCore`** (the `LocationService`/`ReverseGeocodingService` parity).
  Features depend on the protocol, never on MapKit or Mapbox directly. If a **specific** parity gap appears
  — the Mapbox custom map *style* the Android apps ship (`MapStyles.kt`), a `ServiceArea` polygon overlay,
  or a UX the 3-snap sheet needs that MapKit can't match — that surface can drop to a **`MapboxMapProvider`
  implementation** behind the same protocol, *without* a feature rewrite. The Mapbox iOS SDK is then a
  scoped, isolated dependency, not an app-wide one.
- **Why default MapKit and not "match Android = Mapbox":** parity is of *behavior and product*, not of
  *vendor*. MapKit delivers the pin/region/geocode/reverse-geocode the address pickers and the partner map
  need, natively and freely; adopting the Mapbox iOS SDK app-wide imports a paid SDK, a token to rotate, a
  larger binary, and worse battery for capability iOS already has. The cost of being wrong is bounded
  **because of the protocol seam** — a forced gap migrates one provider, not the app. (If the owner has a
  hard brand requirement that the iOS map look pixel-identical to the Mapbox-styled Android map, that is the
  one input that flips the default — surfaced as Q-IOS-02, non-blocking; MapKit is the safe default to
  proceed on.)

### D7 — Stripe: the official `stripe-ios` PaymentSheet for the customer card flow

The customer card path (booking card-pay, `SubscribePlus` membership) uses the **official `stripe-ios` SDK's
PaymentSheet** — the iOS analogue of the Android PaymentSheet the customer app already uses. PaymentSheet
handles SCA/3DS, saved payment methods, and Apple Pay (a free parity win iOS gets that the card sheet
exposes). The flow mirrors Android exactly: the backend creates the PaymentIntent/checkout (the existing
mobile customer `Payment` endpoints), the app presents PaymentSheet with the client secret, and confirms.
**Cash → success, card → PaymentSheet** is the same branch as Android. The `stripe-ios` package is declared
in the **customer** target only (not `CleansiaCore`, not partner — partner has no card flow).

### D8 — Push: APNs → the existing `/api/Device/*` endpoints (no backend change)

iOS registers for remote notifications, obtains the **APNs device token**, and calls the existing
`RegisterDevice` with **`Platform = "ios"`** + the APNs token + the **same `X-Device-Id` deviceId** (D4.4).
The backend is **already iOS-ready** (`Platform "android"|"ios"`). The push-token cluster mirrors the
Android `:core` `DeviceRegistrationClient` / `PushTokenRepository` / `PushTokenSessionObserver`
(re-register on login, clear on logout) hoisted into `CleansiaCore`. **No backend or APNs-contract change**
— the only owner setup is the **APNs auth key / push certificate** in the Apple Developer account (an
owner provisioning step, flagged like the Android `google-services.json`).

### D9 — THE LEAD APP: **partner-first** (the architect's call, owner instruction (b))

**Decision: the lead app is the PARTNER app.** The first vertical is **partner login → Dashboard (a
read-only authed screen)**, and it proves the entire shared architecture before any high-risk feature.

**Rationale (why partner, not customer):**
1. **The first authed screen is READ-ONLY and dependency-free.** The partner **Dashboard** is a read-only
   stats screen — it exercises login → token store → `X-Device-Id`/`X-Time-Zone` headers → the authed
   business client → the 401-refresh → `UiState` rendering **without** Mapbox, Stripe, Google Sign-In, or
   photo upload. It is the cleanest possible proof of the load-bearing auth/session/header architecture
   (D4) and the codegen toolchain (D5). The customer equivalent first screen (Home) sits behind a more
   feature-dense shell and the booking FAB.
2. **The partner critical path is shorter to a shippable vertical.** Partner = a focused work-loop app;
   customer = a booking/commerce app whose hardest feature (the booking wizard + Stripe) is also its
   *primary* flow. Proving the architecture on partner first de-risks it before the team takes on the
   single hardest feature.
3. **Sequencing the hard areas safely.** Partner-first lets the codegen toolchain, auth layer, and state
   architecture be proven on read-only Dashboard, *then* the partner order work-loop (photos + the
   `OnTheWay`→`Start`→`Complete` lifecycle + the map) is tackled with the foundation solid — and the **map
   seam (D6) is first exercised on the partner `OrderDetail`**, where MapKit-vs-Mapbox is decided in
   practice before it also has to serve the customer address picker.

**The trade-off, recorded honestly (the challenger's point, conceded as a real cost):** the **customer**
app is the **richer shared-module reference** — its `:core` usage is more mature, while partner still
carries app-local network duplication (ARCH-001 Phase 3b incomplete). Building partner-first means
`CleansiaCore` is initially shaped by the *less* `:core`-mature app, risking that customer later needs
`CleansiaCore` additions partner didn't surface. **Mitigation (D9.1):** `CleansiaCore` is designed in
Phase 0 against **both** parity maps (not just partner's current code) — the shared package is specified
from the customer app's *mature* `:core` shape (the canonical target), and partner consumes it cleanly
(partner's app-local duplication is *not* ported — iOS partner is born on `CleansiaCore`, the way
ADR-0011 made iOS born-canonical instead of copying partner's legacy form). So partner-**first** for the
*proving vertical*, but `CleansiaCore`-**designed-from-customer's-mature-shape** for the *shared contract*.
This is the synthesis that answers the challenge: lead app ≠ the only input to the shared package.

### D10 — Trusted-device scope: **omit from iOS v1 to match Android**

`trustedDeviceToken` is **omitted from the iOS v1 login flow**, matching Android (whose hand-written
`LoginRequest` does not send it). Rationale: it is **net-new with no Android reference** (contract audit) —
porting it would be *adding* a feature iOS-only, not achieving parity, and the field is **optional (= null)**
on both mobile login commands, so omitting it is a fully supported request. Building an iOS-only
trusted-device flow ahead of Android creates a per-platform behavior divergence on a security-sensitive
path with no cross-client contract to anchor it. **If** trusted-device is later wanted, it should be
designed **once for all mobile clients** (the ADR-0011 "one contract, all clients" posture) and added to
Android and iOS together — recorded as Q-IOS-03 (post-prod, non-blocking).

### D11 — i18n: the 5 locales via a String Catalog (`.xcstrings`)

iOS ships the **same 5 locales** as every other client — **English (en), Czech (cs), Slovak (sk), Ukrainian
(uk), Russian (ru)** — via a **String Catalog (`.xcstrings`)**, Apple's current localization format
(Xcode 15+), per app (each app's strings depend on its own catalog, the per-app-localizer parity). **Every
user-visible string is a catalog key in all 5 locales** (the same rule the frontend/Android catalogs
enforce — no hardcoded strings). The backend `BusinessErrorMessage` keys map to catalog `errors.*` entries,
exactly as the web/Android clients do — the app-local error localizer (D1) reads them.

### D12 — Scope guard

This ADR decides the **architecture and the port strategy**. It does **not**: write any Swift code; choose
the per-screen UI composition of any feature (that is each feature ticket); design a *new* product behavior
(iOS is parity — it ports what the Android apps do, including the `OnTheWay` lifecycle the code has and the
doc lacks); or change the backend (push is already iOS-ready; the only backend-adjacent dependency is the
owner spec-regen, which is a *regen of the existing contract*, not a contract change). A future need for a
Mapbox provider (D6), a trusted-device flow (D10), or an iOS-bespoke `ApiResult` (ADR-0011 D4) is revisited
against this ADR (a new ADR if it changes a contract; a living-doc note if it only changes a provider/home).

---

## Alternatives considered

- **Mapbox iOS SDK app-wide (match Android = Mapbox).** Rejected as the default (D6). Parity is of behavior
  and product, not vendor; iOS has a first-party map (MapKit) of a quality Android lacks, so the Android
  Mapbox choice does not port. App-wide Mapbox imports a paid SDK, a token to rotate (a standing owner ops
  burden that would now extend to iOS), a bigger binary, and worse battery for capability iOS already has.
  Kept as a **scoped fallback behind the `MapProvider` protocol** for a specific forced parity gap (custom
  style, polygon overlay) — one provider migrates, not the app.
- **Customer-first lead app.** Rejected as the *proving vertical* (D9), though its strength is recorded and
  answered: customer is the richer `:core` reference, but its first real screen sits behind a feature-dense
  shell and its primary flow IS the hardest feature (booking + Stripe). Partner's read-only Dashboard proves
  the load-bearing auth/state/codegen architecture with zero high-risk dependencies first. The customer-
  maturity point is honored by **designing `CleansiaCore` from the customer app's mature `:core` shape**
  (D9.1), so the shared package is not shaped only by the less-mature partner code.
- **A DI framework (a Hilt analogue — e.g. Swinject/Factory).** Rejected (D3). Plain initializer injection +
  a composition root is the idiomatic Swift answer and avoids a runtime DI dependency for an app this size;
  the only Hilt subtlety that mattered (the refresh-client cycle) is handled by a lazily-injected separate
  no-auth session, the same explicit boundary Android draws with `NoAuthOkHttp`.
- **TCA / Redux / a third-party state library.** Rejected (D2). Android uses the platform primitive
  (`StateFlow` + sealed state), no extra framework; the iOS parity is `@Observable` + sealed `UiState`/
  `ActionState` enums. A heavyweight architecture library would diverge from the proven Android shape and
  add a large dependency for state a platform primitive already models.
- **Codegen auth too (one generated client, no hand-written auth).** Rejected (D4.2/D5). Auth must be
  hand-written because of the no-Bearer-on-anon allow-list, the empty-token unconfirmed-email special case,
  and the single-use refresh — the exact reasons Android hand-writes `AuthApi.kt`. A generated auth client
  would attach a Bearer to anon endpoints (some reject it) and not model the empty-token gate.
- **Codegen now against the committed 2026-05-31 specs.** Rejected — a **blocking** error (D5, contract
  audit). Those specs are pre-T-0272: wrong login schema, no `trustedDeviceToken`, leaked
  `requiredProfile`/`requiredAudience`, and missing `Device/Mine` + `Device/{id}` revoke +
  `EmployeePayroll/GetPeriodPays`. iOS generated against them would mis-model auth and lack the revoke +
  payroll endpoints. The owner regen is the one hard prerequisite.
- **An iOS-specific copy of the mobile OpenAPI spec.** Rejected (D5). There is **one** spec per mobile host,
  shared by Android and iOS; a per-platform copy would let the clients drift. The owner regenerates the one
  spec; both platforms' generators read it — the same single-contract discipline ADR-0011 set for the
  result type.
- **Alamofire (or another HTTP stack) over `URLSession`.** Rejected (D4/D5). `URLSession` is the platform
  primitive, keeps the dependency surface minimal, has first-class Swift-concurrency (`async`/`await`) for
  the actor-based single-flight refresh, and matches the openapi-generator `urlsession` networking option.
- **Skip the `X-Device-Id` single-source invariant / let push and the header resolve the id separately.**
  Rejected — it silently breaks remote device-revoke (`DeviceIdProvider.kt:14-22`: the revoke match is
  `RefreshToken.DeviceId == Device.DeviceId`). The id is resolved in **exactly one** `DeviceIdProvider`
  (D4.4); a second source is a blocking finding.
- **Build `trustedDeviceToken` for iOS now (net-new feature).** Rejected (D10). It has no Android reference,
  is optional on the command, and building it iOS-only creates a security-path divergence with no cross-
  client contract. If wanted, design it once for all mobile clients and ship Android + iOS together.
- **`.strings`/`.stringsdict` files instead of a String Catalog.** Rejected (D11). The String Catalog
  (`.xcstrings`) is Apple's current format (Xcode 15+), consolidates the 5 locales + pluralization in one
  managed file, and surfaces missing-translation warnings at build — the closest analogue to the catalog
  completeness the other clients enforce.

---

## Consequences

**Cheaper / safer:**
- **iOS is born on the canonical contracts** — `ApiResult`/`ApiError` (ADR-0011 D4), the single result
  shape, the one-source `X-Device-Id`, the shared mobile spec — instead of re-deriving them; the same
  "born-canonical, zero migration" win ADR-0011 captured, now for the whole client.
- **One mobile contract, three generators.** A backend DTO/endpoint change is **one** owner spec-regen
  feeding web (NSwag), Android (openapi-generator kotlin), and iOS (openapi-generator swift5) — the clients
  cannot drift from the backend or each other.
- **The map decision is reversible.** The `MapProvider` protocol means MapKit-by-default costs nothing and
  a forced parity gap migrates one provider, not the app — and the Mapbox token-rotation ops burden does
  not extend to iOS by default.
- **The lead-app call de-risks the hardest features.** Proving the load-bearing auth/state/codegen
  architecture on partner's read-only Dashboard *before* the booking wizard + Stripe means the single
  hardest feature is built on a proven foundation.
- **Multi-tenancy, push, and Stripe are parity wins** — tenant scoping is free (JWT claim), push is already
  iOS-ready, and PaymentSheet additionally unlocks Apple Pay.

**More expensive (new obligations):**
- **The owner spec-regen is a hard, single blocker** for every generated-client ticket
  (`manual_step: mobile-spec-regen`). The foundation that doesn't need the client can start on approval;
  the feature waves cannot.
- A new top-level tree `src/cleansia_ios/` (workspace + `CleansiaCore` SPM package + 2 app targets), a new
  Swift codegen toolchain wired into the build, and the hand-written auth/session/header layer — all
  Phase-0 foundation work that blocks the verticals.
- The `X-Device-Id` single-source invariant, the no-Bearer-on-anon allow-list, and the
  replace-refresh-token-on-every-refresh rule are **iOS obligations a reviewer must verify** (they are
  invisible to the generated client).
- Owner provisioning: the **APNs auth key/cert** (Apple Developer), a **Mapbox token only if** D6's fallback
  is ever taken, and the **Apple Developer account / signing** (an owner setup, not a Claude step).
- 5-locale String Catalogs per app; every string a key in all 5 (the same i18n discipline as the other
  clients).

**Rollout (consumer tickets — see the Phase plan):**
- **Phase 0:** workspace + `CleansiaCore` skeleton + design tokens + the codegen toolchain (held on the
  regen) + the auth/session/header middleware + DI composition root + snackbar/error center.
- **Phase 1:** the partner lead vertical — login → Dashboard (read-only), proving the architecture
  end-to-end.
- **Phase 2+:** parity feature waves ordered by complexity, with the three hard areas (booking+Stripe,
  Mapbox/MapKit across both apps, partner order work-loop+photos) explicitly sized.

---

## How a reviewer verifies compliance

**Mechanical / structural (the gate):**
1. **No hand-edited generated client.** The Swift client under each app's `Generated/` is produced by the
   codegen step (a checked-in script / SPM plugin), regenerated from the on-disk spec; a hand-edit is a
   blocking finding (the NSwag-discipline parity). The spec source is the **shared**
   `openapi/{partner,customer}-mobile-api.json`, not an iOS copy.
2. **Auth is NOT generated.** The auth endpoints (login/register/refresh/confirm/resend/google-auth) are
   hand-written against `URLSession` in `CleansiaCore/Auth`; the generated client carries **no** auth
   surface (it would attach a Bearer to anon paths).
3. **`X-Device-Id` single source.** Grep the iOS tree: the device id is resolved in **exactly one**
   `DeviceIdProvider`, and the **same** value is sent to `/api/Device/Register` *and* the `X-Device-Id`
   header. A second id source is a blocking finding (it breaks remote revoke).
4. **No-Bearer-on-anon allow-list present and complete.** The header layer attaches the Bearer **iff** the
   path is not on the allow-list (the Android 6 + the customer-host `[AllowAnonymous]` set) and a non-
   expired token exists. A Bearer on `/api/Auth/*` or on an anon customer endpoint is a finding.
5. **Refresh token replaced on every refresh.** The token store overwrites the refresh token on each
   successful refresh (single-use/theft-detection); a refresh path that reuses the old refresh token is a
   finding (`RefreshTokenReused`).
6. **Single no-auth session for refresh + single-flight.** Refresh uses a dedicated no-auth `URLSession`;
   concurrent 401s are serialised by the `actor SessionRefresher` (one refresh, others await + retry).
7. **Maps behind the protocol.** No feature imports MapKit or Mapbox directly; all map/geocode use goes
   through `CleansiaCore`'s `MapProvider`/`GeocodingService`. MapKit is the default implementation; a Mapbox
   provider (if ever added) is isolated behind the same protocol.
8. **One result type.** `ApiResult`/`ApiError` are defined **once** in `CleansiaCore` (ADR-0011 D4); no app-
   local fork; repos return `Result<T, ApiError>` and the **view model** (not the repo) surfaces the alert.
9. **Lifecycle parity.** The partner order screens model the `OnTheWay` state (Take → NotifyOnTheWay →
   Start → Complete), matching the **code**, not the 6-state doc.
10. **i18n completeness.** Every user-visible string is a `.xcstrings` key present in all **5** locales; no
    hardcoded strings; backend error keys map to `errors.*` entries.

**Test contract (consumer tickets):**
- **TC-IOS-AUTH-401:** a 401 triggers exactly one refresh under N concurrent requests (single-flight); the
  fresh token is reused by queued callers without a second network call; a rejected/expired refresh emits
  `ForcedSignOut(.sessionExpired)` and clears session-scoped caches.
- **TC-IOS-ANON:** requests to the allow-list paths carry **no** Bearer; authed paths carry it when a token
  is present.
- **TC-IOS-DEVICEID:** the `X-Device-Id` header value equals the `Device/Register` deviceId for the same
  install (one source).
- **TC-IOS-EMPTYTOKEN:** an unconfirmed-email login (200 + empty Token) routes to the confirm-email gate,
  not a session.
- **TC-IOS-STATE:** a screen renders all three `UiState` cases; a mutating action transitions
  `idle → submitting → (idle | error)` and surfaces success via the one-shot effect.

---

## Roles affected

Role files in `agents/knowledge/roles/` (CRC cards — added when the Phase-0 tickets land so they reflect the
written code):
- **`ios-session-refresher.md`** (new) — `SessionRefresher` (actor): *responsibility:* serialise concurrent
  401s into a single token refresh and retry, or force sign-out on refresh failure. *Collaborators:* the
  no-auth refresh session, `TokenStore` (Keychain), `SessionManager`, the session-scoped cache registry.
  *Does NOT know:* any business endpoint, how a screen renders, or the device-id (that is
  `DeviceIdProvider`'s).
- **`ios-device-id-provider.md`** (new) — `DeviceIdProvider`: *responsibility:* be the **single** source of
  the stable per-install device id used by **both** the `X-Device-Id` header and `Device/Register`.
  *Collaborators:* the Keychain (persistence), the header layer, the push registration. *Does NOT know:*
  a second id, the token, or the network call shape. (Mirrors `DeviceIdProvider.kt`'s "two consumers must
  agree on one string.")
- **`ios-header-adapter.md`** (new) — the request adapter: *responsibility:* stamp `X-Device-Id`/
  `X-Device-Label`/`X-Time-Zone` on every request and attach the Bearer **iff** the path is not anonymous.
  *Collaborators:* `DeviceIdProvider`, `TokenStore`, the anon allow-list. *Does NOT know:* refresh logic
  (that is `SessionRefresher`'s), or business payloads.
- **`ios-map-provider.md`** (new) — `MapProvider`/`GeocodingService` (protocol): *responsibility:* abstract
  map rendering + geocoding so features depend on the protocol, not a vendor. *Collaborators:* the default
  `MapKitMapProvider` (and an optional isolated `MapboxMapProvider`). *Does NOT know:* which feature uses it
  or any vendor specifics at the feature layer.

Catalog edits (same change as this ADR, per the pattern-evolution loop):
`agents/knowledge/patterns-mobile.md` gains an **iOS section** cross-referencing ADR-0013 (the
`CleansiaCore`+2-apps shape, `@Observable`+sealed-state parity, hand-written auth, codegen discipline, the
header invariants, MapKit-default-behind-protocol, String Catalog); `consistency.md` notes the iOS analogues
of the §E mobile rules. The living companion
`agents/architecture/decisions/ios-app-architecture.md` is created in parallel.

---

## Open questions raised (owner — all non-blocking; defaults taken)

Filed in `agents/backlog/questions/open.md`:
- **Q-IOS-01 (`post-prod`, owner)** — iOS **minimum deployment target**. **Default taken (non-blocking):**
  **iOS 17** (enables `@Observable` / Observation and the SwiftUI `Map`; the modern-floor posture matching
  Android `minSdk 26`). Only narrows device reach; does not change the architecture.
- **Q-IOS-02 (`post-prod`, owner)** — is there a **hard brand requirement** that the iOS map look pixel-
  identical to the Mapbox-styled Android map? **Default taken:** **No** — MapKit by default (D6), Mapbox a
  scoped fallback behind the protocol. A "yes" only flips the default provider; the seam is unchanged.
- **Q-IOS-03 (`post-prod`, owner)** — should **trusted-device** be added to the mobile clients? **Default
  taken:** **omit from iOS v1 to match Android** (D10); if wanted, design once for all mobile clients and
  ship Android + iOS together.

These do **not** block the plan. The **one true blocker** is the owner **mobile-spec regen**
(`manual_step: mobile-spec-regen`), which gates only the generated-client tickets — the foundation that
doesn't need the client starts on approval.

---

## Challenge / Defense / Verdict trail (condensed)

Author drafted (grounded in the contract audit + the parity map + the real `:core` auth code); challengers
(pragmatic/cost, seam/coupling, cross-platform-parity) attacked; the Lead re-verified every load-bearing
citation against the real Android code and the two investigations and adjudicated.
**Verdict: all challenges RESOLVED; zero blocking (three non-blocking owner questions escalated with
defaults); consensus reached.**

| # | Challenge (severity) | Disposition | Where |
|---|---|---|---|
| CH-1 (parity) | "Parity port" should mean *match Android*, so maps should be **Mapbox** — defaulting to MapKit is a divergence the owner didn't ask for. (MAJOR — the biggest cross-app decision) | REBUT + FRAME | D6 + Alternatives: parity is of **behavior/product**, not vendor; iOS has a first-party map Android lacks. MapKit delivers the needed pin/region/geocode natively + freely; Mapbox app-wide imports a paid SDK + a token to rotate + worse battery for capability iOS already has. The seam (`MapProvider`) makes a forced gap migrate **one provider, not the app**. The brand-identical question is surfaced (Q-IOS-02) but does not block — MapKit is the safe default. |
| CH-2 (coupling/maturity) | The **customer** app is the richer `:core` reference; building **partner-first** shapes `CleansiaCore` from the *less* `:core`-mature app, so customer later needs additions partner never surfaced. (MAJOR — the lead-app trade-off) | CONCEDE + REVISE | D9.1: lead app (the proving vertical) ≠ the only input to the shared package. `CleansiaCore` is **designed from the customer app's mature `:core` shape** in Phase 0; partner consumes it cleanly and partner's app-local duplication is **not** ported (iOS partner is born on `CleansiaCore`). Partner-first for *proving the architecture*; customer-shape for the *shared contract*. |
| CH-3 (risk/sequencing) | Why not customer-first, to confront the hardest feature (booking+Stripe) early and de-risk it? (MODERATE) | DEFEND | D9: proving the **load-bearing** auth/session/header/codegen architecture must come *before* the hardest feature, not on it. Partner's **read-only Dashboard** proves all of that with **zero** Mapbox/Stripe/Google/photo dependencies; then the order work-loop is built on a proven foundation. Customer's first real screen sits behind a feature-dense shell and its primary flow *is* the hardest feature. |
| CH-4 (codegen) | Generating against the committed specs is faster than waiting on the owner — is the regen really a hard blocker? (MODERATE — schedule pressure) | REBUT | D5 + Alternatives + the contract audit: the 2026-05-31 specs are pre-T-0272 — **wrong login schema**, no `trustedDeviceToken`, leaked `requiredProfile`/`requiredAudience`, and **missing** `Device/Mine`+`Device/{id}` revoke + `EmployeePayroll/GetPeriodPays`. Generating against them mis-models auth and lacks revoke+payroll. The regen is a genuine prerequisite; the plan unblocks the **foundation** that doesn't need the client so the wait costs nothing on the critical path. |
| CH-5 (auth) | A generated auth client is less code than hand-writing it — defend hand-written auth. (MODERATE) | DEFEND | D4.2 + Alternatives: auth must be hand-written for the same three reasons Android hand-writes `AuthApi.kt` — the **no-Bearer-on-anon** rule (some anon endpoints reject a Bearer), the **empty-token unconfirmed-email** special case (200 + empty Token → confirm-email gate), and **single-use refresh**. A generated client models none of these and would attach a Bearer to anon paths. |
| CH-6 (silent breakage) | The `X-Device-Id`/`X-Device-Label`/`X-Time-Zone` + allow-list contract is invisible to the generated client — what stops a dev from missing it and silently breaking remote-revoke? (MAJOR — security-relevant, no client captures it) | CONCEDE + REVISE | D4.4 makes it an explicit Phase-0 **header-parity spec** deliverable + the **single-source `DeviceIdProvider`** invariant (the `DeviceIdProvider.kt:14-22` revoke match), with reviewer checks #3/#4 and TC-IOS-DEVICEID/ANON. The id is resolved in **exactly one** place; a second source is a blocking finding. |
| CH-7 (DI) | Replacing Hilt with hand-wired DI loses the framework's cycle-breaking — won't the refresh-client DI cycle bite? (MODERATE) | DEFEND | D3: the only Hilt subtlety that mattered is the refresh-client cycle, handled exactly as Android does — a **separate no-auth session** for refresh, lazily injected by the composition root (the `NoAuthOkHttp` parity). Plain initializer injection is the idiomatic Swift answer; a DI framework is unwarranted overhead at this size. |
| CH-8 (cross-platform) | Building `trustedDeviceToken` for iOS would *exceed* Android and improve security — why omit it? (MODERATE) | DEFEND | D10 + Alternatives: it is **net-new with no Android reference**, **optional (= null)** on both mobile login commands, and an iOS-only build creates a security-path **divergence** with no cross-client contract. The ADR-0011 posture is "one contract, all clients" — if wanted, design it once and ship Android + iOS **together** (Q-IOS-03), not iOS-first. |

**Affirmed unchallenged:** the `CleansiaCore`+2-app-targets shape (the proven Android `:core`+2-apps
parity); `@Observable` + sealed `UiState`/`ActionState` as the state parity; the Keychain token store; the
`stripe-ios` PaymentSheet for the customer card flow; push via the already-iOS-ready `/api/Device/*`; the
String Catalog for the 5 locales; mirroring the **code** lifecycle (`OnTheWay`) not the doc; consuming
ADR-0011 D4's `ApiResult` contract rather than re-deciding it.

**Lead re-verification (against current code + the two investigations, 2026-06-23):**
`AuthInterceptor.kt:25-71` (no-Bearer-on-anon allow-list `:62-69`, `X-Device-Label`/`X-Device-Id` on every
request); `DeviceIdProvider.kt:14-33` (single-source id; revoke match `RefreshToken.DeviceId ==
Device.DeviceId`); `AuthAuthenticator.kt:37-112` (single-flight `synchronized(this)` 401-refresh, separate
`refreshClient`, `ForcedSignOut` + cache clear); `TokenStore.kt:41-110` (EncryptedSharedPreferences →
Keychain parity, refresh-token replaced on save); customer `AuthModule.kt:85-100` (`X-Time-Zone` per-request
on both clients) + `:249-252` (`NoAuthOkHttp`/`TimeZoneInterceptorQ` qualifiers); the openapi-generator
gradle toolchain (`partner-app/build.gradle.kts:10`, `openApiGenerate { generatorName="kotlin";
inputSpec=".../customer-mobile-api.json" }`, `compileKotlin.dependsOn("openApiGenerate")`); the committed
specs' 2026-05-31 staleness + the missing revoke/payroll endpoints (contract audit); `Glob src/cleansia_ios/**`
→ zero files (greenfield) confirmed; ADR-0011 D4 (the born-canonical Swift `ApiResult` this ADR consumes).

**Escalations to the owner:** three **non-blocking** questions with defaults — Q-IOS-01 (deployment target =
iOS 17), Q-IOS-02 (no hard Mapbox brand requirement → MapKit default), Q-IOS-03 (omit trusted-device to
match Android). The **one hard blocker** is the owner **mobile-spec regen** (`manual_step:
mobile-spec-regen`), an existing owner-only step (a regen of the *existing* contract, not a contract change),
which gates only the generated-client tickets — the foundation starts on approval.
