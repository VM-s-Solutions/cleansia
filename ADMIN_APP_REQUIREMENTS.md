# Cleansia Admin App - Complete Requirements

## Overview

This document outlines the complete requirements for the **Cleansia Admin Application** - a separate web application for administrative management of the Cleansia platform.

**Deployment**: Separate domain (e.g., `admin.cleansia.com`)
**Technology**: Angular 17+ (Nx monorepo)
**Estimated Effort**: 7-11 weeks (2-3 months)

---

## Role-Based Access Control

### User Roles

| Role | Description | Access Level |
|------|-------------|--------------|
| **Super Admin** | Full system access, can manage other admins | 100% |
| **Admin** | Can manage employees, orders, invoices, reports | 90% |
| **Manager** | Can view reports, approve invoices, limited editing | 60% |
| **Support** | Read-only access, can view orders and help customers | 40% |

### Role Permissions Matrix

| Feature | Super Admin | Admin | Manager | Support |
|---------|-------------|-------|---------|---------|
| Manage Admins | ✅ | ❌ | ❌ | ❌ |
| Manage Employees | ✅ | ✅ | ❌ | ❌ |
| Approve Employee Registration | ✅ | ✅ | ✅ | ❌ |
| Manage Orders | ✅ | ✅ | ❌ | View Only |
| Approve Invoices | ✅ | ✅ | ✅ | ❌ |
| Mark Invoice Paid | ✅ | ✅ | ❌ | ❌ |
| Manage Pay Periods | ✅ | ✅ | ❌ | ❌ |
| View Reports | ✅ | ✅ | ✅ | ✅ |
| Export Data | ✅ | ✅ | ✅ | ❌ |
| Manage Services/Packages | ✅ | ✅ | ❌ | ❌ |
| System Configuration | ✅ | ❌ | ❌ | ❌ |

### Implementation

```csharp
// Domain model
public enum AdminRole
{
    SuperAdmin = 1,
    Admin = 2,
    Manager = 3,
    Support = 4
}

public class AdminUser
{
    public string Id { get; set; }
    public string Email { get; set; }
    public string FirstName { get; set; }
    public string LastName { get; set; }
    public AdminRole Role { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public string CreatedBy { get; set; }
}

// Authorization policies
public static class AdminPolicy
{
    public const string SuperAdminOnly = "SuperAdminOnly";
    public const string AdminOrAbove = "AdminOrAbove";
    public const string ManagerOrAbove = "ManagerOrAbove";
    public const string AnyAdmin = "AnyAdmin";
}
```

---

## Phase 1: Core Admin Features (High Priority)

### 1. Employee Management

#### 1.1 Employee List Page

**URL**: `/employees`

**Features**:
- Paginated table of all employees
- Advanced filtering
- Bulk actions
- Export to CSV/Excel

**Table Columns**:
- Employee ID
- Full Name
- Email
- Phone
- ICO
- IBAN
- Contract Status (Pending, Active, Inactive)
- Registration Status (Complete/Incomplete)
- Hire Date
- Is Active
- Actions

**Filters**:
- Status (Active/Inactive)
- Contract Status (Pending/Active/Inactive)
- Registration Completion (Complete/Incomplete)
- Hire Date Range
- Search (name, email, ICO)

**Bulk Actions**:
- Activate selected
- Deactivate selected
- Export selected
- Send email to selected

**Single Actions**:
- View details
- Edit profile
- Approve registration
- Deactivate/Activate
- View invoices
- View orders
- Send email

#### 1.2 Employee Details Page

**URL**: `/employees/:id`

**Tabs**:

**Profile Tab**:
- Personal Info: Name, email, phone, birthdate
- Employment: ICO, IBAN, nationality, passport
- Address: Full address details
- Emergency Contact: Name, phone
- Contract: Status, hire date, termination date
- Actions: Edit, Deactivate, Delete

**Employment Tab**:
- Pay Configuration: View/edit pay rates
- Contract Status: Active/Inactive toggle
- Hire Date: Editable
- Notes: Admin notes about employee

**Documents Tab**:
- Uploaded documents grid
- Document types: Passport, Contract, ID, Other
- Upload new document
- Download/View existing documents
- Delete document

**Orders Tab**:
- List of all orders assigned to this employee
- Filters: Status, date range
- Order count: Total, completed, in-progress

**Invoices Tab**:
- List of all invoices for this employee
- Filters: Status, pay period
- Total earned, total paid, total pending

**Pay History Tab**:
- Chart of earnings over time
- Breakdown by period
- Average pay per order
- Bonuses and deductions history

**Actions Bar**:
- Edit Profile
- Change Pay Config
- Approve Registration
- Deactivate/Activate
- Delete (with confirmation)
- Send Email

#### 1.3 Employee Registration Approval Queue

**URL**: `/employees/pending-approvals`

**Features**:
- List of employees with incomplete registrations
- Profile completion percentage
- Quick approve/reject
- Bulk approve

**Columns**:
- Name
- Email
- Registration Date
- Completion %
- Missing Fields
- Actions

**Actions**:
- View full profile
- Approve (if complete)
- Reject with reason
- Request more info (send email)

#### 1.4 Employee Pay Configuration

**URL**: `/employees/:id/pay-config`

