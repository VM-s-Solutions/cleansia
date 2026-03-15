# Cleansia — Mobile App Development Plan

## Current State

- **Android Partner app exists** (173 Kotlin source files)
- **Package:** `cz.cleansia.partner`
- **Min SDK:** 26 / **Target SDK:** 35
- **Architecture:** MVVM + Hilt (DI) + Retrofit (API) + Jetpack Compose (UI) + DataStore (prefs) + Kotlin Coroutines/Flow
- **Features:** Login, Dashboard, Orders, Invoices, Profile, Settings, Onboarding, Notifications, Search
- **Localization:** EN, CS, SK, UK, RU (resource directories: `values`, `values-cs`, `values-sk`, `values-uk`, `values-ru`)
- **Build variants:** `prod` and `mock` flavors; `debug`, `staging`, and `release` build types
- **OpenAPI code generation** for API client (Kotlin + Retrofit, kotlinx.serialization)
- **Security:** EncryptedSharedPreferences, Biometric authentication
- **Database:** Room (local persistence)
- **Mock flavor:** Separate mock repository implementations for offline development
- **Key dependencies:** Coil (images), Lottie (animations), Material 3, Navigation Compose

### Android Partner App — Module Structure
```
cleansia_android/
├── app/
│   └── src/
│       ├── main/java/cz/cleansia/partner/
│       │   ├── config/              # App configuration
│       │   ├── core/
│       │   │   ├── database/        # Room DB, DAOs, entities
│       │   │   ├── extensions/      # Kotlin extension functions
│       │   │   ├── network/         # Retrofit setup, interceptors
│       │   │   ├── notifications/   # Push notification handling
│       │   │   ├── security/        # Encryption, biometrics
│       │   │   ├── storage/         # DataStore, secure storage
│       │   │   └── utils/           # Utility classes
│       │   ├── di/                  # Hilt modules
│       │   ├── domain/
│       │   │   ├── models/          # auth, dashboard, invoices, orders, profile
│       │   │   └── repositories/    # Repository interfaces
│       │   ├── features/
│       │   │   ├── account/         # Account management (screens + viewmodels)
│       │   │   ├── auth/            # Login, register, forgot password
│       │   │   ├── dashboard/       # Dashboard with analytics components
│       │   │   ├── invoices/        # Invoice list and detail
│       │   │   ├── notifications/   # Notification center
│       │   │   ├── onboarding/      # Multi-step onboarding wizard
│       │   │   ├── orders/          # Order list, detail, photo handling
│       │   │   ├── profile/         # Profile with availability, documents, sections
│       │   │   ├── search/          # Search functionality
│       │   │   └── settings/        # App settings
│       │   ├── navigation/          # Navigation graph
│       │   └── ui/                  # Shared UI components and theme
│       └── mock/                    # Mock repositories for offline dev
├── api-spec/                        # Downloaded OpenAPI spec
├── scripts/                         # Build/utility scripts
└── build.gradle.kts                 # OpenAPI generator + dependencies
```

---

## Technology Decision

**Kotlin + Swift** (native per platform) — chosen over KMP

### Why Native over KMP
- Existing Android codebase is pure Kotlin with Jetpack Compose — no shared layer exists
- Better IDE support and debugging (full Android Studio / Xcode experience)
- More control over platform-specific UX and animations
- No shared code layer complexity or KMP version compatibility issues
- Easier to hire specialists (Kotlin devs for Android, Swift devs for iOS)
- Each platform gets first-class API access without abstraction overhead

### Trade-offs
- More code to maintain (separate codebases per platform)
- Feature parity requires discipline and clear specifications
- Higher development cost long-term (two implementations per feature)
- Bug fixes may need to be applied in both codebases

---

## Apps to Build

### 1. iOS Partner App (Swift)

Mirror the existing Android Partner app functionality using native iOS equivalents.

#### Architecture Mapping

| Android (Kotlin)            | iOS (Swift)                        |
|-----------------------------|------------------------------------|
| Jetpack Compose             | SwiftUI                            |
| Hilt                        | Swift DI (Swinject or manual)      |
| Retrofit + OkHttp           | URLSession + async/await           |
| DataStore                   | UserDefaults                       |
| EncryptedSharedPreferences  | Keychain                           |
| ViewModel                   | ObservableObject / @Observable     |
| Flow                        | AsyncStream / Combine              |
| Navigation Compose          | NavigationStack                    |
| Material 3                  | Native iOS components              |
| Room                        | SwiftData / Core Data              |
| Coil                        | AsyncImage / Kingfisher            |
| Lottie (Android)            | Lottie (iOS)                       |
| kotlinx.serialization       | Codable                            |
| Biometric (AndroidX)        | LocalAuthentication (FaceID/Touch) |
| Firebase Cloud Messaging    | Apple Push Notification service    |

