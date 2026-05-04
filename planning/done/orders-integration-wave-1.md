# Orders Integration — Wave 1 (list + detail + home recent + success polish)

**Status:** Ready for execution
**Depends on:** Booking submission (Phase 6 + follow-ups) complete. Orders can now be created; this spec makes them visible + readable post-creation.

## Why this exists

Today the mobile app renders hardcoded sample orders on:
- Orders tab (6 fake rows)
- Order detail screen (`sampleOrder()` default)
- Home tab recent-bookings section (2 hardcoded items)
- Booking success screen (shows the confirmation code only, no order-specific content)

Every other feature of the app is wired to real endpoints. Orders are the last visible mock in regular user flow (Rewards tab stays as-is per product decision — marketing placeholder).

This spec wires the real Orders API end-to-end. Scope is narrowly the **read path**: list, detail, recent-bookings, success enrichment. Order actions (cancel, review, report issue, download receipt, view photos) are **Wave 2** — a follow-up spec.

## Decisions in scope

1. **Native mobile feel, web parity on data.** UI mirrors the web orders screens in terms of WHICH fields to surface, but lays them out in a mobile-idiomatic way (swipeable status chip, stacked cards, pull-to-refresh instead of paginator, status timeline as a simpler vertical list).
2. **Pagination — infinite-scroll, not a pager.** The web `<p-paginator>` doesn't fit mobile. Use LazyColumn + `onAppendPage` when the list scrolls near the end. Default page size 20.
3. **Client-side status filter chips** — like the booking category filter. "All", "Upcoming", "Completed", "Cancelled" tabs. No backend filter call; derive from the in-memory list. Upgrade to backend `orderStatuses` query param only when the list grows large enough that filtering client-side is a problem.
4. **Rewards tab stays mocked.** Marketing placeholder by owner decision.
5. **Order detail loads fresh on entry.** No long-lived cache — users expect up-to-date status. Cache the last-loaded order for instant-render on tab-back, but fire a fresh refresh in the background.
6. **Success screen enrichment** — on the happy path, pass the order id through nav args + fetch the order detail in the background. Display arrival window / assigned cleaner status if populated. Graceful fallback to confirmation code if fetch fails.
7. **Status pill colors** — match web's severity mapping:
   - `New / Pending` → amber
   - `Confirmed / InProgress` → blue
   - `Completed` → green
   - `Cancelled` → red

## Out of scope — Wave 2+

- Cancel / Review / Report-issue / Download-receipt / View-photos actions
- Disputes screens
- Push notifications when order status changes
- Real-time status updates (polling or websockets)
- Deep links back from receipts / emails

## Phase 1 — Data layer

### TASK-OR1: `OrderApi` + DTOs

