# Address Domain Unification — Phase B (Customer Android)

**Status:** Ready for execution
**Depends on:** Phase A (complete) + fresh mobile OpenAPI dump

## Precondition — regenerate the mobile OpenAPI spec

Your frontend NSwag regen refreshed the TypeScript client, but the mobile app reads from a SEPARATE file — `src/cleansia_customer_android/openapi/customer-api.json` — that I dumped manually. As of this spec, that file is still pre-Phase-A (no `latitude`/`longitude`, no `Update` endpoint, no `savedAddressId` on CreateOrder).

**Before starting Phase B:**
1. Run the customer API locally: `dotnet run --project src/Cleansia.Web.Customer`
2. Dump the fresh spec:
   ```
   curl http://localhost:5003/swagger/v1/swagger.json > src/cleansia_customer_android/openapi/customer-api.json
   ```
3. Confirm the dump contains `latitude`/`longitude` fields on `SavedAddressDto` and `AddSavedAddress_Command`, plus a `/api/SavedAddress/Update` endpoint.

**Decision — hand-written vs OpenAPI-generated:** The mobile OpenAPI Generator hook is currently **disabled** in `build.gradle.kts` (line 154, commented). Auth + User endpoints use hand-written Retrofit (`AuthApi`, `UserApi`). For consistency and because `SavedAddress` is a small, stable API, this spec **hand-writes** the SavedAddress Retrofit interface (same pattern as `UserApi.kt`) rather than enabling the code generator. When you eventually flip the generator on (Phase 6 expansion), the hand-written one gets deleted in favour of generated code.

---

## Task Specs

### TASK-B1: Add `SavedAddress` fields to DTO layer + hand-write API

```yaml
task: Add SavedAddressDto + AddSavedAddress/UpdateSavedAddress commands to mobile user layer
id: TASK-B1
type: feature
priority: high
specialist: mobile
app: customer-android
estimated_complexity: small
recommended_model: sonnet

context: |
  Mobile currently stores addresses in DataStore only. To sync with the
  backend, we need Kotlin wire DTOs mirroring the new Phase A shapes.
  Place them alongside UserDto (core/user/UserDto.kt) since they
  travel with the authenticated client — NOT in core/data/ where the
  local UserAddress lives (that's the UI model; keep them separate).

files_to_create:
  - path: src/cleansia_customer_android/app/src/main/java/cz/cleansia/customer/core/user/SavedAddressDto.kt
    change: |
      @Serializable data class SavedAddressDto with these fields:
        id: String
        label: String
        street: String
        city: String
        zipCode: String
        state: String? = null
        countryId: String
        country: String? = null
        latitude: Double? = null
        longitude: Double? = null
        isDefault: Boolean = false

      @Serializable data class AddSavedAddressCommand(
        label, street, city, zipCode,
        state: String? = null, countryId: String? = null,
        setAsDefault: Boolean,
        latitude: Double? = null, longitude: Double? = null,
        userId: String = "" // server overrides from JWT; we send empty
      )

      @Serializable data class UpdateSavedAddressCommand(
        savedAddressId, label, street, city, zipCode,
        state: String? = null, countryId: String? = null,
        latitude: Double? = null, longitude: Double? = null,
        userId: String = ""
      )

      @Serializable data class SetDefaultSavedAddressCommand(
        savedAddressId: String,
        userId: String = ""
      )

files_to_modify: []
dependencies: []
verification:
  - Compile: `./gradlew :app:compileDebugKotlin`
```

### TASK-B2: Hand-write `SavedAddressApi` Retrofit interface

