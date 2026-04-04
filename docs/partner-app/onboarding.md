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

1. Partner receives an email with a verification link
2. Clicking the link opens the confirm-email page with a token query parameter
3. The token is sent to the backend for validation
4. On success, the email is marked as confirmed

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
- Preferred languages
- Availability schedule (days/times they can work)
- Emergency contact (optional -- not required for profile completion)

The employee record has an `isProfileComplete` flag that tracks whether all required fields have been filled.

## Step 5: Document Upload

Partners must upload identity and work-related documents through the profile page. Supported document types:

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

Partners who have not yet been approved see a registration lock screen that displays:

- A **progress bar** indicating overall completion toward approval
- **Categorized requirements** (profile completion, document uploads, admin approval) each with status icons showing completed, pending, or missing items
- Clear guidance on what steps remain before full access is granted

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