```yaml
task: Hand-written Retrofit interface for order-read endpoints
id: TASK-OR1
type: feature
priority: high
specialist: mobile
app: customer-android
estimated_complexity: small
recommended_model: sonnet

context: |
  Mirrors the SavedAddressApi pattern — file under
  core/orders/OrderApi.kt with @AuthRetrofit. Accompanying DTO file
  at core/orders/OrderDtos.kt with kotlinx.serialization classes for
  the wire shapes. Only the read endpoints in Wave 1; action
  endpoints land in Wave 2.

files_to_create:
  - path: src/cleansia_customer_android/app/src/main/java/cz/cleansia/customer/core/orders/OrderDtos.kt
    change: |
      @Serializable data classes:

        OrderListItemDto — mirrors backend OrderListItem shape:
          id, displayOrderNumber, customerAddress (single-line string),
          cleaningDateTime (String ISO-8601 — parse to Instant at UI),
          rooms, bathrooms, totalPrice, estimatedTime,
          orderStatus: CodeDto, paymentStatus: CodeDto, paymentType: CodeDto,
          confirmationCode,
          selectedServices: List<OrderServiceSummaryDto>? (id, name, estimatedTime),
          selectedPackages: List<OrderPackageSummaryDto>? (id, name, price, estimatedTime),
          currency: CurrencyRefDto? { code: String },
          assignedEmployees: List<String>? (ids, informational only)

        OrderDetailDto — mirrors backend OrderItem:
          everything from list-item PLUS:
          address: OrderAddressDto? { street, city, zipCode },
          specialInstructions, accessInstructions, notes,
          statusHistory: List<OrderStatusTrackDto>? { status: CodeDto, createdOn: String },
          assignedEmployees: List<AssignedEmployeeDto>? { name, email, phone, rating },
          review: OrderReviewDto? { rating: Int, comment: String?, createdOn: String },
          receiptNumber: String?,
          actualCompletionTime: Int?, completionNotes: String?

        CodeDto { value: Int?, name: String? } — reused from backend's Code record

        OrderListResponseDto { data: List<OrderListItemDto>, totalRecords: Int, offset: Int, limit: Int }
          — wraps the /GetMyOrders paged response

      Match backend property names exactly (camelCase via default System.Text.Json).

  - path: src/cleansia_customer_android/app/src/main/java/cz/cleansia/customer/core/orders/OrderApi.kt
    change: |
      interface OrderApi {
          @GET("api/Order/GetMyOrders")
          suspend fun getMyOrders(
              @Query("offset") offset: Int = 0,
              @Query("limit") limit: Int = 20,
          ): Response<OrderListResponseDto>

          @GET("api/Order/{id}")
          suspend fun getById(@Path("id") id: String): Response<OrderDetailDto>
      }

      Note: backend's GetMyOrders may accept more filter params (cleaningDateFrom,
      paymentStatuses, etc.) — skip them for MVP. Add if/when we need server-side
      filtering.

      Verify the "GetById" route. Web uses `getById(orderId)` → base route
      api/Order/{id}. If the customer backend uses a different path (e.g.
      api/Order/GetById/{id}), update to match. Check
      c:/Users/cmisa/Desktop/Mike/Projects/cleansia/src/Cleansia.Web.Customer/Controllers/OrderController.cs
      during execution.

  - path: src/cleansia_customer_android/app/src/main/java/cz/cleansia/customer/core/orders/OrderModule.kt
    change: |
      @Module @InstallIn(SingletonComponent::class) object OrderModule {
          @Provides @Singleton
          fun provideOrderApi(@AuthRetrofit retrofit: Retrofit): OrderApi =
              retrofit.create(OrderApi::class.java)
      }

dependencies: []
verification:
  - Syntactic check (no gradle wrapper in this project)
  - Route strings match backend controller exactly (grep OrderController.cs)
```

### TASK-OR2: `OrderRepository`

