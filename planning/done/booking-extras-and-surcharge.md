# Booking Extras & Express Surcharge — complete the pricing model

**Status:** Decision-doc — blocked on product sign-off for Section 1 before Section 2 tasks can execute
**Depends on:** `CreateOrder` extras dict persistence (done), `BookingPolicy.ExpressSurchargeRate` const (done), `ServiceCategory` entity + seed (done), Mobile booking submission spec (`mobile-booking-submission.md`, merged or in-flight — extras UI grafts onto the ViewModel/catalog plumbing that spec adds)

---

## Ground truth — what exists today vs. what's broken

Before anyone writes code, everyone needs to agree on the current state, because at first glance the backend looks like it already supports extras — it doesn't, not really.

### Backend — 60% there, silently broken

- **`CreateOrder.Command`** — already has `Extras: Dictionary<string, bool>` as a field on the command record. It's been there since the first cut of the order domain. Clients can send a dict and the backend will accept it.
- **`Order.cs:131-132`** — the aggregate has a private `_extras` field plus a read-only projection:
  ```csharp
  private Dictionary<string, bool> _extras = new();
  public IReadOnlyDictionary<string, bool> Extras => _extras;
  ```
- **`OrderEntityConfiguration.cs:35-38`** — persistence is wired as a JSON column via EF Core's `HasConversion` + `JsonSerializer.Serialize/Deserialize`. So whatever the client sends gets stored verbatim.
- **`OrderPricingCalculator.cs`** (once TASK-BS1 from `mobile-booking-submission.md` lands) — IGNORES extras completely. Current pricing:
  ```
  subtotal = services.Sum(s => s.BasePrice + s.PerRoomPrice * (rooms + bathrooms))
           + packages.Sum(p => p.Price)
  total    = subtotal * currency.ExchangeRate
  ```
  No extras term. The dict is persisted but never priced.
- **`BookingPolicy.cs:18-30`** — has three relevant constants:
  ```csharp
  public const int ExpressLeadTimeHours  = 2;
  public const int StandardLeadTimeHours = 4;
  public const decimal ExpressSurchargeRate = 0.20m; // 20%
  ```
- **`BookingPolicy.RequiresExpressSurcharge(cleaningDate, now)`** at `BookingPolicy.cs:69-73` — returns `true` when lead time is between 2h and 4h. Called by **`CreateOrder.Validator`** only to *validate* that the user didn't pick a time too close to now (i.e. enforce the 2h floor). The surcharge itself — the 20% — is never applied to `TotalPrice` anywhere in the solution.

**Consequence:** today, a customer who books 2.5 hours ahead (an "express" slot) pays exactly the same as a customer who books 3 weeks ahead. The constant is decorative.

### Mobile — no extras at all, fake "Express" chips

- **`BookingState.kt`** — no `selectedExtraIds` field. The booking state captures services, packages, rooms, bathrooms, address, date/time, payment method — and that's it. No plumbing for extras anywhere in the compose tree.
- **`WhenWhereStep.kt:187-255`** — renders a 7-slot time picker. Status per slot is **hardcoded mock**: the first two slots show `Unavailable`, the next two show `Express` with a lightning bolt, the rest show `Available`. This mock has zero connection to `BookingPolicy.ExpressLeadTimeHours` or to the real time-of-day. A slot at 10:00 tomorrow is "Express" in the UI because it's the third chip in the row, full stop.
- **No surcharge display.** A user tapping the "Express" chip sees the lightning bolt and some microcopy but **no price difference** and no warning that they're about to pay extra (they're not, but the UX implies they might).
- **`CatalogRepository.kt`** — fetches services + packages. No extras endpoint exists to fetch from.

### Web — mirror of mobile

- **`order-wizard.component.*`** — wizard has a services step, a rooms/bathrooms step, a when/where step, a confirm step. No extras step.
- **`BookingBottomSheet.kt`** equivalent on web (the web order-wizard submit path) sends `extras = emptyMap()` hardcoded. Same story as mobile.
- No surcharge handling on the web side either — the when/where step shows a plain calendar/time picker with no "express" affordance.

### Summary of the gap

| Layer | Extras data | Extras pricing | Surcharge enforcement | Surcharge display |
|---|---|---|---|---|
| Backend command | Accepts dict | **Ignores dict** | Lead-time floor only | N/A |
| Backend persistence | JSON column | — | — | — |
| Mobile UI | **Missing** | **Missing** | **Hardcoded mock** | **Missing** |
| Web UI | **Missing** (sends empty) | **Missing** | **Missing** | **Missing** |

This spec closes every "Missing" and every "Ignores."

---

## Section 1 — Open decisions (blocking, PM/owner sign-off required)

These three questions need a product call before Section 2 executes. Each has a recommendation; the executor should NOT assume the recommendation is approved — confirm with the owner.

### 1a. What extras do we charge for, and at what prices?

The `Extra` entity needs to be seeded with a starting list. Candidates, with proposed CZK pricing:

| Slug | Display name (en) | Proposed price (CZK) | Notes |
|---|---|---|---|
| `inside-oven` | Inside oven cleaning | 200 | Deep clean of oven interior, racks, door glass |
| `inside-fridge` | Inside fridge cleaning | 150 | Empty, clean, wipe down, reassemble. Assumes customer has emptied it beforehand. |
| `interior-windows` | Interior windows | 100 | Per-unit price; cleaner judges extent. MVP: flat fee regardless of window count. |
| `laundry-ironing` | Laundry & ironing | 250 | Simplification — flat fee for up to 1h of laundry work. Real per-hour pricing is a future spec. |
| `pet-hair-supplement` | Pet hair deep-clean | 150 | Additive to base service for homes with shedding pets. |

**Shape of the `Extra` domain entity** (mirrors `ServiceCategory`):

```csharp
public class Extra {
    public string Id { get; private set; }
    public string TenantId { get; private set; }  // nullable, single-tenant friendly
    public string Slug { get; private set; }      // unique per-tenant
    public string Name { get; private set; }      // fallback display name (en)
    public string? Description { get; private set; }
    public decimal Price { get; private set; }    // base currency (CZK)
    public int DisplayOrder { get; private set; }
    public Dictionary<string, ExtraTranslation> Translations { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }
}

public record ExtraTranslation(string Name, string? Description);
```

**Key invariants:**
- `(TenantId, Slug)` is a unique index.
- `Price >= 0`.
- Inactive extras do NOT appear in the customer overview endpoint but DO remain referenceable by existing orders (same rule as `Service.IsActive`).
- Prices are stored in the tenant's base currency (CZK for the cz tenant). Currency conversion for display is the same path services take — multiply by `currency.ExchangeRate` in `OrderPricingCalculator`.

**Decisions needed from PM:**

1. **Approve the 5-item starting list** — or substitute / add / remove.
2. **Approve the prices** — these are placeholders. Someone who's run a real cleaning operation should sanity-check.
3. **Flat-fee for interior windows** — confirm the simplification is OK. Alternative is per-window pricing, which needs a UI change (count input) and bumps the complexity of this spec.
4. **Laundry-ironing as flat fee** — confirm the "up to 1h" framing is OK and the 250 CZK number. Real per-time pricing is out of scope here.
5. **Translations** — for each extra, translations must exist in all 5 locales (en/cs/sk/uk/ru) at seed time. The executor will draft translations and include them in the seed SQL; PM reviews before merge.

