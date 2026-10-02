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
**Emergency contacts** are optional. **Documents** are handled separately by the registration lock.
**There is no weekly schedule to fill in** — dispatch is a first-come pull board, and the schedule
the employee record used to carry was read by nothing, so on 2026-09-20 it was removed: the admin
detail section, the two update endpoints, the `availability` and `hasSetAvailability` wire members
and the column went together.
:::

::: tip Bank details live in their own record (ADR-0034)
The payout destination is an `EmployeePayoutDetails` row, not a column on the employee. The
completeness gate reads a scalar — `HasPayoutDetails || IBAN` — where `IBAN` is a **legacy** column
kept because there is no backfill for cleaners onboarded before the new record existed; dropping it
would mark them incomplete and lock them out of the partner surface overnight.

Admins see a **masked** view by default; the plaintext is behind a separate, audited reveal action.
:::

**A Czech or Slovak account can be pasted whole into any one of its three boxes** — prefix, number or
bank code — on Android and iOS, and it lands in all three. Each box used to keep its own digits and
clamp, so pasting `12321414/3545` into the number gave the number `1232141435` and an empty bank code.
The apps read three shapes:

| Pasted | Lands as |
|---|---|
| `19-2000145399/0800`, `2000145399/0800` | prefix, number and bank code; a missing prefix clears the old one |
| `19-2000145399` | prefix and number; the bank code already there stays |
| `CZ65 0800 0000 1920 0014 5399`, or an SK IBAN | the same three parts, with the IBAN's zero padding dropped |

