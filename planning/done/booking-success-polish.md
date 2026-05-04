# Booking Success Screen Polish — rich post-submit experience

**Status:** Ready for execution
**Depends on:** `mobile-booking-submission` spec landed (real CreateOrder submission + confirmation code flowing into BookingSuccessScreen). Addresses unified (Phase A/B). ServiceCategory first-class domain entity in place.

## Decisions in scope for this spec

1. **Two coupled goals, one spec:**
   - Enrich the Booking Success screen with arrival window, order summary, and data-driven "what happens next" timeline.
   - Wire the "Track this booking" / "View Orders" CTAs to a real `OrderDetailScreen` backed by a real `OrderApi`. Today's `OrderDetailScreen.kt` is 100% mock (`order = sampleOrder()` at line 118).
2. **Happy-path first render is instant.** BookingViewModel caches a `BookingResult` (the submitted state + the `CreateOrderResponse`) in a `StateFlow<BookingResult?>`. BookingSuccessScreen reads that synchronously — no network wait on the happy path.
3. **Detail endpoint enriches in the background.** On entry, BookingSuccessScreen *also* kicks off an `OrderRepository.refresh(orderId)`. When it resolves, any server-authoritative fields (cleaner-assigned state, confirmed arrival window) replace the cached values. On fetch failure we silently fall back to the cached `BookingResult` — no user-visible error on success screens.
4. **OrderDetail screen is a first-class route.** `Routes.orderDetail(id)` accepts an orderId nav arg. The screen uses `hiltViewModel<OrderDetailViewModel>()` which calls `OrderRepository.refresh(orderId)` on first composition. Loading and error states are proper (skeleton + retry), not silent.
5. **Backend DTO audit first.** Before writing any mobile code we audit what the existing customer-facing `GET /api/Order/GetById/{id}` (or equivalent — find the exact route) returns. If it's missing fields the UI needs, extend it in TASK-SP1. If a field is genuinely blocked (e.g. cleaner name requires the cleaner surface we don't have yet), the UI gracefully hides that section — flagged as a follow-up, not a blocker.
6. **Signed-in-only.** Guest checkout doesn't exist yet. The success screen assumes an authenticated user. Guest success + confirmation-code-only lookup is a separate spec.
7. **No new services/packages surface from this spec.** BookingSuccessScreen displays the selection the user already made (from `BookingResult`); OrderDetailScreen reads whatever the backend returns.

## What this spec does NOT do

- Push notification wiring (FCM) for cleaner-assigned state. BookingSuccessScreen's timeline shows "waiting" copy; when a cleaner assigns, the next time the user opens the app they see the fresh state from OrderDetail. Real-time push is a separate spec.
- Order cancellation from the success screen.
- Tipping flow.
- Reschedule flow.
- Map preview (Mapbox static map) of the cleaning address.
- Cleaner profile preview — would require a cleaner data surface that doesn't exist in the mobile customer app yet.
- Guest success screen (non-signed-in user completing a booking and needing to look it up via confirmation code). Backend will need an anonymous "find by confirmation code + email/phone" endpoint for that; out of scope here.
- Redesigning `BookingSuccessScreen.kt`'s mascot+hero block — keep existing imagery.
- Adding invoice/receipt download CTAs — separate spec once `EmployeeInvoice` / receipt PDFs are surfaced for customers.

---

## Phase 1 — Backend (DTO audit + minimal extension)

### TASK-SP1: Audit + extend customer `GET /api/Order/{id}` DTO