**Recommendation:** ship these 5 at the proposed prices; backlog admin CRUD for extras as a fast-follow so ops can tune without a code deploy.

---

### 1b. How is the express surcharge surfaced to the customer?

Two options:

**Option A — Transparent line item (recommended).**
The quote response breaks out:
```json
{
  "servicesSubtotal": 800.00,
  "packagesSubtotal": 0.00,
  "extrasSubtotal":   200.00,
  "expressSurchargeApplied": true,
  "expressSurchargeAmount":  200.00,
  "totalPrice": 1200.00,
  "currencyId": "CZK"
}
```
UI shows the surcharge as a separate line in the summary ("Express surcharge (+20%) — 200 CZK"). **Before the user commits** (on the time-slot chip or a lead-time warning banner), the UI also flags: "This slot is within 4 hours — a 20% express surcharge applies."

**Option B — Rolled silently into the total.**
The server adds 20% and returns the rolled-up total. The UI shows a bigger number at the express slot but no explanation. No separate line item.

**Arguments:**
- Option A builds trust. Users don't like surprise surcharges at checkout. Showing the bump *before* the user taps the chip means they opt in knowingly.
- Option B is simpler to build (no extra fields on the response DTO) and keeps the quote response shape flat. But it's a UX tax for a small build savings.
- Both options ship the same underlying math; the difference is purely in the response shape + UI treatment.

**Recommendation: Option A.** Spec tasks assume Option A. If PM picks B, TASK-ES3 drops the two new response fields and TASK-ES4 / TASK-ES7 simplify to just showing the final total.

---

### 1c. Is the 20% surcharge rate configurable per-tenant?

Today the rate is a `public const decimal ExpressSurchargeRate = 0.20m;` in `BookingPolicy.cs`. Hardcoded.

Options:

- **Option 1 — Leave as const.** MVP. No migration, no config UI. Change requires a code deploy.
- **Option 2 — Move to `CountryConfiguration.ExpressSurchargeRate` (nullable decimal, default 0.20).** Each country/tenant can tune independently. Needs: EF migration, admin UI on the country config page, backend lookup (`BookingPolicy.RequiresExpressSurcharge` becomes an instance method taking the config, or the calculator loads the config directly).
- **Option 3 — Full `TenantConfiguration` table.** Even more flexibility. Overkill for MVP.

**Recommendation: Option 1 for this spec, with a follow-up task flagged.** Ship the const. Note in the follow-up backlog that moving to `CountryConfiguration` is a ~2-day lift (entity field + migration + admin UI + calculator wiring).

Same reasoning applies to `ExpressLeadTimeHours` (2h) and `StandardLeadTimeHours` (4h) — keep as consts for MVP; move to config in the same future task.

---

### Decision summary (fill in when signed off)

| Decision | Recommendation | Final (PM sign-off) |
|---|---|---|
| 1a — Starting extras list | 5 items above @ proposed prices | _tbd_ |
| 1b — Surcharge display | Option A (transparent line item) | _tbd_ |
| 1c — Per-tenant rate config | Option 1 (const, for now) | _tbd_ |

**DO NOT start Section 2 tasks until this table has `Final` values.** Any deviation from the recommendation may cascade-change the task specs below.

---

## Section 2 — Implementation tasks (post-decision)

Section 2 assumes all three recommendations in Section 1 are approved. If PM picks different options, the executor must revise task specs before coding.

### Phase 1 — Backend

#### TASK-ES1: `Extra` domain entity + EF config + repository + seed

```yaml
task: New Extra domain entity mirroring ServiceCategory — schema, config, repo, seed
id: TASK-ES1
type: feature
priority: high
specialist: backend
app: backend
estimated_complexity: medium
recommended_model: sonnet

context: |
  Extra is a new first-class domain entity. Shape mirrors ServiceCategory
  (which shipped recently). Tenant-scoped via TenantId, soft-deletable
  via IsActive, translatable via Translations dict.

  Seed 5 initial extras per Section 1a — executor must confirm the
  final list with the PM-signed-off decision table before running the
  seed.

files_to_create:
  - path: src/Cleansia.Core.Domain/Orders/Extra.cs
    change: |
      namespace Cleansia.Core.Domain.Orders;

      public class Extra {
          public string Id { get; private set; } = default!;
          public string? TenantId { get; private set; }
          public string Slug { get; private set; } = default!;
          public string Name { get; private set; } = default!;
          public string? Description { get; private set; }
          public decimal Price { get; private set; }
          public int DisplayOrder { get; private set; }
          public Dictionary<string, ExtraTranslation> Translations { get; private set; } = new();
          public bool IsActive { get; private set; } = true;
          public DateTime CreatedAt { get; private set; }
          public DateTime? UpdatedAt { get; private set; }

          private Extra() { }

          public static Extra Create(
              string slug, string name, decimal price,
              string? description = null,
              int displayOrder = 0,
              string? tenantId = null,
              Dictionary<string, ExtraTranslation>? translations = null)
          {
              if (price < 0) throw new ArgumentOutOfRangeException(nameof(price));
              return new Extra {
                  Id = Guid.NewGuid().ToString(),
                  TenantId = tenantId,
                  Slug = slug,
                  Name = name,
                  Description = description,
                  Price = price,
                  DisplayOrder = displayOrder,
                  Translations = translations ?? new(),
                  IsActive = true,
                  CreatedAt = DateTime.UtcNow,
              };
          }

          public void UpdatePrice(decimal newPrice) {
              if (newPrice < 0) throw new ArgumentOutOfRangeException(nameof(newPrice));
              Price = newPrice;
              UpdatedAt = DateTime.UtcNow;
          }

          public void Deactivate() { IsActive = false; UpdatedAt = DateTime.UtcNow; }
          public void Activate()   { IsActive = true;  UpdatedAt = DateTime.UtcNow; }
      }

      public record ExtraTranslation(string Name, string? Description);

  - path: src/Cleansia.Infra.Database/Configurations/ExtraEntityConfiguration.cs
    change: |
      Mirror ServiceCategoryEntityConfiguration exactly — PK on Id,
      tenant query filter on TenantId, unique index on (TenantId, Slug),
      JSON column for Translations via HasConversion + JsonSerializer,
      Price as decimal(10,2).

  - path: src/Cleansia.Core.Domain/Orders/IExtraRepository.cs
    change: |
      Standard repo interface mirroring IServiceRepository:
        - GetAll() : IQueryable<Extra>
        - GetByIdAsync(string id, CT) : Task<Extra?>
        - GetByIdsAsync(IEnumerable<string> ids, CT) : Task<IReadOnlyList<Extra>>
        - AddAsync(Extra, CT)
        - Update(Extra)

  - path: src/Cleansia.Infra.Database/Repositories/ExtraRepository.cs
    change: |
      EF Core implementation. Mirror ServiceRepository.

files_to_modify:
  - path: src/Cleansia.Infra.Database/ApplicationDbContext.cs
    change: |
      Add: public DbSet<Extra> Extras => Set<Extra>();
      Add ApplyConfiguration(new ExtraEntityConfiguration()) in OnModelCreating.

  - path: src/Cleansia.Config/Repositories/RepositoryExtensions.cs
    change: |
      Register IExtraRepository -> ExtraRepository as scoped, next to
      IServiceRepository registration.

  - path: sql-scripts/insert_seed_data.sql
    # And whichever equivalent seed script runs on fresh envs — grep
    # for 'INSERT INTO "Services"' to find it if there's more than one.
    change: |
      Add INSERT statements for 5 Extra rows per the approved list.
      Include Translations JSON with en/cs/sk/uk/ru entries per row.

      Example row (adjust price per final decision):
        INSERT INTO "Extras" ("Id", "TenantId", "Slug", "Name",
          "Description", "Price", "DisplayOrder", "Translations",
          "IsActive", "CreatedAt")
        VALUES ('<guid>', 'cz', 'inside-oven', 'Inside oven cleaning',
          'Deep clean of oven interior, racks, and door glass.',
          200.00, 10,
          '{"en":{"name":"Inside oven cleaning","description":"Deep clean of oven interior, racks, and door glass."},"cs":{"name":"Úklid trouby zevnitř",...}}'::jsonb,
          true, NOW());

      Five total rows — see Section 1a table for slugs, names, prices.
      Translations drafted by executor; PM reviews before merge.

  - path: sql-scripts/additional_migrations.sql
    # Or whatever the name of the "add-on migrations" script is — grep.
    change: |
      Add CREATE TABLE "Extras" DDL if the project uses a SQL-first
      migration flow alongside EF Core migrations. If EF migrations
      are the sole source of truth, skip this file and flag a
      MANUAL_STEP below for the owner to generate the migration.

dependencies: []
verification:
  - dotnet build Cleansia.Api.sln
  - Manual (by owner after running migration):
    - Extras table exists with correct columns
    - Five seed rows present, all with IsActive = true
    - Unique index (TenantId, Slug) enforced (try inserting a duplicate — should fail)

MANUAL_STEP: |
  Owner must:
  1. Generate the EF migration:
     dotnet ef migrations add AddExtras --project src/Cleansia.Infra.Database --startup-project src/Cleansia.Web
  2. Review the generated migration for accuracy (PK, FK if any, JSON column).
  3. Apply it: dotnet ef database update --project src/Cleansia.Infra.Database --startup-project src/Cleansia.Web
  4. Re-run insert_seed_data.sql against the dev database to populate seed rows.
  5. Verify on the Admin API Swagger that GET /Extras returns the 5 rows once TASK-ES2 lands (it's the next task, so this verification happens after ES2).
```