#### Features to Implement

- [ ] **Authentication**
  - [ ] Login screen
  - [ ] Registration screen
  - [ ] Forgot password flow
  - [ ] Token management (access + refresh)
  - [ ] Biometric unlock (Face ID / Touch ID)
- [ ] **Onboarding**
  - [ ] Multi-step onboarding wizard
  - [ ] First-launch detection
- [ ] **Dashboard**
  - [ ] Stats overview
  - [ ] Analytics components
  - [ ] Pull-to-refresh
- [ ] **Order Management**
  - [ ] Order list with filtering
  - [ ] Order detail view
  - [ ] Accept/reject orders
  - [ ] Photo upload for completed orders
- [ ] **Invoice Management**
  - [ ] Invoice list
  - [ ] Invoice detail view
- [ ] **Profile Management**
  - [ ] Personal info editing
  - [ ] Availability schedule
  - [ ] Document uploads
  - [ ] Profile sections
- [ ] **Settings**
  - [ ] Language selection (EN, CS, SK, UK, RU)
  - [ ] Theme toggle (light/dark)
  - [ ] Notification preferences
  - [ ] Biometric settings
- [ ] **Push Notifications** (APNs)
  - [ ] Remote notification registration
  - [ ] In-app notification center
- [ ] **Search**
  - [ ] Global search across orders/invoices
- [ ] **Account**
  - [ ] Account management
  - [ ] Logout
- [ ] **Localization** (EN, CS, SK, UK, RU)
  - [ ] String catalogs
  - [ ] RTL support consideration
- [ ] **Offline Support**
  - [ ] Local data caching (SwiftData)
- [ ] **OpenAPI Client Generation**
  - [ ] Swift client from OpenAPI spec (URLSession-based)

---

### 2. Android Customer App (Kotlin)

New app for customers to book and manage cleaning services. Reuses patterns and core modules from the Partner app.

#### Features to Implement

- [ ] **Authentication**
  - [ ] Login screen
  - [ ] Registration screen
  - [ ] Forgot password flow
  - [ ] Email confirmation
  - [ ] Token management
- [ ] **Service Catalog**
  - [ ] Browse cleaning services
  - [ ] Browse service packages
  - [ ] Service detail view
- [ ] **Order Wizard**
  - [ ] Step 1: Select services
  - [ ] Step 2: Enter address
  - [ ] Step 3: Pick date/time
  - [ ] Step 4: Payment (Stripe)
  - [ ] Step 5: Review summary
  - [ ] Order submission
- [ ] **Stripe Payment Integration**
  - [ ] Stripe Android SDK setup
  - [ ] Payment sheet / checkout flow
  - [ ] Payment success handling
  - [ ] Payment cancellation handling
- [ ] **Order History & Tracking**
  - [ ] Order list with status filters
  - [ ] Order detail view with timeline
  - [ ] Real-time status updates
- [ ] **Profile Management**
  - [ ] Personal info editing
  - [ ] Address management
- [ ] **Dispute Management**
  - [ ] Create dispute for an order
  - [ ] Dispute list and detail
- [ ] **GDPR Compliance**
  - [ ] Data export request
  - [ ] Account deletion request
  - [ ] Consent management
- [ ] **Push Notifications** (FCM)
  - [ ] Order status updates
  - [ ] Promotional notifications
- [ ] **Localization** (EN, CS, SK, UK, RU)
- [ ] **Theme** (light/dark mode, Material 3)

#### Reusable from Partner App

- `core/network/` — Retrofit setup, interceptors, error handling
- `core/storage/` — DataStore and secure storage utilities
- `core/security/` — Encryption and biometric helpers
- `core/extensions/` — Kotlin extension functions
- `core/utils/` — General utilities
- `di/` — Hilt module patterns
- `features/auth/` — Auth flow structure (screens, viewmodels, components)
- `ui/theme/` — Material 3 theme and design tokens
- `ui/components/` — Shared composables
- Localization infrastructure and string resource structure
- OpenAPI code generation Gradle setup
- Build variant configuration (debug/staging/release, prod/mock flavors)

---

### 3. iOS Customer App (Swift)

