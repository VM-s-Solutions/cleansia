# Authentication

Cleansia uses JWT bearer tokens for API authentication with role-based access control (RBAC). The system supports email/password login and Google OAuth.

## Architecture

| Component | Details |
|-----------|---------|
| Token type | JWT (HS256) |
| Token storage | HttpOnly cookie on the web hosts; `Authorization: Bearer` on the two mobile hosts |
| Access-token lifetime | **Per host** — Admin 15 min, Partner 1440 min, Mobile Customer 30 min |
| Remember me | Selects the **refresh** lifetime, 30 days (Mobile Customer 90) versus 1 day — it does not change the access token |
| Rate limiting | `auth` policy on all auth endpoints |
| Identity | Custom user model with `UserProfile` enum |

::: info Source Files
- Token generation: `src/Cleansia.Core.AppServices/Services/TokenService.cs`
- JWT config: `src/Cleansia.Infra.Common/Configuration/Interfaces/IJwtSettings.cs`
- Auth handlers: `src/Cleansia.Core.AppServices/Features/Auth/`
- Partner API controller: `src/Cleansia.Web.Partner/Controllers/AuthController.cs`
- Partner Mobile controller: `src/Cleansia.Web.Mobile.Partner/Controllers/AuthController.cs`
- Customer Mobile controller: `src/Cleansia.Web.Mobile.Customer/Controllers/AuthController.cs`
:::

## Endpoints

All auth endpoints are rate-limited with the `auth` policy and are accessible without authentication (`[AllowAnonymous]`).

### The market on anonymous requests {#the-market-on-anonymous-requests}

An account belongs to the **operating company** that serves the market it was created in
([ADR-0061](/decisions/adr-0061)). Six anonymous requests that create or read company-scoped rows
therefore carry an **optional `countryId`** — the market, exactly the value `Market/GetOverview`
returns and `Order/Quote` already accepts — and the server resolves the market's company before
validation runs. **No request carries a tenant id**; the market is the only thing a client names.

| Request | Host | `countryId` | What it scopes |
|---|---|---|---|
| `POST /api/Auth/Register` | Customer, Customer Mobile — **not** the partner hosts (removed 2026-09-15; a cleaner registers through `RegisterEmployee`) | optional, last field | the new `User` and `Cart`; the "email already registered" check |
| `POST /api/Auth/RegisterEmployee` | Partner, Partner Mobile | optional, last field | the new `User`, `Cart` and `Employee` — the cleaner is held to this company's countries at approval |
| `POST /api/Auth/GoogleAuth` (all four hosts), `POST /api/Auth/AppleAuth` (Customer, Customer Mobile) | see left | optional, last field | the `User` and `Cart` provisioned on a **first** sign-in — **on a customer host only**. On a partner host `GoogleAuth` signs in an existing Employee or Administrator and provisions nothing: a first-time Google identity is refused `auth.social_account_not_found`, a Customer account `auth.insufficient_privileges`. An existing account keeps its own company whatever market the request names |
| `POST /api/PromoCode/Request` | Customer | optional, last field | the `PromoCode` issued and the e-mail that carries it |
| `POST /api/Referral/Validate` | Customer, Customer Mobile | optional, last field | which company's referral codes are searched |

**Absent or `null` ⇒ the default market** (CZE today), so a client that predates the field behaves as
it always did. The regenerated clients carry the field; sending the chosen market is T-0728, sequenced
before a second market opens.

| Refusal | When | Field |
|---|---|---|
| `country.not_serviced` | `countryId` names a country that is not a market — unknown, inactive, not serviced, unconfigured, or its currency switched off. User input |
| `tenant.not_found` | `countryId` names a market (or the default market is one) that **no operating company serves**. A configuration defect — `Market/GetOverview` never lists such a market, so a client that resolved its market from the directory cannot produce this |

Both land on `CountryId` and are shaped like any validation failure. `country.not_serviced` was
already reachable from the quote and subscribe paths; these six requests are new reach for it, and the
error-contract parity specs say which app owes which key.

### Register

Creates a new **customer** account and sends a confirmation email. Routed on the two customer hosts
only — the partner hosts have no customer registration (a cleaner's account is opened through
`RegisterEmployee`), and a Google sign-in there signs in an existing cleaner or administrator only.

```
POST /api/Auth/Register          # Customer :5003, Customer Mobile :5004
```

**Request body:**

```json
{
  "email": "user@example.com",
  "password": "SecureP@ss123",
  "firstName": "John",
  "lastName": "Doe",
  "language": "en",
  "referralCode": null,
  "countryId": null
}
```

**Response:** `200 OK` with `true` on success.

