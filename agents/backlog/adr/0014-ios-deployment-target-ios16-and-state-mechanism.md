# ADR-0014 — iOS deployment target = iOS 16 (old-device reach); the D2 state mechanism becomes `ObservableObject`/`@Published`, and the iOS-16 API audit of ADR-0013

- **Status:** accepted   <!-- proposed | accepted | superseded | rejected -->
- **Date:** 2026-06-23
- **Supersedes:** ADR-0013 **partially** — only **D2** (the view-model observation mechanism) and the
  **deployment-target assumption** (the Q-IOS-01 default). **Everything else in ADR-0013 stays in force**
  (package shape D1, DI D3, the auth/session/header layer D4, codegen D5, MapKit-default D6, Stripe D7,
  push D8, partner-first lead app D9, trusted-device-omit D10, the 5-locale String Catalog D11). This ADR
  does **not** re-open those.
- **Superseded by:** —
- **Extended by:** ADR-0021 (2026-06-27) — the **sheet half of D6′**: D6′ routed the full-bleed `OrderDetail` *map*
  through `MKMapView`/`UIViewRepresentable`; ADR-0021 decides the *sheet over it* — a **custom non-modal `SnapSheet`**
  on the **16.0 floor** (custom `.presentationDetents` are 16.4+), and **explicitly keeps this ADR's floor at 16.0**
  (it does not amend the deployment target). A status-block pointer; the immutable body below is unchanged.
- **Applies to:** ios | cross-cutting
- **Extends:** ADR-0013 (the iOS architecture & port strategy; this answers its Q-IOS-01 and re-decides its
  D2 for the lower floor), ADR-0011 (the `ApiResult<T>` Swift contract — unaffected by the floor).
- **Ticket:** IOS-ADR-2 (this ADR) · **Consumers:** the same Phase plan (`status/sprint-12.md`), updated in
  parallel; living companion `architecture/decisions/ios-app-architecture.md` updated in parallel.

> **Why a superseding ADR and not an edit:** ADR-0013 is `accepted` and immutable (the ADR rule: never
> edit an accepted ADR — supersede). The owner answered Q-IOS-01 (**iOS 16**, for old-device reach) and
> that answer **changes a load-bearing decision** — `@Observable` (Observation) is **iOS 17-only**, so the
> D2 view-model mechanism must change. That is a real decision, not a typo, so it is recorded as a
> superseding ADR carrying its own audit + trade-off. **Plan-only — no Swift code.**

> **Owner decision this ADR records (Q-IOS-01, answered 2026-06-23):** the iOS **minimum deployment target
> is iOS 16**, not iOS 17. The owner's explicit priority is **old-device reach**: iOS 16 runs on
> **iPhone 8 / 8 Plus / X (2017+)**, which the iOS-17 floor excluded (iOS 17 needs XS/XR, 2018+). The
> reach gain is the deciding factor; the cost (a more verbose state mechanism, a couple of 16-compatible
> API variants) is accepted and recorded below.

---

## Context

ADR-0013 set the iOS architecture with **iOS 17** as a *default assumption* (its own Q-IOS-01, flagged
non-blocking) and chose `@Observable` (the Observation macro) for D2 view models *because* it assumed that
floor. The owner has now **answered Q-IOS-01 as iOS 16** for old-device reach. iOS 16 is a hard floor
change with exactly **one load-bearing consequence and a short list of API-variant picks** — everything
else in ADR-0013 already works on iOS 16. This ADR enumerates the audit so nothing shifts silently.

**The one load-bearing impact: Observation is iOS 17-only.** The `@Observable` macro and the `Observation`
framework require **iOS 17**. On an iOS-16 floor they are unavailable, so the D2 view-model mechanism
**must** change. The **mature, pre-Observation pattern** — `ObservableObject` conformance + `@Published`
properties on the view model, observed in SwiftUI views via `@StateObject` (owner) / `@ObservedObject`
(passed-in) — is available since **iOS 13** and is the correct iOS-16 choice. **Critically, the sealed
`UiState`/`ActionState` translation is UNCHANGED** — they are plain Swift enums (no OS dependency); only
the *observation wrapper around them* changes from `@Observable` to `ObservableObject`+`@Published`. The
facade/state parity to Android's `StateFlow<UiState>` is preserved (see D2′ below).

**Everything else in ADR-0013, audited against iOS 16 (all confirmed compatible — variants noted):**

