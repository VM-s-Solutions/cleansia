# Partner Onboarding

The partner onboarding process ensures that only verified and approved cleaning partners can access the platform. The process involves multiple steps, from account creation to admin approval.

## Onboarding Flow

```
1. Create Account (/register)
   ↓
2. Email Confirmation (/confirm-email)
   ↓
3. Login (/login)
   ↓
4. Profile Completion (/profile)
   ↓
5. Document Upload (/profile)
   ↓
6. Admin Review & Approval (admin-app)
   ↓
7. Full Platform Access
```

## Step 1: Create Account

The partner registration page (`/register`) collects:

- First name
- Last name
- Email address
- Password (with confirmation)
- Phone number

::: info
The registration route is protected by the `guestGuard` -- already authenticated partners are redirected to `/orders`.
:::

After submission, the backend:
1. Creates the user account
2. Creates an associated employee record with `ContractStatus.Pending`
3. Sends an email confirmation link

## Step 2: Email Confirmation

After registration, partners are redirected to `/confirm-email`. The confirmation flow:

1. Partner receives an email with a 6-digit confirmation code
2. The confirm-email page presents a 6-digit code input component with individual digit fields that auto-advance to the next field on entry
3. The form auto-submits when the 6th digit is entered; clipboard paste of a full code is also supported
4. The code is sent to the backend for validation
5. On success, the email is marked as confirmed

::: warning
Partners cannot log in until their email is confirmed. The login flow checks `isEmailConfirmed` and redirects back to the confirmation page if false.
:::

## Step 3: Login

After email confirmation, the partner logs in with email and password. The authentication flow is similar to the customer app:

1. `authService.login(email, password)` returns a `JwtTokenResponse`
2. Session tokens are stored
3. Partner is redirected to `/orders`

## Step 4: Profile Completion

Once logged in, partners need to complete their profile via the `/profile` page.
`Employee.IsProfileComplete()` is the authoritative list:

- Personal details — first name, last name, email, phone, date of birth
- Address — street, city, ZIP, country
- **A payout destination** (see below), passport ID, nationality, registration number
- Business identity:
  - Entity type (Natural Person or Legal Entity)
  - Registration number -- mandatory
  - VAT number -- optional
  - Legal Entity Name -- required only when Entity type is Legal Entity

The **labels on those two fields come from the business country**, not from the app's language.
`CountryConfiguration` has carried `RegistrationNumberLabel` and `VatNumberLabel` since it was seeded —
CZ and SK both say "IČO" and "DIČ" — but nothing read them, so every client hardcoded the Czech word in
its own translation files and a Polish or Ukrainian partner read "IČO" for a registry that has no such
thing. The clients now ask the country and fall back to a neutral "Registration number" only where the
platform holds no configuration: correct everywhere, precise nowhere, which is exactly what a fallback
should be. Flattening every country to the neutral term would have cost CZ and SK the word their own
registries use.

::: info Not part of the completeness check
**Emergency contacts** are optional. **Documents** are handled separately by the registration lock,
and **the availability schedule is not read by matching or dispatch** — dispatch is a first-come
pull board, so a filled-in schedule does not gate anything today.
:::

::: tip Bank details live in their own record (ADR-0034)
The payout destination is an `EmployeePayoutDetails` row, not a column on the employee. The
completeness gate reads a scalar — `HasPayoutDetails || IBAN` — where `IBAN` is a **legacy** column
kept because there is no backfill for cleaners onboarded before the new record existed; dropping it
would mark them incomplete and lock them out of the partner surface overnight.

Admins see a **masked** view by default; the plaintext is behind a separate, audited reveal action.
:::

::: tip Country Configuration
Country-specific labels and validation rules (e.g., field names, format masks) are driven by the `CountryConfiguration` table managed in the admin app.
:::

The employee record has an `isProfileComplete` flag that tracks whether all required fields have been filled.

## Step 5: Document Upload

Partners must upload identity and work-related documents through the profile page. The upload flow works as follows:

1. A **drag-and-drop upload zone** is presented (no pre-selected document type)
2. Files are staged with no type assigned -- each file gets its own **inline type selector** where the partner picks the document type
3. **Validation:** all staged files must have a type selected before the upload can proceed; files missing a type are highlighted in red with an error message
4. Document cards display file-type colored icons (PDF = red, DOC = blue, JPG = yellow, etc.)

Supported document types:

| Document Type | Description |
|---|---|
| `IdentityCard` | Government-issued ID card |
| `Passport` | Passport |
| `DriversLicense` | Driver's license |
| `WorkPermit` | Work authorization document |
| `Contract` | Signed employment contract |
| `Certificate` | Professional certifications |
| `BankStatement` | Bank account verification |
| `TaxDocument` | Tax registration document |
| `InsuranceDocument` | Insurance documentation |
| `Other` | Other supporting documents |

Each document goes through a review workflow:

```
Uploaded → Pending → Approved / Rejected
```

### What the country asks for {#document-requirements}

The upload screen opens on a **checklist** of the document types the cleaner's country expects, each
row showing whether it is required and what has already been uploaded against it. A cleaner used to
open an empty box that named nothing, so the first step of onboarding was contacting support to ask.

