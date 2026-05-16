# Partner Android App — Full Rebuild

> Strategic decision: rebuild the partner Android app from scratch on
> the customer-app architectural patterns. Reuse the `:core` library
> and the OpenAPI-generated client. Don't carry forward partner-specific
> legacy patterns (Room database, foreground timer service, biometric,
> `TokenManager` / `PreferencesManager`).
>
> **Branch:** `feat/partner-app-rebuild` (to be created at execution start)
> **Estimated effort:** 6-10 working days
> **Owner:** Michael (Mike)
> **Date authored:** 2026-05-16

---

## Why a full rebuild

The partner app's current state is a mix of inherited patterns from before customer-app's modernisation:
- Hand-written DTOs in `domain/models/` that have silently drifted from the backend (the [Language registration bug](src/cleansia_android/partner-app/src/main/java/cz/cleansia/partner/domain/repositories/AuthRepository.kt) that triggered this rebuild)
- A bespoke `TokenManager` (EncryptedSharedPreferences + flow-state) that doesn't share `:core`'s `TokenStore`
- A foreground `OrderTimerService` for in-progress cleaning timer that adds complexity for marginal UX gain
- Room database caching that has zero offline-mode requirement on real partners
- Biometric login that isn't actually needed and adds dependency surface
- Two parallel auth visual languages (already partially fixed) but the rest of the screens still use `DynamicCleaningBackground` + hardcoded spacing + manual `statusBarsPadding()` instead of customer's flat theme + Spacing tokens

Patching incrementally has been tried; the next 5 repos to migrate against the generated OpenAPI client all have signature drift that the compiler won't catch. A clean restart on the customer pattern is cheaper than 5 incremental migrations + the visual cleanup work they don't include.

---

## Scope & constraints (from owner)

| Decision | Value |
|---|---|
| Branch | Created on execution start (not during planning) |
| Inventory depth | Forensic — screens + integration + tech debt |
| Preserve | Backend contract (consume via generated client), `:core` module, Hilt+Compose+Nav-Compose architecture, 5-locale i18n strings (re-import only used keys) |
| Logic retention | Reimplement everything in customer-app idiom — `TokenStore`, `NetworkCall`, `takeUntilDestroyed`, MVI-ish StateFlow VMs. Treat all partner patterns as legacy. |
| Partner-only features | DROP biometric, OrderTimerService (timer), PreferencesManager, Room database. Reimplement basic locale/theme persistence via customer's `AppSettings` (DataStore-Preferences). |
| Backend | Minor tidying allowed as we find gaps. No backend rebuild. |

---

## Saved deferred items (cross-reference)

Already preserved in `planning/active/` — won't be touched by the rebuild:
- **`decisions-infra-arch.md`** — INFRA-001 (Bicep IaC, deferred), ARCH-001 Phases 3b/4b (deferred follow-ups after monorepo extract)
- **`httponly-cookie-auth-migration.md`** — Step 6 deploy flip (owner action)
- **`post-android-followups.md`** — Google OAuth client IDs (owner provisioning), deferred polish items
- **`post-customer-android-cleanup.md`** — Customer follow-ups (orthogonal to partner rebuild)
- **`push-notifications-phase-b.md`** — Last Phase B event (orthogonal)

`SHIPPED-SUMMARY.md` covers the historical record of every shipped feature.

Mascot animations + Google IDs + Bicep specs all stay where they are. The rebuild **does not affect** these items.

---

## Current state inventory (audit summary)

### Screens (15 total, ~22 440 LOC across `features/`)

| Feature | Screens | VMs | Partner-only? |
|---|---|---|---|
| `auth/` | 4 (Login, Register, ForgotPassword, ConfirmEmail) | 4 | Already on customer pattern (rebuilt in prior session) |
| `dashboard/` | 2 (Dashboard, AnalyticsDetail) | 2 | Yes — partner-only earnings analytics |
| `orders/` | 2 (Orders list, OrderDetails with timer + photos) | 3 (+ OrderTimerManager, OrderPhotoManager) | Heavy partner-specific UX |
| `profile/` | 1 (multi-section: personal/contact/location/availability/documents/terms) | 1 (+ ProfileValidator, ProfileFormState) | Heavy partner-specific (employee availability calendar) |
| `invoices/` | 2 (Invoices list, InvoiceDetails) | 2 | Entirely partner-only |
| `onboarding/` | 2 (Onboarding pager, ProfileCompletion wizard) | 2 | Partner-specific (employee approval flow) |
| `notifications/` | 1 (stub "Coming Soon") | 0 | Stub only |
| `settings/` | 1 (language/theme/notifications/biometric) | reuses ProfileViewModel | Mixed (biometric to drop) |
| `account/` | 1 (AccountHub navigation) | 1 | Standard |
| `search/` | 0 screens; just GlobalSearchViewModel | 1 | Partner-only (cross-feature search) |