```yaml
task: Repository with list cache + single-order fetch
id: TASK-OR2
type: feature
priority: high
specialist: mobile
app: customer-android
estimated_complexity: medium
recommended_model: sonnet

context: |
  Pattern: @Singleton + StateFlow. Mirrors CatalogRepository / UserRepository
  error handling (String? return, snackbar on failure, ApiErrorParser for
  ProblemDetails).

  Two separate concerns:
    1. List — paginated. Exposes `orders: StateFlow<List<OrderListItemDto>>`
       and `totalRecords: StateFlow<Int>` and `loadingMore: StateFlow<Boolean>`.
       refresh() loads page 0; loadNextPage() appends.
       Drops the cache on sign-out (same as AddressRepository pattern).
    2. Detail — single-shot. getById(id) fetches fresh every call and
       returns the DTO (or null on failure). Called site caches in its VM.
       No repo-side detail cache for MVP — keeps invalidation trivial.

files_to_create:
  - path: src/cleansia_customer_android/app/src/main/java/cz/cleansia/customer/core/orders/OrderRepository.kt
    change: |
      @Singleton class OrderRepository @Inject constructor(
          private val api: OrderApi,
          private val snackbar: SnackbarController,
          @ApplicationContext private val appContext: Context,
      ) {
          private val _orders = MutableStateFlow<List<OrderListItemDto>>(emptyList())
          private val _totalRecords = MutableStateFlow(0)
          private val _loading = MutableStateFlow(false)
          private val _loadingMore = MutableStateFlow(false)
          private val _loaded = MutableStateFlow(false)

          val orders: StateFlow<List<OrderListItemDto>> = _orders.asStateFlow()
          val totalRecords: StateFlow<Int> = _totalRecords.asStateFlow()
          val loading: StateFlow<Boolean> = _loading.asStateFlow()
          val loadingMore: StateFlow<Boolean> = _loadingMore.asStateFlow()
          val loaded: StateFlow<Boolean> = _loaded.asStateFlow()

          private val pageSize = 20

          suspend fun refresh(): String? { /* offset=0, replaces _orders */ }
          suspend fun loadNextPage(): String? { /* offset = _orders.size; appends */ }
          suspend fun getById(id: String): OrderDetailDto? { /* snackbar on fail, returns null */ }
          suspend fun clear() { /* reset all state — called on sign-out */ }
      }

      Network-error catch returns error_generic_network; 4xx uses ApiErrorParser.

files_to_modify:
  - path: src/cleansia_customer_android/app/src/main/java/cz/cleansia/customer/core/auth/AuthAuthenticator.kt
    change: |
      Add `orderRepositoryProvider: Provider<OrderRepository>` to constructor (lazy, breaks the
      SavedAddressApi-style DI cycle). On forced sign-out branches, call
      `runBlocking { orderRepositoryProvider.get().clear() }` alongside the existing
      addressRepositoryProvider clear.

  - path: src/cleansia_customer_android/app/src/main/java/cz/cleansia/customer/core/auth/AuthModule.kt
    change: |
      Extend provideAuthAuthenticator to accept Provider<OrderRepository> and pass through.

  - path: src/cleansia_customer_android/app/src/main/java/cz/cleansia/customer/core/auth/AuthRepository.kt
    change: |
      Add `orderRepository: OrderRepository` to ctor; call orderRepository.clear() in
      logout() — same pattern as addressRepository.clear().

  - path: src/cleansia_customer_android/app/src/main/java/cz/cleansia/customer/core/user/UserRepository.kt
    change: |
      Add orderRepository.clear() to deleteAccount() flow so account-deletion wipes
      orders cache too. (Same pattern as addressRepository.clear().)

dependencies:
  - TASK-OR1
verification:
  - Syntactic check
  - After sign-out, next sign-in starts with empty _orders
```

### TASK-OR3: `OrderRepositoryEntryPoint`

```yaml
task: Hilt EntryPoint for non-VM composable access
id: TASK-OR3
type: feature
priority: high
specialist: mobile
app: customer-android
estimated_complexity: small
recommended_model: sonnet

context: |
  Same pattern as AddressRepositoryEntryPoint / CatalogRepositoryEntryPoint.
  MainShell's Home tab needs to read recent orders without going through a VM,
  because Home is a non-VM composable that uses EntryPoint access for every
  repo-backed piece of content.

files_to_create:
  - path: src/cleansia_customer_android/app/src/main/java/cz/cleansia/customer/core/orders/OrderRepositoryEntryPoint.kt
    change: |
      @EntryPoint @InstallIn(SingletonComponent::class)
      interface OrderRepositoryEntryPoint {
          fun orderRepository(): OrderRepository
      }

dependencies:
  - TASK-OR2
```

## Phase 2 — Orders tab (list screen)

### TASK-OR4: Rewrite `OrdersTab` to real data