```yaml
task: Add SavedAddressApi with 5 endpoints
id: TASK-B2
type: feature
priority: high
specialist: mobile
app: customer-android
estimated_complexity: small
recommended_model: sonnet

context: |
  Hand-written Retrofit interface mirroring the backend endpoints.
  Uses @AuthRetrofit so 401 → refresh is automatic.

files_to_create:
  - path: src/cleansia_customer_android/app/src/main/java/cz/cleansia/customer/core/user/SavedAddressApi.kt
    change: |
      interface SavedAddressApi {
          @GET("api/SavedAddress/GetMine")
          suspend fun getMine(): Response<List<SavedAddressDto>>

          @POST("api/SavedAddress/Add")
          suspend fun add(@Body command: AddSavedAddressCommand): Response<SavedAddressDto>

          @PUT("api/SavedAddress/Update")
          suspend fun update(@Body command: UpdateSavedAddressCommand): Response<SavedAddressDto>

          @POST("api/SavedAddress/SetDefault")
          suspend fun setDefault(@Body command: SetDefaultSavedAddressCommand): Response<Unit>

          @DELETE("api/SavedAddress/Delete/{id}")
          suspend fun delete(@Path("id") id: String): Response<Unit>
      }

files_to_modify:
  - path: src/cleansia_customer_android/app/src/main/java/cz/cleansia/customer/core/user/UserModule.kt
    change: |
      After the existing `provideUserApi` provider, add:

        @Provides
        @Singleton
        fun provideSavedAddressApi(@AuthRetrofit retrofit: Retrofit): SavedAddressApi =
            retrofit.create(SavedAddressApi::class.java)

dependencies:
  - TASK-B1
verification:
  - `./gradlew :app:compileDebugKotlin`
```

### TASK-B3: Rewire `AddressRepository` — backend as source of truth, DataStore as cache

```yaml
task: Refactor AddressRepository to sync with /api/SavedAddress/*
id: TASK-B3
type: refactor
priority: high
specialist: mobile
app: customer-android
estimated_complexity: large
recommended_model: sonnet

context: |
  Today: DataStore-only, seeded with 2 demo addresses. Tomorrow:
  backend authoritative, DataStore caches the last known list so the
  UI renders instantly on cold start (pre-network). DataStore is
  populated from `getMine()` on first signed-in load and after every
  mutation. Guest users (unauthenticated) keep working via local-only
  fallback — no backend calls.

  Keep UserAddress as the UI-facing type. Add mapping:
    SavedAddressDto.toUserAddress() and UserAddress.toAddCommand() /
    UserAddress.toUpdateCommand(savedAddressId=).

  Error handling: mutations that fail surface a snackbar error key
  and do NOT mutate the local cache. Keep-failed-optimistic-update is
  a trap — we'd drift from the server. On refresh failure, keep the
  stale cache and show a snackbar warning once (not per retry).

  Drop the seed data entirely — backend is the source. First-install
  users see an empty list + "Add your first address" UX until they
  do. The AddressManagerScreen's empty state already renders this.

files_to_modify:
  - path: src/cleansia_customer_android/app/src/main/java/cz/cleansia/customer/core/data/UserAddress.kt
    change: |
      Add optional `serverId: String? = null` field (nullable because
      brand-new unsaved picks have no server id until we POST them).
      Also add extension functions at bottom of same file:

        internal fun SavedAddressDto.toUserAddress(): UserAddress =
            UserAddress(
                id = id,                // reuse server id as local id too
                serverId = id,
                label = label,
                street = street,
                city = city,
                zipCode = zipCode,
                country = country.orEmpty(),
                latitude = latitude,
                longitude = longitude,
                isDefault = isDefault,
            )

        internal fun UserAddress.toAddCommand(setAsDefault: Boolean): AddSavedAddressCommand =
            AddSavedAddressCommand(
                label = label,
                street = street,
                city = city,
                zipCode = zipCode,
                state = null,
                countryId = null, // backend picks default (CZE) — see AddSavedAddress handler
                setAsDefault = setAsDefault,
                latitude = latitude,
                longitude = longitude,
            )

        internal fun UserAddress.toUpdateCommand(savedAddressId: String): UpdateSavedAddressCommand =
            UpdateSavedAddressCommand(
                savedAddressId = savedAddressId,
                label = label,
                street = street,
                city = city,
                zipCode = zipCode,
                state = null,
                countryId = null,
                latitude = latitude,
                longitude = longitude,
            )

  - path: src/cleansia_customer_android/app/src/main/java/cz/cleansia/customer/core/data/AddressRepository.kt
    change: |
      Rewrite. Keep DataStore for the local cache only. Constructor now
      injects `api: SavedAddressApi`, `tokenStore: TokenStore` (to detect
      guest-vs-signed-in), `snackbar: SnackbarController`, `@ApplicationContext context`,
      `@ApplicationScope scope: CoroutineScope` (if one exists; otherwise
      use `GlobalScope` annotated with @OptIn(DelicateCoroutinesApi)).

      Expose:
        val addresses: Flow<List<UserAddress>>   // DataStore-backed, same as today
        val selectedId: Flow<String?>            // unchanged
        suspend fun refreshFromServer(): Boolean // returns true on success, false otherwise; snackbars on failure
        suspend fun upsert(address: UserAddress, setAsDefault: Boolean = address.isDefault): Result<UserAddress>
        suspend fun delete(id: String): Result<Unit>
        suspend fun setDefault(id: String): Result<Unit>
        suspend fun setSelected(id: String?)      // local-only, unchanged
        suspend fun rename(id: String, newLabel: String): Result<Unit>
          // implemented as "fetch current address fields + call update command with new label"

      Behavior rules:
        - If `tokenStore.current() == null` → fall back to local-only
          DataStore behavior (current logic). Guest users can pick
          addresses during booking without signing in.
        - Signed-in upsert: if address.serverId == null → POST /Add,
          then write returned DTO into DataStore cache.
        - Signed-in upsert with serverId → PUT /Update, then write
          returned DTO into DataStore.
        - delete with serverId → DELETE /Delete/{id}, then remove from cache.
        - setDefault with serverId → POST /SetDefault, then refetch
          getMine() to update cache (SetDefault returns Unit, we need
          to see the demoted peers).
        - rename → fetch current cached row, call update() with new
          label + existing fields (simpler than a dedicated endpoint).
        - On API failure: return Result.failure, show snackbar via
          SnackbarController, do NOT mutate cache.
        - Drop the seed-demo-data code entirely.

      Pattern for parsing ProblemDetails 400s: use the same
      ApiErrorParser that core/auth/ApiErrorParser.kt uses for auth
      errors. Grep it, reuse it.

  - path: src/cleansia_customer_android/app/src/main/java/cz/cleansia/customer/core/data/DataModule.kt (or wherever AddressRepository is provided)
    change: |
      If AddressRepository had a @Provides / @Inject already, extend
      its constructor with the new deps. Hilt resolves automatically
      once SavedAddressApi is in the graph (TASK-B2).

dependencies:
  - TASK-B1
  - TASK-B2
verification:
  - `./gradlew :app:compileDebugKotlin`
  - Manual device test (after token provisioned): sign in → go to
    Profile → Addresses. List should be empty for fresh signed-in
    users. Add one via map picker → appears in list. Restart app →
    still there (cache hit + refresh). Rename → label updates.
    Delete → removed. Default swap → exactly one default shown.
  - Guest mode: before sign-in, the booking flow can still pick an
    address on-the-fly — that one-off doesn't hit the backend.
```