### Navigation graph (16 routes, sealed-interface NavRoute)

Already well-structured. Routes: `Onboarding`, `Login`, `Register`, `ConfirmEmail`, `ForgotPassword`, `ProfileCompletion`, `Main` (nested: `Dashboard`, `Orders`, `Invoices`), `Profile`, `Settings`, `AccountHub`, `Notifications`, `Analytics`, `OrderDetails(orderId)`, `InvoiceDetails(invoiceId)`.

Deep-link entry points: `cleansia://partner/...` custom scheme + `https://partner.cleansia.cz/...` HTTPS app links (parsed but worth re-verifying manifest wiring).

Issues to fix in rebuild: aggressive `popUpTo(0) { inclusive = true }` in deep-link flows (loses user context), no reactive auth gate (session expiry mid-use doesn't auto-redirect to Login).

### Hilt modules (2)

| Module | Provides | Rebuild action |
|---|---|---|
| `NetworkModule` | Json, OkHttp, two Retrofits (legacy `ApiService` + `@GeneratedClientRetrofit`), 9 generated `*Api` interfaces, AuthInterceptor | Replace with customer-pattern AuthModule + provide generated `*Api`s against single Retrofit (no `@GeneratedClientRetrofit` qualifier needed once legacy `ApiService` is gone) |
| `RepositoryModule` | 5 repository @Binds | Rewrite per-repo as we rebuild each feature |

### Partner-only dependencies to drop

| Dep | Used for | Action |
|---|---|---|
| `androidx.biometric` | BiometricHelper | DROP |
| `androidx.room:runtime` + `room:compiler` (ksp) | CleansiaDatabase + 3 DAOs | DROP entirely (no offline mode in rebuild) |
| `retrofit-converter-scalars` | Hand-written ApiService raw String responses | DROP (generated client doesn't need it) |

Keep: `androidx.splashscreen`, `androidx.security.crypto` (TokenStore in :core uses it), `lottie-compose` only if used in onboarding illustrations (verify).

### Critical backend gaps (block rebuild until resolved)

The OpenAPI-generated client lacks endpoints the current app calls:

| Endpoint | Status | Required fix |
|---|---|---|
| `Order/NotifyOnTheWay` | **Missing from mobile-partner host** | Add controller method in `Cleansia.Web.Mobile.Partner/Controllers/OrderController.cs` that dispatches existing `NotifyOnTheWay.Command` MediatR handler |
| `Dashboard/GetEarningsSummary` | **Removed from backend** | Confirm intentional; rebuild's Dashboard skips this (uses earnings analytics instead) |
| `Dashboard/GetUpcomingOrders` | **Signature changed** — now paged with full Filter object | Rebuild's Dashboard repository constructs the right Filter (e.g. `Filter.EmployeeId = currentUserId, Filter.OrderStatuses = [Confirmed, OnTheWay]`) |

### Tech debt to NOT carry forward (top 10)

1. `OrderTimerService` foreground notification service — drop entirely
2. `BiometricHelper` + biometric prefs — drop
3. Room database + CachedOrder/CachedInvoice/CachedProfile — drop
4. `PreferencesManager` — replace with `AppSettings` (DataStore-Preferences, customer pattern)
5. `TokenManager` (EncryptedSharedPrefs + name/email metadata + sessionExpiredEvent flow) — replace with `:core`'s `TokenStore`. Separate `UserProfileStore` for the metadata.
6. `DynamicCleaningBackground` + all uses — drop (flat backgrounds per customer pattern)
7. Manual `statusBarsPadding()` in 12+ screens — let Scaffold + system insets handle it
8. Hardcoded `Spacer(height=N.dp)` everywhere — use `:core`'s `Spacing.*` tokens
9. Empty `catch (_: Exception) { }` swallows in GlobalSearchViewModel + AnalyticsViewModel + OrderTimerService — proper error handling
10. `runBlocking { preferencesManager.language.first() }` in `MainActivity.onCreate` — refactor to LaunchedEffect inside Compose

---

## The plan — 8 phases

Each phase ends with a verifiable checkpoint. The branch holds work-in-progress; commits land per-phase so any phase can be reverted independently.

### Phase 0 — Pre-flight (1 hour)

**Owner actions before I start:**
- [ ] Confirm the rebuild branch name: `feat/partner-android-rebuild` (or your preferred)
- [ ] Confirm Aspire AppHost will be running during integration tests (port 5002 reachable)
- [ ] Confirm `~/.gradle/gradle.properties` has any partner-specific values that need to survive (NOTHING currently — verified, partner uses no special secrets unlike customer's Mapbox/Stripe/Sentry/Google)

**My actions:**
- [ ] Create branch from current HEAD
- [ ] Commit: "chore: branch off for partner-app full rebuild"

### Phase 1 — Backend tidying (4 hours, includes owner restart)

**Add the `NotifyOnTheWay` controller endpoint to `Cleansia.Web.Mobile.Partner`.**

The MediatR handler exists in `Cleansia.Core.AppServices/Features/Orders/NotifyOnTheWay.cs` but no mobile-partner controller dispatches it. The current `Cleansia.Web.Partner` (web host) exposes it but mobile-partner doesn't.

Steps:
- [ ] Add `[HttpPost("NotifyOnTheWay")]` action to `Cleansia.Web.Mobile.Partner/Controllers/OrderController.cs` mirroring the `TakeOrder`/`StartOrder` actions
- [ ] Rebuild + restart partner-mobile-api host (5002) — **owner action**
- [ ] Verify: `./gradlew :partner-app:dumpOpenApiSpec` produces a spec containing `Order_NotifyOnTheWay`
- [ ] `./gradlew :partner-app:openApiGenerate` produces `OrderApi.orderNotifyOnTheWay()`

**Checkpoint:** generated `OrderApi.kt` contains `orderNotifyOnTheWay()` method.

**Optional backend tidying (deferred to as-needed):**
- Endpoint photo flow has both `UploadPhoto` and `SavePhotos` — the rebuild can pick whichever fits, no immediate backend cleanup needed
- Removed `GetEarningsSummary` — Dashboard rebuild won't use it; document as intentional removal

### Phase 2 — Scrape & save irreplaceable content (2 hours)

Before deleting `partner-app/`, extract anything we can't regenerate:
- [ ] Copy `partner-app/src/main/res/values*/strings.xml` (all 5 locales) to a temporary `scrap/partner-strings/` folder at the rebuild branch root. The rebuild will cherry-pick keys it actually uses; everything else gets dropped.
- [ ] Copy any custom `drawable/`, `drawable-nodpi/`, `mipmap/` files (icons, mascot, launcher icons) to `scrap/partner-drawables/`
- [ ] Copy `proguard-rules.pro` + `google-services.json` (if present) + `AndroidManifest.xml` to `scrap/partner-manifest/` for reference
- [ ] Inventory `partner-app/api-spec/` (we already have the OpenAPI spec at `openapi/partner-mobile-api.json`; old `api-spec/` was deleted in prior session)

**Checkpoint:** `scrap/` folder contains everything that would be lost otherwise; verified by diff-listing against the source folders.

### Phase 3 — Delete + scaffold (2 hours)

- [ ] Delete `partner-app/src/main/java/` entirely (every Kotlin file)
- [ ] Delete `partner-app/src/main/res/` (keep only `mipmap-anydpi-v26/`, `mipmap-{hdpi,mdpi,...}/`, `values/themes.xml` stub — anything required for AndroidManifest to compile)
- [ ] Delete `partner-app/src/test/` + `partner-app/src/androidTest/`
- [ ] Rewrite `partner-app/build.gradle.kts` matching customer-app's structure: drop biometric/room/scalars deps, drop OpenAPI old config (already migrated)
- [ ] Rewrite `partner-app/src/main/AndroidManifest.xml` as a minimal manifest: package, single Activity, no foreground service, no biometric permissions
- [ ] Create empty `partner-app/src/main/java/cz/cleansia/partner/CleansiaPartnerApp.kt` (`@HiltAndroidApp` stub) and `MainActivity.kt` (Compose-Material3 skeleton)
- [ ] Commit: "chore: nuke partner-app src; reset to empty Hilt+Compose scaffold"

**Checkpoint:** `./gradlew :partner-app:assembleDebug` produces an empty APK that launches to a blank Compose screen.

### Phase 4 — Network + Auth (1 day)

- [ ] `NetworkModule.kt` — customer-pattern OkHttp + AuthInterceptor + AuthAuthenticator + single Retrofit at host root (`http://10.0.2.2:5002/`, no `/api` suffix). Provide all 9 generated `*Api` interfaces.
- [ ] `AuthRepository` — reimplement using generated `AuthApi`. Follow the pattern from the migration we already did this session ([AuthRepository.kt](src/cleansia_android/partner-app/src/main/java/cz/cleansia/partner/domain/repositories/AuthRepository.kt)) but built fresh in the new tree, using `:core`'s `TokenStore` (not partner's `TokenManager`) and `:core`'s `NetworkCall` helper (not partner's `safeApiCall`).
- [ ] `UserProfileStore` — new app-specific class. Holds firstName/lastName/email/userId — the metadata that partner's old `TokenManager` was carrying. Uses DataStore-Preferences. Cleared on logout via `:core`'s `SessionScopedCache` multibinding.
- [ ] `AppSettings` — DataStore-Preferences for locale + theme + notification opt-in + onboardingCompleted flag (no biometric). Mirrors customer's `AppSettings`.
- [ ] Build the 4 auth screens (Login, Register, ForgotPassword, ConfirmEmail) — these were already rewritten this session against `:core` widgets; copy their code into the new tree verbatim. They consume `:core` widgets so they work unchanged.
- [ ] Navigation: minimal NavHost with just `Login` ↔ `Register` ↔ `ForgotPassword` ↔ `ConfirmEmail` ↔ stub `Home`

**Checkpoint:** APK can register a new partner (verifies `language` field), receive email confirmation code, sign in, see a stub Home screen. Logout works. Refresh-token flow works (let session expire and re-fetch).

### Phase 5 — Profile (employee) (2 days)

The most complex partner-specific surface. Customer doesn't have anything equivalent.

- [ ] `ProfileRepository` — reimplement against generated `EmployeeApi`. Same surface as today (load employee, update sections, upload photo, manage documents).
- [ ] `ProfileScreen` — single screen, multi-section. Sections: Personal Info, Contact, Address, Bank Details, Emergency Contact, Availability, Documents, Terms.
- [ ] Each section gets its own `@Composable` + edit dialog. Following customer pattern: dialogs not bottom-sheets (matches `CleansiaDialog` from `:core`).
- [ ] Availability sub-system — calendar view + edit dialogs. Most complex piece; budget a full day for it.
- [ ] Document upload — uses generated `EmployeeApi.employeeUploadProfilePhoto` + `employeeSaveMyDocuments`. No partner-specific helper class; the screen calls the repo directly.
- [ ] `ProfileCompletionScreen` — onboarding-wizard version of ProfileScreen; same components, different presentation (linear progress bar, one section per step).

**Checkpoint:** Employee can sign in (Phase 4 result), land on Profile, edit each section, upload a document, set availability. All changes round-trip to the backend.

### Phase 6 — Orders (1.5 days)

- [ ] `OrdersRepository` — wraps generated `OrderApi` (no Room cache, no in-memory cache)
- [ ] `OrdersScreen` — list with filters (status / date / urgency). Pull-to-refresh. No partner-specific week-strip widget (drops `DynamicCleaningBackground`).
- [ ] `OrderDetailsScreen` — info sections + action buttons (TakeOrder, StartOrder, NotifyOnTheWay, CompleteOrder) + photo grid + employee notes/issues. **NO foreground timer** (per scope decision).
- [ ] Photo workflow — uses generated `OrderApi.orderUploadPhoto` (or `orderSavePhotos` — pick one, document choice)
- [ ] Notes/issues — uses generated `OrderApi.orderAddNote` + `orderReportIssue`
- [ ] Status changes update `OrderRepository` state which the Detail screen observes

**Checkpoint:** Can list orders, filter, open details, take an order, mark on-the-way, start, complete (with notes/photos).

### Phase 7 — Dashboard + Invoices (1 day)

- [ ] `DashboardRepository` — calls `dashboardGetStats`, `dashboardGetUpcomingOrders` (paged), `dashboardGetEarningsAnalytics`, `dashboardGetProductivityMetrics`. NO `getEarningsSummary` (removed). Internal transformations are rewritten against the actual `*Dto` shapes (not the old hand-written models).
- [ ] `DashboardScreen` — flat layout: greeting hero + quick stats grid + earnings chart + upcoming orders list. Drop the partner-specific "working hours" widget unless tied to availability.
- [ ] `AnalyticsScreen` — earnings trend + day-of-week + performance score. Customer pattern: scrollable column of section cards.
- [ ] `InvoicesRepository` — wraps generated `EmployeePayrollApi`
- [ ] `InvoicesScreen` — list with status filter
- [ ] `InvoiceDetailsScreen` — header + amount breakdown + orders included + download PDF button. PDF download writes to Downloads folder via `MediaStore` (Android 10+).

**Checkpoint:** Dashboard shows real numbers, Analytics shows charts, Invoices list + detail works, PDF download works.

### Phase 8 — Polish + verify (1 day)

- [ ] Settings screen (language picker + theme toggle + notification opt-in — NO biometric)
- [ ] Account Hub screen
- [ ] Notifications screen (stub for future Phase A push)
- [ ] Onboarding screens (pager + ProfileCompletion wizard) — Phase 5 covered most of ProfileCompletion's logic; onboarding pager is a thin wrapper
- [ ] Deep-link handling — customer-pattern: pop to Main rather than `popUpTo(0)`
- [ ] Locale restoration — use `LaunchedEffect` not `runBlocking`
- [ ] Smoke-test: install fresh APK on emulator, run through registration → onboarding → first order workflow
- [ ] Update `CLAUDE.md` if anything notable changed at the file-tree level
- [ ] Update `SHIPPED-SUMMARY.md` with the rebuild summary

**Checkpoint:** Fresh-install smoke test passes the golden path (register → confirm → onboarding → take an order → complete it → see in dashboard → see invoice later).

---

## Risk register

| Risk | Likelihood | Impact | Mitigation |
|---|---|---|---|
| Backend `NotifyOnTheWay` endpoint takes longer to add than expected | Medium | High (blocks Phase 6) | Phase 1 is dedicated to this; if it takes more than 4h, surface immediately |
| Profile availability calendar UX is harder to rebuild than today's | Medium | Medium | Allocate full day; treat customer's calendar (if any exists in `:core`) as reusable starting point |
| Generated DTO field renames break runtime in ways tests don't catch | High | Medium | Manual emulator smoke test at every phase checkpoint; don't ship Phase 5+ without exercising the actual screens |
| Partner-specific imagery (mascot variants for different employee approval states) might be needed | Low | Low | If we hit it, ship with the customer mascot and document the asset gap |
| Lost work: deleting `partner-app/src/main/` then changing my mind | Low | High | Phase 2 scraps everything we might want; branch isolates everything; can `git revert` per phase |
| Owner unavailable to restart partner-mobile-api between Phase 1 and Phase 2 | Medium | Low (blocks 1h) | Phase 1 is the only thing requiring backend rebuild; everything else uses the generated client which is already up-to-date |

---

## Out of scope

- **Backend rebuild** of `Cleansia.Web.Mobile.Partner` — only the `NotifyOnTheWay` endpoint add. Existing controllers stay as-is.
- **Bicep / IaC** — deferred per `decisions-infra-arch.md`.
- **Google OAuth on partner** — deferred per `post-android-followups.md`. Partner currently has no Google sign-in; rebuild doesn't add it.
- **Push notifications on partner** — deferred. Partner doesn't have Firebase wired up; rebuild doesn't add it.
- **iOS** — no iOS app exists.
- **Offline mode** — Room database is dropped; rebuild relies on network availability. If offline matters later, it's a separate effort.
- **Web partner app** — `Cleansia.App/apps/cleansia-partner.app` is separate and untouched.

---

## Decision points needing owner sign-off

Before execution starts, please confirm/decide:

1. **Branch name**: `feat/partner-android-rebuild` OK?
2. **PDF download path for invoices**: use `MediaStore.Downloads` (Android 10+) or open with system viewer via `Intent.ACTION_VIEW`? (Customer uses ACTION_VIEW for receipts.)
3. **Photo upload UX**: customer-pattern (one photo at a time with progress) or partner's existing "select multiple, batch upload" pattern? Customer wins by default unless partner UX is materially different for cleaner workflow.
4. **Availability edit UX**: re-design from scratch using customer dialog patterns, or replicate today's calendar+dialogs verbatim? The partner version has been iterated on with real cleaner feedback; throwing it away might lose UX learning.
5. **Onboarding pager content**: keep current 5-page intro (welcome / features / availability / documents / terms) verbatim or simplify? Customer doesn't have an onboarding pager at all.

---

## Estimated total: 6-10 working days

- Phase 0: 1h
- Phase 1: 4h (mostly owner-blocked on restart)
- Phase 2: 2h
- Phase 3: 2h
- Phase 4: 1 day
- Phase 5: 2 days
- Phase 6: 1.5 days
- Phase 7: 1 day
- Phase 8: 1 day

Total: ~7 working days of focused work + owner-restart blockers + per-phase smoke testing.

If anything in this plan is wrong about the current partner-app structure, please flag now. The forensic audit was thorough but I may have missed something domain-specific.
