# Global Rates

The Global Rates feature manages **platform-wide pay rate defaults** that apply to all employees by default. It is implemented in the `@cleansia/admin-features/pay-config-management` library.

::: info Per-employee overrides
This page only manages **global** rates. Per-employee rate overrides are managed on the [Employee Detail page](./user-management) using the bulk grade apply feature.
:::

## Architecture

- `PayConfigManagementFacade` -- List management with pagination
- `PayConfigFormFacade` -- Create / edit form for individual rates
- API calls via `AdminClient.adminPayConfigClient`

## What Is a Global Rate?

A global rate is an `EmployeePayConfig` entity record where `EmployeeId IS NULL`. It defines how much the platform pays an employee for completing a service or package, before any per-employee overrides apply.

### Pay Calculation Resolution Order

When calculating pay for an order:

1. **Per-employee config** (`EmployeePayConfig` where `EmployeeId = currentEmployee.Id`) — used if exists
2. **Global rate** (`EmployeePayConfig` where `EmployeeId IS NULL`) — fallback

This means an employee can have specific overrides for some services and use global rates for others.

## Global Rates List

Route: `/pay-config-management`

The list page shows all global rates with:

| Column | Description |
|---|---|
| Service / Package | Target name |
| Base Pay | Flat rate per job |
| Per Room | Bonus per bedroom |
| Per Bathroom | Bonus per bathroom |
| Description | Optional internal notes |
| Actions | Edit, Delete |

An info banner at the top reminds admins that this page is for global rates only and per-employee rates are managed on the employee detail page.

## Create / Edit Form

Routes:
- `/pay-config-management/create`
- `/pay-config-management/:id/edit`

The form follows the standard admin form pattern (`form-grid` + `form-field` + `cleansia-section`).

### Sections

#### Target (create only)

Three fields:

| Field | Description |
|---|---|
| Service | Target service (mutually exclusive with Package) |
| Package | Target package (mutually exclusive with Service) |
| Currency | Currency for this rate (CZK, EUR, etc.) |

::: warning
You must select **either** a Service or a Package — not both. The backend validator enforces this.
:::

#### Pay Rates

| Field | Description |
|---|---|
| Base Pay | Required. Flat rate paid per completed order. |
| Extra Per Room | Optional. Bonus added per bedroom in the order. |
| Extra Per Bathroom | Optional. Bonus added per bathroom in the order. |
| Distance Rate (per km) | Optional. Reimbursement per kilometer of travel. |

#### Pay Limits

| Field | Description |
|---|---|
| Minimum Pay | Optional. If set, calculated pay is clamped to at least this amount. |
| Maximum Pay | Optional. If set, calculated pay is clamped to at most this amount. |

Leave both as `0` for no limits.

#### Notes

Internal description field for admin reference. Not visible to employees.

## Calculation Formula

The full pay calculation for one service/package on one order:

```
pay = BasePay
    + (ExtraPerRoom × rooms)
    + (ExtraPerBathroom × bathrooms)
    + (DistanceRatePerKm × distance)

if MinimumPay > 0 and pay < MinimumPay:
    pay = MinimumPay

if MaximumPay > 0 and pay > MaximumPay:
    pay = MaximumPay
```

Multiple services/packages on the same order are summed together to produce the final `OrderEmployeePay.TotalPay`.

## API Endpoints

| Method | Endpoint | Purpose |
|---|---|---|
| GET | `/api/AdminPayConfig/get-paged` | List global rates (paginated) |
| GET | `/api/AdminPayConfig/details/{id}` | Single rate detail |
| POST | `/api/AdminPayConfig/create` | Create new rate |
| PUT | `/api/AdminPayConfig/update/{id}` | Update existing rate |
| DELETE | `/api/AdminPayConfig/delete/{id}` | Remove a rate |
| GET | `/api/AdminPayConfig/employee-summary/{employeeId}` | Per-employee config summary |
| POST | `/api/AdminPayConfig/bulk-create-for-employee` | Bulk grade apply (used by employee detail) |

## Workflow Example

**Setting up a new service rate**:

1. Admin creates a new `Service` (e.g., "Deep Clean") via Service Management
2. Admin opens Global Rates → Create
3. Picks the new service, sets currency to CZK
4. Sets `BasePay = 800`, `ExtraPerRoom = 200`, `ExtraPerBathroom = 100`
5. Sets `MinimumPay = 1000` (no employee earns less than 1000 CZK for a Deep Clean)
6. Saves. All employees now use this rate when they complete a Deep Clean order.

**Override for a specific employee**:

1. Admin opens Employee Detail → Pay Configuration
2. Picks "Senior" grade + CZK currency
3. Clicks "Apply to All"
4. The system creates per-employee configs for every service and package, multiplying global base prices by `1.0x`
5. The admin can then manually edit individual rows to give this employee custom rates for specific services