Same features as Android Customer App, built natively in Swift/SwiftUI.

#### Features to Implement

- [ ] **Authentication** (login, register, forgot password, email confirmation)
- [ ] **Service Catalog** (browse services and packages)
- [ ] **Order Wizard** (services -> address -> date/time -> payment -> summary)
- [ ] **Stripe Payment Integration** (Stripe iOS SDK)
- [ ] **Order History & Tracking**
- [ ] **Profile Management**
- [ ] **Dispute Management**
- [ ] **GDPR Compliance** (data export, account deletion, consent)
- [ ] **Push Notifications** (APNs)
- [ ] **Localization** (EN, CS, SK, UK, RU)
- [ ] **Theme** (light/dark mode, native iOS styling)

---

## Shared Infrastructure

### API Client Generation

Both platforms generate strongly-typed API clients from the backend OpenAPI spec.

| Aspect          | Android                                       | iOS                                      |
|-----------------|-----------------------------------------------|------------------------------------------|
| Tool            | OpenAPI Generator Gradle plugin               | OpenAPI Generator CLI / Swift package     |
| Language        | Kotlin                                        | Swift                                    |
| HTTP Library    | Retrofit + OkHttp                             | URLSession + async/await                 |
| Serialization   | kotlinx.serialization                         | Codable (JSONDecoder/JSONEncoder)        |
| Spec Source     | `http://localhost:5002/swagger/v1/swagger.json`| Same spec, different output              |

### Push Notifications

| Aspect           | Android                         | iOS                              |
|------------------|---------------------------------|----------------------------------|
| Service          | Firebase Cloud Messaging (FCM)  | Apple Push Notification service  |
| Token Storage    | Backend device registration     | Backend device registration      |
| Silent Push      | FCM data messages               | Background notifications         |
| Rich Notifications| FCM + NotificationCompat       | UNNotificationServiceExtension   |

Backend requirement: Notification service that dispatches to both FCM and APNs based on device registration.

### Deep Linking

| Use Case                          | Android             | iOS                |
|-----------------------------------|---------------------|--------------------|
| Technology                        | App Links           | Universal Links    |
| Order confirmation email          | Open order detail   | Open order detail  |
| Marketing campaign                | Open service catalog| Open service catalog|
| Password reset                    | Open reset flow     | Open reset flow    |
| Email confirmation                | Confirm + redirect  | Confirm + redirect |

### Payments (Customer Apps Only)

| Aspect           | Android                     | iOS                         |
|------------------|-----------------------------|-----------------------------|
| SDK              | Stripe Android SDK          | Stripe iOS SDK              |
| Integration      | PaymentSheet                | PaymentSheet                |
| Backend          | Shared Stripe backend (PaymentController) | Same backend   |

---

## Development Order

### Phase 1: iOS Partner App (estimated 4-6 weeks)

Best to start here because:
- Feature set already defined and proven on Android (173 files as reference)
- Can 1:1 mirror the Android app — no product decisions needed
- Partner app is simpler than Customer app (no payments, no order wizard)
- Establishes iOS codebase patterns for Phase 3

**Milestones:**
- [ ] Week 1: Project setup, DI, networking layer, OpenAPI client gen
- [ ] Week 2: Auth flow (login, register, forgot password, token management)
- [ ] Week 3: Dashboard, order list/detail, accept/reject
- [ ] Week 4: Invoices, profile management, document uploads
- [ ] Week 5: Settings, onboarding, push notifications, biometrics
- [ ] Week 6: Polish, localization (5 languages), testing, TestFlight beta

### Phase 2: Android Customer App (estimated 6-8 weeks)

- More complex than Partner (order wizard, Stripe payments, disputes, GDPR)
- Reuses core module from Partner app (networking, storage, auth patterns)
- New modules: services catalog, order wizard, payments, disputes

**Milestones:**
- [ ] Week 1: Project setup, core module extraction, shared dependencies
- [ ] Week 2: Auth flow (login, register, forgot password, email confirmation)
- [ ] Week 3: Service catalog, browsing and detail views
- [ ] Week 4: Order wizard (multi-step flow)
- [ ] Week 5: Stripe payment integration, checkout success/cancel
- [ ] Week 6: Order history, tracking, order detail with timeline
- [ ] Week 7: Disputes, GDPR compliance, profile management
- [ ] Week 8: Push notifications, polish, localization, testing, internal beta

### Phase 3: iOS Customer App (estimated 6-8 weeks)

