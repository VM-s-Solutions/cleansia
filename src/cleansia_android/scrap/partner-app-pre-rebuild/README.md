# Partner Android — pre-rebuild scrap

> Snapshot of irreplaceable content from the partner Android app
> immediately before the Phase 3 nuke (`feat/partner-android-rebuild`).
> Captured 2026-05-16. Once the rebuild lands and is verified, this
> folder can be deleted.

## Why this exists

The partner app's `src/main/` is being deleted and rebuilt from scratch
on the customer-app architectural patterns. Most files (~22 440 LOC of
Kotlin) will be reimplemented in the customer-app idiom and don't need
preserving. But some content **can't be regenerated from prompts** and
must be carried forward verbatim.

## Contents

### `res/` — all resources
Full copy of every `res/` subdir that contains non-mipmap-launcher
content. The rebuilt app will cherry-pick what it needs:

- `values/strings.xml` + `values-{cs,sk,uk,ru}/strings.xml` —
  hand-translated copy for 5 locales. **Hard to regenerate.** The
  rebuild will trim unused keys but keep the translations.
- `values/themes.xml`, `values-night/themes.xml`,
  `values-night-v31/themes.xml`, `values-v31/themes.xml` —
  splash + theme XML. Mostly replaced by `:core` theme + Compose-only
  patterns, but `Theme.CleansiaPartner.Splash` (the launcher splash
  style) is referenced by `AndroidManifest.xml` and needs an equivalent.
- `drawable/` — `ic_notification`, `notification_bg`, `notification_progress*`,
  `splash_background*`, `ic_launcher_*`. Splash + launcher icons stay.
  Notification drawables drop with `OrderTimerService`.
- `drawable-nodpi/mascot_waving.png` — **mascot artwork**. Reuse verbatim
  in the rebuilt auth + onboarding screens.
- `font/quicksand_bold.ttf` — currently unused outside legacy theme;
  customer-app uses `Poppins` + `Nunito` from `:core`. **Likely deletable**
  but kept here just in case.
- `xml/backup_rules.xml`, `xml/data_extraction_rules.xml`,
  `xml/locales_config.xml` — Android system XML. `locales_config.xml`
  lists supported locales and **must be carried forward verbatim**.
- `layout/notification_timer*.xml` — XML layouts for the foreground
  timer notification. **Deleted with OrderTimerService.**
- `mipmap-anydpi-v26/ic_launcher{,_round}.xml` — launcher icon XML.
  Stays.

### `manifest/AndroidManifest.xml`
Reference for the rebuilt manifest:
- Permissions list (camera, network, notifications) — keep most; drop
  `FOREGROUND_SERVICE*` once `OrderTimerService` is gone.
- Deep-link intent filters (`cleansia://partner/...` custom scheme +
  `https://partner.cleansia.cz/...` app links + `/confirm-email`
  path-prefix) — **must be preserved**.
- `tools:node="remove"` on Sentry's `SentryInitProvider` — keep this;
  partner doesn't initialise Sentry yet and without removal the app
  crashes on first launch with `DSN is required`. When partner adopts
  Sentry, drop this stanza and wire `SENTRY_DSN` via `buildConfigField`.

### `gradle/build.gradle.kts` + `proguard-rules.pro`
The current partner module Gradle config. The rebuild will mostly
match this — same `applicationId`, same `minSdk`, same Java 21,
same `:core` dependency — but **drop**:
- `androidx.room.*` (no offline cache)
- `androidx.biometric` (biometric login dropped)
- `androidx.security.crypto` (replaced by `:core` `TokenStore` which
  uses DataStore-Preferences + Tink)
- `lottie.compose` (no Lottie in the rebuild)
- All `buildConfigField` entries except `API_BASE_URL`

The `openApiGenerate {}` block + `dumpOpenApiSpec` task stay verbatim.

### `kotlin/` — domain logic reference
Five hand-written Kotlin files captured purely for **logic reference**
during reimplementation. They will NOT be copied into the rebuild as-is
— each is replaced by a customer-app-pattern equivalent.

| File | Why kept | Status in rebuild |
|---|---|---|
| `ProfileValidator.kt` | Field-by-field validation rules (regex, length limits, IBAN format) that took real cleaner feedback to converge | Rewrite using customer-app's `FieldValidator` pattern in Phase 5; cherry-pick the regex constants |
| `ProfileFormState.kt` | Multi-section form state shape (personal / contact / location / availability / documents / terms) | Reuse the section breakdown but state via StateFlow not snapshot state |
| `OrderFilterManager.kt` | Filter combinator: status × date range × search query × sort | Reimplemented inline in `OrdersViewModel` in Phase 6 |
| `OrderPhotoManager.kt` | Per-photo upload state + Before/After grouping | Replaced by per-photo `ActionState` in Phase 6 |
| `NavRoutes.kt` | Current sealed-interface NavRoute structure | Reuse the 16-route shape; rewrite as new `sealed interface NavRoute` matching customer-app's typed-route pattern |

## What is NOT in this scrap folder

Things intentionally **not preserved** because they're being dropped:
- `OrderTimerService` (foreground service)
- `BiometricHelper` (biometric login)
- `TokenManager` (EncryptedSharedPreferences) — replaced by `:core` `TokenStore`
- `PreferencesManager` — replaced by customer's `AppSettings` (DataStore)
- `CleansiaDatabase` + 3 DAOs + 3 entities (Room cache)
- `DynamicCleaningBackground` Compose component
- All hand-written DTOs under `domain/models/` — replaced by OpenAPI-generated types
- All `ApiService.kt` Retrofit interface — replaced by OpenAPI-generated `cz.cleansia.partner.api.client.*`

If a removed pattern turns out to be wanted in the rebuild, the full
git history of the deleted code remains on `master`.

## When to delete this folder

After the rebuild passes:
1. `./gradlew :partner-app:compileDebugKotlin` ✓
2. `./gradlew :partner-app:testDebugUnitTest` ✓
3. Owner has done a manual smoke test of all 8 phases on a real device
4. PR merged to `master`

Then `rm -rf scrap/partner-app-pre-rebuild/` and the snapshot's job is
done. Until then, treat this folder as **read-only reference** — do
not modify files here.
