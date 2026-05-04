# Cleansia Customer Android — Implementation Tracker

> Living document. Updated as work progresses.
>
> **⚠ Prior version of this file** (timestamped before 2026-04-14) claimed Phases 3–6 were complete. That was aspirational — the corresponding code never made it to disk (`src/cleansia_customer_android/` did not exist until the 2026-04-14 session). Tracker has been reset to reflect actual on-disk state.

## Phase 2 decisions (2026-04-14)

| # | Decision | Value |
|---|---|---|
| 1 | Primary color | **Figma canonical `#006194`** (overrides brief `#0284c7`) |
| 2 | Body font | **Nunito** (overrides Figma's Liberation Serif artifact) |
| 3 | Dark-mode primary | `#38BDF8` (sky-400) — `#006194` fails WCAG AA on slate-900 |
| 4 | Missing screens | **Claude designs in code** using Figma tokens + brief spec |
| 5 | Dark variants | Derived from tokens (web app slate palette) — no Figma Dark frames |
| 6 | MVP scope | All 15 Figma screens |
| 7 | API client | OpenAPI codegen from `http://localhost:5003/swagger/v1/swagger.json` |
| 8 | Distribution | Firebase App Distribution |
| 9 | Package | `cz.cleansia.customer` |
| 10 | Platform | **Android only** (fresh project, no code reuse from partner app) |
| 11 | Swagger strategy | Checked-in snapshot + `./gradlew refreshSwagger` task |

## Phase 3 — Scaffold (2026-04-14) — COMPLETE on disk, unverified build

### Files written

- `settings.gradle.kts`, `build.gradle.kts`, `gradle.properties`, `.gitignore`
- `gradle/libs.versions.toml` — Kotlin 2.1.10, AGP 8.8.0, Compose BOM 2025.01.01, Hilt 2.53.1, OpenAPI Generator 7.10.0, KSP 2.1.10-1.0.29
- `app/build.gradle.kts` — Java 21, minSdk 26, targetSdk 35, compileSdk 36, R8 full mode, resource shrinking, signing configs, OpenAPI task wiring, Spotless/ktlint
- `app/proguard-rules.pro` — kotlinx.serialization, Retrofit, Hilt rules
- `app/src/main/AndroidManifest.xml` — edge-to-edge, per-app locale config, required permissions
- `app/src/main/java/cz/cleansia/customer/CleansiaApp.kt` — Hilt application
- `app/src/main/java/cz/cleansia/customer/MainActivity.kt` — splash + edge-to-edge + Compose entry
- `app/src/main/java/cz/cleansia/customer/CleansiaRoot.kt` — Scaffold + nav host
- `app/src/main/java/cz/cleansia/customer/navigation/CleansiaNavHost.kt` — stub
- `app/src/main/java/cz/cleansia/customer/ui/theme/{Color,Type,Shape,Theme}.kt` — design system skeleton
- `app/src/main/res/values/{colors,themes,strings}.xml` + locale variants (cs, sk, uk, ru) + night variant
- `app/src/main/res/xml/{locales_config,backup_rules,data_extraction_rules}.xml`
- `.github/workflows/android-ci.yml` — build/lint/test + Firebase App Distribution on master
- `README.md` + `SETUP.md` (owner one-time tasks)

### Owner action items before first build

See `src/cleansia_customer_android/SETUP.md`:

1. Generate Gradle wrapper — `gradle wrapper --gradle-version 8.11.1` (binary `.jar` can't be written via harness)
2. Download Plus Jakarta Sans + Nunito TTFs into `app/src/main/res/font/`
3. Run `./gradlew refreshSwagger` with Customer API running to get initial swagger snapshot
4. Generate launcher icons (Android Studio Image Asset Studio)
5. Add Firebase `google-services.json`
6. Create release keystore + GitHub Actions secrets

### Verification status

- [ ] `./gradlew assembleDebug` — NOT YET RUN (wrapper missing)
- [ ] Preview gallery of design system primitives — not yet built

## Phase 4 — Design system port (IN PROGRESS)

Tokens extracted from Figma screens `0:93` Sign In, `0:915` Home Tab, `0:152` Orders List. Further tokens will be pulled as Phase 6 screens are built.

### Built (13 primitives)

- [x] `ui/theme/Tokens.kt` — gradients, glass tints, shadow colors, status badge enum
- [x] `ui/components/CleansiaButton.kt` — Primary / Secondary / Outlined / Text / Apple social
- [x] `ui/components/CleansiaTextField.kt` — filled, label + inline link, error / helper text
- [x] `ui/components/CleansiaCard.kt` — elevated + tonal variants (32dp radius, primary-tinted shadow)
- [x] `ui/components/StatusBadge.kt` — order lifecycle pill (6 statuses)
- [x] `ui/components/SectionDivider.kt` — "OR CONTINUE WITH" labelled divider
- [x] `ui/components/SectionHeader.kt` — title + optional "See all" link
- [x] `ui/components/SegmentedTabs.kt` — pill segmented control (Upcoming/Past/Cancelled)
- [x] `ui/components/FloatingBottomNav.kt` — translucent pill, 4 tabs, active = filled primary circle
- [x] `ui/components/CleansiaTopAppBar.kt` — brand / title / back / action icon
- [x] `ui/components/StepIndicator.kt` — animated progress bars for booking wizard
- [x] `ui/components/CodeInput.kt` — 6-digit verification input
- [x] `ui/components/TimeSlotPicker.kt` — 4-col grid of 30-min slots 09:00–20:00
- [x] `ui/components/HeroHeading.kt` — 36sp display + 16sp subtitle
- [x] `ui/components/OrderCard.kt` — avatar + title + date + price + status + Details button
- [x] `ui/components/EmptyState.kt` — icon + title + subtitle + optional CTA
- [x] `ui/components/ServiceCard.kt` — bento-style tonal card with circular icon badge

### Still to build (pulled in as needed during Phase 6)

- [ ] Rating stars (interactive + read-only)
- [ ] Chip / tag (for extras, filters)
- [ ] Phone input (country-code selector + number field)
- [ ] Price summary card (service list + subtotal + total)
- [ ] Order status timeline (vertical, pulsing current step)
- [ ] Map preview (static address pin)
- [ ] Bottom sheet wrapper (swipe-to-dismiss + drag handle)
- [ ] Snackbar / toast (success / error variants)
- [ ] Package card (variant of service card with bundle price)
- [ ] Welcome carousel page indicator
- [ ] Gradient hero CTA button (overlaid on image background)
- [ ] Avatar (circular with initials fallback)

### Notes

- **Be Vietnam Pro** appears as body font on Home Tab / Orders List — Liberation Serif was only on Sign In. You approved **Nunito** in Phase 2; sticking with that for consistency with the brief.
- Status badge for **In Progress** on Orders List uses `#004d6a` text on `rgba(64,194,253,0.2)` bg — Figma named this "PENDING" but the color palette matches In Progress. Logic in code treats them separately.
- Bottom nav active indicator in Figma uses `#0284C7` (brief-sky) rather than file-canonical `#006194` — small Figma inconsistency. I'm using `colorScheme.primary` (= `#006194`) for consistency across the app.
- All components have `@Preview` for visual QA in Android Studio. Builds not yet verified (Gradle wrapper pending from owner).

## Phase 5 — API client

1. `./gradlew refreshSwagger` after backend launch
2. Verify `openApiGenerate` produces clean Kotlin + Retrofit interfaces
3. JWT auth interceptor + refresh-on-401 handler
4. EncryptedSharedPreferences-backed token store
5. Smoke test: `GET /api/v1/services` → render in throwaway screen

## Phase 6 — Build MVP screens

### From Figma (15 screens — exact designs, ported)

| # | Screen | Node |
|---|---|---|
| 1 | Splash | `0:535` |
| 2 | Welcome Carousel 1 | `0:499` |
| 3 | Welcome Carousel 2 | `0:444` |
| 4 | Welcome Carousel 3 | `0:659` |
| 5 | Sign In | `0:93` |
| 6 | Home Tab | `0:915` |
| 7 | Book Step 1: Services | `0:699` |
| 8 | Book Step 2: Property | `0:805` |
| 9 | Book Step 3: Schedule | `0:1281` |
| 10 | Book Step 4: Address | `0:3` |
| 11 | Book Step 5: Extras | `0:1136` |
| 12 | Payment Sheet | `0:559` |
| 13 | Booking Success | `0:1064` |
| 14 | Orders List | `0:152` |
| 15 | Order Detail | `0:251` |

### Designed in code (15 — mirror Figma visual language)

Sign Up, Email verify (6-digit), Forgot password, Reset password, Profile home, Edit profile, Addresses list, Add/edit address, Payment methods, Preferences (lang/theme/notif), Legal pages, Track order (public), Rate & review, Language switcher bottom sheet, Empty / error / 404 states.

## Phase 7 — Polish

- [ ] Populate en/cs/sk/uk/ru strings from `src/Cleansia.App/apps/cleansia.app/src/assets/i18n/*.json`
- [ ] FCM push setup
- [ ] Stripe + Google Pay
- [ ] Sentry crash reporting
- [ ] Biometric unlock (BiometricPrompt)
- [ ] Firebase App Distribution first upload

---

## Design Decisions Log

| # | Decision | Rationale |
|---|---|---|
| 1 | Primary `#006194` | Figma canonical, overrides brief's `#0284c7` |
| 2 | Nunito body font | Figma said Liberation Serif — almost certainly a Stitch substitution; Nunito matches brief and is proper mobile body |
| 3 | Plus Jakarta Sans headings | Matches Figma exactly |
| 4 | Dark primary `#38BDF8` | `#006194` fails WCAG AA on `#0F172A` |
| 5 | Dark surfaces from web tokens | No Figma dark frames; reuse proven web app palette |
| 6 | Checked-in swagger snapshot | Matches web NSwag workflow, survives offline builds / CI |
| 7 | KSP only (no kapt) | Faster builds, future-proof |
| 8 | Per-app language via `locales_config.xml` | Android 13+ native way; older devices fall back to system locale |
| 9 | No code reuse from partner app | Different audience, different design, different package |
| 10 | AGP 8.8 / Kotlin 2.1 / Java 21 | Latest stable tooling for Play Market compliance |
| 11 | R8 full mode + resource shrinking | APK size / Play Market optimization |