```yaml
task: Audit customer-facing order detail endpoint; ensure DTO carries everything the mobile success + detail screens need
id: TASK-SP1
type: feature
priority: high
specialist: backend
app: backend
estimated_complexity: small
recommended_model: sonnet

context: |
  Mobile needs a customer-facing order detail endpoint that returns
  enough data to render:
    - confirmation code
    - order status (Order.Status enum)
    - cleaningDate (arrival window — today's model uses a single DateTime;
      if there's an explicit window end time somewhere, surface both;
      otherwise UI derives a 2h window client-side from cleaningDate)
    - totalPrice + currencyId
    - paymentType (cash/card)
    - services[] — id + name (translated fallback) + basePrice
    - packages[] — id + name (translated fallback) + price
    - rooms + bathrooms
    - customerAddress — inline street/city/zipCode either stored on Order
      or loaded via savedAddressId → UserAddress join
    - assignedEmployeeId? (nullable, populated when a cleaner took the order)

  Step 1 — audit. Find the exact route. Grep the customer web project
  for an OrderController that has a GetById / Get / GetOrderById action.
  Possible locations:
    - src/Cleansia.Web.Customer/Controllers/OrderController.cs
    - src/Cleansia.Web.Mobile/Controllers/OrderController.cs
  If neither exposes a per-order getter for customers today, we'll need
  to add one. The partner/admin APIs already have richer order-detail
  endpoints — reuse the DTO shape where it makes sense, strip any
  fields that shouldn't leak to customers (employee pay, internal notes).

  Step 2 — list the existing DTO's fields in a code comment on the
  audit PR description so we have a paper trail.

  Step 3 — extend the DTO minimally to cover the gap. Keep the record
  shape compatible; additive changes only. Any field the customer UI
  would display but the backend doesn't yet have → DO NOT invent it
  here, flag as follow-up. The UI hides missing sections gracefully.

files_to_modify:
  - path: src/Cleansia.Web.Customer/Controllers/OrderController.cs
    change: |
      If no GetById action exists:
        [HttpGet("{id}")]
        [Authorize]
        [ProducesResponseType(typeof(GetOrderDetail.Response), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetById(
            string id,
            CancellationToken cancellationToken)
        {
            var result = await Mediator.Send(
                new GetOrderDetail.Query(id), cancellationToken);
            return HandleResult<GetOrderDetail.Response>(result);
        }

      If a GetById action exists but returns an incomplete DTO, leave
      the action signature alone and only extend the Response record
      that the handler returns.

      Policy / tenancy note: the handler must enforce that the caller's
      UserId matches the order's CustomerId (or the caller has an admin
      role). Unauthorized → 404 (don't leak existence). This is standard
      for customer-scoped reads — mirror whatever the existing
      GetOrders / GetPagedOrders query does for filter predicates.

  - path: src/Cleansia.Core.AppServices/Features/Orders/GetOrderDetail.cs
    # Or wherever the existing customer-facing getter lives
    change: |
      Ensure the Response record exposes (additive-only on existing
      DTO — don't break partner/admin callers if the DTO is shared;
      if shared, prefer a new customer-specific DTO):

        public record Response(
            string Id,
            string ConfirmationCode,
            int Status,                 // OrderStatus enum int
            DateTime CleaningDate,
            decimal TotalPrice,
            string CurrencyId,
            int PaymentType,            // 1 = Cash, 2 = Card
            int Rooms,
            int Bathrooms,
            OrderAddressDto? CustomerAddress,
            IReadOnlyList<OrderServiceDto> Services,
            IReadOnlyList<OrderPackageDto> Packages,
            string? AssignedEmployeeId);

        public record OrderAddressDto(
            string Street, string City, string ZipCode,
            string? CountryId, string? State);

        public record OrderServiceDto(
            string Id, string Name, decimal BasePrice);

        public record OrderPackageDto(
            string Id, string Name, decimal Price);

      Handler projection must Include(order => order.Services),
      Include(order => order.Packages), and resolve the address either
      from the inline snapshot on Order or via the SavedAddress join,
      whichever the current Order model stores. Translation of service
      /package names: fall back to the default name field — a full
      per-language translation layer on order detail is a follow-up;
      today's customer UI already handles missing translations by
      showing the English name.

files_to_create: []

dependencies: []
verification:
  - dotnet build Cleansia.Api.sln
  - dotnet test src/Cleansia.Tests — existing order tests green
  - Manual: sign in as a customer who placed an order; GET /api/Order/{theirOrderId} → 200 with full DTO
  - Manual: GET /api/Order/{someoneElsesOrderId} → 404 (NOT 403; don't leak existence)
  - SQL log check: single query with JOINs, not N+1 across services/packages
  - If any field from the target list is NOT available in the domain,
    file a follow-up note in the PR description listing: field name +
    what backend work would be required to source it. Example follow-up:
    "AssignedEmployeeId is populated but the cleaner's display name is not —
    would need a separate /api/Order/{id}/Cleaner endpoint or an inline
    Cleaner sub-DTO. Deferred."
```

---

## Phase 2 — Mobile (API + repo + OrderDetail screen)

### TASK-SP2: Mobile `OrderApi` + DTOs

```yaml
task: Hand-write OrderApi retrofit interface and order-detail DTOs
id: TASK-SP2
type: feature
priority: high
specialist: mobile
app: customer-android
estimated_complexity: small
recommended_model: sonnet

context: |
  Mobile is hand-written against retrofit — no nswag / no codegen.
  Mirror how BookingApi is structured. Reuse ApiErrorParser for failures.

  Endpoint is authenticated — use @AuthRetrofit so the user's Bearer
  token is attached. Backend enforces that the caller owns the order.

files_to_create:
  - path: src/cleansia_customer_android/app/src/main/java/cz/cleansia/customer/core/orders/OrderDto.kt
    change: |
      @Serializable data class OrderAddressDto(
          val street: String,
          val city: String,
          val zipCode: String,
          val countryId: String? = null,
          val state: String? = null,
      )

      @Serializable data class OrderServiceDto(
          val id: String,
          val name: String,
          val basePrice: Double,
      )

      @Serializable data class OrderPackageDto(
          val id: String,
          val name: String,
          val price: Double,
      )

      @Serializable data class OrderDetailDto(
          val id: String,
          val confirmationCode: String,
          val status: Int,                // 0 New, 1 Pending, 2 Confirmed, 3 InProgress, 4 Completed, 5 Cancelled
          val cleaningDate: String,       // ISO-8601
          val totalPrice: Double,
          val currencyId: String,
          val paymentType: Int,           // 1 Cash, 2 Card
          val rooms: Int,
          val bathrooms: Int,
          val customerAddress: OrderAddressDto? = null,
          val services: List<OrderServiceDto> = emptyList(),
          val packages: List<OrderPackageDto> = emptyList(),
          val assignedEmployeeId: String? = null,
      )

      enum class OrderStatus(val code: Int) {
          New(0), Pending(1), Confirmed(2), InProgress(3),
          Completed(4), Cancelled(5);
          companion object {
              fun fromCode(code: Int): OrderStatus =
                  values().firstOrNull { it.code == code } ?: New
          }
      }

      enum class PaymentMethod(val code: Int) {
          Cash(1), Card(2);
          companion object {
              fun fromCode(code: Int): PaymentMethod =
                  values().firstOrNull { it.code == code } ?: Cash
          }
      }

  - path: src/cleansia_customer_android/app/src/main/java/cz/cleansia/customer/core/orders/OrderApi.kt
    change: |
      interface OrderApi {
          @GET("api/Order/{id}")
          suspend fun getOrderById(@Path("id") id: String): Response<OrderDetailDto>
      }

      If the audit (TASK-SP1) lands on a non-standard route, update
      this path accordingly.

  - path: src/cleansia_customer_android/app/src/main/java/cz/cleansia/customer/core/orders/OrderModule.kt
    change: |
      @Module @InstallIn(SingletonComponent::class) object OrderModule {
          @Provides @Singleton
          fun provideOrderApi(@AuthRetrofit retrofit: Retrofit): OrderApi =
              retrofit.create(OrderApi::class.java)
      }

dependencies:
  - TASK-SP1
verification:
  - Syntactic only (no gradle wrapper in project)
  - Confirm route matches what TASK-SP1 landed on
```

