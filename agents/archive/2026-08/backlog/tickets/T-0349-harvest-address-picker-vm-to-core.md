---
id: T-0349
title: "Harvest: hoist the address-picker ViewModel into CleansiaCore (unify partner + customer)"
status: done
size: S
owner: architect
created: 2026-06-29
updated: 2026-06-30
depends_on: [T-0313]
blocks: []
stories: []
adrs: [0013, 0014]
layers: [ios, architect]
security_touching: false
manual_steps: []
sprint: 12
source: T-0313 Slice C reviewer (the address-picker VM is duplicated partner↔customer; hoisting is a "one way" + committed-partner-surface call)
---

> **Harvest / "one way" call for the architect — NOT a defect.** T-0313 Slice C shipped a customer-local
> address-picker ViewModel that is a near-exact copy of the partner's (`CleansiaPartner/.../Profile/AddressPicker/AddressPickerViewModel.swift`),
> both riding the Core `MapProvider`/`GeocodingService` seam (ADR-0013 D6). There is **no existing Core
> address-picker VM** to reuse (so this was NOT a reuse-fail — correctly applied-now + flagged), but the two
> copies should be unified into one Core type. Hoisting (a) edits the already-committed partner surface and
> (b) declares "the one way to do an address-picker VM" — both architect calls, deliberately deferred from the
> feature slice.

## Scope
- Hoist the address-picker VM into `CleansiaCore` (e.g. `CleansiaCore/Location/AddressPickerViewModel`), parameterized
  (the customer copy already generalized `searchBias` with a `["cz","sk"]` default — keep that).
- Repoint both `CleansiaPartner` (`Profile/AddressPicker`) and `CleansiaCustomer`
  (`Booking/WhenWhere/AddressPicker`) at the Core VM; delete the two app-local copies.
