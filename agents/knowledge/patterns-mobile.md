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
| `navigation.Routes` (`@Serializable`) | `NavigationStack` + typed route enum |
| per-app `openApiGenerate { generatorName=kotlin }` reading `openapi/{partner,customer}-mobile-api.json` | per-app `openapi-generator` **swift5 + urlsession** (`responseAs: AsyncAwait`) reading the **same shared committed specs**; config in `cleansia_ios/openapi/openapi-generator-config.*.yaml`, run via `scripts/generate-api-clients.sh`, emitting `Cleansia{Partner,Customer}Api` SPM packages. Generated output is **gitignored + never hand-edited** (change the spec or config, regenerate). The **auth/session/header spine is hand-written** in `Core/Auth` and **excluded from codegen**. First real generation is owner-gated (`manual_step: mobile-spec-regen`) — the specs are stale pre-T-0272 |

**Parity rule:** reproduce the Android feature's states, empty/loading/error handling, and API calls
exactly. A behavior difference is a bug unless the ticket calls for it. If the Android behavior is
itself wrong, raise a finding — don't silently diverge on iOS only.

## What to mirror, not invent

- `@HiltViewModel` + sealed `*UiState` + `ActionState` + `StateFlow`/`SharedFlow(replay=0)`.
- `@Singleton` repo implementing `SessionScopedCache`, `networkCall { }`, `ApiErrorParser`,
  `SnackbarController`, returning `T?`.
- `cz.cleansia.core.ui.components.*` + `CleansiaTheme`/`CleansiaTypography`. Typed `@Serializable` routes.
- String resources only; shared code in `:core`/`Core`; iOS at parity with Android.
