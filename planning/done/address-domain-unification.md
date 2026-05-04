# Address Domain Unification — Phase A (Backend)

**Status:** Ready for execution
**Owner decision:** Option B (backend geocoding) with client hints
**Rationale:** Multi-country expansion + `EmployeePayConfig.distanceRate` means every address needs coordinates; centralizing geocoding prevents per-client drift and future pay-calculation bugs.

---

## Why this plan exists

Audit revealed three divergent address implementations:

- **Backend** — has `Address` + `SavedAddress` entities, 4 commands (Add/Get/SetDefault/Delete), **no Update, no geocoding, no lat/lng columns**.
- **Customer web app** — ignores backend API, persists saved addresses in `localStorage`.
- **Customer Android app** — ignores backend API, persists in DataStore, has Mapbox map-picker producing lat/lng that the backend can't store.

`Address` and `SavedAddress` **stay separate**. The split is load-bearing: `Address` is an immutable snapshot referenced by `Order.CustomerAddressId` and `Employee.AddressId`; `SavedAddress` wraps it per-user with `Label` + `IsDefault`. Collapsing would either denormalize `Order` (inline address fields) or force per-user columns onto `Address`.

**Phase A (this spec) = backend only.** Phase B (mobile wiring) and Phase C (web wiring) are separate specs authored after Phase A + NSwag regen complete.

---

## Task Specs

### TASK-001: Add `Latitude`/`Longitude` to `Address` entity

```yaml
task: Add nullable lat/lng to Address domain entity
id: TASK-001
type: feature
priority: high
specialist: backend
app: backend
estimated_complexity: small
recommended_model: sonnet

context: |
  Address needs nullable Latitude/Longitude (double?) so Orders,
  Employees, and SavedAddresses all share one geographic truth.
  Nullable because: (1) legacy rows pre-migration have no coords,
  (2) geocoding may fail transiently — we save with null and let
  a future job retry. Distance-based pay (EmployeePayConfig.distanceRate)
  will consume these once populated.

files_to_modify:
  - path: src/Cleansia.Core.Domain/Users/Address.cs
    line_range: '7-56'
    change: |
      Add two private-set properties after State (line 22):
        public double? Latitude { get; private set; }
        public double? Longitude { get; private set; }
      Extend Create() (line 27) and Update() (line 37) with
      optional `double? latitude = null, double? longitude = null`
      trailing params; assign them. Anonymize() (line 48) should
      null both coordinates.

  - path: src/Cleansia.Infra.Database/EntityConfigurations/AddressEntityConfiguration.cs
    line_range: '9-29'
    change: |
      After the State property config (line 28), add:
        builder.Property(a => a.Latitude).HasPrecision(9, 6);
        builder.Property(a => a.Longitude).HasPrecision(9, 6);
      Precision 9,6 = ±180.000000 range with ~11cm resolution,
      standard for geo coordinates.

files_to_create: []

dependencies: []

verification:
  - dotnet build src/Cleansia.Core.Domain/Cleansia.Core.Domain.csproj
  - dotnet build src/Cleansia.Infra.Database/Cleansia.Infra.Database.csproj
```

---

### TASK-002: Create `IGeocodingService` + Mapbox implementation

