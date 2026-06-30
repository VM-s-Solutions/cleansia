# Mobile Patterns (Android: Kotlin/Compose/Hilt — iOS: Swift/SwiftUI) — REAL TYPES

The catalog for both mobile platforms, bound to the **actual Android idioms in this repo** (verified
from source). Android is the **reference implementation**; iOS mirrors it surface-for-surface. Read
this + [`conventions.md`](./conventions.md) before touching `.kt`/`.swift`. **Reuse these exact base
types, components, and the exact ViewModel/Repository idiom — never invent parallel ones.**
Push/notification prose: [`../../docs/architecture/push-notifications.md`](../../docs/architecture/push-notifications.md).

> **Binding rule for every mobile agent:** before writing a feature, open the nearest existing
> feature (e.g. `features/orders/`) in the same app and mirror its idiom exactly. The samples below
> are copied from live code (customer-app `features/orders/OrderDetail*`).

---

## Modules & namespaces (verified)

- `:core` → package `cz.cleansia.core.*` — shared by both apps. Real packages: `auth` (`TokenStore`,
  `SessionManager`, `AuthInterceptor`, `AuthAuthenticator`, `JwtDecoder`, `SessionScopedCache`),
  `network` (`NetworkCall.kt` → `networkCall { }`, enum serializers), `snackbar` (`SnackbarController`,
  `GlobalSnackbarHost`), `ui.components` (`CleansiaButton.kt`, `CleansiaTextField.kt`, `CleansiaDialog.kt`,
  `CleansiaDropdown.kt`, `CleansiaPhoneInput.kt`, `CleansiaSectionHeader.kt`, `CodeInput.kt`,
  `MascotEmptyState.kt`, …), `ui.theme` (`CleansiaTypography` = Poppins headings / Nunito body,
  `SemanticColors`, `Shape`, `Spacing`), `location`, `servicearea`, `format`, `freshness`, `sentry`.
- `:customer-app` → `cz.cleansia.customer.*`. Features inline ViewModel+Screens under
  `features/<name>/`; data adapters under `core/<domain>/`; theme `ui.theme.CleansiaTheme`;
  `ui.state.ActionState`; typed routes in `navigation.Routes`.
- `:partner-app` → `cz.cleansia.partner.*`. Features split `features/<name>/screens` + `…/viewmodels`.
  Has a local Room DB for notifications (`core.notifications.db`) that customer-app does not.