**Validation rules:**
- Email must be valid and **not registered with any Cleansia company** — one email is one identity
  across the holding (`user.existing_email`; the check reads across companies on purpose, and the
  global unique index on `Users.Email` arbitrates the race two simultaneous registrations cannot see)
- First/last name required
- Password must meet complexity requirements
- Language must reference a valid language record
- `countryId`, if sent, must name a market with an operating company (the table above)

::: tip Re-registration
If a user exists but hasn't confirmed their email, calling Register again refreshes the confirmation
code and resends the email — **only in the market the account was created in**. The same email
re-registered with another company's market is refused `user.existing_email`; a visitor never silently
re-registers into a company they did not choose.
:::

---

### Register Employee

Registers a user specifically as an employee/partner.

```
POST /api/Auth/RegisterEmployee
```

**Request body:** Same structure as Register, including the optional `countryId` — the market whose
company the cleaner is registering with. At approval an admin must assign a work country that company
serves (`employee.work_country_operator_mismatch` otherwise, on the admin host).

**Response:** `200 OK` with `true`.

---

### Login (Partner)

Authenticates a partner/employee user and returns a JWT token.

```
POST /api/Auth/Login
```

**Request body:**

```json
{
  "email": "partner@example.com",
  "password": "SecureP@ss123",
  "rememberMe": true
}
```

**Response:**

```json
{
  "token": "eyJhbGciOiJIUzI1NiIs...",
  "isEmailConfirmed": true
}
```

::: warning Token is empty if email not confirmed
If `isEmailConfirmed` is `false`, the `token` field will be an empty string. The client must redirect to the email confirmation flow.
:::

**Auto-upgrade:** If a Customer-profile user logs into the Partner app, their profile is automatically upgraded to Employee and an Employee record is created.

---

### Google OAuth

Authenticates via Google ID token.

```
POST /api/Auth/GoogleAuth
```

**Request body:**

```json
{
  "idToken": "google-oauth-id-token",
  "countryId": null
}
```

`countryId` — optional; the market whose company a **first-time** sign-in registers with (the table
above). Ignored for an existing account: it is found by verified e-mail across the holding and keeps
its own company, and the tokens it receives carry that company's `tenant_id`.

**Response:** Same `JwtTokenResponse` as Login.

---

### Confirm User Email

Confirms the user's email using the confirmation code sent via email.

```
PUT /api/Auth/ConfirmUserEmail
```

**Request body:**

```json
{
  "email": "user@example.com",
  "confirmationCode": "abc123"
}
```

**Response:** `JwtTokenResponse` with a valid token upon successful confirmation.

---

### Resend Confirmation Email

Resends the email confirmation code.

```
POST /api/Auth/ResendConfirmationEmail
```

**Request body:**

```json
{
  "email": "user@example.com"
}
```

**Response:** `200 OK` with `true`.

---

### Forgot Password <Badge type="info" text="Mobile API only" />

Initiates the password reset flow.

```
POST /api/Auth/ForgotPassword
```

**Request body:**

```json
{
  "email": "user@example.com"
}
```

**Response:** `200 OK` with `true`.

## Token Configuration

JWT settings are defined in `appsettings.json` under the `JwtSettings` section:

```json
{
  "JwtSettings": {
    "Secret": "SET_VIA_USER_SECRETS_OR_KEY_VAULT",
    "AccessTokenExpMinutes": 15,
    "RefreshTokenExpDays": 30,
    "RefreshTokenShortExpDays": 1
  }
}
```

| Setting | Description |
|---------|-------------|
| `Secret` | HMAC-SHA256 signing key (min 32 chars). Key Vault for deployed environments; the same value across every host in an environment. |
| `AccessTokenExpMinutes` | Access-token lifetime. **Per host, and deliberately different:** Admin **15**, Partner **1440**, Mobile Customer **30**. Admin's 15 is ADR-0030 and is pinned by a test — see [ADR-0030](/decisions/adr-0030). |
| `RefreshTokenExpDays` | Refresh lifetime when the user chose *remember me* — **30** (Mobile Customer **90**). |
| `RefreshTokenShortExpDays` | Refresh lifetime when they did not — **1**. |

::: danger These keys are bound by name, and an unknown key is silently ignored
`AutoBindConfig` calls `Bind()` without `ErrorOnUnknownConfiguration`, so a misspelled or obsolete key
does not fail startup — it is dropped, and the host runs on the built-in default. There is no warning
in the log. The pair `DefaultTokenExpHours` / `CookieTokenExpHours` documented here until 2026-08-14 is
exactly that case: those keys existed in no `.cs`, `.json`, `.bicep` or `.yml` in the repository, so an
operator who set a 6-hour lifetime got Admin's 15 minutes and no signal that the setting was ignored.
:::