### TASK-SP3: `OrderRepository` — current-order cache + refresh semantics

```yaml
task: OrderRepository mirrors UserRepository pattern; StateFlow-backed cache
id: TASK-SP3
type: feature
priority: high
specialist: mobile
app: customer-android
estimated_complexity: small
recommended_model: sonnet

context: |
  OrderRepository manages the "currently viewing" order plus a small
  keyed cache so navigating away and back doesn't always re-fetch.
  Mirrors UserRepository's pattern: StateFlow for current value,
  suspend refresh(id), snackbar on failure.

  Cache strategy: in-memory Map<String, OrderDetailDto>. First call
  for an id returns cached if present AND kicks a background refresh
  IF last-fetched is stale (> 60s). Force-refresh with refresh(id,
  force = true).

  Scope: @Singleton. Lives across the whole app so navigating
  BookingSuccess → OrderDetail reuses the same cached value if
  BookingSuccess prefetched it.

files_to_create:
  - path: src/cleansia_customer_android/app/src/main/java/cz/cleansia/customer/core/orders/OrderRepository.kt
    change: |
      @Singleton class OrderRepository @Inject constructor(
          private val api: OrderApi,
          private val snackbar: SnackbarController,
          @ApplicationContext private val appContext: Context,
      ) {
          private data class CacheEntry(val value: OrderDetailDto, val fetchedAt: Long)

          private val cache = mutableMapOf<String, CacheEntry>()
          private val mutex = Mutex()

          private val _currentOrder = MutableStateFlow<OrderDetailDto?>(null)
          val currentOrder: StateFlow<OrderDetailDto?> = _currentOrder.asStateFlow()

          private val _loading = MutableStateFlow(false)
          val loading: StateFlow<Boolean> = _loading.asStateFlow()

          private val _error = MutableStateFlow<String?>(null)
          val error: StateFlow<String?> = _error.asStateFlow()

          /** Pull the cached order (if any) for the given id. Synchronous. */
          fun peek(id: String): OrderDetailDto? = cache[id]?.value

          /** Clear the currently-viewing order. Call when leaving OrderDetail. */
          fun clearCurrent() {
              _currentOrder.value = null
              _error.value = null
          }

          /**
           * Fetch (or re-fetch) the order by id. Populates currentOrder
           * and the keyed cache. Returns null-on-success or a user-facing
           * error string on failure.
           * If cached and not forced and fresh enough (<60s), returns the
           * cached value instantly and skips the network call.
           */
          suspend fun refresh(id: String, force: Boolean = false): String? = mutex.withLock {
              val cached = cache[id]
              val now = System.currentTimeMillis()
              if (!force && cached != null && (now - cached.fetchedAt) < 60_000L) {
                  _currentOrder.value = cached.value
                  _error.value = null
                  return@withLock null
              }
              _loading.value = true
              _error.value = null
              try {
                  val response = runCatching { api.getOrderById(id) }.getOrNull()
                  if (response == null) {
                      val msg = appContext.getString(R.string.error_generic_network)
                      snackbar.showError(msg)
                      _error.value = msg
                      // Keep the last cached value visible if we had one.
                      if (cached != null) _currentOrder.value = cached.value
                      return@withLock msg
                  }
                  if (!response.isSuccessful) {
                      val msg = ApiErrorParser.parseToUserMessage(
                          appContext, response.errorBody(), response.code()
                      )
                      snackbar.showError(msg)
                      _error.value = msg
                      if (cached != null) _currentOrder.value = cached.value
                      return@withLock msg
                  }
                  val body = response.body()
                  if (body == null) {
                      val msg = appContext.getString(R.string.error_generic_network)
                      _error.value = msg
                      return@withLock msg
                  }
                  cache[id] = CacheEntry(body, now)
                  _currentOrder.value = body
                  _error.value = null
                  return@withLock null
              } finally {
                  _loading.value = false
              }
          }

          /**
           * Background-enrich an already-known order. Used by
           * BookingSuccessScreen after a successful submit to
           * silently upgrade the cached BookingResult with any
           * server-authoritative data (cleaner-assigned, etc).
           * On failure, swallows — no snackbar. UI falls back to
           * the cached BookingResult it already rendered.
           */
          suspend fun tryEnrich(id: String) {
              runCatching { api.getOrderById(id) }
                  .getOrNull()
                  ?.takeIf { it.isSuccessful }
                  ?.body()
                  ?.let { body ->
                      mutex.withLock {
                          cache[id] = CacheEntry(body, System.currentTimeMillis())
                          _currentOrder.value = body
                      }
                  }
          }
      }

  - path: src/cleansia_customer_android/app/src/main/java/cz/cleansia/customer/core/orders/OrderRepositoryEntryPoint.kt
    change: |
      @EntryPoint @InstallIn(SingletonComponent::class)
      interface OrderRepositoryEntryPoint {
          fun orderRepository(): OrderRepository
      }

      # Not strictly needed if everything consumes via @HiltViewModel
      # injection, but the BookingSuccessScreen composable may need
      # direct access from outside a ViewModel scope. Add if used,
      # else omit.

dependencies:
  - TASK-SP2
verification:
  - Syntactic only
  - Verify snackbar + error-state handling match UserRepository's style
```

### TASK-SP4: `OrderDetailViewModel` + real `OrderDetailScreen`