**Put shared code in `:core`, never duplicate it across the two apps.** When the shared logic must
reach an **app-specific generated client** (the per-app NSwag/OpenAPI Retrofit service), keep the
logic in `:core` behind a small **per-app binding seam** — a `:core` interface that each app `@Binds`
to its own thin impl over its generated client (e.g. `cz.cleansia.core.notifications.DeviceRegistrationClient`,
implemented per-app by `DeviceApiClient` wrapping the app's `safeApiCall`). Parameterize per-app config
(e.g. a DataStore name) by `@Provides`-ing the concrete value from each app's Hilt module behind a
`:core` qualifier — never hardcode a partner-vs-customer choice in `:core`. Mirrors the existing
`ApiResult`/`TokenStore`/`DeviceIdProvider` factoring (ADR-0011).

---

## ViewModel — exact idiom (from `features/orders/OrderDetailViewModel.kt`)

`@HiltViewModel` + `@Inject` constructor; **sealed `*UiState`** (Loading / Error / Loaded);
**`StateFlow`** for everything the screen observes; **`SharedFlow(replay=0, extraBufferCapacity=1)`**
for one-shot effects; injects `SnackbarController` + `@ApplicationContext` (for `getString`):

```kotlin
sealed interface OrderDetailUiState {
    data object Loading : OrderDetailUiState
    data class Error(val canRetry: Boolean) : OrderDetailUiState
    data class Loaded(val order: OrderDetailDto) : OrderDetailUiState
}

@HiltViewModel
class OrderDetailViewModel @Inject constructor(
    private val orderRepository: OrderRepository,
    private val snackbar: SnackbarController,
    @ApplicationContext private val appContext: Context,
    savedStateHandle: SavedStateHandle,
) : ViewModel() {

    private val orderId: String? = savedStateHandle["orderId"]          // nav arg name == route param

    private val _state = MutableStateFlow<OrderDetailUiState>(OrderDetailUiState.Loading)
    val state: StateFlow<OrderDetailUiState> = _state.asStateFlow()

    private val _cancelState = MutableStateFlow<ActionState>(ActionState.Idle)
    val cancelState: StateFlow<ActionState> = _cancelState.asStateFlow()

    private val _cancelResult = MutableSharedFlow<CancelOrderResponse>(extraBufferCapacity = 1)
    val cancelResult: SharedFlow<CancelOrderResponse> = _cancelResult.asSharedFlow()

    fun cancel(reason: String?) {
        val id = orderId ?: return
        if (_cancelState.value is ActionState.Submitting) return        // re-entry guard (idempotent)
        viewModelScope.launch {
            _cancelState.value = ActionState.Submitting
            val result = orderRepository.cancel(id, reason?.trim()?.ifBlank { null })
            if (result == null) {                                        // null = failure (repo already snackbarred)
                _cancelState.value = ActionState.Error(appContext.getString(R.string.order_cancel_retry_hint))
            } else {
                snackbar.showSuccess(appContext.getString(R.string.order_cancel_success_no_refund))
                _cancelState.value = ActionState.Idle
                _cancelResult.emit(result)                               // effect → screen closes sheet
                orderRepository.refresh(); load()                        // invalidate cache + re-fetch
            }
        }
    }
}
```

The shared one-shot action state — canonical home is `:core` (`cz.cleansia.core.ui.state.ActionState`);
new code in either app imports it from there. The customer app's `cz.cleansia.customer.ui.state.ActionState`
is the identical pre-existing copy, kept until its call sites migrate:

```kotlin
sealed interface ActionState {
    data object Idle : ActionState
    data object Submitting : ActionState
    data class Error(val message: String) : ActionState   // pre-localized in the VM
}
```

**Rules:** `StateFlow` for observed state, `SharedFlow(replay=0)` for effects (close-sheet, success);
expose read-only (`.asStateFlow()` / `.asSharedFlow()`); guard re-entry on submit; success is an
*effect*, never a state; error messages are localized in the VM from `R.string.*`.

## Screen — exact idiom (from `features/orders/OrderDetailScreen.kt`)

Inject via `hiltViewModel()`; collect every flow with `collectAsStateWithLifecycle()`; **derive**
booleans/strings from the sealed types for sub-composable params; observe effects with
`LaunchedEffect(viewModel) { flow.collect { … } }`; keep sheet/dialog visibility in
`remember { mutableStateOf(...) }` (not in the VM); pattern-match state only to render the body:

```kotlin
@Composable
fun OrderDetailScreen(onBack: () -> Unit = {}, viewModel: OrderDetailViewModel = hiltViewModel()) {
    val state by viewModel.state.collectAsStateWithLifecycle()
    val cancelState by viewModel.cancelState.collectAsStateWithLifecycle()
    val cancelling = cancelState is ActionState.Submitting
    val cancelError = (cancelState as? ActionState.Error)?.message
    var showCancelSheet by remember { mutableStateOf(false) }

    LaunchedEffect(viewModel) { viewModel.cancelResult.collect { showCancelSheet = false } }

    Scaffold(/* topBar/bottomBar derived from state */) { padding ->
        when (val s = state) {
            OrderDetailUiState.Loading -> LoadingSpinner()
            is OrderDetailUiState.Error -> ErrorView(canRetry = s.canRetry, onRetry = viewModel::refresh)
            is OrderDetailUiState.Loaded -> OrderDetailContent(order = s.order /* … */)
        }
        if (showCancelSheet) CancelOrderSheet(error = cancelError, submitting = cancelling,
            onSubmit = viewModel::cancel, onDismiss = { showCancelSheet = false })
    }
}
```

## Networking & Repository — exact idiom

Two layers: an **Api adapter** wrapping the generated Retrofit service and mapping wire→app DTOs
(`raw.mapBody { it.toAppDto() }`), and a **`@Singleton` Repository** that caches via `StateFlow`,
implements `SessionScopedCache` (`clear()` on sign-out), wraps calls in **`networkCall { }`**, parses
HTTP errors via **`ApiErrorParser.parseToUserMessage(...)`** → **`snackbar.showError(...)`**, and
returns **`T?` (null = failure, already snackbarred)**:

```kotlin
@Singleton
class OrderRepository @Inject constructor(
    private val api: OrderApi,
    private val snackbar: SnackbarController,
    @ApplicationContext private val appContext: Context,
) : SessionScopedCache {

    suspend fun cancel(orderId: String, reason: String?): CancelOrderResponse? {
        val resp = networkCall { api.cancel(CancelOrderRequest(orderId, reason)) } ?: return null
        if (!resp.isSuccessful) {
            snackbar.showError(ApiErrorParser.parseToUserMessage(appContext, resp.errorBody(), resp.code()))
            return null
        }
        return resp.body()
    }

    override suspend fun clear() { /* reset cached StateFlows */ }
}
```

`networkCall` re-throws `CancellationException` (structured concurrency) and returns `null` on any
other throwable. API services are provided per feature via a Hilt `@Module @InstallIn(SingletonComponent::class) object`
using `@AuthRetrofit` (main) vs `@NoAuthRetrofit` (refresh-only) qualifiers.

## Shared UI & theme

Use `cz.cleansia.core.ui.components.*` — `CleansiaPrimaryButton`, `CleansiaOutlinedButton`,
`CleansiaTextLink` (with `CleansiaButtonSize.{Small,Medium,Large}`), `CleansiaTextField`,
`CleansiaDialog`, `MascotEmptyState`, etc. Colors/typography via `MaterialTheme.colorScheme.*` /
`MaterialTheme.typography.*` inside `CleansiaTheme` (which applies `CleansiaTypography`). Never style
raw components one-off; never duplicate a `:core` component.

## Navigation — typed routes

`navigation/Routes.kt` defines `@Serializable data object`/`data class` routes; args are constructor
params (`Routes.OrderDetail(orderId)`), read back via `savedStateHandle["orderId"]` (name matches).
No magic-string routes.

## Strings & states

All user text in `res/values/strings.xml`, accessed via `stringResource(R.string.x)` (or
`appContext.getString` in the VM), domain-prefixed (`order_`, `auth_`, `error_`). Loading/Error/Loaded
handled by the sealed `*UiState`; empty states use `MascotEmptyState`; transient errors go to the
snackbar (not the main state); submit errors use `ActionState.Error`.

---

## iOS — SwiftUI/MVVM parity port

`src/cleansia_ios` (scaffolded — workspace + `CleansiaCore` SPM package + `CleansiaPartner`/
`CleansiaCustomer` app targets, iOS-16 floor) mirrors Android 1:1. The canonical sealed `UiState<T>` /
`ActionState` enums and the one `ApiResult`/`ApiError` live in `CleansiaCore/Sources/CleansiaCore/State`
and `…/Network`; the `:core` sub-packages map by name (`auth`→`Auth`, `network`→`Network`,
`snackbar`→`Snackbar`, `ui.components`→`Components`, `ui.theme`→`DesignSystem`, `ui.state`→`State`, plus
`DI`). The Xcode projects are XcodeGen-generated from each app's `project.yml` (gitignored output).

| Android | iOS equivalent |
|---|---|
| `:core` module | the `CleansiaCore` Swift package (theme, components, auth/network, state) |
| Hilt `NetworkModule`/`AuthModule` per app | a hand-rolled per-app `AppContainer` (initializer injection; no Hilt analogue) conforming to the `Core/DI` `AppContainer` protocol. `Core/DI`'s `@MainActor BaseAppContainer` is the lazy composition root: it `lazy`-builds ONE `AuthSpine` (the `AuthApiClient`, which is **both** the `AuthClient` and the `RefreshClient` — it owns the separate authed + no-auth sessions internally) and exposes the same instance as `authClient`/`refreshClient`; the `SessionManager` + `SessionRefresher` are wired off that one spine + the shared `TokenStore`/cache registry so there's a single token source. `PartnerAppContainer`/`CustomerAppContainer` live in the app targets, own a `BaseAppContainer`, and pass their app-specific `make…AuthSpine`/`makeApiClient` factories; the App owns one + reads its snackbar and observes `sessionManager` |
| two OkHttp clients (`@NoAuthOkHttp` refresh vs `@AuthOkHttp`) | the `AuthApiClient` holds **two `URLSession`s** — an authed session + a separate no-auth `.ephemeral` session used for `/api/Auth/*` (login/refresh/forgot) so a 401-on-refresh can't loop; `AuthNetworkBoundary` in `Core/DI` is the generic lazy-seam variant kept for surfaces that need the boundary made explicit |
| `Set<SessionScopedCache>` Hilt multibinding (`SessionScopedModule`) | a `SessionScopedCacheRegistry` in `Core/Auth` — repos `register` themselves (held weakly); both sign-out and the 401-refresh path call `clearAll()`, so the two clear-paths can't drift |
| `AuthInterceptor` anon path-skip (one hardcoded list) | `HeaderAdapter` takes an injected `AnonymousAllowList` (`Core/Auth`) — **host-specific**: `.partner` is auth-only; `.customer` adds the guest-booking surface (`Service/Package/Extra GetOverview`, `Membership/GetPlans`, `Order/{Quote,CreateOrder,Lookup,LookupBatch}`, `Payment/CreateOrder`, `Referral/Validate`). Same case-insensitive path-contains match as Android; `Logout` is never anon. **`AnonymousAllowList` also carries a `dualUsePaths` set** (`isDualUse(path:)`) — `.customer` = `Order/{Quote,CreateOrder}` + `Payment/CreateOrder`; `HeaderAdapter` attaches the Bearer when `isDualUse OR !isAnonymous`, so a dual-use path is Bearer-iff-token (signed-in carries it for tier/membership pricing + user binding; true guest stays tokenless). Pure-anon + guest-read paths stay tokenless even signed-in; `.partner` has none (T-0332) |
| `AuthAuthenticator` `synchronized(this)` single-flight 401-refresh | `actor SessionRefresher` (`Core/Auth`): coalesces concurrent 401s into ONE network refresh (queued callers reuse the freshly-stored token), **replaces** the stored refresh token every refresh (theft-detection), and on failure/expiry wipes the `TokenStore` + `clearAll()` caches + emits `ForcedSignOut` via the `SessionManager` (no retry) |
| `BuildConfig.API_BASE_URL` | per-app `AppConfig.apiBaseURL` reading the `API_BASE_URL` Info.plist key (set from the build setting; each app points at its own `…-mobile-…` host) |
| `ui.theme.Spacing` / `CleansiaShapes` | `Spacing` + `CornerRadius` enums in `Core/DesignSystem` (same 8-pt scale + 6/12/16/24/32 corners + a `pill`) |
| Material `colorScheme.*` (per-app `lightColorScheme`/`darkColorScheme`) | `CleansiaColors` in `Core/DesignSystem` — the **same Material slot names** (`primary`/`onPrimary`/`surface`/`outline`/`error`…) as `Color.dynamic(light:dark:)`, so components read 1:1 with the Compose source; the sky/slate ramp is `Palette` (internal) |
| `CleansiaTypography` (Poppins headings / Nunito body) | `CleansiaTypography` in `Core/DesignSystem` — same slot names returning `Font`; `CleansiaFont.{poppins,nunito}` register bundled `.ttf` (owner step) and **fall back to system font** if absent so it always builds |
| `cz.cleansia.core.ui.components.*` Composables | the same `Cleansia*` names as `View`s in `Core/Components` — **native SwiftUI, no Material re-impl** (Gate-DP): bottom-sheet pickers → `.sheet`+`.presentationDetents`, Material Checkbox → SF-Symbol tappable row, custom Dialog → overlay card; same layout/labels/branding |
| `@HiltViewModel` + `StateFlow` | `ObservableObject` + `@Published` state; own a VM with `@StateObject`, inject with `@ObservedObject` (the iOS-16 foot-gun, ADR-0014 #11). New VMs may subclass the `@MainActor open class ViewModel` base in `Core/State` (NOT `@Observable`) |
| sealed `*UiState` (Loading/Error/Loaded) | an `enum State { case loading, error(canRetry: Bool), loaded(OrderDetailDto) }` |
| `ActionState` (Idle/Submitting/Error) | an `enum ActionState` mirror |
| `SharedFlow(replay=0)` effects | a `PassthroughSubject` / async stream |
| `@Composable Screen` + `…Content` | a `View` + stateless preview subview |
| `@Singleton Repository : SessionScopedCache` | an injected actor/class with a `clear()` on sign-out |
| `networkCall { }` + `ApiErrorParser` | an equivalent throwing wrapper + error→message parser |
| `SnackbarController` | `@MainActor ObservableObject` in `Core/Snackbar` with `@Published current: SnackbarMessage?` |
| `GlobalSnackbarHost` composable at the nav root | a `.snackbarHost(controller)` root `ViewModifier` (bottom overlay), one per app |
| `SnackbarController` injected via Hilt | the controller in `@Environment(\.snackbarController)`; the App owns it as `@StateObject` |
| Android `SnackbarInsetState` global inset flow | the `bottomInset:` parameter on `.snackbarHost` (iOS-native, view-local — set by screens with bottom chrome) |
| `ApiErrorParser.parseToUserMessage` | an app-injectable `ApiErrorLocalizing` seam (`ApiErrorLocalizer`); server message wins, else status→localized fallback |
| `stringResource(R.string.x)` | `String(localized:)` / `Localizable.strings` |
| `navigation.Routes` (`@Serializable`) — **top-level audience hops** (Splash/Login/Lock/Main via `popUpTo{inclusive}`) | **the flat-enum root-switch** (`PartnerRootView` over a closed `enum Route`: `.splash`/`.login`/`.verifyEmail`/`.registrationLock`/`.dashboard`-shell), seeded `hasValidSession ? .splash : .login`, a verified login bounces through `.splash` which re-resolves shell-vs-lock (**ADR-0020**, reviewer #23). A top-level audience state modeled as a pushed `NavigationPath` is a deviation |
| `navigation.Routes` (`@Serializable`) — **intra-audience push** (OrderDetail, ProfileSection, onboarding-chain sections) | `NavigationStack` + typed route enum (the push container **within** a root audience state, NOT the audience selector). **Concrete (T-0310, §7.7):** the Profile tab hosts an in-tab `NavigationStack` over a typed `ProfileRoute` enum; the RegistrationLock (a root audience state) owns its OWN local `NavigationStack` over the same gate-section routes and pushes the **shared** section set over itself with `onboarding == true` — fail-closed, no cross-audience routing into the shell |
| per-app `openApiGenerate { generatorName=kotlin }` reading `openapi/{partner,customer}-mobile-api.json` | per-app `openapi-generator` **swift5 + urlsession** (`responseAs: AsyncAwait`) reading the **same shared committed specs**; config in `cleansia_ios/openapi/openapi-generator-config.*.yaml`, run via `scripts/generate-api-clients.sh`, emitting `Cleansia{Partner,Customer}Api` SPM packages. Generated output is **gitignored + never hand-edited** (change the spec or config, regenerate). The **auth/session/header spine is hand-written** in `Core/Auth` and **excluded from codegen**. First real generation is owner-gated (`manual_step: mobile-spec-regen`) — the specs are stale pre-T-0272 |
| Android's generated Retrofit service authed by the OkHttp `AuthInterceptor`/`AuthAuthenticator` already installed in the client | **the generated swift5 client authenticates ONLY via a custom `RequestBuilderFactory` installed into the generated global config** (`Cleansia{Partner,Customer}ApiAPI.requestBuilderFactory`) — its `RequestBuilder` subclass routes **every** generated request through the **same** `Core/Auth` spine (`HeaderAdapter` for Bearer-iff-not-anon + `X-Device-Id`/`X-Device-Label`/`X-Time-Zone`, `actor SessionRefresher` for single-flight 401→refresh→retry), using only the generator's `open` points so it survives regeneration (**ADR-0019**). The generated APIs are static, apply only the static `customHeaders`, and are all `requiresAuthentication: false` — so without this they 401 tokenless |
| `core/settings/AppSettingsRepository.kt` (DataStore `partner_app_settings`: `onboarding_seen`, `language`, `theme`) | a single general **`AppSettingsStore`** in `CleansiaCore`, **`UserDefaults`-backed** (DataStore's wiped-on-uninstall parity — NOT Keychain): `hasSeenOnboarding`/`markSeen()` + a resolved language tag ∈ {en,cs,sk,uk,ru} (sprint-12 §7.5 D1, reviewer #26a) |
| `core/validation/EmailValidator.kt` + the `passwordHas*` getters in `RegisterUiState` | `CleansiaCore/Validation/EmailValidator.swift` (already hoisted) + a Core **`PasswordPolicy`** (≥8 && letter && digit — the predicate lifted OUT of the VM) feeding a Core **`PasswordRuleList`** view (`:core` `PasswordRuleList.kt` parity) — shared by partner + customer (sprint-12 §7.5 D4, reviewer #26c) |
| hand-written `AuthApi.kt` Retrofit verbs (`@POST`/`@PUT` per endpoint) | the hand-written `Auth.swift` spine `send()` takes an **`httpMethod:` param defaulting `.post`**; `ConfirmUserEmail` passes `.put` (header-parity §3 — hardcoding POST is a silent 405). All four T-0305 paths (Register/ConfirmUserEmail/ResendConfirmationEmail/ForgotPassword) are already in `AnonymousAllowList.sharedAuth`; `Logout` stays authed (sprint-12 §7.5 D3, reviewer #25) |
| `core/location/ReverseGeocodingService.kt` (Mapbox Geocoding v5 over OkHttp; `accessToken` from BuildConfig) | `CleansiaCore/Location` **`GeocodingService`** protocol + **`CLGeocoderGeocodingService`** default impl — a 1:1 port (`reverseGeocode`/`forwardGeocode` → `GeocodedAddress?`/`[GeocodedAddress]`) **minus the Mapbox token + the OkHttp/network args** (MapKit = system framework, **no token**). Best-effort: nil/`[]` on error, **cancel the in-flight geocode before re-firing** (`kCLErrorGeocodeCanceled` swallowed) — the `runCatching{}.getOrNull()` parity. Debounce ports VERBATIM: **300ms forward / 500ms reverse** (`AddressPickerScreen.kt:188,171` — also the `CLGeocoder` rate-limit guard) (sprint-12 §7.6 D1/D3, reviewer #27) |
| `core/location/{GeocodedAddress,UserLocation}.kt` + `MapStyles.kt` (Mapbox style URIs) | `Coordinate` + `GeocodedAddress` plain value types in `CleansiaCore/Location` (the `GeocodedAddress.kt` field parity). **`MapStyles.kt` is NOT ported** — the stock MapKit standard style is the parity baseline; a custom Mapbox Studio style returns only if Q-IOS-02 flips to "yes" (sprint-12 §7.6 D4) |
| Mapbox `MapboxMap` + center-pin overlay + my-location FAB (`AddressPickerScreen.kt`) | **`MapProvider`** picker-map factory (a `Map(coordinateRegion:annotationItems:[])` + SwiftUI overlay pin the map pans under — iOS-16 variant, NO `Map{Marker}`/`onMapCameraChange`, reviewer #12) in `CleansiaCore/Location`, the **only** sanctioned MapKit consumer. **Current-location/the my-location FAB + the `LocationProvider` (`CLLocationManager`) seam are DEFERRED to T-0310** (needs T-0325's `NSLocationWhenInUseUsageDescription` plist key — owner); T-0306 centers on the **Prague default** + ships pan+search parity. Full-bleed `OrderDetail` map + service-area polygon overlay added **additively** later (`MKMapView`/`UIViewRepresentable`, ADR-0014 D6′). The AddressPicker has **NO `UiState`/`ActionState`** — plain `@Published` state + a one-shot `onConfirmed(GeocodedAddress)` callback (sprint-12 §7.6 D1/D2/D3) |
| Mapbox `MapBackdrop` full-bleed map (single address pin, camera-padded for the sheet — `OrderDetailScreen.kt:256-299`) | the **additive `MapProvider.fullBleedMap(coordinate:)` method** (`MKMapView`/`UIViewRepresentable` inside `MapKitMapProvider`, ADR-0014 D6′) — **ONE address pin, camera bottom-padded for the sheet peek**, NO `overlays:`/`polygon:` param (there is **no service-area polygon data** in the partner spec — `ServiceCityDto` has only `zipPrefix`; Android renders no polygon either; overlay support is additive IF T-0334 ever has geometry). The §7.6 D1 minimal-now/additive-later seam reaching T-0307; feature/VM import no MapKit (reviewer #7/#12/#30) (sprint-12 §7.9 (a)) |
| `BottomSheetScaffold` + `rememberStandardBottomSheetState(PartiallyExpanded, skipHiddenState=true)`, full-bleed map always behind, `sheetPeekHeight=0.75·screen` (the Wolt/Foodora **non-modal** 3-snap sheet, `OrderDetailScreen.kt:172-245`) | the **custom non-modal `SnapSheet` `CleansiaCore` container** (`GeometryReader`+`DragGesture`, 3 snap offsets map-focus/peek≈0.75/expanded, layered over `fullBleedMap` — **iOS-16.0-safe, NOT `.presentationDetents`** which are `.medium`/`.large`-only on 16.0, custom 16.4+) — **ADR-0021** (the floor stays 16.0). NOT a modal `.sheet` (which would change the layout — dimmed screen behind, drag-to-dismiss, no live map). Native `.sheet`+`.presentationDetents` stays the **modal**-sheet way (the customer booking sheet, ADR-0018 D3); the discriminator = *modal-over-a-screen vs non-modal-over-a-live-backdrop* (reviewer #29) |
| Composable `OrderPrimaryAction` inlining `when(status){…}` (status×ownership×photos → action, `OrderPrimaryAction.kt:54-126`) | a **pure shared `OrderPrimaryAction.action(for:isMine:hasAfterPhotos:) -> OrderPrimaryAction` sealed enum** (`.take/.notifyOnTheWay/.start/.complete/.completeBlocked/.none`), one tested function for the **three** call sites (detail footer, list inline row, panes) — NOT three inline switches. Presentational; consumes `isMine`/`hasAfterPhotos` (ownership trust = SECURITY §7.8 O1–O4). Canonicalizes the Android inlined table (sprint-12 §7.9 (c), reviewer #31) |
| `Code?.toOrderStatus()` matching `Code.value` against `OrderStatus.values()` (`OrderStatusPill.kt:40-42`) | one `extension Code { func toOrderStatus() -> OrderStatus? { value.flatMap(OrderStatus.init(rawValue:)) } }` — the read-path DTOs (`OrderItem`/`OrderListItem`.`orderStatus`) carry the **`Code` envelope** `{type,name,value:Int?}` (the action responses carry the typed `OrderStatus`); `OrderStatus: Int` rawValues 0…6 = the backend ints (0 New·1 Pending·2 Confirmed·3 OnTheWay·4 InProgress·5 Completed·6 Cancelled). Mapped in **one** place — no raw-`Int` `.value` compares, no second mapper (sprint-12 §7.9, reviewer #31) |
| `@Singleton OrdersRepository` per-pane (~30s) + per-order `Staleness` watermarks + `invalidatePanesFor(mutation)` (the silent-stale resume + `OrdersListUiState` flag-bag, `OrdersRepository.kt:159-192` / `OrdersListViewModel.kt:89-120`) | the cache is **PORTED** (an actor/class with the same per-pane/per-order watermarks + mutation→panes map, registered in the `SessionScopedCacheRegistry`) — load-bearing for no-flash resume; **NOT** simplified to load-on-appear+`.refreshable` (that's an un-approved behavior divergence). The list state is **sealed per-pane `UiState<[OrderListItem]>` + a `RefreshPhase` enum** (`idle`/`userRefreshing`/`backgroundRefreshing`; PTR binds `==.userRefreshing` only — the silent-stale parity), **NOT** the E1 flag-bag (Android E1 fix → T-0337). Inline commit = iOS-native confirm/`swipeActions`, the **`SlideToCommit`→native** Gate-DP swap (sprint-12 §7.9 (e), reviewer #30) |
| `ActivityResultContracts.GetContent()` system image picker (gallery-only, `pickImage.launch("image/*")`, `PhotosSection.kt:146-161,200`) — Compose has no native camera/photo-source control either | the Core **`CameraOrLibraryPicker` `UIViewControllerRepresentable`** (`CleansiaCore/Components`) wrapping a camera-capable `UIImagePickerController` — **the repo's FIRST `UIViewControllerRepresentable`** + the canonical "imperative-UIKit-controller-behind-a-SwiftUI-seam" idiom (the *controller* analogue of the `MKMapView`/`UIViewRepresentable` *view* seam, ADR-0014 D6′; both ADR-0018 D2 brand-skin-over-native seams). The single Add tile opens a native `.confirmationDialog` action sheet → Take Photo (`.camera`) / Choose from Library (`.photoLibrary`). **Gate-DP camera-vs-gallery divergence (architect sign-off):** iOS adds **camera + library** over Android's **gallery-only** — the T-0308 ticket's camera requirement, an enhancement that ADDS a source affordance, not a layout/flow/branding change. Rejected: **PHPicker** (library-only — no camera), **AVFoundation** (over-engineered — rebuilds the system camera). NOT a feature/VM hand-rolled `UIImagePickerController` (sprint-12 §7.10 (a), reviewer #32) |
| Coil `SubcomposeAsyncImage(model = ImageRequest(blobUrl)…)` with loading/error states (`PhotosSection.kt:235-272`) + raw camera bytes base64'd uncompressed (`:155-159` — Android comments base64 is slow for multi-MB images) | SwiftUI **`AsyncImage(url:content:placeholder:)`** (the ADR-0018 D3 Coil→`AsyncImage` row — same frame/aspect + loading/broken-image states, **NO 3rd-party dep**; `blobUrl` is a per-fetch SAS URL so disk-cache parity isn't load-bearing — Kingfisher is the scoped fallback only if a future surface needs it) **+** a pure Core **`ImageCompressor`** (downscale longest-side ≤1920px aspect-preserved + JPEG **0.7** + `contentType "image/jpeg"`, OFF the main thread) before base64 — an iOS PERF divergence from Android's raw bytes (smaller base64-over-JSON body + bounded memory on the 2017 floor), changing pixels not layout. Single-photo upload via the **batch-of-one** `orderSavePhotos(SaveOrderPhotosCommand{orderId, photos:[{photoType, BlobFileDto{fileName, base64Content, contentType}, notes}]})` (`OrdersRepository.kt:264-291`); read `orderGetPhotos`; delete `orderDeletePhoto(photoId)`. **The Complete gate trusts the RE-FETCHED `OrderItem.hasAfterPhotos`** (`OrderDetailScreen.kt:558`), kept live by the post-mutation parent refresh (the `mutationVersion`→`onContentMutated` parity) — **NOT** `GetOrderPhotosResponse.afterPhotoCount` (sprint-12 §7.10 (b)/(c), reviewer #32) |
| Invoice-PDF viewing: the VM streams the `downloadInvoice` `ResponseBody` → app cache dir → a `FileProvider` URI handed to `Intent.ACTION_VIEW` (the system PDF viewer; a `notifyNoPdfViewer()` fallback if none installed — `InvoiceDetailViewModel.kt:81-108` / `InvoiceDetailScreen.kt:91-104`) | the Core **`QuickLookPreview` `UIViewControllerRepresentable`** (`CleansiaCore/Components`) wrapping **`QLPreviewController`** — the **2nd member of the `CameraOrLibraryPicker` family** (the canonical imperative-UIKit-controller-behind-a-SwiftUI-seam idiom; ADR-0018 D2 brand-skin-over-native), **reused by the customer app (T-0314)** so it MUST be Core, not partner-local. The generated swift5+urlsession `employeePayrollDownloadInvoice` (`format: binary`) **writes the body to disk and returns a local file `URL` itself** — so the VM holds the URL and surfaces it via a **ONE-SHOT event** (NOT a route); the screen presents `QuickLookPreview` over it. The "Open PDF" affordance is **guarded on the DTO's `pdfGenerationFailed`** (disabled/hidden when true — iOS does it better than Android's unconditional download). **NO `FileDownload` Core seam** (the generated client IS the download — an orchestration seam would be dead abstraction). **The previewed PDF is deleted from cache on dismiss — SECURITY E4** (`security/ios-earnings.md`); the coordinator hosts that cleanup. **Recorded Gate-DP divergence:** FileProvider/`ACTION_VIEW` → Core `QuickLookPreview`; same in-app PDF viewing, native mechanism, no stream-to-cache/FileProvider/no-viewer branch. **Rejected:** a partner-local representable (duplicated into customer); a share-sheet (export, not a viewer); `SafariView` (web URLs, not a `file://` PDF) (sprint-12 §7.12 (b), reviewer #33) |
| Per-screen private `formatMoney`/`currencySymbol`/`formatDate` copied across `EarningsSummaryScreen.kt`/`InvoiceDetailScreen.kt`/`InvoicesListScreen.kt`/`PeriodPayScreen.kt` (two grouped money precisions: `%,.0f` whole for the earnings headline `:421`, `%,.2f` decimal for invoices/PeriodPay) | a small Core **`EarningsFormat`** (`CleansiaCore`, the `EmailValidator`/`PasswordPolicy` factoring): `formatMoney`(`%,.2f` grouped) + `formatMoneyWhole`(`%,.0f` grouped) + ISO→local date helpers, reusing the **currency-symbol resolution HARVESTED to Core** (≥3 call sites — a `NumberFormatter(.currency)`/`Locale` lookup with the never-crash raw-`code` fallback, the `Currency.getInstance(code).getSymbol(Locale)?:code` parity). **Do NOT overload `DashboardFormat.money`** (it is `%.0f` ungrouped — the dashboard hero's own contract, neither earnings format). PeriodPay's `currencyCode` is threaded via the **nav route** (`EarningsRoute.periodPay`), not the DTO (`PeriodPaySummary` has none — `PeriodPayViewModel.kt:43-44`). Client-side display only — server amounts authoritative. The Earnings **summary** REUSES `PartnerDashboardClient.getStats` (the `DashboardStatsDto` the Dashboard hero renders — `EarningsSummaryViewModel.kt:23-32,49`), NOT a payroll-client duplicate or a `GetPeriodPays`-derived summary (sprint-12 §7.12 (c)/(d), reviewer #33) |
| `:core/notifications/{PushTokenRepository,PushTokenSessionObserver,DeviceRegistrationClient}.kt` — FCM token (`FirebaseMessaging.getInstance().token` + the messaging-service `onNewToken`) → `/api/Device/Register`/`Unregister` (`Platform="android"`); registration is a session×token PROPERTY (`combine(session,token).filterNotNull().distinctUntilChanged()→ensureRegistered`); `unregisterDevice()` BEFORE the token wipe (`AuthRepository.kt:210-225`); `clear()` = `SessionScopedCache` local-only | a Core **`PushRegistrar`** protocol in `CleansiaCore/Push` — the **SOLE** consumer of `UNUserNotificationCenter` + `UIApplication.registerForRemoteNotifications` (feature/lifecycle code imports neither `UserNotifications` nor `UIKit` — the `MapProvider`/`CameraOrLibraryPicker` seam family, ADR-0014 D6′/ADR-0018 D2) exposing `requestAuthorization`/`registerForRemoteNotifications`/an APNs-token stream (the `fcmToken: StateFlow<String?>` parity); the AppDelegate push callbacks via a per-app **`@UIApplicationDelegateAdaptor`** feeding it; a Core **`PushSessionObserver`** (the `PushTokenSessionObserver.kt` combine-parity); `Device/*` over the **ADR-0019** spine, **`Platform="ios"`**, the one `X-Device-Id`; `unregisterDevice()` from `AuthApiClient.logout()` BEFORE the `TokenStore` wipe + the local `clear()` via the `SessionScopedCacheRegistry`. Minimal `willPresent`/`didReceive`-tap now; in-app feed/badge → **T-0336**; the `aps-environment` entitlement (no plist key); delivery owner-gated → **T-0342** (sprint-12 §7.13, reviewer #34) |
| `customer-app/.../core/auth/GoogleSignInController.kt` — provider acquisition returns a **typed `GoogleSignInResult`** (`Success(idToken, googleId, email, first, last) \| Cancelled \| NoAccount \| NotConfigured \| Failure`), **never navigates**, swallows-and-logs cancel/no-account; the VM maps the result → `AuthOutcome` then the repo's `googleAuth` POST | a Core **`SocialSignInProviding`** protocol (`CleansiaCore/Auth`) returning a typed **`SocialSignInResult`** (`.google(GoogleCredential) \| .apple(AppleCredential) \| cancelled \| noAccount \| notConfigured \| failure`) — fakeable, so the VM unit-tests against fakes (no live provider). The **acquisition impls are APP-LOCAL** in `CleansiaCustomer` (partner offers no social login — an ADR-0013 D3 split): `AppleSignInController` (`#if canImport(AuthenticationServices)`, the SOLE AuthenticationServices consumer — generates a crypto-random raw nonce, sets `request.nonce = SHA256(rawNonce)` HASHED to Apple, returns the **RAW** nonce to the backend; `.fullName`/`.email` scopes; name only on first authorization) + `GoogleSignInController` (`#if canImport(GoogleSignIn)`, the SOLE GoogleSignIn consumer — `serverClientID` = backend `Google:ClientId`, empty config → `.notConfigured` FAIL-SAFE, no crash). The seam keeps both first-party frameworks behind the protocol (the `PushRegistrar`/`CameraOrLibraryPicker` seam-family, with `#else` no-op fallbacks so Core/tests compile without the SPM dep). **Consumption = the Core spine:** two new `AuthApiClient` methods `googleAuth`/`appleAuth` (hand-written request DTOs, anon `noAuthSession`/no Bearer, `/api/Auth/{GoogleAuth,AppleAuth}`) that **reuse the SAME `resolveEmailGate` + single Keychain `persist`** — ~10 lines each, **NO parallel social token-write path** (a finding). The official `ASAuthorizationAppleIDButton` (via a `UIViewRepresentable` driving the seam, NOT SwiftUI's built-in request handler) is **first**, the Google button **second**, below the Core **`LabelledDivider`** (reused, NOT re-declared per app) `OR` divider on SignIn + SignUp (AR-ACCT-2/4.8). The Google button is a CUSTOM outlined label rendering the **real multicolor Google "G"** brand mark (a vector-PDF `google_g` imageset in the customer assets, `renderingMode(.original)` so it stays 4-color) + the localized "Continue with Google" — Google branding REQUIRES the official "G", NOT an SF Symbol. The provider snackbars are provider-**neutral** (`auth_social_*`, ×5) since Apple + Google share the `.noAccount`/`.notConfigured`/`.failure` branches. LIVE sign-in owner-gated → **T-0344** (Apple capability + `Apple:BundleId`) / **T-0345** (Google client ids); the `com.apple.developer.applesignin` entitlement + the GoogleSignIn-iOS SPM dep + the reversed-client-id URL-scheme **slot** (placeholder) ship now (sprint-12 §7.14 D6 / §7.15 D2/D3/D6, T-0312 Slice C) |

**Generated-client auth — the ONE way (ADR-0019, reviewer #13-gen):** authenticate the generated business
client **only** through the Core-spine-backed `RequestBuilderFactory` (above). **Deviations a reviewer
rejects:** (a) a **second token source** — the app-side generated wrapper or a call site reading `TokenStore`,
setting `Authorization`/`Bearer`, or writing a Bearer into `customHeaders` (the Bearer is set in **exactly
one** place, the `HeaderAdapter`); (b) **per-call header injection** — `.addHeader(...)` for auth/device
headers at a call site/wrapper (headers are stamped uniformly by the `HeaderAdapter`); (c) **per-call 401
handling** — a call site catching a 401 and refreshing itself (the single-flight refresh is the factory's,
once, for all). Authentication is decided by the injected `AnonymousAllowList`, **not** the generated
`requiresAuthentication` flag. T-0303 proves it; every later authed wave installs the same factory per host and
writes no auth code.

**Partner router — the ONE way (ADR-0020, reviewer #23):** the partner app's **top-level audience** (logged-out
/ resolving / locked / in-shell) is the **flat-enum `PartnerRootView` root-switch** — a closed `enum Route`
(`.splash`/`.login`/`.verifyEmail`/`.registrationLock`/`.dashboard`-shell) the root view `switch`es over,
seeded `hasValidSession ? .splash : .login`, where a **verified login bounces through `.splash`** (which
re-resolves shell-vs-lock — the Android `PartnerNavHost.kt:118-124` idiom). `NavigationStack` is the
**intra-audience** push container, NOT the audience selector. **Deviations a reviewer rejects:** a top-level
audience state modeled as a pushed `NavigationPath`; a seed of `.dashboard` (the fail-open hole — must be
`.splash`); a verified login routing straight to `.dashboard` (bypassing the gate). The customer app copies
the *pattern* (its own root view + audience states), not the partner enum.

**Partner registration gate — fail-closed (sprint-12 §7.4 Decision 1, reviewer #24, SECURITY):** the gate sits
**between login and the shell** (the shell is unreachable until complete). The predicate is the **AND** of
`hasCompletedProfile == true && areDocumentsUploaded == true && contractStatus ∈ {Approved(4), Active(2)}`
(`RegistrationLockViewModel.kt:103-109`) — **any nil/unknown/other → LOCKED**; availability is NOT a clause.
**Both error paths fail CLOSED:** the SplashGate routes a status-API `.failure` to the lock (never the shell);
the lock VM's `.failure` preserves the cached status and never unlocks (only the success "complete" watermark
unlocks). **Deviations a reviewer rejects:** a permissive optional default (`?? true`) on any gate field; a
`.failure` reaching the shell; a `.failure` clearing/unlocking the gate. Later partner waves render inside the
shell (past the gate) and must not add a second, weaker status check.

**Device-local settings — the ONE way (sprint-12 §7.5 Decision 1, reviewer #26a):** all device-local
(wiped-on-uninstall) preferences go through a **single, general `AppSettingsStore` in `CleansiaCore`,
backed by `UserDefaults`** (the `AppSettingsRepository.kt` DataStore parity — `partner_app_settings`,
`onboarding_seen`, `language`). It exposes `hasSeenOnboarding` (get + `markSeen()`) + a resolved language tag
∈ `{en,cs,sk,uk,ru}` (persisted-if-in-set → `Locale.current.language.languageCode`-if-in-set → `"en"`).
**Deviations a reviewer rejects:** a single-purpose `OnboardingStateStore` (or per-pref stores) instead of the
one general store; **secrets (token, device id) in `AppSettingsStore`** or **settings (onboarding-seen,
language) in the Keychain** — the Keychain holds only the security-load-bearing device id + the session
(header-parity §2/§6), `UserDefaults` holds non-secret prefs that must reset on reinstall (DataStore parity).

**Top-level audience state may carry a payload (ADR-0020 fold-in, sprint-12 §7.5 Decision 2, reviewer #26b):**
a flat-enum `Route` case may take an associated value when a nav input must reach the destination — e.g.
`case verifyEmail(email: String?)` threads the ConfirmEmail resend email (the iOS analogue of Android reading
it from `UserProfileStore`, which iOS does **not** build). **Do NOT** stand up a `UserProfileStore` to carry a
single nav input; the associated value is the seam. The router still only *lands* the state — it does not
interpret the payload. A cold-start into a payload-less case (`.verifyEmail(nil)`) degrades gracefully
(disable the action needing it + show `error_generic`); the existing `requiresEmailConfirmation==true →
.verifyEmail` gate is preserved (the payload is additive).

**Client-side password policy — the ONE way (sprint-12 §7.5 Decision 4, reviewer #26c):** the register/sign-up
password rule (**≥8 && ≥1 letter && ≥1 digit** — `RegisterViewModel.kt:37-39`) is a Core
**`PasswordPolicy`** validator in `CleansiaCore/Validation` (the `EmailValidator` factoring), rendered by a
Core **`PasswordRuleList`** component (the already-shared `:core` `PasswordRuleList.kt` parity — neutral /
green-check / red-cross rows + a `hasInput` flag). Partner (now) and customer (T-0312+) import both from Core.
**Client-side UX only — the backend `BaseAuthValidator` is authoritative.** **Deviations a reviewer rejects:**
a VM-local copy of the predicate (the Android `RegisterUiState`-getter smell, lifted to Core on iOS); a
per-app password-rule widget instead of the Core component.

**Per-host register endpoint — the ONE way (sprint-12 §7.15 Decision 3, T-0312 Slice B):** the shared
`AuthApiClient` register path differs per audience — **partner self-registers cleaners at
`api/Auth/RegisterEmployee`, customer self-registers at `api/Auth/Register`**. This is a **construction-time
`RegisterEndpoint` parameter** on the one `AuthApiClient.init` (default `.employee` so the partner factory +
every existing call site stay byte-equivalent; the customer factory passes `.customer`) consumed by the
**single** `register(...)` method — NOT a second method, a `RegistrationAuthClient` fork, or a per-app
subclass. The hand-written body (`RegisterRequest`: email/password/firstName/lastName/language) serializes
identically for both endpoints (the customer `Register` accepts the partner field set; `referralCode` is
omitted → null, a T-0314 concern). **Deviations a reviewer rejects:** a parallel `customerRegister(...)`
method (forks the one register code path); hard-coding either path in `register(...)`; the body diverging
between hosts. **Guard tests:** the customer client targets `/api/Auth/Register` (NOT `RegisterEmployee`,
no Bearer) AND the default stays `RegisterEmployee` (the partner byte-equivalence proof).

**The event-driven customer auth VM — the ONE way (sprint-12 §7.15 Decision 2, T-0312 Slice B; the Android
`AuthViewModel` parity):** the customer auth surface (SignIn/SignUp/EmailVerify/ForgotPassword) is driven by a
`CustomerAuthViewModel` that **emits an `AuthOutcome` (`signedIn` / `needsEmailConfirm(email)` /
`passwordReset`) via a `PassthroughSubject` and NEVER navigates** — `CustomerRootView` maps the outcome to a
route (`signedIn → .home`, `needsEmailConfirm(email) → .verifyEmail(email:)`, `passwordReset → .login`) in a
static `afterAuth(_:)` (unit-testable without a view). The email rides the `.verifyEmail(email:)` associated
value (the §7.5 D2 seam above), not a store; `needsEmailConfirm` is the surfacing of the **empty-token gate**
(`200 + empty/blank token` or `isEmailConfirmed == false` → verify, never `.home` or an error — the shared
`resolveEmailGate` in the Core spine, reused, never reimplemented). The four screens may each own a VM sharing
the one outcome contract (the partner per-screen `loginSuccess`-subject pattern, generalized). **Social (Google/Apple)
joins this contract identically** (T-0312 Slice C): the app-local `SocialSignInProviding` acquisition hands a typed
`SocialSignInResult` to the VM → the spine's `googleAuth`/`appleAuth` (which reuse the SAME `resolveEmailGate` + single
`persist`) → the SAME `AuthOutcome` (Apple's verified users → `signedIn`; an empty/unverified token → `needsEmailConfirm`).
Cancel is silent (no toast/no outcome); `.notConfigured`/`.failure`/`.noAccount` snackbar without touching the spine.
**Deviations a reviewer rejects:** the VM calling navigation directly; a social/parallel token-write path bypassing the one
Keychain `persist` (the social methods MUST route through `resolveEmailGate`, never a second persist); an empty-token success
landing `.home`; the acquisition controller navigating or touching the Keychain/gate; a stored Bearer attached on the anon
`/api/auth/{googleauth,appleauth}` routes.

**iOS maps — the ONE way (sprint-12 §7.6, T-0306; ADR-0013 D6 + ADR-0014 D6′ + ADR-0018 Gate-DP, reviewer
#7/#12/#27):** all map + geocode use goes through `CleansiaCore/Location`'s **`MapProvider`** /
**`GeocodingService`** protocols; the **only** sanctioned MapKit/CoreLocation consumers are the
`MapKitMapProvider`-produced view + `CLGeocoderGeocodingService` — **feature/VM code imports neither MapKit
nor CoreLocation** (reviewer #7/#27). The seam ships **minimally and grows additively:** T-0306 ships the
**picker-map factory only** (iOS-16 `Map(coordinateRegion:annotationItems:[])` + a SwiftUI overlay pin the
map pans under — NO `Map{Marker}`/`onMapCameraChange`, reviewer #12); T-0307's full-bleed `OrderDetail` map +
service-area polygon overlay are an **additive method** later (`MKMapView`/`UIViewRepresentable`), **not**
designed ahead. `GeocodingService` is a 1:1 `ReverseGeocodingService.kt` port **minus the Mapbox token +
network args** — **best-effort** (nil/`[]` on error, **cancel-before-refire** for `kCLErrorGeocodeCanceled`,
never block the confirm/crash — the `runCatching{}.getOrNull()` parity), debounce **300ms forward / 500ms
reverse** ported VERBATIM (the `CLGeocoder` rate-limit guard; iOS-16 reverse-on-idle is a VM-owned
Combine/`Task` debounce, not a map callback). The **AddressPicker has NO `UiState<T>`/`ActionState`** — it is
an interactive map with plain `@Published` state + a one-shot `onConfirmed(GeocodedAddress)` callback, neither
an E1 load-fetch nor an E2 mutation screen — so the **sealed-state absence is correct, not a finding**
(reviewer #27). **Current-location/the my-location FAB are DEFERRED out of T-0306** (the `LocationProvider`/
`CLLocationManager` seam + the FAB home to T-0310, gated on T-0325's `NSLocationWhenInUseUsageDescription`
plist key — owner); T-0306 centers on the **Prague default** + ships **pan-to-place + search at full parity**.
This is the recorded **Gate-DP divergence** (iOS omits current-location pending T-0325; pan/search parity full;
the divergence touches a deferred affordance, **not** layout/flow/branding). **No Mapbox token, no map SDK, no
`Package.swift` change** — a **net reduction** in secret surface vs Android's `MAPBOX_ACCESS_TOKEN` BuildConfig;
`MapStyles.kt` is NOT ported (stock MapKit style is the baseline; Q-IOS-02 stays "No"). **Deviations a reviewer
rejects:** a feature/VM `import MapKit`/`import CoreLocation`; a second MapKit consumer outside the providers;
the iOS-17-only `Map{Marker}`/`MapPolygon`/`onMapCameraChange` API (#12); a hand-rolled per-feature geocode/
debounce instead of the Core `GeocodingService`; building the my-location FAB before T-0325's plist key exists
(a dead control); flagging the missing `UiState`/`ActionState` on the picker.

**iOS partner Profile tab — the ONE way (sprint-12 §7.7, T-0310; ADR-0020 + §7.5 D1 + §7.6 D2 + ADR-0018 Gate-DP +
the Parity rule, reviewer #28):** the Profile tab hosts an **in-tab `NavigationStack` over a typed `ProfileRoute`
enum INSIDE the `.dashboard` shell** — the ADR-0020 **intra-audience push** (the root `enum` stays the audience
selector). `ProfileRoute` = the `NavRoutes.kt:54-91` push routes minus the audience cases; the four **gate** sections
(`.personal/.address/.identification/.bank`) carry an `onboarding: Bool` payload, Emergency/Documents do not; the
AddressPicker is a `.sheet`/`.fullScreenCover` return-value flow (`onConfirmed`), **not** a route. **The RegistrationLock
(a ROOT audience state, NOT in the shell) owns its OWN local `NavigationStack` + onboarding-chain VM and pushes the
SHARED section set over ITSELF with `onboarding == true`** (the Android `popUpTo(RegistrationLock){inclusive=false}` +
`ON_RESUME` re-resolve parity, `OnboardingChainViewModel.kt:86-121`); on pop the lock re-resolves and **only** the
success watermark flips the root to `.dashboard` — **fail-CLOSED, no cross-audience routing into the shell's Profile
tab** (composes with the registration-gate #24). **Section screens are ONE set of Views/VMs hosted by TWO stacks**
(Profile tab = maintenance edits, `onboarding == false` → pop on save; lock = onboarding chain, `onboarding == true` →
chain forward), the `onboarding` flag the **only** switch. **Device-local settings** read/write the one `AppSettingsStore`
(extended with writable `setLanguage` + a `Theme` enum + `setTheme`, the `AppSettingsRepository.kt:37-51` parity —
UserDefaults, NOT Keychain, #26a); the **theme is honored via `.preferredColorScheme` on the root** (`.system`→`nil`).
**The Profile hub + each section load are sealed `UiState<T>`; the save is `ActionState` + a one-shot effect** — Android's
`ProfileUiState`/`*UiState` **flag-bags (E1) are NOT replicated** (the Parity rule: Android-wrong → diverge correctly,
raise the finding — android fix **T-0337**); every validation/error string is an `.xcstrings` key ×5 (NOT the Android
hardcoded literals). **Deviations a reviewer rejects:** a section push modeled as an audience hop (or the audience as a
`ProfileRoute`); a Fix CTA that renders/routes into the shell's Profile-tab stack (a fail-OPEN shell reach before
complete); a second forked copy of a section View/VM for the lock; a second settings store (or theme/language in the
Keychain); a ported flag-bag `…UiState` struct or a hardcoded validation string; building the my-location FAB before
T-0325's plist key exists (DEFERRED → T-0335 — a dead control); a "Notifications" prefs row/screen in the partner
Profile hub (DROPPED — no backend contract; the Preferences group is Language + Theme + Devices, the
`ProfileScreen.kt:183-204` parity). **Deferred (NOT findings — recorded Gate-DP divergences):** the advisory
`ServiceAreaRow` (→ T-0334); the current-location FAB (→ T-0335, gated on T-0325). The **Device/Mine list + revoke**
screen is **SECURITY-ruled** (decisions 6–8), out of this rule's scope.

**iOS partner order work-loop — the ONE way (sprint-12 §7.9, T-0307; ADR-0021 + ADR-0013 D6/D9 + ADR-0014 D2′/D6′ +
ADR-0018 D3 + §7.6 D1 + §7.7 D5 + the Parity rule; reviewer #29/#30/#31):**
- **The full-bleed `OrderDetail` map** is the **additive `MapProvider.fullBleedMap(coordinate:)` method** —
  `MKMapView`/`UIViewRepresentable` inside `MapKitMapProvider`, **ONE address pin, camera bottom-padded for the sheet**,
  **NO** overlay/polygon param (no polygon data in the partner spec; Android renders none; overlay is additive IF
  T-0334 ever has geometry). The §7.6 D1 minimal-now/additive-later seam — feature/VM import no MapKit (#7/#12/#30).
- **The OrderDetail sheet** is the **custom non-modal `SnapSheet` Core container** (`GeometryReader`+`DragGesture`, 3
  snap offsets map-focus/peek≈0.75/expanded, layered over `fullBleedMap`) — **ADR-0021**, **16.0-safe** (no
  `.presentationDetents`; the floor STAYS 16.0). **NOT a modal `.sheet`** (that changes the layout — Gate-DP D1 failure).
  Native `.sheet`+`.presentationDetents` stays the way for **modal** sheets (the customer booking sheet); the
  discriminator = *modal-over-a-screen* (native `.sheet`) vs *non-modal-over-a-live-backdrop* (`SnapSheet`) (#29).
- **The primary lifecycle action** is the **pure shared `OrderPrimaryAction.action(for:isMine:hasAfterPhotos:)`** sealed
  enum (one tested function, three call sites — NOT inline switches), mirroring `OrderPrimaryAction.kt`'s table; it is
  **presentational** and consumes `isMine`/`hasAfterPhotos` — the **ownership trust is SECURITY §7.8 (O1–O4)**, not this
  function (#31). The OrderDetail VM is the sealed `OrderDetailUiState` + `ActionState` + an `OrderAction?` in-flight
  (already canonical on Android — ported 1:1).
- **`orderStatus` is a `Code` envelope** on the read-path DTOs (`OrderItem`/`OrderListItem`) — map it to the typed
  `OrderStatus` in **one** `Code.toOrderStatus()` extension (`value.flatMap(OrderStatus.init(rawValue:))`); no raw-`Int`
  `.value` compares, no second mapper (the action responses already carry the typed enum) (#31).
- **The OrdersList** is **sealed per-pane `UiState<[OrderListItem]>` + a `RefreshPhase` enum** (`idle`/`userRefreshing`/
  `backgroundRefreshing`; PTR fires `==.userRefreshing` ONLY — the silent-stale parity), **NOT** the Android
  `OrdersListUiState` E1 flag-bag (Android fix → T-0337). The **per-pane/per-order `Staleness` cache is PORTED** (~30s
  watermarks + `invalidatePanesFor(mutation)`, registered in the `SessionScopedCacheRegistry`) — load-bearing for
  no-flash resume; simplifying to load-on-appear+`.refreshable` is an **un-approved** behavior divergence (#30). The
  inline commit affordance is **iOS-native** (`SlideToCommit`→native confirm/`swipeActions` — the noted Gate-DP swap).
- **The photo slot is a precursor seam:** T-0307 renders a **disabled/placeholder** Photos section (visibly disabled,
  not a dead control) + derives `hasAfterPhotos` (feeding `.complete`/`.completeBlocked`); **T-0308 fills capture
  additively** (no OrderDetail re-layout). **Deviations a reviewer rejects:** a modal `.sheet` for OrderDetail; a
  2-anchor collapse without the noted+re-approved ADR-0021 fallback; a `fullBleedMap` overlay/polygon param with no
  data; a ported `OrdersListUiState` flag-bag; PTR on background refresh; dropping the staleness cache un-approved;
  an inline per-site action switch; a raw `orderStatus.value == N` compare; a feature `import MapKit`.

**iOS partner photos — the ONE way (sprint-12 §7.10, T-0308; ADR-0018 D2/D3 + ADR-0016 AR-PRIV-4 + ADR-0013 parity +
the Parity rule; reviewer #32):** the partner order Photos surface (camera/library capture → base64-over-JSON upload,
read-back, delete, the After-photo Complete-unblock) fills the §7.9 (d) precursor slot.
- **Capture seam:** photo capture goes through the Core **`CameraOrLibraryPicker` `UIViewControllerRepresentable`**
  (`CleansiaCore/Components`) wrapping a camera-capable `UIImagePickerController` — **the repo's FIRST
  `UIViewControllerRepresentable`** + the canonical "imperative-UIKit-controller-behind-a-SwiftUI-seam" idiom (the
  *controller* analogue of the `MKMapView`/`UIViewRepresentable` *view* seam; both ADR-0018 D2 brand-skins). The single
  Add tile → a native `.confirmationDialog` (Take Photo / Choose from Library). iOS adds **camera + library** over
  Android's **gallery-only** (`GetContent`) — the recorded Gate-DP enhancement divergence.
- **Compression:** the upload runs through a pure Core **`ImageCompressor`** (downscale longest-side ≤1920px
  aspect-preserved + JPEG **0.7** + `image/jpeg`, OFF the main thread) before base64 — an iOS PERF divergence from
  Android's raw bytes (`PhotosSection.kt:155-159`). A bounded pure helper → strict TDD, no optimizer pass.
- **Read-back + gate:** thumbnails render with SwiftUI **`AsyncImage`** (the Coil→`AsyncImage` swap, no 3rd-party
  dep); the Complete gate consumes the **RE-FETCHED `OrderItem.hasAfterPhotos`** (`OrderDetailScreen.kt:558`), kept
  live by the post-mutation parent refresh (the `mutationVersion`→`onContentMutated` parity) — **NOT**
  `GetOrderPhotosResponse.afterPhotoCount`. Single-photo upload via the batch-of-one `orderSavePhotos`; delete via
  `orderDeletePhoto(photoId)`. Upload windows: `canUploadBefore = status ∈ {_3,_4}`, `canUploadAfter = status == _4`;
  terminal orders read-only. `PhotoType._1 = Before`, `._2 = After`.
- **Plist:** `NSCameraUsageDescription` + `NSPhotoLibraryUsageDescription` land **in-ticket** in the **PARTNER**
  `project.yml` `info.properties` (the `API_BASE_URL`/`UIAppFonts` precedent), localized ×5 via `InfoPlist.strings`,
  describing the real use (AR-PRIV-4). **Partner-only now; the Customer app carries its own at T-0314.** The
  `PrivacyInfo.xcprivacy` photos data-type is declared (AR-PRIV-1).
- **Deviations a reviewer rejects:** a feature/VM hand-rolling `UIImagePickerController`/AVFoundation outside the Core
  seam; a PHPicker-only (no-camera) picker; raw/un-downscaled bytes base64'd (the Android shape copied — the
  un-approved perf divergence); main-thread base64 encode; a 3rd-party image lib for the partner thumbnails; the
  Complete gate computed off `afterPhotoCount`/any client photo count instead of the re-fetched `hasAfterPhotos`; an
  upload/delete that doesn't bump the parent order refresh (so `hasAfterPhotos` goes stale); a deferred/owner-manual
  plist key, a missing/generic/non-localized purpose string, or the keys pre-added to the Customer `project.yml` before
  T-0314. **The photo-upload OWNERSHIP / EXIF-strip gate is SECURITY's (`security/ios-orders.md`) — not this rule.**
- **Catalog correction (the false `UIViewControllerRepresentable` precedent):** the **AddressPicker (T-0306) is pure
  MapKit/SwiftUI** — `Map(coordinateRegion:annotationItems:[])` + a SwiftUI overlay pin + `CLGeocoder`/`MKLocalSearch`
  — and uses **neither** a `UIViewControllerRepresentable` **nor** a `UIViewRepresentable`. Any claim it established a
  representable precedent is **FALSE**; `CameraOrLibraryPicker` (T-0308) is the repo's **first**
  `UIViewControllerRepresentable` (the `MKMapView`/`UIViewRepresentable` `fullBleedMap`, T-0307, is the first *view*
  representable). Do not cite the AddressPicker as a controller-representable precedent.

**iOS partner earnings/invoices/PeriodPay — the ONE way (sprint-12 §7.12, T-0309; ADR-0020 + §7.7 D1 + the §7.10 D1
Core-seam precedent + ADR-0018 D2/D3 + the §7.5 D4/§7.7 D4 Core-utility precedent + ADR-0013 parity + the Parity rule;
reviewer #33):** the partner earnings surface (Earnings summary, invoices list + detail + PDF, PeriodPay) over the
generated `PartnerEmployeePayrollAPI` — all on the ADR-0019 spine. **All four rulings APPLY accepted ADRs/records — no new
ADR.**
- **Nav (a):** the partner shell's **`.invoices` tab IS the surface** — it roots an **in-tab `NavigationStack` over a
  typed `EarningsRoute` enum** (`.summary`/`.invoices`/`.invoiceDetail(id)`/`.periodPay(payPeriodId,currencyCode)`),
  **landing on `.summary`** (the Earnings summary — Android built it specifically to avoid the empty-invoices-list
  landing, `EarningsSummaryScreen.kt:56-66`). This is the **ADR-0020 D4 / §7.7 D1 intra-audience push** (the root
  `PartnerRootView` enum stays the audience selector). The Dashboard's `onOpenEarnings` sets `ShellModel.selection =
  .invoices` — a **tab switch** (the `selectOrders()`/`onOpenOrders` parity), **NOT** a push. **Recorded Gate-DP
  divergence** (same class as the T-0304 `MainScaffold`→`TabView` swap): Android Earnings(pushed)+Invoices(tab) → iOS
  single tab + in-tab stack; same nav structure/content/back-stack order, native mechanism. `.periodPay` carries
  `currencyCode` (the `PeriodPaySummary` DTO has none — `PeriodPayViewModel.kt:43-44`).
- **PDF (b):** invoice PDF viewing goes through the **Core `QuickLookPreview`** (`QLPreviewController`
  `UIViewControllerRepresentable` in `CleansiaCore/Components`) — the **2nd member of the §7.10 D1 `CameraOrLibraryPicker`
  family**, reused by the customer app (T-0314) so it lives in Core. The generated swift5 `employeePayrollDownloadInvoice`
  **writes the body to disk → returns a local file URL**; the VM surfaces it via a **one-shot event** → the screen
  presents `QuickLookPreview`. The "Open PDF" affordance is **gated on `pdfGenerationFailed`**. **NO `FileDownload`
  seam** (the codegen IS the download). **The previewed PDF is deleted from cache on dismiss — SECURITY E4
  (`security/ios-earnings.md`)**, hosted by the coordinator. **Rejected:** a partner-local representable; a share-sheet
  (export, not a viewer); `SafariView` (web URLs, not a `file://` PDF).
- **Format (c):** money/date go through a small **Core `EarningsFormat`** (`formatMoney` `%,.2f` decimal for
  invoices/PeriodPay + `formatMoneyWhole` `%,.0f` for the earnings headline + ISO→local dates), reusing the
  **currency-symbol resolution HARVESTED to Core** (≥3 call sites — never-crash raw-`code` fallback). **Do NOT overload
  `DashboardFormat.money`** (it is `%.0f` ungrouped — the dashboard hero's contract, neither earnings format).
- **Stats (d):** the Earnings summary **REUSES `PartnerDashboardClient.getStats`** (the `DashboardStatsDto` the Dashboard
  hero renders — `EarningsSummaryViewModel.kt:23-32,49`), NOT a payroll-client duplicate or a `GetPeriodPays`-derived
  summary. `employeeId = null` (server scopes to the caller — the no-`UserProfileStore` fact §7.5 D2; the read-scoping
  trust is SECURITY's).
- **List state:** sealed per-list `UiState<[EmployeeInvoiceDto]>` + a `RefreshPhase` enum (the §7.9 (e) convention) + the
  PORTED my-invoices staleness watermark — the Android `InvoicesListUiState` **E1 flag-bag NOT replicated** → Android fix
  **T-0337**.
- **Deviations a reviewer rejects:** the earnings surface modeled as a pushed screen off the Dashboard tab, or the tab
  landing on the invoices list (the empty-landing Android removed); a partner-local `QLPreviewController` wrapper, a
  share-sheet/`SafariView` substituted for the viewer, a built `FileDownload` seam, an Open-PDF affordance not gated on
  `pdfGenerationFailed`, a VM re-streaming the body to disk; per-screen private money/symbol/date copies, or overloading
  `DashboardFormat.money`; a second stats fetch on the payroll client or a `GetPeriodPays`-derived summary; a ported
  `InvoicesListUiState` flag-bag. **The read-scoping / PII gate (own-id-only + the post-preview PDF cache-cleanup) is
  SECURITY's** (`security/ios-earnings.md`) — not this rule.
- **Parity catch-ups (Android is thin → iOS does it right, file the Android follow-up):** Android renders Open-PDF
  unconditionally (no `pdfGenerationFailed` gate) and hand-wrote a `PeriodPayApi` Retrofit interface (the spec didn't
  carry `GetPeriodPays` at the time — `PeriodPayApi.kt:8-18`); iOS gates the affordance off the flag and uses the
  **generated** `employeePayrollGetPeriodPays` (the regen'd spec now carries it). Both Android catch-ups are PM-filed
  follow-ups, independent of the iOS wave.

**iOS push — the ONE way (sprint-12 §7.13, T-0311; ADR-0013 D8 + ADR-0014 D6′ + ADR-0018 D2 + ADR-0019 + the
`SessionScopedCacheRegistry`; reviewer #34):** APNs push **registration + token plumbing + device lifecycle + a
minimal foreground/tap** — the well-factored Android `:core` push ported over APNs. **All rulings APPLY accepted
ADRs/records — no new ADR.** (The in-app feed / bell badge / persistence / templates / channels are DEFERRED →
**T-0336**.)
- **Seam (a):** a **`PushRegistrar`** protocol in `CleansiaCore/Push` is the **SOLE** consumer of
  `UNUserNotificationCenter` + `UIApplication.registerForRemoteNotifications` — feature/lifecycle code **imports neither
  `UserNotifications` nor `UIKit`** (the `MapProvider`/`GeocodingService` / `CameraOrLibraryPicker`/`QuickLookPreview`
  seam-family — ADR-0014 D6′ system-framework-behind-a-protocol + ADR-0018 D2 brand-skin-over-native). It exposes
  **`requestAuthorization`** (the `POST_NOTIFICATIONS` parity), **`registerForRemoteNotifications`** (main-actor), and an
  **APNs-token stream the AppDelegate feeds** (the `PushTokenRepository.fcmToken: StateFlow<String?>` parity, fed
  out-of-band by the OS callback). The APNs-token AppDelegate callbacks
  (`didRegisterForRemoteNotificationsWithDeviceToken`/`didFailToRegister`/`willPresent`/`didReceive`) are received via a
  **per-app `@UIApplicationDelegateAdaptor`** (the canonical SwiftUI AppDelegate bridge — SwiftUI's `App` has no native
  push hook) that **only forwards** into the Core registrar/deep-link — the one allowed `UIKit`/`UserNotifications`
  touch-point (the App-target composition-root parity, like installing the `RequestBuilderFactory`/`MapProvider`).
- **Lifecycle (b):** register/clear is a Core **`PushSessionObserver`** — the `PushTokenSessionObserver.kt` parity:
  **registration is a PROPERTY of session×token state, not an event** —
  `combine(session, token).filterNotNull().distinctUntilChanged() → ensureRegistered` (`:56-64`), attached once from the
  App (the `MainActivity.onCreate` parity). `ensureRegistered` **short-circuits on the persisted last-registered token**
  (`UserDefaults`, NOT Keychain — the `PushTokenDataStore` parity; not a secret) and **persists on success only**.
  **`unregisterDevice()` is invoked from `AuthApiClient.logout()` BEFORE the `TokenStore` wipe** (best-effort — the
  `Device/Unregister` DELETE needs the Bearer; the `AuthRepository.kt:210-225` ordering) and the local `clear()` is the
  **`SessionScopedCache`** run by the registry on **both** sign-out paths (user logout + forced-401). **The
  unregister-ordering GATE is SECURITY's (Gate-SEC) — this rule fixes the seam + the invocation HOME, not the mandate.**
- **Scope/permission (c):** **minimal** `willPresent` (foreground banner) + `didReceive` (tap → existing order route via a
  **`PartnerNotificationDeepLink`** port) only. **NO Info.plist purpose string** — APNs needs only the **`aps-environment`
  entitlement** + the runtime `requestAuthorization` (the OS shows its own alert; notifications has no plist key, unlike
  location/camera/photo). **Skip the rationale string** (strict parity — Android requests `POST_NOTIFICATIONS` silently;
  the one optional soft-ask `.xcstrings` key ×5 is the recorded, un-built fallback). **NO `UiState`/`ActionState`** —
  fire-and-forget background plumbing; the **sealed-state ABSENCE is correct** (the §7.6 D3 AddressPicker precedent — do
  NOT flag it).
- **Recorded Gate-DP divergence (ADR-0013 D8):** *Android FCM (`FirebaseMessaging` token + the messaging-service
  `onNewToken`) → iOS APNs (`registerForRemoteNotifications` + the `@UIApplicationDelegateAdaptor` `didRegister…DeviceToken`
  + `UNUserNotificationCenter`); the SAME `Device/Register`/`Device/Unregister` contract, **`Platform="ios"`**, the one
  `X-Device-Id` (== `DeviceIdProvider`); the mechanism is the native platform push transport, the contract + register/clear
  lifecycle are identical.* (No Firebase-project-migration analogue — `runFirebaseProjectMigrationOnce` is FCM-specific,
  correctly NOT ported.)
- **Owner gate:** end-to-end delivery (a push arriving on a device) needs the owner's **APNs `.p8` key + Push capability +
  provisioning** — filed as **T-0342** (NOT "T-0341", which is taken). T-0311 ships **code-complete + the `aps-environment`
  entitlement**; delivery is owner-gated (the **T-0325-gates-T-0335** pattern).
- **Deviations a reviewer rejects:** a feature/VM/lifecycle file `import UserNotifications`/`import UIKit` for push (the
  registrar is the only consumer); a second push consumer outside `PushRegistrar`; a hand-rolled
  `UIApplication.shared.delegate` instead of `@UIApplicationDelegateAdaptor`; registration bolted onto `afterLogin`/an
  event hook instead of the session-state observer (the brittleness the Android `:core` deleted); the last-registered
  token in the Keychain (or a secret in its `UserDefaults` store), or a device id not from the one `DeviceIdProvider`;
  `unregisterDevice()` AFTER the token wipe or skipped on a logout path; a second clear-path not via the
  `SessionScopedCacheRegistry`; the device token POSTed with anything but `Platform="ios"`; an in-app feed/bell
  badge/push persistence built in T-0311 (it is T-0336); a notifications Info.plist purpose string (a non-existent
  requirement) or a missing `aps-environment` entitlement; a flagged-as-missing `UiState`/`ActionState` on the
  registrar/observer (the §7.6 D3 mis-fire). **The registration / logout-clear-ordering SECURITY gate is parallel
  (Gate-SEC) — not this rule.**

**iOS customer booking sheet — the ONE way (sprint-12 §7.16, T-0313; ADR-0018 D3 modal mapping + ADR-0021 D3
modal/non-modal discriminator; the HARD AREA #1 wizard):** the Bolt-style 3-step booking wizard
(`BookingBottomSheet.kt`) is a **modal** anchored sheet → native SwiftUI **`.sheet` + `.presentationDetents`**
(the customer `BookingSheetView`, presented from `CustomerShellView.book()` — the now-live Book FAB), **NOT** the
partner `SnapSheet`. The discriminator (ADR-0021 D3, reviewer #29) is *modal-over-a-screen* vs
*non-modal-over-a-live-backdrop*: the booking sheet dims the screen behind it / drags to dismiss / has no live map,
so it is the native modal — `SnapSheet` is reserved for the partner OrderDetail over `fullBleedMap`. The Android
`AnchoredDraggableState` 4-anchor draggable maps to `.presentationDetents([.large])` (the sheet opens near-full,
mirroring `animateTo(Full)`) + `.presentationDragIndicator(.visible)` (the Compose drag-handle pill). The shared
**`BookingViewModel`** is the Core `ViewModel` base (`ObservableObject`/`@Published`, NOT `@Observable`) mirroring
the Android **5 StateFlows** — `state` (an immutable `BookingState` value rebuilt via `update { copy }`),
`submitState` (the Core `ActionState`), `quoteState`/`promoState`/`referralState` (sealed enums, the
`QuoteState`/`PromoCodeUiState`/`ReferralCodeUiState` parity) — plus the sealed **`BookingSubmitOutcome`**
(`.success`/`.cardPending`/`.failed`/`.profileIncomplete`, the Android parity). **Step nav** (`currentStep` 1…3 +
`advance`/`back` + `reset`) lives on the VM; the **per-step `canContinue` gates live in the VIEW** as a pure
**`BookingStepGate.canContinue(step:state:)`** helper (the Android composable-gate parity — step1: ≥1 service OR
package AND rooms≥1; step2: street+date+time; step3: paymentMethod), strict-TDD'd. The back-arrow decrements / the
`xmark` closes on step 1 (`if !vm.back() { onDismiss() }`). **The VM holds no navigation** — the sheet's
`isPresented` lives on the shell model. **Deviations a reviewer rejects:** a `SnapSheet`/`GeometryReader`+drag
re-impl for the booking sheet (it is the native modal); a `canContinue` baked into the VM instead of the pure
view-consumed gate; navigation driven from the booking VM; an `@Observable` booking VM; a flag-bag booking state
instead of the value + sealed states. (Slices B/C/D/E fill the step bodies + server pricing + cash/card + the
Stripe seam; Slice A is the scaffold + step nav + the 5-state shape, no network.)
**Slice B (T-0313 §7.16 pricing ruling, done):** ServicesStep renders to the Slice-A `BookingStepGate` (services/packages
Set-backed multi-select + rooms/bathrooms steppers + service-category chips derived distinct-by-slug/sorted-by-displayOrder)
over a **`CatalogClient` protocol** (DTO→domain map off the generated `CustomerService/Package GetOverview`, ADR-0019 spine)
surfaced as a **sealed `UiState<Catalog>`** on the VM (NOT the Android `loading`+`loaded`+`services`+`packages` flag-bag — the
E1 catch). **Server is authoritative for pricing:** the VM's `quoteState` FSM (`idle`/`quoting`/`quoted(BookingQuote)`,
no `Error` variant — a swallowed failure keeps the prior quote, the Android `QuoteState` parity) is driven by a Combine
**quoteWatcher** — `$state.map(\.quoteRequest).removeDuplicates().debounce(400ms).sink → orderQuote`; iOS computes ONLY the
**display math** in a pure **`BookingPricing`** port (max(tier,promo) discount FIRST, then +20% express on the discounted
subtotal for the 2–4h lead band, mirroring `CreateOrder.Handler` ordering so the shown total == the charged raw subtotal).
The footer Continue/Slide label shows the live total via `BookingPricing.finalTotal` (the `BookingBottomSheet.kt` footer
parity). **The customer app installs its OWN `CustomerGeneratedAuth` `RequestBuilderFactory`** (the per-host ADR-0019 twin of
`PartnerGeneratedAuth`, `CustomerAuthSpine.make` now returns a stack exposing the `headerAdapter`) — the first customer
business client, authed through the one Core spine. Quote/catalog are in `AnonymousAllowList.customer`, so the
`HeaderAdapter` withholds the Bearer on those paths even with a stored token — guest booking works tokenless. The
**signed-in dual-use carve-out (T-0332, Slice D, done)**: `AnonymousAllowList` now also carries a `dualUsePaths` set
(`.customer` = `Order/Quote`, `Order/CreateOrder`, `Payment/CreateOrder`); `HeaderAdapter.apply` attaches the Bearer when
`isDualUse(path)` **OR** `!isAnonymous(path)` — so a dual-use path gets the Bearer **iff a token exists** (signed-in →
order binds to the user + tier/membership discounts; true guest with no token → still tokenless). The guest-booking
allow-list entries STAY (the carve-out is an additive classification, NOT a deletion). Pure-anon paths
(login/register/confirm/forgot/google/apple) and the guest READ paths (`*/GetOverview`, `Order/Lookup`, `Referral/Validate`)
are anon-but-not-dual-use → **never** get the Bearer even signed-in; `Payment/CreatePaymentIntent` is in no anon set →
always authed. `partner` has an empty `dualUsePaths` (no regression). The factory carries the Bearer on non-allow-listed paths. **Deviations a reviewer rejects:**
a ported catalog flag-bag instead of `UiState<Catalog>`; a quote computed/totaled client-side instead of the server response;
the discount applied AFTER the surcharge or promo/tier summed instead of max(); a per-call Bearer/401 on the quote/catalog
call instead of the factory; a debounce that re-quotes on an unchanged input (must `removeDuplicates` first).

**Slice C (T-0313 §7.16 When&Where + Confirm extras/promo/referral, done):** Step 2 = the address row (a `.fullScreenCover`
over a customer-local `BookingAddressPickerView` reusing the Core `MapProvider`/`GeocodingService` seam — the partner
`AddressPickerView` mirrored, NOT imported: the picker *View+VM* is presentation tied to each app's `L10n`, app-local like every
screen; `onConfirmed(GeocodedAddress)` → `vm.applyAddress` writes street+city+zip+coords into `BookingState`, `savedAddressId`
stays nil — **saved-address list/CRUD is the Android "Address Manager" overlay, DEFERRED → T-0314**) + a pure **`BookingTimeSlots`**
port of `buildDays`/`timeSlotsFor`/`combineDateAndTime` (today+7 day strip; 1h windows 08:00–19:00; the lead-time bands
`<2h Unavailable` / `2–4h Express` / first `≥4h` Earliest / rest Available — the **same `EXPRESS_LEAD_HOURS`/`STANDARD_LEAD_HOURS`
boundary that drives `BookingPricing.requiresExpressSurcharge`**, asserted in a guard test). `selectedDate` stores the localized
day label (matched back to a `BookingDay`); `vm.selectDay`/`selectTime` set `selectedInstant`. The map seam is threaded App-root →
`CustomerAppContainer.geocodingService`/`mapProvider` → shell → `BookingSheetView` → step (feature/VM import NO MapKit — a Core
`PreviewMapProvider` exists so feature previews need no MapKit either). Step 3 (the rest is Slice D): the extras catalog rides a
new **`ExtraClient`** (generated `Extra/GetOverview` DTO→`CatalogExtra`, ADR-0019 spine) as a sealed **`UiState<[CatalogExtra]>`**
(NOT a flag-bag), sorted-by-displayOrder **on the VM** (the View consumes pre-sorted); `vm.toggleExtra(slug)` mutates the
slug→true `selectedExtraSlugs`. Promo + referral are **one-shot Apply-validate FSMs** mirroring `validatePromoCodeNow`/
`validateReferralCodeNow`: `PromoCodeState` (`idle`/`validating`/`valid(discount)`/`invalid(PromoCodeError?)`) over a
**`PromoCodeClient`** (`PromoCode/Validate`, subtotal from the quoted total) and `ReferralCodeState`
(`…/invalid(ReferralValidationError?)`) over a **`ReferralClient`** (`Referral/Validate`, **fail-soft** — a network failure or
typed-invalid is `.invalid`, never fatal; the wire payload still forwards the raw code at submit, Slice D). Valid persists the
normalized code into `BookingState`; the typed-error enums map to localized `.xcstrings` keys (NOT the `code: String?` placeholder).
The code dialogs are native `.sheet`+`.presentationDetents([.medium])` owning local input+FSM, firing the VM's async validate once
per Apply, swapping to Done on Valid (the `PromoCodeBottomSheet.kt` parity). **Recorded parity divergence:** Android's ConfirmStep
*removed* the referral row (signup-only); the ticket re-scopes the referral FSM+row into Slice C, so iOS ships it (the
`validateReferralCodeNow` FSM is still live on the Android VM). **Deviations a reviewer rejects:** a feature/VM `import MapKit`/
`CoreLocation` (the §7.6 seam — the picker file is the only sanctioned consumer); the picker *VM logic* copied between apps rather
than the seam reused (the picker View is app-local presentation, but a duplicated **VM** is a harvest-to-Core candidate — flag, an
Architect call); an extras/promo/referral flag-bag instead of the sealed `UiState`/FSM; a referral `.invalid` that blocks
continue/submit (must fail-soft); the lead-time slot bands diverging from the `BookingPricing` express boundary; saved-address CRUD
built here instead of T-0314. (Slice D fills submit + cash/card + the Stripe seam + the T-0332 signed-in Quote/CreateOrder carve-out.)

**Slice D (T-0313 §7.16 Confirm-rest + cash submit + the T-0332 carve-out, done; NO card/Stripe = Slice E):** the rest of
Step 3 — special instructions (a native `TextEditor` placeholder field — Core `CleansiaTextField` is single-line), the **Plus
preferred-cleaner picker** (a `PreferredCleanerViewModel` gating on `Membership/GetMine.hasMembership` → only then
`Order/MyServingCleaners`; `isVisible = isPlus && !cleaners.isEmpty`, renders nothing otherwise — the Android
`PreferredCleanerPicker` parity), the **cancellation policy** (a pure `CancellationPolicyBuilder` TDD'd from the backend
`BookingPolicy` constants `standardFree=24`/`penalty=4`, Plus widens the free window only when strictly `>24h`), and **trust
badges**. The **Slice-A `paymentMethod: String` debt is resolved** → a `PaymentMethod` enum (`cash`/`card`) with
`.paymentType` mapping to the generated `PaymentType` (`._1`=Cash, `._2`=Card). **`BookingViewModel.submit()` → `BookingSubmitOutcome`**
(`.success` / `.cardPending` / `.failed` / `.profileIncomplete`, sealed) mirrors `BookingViewModel.submit()` 296-495:
(a) auth/profile pre-flight — `tokenStore.current() != nil` (the session source of truth, NOT a profile cache) else `.failed`;
`ProfileClient.currentProfile()` (`User/GetCurrentUser`) failure → `.failed`, name/email/phone blank → `.profileIncomplete`;
(b) require `selectedInstant` else `.failed`; (c) **reuse-cached-or-refetch quote** (`resolvedQuote`: reuse iff
`lastQuoteRequest == state.quoteRequest`, else re-quote, fail → `.failed`); (d) **country resolve** via a `CountryResolver`
(`Country/GetServiced`, iso match lowercased both sides — the iOS `ServiceAreaProvider` seam (T-0334 draft) doesn't exist yet,
so this is the minimal booking-only resolve; nil for saved-address or unmatched iso → backend single-country fallback);
(e) `BookingOrderCommandFactory.make` — **inline-address XOR savedAddressId**, extras slug→true, promo **only when
`promoState == .valid`**, referral raw (fail-soft), **`totalPrice` = the quoted RAW `totalPrice` echoed VERBATIM** (never
`FinalPriceAfterDiscount`; the **same `cleaningDate` passed to Quote+Create** so the server express-surcharge matches —
mismatch surfaces `TotalPriceNotMatch`, never a silent re-price); (f) CASH (`paymentType=1`) → `.success` (a fresh one-off
cash order is **created server-side (Pending+New), NOT auto-confirmed** — §7.16 D4 corrects CLAUDE.md; the success screen
shows only a confirmation code + a status-accurate "Booking received", never "Confirmed") → the sheet swaps to
`BookingSuccessView` (confirmation code) + `vm.reset()` on done; CARD → `.cardPending` (a
placeholder — **NO Stripe call this slice**; real `CreatePaymentIntent`+PaymentSheet = Slice E). **Double-submit debounce:**
the `submit()` guard `!submitState.isSubmitting` returns `.failed` immediately on re-entry (single in-flight; re-enabled only
on a terminal outcome via `defer`) since CreateOrder has NO server idempotency; the slide-to-confirm is disabled+busy for the
whole round-trip (`canConfirm = canContinue && !isSubmitting`, `allowsHitTesting(!busy)`). The View drives navigation (success
screen / snackbar), NOT the VM. **Deviations a reviewer rejects:** echoing `FinalPriceAfterDiscount` instead of the raw
`totalPrice`; a different `cleaningDate` to Create than Quote; sending both inline-address AND savedAddressId; no
double-submit guard (a tap-storm = N orders); a card branch that calls Stripe (that's Slice E); logging the token/secret;
the preferred-cleaner picker fetching cleaners for non-Plus users.

**iOS Stripe seam — adding a new intent type (T-0314 §7.17 Slice C):** extend `PaymentIntentKind {payment, setup}` on
`PaymentSheetPresentation` + branch the `StripePaymentController` switch (one `PaymentSheet(setupIntentClientSecret:)`
path for membership SetupIntent alongside the T-0313 `paymentIntentClientSecret` path) — **NEVER a second Stripe
importer** (`StripePaymentController` stays the sole `import StripePaymentSheet`; secrets stay `<redacted>` in
`description`). The same Gate-SEC rules carry to every intent: `.completed` is UX-only (re-read the server, the webhook
is the sole paid authority), fail-closed on an empty publishable key (`StripeConfig.isCardPaymentAvailable` → hide the
CTA + the branch is unreachable), replay one idempotency token across a two-phase confirm.

**Debounced VM Combine pipelines — the scheduler seam (harvested T-0313):** when a VM debounces a `@Published` pipeline (the
quoteWatcher 400ms), inject the scheduler as a Core **`AnyScheduler`/`AnySchedulerOf`** (`CleansiaCore/State`, a minimal
Combine `Scheduler` type-eraser — no swift-clocks dep) defaulting to `.main`; behavioral tests pass a `TestScheduler` and
`advance(by:)` the virtual clock so "no re-quote on unchanged input" + "one quote after settling" are deterministic with no
real timer. Keep the generic `where`-clause on ONE declaration line (≤120 col) so swiftformat's `wrapMultilineStatementBraces`
doesn't fight swiftlint's `opening_brace` (its `ignore_multiline_statement_conditions` covers `if`/`guard`, not a type/func
`where`).

**iOS customer Home/Orders/OrderDetail — the ONE way (sprint-12 §7.17, T-0314 Slice A; ADR-0019 spine + ADR-0018 D3 modal
mapping + the §7.10 D1 `QuickLookPreview` Core seam + the §7.9 sealed-state/`Code`-mapping conventions + the Parity rule;
Gate-DP):** the customer read cluster (Home + paged Orders + OrderDetail with cancel/review/receipt) over the generated
`CustomerOrderAPI` (`orderGetMyOrders`/`orderGetById`/`orderCancel`/`orderSubmitReview`/`orderDownloadReceipt`/`orderGetPhotos`).
- **The 7-state `OrderStatus` (open risk) — map EXACTLY (`OrderEnums.kt:11`):** `New=0·Pending=1·Confirmed=2·**OnTheWay=3**·
  InProgress=4·Completed=5·Cancelled=6`. The generated `OrderStatus` enum is `_0…_6` (raw == backend int); read the read-path
  `Code` envelope through the **one** `Code.toOrderStatus()` extension (`value.flatMap(OrderStatus.init(rawValue:))`) — never a raw
  `.value == N` compare. **Do NOT use the CLAUDE.md 6-state lifecycle (it omits OnTheWay)** — the timeline/LiveProgressHero/status
  labels MUST handle all 7 or OnTheWay orders render wrong. The LiveProgressHero step indicator is **5 steps** (Booked·Accepted·On
  the way·Started·Finished) with the active index from a pure `LiveProgress.activeStep(for:)` (the `LiveProgressHero.kt:296-303`
  table — strict-TDD'd, esp. that `_3` is its own `.onTheWay` step, not folded into InProgress). Status labels are `.xcstrings` ×5.
- **The paged Orders list** is a sealed `UiState<[OrderListItem]>` + `RefreshPhase` (PTR binds `.userRefreshing`; on-appear refresh is
  `.backgroundRefreshing`) over a `@Singleton`-parity **`OrderRepository`** (an injected `@MainActor` class registered in the
  `SessionScopedCacheRegistry`) that owns the list cache + **ADDITIVE** pagination (`refresh()` replaces page 0, `loadNextPage()`
  appends `offset == orders.count`) — the `OrderRepository.kt` parity; the VM observes its `@Published` via Combine. A refresh
  failure while already loaded STAYS loaded (snackbar only); first-load failure → `.error`.
- **OrderDetail** is `UiState<OrderItem>` + a separate **`PhotosUiState`** side-channel (lazy `ensurePhotosLoaded()` → `orderGetPhotos`,
  **fresh fetch each open** — SAS URLs ~1h, no cross-open cache) + three sealed **`ActionState`**s (cancel/review/receipt) each with a
  paired one-shot **`PassthroughSubject`** effect (close-sheet / file-URL), never a success-as-state. The receipt path: the generated
  `orderDownloadReceipt` **returns a local file `URL`** → the VM surfaces it via the effect → the screen presents the **Core
  `QuickLookPreview`** with `deleteOnDismiss` (the §7.10 D1 seam, reused — SECURITY E4). A **5-min active-order poller** (Confirmed/
  OnTheWay/InProgress only; self-cancels on terminal) + refresh-on-`.task` + an **`OrderEventBus`** seam cover refresh.
  **Customer push registration is NOT built (that was partner T-0311); the poller + on-appear + the bus seam cover refresh until
  customer push lands — flag it, do not build push here.** Cancel is a modal `.sheet` previewing the fee/refund via a pure TDD'd
  `CancellationFeePreview` (oops≤15m/free≥24h/half 4–24h/full<4h, the `CancelOrderSheet.kt` tiers; server recomputes
  authoritatively). **No camera/photo Info.plist keys** — the customer only *views* photos (`AsyncImage` + a fullscreen pager); capture
  is partner-only (§7.10).
- **The T-0313 success→OrderDetail fold:** `BookingSuccessView` gains a "View order" CTA (next to "Go home") that threads the new
  `orderId` (already on `BookingSubmitOutcome.success`) up through `BookingSheetView.onViewOrder` → the shell jumps to the Orders tab
  and pushes `.detail(orderId)` (T-0313 deferred this since Orders didn't exist).
- **HomeTab** is the injection-seam VM (no own state) observing the customer singletons that exist (orders now; loyalty/membership/
  catalog/address/recurring land in later slices — observe what exists, stub the rest cleanly) + a `refreshCatalog` seam; the soft
  profile-completeness nudge **routes to the Profile tab** (EditProfile lands in Slice F; the nudge just navigates).
- **Deviations a reviewer rejects:** a raw `orderStatus.value == N` compare or a second `Code→OrderStatus` mapper; folding OnTheWay
  into InProgress (a 6-state timeline); a list `UiState` flag-bag or non-additive pagination; PTR firing on background refresh;
  dropping the `OrderRepository` singleton/cache un-approved; success modeled as a state instead of a one-shot effect; a partner-local
  `QLPreviewController` wrapper or a stream-to-cache instead of the generated download + Core `QuickLookPreview`; camera/photo plist
  keys added to the customer (it only views); building customer push here.

**iOS customer addresses (AddressManager 3-pane + saved-address CRUD) — the ONE way (sprint-12 §7.17, T-0314 Slice E; ADR-0019
spine + the §7.6 map seam + the §7.16 Slice C booking picker reuse + the Parity rule; Gate-DP):** the saved-address surface
(`AddressManagerScreen.kt`) over the generated `CustomerSavedAddressAPI` (`savedAddressGetMine`/`savedAddressAdd`/
`savedAddressUpdate`/`savedAddressSetDefault`/`savedAddressDelete`).
- **3-pane native SwiftUI** (`List` / `AddOnMap` / `ReviewNew`) hosted by a holder `AddressManagerViewModel` exposing the repo +
  the Core `MapProvider`/`GeocodingService` seams + snackbar; the pane + the picked-`GeocodedAddress` draft live on the VM (so the
  List→AddOnMap→ReviewNew→save→back-to-List flow is unit-testable without a view). The **AddOnMap pane REUSES the existing
  customer-local `BookingAddressPickerView`** (§7.16 Slice C — same pan/search/geocode picker, `onConfirmed`/`onBack`); do NOT hoist
  the picker to Core (the picker→Core HARVEST is **T-0349**, deferred). The VM holds no app navigation — the host's onBack closes.
- **`SavedAddressRepository`** is `@MainActor`, a `SessionScopedCache`, registered, caching the `[SavedAddress]` list — and is
  **server-scoped only**. The Android `AddressRepository.kt` guest/DataStore offline path + `serverId`/local-id duality are NOT
  ported (they exist on Android purely for the offline guest cache); the iOS repo always hits the backend. Ownership is enforced
  server-side (`BeOwnedByCaller`) — **add NO client ownership check.**
- **Mutations refetch the list** rather than mirroring server invariants in two places: `setDefault` (the server demotes peers),
  `add`/`update`, and especially **Delete — `savedAddressDelete` returns an intentional empty-200 with NO id in the body, so the
  repo refetches `getMine` rather than expecting a returned id**. A mutation's `getMine` failure surfaces the error (the VM
  snackbars it); the mutation already succeeded server-side.
- **Country-bias DEFERRED → T-0334** (the §7.7 D3 "design the seam, defer the affordance" move): ship the pan/search/save at full
  parity WITHOUT the service-area country-bias on search **and** without the Android ReviewPane "city not serviced" advisory banner
  (the `ServiceAreaProvider` that drives both rides the deferred T-0334; Slice D did not touch it). Recorded Gate-DP divergence —
  the divergence touches a deferred advisory affordance, not layout/flow/branding; the backend re-validates the city on submit.
- **Reachability:** an "Saved addresses" row + a `.addresses` case extend the interim `ProfileHubView`/`ProfileRoute` **in place**
  (Slice F keeps it when it builds the full hub — do NOT rebuild the hub here). i18n ×5 from the Android customer `strings.xml`.
- **Deviations a reviewer rejects:** a client-side ownership/serverId check; a ported guest/DataStore offline path; a Delete that
  expects a returned id instead of refetching; the country-bias/`ServiceAreaProvider` or the city-not-serviced banner built here
  (T-0334); hoisting the picker to Core (T-0349); the picker VM logic copied instead of reusing `BookingAddressPickerView`; a
  flag-bag pane state; the AddressManager VM driving app navigation.

**Parity deviation (Android is wrong, iOS is right) — auth validation strings:** the Android partner
`RegisterViewModel.kt:64-84` + `ForgotPasswordViewModel.kt:45-52` set validation errors as **hardcoded English
literals** (no `@ApplicationContext Context`, no `R.string.*`) → they never localize across the 5 locales (a
violation of `consistency.md` E8 — see the deviation recorded there). **iOS uses `Localizable.xcstrings` keys
×5** (ADR-0013 D11 / reviewer #10) — the correct behavior; **do NOT copy the Android literals.** The Android
fix (move to `R.string.*`) is a PM-filed android follow-up, not part of the iOS wave. This is the canonical
application of the Parity rule below (Android-wrong → raise a finding, diverge on iOS, fix Android separately).

**Parity rule:** reproduce the Android feature's states, empty/loading/error handling, and API calls
exactly. A behavior difference is a bug unless the ticket calls for it. If the Android behavior is
itself wrong, raise a finding — don't silently diverge on iOS only.

## What to mirror, not invent

- `@HiltViewModel` + sealed `*UiState` + `ActionState` + `StateFlow`/`SharedFlow(replay=0)`.
- `@Singleton` repo implementing `SessionScopedCache`, `networkCall { }`, `ApiErrorParser`,
  `SnackbarController`, returning `T?`.
- `cz.cleansia.core.ui.components.*` + `CleansiaTheme`/`CleansiaTypography`. Typed `@Serializable` routes.
- String resources only; shared code in `:core`/`Core`; iOS at parity with Android.
