# Mobile Apps Overview

Cleansia ships native mobile apps on **Android** (Kotlin / Jetpack Compose) and **iOS**
(Swift / SwiftUI). Each platform carries a **partner** app (for cleaners) and a **customer** app,
built on a per-platform shared library so the two audiences don't fork the design system, the auth
spine or the network layer.

| Platform | Shared library | Partner app | Customer app |
|---|---|---|---|
| Android | Gradle module `:core` (`cz.cleansia.core`) | `:partner-app` — `cz.cleansia.partner` | `:customer-app` — `cz.cleansia.customer` |
| iOS | SPM package `CleansiaCore` | XcodeGen target `CleansiaPartner` — `cz.cleansia.partner` | XcodeGen target `CleansiaCustomer` — `cz.cleansia.customer` |

The two platforms deliberately mirror each other: `CleansiaCore` is the iOS port of Android's
`:core`, down to the file names (`AuthInterceptor` ↔ `HeaderAdapter`, `TokenStore` ↔
`KeychainTokenStore`, `SnackbarController` on both). Where a rule is cross-platform, the Kotlin and
Swift sources say so in a comment and cite each other.

## The two backend hosts

Mobile does **not** talk to the web hosts. There is a dedicated mobile API per audience:

| App | Host project | Local port |
|---|---|---|
| Partner (Android + iOS) | `Cleansia.Web.Mobile.Partner` | `5002` |
| Customer (Android + iOS) | `Cleansia.Web.Mobile.Customer` | `5004` |

The web hosts authenticate with an HttpOnly cookie plus a CSRF token, which a native client cannot
read — so the mobile hosts return the tokens in the JSON body instead. See
[API Integration](/mobile-app/api-integration) for the full contract.

---

## Android

Root: `src/cleansia_android/` (`rootProject.name = "CleansiaAndroid"`).

### Module layout

```
src/cleansia_android/
├── settings.gradle.kts          # include(":core") / (":partner-app") / (":customer-app")
├── build.gradle.kts             # plugins declared `apply false`; subprojects opt in
├── gradle/libs.versions.toml    # ONE version catalog for every module
├── gradle.properties            # 4g heap, config cache on, ksp.useKSP2=false
├── core/                        # Android library — cz.cleansia.core
│   └── src/main/java/cz/cleansia/core/
│       ├── auth/                # TokenStore, AuthInterceptor, DeviceHeadersInterceptor,
│       │                        # AuthAuthenticator, SessionManager, SessionScopedCache, JwtDecoder
│       ├── network/             # ApiResult, ApiError, SafeApiCall, NetworkCall, RetryAfterInterceptor
│       ├── ui/theme/            # design tokens — BrandColors, SemanticColors, Type, Spacing, Shape
│       ├── ui/components/       # Cleansia* composables
│       ├── ui/state/            # ActionState
│       ├── snackbar/            # global snackbar bus
│       ├── notifications/       # FCM token lifecycle + device registration
│       ├── location/            # FusedLocation wrapper, Mapbox geocoding, map styles
│       ├── servicearea/ settings/ format/ validation/ media/ freshness/ sentry/ config/
├── partner-app/                 # cz.cleansia.partner
└── customer-app/                # cz.cleansia.customer
```

Both apps declare `implementation(project(":core"))`. `:core` exposes the Mapbox and
FusedLocation stacks as `api` dependencies because the app-side pickers call `MapboxMap` and
`UserLocation` directly.

::: info Source files
- `src/cleansia_android/settings.gradle.kts` — the module list
- `src/cleansia_android/core/build.gradle.kts`, `partner-app/build.gradle.kts`, `customer-app/build.gradle.kts`
- `src/cleansia_android/gradle/libs.versions.toml` — every dependency version
:::

### Toolchain

| Setting | Value | Where |
|---|---|---|
| Gradle wrapper | 8.13 | `gradle/wrapper/gradle-wrapper.properties` |
| Android Gradle Plugin | 8.13.2 | `libs.versions.toml` → `agp` |
| Kotlin | 2.1.10 (KSP `2.1.10-1.0.31`) | `libs.versions.toml` |
| `compileSdk` / `targetSdk` | 35 | every module |
| `minSdk` | 26 (Android 8.0) | every module |
| Java source/target | 21, with core-library desugaring | every module |
| DI | Hilt 2.54 via KSP | — |
| Compose | BOM `2025.02.00`, Material 3 | — |
| Networking | Retrofit 2.11 + OkHttp 4.12 + kotlinx.serialization | — |

