# Cleansia - Bug Fixes & Improvements Plan

> Generated: 2026-04-02 | Status: Planning

---

## Table of Contents

1. [Customer App Issues](#1-customer-app-issues)
2. [Partner App Issues](#2-partner-app-issues)
3. [Cross-App Issues](#3-cross-app-issues)
4. [Documentation Platform](#4-documentation-platform)

---

## 1. Customer App Issues

### 1.1 Lighthouse Performance Optimization (Score: 46% -> 80%+)

**Priority:** High | **Effort:** Medium

**Root Causes to Investigate:**
- Large bundle size (check `npx nx build cleansia.app --stats-json` output)
- Unoptimized images (mascot, logos not using WebP/AVIF with proper dimensions)
- Render-blocking CSS (PrimeNG theme loaded synchronously)
- Missing lazy loading on below-fold components
- SSR hydration mismatch causing re-renders

**Action Items:**
- [ ] Run Lighthouse audit locally and identify top 5 bottlenecks
- [ ] Implement route-level lazy loading for non-critical feature modules
- [ ] Add `loading="lazy"` to below-fold images
- [ ] Preload critical fonts (Nunito, Poppins) with `<link rel="preload">`
- [ ] Optimize PrimeNG imports — use tree-shakable imports instead of full modules
- [ ] Add `OnPush` change detection to remaining components
- [ ] Compress assets with Brotli in production build

---

### 1.2 Order Status Update Emails — Not Implemented

**Priority:** High | **Effort:** Medium

**Current State:** No email is sent when order status changes. Only receipt email exists.

**Email notifications needed:**
| Event | Recipient | Template |
|-------|-----------|----------|
| Order Confirmed | Customer | "Your order has been confirmed" |
| Employee Assigned | Customer | "A cleaner has been assigned" |
| Order Started | Customer | "Your cleaning has started" |
| Order Completed | Customer | "Your cleaning is complete" |
| Order Cancelled | Customer | "Your order has been cancelled" |

**Backend Changes:**
- [ ] Add `EmailType` enum values: `OrderConfirmed`, `OrderStarted`, `OrderCompleted`, `OrderCancelled`, `EmployeeAssigned`
- [ ] Add `IEmailService` methods: `SendOrderStatusUpdateEmailAsync(email, order, newStatus, languageCode, ct)`
- [ ] Create SendGrid dynamic templates for each status
- [ ] Wire emails into: `CompleteOrder.Handler`, `StartOrder.Handler`, `TakeOrder.Handler`, `CancelOrder.Handler`
- [ ] Add `SendGrid__OrderStatusTemplateId` config

**Frontend Changes:** None (emails only)

---

### 1.3 Order Receipt PDF — Missing "Extras" Expenses

**Priority:** Medium | **Effort:** Low

**Current State:** `ReceiptPdfData` only includes Services and Packages. The `Order.Extras` dictionary (boolean flags) has no pricing data.

**Root Problem:** Extras are stored as `Dictionary<string, bool>` — they track selection but not individual prices. The total price already includes extras, but they're not itemized.

**Action Items:**
- [ ] Investigate how extras pricing is calculated in `CreateOrder.Handler` — likely summed into `TotalPrice`
- [ ] Add `Extras` field to `ReceiptPdfData` as `List<ReceiptLineItem>`
- [ ] Map extras in `ReceiptService.CreateReceiptData()` — need to source prices from service/package configuration
- [ ] Add extras rows to `DefaultReceiptLayoutBuilder.BuildItemsTable()` after packages section
- [ ] Test with orders that have extras selected

---

### 1.4 "View Order Status" Button — Redirects to Login

**Priority:** High | **Effort:** Medium

**Current State:**
- Receipt email has a "View Order Status" link: `{domain}/orders?orderId={id}`
- The route expects a path param (`/orders/:orderId`), not query param
- `OrderController.GetById()` requires authentication (`[Permission(Policy.CanViewOrderDetail)]`)
- No anonymous order detail endpoint exists for the frontend

**Solution: Create Anonymous Order Lookup Page**

**Backend:**
- [ ] The `LookupOrder` endpoint already exists (`[AllowAnonymous]`) and accepts `orderNumber` + `email`
- [ ] Create new `[AllowAnonymous] GetOrderByConfirmationCode(string orderId, string code)` endpoint
- [ ] Generate a short-lived confirmation code per order for email links

**Frontend:**
- [ ] Create `/order-status/:orderId` route (anonymous, no auth guard)
- [ ] Create `OrderStatusComponent` — displays order details in read-only mode
- [ ] If user is not logged in, show a verification step (enter email to verify ownership)
- [ ] Update `EmailService` to generate correct URL: `{domain}/order-status/{orderId}`

---

## 2. Partner App Issues

### 2.1 "Take Order" Not Working on Order Detail Page

**Priority:** High | **Effort:** Low

**Current State:** Take order works from the orders list page but may not be available on the detail page.

**Investigation:**
- `orders.facade.ts:153` handles take order from list
- Order detail page (`order-details.component.html`) may not have a "Take Order" button for available orders

**Action Items:**
- [ ] Verify if "Take Order" button exists on `order-details.component.html` for orders with status "Pending"
- [ ] If missing, add a Take Order button that calls the same `TakeOrder` endpoint
- [ ] Ensure the button only shows for available orders where employee is not yet assigned
- [ ] Test end-to-end: view available order detail -> take order -> refresh shows assigned

---

### 2.2 "Start Order" Container Design

**Priority:** Low | **Effort:** Low

**Current State:** Simple button with `pi-play` icon in `cleansia-order-details__primary-action` container.

**Action Items:**
- [ ] Review Android app design for Start Order flow
- [ ] Redesign the start order section as a prominent card with:
  - Order summary (date, time, address)
  - Customer info
  - Large "Start Cleaning" call-to-action button
  - Checklist of pre-start requirements
- [ ] Add confirmation dialog before starting

---

### 2.3 Finish Order Flow — Doesn't Match Android App

**Priority:** Medium | **Effort:** Medium

**Current State:** Dialog-based completion with:
- Time comparison (estimated vs actual)
- Actual time input with +15 min buttons
- Completion notes (required)
- Backend requires "after photos" but dialog doesn't show this requirement

**Differences from Android:**
- Android likely has a step-by-step flow: Upload After Photos -> Enter Time -> Add Notes -> Complete
- Web uses a single dialog without photo upload step

**Action Items:**
- [ ] Add photo upload requirement to the completion flow (before showing dialog)
- [ ] Show "Upload After Photos" step if no after photos exist yet
- [ ] Display photo count indicator in the dialog
- [ ] Consider multi-step dialog: Step 1 (Photos) -> Step 2 (Time & Notes) -> Complete
- [ ] Validate that after photos exist before allowing completion (mirror backend validation)

---

### 2.4 Order Photos Not Displaying — 409 Error

**Priority:** Critical | **Effort:** Medium

**Current State:** `GET https://stcleansiasdev.blob.core.windows.net/order-photos/... 409 (Public access is not permitted on this storage account.)`

**Root Cause:** Photos are saved with direct blob URLs in the database. The storage account has public access disabled (which is correct for security). But the frontend loads images directly from blob storage URLs.

**Solution Options:**

**Option A (Recommended): Generate SAS URLs server-side**
- [ ] Modify `GetOrderPhotos.Handler` to generate time-limited SAS tokens for each photo URL
- [ ] Return SAS-signed URLs instead of raw blob URLs
- [ ] SAS tokens expire after 1 hour (configurable)
- [ ] Frontend loads images from SAS URLs — no public access needed

**Option B: Proxy through API**
- [ ] Create `GET /api/order/photo/{photoId}` endpoint that streams the blob
- [ ] Frontend loads images through the API
- [ ] Simpler but adds load to API server

**Action Items (Option A):**
- [ ] Add `GenerateSasUri` method to `IBlobContainerClient`
- [ ] Update `GetOrderPhotos.Handler` to return SAS URLs
- [ ] Update `SaveOrderPhotos.Handler` to store blob name (not full URL) in DB
- [ ] Frontend: no changes needed (still loads from URL, just SAS-signed)

---

### 2.5 "Assigned Employee" Shows Only First Employee

**Priority:** Medium | **Effort:** Low

**Current State:** `order-details.component.html:136` displays only `assignedEmployees![0]`. Orders can have multiple assigned employees.

**Action Items:**
- [ ] Iterate over all assigned employees instead of just index `[0]`
- [ ] Display as a list/cards with each employee's name and phone
- [ ] Update section title from "Assigned Employee" to "Assigned Employees" (pluralize when > 1)

---

### 2.6 Dashboard "Pending Earnings" Shows 0

**Priority:** Medium | **Effort:** Medium

**Current State:** 
- `GetDashboardStats.cs:59` calculates: `CurrentPeriodEarnings: latestInvoice?.TotalAmount ?? 0`
- This returns 0 if no invoice has been generated yet
- A completed order doesn't automatically create an invoice — invoices are generated by the pay period system

**Root Issue:** "Pending Earnings" should show earnings from completed orders that haven't been invoiced yet, not just the latest invoice amount.

**Action Items:**
- [ ] Change `CurrentPeriodEarnings` calculation to sum completed orders in current pay period that haven't been invoiced
- [ ] Query: `SUM(order.EmployeePayAmount) WHERE order.Status == Completed AND order.EmployeePayCalculated == true AND order NOT IN invoiced orders`
- [ ] Verify `EmployeePayCalculated` is set correctly when order completes
- [ ] Test: complete an order -> dashboard shows pending earnings > 0

---

## 3. Cross-App Issues

### 3.1 Currency Inconsistency Across Apps

**Priority:** Critical | **Effort:** Medium

**Symptoms:**
- Order created in CZK shows as `¤5,698.00` in receipt (wrong symbol)
- Partner app shows EUR instead of CZK
- Admin app shows EUR

**Root Causes:**

1. **Receipt PDF** — `DefaultReceiptLayoutBuilder` uses `CultureInfo` for formatting. The `¤` symbol appears when the culture doesn't match the currency. The `FormatCurrency` method likely uses `ToString("C")` without specifying the correct culture.

2. **Partner App** — Dashboard stats hardcode `Kč` suffix (`dashboard.facade.ts:150`), but order detail pages may use a different formatting approach or default to EUR.

3. **Admin App** — Currency display not tied to order's currency, may use system default (EUR).

**Hardcoded EUR Locations Found:**

| File | Line | Issue |
|------|------|-------|
| `ReceiptService.cs` | 101 | `order.Currency?.Symbol ?? "€"` |
| `FileExtensions.cs` | 50 | `currency?.Code ?? "EUR"` |
| `order-packages.component.ts` | 18 | `currency:'EUR'` in Angular pipe |
| `invoices.models.ts` | 51 | `invoice.currencyCode \|\| 'EUR'` |
| `invoice-detail.component.ts` | 40 | Hardcoded EUR fallback |
| `orders.models.ts` | 51, 153 | Hardcoded EUR fallback |
| `order-management.models.ts` | 70 | `row.currency?.symbol \|\| 'EUR'` |
| `service-management.facade.ts` | 90 | `currency: 'EUR'` |
| `package-management.facade.ts` | 91 | `currency: 'EUR'` |
| `invoice-management.models.ts` | 49 | Hardcoded EUR |
| `order-detail.facade.ts` | 64 | Hardcoded EUR |
| `reports.facade.ts` | 128 | Hardcoded EUR |
| `dashboard.facade.ts` | 150 | Hardcoded `Kč` |
| **Android** (10+ files) | Various | `"EUR"` defaults in models, screens |

**Action Items:**
- [ ] **Backend**: Ensure all DTOs return `currencyCode` (e.g., "CZK") alongside monetary amounts
- [ ] **Receipt PDF**: Fix `ReceiptService.cs:101` — use `order.Currency?.Symbol` with proper fallback from DB, not hardcoded `€`
- [ ] **Shared Angular Pipe**: Create `CleansiaCurrencyPipe` in `@cleansia/services` that formats using the currency code from the data
- [ ] **Partner App**: Replace all hardcoded EUR in 6 files listed above
- [ ] **Admin App**: Replace all hardcoded EUR in 4 files listed above
- [ ] **Customer App**: Verify checkout and order list use correct currency from order
- [ ] **Android App**: Replace hardcoded EUR defaults in 10+ model/screen files
- [ ] **Dashboard**: Replace hardcoded `Kč` in `dashboard.facade.ts:150` with dynamic currency from order data
- [ ] Remove ALL hardcoded currency symbols (`Kč`, `EUR`, `€`) — always derive from order's `currencyCode`

---

### 3.2 Blob Storage Photo Access (Applies to All Apps)

See [2.4 Order Photos Not Displaying](#24-order-photos-not-displaying--409-error) — the SAS URL solution applies to all apps that display blob-stored content.

---

## 4. Documentation Platform (SWA App)

**Priority:** Low | **Effort:** High

**Goal:** Create a separate Static Web App for comprehensive documentation with:
- Chapters per application (Customer, Partner, Admin, Mobile)
- Architecture overview
- Feature flow descriptions
- API documentation

**Recommended Stack:**
- **Framework**: VitePress (Vue-based, built for documentation)
- **Hosting**: Azure Static Web Apps (SWA)
- **Structure**: Markdown files organized by section
- **CI/CD**: GitHub Actions auto-deploy on push to `docs/` folder

**Why VitePress over alternatives:**
- Markdown-based — easy for developers to write
- Built-in search, dark mode, sidebar navigation
- Generates static site — perfect for SWA
- Used by Vue, Vite, Rollup docs

**Proposed Structure:**
```
docs/
├── .vitepress/
│   └── config.ts          # Site config, sidebar, nav
├── index.md               # Home page
├── architecture/
│   ├── overview.md         # System architecture diagram
│   ├── backend.md          # .NET API structure
│   ├── frontend.md         # Angular app structure
│   ├── database.md         # PostgreSQL schema
│   └── infrastructure.md   # Azure resources
├── customer-app/
│   ├── overview.md
│   ├── authentication.md
│   ├── ordering-flow.md
│   ├── checkout.md
│   └── order-tracking.md
├── partner-app/
│   ├── overview.md
│   ├── onboarding.md
│   ├── order-management.md
│   ├── invoicing.md
│   └── dashboard.md
├── admin-app/
│   ├── overview.md
│   ├── user-management.md
│   ├── order-management.md
│   └── reporting.md
├── mobile-app/
│   ├── overview.md
│   ├── features.md
│   └── api-integration.md
├── api/
│   ├── authentication.md
│   ├── orders.md
│   ├── payments.md
│   └── webhooks.md
└── deployment/
    ├── ci-cd.md
    ├── azure-setup.md
    └── environment-config.md
```

**Action Items:**
- [ ] Initialize VitePress project in `docs/` folder
- [ ] Configure Azure SWA for the docs site
- [ ] Create GitHub Actions workflow for auto-deploy
- [ ] Write initial architecture overview
- [ ] Document each app's features and flows
- [ ] Add API endpoint documentation (auto-generate from Swagger/OpenAPI)

---

## Priority Summary

| # | Issue | Priority | Effort | App |
|---|-------|----------|--------|-----|
| 2.4 | Order photos 409 error | Critical | Medium | Partner |
| 3.1 | Currency inconsistency | Critical | Medium | All |
| 1.2 | Order status emails | High | Medium | Customer |
| 1.4 | View Order Status link | High | Medium | Customer |
| 2.1 | Take Order on detail page | High | Low | Partner |
| 1.1 | Lighthouse performance | High | Medium | Customer |
| 2.6 | Dashboard pending earnings | Medium | Medium | Partner |
| 2.3 | Finish order flow | Medium | Medium | Partner |
| 1.3 | Receipt missing extras | Medium | Low | Customer |
| 2.5 | Assigned employees display | Medium | Low | Partner |
| 2.2 | Start order design | Low | Low | Partner |
| 4 | Documentation platform | Low | High | N/A |
