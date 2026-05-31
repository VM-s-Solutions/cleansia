# Service areas — design + plan

> **Status:** ✅ Shipped 2026-05-19. All 4 sessions complete. Country.IsServiced
> flag + ServiceCity entity + validators across 4 backend commands +
> customer wizard `getServiced()` swap + admin Service Area management UI
> shipped. Seed `Countries.IsServiced` (CZE=true, 40 others=false) and 10
> Czech ServiceCities rows added to `sql-scripts/insert_seed_data.sql`.
>
> Spec for upcoming sessions. **Owner has approved all design decisions** in
> the questions thread on 2026-05-17. Execute over the 4 sessions below.
>
> Triggering bug: customer wizard auto-defaults `countryId` to
> `countries[0]` from an alphabetical `GetCountryOverview` list — so
> "Argentina" wins and CZ addresses are persisted with `CountryId = Argentina`.
> Backend then trusts that, geocoding biases wrong, and the entire
> address-resolution pipeline corrupts the data.

## Root cause

Two layers of missing concept:

1. **Default-country resolution is a guess.** The customer order wizard does
   `countries.find(...) ?? countries[0]` over the full `Country` catalog,
   sorted alphabetically. There's no "this is our default" signal in the
   data, and the picker is hidden from the user, so the bad default ships
   silently.
2. **No "service area" concept at all.** `Country.IsActive` is a binary
   admin-catalog flag — it doesn't distinguish "we operate here" from "we
   know this country exists". `ServiceCity` doesn't exist. So the system has
   no way to say "we serve Praha and Brno in CZ; we don't serve anywhere
   else yet".

The fix is to introduce **two layers of service-area data** and rewire the
5 frontends and 4 address-touching backend commands to use them.

## Conceptual model

**Layer 1 — Served countries.** `Country.IsServiced: bool` (new). A country
we operate in. Customer/partner-facing pickers see only `IsServiced && IsActive`.
Admin sees the full catalog. CZ-only today, expand by flipping the flag.

**Layer 2 — Served cities.** New `ServiceCity` entity (per country).
Customer order creation must match a `ServiceCity` (by city name,
case-insensitive). Employee addresses do NOT need this — cleaners can live
anywhere, they commute to served cities for jobs.

**ZIP-prefix support (Phase 2-ready).** `ServiceCity.ZipPrefix: string?`
ships in v1 schema but is **unused** by the v1 validator (city-name match
only). Future-proofing: adding a column under load is painful; flipping a
validator behind a flag isn't. Day-1 schema flexibility, day-1 behavioural
simplicity.

## Backend changes

### New entity

```csharp
public class ServiceCity : Auditable, ITenantEntity
{
    public string CountryId { get; private set; }
    public Country Country { get; private set; }
    [MaxLength(100)] public string Name { get; private set; }       // "Praha"
    [MaxLength(20)] public string? ZipPrefix { get; private set; }  // future use only in v1
    public bool IsActive { get; private set; } = true;

    public static ServiceCity Create(string countryId, string name, string? zipPrefix = null) =>
        new() { CountryId = countryId, Name = name.Trim(), ZipPrefix = zipPrefix?.Trim() };
}
```

EF config: unique index on `(CountryId, NormalizedName)` where
`NormalizedName = Name.ToLower()`. `ZipPrefix` indexed (lookup speed once
enforcement turns on).

### Country entity diff

```csharp
public bool IsServiced { get; private set; } = false;
public Country SetServiced(bool isServiced) { IsServiced = isServiced; return this; }
```

Existing `IsActive` stays as-is — it means "exists in catalog", governs
admin pickers only.

### Migration