> `ksp.useKSP2=false` is set globally in `gradle.properties`: partner-app's KSP run hits
> *"unexpected jvm signature V"* under KSP2 with Hilt 2.54 + Java 21. Do not flip it per-module.

### Build types and where the base URL comes from

Neither app has product flavors. Each build type bakes `API_BASE_URL` into `BuildConfig`:

**`:partner-app`**

| Build type | Minify | `applicationId` suffix | Default `API_BASE_URL` |
|---|---|---|---|
| `debug` | no | `.debug` | `https://api-cleansia-partner-mobile-weu-dev.azurewebsites.net/` |
| `staging` | yes | `.staging` | `https://api-cleansia-partner-mobile-weu-dev.azurewebsites.net/` |
| `release` | yes + shrink | — | `https://api-cleansia-partner-mobile-weu-dev.azurewebsites.net/` |

**`:customer-app`** (`debug` and `release` only — there is no staging type)

| Build type | Minify | `applicationId` suffix | Default `API_BASE_URL` |
|---|---|---|---|
| `debug` | no | `.debug` | `https://api-cleansia-customer-mobile-weu-dev.azurewebsites.net/` |
| `release` | yes + shrink | — | `https://api-cleansia-customer-mobile-weu-dev.azurewebsites.net/` |

**Every build type of both apps points at the Azure DEV host**, deliberately matching what iOS
ships in its `project.yml`, so a fresh clone of either platform hits the same backend with no local
setup — and so a release build and a TestFlight build are talking to the same place.

> Partner's `release` and `staging` types used to default to `api.cleansia.cz` and
> `staging-api.cleansia.cz`. Neither hostname has ever resolved: there is no prod resource group,
> no binding and no certificate, and the only other mention of the first in the tree is a
> commented-out line in a bicepparam whose own header reads *"AUTHORED, NOT DEPLOYED"*. A partner
> release build shipped against them failed every request at DNS. When a real production host
> exists, set it — do not restore those names on the assumption they mean something.

> The partner app resolves its URL **per build type**; the customer app resolves it **once in
> `defaultConfig`**, so its `release` build inherits the same default. Either way a production
> build must be given a real `API_BASE_URL` explicitly — neither picks up a production host on its
> own.

Point either app somewhere else **without editing a build file** — the override wins for every build
type:

```bash
# emulator → a backend running on your machine (10.0.2.2 is the emulator's alias for the host)
./gradlew :partner-app:installDebug  -PAPI_BASE_URL=http://10.0.2.2:5002/
./gradlew :customer-app:installDebug -PAPI_BASE_URL=http://10.0.2.2:5004/

# or make it sticky in ~/.gradle/gradle.properties
API_BASE_URL=http://192.168.1.20:5004/
```

The URL **must end with a slash** (Retrofit rejects a base URL without one); the network module
normalises a missing one anyway.

### Other configuration read at build time

All of these resolve from `~/.gradle/gradle.properties` or the equivalent environment variable, and
fall back to an empty string so an unconfigured clone still builds:

| Property | Apps | Effect when empty |
|---|---|---|
| `MAPBOX_DOWNLOADS_TOKEN` | both (repo credential, `settings.gradle.kts`) | Mapbox artifacts fail to resolve |
| `MAPBOX_ACCESS_TOKEN` | both | maps fail to load at runtime with a clear error |
| `API_BASE_URL` | both | per-build-type default above |
| `SENTRY_DSN` | customer | Sentry stays dormant (no-op init) |
| `STRIPE_PUBLISHABLE_KEY` | customer | PaymentSheet fails at runtime with a clear error |
| `GOOGLE_WEB_CLIENT_ID` | customer | the Google sign-in button fails with a clear message |

`google-services.json` is gitignored per app. If it is missing, the build copies the committed
`google-services.sample.json` placeholder so `assembleDebug` still produces a working APK — push
then silently no-ops at runtime. Replace it with the real Firebase config before any release build.

### Permissions

The two apps ask for almost the same things, and the list is deliberately short — it is what a
Play Data safety declaration is filled in from.

