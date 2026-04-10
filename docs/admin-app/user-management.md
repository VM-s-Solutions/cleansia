# Employee Management

The employee management feature is the primary tool for administrators to oversee partner (employee) accounts, review applications, manage documents, and control access. It is implemented in the `@cleansia/admin-features/employee-management` library.

## Architecture

- `EmployeeManagementFacade` -- Employee list management with pagination and filtering
- `EmployeeDetailFacade` -- Individual employee detail, document review, approval/rejection
- `RejectDialogComponent` -- Shared dialog for providing rejection reasons

## Employee List

Route: `/employee-management`

The employee list page displays all registered partners/employees with:

- Name and contact information
- Contract status (Pending, Approved, Rejected)
- Profile completion status
- Registration date
- Filtering and sorting capabilities
- Pagination (server-side)

Clicking an employee row navigates to the detail page.

## Employee Detail

Route: `/employee-management/:id`

The detail page provides comprehensive information about a partner and tools for managing their account.

### Sections

| Section | Content |
|---|---|
| Personal Info | Name, email, phone, date of birth |
| Address | Street, city, zip code, country |
| Employment | Contract status, employment details |
| Emergency Contact | Emergency contact name, phone, relationship |
| Contract Status | Current status with approval/rejection actions |
| Profile Completion | Whether all required fields are filled |
| Availability | Weekly availability schedule with edit capability |
| Pay Configuration | Per-employee rate overrides with bulk grade apply |
| Documents | Uploaded documents with review workflow |

### Inline Profile Editing

Admins can edit employee profiles directly from the detail page. Each section supports an **Edit / Save / Cancel** pattern:

1. Click **Edit** on a section to enter edit mode
2. Modify fields as needed
3. Click **Save** to persist changes or **Cancel** to discard

Editable sections: Personal Info, Address, Employment, Emergency Contact.

Changes are saved via the `AdminUpdateEmployee` endpoint:

```
PUT /api/AdminEmployee/{employeeId}/update
```

This sends the updated employee data to the backend, which validates and persists the changes.

## Document Approval Workflow

Each uploaded document goes through a review process:

```
Uploaded → Pending → Approved / Rejected
```

### Document Types

| Type | Description |
|---|---|
| `IdentityCard` | Government-issued ID |
| `Passport` | Passport document |
| `DriversLicense` | Driver's license |
| `WorkPermit` | Work authorization |
| `Contract` | Employment contract |
| `Certificate` | Professional certifications |
| `BankStatement` | Bank account verification |
| `TaxDocument` | Tax documents |
| `InsuranceDocument` | Insurance papers |
| `Other` | Other documents |

### Document Statuses

| Status | CSS Class | Description |
|---|---|---|
| `Pending` | `status-pending` | Awaiting admin review |
| `Approved` | `status-approved` | Document accepted |
| `Rejected` | `status-rejected` | Document rejected (with reason) |

### Document Actions

**Approve:**
```typescript
facade.approveDocument(documentId);
// Calls adminEmployeeDocumentClient.approve(documentId)
```

**Reject:**
```typescript
facade.openRejectDocumentDialog(document);
// Opens RejectDialogComponent for reason input
// Calls adminEmployeeDocumentClient.reject(documentId, { notes: reason })
```

**Download:**
```typescript
facade.downloadDocument(document);
// Downloads the file via adminEmployeeDocumentClient.download(documentId)
// Triggers browser file download
```

**Preview:**
```typescript
facade.previewDocument(document);
// Downloads blob and opens in new browser tab
```

### Document Display

Documents are grouped by status for easy review:
- `pendingDocuments` -- Documents awaiting review (action required)
- `approvedDocuments` -- Previously approved documents
- `rejectedDocuments` -- Previously rejected documents

Each document card shows:
- File name
- Document type (translated label)
- Status badge
- File size (formatted: KB/MB)
- Upload date
- Action buttons (approve, reject, download, preview)

