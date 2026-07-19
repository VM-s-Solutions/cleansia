---
id: T-0335
title: iOS LocationProvider Core seam + the my-location FAB + picker auto-center (gated on T-0325's NSLocationWhenInUseUsageDescription)
status: done
size: M
owner: pm
created: 2026-06-26
updated: 2026-07-19
depends_on: [T-0310, T-0325]
blocks: []
stories: []
adrs: [0013, 0014, 0018]
layers: [ios]
security_touching: false
manual_steps: []
sprint: 12
source: sprint-12 §7.6 Decision 2 + §7.7 Scope A (architect)
---

> **Deferred — NOT to be implemented now; gated on T-0325.** Surfaced by T-0306 (§7.6 D2), homed to T-0310, then
> re-homed here by the T-0310 Understand pass (§7.7 Scope A) because **T-0325 (the
> `NSLocationWhenInUseUsageDescription` Info.plist key) is still `proposed`** — an open owner manual_step. Without
> that key the iOS permission prompt never appears, so building the my-location FAB would ship a **dead control**
> (the §7.6/T-0331 dead-control precedent). The AddressPicker is fully usable without it (pan + search at full
> parity, Prague-default center). **No-decision note (panel skipped):** parity build of an Android affordance
> behind the Core map/location seam, once its owner plist dependency is live; no new behavior/decision (§7.6 D2 +
> §7.7 Scope A own the defer + recorded the `LocationProvider` shape).

## Context

The Android partner AddressPicker auto-centers on the FusedLocation fix on open + shows a my-location FAB
(`partner-app/.../features/profile/AddressPickerScreen.kt:131-161` auto-center / permission launch, `:272-295`
the FAB; via `core/.../location/LocationService.kt`). iOS deferred this twice:

- **T-0306 (§7.6 D2):** the picker shipped pan + search at full parity, centered on the **Prague default**
  (`14.4378, 50.0755`, zoom 15 — `AddressPickerScreen.kt:90-91`); current-location homed to T-0310 IF T-0325's
  plist key existed.
- **T-0310 (§7.7 Scope A):** T-0325 is still `proposed`, so the FAB stayed deferred (a dead control without the
  key); T-0310 wired the picker into the Address section with pan/search only. The **`LocationProvider` protocol
  shape was recorded** in §7.7 Scope A (the Core seam design) but NOT built.

This ticket builds the seam + FAB once T-0325 has landed the plist key.

## Acceptance criteria
- [x] **AC0 — T-0325 landed (the gate).** `NSLocationWhenInUseUsageDescription` is present (localized ×5) in the
  partner app's Info.plist/`project.yml`. **This ticket does not start until T-0325 is `done`.**
- [x] **AC1 — `LocationProvider` Core seam.** A `CleansiaCore/Location` protocol (the §7.7 Scope A shape):
  `authorizationStatus`, `requestWhenInUseAuthorization() async`, `currentLocation() async -> Coordinate?`
  (best-effort, `nil` on denied/unavailable — never throws to the FAB); default impl
  `CLLocationManagerLocationProvider` — **the only CoreLocation consumer besides the map/geocoding providers**
  (feature/VM imports neither — reviewer #7/#27). The `LocationService.kt` parity.
- [x] **AC2 — The my-location FAB + auto-center on the AddressPicker.** On open, request when-in-use auth; on
  grant, center on the fix; the FAB re-centers on tap (the `AddressPickerScreen.kt:131-161,272-295` parity).
  Denied/restricted degrades gracefully to the Prague default (no crash, no nag loop).
- [x] **AC3 — Gate-DP closed + gates green.** Cite `AddressPickerScreen.kt`; native SwiftUI; the §7.6/§7.7
  current-location divergence is now closed (the affordance present). The Info.plist purpose string + privacy-
  manifest location entry are carried **in this ticket** (Gate-AR / check #16 — not deferred). `CleansiaCore` +
  both targets compile; Swift suites green; blocking SwiftLint/SwiftFormat (T-0323) passes; reviewer #27/#28
  (the FAB is now expected, no longer a dead-control finding).

## Out of scope
- **No background location / always-on** — when-in-use only (matches Android FusedLocation usage + the T-0325
  key). No `NSLocationAlwaysUsageDescription`.
- **No location use outside the AddressPicker** — this is the picker auto-center affordance only.

## Implementation notes
Mirror `core/.../location/LocationService.kt` + `AddressPickerScreen.kt:131-161,272-295`. The `LocationProvider`
shape is recorded in `architecture/decisions/ios-app-architecture.md` (§"Partner Profile tab" / Scope A) +
sprint-12 §7.7 Scope A. Reviewer-per-developer; no `security` gate (a permission affordance, no authz/endpoint
surface — but the privacy-manifest/purpose-string carry is a Gate-AR check); no `optimizer`. **Routing:** `[ios]`.

## Status log
- 2026-06-26 — draft (created by architect ruling, sprint-12 §7.6 D2 + §7.7 Scope A). Deferred + gated on T-0325
  (still `proposed`); building the FAB before the plist key is a dead-control finding. The `LocationProvider`
  protocol shape was recorded in §7.7 Scope A (not built). Dedup-checked: not an existing INDEX ticket.
  `depends_on: [T-0310, T-0325]`; `security_touching: false`; `manual_steps: []` (the plist key is T-0325's owner
  step, tracked there). No panel (no-decision: parity build behind the recorded Core seam once the dependency is
  live).
- 2026-07-19 — **done** (ios). T-0325 landed first (owner approved the copy ×5 the same day — AC0 satisfied).
  Test-first: `AddressPickerViewModelLocationTests` (13 stubbed permission/fix flow tests) +
  `LocationAuthorizationStatusMappingTests` (5 CL-status mapping tests) written red, then the seam built green.
  **AC1:** `CleansiaCore/Location/LocationProvider.swift` — the exact §7.7 Scope A shape
  (`authorizationStatus` / `requestWhenInUseAuthorization() async` / `currentLocation() async -> Coordinate?`,
  best-effort nil, never throws) + `CLLocationManagerLocationProvider` (`@preconcurrency CLLocationManagerDelegate`;
  fresh `requestLocation()` fix → cached `manager.location` fallback → nil — the `LocationService.kt` layered
  parity) as the ONLY CoreLocation consumer besides `CLGeocoderGeocodingService`; injected via a Core
  `\.locationProvider` EnvironmentValues key (live default; DEBUG `PreviewLocationProvider` mirrors
  `PreviewMapProvider`). Feature/VM code imports no CoreLocation (reviewer #7/#27).
  **AC2:** `AddressPickerViewModel` (Core, shared) gains `autoCenterOnOpen`/`recenterOnMyLocation` + a one-shot
  `locationFailed` (.denied/.unavailable) subject; both pickers (partner `AddressPickerView`, customer
  `BookingAddressPickerView` — booking + AddressManager reuse) get the my-location FAB above the confirm card +
  on-open auto-center (`AddressPickerScreen.kt:131-161,272-295` parity). Already-denied/restricted on open stays
  SILENT on the Prague default (iOS never re-prompts — the no-nag divergence from Android's every-open snackbar,
  per the AC2 "no nag loop" wording); a fresh prompt answered denied and every explicit FAB tap do report
  (snackbar, app-local ×5 keys mirroring the Android per-app strings: partner `address_picker_location_*`,
  customer `address_picker_my_location_*`).
  **AC3:** purpose string ×5 via T-0325 (both apps) + the `NSPrivacyCollectedDataTypePreciseLocation`
  app-functionality entry added to BOTH `PrivacyInfo.xcprivacy` (Gate-AR #16 carried here, not deferred).
  Gates: CleansiaCore 358/358 on iPhone 17 + the iPhone14-iOS16 floor; partner 457 (−2 known-local TCC
  `LocalizableCatalogFormatTests`) and customer 586 (−2 known-local Stripe-key `Booking*SubmitTests`) on both
  sims; `swiftformat --lint` 0.60.1 + `swiftlint --strict` 0.65.0 clean. T-0334's
  `servicedCountryCodesProvider` bias wiring untouched.

## Review
<!-- reviewer / qa write verdicts here; PM reconciles before advancing state -->
- 2026-07-19 (ios, harvest note) — `patterns-mobile.md` map-seam row + §"Map seam" prose updated in the same
  change: the two "current-location DEFERRED (T-0310/T-0325)" fragments now record the SHIPPED shape
  (`LocationProvider` behind `\.locationProvider`, `CLLocationManagerLocationProvider` as the sole CoreLocation
  consumer besides geocoding, VM-owned `autoCenterOnOpen`/`recenterOnMyLocation` + one-shot `locationFailed`,
  silent-when-already-denied on open).