**`:partner-app`** — `AndroidManifest.xml`

```xml
<uses-permission android:name="android.permission.INTERNET" />
<uses-permission android:name="android.permission.ACCESS_NETWORK_STATE" />
<uses-permission android:name="android.permission.POST_NOTIFICATIONS" />
<uses-permission android:name="android.permission.ACCESS_FINE_LOCATION" />
<uses-permission android:name="android.permission.ACCESS_COARSE_LOCATION" />
```

**`:customer-app`** — `INTERNET`, `POST_NOTIFICATIONS`, `ACCESS_FINE_LOCATION`,
`ACCESS_COARSE_LOCATION`. It does not declare `ACCESS_NETWORK_STATE`; that is the only difference.

**Neither app declares a camera or storage permission, and neither needs one.** Every picker in
both apps goes through the system picker — `GetContent()` on partner (job photos, identity
documents), `PickVisualMedia()` and `GetMultipleContents()` on customer (avatar, dispute
evidence) — which returns a URI that already carries a read grant. Every consumer reads it with
`contentResolver.openInputStream`, never a file path, so no permission applies at any API level.

This matters beyond tidiness. `CAMERA` without a matching
`<uses-feature android:name="android.hardware.camera" android:required="false" />` makes Play
infer the hardware as required and filter the store listing to camera devices, and
`READ_MEDIA_IMAGES` obliges the console's *Photo and video permissions* declaration. Both were
declared and unused until they were removed; do not reinstate either without a real call site.

Library manifests merge in four more that neither app declares: `WAKE_LOCK` and
`com.google.android.c2dm.permission.RECEIVE` from Firebase Messaging, `ACCESS_WIFI_STATE` from
Mapbox, and a signature-level `${applicationId}.DYNAMIC_RECEIVER_NOT_EXPORTED_PERMISSION` from
androidx.core. They appear in the merged manifest and in the APK, so a Play declaration should be
read from the merged output, not from the source file above.

Both apps set `android:usesCleartextTraffic="false"` with a `network_security_config`, register the
FCM `MESSAGING_EVENT` service as `exported="false"`, and expose a `FileProvider` under
`${applicationId}.fileprovider` for opening downloaded PDFs.

### Deep links

Declared on **`:partner-app`** only; the customer app's `MainActivity` has just the `LAUNCHER`
filter today and receives notification taps through `singleTop` + `onNewIntent`.

| Pattern | Filter |
|---|---|
| `https://partner.cleansia.cz/…` | App Links, `autoVerify="true"` |
| `cleansia://partner/…` | custom scheme |
| `https://partner.cleansia.cz/confirm-email/…` | App Links, `autoVerify="true"` |

### Languages

English, Czech, Slovak, Ukrainian and Russian — `values/` (en, the base) plus `values-cs`,
`values-sk`, `values-uk`, `values-ru`, present on `:core` and on both apps, with a per-app
`res/xml/locales_config.xml`. The customer app additionally pins `resourceConfigurations` to the
same set so no stray locale ships.

### Local setup on macOS

::: danger `java` on your PATH lies about openjdk@21
`brew install openjdk@21` installs a **keg-only** formula. It is not symlinked into
`/opt/homebrew/bin`, and `/usr/libexec/java_home -V` does not see it either — both report *"Unable
to locate a Java Runtime"* **even when the JDK is installed and working**. That false negative has
sent more than one person off reinstalling a JDK they already had. Always export the explicit path
first:

```bash
export JAVA_HOME=/opt/homebrew/opt/openjdk@21/libexec/openjdk.jdk/Contents/Home
```
:::

The rest of the toolchain, installable without `sudo` (the Temurin **cask** needs `sudo` and fails
in a non-interactive shell — use the `openjdk` **formula**):

```bash
brew install openjdk@21
brew install --cask android-commandlinetools     # puts sdkmanager on PATH, no sudo on Apple Silicon

export JAVA_HOME="$(brew --prefix openjdk@21)/libexec/openjdk.jdk/Contents/Home"
export ANDROID_HOME="$HOME/Library/Android/sdk"
yes | JAVA_HOME=$JAVA_HOME sdkmanager --sdk_root=$ANDROID_HOME --licenses
sdkmanager --sdk_root=$ANDROID_HOME "platform-tools" "platforms;android-35" "build-tools;35.0.0"

# gitignored, holds only sdk.dir — never commit it
echo "sdk.dir=$ANDROID_HOME" > src/cleansia_android/local.properties
```