- Mirror Android Customer app in Swift/SwiftUI
- iOS patterns already established from Phase 1 (Partner app)
- Can develop in parallel with Phase 2 if resources allow

**Milestones:**
- [ ] Week 1: Project setup, reuse CleansiaCore package from Partner app
- [ ] Week 2: Auth flow with email confirmation
- [ ] Week 3: Service catalog
- [ ] Week 4: Order wizard
- [ ] Week 5: Stripe iOS SDK integration
- [ ] Week 6: Order history, tracking
- [ ] Week 7: Disputes, GDPR, profile
- [ ] Week 8: Push notifications, polish, localization, testing, TestFlight beta

---

## Project Structure

### Android (monorepo approach)

Restructure the current single-module Partner app into a multi-module project that shares code between Partner and Customer apps.

```
cleansia_android/
├── app-partner/                  # Partner app module
│   ├── src/main/
│   │   ├── java/.../partner/
│   │   │   ├── features/         # Partner-specific features
│   │   │   │   ├── dashboard/
│   │   │   │   ├── invoices/
│   │   │   │   ├── onboarding/
│   │   │   │   └── orders/       # Partner order management (accept/reject)
│   │   │   └── navigation/
│   │   └── res/
│   └── src/mock/                 # Mock repositories
│
├── app-customer/                 # Customer app module
│   ├── src/main/
│   │   ├── java/.../customer/
│   │   │   ├── features/         # Customer-specific features
│   │   │   │   ├── catalog/      # Service browsing
│   │   │   │   ├── checkout/     # Stripe payment
│   │   │   │   ├── disputes/     # Dispute management
│   │   │   │   ├── gdpr/         # Data export, deletion
│   │   │   │   ├── orders/       # Customer order history
│   │   │   │   └── wizard/       # Order creation wizard
│   │   │   └── navigation/
│   │   └── res/
│   └── src/mock/
│
├── core/                         # Shared core module
│   ├── network/                  # Retrofit, interceptors, error handling
│   ├── storage/                  # DataStore, encrypted prefs
│   ├── security/                 # Encryption, biometrics
│   ├── database/                 # Room setup (shared tables)
│   ├── extensions/               # Kotlin extensions
│   └── utils/                    # Utilities
│
├── feature-auth/                 # Shared auth feature module
│   ├── screens/                  # Login, register, forgot password
│   ├── viewmodels/
│   └── components/
│
├── feature-profile/              # Shared profile feature module
│   ├── screens/
│   ├── viewmodels/
│   └── components/
│
├── shared-ui/                    # Shared UI components and theme
│   ├── theme/                    # Material 3 theme, colors, typography
│   └── components/               # Reusable composables
│
├── api-spec/                     # OpenAPI specifications
├── scripts/                      # Build and generation scripts
├── build.gradle.kts
└── settings.gradle.kts
```

### iOS

```
cleansia_ios/
├── CleansiaPartner/              # Partner app target
│   ├── App/                      # App entry point, configuration
│   ├── Features/
│   │   ├── Dashboard/
│   │   ├── Invoices/
│   │   ├── Onboarding/
│   │   ├── Orders/
│   │   └── Settings/
│   ├── Navigation/
│   └── Resources/                # Assets, localization
│
├── CleansiaCustomer/             # Customer app target
│   ├── App/
│   ├── Features/
│   │   ├── Catalog/
│   │   ├── Checkout/
│   │   ├── Disputes/
│   │   ├── GDPR/
│   │   ├── Orders/
│   │   └── Wizard/
│   ├── Navigation/
│   └── Resources/
│
├── CleansiaCore/                 # Shared Swift Package
│   ├── Sources/
│   │   ├── Networking/           # URLSession, API client, error handling
│   │   ├── Storage/              # UserDefaults, Keychain wrappers
│   │   ├── Security/             # Biometrics, encryption
│   │   ├── Models/               # Shared domain models
│   │   └── Extensions/           # Swift extensions
│   └── Tests/
│
├── CleansiaUI/                   # Shared UI Swift Package
│   ├── Sources/
│   │   ├── Theme/                # Colors, fonts, spacing
│   │   └── Components/           # Reusable SwiftUI views
│   └── Tests/
│
├── CleansiaAuth/                 # Shared auth Swift Package
│   ├── Sources/
│   │   ├── Screens/
│   │   ├── ViewModels/
│   │   └── Components/
│   └── Tests/
│
├── CleansiaProfile/              # Shared profile Swift Package
│   ├── Sources/
│   │   ├── Screens/
│   │   └── ViewModels/
│   └── Tests/
│
├── Package.swift                 # Swift Package Manager manifest
└── CleansiaWorkspace.xcworkspace
```

