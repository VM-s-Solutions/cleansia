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
contract** — not a new product. The structure mirrors the proven Android `:core`+2-apps shape:

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
| Headers | `X-Device-Id` (one source = Device/Register id), `X-Device-Label`, `X-Time-Zone`; no-Bearer-on-anon allow-list | `AuthInterceptor` + per-request `X-Time-Zone` |
| Codegen | **openapi-generator swift5 + urlsession**, from the **owner-regenerated** shared spec | openapi-generator kotlin |
| Maps | **MapKit by default**, behind a `MapProvider` protocol; Mapbox iOS SDK = scoped fallback. **iOS-16 variant:** `Map(coordinateRegion:annotationItems:)` for pickers; `MKMapView` via `UIViewRepresentable` for the full-bleed map + polygon overlays (the SwiftUI `Map {...}`/`Marker`/`MapPolygon` API is iOS-17-only) | Mapbox (no first-party map on Android) |
| Stripe | `stripe-ios` **PaymentSheet** (customer target only) | Android PaymentSheet |
| Push | APNs token → existing `/api/Device/*` with `Platform="ios"` | FCM → `/api/Device/*` |
| Lead app | **PARTNER** (read-only Dashboard proves the architecture first) | — |
| trusted-device | **omit v1 to match Android** | not sent by Android |
| i18n | 5 locales (en/cs/sk/uk/ru) via **String Catalog** `.xcstrings` | `values-*/strings.xml` |

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

## Trade-off space (what was weighed — see ADR-0013/ADR-0014 §Alternatives + the Challenge trails)

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