- The View stays app-local (it imports MapKit for the `MapProvider.pickerMap` seam's `MKCoordinateRegion`
  signature — that's the sanctioned seam boundary); only the VM hoists.
- Architect rules the Core home + the "one way" entry; reviewer confirms partner + customer non-regression
  (the partner AddressPicker tests + the customer booking address tests stay green).

## Done when
- [x] One Core address-picker VM; partner + customer both consume it; the two app-local copies are gone.
- [x] Partner + customer address flows non-regressing (existing tests green).
- [x] patterns-mobile.md "one way" entry updated; the T-0313 Slice-C harvest-candidate flag resolved.

## Status log
- 2026-06-29 — filed from the T-0313 Slice C review. Customer-local copy ships in T-0313 (parity with the
  partner's app-local copy); this unifies them — an architect "one way" + committed-partner-surface decision.
- 2026-06-30 — implemented on `phase/hardening-1` (test-first). Added public
  `CleansiaCore/Sources/CleansiaCore/Location/AddressPickerViewModel.swift` (customer's parameterized shape,
  `searchBias: [String] = ["cz","sk"]`); deleted both app-local copies; repointed the customer
  `BookingAddressPickerView` (`BookingAddressPickerViewModel` → `AddressPickerViewModel`); partner View needed
  no change (same type name resolves to Core). Partner VM test kept in `CleansiaPartner/Tests`, now exercising
  the Core type (assertions UNTOUCHED, incl. `lastForwardBias == ["cz","sk"]`); added a thin Core
  `AddressPickerViewModelTests` (default + injected-bias forwarding). No injected-dependency decision needed —
  the VM already depended only on Core types (`GeocodingService`/`GeocodedAddress`/`Coordinate`/`ViewModel`).
  Regenerated both Xcode projects (xcodegen). Tests all green: Core 218, Partner 366, Customer 362. swiftformat
  0/501 (0.60.1), swiftlint --strict 0 violations. Catalog updated (`patterns-mobile.md` + the living
  `ios-app-architecture.md`); the T-0313 Slice-C harvest-candidate flag resolved. NOT committed — reviewer runs next.
- 2026-06-30 — **proposed → done** (HARDENING-1, `d834e92` on `phase/hardening-1`, off master `3e7ce52`).
  Reviewer **APPROVE** (partner + customer non-regression confirmed). Public framework-pure
  `CleansiaCore/Location/AddressPickerViewModel` (customer's parameterized shape, `searchBias: [String] =
  ["cz","sk"]` default — load-bearing for the partner non-regression); both app-local copies deleted; partner
  View needed no change (same type name now resolves to Core), customer `BookingAddressPickerView` repointed;
  Views stay app-local (the sanctioned `pickerMap`/`fullBleedMap` MapKit binding). Tests **Core 218 / Partner
  366 / Customer 362** green; swiftformat 0.60.1 + swiftlint --strict clean. **No new ADR** (home change via
  the ADR-0013 escape clause — a provider/home edit, recorded as a living-doc note, not a contract change).
  Harvest recorded in `patterns-mobile.md` + `ios-app-architecture.md`. NOT committed by the PM — the owner
  commits the backlog edits with the phase PR.

## Architect ruling (2026-06-30)

**Verdict: APPROVED as a clean composition of ADR-0013 D6 + ADR-0014. No new ADR.** This hoist introduces
no new decision — it applies the already-accepted Core/seam shape to a duplicated VM. It is recorded as a
living-doc + `patterns-mobile.md` "one way" edit per ADR-0013's own escape clause ("a future need for a
… home is revisited … a living-doc note if it only changes a provider/home"). Hoisting a VM's *home* is
exactly a home change, not a contract change.

**1. Core home.** `CleansiaCore/Location/AddressPickerViewModel` (the `Sources/CleansiaCore/Location/`
package dir that already houses the map/geocode seam — `MapProvider`, `GeocodingService`,
`MapKitMapProvider`, `Coordinate`, `GeocodedAddress`). The VM already depends only on Core types
(`GeocodingService`, `GeocodedAddress`, `Coordinate`, `ViewModel`) — it has zero app-local deps, so the
move is mechanical. Name it `AddressPickerViewModel` (drop the customer's `Booking`-prefix; the type is
generic, not booking-specific) and make it `public` + `public init`.

**2. Public API = the customer's parameterized shape.** The Core VM keeps the customer copy's
`init(geocoding:, reverseDebounce: = .milliseconds(500), searchDebounce: = .milliseconds(300),
searchBias: [String] = ["cz","sk"])`. The `["cz","sk"]` **default is load-bearing for non-regression**:
the partner today hardcodes `["cz","sk"]` and its test asserts `lastForwardBias == ["cz","sk"]`
(`AddressPickerViewModelTests.swift:149`). Partner repoints by calling the default (passes no `searchBias`)
→ the assertion stays green with no test edit. `searchBias` is the only intended variation point; it maps
1:1 to `GeocodingService.forwardGeocode(query:, countryIsoCodes:)`. Do **not** widen the API beyond this.
(Aligns with the per-country-variation seam: the country bias is a caller-supplied parameter, never a
country-code branch inside the VM — a future `searchBias` driven off booking country/`CountryConfiguration`
slots in at the call site without touching Core.)

**3. The View stays app-local — only the VM hoists.** Both `AddressPickerView` (partner) and
`BookingAddressPickerView` (customer) `import MapKit` and bind `pickerMap(region: Binding<MKCoordinateRegion>,
showsUserLocation:)` — `MKCoordinateRegion` is the sanctioned seam boundary in `MapProvider.swift:5`. ADR-0013
D6 invariant #7 ("no feature imports MapKit directly … all map use goes through `CleansiaCore`'s
`MapProvider`") is about *geocode/map logic*, not the View's binding to the protocol's MapKit-typed signature
— that MapKit import is the documented, allowed View-layer touch. The two Views remain distinct (partner vs.
booking chrome/L10n/navigation differ); they are out of scope for unification here. Only the VM is shared.

**4. Repoint plan.** (a) Add `public AddressPickerViewModel` under `CleansiaCore/Location/`. (b) Partner:
`AddressPickerView` constructs `AddressPickerViewModel(geocoding:)` (default bias) — type now resolves to the
Core type via the existing `import CleansiaCore`; delete
`CleansiaPartner/.../Profile/AddressPicker/AddressPickerViewModel.swift`. (c) Customer:
`BookingAddressPickerView` constructs `AddressPickerViewModel(geocoding:)` (rename the local reference from
`BookingAddressPickerViewModel`); delete
`CleansiaCustomer/.../Booking/WhenWhere/AddressPicker/BookingAddressPickerViewModel.swift`. (d) Move the
partner VM test into Core (`CleansiaCore/Tests`) as the canonical suite, OR keep it in
`CleansiaPartner/Tests` re-pointed at the Core type — reviewer's call; either way the assertions are
unchanged. No `manual_step` (no codegen, no migration).

**5. Non-regression gate (reviewer).** Partner `AddressPickerViewModelTests` (12 cases, incl. the
`["cz","sk"]` bias assertion) stay green unchanged. Customer side: there is **no dedicated booking-address
VM test today** (the T-0313 copy shipped without one) — non-regression rides on the partner VM suite (same
logic, now the shared type) plus the customer booking-flow tests that drive the picker. Reviewer should
confirm the customer booking address-pick path still resolves+confirms; a thin Core-level test asserting the
non-default `searchBias` is forwarded would harden the parameterization but is not required to close this.

**6. Catalog edit.** Update `patterns-mobile.md` iOS section: the address-picker VM is a **Core type**, the
View is app-local and the **only** sanctioned MapKit import at the feature layer is the `pickerMap` /
`fullBleedMap` protocol binding. Resolve the T-0313 Slice-C harvest-candidate flag. The living companion
`agents/architecture/decisions/ios-app-architecture.md` gets the same note.