`local.properties` is gitignored and absent from a fresh worktree; copy it from your main checkout
rather than regenerating the SDK.

### Verifying a change — without a false green

```bash
cd src/cleansia_android
export JAVA_HOME=/opt/homebrew/opt/openjdk@21/libexec/openjdk.jdk/Contents/Home

./gradlew --console=plain --no-daemon \
  :core:testDebugUnitTest          --rerun \
  :partner-app:testDebugUnitTest   --rerun \
  :customer-app:testDebugUnitTest  --rerun
echo "gradle exited $?"
```

Two ways this command reports success when nothing was verified — both have cost real debugging
time:

- **Never pipe Gradle into `tail`, `head` or `grep`.** A shell pipeline exits with the status of the
  *last* command, so `./gradlew … | tail -40` exits `0` on a failed build. If you must page the
  output, run `set -o pipefail` first, or redirect to a file and read it afterwards.
- **`53 actionable tasks: 53 up-to-date` is a non-run, not a pass.** Gradle skips an up-to-date
  `testDebugUnitTest` and prints a green summary having executed no test at all. `--rerun` (a
  per-task option — it binds to the task name it follows) forces execution.

Do **not** add `--offline`: the local dependency cache is incomplete (`mockwebserver` is missing)
and offline resolution fails outright. A plain online resolve does not need `MAPBOX_DOWNLOADS_TOKEN`
once the cache is warm.

---

## iOS

Root: `src/cleansia_ios/`. Full layout and owner steps: `src/cleansia_ios/README.md` and
`src/cleansia_ios/MANUAL_STEPS.md`.

```
src/cleansia_ios/
├── Cleansia.xcworkspace/        # opens CleansiaCore + both generated app projects
├── CleansiaCore/                # shared SPM package — the Android :core equivalent
│   ├── Package.swift            # swift-tools 5.9, platforms: [.iOS(.v16)], defaultLocalization "en"
│   ├── Sources/CleansiaCore/
│   │   ├── Auth/                # hand-written auth/session/header spine (NOT generated)
│   │   ├── Network/             # ApiResult / ApiError / ProblemDetails / apiResult()
│   │   ├── State/               # UiState / ActionState / RefreshPhase / Staleness
│   │   ├── DesignSystem/  Components/  Snackbar/  DI/  Location/  Push/  Media/
│   │   ├── LiveActivity/        # the card views the app and the widget both render
│   │   ├── Format/  Validation/  ServiceArea/  Settings/  Localization/  Config/
│   │   └── Resources/Localizable.xcstrings
│   └── Tests/CleansiaCoreTests/
├── Config/                      # Base.xcconfig (committed) + Local.xcconfig (gitignored)
├── openapi/                     # openapi-generator config for the Swift business clients
├── scripts/                     # generate-api-clients.sh, refresh-mobile-spec.sh, check-local-config.sh
├── fastlane/                    # TestFlight lanes (see fastlane/README.md)
├── CleansiaPartnerApi/          # GENERATED swift5 client — gitignored, machine-owned
├── CleansiaCustomerApi/         # GENERATED swift5 client — gitignored, machine-owned
├── CleansiaPartner/             # project.yml (XcodeGen spec), Sources/, Tests/, Resources/
├── CleansiaCustomer/            # same shape, plus LiveActivity/ (CleansiaCustomerLiveActivity)
├── .swiftlint.yml               # strict, blocking — force_* = error
└── .swiftformat                 # strict — runs --lint in CI
```

The customer app additionally ships a **WidgetKit Live Activity extension**
(`CleansiaCustomer/LiveActivity/`, target `CleansiaCustomerLiveActivity`). Its card views live in
`CleansiaCore/LiveActivity/` and are linked by both the extension and the app target, so the lock
screen and the in-app order screen cannot drift apart.

### The Xcode projects and `Info.plist` are generated — do not edit them

