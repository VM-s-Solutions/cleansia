# Cleansia — Bug Fixes & Improvements Plan (v5)

> Updated: 2026-04-08 | Status: Active

---

## Completed

All items from v3 and v4 have been implemented and verified:

| # | Item | Status |
|---|---|---|
| BUG-1 | submitReview fix | DONE |
| BUG-2 | Default sort | DONE |
| BUG-3 | Registration lock | DONE |
| BUG-4 | Translation keys | DONE |
| BUG-5 | Admin native inputs | DONE |
| BUG-6 | Common translations | DONE |
| BUG-7 | Day names | DONE |
| BUG-8 | Pay config grades | DONE |
| BUG-9 | Password unification | DONE |
| BUG-10 | Address validation | DONE |
| BUG-11 | Past-due filter | DONE |
| BUG-12 | IČO/DIČ model | DONE |
| BUG-13 | Take Order refresh | DONE |
| BUG-14 | Available orders filtering | DONE |
| BUG-15 | New order status | DONE |
| BUG-16 | Review ownership | DONE |
| BUG-17 | Rebook timing | DONE |
| BUG-18 | Loading states | DONE |
| BUG-19 | Note/issue dialog translations | DONE |
| BUG-20 | Notes and issues displayed | DONE |
| BUG-21 | Language switcher cleanup | DONE |
| BUG-22 | New status translations | DONE |
| IMP-2 | Manual pay period creation | DONE |
| IMP-3 | Per-employee pay config | DONE |
| IMP-4 | OrderStatusUpdate template type | DONE |
| CONTENT-1 | Precision Care | DONE |
| CONTENT-2 | 6-step process | DONE |
| CONTENT-3 | FAQ expansion | DONE |
| CONTENT-4 | Benefits rewrite | DONE |

---

## Remaining (needs external setup)

### IMP-1: Google OAuth setup

**Priority:** Medium | **Status: OPEN — needs Google Cloud Console project**

**Current state:** Dev environment has a `googleClientId` from another project. Production and staging have empty strings. Backend skips Google token validation in dev mode.

**What's needed:**
1. Create a Google Cloud project for Cleansia
2. Enable OAuth 2.0 in Google Cloud Console
3. Create OAuth client ID (Web application type)
4. Add authorized redirect URIs for each environment
5. Set `googleClientId` in `environment.prod.ts` and `environment.staging.ts`
6. Configure backend `appsettings.json` with Google client secret

**Files:**
- `apps/cleansia-partner.app/src/environments/environment.prod.ts`
- `apps/cleansia-partner.app/src/environments/environment.staging.ts`
- Backend `appsettings.Production.json`

**Note:** For Seznam.cz email: Google OAuth won't work — Seznam is not a Google identity provider. Users with Seznam email need to use email/password login.

---

## v5 — Current Work

### TASK-001: Lighthouse performance optimization (Customer App)

**Priority:** High | **Status: PLANNED**

**Symptom:** Lighthouse Performance score at 46%.

**Root causes identified:**
- No route preloading strategy configured
- Dual animation modules (BrowserAnimationsModule + provideAnimationsAsync)
- All `<img>` tags use standard HTML instead of NgOptimizedImage
- Redundant CDN stylesheet preloading in index.html

**Files:** `app.config.ts`, `index.html`, hero/benefits/gallery component templates

---

### TASK-002: Order status update email not sending

**Priority:** High | **Status: PLANNED**

**Symptom:** No email sent when order status changes (start, complete).

**Root cause:** Code is fully implemented (EmailType.OrderStatusUpdate, SendOrderStatusUpdateEmailAsync in CompleteOrder/StartOrder handlers, full EmailService implementation). Likely missing `OrderStatusUpdateTemplateId` in SendGrid appsettings config — code silently fails if template ID is null.

**Files:** `appsettings.json` (Customer + Partner APIs), `EmailService.cs`

---

### TASK-003: Order receipt PDF missing extras pricing

**Priority:** Medium | **Status: PLANNED**

**Symptom:** PDF receipt shows extras as name badges only, no individual prices.

**Root cause:** `ReceiptService.CreateReceiptData()` extracts extras as `List<string>` (names only). `ReceiptPdfData.Extras` is string-typed. `DefaultReceiptLayoutBuilder.BuildExtrasSection()` renders as tags without prices.

**Files:** `ReceiptPdfData.cs`, `ReceiptService.cs`, `DefaultReceiptLayoutBuilder.cs`

---

### TASK-004: Anonymous order detail page (Customer App)

**Priority:** High | **Status: PLANNED**

**Symptom:** "View Order Status" button in receipt email redirects to login. No anonymous order detail page exists.

**Root cause:** `/orders/:id` route is behind `customerAuthGuard`. `/track-order` route is public but only shows summary, no detail view. Need a new public route with a component that uses the Lookup API (orderNumber + email).

**Files:** `app.routes.ts`, `track-order.component.html/ts`, new `anonymous-order-detail` component
