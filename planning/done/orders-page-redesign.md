# Orders page redesign — analysis + plan

> **Status:** ✅ Shipped 2026-05-19. Wolt-style OfferCard with slide-to-take,
> right-edge chevron, distance badge (LocationProvider with 3-tier
> fallback), per-list NgRx slices, plurals for scope, and inline
> per-tab refresh wired end-to-end on partner Android. Backend
> `CustomerAddressApproximate` field shipped to gate full address
> behind take. Customer wizard city-served guard shipped alongside.
>
> Spec for the next session. Frozen here so we don't re-investigate next time.
> **Owner has approved all design decisions** in the questions thread on
> 2026-05-17. Just execute when the next session starts.

## Why redesign

Current Orders page shows identical rows in all three tabs (Available /
Active / Completed). Each row contains only `#ORD-XXXX` + date. Cleaner has
no way to evaluate a job without drilling into details — every decision
requires multiple taps. Money, location, and scope are all absent.

## Cleaner's mental model per tab

| Tab | Question they're answering | What they need on the row |
|---|---|---|
| **Available** | "Is this job worth taking?" | **Money** + time + distance + scope + one-tap Take |
| **Active** | "What's next, what comes after?" | Pinned in-progress + grouped by day + inline status action |
| **Completed** | "How much did I earn this period?" | Summary card + period filter + per-row pay + rating |

## Target layout per tab

### Available
- Top: search bar + sort dropdown
- Summary line: `6 jobs · earn up to €860 · Sort: ▼`
- Each row shows: **€amount (cleaner pay, not customer price)** + time-relative
  (Today 14:00) + duration + distance-from-current-location + customer
  address + rooms/bathrooms/extras count + inline `[ Take → ]` button
- Highest-value job gets a 🔥 chip; starting-soon (<2h) gets a clock chip

### Active
- Pinned in-progress card at top (if any) with gradient + Complete CTA
- Grouped by day (Today / Tomorrow / Later)
- Compact rows (60dp) with time + city + amount + status-dependent inline
  action: Confirmed → "On the way", OnTheWay → "Start", InProgress → "Complete"

### Completed
- Summary card: `€sum · count jobs · ★ avg` for selected period
- Period filter chip: This week / This month / Last month / All
- Grouped by day with day-header chips
- Each row: customer name + time + amount + per-job ★ rating + actual
  completion time

## Backend changes required

Findings from investigation (some work is already done, **flagging discoveries** below):

| Item | Status | Notes |
|---|---|---|
| `Address.Latitude` / `.Longitude` columns | **EXISTS** | Already on the entity, EF mapping in place. No migration needed. |
| `IGeocodingService` (Mapbox) | **EXISTS** | `Cleansia.Infra.Services.Geocoding.MapboxGeocodingService` registered scoped. Handles missing token + network failures. **Unused** — no call site populates Address coords. |
| `PayCalculator` + `EmployeePayConfig` | **EXISTS** | `Cleansia.Core.Domain.EmployeePayroll.Services.PayCalculator` with extensions. Need to wire from `OrderListItem` mapper. |

**Work needed:**

1. **`AddressGeocoder` orchestrator** in AppServices that wraps `IGeocodingService` + country-ISO lookup. Register in DI. (Code drafted last session, deleted intentionally to keep this session's diff small. Re-create.)
2. **Wire `AddressGeocoder.PopulateCoordinatesAsync`** into the 4 Address.Create / Update call sites:
   - `Cleansia.Core.AppServices/Features/Employees/UpdateAddressInfo.cs:81`
   - `Cleansia.Core.AppServices/Features/Employees/AdminUpdateEmployee.cs:125`
   - `Cleansia.Core.AppServices/Features/Employees/UpdateEmployee.cs:234`
   - `Cleansia.Core.AppServices/Features/Orders/CreateOrder.cs:434`
3. **Lazy backfill** in `GetPagedOrders` handler: after fetching the page, scan
   for `Order.CustomerAddress` with null coords and fire-and-forget a geocode
   on a background task. Next call sees the populated row. Self-healing.
4. **Extend `OrderListItem` DTO** with:
   - `decimal? EstimatedCleanerPay` — computed via `PayCalculator` per employee.
     For Available tab: use the requesting employee's PayConfig. For Active/
     Completed: use the persisted `OrderEmployeePay.TotalPay` if a row exists,
     else recompute.
   - `double? CustomerAddressLatitude`, `double? CustomerAddressLongitude` —
     exposed for client-side Haversine. Already on the Address entity.
5. **Extend `OrderListItem` mapper** (`OrderMappers.cs`) to populate the new
   fields. PayConfig lookup needs an `IEmployeePayConfigRepository` dependency.
6. **Extend `GetPagedOrders` Sort** to support: `totalPrice` (desc),
   `estimatedCleanerPay` (desc), `cleaningDateTime` (asc — already works).
   The mapping happens in whatever query builder backs `GetPagedOrders`.

## Mobile changes required

1. **Add `play-services-location` dependency** to `partner-app/build.gradle.kts`.
   Same artifact customer-app uses if any (else Google Play Services 21+).
2. **Runtime permission flow**:
   - `ACCESS_FINE_LOCATION` permission in manifest
   - On Orders tab first composition, `LaunchedEffect(Unit)` requests permission
   - Rationale screen if denied once: "Show distance to jobs — allow location"
   - Hard-deny gracefully degrades to "no distance, but everything else works"
3. **`LocationProvider` Hilt singleton**:
   - Wraps `FusedLocationProviderClient`
   - Exposes `currentLocation: StateFlow<LatLng?>`
   - Single `getLastLocation()` call on Orders-tab entry (cheap)
   - Optional 30s refresh while tab is foregrounded
4. **Haversine helper** in `cz.cleansia.partner.core.location.Distance.kt`
   (or `:core` if customer-app needs it later)
5. **`OrdersListViewModel`** extensions:
   - Inject `LocationProvider`
   - Expose `searchQuery: String`, `sortBy: SortOption`
   - Derive per-row distance from `LocationProvider.currentLocation` + each
     order's `customerAddressLatitude/Longitude`
6. **`OrdersListScreen` rebuild** — completely fresh composition with the 3
   different row shapes per tab per design above
7. **~30 new strings × 5 locales**

## Out of scope for next session

- Service-area-based filtering (no schema concept yet)
- Background location updates while app closed (battery-hostile, not worth it)
- Map view of available jobs (probably worth its own session if asked)
- Customer rating on completed rows requires `OrderReview` join in the list
  query — flag if expensive, may need a separate batched lookup

## Pacing for next session

- **Backend pass**: ~1.5–2h (AddressGeocoder + 4 call site updates + DI + lazy
  backfill + DTO extension + mapper + sort)
- **Owner**: restart partner-mobile-api + regen OpenAPI spec
- **Mobile pass**: ~2.5–3h (location flow + permission UX + 3 row shapes +
  search + sort + grouping + summary card + Haversine + period filter + strings)

Whole thing fits in one focused session if we don't bikeshed mid-flight.