::: danger Never hand-edit `Info.plist`, and never sign in Xcode's UI
Each app's `Info.plist` **and** its `.xcodeproj` are produced by
[XcodeGen](https://github.com/yonaskolb/XcodeGen) from the checked-in `project.yml` spec — the
`Info.plist` keys come from `targets.<Target>.info.properties`. The `.xcodeproj` is gitignored and
rebuilt from scratch on every generate; `Info.plist` **is** committed, which makes it reviewable in
a diff and is exactly why it gets mistaken for a hand-editable file.

Anything you type into `Info.plist` or into Xcode's **Signing & Capabilities** editor is
**silently discarded on the next `xcodegen generate`**. That includes the advice Xcode itself gives
you (*"Select a development team in the Signing & Capabilities editor"*) — do not follow it.

| To change | Edit |
|---|---|
| A plist key (`API_BASE_URL`, `UIAppFonts`, `NS*UsageDescription`, `NSSupportsLiveActivities`) | `project.yml` → `info.properties`, then regenerate |
| `DEVELOPMENT_TEAM`, `STRIPE_PUBLISHABLE_KEY` | `Config/Local.xcconfig` (gitignored) |
| Marketing version | `MARKETING_VERSION` in **both** `project.yml` files |
| Build number | injected at archive time by fastlane; never written into `project.yml` |
:::

`API_BASE_URL` is the worked example of the mechanism — `project.yml` puts it in the generated
`Info.plist`, and the app reads it back out of the bundle at launch:

```swift
// src/cleansia_ios/CleansiaPartner/Sources/AppConfig.swift
enum AppConfig {
    static let apiBaseURL: URL = {
        guard
            let raw = Bundle.main.object(forInfoDictionaryKey: "API_BASE_URL") as? String,
            let url = URL(string: raw)
        else {
            fatalError("API_BASE_URL missing or malformed in Info.plist")
        }
        return url
    }()
}
```

### After every pull, regenerate

`.xcodeproj` is gitignored and the generated API clients are too, so a fresh checkout — and every
`git pull` or branch switch that touches a `project.yml` — needs:

```bash
brew install xcodegen                                       # once

cd src/cleansia_ios
./scripts/generate-api-clients.sh                            # emits Cleansia{Partner,Customer}Api
(cd CleansiaPartner  && xcodegen generate)
(cd CleansiaCustomer && xcodegen generate)
open Cleansia.xcworkspace
```

This is exactly the sequence CI runs, which is what keeps "works on my machine" honest.

### Local build configuration (once)

Two values are per-developer and never committed — the Stripe publishable key and your Apple
`DEVELOPMENT_TEAM`. They live in one gitignored file shared by both apps:

```bash
cp src/cleansia_ios/Config/Local.xcconfig.example src/cleansia_ios/Config/Local.xcconfig
# fill in STRIPE_PUBLISHABLE_KEY (pk_...) and DEVELOPMENT_TEAM (10 chars)
```

`Config/Base.xcconfig` is committed, supplies empty defaults and `#include?`s `Local.xcconfig` last,
so your values win and nothing tracked or generated ever carries them. `scripts/check-local-config.sh`
runs as a pre-build phase on both app targets and grades the diagnostic so a fresh clone still
builds:

| Missing | Simulator / Debug | Device / Release |
|---|---|---|
| `STRIPE_PUBLISHABLE_KEY` | warning — card payment hidden, cash only (fail-closed) | build **error** on Release |
| `DEVELOPMENT_TEAM` | no diagnostic (the simulator needs no signing) | build **error** |

A secret key (`sk_…`) in the publishable slot is always a build error. The severity rules have their
own test: `./scripts/tests/check-local-config.test.sh`.

### Building and testing

`CleansiaCore` is iOS-only (`platforms: [.iOS(.v16)]`), so a bare `swift build` host-builds for
macOS and fails the iOS-only SwiftUI availability checks. Always target a simulator.

::: warning There are three test schemes, and `CleansiaCoreTests` is under neither app
Running the two app schemes leaves the entire `CleansiaCore` suite unexecuted. The package's tests
have to be run from the **package directory**, against its own scheme:

```bash
cd src/cleansia_ios/CleansiaCore
set -o pipefail
xcodebuild -scheme CleansiaCore \
  -destination 'platform=iOS Simulator,name=iPhone 17' build test | xcbeautify
```

Then the two app schemes, from the workspace:

```bash
cd src/cleansia_ios
set -o pipefail
xcodebuild -workspace Cleansia.xcworkspace -scheme CleansiaPartner \
  -destination 'platform=iOS Simulator,name=iPhone 17' build test | xcbeautify
xcodebuild -workspace Cleansia.xcworkspace -scheme CleansiaCustomer \
  -destination 'platform=iOS Simulator,name=iPhone 17' build test | xcbeautify
```

`set -o pipefail` is not decoration: piping `xcodebuild` into `xcbeautify` otherwise hands you
`xcbeautify`'s exit code, and a failed build reads as a pass. Every `xcodebuild` step in
`ios-ci.yml` sets it for this reason.

Substitute a simulator your Xcode actually ships — `xcrun simctl list devices available` — rather
than copying the device name above. CI picks one at runtime for exactly that reason.
:::

### Lint — SwiftFormat first, then SwiftLint

Both tools gate CI and both are **version-pinned**, because a Homebrew bump silently adds default
rules that fail CI on code a developer linted clean locally.

| Tool | Pinned version | CI invocation |
|---|---|---|
| SwiftFormat | `0.60.1` | `swiftformat --lint .` |
| SwiftLint | `0.65.0` | `swiftlint lint --strict` |

::: warning A red "SwiftLint" is very often SwiftFormat
They run in that order and the failure messages look alike, so the first red step gets blamed on the
wrong tool. Check *which step* failed before you start chasing lint rules. Format your hand-written
Swift **before** committing:

```bash
cd src/cleansia_ios
swiftformat .            # write mode, locally
swiftlint lint --strict
```

Match the pinned versions locally (`swiftformat --version`, `swiftlint version`) — CI asserts them
at install time so a PATH-shadowed Homebrew copy fails loudly instead of producing mystery churn.
The generated API packages and `**/Generated` are excluded from both configs.
:::

### Shipping to TestFlight

`fastlane` lanes run **on the owner's Mac** (they reuse the working-tree Stripe key,
`GoogleService-Info.plist` and Xcode-managed signing — no secrets in CI):

