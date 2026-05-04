# Cleansia Customer Android — Play Store Readiness Audit

Audit date: 2026-04-18. Target: `src/cleansia_customer_android` (applicationId `cz.cleansia.customer`, versionName 0.1.0).

Verdict: NOT Play Store ready. Multiple P0 blockers (no signing keystore, no networking/auth layer, no privacy policy, no account deletion, no crash reporting, 3 of 4 non-English locales only have 2 strings each).

---

## 1. Build & signing

**Finding.** `app/build.gradle.kts:11-63` — namespace `cz.cleansia.customer`, applicationId `cz.cleansia.customer`, versionCode `1`, versionName `"0.1.0"`, minSdk `26`, targetSdk `35`, compileSdk `35`. Release buildType has `isMinifyEnabled = true`, `isShrinkResources = true`, proguardFiles `proguard-android-optimize.txt` + `proguard-rules.pro` (`app/proguard-rules.pro` — 21 lines covering kotlinx.serialization, Retrofit, Hilt, OkHttp).

signingConfigs.release is declared at `app/build.gradle.kts:36-46` but only wires up if `rootProject.file("keystore/release.jks")` exists. No keystore file present in repo (confirmed via `ls` of root). Env vars `RELEASE_KEYSTORE_PASSWORD`, `RELEASE_KEY_ALIAS`, `RELEASE_KEY_PASSWORD` expected — unset locally. If keystore missing, the `signingConfig = signingConfigs.getByName("release")` at line 61 resolves to an empty config and release build will be unsigned.

No `bundle { }` block — relies on Android Gradle Plugin defaults (AAB produced via `bundleRelease`, which works but is not explicitly configured for density/language/abi splits).

- Risk: High
- Blocker: **Yes** (no real signing config, no AAB tuning)

## 2. Manifest (Play Policy compliance)

**Finding.** `app/src/main/AndroidManifest.xml`:
- Permissions (lines 5-9): `INTERNET`, `POST_NOTIFICATIONS`, `USE_BIOMETRIC`, `ACCESS_FINE_LOCATION`, `ACCESS_COARSE_LOCATION`. `USE_BIOMETRIC` declared but no biometric code exists (§4/5).
- No `<queries>` element.
- `android:allowBackup="false"` (line 13) — good.
- `android:dataExtractionRules="@xml/data_extraction_rules"` + `android:fullBackupContent="@xml/backup_rules"` present (lines 14-15) — excludes `cleansia_secure.xml` and `auth_prefs.xml`, but neither file currently exists in code.
- No `android:networkSecurityConfig`.
- No `android:usesCleartextTraffic` attribute (defaults to `false` on targetSdk 28+, OK).
- `MainActivity` has `android:exported="true"` (line 26), single LAUNCHER intent-filter, no deep links, no App Links.
- `tools:targetApi="34"` on the `<application>` tag (line 22) conflicts with the actual `targetSdk = 35` — cosmetic, not a blocker.
- targetSdk 35 is current-year requirement. Compliant.

- Risk: Medium (missing `<queries>` will break any future `startActivity` for browser/phone/email intents; `USE_BIOMETRIC` declared but unused looks sloppy to reviewers)
- Blocker: No (but clean up before submit)

## 3. Secrets & keys

**Finding.** Mapbox access token injected via `buildConfigField` at `app/build.gradle.kts:30-33` from `gradle.properties` or env — good pattern. Consumed at `app/src/main/java/cz/cleansia/customer/CleansiaApp.kt:13` and `core/location/LocationModule.kt:41`. Mapbox downloads token read in `settings.gradle.kts:28-30` from gradle properties — good.

`local.properties` (repo root) contains only `sdk.dir` — no secrets. No hardcoded API keys/tokens found elsewhere. No `<meta-data>` tags in manifest. BuildConfig declarations only the Mapbox token.

Concern: Mapbox **public** token ships in APK/AAB by definition. If scoped to URL restrictions in Mapbox dashboard this is fine; worth confirming.

- Risk: Low
- Blocker: No

## 4. Dependencies

**Finding.** Versions from `gradle/libs.versions.toml`:
- AGP `8.9.1`, Kotlin `2.1.10`, KSP `2.1.10-1.0.31`, Hilt `2.54`, Compose BOM `2025.02.00`, Navigation `2.8.7`, Coroutines `1.10.1`, Retrofit `2.11.0`, **OkHttp `4.12.0`** (OK, no known CVEs), Coil `3.0.4`, DataStore `1.1.1`, security-crypto `1.1.0-alpha06` (alpha — risk), splashscreen `1.0.1`, lifecycle `2.8.7`, androidxCore `1.15.0`, Mapbox `11.8.0`, play-services-location `21.2.0`.

