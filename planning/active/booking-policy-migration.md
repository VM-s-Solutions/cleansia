# Booking Policy Migration — Manual Steps

This document lists the manual steps required to complete the booking-policy refactor
that introduces 1-hour arrival windows, minimum lead time, express pricing,
tiered cancellation fees, and persisted saved addresses.

All code changes have been made. The items below cannot be done by Claude and
must be performed by the owner.

---

## 1. EF Core migration

The following schema changes were introduced and need a migration:

**New table `SavedAddresses`:**
- `Id` (string, PK)
- `UserId` (string, FK → Users)
- `AddressId` (string, FK → Addresses)
- `Label` (varchar(50), required)
- `IsDefault` (bool, default false)
- Auditable fields (`CreatedOn`, `UpdatedOn`, `CreatedBy`, `UpdatedBy`)
- Tenant fields
- Filtered unique index `IX_SavedAddresses_UserId_Default_Unique` on `UserId`
  where `IsDefault = true` — enforces one default per user
- Index `IX_SavedAddresses_UserId`

**New columns on `Orders`:**
- `CancelledAt` (datetime, nullable)
- `CancellationRefundAmount` (decimal, nullable)
- `CancellationFeeRate` (decimal, nullable)
- `CancelledBy` (varchar(20), nullable) — "customer" / "cleaner" / "system"
- `CancellationReason` (varchar(500), nullable)

**Command:**

```bash
cd src/Cleansia.Infra.Database
dotnet ef migrations add AddBookingPolicyFields --startup-project ../Cleansia.Web
dotnet ef database update --startup-project ../Cleansia.Web
```

Verify the generated migration file looks sensible before applying.

---

## 2. NSwag client regeneration

Four new endpoints and one new DTO were added to the customer API:

**Customer OrderController additions:**
- `POST /api/order/Cancel` → `CancelOrder.Response`

**New `SavedAddressController`:**
- `GET    /api/savedaddress/GetMine` → `IReadOnlyList<SavedAddressDto>`
- `POST   /api/savedaddress/Add` → `SavedAddressDto`
- `POST   /api/savedaddress/SetDefault` → 200 OK
- `DELETE /api/savedaddress/Delete/{id}` → 200 OK

**New DTOs:** `SavedAddressDto`, `CancelOrder.Command`, `CancelOrder.Response`,
`AddSavedAddress.Command`, `SetDefaultSavedAddress.Command`, `DeleteSavedAddress.Command`,
`GetSavedAddresses.Query`.

**Command:**

```bash
cd src/Cleansia.App
npm run generate-customer-client
```

---

## 3. Web frontend migrations

### 3a. Migrate from localStorage-based saved addresses to backend API

Current state: `libs/cleansia-customer-features/profile/src/lib/profile/profile.component.ts`
uses `cleansia_saved_addresses` key in localStorage.

Action:
- Replace localStorage calls with the newly-generated `SavedAddressClient` methods.
- Keep localStorage as a one-time read during app bootstrap to migrate existing
  user data, then clear the key.

### 3b. Wire cancel endpoint to order detail

Add a "Cancel booking" button on `order-detail.component.html` that calls
`OrderClient.cancel({ orderId, reason })`. Show a confirmation dialog that
displays the current refund amount using the tier math (duplicate the
`BookingPolicy.CalculateCancellationFeeRate` logic in TypeScript, or have the
backend compute-and-return preview). Suggest a preview endpoint for cleanliness.

### 3c. Time-slot visual for "express" and "unavailable"

`order-wizard.component.html` now uses the new availability states. Ensure the
SCSS includes visual styles for the new CSS classes:

- `.order-wizard__time-slot--express` — orange accent, visible "Express +20%" label
- `.order-wizard__time-slot--unavailable` — dimmed 40% opacity, "Unavailable" label
- `.order-wizard__time-slot-tag--express` — small orange pill
- `.order-wizard__time-slot-tag--unavailable` — small grey pill
- `.order-wizard__time-hint--cancel` — info-row styling with cancel hint
- `.order-wizard__cancel-policy` + related — box with 3 tier rows