---

## Testing Strategy

### Unit Tests

| Platform | Framework    | Focus Areas                                        |
|----------|--------------|----------------------------------------------------|
| Android  | JUnit + MockK + Turbine | ViewModels, repositories, use cases, mappers |
| iOS      | XCTest       | ViewModels, services, repositories, mappers        |

### UI Tests

| Platform | Framework              | Focus Areas                              |
|----------|------------------------|------------------------------------------|
| Android  | Compose Testing        | Screen rendering, navigation, user flows |
| iOS      | XCUITest               | Screen rendering, navigation, user flows |

### Coverage Targets

- [ ] 70%+ code coverage on `core` modules
- [ ] 70%+ code coverage on `feature` modules
- [ ] Critical flows covered by UI tests: auth, order creation, payment
- [ ] Mock flavor (Android) / preview mocks (iOS) for offline development

### API Contract Testing

- Validate generated API clients against the OpenAPI spec on every backend change
- CI step: regenerate clients and verify compilation

---

## App Store Distribution

### Android — Google Play Store

- **Partner app:** `cz.cleansia.partner`
- **Customer app:** `cz.cleansia.customer`
- Separate Play Store listings
- Internal testing track for beta builds
- Staged rollouts for production releases

### iOS — Apple App Store

- **Partner app:** Cleansia Partner
- **Customer app:** Cleansia Customer
- Separate App Store listings
- TestFlight for beta distribution
- App Review guidelines compliance

### CI/CD Pipeline

| Stage            | Android                          | iOS                               |
|------------------|----------------------------------|-----------------------------------|
| Build            | Gradle                           | xcodebuild / Swift Build          |
| Test             | `./gradlew test`                 | `xcodebuild test`                 |
| Lint             | Android Lint + ktlint            | SwiftLint                         |
| Code Gen         | OpenAPI Generator Gradle plugin  | OpenAPI Generator CLI             |
| Distribution     | Fastlane + Google Play           | Fastlane + TestFlight + App Store |
| Signing          | Gradle signing configs           | Xcode automatic / manual signing  |
| Environments     | debug / staging / release        | debug / staging / release         |

### Release Checklist (per app, per platform)

- [ ] All tests passing
- [ ] Localization complete (EN, CS, SK, UK, RU)
- [ ] App Store screenshots (all required device sizes)
- [ ] App Store description and metadata
- [ ] Privacy policy URL
- [ ] GDPR compliance verified
- [ ] Performance profiling (no memory leaks, smooth scrolling)
- [ ] Accessibility audit (VoiceOver / TalkBack)
- [ ] Beta testing round completed
- [ ] Version number and build number incremented
- [ ] Release notes prepared

---

## Overall Progress Tracker

### Phase 1: iOS Partner App
- [ ] Project scaffolding and CI setup
- [ ] Core module (networking, storage, security)
- [ ] Authentication feature
- [ ] Dashboard feature
- [ ] Orders feature
- [ ] Invoices feature
- [ ] Profile feature
- [ ] Settings and onboarding
- [ ] Push notifications
- [ ] Localization (5 languages)
- [ ] Testing (70%+ coverage)
- [ ] TestFlight beta
- [ ] App Store submission

### Phase 2: Android Customer App
- [ ] Multi-module restructure (extract core from Partner)
- [ ] Customer app module setup
- [ ] Authentication with email confirmation
- [ ] Service catalog
- [ ] Order wizard (5-step flow)
- [ ] Stripe payment integration
- [ ] Order history and tracking
- [ ] Dispute management
- [ ] GDPR compliance
- [ ] Profile management
- [ ] Push notifications
- [ ] Localization (5 languages)
- [ ] Testing (70%+ coverage)
- [ ] Internal testing beta
- [ ] Play Store submission

### Phase 3: iOS Customer App
- [ ] Customer app target setup
- [ ] Authentication with email confirmation
- [ ] Service catalog
- [ ] Order wizard (5-step flow)
- [ ] Stripe iOS SDK integration
- [ ] Order history and tracking
- [ ] Dispute management
- [ ] GDPR compliance
- [ ] Profile management
- [ ] Push notifications
- [ ] Localization (5 languages)
- [ ] Testing (70%+ coverage)
- [ ] TestFlight beta
- [ ] App Store submission