Spaces of every kind are ignored (a non-breaking one included) and an en or em dash counts as a hyphen.
**A bare number goes to the number field, whichever box it was pasted into**: up to ten digits pasted
at once (spaces ignored) into an empty box become the number, and the prefix and bank code stay as they
are, so `2000145399` pasted into an empty prefix box no longer turns into the prefix `200014`. Typing
never jumps, because the number pad types one digit at a time, and eleven or more bare digits are not
an account number, so the box keeps its own clamp. **A paste into a box that already holds digits is
read together with them**, because every shape is matched against the box's whole new text. Pasted at
the caret, `2000145399` after a `19` in the prefix box is the twelve digits `192000145399`, which is no
account, so the box clamps it to the prefix `192000`, and `2000145399/0800` pasted there falls back to
the clamp the same way. A shorter paste can be routed with the box's digits in front: `2000` after `19`
makes the number `192000`, and the prefix stays `19`. Pasted over all of the box's digits, the paste is
read on its own, though a bare number still has to be at least two characters longer than what it
replaces. The rule has one more cost: a real two-digit prefix such as `19` pasted into an empty prefix
box goes to the number; typed, it stays. **After a routed paste the keyboard closes, on both apps**,
because both rebuild the three boxes: iOS with `.id(pasteRevision)`, so each box draws its new value,
and Android with `key(pasteRevision)`, so pasting the same text into the same box a second time is not
dropped as a repeat. **Each box keeps the digits `0`–`9` only**, up to its length, on both apps
(`clampSegment`, since 2026-10-02): a keyboard can type Arabic-Indic or full-width digits, which the
server refuses, and the boxes used to keep them. The apps only split text — the server keeps every rule
(the mod-11 check, the bank-code shape, the IBAN cross-check). Partner web splits the two separator
shapes as well, and it also sends a bare number to the number field from any box, leaving the prefix
and bank code alone as the apps do (since 2026-10-02; it used to clear the prefix). A written-out
account without a prefix still clears the old one there too. The web still differs in four ways: it
does not decompose an IBAN, does not ignore spaces (ordinary or non-breaking) inside the digits, only
around the `-` and `/`, does not read an en or em dash as a hyphen, and sends a one-digit paste to the number, where the apps need at least
two characters.

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
5. Every file is its own document — several of one type are allowed — and a file byte-identical to one
   the cleaner already holds, active and not rejected, is refused (`employee_document.duplicate_file`)
   → [Business rules — the papers a cleaner uploads](/product/business-rules#employee-documents)

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

Seeded today: **CZ and SK** carry `IdentityCard` (required), `WorkPermit` (optional) and, since
2026-09-28, `InsuranceDocument` (required — a cleaner is approved only with a valid liability insurance
certificate, whichever policy the platform finally promises). The list is
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
- Contract status is `Pending` or `Rejected` — a cleaner rejected earlier can be approved again, and
  nothing returns them to `Pending` by itself → [Business rules](/product/business-rules#employee-documents)
- Every document type the **work country** marks required is present **and** `Approved`
- Every partner document in force for that market — the framework contract, the self-billing
  agreement, the data-processing agreement — is accepted at its current version
  (`employee.legal_documents_not_accepted`; none is seeded yet, so nothing is refused today)
  → [A cleaner's own documents](/product/business-rules#cleaner-documents)

That last line is enforced, not advisory. Approval used to consult `isProfileComplete()` alone, which
excludes documents deliberately — so an admin could approve a cleaner who had uploaded nothing, or whose
every document had been rejected, and "Approved" meant only that somebody had pressed the button. It now
means the paperwork exists and was accepted. See [document requirements](#document-requirements) for
where the required list comes from, and note that editing those rows never reaches back and re-judges
anyone already approved.

**Approval actions:**
- `approveEmployee()` -- Sets `ContractStatus` to `Approved`, granting full access
- `rejectEmployee(reason)` -- Sets `ContractStatus` to `Rejected` with a reason, which the cleaner
  reads word for word on their lock screen

Either decision is pushed to the cleaner — see the [lock screen](#registration-lock-screen) below.

::: warning
Until approved, the partner can log in and access their profile, but their ability to take and manage orders may be restricted. The `contractStatus` field determines the partner's access level.
:::

### Registration Lock Screen

Partners who have not yet been approved see a registration lock screen that displays a **progress
bar** and the requirements still between them and work — three rows on the web, and on the Android
and iOS apps a fourth, **Contract documents**:

1. **Profile Information** -- lists the names of any missing required fields (translated to the partner's language)
2. **Required Documents** -- whether at least one active (uploaded) document exists. The
   documents screen behind it lists what the country actually asks for, per
   [document requirements](#document-requirements)
3. **Contract documents** (mobile) -- shown only while a partner document is in force for the market,
   and done once every one is accepted at its current version; until one is in force, approval does
   not wait on it
4. **Admin Approval** -- shows one of the following distinct states:
   - _"Complete profile first"_ -- profile is not yet complete
   - _"Awaiting review"_ -- profile is complete and pending admin decision
   - _"Rejected: {reason}"_ -- admin has rejected the application with a reason
   - _"Approved"_ -- admin has approved the partner

**On the mobile apps a finished row still opens its section, until approval** (owner ruling
2026-10-01). The lock replaces the whole app until an admin approves, so a row that went inert at
*Done* left a cleaner who had filled everything in with no way back to correct it — and
*Documents: Done* means one active document, not every type the country requires, so a cleaner who had
uploaded only an ID could not get back to add the insurance certificate approval needs. A *Done* row
keeps its *Done* label and gains a chevron: Profile opens Personal at the start of the onboarding
chain, from which every section is one step-dot away; Documents opens the documents screen; Contract
documents opens the documents to read and accept. This holds for a **rejected** cleaner too, and **an
edit does not resubmit anything** — the application stays where the admin left it. With a complete
profile the Personal, Address and Identification buttons read **Save** rather than *Next*, because
saving returns to the lock instead of moving on. Partner web already left the profile open behind the lock (see
*Excluded Routes* below), so this brings the apps to parity.

**A rejection says why, and offers a way out.** On both apps the rejected Approval row is drawn as an
error, shows the admin's reason under its line — **verbatim and untranslated**, as partner web shows
it, trimmed, and nothing when the admin left it blank — and offers **Contact support**, which opens an
e-mail to `support@cleansia.cz` with the subject *"Cleansia partner — application rejected"* in the
app's language. Partner web offers the same link under the reason since 2026-10-02, with the subject in
the UI language, and only while the application is rejected. The reason never travels in the push (below); the lock reads it from the
registration status.

**The decision reaches the cleaner without a pull.** An admin's approval or rejection pushes the cleaner
`employee.registration_approved` or `employee.registration_rejected` — push-only, with no feed row and
no deep link, because a tap opens the app and for this cleaner the app *is* the lock
([event catalogue](/architecture/push-notifications#registration-decided)). The lock re-checks:

| When | Android | iOS |
|---|---|---|
| The lock comes back on screen — the app returns to the foreground, or a section pops back | on `ON_RESUME`, once a 15-second stale window has passed | every time (`scenePhase` → `.active`, and `onAppear`) |
| A decision push arrives while the lock is on screen | at once, inside the stale window too | at once |
| Pull to refresh | yes | yes |

An approval found by that re-check unlocks the app; a rejection redraws the Approval row.

::: warning A document the admin rejected still reads *Done*
*Required Documents* counts any active document, whatever its review status, so a cleaner whose
required document an admin rejected still sees *Documents: Done* and *Awaiting review*. Reading the
per-type statuses the documents screen already loads (`GetMyDocumentRequirements`) would fix it on the
client; that is filed as T-0801 and not built.
:::

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