Not included: **Crashlytics, Firebase Analytics, Sentry, Bugsnag, LeakCanary, Timber, androidx.biometric.** Only JUnit 4 (no UI/instrumentation tests).

- Risk: Medium — `security-crypto 1.1.0-alpha06` in production is risky (AOSP has paused this library, users may see crashes on certain devices); no crash reporting means any production crash is invisible.
- Blocker: **Yes** for crash reporting (Play Policy strongly expects reviewable crash data, and without it you cannot triage ANRs the console will surface)

## 5. Auth & token storage

**Finding.** **No auth implementation exists.** Grep across `app/src/main/java` returned zero hits for `TokenStorage`, `AuthRepository`, `AuthInterceptor`, `Bearer`. No `Retrofit.Builder()` anywhere. No `baseUrl` string. Only `OkHttpClient` usage is in `core/location/LocationModule.kt:30-33` for Mapbox geocoding.

The only DataStore instances are `core/settings/AppSettingsRepository.kt:11` (`app_settings`) and `core/data/AddressRepository.kt:13` (`user_addresses`). Neither stores auth tokens.

`features/auth/SignInScreen.kt` and `SignUpScreen.kt` exist purely as UI — button callbacks are `onSignInClick(email, password)` lambdas that are wired to nothing persistent.

Logout flow: `features/profile/ProfileTab.kt:156` renders a LogoutRow with an `onLogout: () -> Unit = {}` callback — no token clearing, no session clearing, because there is no session.

No biometric wiring.

- Risk: High
- Blocker: **Yes** — shipping an app that cannot authenticate is a non-starter

## 6. Network security

**Finding.** No `network_security_config.xml` file (confirmed via `ls res/xml/`). No certificate pinning anywhere. No `HttpLoggingInterceptor` usage anywhere (grep confirms) — which means the logging dependency at `app/build.gradle.kts:136` is currently unused. Good for release leak risk, but also means zero network observability in debug.

- Risk: Low-Medium (no pinning is acceptable for a mid-risk consumer app; add one before handling payments)
- Blocker: No

## 7. Privacy & data

**Finding.**
- `res/values/strings.xml:31` has `register_terms_and_conditions` label but `SignUpScreen.kt:182-184` just renders a checkbox — no links to any Terms or Privacy URL anywhere in the codebase.
- Profile screen has a "Privacy & data" row (`features/profile/ProfileTab.kt:99` + `res/values/strings.xml:240`) but `onRowClick("privacy")` is not handled in navigation — dead row.
- **No account-deletion flow.** Grep for `DeleteAccount`/`delete account` returns zero matches. Google Play Account Deletion policy (effective since May 2024) requires both in-app AND web-based deletion paths. **Hard blocker.**
- No GDPR/cookie/consent banner.
- No analytics or tracking SDK.

- Risk: High
- Blocker: **Yes** (account deletion is a mandatory Play Policy item)

## 8. Accessibility

**Finding.** 30 `contentDescription` occurrences across 16 files; of those **21 are `contentDescription = null`** across 15 files, including `CleansiaButton.kt` (3), `AddressManagerScreen.kt` (2), `SignUpScreen.kt` (2), `EditProfileScreen.kt` (2). Many are legitimate (decorative icons beside visible text labels), but several tappable icons in `BookingBottomSheet.kt`, `OrderDetailScreen.kt`, `BookingSuccessScreen.kt` need audit — could not verify per-usage from counts alone.

Hardcoded English in code: `CleansiaTextField.kt:69` `contentDescription = if (passwordVisible) "Hide password" else "Show password"` — not localized. `OrderDetailScreen.kt` has 3 hardcoded string literals (lines 182, 209, 299) — e.g. `"${order.rooms} rooms · ${order.bathrooms} bath"`.

No `semantics` modifiers found anywhere in code. Touch-target audit: 48.dp/40.dp/32.dp/24.dp sizes appear 95 times — many .size(32.dp) and .size(18.dp) wrap clickable icons in `ProfileTab.kt:407-416`, `SwipeToConfirmButton.kt`, etc. Under the 48dp Material minimum in several places; risk of failing TalkBack audits.

- Risk: Medium
- Blocker: No (but expect Play pre-launch report flags)

## 9. i18n & localization

**Finding.** `res/values/strings.xml` has **488 lines / ~330 strings** (en). Translations:
- `values-cs/strings.xml` — 202 lines (roughly 60% coverage)
- `values-sk/strings.xml` — **5 lines** (only `app_name` + `splash_tagline`)
- `values-uk/strings.xml` — **5 lines** (only `app_name` + `splash_tagline`)
- `values-ru/strings.xml` — **5 lines** (only `app_name` + `splash_tagline`)