```yaml
task: Backend geocoding abstraction with Mapbox implementation
id: TASK-002
type: feature
priority: high
specialist: backend
app: backend
estimated_complexity: medium
recommended_model: sonnet

context: |
  Server-side geocoding so every address entry point (web, mobile,
  admin, future CSV imports) produces consistent coordinates. Uses
  Mapbox Geocoding API v6 (~$0.50/1000 requests, 100k/month free tier
  covers pre-launch). Client-provided lat/lng hints skip the call.

  Pattern mirrors existing SendGridConfig (reads from appsettings
  section "Mapbox"). Registered via AddInfrastructureServices() in
  Cleansia.Infra.Services so it's available to AppServices handlers
  via DI. Failures are non-fatal — handler catches, logs, saves null.

files_to_create:
  - path: src/Cleansia.Infra.Common/Configuration/Interfaces/IMapboxConfig.cs
    change: |
      Interface with:
        string GeocodingAccessToken { get; }
        string? DefaultCountryIsoCode { get; }  // narrows results
      Section name "Mapbox".

  - path: src/Cleansia.Infra.Common/Configuration/MapboxConfig.cs
    change: |
      Class : AutoBindConfig(configuration, "Mapbox"), IMapboxConfig.
      Same pattern as SendGridConfig.cs lines 6-21.

  - path: src/Cleansia.Core.AppServices/Services/Interfaces/IGeocodingService.cs
    change: |
      namespace Cleansia.Core.AppServices.Services.Interfaces;

      public record GeoCoordinates(double Latitude, double Longitude);

      public interface IGeocodingService
      {
          // Returns null if geocoding fails (network, no match, quota).
          // Callers MUST treat null as "save without coords, don't error".
          Task<GeoCoordinates?> GeocodeAsync(
              string street, string city, string zipCode,
              string? countryIsoCode,
              CancellationToken cancellationToken);
      }

  - path: src/Cleansia.Infra.Services/Geocoding/MapboxGeocodingService.cs
    change: |
      Implements IGeocodingService. Uses HttpClient (injected via
      IHttpClientFactory — add named client "Mapbox"). Calls:
        GET https://api.mapbox.com/search/geocode/v6/forward
          ?q={encoded full address}
          &country={iso2 lowercase}
          &limit=1
          &access_token={config.GeocodingAccessToken}
      Parses response.features[0].geometry.coordinates = [lng, lat].
      On any exception (HttpRequestException, JsonException, timeout,
      no features): log warning with address, return null.
      Log at LogLevel.Warning, not Error — expected failure mode.
      Token blank → return null immediately without HTTP call (dev
      machines without geocoding token still work).

files_to_modify:
  - path: src/Cleansia.Infra.Services/Cleansia.Infra.Services.csproj
    change: |
      Already depends on Cleansia.Infra.Common; verify. Add nothing
      if System.Net.Http.Json is transitively available (.NET 10).

  - path: src/Cleansia.Infra.Services/ServiceCollectionExtensions.cs
    line_range: '10-21'
    change: |
      Inside AddInfrastructureServices(), after the PdfService line 18:
        services.AddHttpClient("Mapbox", c =>
        {
            c.Timeout = TimeSpan.FromSeconds(5);
        });
        services.AddScoped<IGeocodingService, MapboxGeocodingService>();

  - path: src/Cleansia.Config/Services/ServiceExtensions.cs
    line_range: '13-26'
    change: |
      No change here — AddInfrastructureServices() is already called
      at line 23 and registers IGeocodingService.

  - path: src/Cleansia.Web.Customer/appsettings.json
    change: |
      Add top-level section:
        "Mapbox": {
          "GeocodingAccessToken": "",
          "DefaultCountryIsoCode": "cz"
        }
      Blank token = geocoding disabled (service returns null). Real
      token supplied via Azure App Config / user-secrets in production.

  - path: src/Cleansia.Web.Mobile/appsettings.json
    change: Same "Mapbox" section as Customer — mobile API may also geocode.

  - path: src/Cleansia.Web/appsettings.json
    change: Same "Mapbox" section — partner API doesn't geocode today but keep config symmetric.

  - path: src/Cleansia.Web.Admin/appsettings.json
    change: Same "Mapbox" section — admin may create orders too.

dependencies: []

verification:
  - dotnet build Cleansia.Api.sln
  - With blank token, service returns null without error (unit test or manual)
  - With real token, service returns coordinates for 'Václavské nám. 1, Prague, 11000'

manual_steps:
  - type: secret_provisioning
    description: |
      Owner: obtain a Mapbox geocoding-scoped access token (NOT the
      public maps token — geocoding needs a secret-scoped token). Add
      to user-secrets for local dev:
        dotnet user-secrets set "Mapbox:GeocodingAccessToken" "sk.xxx" \
          --project src/Cleansia.Web.Customer
      Repeat for Mobile/Admin/Web. Production: Azure Key Vault.
```

---

### TASK-003: Wire geocoding + coord hints into `AddSavedAddress`

