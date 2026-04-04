# Mobile App Overview

The Cleansia Partner mobile app is a native Android application built with Kotlin and Jetpack Compose. It serves as the primary tool for cleaning employees to manage orders, track time, document work with photos, and view invoices.

## Tech Stack

| Technology | Version/Details |
|------------|----------------|
| Language | Kotlin |
| UI Framework | Jetpack Compose with Material 3 |
| Min SDK | 26 (Android 8.0) |
| Target SDK | 35 (Android 15) |
| Compile SDK | 35 |
| DI | Hilt (with KSP) |
| Networking | Retrofit + OkHttp |
| Serialization | kotlinx.serialization |
| Database | Room |
| Image loading | Coil |
| Animations | Lottie |
| Navigation | Compose Navigation |
| Security | EncryptedSharedPreferences, Biometric |

::: info Source Files
- Build config: `src/cleansia_android/app/build.gradle.kts`
- Manifest: `src/cleansia_android/app/src/main/AndroidManifest.xml`
- App entry: `src/cleansia_android/app/src/main/java/cz/cleansia/partner/CleansiaApp.kt`
:::

## Project Structure

```
src/cleansia_android/app/src/main/java/cz/cleansia/partner/
├── CleansiaApp.kt              # Application class (Hilt entry point)
├── MainActivity.kt             # Single activity
├── config/                     # App configuration
├── core/
│   ├── database/               # Room database, DAOs, entities
│   ├── extensions/             # Kotlin extension functions
│   ├── network/                # Retrofit, interceptors, API result wrapper
│   ├── notifications/          # Foreground service (order timer)
│   ├── security/               # Biometric, encryption
│   ├── storage/                # TokenManager, DataStore
│   └── utils/                  # Utilities
├── di/                         # Hilt modules
├── domain/
│   ├── models/                 # Domain models (auth, dashboard, invoices, orders, profile)
│   └── repositories/           # Repository interfaces
├── features/
│   ├── account/                # Account management screens
│   ├── auth/                   # Login, register, email confirmation
│   ├── dashboard/              # Home screen with analytics
│   ├── invoices/               # Invoice list and details
│   ├── notifications/          # Push notifications
│   ├── onboarding/             # First-launch onboarding flow
│   ├── orders/                 # Order list, details, photos, timer
│   ├── profile/                # Employee profile
│   ├── search/                 # Global search
│   └── settings/               # App settings
├── navigation/                 # Compose Navigation graph
└── ui/                         # Theme, shared components
```

## Build Variants

### Build Types

| Type | Minified | API Base URL | App Name |
|------|----------|-------------|----------|
| `debug` | No | `http://10.0.2.2:5002/api` | Cleansia Dev |
| `staging` | Yes | `https://staging-api.cleansia.cz/api` | Cleansia Staging |
| `release` | Yes + shrink | `https://api.cleansia.cz/api` | Cleansia Partner |

### Product Flavors

| Flavor | Description |
|--------|-------------|
| `prod` | Real API integration |
| `mock` | Mock data for development (debug builds only) |

The `mock` flavor is restricted to `debug` build type only via `androidComponents.beforeVariants`.

## Permissions

```xml
<uses-permission android:name="android.permission.INTERNET" />
<uses-permission android:name="android.permission.ACCESS_NETWORK_STATE" />
<uses-permission android:name="android.permission.CAMERA" />
<uses-permission android:name="android.permission.READ_EXTERNAL_STORAGE" android:maxSdkVersion="32" />
<uses-permission android:name="android.permission.READ_MEDIA_IMAGES" />
<uses-permission android:name="android.permission.POST_NOTIFICATIONS" />
<uses-permission android:name="android.permission.FOREGROUND_SERVICE" />
<uses-permission android:name="android.permission.FOREGROUND_SERVICE_SPECIAL_USE" />
```

| Permission | Purpose |
|------------|---------|
| `INTERNET` | API communication |
| `ACCESS_NETWORK_STATE` | Offline detection |
| `CAMERA` | Before/after photos |
| `READ_EXTERNAL_STORAGE` / `READ_MEDIA_IMAGES` | Photo gallery access |
| `POST_NOTIFICATIONS` | Order timer, push notifications |
| `FOREGROUND_SERVICE` + `SPECIAL_USE` | Order timer foreground service |

## Room Database

Local caching for offline support using Room:

```kotlin
@Database(
    entities = [CachedOrder::class, CachedInvoice::class, CachedProfile::class],
    version = 1,
    exportSchema = false
)
abstract class CleansiaDatabase : RoomDatabase() {
    abstract fun orderDao(): OrderDao
    abstract fun invoiceDao(): InvoiceDao
    abstract fun profileDao(): ProfileDao
}
```

**Source:** `src/cleansia_android/app/src/main/java/cz/cleansia/partner/core/database/CleansiaDatabase.kt`

| Entity | DAO | Purpose |
|--------|-----|---------|
| `CachedOrder` | `OrderDao` | Offline order list cache |
| `CachedInvoice` | `InvoiceDao` | Offline invoice cache |
| `CachedProfile` | `ProfileDao` | Employee profile cache |

## Deep Links

The app supports three deep link patterns:

| Pattern | Purpose |
|---------|---------|
| `https://partner.cleansia.cz/*` | App Links (verified) |
| `cleansia://partner/*` | Custom scheme links |
| `https://partner.cleansia.cz/confirm-email/*` | Email confirmation |

## OpenAPI Code Generation

The Android project uses the OpenAPI Generator Gradle plugin to generate API client code from the backend's Swagger spec:

```bash
# Download spec from running backend
./gradlew downloadApiSpec

# Generate Kotlin API client
./gradlew generateApiClient

# Both in one step
./gradlew updateApiClient
```

Generated code goes to `build/generated/openapi/src/main/kotlin` with package `cz.cleansia.partner.api.generated`.

## Key Dependencies

| Category | Library |
|----------|---------|
| Core | `androidx.core.ktx`, `lifecycle-runtime-ktx` |
| Compose | BOM-managed, `material3`, `material-icons-extended` |
| DI | `hilt-android`, `hilt-navigation-compose` |
| Network | `retrofit`, `okhttp`, `okhttp-logging` |
| Serialization | `kotlinx-serialization-json`, `retrofit-kotlinx-serialization` |
| Storage | `datastore-preferences`, `security-crypto`, `room-runtime` |
| UI | `coil-compose`, `lottie-compose`, `splashscreen` |
| Auth | `biometric` |