```bash
cd src/cleansia_ios
bundle exec fastlane customer   # Customer → TestFlight
bundle exec fastlane partner    # Partner  → TestFlight
bundle exec fastlane all
```

Each lane regenerates the OpenAPI client and the `.xcodeproj`, picks the next build number from
TestFlight, archives Release with automatic signing, and uploads. See
`src/cleansia_ios/fastlane/README.md`.

---

## Continuous integration

| Workflow | Triggers | What it runs |
|---|---|---|
| `.github/workflows/android-ci.yml` | **every** pull request (not path-scoped), plus pushes to `master` scoped to `src/cleansia_android/**` | Temurin JDK 21 + the runner's preinstalled SDK → `compileDebugKotlin` and `testDebugUnitTest` for `:core`, `:partner-app` and `:customer-app`. JVM only, no emulator. Test reports are uploaded on failure. |
| `.github/workflows/ios-ci.yml` | pull requests and pushes to `master`, scoped to `src/cleansia_ios/**`, `src/cleansia_android/openapi/**` and the workflow file | macOS runner on the newest installed Xcode → local-config test → generate API clients → `xcodegen generate` for both apps → SwiftFormat lint → SwiftLint strict → build **and test** `CleansiaCore`, `CleansiaPartner`, `CleansiaCustomer`. |

Android CI runs on every PR on purpose: a path-scoped PR check *skips* silently, and a skipped check
looks the same as a passing one at a glance. iOS is path-scoped because macOS runners bill at a
higher rate — but a change to the shared mobile OpenAPI specs (which live in the Android tree)
re-triggers it, since those specs feed the iOS codegen too.

## Where to go next

- [Features](/mobile-app/features) — how a feature is laid out on each platform, and which code is
  shared versus app-owned.
- [API Integration](/mobile-app/api-integration) — the generated clients, the hand-written auth
  spine, and the header contract both platforms honour.
- [Push Notifications](/architecture/push-notifications) — the FCM/APNs dev-setup runbook, including
  the failure modes that hit iOS only.
- `src/cleansia_ios/README.md` and `src/cleansia_ios/MANUAL_STEPS.md` — the iOS layout in full plus
  the owner-only steps (signing, provisioning, spec regeneration).
