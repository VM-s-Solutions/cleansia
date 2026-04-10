# Employee Pay System — How It Works

> A complete explanation of the Cleansia employee pay configuration, calculation, invoicing, and payment system.

## TL;DR

The system has **4 stages** and **3 actors** (Admin, Background Job, Employee):

```
  CONFIGURE           CALCULATE             INVOICE              PAY
  ─────────           ─────────             ───────              ───
  EmployeePayConfig   OrderEmployeePay      EmployeeInvoice      Status: Paid
  (per service        (per completed         (per pay period      (no actual
  /package)           order)                per employee)         bank transfer)
```

**Key insight**: There is **no real payment integration**. The admin manually marks invoices as "Paid" after performing the bank transfer outside the system. The system is a **record-keeping tool**, not a payment processor.

---

## The Four Entities

### 1. `EmployeePayConfig` — The Rate Card

Stores **how much an employee earns** for doing a specific service or package.

**Scope**: Can be **global** (for all employees) OR **employee-specific** (an override for a single employee).

**Fields**:
```
EmployeeId (optional)   — null = global rate, set = per-employee override
ServiceId OR PackageId  — mutually exclusive, one required
BasePay                 — flat amount per job
ExtraPerRoom            — bonus per bedroom
ExtraPerBathroom        — bonus per bathroom
DistanceRatePerKm       — travel cost reimbursement
MinimumPay / MaximumPay — clamp total to a range
CurrencyId              — CZK, EUR, etc.
```

**Calculation formula** (`EmployeePayConfig.CalculatePay`):
```
pay = BasePay + (ExtraPerRoom × rooms) + (ExtraPerBathroom × bathrooms) + (DistanceRatePerKm × distance)
pay = clamp(pay, MinimumPay, MaximumPay)
```

### 2. `OrderEmployeePay` — The Pay Slip

One record per **(order, employee)** pair. Created when an order is completed and the admin triggers pay calculation for the cleaner.

**Fields**:
```
OrderId           — which order this pay is for
EmployeeId        — which cleaner
PayPeriodId       — which pay period it falls into (the active one)
BasePay           — from EmployeePayConfig
ExtrasPay         — from additional order extras
ExpensesPay       — distance × rate
BonusPay          — optional manual bonus
DeductionPay      — optional manual deduction
TotalPay          — Base + Extras + Expenses + Bonus - Deduction (min 0)
IsApproved        — locked once approved
EmployeeInvoiceId — null until assigned to invoice
```

### 3. `PayPeriod` — The Billing Cycle

A time window (typically bi-weekly) during which OrderEmployeePay records accumulate.

**Lifecycle**:
```
  Open → Closed → Paid
```

- **Open**: Accepting new pay calculations. One period should be open at a time.
- **Closed**: Locked. No new calculations. Invoices are generated.
- **Paid**: All invoices for this period are marked paid. Period is archived.

**Validation**: A period can only be closed if **all** its invoices are `Paid`.

### 4. `EmployeeInvoice` — The Bill to Pay

Generated at period close. Aggregates all `OrderEmployeePay` records for one employee in one pay period.

**Lifecycle**:
```
  Pending → Approved → Paid
       ↘ Disputed ↗
       ↘ Rejected
       ↘ Cancelled
```

**Fields**:
```
EmployeeId, PayPeriodId
InvoiceNumber       — auto-generated
VariableSymbol      — Czech bank transfer reference
SubTotal            — Sum(OrderEmployeePay.TotalPay)
BonusAmount         — manual override
DeductionAmount     — manual override
TotalAmount         — SubTotal + BonusAmount - DeductionAmount
CurrencyId
Status              — Pending / Approved / Paid / Disputed / Rejected / Cancelled
PdfBlobUrl          — generated PDF in Azure Blob
BankTransferNote    — reference once admin pays
ApprovedAt/By
PaidAt
```

---

## The Full Flow (Step by Step)

### Phase 1: Setup (One-Time, Per Employee or Globally)

The admin creates `EmployeePayConfig` records:

**Option A: Global rates** (`EmployeeId = null`) — applies to all employees by default.
**Option B: Per-employee overrides** (`EmployeeId = <id>`) — overrides the global rate for that specific employee.

The **"Apply Grade Template"** feature on the employee detail page does this in bulk: it takes a grade (Junior/Medior/Senior), applies a multiplier (0.5x / 0.75x / 1.0x) to the service's base price, and creates per-employee configs for ALL services and packages in one click.

### Phase 2: Order Completion → Pay Calculation

When a cleaner completes an order:

1. Admin (or an automated trigger) calls `POST /api/EmployeePayroll/CalculateOrderPay` with `{ OrderId, EmployeeId }`.
2. The handler:
   - Finds the **currently open** `PayPeriod`.
   - Loads the employee's `EmployeePayConfig` for each service/package in the order (falls back to global if no employee-specific override exists).
   - Sums the calculated pay across all services/packages.
   - Creates an `OrderEmployeePay` record with `PayPeriodId = currentOpenPeriod.Id` and `EmployeeInvoiceId = null`.

At this point, the cleaner has "earned" money but has no invoice yet.

### Phase 3: Pay Period Closes → Invoices Generated

Two ways this happens:

**A. Automatic (nightly background job)** — `PayPeriodTimerFunction.cs`:
- Runs daily at 02:00 UTC.
- Finds Open periods where `EndDate < Today`.
- Closes each expired period.
- **For each employee with unpaid `OrderEmployeePay` records in that period**:
  - Creates an `EmployeeInvoice` aggregating all their pays.
  - Assigns each `OrderEmployeePay` to the new invoice.
  - Generates a PDF invoice and uploads it to Azure Blob.
  - Emails the PDF to the employee.
- If no new Open period exists, creates the next one automatically.

**B. Manual** — `ClosePayPeriod` command + `GenerateInvoice` command:
- Admin clicks "Close Period" on the pay period management page.
- Admin manually generates each invoice via API.

### Phase 4: Admin Approves + Pays

For each generated invoice, the admin goes through:

1. **Review**: Opens invoice detail, checks line items (individual OrderEmployeePay records).
2. **Approve**: `PUT /api/AdminInvoice/approve` — status `Pending → Approved`.
3. **Pay**: Admin performs a **real bank transfer** outside the system using the invoice's `VariableSymbol` and `TotalAmount`.
4. **Mark Paid**: `PUT /api/AdminInvoice/mark-paid` with the bank transfer reference — status `Approved → Paid`.

Once ALL invoices in a period are `Paid`, the period itself can be marked `Paid`.

---

## Critical Points

### ⚠️ No Real Payment Integration

The word "payment" in this system means **"admin clicks a button after doing a real bank transfer"**. There is:

- ❌ No Stripe Connect for paying cleaners
- ❌ No bank API integration
- ❌ No automated payouts
- ✅ PDF invoice generation
- ✅ Email delivery to employees
- ✅ Status tracking

The system exists to **track what was owed and what was paid** — the actual money movement happens in the admin's bank app.

### ⚠️ "Grade Template" Is For Employee Setup, Not Pay Configs

The grade multiplier feature should NOT exist on the generic pay config create/edit page (the one accessible via sidebar → Pay Configs). It only makes sense when **onboarding an employee**.

The legacy pay-config-management form has a grade multiplier field left over from before the bulk employee setup was implemented. It should be **removed** from that form, because:

- Global pay configs don't have a grade — they ARE the base rate
- Per-employee configs are created via the new bulk API in employee detail, not this form
- Having the multiplier here causes confusion

### ⚠️ Two Parallel Config Systems Exist

You noticed this and you're right:

1. **Sidebar → Pay Configs** (`/pay-config-management`) — the old manual form. Creates one config at a time. Can target a service OR package, with or without an employee. Has the confusing grade multiplier.
2. **Employee detail → Pay Configuration section** — the new bulk approach. One click creates configs for ALL services + packages using a grade template.

**Recommendation**: The old page should become **read-only view of global rates**, and editing should happen via:
- Global rates: service/package detail pages (since they're tied to the service)
- Employee overrides: employee detail page only (with the bulk grade apply)

---

## How This Maps to What's Broken

| Issue you reported | Root cause | Fix |
|---|---|---|
| Pay config page layout corrupted | Missing `page-wrapper` wrapper pattern used by other admin pages | Wrap content in standard container |
| Create/edit layout is cramped | Custom `.form-grid` / `.form-field` CSS that doesn't match the standard `info-grid` / `info-item` | Use the standard layout (already done in last session) |
| Grade multiplier on create/edit page is confusing | This field was added before the bulk employee setup existed. It applies a multiplier to the current form values, which doesn't make sense for a single global config | **Remove the grade multiplier section entirely from this form**. Grade templates only belong on the employee detail page. |
| Two systems doing similar things | Historical: bulk employee setup was added on top of the old single-config form without removing the old approach | Deprecate the old form as "manage global rates", restrict edit to via service/package pages |

---

## Recommended Next Steps

1. **Remove** the Grade Template section from `pay-config-form.component.html` (the form accessed via sidebar).
2. **Fix** the layout of `pay-config-management.component.html` (the list page) to use the standard `page-wrapper` structure like other admin pages.
3. **Relabel** the sidebar link from "Pay Configs" to "Global Rates" — clearer that it's for platform-wide defaults.
4. **Document** in the admin UI that per-employee rates are managed on the employee detail page.
5. **Consider**: eventually moving global rate editing into the Service/Package detail pages, and removing the dedicated pay-config-management area entirely.