```yaml
task: AddSavedAddress accepts lat/lng hints, falls back to geocoder
id: TASK-003
type: feature
priority: high
specialist: backend
app: backend
estimated_complexity: medium
recommended_model: sonnet

context: |
  Mobile's map picker produces lat/lng already — don't re-geocode
  them (waste of a call). Web will not send hints initially, so
  geocode server-side. Geocoding failure is non-fatal.

files_to_modify:
  - path: src/Cleansia.Core.AppServices/Features/SavedAddresses/AddSavedAddress.cs
    line_range: '14-126'
    change: |
      1. Command record (line 14-23) — add two optional fields before UserId:
           double? Latitude,
           double? Longitude,
         (UserId stays last with = "" default so controller-side `with { UserId = ... }` works.)

      2. Handler ctor (line 72) — inject IGeocodingService:
           Handler(IAddressRepository addressRepository,
                   ISavedAddressRepository savedAddressRepository,
                   ICountryRepository countryRepository,
                   IGeocodingService geocoder,
                   ILogger<Handler> logger)

      3. Handle() — after countryId is resolved (line 87), before the
         address-lookup block (line 90):

           // Prefer client-provided hints; fall back to server geocoding.
           double? lat = command.Latitude;
           double? lng = command.Longitude;
           if (lat == null || lng == null)
           {
               var country = await countryRepository.GetByIdAsync(countryId, cancellationToken);
               var coords = await geocoder.GeocodeAsync(
                   command.Street, command.City, command.ZipCode,
                   country?.IsoCode, cancellationToken);
               if (coords != null)
               {
                   lat = coords.Latitude;
                   lng = coords.Longitude;
               }
               else
               {
                   logger.LogWarning(
                       "Geocoding failed for saved address {City}/{Zip}; saving without coordinates.",
                       command.City, command.ZipCode);
               }
           }

      4. The reuse/create block (line 90-92) — if an existing Address
         matches street/city/zip/country, the existing row already has
         (or lacks) its own coords; we leave them alone to avoid
         overwriting good data with stale hints. If creating a NEW
         Address, pass lat/lng:
           ?? Address.Create(command.Street, command.City, command.ZipCode,
                             countryId, command.State, lat, lng);

      5. No validator changes — coords are always optional.

  - path: src/Cleansia.Core.AppServices/Features/SavedAddresses/DTOs/SavedAddressDto.cs
    line_range: '3-12'
    change: |
      Add two fields before IsDefault:
        double? Latitude,
        double? Longitude,
      Handler Success block (AddSavedAddress.cs line 114-123) now
      populates them from `address.Latitude`, `address.Longitude`.

  - path: src/Cleansia.Core.AppServices/Features/SavedAddresses/GetSavedAddresses.cs
    change: |
      Find the projection from SavedAddress → SavedAddressDto and add
      Latitude/Longitude mapping from sa.Address.Latitude/Longitude.
      Verify nullable handling.

dependencies:
  - TASK-001 # needs lat/lng fields on Address
  - TASK-002 # needs IGeocodingService

verification:
  - dotnet build Cleansia.Api.sln
  - Manual: POST /api/SavedAddress/Add without lat/lng → geocoded server-side
  - Manual: POST with hints → coords used as-is (compare DB row)
  - Manual: Revoke token → save still succeeds with null coords (warning logged)
```

---

### TASK-004: New `UpdateSavedAddress` command

```yaml
task: Add UpdateSavedAddress command for label + address edits
id: TASK-004
type: feature
priority: high
specialist: backend
app: backend
estimated_complexity: medium
recommended_model: sonnet

context: |
  Mobile's "rename" feature has no backend endpoint. Also: when a
  user edits address fields (moved house), we need to update. If
  street/city/zip/country change, we may point the SavedAddress at
  a different Address row (reuse-or-create, like AddSavedAddress).
  Label-only edits skip that entirely.

files_to_create:
  - path: src/Cleansia.Core.AppServices/Features/SavedAddresses/UpdateSavedAddress.cs
    change: |
      Mirror AddSavedAddress.cs structure (Command / Validator / Handler).

      Command record:
        public record Command(
            string SavedAddressId,
            string Label,
            string Street,
            string City,
            string ZipCode,
            string? State,
            string? CountryId,
            double? Latitude,
            double? Longitude,
            string UserId = ""
        ) : ICommand<SavedAddressDto>;

      Validator: same field rules as AddSavedAddress (lines 29-68),
      plus SavedAddressId NotEmpty → Required.

      Handler:
        1. Load SavedAddress by id; return Failure(AddressNotFound) if null.
        2. Ownership check: saved.UserId != command.UserId → Failure(AddressNotOwnedByUser).
        3. Resolve countryId (same fallback logic as AddSavedAddress line 80-87).
        4. Look up Address by (Street, City, ZipCode, countryId). If
           different from saved.AddressId: point saved at the new/reused
           Address (call saved.SetAddressId(newId) — add this method on
           SavedAddress entity; it currently lacks it).
        5. If NEW Address being created, geocode (same logic as TASK-003).
        6. Always: saved.UpdateLabel(command.Label).
        7. Return success SavedAddressDto projection.

files_to_modify:
  - path: src/Cleansia.Core.Domain/Users/SavedAddress.cs
    line_range: '13-47'
    change: |
      Add public method after UpdateLabel (line 36):
        public SavedAddress SetAddressId(string addressId)
        {
            AddressId = addressId;
            return this;
        }

  - path: src/Cleansia.Core.AppServices/Authentication/Policy.cs
    line_range: '32'
    change: |
      No new permission — reuse existing CanManageSavedAddresses.
      (It already gates Add/SetDefault/Delete; Update fits the same scope.)

  - path: src/Cleansia.Web.Customer/Controllers/SavedAddressController.cs
    line_range: '40-51'
    change: |
      After SetDefault endpoint (line 51), add:
        [HttpPut("Update")]
        [Permission(Policy.CanManageSavedAddresses)]
        [ProducesResponseType(typeof(SavedAddressDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> Update(
            [FromBody] UpdateSavedAddress.Command command,
            CancellationToken cancellationToken)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? string.Empty;
            var enriched = command with { UserId = userId };
            var result = await Mediator.Send(enriched, cancellationToken);
            return HandleResult<SavedAddressDto>(result);
        }

dependencies:
  - TASK-001
  - TASK-002
  - TASK-003 # shares the geocoding pattern; do after

verification:
  - dotnet build Cleansia.Api.sln
  - Manual: PUT /api/SavedAddress/Update — label-only edit succeeds without re-geocoding existing Address
  - Manual: PUT with different street → new Address row created/reused, coords geocoded
  - Manual: PUT someone else's SavedAddressId → 400 with AddressNotOwnedByUser
```