The rows are admin-managed, one per (country, document type), and are what
[admin approval](#admin-approval) gates on. **A country with no rows configured gates nothing** — an
unseeded market behaves exactly as it did before the gate existed, rather than locking every cleaner in
it out of approval.

The checklist keys on the cleaner's **work country**, falling back to their address country. Work
country is the jurisdiction the requirements belong to, but it is only set at approval — and this screen
exists precisely for people who are not approved yet. A wrong guess costs a misleading prompt, not a
refusal; the approval gate itself only ever reads the work country.

An **optional** row is a prompt, not a gate. It is how a document type is offered as
expected-but-not-blocking; removing the row drops it from the screen entirely, clearing its required
flag keeps the prompt and drops the gate.

Seeded today: **CZ and SK** carry `IdentityCard` (required) and `WorkPermit` (optional). The list is
deliberately short and is not a statement about Czech or Slovak employment law — `WorkPermit` is
optional because it applies to non-EU nationals and to nobody else, and a per-country flag cannot say
"required for some of these people".

### Replacing and removing {#document-replace-and-remove}

A cleaner has two doors on a document they already own, and they are deliberately different.

**Replace** supersedes a document with a newer file and needs no admin. The new version is created
*before* the old one is retired, so the document count never dips, `AreDocumentsUploaded` never flips,
and the registration lock never re-engages. The document **type** is carried over from the version being
replaced rather than taken from the caller — otherwise a replacement could satisfy a requirement by
relabelling a document an admin had already approved. The new version lands `Pending`: it is new
evidence and has not been looked at, which is also why replacing cannot be used to dodge review.

**Request removal** asks an admin and changes nothing. The document stays active until the request is
answered, so a request nobody answers leaves the cleaner exactly as they were. A reason is required —
without one an admin is being asked to rule on nothing. One open request per document; an answered one
does not block a new one.

::: warning
**The partner cannot delete a document.** The button that did soft-deleted on the spot, and that flipped
`AreDocumentsUploaded`, which re-engaged the registration lock — one tap, with no confirmation on either
mobile platform, cost a cleaner their access to work. Some of these documents the employer is required
to hold, so the person least placed to judge whether one can go was the only one who could remove it.
Approving a removal request is now the only thing in the platform that removes one.
:::

::: tip
Documents are uploaded as files and stored in Azure Blob Storage. The admin app provides a download/preview interface for reviewing uploaded documents.
:::

## Step 6: Admin Approval {#admin-approval}

After the partner completes their profile and uploads required documents, an admin reviews the application:

1. Admin views the partner's profile in the admin app (Employee Management)
2. Admin reviews each uploaded document (approve/reject individually)
3. Admin can approve or reject the partner overall

**Approval criteria:**
- Profile is complete (`isProfileComplete === true`)
- Contract status is `Pending`
- Every document type the **work country** marks required is present **and** `Approved`

That last line is enforced, not advisory. Approval used to consult `isProfileComplete()` alone, which
excludes documents deliberately — so an admin could approve a cleaner who had uploaded nothing, or whose
every document had been rejected, and "Approved" meant only that somebody had pressed the button. It now
means the paperwork exists and was accepted. See [document requirements](#document-requirements) for
where the required list comes from, and note that editing those rows never reaches back and re-judges
anyone already approved.

**Approval actions:**
- `approveEmployee()` -- Sets `ContractStatus` to `Approved`, granting full access
- `rejectEmployee(reason)` -- Sets `ContractStatus` to `Rejected` with a reason

::: warning
Until approved, the partner can log in and access their profile, but their ability to take and manage orders may be restricted. The `contractStatus` field determines the partner's access level.
:::

### Registration Lock Screen

Partners who have not yet been approved see a registration lock screen that displays a **progress bar** and four requirement categories:

1. **Profile Information** -- lists the names of any missing required fields (translated to the partner's language)
2. **Availability** -- whether a weekly availability schedule has been set
3. **Required Documents** -- whether at least one active (uploaded) document exists. The
   documents screen behind it lists what the country actually asks for, per
   [document requirements](#document-requirements)
4. **Admin Approval** -- shows one of the following distinct states:
   - _"Complete profile first"_ -- profile is not yet complete
   - _"Awaiting review"_ -- profile is complete and pending admin decision
   - _"Rejected: {reason}"_ -- admin has rejected the application with a reason
   - _"Approved"_ -- admin has approved the partner

Signing out from this screen is confirmed on both mobile platforms. It is the one destructive thing
the screen offers and the control sat one tap away from it.

::: info Excluded Routes
The following pages are accessible even when the registration lock is active and are **not** blocked by the lock screen: Profile, GDPR, 404, Login, Register, Confirm Email, Forgot Password.
:::

## Step 7: Full Platform Access

Once approved, the partner has full access to:
- Browse and take available orders
- Start and complete assigned orders
- Upload before/after photos
- View earnings dashboard
- Access invoices and download PDFs

## Password Reset

Partners who forget their password can use the `/forgot-password` flow:

1. Enter email address
2. Receive reset link via email
3. Click link and set new password
4. Redirect to `/login`