---

#### TASK-ES2: Anonymous `GET /api/Extra/GetOverview` customer endpoint

```yaml
task: New GET /api/Extra/GetOverview on the customer API returning ExtraListItem[]
id: TASK-ES2
type: feature
priority: high
specialist: backend
app: backend
estimated_complexity: small
recommended_model: sonnet

context: |
  Parallel to GetServiceOverview and GetPackageOverview. Anonymous —
  guest users need to see the extras list when building a booking,
  same as they see services.

  Query handler is trivial: repo.GetAll().Where(e => e.IsActive)
  .OrderBy(DisplayOrder).Select(MapToDto).ToListAsync().

  DTO shape parallels ServiceListItem.

files_to_create:
  - path: src/Cleansia.Core.AppServices/Features/Extras/DTOs/ExtraListItem.cs
    change: |
      namespace Cleansia.Core.AppServices.Features.Extras.DTOs;

      public record ExtraListItem(
          string Id,
          string Slug,
          string Name,
          string? Description,
          decimal Price,
          int DisplayOrder,
          IReadOnlyDictionary<string, ExtraTranslationDto>? Translations);

      public record ExtraTranslationDto(string Name, string? Description);

  - path: src/Cleansia.Core.AppServices/Features/Extras/GetExtraOverview.cs
    change: |
      MediatR query + handler + response pattern, mirroring
      GetServiceOverview.cs. Handler returns List<ExtraListItem>.
      No validator needed (no inputs).

  - path: src/Cleansia.Core.AppServices/Features/Extras/Mappers/ExtraMapper.cs
    change: |
      Extension method Extra.MapToDto() returning ExtraListItem.
      Translations pass through as ExtraTranslation -> ExtraTranslationDto.

files_to_modify:
  - path: src/Cleansia.Web.Customer/Controllers/ExtraController.cs
    # Create this file if it doesn't exist — mirror ServiceController.
    change: |
      [ApiController]
      [Route("api/[controller]")]
      public class ExtraController(IMediator mediator) : BaseController
      {
          [HttpGet("GetOverview")]
          [AllowAnonymous]
          [ProducesResponseType(typeof(IReadOnlyList<ExtraListItem>), StatusCodes.Status200OK)]
          public async Task<IActionResult> GetOverview(CancellationToken ct) =>
              Ok(await mediator.Send(new GetExtraOverview.Query(), ct));
      }

  - path: src/Cleansia.Web.Mobile/Controllers/ExtraController.cs
    # Only if Mobile API has its own controller tree — mirror the pattern
    # used by other anonymous overview endpoints on the Mobile API.
    change: |
      Same shape as the Customer controller above.

dependencies:
  - TASK-ES1
verification:
  - dotnet build Cleansia.Api.sln
  - Manual: curl http://localhost:5003/api/Extra/GetOverview → 200 with 5 items
  - Manual: each item has populated translations for en/cs/sk/uk/ru

MANUAL_STEP: |
  After this task lands and the backend is running fresh, owner:
  1. Regenerates the web customer TypeScript client:
     cd src/Cleansia.App && npm run generate-customer-client
  2. Regenerates the partner client if extras become visible on partner
     side (they shouldn't for MVP — partner sees extras via the order
     detail which already passes them through the Order aggregate).
  3. No NSwag regen needed for mobile (mobile is hand-written Retrofit).
  4. Refreshes the OpenAPI dump under src/cleansia_customer_android/openapi/
     as a sanity check:
     curl http://localhost:5003/swagger/v1/swagger.json > src/cleansia_customer_android/openapi/customer-api.json
```

---

#### TASK-ES3: Extend `OrderPricingCalculator` — extras + express surcharge