**Form Fields**:
- Service Pay Rate (per service)
- Package Pay Rate (per package)
- Extra Pay Rate (per extra)
- Distance Pay Rate (per km)
- Min Pay Per Order
- Max Pay Per Order
- Bonus Eligibility (yes/no)
- Effective Date (when this config applies)

**Actions**:
- Save
- Reset to Global Default
- View History (previous pay configs)

**Backend API**:
```csharp
POST /api/admin/employees/{id}/pay-config
GET  /api/admin/employees/{id}/pay-config
GET  /api/admin/employees/{id}/pay-config/history
```

---

### 2. Pay Period Management

#### 2.1 Pay Period List

**URL**: `/pay-periods`

**Features**:
- List of all pay periods (past, current, future)
- Status badges (Open, Closed, Paid)
- Quick actions

**Columns**:
- Period Label (e.g., "2025-01")
- Start Date
- End Date
- Status
- Total Employees
- Total Orders
- Total Amount
- Paid Amount
- Unpaid Amount
- Actions

**Filters**:
- Status (Open, Closed, Paid)
- Date Range

**Actions**:
- View Details
- Close Period
- Reopen Period (rare, Super Admin only)
- Create New Period
- Export Summary

#### 2.2 Pay Period Details

**URL**: `/pay-periods/:id`

**Summary Section**:
- Period Label
- Start Date - End Date
- Status Badge
- Total Employees: X
- Total Orders: Y
- Total Amount: Z CZK
- Paid Amount: A CZK
- Unpaid Amount: B CZK
- Created: Date by User
- Closed: Date by User (if closed)

**Tabs**:

**Employees Tab**:
- Table of employees who worked in this period
- Columns: Name, Order Count, Total Pay, Invoice Status
- Click employee → view their orders in this period

**Orders Tab**:
- All orders in this period
- Filters: Employee, status, date
- Shows which orders have been paid, which are pending

**Invoices Tab**:
- All invoices generated for this period
- Filters: Employee, status
- Bulk actions: Approve all, Mark all paid, Export

**Timeline Tab**:
- Period lifecycle events
- Created, Closed, Invoices Generated, Reminders Sent, etc.

**Actions**:
- Close Period (if Open)
- Reopen Period (if Closed, Super Admin only)
- Send Reminder Emails (if Open, near end date)
- Generate Invoices (manual trigger)
- Export Period Summary (PDF/Excel)

#### 2.3 Close Pay Period Workflow

**Trigger**: Click "Close Period" button

**Confirmation Dialog**:
```
Close Pay Period: 2025-01

This will:
✅ Close the period (no new orders can be added)
✅ Generate invoices for 15 employees
✅ Send email notifications with invoice PDFs
✅ Create next pay period (2025-02)

Are you sure you want to proceed?

[ Cancel ]  [ Close Period ]
```

**Process**:
1. Validate period can be closed
2. Show progress indicator
3. Close period in backend
4. Generate invoices (show progress)
5. Send emails (show progress)
6. Create next period
7. Show success message
8. Redirect to closed period details

**Backend API**:
```csharp
POST /api/admin/pay-periods/{id}/close
POST /api/admin/pay-periods/{id}/reopen
POST /api/admin/pay-periods/{id}/send-reminders
GET  /api/admin/pay-periods/{id}/summary
```

---

### 3. Order Management

#### 3.1 Order List (Admin View)

**URL**: `/orders`

