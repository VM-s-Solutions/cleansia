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
| Pay Configuration | Per-employee rate overrides, one currency each, with bulk grade apply |
| Payout Details | **Masked** bank destination, with an audited reveal action |
| Documents | Uploaded documents with review workflow |

### Payout Details — masked by default, reveal is audited

ADR-0034. The bank destination is an `EmployeePayoutDetails` record of its own, never a column on the
employee and never on `EmployeeDto`. Two endpoints, two DTOs:

| Endpoint | Returns | Notes |
|---|---|---|
| `GET /api/AdminEmployee/{employeeId}/payout-details` | `MaskedPayoutDetails` | Policy `CanViewEmployeePayoutDetailsAdmin` (Accountant or above — the admin-host twin of the cleaner's own `CanViewEmployeePayoutDetails`, ADR-0066). The record has **no unmasked field at all** — a client cannot render what it was never sent, so widening it is a schema change a reviewer sees |
| `POST /api/AdminEmployee/{employeeId}/payout-details/reveal` | `RevealedPayoutDetails` | Policy `CanRevealEmployeePayoutDetails` (Manager or above — the Accountant sees the masked view only, per the owner's matrix), **rate-limited under the `auth` policy**. A command, not a query — that is what puts it through the audit engine, the compensating control for plaintext storage. It stamps `LastRevealedAt` / `RevealCount`, both shown on the masked view. The rate limit matters: masking only bounds exposure if the number of reveals does, and the same policy that lists employee ids reaches this route |

The record is never `Include`d on the employee grid or any paged query.

`MaskedPayoutDetails` carries `CurrencyId`: the currency the cleaner declares the account holds. It is
nullable, written only by the cleaner's own `UpdateBankDetails` (never by an admin), and an undeclared
account is read as the currency of the cleaner's work country — CZ is CZK, SK is EUR, PL is PLN — never
the platform default. It is not a label — `ApproveInvoice` compares it with the invoice's currency and
refuses `payroll.invoice.payout_currency_mismatch` when they differ, because approval is the last point
where the platform can refuse a transfer that is then keyed by hand. Before that comparison, approval
refuses `payroll.invoice.payout_details_missing` when there is no usable record at all (absent, no
scheme, or not `Provided`) — a cleaner with pay rows and no destination, reachable through an admin
reassignment or an erasure, gets their invoice document on time and the admin cannot commit to a
transfer to nowhere. The masked rows on the detail page do not render the currency.
→ [/flows/pay-and-payouts](/flows/pay-and-payouts#approval-is-the-last-refusal),
[/domain/roles/employee-payout-details](/domain/roles/employee-payout-details)

### Pay Configuration

Per-employee rate overrides are live. An `EmployeePayConfig` row with a non-null `EmployeeId`
overrides the platform-wide row for the same service or package **in the same currency**;
`CalculateOrderPay` reads only rows in the order's currency, picks the employee-specific config when
one exists and falls back to the global one otherwise. The unique index is
`(EmployeeId, ServiceId, PackageId, CurrencyId)`, so a cleaner legitimately holds a CZK rate and a EUR
rate for the same service, and neither counts for an order in the other currency. The bulk apply seeds a
whole rate template at once (standard 0.5×, experienced 0.6×, expert 0.7×) in one currency.
→ [/product/business-rules#rates-per-currency](/product/business-rules#rates-per-currency)

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

**A cap needs a reason, and the cleaner sees it** (owner ruling 2026-09-28). The body is
`{ weeklyOrderLimit, reason }`; a cap without a reason is refused (`employee.weekly_limit_reason_required`,
at most 500 characters), the reason goes on the audit row, lifting the cap clears it, and an erasure
clears it too. The employee detail shows the recorded reason and prefills it in the editor; the cleaner
reads the cap and its reason on their profile (partner web, Android, iOS).

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

**A changed IČO is checked; an untouched one is not** (since 2026-10-05). Every section the page saves
resends the stored registration number, so only a number that differs from the stored one is judged:
first its format in the register country — the work country, else the address country — refused with
`validation.registration_number.invalid_format` (a required IČO cannot be cleared), then that country's
register, with approval's four refusals for an approved cleaner and only *not registered* for anyone
else. Until then the edit wrote any number unchecked.
→ [The business register](/product/business-rules#business-register)

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

The detail page and the list row offer **Approve** when `canApprove()` is `true` and **Reject** when
`canReject()` is:
- both need `isProfileComplete === true` and the role's policy (`CanApproveEmployee`, `CanRejectEmployee`)
- **Approve** is offered on a `Pending` **or `Rejected`** contract — a cleaner rejected earlier can be
  approved again once the gates below pass, and approving clears the rejection reason
- **Reject** is offered on a `Pending` contract only

Nothing moves a rejected cleaner back to `Pending` by itself; the contract stays `Rejected` until an
administrator approves it. → [Business rules — the papers a cleaner uploads](/product/business-rules#employee-documents)

The **server** adds three more. The first is the one that bites: every document type the employee's work
country marks required must be present **and** `Approved`. The second is pay coverage in the work
country's currency, described under [Approve Employee](#approve-employee) below. The third, since
2026-10-04, is the business register: for a Czech cleaner, ARES must hold the IČO, show the business
live and a trade licence in force, and answer at all (`validation.registration_number.not_registered`,
`employee.business_ceased`, `employee.trade_licence_inactive`, `employee.business_registry_unavailable`,
the last one a *try again*) → [The business register](/product/business-rules#business-register).
Approval used to consult
`IsProfileComplete()` alone, which excludes documents deliberately — so an admin could approve a cleaner
who had uploaded nothing, or whose every document had been rejected, and `Approved` meant only that
somebody had pressed the button. The refusal comes back as `employee.documents_not_approved`.

A country with **no** requirement rows configured gates nothing, which keeps the rule additive: a market
whose requirements have not been entered behaves exactly as it did before. Editing the rows never
reaches back and re-judges anyone already approved — approval is decided at the moment it happens, and
the requirements are an input to that decision rather than a standing property of the cleaner.

### Document requirements — per country, admin-managed {#document-requirements}

One row per (country, document type), carrying a required flag and a sort order. Admin-managed rather
than a constant on the owner's ruling: requirements change with the law, and a change that needs a
release is a change that waits for one.

| Endpoint | What it does |
|---|---|
| `GET /api/AdminEmployeeDocument/requirements/{countryId}` | The country's rows, optional ones included |
| `PUT /api/AdminEmployeeDocument/requirements` | **Upsert** on (country, type) — saving the same pair twice edits the flag |
| `DELETE /api/AdminEmployeeDocument/requirements/{requirementId}` | Hard delete; this is configuration, not a record of anything that happened |

Seeded today: CZ and SK carry `IdentityCard` (required), `WorkPermit` (optional) and `InsuranceDocument`
(optional since 2026-10-04 — recommended, gating nothing; it was required from 2026-09-28). A database
seeded before 2026-10-04 keeps the insurance row required until an administrator clears the flag here or
the database is reseeded.

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
facade.openApproveEmployeeDialog();
// Opens ApproveDialogComponent: a required work country, optional notes (≤ 1000 chars)
// On confirm: calls adminEmployeeClient.approve(employeeId, { workCountryId, notes })
// Reloads employee detail on success
```

Sets the employee's `ContractStatus` to `Approved` and `WorkCountryId` to the country picked, granting
full platform access. The country must exist and be serviced (`country.not_found`,
`country.not_serviced`). The cleaner is pushed `employee.registration_approved`, and their
registration lock lifts on its next load.

The work country also decides the currency the cleaner will be paid in — and the currency of every
order they will see on their board and be allowed to take (owner ruling 2026-09-12) — and approval is
refused when that currency is not covered. `ICurrencyResolutionService.ResolveCurrencyForCountryAsync`
reads the country's `CountryConfiguration.DefaultCurrencyCode` and returns the `Currency` row it names;
a country with no configuration, a blank code or a code naming no row makes it **throw** rather than
hand back the platform default (owner ruling 2026-09-12: a working country without a currency is a
configuration defect, and the approval fails loudly instead of approving a cleaner into the wrong
currency) — the same chain that later labels the cleaner's earnings, and the same one that
prices a customer's booking from its address country, entered here at the work country because this
is the command that assigns it. Every active service and package must then have a pay
config in that currency, platform-wide or this cleaner's own (`PayCoverage.Applies`); an uncovered
entry refuses `employee.pay_config_missing`, one failure per entry with the entry's name as the error
code, so the refusal says what to configure. A rate in another currency does not count — a cleaner
approved on a CZK rate for a EUR market would sit silently unpaid. Approval creates no pay config; it
only checks that they exist. Because a platform-wide row covers every cleaner, a fully configured
[Global Rates page](./pay-config) in the market's currency is what makes approval satisfiable without
any per-employee work.

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

**The cleaner reads the reason word for word.** Partner web and both partner apps show it, untranslated,
on the cleaner's registration lock under the rejection, beside a *Contact support* action — so write it
for the cleaner. It is never put in the push: the cleaner is pushed `employee.registration_rejected`,
which only says the application was rejected.
→ [The registration lock](/partner-app/onboarding#registration-lock-screen)

## Pay Configuration

The Pay Configuration section on the employee detail page allows admins to manage **per-employee pay rate overrides**. This is the only place where employee-specific rates are managed — Global Rates are managed separately on the [Global Rates page](./pay-config).

### Progress Summary

At the top of the section, a summary banner shows configuration coverage:

```
Services: X / Y configured
Packages: X / Y configured
```

### Bulk Apply Rate Template

The fastest way to onboard a partner. Pick a rate template and currency, click **Apply to All**:

| Template    | Multiplier |
|-------------|-----------|
| Standard    | 0.5x      |
| Experienced | 0.6x      |
| Expert      | 0.7x      |

Every template leaves the company a margin (owner ruling 2026-09-28). The old junior / medior / senior
ranks — senior paid the whole customer price — are refused (`common.invalid_enum_value`).

The multiplier is applied to the catalogue price **in the currency picked**: the `ServicePrices` row
(`BasePrice`, `PerRoomPrice`) and the `PackagePrices` row (`Price`) for that currency. A service or
package carries no price of its own — prices are authored per currency, and nothing converts — so the
result is a per-employee `EmployeePayConfig` in that same currency:

```
service:  BasePay = BasePrice × m      ExtraPerRoom = PerRoomPrice × m      (rounded to 2 places)
package:  BasePay = Price × m          ExtraPerRoom = 0
          ExtraPerBathroom = 0, description "Auto-generated from the {template} rate template"
```

An entry with no price row in that currency is **skipped and counted in `skippedCount`**, never
defaulted: there is nothing to derive a rate from, and a zero rate is a cleaner paid nothing. Nothing is
derived from a price in another currency — that is exactly the defect this closes, where generating EUR
configs produced EUR pay from CZK numbers.

**Overwrite Existing** checkbox: when enabled, this employee's existing configs **in that currency** are
removed and replaced; rates in any other currency are untouched. When disabled (default), an entry that
already has a config in that currency is skipped. The price guard runs before the overwrite branch, so a
bulk run against a currency the catalogue is not priced in deletes nothing and creates nothing.

The currency select lists every currency the platform knows, switched on or not (the admin overview);
the command checks only that it exists (`currency.invalid`). Whether anything is generated is decided by
whether the catalogue is priced in it.

API call:
```
POST /api/AdminPayConfig/bulk-create-for-employee
{
  "employeeId": "...",
  "grade": "standard" | "experienced" | "expert",   // the field keeps its wire name
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

Each row shows the rate breakdown: `basePay + extraPerRoom/room + extraPerBathroom/bath {currencyCode}`.
The summary (`GetEmployeePayConfigSummary`) shows **one** config per entry — the first found for the
employee, whichever currency it is in — so a cleaner holding a CZK and a EUR rate for the same service
sees one of them here, labelled with its code; the coverage counts are over entries, not currencies.

### How Pay is Calculated for Orders

When an order is completed, the system looks up the pay rate **in the order's currency**, in this order:

1. **Per-employee config** (`EmployeePayConfig` where `EmployeeId = currentEmployee.Id` and
   `CurrencyId = order.CurrencyId`) — used if exists
2. **Global rate** (`EmployeePayConfig` where `EmployeeId IS NULL` and `CurrencyId = order.CurrencyId`)
   — fallback

A config in any other currency is not read at all. This means an employee can have overrides for some
services and use global rates for others; it also means an override authored in CZK does nothing for a
EUR order, which falls through to the EUR global rate.
→ [/product/business-rules#rates-per-currency](/product/business-rules#rates-per-currency)

## Customers {#customers}

Route: `/customers`, the *Customers* sidebar entry after *Receivables* (a `customers-list` in the
`loyalty-user-detail` library). Entry, route and endpoint carry `CanViewOrderCustomer` — Support or above,
so an Administrator, a Manager and a Support see it and an Accountant does not. The list pages
`GET api/AdminCustomer/get-paged` server-side; the filter drawer's search matches part of the first name,
last name, e-mail or phone (after the drawer's shared debounce), and a status filter picks **Active**,
**Inactive** or **All**, opening on Active. The columns are name, e-mail, phone, status, e-mail
confirmed and created. A row click, or its view action, opens the customer detail below.

## Customer detail — credit is held per currency {#customer-credit}

Route: `/customers/:id` (the `loyalty-user-detail` library; it kept its name when it grew a credit
balance, a ledger and the two credit actions).

**Points and credit are two different things, offered side by side** (owner ruling 2026-10-01). The
header actions carry **Grant points**, **Revoke points** and **Issue credit** next to each other, after
*Export subject data* and *Incident file*, with Issue credit the one filled primary at the right. Points
are the **loyalty** balance — `GrantPointsManually` / `RevokePointsManually`, both behind
`CanGrantLoyaltyPoints` — and the tier follows their total. Credit is **money** in a named currency on
the customer's balance, spent on a booking in that currency — `IssueCustomerCredit`, behind
`CanIssueCustomerCredit`; the dialog labels the amount with the currency picked (*Amount in CZK*) and
preselects none. Both policies are Support or above. Neither action stands in for the other: in every
locale the credit label and its confirmation share no word with the points ones (a copy spec pins it),
so the uk and ru credit action speaks of money put on the balance, not of bonuses.

A customer holds **one credit account per currency** —
credit issued for a EUR booking is EUR and cannot be spent on a CZK one — so the page shows one
balance block per account, each labelled with its currency code, and the two actions name a currency:

| Action | Command | Currency |
|---|---|---|
| Issue credit | `IssueCustomerCredit` | Chosen by the admin in the dialog from the active currencies; must exist and be active (`currency.not_found`, `currency.invalid`). Capped by the unit-free sanity guard of 10 000 in whatever currency it names. |
| Expire credit | `ExpireCustomerCredit` | The account the admin clicked **Expire** on — the dialog carries that account's `currencyId` and balance. Required on the wire; unknown is `currency.not_found`. |

**A discharge takes one account's whole balance and leaves the others alone.** It is **not** a step
before an erasure: since 2026-09-24 a completed erasure writes off every positive balance itself, in
every currency, and a positive balance no longer refuses one (the dialog says so) →
[Credit on a deleted account](/product/business-rules#credit-on-account-deletion). The erasure and
*Retry* confirmations on the data-protection page warn that the customer's unused credit is written off,
not paid out and not restorable. The command drains the named account to zero, writes one
`Expired` ledger row on it, and answers `amountExpired` with the account's `currencyCode`; a discharge
in a currency the customer holds nothing in is a no-op that answers zero, not an error. The old refusal
for a customer funded in two currencies (`credit.held_in_multiple_currencies`) is gone — it had no
admin action that could resolve it. `GetUserCredit` returns every account with its own `currencyId`,
`currencyCode` and ledger, which is what the per-account blocks and the Expire dialog read.
→ [Money constants](/product/business-rules#money-constants)

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

The facades provide formatting helpers:
- `formatFileSize(bytes)` (`EmployeeDocumentsFacade`) -- Converts bytes to "1.5 KB" or "2.3 MB"
- `formatDate(date)` / `formatDateTime(date)` (`EmployeeDetailFacade`) -- The shared
  `formatDate(value, lang, style)` from `@cleansia/utils` (`'date' | 'dateTime' | 'utcDate'`; the last
  is for a calendar day the wire carries as midnight UTC), in the **session's language** rather than a
  pinned `en-GB` — `21. 9. 2026` and `21. 9. 2026 11:00` in Czech, `Sep 21, 2026` in English; seconds
  never print; the helper returns `''` for an empty or invalid value and the facade prints `-` in its
  place. The admin and partner lists and details format their dates in the session's language through
  this helper, and money through `formatMoney` (`1 250,00 Kč`) — except the membership-plan price
  cells, which keep a local `299.00 CZK` format, and the package form's derived gross, which prints a
  bare number.