```yaml
task: OrdersTab reads from OrderRepository; delete mock
id: TASK-OR4
type: refactor
priority: high
specialist: mobile
app: customer-android
estimated_complexity: large
recommended_model: sonnet

context: |
  Delete sampleOrders() and the mock OrderListItem data class. Replace with
  OrderListItemDto-driven rendering. Keep the existing visual style (cards
  with left status-bar, status badge, date, address, price) but drive
  everything from the DTO.

  Visual additions (mobile parity with web):
    - Status filter chip row at top: All / Upcoming / Completed / Cancelled
    - Stats strip above list (optional, minor): total orders count + upcoming count
    - Pull-to-refresh (Compose's PullToRefresh)
    - Infinite scroll (LazyColumn + LaunchedEffect on scroll position)
    - Empty state: mascot + "No bookings yet" + "Book your first cleaning" CTA
    - Loading state: 3 skeleton rows using existing shimmer if available, else
      simple placeholder boxes
    - Error state: "Couldn't load your bookings" + retry button

  Row card content (from DTO):
    - Left border color — derive from orderStatus.value using the same
      severity mapping as web (amber/blue/green/red).
    - Header row: `#{displayOrderNumber}` + status pill
    - Date/time: parse cleaningDateTime, render "Apr 22 · 10:00" (locale-aware
      via java.time.format with current Configuration.locale)
    - Address: single line, icon prefix, ellipsis overflow
    - Services summary: "General Cleaning + Bathroom Cleaning" (join up to 2
      service names; add "+ N more" if >2). Uses selectedServices[].name.
    - Price: totalPrice formatted with currency.code ("1 299 CZK" — reuse
      the existing formatQuotedTotal helper or port a new one to
      core/format/CurrencyFormatter.kt).
    - Payment status icon (optional): small paid/pending indicator on the right

  Filter tabs logic:
    - "All" = no filter
    - "Upcoming" = orderStatus.value in [New, Pending, Confirmed, InProgress]
      (values 0-3)
    - "Completed" = orderStatus.value == 4
    - "Cancelled" = orderStatus.value == 5
    - Derive `val filtered = orders.filter { ... }`. No network round-trip.

  Tap behavior: row tap → navigate to OrderDetail(id).