```yaml
task: Add extras pricing and express surcharge to OrderPricingCalculator
id: TASK-ES3
type: feature
priority: high
specialist: backend
app: backend
estimated_complexity: medium
recommended_model: sonnet

context: |
  The calculator currently prices services + packages with currency
  conversion. This task adds two new terms:

    1. EXTRAS: each selected extra adds its Price (in base currency)
       to the pre-conversion subtotal.
    2. EXPRESS SURCHARGE: if cleaningDate is within ExpressLeadTimeHours
       (2h) AND beyond StandardLeadTimeHours (4h) — i.e. the "express
       window" — the pre-conversion subtotal gets multiplied by
       (1 + ExpressSurchargeRate) = 1.20.

       Re-read BookingPolicy.RequiresExpressSurcharge to confirm the
       exact window. Per BookingPolicy.cs:69-73 the rule is:
         leadTime >= ExpressLeadTimeHours && leadTime < StandardLeadTimeHours
       i.e. 2h <= leadTime < 4h.
       Below 2h → booking is rejected by validator (not a pricing issue).
       At or above 4h → no surcharge.

  The pricing order matters:
    baseSubtotal   = services + packages  (existing)
    extrasSubtotal = sum(selected extras' Price)
    preSurchargeSubtotal = baseSubtotal + extrasSubtotal
    surcharged     = preSurchargeSubtotal * (IsExpress ? 1.20 : 1.0)
    total          = surcharged * currency.ExchangeRate

  Surcharge applies to EVERYTHING including extras — it's a "we're
  rushing for you" premium, not a services-only markup. Product
  confirms this assumption (flag for 1b sign-off if not covered there).

  The Extras dict on CreateOrder.Command is REPURPOSED but not
  reshaped: keys that exist with value `true` are treated as
  "this extra id is selected." Keys with value `false` or absent are
  ignored. Command shape stays Dictionary<string, bool> — backward
  compatible: empty dict = no extras (same as today).

files_to_modify:
  - path: src/Cleansia.Core.AppServices/Services/Interfaces/IOrderPricingCalculator.cs
    change: |
      Update the record AND the method signature:

        public record OrderPricingResult(
            decimal TotalPrice,
            string CurrencyId,
            decimal ServicesSubtotal,
            decimal PackagesSubtotal,
            decimal ExtrasSubtotal,                 // NEW
            bool    ExpressSurchargeApplied,        // NEW
            decimal ExpressSurchargeAmount,         // NEW — in BASE currency, before exchange conversion. Callers that display in target currency must multiply by ExchangeRate.
            decimal ExchangeRate);

        public interface IOrderPricingCalculator
        {
            Task<OrderPricingResult> CalculateAsync(
                IEnumerable<string> selectedServiceIds,
                IEnumerable<string> selectedPackageIds,
                IEnumerable<string> selectedExtraIds,     // NEW
                int rooms,
                int bathrooms,
                DateTime? cleaningDate,                   // NEW — used for surcharge check; null = skip surcharge (quote without date)
                string? currencyId,
                CancellationToken cancellationToken);
        }

      Note on ExpressSurchargeAmount: deliberately pre-conversion so
      the field has stable semantics regardless of currency. Consumers
      needing the target-currency amount multiply by ExchangeRate.

  - path: src/Cleansia.Core.AppServices/Services/OrderPricingCalculator.cs
    change: |
      1. Add IExtraRepository to the primary constructor (DI).
      2. Method signature matches the new interface.
      3. Implementation:

         var extras = selectedExtraIds.Any()
             ? await _extraRepository.GetByIdsAsync(selectedExtraIds, ct)
             : Array.Empty<Extra>();
         var extrasSubtotal = extras.Sum(e => e.Price);

         var baseSubtotal =
             services.Sum(s => s.BasePrice + s.PerRoomPrice * (rooms + bathrooms))
           + packages.Sum(p => p.Price);

         var preSurchargeSubtotal = baseSubtotal + extrasSubtotal;

         bool surchargeApplies = cleaningDate.HasValue &&
             BookingPolicy.RequiresExpressSurcharge(cleaningDate.Value, DateTime.UtcNow);

         decimal surchargeAmount = surchargeApplies
             ? preSurchargeSubtotal * BookingPolicy.ExpressSurchargeRate
             : 0m;

         decimal surchargedSubtotal = preSurchargeSubtotal + surchargeAmount;

         decimal totalPrice = Math.Round(surchargedSubtotal * currency.ExchangeRate, 2);

         return new OrderPricingResult(
             TotalPrice: totalPrice,
             CurrencyId: currency.Id,
             ServicesSubtotal: Math.Round(services.Sum(s => s.BasePrice + s.PerRoomPrice * (rooms + bathrooms)) * currency.ExchangeRate, 2),
             PackagesSubtotal: Math.Round(packages.Sum(p => p.Price) * currency.ExchangeRate, 2),
             ExtrasSubtotal:   Math.Round(extrasSubtotal * currency.ExchangeRate, 2),
             ExpressSurchargeApplied: surchargeApplies,
             ExpressSurchargeAmount:  surchargeAmount,                           // base currency per interface contract
             ExchangeRate: currency.ExchangeRate);

         Subtotals returned are in TARGET currency (already multiplied
         by exchange rate), matching what today's TotalPrice does.
         ExpressSurchargeAmount is explicitly base-currency; document
         in the record's XML doc comment.

  - path: src/Cleansia.Core.AppServices/Features/Orders/CreateOrder.cs
    change: |
      PriceMatchesAsync currently calls the calculator with 5 args.
      Add the 2 new args:

        var selectedExtraIds = command.Extras?
            .Where(kv => kv.Value)
            .Select(kv => kv.Key)
            ?? Array.Empty<string>();

        var result = await _pricingCalculator.CalculateAsync(
            command.SelectedServiceIds,
            command.SelectedPackageIds,
            selectedExtraIds,
            command.Rooms,
            command.Bathrooms,
            command.CleaningDate,     // already on the command
            command.CurrencyId,
            cancellationToken);

        return result.TotalPrice == command.TotalPrice;

      No other changes in CreateOrder.cs — the handler continues to
      persist the Extras dict as-is via the Order aggregate's existing
      method (_extras = command.Extras).

  - path: src/Cleansia.Core.AppServices/Features/Orders/QuoteOrder.cs
    # Created by TASK-BS2 in the mobile-booking-submission spec.
    # If that spec hasn't landed yet, this task is blocked until it does.
    change: |
      QuoteOrder.Command gains two optional fields:
        public IEnumerable<string> SelectedExtraIds { get; init; } = Array.Empty<string>();
        public DateTime? CleaningDate { get; init; }

      Handler passes both through to the calculator.

      Response already mirrors OrderPricingResult (either re-exports or
      wraps) — update whichever it does to include the three new fields.

      Validator: SelectedExtraIds — no validation beyond "if provided,
      each id must exist" (mirror the existing SelectedServiceIds rule).
      CleaningDate — optional; no validation (unlike CreateOrder which
      enforces the 2h floor, Quote is read-only and should be lenient
      so the UI can quote any time the user's considering, then enforce
      at Create).

  - path: src/Cleansia.Core.AppServices/Features/Orders/QuoteOrder.cs
    # Second change on the same file — validator amendment
    change: |
      Add to QuoteOrder.Validator (after existing rules):

        RuleForEach(x => x.SelectedExtraIds)
            .MustAsync(async (id, ct) =>
                await _extraRepository.GetByIdAsync(id, ct) != null)
            .WithMessage(BusinessErrorMessage.Order.InvalidSelectedExtras);

      Inject IExtraRepository into the validator.

  - path: src/Cleansia.Core.AppServices/Common/BusinessErrorMessage.cs
    change: |
      Add under the Order static class:
        public const string InvalidSelectedExtras = "order.invalid_selected_extras";

files_to_create:
  - path: src/Cleansia.Tests/Features/Orders/OrderPricingCalculatorExtrasTests.cs
    change: |
      xUnit unit tests covering:
        - Empty extras → ExtrasSubtotal == 0, total matches services+packages as before
        - Two extras selected → ExtrasSubtotal = sum of their prices * exchangeRate
        - Express window (3h lead time, 4h > leadTime >= 2h) →
          ExpressSurchargeApplied == true, total includes 20% markup
        - Non-express (e.g. 24h lead time) → ExpressSurchargeApplied == false, no markup
        - Cleaning date null → surcharge never applies
        - Both extras AND express → surcharge is on (services + packages + extras),
          not just services+packages.

      Use FakeItEasy or NSubstitute per repo convention (grep existing
      tests to confirm).

dependencies:
  - TASK-ES1
  - TASK-ES2
  # TASK-BS1 (pricing calculator extraction from mobile-booking-submission spec)
  # is a hard prerequisite — this task amends OrderPricingCalculator which
  # only exists after BS1 lands. If BS1 isn't merged when this starts,
  # escalate to the owner to sequence.
verification:
  - dotnet build Cleansia.Api.sln
  - dotnet test src/Cleansia.Tests — new tests green, existing tests still green
  - Manual:
    - POST /api/Order/Quote with extras selected + a cleaningDate 30min+2h in the future → response shows expressSurchargeApplied=true, extrasSubtotal populated, total = 1.20 * (services+packages+extras) * exchangeRate
    - POST with extras but cleaningDate 2 days out → expressSurchargeApplied=false, no 20% bump
    - POST /api/Order/Create with a TotalPrice that doesn't match the calculator (e.g. client estimate without surcharge) → 400 from PriceMatches validator
```