| ADR-0013 decision | iOS-16 status | Action |
|---|---|---|
| **D1 package shape** (workspace + `CleansiaCore` SPM + 2 targets) | SPM + multi-target: fine far below 16 | unchanged |
| **D2 state** (`@Observable`) | **`@Observable` is iOS 17-only** | **RE-DECIDED → `ObservableObject`/`@Published` (D2′)** |
| **D3 DI** (initializer injection + composition root) | language-level, no OS dependency | unchanged |
| **D4 auth/session/headers** (Keychain, `URLSession`, `actor`, single-flight) | Keychain (iOS 2+), `URLSession` async/await (iOS 13+/15+), `actor` (Swift 5.5 / iOS 13+) — all ≤ 16 | unchanged |
| **D5 codegen** (openapi-generator swift5 + urlsession) | generated client is Swift/Foundation over `URLSession` — runtime fine on 16 | unchanged |
| **D6 maps** (MapKit default behind `MapProvider`) | **the SwiftUI `Map { Marker/Annotation }` content-builder + `Marker`/`MapPolygon`/`MapCameraPosition` are iOS 17-only**; the **`Map(coordinateRegion:annotationItems:)`** initializer + `MapMarker`/`MapAnnotation` are **iOS 14–16** | **variant picked (D6′)** — use the iOS-16 Map region API; full-bleed partner map + service-area polygon overlays go through a `UIViewRepresentable` over `MKMapView` (the protocol seam already isolates this) |
| **D7 Stripe** (`stripe-ios` PaymentSheet) | `stripe-ios` supports **iOS 13+**; PaymentSheet fine on 16 | unchanged |
| **D8 push** (APNs → `/api/Device/*`) | APNs / `UNUserNotificationCenter`: fine far below 16 | unchanged |
| **D9 lead app** (partner-first) | no OS dependency | unchanged |
| **D10 trusted-device** (omit v1) | no OS dependency | unchanged |
| **D11 i18n** (String Catalog `.xcstrings`) | `.xcstrings` is an **Xcode-15 build-time authoring** format that **compiles to standard `.strings`/`.stringsdict`** — the **runtime deployment target is unaffected**; it works for an iOS-16-targeting app built with Xcode 15+ | unchanged (claim verified — see D11 note below) |
| **Navigation** (`NavigationStack`) | `NavigationStack` is **iOS 16+** — **available** on the floor | unchanged (would only break below 16) |

So the floor change is **one re-decision (D2) + two API-variant picks (D6 Map, confirmed-only on D11/nav)**.
No decision in ADR-0013 is *broken* by iOS 16; the package shape, the load-bearing auth layer, codegen,
Stripe, push, and the lead-app/maps/trusted-device/i18n calls all hold.

---

## Decision

> **Contract principle.** The iOS **minimum deployment target is iOS 16** (old-device reach: iPhone 8/X,
> 2017+). The D2 view-model **observation mechanism** becomes **`ObservableObject` + `@Published`**
> (observed via `@StateObject`/`@ObservedObject`), the mature pre-Observation pattern available since
> iOS 13 — the sealed `UiState`/`ActionState` enums and the facade/state parity are **unchanged**. Maps use
> the **iOS-16-compatible MapKit API** behind ADR-0013's `MapProvider` seam (the SwiftUI `Map(coordinateRegion:)`
> region initializer; full-bleed map + polygon overlays via a `UIViewRepresentable` over `MKMapView`). All
> other ADR-0013 decisions stand.

### Q-IOS-01 — answered: iOS 16 (supersedes ADR-0013's iOS-17 default)

The minimum deployment target is **iOS 16.0**. Rationale: **device reach** — iOS 16 is the latest OS that
runs on **iPhone 8 / 8 Plus / X (2017)**, a population the iOS-17 floor (XS/XR and newer) drops. The owner
weighed reach over the marginal developer-ergonomics win of Observation and chose reach. This is the modern
floor that still includes 2017 hardware (the analogue of Android `minSdk 26` = Android 8, 2017).

### D2′ — State mechanism on iOS 16: `ObservableObject` + `@Published`; the enums and parity are unchanged

