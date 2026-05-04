# Mobile Booking Submission — Phase 6 (minimum-viable real submission)

**Status:** Ready for execution
**Depends on:** Address unification Phase A/B complete + `ServiceCategory` entity + seed (done; migration applied by owner)

## Decisions in scope for this spec

1. Extras dictionary stays empty on submit. Backend's `PriceMatchesAsync` doesn't price extras anyway; adding the extras UI is a later spec.
2. **Backend quote endpoint** — added here. Mobile fetches `POST /api/Order/Quote` and submits the server-returned total (prevents all client-calc drift).
3. Guest checkout skipped. Booking requires sign-in. Flagged for follow-up spec.
4. Success screen gets the real confirmation code + order id; UI polish (arrival window, summary) is a follow-up spec.
5. **Service categories** are already a first-class domain entity (done this morning). The existing `ServiceListItem` DTO needs to surface the category so mobile can replace its hardcoded `ServiceCategory` enum with real backend-driven chips keyed on `Slug`.

## What this spec does NOT do

- Add extras UI or any extras pricing.
- Implement a rich order summary on the success screen.
- Add a guest checkout path.
- Redesign the date/time picker (still uses today's mock slots — we just map those to real `DateTime` at submit).
- Replace the address manager UX — uses the post-Phase-B `serverId` linkage.

---

## Phase 1 — Backend (quote endpoint + category surfacing)

### TASK-BS0: Surface `ServiceCategory` on the service DTO + overview endpoint

```yaml
task: Add CategoryDto to ServiceListItem; Include(Category) in GetServiceOverview
id: TASK-BS0
type: feature
priority: high
specialist: backend
app: backend
estimated_complexity: small
recommended_model: sonnet

context: |
  ServiceCategory exists as a domain entity now, but the anonymous
  /api/service/GetOverview endpoint doesn't return category data.
  Mobile needs category.slug to map to its icon/color palette and
  category.name (or translated equivalent) for the chip label.

  Add a minimal CategoryDto — id, slug, name, displayOrder — and
  nest it on the existing ServiceListItem. Admin + internal endpoints
  can stay unchanged for now; only the customer overview needs this.

  Translations: ServiceListItem already surfaces service-level
  Translations dict. Keep the same pattern for CategoryDto — include
  a Translations property so mobile can localize the chip label
  without a separate categories/translate call.

files_to_modify:
  - path: src/Cleansia.Core.AppServices/Features/Services/DTOs/ServiceListItem.cs
    change: |
      Add `CategoryDto Category` as a required property on the record.
      Import any namespaces needed.

  - path: src/Cleansia.Core.AppServices/Mappers/ServiceMapper.cs
    # Or wherever .MapToDto() is defined — grep first
    change: |
      In the service → ServiceListItem projection, map Category to
      a CategoryDto via s.Category.MapToDto() (add the mapper).
      Projection must include s.Category — update any underlying
      IQueryable projection so EF doesn't generate N+1 queries.

  - path: src/Cleansia.Core.AppServices/Features/Services/GetServiceOverview.cs
    change: |
      Current shape:
        return await serviceRepository.GetAll()
            .Select(service => service.MapToDto())
            .ToListAsync(cancellationToken);
      Ensure the IQueryable is .Include(s => s.Category) OR that the
      projection lambda touches s.Category.* fields directly (EF will
      auto-include that column). The .Select(...MapToDto()) path
      typically auto-includes because MapToDto projects Category
      fields. Verify by running the endpoint and watching SQL logs —
      should be one query, not N+1.

files_to_create:
  - path: src/Cleansia.Core.AppServices/Features/Services/DTOs/CategoryDto.cs
    change: |
      namespace Cleansia.Core.AppServices.Features.Services.DTOs;

      public record CategoryDto(
          string Id,
          string Slug,
          string Name,
          string? Description,
          int DisplayOrder,
          IReadOnlyDictionary<string, TranslationDto>? Translations);

      public record TranslationDto(string Name, string? Description);

      Reuse whatever Translation DTO shape already exists in the
      codebase if present — grep for `record.*Translation` in
      AppServices. If one exists (likely for services), reuse it.

  - path: src/Cleansia.Core.AppServices/Mappers/ServiceCategoryMapper.cs
    # Or append to an existing mapper file if the convention is file-per-area
    change: |
      public static class ServiceCategoryMapper {
          public static CategoryDto MapToDto(this ServiceCategory category) =>
              new(
                  Id: category.Id,
                  Slug: category.Slug,
                  Name: category.Name,
                  Description: category.Description,
                  DisplayOrder: category.DisplayOrder,
                  Translations: category.Translations?.ToDictionary(
                      kv => kv.Key,
                      kv => new TranslationDto(kv.Value.Name, kv.Value.Description)));
      }

dependencies: []
verification:
  - dotnet build Cleansia.Api.sln
  - Manual: GET /api/service/GetOverview returns services with a
    non-null category block containing slug, name, translations.
  - Check SQL logs for a single JOIN query, not N+1.
```

### TASK-BS1: Extract order pricing into a reusable calculator

```yaml
task: Pull CreateOrder's price formula into IOrderPricingCalculator
id: TASK-BS1
type: refactor
priority: high
specialist: backend
app: backend
estimated_complexity: small
recommended_model: sonnet

context: |
  Today PriceMatchesAsync (src/Cleansia.Core.AppServices/Features/Orders/CreateOrder.cs
  ~line 140) runs inline in CreateOrder.Handler. The new QuoteOrder handler
  needs the exact same math. Extract to a shared calculator so there's
  zero formula drift between quote and validate.

files_to_create:
  - path: src/Cleansia.Core.AppServices/Services/Interfaces/IOrderPricingCalculator.cs
    change: |
      namespace Cleansia.Core.AppServices.Services.Interfaces;

      public record OrderPricingResult(
          decimal TotalPrice,
          string CurrencyId,
          decimal ServicesSubtotal,
          decimal PackagesSubtotal,
          decimal ExchangeRate);

      public interface IOrderPricingCalculator
      {
          Task<OrderPricingResult> CalculateAsync(
              IEnumerable<string> selectedServiceIds,
              IEnumerable<string> selectedPackageIds,
              int rooms,
              int bathrooms,
              string? currencyId,
              CancellationToken cancellationToken);
      }

  - path: src/Cleansia.Core.AppServices/Services/OrderPricingCalculator.cs
    change: |
      Port the PriceMatchesAsync formula verbatim. Load packages via
      IPackageRepository.GetByIds, services via IServiceRepository.GetByIds.
      Sum packages.Price + services.Sum(s => s.BasePrice + s.PerRoomPrice * (rooms + bathrooms)).
      Resolve currency via ICurrencyRepository.GetByIdAsync when currencyId
      provided, else GetDefaultAsync. Multiply subtotal by currency.ExchangeRate.
      Return OrderPricingResult. Keep behavior identical — do not add extras pricing.

files_to_modify:
  - path: src/Cleansia.Core.AppServices/Features/Orders/CreateOrder.cs
    line_range: '140-154' # the PriceMatchesAsync helper
    change: |
      Replace PriceMatchesAsync body with:
        var result = await _pricingCalculator.CalculateAsync(
            command.SelectedServiceIds,
            command.SelectedPackageIds,
            command.Rooms,
            command.Bathrooms,
            command.CurrencyId,
            cancellationToken);
        return result.TotalPrice == command.TotalPrice;

      Inject IOrderPricingCalculator into Handler's primary constructor.
      Remove the now-unused fields (_packageRepository, _serviceRepository,
      _currencyRepository) ONLY IF they're not used elsewhere in the Handler —
      grep first, they likely are.

  - path: src/Cleansia.Config/Services/ServiceExtensions.cs
    change: |
      Register the calculator alongside other scoped services (around line 22):
        services.AddScoped<IOrderPricingCalculator, OrderPricingCalculator>();

dependencies: []
verification:
  - dotnet build Cleansia.Api.sln
  - dotnet test src/Cleansia.Tests — all existing CreateOrder tests still green
```

### TASK-BS2: Add `QuoteOrder` command + customer endpoint

```yaml
task: New POST /api/Order/Quote endpoint returning OrderPricingResult
id: TASK-BS2
type: feature
priority: high
specialist: backend
app: backend
estimated_complexity: small
recommended_model: sonnet

context: |
  Mobile and eventually web call this before submit to get the
  server-authoritative total. Response shape matches what submit will
  validate against — zero drift.

  Endpoint is [AllowAnonymous] (same as Create) so guest checkout works
  later without changes.

files_to_create:
  - path: src/Cleansia.Core.AppServices/Features/Orders/QuoteOrder.cs
    change: |
      Mirror the CreateOrder file layout (Command + Validator + Handler
      all in one class). Command accepts SelectedServiceIds,
      SelectedPackageIds, Rooms, Bathrooms, optional CurrencyId.
      Validator: rooms/bathrooms >= 1, service/package IDs exist via
      the same repositories CreateOrder uses. Handler: delegates to
      IOrderPricingCalculator and returns BusinessResult<Response>.
      Response = OrderPricingResult (re-export or wrap; prefer wrap so
      Response stays a DTO record).

files_to_modify:
  - path: src/Cleansia.Web.Customer/Controllers/OrderController.cs
    change: |
      Add after the existing Create action:
        [HttpPost("Quote")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(QuoteOrder.Response), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Quote(
            [FromBody] QuoteOrder.Command command,
            CancellationToken cancellationToken)
        {
            var result = await Mediator.Send(command, cancellationToken);
            return HandleResult<QuoteOrder.Response>(result);
        }

  - path: src/Cleansia.Web.Mobile/Controllers/OrderController.cs
    change: |
      Same Quote action. Mobile API may or may not be the one the app hits
      today — the app uses BuildConfig.API_BASE_URL which currently points
      at :5003 (customer). But adding it to both keeps future flexibility
      cheap. If mobile's OrderController doesn't exist yet, skip this file.
      Check first.

dependencies:
  - TASK-BS1
verification:
  - dotnet build Cleansia.Api.sln
  - Manual: POST /api/Order/Quote with a valid service id → returns totalPrice
  - Manual: POST with invalid service id → 400 InvalidSelectedServices (same error as Create)
```

---

## Phase 2 — Mobile

### TASK-BS3: Catalog DTOs + CatalogApi (services & packages)

```yaml
task: Mobile DTOs and Retrofit interface for services/packages catalogs
id: TASK-BS3
type: feature
priority: high
specialist: mobile
app: customer-android
estimated_complexity: small
recommended_model: sonnet

context: |
  Mobile needs a real catalog. Backend endpoints are anonymous so
  we hand-write against @NoAuthRetrofit — matches how AuthApi is wired.

files_to_create:
  - path: src/cleansia_customer_android/app/src/main/java/cz/cleansia/customer/core/catalog/CatalogDto.kt
    change: |
      @Serializable data class TranslationDto(
          val name: String,
          val description: String? = null,
      )

      @Serializable data class CategoryDto(
          val id: String,
          val slug: String,
          val name: String,
          val description: String? = null,
          val displayOrder: Int = 0,
          val translations: Map<String, TranslationDto>? = null,
      )

      @Serializable data class ServiceListItem(
          val id: String,
          val name: String,
          val description: String? = null,
          val basePrice: Double,
          val perRoomPrice: Double,
          val category: CategoryDto,
          val translations: Map<String, TranslationDto>? = null,
      )

      @Serializable data class PackageServiceSummary(
          val id: String,
          val name: String,
      )

      @Serializable data class PackageListItem(
          val id: String,
          val name: String,
          val description: String? = null,
          val price: Double,
          val translations: Map<String, TranslationDto>? = null,
          val includedServices: List<PackageServiceSummary>? = null,
      )

      Note: backend uses decimal; JSON is typically numeric. Use Double
      here (matches Mapbox lat/lng precedent).
      Category is required (non-nullable) — every service has one per
      the domain invariant established in the ServiceCategory task.

  - path: src/cleansia_customer_android/app/src/main/java/cz/cleansia/customer/core/catalog/CatalogApi.kt
    change: |
      interface CatalogApi {
          @GET("api/service/GetOverview")
          suspend fun getServices(): Response<List<ServiceListItem>>

          @GET("api/package/GetOverview")
          suspend fun getPackages(): Response<List<PackageListItem>>
      }

  - path: src/cleansia_customer_android/app/src/main/java/cz/cleansia/customer/core/catalog/CatalogModule.kt
    change: |
      @Module @InstallIn(SingletonComponent::class) object CatalogModule {
          @Provides @Singleton
          fun provideCatalogApi(@NoAuthRetrofit retrofit: Retrofit): CatalogApi =
              retrofit.create(CatalogApi::class.java)
      }

dependencies: []
verification:
  - Syntactic check — no gradle wrapper in this project
```

### TASK-BS4: `CatalogRepository` with in-memory cache

```yaml
task: Repository that fetches + caches services/packages
id: TASK-BS4
type: feature
priority: high
specialist: mobile
app: customer-android
estimated_complexity: small
recommended_model: sonnet

context: |
  Pattern mirrors UserRepository. Fetch once, cache in StateFlow, expose
  refresh() + getters. No DataStore — catalog is cheap enough to refetch
  on cold start. Snackbar on failure.

files_to_create:
  - path: src/cleansia_customer_android/app/src/main/java/cz/cleansia/customer/core/catalog/CatalogRepository.kt
    change: |
      @Singleton class CatalogRepository @Inject constructor(
          private val api: CatalogApi,
          private val snackbar: SnackbarController,
          @ApplicationContext private val appContext: Context,
      ) {
          private val _services = MutableStateFlow<List<ServiceListItem>>(emptyList())
          private val _packages = MutableStateFlow<List<PackageListItem>>(emptyList())
          private val _loading = MutableStateFlow(false)

          val services: StateFlow<List<ServiceListItem>> = _services.asStateFlow()
          val packages: StateFlow<List<PackageListItem>> = _packages.asStateFlow()
          val loading: StateFlow<Boolean> = _loading.asStateFlow()

          // Distinct, display-order-sorted categories derived from the service list.
          // No separate endpoint — every service carries its category inline, so
          // distinct-by-slug + sort is enough. If we ever want categories with
          // zero services to still render as chips, add a /categories endpoint later.
          val categories: StateFlow<List<CategoryDto>> = _services
              .map { list -> list.map { it.category }.distinctBy { it.slug }.sortedBy { it.displayOrder } }
              .stateIn(GlobalScope, SharingStarted.Eagerly, emptyList())

          suspend fun refresh(): String? {
              _loading.value = true
              try {
                  val s = runCatching { api.getServices() }.getOrNull()
                  val p = runCatching { api.getPackages() }.getOrNull()
                  if (s == null || p == null || !s.isSuccessful || !p.isSuccessful) {
                      snackbar.showErrorKey(R.string.error_generic_network)
                      return appContext.getString(R.string.error_generic_network)
                  }
                  _services.value = s.body().orEmpty()
                  _packages.value = p.body().orEmpty()
                  return null
              } finally {
                  _loading.value = false
              }
          }
      }

dependencies:
  - TASK-BS3
verification:
  - Syntactic check
```

### TASK-BS5: Wire catalog into ServicesStep — real data, not mocks

```yaml
task: ServicesStep reads from CatalogRepository; BookingState keeps IDs only
id: TASK-BS5
type: refactor
priority: high
specialist: mobile
app: customer-android
estimated_complexity: medium
recommended_model: sonnet

context: |
  BookingState today holds full ServiceItem/PackageItem objects.
  Switch to IDs only — selectedServiceIds, selectedPackageIds. The
  step component reads the real list from CatalogRepository and
  renders toggleable cards.

files_to_modify:
  - path: src/cleansia_customer_android/app/src/main/java/cz/cleansia/customer/features/booking/BookingState.kt
    change: |
      Replace:
        selectedServices: List<ServiceItem>
        selectedPackages: List<PackageItem>
      With:
        selectedServiceIds: Set<String> = emptySet()
        selectedPackageIds: Set<String> = emptySet()

      Delete the local ServiceItem/PackageItem/ServiceTier/FeaturedPackage
      data classes if they're not reused. ServicesStep can hold them
      transiently; state is just the IDs.

      servicesTotal() becomes a function that takes the catalog maps
      (or lives in a BookingViewModel that has the catalog injected).
      Simpler: delete servicesTotal() from BookingState entirely. Price
      display comes from QuoteOrder response (TASK-BS7), not local calc.

  - path: src/cleansia_customer_android/app/src/main/java/cz/cleansia/customer/features/booking/ServicesStep.kt
    line_range: '59-125' # the hardcoded ServiceCategory enum + mock literals
    change: |
      1. DELETE the hardcoded `ServiceCategory` enum (lines 59-65).
         Categories now come from the backend.

      2. Keep the LOCAL icon/color lookup — this is UX design, not
         data. Define it as a top-level map keyed by backend slug:

           private data class CategoryPalette(val icon: ImageVector, val tint: Color)

           private val CategoryPalettes = mapOf(
               "home" to CategoryPalette(Icons.Outlined.CleaningServices, Color(0xFF0284C7)),
               "deep" to CategoryPalette(Icons.Outlined.Spa, Color(0xFF7C3AED)),
               "laundry" to CategoryPalette(Icons.Outlined.LocalLaundryService, Color(0xFF0891B2)),
               "pet" to CategoryPalette(Icons.Outlined.Pets, Color(0xFFEA580C)),
           )

           // Fallback for any future backend slug we don't yet have a palette for.
           private val DefaultPalette = CategoryPalette(Icons.Outlined.Star, Color(0xFF0284C7))

           private fun CategoryDto.palette() = CategoryPalettes[slug] ?: DefaultPalette

      3. DELETE the hardcoded service/package catalogs (lines 77-125).

      4. Inject CatalogRepository via EntryPoint (mirrors
         AddressRepositoryEntryPoint). Observe repo.services +
         repo.packages + repo.categories.

      5. The chip row iterates `repo.categories()` + the synthetic
         "All" entry prepended. Each chip uses `category.palette()`
         for icon/color. Label uses the best-available translated
         name — pull the current app language from AppSettingsRepository
         and read category.translations[lang]?.name ?: category.name.

      6. The services list filter now keys on category.id (not the
         dead enum): `service.category.id == activeCategoryId`.
         `activeCategoryId: String?` — null means "All".

      7. Service cards:
         - icon + tint pulled from `service.category.palette()`
         - toggle state against BookingState.selectedServiceIds
         - tap → vm.update { s -> s.copy(selectedServiceIds = ...) }

      8. Price display per card reads service.basePrice directly
         (server-side currency is CZK for MVP; quote endpoint handles
         conversion later).

      9. Empty state: while loading = skeleton / "Loading services...".
         On failure, snackbar already fired; show a retry button
         that calls catalogRepo.refresh() again.

      10. strings.xml — DELETE the `booking_cat_*` keys in
          values/ and all locale folders. They were tied to the
          hardcoded enum; translations now live on the backend
          category record.

  - path: src/cleansia_customer_android/app/src/main/java/cz/cleansia/customer/features/main/MainShell.kt
    change: |
      In the existing LaunchedEffect that refreshes profile + addresses,
      also trigger catalog refresh. Mirror the same EntryPoint pattern:

        val catalogRepo = remember {
            EntryPointAccessors.fromApplication(
                context, CatalogRepositoryEntryPoint::class.java
            ).catalogRepository()
        }
        LaunchedEffect(Unit) { catalogRepo.refresh() }

      Create CatalogRepositoryEntryPoint.kt (mirrors AddressRepositoryEntryPoint).

files_to_create:
  - path: src/cleansia_customer_android/app/src/main/java/cz/cleansia/customer/core/catalog/CatalogRepositoryEntryPoint.kt
    change: |
      @EntryPoint @InstallIn(SingletonComponent::class)
      interface CatalogRepositoryEntryPoint {
          fun catalogRepository(): CatalogRepository
      }

dependencies:
  - TASK-BS4
verification:
  - Services tab shows real services from backend (once a backend is running)
  - Selection state survives step transitions
  - Empty/loading states render sanely
```

### TASK-BS6: Address linkage (TASK-B5 from the prior spec, closed here)

```yaml
task: BookingState carries serverId when saved address is picked
id: TASK-BS6
type: feature
priority: high
specialist: mobile
app: customer-android
estimated_complexity: small
recommended_model: sonnet

context: |
  Phase B left BookingState copying street/city/zipCode from the
  picked UserAddress but dropping serverId. The submit payload needs
  to know: saved → savedAddressId, one-off → inline customerAddress.

files_to_modify:
  - path: src/cleansia_customer_android/app/src/main/java/cz/cleansia/customer/features/booking/BookingState.kt
    change: |
      Add alongside street/city/zipCode:
        savedAddressId: String? = null

      Invariant: if savedAddressId != null, street/city/zipCode must
      still be populated too (for UI display) but they're redundant
      for the backend. On submit we send savedAddressId and OMIT
      customerAddress.

      If user edits the form fields manually after picking a saved
      address, null out savedAddressId (they're overriding to a
      one-off now). Trigger this in the WhenWhereStep field listeners
      OR in whatever BookingViewModel owns BookingState.

  - path: src/cleansia_customer_android/app/src/main/java/cz/cleansia/customer/features/booking/WhenWhereStep.kt
    change: |
      In the AddressManagerScreen.onAddressSelected callback (around
      lines 368-374), when the picked UserAddress has serverId != null,
      copy it into BookingState.savedAddressId alongside the display
      fields. When serverId == null (user picked a one-off on-the-fly
      from the map without saving it), set savedAddressId = null.

      Field listeners for street/city/zipCode inputs: on user edit,
      bookingState.savedAddressId = null.

dependencies: []
verification:
  - Pick saved address → state has savedAddressId
  - Pick unsaved one-off → state has savedAddressId = null, inline fields populated
  - Edit the street field after picking saved → savedAddressId nulled
```

### TASK-BS7: `BookingViewModel` + submit/quote wiring

```yaml
task: ViewModel owns BookingState, calls Quote before submit, calls Create
id: TASK-BS7
type: feature
priority: high
specialist: mobile
app: customer-android
estimated_complexity: large
recommended_model: sonnet

context: |
  BookingState today is a remember-ed local in BookingBottomSheet. Move
  to a @HiltViewModel scoped to the booking graph (or the bottom sheet
  composable using hiltViewModel() with a NavBackStackEntry).

  On step-3 confirm swipe:
    1. Build a QuoteOrder.Command from current state.
    2. Call /api/Order/Quote. On failure, snackbar + abort.
    3. Use returned totalPrice + currencyId as the submit command's values.
    4. Build CreateOrder.Command, call /api/Order/Create.
    5. On success, pop state + navigate to Success with confirmation code.
    6. On failure, parse ProblemDetails, snackbar, stay on confirm screen.

  Customer contact: pulled from UserRepository.currentUser (already
  loaded at shell start). If currentUser is null at submit time, show
  snackbar "Please sign in to continue" (guest checkout is a separate
  spec).

files_to_create:
  - path: src/cleansia_customer_android/app/src/main/java/cz/cleansia/customer/core/booking/BookingApi.kt
    change: |
      interface BookingApi {
          @POST("api/Order/Quote")
          suspend fun quote(@Body command: QuoteOrderCommand): Response<QuoteOrderResponse>

          @POST("api/Order/Create")
          suspend fun create(@Body command: CreateOrderCommand): Response<CreateOrderResponse>
      }

      Routes verify against backend — Create currently lives at
      /api/Order/Create; Quote is added by TASK-BS2.

      Include the command and response data classes in this file or a
      sibling BookingDtos.kt:

      @Serializable data class QuoteOrderCommand(
          val selectedServiceIds: List<String>,
          val selectedPackageIds: List<String>,
          val rooms: Int,
          val bathrooms: Int,
          val currencyId: String? = null,
      )
      @Serializable data class QuoteOrderResponse(
          val totalPrice: Double,
          val currencyId: String,
          val servicesSubtotal: Double,
          val packagesSubtotal: Double,
          val exchangeRate: Double,
      )

      @Serializable data class CreateOrderAddressDto(
          val street: String,
          val city: String,
          val zipCode: String,
          val countryId: String? = null,
          val state: String? = null,
      )
      @Serializable data class CreateOrderCommand(
          val customerName: String,
          val customerEmail: String,
          val customerPhone: String,
          val customerAddress: CreateOrderAddressDto? = null,
          val savedAddressId: String? = null,
          val selectedPackageIds: List<String>,
          val selectedServiceIds: List<String>,
          val rooms: Int,
          val bathrooms: Int,
          val extras: Map<String, Boolean> = emptyMap(),
          val cleaningDate: String, // ISO-8601
          val paymentType: Int,     // 1 = Cash, 2 = Card (match backend enum)
          val currencyId: String? = null,
          val totalPrice: Double,
          val language: String = "en",
      )
      @Serializable data class CreateOrderResponse(
          val id: String,
          val confirmationCode: String,
          val stripeSessionId: String? = null,
      )

  - path: src/cleansia_customer_android/app/src/main/java/cz/cleansia/customer/core/booking/BookingModule.kt
    change: |
      @Provides @Singleton
      fun provideBookingApi(@AuthRetrofit retrofit: Retrofit): BookingApi =
          retrofit.create(BookingApi::class.java)

      Rationale: CreateOrder is AllowAnonymous backend-side so @NoAuthRetrofit
      would also work, but @AuthRetrofit lets us include the user's Bearer
      token which the backend uses (when present) to tie the order to the
      customer and preload their email. Functionally OK either way;
      @AuthRetrofit is the better default.

  - path: src/cleansia_customer_android/app/src/main/java/cz/cleansia/customer/features/booking/BookingViewModel.kt
    change: |
      @HiltViewModel class BookingViewModel @Inject constructor(
          private val bookingApi: BookingApi,
          private val userRepository: UserRepository,
          private val snackbar: SnackbarController,
          @ApplicationContext private val context: Context,
      ) : ViewModel() {
          private val _state = MutableStateFlow(BookingState())
          val state: StateFlow<BookingState> = _state.asStateFlow()

          private val _submitting = MutableStateFlow(false)
          val submitting: StateFlow<Boolean> = _submitting.asStateFlow()

          private val _quote = MutableStateFlow<QuoteOrderResponse?>(null)
          val quote: StateFlow<QuoteOrderResponse?> = _quote.asStateFlow()

          fun update(transform: (BookingState) -> BookingState) {
              _state.value = transform(_state.value)
          }

          // Call whenever selections/rooms change to refresh the price.
          suspend fun refreshQuote() {
              val s = _state.value
              if (s.selectedServiceIds.isEmpty() && s.selectedPackageIds.isEmpty()) {
                  _quote.value = null
                  return
              }
              val result = runCatching {
                  bookingApi.quote(QuoteOrderCommand(
                      selectedServiceIds = s.selectedServiceIds.toList(),
                      selectedPackageIds = s.selectedPackageIds.toList(),
                      rooms = s.rooms,
                      bathrooms = s.bathrooms,
                  ))
              }.getOrNull()
              if (result?.isSuccessful == true) {
                  _quote.value = result.body()
              }
              // Quote failures are silent during editing — user hasn't tried
              // to submit yet. Log only. Failure at submit-time surfaces below.
          }

          suspend fun submit(): CreateOrderResponse? {
              if (_submitting.value) return null
              _submitting.value = true
              try {
                  val user = userRepository.currentUser.value
                  if (user == null) {
                      snackbar.showErrorKey(R.string.error_booking_sign_in_required)
                      return null
                  }
                  val s = _state.value
                  val quoteCmd = QuoteOrderCommand(
                      selectedServiceIds = s.selectedServiceIds.toList(),
                      selectedPackageIds = s.selectedPackageIds.toList(),
                      rooms = s.rooms,
                      bathrooms = s.bathrooms,
                  )
                  val quoteResp = runCatching { bookingApi.quote(quoteCmd) }
                      .getOrNull()
                  if (quoteResp == null || !quoteResp.isSuccessful) {
                      val msg = if (quoteResp != null) {
                          ApiErrorParser.parseToUserMessage(context, quoteResp.errorBody(), quoteResp.code())
                      } else context.getString(R.string.error_generic_network)
                      snackbar.showError(msg)
                      return null
                  }
                  val quoted = quoteResp.body()!!

                  val createCmd = CreateOrderCommand(
                      customerName = listOfNotNull(user.firstName, user.lastName).joinToString(" "),
                      customerEmail = user.email,
                      customerPhone = user.phoneNumber.orEmpty(),
                      customerAddress = if (s.savedAddressId == null)
                          CreateOrderAddressDto(s.street, s.city, s.zipCode) else null,
                      savedAddressId = s.savedAddressId,
                      selectedPackageIds = s.selectedPackageIds.toList(),
                      selectedServiceIds = s.selectedServiceIds.toList(),
                      rooms = s.rooms,
                      bathrooms = s.bathrooms,
                      extras = emptyMap(),
                      cleaningDate = combineToIso8601(s.selectedDate, s.selectedTime),
                      paymentType = if (s.paymentMethod == "card") 2 else 1,
                      currencyId = quoted.currencyId,
                      totalPrice = quoted.totalPrice,
                  )
                  val createResp = runCatching { bookingApi.create(createCmd) }
                      .getOrNull()
                  if (createResp == null || !createResp.isSuccessful) {
                      val msg = if (createResp != null) {
                          ApiErrorParser.parseToUserMessage(context, createResp.errorBody(), createResp.code())
                      } else context.getString(R.string.error_generic_network)
                      snackbar.showError(msg)
                      return null
                  }
                  return createResp.body()
              } finally {
                  _submitting.value = false
              }
          }

          private fun combineToIso8601(dateLabel: String, timeLabel: String): String {
              // Today's mock picker outputs date labels like "Today"/"Wed"/"23"
              // and time like "10:00". Map to a LocalDateTime.
              // Short-term: resolve the label via WhenWhereStep's own mapping logic
              // (it has the Date objects already) and hand a precomputed ISO string
              // into BookingState instead. Decide with the executor: easier to
              // push that logic into WhenWhereStep than reconstruct here.
              // Target: ISO-8601 UTC, e.g. "2026-04-25T10:00:00Z"
              TODO("resolve via WhenWhereStep's date mapping")
          }
      }

      Implementation note for the executor: prefer to store the
      ACTUAL LocalDateTime on BookingState (e.g. selectedInstant: Instant?)
      instead of string labels — change WhenWhereStep to write the real
      picked value. Then combineToIso8601 just does selectedInstant.toString().

files_to_modify:
  - path: src/cleansia_customer_android/app/src/main/java/cz/cleansia/customer/features/booking/BookingState.kt
    change: |
      Add selectedInstant: Instant? = null (kotlinx.datetime.Instant).
      Keep the mock selectedDate/selectedTime strings for UI display ONLY;
      the source of truth for submit is selectedInstant.

  - path: src/cleansia_customer_android/app/src/main/java/cz/cleansia/customer/features/booking/WhenWhereStep.kt
    change: |
      Wherever a time slot is selected, compute the full Instant from
      the day + slot labels and write to bookingState.selectedInstant.
      This is the only real logic change to WhenWhereStep in this spec.

  - path: src/cleansia_customer_android/app/src/main/java/cz/cleansia/customer/features/booking/BookingBottomSheet.kt
    change: |
      Replace the remember { mutableStateOf(BookingState()) } with
      hiltViewModel<BookingViewModel>() and a collectAsState on state.

      The onComplete handler (line ~333): replace the direct call with:

        scope.launch {
            val response = bookingVm.submit()
            if (response != null) {
                onBookingComplete(response.confirmationCode, response.id)
            }
            // else: snackbar was shown, sheet stays open for retry
        }

      Signature change: onBookingComplete(code, orderId) — cascade
      through MainShell and CleansiaNavHost to the BookingSuccess route.

  - path: src/cleansia_customer_android/app/src/main/java/cz/cleansia/customer/features/main/MainShell.kt
    change: |
      Update onBookingComplete to accept (confirmationCode, orderId) and
      forward to the nav caller.

  - path: src/cleansia_customer_android/app/src/main/java/cz/cleansia/customer/navigation/CleansiaNavHost.kt
    change: |
      Update BookingSuccess route to accept confirmationCode + orderId
      as nav arguments. Pass into BookingSuccessScreen.

  - path: src/cleansia_customer_android/app/src/main/java/cz/cleansia/customer/features/booking/BookingSuccessScreen.kt
    change: |
      Add confirmationCode: String, orderId: String parameters.
      Display the confirmation code prominently. UI polish (arrival
      window, summary) is a follow-up spec — this task just shows
      the code somewhere readable.

  - path: src/cleansia_customer_android/app/src/main/res/values/strings.xml
    change: |
      Add:
        <string name="error_booking_sign_in_required">Please sign in to complete your booking.</string>
        <string name="booking_success_confirmation_code">Confirmation code</string>
      Mirror across values-cs/sk/uk/ru with sensible translations.

dependencies:
  - TASK-BS2 # backend quote endpoint
  - TASK-BS5 # real catalog so state has real IDs
  - TASK-BS6 # address linkage
verification:
  - Build the Android app in Android Studio (no gradle wrapper for CLI)
  - Manual flow: sign in → pick a saved address (or one-off) → pick
    services → pick date/time → confirm. Network tab shows
    /api/Order/Quote then /api/Order/Create. Success screen renders
    confirmation code.
  - Pick a service combo, submit — verify server-calculated totalPrice
    is what's sent on Create (not a client estimate).
  - Submit with only saved address → request body has savedAddressId,
    no customerAddress block.
  - Submit with only inline → request body has customerAddress, no
    savedAddressId.
  - Sign out, try to book → "Please sign in to continue" snackbar.
```

---

## Execution order

1. **TASK-BS0** (CategoryDto on ServiceListItem) — backend, no deps; parallelizable with BS1
2. **TASK-BS1** (pricing calculator extraction) — backend, parallelizable with BS0
3. **TASK-BS2** (Quote endpoint) — backend, depends on BS1

**→ MANUAL STEP: dump fresh OpenAPI spec from local customer API**
```
curl http://localhost:5003/swagger/v1/swagger.json > src/cleansia_customer_android/openapi/customer-api.json
```
(Sanity dump — mobile is hand-written, doesn't consume the JSON.)

**→ MANUAL STEP: NSwag regen for web TypeScript client** (so web can consume Quote too later)
```
cd src/Cleansia.App && npm run generate-customer-client
```

4. **TASK-BS3** (mobile catalog DTOs + API) — mobile, depends on BS0 (needs CategoryDto shape confirmed)
5. **TASK-BS4** (catalog repo) — mobile, depends on BS3
6. **TASK-BS5** (wire ServicesStep to real catalog + backend categories) — mobile, depends on BS4
7. **TASK-BS6** (address linkage) — mobile, no deps — parallelizable with BS3/4/5
8. **TASK-BS7** (BookingViewModel + submit wiring + success screen) — mobile, depends on BS2 + BS5 + BS6

Parallelizable: BS0+BS1 together (backend); BS3+BS6 together (mobile); BS4 after BS3; BS5 after BS4.

Estimated tokens: ~85k total (BS0 adds ~15k).

---

## Out of scope (followup specs)

- **Guest checkout** — booking currently requires sign-in. Open question: what's the right UX — prompt for sign-in inside the sheet, or redirect to sign-in with a return-to-booking flow? Worth a small design pass.
- **Extras UI + backend pricing** — backend already accepts the dict but ignores it in pricing. Need product call on what extras cost and how they present in the UI.
- **Booking success UI polish** — order summary block, arrival window, map preview, "view order" CTA wired to real order detail.
- **Price-impact live updates** — call `/Quote` whenever selection changes so the bottom-sheet footer shows the live total instead of showing nothing until submit.
- **Date/time picker redesign** — today's "Today/Wed/23" labels are cute but brittle. A real calendar + time picker with lead-time enforcement from BookingPolicy is a bigger UX pass.
- **Extras surcharge pricing** — `BookingPolicy.ExpressSurchargeRate` (20%) is referenced in the CreateOrder validator but not in pricing. Audit whether that's a bug or intentional.