### TASK-B4: Trigger `refreshFromServer()` on sign-in

```yaml
task: Kick off address refresh when session becomes authenticated
id: TASK-B4
type: feature
priority: medium
specialist: mobile
app: customer-android
estimated_complexity: small
recommended_model: sonnet

context: |
  SessionManager already emits sign-in/sign-out events. Subscribe
  from the same place we subscribe for user-profile fetch (MainShell
  LaunchedEffect, or a dedicated bootstrap VM). Parallel fetch:
  profile + addresses.

files_to_modify:
  - path: src/cleansia_customer_android/app/src/main/java/cz/cleansia/customer/features/main/MainShell.kt
    line_range: '77-90' # the existing LaunchedEffect(Unit) { profileVm.refresh() }
    change: |
      Alongside profileVm.refresh(), also trigger address repo refresh.
      Inject AddressRepository via EntryPointAccessors (similar to
      TokenStoreEntryPoint usage), or create an AddressBootstrapViewModel
      that owns this concern. Simpler: use EntryPointAccessors.

      After profileVm.refresh() call, add:

        val addressRepo = EntryPointAccessors
            .fromApplication(context, AddressRepositoryEntryPoint::class.java)
            .addressRepository()
        addressRepo.refreshFromServer()  // swallow return; failures show snackbar

  - path: src/cleansia_customer_android/app/src/main/java/cz/cleansia/customer/core/data/AddressRepositoryEntryPoint.kt (CREATE)
    change: |
      @EntryPoint
      @InstallIn(SingletonComponent::class)
      interface AddressRepositoryEntryPoint {
          fun addressRepository(): AddressRepository
      }

dependencies:
  - TASK-B3
verification:
  - Sign in → see addresses appear within ~1 second
  - Sign out → list clears (forced sign-out flow already wipes DataStore... verify this; if not, do it in AddressRepository.clear() on ForcedSignOut event)
```