---

### Phase 2 — Mobile

#### TASK-ES4: Mobile extras catalog + state + UI in ConfirmStep

```yaml
task: Fetch extras, add selectedExtraIds to BookingState, render toggle rows in ConfirmStep
id: TASK-ES4
type: feature
priority: high
specialist: mobile
app: customer-android
estimated_complexity: medium
recommended_model: sonnet

context: |
  Extras live in the ConfirmStep, not as their own step. Rationale:
  extras are nice-to-haves users consider LAST, after they've already
  picked services + date + address. Adding a 5th step in the booking
  flow bloats the happy path. The ConfirmStep today is a summary
  card + payment method + "Book now" button; we add a collapsible
  "Extras" card between the summary and the payment section.

  Visual: each extra is a row with icon + name + price + toggle.
  Rows reflow the summary total live (via refreshQuote()).

  The extras list comes from CatalogRepository (extended here) which
  gets a new /api/Extra/GetOverview call on its next refresh.

files_to_modify:
  - path: src/cleansia_customer_android/app/src/main/java/cz/cleansia/customer/features/booking/BookingState.kt
    change: |
      Add alongside selectedServiceIds / selectedPackageIds:
        selectedExtraIds: Set<String> = emptySet(),

      Null extras on submit = empty dict (existing backward-compat).

  - path: src/cleansia_customer_android/app/src/main/java/cz/cleansia/customer/core/catalog/CatalogDto.kt
    change: |
      Add:
        @Serializable data class ExtraListItem(
            val id: String,
            val slug: String,
            val name: String,
            val description: String? = null,
            val price: Double,
            val displayOrder: Int = 0,
            val translations: Map<String, TranslationDto>? = null,
        )

  - path: src/cleansia_customer_android/app/src/main/java/cz/cleansia/customer/core/catalog/CatalogApi.kt
    change: |
      Add:
        @GET("api/Extra/GetOverview")
        suspend fun getExtras(): Response<List<ExtraListItem>>

  - path: src/cleansia_customer_android/app/src/main/java/cz/cleansia/customer/core/catalog/CatalogRepository.kt
    change: |
      Add a third StateFlow:
        private val _extras = MutableStateFlow<List<ExtraListItem>>(emptyList())
        val extras: StateFlow<List<ExtraListItem>> = _extras.asStateFlow()

      In refresh(), also fetch extras. Tolerate extras-fetch failure
      without blocking services/packages (extras are additive — if the
      endpoint is down, the booking flow still works with an empty list).

        val e = runCatching { api.getExtras() }.getOrNull()
        if (e?.isSuccessful == true) _extras.value = e.body().orEmpty()
        // else: silent — empty list, log, move on

  - path: src/cleansia_customer_android/app/src/main/java/cz/cleansia/customer/features/booking/ConfirmStep.kt
    # If the file doesn't exist by this name, grep for the confirm/summary
    # step component in features/booking/.
    change: |
      1. Inject CatalogRepository via the existing EntryPoint.
      2. Observe catalogRepo.extras as state.
      3. Between the summary card and the payment card, render:

         ExtrasCard(
             extras = extras,
             selected = state.selectedExtraIds,
             onToggle = { id ->
                 bookingVm.update { s ->
                     s.copy(selectedExtraIds =
                         if (id in s.selectedExtraIds) s.selectedExtraIds - id
                         else s.selectedExtraIds + id)
                 }
                 scope.launch { bookingVm.refreshQuote() }  // price re-quote
             },
         )

      4. ExtrasCard is a new composable in the same file (or
         extracted to ExtrasCard.kt if >100 lines). Shape:

         - Header row: "Extras (optional)" (localized) + expand/collapse chevron
         - Collapsed: shows count of selected (or nothing if none)
         - Expanded: list of rows with name + price + Switch
         - If list empty (catalog fetch failed or empty seed): hide entire card silently

      5. Translated names: same pattern as services — resolve
         extra.translations[lang]?.name ?: extra.name via AppSettingsRepository.

      6. Price formatting uses the existing currency formatter utility
         (grep for `fun formatPrice` or similar in core/).

  - path: src/cleansia_customer_android/app/src/main/res/values/strings.xml
    change: |
      Add:
        <string name="booking_extras_header">Extras (optional)</string>
        <string name="booking_extras_selected_count">%1$d selected</string>
        <string name="booking_extras_none_selected">None</string>

      Mirror in values-cs/sk/uk/ru.

dependencies:
  - TASK-ES2   # backend endpoint exists
  - TASK-BS5   # mobile-booking-submission spec's catalog wiring — this task extends that repo
verification:
  - Build in Android Studio
  - Manual flow: open booking → confirm step → extras card shows 5 items
  - Toggle an extra → total updates within ~500ms (live quote roundtrip)
  - Toggle off → total reverts
  - Airplane mode during catalog refresh → extras card hidden, rest of flow still works
```

---

#### TASK-ES5: Mobile submit wiring — include `selectedExtraIds` in Create + Quote commands