`build.gradle.kts:24` declares `resourceConfigurations += listOf("en", "cs", "sk", "uk", "ru")`, and `res/xml/locales_config.xml` lists all five — so a Slovak/Ukrainian/Russian user gets 2 strings in their language and the rest in English (fallback). Declaring locales you can't actually serve is embarrassing but not a policy blocker.

Hardcoded user-visible English: `CleansiaTextField.kt:69` password-toggle description; `OrderDetailScreen.kt:182,209,299` (rooms/rating/review).

- Risk: Medium
- Blocker: No (ship with en+cs only, remove sk/uk/ru from resourceConfigurations until translated)

## 10. Crash reporting & observability

**Finding.** No Crashlytics, no Sentry, no Bugsnag in deps or code. No `Thread.setDefaultUncaughtExceptionHandler` usage (grep confirms). `CleansiaApp.kt` does only Mapbox token init — nothing else in Application.onCreate.

- Risk: High
- Blocker: **Yes** — Play Console will surface ANR/crash rates and you have no way to debug them

## 11. App icons & adaptive icon

**Finding.** `res/mipmap-anydpi-v26/ic_launcher.xml` and `ic_launcher_round.xml` both exist. Each has `<background>` (colorRes `@color/ic_launcher_background` = `#0284C7` at `res/values/colors.xml:6`), `<foreground>` (`@drawable/ic_launcher_foreground`), `<monochrome>` (same foreground drawable reused) — Android 13 themed-icon compliant. No legacy `mipmap-xxxhdpi/ic_launcher.png` set. Most OEM launchers handle adaptive-only fine, but Play may flag missing legacy PNG for older launchers.

- Risk: Low
- Blocker: No

## 12. Splash screen

**Finding.** `res/values/themes.xml:5-9` defines `Theme.Cleansia.Splash` with parent `Theme.SplashScreen`, setting `windowSplashScreenBackground`, `windowSplashScreenAnimatedIcon`, and `postSplashScreenTheme = @style/Theme.Cleansia`. Manifest (line 20, 29) applies `Theme.Cleansia.Splash` to application and MainActivity. `androidx.core:core-splashscreen:1.0.1` is in deps. Properly configured.

`MainActivity.onCreate` not inspected in this pass — should call `installSplashScreen()` before `super.onCreate()`; worth confirming.

- Risk: Low
- Blocker: No

## 13. Back navigation & predictive back

**Finding.** `android:enableOnBackInvokedCallback` **not set** anywhere (grep confirms zero matches in manifest). On Android 13+/14, app will fall back to legacy back behavior — works but skips predictive-back preview gesture. Will be required in an upcoming Android release.

- Risk: Low
- Blocker: No (yet)

## 14. App category, content rating

**Finding.** `android:label="@string/app_name"` → "Cleansia" (all locales). No `android:appCategory` on `<application>`. No Play Console metadata (that's submission-time, not code).

- Risk: Low
- Blocker: No

## 15. Play Policy-specific

**Finding.**
- **Location** (`ACCESS_FINE_LOCATION` + `ACCESS_COARSE_LOCATION` at manifest lines 8-9): used by `core/location/LocationService.kt` via FusedLocationProvider, for "my location" on booking address picker. **No prominent-disclosure UI** present before permission request. Play requires a pre-permission dialog explaining use in plain language.
- **POST_NOTIFICATIONS** (line 6): no runtime permission rationale dialog visible in code.
- **No foreground services** declared.
- **No accessibility services** declared.
- **No advertising ID / AD_ID permission.**
- **No AdMob / ads SDK.**
- **Biometric** permission declared but unused — remove from manifest or ship the feature.

- Risk: Medium
- Blocker: **Yes** for location prominent-disclosure; **Yes** for unused `USE_BIOMETRIC` (Play reviewers flag permissions with no matching use)

---

## Summary of blockers

P0 (cannot submit):
1. No auth / networking layer at all (§5)
2. No signing keystore present (§1)
3. No account-deletion flow (§7)
4. No crash reporting wired (§10)
5. Location permission has no prominent disclosure UI (§15)

P1 (required before public launch):
1. `USE_BIOMETRIC` permission declared but unused — remove or implement (§15)
2. Privacy/Terms links missing from Sign-up screen + Profile "Privacy & data" row is a dead link (§7)
3. sk/uk/ru translations are 2 strings each — either complete or drop the locales (§9)
4. Hardcoded English strings in `CleansiaTextField.kt:69` and `OrderDetailScreen.kt:182,209,299` (§9)
5. `security-crypto 1.1.0-alpha06` is alpha in prod (§4)
6. No `<queries>` element in manifest (§2)
7. Enable predictive back callback (§13)

P2 (polish):
- Audit all `contentDescription = null` for tappable icons (§8)
- Audit touch-target sizes below 48dp (§8)
- Remove the `tools:targetApi="34"` hint (now stale vs targetSdk 35) (§2)
- Configure explicit AAB split config (§1)