### TASK-B5: Wire `savedAddressId` into booking submission

```yaml
task: Booking flow POSTs CreateOrder with savedAddressId when a saved address is picked
id: TASK-B5
type: feature
priority: medium
specialist: mobile
app: customer-android
estimated_complexity: medium
recommended_model: sonnet

context: |
  Mobile's booking flow (BookingBottomSheet) currently has the UI for
  picking an address but the actual CreateOrder POST is not wired
  yet — per Phase 6 plan. Scope of THIS task is narrow: when the
  mobile order-submission code eventually lands, it must use
  savedAddressId whenever the user picked one of their saved
  addresses, and fall back to inline customerAddress for one-off picks.

  If the Create Order POST doesn't exist yet (grep the codebase;
  BookingBottomSheet.onComplete is a no-op as of this spec), this
  task is a NOTE in the future order-submission spec rather than a
  file edit today. Mark as deferred with a pointer.

files_to_modify: []  # conditional — see below

conditional_edit:
  condition: "if CreateOrder API call exists on mobile"
  path: wherever the booking VM builds the payload
  change: |
    When BookingState references a saved address whose serverId != null,
    populate CreateOrderCommand.savedAddressId and OMIT customerAddress.
    When the user picks a one-off address (no serverId), populate inline
    customerAddress with { street, city, zipCode, state=null, countryId=null }.
    Backend enforces XOR; don't send both.

dependencies:
  - TASK-B3
verification:
  - Deferred until Phase 6 order-submission spec lands
```

### TASK-B6: Drop unused i18n keys + confirm snackbar keys present

```yaml
task: Verify error snackbar strings exist for address failures
id: TASK-B6
type: content
priority: low
specialist: mobile
app: customer-android
estimated_complexity: small
recommended_model: haiku

context: |
  Mobile snackbar infrastructure parses backend error keys via
  resources.getIdentifier() (see ApiErrorParser). Add string
  resources for the three new/existing backend error keys:
    address.not_owned_by_user → error_address_not_owned_by_user
    address.label_required    → error_address_label_required
    order.address_exactly_one_required → error_order_address_exactly_one_required
    (generic fallback already exists: error_generic_network)

files_to_modify:
  - path: src/cleansia_customer_android/app/src/main/res/values/strings.xml
    change: |
      Add (keep alphabetical order among error_* keys):
        <string name="error_address_label_required">Please enter a label for this address.</string>
        <string name="error_address_not_owned_by_user">You can only manage your own addresses.</string>
        <string name="error_order_address_exactly_one_required">Please select a saved address or enter a new one.</string>

  - path: src/cleansia_customer_android/app/src/main/res/values-cs/strings.xml
    change: |
      Add Czech translations of the above 3 strings.

dependencies: []
verification:
  - `./gradlew :app:processDebugResources`
```

---

## Execution order

1. **TASK-B1** (DTOs) + **TASK-B6** (strings) in parallel
2. **TASK-B2** (SavedAddressApi + Hilt)
3. **TASK-B3** (AddressRepository rewrite) — the big one
4. **TASK-B4** (refresh on sign-in)
5. **TASK-B5** deferred to Phase 6 order-submission spec

Estimated total token usage: ~65k.

---

## Manual steps before starting Phase B

1. **Regenerate mobile OpenAPI spec** (instructions above) so you can cross-check endpoint shapes as you go. Not strictly required — the spec is hand-written and doesn't read the JSON — but good sanity check.
2. **Provision Mapbox secret token** (optional for Phase B itself, but geocoding in TASK-003 won't work without it). Phase B can still be tested end-to-end — the backend will save with null coords when the token is blank and the mobile hints are also null.

## Out of scope for Phase B

- **Order submission** — lands in a separate Phase 6 spec; TASK-B5 is a forward reference.
- **Admin app address management** — admin doesn't manage customer addresses today.
- **Partner app routing** — partner sees order addresses but doesn't create them.