---

### TASK-005: `CreateOrder` accepts `SavedAddressId` (optional)

```yaml
task: CreateOrder resolves SavedAddressId as alternative to inline address
id: TASK-005
type: feature
priority: high
specialist: backend
app: backend
estimated_complexity: medium
recommended_model: sonnet

context: |
  Both clients want to book from "my default address" without
  re-typing. Add optional SavedAddressId — handler resolves it to the
  underlying Address. Inline CustomerAddress remains supported (guest
  checkout, first-time buyers). Exactly one of the two must be
  present; validator enforces.

files_to_modify:
  - path: src/Cleansia.Core.AppServices/Features/Orders/CreateOrder.cs
    line_range: '149-163'
    change: |
      Command record — make CustomerAddress nullable, add SavedAddressId:
        public record Command(
            string CustomerName,
            string CustomerEmail,
            string CustomerPhone,
            AddressDto? CustomerAddress,         // was non-null
            string? SavedAddressId,              // NEW
            IEnumerable<string> SelectedPackageIds,
            ... rest unchanged
        ) : ICommand<Response>;

  - path: src/Cleansia.Core.AppServices/Features/Orders/CreateOrder.cs
    line_range: '25-147'
    change: |
      Validator:
        1. Wrap the CustomerAddress field rules (lines 68-93) in:
             When(x => x.CustomerAddress != null, () => { ... });

        2. After the existing `When (CurrencyId)` block (~line 106), add:
             RuleFor(x => x)
                 .Must(cmd => (cmd.CustomerAddress != null) ^ (!string.IsNullOrEmpty(cmd.SavedAddressId)))
                 .WithMessage(BusinessErrorMessage.OrderAddressExactlyOneRequired)
                 .WithName("CustomerAddress");
           (XOR: exactly one of inline-address-or-SavedAddressId.)

        3. When SavedAddressId present, validate ownership in handler
           (not validator — needs DB lookup + scoping to caller).

  - path: src/Cleansia.Core.AppServices/Features/Orders/CreateOrder.cs
    line_range: '170-210'
    change: |
      Handler:
        1. Inject ISavedAddressRepository into Handler ctor (line 170-184).
        2. Replace the address resolution block (lines 188-208) with:

             Address address;
             if (!string.IsNullOrEmpty(command.SavedAddressId))
             {
                 var saved = await savedAddressRepository.GetByIdAsync(
                     command.SavedAddressId, cancellationToken);
                 if (saved == null)
                     return BusinessResult<Response>.Failure(new Error(
                         nameof(command.SavedAddressId), BusinessErrorMessage.NotFound));

                 // TODO: when order creation becomes authenticated on
                 // Cleansia.Web.Customer, also enforce saved.UserId == callerId.
                 // For now the command is AllowAnonymous — document this.

                 address = saved.Address
                     ?? await addressRepository.GetByIdAsync(saved.AddressId, cancellationToken)!;
             }
             else
             {
                 // Existing inline-address path — countryId resolution +
                 // GetAddressAsync-or-Create (lines 188-208). Pull into
                 // a local helper method ResolveInlineAddressAsync() for
                 // readability.
             }

  - path: src/Cleansia.Core.AppServices/Common/BusinessErrorMessage.cs
    line_range: '42-43'
    change: |
      Add near the existing address errors (after line 43):
        public const string OrderAddressExactlyOneRequired = "order.address_exactly_one_required";

dependencies:
  - TASK-001 # address has lat/lng now (carries through to order)

verification:
  - dotnet build Cleansia.Api.sln
  - Manual: POST /api/Order/Create with inline CustomerAddress → works as before
  - Manual: POST with only SavedAddressId (valid) → order created, Order.CustomerAddressId = saved.AddressId
  - Manual: POST with both → 400 OrderAddressExactlyOneRequired
  - Manual: POST with neither → 400 OrderAddressExactlyOneRequired
  - Manual: POST with bogus SavedAddressId → 400 NotFound
  - Integration tests under src/Cleansia.IntegrationTests/ for Order Create should still pass
```

