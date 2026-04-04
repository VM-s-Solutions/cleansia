# Cleansia - Bug Fixes & Improvements Plan

> Generated: 2026-04-02 | Updated: 2026-04-03 | Status: In Progress

---

## Table of Contents

1. [Customer App Issues](#1-customer-app-issues)
2. [Partner App Issues](#2-partner-app-issues)
3. [Cross-App Issues](#3-cross-app-issues)
4. [Documentation Platform](#4-documentation-platform)

---

## 1. Customer App Issues

### 1.1 Lighthouse Performance Optimization (Score: 46% -> 80%+)

**Priority:** High | **Effort:** Medium | **Status: NOT STARTED**

**Action Items:**
- [ ] Run Lighthouse audit locally and identify top 5 bottlenecks
- [ ] Implement route-level lazy loading for non-critical feature modules
- [ ] Add `loading="lazy"` to below-fold images
- [ ] Preload critical fonts (Nunito, Poppins) with `<link rel="preload">`
- [ ] Optimize PrimeNG imports — use tree-shakable imports instead of full modules
- [ ] Add `OnPush` change detection to remaining components
- [ ] Compress assets with Brotli in production build

---

### 1.2 Order Status Update Emails

**Priority:** High | **Effort:** Medium | **Status: DONE**

- [x] Added `EmailType.OrderStatusUpdate` enum value
- [x] Added `SendOrderStatusUpdateEmailAsync()` to `IEmailService` and `EmailService`
- [x] Created SendGrid dynamic template (`email-templates/order-status-update.html`) — professional design matching other templates
- [x] Created test data for SendGrid preview (`email-templates/test-data/`)
- [x] Wired into `CompleteOrder.Handler` and `StartOrder.Handler`
- [x] Added `SendGrid__OrderStatusUpdateTemplateId` config and az cli script (`scripts/configure-order-status-email.sh`)
- [x] Subject is dynamically translated from backend translations
- [x] All 6 email templates redesigned with professional corporate style

---

### 1.3 Order Receipt PDF — Extras & Design

**Priority:** Medium | **Effort:** Low | **Status: DONE**

- [x] Service prices now include `PerRoomPrice * (rooms + bathrooms)` — no more hidden charges
- [x] Added `Extras` field to `ReceiptPdfData` (list of selected extra names)
- [x] Added `BuildExtrasSection` in receipt layout (shows extras as tags)
- [x] Added `BuildOrderDetails` section (cleaning date, rooms, bathrooms, estimated duration)
- [x] Added `PaymentType` display alongside `PaymentStatus`
- [x] Removed redundant `Subtotal` (was always same as `Total`)
- [x] Payment section redesigned — plain text labels instead of colored badges
- [x] Footer now includes generation timestamp

---

### 1.4 "View Order Status" Button — Anonymous Access

**Priority:** High | **Effort:** Medium | **Status: DONE**

- [x] Existing `/track-order` route is already public (no auth guard)
- [x] `TrackOrderComponent` enhanced to auto-fill from email link query params (`?orderNumber=X&email=Y`)
- [x] All email links updated to use `/track-order?orderNumber={orderNumber}&email={email}` format
- [x] Refactored to use `CustomerOrderClient` (generated NSwag client) instead of raw `HttpClient`
- [x] Added rate limiting (`[EnableRateLimiting("auth")]`) to `Lookup` and `LookupBatch` endpoints — prevents brute-force

---

### 1.5 Order Receipt Email — Currency Symbol

**Priority:** Medium | **Effort:** Low | **Status: DONE**

- [x] Fixed `ToString("C")` (which produced `¤` on invariant culture) to `{symbol}{amount:N2}`
- [x] Receipt email now shows correct currency symbol from order data

---

### 1.6 Cash vs Card Receipt Sending

**Priority:** Low | **Effort:** None | **Status: VERIFIED (NO CHANGE NEEDED)**

- Cash orders: receipt generated immediately on creation (correct — order confirmed right away)
- Card orders: receipt generated after Stripe webhook confirms payment (via `CompleteOrder`)

---

## 2. Partner App Issues

### 2.1 "Take Order" on Detail Page

**Priority:** High | **Effort:** Low | **Status: DONE**

- [x] Added `canTakeOrder` computed signal (checks: pending/confirmed status, employee not already assigned)
- [x] Added `takeOrder()` method to component and `takeOrder()` to facade
- [x] Added Take Order button to `order-details.component.html`

---

### 2.2 Start/Complete Order Container Design

**Priority:** Low | **Effort:** Low | **Status: DONE**

- [x] Removed gradient backgrounds, pulse animations, glow effects from `__primary-action`
- [x] Clean right-aligned buttons with simple hover states
- [x] Complete Order button uses green (`#16a34a`), Take Order uses blue (`#0284c7`)
- [x] Report Issue + Add Note + Complete Order now in same row (`__actions-row`)

---

### 2.3 Order Flow — Aligned with Android App

**Priority:** Medium | **Effort:** Medium | **Status: DONE**

- [x] Removed `CompleteOrderDialogComponent` — replaced with direct completion button
- [x] Auto-calculates `actualMinutes` from IN_PROGRESS status history `createdOn` timestamp
- [x] Created `ReportIssueDialogComponent` — danger-themed dialog with textarea, calls `reportIssue` endpoint
- [x] Created `AddNoteDialogComponent` — primary-themed dialog with textarea, calls `addNote` endpoint
- [x] Added "Report Issue" (red outlined) and "Add Note" (blue outlined) buttons during IN_PROGRESS
- [x] Added elapsed time display (`__elapsed-time`) showing hours/minutes since order started
- [x] Backend endpoints `ReportOrderIssue` and `AddOrderNote` already existed — no backend changes needed

---

### 2.4 Order Photos — 409 Error (SAS URLs)

**Priority:** Critical | **Effort:** Medium | **Status: DONE**

- [x] Added `GenerateSasUri(string blobName, TimeSpan expiry)` to `IBlobContainerClient`
- [x] Implemented in `BlobContainerClient` for both connection string and managed identity modes
- [x] `GetOrderPhotos.Handler` now returns SAS-signed URLs (1-hour expiry)
- [x] Delete photo button changed to solid red background with white icon (danger style)

---

### 2.5 "Assigned Employee" Display

**Priority:** Medium | **Effort:** Low | **Status: DONE**

- [x] Template now iterates all employees (was only showing `[0]`)
- [x] Shows `fullName`, `phoneNumber`, and `email` for each employee
- [x] Section title pluralizes ("Assigned Employee" vs "Assigned Employees")
- [x] Added label/value styles with separator between employees

---

### 2.6 Dashboard "Pending Earnings"

**Priority:** Medium | **Effort:** Medium | **Status: DONE**

- [x] Changed `CurrentPeriodEarnings` to use `GetUnassignedPays()` sum (completed orders not yet invoiced)
- [x] Falls back to latest invoice amount if no unassigned pays exist

---

### 2.7 Print Functionality

**Priority:** Low | **Effort:** Low | **Status: DONE**

- [x] Added `@media print` rules to hide: action buttons, elapsed time, back button, header actions (print/download buttons)

---

### 2.8 Registration Lock Flash

**Priority:** Medium | **Effort:** Low | **Status: DONE**

- [x] Fixed `shouldShowRegistrationLock` to check `employeeStatus != null` before showing
- [x] No longer flashes "incomplete profile" while API is still loading

---

### 2.9 Translations

**Priority:** Low | **Effort:** Low | **Status: DONE**

- [x] Added missing translations to EN and CS: `elapsed_time`, `report_issue`, `add_note`, `take_order`, `assigned_employees`, `employee_email`

---

## 3. Cross-App Issues

### 3.1 Currency Inconsistency

**Priority:** Critical | **Effort:** Medium | **Status: DONE (web apps)**

**Fixed files (14+):**
- [x] `ReceiptService.cs` — fallback changed from `€` to `Kč`
- [x] `FileExtensions.cs` — fallback changed from `EUR`/`€` to `CZK`/`Kč`
- [x] `order-packages.component.ts` — uses dynamic `currencyCode()` input
- [x] `orders.models.ts` — uses `row?.currency?.code || 'CZK'`
- [x] `order-management.models.ts` — uses `row.currency?.symbol || 'Kc'`
- [x] `order-detail.facade.ts` — uses `order?.currency?.symbol || 'Kc'`
- [x] `service-management.facade.ts` — changed to `'CZK'`
- [x] `package-management.facade.ts` — changed to `'CZK'`
- [x] `invoice-management.models.ts` — changed to `'CZK'`
- [x] `invoice-detail.facade.ts` — changed to `'CZK'`
- [x] `invoices.models.ts` — changed to `'CZK'`
- [x] `invoice-detail.component.ts` — changed to `'CZK'`
- [x] `reports.facade.ts` — changed to `'CZK'` with `cs-CZ` locale
- [x] `EmailService.cs` — receipt email uses `{symbol}{amount:N2}` instead of `ToString("C")`

**Remaining:**
- [ ] **Android App** — 10+ files still have hardcoded `"EUR"` defaults (separate task)
- [ ] **Shared Currency Pipe** — consider creating `CleansiaCurrencyPipe` for consistent formatting across Angular apps

---

### 3.2 Blob Storage Photo Access

See [2.4 Order Photos](#24-order-photos--409-error-sas-urls) — **DONE**

---

## 4. Documentation Platform (SWA App)

**Priority:** Low | **Effort:** High | **Status: NOT STARTED**

**Recommended Stack:** VitePress + Azure Static Web Apps

**Action Items:**
- [ ] Initialize VitePress project in `docs/` folder
- [ ] Configure Azure SWA for the docs site
- [ ] Create GitHub Actions workflow for auto-deploy
- [ ] Write initial architecture overview
- [ ] Document each app's features and flows
- [ ] Add API endpoint documentation (auto-generate from Swagger/OpenAPI)

---

## Summary

| # | Issue | Priority | Status |
|---|-------|----------|--------|
| 2.4 | Order photos 409 error (SAS URLs) | Critical | **DONE** |
| 3.1 | Currency inconsistency | Critical | **DONE** (web) |
| 1.2 | Order status emails | High | **DONE** |
| 1.4 | View Order Status link | High | **DONE** |
| 2.1 | Take Order on detail page | High | **DONE** |
| 1.1 | Lighthouse performance | High | NOT STARTED |
| 2.6 | Dashboard pending earnings | Medium | **DONE** |
| 2.3 | Order flow (Android alignment) | Medium | **DONE** |
| 1.3 | Receipt extras & design | Medium | **DONE** |
| 2.5 | Assigned employees display | Medium | **DONE** |
| 1.5 | Receipt email currency | Medium | **DONE** |
| 2.8 | Registration lock flash | Medium | **DONE** |
| 2.2 | Start/Complete order design | Low | **DONE** |
| 2.7 | Print functionality | Low | **DONE** |
| 2.9 | Translations | Low | **DONE** |
| 1.6 | Cash vs card receipt | Low | **VERIFIED** |
| 4 | Documentation platform | Low | NOT STARTED |

**Completion: 15/17 items done (88%)**

Remaining items:
1. **Lighthouse performance** (1.1) — requires profiling and incremental optimization
2. **Documentation platform** (4) — large standalone effort, VitePress + SWA setup
