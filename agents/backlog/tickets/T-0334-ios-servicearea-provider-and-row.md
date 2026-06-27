---
id: T-0334
title: iOS ServiceAreaProvider Core seam + the advisory ServiceAreaRow indicator (+ the forward-geocode country-bias it backs)
status: draft
size: M
owner: pm
created: 2026-06-26
updated: 2026-06-26
depends_on: [T-0310, T-0306]
blocks: []
stories: []
adrs: [0013, 0014]
layers: [ios]
security_touching: false
manual_steps: []
sprint: 12
source: sprint-12 §7.7 Decision 3 (architect)
---

> **Deferred — NOT to be implemented now.** Surfaced by the T-0310 Understand pass (sprint-12 §7.7 Decision 3).
> The Android partner Address section has a 3-state advisory `ServiceAreaRow` (in-serviced-city / outside /
> country-not-serviced) driven by `core/servicearea/ServiceAreaProvider`. It is **advisory-only** (never a save
> gate), so T-0310 ships the Address section at full pan/search/save parity **without** it — a recorded Gate-DP
> divergence (architect sign-off). **No-decision note (panel skipped):** porting an already-designed Android
> Core seam at parity behind the existing `MapProvider`/`GeocodingService` factoring; no new behavior or
> architectural decision (sprint-12 §7.7 D3 owns the defer; this is the build of the deferred half).

## Context

Android's partner Address section (`partner-app/.../features/profile/AddressSectionViewModel.kt`) shows a 3-state
service-area indicator row driven by `core/servicearea/ServiceAreaProvider`:

- `ServiceAreaStatus` (`AddressSectionViewModel.kt:50-55`): `InServicedCity` / `OutsideServicedCity` /
  `CountryNotServiced` / `Unknown`, refreshed fire-and-forget (`:163-195`).
- The provider (`core/servicearea/ServiceAreaProvider.kt`) is a `:core` singleton: lazy-cached countries + cities
  (a `Mutex`), `refresh()` on sign-in/config-change, an ISO alpha-2↔alpha-3 reconciliation
  (`AddressSectionViewModel.kt:178-181`), backed by a per-app `ServiceAreaDataSource` adapter over the generated
  client.
- The **same provider also backs the forward-geocode country-bias** (`ServiceAreaProvider.kt:14-16`,
  `servicedCountryIsoCodes()`) — which **T-0306 also deferred** (it shipped unbiased MapKit search).

The row is **explicitly advisory** — `AddressSectionViewModel.kt:165-167` "only feeds the indicator row — failures
degrade to Unknown rather than blocking save"; a cleaner home address "aren't blocked by city — only customer order
creation is" (`:42-44`). The save resolves `countryId` independently (`:213-222`). So T-0310 deferred it
(sprint-12 §7.7 D3) rather than balloon a screen ticket with a Core seam; this ticket is the deferred build.

## Acceptance criteria
- [ ] **AC1 — `ServiceAreaProvider` Core seam ported to `CleansiaCore`.** A lazy-cached provider (countries +
  cities, an actor/`Mutex` equivalent, a `refresh()` hook) + `ServicedCity`/`ServicedCountry` value types + a
  per-app `ServiceAreaDataSource` binding seam each app implements over its generated client (the
  `ServiceAreaProvider.kt` parity). Feature/VM code reaches the provider, not the generated client directly.
- [ ] **AC2 — The advisory `ServiceAreaRow` on the partner Address section.** The 3-state indicator
  (`InServicedCity`/`OutsideServicedCity`/`CountryNotServiced`/`Unknown`) refreshed off the picked address,
  **fire-and-forget, NEVER a save gate** (failures → `Unknown`, the `:163-195` parity). Localized ×5.
- [ ] **AC3 — The forward-geocode country-bias wired.** The `GeocodingService.forwardGeocode(query,
  countryIsoCodes:)` call passes `serviceAreaProvider.servicedCountryIsoCodes()` so search suggestions bias to
  serviced countries (the `ServiceAreaProvider.kt:14-16` use T-0306 deferred). MapKit `MKLocalSearch` region/bias
  applied where supported; a no-bias fallback is acceptable if MapKit can't honor it (note in-ticket).
- [ ] **AC4 — Gate-DP + gates green.** Cite `AddressSectionScreen.kt`/`AddressSectionViewModel.kt`; native
  SwiftUI; the divergence closed (the row now present). `CleansiaCore` + both targets compile; the Swift suites
  green; the blocking SwiftLint/SwiftFormat gate (T-0323) passes; reviewer #28 (the row's presence is now
  expected) + #27 (no feature `import CoreLocation`/`MapKit`; the provider is the seam).

## Out of scope
- **No save gate.** The row never blocks `UpdateAddressInfo`; the save's independent `countryId` resolution
  (`AddressSectionViewModel.kt:213-222`) stays. (`UpdateAddressInfo`'s server-side validator remains authoritative.)
- **No customer-app wiring** — partner Address only (the customer order-wizard service-area indicator is its own
  later ticket, T-0314 cluster).

## Implementation notes
Mirror `core/servicearea/ServiceAreaProvider.kt` + `AddressSectionViewModel.kt:50-55,163-195`. Home the provider
in `CleansiaCore` (the `:core` parity). Reviewer-per-developer; no `security` gate (advisory indicator, no authz/
endpoint surface); no `optimizer`. **Routing:** `[ios]`. **Suggested home:** after T-0310 (the Address section it
attaches to must exist first).

## Status log
- 2026-06-26 — draft (created by architect ruling, sprint-12 §7.7 Decision 3). The advisory `ServiceAreaRow` +
  the `ServiceAreaProvider` Core seam (+ the forward-geocode country-bias it backs, itself deferred from T-0306)
  are deferred out of T-0310 to keep that screen ticket scoped (`M`); the Address section ships pan/search/save
  at full parity without the row (a recorded Gate-DP divergence, architect sign-off). Dedup-checked: not an
  existing INDEX ticket. `depends_on: [T-0310, T-0306]`; `security_touching: false`; `manual_steps: []`. No panel
  (no-decision: parity build of an already-designed Android Core seam behind the existing map factoring).

## Review
<!-- reviewer / qa write verdicts here; PM reconciles before advancing state -->
