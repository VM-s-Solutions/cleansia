# Cleansia — Feature Roadmap & Remarks Plan

> Created: 2026-04-03 | Status: Planning

---

## Table of Contents

1. [Pay Period Management](#1-pay-period-management)
2. [Emergency Contact Optional](#2-emergency-contact-optional)
3. [Registration Lock Screen Improvements](#3-registration-lock-screen-improvements)
4. [Admin Employee Profile Editing](#4-admin-employee-profile-editing)
5. [Document Deletion Protection](#5-document-deletion-protection)
6. [Orders Page Redesign](#6-orders-page-redesign)
7. [Order Taking Validations](#7-order-taking-validations)
8. [Customer Rating & Review](#8-customer-rating--review)
9. [Employee Pay Config Admin UI](#9-employee-pay-config-admin-ui)
10. [Cleaning Time Visibility](#10-cleaning-time-visibility)
11. [Documentation Updates](#11-documentation-updates)

---

## 1. Pay Period Management

**Priority:** Low | **Effort:** Low | **Status: ALREADY IMPLEMENTED**

The `CloseExpiredPeriodsAndOpenNewAsync` method in `PayPeriodBackgroundService.cs` already:
- Closes expired periods daily at 2 AM UTC
- Checks if an active period exists
- Auto-creates a new period if none are active (starting day after closed period, 1 month duration)
- Sends email notifications

**Remaining work:**
- [ ] Add a manual "Create Pay Period" button in admin app for edge cases (e.g., first setup)
- [ ] Verify the seed data creates an initial active pay period

**Files:** `src/Cleansia.Functions/Functions/PayPeriodTimerFunction.cs`, `src/Cleansia.Core.AppServices/Services/PayPeriodBackgroundService.cs`

---

## 2. Emergency Contact Optional

**Priority:** Medium | **Effort:** Low | **Status: NEEDS FIX**

**Current state:** Properties are nullable (`string? EmergencyContactName/Phone`) but `IsProfileComplete()` and `GetMissingProfileFields()` require both fields.

**Changes needed:**

**Backend** (`src/Cleansia.Core.Domain/Users/Employee.cs`):
- [ ] Remove `EmergencyContactName` and `EmergencyContactPhone` from `IsProfileComplete()` check (line ~197)
- [ ] Remove them from `GetMissingProfileFields()` list (line ~226)
- [ ] Keep the `UpdateEmergencyContact` handler as-is (already validates only when provided)

**Impact:** Employees can complete registration without emergency contacts. The fields remain available for voluntary input.

---

## 3. Registration Lock Screen Improvements

**Priority:** Medium | **Effort:** Medium | **Status: PARTIALLY DONE, NEEDS ENHANCEMENT**

**Current state:** Shows generic "Profile incomplete" and "Documents missing" messages. Recent fix added `MissingFields` from API.

**Enhancements needed:**

- [ ] Show document approval status explicitly:
  - "Documents uploaded — awaiting admin approval" (with clock icon)
  - "Documents approved" (with checkmark)
  - "Documents rejected — please resubmit" (with X icon and rejection reason)
- [ ] Show profile completion as a checklist with individual field status
- [ ] Add a progress bar (e.g., "Profile 70% complete")
- [ ] Show employee contract status: `Pending` → `UnderReview` → `Approved` / `Rejected`
- [ ] Differentiate between "you need to do something" vs "waiting for admin"

**Files:**
- `libs/shared/components/src/lib/cleansia-registration-lock/`
- `libs/core/partner-services/src/lib/services/registration-completion.service.ts`
- Backend: `RegistrationCompletionStatus` DTO needs `documentApprovalStatus` and `contractStatus` fields

---

## 4. Admin Employee Profile Editing

**Priority:** High | **Effort:** Medium | **Status: NOT IMPLEMENTED**

**Current state:** Admin can only VIEW employee details and manage documents/approval. Cannot edit profile fields.

**Changes needed:**

**Frontend** (`libs/cleansia-admin-features/employee-management/src/lib/employee-detail/`):
- [ ] Convert read-only displays to editable form inputs for:
  - Personal info (name, email, phone, DOB)
  - Address fields
  - Employment info (tax ID, IBAN, passport)
  - Emergency contact
- [ ] Add "Edit" / "Save" / "Cancel" buttons per section (same pattern as availability editing)
- [ ] Show inline validation errors

**Backend:**
- [ ] Create `AdminUpdateEmployee` command handler (admin-specific, bypasses employee ownership check)
- [ ] Add `[Permission(Policy.CanUpdateEmployee)]` to endpoint
- [ ] Add endpoint to `AdminController` or `EmployeeManagementController`

---

## 5. Document Deletion Protection

**Priority:** High | **Effort:** Low | **Status: NOT IMPLEMENTED**

**Current state:** `DeleteMyDocument.cs` handler does NOT check document approval status before deletion.

**Changes needed:**

**Backend** (`src/Cleansia.Core.AppServices/Features/EmployeeDocuments/DeleteMyDocument.cs`):
- [ ] Add validation rule in `Validator`:
  ```csharp
  RuleFor(x => x.DocumentId)
      .MustAsync(async (docId, ct) => {
          var doc = await documentRepo.GetByIdAsync(docId, ct);
          return doc?.Status != DocumentStatus.Approved;
      })
      .WithMessage("Cannot delete an approved document. Contact admin for assistance.");
  ```
- [ ] Return clear error message when employee tries to delete approved document

**Frontend:**
- [ ] Hide or disable delete button for documents with `Approved` status
- [ ] Show tooltip: "Approved documents cannot be deleted"

---

## 6. Orders Page Redesign

**Priority:** High | **Effort:** Medium | **Status: NEEDS REDESIGN**

**Current state:** Uses filter-based layout. Two table definitions exist but tabs aren't clearly visible to new users.

**User requirement:** Display both tables one after another OR have separate pages.

**Option A — Two tables stacked (recommended):**
- [ ] Show "Available Orders" section with its own header and table (always visible)
- [ ] Show "My Orders" section below with its own header and table (always visible)
- [ ] Each section has its own search/filter bar
- [ ] Remove tab navigation entirely
- [ ] "Available Orders" has a count badge in the section title
- [ ] "My Orders" sorted by next cleaning date (soonest first)

**Option B — Separate routes:**
- [ ] `/orders/available` — Available orders page
- [ ] `/orders/my-orders` — My orders page
- [ ] Sidebar shows both links under "Orders" submenu

**Files:**
- `libs/cleansia-partner-features/orders/src/lib/orders/orders.component.html`
- `libs/cleansia-partner-features/orders/src/lib/orders/orders.component.ts`
- `libs/cleansia-partner-features/orders/src/lib/orders/orders.facade.ts`

---

## 7. Order Taking Validations

**Priority:** High | **Effort:** Medium | **Status: NOT IMPLEMENTED**

Three validations needed in `TakeOrder.cs`:

### 7.1 Weekly order limit based on rating

- [ ] Add rating-based order limit validation:

| Rating | Max Orders/Week |
|--------|----------------|
| 0 - 3.5 | 3 |
| 3.6 - 4.5 | 4-6 |
| 4.5+ | 6+ (unlimited) |

- [ ] Query employee's orders in current week (Monday-Sunday)
- [ ] Compare count against limit from rating tier
- [ ] Return clear error: "You've reached your weekly order limit (3). Improve your rating to take more orders."

### 7.2 Time conflict validation

- [ ] Check if employee already has an order at the same date/time
- [ ] Consider estimated duration for overlap detection:
  ```
  New order: 10:00-12:00
  Existing:  11:00-13:00 → CONFLICT
  Existing:  13:00-15:00 → OK
  ```
- [ ] Return: "You already have an order scheduled at this time (ORD-XXXX at 11:00)"

### 7.3 Prevent rapid-fire order taking

- [ ] Add a cooldown (e.g., 10 seconds) between `TakeOrder` calls per employee
- [ ] Use in-memory cache or rate limiter
- [ ] Prevents clicking through all available orders instantly

**Files:**
- `src/Cleansia.Core.AppServices/Features/Orders/TakeOrder.cs` — add validator rules
- `src/Cleansia.Core.Domain/Repositories/IOrderRepository.cs` — add `GetEmployeeOrderCountThisWeek(employeeId)`
- `src/Cleansia.Core.Domain/Repositories/IOrderRepository.cs` — add `HasOverlappingOrder(employeeId, dateTime, durationMinutes)`

---

## 8. Customer Rating & Review

**Priority:** Medium | **Effort:** Medium | **Status: BACKEND DONE, FRONTEND NEEDS WORK**

**Current state:** `SubmitOrderReview` handler exists with rating (1-5) + comment. `OrderReviewDto` maps the data. But customer app UI for submitting reviews needs verification.

**Changes needed:**

**Customer app:**
- [ ] Verify review UI exists on order detail page (`libs/cleansia-customer-features/orders/src/lib/order-detail/`)
- [ ] Add star rating component (1-5 stars, clickable)
- [ ] Add comment textarea (optional, max 1000 chars)
- [ ] Show assigned employee info (name, photo if available)
- [ ] Only show review form for `Completed` orders without existing review
- [ ] Show existing review as read-only after submission

**Partner app / Admin app:**
- [ ] Display customer reviews on order detail page
- [ ] Show average rating on employee profile/dashboard
- [ ] Admin: display reviews in employee detail page

**Backend:**
- [ ] Verify `Employee.AverageRating` is recalculated after new review
- [ ] Add `GetEmployeeReviews` query for admin/partner dashboard

---

## 9. Employee Pay Config Admin UI

**Priority:** Medium | **Effort:** Medium | **Status: BACKEND DONE, NO UI**

**Current state:** Full CRUD API exists (`PayConfigController`): Create, Update, Delete, GetPaged, GetById. Domain model `EmployeePayConfig` exists with service-based pay rates.

**Changes needed:**

**Admin frontend** (`libs/cleansia-admin-features/`):
- [ ] Create `pay-config-management` feature module with:
  - Pay config list page (table with service, base rate, per-room rate, employee)
  - Pay config create/edit form
  - Bulk assignment by grade
- [ ] Add sidebar menu item: "Pay Configs" (pi pi-money-bill)
- [ ] Add route: `/pay-config-management`

**Grade-based prefill feature:**
- [ ] Add predefined grade templates:

| Grade | Base Rate Multiplier | Description |
|-------|---------------------|-------------|
| Junior | 0.5x | New employees, rating < 4.0 |
| Medior | 0.75x | Experienced, rating 4.0-4.5 |
| Senior | 1.0x | Top performers, rating 4.5+ |

- [ ] "Apply Grade" dropdown on employee detail page
- [ ] Auto-creates pay configs for all services based on grade multiplier
- [ ] Admin can override individual rates after applying grade

**Files:**
- Backend: `src/Cleansia.Web/Controllers/PayConfigController.cs` (already exists)
- Frontend: Create new feature in `libs/cleansia-admin-features/pay-config-management/`
- Admin routes: Add to `apps/cleansia-admin.app/src/app/app.routes.ts`

---

## 10. Cleaning Time Visibility

**Priority:** Low | **Effort:** Low | **Status: PARTIALLY DONE**

**Current state:** Orders table shows `cleaningDateTime` formatted as date only (`toLocaleDateString('en-GB')`). Time component is not displayed.

**Changes needed:**

- [ ] Update `getValue` in `orders.models.ts` to include time:
  ```typescript
  getValue: (row) => row?.cleaningDateTime
    ? new Date(row.cleaningDateTime).toLocaleString('en-GB', {
        day: '2-digit', month: '2-digit', year: 'numeric',
        hour: '2-digit', minute: '2-digit'
      })
    : '',
  ```
- [ ] Apply to both Available Orders and My Orders table definitions

**Files:** `libs/cleansia-partner-features/orders/src/lib/orders/orders.models.ts`

---

## 11. Documentation Updates

**Priority:** Low | **Effort:** Low | **Status: ONGOING**

After implementing the features above, update:
- [ ] VitePress docs: `docs/partner-app/order-management.md` — new order page layout, validation rules
- [ ] VitePress docs: `docs/partner-app/onboarding.md` — emergency contact now optional
- [ ] VitePress docs: `docs/admin-app/user-management.md` — admin can edit employee profiles
- [ ] VitePress docs: `docs/api/orders.md` — new validation rules on TakeOrder
- [ ] `agents/DEPLOYMENT_PLAN.md` — update if any config changes needed
- [ ] `agents/BUGFIX_AND_IMPROVEMENTS_PLAN.md` — mark items as done

---

## Implementation Priority

| Phase | Items | Effort | Impact |
|-------|-------|--------|--------|
| **Phase 1** | Emergency contact optional (#2), Document deletion protection (#5), Cleaning time display (#10) | Low | Quick wins |
| **Phase 2** | Order taking validations (#7), Orders page redesign (#6) | Medium | Core functionality |
| **Phase 3** | Admin employee editing (#4), Pay config admin UI (#9) | Medium | Admin capabilities |
| **Phase 4** | Customer rating UI (#8), Registration lock enhancements (#3) | Medium | UX polish |
| **Phase 5** | Documentation updates (#11), Pay period manual creation (#1) | Low | Maintenance |

---

## Summary

| # | Feature | Status | Details |
|---|---------|--------|---------|
| 1 | Pay Period auto-creation | **DONE** | Auto-creates in background job |
| 2 | Emergency contact optional | **DONE** | Removed from IsProfileComplete |
| 3 | Registration lock improvements | **DONE** | Progress bar, categories, status icons |
| 4 | Admin edit employee profile | **DONE** | Backend handler + frontend edit UI |
| 5 | Document deletion protection | **DONE** | NotBeApprovedAsync validator |
| 6 | Orders page redesign | **DONE** | Two stacked sections with count badges |
| 7 | Order taking validations | **DONE** | Weekly limit (rating-based) + time conflict |
| 8 | Customer rating & review | **DONE** | Star rating + comment on order detail |
| 9 | Pay config admin UI | **DONE** | Full CRUD with grade multiplier templates |
| 10 | Cleaning time visibility | **DONE** | Date + time format on orders page |
| 11 | Documentation updates | Ongoing | Update VitePress docs |

**Completion: 10/11 items done (91%)**