The view model is an **`ObservableObject`** exposing its state as **`@Published`** properties; SwiftUI views
observe it with **`@StateObject`** (the view that owns it) or **`@ObservedObject`** (a view it's passed to).
The state *types* are the **same sealed enums** ADR-0013 D2 fixed:

```swift
// UNCHANGED from ADR-0013 D2 — plain enums, no OS dependency.
enum UiState<T> { case loading; case error(ApiError); case loaded(T) }
enum ActionState { case idle; case submitting; case error(ApiError) }

// CHANGED: the observation wrapper. iOS-16 mechanism = ObservableObject + @Published
// (instead of the iOS-17 @Observable macro).
final class SomeViewModel: ObservableObject {
    @Published private(set) var state: UiState<SomeData> = .loading
    @Published var action: ActionState = .idle
    // … same collaborators (initializer-injected, D3), same repository calls returning ApiResult<T> (ADR-0011 D4).
}

// In the View:
//   @StateObject private var vm = ...      (or @ObservedObject for a passed-in vm)
//   switch vm.state { case .loading: …; case .error(let e): …; case .loaded(let d): … }
```

- **The facade/state parity to Android holds.** The view model is still the single source of truth, still
  exposes a sealed `UiState<T>` the view switches over to render the **three explicit data states**, still
  has a one-shot `ActionState` for mutations with success signalled via the SnackbarController bus (the
  `SharedFlow` analogue). What changed is *how the view subscribes* (`@Published`/`@ObservedObject` instead
  of the `@Observable` macro) — **not** the state shape, not the parity, not the "no third-party state
  library" rule (no TCA/Redux). The consistency-catalog E1/E2 sealed-state parity is intact.
- **The boilerplate cost, recorded honestly (the new trade-off this floor introduces).** `ObservableObject`
  + `@Published` is **more verbose** than `@Observable`: every observed property needs the `@Published`
  attribute (the macro infers it), the view model must be a `class` conforming to `ObservableObject`
  (already true), and views must pick the right wrapper (`@StateObject` for ownership vs `@ObservedObject`
  for injection — a known foot-gun: using `@ObservedObject` where `@StateObject` is needed re-creates the
  VM on re-render). Observation's finer-grained, per-property view invalidation is also lost — `@Published`
  invalidates the whole observing view on any published change. **At this app's scale these are ergonomics
  and a small perf nuance, not a correctness or architecture problem** — `ObservableObject` is the pattern
  the vast majority of shipping SwiftUI apps still use, it is fully battle-tested, and it does not disturb
  the facade/state seam. The cost is accepted in exchange for the 2017-device reach.
- **A migration note for the future (non-binding):** because only the *wrapper* differs and the enums +
  facade boundary are identical, a later bump to an iOS-17 floor could adopt `@Observable` view-model by
  view-model with **no change to the state types, the views' switch, or the parity** — the seam was chosen
  to make that swap mechanical, not a rewrite. Recorded for whenever the floor rises; not planned now.

### D6′ — MapKit on iOS 16: the region API + `MKMapView` for the rich surfaces (behind the unchanged `MapProvider` seam)

ADR-0013 D6's decision (MapKit default, behind a `MapProvider`/`GeocodingService` protocol, Mapbox a scoped
fallback) **stands**. Only the concrete MapKit API picks change for iOS 16:

- The newer **SwiftUI MapKit** (`Map { Marker(...) ; Annotation(...) ; MapPolygon(...) }` content builder,
  `MapCameraPosition`, the `Marker`/`MapCircle`/`MapPolygon` types) is **iOS 17-only** — **not** used.
- The **iOS-16-available** SwiftUI `Map(coordinateRegion:interactionModes:showsUserLocation:annotationItems:)`
  initializer + `MapMarker`/`MapAnnotation`/`MapPin` are used for the **address-picker pin/region** surfaces.
- The **full-bleed partner `OrderDetail` map** (the 3-snap-sheet backdrop) and any **service-area polygon
  overlay** use a **`UIViewRepresentable` over `MKMapView`** (UIKit MapKit — available far below 16,
  full-featured: overlays, custom annotations, camera control). This is wrapped **inside the
  `MapKitMapProvider`**, so features still depend only on the `MapProvider` protocol — the seam ADR-0013 D6
  established is exactly what absorbs this variant without touching any feature. `CLGeocoder`/`MKLocalSearch`
  geocoding is unchanged (available below 16).
- **The reversibility argument is unchanged:** if a parity gap forces Mapbox on one surface, the
  `MapboxMapProvider` swaps in behind the same protocol. The iOS-16 floor does **not** weaken D6 — it just
  routes the rich-map surfaces through `MKMapView` (which iOS gives us for free) instead of the iOS-17
  SwiftUI map.

### D11 note — String Catalog claim verified for iOS 16