```yaml
task: Replace sampleOrder() mock; real VM-driven OrderDetailScreen with loading/error states
id: TASK-SP4
type: refactor
priority: high
specialist: mobile
app: customer-android
estimated_complexity: medium
recommended_model: sonnet

context: |
  Today OrderDetailScreen.kt has (around line 118):
    val order = sampleOrder()
  Everything rendered is fake. Completely rewire:
    - Screen takes `orderId: String` as a nav arg (currently it takes
      the full sample object or no arg at all; audit today's signature).
    - Screen uses hiltViewModel<OrderDetailViewModel>() which calls
      orderRepository.refresh(orderId) in init (or a LaunchedEffect
      keyed on orderId).
    - Render states: Loading (skeleton), Error (message + retry), Loaded.
    - Drop sampleOrder() entirely — keep the fixture renamed to
      previewOrder() and move it into the @Preview-only block so
      Compose previews still work without network.

  Nav wiring: add Routes.orderDetail(id) if not already present.

files_to_modify:
  - path: src/cleansia_customer_android/app/src/main/java/cz/cleansia/customer/features/orders/OrderDetailScreen.kt
    change: |
      Replace the existing signature with:
        @Composable
        fun OrderDetailScreen(
            orderId: String,
            onBack: () -> Unit,
            vm: OrderDetailViewModel = hiltViewModel(),
        ) {
            LaunchedEffect(orderId) { vm.load(orderId) }
            val order by vm.order.collectAsState()
            val loading by vm.loading.collectAsState()
            val error by vm.error.collectAsState()

            Scaffold(
                topBar = { /* keep existing top bar, pass onBack */ },
            ) { padding ->
                when {
                    loading && order == null -> OrderDetailSkeleton(Modifier.padding(padding))
                    error != null && order == null -> OrderDetailErrorState(
                        message = error!!,
                        onRetry = { vm.retry() },
                        modifier = Modifier.padding(padding),
                    )
                    order != null -> OrderDetailContent(
                        order = order!!,
                        modifier = Modifier.padding(padding),
                    )
                    else -> Box(Modifier.padding(padding))
                }
            }

            DisposableEffect(Unit) {
                onDispose { vm.onScreenLeft() }
            }
        }

      DELETE the `val order = sampleOrder()` line.
      Move sampleOrder() into the Previews section (see below).

      Rename the existing mock-driven body into `OrderDetailContent(
      order: OrderDetailDto, modifier: Modifier = Modifier)` and update
      every reference:
        - Price, services[], packages[], cleaningDate, confirmationCode
          all read from the real DTO.
        - OrderStatus.fromCode(order.status) drives any status chip.
        - PaymentMethod.fromCode(order.paymentType) drives the payment
          chip.
        - If order.assignedEmployeeId == null, show a "We're matching
          you with a cleaner" placeholder card; if non-null, show a
          "Cleaner assigned" card (with TBD name since SP1 may not
          surface cleaner name — show just the initial/avatar
          placeholder).

      Add the Previews block at the bottom:
        @Preview
        @Composable
        private fun OrderDetailPreview() {
            val sample = OrderDetailDto(
                id = "sample",
                confirmationCode = "CL-ABCD",
                status = OrderStatus.Confirmed.code,
                cleaningDate = "2026-04-30T10:00:00Z",
                totalPrice = 1499.0,
                currencyId = "CZK",
                paymentType = PaymentMethod.Card.code,
                rooms = 2,
                bathrooms = 1,
                customerAddress = OrderAddressDto("Hlavní 1", "Praha", "11000"),
                services = listOf(
                    OrderServiceDto("s1", "Home cleaning", 800.0),
                    OrderServiceDto("s2", "Windows", 300.0),
                ),
                packages = emptyList(),
                assignedEmployeeId = null,
            )
            CleansiaTheme {
                OrderDetailContent(order = sample)
            }
        }

        @Preview
        @Composable
        private fun OrderDetailLoadingPreview() {
            CleansiaTheme { OrderDetailSkeleton() }
        }

        @Preview
        @Composable
        private fun OrderDetailErrorPreview() {
            CleansiaTheme {
                OrderDetailErrorState(
                    message = "Couldn't load this order",
                    onRetry = {}
                )
            }
        }

  - path: src/cleansia_customer_android/app/src/main/java/cz/cleansia/customer/navigation/CleansiaNavHost.kt
    change: |
      Add (or update) the orderDetail route:
        composable(
            route = "orderDetail/{orderId}",
            arguments = listOf(navArgument("orderId") { type = NavType.StringType }),
        ) { backStackEntry ->
            val orderId = backStackEntry.arguments?.getString("orderId").orEmpty()
            OrderDetailScreen(
                orderId = orderId,
                onBack = { navController.popBackStack() },
            )
        }

      And in Routes (if there's a central helper):
        object Routes {
            fun orderDetail(id: String) = "orderDetail/$id"
            ...
        }

files_to_create:
  - path: src/cleansia_customer_android/app/src/main/java/cz/cleansia/customer/features/orders/OrderDetailViewModel.kt
    change: |
      @HiltViewModel class OrderDetailViewModel @Inject constructor(
          private val orderRepository: OrderRepository,
      ) : ViewModel() {
          private var currentId: String? = null

          val order: StateFlow<OrderDetailDto?> = orderRepository.currentOrder
          val loading: StateFlow<Boolean> = orderRepository.loading
          val error: StateFlow<String?> = orderRepository.error

          fun load(id: String) {
              currentId = id
              // If we already have a cached value, render it instantly;
              // refresh() takes care of the fresh/stale dance internally.
              viewModelScope.launch { orderRepository.refresh(id) }
          }

          fun retry() {
              val id = currentId ?: return
              viewModelScope.launch { orderRepository.refresh(id, force = true) }
          }

          fun onScreenLeft() {
              orderRepository.clearCurrent()
          }
      }

  - path: src/cleansia_customer_android/app/src/main/java/cz/cleansia/customer/features/orders/OrderDetailSkeleton.kt
    change: |
      @Composable
      fun OrderDetailSkeleton(modifier: Modifier = Modifier) {
          Column(modifier.fillMaxSize().padding(16.dp)) {
              // Use the existing shimmer / SkeletonBox primitive if one
              // exists in shared UI. If not, draw a handful of grey
              // boxes sized to the real content (header card, summary
              // card, timeline). Keep it simple.
          }
      }

  - path: src/cleansia_customer_android/app/src/main/java/cz/cleansia/customer/features/orders/OrderDetailErrorState.kt
    change: |
      @Composable
      fun OrderDetailErrorState(
          message: String,
          onRetry: () -> Unit,
          modifier: Modifier = Modifier,
      ) {
          Column(
              modifier.fillMaxSize().padding(24.dp),
              horizontalAlignment = Alignment.CenterHorizontally,
              verticalArrangement = Arrangement.Center,
          ) {
              Icon(Icons.Outlined.ErrorOutline, contentDescription = null)
              Spacer(Modifier.height(12.dp))
              Text(message, style = MaterialTheme.typography.bodyLarge)
              Spacer(Modifier.height(16.dp))
              Button(onClick = onRetry) {
                  Text(stringResource(R.string.common_retry))
              }
          }
      }

dependencies:
  - TASK-SP3
verification:
  - Navigate to OrderDetail via the new route with a real orderId → real data renders
  - Disable network, navigate again → error state shows with a working retry
  - While loading, skeleton renders (no blank flash)
  - Previews render in Android Studio without DI
```