---

### TASK-006: State field decision (documentation only)

```yaml
task: Decide and document Address.State policy
id: TASK-006
type: improvement
priority: low
specialist: docs
app: backend
estimated_complexity: small
recommended_model: haiku

context: |
  Address.State is nullable today. CZ/SK/UK don't use states, US/CA do.
  Recommendation: keep nullable column, keep collecting it where UI
  surfaces it (currently: nowhere on mobile, order wizard has a state
  field). No schema change. Document the decision so future contributors
  don't re-litigate.

files_to_modify:
  - path: CLAUDE.md
    change: |
      Under "Conventions Summary" or a new "Multi-country notes"
      subsection, add a one-line note:
      "- Address.State is nullable — used for US/CA when we launch there;
         empty for CZ/SK/UA/RU/DE/PL. Do not remove."

dependencies: []

verification:
  - Lint CLAUDE.md (no rendering issues on GitHub)
```

---

## Execution Plan

### Phase 1 — Domain + config foundation (parallelizable)
- **TASK-001** (Address lat/lng) — specialist: backend
- **TASK-002** (IGeocodingService + Mapbox impl + config) — specialist: backend
- **TASK-006** (State policy doc) — specialist: docs

### >> MANUAL STEP (owner): Create EF migration
```
dotnet ef migrations add AddAddressGeocoding \
  --project src/Cleansia.Infra.Database \
  --startup-project src/Cleansia.Web.Customer
dotnet ef database update ...
```

### >> MANUAL STEP (owner): Provision Mapbox geocoding secret token
- Obtain `sk.*` (secret-scoped) token from Mapbox dashboard — distinct from the public maps token used by mobile.
- Add to user-secrets for all 4 web projects (Customer, Mobile, Web, Admin).
- Production: Azure Key Vault / App Config.

### Phase 2 — Command surface (depends on Phase 1)
- **TASK-003** (AddSavedAddress + hints + geocoding) — specialist: backend
- **TASK-004** (UpdateSavedAddress new command + Update endpoint) — specialist: backend
- **TASK-005** (CreateOrder SavedAddressId) — specialist: backend

### >> MANUAL STEP (owner): Regenerate NSwag clients
```
npm run generate-customer-client
npm run generate-admin-client
# partner client not strictly needed (no new partner endpoints)
```

### Phase 3 — Verification
- `dotnet build Cleansia.Api.sln`
- `dotnet test src/Cleansia.Tests`
- `dotnet test src/Cleansia.IntegrationTests` (focus: Orders/CreateOrderIntegrationTests)
- Manual sanity curls against local Cleansia.Web.Customer:
  - `POST /api/SavedAddress/Add` with + without hints
  - `PUT /api/SavedAddress/Update` label-only + full edit
  - `POST /api/Order/Create` inline + SavedAddressId + both + neither

### Model Recommendations
- Phase 1: **sonnet** — standard domain + DI work
- Phase 2: **sonnet** — standard CQRS handlers
- TASK-006: **haiku** — single-line doc addition

### Token Estimate
- Phase 1: ~20k (3 tasks, one medium)
- Phase 2: ~35k (3 medium tasks, all share context)
- Total: ~55k tokens

---

## Out of Scope (separate specs)

- **Phase B — Mobile sync** — rewire `AddressRepository` (Android) to call `/api/SavedAddress/*`, keep DataStore as cache only.
- **Phase C — Web sync** — delete `localStorage` code in `profile.component.ts`, use generated `SavedAddressClient`, add "pick saved address" step to order wizard.
- **Retry job for null-coordinate addresses** — nightly `Cleansia.Functions` task that re-geocodes addresses where `Latitude IS NULL`. Nice-to-have post-launch.
- **Partner app routing integration** — once Orders have coords, partner app can show cleaner→job distance. Separate spec.
