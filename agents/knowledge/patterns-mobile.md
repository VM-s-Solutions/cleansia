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
| `AuthInterceptor` anon path-skip (one hardcoded list) | `HeaderAdapter` takes an injected `AnonymousAllowList` (`Core/Auth`) — **host-specific**: `.partner` is auth-only; `.customer` adds the guest-booking surface (`Service/Package/Extra GetOverview`, `Membership/GetPlans`, `Order/{Quote,CreateOrder,Lookup,LookupBatch}`, `Payment/CreateOrder`, `Referral/Validate`). Same case-insensitive path-contains match as Android; `Logout` is never anon |
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
