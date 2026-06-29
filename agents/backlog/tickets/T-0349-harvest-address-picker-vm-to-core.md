---
id: T-0349
title: "Harvest: hoist the address-picker ViewModel into CleansiaCore (unify partner + customer)"
status: proposed
size: S
owner: architect
created: 2026-06-29
updated: 2026-06-29
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
- [ ] One Core address-picker VM; partner + customer both consume it; the two app-local copies are gone.
- [ ] Partner + customer address flows non-regressing (existing tests green).
- [ ] patterns-mobile.md "one way" entry updated; the T-0313 Slice-C harvest-candidate flag resolved.

## Status log
- 2026-06-29 — filed from the T-0313 Slice C review. Customer-local copy ships in T-0313 (parity with the
  partner's app-local copy); this unifies them — an architect "one way" + committed-partner-surface decision.