::: warning
The `Secret` value must **never** be committed to source control. Use `dotnet user-secrets` for local development and Azure Key Vault references for deployed environments.
:::

## JWT Claims

The token includes standard claims set from the `User` entity via `user.SetClaims()`:

| Claim | Source |
|-------|--------|
| `sub` (NameIdentifier) | `User.Id` |
| `email` | `User.Email` |
| `role` | `User.Profile` (`Customer`, `Employee`, `Administrator`) — the **audience** the account belongs to |
| `admin_role` | `User.AdminRole` (`Administrator`, `Manager`, `Support`, `Accountant`) — present on an `Administrator`-profile token only; the set an admin-host permission requires is checked against it ([ADR-0066](/decisions/adr-0066)). The partner hosts never read it |
| `tenant_id` | `User.TenantId` — the operating company the account belongs to, **always present** (the column is NOT NULL). It is the only thing that scopes an authenticated request; the request body never carries it and cannot override it |

`JwtTokenResponse` repeats the two hints the web cannot read out of an HttpOnly cookie — `role` and, for an
administrator, `adminRole` — beside the token; the admin app stores both and gates its UI on them. The
server remains the gate.

Every token mint — login on all five hosts, refresh, Google/Apple, e-mail confirmation — runs on an
anonymous request and writes a `RefreshToken` row; the server adopts the authenticated user's tenant
before writing it, so the row and the claim always agree ([ADR-0061](/decisions/adr-0061) D4).

## RBAC Policies

Authorization policies are defined in `src/Cleansia.Core.AppServices/Authentication/Policy.cs` and every one
maps, in `PolicyBuilder.Map`, to one physical policy — `Anonymous`, `Authenticated`, `CustomerOnly`,
`EmployeeOrAdmin`, `OwnerOrElevated`, `AdminOnly` (any administrator role), or one of the four
administrator **sets** — `AdministratorOnly`, `ManagerOrAbove`, `SupportOrAbove`, `AccountantOrAbove` —
which require the `admin_role` claim. Key policies:

| Category | Policy | Allowed |
|----------|--------|---------|
| Orders | `CanViewPagedOrder` | Employee or any administrator (the partner hosts) |
| Orders | `CanViewPagedOrderAdmin` | Support or above (the admin host's own list — every order of the company) |
| Orders | `CanTakeOrder` | Employee |
| Orders | `CanStartOrder` | Employee |
| Orders | `CanCompleteOrder` | Employee |
| Orders | `CanUploadOrderPhoto` | Employee |
| Orders | `CanSubmitOrderReview` | Customer |
| Users | `CanGetCurrentUser` | Authenticated (all) |
| Employees | `CanApproveEmployee` | Support or above |
| Payroll | `CanViewPagedInvoicesAdmin` | Accountant or above |
| Admin | `CanViewAdminUsers` | Manager or above |
| Admin | `CanSetAdminRole` | Administrator |
| Company | `CanViewCompanyLifecycle`, `CanViewTenantConfigurations`, `CanViewLegalDocuments` | Administrator |

Policies are enforced using the `[Permission(Policy.XYZ)]` attribute on controller actions. A read the admin
host shares with a partner host has an `…Admin` twin when the role must gate it, so a cleaner's own reads
stay open. The full row-by-row table is [ADR-0066 D3](/decisions/adr-0066); who has what, by area, is on
the [AdminRoleGate](/domain/roles/admin-role-gate) card.

### RequireCompleteProfile Filter

Certain partner-facing endpoints are additionally protected by the `[RequireCompleteProfile]` action filter. This filter checks whether the authenticated employee has completed their registration (profile, availability, documents, and admin approval). If the employee's profile is incomplete, the filter short-circuits the request and returns **403 Forbidden**.

Protected endpoint groups:
- **Order** endpoints (browse, take, start, complete)
- **Dashboard** endpoints
- **Payroll** endpoints
- **Dispute** endpoints

## CORS Configuration

CORS origins are set per environment in `appsettings.json`:

```json
{
  "CorsOrigins": [
    "http://localhost:4200",
    "http://localhost:4201",
    "https://partner.cleansia.cz"
  ]
}
```

Production origins (`appsettings.Production.json`):

```json
{
  "CorsOrigins": [
    "https://partner.cleansia.cz",
    "https://cleansia.cz"
  ]
}
```

## Error Responses

All auth endpoints return RFC 7807 Problem Details on failure:

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "Bad Request",
  "status": 400,
  "errors": {
    "Email": ["user.not_existing_email"]
  }
}
```

| Status | Meaning |
|--------|---------|
| `200` | Success |
| `400` | Validation error (see `errors` object) |
| `401` | Missing or expired token |
| `403` | Insufficient permissions |
