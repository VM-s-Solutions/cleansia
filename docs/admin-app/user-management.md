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
| Payout Details | **Masked** bank destination, with an audited reveal action |
| Documents | Uploaded documents with review workflow |

### Payout Details — masked by default, reveal is audited

ADR-0034. The bank destination is an `EmployeePayoutDetails` record of its own, never a column on the
employee and never on `EmployeeDto`. Two endpoints, two DTOs:

| Endpoint | Returns | Notes |
|---|---|---|
| `GET /api/AdminEmployee/{employeeId}/payout-details` | `MaskedPayoutDetails` | Policy `CanViewEmployeePayoutDetails`. The record has **no unmasked field at all** — a client cannot render what it was never sent, so widening it is a schema change a reviewer sees |
| `POST /api/AdminEmployee/{employeeId}/payout-details/reveal` | `RevealedPayoutDetails` | Policy `CanRevealEmployeePayoutDetails`, **rate-limited under the `auth` policy**. A command, not a query — that is what puts it through the audit engine, the compensating control for plaintext storage. It stamps `LastRevealedAt` / `RevealCount`, both shown on the masked view. The rate limit matters: masking only bounds exposure if the number of reveals does, and the same policy that lists employee ids reaches this route |

The record is never `Include`d on the employee grid or any paged query.

### Pay Configuration

Per-employee rate overrides are live. An `EmployeePayConfig` row with a non-null `EmployeeId`
overrides the platform-wide row for the same service or package; `CalculateOrderPay` picks the
employee-specific config when one exists and falls back to the global one otherwise. The bulk apply
seeds a whole grade at once (junior 0.5×, medior 0.75×, senior 1.0×).

### Weekly Order Limit — a brake, and nobody is behind it by default

```
PUT /api/AdminEmployee/{employeeId}/weekly-order-limit
```

`Employee.WeeklyOrderLimit` is nullable and `null` means **unlimited**, which is every cleaner unless an
admin has typed a number for that one person. Send `null` to lift a cap; the floor is `1`, and anything
below it is refused with `employee.weekly_limit_invalid`.

Until 2026-08-22 this was not a setting at all but a rating ladder — 3, 6 or 10 jobs a week by score —
applied to everyone automatically. It throttled hardest exactly the cleaners who most needed the work,
because a new cleaner's rating starts at zero for want of reviews rather than for want of quality. The
owner's ruling was to remove the automatic cap and keep the mechanism as something an admin **chooses**,
for a cleaner whose behaviour warrants it.

Treat it accordingly. It caps somebody's earnings, so it is a narrow audited command
(`employee.weekly_limit.update`) with a real before/after snapshot, not a field on the bulk profile
save — an admin should not be able to throttle a cleaner as a side effect of correcting their address.
A cleaner at their cap sees `order.weekly_limit_reached` when they try to take a job.

**Read the number as "outstanding commitments", not "jobs this week".** The count behind it includes only
orders in a slot-blocking status, which excludes `Completed` as well as `Cancelled` — so a finished job
leaves the count and the cleaner may take another. A cancelled job no longer eats the week, which is the
half that was wanted; a completed one no longer counts either, which is the half that came with it. An
admin who types 3 is capping how much a cleaner may have *open at once*, and the real weekly ceiling is
well above three.

→ [ADR-0053](/decisions/adr-0053)

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

The **server** adds one more, and it is the one that bites: every document type the employee's work
country marks required must be present **and** `Approved`. Approval used to consult
`IsProfileComplete()` alone, which excludes documents deliberately — so an admin could approve a cleaner
who had uploaded nothing, or whose every document had been rejected, and `Approved` meant only that
somebody had pressed the button. The refusal comes back as `employee.documents_not_approved`.

A country with **no** requirement rows configured gates nothing, which keeps the rule additive: a market
whose requirements have not been entered behaves exactly as it did before. Editing the rows never
reaches back and re-judges anyone already approved — approval is decided at the moment it happens, and
the requirements are an input to that decision rather than a standing property of the cleaner.

### Document requirements — per country, admin-managed

One row per (country, document type), carrying a required flag and a sort order. Admin-managed rather
than a constant on the owner's ruling: requirements change with the law, and a change that needs a
release is a change that waits for one.

| Endpoint | What it does |
|---|---|
| `GET /api/AdminEmployeeDocument/requirements/{countryId}` | The country's rows, optional ones included |
| `PUT /api/AdminEmployeeDocument/requirements` | **Upsert** on (country, type) — saving the same pair twice edits the flag |
| `DELETE /api/AdminEmployeeDocument/requirements/{requirementId}` | Hard delete; this is configuration, not a record of anything that happened |

Seeded today: CZ and SK carry `IdentityCard` (required) and `WorkPermit` (optional).

### Deletion requests — the only thing that removes a document

Partners cannot delete their own documents. The button that let them soft-deleted on the spot, which
flipped `AreDocumentsUploaded` and re-engaged the registration lock: one tap cost a cleaner their access
to work, on documents the employer is required to hold. They now **ask**, and the request changes
nothing until it is answered.

| Endpoint | What it does |
|---|---|
| `GET /api/AdminEmployeeDocument/deletion-requests?status=` | The queue. Defaults to `Pending`, oldest first |
| `POST /api/AdminEmployeeDocument/deletion-requests/{requestId}/resolve` | Approve or reject; approving is what performs the deletion |

Rejecting **requires** notes. Approval speaks for itself — the document is gone — but a refusal without a
reason tells the cleaner only that somebody said no, and the cleaner cannot see the queue. Approving a
request whose document has already gone is not an error: the outcome asked for is the outcome they have,
and answering it anyway is what clears the row out of the queue.

::: info Erasure ordering
`DocumentDeletionRequest` holds a `Restrict` foreign key to the document, so GDPR erasure removes the
requests **before** the documents — otherwise a surviving request makes the erasure throw rather than
skip. → [/flows/gdpr-and-audit](/flows/gdpr-and-audit)
:::

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