**Features**:
- All orders (not just employee's)
- Advanced filtering
- Bulk actions
- Export functionality

**Columns**:
- Order #
- Customer Name
- Customer Email
- Cleaning Date
- Status
- Payment Status
- Payment Type
- Total Amount
- Assigned Employees
- Created Date
- Actions

**Filters**:
- Status (Pending, Confirmed, InProgress, Completed, Cancelled)
- Payment Status (Pending, Paid, Failed, Refunded)
- Payment Type (Card, Cash)
- Date Range (cleaning date)
- Assigned Employee
- Customer Name/Email
- Order Number

**Bulk Actions**:
- Assign Employee
- Cancel Orders
- Export Selected
- Send Emails

**Single Actions**:
- View Details
- Edit Order
- Assign/Unassign Employee
- Cancel Order
- Refund Payment (if Stripe)
- Download Receipt
- Send Email to Customer

#### 3.2 Order Details (Admin View)

**URL**: `/orders/:id`

**Sections**:

**Header**:
- Order Number
- Status Badge
- Payment Status Badge
- Actions: Edit, Cancel, Assign Employee, Refund

**Customer Information** (Editable):
- Name
- Email
- Phone
- Address

**Service Details** (Editable):
- Selected Services
- Selected Packages
- Extras
- Number of Rooms
- Number of Bathrooms
- Cleaning Date/Time
- Estimated Time
- Distance (km)

**Payment Information**:
- Payment Type (Card/Cash)
- Payment Status
- Total Amount
- Currency
- Stripe Session ID (if Card)
- Payment Intent ID (if Card)
- Actions: Refund (if paid via Stripe)

**Employee Assignment**:
- Currently Assigned Employees
- Required Employee Count
- Max Employee Count
- Add Employee (dropdown)
- Remove Employee
- Auto-Assign (find available employees)

**Admin Notes**:
- Text area for admin notes
- Save Notes button

**Status History**:
- Timeline of status changes
- Who changed, when, from what to what

**Photos** (if completed):
- Before/After photo gallery
- View full size
- Download photos

**Receipt** (if paid):
- Download Receipt PDF button
- Receipt Number
- Generated Date

**Actions Bar**:
- Edit Order
- Cancel Order
- Assign Employee
- Refund Payment
- Resend Receipt Email
- Add Note

#### 3.3 Order Assignment Interface

**URL**: `/orders/:id/assign-employees`

**Features**:
- Drag-and-drop interface OR selection-based
- Show available employees
- Show employee capacity/schedule
- Validate assignment

**Layout**:

Left Panel: **Available Employees**
- Filter: By availability, by capacity
- Each employee card shows:
  - Name
  - Current capacity (X/Y orders)
  - Rating (if tracking)
  - Distance from order location

Right Panel: **Assigned to This Order**
- Drag employees here
- Shows: Required (min), Max
- Validation: Cannot assign more than max

**Actions**:
- Auto-Assign: Automatically find available employees
- Save Assignment
- Cancel

**Backend API**:
```csharp
POST /api/admin/orders/{id}/assign-employee
POST /api/admin/orders/{id}/unassign-employee
POST /api/admin/orders/{id}/auto-assign
```

---

### 4. Invoice Management

#### 4.1 Invoice List (Admin View)

**URL**: `/invoices`

**Features**:
- All employee invoices
- Advanced filtering
- Bulk operations
- Export functionality

**Columns**:
- Invoice #
- Variable Symbol
- Employee Name
- Pay Period
- Total Amount
- Status
- Created Date
- Paid Date
- Actions

**Filters**:
- Status (Pending, Approved, Paid, Disputed, Rejected, Cancelled)
- Employee (dropdown)
- Pay Period (dropdown)
- Date Range (created date)
- Amount Range (min-max)

**Bulk Actions**:
- Approve Selected
- Mark Selected as Paid
- Export Selected
- Send Emails

**Single Actions**:
- View Details
- Approve
- Reject
- Mark as Paid
- Dispute
- Cancel
- Download PDF
- Regenerate PDF
- Send Email

#### 4.2 Invoice Details (Admin View)

**URL**: `/invoices/:id`

**Summary Section**:
- Invoice Number
- Variable Symbol
- Employee Name
- Pay Period
- Status Badge
- Total Amount
- Currency

**Breakdown Section**:
- Subtotal (from orders)
- Bonus Amount (editable if Pending)
- Deduction Amount (editable if Pending)
- Total Amount (calculated)

**Order Pay Breakdown**:
- Table of all orders in this invoice
- Columns: Order #, Date, Base Pay, Extras Pay, Expenses Pay, Total
- Click order → view order details

**Admin Actions Panel**:

**If Status = Pending**:
- Edit Bonus Amount (input field)
- Edit Deduction Amount (input field)
- Admin Notes (textarea)
- Actions: Approve, Reject

**If Status = Approved**:
- Mark as Paid:
  - Payment Date (date picker)
  - Payment Amount (input, pre-filled with total)
  - Bank Reference (input)
  - Notes (textarea)
  - Button: Mark as Paid

**If Status = Disputed**:
- Dispute History (list of disputes)
- Resolve Dispute:
  - Resolution (textarea)
  - Button: Resolve Dispute

**If Status = Paid**:
- Payment Details (date, amount, reference)
- View Only

**Always Available**:
- Download PDF
- Regenerate PDF (if generation failed)
- Cancel Invoice (if not Paid)
- Send Email to Employee

**Dispute History Section** (if any):
- Each dispute shows:
  - Reason
  - Submitted by (employee)
  - Submitted date
  - Status (Open/Resolved)
  - Resolution (if resolved)
  - Resolved by
  - Resolved date

#### 4.3 Invoice Approval Workflow

**URL**: `/invoices/approval-queue`

**Features**:
- List of invoices pending approval
- Side-by-side review interface
- Validation checks
- Batch approval

**Interface**:

Left Panel: **Invoice List**
- Pending invoices
- Click to review

Right Panel: **Invoice Details**
- All invoice information
- Editable bonus/deduction
- Validation checks:
  - ✅ All orders in period included
  - ✅ No duplicate payments
  - ✅ Pay amounts match configuration
- Actions: Approve, Reject, Skip

**Batch Actions**:
- Approve All Valid
- Approve Selected

**Backend API**:
```csharp
POST /api/admin/invoices/{id}/approve
POST /api/admin/invoices/{id}/reject
POST /api/admin/invoices/bulk-approve
GET  /api/admin/invoices/pending-approval
```

#### 4.4 Invoice Dispute Management

**URL**: `/invoices/disputes`

**Features**:
- List of all disputed invoices
- Dispute details
- Resolution workflow

**Columns**:
- Invoice #
- Employee
- Dispute Reason
- Submitted Date
- Status (Open/Resolved)
- Actions

**Actions**:
- View Details
- Resolve Dispute
- View Related Orders

**Resolve Dispute Dialog**:
- Show dispute reason
- Show pay calculation breakdown
- Resolution textarea
- Actions: Approve with Changes, Reject Dispute

---

### 5. Pay Configuration Management

#### 5.1 Global Pay Configuration

**URL**: `/settings/pay-config`

**Form**:

**Default Rates** (applies to all new employees):
- Service Pay Rate: X CZK per service
- Package Pay Rate: Y CZK per package
- Extra Pay Rate: Z CZK per extra
- Distance Pay Rate: W CZK per km
- Min Pay Per Order: A CZK
- Max Pay Per Order: B CZK

**Service-Specific Rates**:
- Table of services with individual pay rates
- Override global rate for specific services

**Package-Specific Rates**:
- Table of packages with individual pay rates
- Override global rate for specific packages

**Extra-Specific Rates**:
- Table of extras with individual pay rates

**Actions**:
- Save Configuration
- Reset to Defaults
- View History

#### 5.2 Employee-Specific Overrides

**URL**: `/settings/pay-config/overrides`

**List**:
- Employees who have custom pay configs
- Show: Name, Effective Date, Different From Global
- Actions: Edit, Remove Override

**Backend API**:
```csharp
GET  /api/admin/pay-config/global
POST /api/admin/pay-config/global
GET  /api/admin/pay-config/employee/{id}
POST /api/admin/pay-config/employee/{id}
GET  /api/admin/pay-config/history
```

---

## Phase 2: Financial & Reporting (Medium Priority)

### 6. Financial Reports

#### 6.1 Revenue Reports

**URL**: `/reports/revenue`

**Metrics**:
- Total Revenue (selected period)
- Revenue by Service Type (pie chart)
- Revenue by Payment Type (Card vs Cash)
- Average Order Value
- Revenue Trend (line chart over time)

**Filters**:
- Date Range
- Service Type
- Payment Type

**Export**: CSV, Excel, PDF

#### 6.2 Payroll Reports

**URL**: `/reports/payroll`

**Metrics**:
- Total Payroll (selected period)
- Payroll by Employee (bar chart)
- Average Pay Per Order
- Payroll Trend (line chart)
- Bonuses vs Deductions

**Filters**:
- Date Range
- Employee
- Pay Period

**Export**: CSV, Excel, PDF (for accounting)

#### 6.3 Tax Reports

**URL**: `/reports/taxes`

**Features**:
- Annual earnings per employee
- VAT summary (if applicable)
- Expense breakdown
- Downloadable tax documents

**Export**: PDF for tax filing

#### 6.4 Profit/Loss Report

**URL**: `/reports/profit-loss`

**Metrics**:
- Income (revenue from orders)
- Expenses (employee pay + overhead)
- Profit Margin (calculated)
- Trend Comparison (monthly/quarterly)

**Filters**:
- Date Range
- Comparison Period

**Export**: PDF, Excel

---

### 7. Service & Package Management

#### 7.1 Service Catalog

**URL**: `/services`

**List**:
- Table of all cleaning services
- Columns: Name, Description, Base Price, Per-Room Price, Per-Bathroom Price, Active, Actions

**Actions**:
- Create New Service
- Edit Service
- Deactivate Service
- Delete Service (if no orders)

**Create/Edit Form**:
- Name (CS/EN)
- Description (CS/EN)
- Base Price
- Per-Room Price
- Per-Bathroom Price
- Is Active (toggle)

#### 7.2 Package Catalog

**URL**: `/packages`

**List**:
- Table of all service packages
- Columns: Name, Description, Price, Included Services, Active, Actions

**Actions**:
- Create New Package
- Edit Package
- Deactivate Package
- Delete Package (if no orders)

**Create/Edit Form**:
- Name (CS/EN)
- Description (CS/EN)
- Price
- Included Services (multi-select)
- Is Active (toggle)

**Backend API**:
```csharp
GET    /api/admin/services
POST   /api/admin/services
PUT    /api/admin/services/{id}
DELETE /api/admin/services/{id}

GET    /api/admin/packages
POST   /api/admin/packages
PUT    /api/admin/packages/{id}
DELETE /api/admin/packages/{id}
```

---

## Phase 3: System Configuration (Medium Priority)

### 8. Company Information

**URL**: `/settings/company`

**Form**:
- Company Name
- Address (Street, City, Postal Code, Country)
- ICO (Company ID)
- DIC (VAT ID)
- Bank Account (IBAN)
- Contact Email
- Contact Phone
- Logo Upload (for invoices/receipts)

**Actions**:
- Save
- Upload New Logo
- Preview (how it appears on invoices)

### 9. Email Template Management

**URL**: `/settings/email-templates`

**List**:
- All email templates
- Columns: Name, Type, Language, Last Updated, Actions

**Edit Template**:
- Template HTML editor (rich text OR code)
- Available Variables (list with descriptions)
- Preview with Sample Data
- Send Test Email

**Templates**:
1. Confirmation Email (CS/EN)
2. Password Reset (CS/EN)
3. Order Receipt (CS/EN)
4. Period Closed (CS/EN)
5. Period End Reminder (CS/EN)

### 10. Invoice Template Management

**URL**: `/settings/invoice-templates`

**List**:
- Invoice templates by country/language
- Columns: Country, Language, Active, Last Updated, Actions

**Upload New Template**:
- Upload HTML file
- Select Country
- Select Language
- Set as Active

**Edit Template**:
- HTML editor
- Preview (generate sample invoice PDF)

**Actions**:
- Activate/Deactivate
- Download Template
- Preview

### 11. System Settings

**URL**: `/settings/system`

**Sections**:

**Countries**:
- Manage supported countries
- Add/Edit/Remove

**Currencies**:
- Manage supported currencies
- Exchange rates
- Default currency

**Languages**:
- Manage supported languages
- Default language

**Invoice Configurations**:
- Per-country settings
- VAT requirements
- Legal disclaimers

---

## Phase 4: Advanced Features (Low Priority)

### 12. Admin User Management

**URL**: `/settings/admin-users`

**Features**:
- List of all admin users
- Role assignment
- Create/Edit/Deactivate admins

**Columns**:
- Name
- Email
- Role
- Active
- Last Login
- Created Date
- Actions

**Create/Edit Admin**:
- Email
- First Name
- Last Name
- Role (Super Admin, Admin, Manager, Support)
- Is Active
- Send Welcome Email (checkbox)

**Actions**:
- Create New Admin
- Edit Admin
- Deactivate Admin
- Reset Password
- View Activity Log

### 13. Background Jobs Management

**URL**: `/system/jobs`

**Features**:
- Embed Hangfire dashboard
- View recurring jobs
- View failed jobs
- Manual job triggering

**Job List**:
- Close Expired Pay Periods (Daily 2 AM UTC)
- Send Period End Reminders (Daily 9 AM UTC)

**Actions**:
- View Job Details
- Manually Trigger Job
- Pause/Resume Job
- View Execution History
- Retry Failed Job

---

## Technical Architecture

### Frontend Structure

```
apps/cleansia-admin.app/
├── src/
│   └── app/
│       ├── app.routes.ts
│       ├── app.component.ts
│       └── pages/
│           ├── dashboard/
│           ├── employees/
│           ├── pay-periods/
│           ├── orders/
│           ├── invoices/
│           ├── reports/
│           └── settings/

libs/cleansia-admin-features/
├── employee-management/
│   ├── employee-list/
│   ├── employee-details/
│   ├── employee-pay-config/
│   └── registration-approvals/
├── pay-period-management/
│   ├── period-list/
│   ├── period-details/
│   └── close-period/
├── order-management/
│   ├── order-list/
│   ├── order-details/
│   └── order-assignment/
├── invoice-management/
│   ├── invoice-list/
│   ├── invoice-details/
│   ├── invoice-approval/
│   └── invoice-disputes/
├── financial-reports/
│   ├── revenue-report/
│   ├── payroll-report/
│   ├── tax-report/
│   └── profit-loss-report/
└── system-configuration/
    ├── company-info/
    ├── email-templates/
    ├── invoice-templates/
    └── admin-users/
```

### Backend API Extensions

**New Controllers**:
- `AdminEmployeeController`
- `AdminOrderController`
- `AdminInvoiceController`
- `AdminPayPeriodController`
- `AdminPayConfigController`
- `AdminServiceController`
- `AdminReportController`
- `AdminSettingsController`
- `AdminUserController`

### Shared Components

**Data Tables**:
- PrimeNG `p-table` with pagination, sorting, filtering
- Responsive design
- Export functionality

**Charts**:
- Chart.js or PrimeNG charts
- Line, bar, pie, doughnut charts

**Forms**:
- Reactive forms with validation
- Rich text editor (for templates)
- File upload component
- Date range picker

**Export Functionality**:
- CSV export (simple data)
- Excel export (EPPlus library)
- PDF export (existing PDF service)

---

## Deployment

### Hosting
- **Separate Domain**: `admin.cleansia.com`
- **Same Infrastructure**: Azure/AWS alongside main app
- **Separate Build**: `npm run build:cleansia-admin`

### Security
- **HTTPS Only**: Enforce SSL
- **IP Whitelist** (Optional): Restrict to office IPs
- **2FA** (Future): Two-factor authentication for admins
- **Audit Logging**: Log all admin actions

### CI/CD
- Separate pipeline from partner app
- Deploy to admin subdomain
- Independent versioning

---

## Priority Summary

### Must Have (Phase 1) - 3-4 weeks
1. Employee Management (list, details, approval)
2. Pay Period Management (list, details, close)
3. Order Management (list, details, assignment)
4. Invoice Management (list, details, approval)
5. Pay Configuration

### Should Have (Phase 2) - 1-2 weeks
6. Financial Reports
7. Service/Package Management
8. Export Functionality

### Nice to Have (Phase 3) - 1-2 weeks
9. Company Information
10. Email Templates
11. Invoice Templates
12. System Settings

### Future (Phase 4) - 2-3 weeks
13. Admin User Management
14. Audit Log
15. Background Jobs UI
16. Advanced Analytics

---

## Next Steps

1. **Partner App Fixes**: Complete first (2-3 weeks)
2. **Admin App Design**: UI/UX design while partner fixes in progress
3. **Admin App Phase 1**: Start development after partner app complete
4. **Iterative Development**: Build phase by phase, test with real users

---

**Document Version**: 1.2
**Last Updated**: 2026-01-02
**Status**: Phase 1 IN PROGRESS - Core features partially implemented

---

## Updates from Partner App Implementation

### Already Implemented Backend Features

The following backend features are **already complete** and ready for the admin UI:

✅ **Invoice Cancellation** (NEW - 2025-12-20):
- `PUT /api/employeepayroll/CancelInvoice` endpoint
- Admin can cancel invoices with reason tracking
- Business rules: Cannot cancel paid or already-cancelled invoices
- Tracks: `IsCancelled`, `CancellationReason`, `CancelledAt`, `CancelledBy`

✅ **Order Photo Management** (NEW - 2025-12-20):
- `POST /api/order/UploadOrderPhoto` - Upload photos
- `GET /api/order/GetOrderPhotos/{orderId}` - Get all photos
- `DELETE /api/order/DeleteOrderPhoto/{photoId}` - Delete photo
- `GET /api/order/DownloadOrderReceipt/{orderId}` - Download receipt
- PhotoType enum: BeforeService, AfterService

✅ **Dispute Management Backend** (NEW - 2025-12-20):
- Complete CQRS handlers for all dispute operations
- DisputeController with HandleResult pattern
- Database seed data (8 sample disputes)
- Permission system (CustomerOnly, AdminOnly)
- **Ready for UI implementation in Admin App**

✅ **Order Search & Filters** (NEW - 2025-12-20):
- Comprehensive OrderFilter model
- Filter by: name, email, phone, status, dates, prices
- GetPagedOrders handler with full filter support

✅ **Health Monitoring** (NEW - 2025-12-20):
- `GET /api/health` endpoint
- Checks: Database, Blob Storage, SendGrid, Stripe
- Returns 200 OK (healthy) or 503 (unhealthy)
- **Admin can integrate this into system monitoring dashboard**

✅ **Request Logging** (NEW - 2025-12-20):
- All HTTP requests logged with structured logging
- Tracks: method, path, user, IP, duration, status code
- **Admin can view logs for troubleshooting**

✅ **Strong Password Policy**:
- 12+ characters, complexity requirements
- Backend validation in BaseAuthValidator
- **Admin should use same policy**

✅ **Profile Completion Validation**:
- RegistrationCompletionService implemented
- Employee status tracking
- **Admin can see completion status in employee list**

### Backend Features That Need Admin UI

These features exist in the backend but need admin interface:

**Priority 1 - Add to Phase 1:**

1. **Invoice Cancellation UI**:
   - Add "Cancel Invoice" button in invoice details page
   - Show cancellation dialog with reason input
   - Display cancelled status and reason in invoice list
   - Show who cancelled and when

2. **Order Photo Gallery**:
   - Display before/after photos in order details
   - Allow download of individual photos
   - Show photo upload date and type
   - Option to delete photos

3. **Dispute Management UI** (IMPORTANT):
   - List all disputes (customer-submitted)
   - View dispute details with evidence
   - Update dispute status (Open, InReview, Resolved, Closed, Escalated)
   - Add resolution notes
   - Track dispute history

4. **Order Search & Advanced Filters**:
   - Use existing OrderFilter backend
   - Add filter panel to order list page
   - Support all filter types already in backend

**Priority 2 - Add to Phase 2:**

5. **System Health Dashboard**:
   - Display health check status from `/api/health`
   - Show green/red indicators for each service
   - Alert when services are unhealthy
   - Historical uptime tracking

6. **Request Logs Viewer** (Optional):
   - View recent HTTP request logs
   - Filter by user, path, status code, date
   - Useful for troubleshooting issues

### Updated Phase 1 Requirements

**Phase 1: Core Admin Features** should now include:

1. Employee Management ✓ (original)
2. Pay Period Management ✓ (original)
3. Order Management ✓ (original)
   - **+** Advanced search filters (backend ready)
   - **+** Photo gallery view (backend ready)
4. Invoice Management ✓ (original)
   - **+** Invoice cancellation (backend ready)
5. Pay Configuration ✓ (original)
6. **Dispute Management (NEW - backend ready)**
   - Dispute list with filters
   - Dispute details view
   - Status update workflow
   - Resolution tracking

### Updated Phase 2 Requirements

**Phase 2: Financial & Reporting** should now include:

1. Financial Reports ✓ (original)
2. Service/Package Management ✓ (original)
3. Export Functionality ✓ (original)
4. **System Health Monitoring (NEW)**
   - Health status dashboard
   - Service status indicators
   - Uptime tracking

### Notes for Implementation

**When Building Admin App:**

1. **Reuse Partner App Components**:
   - `cleansia-button` component (already standardized)
   - Sidebar menu structure (mobile-responsive)
   - Form validation patterns
   - API client services

2. **Backend APIs Already Available**:
   - All order endpoints support admin access
   - Invoice endpoints include cancellation
   - Dispute endpoints complete and ready
   - Photo management endpoints ready
   - Health check endpoint ready

3. **Permission System**:
   - Use existing Policy.cs for permissions
   - Add new AdminOnly permissions as needed
   - CustomerOnly already implemented for disputes

4. **Mobile Responsiveness**:
   - Admin app should also be mobile-responsive
   - Reuse sidebar patterns from partner app
   - Touch-friendly for tablet use

### Recommended Priority Adjustments

**Original Phase 1** (3-4 weeks):
- Employee Management
- Pay Period Management
- Order Management
- Invoice Management
- Pay Configuration

**Updated Phase 1** (3-4 weeks):
- All original Phase 1 features
- **+ Invoice Cancellation UI** (1 day)
- **+ Order Photo Gallery** (1 day)
- **+ Dispute Management UI** (2-3 days)
- **+ Advanced Order Filters** (1 day)

**Updated Phase 2** (1-2 weeks):
- All original Phase 2 features
- **+ System Health Dashboard** (1 day)

---

## Implementation Progress (Updated 2026-01-03)

### ✅ COMPLETED

#### Infrastructure & Setup
- [x] **Admin App Angular Application** - `apps/cleansia-admin.app/`
- [x] **Admin API Backend** - `Cleansia.Web.Admin` project
- [x] **Admin Client Service** - NSwag-generated `AdminClient` with all endpoints
- [x] **Admin Authentication** - Separate JWT auth for admin users
- [x] **Admin Guard** - Route protection for admin pages
- [x] **Routing Configuration** - Lazy-loaded routes for all features
- [x] **i18n Support** - Czech and English translations
- [x] **Admin NgRx Stores** - `libs/data-access/admin-stores/`

#### Authentication & Login
- [x] **Admin Login Page** (`/login`)
  - Email/password authentication
  - JWT token storage
  - Redirect to dashboard after login
- [x] **Unauthorized Page** - Access denied handling

#### 1. Employee Management (Complete)
- [x] **Employee List Page** (`/employee-management`)
  - Paginated table with all employees
  - Filters: Status, Contract Status, Search
  - Actions: View Details, Approve, Reject
  - Status badges with color coding
  - Sorting support
- [x] **Employee Detail Page** (`/employee-management/:employeeId`)
  - Personal information section
  - Employment details section
  - Documents section with approval/rejection
  - Contract status display
  - Day of week availability display
  - Back navigation
- [x] **Employee Approval/Rejection**
  - Approve employee with single click
  - Reject employee with reason dialog
  - Backend: `ApproveEmployee`, `RejectEmployee` commands
- [x] **Document Management**
  - View employee documents
  - Approve/Reject documents with dialog
- [x] **Reject Dialog Component** - Reusable dialog for rejection reasons

#### 2. Pay Period Management (Complete)
- [x] **Pay Period List Page** (`/pay-periods`)
  - Paginated table with all pay periods
  - Filters: Status, Year
  - Actions: View Details, Close Period
  - Status badges (Open, Closed, Paid)
  - Sorting support
- [x] **Pay Period Detail Page** (`/pay-periods/:id`)
  - Period summary information
  - Status display with badges
  - Date range display
  - Total employees, orders, amount info
  - Close period action
- [x] **Close Period Workflow**
  - Confirmation dialog
  - Backend integration

#### 3. Order Management (Complete)
- [x] **Order List Page** (`/order-management`)
  - Paginated table with all orders
  - Multi-select filters: Order Status, Payment Status
  - Search filter (name, email, phone)
  - Date range filter (cleaning date)
  - Custom status badge templates
  - Sorting support
- [x] **Order Detail Page** (`/order-management/:orderId`)
  - Customer information section
  - Service details section
  - Payment information section
  - Status history timeline with icons
  - Back navigation

#### 4. Invoice Management (Complete)
- [x] **Invoice List Page** (`/invoice-management`)
  - Paginated table with all invoices
  - Filters: Status, Employee, Pay Period, Date Range
  - Status badges (Pending, Approved, Paid, Disputed, Rejected, Cancelled)
  - Actions: View Details
- [x] **Invoice Detail Page** (`/invoice-management/:invoiceId`)
  - Full invoice information display
  - Status banner with colored badges
  - Invoice summary section
  - Financial summary (subtotal, bonus, deductions, total)
  - Approval & payment info section
  - Order pays breakdown table
  - Admin notes display
- [x] **Invoice Actions**
  - ✅ Approve Invoice - Single click approval
  - ✅ Mark as Paid - Mark approved invoices as paid
  - ✅ Cancel Invoice - Cancel with reason dialog
  - ✅ Download PDF - Download invoice document
  - ✅ Regenerate PDF - Regenerate with current language
- [x] **Invoice Status Workflow**
  - Pending → Approve → Approved
  - Approved → Mark Paid → Paid
  - Pending/Approved → Cancel → Cancelled
- [x] **Backend Integration**
  - `ApproveInvoiceCommand`
  - `MarkInvoicePaidCommand`
  - `CancelInvoiceCommand` (with reason & cancelledBy)
  - `RegenerateInvoicePdfCommand` (with languageCode)
  - Invoice details endpoint

#### Shared Components Used
- [x] `CleansiaTableComponent` - Data tables with pagination & sorting
- [x] `CleansiaButtonComponent` - Consistent button styling
- [x] `CleansiaSectionComponent` - Content sections
- [x] `CleansiaTitleComponent` - Page titles
- [x] `CleansiaLoaderComponent` - Loading states
- [x] `CleansiaLanguageSwitcherComponent` - Language toggle
- [x] `CleansiaSelectComponent` - Dropdown selects
- [x] `CleansiaMultiselectComponent` - Multi-select filters
- [x] `CleansiaTextInputComponent` - Text input fields
- [x] `RejectDialogComponent` - Rejection reason input

### 🔜 NEXT PRIORITY (Immediate)

#### Order Photo Gallery View
- [x] Photo gallery in Order Detail page
- [x] Before/After photo display
- [x] Photo download functionality
- [x] Photo zoom/lightbox view

#### Reports
- [ ] Revenue Reports (`/reports/revenue`)
  - Total revenue by period
  - Revenue by service type
  - Revenue trends chart
- [ ] Payroll Reports (`/reports/payroll`)
  - Total payroll by period
  - Payroll by employee
  - Payroll trends chart

#### Service & Package Catalog
- [ ] Service Catalog (`/services`)
  - List all cleaning services
  - Create/Edit/Delete services
  - Set pricing (base, per-room, per-bathroom)
  - Activate/Deactivate services
- [ ] Package Catalog (`/packages`)
  - List all service packages
  - Create/Edit/Delete packages
  - Set included services
  - Activate/Deactivate packages

#### Admin User Management
- [ ] Admin User List (`/settings/admin-users`)
  - List all admin users
  - Role assignment (Super Admin, Admin, Manager, Support)
  - Active/Inactive status
- [ ] Create/Edit Admin User
  - Email, name, role
  - Password reset
  - Deactivate admin

#### System Settings
- [ ] Countries Management (`/settings/countries`)
  - List supported countries
  - Add/Edit/Remove countries
- [ ] Currencies Management (`/settings/currencies`)
  - List supported currencies
  - Exchange rates configuration
  - Set default currency
- [ ] Languages Management (`/settings/languages`)
  - List supported languages
  - Set default language
  - Enable/Disable languages

### ⏸️ DEFERRED (Implement Later)

#### Pay Configuration Management
- [ ] Global Pay Configuration (`/settings/pay-config`)
- [ ] Employee-Specific Overrides
- [ ] Pay Rate History
*Reason: Can be implemented later*

#### Dispute Management
- [ ] Dispute List (`/disputes`)
- [ ] Dispute Details
- [ ] Status Update Workflow
- [ ] Resolution Tracking
*Reason: Can be implemented later*

#### Employee Bulk Actions
- [ ] Activate selected employees
- [ ] Deactivate selected employees
- [ ] Export selected to CSV/Excel
- [ ] Send email to selected
*Reason: Nice to have, can be implemented later*

### 📝 NOT LISTED (Missing from Original Requirements)

#### Company Information Management
- [ ] Company profile settings (`/settings/company`)
  - Company name, address, ICO, DIC
  - Bank account details (IBAN)
  - Contact information
  - Logo upload

#### Template Management
- [ ] Invoice Templates (`/settings/templates/invoices`)
  - Upload/Edit invoice HTML templates
  - Preview with sample data
  - Multi-language support
- [ ] Order Receipt Templates (`/settings/templates/receipts`)
  - Upload/Edit receipt templates
  - Preview functionality
- [ ] Email Templates (`/settings/templates/emails`)
  - Edit email templates (confirmation, password reset, etc.)
  - Available variables documentation
  - Send test emails

#### Translation Management
- [ ] Email Translations (`/settings/translations/emails`)
  - Manage email text in CS/EN
- [ ] Application Translations (`/settings/translations/app`)
  - Manage UI text translations
  - Add new translation keys

#### Internationalization (i18n) Management
- [ ] Language Configuration (`/settings/languages`)
  - Add/Remove supported languages
  - Set default language
  - Enable/Disable languages

### Phase 2 Features (Not Started)
- [ ] Tax Reports
- [ ] Profit/Loss Report
- [ ] System Health Dashboard
- [ ] Background Jobs Management (Hangfire UI)
- [ ] Audit Log
- [ ] Advanced Analytics

---

## Summary

| Feature Area | Status | Completion |
|--------------|--------|------------|
| Infrastructure & Setup | ✅ Complete | 100% |
| Employee Management | ✅ Complete | 100% |
| Pay Period Management | ✅ Complete | 100% |
| Order Management | ✅ Complete | 100% |
| Invoice Management | ✅ Complete | 100% |
| Order Photo Gallery | ✅ Complete | 100% |
| Reports (Revenue, Payroll) | 🔜 Next Priority | 0% |
| Service/Package Catalog | 🔜 Next Priority | 0% |
| Admin User Management | 🔜 Next Priority | 0% |
| System Settings | 🔜 Next Priority | 0% |
| Pay Configuration | ⏸️ Deferred | 0% |
| Dispute Management | ⏸️ Deferred | 0% |
| Employee Bulk Actions | ⏸️ Deferred | 0% |
| Company Information | 📝 Not Listed | 0% |
| Template Management | 📝 Not Listed | 0% |
| Translation Management | 📝 Not Listed | 0% |
| **Phase 1 Overall** | **In Progress** | **~90%** |

### Next Priority Tasks (Updated 2026-01-03)
1. ✅ ~~Employee Management~~ - Complete
2. ✅ ~~Pay Period Management~~ - Complete
3. ✅ ~~Order Management~~ - Complete
4. ✅ ~~Invoice Management~~ - Complete
5. ✅ ~~Order Photo Gallery View~~ - Complete
6. 🔜 **Reports (Revenue, Payroll)** - Next
7. 🔜 **Service/Package Catalog** - Next
8. 🔜 **Admin User Management** - Next
9. 🔜 **System Settings (Countries, Currencies, Languages)** - Next