```yaml
task: Pipe selectedExtraIds through BookingViewModel.submit() and refreshQuote()
id: TASK-ES5
type: feature
priority: high
specialist: mobile
app: customer-android
estimated_complexity: small
recommended_model: sonnet

context: |
  BookingViewModel.submit() builds a CreateOrderCommand. The Extras
  field on that command currently sends emptyMap(). Replace with:

    extras = state.selectedExtraIds.associateWith { true }

  And refreshQuote() builds a QuoteOrderCommand — add selectedExtraIds
  and cleaningDate (the real Instant from BookingState.selectedInstant
  when set) to that call.

files_to_modify:
  - path: src/cleansia_customer_android/app/src/main/java/cz/cleansia/customer/features/booking/BookingViewModel.kt
    change: |
      1. In refreshQuote(), update the command:

         val quoteCmd = QuoteOrderCommand(
             selectedServiceIds = s.selectedServiceIds.toList(),
             selectedPackageIds = s.selectedPackageIds.toList(),
             selectedExtraIds   = s.selectedExtraIds.toList(),           // NEW
             rooms = s.rooms,
             bathrooms = s.bathrooms,
             cleaningDate = s.selectedInstant?.toString(),               // NEW — ISO-8601
             currencyId = null,
         )

      2. In submit(), same additions to the quoteCmd built there.

      3. On the CreateOrderCommand, replace:
           extras = emptyMap()
         with:
           extras = s.selectedExtraIds.associateWith { true }

      4. Consume the surcharge fields from the quote response for the
         user-visible summary (already happening if TASK-ES4 wired
         them to the UI — this task just ensures the ViewModel exposes
         them, not just totalPrice):

         data class BookingQuoteSummary(
             val total: Double,
             val servicesSubtotal: Double,
             val packagesSubtotal: Double,
             val extrasSubtotal: Double,
             val expressSurchargeApplied: Boolean,
             val expressSurchargeAmount: Double,
             val currencyId: String,
         )
         // Expose via StateFlow<BookingQuoteSummary?>, map from the
         // QuoteOrderResponse on each refreshQuote().

  - path: src/cleansia_customer_android/app/src/main/java/cz/cleansia/customer/core/booking/BookingApi.kt
    change: |
      Update QuoteOrderCommand + QuoteOrderResponse data classes:

        @Serializable data class QuoteOrderCommand(
            val selectedServiceIds: List<String>,
            val selectedPackageIds: List<String>,
            val selectedExtraIds: List<String> = emptyList(),     // NEW
            val rooms: Int,
            val bathrooms: Int,
            val cleaningDate: String? = null,                     // NEW
            val currencyId: String? = null,
        )

        @Serializable data class QuoteOrderResponse(
            val totalPrice: Double,
            val currencyId: String,
            val servicesSubtotal: Double,
            val packagesSubtotal: Double,
            val extrasSubtotal: Double,                    // NEW
            val expressSurchargeApplied: Boolean,          // NEW
            val expressSurchargeAmount: Double,            // NEW
            val exchangeRate: Double,
        )

      CreateOrderCommand.extras stays Map<String, Boolean> — backend
      shape is unchanged.

dependencies:
  - TASK-ES3   # backend command + response shapes exist
  - TASK-ES4   # state has selectedExtraIds
verification:
  - Proxy the request (Android Studio network inspector): submit a
    booking with 2 extras selected → request body's extras field
    contains both ids mapped to true
  - Quote request during edit includes selectedExtraIds
  - Confirm step summary shows "Extras: 350 CZK" line when 2 extras totalling 350 are selected
```

---

#### TASK-ES6: Mobile express-surcharge display in `WhenWhereStep`

```yaml
task: Make the mock "Express" chips real — show lead-time-based status + surcharge hint
id: TASK-ES6
type: feature
priority: medium
specialist: mobile
app: customer-android
estimated_complexity: medium
recommended_model: sonnet

context: |
  Today WhenWhereStep.kt:187-255 hardcodes 2 "Unavailable" + 2
  "Express" + rest "Available." Replace with a real lead-time
  calculation using BookingPolicy-equivalent constants on the mobile
  side.

  MVP approach: copy the 3 constants into a mobile-side BookingPolicy.kt
  singleton. Rationale: they're stable (no pending product change),
  they match the backend's const values, and a new /api/Policy/GetBookingPolicy
  endpoint is overkill for MVP. When we move the backend const to
  CountryConfiguration (follow-up per Section 1c), replace this
  singleton with a fetched config at the same time.

  For each time slot:
    leadTime = slotInstant - Clock.System.now()
    - < 2h                   → Unavailable (grey chip)
    - 2h <= leadTime < 4h    → Express    (orange chip + "+20%")
    - >= 4h                  → Available  (default chip)

  The "+20%" is static text from BookingPolicy.kt — no server call
  needed for the chip label. The actual applied surcharge amount
  comes from the quote response in ConfirmStep.

files_to_create:
  - path: src/cleansia_customer_android/app/src/main/java/cz/cleansia/customer/core/booking/BookingPolicy.kt
    change: |
      object BookingPolicy {
          const val EXPRESS_LEAD_TIME_HOURS  = 2
          const val STANDARD_LEAD_TIME_HOURS = 4
          const val EXPRESS_SURCHARGE_RATE   = 0.20  // 20%

          enum class SlotStatus { Unavailable, Express, Available }

          fun statusFor(slotInstant: Instant, now: Instant = Clock.System.now()): SlotStatus {
              val hours = (slotInstant - now).inWholeMinutes / 60.0
              return when {
                  hours < EXPRESS_LEAD_TIME_HOURS  -> SlotStatus.Unavailable
                  hours < STANDARD_LEAD_TIME_HOURS -> SlotStatus.Express
                  else                             -> SlotStatus.Available
              }
          }
      }

      Flag for the future-config move:
        // TODO: When CountryConfiguration surcharge config ships on the
        // backend, replace these consts with a repository-backed lookup.

files_to_modify:
  - path: src/cleansia_customer_android/app/src/main/java/cz/cleansia/customer/features/booking/WhenWhereStep.kt
    line_range: '187-255'
    change: |
      1. Delete the hardcoded Unavailable/Express/Available literals.
      2. For each time slot, compute:
           val slotInstant = resolveInstantFor(day, slotLabel)
             // already exists per TASK-BS7 / mobile-booking-submission
           val status = BookingPolicy.statusFor(slotInstant)

      3. Chip rendering based on status:
           - Unavailable: grey background, strikethrough time label,
             not selectable
           - Express: orange background, lightning icon (keep existing),
             subtitle "+20%" underneath the time
           - Available: default styling, no subtitle

      4. When user picks an Express slot, show a one-time BottomSheetDialog
         or AlertDialog on first tap in a session:
           Title: "Fast cleaning"
           Body: "This slot is within 4 hours of now. A 20% express
                  surcharge applies to the total."
           Confirm: "OK, continue"
           Cancel: "Pick a later time"

         Remember dismissal in a ViewModel flag so subsequent taps
         on other Express slots in the same flow don't re-prompt.

  - path: src/cleansia_customer_android/app/src/main/res/values/strings.xml
    change: |
      Add:
        <string name="booking_slot_express_surcharge_badge">+20%</string>
        <string name="booking_slot_express_warning_title">Fast cleaning</string>
        <string name="booking_slot_express_warning_body">This slot is within 4 hours of now. A 20%% express surcharge applies to the total.</string>
        <string name="booking_slot_express_warning_confirm">OK, continue</string>
        <string name="booking_slot_express_warning_cancel">Pick a later time</string>

      Mirror in values-cs/sk/uk/ru. Note the `%%` escaping for the
      percent sign in XML-format strings.

dependencies:
  - TASK-BS7   # BookingState.selectedInstant must exist for real time math
verification:
  - Manual: system clock at a known time; slot 1h out → Unavailable,
    slot 3h out → Express w/ +20% badge, slot 2 days out → Available
  - Tap an Express slot → warning dialog shows on first tap
  - Dismiss dialog, tap another Express slot in the same session → no re-prompt
  - Quote response in confirm step matches the surcharge the UI advertised
```

