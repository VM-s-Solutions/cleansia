# ARCH-001 — Android monorepo + shared `:core` module

> Extracted from `decisions-infra-arch.md` once the trigger fired: a
> session began with both apps stable and the owner committed to the
> 2-3 day refactor. Customer-side patterns are canonical; partner adopts
> them as part of the same move.

## TL;DR

The two Android apps share a lot of conceptual surface (theme, components,
auth, network) but **almost zero byte-identical code**. The customer app
was rewritten Phase A onward and uses materially different idioms (TokenStore
vs partner's TokenManager, separate `CleansiaPrimaryButton`/`SecondaryButton`
vs partner's enum-driven `CleansiaButton`, AGP 8.9.1 + Kotlin 2.1.10 + Java
21 vs partner's AGP 8.13.2 + Kotlin 2.0.20 + Java 17).

So this isn't "move ~30 files to :core". It's a **6-phase rewrite**:
align the build toolchain → align the auth/network plumbing → extract
shared primitives → rewrite partner call sites against the new APIs → 
verify both apps build + smoke-test.

**Estimated total: 3 working days.** Each phase is independently verifiable
and can be checkpointed.

---

## Pre-flight: what the two apps look like today

### Build toolchain divergence
| Setting | Partner (`cleansia_android`) | Customer (`cleansia_customer_android`) |
|---|---|---|
| AGP | 8.13.2 (newer) | 8.9.1 |
| Kotlin | 2.0.20 | 2.1.10 (newer) |
| KSP | 2.0.20-1.0.25 | 2.1.10-1.0.31 |
| Java target | 17 | 21 + core library desugaring |
| Hilt | 2.52 | 2.54 |
| Compose BOM | 2024.11.00 | 2025.02.00 |
| Coil | 2.7.0 | 3.0.4 |
| Spotless/ktlint | none | 6.25.0 / 1.3.1 |
| Build flavors | `prod` / `mock` | none |
| Google Services plugin | no | yes (FCM) |
| Mapbox repo | no | yes (Mapbox Maps SDK) |
| OpenAPI generator config | imperative `tasks.register<GenerateTask>` | declarative `openApiGenerate { }` block |

**Implication:** Aligning these is itself a phase. We can't just `apply
plugin 'com.android.library'` to a `:core` module and have it consume from
both apps — each app's compileSdk/AGP/Kotlin must match the `:core` module.

### File divergence (audit findings)
- **`core/network/`** — partner has 7 files (ApiError, ApiErrorTranslator,
  ApiResult, ApiService, AuthInterceptor, GeneratedApiAdapter,
  NetworkMonitor, SafeApiCall). Customer has only 2 (IntEnumSerializers,
  NetworkCall). They evolved entirely separately.
- **`core/auth/`** — exists only in customer (12 files: AuthApi, Auth-
  Authenticator, AuthInterceptor, AuthModule, AuthRepository, etc.).
  Partner equivalents live in `core/storage/TokenManager.kt` and
  `core/network/AuthInterceptor.kt`. **No structural overlap.**
- **`ui/theme/`** — both have Color/Spacing/Type/Theme.kt files but with
  different naming conventions (partner: `Spacing.lg`, customer:
  `Spacing.M`), different values, different colour tokens. Neither app
  actually **uses** its own `Spacing` constants — dead code in both
  (verified by grep).
- **`ui/components/CleansiaButton`** — completely different APIs:
  - Partner: `CleansiaButton(text, onClick, style: CleansiaButtonStyle.PRIMARY)`
  - Customer: `CleansiaPrimaryButton(text, onClick, size: CleansiaButtonSize.Large)`
- **`ui/components/CleansiaTextField`** — partner 142 lines, customer 83
  lines, different field shapes.

### App-specific (correctly so — not migration candidates)
- Partner: Room database (`CleansiaDatabase`, 3 DAOs, 3 cached-entity
  classes), `NotificationHelper` + `OrderTimerService` for foreground
  service work, biometric auth, the entire `database/` package, ~24
  partner-only `ui/components/*` (FilterDrawer, Charts, OfflineBanner,
  SwipeToConfirmButton with shimmer + haptics, etc.).
- Customer: Booking flow (BookingApi/Dtos/Module), Catalog (anonymous
  pre-login fetch), Memberships, RecurringBookings, Loyalty, Promo,
  Referral, Disputes, SavedAddresses, Stripe payments, Mapbox location,
  Sentry tracker, GoogleSignInController, the entire FCM stack (Firebase
  Messaging Service + 7 notification files), ~6 customer-only `ui/components/*`
  (MascotAnimation, BusyMascotOverlay, CleansiaBrandWordmark, etc.).

### Scope reality check
- Genuinely shared concepts: theme, auth/network plumbing, snackbar host,
  formatters, ~3 UI primitives (Button, TextField, SectionHeader).
- Customer's evolved code is the canonical baseline.
- Partner gets **rewritten** to consume `:core` APIs; this isn't a
  "lift-and-shift" of either app's files.

---

## Target architecture

```
src/cleansia_android/                  # NEW root project
├── settings.gradle.kts                       # Includes :core, :partner-app, :customer-app
├── gradle/
│   └── libs.versions.toml                    # Unified catalog (Customer's, with Partner additions)
├── core/                                     # NEW module — cz.cleansia.core
│   ├── build.gradle.kts                      # library, compileSdk 35, Java 21
│   └── src/main/java/cz/cleansia/core/
│       ├── network/                          # NetworkCall, IntEnumSerializers, NetworkErrorInterceptor
│       ├── auth/                             # TokenStore, AuthInterceptor, AuthAuthenticator (interfaces + customer impls)
│       ├── snackbar/                         # SnackbarController + GlobalSnackbarHost
│       ├── ui/theme/                         # Spacing, Type, base Color tokens (unified naming)
│       ├── ui/components/                    # CleansiaPrimaryButton, CleansiaSecondaryButton,
│       │                                     # CleansiaOutlinedButton, CleansiaTextField, CleansiaSectionHeader
│       └── format/                           # OrderFormatters, DisputeFormatters
├── partner-app/                              # MOVED from src/cleansia_android/app/
│   ├── build.gradle.kts                      # Keeps partner-specific config (Room, biometric, flavors)
│   └── src/main/java/cz/cleansia/partner/    # Stripped of moved code
└── customer-app/                             # MOVED from src/cleansia_customer_android/app/
    ├── build.gradle.kts                      # Keeps customer-specific config (Firebase, Mapbox, Stripe)
    └── src/main/java/cz/cleansia/customer/   # Stripped of moved code
```

The two existing app directories (`src/cleansia_android/`,
`src/cleansia_customer_android/`) get **deleted** at the end of Phase 6.
The new layout has a single Gradle root.

---

## Phase 1 — Toolchain alignment (Day 1 AM, ~4h)

Both apps must build on the same AGP/Kotlin/KSP. Adopt the **newer of
each** since downgrading is a lateral move at best.

### Tasks
1. **Bump partner to Kotlin 2.1.10 + KSP 2.1.10-1.0.31.**
   - Update `gradle/libs.versions.toml`.
   - Re-run partner build; fix any deprecation warnings KSP raises.
2. **Bump customer to AGP 8.13.2.**
   - Update `libs.versions.toml`.
   - Customer's `google-services` plugin needs compatible version (check
     compatibility matrix).
3. **Decide Java target.** Both should land on Java 21 + core library
   desugaring (customer's setup). Partner's `compileOptions { sourceCompatibility
   = JavaVersion.VERSION_17 }` becomes 21; add desugaring dep.
4. **Compose BOM:** adopt customer's 2025.02.00 in partner.
5. **Hilt:** adopt customer's 2.54 in partner.
6. **Coil:** adopt customer's 3.0.4 in partner. **Breaking** — Coil 3 has
   API changes; partner has ~5 `AsyncImage` call sites (verify in
   `features/orders/`).

### Verification
- `./gradlew :app:compileProdDebugKotlin :app:testDebugUnitTest` green
  in **both** existing app directories before moving on.
- Smoke-test both apps on emulator (run, sign in, hit a screen).

### Risk
If Coil 3 has breaking changes in partner, this could balloon. Pre-check
by reading the Coil 2 → 3 migration guide before starting.

---

## Phase 2 — Unified version catalog + root settings.gradle (Day 1 PM, ~3h)

### Tasks
1. **Create `src/cleansia_android/`** with a root `settings.gradle.kts`.
2. **Merge `libs.versions.toml`** — start from customer's, add partner-
   only entries (Room, biometric, lottie, splashscreen).
3. **Move both app directories temporarily**:
   - `src/cleansia_android/app` → `src/cleansia_android/partner-app`
   - `src/cleansia_customer_android/app` → `src/cleansia_android/customer-app`
   - Leave the old wrapper dirs for Phase 6 cleanup.
4. **Each `app/build.gradle.kts` keeps its existing config** but stops
   declaring repos + plugin versions (the root settings handles that).
5. **Root `settings.gradle.kts`:**
   ```kotlin
   rootProject.name = "CleansiaAndroid"
   include(":partner-app", ":customer-app")
   ```
   (`:core` lands in Phase 3.)
6. **Mapbox repo** moves to root settings (customer-only consumer for
   now; partner doesn't need it).

### Verification
- `./gradlew :partner-app:compileProdDebugKotlin :customer-app:compileDebugKotlin`
  green from the new root.
- Run both apps on emulator.

### Risk
- Spotless config currently scoped to customer's `app/`. Either narrow
  it to `:customer-app` for now or extend to both (low risk).
- Each app keeps its own `applicationId` + namespace — confirm both
  still install side-by-side on a device.

---

## Phase 3 — `:core` module skeleton + auth/network migration (Day 2 AM, ~4h)

### Tasks
1. **Create `:core` Android library module** at `src/cleansia_android/core/`.
   - `cz.cleansia.core` package root.
   - Compose enabled, Hilt enabled, KSP enabled.
   - Depends on: AndroidX core, Compose Material3, kotlinx-serialization,
     Retrofit, OkHttp, Hilt, DataStore, Security-Crypto.
   - **Does NOT** depend on app-specific libs (no Stripe, no Mapbox, no Firebase).
2. **Move from customer to `:core`:**
   - `core/network/NetworkCall.kt` → `cz/cleansia/core/network/NetworkCall.kt`
   - `core/network/IntEnumSerializers.kt` → `cz/cleansia/core/network/IntEnumSerializers.kt`
     (rename: drop customer-specific imports, accept enum classes as parameters)
   - `core/auth/AuthInterceptor.kt` → `cz/cleansia/core/auth/AuthInterceptor.kt`
   - `core/auth/AuthAuthenticator.kt` → `cz/cleansia/core/auth/AuthAuthenticator.kt`
   - `core/auth/TokenStore.kt` → `cz/cleansia/core/auth/TokenStore.kt`
   - `core/auth/NetworkErrorInterceptor.kt` → `cz/cleansia/core/auth/NetworkErrorInterceptor.kt`
   - `core/auth/SessionManager.kt`, `SessionScopedCache.kt`, `SessionScopedModule.kt`,
     `JwtDecoder.kt` → `cz/cleansia/core/auth/`
   - `ui/snackbar/SnackbarController.kt` → `cz/cleansia/core/snackbar/SnackbarController.kt`
   - `ui/snackbar/GlobalSnackbarHost.kt` → `cz/cleansia/core/snackbar/GlobalSnackbarHost.kt`
   - `ui/snackbar/SnackbarInset.kt` → `cz/cleansia/core/snackbar/SnackbarInset.kt`
3. **AuthApi / AuthRepository stay app-specific** — each app has its own
   set of `/Auth/*` endpoints (partner's is hand-written, customer's is
   NSwag-generated). `:core` only owns the OkHttp wiring (TokenStore +
   Interceptor + Authenticator).
4. **Update customer-app:**
   - Replace customer's `core/network/*`, `core/auth/*` (except AuthApi/Repository),
     `ui/snackbar/*` with imports from `cz.cleansia.core.*`.
   - `customer-app/build.gradle.kts` adds `implementation(project(":core"))`.
5. **Build customer-app** to verify everything resolves.
6. **Then partner-app:**
   - Delete `partner/core/network/AuthInterceptor.kt`, `partner/core/storage/TokenManager.kt`
     (replaced by `:core`).
   - Delete `partner/core/network/{ApiError, ApiErrorTranslator, ApiResult,
     SafeApiCall, GeneratedApiAdapter, NetworkMonitor}.kt` (partner's
     bespoke error stack; replace with `:core`'s `NetworkCall`).
   - Rewrite partner's auth wiring (`AuthModule` if it exists, otherwise
     wherever the OkHttp builder lives) to use `core.auth.TokenStore` +
     `AuthInterceptor` + `AuthAuthenticator`.
   - Rewrite partner's `AuthApi` to match the customer-style auth flow if
     materially different.
   - Update every partner-side `.subscribe(`/`callApi {`/etc. that used the
     old error pipeline to use `NetworkCall { … }` instead.

### Verification
- Both apps build green.
- **Smoke test:** sign in to each app. The auth token plumbing is the
  highest-risk change; if 401 → refresh → retry doesn't work, partner's
  whole network layer breaks silently.

### Risk
This phase touches the most load-bearing code in both apps. If something
breaks, it can break in subtle ways (token persists across logouts, etc.).
**Mitigation:** commit after Phase 2; if Phase 3 goes sideways, revert
just this phase.

---

## Phase 4 — `:core` UI primitives + theme (Day 2 PM, ~4h)

### Tasks
1. **Move customer's UI primitives to `:core`:**
   - `ui/components/CleansiaButton.kt` (Primary/Secondary/Outlined/TextButton variants)
   - `ui/components/CleansiaTextField.kt`
   - `ui/components/CleansiaSectionHeader.kt`
   - `ui/components/CleansiaCheckbox.kt`
   - `ui/components/CleansiaDialog.kt`
   - `ui/components/LabelledDivider.kt`
2. **Move customer's theme to `:core`:**
   - `ui/theme/Spacing.kt` (XXS/XS/S/M/ML/L/XL/XXL naming)
   - `ui/theme/Type.kt`
   - `ui/theme/Shape.kt`
   - Base colour palette (Slate, Sky, semantic) — keep as `core.ui.theme.CleansiaColors`
   - Brand gradients — keep customer-app-specific (they're marketing-y)
3. **`CleansiaTheme` composable** stays in each app:
   - Each app's `Theme.kt` declares its own `colorScheme` + `Typography`
     and wraps content with `MaterialTheme { … }`.
   - `:core` doesn't dictate the theme shape, only the tokens.
4. **Update customer-app:**
   - Delete moved files from `customer/ui/`.
   - Update all 18 customer feature files' imports from
     `cz.cleansia.customer.ui.components.CleansiaPrimaryButton` →
     `cz.cleansia.core.ui.components.CleansiaPrimaryButton`.
5. **Rewrite partner's UI to consume `:core` primitives:**
   - Delete `partner/ui/components/CleansiaButton.kt`,
     `CleansiaTextField.kt`, `SectionHeader.kt`.
   - Update ~15 partner call sites that used the old `CleansiaButton(text,
     style=PRIMARY)` API:
     - `CleansiaButton(text="Save", style=CleansiaButtonStyle.PRIMARY)` →
       `CleansiaPrimaryButton(text="Save", onClick=…)`
     - `CleansiaButton(style=CleansiaButtonStyle.OUTLINED)` →
       `CleansiaOutlinedButton(…)`
   - `CleansiaTextField` call sites (6 files) similarly.
6. **Partner's `ui/theme/Spacing.kt`** — delete (dead code), partner adopts
   `core.ui.theme.Spacing` (no call sites to update since neither uses it).
7. **Partner's app-specific theme tokens** (`Color.kt`'s Timer + Workflow
   objects) — keep in `partner-app/ui/theme/`.

### Verification
- Both apps build green.
- **Visual smoke test:** Open every screen in each app on emulator. UI
  shouldn't visually shift since we're keeping each app's `colorScheme`
  + `Typography`; only primitive call signatures changed.

### Risk
Partner's existing button styling may not exactly match customer's
button. Visual deltas (rounded corners, padding, height) are expected and
benign — but worth a sanity check before merging.

---

## Phase 5 — Cross-app utilities + Sentry / format helpers (Day 3 AM, ~3h)

### Tasks
1. **Move customer's format helpers to `:core`:**
   - `ui/format/OrderFormatters.kt` (status labels, time formatting)
   - `ui/format/DisputeFormatters.kt`
   - Equivalent partner files exist (`core/utils/DateTimeUtils.kt`,
     `core/utils/CurrencyUtils.kt`); consolidate.
2. **Move Sentry tracker to `:core`:**
   - `core/auth/SentryUserTracker.kt` → `cz/cleansia/core/sentry/SentryUserTracker.kt`
   - Wrapped in a no-op default impl for partner (Sentry isn't currently
     wired in partner). Adding Sentry to partner is **out of scope** —
     this just moves the helper.
3. **HapticFeedback** (partner-only currently) — move to `:core` so
   customer can adopt it later. Keep both apps using it via injection.

### Verification
- Both apps build green.

---

## Phase 6 — Cleanup, repo restructure, final verification (Day 3 PM, ~3h)

### Tasks
1. **Delete the old wrapper directories:**
   - `src/cleansia_android/` (now empty)
   - `src/cleansia_customer_android/` (now empty)
2. **Update CLAUDE.md** root quick-reference:
   - Path: `Mobile | Kotlin, Jetpack Compose, MVVM + Hilt | src/cleansia_android/`
3. **Update CI configs** if any reference the old paths (check `.github/workflows/`).
4. **`./gradlew clean :partner-app:assembleProdDebug :customer-app:assembleDebug`
   from the new root.**
5. **Run each app on emulator. Sign in to both. Exercise the golden path
   in each (partner: take an order; customer: book a cleaning).**
6. **Update SHIPPED-SUMMARY.md** with the ARCH-001 summary.

### Verification
- Both APKs install side-by-side (different `applicationId`).
- All ~110 unit tests across both apps green.

---

## Risk register

| Risk | Likelihood | Impact | Mitigation |
|---|---|---|---|
| Coil 2 → 3 migration breaks partner image loading | Medium | High | Pre-read migration guide before Phase 1. Check call sites with `AsyncImage`. |
| AGP 8.13.2 + Google Services plugin compatibility | Low | High | Verify the plugin's compatibility matrix during Phase 1. Worst case: pin Google Services to a compatible older version. |
| Auth migration breaks partner sign-in silently | Medium | Critical | Phase 3 ends with a manual sign-in test before merging. Keep partner's old TokenManager in a branch for quick revert. |
| Partner's button styling shifts visually | High | Low | Expected; this IS the consolidation. QA on emulator confirms it's not regression-bad. |
| KSP version bump triggers cascade of compiler errors in partner | Low | Medium | Bump KSP separately from Kotlin so the cause is clear. |
| Files actually depended on by an in-flight commit | Low | High | Confirm both apps' `git status` is clean before starting. |

## What stays app-specific (NOT moved)

These are app-specific by design and would create more coupling than they
remove if shared:

- All `features/` trees in both apps
- Partner: `core/database/` (Room), `core/notifications/{NotificationHelper, OrderTimerService}.kt`,
  `core/security/BiometricHelper.kt`, `core/storage/PreferencesManager.kt`,
  partner's 24 app-specific UI components (FilterDrawer, Charts, etc.),
  flavor configs (`prod`/`mock`)
- Customer: All `core/{booking, catalog, disputes, loyalty, memberships,
  notifications, orders, payments, promo, recurring, referral, user}/`,
  `core/location/*`, `core/settings/AppSettings*.kt`, `core/data/AddressRepository.kt`,
  customer's 6 brand-y UI components (MascotAnimation, etc.), Firebase
  service, Stripe config, Mapbox config, signing config
- Each app's `MainActivity`, `AppNavigation`, `App.kt`, `AndroidManifest.xml`
- Each app's `AuthApi` + `AuthRepository` (different endpoint sets:
  partner has employee/availability/payroll endpoints, customer has
  booking/Plus/loyalty endpoints)
- Each app's `gradle.properties`, `proguard-rules.pro`, `res/` (i18n
  strings differ entirely)

---

## Out of scope explicitly

- iOS — no iOS app exists
- Full multi-flavor monorepo (partner+customer as flavors of one app) —
  rejected by the original spec; we'd lose `prod`/`mock` flavor on
  partner side
- Adding Sentry to partner (Sentry tracker moves to `:core` but partner
  doesn't wire it up here)
- Rewriting partner's `SwipeToConfirmButton` to match customer's (intentionally
  different UX per the spec)
- Migrating partner's Room database to anything (stays app-specific)

---

## Decision summary

| Item | Decision |
|---|---|
| Customer's code is canonical | ✅ confirmed by owner |
| Gradle layout | Single project at `src/cleansia_android/` |
| Toolchain | Bump partner to match customer (Kotlin 2.1, Java 21, Compose BOM 2025.02), then bump AGP everywhere to 8.13 |
| Scope | All 6 phases, ~3 working days |
| Verification gate | After each phase: build + emulator smoke test |
| Rollback unit | Per phase — commit after each |