---

## Phase 3 — Success screen enrichment

### TASK-SP5: `BookingSuccessScreen` rich content + `BookingResult` cache

```yaml
task: BookingSuccessScreen reads BookingViewModel.bookingResult; renders arrival window, summary, data-driven timeline; backgrounds an OrderRepository.tryEnrich(orderId)
id: TASK-SP5
type: feature
priority: high
specialist: mobile
app: customer-android
estimated_complexity: large
recommended_model: sonnet

context: |
  Today BookingSuccessScreen takes confirmationCode + orderId + nav
  callbacks. It renders a mascot + generic copy + a primary-outlined
  confirmation-code card (SelectionContainer wrapped for copy/paste)
  and a hardcoded timeline.

  Enrich it to show:
    1. Mascot + hero copy (keep).
    2. Confirmation code card (keep).
    3. NEW — Expected arrival window (e.g. "Wednesday, April 30
       between 10:00 and 12:00"). Source: BookingResult.cleaningDate
       (the Instant already stored on BookingState by the
       mobile-booking-submission spec). UI formats in user's locale
       and derives a +2h end from the start.
    4. NEW — Order summary card: services list (names), packages list
       (names), rooms/bathrooms, address line, total price (with
       currency), payment method chip (cash/card). All sourced from
       BookingResult.
    5. NEW — "What happens next" timeline — data-driven:
       - Base state: "We're matching a cleaner" (active),
         "They'll confirm within 2 hours" (pending),
         "You'll get a push notification" (pending).
       - If background enrich completes AND order.assignedEmployeeId
         != null: flip to "A cleaner has been assigned" (done),
         "They're prepping for your cleaning" (active),
         "You'll be notified when they're on the way" (pending).
       - If order.status == Cancelled (shouldn't happen on success,
         but defensive): hide timeline, show "This booking was
         cancelled" state. Edge case for back-stack re-entry.
    6. CTAs:
       - "Track this booking" → navigate to Routes.orderDetail(orderId)
       - "Book another cleaning" → navigate back to Home (pop booking stack)
       - "View all orders" → navigate to Orders tab

  Design decision (documented in spec header): BookingViewModel caches
  the full BookingResult (the submitted BookingState + the
  CreateOrderResponse). BookingSuccessScreen reads that synchronously
  — no network wait on the happy path. In parallel, it calls
  orderRepository.tryEnrich(orderId) to silently upgrade to
  server-authoritative state (cleaner-assigned etc.) when that
  resolves.

  If the user somehow lands on BookingSuccess via a deep link or
  back-stack restore where BookingViewModel.bookingResult is null
  (process death), we fall through to a skeleton and rely on
  orderRepository.refresh(orderId) to populate. In that edge case
  we're essentially re-using OrderDetailDto shape — map that back into
  the summary card. Acceptable: the screen still works, just flickers
  briefly.

files_to_modify:
  - path: src/cleansia_customer_android/app/src/main/java/cz/cleansia/customer/features/booking/BookingViewModel.kt
    change: |
      Add alongside the existing state:

        data class BookingResult(
            val orderId: String,
            val confirmationCode: String,
            val cleaningInstant: Instant,
            val totalPrice: Double,
            val currencyId: String,
            val paymentMethodCode: Int,   // 1 cash, 2 card
            val rooms: Int,
            val bathrooms: Int,
            val addressStreet: String,
            val addressCity: String,
            val addressZip: String,
            // Resolved snapshots from catalog at submit time:
            val services: List<BookingResultLineItem>,
            val packages: List<BookingResultLineItem>,
        )
        data class BookingResultLineItem(val id: String, val name: String, val price: Double)

        private val _bookingResult = MutableStateFlow<BookingResult?>(null)
        val bookingResult: StateFlow<BookingResult?> = _bookingResult.asStateFlow()

      Extend submit() to populate _bookingResult on success just before
      returning. Snapshot service/package names from the
      CatalogRepository at submit time (don't re-resolve later — the
      catalog might evolve). Pseudo:

        val services = catalogRepository.services.value
            .filter { it.id in s.selectedServiceIds }
            .map { BookingResultLineItem(it.id, it.translatedName(lang), it.basePrice) }
        val packages = catalogRepository.packages.value
            .filter { it.id in s.selectedPackageIds }
            .map { BookingResultLineItem(it.id, it.translatedName(lang), it.price) }
        _bookingResult.value = BookingResult(
            orderId = response.id,
            confirmationCode = response.confirmationCode,
            cleaningInstant = s.selectedInstant ?: Clock.System.now(),
            totalPrice = quoted.totalPrice,
            currencyId = quoted.currencyId,
            paymentMethodCode = if (s.paymentMethod == "card") 2 else 1,
            rooms = s.rooms,
            bathrooms = s.bathrooms,
            addressStreet = s.street,
            addressCity = s.city,
            addressZip = s.zipCode,
            services = services,
            packages = packages,
        )

      Inject CatalogRepository into BookingViewModel's constructor.

      Add a `fun clearBookingResult()` that nulls _bookingResult and
      calls update { BookingState() }. Call this when the user taps
      "Book another cleaning" on the success screen so next booking
      starts fresh.

  - path: src/cleansia_customer_android/app/src/main/java/cz/cleansia/customer/features/booking/BookingSuccessScreen.kt
    change: |
      New signature:
        @Composable
        fun BookingSuccessScreen(
            orderId: String,
            onTrackBooking: (String) -> Unit,
            onBookAnother: () -> Unit,
            onViewAllOrders: () -> Unit,
            bookingVm: BookingViewModel,            // scoped to the booking graph
            orderVm: OrderDetailViewModel = hiltViewModel(),
        ) { ... }

      Body (sketch):
        val result by bookingVm.bookingResult.collectAsState()
        val serverOrder by orderVm.order.collectAsState()

        LaunchedEffect(orderId) {
            // Background enrich — silent failure. tryEnrich doesn't
            // flip loading/error state; it upgrades currentOrder only
            // on success.
            orderVm.load(orderId)
        }

        Column(...) {
            MascotHero()                            // existing block
            ConfirmationCodeCard(code = result?.confirmationCode
                ?: serverOrder?.confirmationCode
                ?: "...")
            ArrivalWindowSection(
                instant = serverOrder?.cleaningDate?.let { Instant.parse(it) }
                    ?: result?.cleaningInstant,
            )
            OrderSummaryCard(
                services = serverOrder?.services?.map {
                    BookingResultLineItem(it.id, it.name, it.basePrice)
                } ?: result?.services.orEmpty(),
                packages = serverOrder?.packages?.map {
                    BookingResultLineItem(it.id, it.name, it.price)
                } ?: result?.packages.orEmpty(),
                rooms = serverOrder?.rooms ?: result?.rooms ?: 0,
                bathrooms = serverOrder?.bathrooms ?: result?.bathrooms ?: 0,
                address = serverOrder?.customerAddress?.let {
                    "${it.street}, ${it.city} ${it.zipCode}"
                } ?: result?.let {
                    "${it.addressStreet}, ${it.addressCity} ${it.addressZip}"
                },
                totalPrice = serverOrder?.totalPrice ?: result?.totalPrice ?: 0.0,
                currencyId = serverOrder?.currencyId ?: result?.currencyId ?: "CZK",
                paymentCode = serverOrder?.paymentType ?: result?.paymentMethodCode ?: 1,
            )
            WhatHappensNextTimeline(
                cleanerAssigned = serverOrder?.assignedEmployeeId != null,
                status = serverOrder?.status?.let(OrderStatus::fromCode),
            )
            CtaRow(
                onTrack = { onTrackBooking(orderId) },
                onBookAnother = {
                    bookingVm.clearBookingResult()
                    onBookAnother()
                },
                onViewAllOrders = onViewAllOrders,
            )
        }

      Keep the SelectionContainer around the confirmation code card for
      copy/paste.

      Extract subcomposables (ArrivalWindowSection, OrderSummaryCard,
      WhatHappensNextTimeline, CtaRow) as private top-level @Composables
      in this file. Each gets its own @Preview with fixture data.

  - path: src/cleansia_customer_android/app/src/main/java/cz/cleansia/customer/navigation/CleansiaNavHost.kt
    change: |
      Update the BookingSuccess route handler to pass the new
      callbacks:
        composable(
            route = "bookingSuccess/{orderId}/{code}",
            ...
        ) { entry ->
            val orderId = entry.arguments?.getString("orderId").orEmpty()
            val parentEntry = remember(entry) {
                navController.getBackStackEntry("bookingGraph")
            }
            val bookingVm: BookingViewModel = hiltViewModel(parentEntry)
            BookingSuccessScreen(
                orderId = orderId,
                onTrackBooking = { id -> navController.navigate(Routes.orderDetail(id)) },
                onBookAnother = { navController.popBackStack("home", inclusive = false) },
                onViewAllOrders = { navController.navigate(Routes.ordersTab) },
                bookingVm = bookingVm,
            )
        }

      The parentEntry / scoped ViewModel lookup is the tricky bit —
      it's how BookingViewModel survives the nav from Confirm → Success.
      If the app doesn't have a nested "bookingGraph" route today, add
      one. If that's too intrusive, fallback: scope BookingViewModel to
      the activity (@ActivityRetainedScoped instead of @HiltViewModel)
      — simpler but leakier. Prefer the nav graph scope.

files_to_create: []

dependencies:
  - TASK-SP3    # OrderRepository + tryEnrich
  - TASK-SP4    # OrderDetailViewModel (reused here)
  - TASK-SP6    # strings (can land in parallel; screen won't render without them)
verification:
  - Submit a booking end-to-end on a device → success screen shows
    arrival window, summary card, timeline, all from the cached
    BookingResult (no network flicker).
  - Disable network immediately after submit, land on success screen
    → content still renders from cache; timeline stays in "matching"
    state.
  - Place a test order where backend marks it Confirmed + assigns a
    cleaner (manual DB poke or partner-app pickup) → come back to
    success screen (or reopen), timeline flips to "cleaner assigned"
    copy. This validates tryEnrich wiring.
  - Tap "Track this booking" → navigates to OrderDetail with correct
    orderId.
  - Tap "Book another cleaning" → booking state is reset, user lands
    on Home.
```