ADR-0013 D11 (5 locales via String Catalog `.xcstrings`) **stands and is verified**: `.xcstrings` is an
**Xcode-15 build-time authoring** format. At build it compiles to the standard `.strings`/`.stringsdict`
the runtime has consumed since iOS 2. **The deployment target is irrelevant to it** — an iOS-16-targeting
app built with Xcode 15+ uses String Catalogs normally. No change; the D11 claim is confirmed at the floor.

### Navigation confirmation

ADR-0013 used `NavigationStack` (the nav-scaffolding parity). `NavigationStack` is **iOS 16+** — **available
on the floor**. Only a sub-16 floor would force the deprecated `NavigationView`. The nav decision holds
unchanged; recorded so the dev does not reach for `NavigationView`.

### Scope guard

This ADR changes **only** the deployment target (Q-IOS-01 → iOS 16) and the D2 observation mechanism (→
`ObservableObject`/`@Published`), and records the iOS-16 API-variant picks for D6 (and the D11/nav
confirmations). It does **not** re-open D1/D3/D4/D5/D7/D8/D9/D10/D11, write Swift code, or change the Phase
plan's structure — only the tickets' "use `ObservableObject` not `@Observable`", "target iOS 16", and "use
the iOS-16 Map variant" notes are updated in parallel (sprint-12). A future floor rise to iOS 17 would be a
new ADR adopting `@Observable` + the SwiftUI MapKit API (mechanical per D2′'s migration note).

---

## Alternatives considered

- **Keep iOS 17 (so `@Observable` and the SwiftUI MapKit API stay).** Rejected per the owner's answer —
  it **drops iPhone 8/8 Plus/X (2017)**, the exact reach the owner prioritised. The Observation ergonomics
  and per-property invalidation are a developer-side nicety, not a user-facing capability; the owner valued
  2017-device reach over them.
- **iOS 16 floor but back-port `@Observable` via `@available(iOS 17)` + an `ObservableObject` fallback per
  view model (dual code paths).** Rejected — it doubles the state mechanism (two observation patterns to
  maintain, `#available` branches in every view) for zero user benefit on an iOS-16 floor where the
  fallback path is the one that always runs. One mechanism (`ObservableObject`) is simpler and is the
  catalog "one way to do X" posture. Revisit only if/when the floor rises to 17 (then drop the fallback,
  not add a branch).
- **iOS 15 or lower for even more reach.** Rejected — it would lose `NavigationStack` (forcing the
  deprecated `NavigationView`) and other 16-era affordances, for marginally older hardware. iOS 16 is the
  sweet spot: it still includes 2017 phones (the owner's target) **and** keeps `NavigationStack`. Not what
  the owner asked for; recorded as the lower bound considered.
- **A third-party state library (TCA) to paper over the `ObservableObject` boilerplate.** Rejected — same
  as ADR-0013 D2's rejection: it diverges from the proven Android "platform primitive, no extra framework"
  shape and adds a large dependency. The `ObservableObject` verbosity is an accepted ergonomics cost, not a
  reason to import an architecture framework.
- **Mapbox app-wide on iOS 16 (since the SwiftUI MapKit API is reduced on 16).** Rejected — the reduced API
  is only the *SwiftUI* surface; **UIKit `MKMapView` is fully featured on iOS 16** (overlays, polygons,
  custom annotations), so MapKit still covers every parity need via the `UIViewRepresentable` (D6′). The
  iOS-16 floor is **not** a reason to flip D6 to Mapbox; the MapKit-default rationale (free, native, no
  token) is unchanged.

---

## Consequences

**Cheaper / safer:**
- **+1 device generation of reach** — iPhone 8/8 Plus/X (2017) can run the apps, the owner's stated
  priority. (Android `minSdk 26` already includes 2017 hardware; iOS 16 brings iOS to parity on the
  old-device floor.)
- **`ObservableObject` is the most battle-tested SwiftUI state mechanism** — years of production use, vast
  documentation, every Swift dev knows it. Lower onboarding risk than the newer Observation macro.
- **The seam choices absorb the floor change with zero feature impact** — the `MapProvider` protocol
  already isolates the map API (so the iOS-16 Map variant is a provider-internal detail), and the sealed
  `UiState`/`ActionState` enums are OS-independent (so only the observation wrapper changes). The
  architecture ADR-0013 set was robust to this floor change by design.

**More expensive (the new trade-offs this floor introduces — recorded, not silent):**
- **`ObservableObject`/`@Published` boilerplate** — every observed property needs `@Published`; views must
  correctly choose `@StateObject` (ownership) vs `@ObservedObject` (injection) — a known foot-gun the
  reviewer must watch (using `@ObservedObject` for an owned VM re-creates it on re-render). More verbose
  than `@Observable`; accepted for the reach.
- **Lost per-property view invalidation** — `@Published` invalidates the whole observing view on any change
  (vs Observation's fine-grained tracking). A minor perf nuance at this app's scale, not a correctness
  issue; only matters for unusually heavy screens (none identified in the parity map).
- **The rich-map surfaces go through `UIViewRepresentable`/`MKMapView`** instead of the iOS-17 SwiftUI Map —
  slightly more UIKit-bridging code in the `MapKitMapProvider` (well-trodden, fully documented). Hidden
  behind the `MapProvider` seam, so no feature sees it.
- **A future iOS-17 bump is a (small) follow-on** — adopting `@Observable` + the SwiftUI MapKit API later is
  mechanical (D2′ migration note) but is real future work. Recorded; not planned now.

**Plan impact (parallel updates, this change):**
- **No new tickets, no removed tickets, no structural Phase change.** Every ADR-0013 ticket stands; the
  notes change: T-0297/T-0303/T-0304+ (any VM-bearing ticket) say "`ObservableObject`/`@Published`, target
  iOS 16"; T-0306 (map seam) says "iOS-16 Map region API + `MKMapView` `UIViewRepresentable` for the
  full-bleed/overlay surfaces"; T-0296 sets the iOS-16 deployment target on both targets + `CleansiaCore`'s
  `Package.swift` `platforms: [.iOS(.v16)]`.
- **The reviewer compliance list gains:** #11 — no `@available(iOS 17)`/`@Observable`/Observation usage
  (the floor is 16); view models conform to `ObservableObject` with `@Published` state; views use
  `@StateObject` for owned VMs. #12 — no iOS-17-only SwiftUI MapKit API (`Map {...}` content builder,
  `Marker`/`MapPolygon`/`MapCameraPosition`); map rich surfaces via `MKMapView`.

---

## How a reviewer verifies compliance (delta to ADR-0013's list)

ADR-0013's checks #1–#10 stand. Added by this ADR:
11. **Deployment target = iOS 16.** Both app targets and `CleansiaCore`'s `Package.swift` declare iOS 16
    (`platforms: [.iOS(.v16)]`). No `@available(iOS 17, *)` gating an always-on path; no `import Observation`
    / `@Observable`. View models conform to `ObservableObject` and expose `@Published` state; owned VMs use
    `@StateObject`, injected VMs use `@ObservedObject`.
12. **No iOS-17-only MapKit SwiftUI API.** No `Map { Marker/Annotation/MapPolygon }` content-builder,
    `MapCameraPosition`, or `Marker`/`MapPolygon`/`MapCircle` SwiftUI types. The address-picker pin/region
    uses the iOS-16 `Map(coordinateRegion:annotationItems:)`; the full-bleed map + polygon overlays use a
    `UIViewRepresentable` over `MKMapView`, all inside `MapKitMapProvider` (features import only
    `MapProvider`).
13. **State shape unchanged.** `UiState`/`ActionState` remain the sealed enums from ADR-0013 D2 (this ADR
    changed only the observation wrapper). One `ApiResult`/`ApiError` (ADR-0011 D4) unchanged.

**Test contract:** ADR-0013's TC-IOS-* are unchanged (TC-IOS-STATE asserts the three `UiState` cases and
the `idle→submitting→(idle|error)` action transition — the enum behavior, which is mechanism-independent).

---

## Roles affected

No new roles. The ADR-0013 role CRCs (`ios-session-refresher`, `ios-device-id-provider`,
`ios-header-adapter`, `ios-map-provider`) are unchanged — none depend on the observation mechanism or the
floor. Catalog edit (same change): the `patterns-mobile.md` iOS section's state note changes from
"`@Observable` view models" to "`ObservableObject`/`@Published` view models (iOS-16 floor; sealed
`UiState`/`ActionState` unchanged)"; the map note adds the iOS-16 MapKit-variant line. The living companion
`architecture/decisions/ios-app-architecture.md` is updated in parallel.

