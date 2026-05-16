# Post-Android Cleanup — Execution Plan

Single-session plan covering: WEB-P-001 Mapbox, 3 Google OAuth wires, INFRA-001 + ARCH-001 decision docs, booking-extras feature.

## Phase A — WEB-P-001 (Mapbox autocomplete in partner profile)

**Scope:** Reuse `CleansiaAddressAutocompleteComponent` (from `libs/shared/components/`) in partner profile address fields.

**Files to touch:**
1. `apps/cleansia-partner.app/src/environments/environment.ts` — add `mapboxToken` slot.
2. `apps/cleansia-partner.app/src/environments/environment.staging.ts` — same.
3. `apps/cleansia-partner.app/src/environments/environment.prod.ts` — same.
4. `apps/cleansia-partner.app/src/app/app.config.ts` — provide `MAPBOX_ACCESS_TOKEN` from `environment.mapboxToken` (mirror customer's line 96 pattern).
5. `libs/cleansia-partner-features/profile/src/lib/components/profile-personal-info/profile-personal-info.component.ts` — inject + handle `picked` event, update form controls.
6. `libs/cleansia-partner-features/profile/src/lib/components/profile-personal-info/profile-personal-info.component.html` — insert `<cleansia-address-autocomplete>` above the street/city/zip/country block.

**Verification:** `nx build cleansia-partner.app`.

**Risk:** None — purely additive.

---

## Phase B — Google OAuth (3 wires)

User confirmed: use the `googleClientId` placeholder already in env files. Real value gets set at deploy time.

### B1 — WEB-C-004 (customer web Google sign-in SDK initialization)

**Current state (research):** Both `login.component.html:55` and `register.component.html:149` have `#googleBtn` template refs. Facades `googleLogin()` / `googleRegister()` are fully wired to `authenticateWithGoogle()`. **What's missing:** the Google Identity Services script + `window.google.accounts.id.renderButton()` call.

**Files to touch:**
1. `apps/cleansia.app/src/index.html` — add Google Identity Services `<script src="https://accounts.google.com/gsi/client" async defer>`.
2. `libs/cleansia-customer-features/login/src/lib/login/login.component.ts` — `ngAfterViewInit`: initialize Google ID SDK with `environment.googleClientId`, render button into `#googleBtn`, callback invokes `facade.googleLogin(credential)`.
3. `libs/cleansia-customer-features/register/src/lib/register/register.component.ts` — same pattern with `facade.googleRegister`.

**Risk:** Low. The Google Identity SDK is a script tag + 2 API calls. Failures (e.g. invalid client ID) just don't render the button.

### B2 — MOB-C-009 (customer Android Google sign-in)

**Current state:** `SignInScreen.kt:144` and `SignUpScreen.kt:246` have `if (false) { /* Google button */ }` stubs. `CleansiaNavHost.kt:176,205` has `onGoogleSignIn = { /* hidden */ }` hooks. `AuthApi.googleAuth` + `AuthRepository.googleAuth` already implemented and wired.

**Strategy:** Flip the `if (false)` to `if (true)`, unhide the buttons, wire `onGoogleSignIn` callbacks to call `authViewModel.signInWithGoogle()`. The ViewModel method already exists if the repo does — I'll verify and add if missing. Use the Credential Manager API (modern Android approach) since it's what Google now recommends — but for time-budget reasons I'll use the simpler `GoogleSignInClient` path that the partner app uses (if partner uses it). If partner has nothing, I'll wire CredentialManager from scratch.

**Files to touch (estimate):**
1. `app/src/main/java/cz/cleansia/customer/features/auth/SignInScreen.kt:144` — unhide.
2. `app/src/main/java/cz/cleansia/customer/features/auth/SignUpScreen.kt:246` — unhide.
3. `app/src/main/java/cz/cleansia/customer/navigation/CleansiaNavHost.kt:176,205` — wire callback.
4. `app/src/main/java/cz/cleansia/customer/features/auth/AuthViewModel.kt` (or similar) — add `signInWithGoogle(activity)` method that launches CredentialManager → calls repo.googleAuth.
5. `app/build.gradle.kts` — add `androidx.credentials:credentials` + `androidx.credentials:credentials-play-services-auth` if not present.
6. `app/src/main/AndroidManifest.xml` — add `com.google.android.libraries.identity.googleid` metadata if required.

**Risk:** Medium. Mobile changes have higher iteration cost (no hot-reload for native). Will build with `./gradlew :app:compileDebugKotlin` after each change.

### B3 — MOB-C-009b (customer Android forgot-password wiring)

**Current state:** `ForgotPasswordScreen.kt` UI is fully built (EmailStep → CodeStep). NavHost callbacks `onSendCode()` and `onChangePassword()` are stubs. Backend `RequestPasswordChange.cs` exists.

**Files to touch:**
1. NavHost — wire `onSendCode = { email -> authViewModel.requestPasswordChange(email) }`.
2. AuthViewModel — add `requestPasswordChange(email)` (calls `authRepository.requestPasswordChange`) + `changePassword(code, password)` (calls `authRepository.changePassword`).
3. AuthRepository — add the two methods if missing; call `AuthApi.requestPasswordChange` + `AuthApi.changePassword`.
4. AuthApi.kt — add the two endpoint definitions if missing.

**Risk:** Low. Pure plumbing.

**Verification (B1+B2+B3):**
- `npx nx build cleansia.app` for web side.
- `./gradlew :app:compileDebugKotlin :app:testDebugUnitTest` for mobile.
- Visual smoke test by user (Google button renders, forgot-password screens advance).

---

## Phase C — Decision docs (INFRA-001 + ARCH-001)

These are "decide, don't implement" items. I'll write a single decision-doc-style markdown that surfaces the tradeoff for each and proposes a default. User chooses; implementation is a separate session.

**Files to create:**
1. `planning/active/decisions-infra-arch.md` — covers both decisions in one doc since they're related to "platform investment".

**Risk:** None — doc only.

---

## Phase D — Booking-extras (with mid-feature gates)

User confirmed: pause for M1 migration + M3 NSwag regen.

### Sequencing

**Sub-phase D.1 — Backend entity (ES1) + seed (M2 SQL)**
1. `src/Cleansia.Core.Domain/Orders/Extra.cs` — domain entity (mirrors `ServiceCategory` shape).
2. `src/Cleansia.Core.Domain/Orders/IExtraRepository.cs` — interface.
3. `src/Cleansia.Infra.Database/Repositories/ExtraRepository.cs` — impl.
4. `src/Cleansia.Infra.Database/Configurations/ExtraEntityConfiguration.cs` — EF config.
5. `src/Cleansia.Infra.Database/CleansiaDbContext.cs` — register `DbSet<Extra>`.
6. `sql-scripts/insert_seed_data.sql` — append 5 INSERTs with translations JSON.
7. DI registration (search for similar `services.AddScoped<IServiceRepository, ServiceRepository>` and add Extra alongside).

**GATE M1 + M2:** Pause. Surface:
> M1: `dotnet ef migrations add AddExtras` then `dotnet ef database update`.
> M2: `psql -f sql-scripts/insert_seed_data.sql` (or your preferred apply path).

**Sub-phase D.2 — Anonymous GetOverview endpoint (ES2)**
8. `src/Cleansia.Core.AppServices/Features/Extras/GetExtraOverview.cs` — MediatR query + handler + DTO.
9. `src/Cleansia.Web.Customer/Controllers/ExtraController.cs` — `[AllowAnonymous] [HttpGet("GetOverview")]`.

**GATE M3:** Pause. Surface:
> M3: `npm run generate-customer-client` (so `ExtraClient` + `ExtraListItem` DTOs land in the customer TS client).

**Sub-phase D.3 — Pricing + handlers (ES3)**
10. `src/Cleansia.Core.AppServices/Services/OrderPricingCalculator.cs` — add `selectedExtraIds` + `cleaningDate` params; add `ExtrasSubtotal`, `ExpressSurchargeApplied`, `ExpressSurchargeAmount` fields.
11. `OrderPricingResult` record — extend.
12. `QuoteOrder.cs` — pass new params.
13. `CreateOrder.cs` — pass new params; persist selectedExtraIds on the order.
14. `src/Cleansia.Core.Domain/Orders/Order.cs` (or order-extra join) — figure out how to persist selected extras (likely a join table `OrderExtras` like `OrderSelectedServices`).

**Sub-phase D.4 — Mobile (ES4 + ES5 + ES6)**
15. `core/booking/BookingPolicy.kt` — NEW: constants + SlotStatus enum.
16. `CatalogRepository.kt` — add `extras: StateFlow`, fetch `/api/Extra/GetOverview`.
17. `BookingState.kt` — add `selectedExtraIds: Set<String>`.
18. `BookingViewModel.kt` — wire to Quote + Create commands.
19. `BookingApi.kt` — update `QuoteOrderCommand`, `QuoteOrderResponse` data classes.
20. `ConfirmStep.kt` — render ExtrasCard with toggles.
21. `WhenWhereStep.kt` — wire to real BookingPolicy + first-tap warning dialog.

**Sub-phase D.5 — Customer web (ES7)**
22. `libs/cleansia-customer-features/order-wizard/.../order-wizard.facade.ts` — fetch extras, manage selectedExtraIds state, pipe to quote API.
23. `order-wizard.component.html` — render extras section + surcharge line item.

**Sub-phase D.6 — i18n (ES8)**
24. Mobile: 8 keys × 5 locales (40 entries) — `values/strings.xml`, `values-cs/`, `values-sk/`, `values-uk/`, `values-ru/`.
25. Web: 5 keys × 5 locales (25 entries) — `apps/cleansia.app/src/assets/i18n/{en,cs,sk,uk,ru}.json`.

**Verification at each sub-phase:**
- Backend: `dotnet build Cleansia.Api.sln` clean.
- Mobile: `./gradlew :app:compileDebugKotlin :app:testDebugUnitTest`.
- Web: `npx nx build cleansia.app`.

---

## Execution order

1. Phase A (Mapbox) — fast, no gates, ships standalone.
2. Phase B.1 (Web Google) — fast, no gates.
3. Phase B.3 (Mobile forgot-password) — easier than B.2, build trust on mobile changes.
4. Phase B.2 (Mobile Google) — harder, builds on B.3 ViewModel pattern.
5. Phase C (Decision docs) — text only.
6. Phase D — gated multi-step.

If anything in Phase B blocks (build failure, missing partner-app Google reference impl to mirror), surface immediately and either reroute or skip with rationale.