### TASK-SP6: Strings across 5 locales

```yaml
task: Add translation keys for all new success + order-detail strings
id: TASK-SP6
type: content
priority: high
specialist: mobile
app: customer-android
estimated_complexity: small
recommended_model: sonnet

context: |
  Five locales: en (default), cs, sk, uk, ru. Customer Android app
  mirrors the 5-locale convention from the web.

  Strings to add (key → English source):
    booking_success_arrival_window    → "Expected arrival"
    booking_success_summary           → "Your booking"
    booking_success_services_heading  → "Services"
    booking_success_packages_heading  → "Packages"
    booking_success_rooms_label       → "%1$d rooms · %2$d bathrooms"
    booking_success_track             → "Track this booking"
    booking_success_book_another      → "Book another cleaning"
    booking_success_view_all          → "View all orders"
    booking_success_payment_cash      → "Cash"
    booking_success_payment_card      → "Card"
    booking_success_total_label       → "Total"
    booking_success_timeline_matching → "Matching you with a cleaner"
    booking_success_timeline_confirm  → "They'll confirm within 2 hours"
    booking_success_timeline_notify   → "You'll get a push notification"
    booking_success_timeline_assigned → "A cleaner has been assigned"
    booking_success_timeline_prepping → "They're prepping for your cleaning"
    booking_success_timeline_onway    → "You'll be notified when they're on the way"
    order_detail_loading              → "Loading order..."
    order_detail_error                → "Couldn't load this order"
    order_detail_status_new           → "Placed"
    order_detail_status_pending       → "Awaiting payment"
    order_detail_status_confirmed     → "Confirmed"
    order_detail_status_inprogress    → "In progress"
    order_detail_status_completed     → "Completed"
    order_detail_status_cancelled     → "Cancelled"
    order_detail_cleaner_tbd          → "We're matching you with a cleaner"
    common_retry                      → "Retry"

  Translations: content specialist / native-speaker review pass will
  iterate. Start with reasonable MT-ish drafts for cs/sk/uk/ru so the
  UI renders; final polish is a CONTENT-* follow-up if needed.

files_to_modify:
  - path: src/cleansia_customer_android/app/src/main/res/values/strings.xml
    change: Add the English source strings per the list above.

  - path: src/cleansia_customer_android/app/src/main/res/values-cs/strings.xml
    change: Czech translations (e.g. booking_success_track → "Sledovat rezervaci").

  - path: src/cleansia_customer_android/app/src/main/res/values-sk/strings.xml
    change: Slovak translations.

  - path: src/cleansia_customer_android/app/src/main/res/values-uk/strings.xml
    change: Ukrainian translations.

  - path: src/cleansia_customer_android/app/src/main/res/values-ru/strings.xml
    change: Russian translations.

files_to_create: []

dependencies: []
verification:
  - App switches languages (settings screen) → all new strings render
    in each locale; none fall back to the English default
  - No duplicate keys; no missing keys across locale files (diff them)
```