---

### Phase 3 — Web

#### TASK-ES7: Web order wizard — extras catalog fetch, extras UI, surcharge surfacing

```yaml
task: Port the extras flow to the Angular order-wizard — catalog fetch, UI, submit
id: TASK-ES7
type: feature
priority: high
specialist: frontend
app: customer-web
estimated_complexity: large
recommended_model: sonnet

context: |
  Web order-wizard lives at:
    libs/cleansia-customer-features/order-wizard/src/lib/order-wizard/

  Today it submits extras = emptyMap() hardcoded. Mirror mobile:
  fetch extras, add a section in the confirm/summary step, pass
  selectedExtraIds through the quote + create calls.

files_to_modify:
  - path: src/Cleansia.App/libs/core/customer-services/src/lib/client/customer-client.ts
    change: |
      NO hand edits — regenerated from NSwag after backend TASK-ES2
      lands. Flagged MANUAL_STEP for the owner below.

  - path: src/Cleansia.App/libs/cleansia-customer-features/order-wizard/src/lib/order-wizard/order-wizard.models.ts
    change: |
      Add to the wizard state model:
        selectedExtraIds: string[];
      Default to [] in the initial state.

  - path: src/Cleansia.App/libs/cleansia-customer-features/order-wizard/src/lib/order-wizard/order-wizard.component.ts
    change: |
      1. Inject the extras service (auto-generated NSwag client).
      2. On init, call extrasService.getOverview() in parallel with
         services + packages — add to the existing forkJoin.
      3. Expose extras() signal for the template.

  - path: src/Cleansia.App/libs/cleansia-customer-features/order-wizard/src/lib/order-wizard/components/wizard-summary-step.component.ts
    change: |
      1. Receive @Input() extras from parent.
      2. Render a <cleansia-section> titled "Extras (optional)" between
         the summary section and the payment section.
      3. Each extra renders as a row with <cleansia-toggle> or
         equivalent — name + price + switch.
      4. Toggle emits an event that updates wizard state + triggers
         a quote refresh (same pattern as rooms/bathrooms changes
         already work).
      5. The summary card shows new line items:
           - "Extras: {{extrasSubtotal | currency}}" when non-zero
           - "Express surcharge (+20%): {{expressSurchargeAmount | currency}}"
              when expressSurchargeApplied == true
              (rendered in a distinct color — grep for how other
               "warning-ish" line items are styled)

  - path: src/Cleansia.App/libs/cleansia-customer-features/order-wizard/src/lib/order-wizard/order-wizard.component.html
    change: |
      Pass the extras + selected state + surcharge details down to
      wizard-summary-step.

  - path: src/Cleansia.App/libs/shared/assets/src/styles/pages/cleansia-customer/order-wizard.component.scss
    change: |
      Add styles for:
        .wizard-extras-row
        .wizard-extras-price
        .wizard-surcharge-line   (uses a warning tint — orange/amber)
        .wizard-summary-extras-card

  - path: src/Cleansia.App/apps/cleansia.app/src/assets/i18n/en.json
    change: |
      Add under customer.order_wizard (or the existing key tree —
      grep to confirm):
        "extras_section_title": "Extras (optional)",
        "extras_none_selected": "No extras selected",
        "extras_subtotal_line": "Extras",
        "surcharge_express_line": "Express surcharge (+20%)",
        "surcharge_express_tooltip": "This cleaning is within 4 hours of now, so a 20% express surcharge is added."

  - path: src/Cleansia.App/apps/cleansia.app/src/assets/i18n/cs.json
    change: "same keys, Czech translations"
  - path: src/Cleansia.App/apps/cleansia.app/src/assets/i18n/sk.json
    change: "same keys, Slovak translations"
  - path: src/Cleansia.App/apps/cleansia.app/src/assets/i18n/uk.json
    change: "same keys, Ukrainian translations"
  - path: src/Cleansia.App/apps/cleansia.app/src/assets/i18n/ru.json
    change: "same keys, Russian translations"

dependencies:
  - TASK-ES3   # backend pricing includes extras + surcharge
  # And the MANUAL_STEP NSwag regen must have happened first.
verification:
  - npx nx serve cleansia-app → open order wizard
  - Summary step shows extras card
  - Toggle an extra → summary total updates within ~500ms
  - Pick a cleaning time 3h out → "Express surcharge" line appears
    in summary, total reflects 20% bump
  - Submit → network request body's extras field is { "inside-oven": true, ... }
  - Backend accepts → order created, redirect to success

MANUAL_STEP: |
  Before starting this task, owner regenerates the customer TypeScript
  client so the ExtraClient / ExtraListItem types exist:
    cd src/Cleansia.App && npm run generate-customer-client
```

---

### Phase 4 — i18n

#### TASK-ES8: Translation strings across all 5 locales

```yaml
task: Add all new UI strings to en/cs/sk/uk/ru for both mobile and web
id: TASK-ES8
type: content
priority: medium
specialist: frontend
app: customer-android + customer-web
estimated_complexity: small
recommended_model: sonnet

context: |
  Consolidation pass. Previous tasks add strings locale-by-locale as
  they're introduced — this task is an audit + completion pass to
  guarantee parity across all 5 locales.

  Mobile strings touched by prior tasks:
    - booking_extras_header
    - booking_extras_selected_count
    - booking_extras_none_selected
    - booking_slot_express_surcharge_badge
    - booking_slot_express_warning_title
    - booking_slot_express_warning_body
    - booking_slot_express_warning_confirm
    - booking_slot_express_warning_cancel

  Web strings touched:
    - customer.order_wizard.extras_section_title
    - customer.order_wizard.extras_none_selected
    - customer.order_wizard.extras_subtotal_line
    - customer.order_wizard.surcharge_express_line
    - customer.order_wizard.surcharge_express_tooltip

  Extra translations (domain — seeded in DB):
    - Each of the 5 extras needs en/cs/sk/uk/ru entries in the
      Translations JSON column. Executor drafts, PM reviews.

files_to_modify:
  - path: src/cleansia_customer_android/app/src/main/res/values/strings.xml
    change: "confirm all 8 mobile keys present and English-correct"
  - path: src/cleansia_customer_android/app/src/main/res/values-cs/strings.xml
    change: "all 8 mobile keys with Czech translations"
  - path: src/cleansia_customer_android/app/src/main/res/values-sk/strings.xml
    change: "all 8 mobile keys with Slovak translations"
  - path: src/cleansia_customer_android/app/src/main/res/values-uk/strings.xml
    change: "all 8 mobile keys with Ukrainian translations"
  - path: src/cleansia_customer_android/app/src/main/res/values-ru/strings.xml
    change: "all 8 mobile keys with Russian translations"

  - path: src/Cleansia.App/apps/cleansia.app/src/assets/i18n/en.json
    change: "confirm all 5 web keys present"
  - path: src/Cleansia.App/apps/cleansia.app/src/assets/i18n/cs.json
    change: "all 5 web keys with Czech translations"
  - path: src/Cleansia.App/apps/cleansia.app/src/assets/i18n/sk.json
    change: "all 5 web keys with Slovak translations"
  - path: src/Cleansia.App/apps/cleansia.app/src/assets/i18n/uk.json
    change: "all 5 web keys with Ukrainian translations"
  - path: src/Cleansia.App/apps/cleansia.app/src/assets/i18n/ru.json
    change: "all 5 web keys with Russian translations"

  - path: sql-scripts/insert_seed_data.sql
    change: |
      Verify each of the 5 Extra rows' Translations JSON has all 5
      language keys filled. If any are missing (e.g. English-only
      fallback from TASK-ES1's initial draft), add them now.

dependencies:
  - TASK-ES4
  - TASK-ES6
  - TASK-ES7
verification:
  - Switch mobile app language to each of 5 locales → extras UI + surcharge copy render in the locale
  - Switch web language to each of 5 locales → wizard extras + surcharge copy render in the locale
  - Query extras from the DB in each locale → translated names surface correctly in the overview endpoint (backend returns all translations; client picks the active one)
```

