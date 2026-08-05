# Mobile App Features

This page describes **how a feature is built** in the mobile apps — what belongs in the shared
library, what a feature looks like on each platform, and where to look for a given piece of
behaviour.

It deliberately does **not** inventory screens. Mobile ships every sprint; a prose list of features
is stale before it is read, and a stale list is worse than none because it looks authoritative. The
directory tree *is* the inventory — the conventions below tell you how to read it.

For what the apps do from the outside, the audience docs are the better read:
[Partner App](/partner-app/overview) and [Customer App](/customer-app/overview) describe the same
domain from the web side, and the mobile apps target the same order lifecycle and the same backend
rules.

## Shared versus app-owned

The split is the same on both platforms, and it is deliberate: anything a wrong implementation could
make *insecure* or *inconsistent between the two apps* lives in the shared library, with exactly one
definition.

| Concern | Android (`:core`, `cz.cleansia.core`) | iOS (`CleansiaCore`) |
|---|---|---|
| Token storage | `auth/TokenStore` (EncryptedSharedPreferences) | `Auth/KeychainTokenStore` |
| Outgoing headers | `auth/AuthInterceptor` + `auth/DeviceHeadersInterceptor` | `Auth/HeaderAdapter` + `Auth/AnonymousAllowList` |
| 401 refresh | `auth/AuthAuthenticator` | `Auth/SessionRefresher` |
| Device identity | `auth/DeviceIdProvider` | `Auth/DeviceIdProvider` |
| Forced sign-out fan-out | `auth/SessionManager` + `auth/SessionScopedCache` | `Auth/SessionManager` + `SessionScopedCacheRegistry` |
| Result / error envelope | `network/ApiResult`, `network/ApiError` | `Network/ApiResult`, `Network/ApiError` |
| One-shot action state | `ui/state/ActionState` | `State/ActionState` |
| Design tokens | `ui/theme/` — `BrandColors`, `SemanticColors`, `Type`, `Spacing`, `Shape` | `DesignSystem/` |
| Shared components | `ui/components/Cleansia*` | `Components/Cleansia*` |
| Global snackbar | `snackbar/SnackbarController` | `Snackbar/SnackbarController` |
| Push token lifecycle | `notifications/PushTokenRepository` | `Push/PushTokenRegistrar` |
| Location + geocoding | `location/` | `Location/` |
| Serviced countries/cities | `servicearea/` | `ServiceArea/` |
| Photo compression (both strip EXIF/GPS on re-encode) | `media/ImageCompressor` — reads the orientation tag first, via `androidx.exifinterface` | `Media/ImageCompressor` |
| Formatters, validators | `format/`, `validation/` | `Format/`, `Validation/` |

Everything else — screens, view models, per-feature API clients, navigation, the app's own theme
wrapper — is app-owned, because the partner and the customer app genuinely disagree about it.

::: warning One definition, not two copies
`ApiError`/`ApiResult` and the auth spine exist **once** per platform on purpose. A second copy in an
app module is the drift that produced "blank screen" bugs before the shared module existed. If you
need a variation, widen the shared type rather than forking it.

Two pre-`:core` duplicates survive on Android and are **known, tracked for migration, not
precedent**: `cz.cleansia.customer.ui.state.ActionState` (an identical copy of `:core`'s — its doc
comment says so) and the customer app's `ui/format/` formatters, which shadow `core/format/`. Import
the `:core` definition in new code.
:::

## Anatomy of a feature

### Android

```
partner-app/src/main/java/cz/cleansia/partner/
├── features/<area>/            # Compose screens + ViewModels + area-local composables
│   ├── <Area>Screen.kt
│   ├── <Area>ViewModel.kt      # @HiltViewModel, exposes StateFlow
│   └── <Area>Card.kt …         # presentational pieces
├── data/<area>/                # repositories + their Hilt @Module
│   ├── <Area>Repository.kt
│   └── <Area>Module.kt
├── core/                       # app-level plumbing: network/, auth/, notifications/,
│                               # devices/, settings/, servicearea/, location/
├── navigation/                 # PartnerNavHost.kt + NavRoutes.kt
└── ui/theme/                   # the app's Material 3 theme over :core's tokens
```