### TASK-SP7: Compose Previews for success + detail content

```yaml
task: Add / update @Preview functions with sample CreateOrderResponse and OrderDetailDto fixtures for design iteration
id: TASK-SP7
type: feature
priority: medium
specialist: mobile
app: customer-android
estimated_complexity: small
recommended_model: sonnet

context: |
  Designers iterate on Compose previews. Each major block in the new
  success screen (ArrivalWindowSection, OrderSummaryCard,
  WhatHappensNextTimeline, CtaRow) gets its own @Preview. OrderDetail
  block previews were already added in SP4 — this task extends them
  with more states.

  Fixtures go in a shared `features/orders/internal/Fixtures.kt` file
  (internal visibility, preview-only) so both the success and detail
  previews share one source of truth.

files_to_create:
  - path: src/cleansia_customer_android/app/src/main/java/cz/cleansia/customer/features/orders/internal/Fixtures.kt
    change: |
      internal val FixtureOrderMatching = OrderDetailDto(
          id = "sample-1",
          confirmationCode = "CL-ABCD",
          status = OrderStatus.Confirmed.code,
          cleaningDate = "2026-04-30T10:00:00Z",
          totalPrice = 1499.0,
          currencyId = "CZK",
          paymentType = PaymentMethod.Card.code,
          rooms = 2,
          bathrooms = 1,
          customerAddress = OrderAddressDto("Hlavní 1", "Praha", "11000"),
          services = listOf(
              OrderServiceDto("s1", "Home cleaning", 800.0),
              OrderServiceDto("s2", "Windows", 300.0),
          ),
          packages = emptyList(),
          assignedEmployeeId = null,     // matching state
      )

      internal val FixtureOrderAssigned = FixtureOrderMatching.copy(
          assignedEmployeeId = "cleaner-42",
          status = OrderStatus.Confirmed.code,
      )

      internal val FixtureOrderInProgress = FixtureOrderAssigned.copy(
          status = OrderStatus.InProgress.code,
      )

      internal val FixtureBookingResult = BookingViewModel.BookingResult(
          orderId = "sample-1",
          confirmationCode = "CL-ABCD",
          cleaningInstant = Instant.parse("2026-04-30T10:00:00Z"),
          totalPrice = 1499.0,
          currencyId = "CZK",
          paymentMethodCode = 2,
          rooms = 2,
          bathrooms = 1,
          addressStreet = "Hlavní 1",
          addressCity = "Praha",
          addressZip = "11000",
          services = listOf(
              BookingResultLineItem("s1", "Home cleaning", 800.0),
              BookingResultLineItem("s2", "Windows", 300.0),
          ),
          packages = emptyList(),
      )

files_to_modify:
  - path: src/cleansia_customer_android/app/src/main/java/cz/cleansia/customer/features/booking/BookingSuccessScreen.kt
    change: |
      Add @Preview blocks at the bottom of the file:
        @Preview
        @Composable
        private fun ArrivalWindowPreview() {
            CleansiaTheme {
                ArrivalWindowSection(Instant.parse("2026-04-30T10:00:00Z"))
            }
        }

        @Preview
        @Composable
        private fun OrderSummaryPreview() {
            CleansiaTheme {
                OrderSummaryCard(
                    services = FixtureBookingResult.services,
                    packages = FixtureBookingResult.packages,
                    rooms = 2, bathrooms = 1,
                    address = "Hlavní 1, Praha 11000",
                    totalPrice = 1499.0,
                    currencyId = "CZK",
                    paymentCode = 2,
                )
            }
        }

        @Preview
        @Composable
        private fun TimelineMatchingPreview() {
            CleansiaTheme {
                WhatHappensNextTimeline(cleanerAssigned = false, status = OrderStatus.Confirmed)
            }
        }

        @Preview
        @Composable
        private fun TimelineAssignedPreview() {
            CleansiaTheme {
                WhatHappensNextTimeline(cleanerAssigned = true, status = OrderStatus.Confirmed)
            }
        }

  - path: src/cleansia_customer_android/app/src/main/java/cz/cleansia/customer/features/orders/OrderDetailScreen.kt
    change: |
      Replace the in-file sampleOrder() fixture with imports of
      FixtureOrderMatching / FixtureOrderAssigned / FixtureOrderInProgress
      and add previews for each state plus the skeleton + error states
      (already in SP4 — confirm they're still wired).

dependencies:
  - TASK-SP4
  - TASK-SP5
verification:
  - Open each file in Android Studio → all @Preview annotations render
    without DI errors (fixtures are pure data — no Hilt required)
  - Designer can toggle between matching / assigned / in-progress
    timeline states visually
```