Also add i18n for `cs.json`, `sk.json`, `uk.json`, `ru.json` to match the
newly-added English keys in `en.json`:
- `pages.order.slot_express`, `slot_unavailable`, `cancel_hint`
- `pages.order.cancel_policy_title` + 6 tier keys

### 3d. Express surcharge in total calculation

`order-wizard.facade.ts` currently calculates `total = services + packages`.
Update to apply `EXPRESS_SURCHARGE_RATE` when the selected slot's availability
is `"express"` and show the surcharge line in the price breakdown.

---

## 4. Mobile app — localizations

The mobile app introduced many new strings in `values/strings.xml` (English).
Mirror them in:
- `values-cs/strings.xml`
- `values-sk/strings.xml`
- `values-uk/strings.xml`
- `values-ru/strings.xml`

Key groups to translate:
- `booking_slot_*` (unavailable, express, earliest)
- `booking_cancel_*` (hint, title, 3 tiers)
- `booking_save_address`, `booking_set_as_default`
- `add_address_*` (all add-address screen strings)
- `address_error_*` + `address_field_*` (validation messages)

---

## 5. Error-message translations

New `BusinessErrorMessage` keys were added:
- `order.cleaning_date.below_lead_time`
- `order.already_cancelled`
- `order.already_completed`
- `order.in_progress_cannot_cancel`
- `order.cancellation_window_closed`
- `address.not_owned_by_user`
- `address.label_required`

Add entries under `errors.*` in all 5 web i18n files and mobile strings.xml files.

---

## 6. First-time customer detection

`CancelOrder.cs` has `const bool isFirstTime = false;` placeholder. Once the
`User` entity or `OrderRepository` exposes a `GetCompletedOrderCount(userId)`
helper, wire it in to activate the 60-minute "oops window" for first-time
customers (vs 15 min default).

---

## 7. Email notifications

Consider new email templates for:
- Cancellation confirmation (shows refund amount + timeline)
- Cleaner-cancelled-your-booking (apology + 500 CZK credit)

Not blocking — the core flow works without them, ops can handle manually initially.

---

## 8. Tests

Cleansia.Tests — add unit tests for:
- `BookingPolicy.CalculateCancellationFeeRate` — each tier + oops window
- `BookingPolicy.IsBelowMinimumLeadTime` / `RequiresExpressSurcharge`
- `CancelOrder.Handler` — happy path, already-cancelled, not-owner, in-progress
- `AddSavedAddress.Handler` — default-flag clearing when `SetAsDefault=true`
- `SetDefaultSavedAddress.Handler` — ownership check, previous default cleared

---

## Summary of timings (confirm with product)

| Setting | Value | Where |
|---|---|---|
| Minimum booking lead time | 2 hours (express), 4 hours (standard) | `BookingPolicy` |
| Express surcharge | +20% | `BookingPolicy.ExpressSurchargeRate` |
| Booking window duration | 1 hour | `BookingPolicy.WindowDurationMinutes` |
| Free-cancel cutoff | 24 hours before start | `BookingPolicy.FreeCancellationHours` |
| Partial-fee cutoff | 4 hours before start | `BookingPolicy.PartialCancellationHours` |
| Partial fee rate | 50% | `BookingPolicy.PartialCancellationFeeRate` |
| Oops window (returning) | 15 min after booking | `BookingPolicy.OopsWindowMinutesStandard` |
| Oops window (first-time) | 60 min after booking | `BookingPolicy.OopsWindowMinutesFirstTime` |
| Cleaner no-show credit | 500 CZK | `BookingPolicy.NoShowCreditCzk` |

All numbers are constants in `Cleansia.Core.AppServices/Features/Orders/BookingPolicy.cs`.
Change them there and mobile/web will need to be kept in sync (or ideally fetched
at runtime via a `/api/config/booking-policy` endpoint — consider for a future pass).
