# Frontend Cleanup Plan

Backend is shipped (waves 3F-3K + final code-quality pass). NSwag clients have been regenerated (Customer/Partner/Admin). This plan covers the frontend-side work that closes the loop and reviews architecture before the next feature wave.

## Phase 1 — Backend-driven follow-ups (blocking user-facing bugs)

### TASK-FE-1: Add missing backend error-key translations

**Severity:** HIGH — users currently see raw keys (e.g. `errors.gdpr.deletion_already_pending`) instead of localized messages.

**Scope:** 6 backend `BusinessErrorMessage` keys × 5 languages × 3 apps = 90 i18n entries.

**Keys to add under `errors.*`:**
- `gdpr.deletion_already_pending`
- `gdpr.deletion_blocked_by_order`
- `gdpr.deletion_blocked_by_invoice`
- `auth.refresh_token_reused`
- `order.review.already_exists`
- `recurring_booking.not_found`

**Files (15):**
- `apps/cleansia.app/src/assets/i18n/{en,cs,sk,uk,ru}.json`
- `apps/cleansia-partner.app/src/assets/i18n/{en,cs,sk,uk,ru}.json`
- `apps/cleansia-admin.app/src/assets/i18n/{en,cs,sk,uk,ru}.json`

Match tone/format with adjacent existing entries in each file. Some apps may not surface every key (e.g. partner won't trigger GDPR delete flows), but include them for completeness so any future regression doesn't show raw keys.

---

## Phase 2 — Architecture review

The audits below should produce findings, not changes. Each is scoped to one slice so they can be parallelized.

### TASK-FE-2: Customer app audit (`cleansia.app`)

Verify against CLAUDE.md conventions:
- Components delegate ALL business logic to facades (`*.facade.ts`)
- Facades extend `UnsubscribeControlDirective`
- Signal-based state (no inline `BehaviorSubject` where a signal would do)
- `ChangeDetectionStrategy.OnPush` on presentational components
- No raw `<select>`/`<input>`/`<button>` — must use `<cleansia-*>` shared components or PrimeNG
- Translations via `TranslatePipe` (standalone) — no hardcoded user strings
- No inline templates/styles in components
- No `any` type
- Routes use `loadComponent()` lazy-loading where appropriate (SSR perf)
- NgRx stores in `data-access/customer-stores` are used for cross-feature state, not feature-local state

### TASK-FE-3: Partner app audit (`cleansia-partner.app`)

Same conventions as Phase 2A, plus:
- SPA — verify route guards on all authenticated routes
- Confirm `auth.interceptor` attaches Partner audience JWT
- Order/payroll feature modules respect facade pattern

### TASK-FE-4: Admin app audit (`cleansia-admin.app`)

Same conventions as Phase 2A, plus:
- Verify `[Permission]` policy claims are resolved client-side via the policy directive
- Confirm role-gated UI elements hide when the user lacks permission (defense in depth — backend enforces, but UX shouldn't show then 403)

### TASK-FE-5: Shared libs audit

`libs/shared/`, `libs/data-access/`, `libs/core/`:
- No app-specific imports inside shared libs
- NSwag clients (`libs/core/{customer,partner,admin}-services/`) untouched (auto-generated)
- `cleansia-*-features` libs scoped per audience (no cross-feature deps that should be shared)
- Component story: every reusable shared component should have a use site outside its origin module

---

## Phase 3 — Apply findings

After audits 2-5, batch the fixes by category:
- Convention violations (high impact: facades, change detection, hardcoded strings)
- Dead code / unused exports
- Component duplication that should fold into shared
- Translations missing for newly added flows

---

## Phase 4 — Verify

Per app:
- `npx nx lint cleansia-app && npx nx build cleansia-app --configuration=production`
- `npx nx lint cleansia-partner-app && npx nx build cleansia-partner-app --configuration=production`
- `npx nx lint cleansia-admin-app && npx nx build cleansia-admin-app --configuration=production`

For UI changes that affect user-facing flows: start the dev server, exercise the golden path + at least one edge case in a browser. Type/lint checks confirm code shape, not feature correctness.

---

## Out of scope (deferred to product wave)

- Lighthouse perf optimization (was IMP item, not regression)
- Recurring-booking UX (backend stub only — no UI yet)
- Mobile app changes (separate workstream)

---

# Audit findings (recorded after Phase 2 audit, before Phase 3)

Status as of audit: i18n keys added (Phase 1 ✅). Phase 2 audit run via 4 parallel agents covering Customer / Partner / Admin / Shared. Totals: **53 HIGH, 41 MEDIUM** findings.

## Customer app (`cleansia.app`)

**6 HIGH, 13 MEDIUM**

### HIGH — Components doing facade work (no facade exists)
- `profile/profile.component.ts:181-443` (496 LOC) — direct calls to `customerClient.userClient.updateCurrentUser`, `changePassword`, `countryClient.getOverview`. **Extract `ProfileFacade`.**
- `orders/order-detail/order-detail.component.ts:85-225` (312 LOC, also `(err: any)` at L225) — direct `membershipClient.getMine`, `orderClient.getById/downloadReceipt/submitReview`. **Extract `OrderDetailFacade`.**
- `gdpr/gdpr.component.ts:67-146` — direct `gdprClient.consentsGet/export/deleteAccount` + `authService.logout()`. **Extract `GdprFacade`.**
- `disputes/disputes.component.ts:168,195` — direct API subscriptions. **Extract `DisputesFacade`.**
- `profile/membership/membership-management.component.ts` + `membership-subscribe.component.ts` — 4+ direct `.subscribe()` API calls. **Extract `MembershipFacade`.**
- `orders/track-order/track-order.component.ts:105,131` + `order-lookup.component.ts:75` + `guest-order-detail.component.ts:112` — `orderClient.lookup/lookupBatch` from components. **Extract shared `TrackOrderFacade`.**

### MEDIUM
- 9 components missing `OnPush`: `disputes`, `forgot-password`, `gdpr`, `home`, `order-wizard`, `order-detail`, `orders`, `profile`, `services-catalog`.
- 3 facades don't extend `UnsubscribeControlDirective` (use `takeUntilDestroyed()` instead): `order-wizard.facade.ts:56`, `recurring-bookings.facade.ts:41`, `rewards.facade.ts:24`. Either extend or update CLAUDE.md to allow modern equivalent.
- 3 inline templates: `legal-pages/terms`, `legal-pages/privacy`, `order-wizard/components/wizard-summary-step.component.ts:47` (~250 lines).
- 3 `: any` types: `login.component.ts:73`, `register.component.ts:156` (Google callback), `order-detail.component.ts:225`.
- Raw `<button>` tags: ~40+ across `recurring-bookings/create-recurring-wizard` (10+), `profile.component.html` (5+), `services-catalog.component.html` (6+), `order-wizard.component.html` (8+).
- Hardcoded `aria-label="Scroll to top"` in `profile.component.html:410`.
- `services-catalog.component.ts` (235 LOC) has no facade.
- `forgot-password.component.ts:73` has `route.queryParams.subscribe` despite facade existing.

### Clean
- App `app.routes.ts` uses `loadChildren`/`loadComponent` lazy boundary correctly.
- NgRx stores used appropriately for cross-feature state (services, packages, saved address, customer user).

## Partner app (`cleansia-partner.app`)

**28 HIGH, 5 MEDIUM**

### HIGH — Inline templates (16 files)
GDPR component (~100 lines inline) and most order-details sub-components have `template:` strings instead of `templateUrl`. Files:
- `gdpr/gdpr.component.ts:18`
- `profile/components/profile-{personal-info,emergency-contact,bank-details,availability}.component.ts`
- `orders/order-details/components/order-{additional-services,extras,packages,status,payment-info,service-details,customer-info,header}.component.ts`
- `orders/order-details/components/photo-gallery.component.ts:26` (~100 lines)
- `orders/order-details/components/{report-issue-dialog,add-note-dialog}.component.ts` (inline `template:` AND `styles:[]`)

### HIGH — `: any` usage
- `profile/profile.models.ts:121,177` — `EmployeeItem` and `formData` typed as `any`.
- `orders/orders.helpers.ts:27,36` and `orders.component.ts:268,272` — `paymentStatus`/`orderStatus: any` in 4 places. Use `OrderStatus`/`PaymentStatus` enums from `partner-services`.
- `orders/order-details/order-details.component.ts:287` — `orderDetails: any`.
- `orders/order-details/order-details.helpers.ts:157` — `currency: any`.

### HIGH — Business logic / subscriptions in components
- `orders/order-details/components/order-photos.component.ts:50-218` — entire file: 4 `.subscribe()` chains (`loadPhotos`, `savePhotos`, `deletePhoto`, dialog confirm), `partnerClient` injected directly, no facade. **Extract `OrderPhotosFacade`.**
- `profile/profile-documents.facade.ts:50` — does NOT extend `UnsubscribeControlDirective`; `.subscribe()` at line 286 untorn down.
- `orders/orders.component.ts:163-181` — 3 `.subscribe()` blocks in `ngAfterViewInit` (form valueChanges, lang change). **Move to `OrdersFacade` (which already extends the directive).**
- `invoices/invoices.component.ts:122-140` — same pattern, 3 subscriptions on form/lang in component.

### HIGH — Missing OnPush (22 components)
Almost every partner component except dashboard + 2 dialogs is missing it. Includes: `forgot-password`, `login`, `register`, `confirm-email`, `gdpr`, `invoices`, `invoice-detail`, `orders`, `order-details` + all `order-details/components/*`, `photo-gallery`, `order-photos`, `profile` + all `profile/components/*`.

### HIGH — Hardcoded string
- `orders/order-details/order-details.component.ts:299` — `${orderDetails.estimatedTime} minutes` patched into form.

### HIGH — Routes / guards
- `app.routes.ts:59` — `CleansiaPartnerRoute.GDPR` has no `authGuard`. Decide intent + add guard.

### MEDIUM
- `forgot-password.component.ts:70` — `route.queryParams.subscribe` without teardown.
- `profile/components/profile-personal-info.component.ts:188` — `control.valueChanges.subscribe` no unsubscribe.
- `photo-gallery.component.ts:71` — hardcoded `'Photo'` alt fallback.
- `auth.interceptor.ts:5-19` — attaches token by URL containing `/api/` only; no 401 refresh-token retry pipeline integration. Document or wire up.
- `photo-gallery.component.ts:17,261` and `order-photos.component.ts:237` + `order-photos.helpers.ts:47` — `date: any`. Use `Date | string`.

## Admin app (`cleansia-admin.app`)

**8 HIGH, 5 MEDIUM**

### HIGH — Cross-cutting structural issues
- **`CleansiaPermissionDirective` does not exist anywhere.** Convention #10 (defense-in-depth UI gating by policy claim) is fully unimplemented. Admin relies on `adminGuard` route check + backend enforcement. **Implement directive in `libs/shared/directives` and apply on action buttons.**
- **37 of 38 facades do NOT extend `UnsubscribeControlDirective`.** Only `admin-login.facade.ts:14` does. All others use ad-hoc `private destroy$ = new Subject<void>()`.
- 22 of 42 components missing `OnPush`: `admin-user-management`, `employee-detail`, `reports`, `admin-login`, `admin-order-photos`, etc.

### HIGH — Components doing facade work
- `order-detail/components/admin-order-photos.component.ts:41,89-108` — injects `AdminClient` directly, calls `adminOrderClient.photos()` + `.subscribe()`. **Extract `AdminOrderPhotosFacade`.**
- `employee-detail/employee-detail.component.ts:145,149` — subscribes to NgRx selector + form `valueChanges` in component without `takeUntil`.
- `loyalty-promo-codes/promo-code-form.component.ts:163,168,177` — 3 raw `.subscribe()` on `valueChanges` no unsubscribe.
- `loyalty-tier-configs/tier-configs.component.ts:148` — same pattern.

### HIGH — `: any` abuse
- `employee-detail.facade.ts:37` — `readonly employee = signal<any>(null)`. Use `EmployeeDetailDto`.
- `employee-detail.facade.ts:403,412,421` — 3 mappers with `(s: any)`.
- `pay-config-form.facade.ts:89,90,103,107,108,121,125,126` — 8 `: any` mapping responses.
- `reports.component.ts:114,133,152,177,183,189,195,214,223,234` — 10 `(row: any)` value-getters in column defs.

### MEDIUM
- `currency-management.models.ts:103,121` + `currency-management.component.ts:105` — `(row as any).isDefault`. Add field to typed model.
- `employee-detail/employee-documents-section.component.ts:22` — inline `template:` (~80 lines).
- `employee-detail.component.ts:130-132` — hardcoded `'Junior (0.5x)'` etc.
- `admin-order-photos.component.ts:118` — `dateObj.toLocaleString('en-GB')` ignores user lang.
- `loyalty-tier-configs/tier-configs.component.html:24-52` — uses raw `<p-table>` instead of `<cleansia-table>` (the other 19 list views are correct).

### Clean
- All 20 feature routes gated with `adminGuard`; login uses `guestGuard`.
- 19 of 20 list views use `<cleansia-table>` correctly.
- No raw `<select>`/`<button>`/`<input>` HTML — PrimeNG/cleansia wrappers used.
- Login is admin-only by design (backend rejects non-admin profile, UI doesn't surface other login flows).

## Shared libs

**11 HIGH, 18 MEDIUM**

### HIGH — Audience leak (3 components)
- `libs/shared/components/cleansia-customer-footer.component.ts:4` — imports `CustomerAuthService` from `@cleansia/customer-services`.
- `libs/shared/components/cleansia-customer-navbar.component.ts:24` — same.
- `libs/shared/components/cleansia-registration-lock.component.ts:4-13` — imports from `@cleansia/partner-services` AND `@cleansia/partner-stores`.

**Move all 3 to their respective audience-features lib.**

### HIGH — Store cross-import (7 files)
NgRx admin/customer stores import DTOs from `@cleansia/partner-services`:
- `libs/data-access/admin-stores/user/user.{state,actions}.ts` — `UserListItem` from partner-services.
- `libs/data-access/customer-stores/user/{state,actions}.ts` — `UserListItem`/`ApiException` from partner-services.
- `libs/data-access/customer-stores/order/{state,actions}.ts` — `OrderItem`/`OrderListItem` from partner-services.
- `libs/data-access/customer-stores/dispute/{state,actions}.ts` — `DisputeDetails` from partner-services.
- `libs/data-access/customer-stores/catalog/{state,actions}.ts` — `PackageListItem`/`ServiceListItem` from partner-services.
- `libs/data-access/customer-stores/user.effects.ts:3` — `GetCurrentUserQuery` from partner-services.

### MEDIUM — Orphan / mis-located shared components
- `cleansia-top-navbar/` — zero usages. Delete.
- `cleansia-country-select/` — zero usages. Delete.
- `cleansia-time-picker/` — only self-references. Delete or wire up.
- `cleansia-customer-footer` + `cleansia-customer-navbar` — only customer.app uses them. Move to `cleansia-customer-features/`.
- `cleansia-registration-lock` — only partner.app uses. Move to `cleansia-partner-features/`.
- `libs/shared/charts/` — empty package, no source. Delete the lib.

### MEDIUM — `: any` in shared utils/components
- `data-access/partner-stores/dashboard.reducer.ts:32-37` — error fields + state fields all `any|null`.
- `data-access/admin-stores/user.effects.ts:18` — `user: {} as any`.
- `shared/utils/object.utils.ts:18,22,27` — `getObjectValues(obj: any)`, `parseBlobToJson Promise<any>`, `convertEnumToArray(enumObj: any)`.
- `shared/utils/form.utils.ts:56,100,115,159,177` — 5 public form helpers all `any`.
- `shared/components/cleansia-table.models.ts:64,74-76` — `pipeArgs?: any[]`, `(item: any)` callbacks.
- `shared/components/cleansia-{select,multiselect,radio,textarea,text-input,calendar}/*.component.ts` — `output<any>()`, `writeValue(value: any)`, `innerValue: any`.
- `shared/models/filter.models.ts:15,20,33,39,64` — filter value/initialValue/template all `any`.
- `core/services/dialog.service.ts:51` — `messageParams?: Record<string, any>`. Use `unknown`.
- `shared/components/cleansia-customer-footer.ts:34` — `submitRequest(form: any)`.

### MEDIUM — `core/services/` business logic (should be features)
- `core/services/guest-order.service.ts` — local-storage CRUD + 5-order cap business rule, not a thin client wrapper. Move to `cleansia-customer-features/`.
- `core/services/snackbar.service.ts:124-137` — hardcoded `knownMappings` (afterphotosrequired, ordernotinprogress, etc.) — domain knowledge in a "core" service. Move mapping table to features layer.
- `core/services/mapbox-autocomplete.service.ts:106` — hardcoded `country=cz,sk`. Inject as config.

### MEDIUM — OnPush
- `libs/shared/components/` — only 12 of 38 components declare `OnPush`. Add to remaining 26.

### LOW
- `libs/shared/components/index.ts` — barrel re-exports inconsistent. Re-export every component or delete dead folders.

### Clean
- No app-import leaks (no `from 'apps/*'` or feature-lib imports inside `libs/shared/`).

---

## Phase 3 — Apply findings (waves)

Sequential application — each wave builds + lints before the next starts.

### Wave 1 — Cross-cutting safety (highest leverage, lowest risk)
1. Fix 7 store cross-imports (admin/customer stores using partner-services).
2. Move 3 audience-specific components out of `shared/components/`.
3. Add `authGuard` to partner GDPR route.
4. Delete 4 orphan shared components (`cleansia-top-navbar`, `cleansia-country-select`, `cleansia-time-picker` if dead, `libs/shared/charts/`).

### Wave 2 — Convention compliance
5. Add `ChangeDetectionStrategy.OnPush` to ~50 components (mostly mechanical).
6. Replace ~25 `: any` types with proper DTOs/enums from generated clients.
7. Sweep all 37 admin facades + 4 partner facades to extend `UnsubscribeControlDirective`.

### Wave 3 — Inline template extraction
8. Extract 16 partner inline templates to `.html` files.
9. Extract 3 customer inline templates.
10. Extract 1 admin inline template.

### Wave 4 — Component → facade refactor
11. Customer: extract 6 missing facades (`profile`, `gdpr`, `disputes`, `orders`, `track-order`, `membership`).
12. Partner: extract `OrderPhotosFacade`, move subscriptions out of `orders/invoices`/forms.
13. Admin: extract `AdminOrderPhotosFacade`, fix `employee-detail`/`promo-code-form`/`tier-configs` subscription leaks.

### Wave 5 — Admin permission directive
14. Implement `CleansiaPermissionDirective` in `libs/shared/directives/`.
15. Apply to action buttons across 20 admin features.

### Wave 6 — Polish
16. Hardcoded strings → translations (handful of cases).
17. Replace ~40 raw `<button>` tags with `<cleansia-button>` (customer + partner).
18. Tighten generic types on shared form/table components (`<T>` instead of `any`).
19. Clean up `core/services/` (move guest-order to customer-features; make snackbar mapping configurable; mapbox country whitelist via config).
20. Convert `loyalty-tier-configs` `<p-table>` to `<cleansia-table>` for consistency.

---

## Verification protocol

After each wave:
```bash
cd src/Cleansia.App
npx nx lint cleansia-app && npx nx build cleansia-app --configuration=production
npx nx lint cleansia-partner-app && npx nx build cleansia-partner-app --configuration=production
npx nx lint cleansia-admin-app && npx nx build cleansia-admin-app --configuration=production
```

After Wave 4 + 5: also start dev server for the affected app and exercise the golden path of touched features in a browser.