---

## Execution order

1. **TASK-SP1** — backend audit + minimal DTO extension. No deps.

**→ MANUAL STEP: verify DTO by hitting the customer endpoint locally**
```
curl -H "Authorization: Bearer $TOKEN" http://localhost:5003/api/Order/{orderId} | jq
```
Confirm the field list matches what's listed in TASK-SP1. Flag any gaps (e.g. cleaner-name, translations) as follow-up specs before SP2 starts.

**→ MANUAL STEP: no NSwag regen needed for mobile (hand-written), but if the same DTO will be consumed by web later:**
```
cd src/Cleansia.App && npm run generate-customer-client
```
Optional — skip if only the mobile app consumes this route today.

2. **TASK-SP2** — mobile OrderApi + DTOs. Depends on SP1.
3. **TASK-SP3** — OrderRepository. Depends on SP2.
4. **TASK-SP4** — OrderDetailViewModel + real OrderDetailScreen. Depends on SP3.
5. **TASK-SP6** — strings. Parallelizable with SP2/SP3/SP4 (no code deps).
6. **TASK-SP5** — BookingSuccessScreen enrichment + BookingViewModel BookingResult caching. Depends on SP3 + SP4 + SP6.
7. **TASK-SP7** — previews + fixtures. Depends on SP4 + SP5.

Parallelizable paths:
- SP6 (strings) runs in parallel with everything on the code side.
- SP2 + SP3 can be drafted together as a single PR if desired — they're tightly coupled.
- SP4 must land before SP5 because SP5 reuses OrderDetailViewModel.

Estimated tokens: ~70k total for the full flow (heaviest is SP5 — the success screen enrichment touches VM, nav, and multiple new composables).

---

## Out of scope (followup specs)

- **Guest confirmation-code lookup** — anonymous endpoint (POST /api/Order/FindByCode with {code, email|phone}) + a lightweight guest success screen that doesn't require sign-in. Needs a rate-limit layer and a careful think on what data to expose to non-authenticated callers.
- **Push notifications for cleaner-assigned / on-the-way** — FCM integration, server-side notification triggers, in-app handler. Biggest follow-up by far.
- **Cleaner preview on OrderDetail** — name, photo, rating. Requires a customer-scoped cleaner-summary endpoint (backend does NOT expose the Employee entity directly to customers today) + a mobile EmployeeSummaryDto + careful thought on what a customer is allowed to see pre- vs post-assignment.
- **Reschedule flow** — customer-initiated date/time change. Backend has some admin-side reschedule plumbing; customer-facing doesn't exist yet.
- **Cancel booking from the success screen** — backend already supports order cancellation; wire a CTA + confirmation dialog + cancellation-reason picker.
- **Tipping flow** — add a tip after the cleaner's work is Completed. Needs a backend tip model + Stripe integration.
- **Map preview of the address** — Mapbox static map render of customerAddress. Mobile already has Mapbox deps for the address-picker flow; reuse it.
- **Invoice / receipt download** — PDF surface for customer orders once EmployeeInvoice / eet-receipt artifacts become customer-visible.
- **Booking retry on submit failure with better error UX** — today's snackbar is minimal; a proper retry dialog with the server's error reason would help.
- **Live status refresh on OrderDetail** — today we refresh on entry + retry. A pull-to-refresh gesture + a websocket-or-polling live-status bar would be nicer for orders currently in-progress.
- **Order status change notifications in-app** — banner when a background enrich detects status transition (e.g. "Your cleaner just arrived").