---

## Execution order

Execute strictly in this order, respecting dependencies:

### Backend phase (blocking — must finish before mobile/web)

1. **TASK-ES1** — Extra entity, EF config, repo, seed.
   → **MANUAL STEP:** Owner creates + applies EF migration. Owner re-runs seed SQL.
2. **TASK-ES2** — Customer overview endpoint.
   → **MANUAL STEP:** Owner regenerates customer TypeScript client via NSwag.
   → **MANUAL STEP:** Owner dumps fresh OpenAPI JSON for mobile parity.
3. **TASK-ES3** — Pricing calculator + CreateOrder / QuoteOrder wiring + validator.
   → Also requires the owner re-run NSwag regen since QuoteOrder response DTO gained fields.

### Mobile + Web — parallelizable

4. **TASK-ES4** (mobile catalog + state + UI) and **TASK-ES7** (web) in parallel.
5. **TASK-ES5** (mobile submit wiring) — depends on ES4 + ES3.
6. **TASK-ES6** (mobile express chips) — can start in parallel with ES5 (no dep on ES5).

### Wrap-up

7. **TASK-ES8** — translations audit across all 5 locales.

### Parallelism summary

- Backend must sequence ES1 → ES2 → ES3 (hard deps).
- After backend + NSwag regen, mobile (ES4/ES5/ES6) and web (ES7) run in parallel.
- ES8 is last, gating merge once all UI strings are in place.

Estimated tokens total: ~95k (ES1+ES2 ~15k backend shape; ES3 ~20k calculator + tests; ES4+ES5+ES6 ~30k mobile; ES7 ~20k web; ES8 ~10k i18n).

---

## MANUAL_STEP summary (for the owner's checklist)

| # | When | Action |
|---|---|---|
| M1 | After TASK-ES1 | `dotnet ef migrations add AddExtras` against the Database project, review, apply. |
| M2 | After TASK-ES1 | Re-run `sql-scripts/insert_seed_data.sql` to populate 5 seed Extras. |
| M3 | After TASK-ES2 | Regenerate customer TS client: `cd src/Cleansia.App && npm run generate-customer-client`. |
| M4 | After TASK-ES2 | Dump fresh OpenAPI JSON for mobile parity sanity check. |
| M5 | After TASK-ES3 | Second NSwag regen to pick up updated QuoteOrderResponse + CreateOrderCommand extras wiring. |
| M6 | Before TASK-ES7 starts | Confirm M5 happened — web task depends on fresh client types. |
| M7 | After TASK-ES8 | Verify all 5 Extras have complete Translations dict (all 5 languages) in the seeded DB. |
| M8 | Pre-merge | Backfill Extras into production DB — run a production-safe seed script, not the dev seed file as-is. Coordinate with deploy. |

Claude does NOT run any of the above. Flag each in the PR description so the owner's aware.

---

## Out of scope (explicit)

- **Per-tenant surcharge config** — `BookingPolicy` constants stay hardcoded. Follow-up task to move to `CountryConfiguration` or `TenantConfiguration`.
- **Per-service bundled extras** — no "Deep clean includes oven cleaning for free" logic. Each extra is an independent line item. If product wants bundles later, it's a new entity (ServiceExtraBundle) and a calculator change.
- **Dynamic per-minute / per-unit pricing for extras** — laundry-ironing is a flat 250 CZK, not 250 CZK/hour. Interior windows is a flat 100 CZK, not per window. Upgrading these to metered pricing is a follow-up spec with a UI change (quantity input per extra).
- **Admin CRUD for Extras** — no admin UI in this spec. Admins add/edit extras via direct SQL for MVP. Follow-up: admin extras page (mirror the services page).
- **Partner-side extras visibility** — partner app sees extras via the existing Order aggregate (they're already in `order.extras`). No changes to partner UI in this spec. If partners need a checklist view of extras during the job, that's a partner-UI spec.
- **Analytics on extras uptake** — no tracking of which extras sell, no conversion funnel, no A/B of prices. All that is follow-up analytics work.
- **Refund / modification flows when extras change mid-order** — current order lifecycle doesn't support in-flight modification of selected extras. If a customer wants to add an oven clean after confirming, they call support. Out of scope.

---

## Risk register

- **R1 — NSwag regen missed between ES2 and ES7.** If the owner forgets M3, web's TypeScript client won't know about `ExtraClient` and the wizard build fails at ES7. Mitigation: M3 is double-flagged in the MANUAL_STEP table and in ES7's preamble.
- **R2 — Surcharge math disagreement between client and server.** If the mobile-side `BookingPolicy.kt` constants drift from the backend (e.g. someone bumps the backend to 15% without updating mobile), the chip advertises +20% but the quote shows +15%. Mitigation: ES6 TODO comment plus a backlog item to move to fetched config. Acceptable drift risk for MVP.
- **R3 — Seed script conflicts with existing data.** Customers who've run previous seed scripts on local dev DBs won't automatically pick up the new Extras rows. Mitigation: M2 note in the PR description; executor adds an `ON CONFLICT DO NOTHING` guard to the seed INSERTs.
- **R4 — Order persistence of non-existent extra slugs.** If the UI is out of sync with the Extras table (e.g. admin deactivates `pet-hair-supplement` after the client cached the catalog), the user can still select it, and backend's `InvalidSelectedExtras` validator fires. Mitigation: the validator check already exists (added in ES3); UI gets a server error → snackbar. Not bulletproof but acceptable for MVP.
- **R5 — Pricing test coverage gap.** Section 2 mentions new tests but only for the calculator. `CreateOrder.Validator.PriceMatchesAsync` integration tests should also cover the extras + surcharge cases. Mitigation: add an integration test class in `src/Cleansia.IntegrationTests/` as a follow-up within this spec if time permits; otherwise flag as a separate task.

---

## Sign-off checklist

Before ANY task in Section 2 starts:

- [ ] Section 1a — Extras list + prices approved
- [ ] Section 1b — Surcharge display model picked (default: Option A — transparent)
- [ ] Section 1c — Per-tenant config deferred to follow-up (default: yes, consts stay)
- [ ] Translations drafted for all 5 Extras in all 5 locales, PM-reviewed
- [ ] TASK-BS1 + TASK-BS2 (from `mobile-booking-submission.md`) merged — this spec layers on top of them

Once all boxes are checked, the executor can pick up TASK-ES1 and start.