The customer app uses the same shape with one naming difference: its data layer lives under
`core/<area>/` (`<Area>Api.kt`, `<Area>Dtos.kt`, `<Area>Repository.kt`, `<Area>Module.kt`) and its
navigation entry point is `navigation/CleansiaNavHost.kt` + `navigation/Routes.kt`.

Hilt modules sit **next to the repository they provide**, not in one central `di/` package — so
deleting a feature deletes its wiring with it.

### iOS

```
CleansiaPartner/
├── Sources/
│   ├── CleansiaPartnerApp.swift        # @main entry
│   ├── PartnerAppContainer.swift       # the app's AppContainer (DI root)
│   ├── PartnerClients.swift            # MobileApiClient adapter over the generated client
│   ├── AppConfig.swift                 # reads API_BASE_URL out of the generated Info.plist
│   ├── L10n*.swift                     # typed accessors over Localizable.xcstrings
│   ├── Features/<Area>/
│   │   ├── <Area>View.swift            # SwiftUI view
│   │   ├── <Area>ViewModel.swift       # @MainActor final class : ViewModel (ObservableObject)
│   │   └── <Area>Content.swift …       # extracted subviews
│   └── Data/                           # most clients live app-level here (Partner*Client.swift);
│                                       # a few sit beside their feature under Features/<Area>/Data/
├── Tests/                              # hosted XCTest target, one file per view model
└── Resources/Localizable.xcstrings
```

The customer app is the same shape but consistently keeps each feature's clients beside the feature —
`Sources/Features/<Area>/Data/<Area>Client.swift` — and adds the `LiveActivity/` target directory.

State is `ObservableObject` + `@Published`, not `@Observable`: the deployment floor is iOS 16
(ADR-0014), where the observation macro is unavailable.

### The state contract, side by side

Both platforms use the same three-layer contract — the client/repository returns `ApiResult`, the
view model translates it into a `UiState` plus an `ActionState`, and errors go through the shared
snackbar via the app's error localizer. The `Devices` screen exists on both platforms and shows the
whole shape:

::: code-group

```kotlin [Android]
// partner-app/.../features/devices/DevicesViewModel.kt
sealed interface DevicesUiState {
    data object Loading : DevicesUiState
    data object Error : DevicesUiState
    data class Loaded(val devices: List<UserDeviceDto>) : DevicesUiState
}

@HiltViewModel
class DevicesViewModel @Inject constructor(
    private val devicesRepository: DevicesRepository,
    private val errorTranslator: ApiErrorTranslator,
    private val snackbar: SnackbarController,
    /* … */
) : ViewModel() {

    private val _state = MutableStateFlow<DevicesUiState>(DevicesUiState.Loading)
    val state: StateFlow<DevicesUiState> = _state.asStateFlow()

    private val _revokeState = MutableStateFlow<ActionState>(ActionState.Idle)
    val revokeState: StateFlow<ActionState> = _revokeState.asStateFlow()

    fun load() = viewModelScope.launch {
        _state.value = DevicesUiState.Loading
        when (val result = devicesRepository.getMyDevices()) {
            is ApiResult.Success -> _state.value = DevicesUiState.Loaded(result.data)
            is ApiResult.Error -> {
                snackbar.showError(errorTranslator.translate(result.error))
                _state.value = DevicesUiState.Error
            }
        }
    }
}
```

```swift [iOS]
// CleansiaPartner/Sources/Features/Devices/DevicesViewModel.swift
@MainActor
final class DevicesViewModel: ViewModel {
    @Published private(set) var state: UiState<[UserDevice]> = .loading
    @Published private(set) var revokeAction: ActionState = .idle

    private let client: PartnerDevicesClient
    private let snackbar: SnackbarController
    private let localizer = ApiErrorLocalizer()

    func load() async {
        state = .loading
        switch await client.myDevices() {
        case let .success(devices):
            state = .loaded(devices)
        case let .failure(error):
            state = .error(error)
            snackbar.showError(localizer.message(for: error))
        }
    }
}
```

:::

Two differences are real and intentional:

- **`UiState` is shared on iOS, per-feature on Android.** `CleansiaCore/State/UiState` is a generic
  `loading | error(ApiError) | loaded(T)` enum. Android view models declare their own
  `sealed interface <Feature>UiState` because their loaded cases usually carry more than one payload.
- **`ActionState` is shared on both** — `Idle | Submitting | Error(message)`. Success is
  deliberately **not** a state: it is an effect that returns the action to `Idle` and (where needed)
  emits on a separate one-shot channel (`SharedFlow` / `PassthroughSubject`), so the same action stays
  re-armable for a retry.

## Freshness and session-scoped caches

Two shared mechanisms decide when a screen re-fetches and when it must forget everything:

| Mechanism | Android | iOS | Purpose |
|---|---|---|---|
| Freshness watermark | `core/freshness/Staleness` | `State/Staleness` | a repository stamps `markFresh()` after a successful fetch; a view model checks `isStale()` on screen entry and skips the round trip if the cache is warm. A user-initiated pull always bypasses it — the user's intent outranks the cache age. |
| Session-scoped cache | `auth/SessionScopedCache` (a Hilt multibinding) | `SessionScopedCacheRegistry` | any repository holding per-user state joins the set and is wiped automatically on sign-out **and** on forced sign-out. There is no hand-maintained list to keep in sync, so the two clear-paths cannot drift. |

A repository may hold both, and should reset its `Staleness` from its `clear()` so the watermark
does not survive a session swap.

## Localization

Both platforms ship English, Czech, Slovak, Ukrainian and Russian — the same fixed set as the web
apps.

| Platform | Catalogs | Accessed via |
|---|---|---|
| Android | `res/values/strings.xml` (en, base) + `values-cs`, `values-sk`, `values-uk`, `values-ru`, on `:core` and both apps | `context.getString(R.string.…)` / `stringResource(...)` |
| iOS | `Localizable.xcstrings` in `CleansiaCore/Sources/CleansiaCore/Resources/` and in each app's `Resources/` | the app's typed `L10n*.swift` accessors; `CoreL10n` inside the package |

Backend error keys resolve through the platform's own naming convention, **not** the web apps'
`api.*` namespace — see [API Integration](/mobile-app/api-integration#error-keys-become-user-facing-text).

## Tests

| Suite | Location | Run with |
|---|---|---|
| Android `:core` | `core/src/test/` | `./gradlew :core:testDebugUnitTest --rerun` |
| Android partner | `partner-app/src/test/` | `./gradlew :partner-app:testDebugUnitTest --rerun` |
| Android customer | `customer-app/src/test/` | `./gradlew :customer-app:testDebugUnitTest --rerun` |
| iOS `CleansiaCore` | `CleansiaCore/Tests/CleansiaCoreTests/` | the `CleansiaCore` scheme, **from the package directory** |
| iOS partner | `CleansiaPartner/Tests/` | the `CleansiaPartner` scheme, from the workspace |
| iOS customer | `CleansiaCustomer/Tests/` | the `CleansiaCustomer` scheme, from the workspace |

The exact invocations, and the two ways these commands can report a false green, are in
[Overview → Verifying a change](/mobile-app/overview#verifying-a-change-without-a-false-green) and
[Overview → Building and testing](/mobile-app/overview#building-and-testing).

Android suites are pure JVM and there is no emulator step in CI. `:core` and `:customer-app` set
`unitTests.isReturnDefaultValues = true` so calls into `android.util.Log` and friends return
zero/null/false instead of throwing *"Method X not mocked"* — which is what you want for
fire-and-forget logging inside a class under test. iOS suites are hosted XCTest targets on a
simulator.

The two platforms substitute dependencies differently, and both choices are deliberate:

- **iOS** uses hand-written fakes over the client protocol — `Tests/Fake*Client.swift`,
  `Tests/Support/*Fakes.swift`.
- **Android** uses MockK, Turbine for flow assertions (in the two app modules), **and MockWebServer**
  where the wire contract matters. A hand-written Retrofit interface carries that contract in its
  `@Query` **names**;
  a mocked interface can only pin argument *values*, never the names the server binds. MockWebServer
  lets the test assert the URL that actually went out.