---

## Open questions

- **Q-IOS-01 — ANSWERED (iOS 16).** Moved toward `answered.md` semantics (the owner decided); recorded here
  and in `questions/open.md`. No longer open.
- **Q-IOS-02 / Q-IOS-03** — unchanged from ADR-0013 (MapKit default; trusted-device omit). Not touched.

---

## Challenge / Defense / Verdict trail (condensed)

Author drafted the floor change + the D2 re-decision + the iOS-16 API audit; challengers (state-mechanism,
maps, future-cost) attacked; the Lead verified the iOS-version API facts and the parity preservation and
adjudicated. **Verdict: all challenges RESOLVED; zero blocking; consensus reached.** (ADR-0013's
already-decided parts were **not** re-litigated — only the floor-affected D2 + the audit.)

| # | Challenge (severity) | Disposition | Where |
|---|---|---|---|
| CH-1 (state) | Dropping `@Observable` for `ObservableObject` loses fine-grained invalidation and adds boilerplate — does it disturb the facade/state parity the whole architecture rests on? (MAJOR — the load-bearing impact) | REBUT + RECORD | D2′: the sealed `UiState`/`ActionState` enums and the facade boundary are **identical** — only the observation *wrapper* changes (`@Published`/`@ObservedObject` vs the macro). The parity to `StateFlow<UiState>` holds. The boilerplate + whole-view-invalidation are recorded as **accepted ergonomics/perf trade-offs** (Consequences), not architecture problems; `ObservableObject` is the most-shipped SwiftUI state pattern. |
| CH-2 (maps) | The SwiftUI MapKit API is reduced on iOS 16 — does that force Mapbox after all, undoing D6? (MAJOR) | REBUT | D6′ + Alternatives: only the *SwiftUI* MapKit surface is iOS-17; **UIKit `MKMapView` is fully featured on iOS 16** (overlays, polygons, custom annotations). The rich surfaces route through a `UIViewRepresentable` **inside** `MapKitMapProvider` — the D6 seam absorbs it with **zero** feature impact. MapKit-default stands; the floor is not a reason to flip to Mapbox. |
| CH-3 (cleanliness) | Why not back-port `@Observable` behind `#available` so the codebase is "ready" for an iOS-17 bump? (MODERATE) | DEFEND | Alternatives: dual observation paths double the maintenance for zero benefit on an iOS-16 floor (the fallback always runs). One mechanism is the catalog "one way" posture; D2′'s migration note already makes a future bump mechanical (enums + facade unchanged), so no `#available` scaffolding is needed now. |
| CH-4 (silent shifts) | Does anything ELSE in ADR-0013 quietly break on iOS 16 (String Catalog, NavigationStack, Stripe, Keychain, codegen)? (MAJOR — the audit must be complete, not assumed) | REBUT (audited) | Context table + D11 note + Nav confirmation: String Catalog is a **build-time** format (runtime irrelevant — verified); `NavigationStack` is **iOS 16+** (available); Stripe `stripe-ios` is iOS 13+; Keychain/APNs/`URLSession`/`actor`/SPM all ≤ 16; the generated swift5 client is plain Swift/Foundation. **Only** `@Observable` (→ D2′) and the SwiftUI MapKit API (→ D6′ variant) shift; everything else holds. |

