# Sprint 12 — iOS PORT (Wave 10): parity Swift/SwiftUI customer + partner apps

**Status:** PLANNED (backlog only — no code, no commits)
**Created:** 2026-06-23
**Source:** **ADR-0013** (`adr/0013-ios-app-architecture-and-port-strategy.md`, **accepted** 2026-06-23) +
**ADR-0014** (`adr/0014-ios-deployment-target-ios16-and-state-mechanism.md`, **accepted** 2026-06-23 —
partially supersedes ADR-0013: **iOS-16 floor** + `ObservableObject`/`@Published` state + the iOS-16 MapKit
variant; all other ADR-0013 decisions stand). Companion living doc
`architecture/decisions/ios-app-architecture.md`. Evidence base: the **Mobile API contract audit**
(security, 2026-06-22) + the **Android parity map** (analyst, 2026-06-22). ADR-0011 D4 supplies the
born-canonical Swift `ApiResult<T>` contract.

> **Owner revision applied 2026-06-23 (Q-IOS-01 answered = iOS 16, recorded in ADR-0014):** lower the floor
> from iOS 17 to **iOS 16** for old-device reach (iPhone 8/8 Plus/X, 2017+). Real impacts: state mechanism →
> **`ObservableObject`/`@Published`** (`@Observable` is iOS-17-only; the `UiState`/`ActionState` **enums +
> facade parity are unchanged**); maps use the **iOS-16 MapKit variant** (`Map(coordinateRegion:)` +
> `MKMapView` `UIViewRepresentable` for the full-bleed/overlay surfaces — the SwiftUI `Map {...}` API is
> iOS-17-only). **No new/removed tickets, no structural change** — only the per-ticket notes + the
> deployment-target + the reviewer checks (#11/#12) below. Everything else in this plan is unchanged.
**Goal:** port the Kotlin/Compose customer + partner apps to **Swift/SwiftUI** as **parity** apps sharing
the **same Mobile API contract**, on a `CleansiaCore` SPM package + 2 app targets, **partner-first**, with
a hand-written auth/session/header layer to the exact Android contract. iOS code lives at
**`src/cleansia_ios/`** (greenfield — created on the first iOS ticket).

> Wave 8 (pre-iOS cleanup, sprint-10) deduplicated the contract surface specifically to de-risk this
> port. Wave 9 (audit log, sprint-11) is a separate backend/admin feature. **Wave 10 is the iOS port.**
> Ticket ids **T-0296…T-0314**, next free after T-0295.

---

## 1. Owner decisions this wave builds to (ADR-0013 + ADR-0014)

- **Deployment target = iOS 16** (ADR-0014 / Q-IOS-01 answered — old-device reach iPhone 8/X 2017+). Set on
  both app targets + `CleansiaCore`'s `Package.swift` (`platforms: [.iOS(.v16)]`, T-0296). State VMs use
  **`ObservableObject` + `@Published`** (not `@Observable`); maps use the **iOS-16 MapKit variant**.
- **PLAN FIRST** — this is the design (ADR-0013 + ADR-0014) + this proposed backlog; **no Swift code, no
  commits** until the owner approves.
- **THE ARCHITECT DECIDES THE LEAD APP** — decided: **PARTNER** (D9). First vertical = partner login →
  **read-only Dashboard**, proving auth/session/headers/codegen/state with **zero** Mapbox/Stripe/Google/
  photo dependencies. The shared package is **designed from the customer app's mature `:core` shape**
  (D9.1) so it isn't shaped only by the less-`:core`-mature partner code.
- **The one hard blocker = the owner mobile-spec regen** (`manual_step: mobile-spec-regen`, **owner-only**).
  The committed specs (`src/cleansia_android/openapi/{partner,customer}-mobile-api.json`, 2026-05-31) are
  pre-T-0272 (wrong login schema, no `trustedDeviceToken`, leaked `requiredProfile`/`requiredAudience`,
  **missing** `Device/Mine` + `Device/{id}` revoke + `EmployeePayroll/GetPeriodPays`). **iOS codegen MUST
  NOT run against them.** A regen of the *existing* contract (not a contract change) feeds web (NSwag),
  Android (openapi-generator kotlin), and iOS (openapi-generator swift5) from the same spec.
- **Owner questions:** **Q-IOS-01 ANSWERED — iOS 16** (ADR-0014, above). **Q-IOS-02** (no Mapbox brand
  requirement → MapKit default) + **Q-IOS-03** (omit trusted-device to match Android) remain non-blocking
  with their defaults — `questions/open.md`.
- **Maps: MapKit by default behind a `MapProvider` protocol** (D6). **Stripe: `stripe-ios` PaymentSheet**
  (D7). **Push: APNs → existing `/api/Device/*`, `Platform="ios"`** (D8). **i18n: 5 locales via String
  Catalog** (D11). **trusted-device: omit v1** (D10).

---

## 2. Phase structure (the sequence)

```
PHASE 0  FOUNDATION (blocks everything) ── runnable on approval EXCEPT the codegen first-run (held on regen)
   workspace + CleansiaCore skeleton + design tokens + DI composition root + snackbar/error center
   + the hand-written auth/session/header middleware + the Swift codegen toolchain
        │
PHASE 1  LEAD VERTICAL (partner) ── proves the architecture end-to-end ── HELD on regen (needs the client)
   partner login (hand-written auth) → read-only Dashboard (generated client + UiState)
        │
PHASE 2+ PARITY FEATURE WAVES ── ordered by complexity; the 3 hard areas called out
   partner order work-loop+photos+map  ·  customer booking wizard+Stripe  ·  Mapbox/MapKit both apps  · …
```

**Runnable-on-approval vs owner-blocked (the clean split):**
- **Runnable as soon as the plan is approved (no generated client needed):** T-0296 (workspace+package
  skeleton), T-0297 (design tokens + components), T-0298 (DI composition root), T-0299 (snackbar/error
  center), T-0300 (the hand-written auth/session/header **middleware** — it is hand-written, not
  generated), T-0301 (the header-parity **spec document**). These build against `URLSession` + `CleansiaCore`
  with **no** generated business client.
- **BLOCKED on the owner mobile-spec regen** (`manual_step: mobile-spec-regen`): T-0302 (the codegen
  toolchain **first real generation**) and **every Phase-1/2+ ticket that touches a generated client**
  (T-0303 onward). The toolchain *wiring* (T-0302) can be authored against the stale spec to prove the
  pipeline, but is **held from `done`** until the regen lands and it generates the real surface.

---

## 3. Wave-10 ticket table

| ID | Title | Size | Status | Layers | depends_on | manual_step | Phase / batch |
|----|-------|------|--------|--------|-----------|-------------|---------------|
| **T-0296** | Xcode workspace + `CleansiaCore` SPM package skeleton + 2 app targets (`CleansiaPartner`/`CleansiaCustomer`), bundle ids, signing placeholders. **Deployment target = iOS 16** on both targets + `Package.swift` `platforms: [.iOS(.v16)]` (ADR-0014) | M | **proposed** | ios | — | — | **0 FIRST/ALONE** |
| **T-0297** | Design tokens (colors/spacing/shape/type) + the `Cleansia*` SwiftUI component parity (Button/TextField/Dropdown/Dialog/Checkbox/CodeInput) in `CleansiaCore`. **VM pattern = `ObservableObject`/`@Published`** (iOS-16, not `@Observable`); sealed `UiState`/`ActionState` enums unchanged (ADR-0014 D2′) | M | **proposed** | ios | T-0296 | — | 0 |
| **T-0298** | DI composition root (`AppContainer` per app, initializer injection; the lazy no-auth refresh-session boundary) | S | **proposed** | ios | T-0296 | — | 0 |
| **T-0299** | Global snackbar bus + error center (`SnackbarController` parity + the app-local `ApiError→String` localizer seam) | S | **proposed** | ios | T-0296 | — | 0 |
| **T-0300** | **The auth/session/header middleware (hand-written, load-bearing)** — Keychain `TokenStore`, hand-written `AuthClient` + no-auth refresh session, `actor SessionRefresher` single-flight 401-refresh, `DeviceIdProvider` (one source), `HeaderAdapter` (X-Device-Id/Label/Time-Zone + no-Bearer-on-anon allow-list), `SessionManager`/ForcedSignOut + session-scoped cache registry | **L → split** | **proposed** | ios | T-0296, T-0298 | — | 0 (the spine) |
| **T-0301** | **Header-parity spec document** — the invisible out-of-band contract written down for the iOS dev (X-Device-Id==Device/Register id invariant, the full anon allow-list incl. customer host, X-Time-Zone, replace-refresh-on-refresh, empty-token gate) | S | **proposed** | ios, docs | — | — | 0 (no-decision doc) |
| **T-0302** | Swift codegen toolchain — openapi-generator **swift5 + urlsession**, wired into the build (script/SPM plugin, the `dependsOn(openApiGenerate)` parity), reading the **shared** mobile spec; never-hand-edit discipline | M | **proposed** (**held on regen**) | ios | T-0296 | **mobile-spec-regen (owner)** | 0 → first real gen **BLOCKED on regen** |
| **T-0303** | **Phase-1 partner lead vertical** — partner login (hand-written auth, empty-token gate) → **read-only Dashboard** (generated partner client + `UiState`), proving auth/session/headers/codegen/state end-to-end | M | **proposed** (**held on regen**) | ios | T-0300, T-0302 | rides T-0302 regen | **1 (the proving vertical)** |
| **T-0304** | Partner shell (Dashboard·Orders·Invoices·Profile tabs) + RegistrationLock gate (fails CLOSED) + SplashGate | M | **proposed** | ios | T-0303 | — | 2 (partner) |
| **T-0305** | Partner auth completeness — Register/Forgot/ConfirmEmail/Onboarding chain | M | **proposed** | ios | T-0303 | — | 2 (partner) |
| **T-0306** | **Map seam + MapKit default** — `MapProvider`/`GeocodingService` protocol in `CleansiaCore` + `MapKitMapProvider` + the partner `AddressPicker` (first map surface). **iOS-16 variant (ADR-0014 D6′):** `Map(coordinateRegion:annotationItems:)` for the picker; `MKMapView` via `UIViewRepresentable` for the full-bleed map + polygon overlays — NO iOS-17-only `Map {...}`/`Marker`/`MapPolygon` | M | **proposed** | ios | T-0300 | — | 2 (**HARD AREA #2 — first half**) |
| **T-0307** | **Partner order work-loop** — OrdersList + OrderDetail (full-bleed map + 3-snap sheet) + the **OnTheWay** lifecycle (Take→NotifyOnTheWay→Start→Complete) + checklist/notes/issues/timeline | **L → split** | **proposed** | ios | T-0304, T-0306 | — | 2 (**HARD AREA #3**) |
| **T-0308** | **Partner photo upload** — camera capture → **JSON base64** photos (partner shape) on OrderDetail | M | **proposed** | ios | T-0307 | — | 2 (HARD AREA #3 cont.) |
| **T-0309** | Partner earnings + invoices + PeriodPay (`EmployeePayroll/GetPeriodPays` — a regen'd-spec endpoint) | M | **proposed** | ios | T-0304 | — | 2 (partner) |
| **T-0310** | Partner profile section editors + onboarding-chain + settings + **Devices** (Device/Mine list + revoke) + Notifications | M | **proposed** | ios | T-0304, T-0306 | — | 2 (partner) |
| **T-0311** | **Push (APNs)** — register for remote notifications → APNs token + `Platform="ios"` + same `X-Device-Id` to `/api/Device/*`; re-register on login, clear on logout (the `:core` push parity) | M | **proposed** | ios | T-0300 | **owner: APNs auth key/cert** | 2 (cross-app) |
| **T-0312** | **Customer app shell + auth** — SignIn/SignUp/EmailVerify (+ Google Sign-In, customer-only) + Main shell (Home·Orders·Rewards·Profile + center **Book FAB**) | M | **proposed** | ios | T-0302, T-0306 | rides regen | 2 (customer) |
| **T-0313** | **Customer booking wizard + Stripe** — the 3-step Bolt-style anchored sheet (Services / WhenWhere / Confirm), client-side pricing, **cash→success vs card→`stripe-ios` PaymentSheet** | **L → split** | **proposed** | ios | T-0312 | — | 2 (**HARD AREA #1 — hardest**) |
| **T-0314** | Customer parity tail — Home, Orders+OrderDetail, Rewards/loyalty, Membership (SubscribePlus→Stripe), Recurring, **Disputes (multipart `IFormFile` evidence)**, Addresses (map seam), Profile/Settings (incl. **DeleteAccount/GDPR**, Devices, Notification prefs) | **L → split** | **proposed** | ios | T-0312, T-0306, T-0313 | — | 2 (customer) |

**Sizing note:** the three `L → split` tickets (T-0300, T-0307, T-0313, T-0314) are the effort
concentrators and **must be split into child tickets by the PM before dispatch** (the catalog bans `L`
in-flight). Indicative splits are in §6. Every ticket carries **reviewer-per-developer**; the auth spine
(T-0300) and the booking+payment + GDPR-delete surfaces (T-0313, T-0314) carry a **security gate**.

---

## 4. The three effort-dominating areas (called out + sized)

| Hard area | Tickets | Why it dominates | Sequencing safeguard |
|---|---|---|---|
| **#1 Customer booking wizard + Stripe PaymentSheet** | **T-0313** (L→split) | The Bolt-style 3-step anchored sheet + client-side pricing + cash/card branch + SCA/3DS via PaymentSheet — the customer **primary** flow AND its hardest feature | Built **last**, on a foundation already proven by the partner vertical (the reason for partner-first, D9) |
| **#2 Mapbox vs MapKit across BOTH apps** | **T-0306** (seam+MapKit+partner picker), reused by **T-0307/T-0310/T-0314** | Partner OrderDetail full-bleed map + 3-snap sheet + both address pickers; the single biggest cross-app vendor decision | Decided MapKit-default-behind-`MapProvider` (D6); first exercised on the partner picker (T-0306) so the choice is proven before the customer picker depends on it |
| **#3 Partner order work-loop + photo upload + the codegen toolchain** | **T-0302** (toolchain), **T-0307** (work-loop, L→split), **T-0308** (photos) | Foundational toolchain blocks all verticals; the work-loop is the richest partner feature (OnTheWay lifecycle, map, sheet, photos) | Toolchain is Phase 0; the work-loop is the **first** Phase-2 feature so the order seam is proven before customer commerce |

---

## 5. Dependency-ordered batch plan

```
PHASE 0 (runnable on approval — the foundation)
  T-0296 (workspace+package) ── FIRST/ALONE ──┐
        ├─► T-0297 (tokens+components)         │
        ├─► T-0298 (DI root) ─► T-0300 (AUTH SPINE, L→split) ─► T-0306 (map seam+MapKit)
        ├─► T-0299 (snackbar/error)            │
        ├─► T-0301 (header-parity spec)  [no deps]
        └─► T-0302 (codegen toolchain) ── wiring runnable; FIRST REAL GEN held on regen ──┐
                                                                                          │
        ── OWNER: mobile-spec-regen (the one hard blocker) ───────────────────────────────┤
                                                                                          │
PHASE 1 (held on regen)                                                                   ▼
  T-0303 (partner login → read-only Dashboard) ◀── needs T-0300 (auth) + T-0302 (client)

PHASE 2+ (after Phase 1 proves the architecture)
  partner:  T-0304 (shell) ─► {T-0305 auth-rest, T-0307 order-loop ─► T-0308 photos, T-0309 pay, T-0310 profile/devices}
  map:      T-0306 (seam) reused by T-0307 / T-0310 / T-0314
  push:     T-0311 (APNs) ── after T-0300 ── cross-app
  customer: T-0312 (shell+auth) ─► T-0313 (booking+Stripe, HARDEST) ─► T-0314 (customer tail)
```

**Dispatch order:**
1. **On approval:** T-0296 first/alone → then fan out {T-0297, T-0298, T-0299, T-0301} + start T-0300
   (auth spine, split) + author T-0302 wiring.
2. **OWNER regen** (the one blocker) → releases T-0302's first real generation → **T-0303** (Phase 1).
3. **After Phase 1 proves the architecture:** partner Phase-2 batch, the map seam (T-0306), push (T-0311),
   then the customer batch ending in the booking wizard (T-0313) + the tail (T-0314).

---

## 6. Indicative splits for the `L` tickets (PM finalizes before dispatch)

- **T-0300 (auth spine) →** (a) Keychain `TokenStore` + `DeviceIdProvider` single-source; (b) hand-written
  `AuthClient` + no-auth refresh session + empty-token gate; (c) `actor SessionRefresher` single-flight +
  ForcedSignOut + session-scoped-cache registry; (d) `HeaderAdapter` (X-Device-Id/Label/Time-Zone + anon
  allow-list). Each child red-first against the TC-IOS-AUTH-401 / ANON / DEVICEID / EMPTYTOKEN contract.
- **T-0307 (partner order-loop) →** (a) OrdersList; (b) OrderDetail map+3-snap sheet shell;
  (c) the OnTheWay→Start→Complete lifecycle + actions; (d) checklist/notes/issues/timeline.
- **T-0313 (booking+Stripe) →** (a) the anchored 3-step sheet scaffold + step nav; (b) Services step +
  client-side pricing; (c) WhenWhere step (map seam); (d) Confirm + cash→success vs card→PaymentSheet.
- **T-0314 (customer tail) →** split by feature cluster (Home/Orders · Rewards/Membership/Recurring ·
  Disputes-multipart · Addresses · Profile/Settings/GDPR-delete/Devices).

---

## 7. Owner manual-steps & provisioning (NOT the agents)

1. **mobile-spec-regen (owner-only) — the one hard blocker.** Regenerate **both** mobile specs
   (`src/cleansia_android/openapi/{partner,customer}-mobile-api.json`) to the current post-T-0272 contract
   (correct `MobileLogin`/`MobilePartnerLogin` schema with optional `trustedDeviceToken`, `[JsonIgnore]`d
   refresh fields, **plus** `Device/Mine` + `Device/{id}` revoke + `EmployeePayroll/GetPeriodPays`). This
   is a regen of the *existing* contract — no backend code change. It also re-feeds Android's
   openapi-generator (kotlin) from the same spec, keeping all three clients aligned. **Gates T-0302's first
   real generation and every generated-client ticket (T-0303+).**
2. **APNs auth key / push certificate (Apple Developer)** — for T-0311 push (the Android
   `google-services.json` analogue). Owner provisioning; flagged, not built by agents.
3. **Apple Developer account + signing / bundle ids** (`cz.cleansia.partner` / `cz.cleansia.customer`) —
   owner setup for T-0296's targets.
4. **Mapbox token — ONLY IF** Q-IOS-02 flips the default to Mapbox (D6 fallback). **Not needed** under the
   MapKit default; no extra owner ops burden by default.

**No ef-migration, no backend code change** in this wave — iOS is a client of the existing contract; push
is already iOS-ready. The only owner contract step is the **regen of the existing spec**.

---

## 8. Gates & verification (per `agents/process/quality-gates.md`)

- **Reviewer-per-developer** on every ticket (concurrent).
- **Security gate mandatory on T-0300** (the auth/session/header spine — a wrong anon allow-list leaks a
  Bearer to anon endpoints; a second device-id source breaks remote-revoke; a reused refresh token
  self-revokes), **T-0313** (card/payment flow), **T-0314** (GDPR delete/export + dispute evidence).
- **TDD on the auth spine:** TC-IOS-AUTH-401 (single-flight), TC-IOS-ANON (no-Bearer-on-anon),
  TC-IOS-DEVICEID (header==Device/Register id), TC-IOS-EMPTYTOKEN (200+empty→confirm gate), TC-IOS-STATE
  (the three UiState cases) — **red-first**.
- **Reviewer compliance checks (ADR-0013 + ADR-0014 §"How a reviewer verifies"):** #1 no hand-edited
  generated client · #2 auth NOT generated · #3 X-Device-Id single source · #4 anon allow-list complete
  (incl. customer host) · #5 refresh token replaced every refresh · #6 single no-auth session +
  single-flight · #7 maps behind `MapProvider` (no direct MapKit/Mapbox import in features) · #8 one
  `ApiResult`/`ApiError` in `CleansiaCore` (ADR-0011 D4) · #9 OnTheWay lifecycle parity (mirror the code) ·
  #10 i18n 5-locale completeness, no hardcoded strings · **#11 (ADR-0014) deployment target = iOS 16; no
  `import Observation`/`@Observable`/`@available(iOS 17)` always-on path — VMs conform to `ObservableObject`
  with `@Published` state, `@StateObject` for owned VMs vs `@ObservedObject` for injected (the foot-gun)** ·
  **#12 (ADR-0014) no iOS-17-only SwiftUI MapKit API (`Map {...}` content builder / `Marker`/`MapPolygon`/
  `MapCameraPosition`) — rich-map surfaces via `MKMapView` inside `MapKitMapProvider`**.
- **Mechanical:** the Xcode workspace builds; `CleansiaCore` + both app targets compile; the codegen step
  produces the client from the on-disk spec (no hand-edit); the Swift test suites run.

---

## 9. Definition of wave-done (rolling — this is a multi-phase port, not a single sprint)

Phase 0 done = the workspace + `CleansiaCore` + the auth/session/header spine + DI + snackbar/error +
the codegen toolchain all build, with the auth contract tests green. Phase 1 done = partner login →
read-only Dashboard works end-to-end against the **regenerated** client, proving the architecture. Each
Phase-2+ feature ticket has an owner, a current state, satisfied-or-blocked deps, AC↔evidence, the
ADR-0013 reviewer checks green, and a status-log line per transition. INDEX.md + this doc match reality.
The three non-blocking owner questions (Q-IOS-01/02/03) are tracked with their defaults; the
mobile-spec-regen is confirmed before any generated-client ticket advances to `done`.