files_to_modify:
  - path: src/cleansia_customer_android/app/src/main/java/cz/cleansia/customer/features/orders/OrdersTab.kt
    change: |
      Full rewrite of the tab composable. Resolve OrderRepository via
      EntryPoint. collectAsState on orders / loading / loaded / loadingMore.
      LaunchedEffect(Unit) triggers refresh() on first compose if not loaded.

      Delete:
        - sampleOrders() function
        - Any mock-specific data classes (local OrderListItem, OrderStatus enum if
          it's purely mock — check whether backend mirrors it via orderStatus.value)

      Keep:
        - The card + pill visual style; adapt to new data
        - The Compose layout (LazyColumn)

      New composables:
        - OrdersStatusFilterRow (chip row, mirrors CategoryChip aesthetic)
        - OrderCard(item: OrderListItemDto, onClick, modifier)
        - OrdersEmpty, OrdersError, OrdersLoading
      New helpers:
        - val LocalCurrencyFormatter = compositionLocalOf {...} OR a top-level
          `formatOrderPrice(amount, currencyCode)` function mirroring the
          booking bottom-sheet's formatQuotedTotal.
        - `formatOrderDateTime(instant: Instant): String` — e.g. "Apr 22 · 10:00"
        - `statusSeverityColor(statusValue: Int): Color` — in MaterialTheme tokens

      Tap routing:
        - OrdersTab signature gains `onOrderClick: (orderId: String) -> Unit = {}`
          (if not already there — check MainShell wiring). MainShell forwards to
          NavHost.

files_to_create:
  - path: src/cleansia_customer_android/app/src/main/java/cz/cleansia/customer/core/format/OrderFormatters.kt
    change: |
      Extracted shared helpers (used by OrdersTab, OrderDetailScreen, HomeTab):
        fun formatOrderDateTime(iso: String): String
        fun formatOrderDateRange(iso: String, estimatedMinutes: Int): String
          → "Apr 22 · 10:00–12:00"
        fun formatOrderPrice(amount: Double, currencyCode: String?): String
          → "1 299 Kč" (localized thousands separator; fall back to CZK if null)
        @Composable fun statusColor(statusValue: Int?): Color
          → reads MaterialTheme colorScheme; returns amber/blue/green/red/outline

dependencies:
  - TASK-OR3
verification:
  - Orders tab shows real bookings from backend
  - Pull-to-refresh works
  - Status filter chips work client-side
  - Tap navigates to OrderDetail
  - Empty state shows when user has zero orders
  - Error snackbar fires on network failure
```

## Phase 3 — Order detail screen

### TASK-OR5: Rewrite `OrderDetailScreen` + add VM

```yaml
task: Real data in OrderDetailScreen via OrderDetailViewModel
id: TASK-OR5
type: refactor
priority: high
specialist: mobile
app: customer-android
estimated_complexity: large
recommended_model: sonnet

context: |
  Today the screen takes an `order: OrderDetail = sampleOrder()` default.
  Rewrite to take `orderId: String` via nav args + render from VM-fetched
  OrderDetailDto.

  Sections (mobile-native layout):
    1. Top app bar with back arrow, title "Order #{displayOrderNumber}"
    2. Hero card — status pill + confirmation code + created-on timestamp,
       prominent.
    3. Cleaning details — date/time (as arrival-window style), rooms,
       bathrooms, estimated duration.
    4. Address card — street / city zipCode / icon.
    5. Services list — name + estimated time per service (no price — aggregate
       shown in Total).
    6. Packages list — name + price per package.
    7. Price breakdown — Total prominent, services subtotal + packages
       subtotal as sub-rows if both present.
    8. Special instructions / notes — shown only if non-empty.
    9. Status timeline — vertical list: createdOn per status entry, current
       status highlighted.
    10. Assigned cleaners — name + rating stars per entry; "Not yet assigned"
        placeholder when empty.
    11. Review section — conditional: if orderStatus.value == 4 (Completed):
        show existing review if present, OTHERWISE show "Rate this cleaning"
        CTA that leads to a Wave 2 review form (placeholder button for now —
        disabled with TODO, or deep-link to a future route).

  Conditional rendering guards:
    - Review section: orderStatus.value == 4
    - Status timeline: statusHistory non-empty
    - Assigned cleaners: assignedEmployees non-empty (otherwise placeholder)
    - Notes: specialInstructions/notes non-empty
    - Confirmation code: always visible

  Actions footer (Wave 1): none exposed yet. Cancel/Report/Receipt all
  land in Wave 2 spec. Leave room in the layout for the action bar.

files_to_modify:
  - path: src/cleansia_customer_android/app/src/main/java/cz/cleansia/customer/features/orders/OrderDetailScreen.kt
    change: |
      Full rewrite. New signature:

        @Composable
        fun OrderDetailScreen(
            orderId: String,
            onBack: () -> Unit,
        )

      Resolves OrderDetailViewModel via hiltViewModel(), which fetches on init
      keyed on orderId. Collects `order: StateFlow<OrderDetailDto?>` + `loading`
      + `error: StateFlow<String?>`.

      Delete the OrderDetail + sampleOrder() + AssignedCleaner + TimelineEntry +
      OrderReview mock data classes — replaced by DTO.

      Use Wave-1 fields only; leave the Wave-2 actions to a later spec.

files_to_create:
  - path: src/cleansia_customer_android/app/src/main/java/cz/cleansia/customer/features/orders/OrderDetailViewModel.kt
    change: |
      @HiltViewModel class OrderDetailViewModel @Inject constructor(
          private val orderRepository: OrderRepository,
          savedStateHandle: SavedStateHandle,
      ) : ViewModel() {
          private val orderId: String = savedStateHandle["orderId"] ?: error("orderId required")

          private val _order = MutableStateFlow<OrderDetailDto?>(null)
          val order: StateFlow<OrderDetailDto?> = _order.asStateFlow()

          private val _loading = MutableStateFlow(false)
          val loading: StateFlow<Boolean> = _loading.asStateFlow()

          init { refresh() }

          fun refresh() {
              if (_loading.value) return
              _loading.value = true
              viewModelScope.launch {
                  _order.value = orderRepository.getById(orderId)
                  _loading.value = false
              }
          }
      }

dependencies:
  - TASK-OR2
verification:
  - Open an order from Orders tab → see real data
  - Pull-to-refresh (if added — optional) re-fetches
  - Status pill color matches status
  - Review section conditional works (completed order only)
  - Loading skeletons + error state render correctly
```

## Phase 4 — Home tab integration

### TASK-OR6: Home tab recent bookings

```yaml
task: Replace mockRecent with OrderRepository's 2 most recent orders
id: TASK-OR6
type: refactor
priority: medium
specialist: mobile
app: customer-android
estimated_complexity: small
recommended_model: sonnet

context: |
  HomeTab.kt has a "Recent bookings" section around line 80 using mockRecent.
  Replace with the first 2 entries from orderRepository.orders (already
  ordered by backend — check — most recent first).

  Also the Milestone progress (lines 532–534) uses `val current = 3`. Derive
  from completed-orders count: orderRepository.orders.count { it.orderStatus.value == 4 }.

  Trigger a refresh() on first compose if loaded is false — same gate pattern
  as profile/address/catalog in MainShell.

files_to_modify:
  - path: src/cleansia_customer_android/app/src/main/java/cz/cleansia/customer/features/home/HomeTab.kt
    change: |
      1. Resolve OrderRepository via EntryPoint (top of the composable).
      2. Collect orders as state.
      3. Replace `val mockRecent = listOf(...)` (around line 80) with
         `val recent = orders.take(2)`. Empty state: section collapses.
      4. Replace milestone current (line 532) with
         `val current = orders.count { it.orderStatus.value == 4 }`.
      5. Each recent row tappable — invokes the existing `onOrderClick(id)`
         callback wired up by MainShell.

  - path: src/cleansia_customer_android/app/src/main/java/cz/cleansia/customer/features/main/MainShell.kt
    change: |
      Extend existing LaunchedEffect(Unit) parallel prefetch: also kick off
      orderRepository.refresh() if not loaded, mirroring the existing profile
      + address + catalog prefetch pattern.

dependencies:
  - TASK-OR4 (shares OrderCard / formatters)
verification:
  - Home tab shows real recent orders
  - Milestone counter reflects actual completed orders
  - Tap a recent row → order detail
```

## Phase 5 — Booking success enrichment

### TASK-OR7: BookingSuccessScreen fetches real order

```yaml
task: Show arrival window + cleaner-assigned status on success screen
id: TASK-OR7
type: feature
priority: medium
specialist: mobile
app: customer-android
estimated_complexity: small
recommended_model: sonnet

context: |
  BookingSuccess currently receives confirmationCode + orderId via nav args
  and renders a static timeline. Enrich by fetching the real order in the
  background and showing:
    - Arrival window (formatted from cleaningDateTime + estimatedTime)
    - Cleaner-assigned state — "We're matching a cleaner" while empty,
      cleaner's name + rating once populated.

  Graceful fallback: if the fetch fails/404s (brand-new order may have a
  slight backend lag before it's queryable), keep the existing static
  content. Don't block the success animation on the fetch.

files_to_modify:
  - path: src/cleansia_customer_android/app/src/main/java/cz/cleansia/customer/features/booking/BookingSuccessScreen.kt
    change: |
      Add OrderDetailViewModel (hilt-injected) — same VM the OrderDetail screen
      uses, same orderId nav arg. Collect state. When order.value != null and
      cleaningDateTime parses, display arrival window. When
      assignedEmployees?.isNotEmpty() == true, display cleaner card. Else
      placeholders.

  - path: src/cleansia_customer_android/app/src/main/java/cz/cleansia/customer/navigation/CleansiaNavHost.kt
    change: |
      Ensure Routes.BookingSuccess passes `orderId` into the OrderDetailViewModel
      via SavedStateHandle. Route string is already `booking/success/{confirmationCode}/{orderId}`
      — the VM reads orderId from the arg. Might need to make the VM read a
      different arg name; reconcile to "orderId".

dependencies:
  - TASK-OR5
verification:
  - Complete a booking → lands on success screen → within ~1-2s, arrival
    window appears. Cleaner-assigned section shows placeholder until
    backend dispatches one.
```

## Phase 6 — i18n + polish

### TASK-OR8: i18n strings

```yaml
task: New strings for orders flows
id: TASK-OR8
type: content
priority: medium
specialist: mobile
app: customer-android
estimated_complexity: small
recommended_model: haiku

context: |
  Reuse existing keys where possible. New strings (EN + CS, rest fall back):

files_to_modify:
  - path: src/cleansia_customer_android/app/src/main/res/values/strings.xml
    change: |
      Add under an "Orders" block:
        orders_title = "Your bookings"
        orders_empty_title = "No bookings yet"
        orders_empty_subtitle = "Your first cleaning is one tap away."
        orders_empty_cta = "Book your first cleaning"
        orders_error_title = "Couldn't load your bookings"
        orders_retry = "Retry"
        orders_filter_all = "All"
        orders_filter_upcoming = "Upcoming"
        orders_filter_completed = "Completed"
        orders_filter_cancelled = "Cancelled"
        orders_services_more = "+ %1$d more"
        order_detail_title = "Order #%1$s"
        order_detail_services = "Services"
        order_detail_packages = "Packages"
        order_detail_special_instructions = "Special instructions"
        order_detail_cleaning_date = "Cleaning date"
        order_detail_rooms = "Rooms"
        order_detail_bathrooms = "Bathrooms"
        order_detail_duration = "Duration"
        order_detail_total = "Total"
        order_detail_cleaner_assigning = "We're matching a cleaner for you"
        order_detail_cleaner_assigned = "Your cleaner"
        order_detail_review_completed = "Rate this cleaning"
        order_detail_review_thanks = "Thanks for your review"
        order_detail_loading = "Loading order…"
        order_detail_error = "Couldn't load this order"
        booking_success_arrival_window = "Expected arrival"
        booking_success_cleaner_assigning = "We're matching a cleaner"

      Status pill labels — reuse backend's statusCode.name if possible
      (it's already localized server-side). Otherwise hardcode en/cs
      mappings for the 6 OrderStatus enum values.

  - path: src/cleansia_customer_android/app/src/main/res/values-cs/strings.xml
    change: Same keys, Czech translations.

dependencies: []
verification:
  - All new UI text is localized
```

---

## Execution order

Wave 1 runs as four sequential phases. Nothing in later phases parallelizes cleanly — most of it builds on the repo + VM from the previous phase.

1. **Phase 1**: OR1 → OR2 → OR3 (data layer). Sequential.
2. **Phase 2**: OR4 (Orders tab).
3. **Phase 3**: OR5 (Order detail).
4. **Phase 4**: OR6 (Home tab) — parallelizable with Phase 5.
5. **Phase 5**: OR7 (Booking success polish).
6. **Phase 6**: OR8 (i18n — can start early, merge last).

OR6 + OR7 can execute in parallel once OR5 lands. OR8 can start any time but should land last so the other tasks see the strings.

Estimated tokens: ~90k total. The list + detail rewrites (OR4 + OR5) are the bulk.

---

## Open questions / flags for execution

1. **`GetMyOrders` response shape** — we assumed backend returns a paged `{ data, totalRecords, offset, limit }` wrapper. The NSwag web client uses `getPaged()`, which suggests that's the shape. Confirm by fetching the live swagger during OR1.

2. **Order status enum values** — backend is `New=0 / Pending=1 / Confirmed=2 / InProgress=3 / Completed=4 / Cancelled=5`. Web mobile code should key on `orderStatus.value` (the int), not `orderStatus.name` (the string), for filter logic.

3. **Timezone handling for dates** — backend sends `cleaningDateTime` as ISO-8601 UTC. Mobile must format in the device's local timezone for user-facing display. Use `kotlinx.datetime` for parsing + `TimeZone.currentSystemDefault()`.

4. **Currency code** — backend DTOs include `currency.code`. Mobile's price formatter should respect it (not hardcode CZK). The booking flow currently hardcodes "Kč"; harmonize via shared formatter in OrderFormatters.kt.

5. **"Pull to refresh" gesture on OrdersTab** — Compose Material3 has `PullToRefreshBox` (available in the version the app uses — check). If not, use Accompanist's deprecated version or skip for now and let the "Retry" button handle it.

## Wave 2 preview (for scope context, not this spec)

- Cancel order (with 24h policy warning)
- Submit review (stars + comment, completed-order only)
- Report issue (feeds Disputes backend)
- Download receipt (PDF intent)
- View order photos (before/after gallery)