**Affirmed unchallenged:** Q-IOS-01 = iOS 16 (the owner's reach decision); the package shape, DI,
auth/session/header layer, codegen, Stripe, push, partner-first, trusted-device-omit, and the 5-locale
String Catalog all hold unchanged on the floor; the `UiState`/`ActionState` enums + the `ApiResult`
contract (ADR-0011) are floor-independent.

**Lead verification (iOS-version API facts, 2026-06-23):** `@Observable`/Observation = iOS 17 (→ replaced
by `ObservableObject`/`@Published`, iOS 13+); SwiftUI `Map {...}` content builder + `Marker`/`MapPolygon`/
`MapCameraPosition` = iOS 17 (→ iOS-16 `Map(coordinateRegion:annotationItems:)` + `MKMapView` via
`UIViewRepresentable`); `NavigationStack` = iOS 16 (available); String Catalog `.xcstrings` = Xcode-15
build-time, compiles to `.strings`/`.stringsdict` (runtime floor-independent); `stripe-ios` PaymentSheet =
iOS 13+; Keychain/APNs/`URLSession` async/`actor`/SPM all ≤ iOS 16. iOS 16 runs on iPhone 8/8 Plus/X (2017);
iOS 17 needs XS/XR (2018) — the reach delta the owner prioritised.

**Escalations to the owner:** none new — this ADR *records* the owner's Q-IOS-01 answer. Q-IOS-02/Q-IOS-03
remain as ADR-0013 left them.