- Add `Countries.IsServiced` (default false).
- Create `ServiceCities` table.
- **Seed CZE with `IsServiced = true`.**
- Seed initial cities for CZE: Praha, Brno, Plzeň, Ostrava, Liberec, Olomouc,
  České Budějovice, Hradec Králové, Ústí nad Labem, Pardubice. (Owner can
  edit via admin UI later — these are starter rows so v1 isn't empty.)

### New repositories

```csharp
public interface IServiceCityRepository : IRepository<ServiceCity, string>
{
    Task<IReadOnlyList<ServiceCity>> GetByCountryAsync(string countryId, CancellationToken ct);
    Task<ServiceCity?> FindMatchAsync(string countryId, string cityName, CancellationToken ct);
}
```

`ICountryRepository` gains:
```csharp
Task<IReadOnlyList<Country>> GetServicedAsync(CancellationToken ct);
```

### New AppService features

- `Features/Countries/GetServicedCountries.cs` — IRequest, IRequestHandler,
  no validation. Returns `CountryListItem[]` filtered to `IsServiced && IsActive`,
  sorted by `Name`.
- `Features/ServiceAreas/GetServiceCities.cs` — optional `?countryId=`
  filter. Returns flat list across all serviced countries when unset.
- Admin-only CRUD:
  - `Features/ServiceAreas/CreateServiceCity.cs`
  - `Features/ServiceAreas/UpdateServiceCity.cs`
  - `Features/ServiceAreas/DeleteServiceCity.cs`
  - `Features/Countries/SetCountryServiced.cs` (toggles `IsServiced`).

### New controllers / endpoints

| Host | Endpoint | Purpose |
|---|---|---|
| Customer (Mobile + Web) | `GET /countries/serviced` | populates pickers |
| Customer (Mobile + Web) | `GET /service-cities` | for Mapbox bias + city validator |
| Partner (Mobile + Web) | `GET /countries/serviced` | employee profile picker |
| Admin | `GET /service-cities` (full) | admin list |
| Admin | `POST/PUT/DELETE /service-cities` | CRUD |
| Admin | `PUT /countries/{id}/serviced` | toggle |

### Validation hookup (4 commands)

Existing commands touching `Address.Create` / `Address.Update`:

1. **`Cleansia.Core.AppServices/Features/Orders/CreateOrder.cs:434`** —
   customer flow. Must enforce **country IsServiced** AND **city in ServiceCity**.
   Replace the alphabetical fallback (line 432-435) with: if no `CountryId`
   supplied, fall back to the single serviced country; if more than one
   serviced country exists, reject with `country.required`.
2. **`Cleansia.Core.AppServices/Features/Employees/UpdateAddressInfo.cs:81`** —
   self-edit by employee. Enforce **country IsServiced only** (no city check).
3. **`Cleansia.Core.AppServices/Features/Employees/UpdateEmployee.cs:234`** —
   self-edit during onboarding. Enforce **country IsServiced only**.
4. **`Cleansia.Core.AppServices/Features/Employees/AdminUpdateEmployee.cs:125`** —
   admin edit of employee. Skip both checks (admin can record an address
   anywhere — they're recording reality, not creating service obligation).

New error keys in `BusinessErrorMessage`:
- `country.not_serviced` — "We don't currently operate in this country."
- `country.required` — "Please select a country." (used when multi-serviced and none picked)
- `city.not_serviced` — "We don't currently serve this city."

Each gets entries in all 5 i18n locales × 3 frontend apps.

## Frontend changes (5 apps)

### Customer web — `Cleansia.App/libs/cleansia-customer-features/order-wizard`

`order-wizard.facade.ts:543` is the bug site:
```ts
this.customerClient.countryClient.getOverview()  // ← REPLACE
this.customerClient.countryClient.getServiced()  // ← WITH (after NSwag regen)
```

```ts
if (countries.length > 0 && !this.formData().address.countryId) {
  // Replace `countries[0]` with explicit single-serviced fallback.
  // If multiple serviced countries exist, require user selection (don't auto-pick).
  const onlyOne = countries.length === 1 ? countries[0] : null;
  if (onlyOne) {
    this.updateFormData({
      address: new AddressDto({ ...this.formData().address, countryId: onlyOne.id ?? '' }),
    });
  }
}
```

Customer profile address screens: same swap from `getOverview()` to `getServiced()`.

Address picker (Mapbox geocoder integration): pass `country=` query param from
the serviced-country ISO codes. Locate the geocoder call in customer-features
and inject `served.map(c => c.isoCode.toLowerCase()).join(',')`.

City validation: after Mapbox picker confirms an address, call
`/service-cities?countryId=X`. If the picked city name (case-insensitive) isn't
in the response, show inline error "We don't serve this city yet" and disable
the wizard's Continue button. Backend re-validates on submit.

**Per owner decision:** country is shown on the chosen address card already
(once geocoded — it's part of the address string). Don't add a separate
country dropdown to the wizard form. The country IS visible at the address
picker step (in the Mapbox suggestions, biased to served countries).

### Customer mobile — `cleansia_customer`

- `core/data/CountryRepository.kt` (or wherever it lives): swap to
  `/countries/serviced`.
- `features/addresses/AddressManagerScreen.kt`: Mapbox forward-geocode
  builder gets `country=cz,...` parameter from the served list.
- Review pane (after picking): show country badge below address (this is
  the "existing address display" case the owner specifically called out).
- On confirm, call `/service-cities?countryId=` and gate the Save action on
  match.

### Partner web — `Cleansia-Partner.App` employee profile

- Country picker on employee onboarding: swap to `getServiced()`.
- No city check (employees can live anywhere).

### Partner mobile — `cleansia_android/partner-app`

- Employee profile address screens: same.

### Admin web — `Cleansia-Admin.App`

- Keep `getOverview()` everywhere — admin needs the full catalog.
- **New "Service area" management page** (under Settings or a top-level nav slot):
  - Tab 1: "Countries served" — table of countries with `IsServiced` toggle.
    Toggling fires `PUT /countries/{id}/serviced`.
  - Tab 2: "Cities served" — country dropdown filter + table of cities for
    selected country + add/edit/delete CRUD modal. Form fields: `name`,
    `zipPrefix` (optional, with helper text "for future use — not enforced
    yet").

## Pacing (4 sessions)

### Session 1 — backend foundation
- Country.IsServiced + ServiceCity entity + EF configs + migration.
- ICountryRepository.GetServicedAsync + IServiceCityRepository.
- All new GET endpoints (customer + admin).
- All admin CRUD endpoints.
- New `BusinessErrorMessage` keys.
- **Manual step:** owner generates + applies EF migration. Seeds Praha
  cities by hand (or via the new admin UI in Session 4 — whichever is
  faster).
- **Manual step:** owner regenerates all 4 NSwag clients (admin, partner,
  customer, mobile-customer/partner).

### Session 2 — backend validation
- Wire `country.not_serviced` + `country.required` + `city.not_serviced`
  validators into the 4 commands.
- Add the error-message i18n entries × 5 locales × 3 frontend apps (15 keys
  total — but most apps need the same 3 keys).

### Session 3 — customer-facing apps
- Customer web: order-wizard + profile + Mapbox geocoder bias + city
  validator + inline error handling.
- Customer mobile: same.

### Session 4 — partner + admin
- Partner web + mobile: country pickers use `getServiced()`.
- Admin web: new "Service area" management page (2 tabs).
- Acceptance test: book a Czech address as customer → succeeds. Book a
  Slovak address while CZ-only → blocked at picker (no SK in suggestions)
  AND blocked at backend (defense-in-depth).

## Out of scope (explicit)

- **Geofence polygons.** Considered, rejected — would need PostGIS, polygon
  drawing in admin, geocode-in-polygon checks. Defer to "Service area v2"
  once we've validated city-allow-list works.
- **Per-employee service radius.** Different problem — that's about which
  cleaners get offered which jobs based on their personal coverage area.
  Doesn't block this work.
- **Auto-detecting user's country from IP / Mapbox response.** UX nice-to-have
  for when we have >1 serviced country. Not needed for v1 (CZ-only).
- **Validating the rest of the address (street exists, ZIP valid).** That's
  a Mapbox-bias concern — if the bias is set to served countries, Mapbox
  won't suggest nonsense. We don't need to validate it ourselves.

## Risks / edge cases

- **Existing data:** orders / addresses currently saved with `CountryId =
  Argentina` (or other) are real records. Migration must NOT cascade-delete
  them. Strategy: keep them. The validators only fire on new writes. A
  separate one-off SQL script can re-stamp obvious mistakes (e.g.
  `UPDATE Addresses SET CountryId = (SELECT Id FROM Countries WHERE IsoCode = 'CZE') WHERE City IN ('Praha','Brno',...)`) — but that's owner-driven, not part of this plan.
- **Admin-created orders bypass customer rules?** Currently `CreateOrder` is
  also used by admin to record offline bookings. Decision: admin orders
  still get city validation — if admin wants to record an order outside the
  service area, they should first add the city to ServiceCity. Keeps the
  data consistent.
- **City-name match is case-insensitive but accent-sensitive in v1.** "Praha"
  matches, "praha" matches, but a Mapbox response of "Prague" (English-
  language fallback) would NOT match the seed `"Praha"`. Mitigation: seed
  cities with both Czech + English variants, OR normalise more aggressively
  (Unicode NFD + strip diacritics). Defer the normalisation work — log
  mismatches and revisit if real users hit it.
