# Authentication

Cleansia uses JWT bearer tokens for API authentication with role-based access control (RBAC). The system supports email/password login and Google OAuth.

## Architecture

| Component | Details |
|-----------|---------|
| Token type | JWT (HS256) |
| Token storage | Client-side (cookie or header) |
| Default expiration | 6 hours |
| Remember me expiration | 1 hour (cookie-based) |
| Rate limiting | `auth` policy on all auth endpoints |
| Identity | Custom user model with `UserProfile` enum |

::: info Source Files
- Token generation: `src/Cleansia.Core.AppServices/Services/TokenService.cs`
- JWT config: `src/Cleansia.Infra.Common/Configuration/Interfaces/IJwtSettings.cs`
- Auth handlers: `src/Cleansia.Core.AppServices/Features/Auth/`
- Partner API controller: `src/Cleansia.Web/Controllers/AuthController.cs`
- Mobile API controller: `src/Cleansia.Web.Mobile/Controllers/AuthController.cs`
:::

## Endpoints

All auth endpoints are rate-limited with the `auth` policy and are accessible without authentication (`[AllowAnonymous]`).

### Register

Creates a new user account and sends a confirmation email.

```
POST /api/Auth/Register
```

**Request body:**

```json
{
  "email": "user@example.com",
  "password": "SecureP@ss123",
  "firstName": "John",
  "lastName": "Doe",
  "language": "en"
}
```

**Response:** `200 OK` with `true` on success.

**Validation rules:**
- Email must be valid and not already confirmed
- First/last name required
- Password must meet complexity requirements
- Language must reference a valid language record

::: tip Re-registration
If a user exists but hasn't confirmed their email, calling Register again refreshes the confirmation code and resends the email.
:::

---

### Register Employee

Registers a user specifically as an employee/partner.

```
POST /api/Auth/RegisterEmployee
```

**Request body:** Same structure as Register.

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
  "idToken": "google-oauth-id-token"
}
```

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
    "DefaultTokenExpHours": 6,
    "CookieTokenExpHours": 1
  }
}
```

| Setting | Description |
|---------|-------------|
| `Secret` | HMAC-SHA256 signing key (min 32 chars). Stored in Key Vault for deployed environments. |
| `DefaultTokenExpHours` | Expiration for standard login (default: 6h) |
| `CookieTokenExpHours` | Expiration when `rememberMe: true` (default: 1h) |

::: warning
The `Secret` value must **never** be committed to source control. Use `dotnet user-secrets` for local development and Azure Key Vault references for deployed environments.
:::

## JWT Claims

The token includes standard claims set from the `User` entity via `user.SetClaims()`:

| Claim | Source |
|-------|--------|
| `sub` (NameIdentifier) | `User.Id` |
| `email` | `User.Email` |
| `role` | `User.Profile` (Customer, Employee, Admin, SuperAdmin) |

## RBAC Policies

Authorization policies are defined in `src/Cleansia.Core.AppServices/Authentication/Policy.cs`. Key policies:

| Category | Policy | Allowed Roles |
|----------|--------|---------------|
| Orders | `CanViewPagedOrder` | Admin, Employee |
| Orders | `CanTakeOrder` | Employee |
| Orders | `CanStartOrder` | Employee |
| Orders | `CanCompleteOrder` | Employee |
| Orders | `CanUploadOrderPhoto` | Employee |
| Orders | `CanSubmitOrderReview` | Customer |
| Users | `CanGetCurrentUser` | Authenticated (all) |
| Employees | `CanApproveEmployee` | Admin |
| Admin | `CanViewAdminUsers` | SuperAdmin |

Policies are enforced using the `[Permission(Policy.XYZ)]` attribute on controller actions.

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
