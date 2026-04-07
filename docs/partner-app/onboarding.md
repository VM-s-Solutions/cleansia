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

Once logged in, partners need to complete their profile via the `/profile` page. Required profile information includes:

- Personal details (name, phone, date of birth)
- Address information
- Bank account details (for invoice payments)
- Business identity:
  - Entity type (Natural Person or Legal Entity)
  - Registration Number (IČO) -- mandatory
  - VAT Number (DIČ) -- optional
  - Legal Entity Name -- required only when Entity type is Legal Entity
- Preferred languages
- Availability schedule (days/times they can work)

::: info
Emergency contacts are optional and are **not** required for profile completion.
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

::: warning
Approved documents cannot be deleted by the partner. Only pending or rejected documents can be removed.
:::

::: tip
Documents are uploaded as files and stored in Azure Blob Storage. The admin app provides a download/preview interface for reviewing uploaded documents.
:::

## Step 6: Admin Approval

After the partner completes their profile and uploads required documents, an admin reviews the application:

1. Admin views the partner's profile in the admin app (Employee Management)
2. Admin reviews each uploaded document (approve/reject individually)
3. Admin can approve or reject the partner overall

**Approval criteria:**
- Profile is complete (`isProfileComplete === true`)
- Contract status is `Pending`
- Required documents are uploaded and approved

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
3. **Required Documents** -- whether at least one active (uploaded) document exists
4. **Admin Approval** -- shows one of the following distinct states:
   - _"Complete profile first"_ -- profile is not yet complete
   - _"Awaiting review"_ -- profile is complete and pending admin decision
   - _"Rejected: {reason}"_ -- admin has rejected the application with a reason
   - _"Approved"_ -- admin has approved the partner

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