## Employee Approval / Rejection

### Approval Criteria

The `canApproveOrReject()` method returns `true` when:
- `isProfileComplete === true`
- `contractStatus === 'Pending'`

### Approve Employee

```typescript
facade.approveEmployee();
// Calls adminEmployeeClient.approve(employeeId)
// Reloads employee detail on success
```

Sets the employee's `ContractStatus` to `Approved`, granting full platform access.

### Reject Employee

```typescript
facade.openRejectEmployeeDialog();
// Opens RejectDialogComponent
// On confirm: calls adminEmployeeClient.reject(employeeId, { reason })
// Reloads employee detail on success
```

::: warning
Rejecting an employee prevents them from accessing order management features. The rejection reason is stored and can be reviewed later.
:::

## Availability Management

Admins can view and edit an employee's weekly availability schedule:

1. Click "Edit Availability" to enter edit mode (`editingAvailability` signal)
2. Modify time ranges for each day of the week
3. Click "Save" to persist changes via `adminEmployeeClient.updateAvailability()`
4. Click "Cancel" to discard changes

The availability is stored as a map of day names to `TimeRange[]` arrays.

## Pay Configuration

The Pay Configuration section on the employee detail page allows admins to manage **per-employee pay rate overrides**. This is the only place where employee-specific rates are managed — Global Rates are managed separately on the [Global Rates page](./pay-config).

### Progress Summary

At the top of the section, a summary banner shows configuration coverage:

```
Services: X / Y configured
Packages: X / Y configured
```

### Bulk Apply Grade Template

The fastest way to onboard an employee. Pick a grade and currency, click **Apply to All**:

| Grade  | Multiplier | Use Case                          |
|--------|-----------|-----------------------------------|
| Junior | 0.5x      | New hire, in training             |
| Medior | 0.75x     | Experienced cleaner               |
| Senior | 1.0x      | Top performer, full base rate     |

The multiplier is applied to each service's `BasePrice` and `PerRoomPrice`, and to each package's `Price`. The result is stored as a per-employee `EmployeePayConfig` record.

**Overwrite Existing** checkbox: when enabled, existing per-employee configs are deleted and replaced. When disabled (default), existing configs are skipped.

API call:
```
POST /api/AdminPayConfig/bulk-create-for-employee
{
  "employeeId": "...",
  "grade": "junior" | "medior" | "senior",
  "currencyId": "...",
  "overwriteExisting": false
}
```

Returns:
```
{
  "createdCount": 15,
  "skippedCount": 3
}
```

### Service & Package Tables

Two tables list every active service and package with status icons:

- ✓ Green checkmark — employee has a per-employee config for this item
- ✗ Grey X — employee uses the global rate (or no rate exists)

Each row shows the rate breakdown: `basePay + extraPerRoom/room + extraPerBathroom/bath {currency}`.

### How Pay is Calculated for Orders

When an order is completed, the system looks up the pay rate in this order:

1. **Per-employee config** (`EmployeePayConfig` where `EmployeeId = currentEmployee.Id`) — used if exists
2. **Global rate** (`EmployeePayConfig` where `EmployeeId IS NULL`) — fallback

This means an employee can have overrides for some services and use global rates for others.

## Reject Dialog

The `RejectDialogComponent` is a shared PrimeNG DynamicDialog used for both employee and document rejection:

```typescript
interface RejectDialogData {
  title: string;     // Dialog header (translated)
  subtitle: string;  // Explanation text (translated)
}

interface RejectDialogResult {
  reason: string;    // Admin-provided rejection reason
}
```

## Formatting Utilities

The facade provides formatting helpers:
- `formatFileSize(bytes)` -- Converts bytes to "1.5 KB" or "2.3 MB"
- `formatDate(date)` -- Formats as `en-GB` locale date
- `formatDateTime(date)` -- Formats as `en-GB` locale date + time
