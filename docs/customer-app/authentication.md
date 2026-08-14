# Customer Authentication

The customer app supports two authentication methods: **email/password** login and **Google OAuth**. Authentication is managed by the `CustomerAuthService` and the `LoginFacade`.

## Login Flow

### Email + Password

1. User enters email and password on `/login`
2. `LoginFacade.login()` calls `CustomerAuthService.login(email, password, rememberMe)`
3. The server issues the **access and refresh tokens as HttpOnly cookies** via `Set-Cookie`. The
   response body carries only what JavaScript is allowed to see: `csrfToken`, `role`,
   `refreshTokenExpiresAt` and `isEmailConfirmed`
4. If `isEmailConfirmed` is `false`, user is redirected to `/confirm-email?email=...`
5. If confirmed, `CustomerAuthService.setSession(authResult)` persists those JS-readable companions
6. Guest order data is cleared via `GuestOrderService.clear()`
7. NgRx action `loadCustomerUser()` is dispatched to fetch user profile
8. User is redirected to `/orders`

```typescript
// Login form fields
formGroup = new FormGroup({
  email: new FormControl('', [Validators.required, Validators.email]),
  password: new FormControl('', [Validators.required]),
  rememberMe: new FormControl(false),
});
```

### Google OAuth

1. Google Sign-In SDK (`accounts.google.com/gsi/client`) is loaded dynamically
2. A "Continue with Google" button is rendered via `google.accounts.id.renderButton()`
3. On callback, the Google `credential` (JWT) is decoded client-side to extract `googleId`, `email`, `firstName`, `lastName`
4. `LoginFacade.googleLogin(credential)` calls `CustomerAuthService.authenticateWithGoogle()`
5. Backend validates the Google token, creates/links the account, and returns a `JwtTokenResponse`
6. Session is stored and user is redirected to `/orders`

::: info Google Client ID
The Google OAuth client ID is configured in the environment file:
```
354682423254-boe1nlnb1dbd3m6a013d3nkpo2e9bgiq.apps.googleusercontent.com
```
:::

::: warning SSR Safety
The Google Sign-In initialization uses `isPlatformBrowser` to prevent server-side execution. The SDK script is loaded lazily with retry logic (up to 20 retries at 300ms intervals).
:::

## Registration

The registration flow (`/register`) collects:
- First name, last name
- Email address
- Password (with confirmation)
- Phone number

After successful registration, an email confirmation is sent and the user is redirected to `/confirm-email`.

## Email Confirmation

The `/confirm-email` page accepts a token via query parameter. When the page loads, it sends the token to the backend for verification. On success, the user can proceed to login.

## Password Reset

1. User navigates to `/forgot-password`
2. Enters their email address
3. Backend sends a reset link via email
4. User clicks the link, enters a new password
5. On success, redirected to `/login`

## Session tokens {#session-tokens}

**The web customer app never holds a token in JavaScript.** Both the access and the refresh token are
HttpOnly cookies set by the server, so an XSS payload cannot read or exfiltrate either one. This is the
security property of the design — do not add a token to `localStorage`, to a service field, or to an
`Authorization` header on this client.

`setSession()` persists only the three JS-readable companions: `role` (a UI hint for permission
gating — the server remains the source of truth), `refreshTokenExp` (so the app can tell a live session
from an expired one without a round-trip), and `csrfToken`.

The interceptor chain therefore does two things on every request:

1. Sends `withCredentials: true`, which carries the HttpOnly auth cookie and lets the browser accept
   `Set-Cookie` on the way back. **Required end to end — omit it and the request is anonymous.**
2. On a state-changing method, echoes the stored CSRF token as `X-CSRF-Token`. The server verifies it
   against the cookie's half — a **double-submit pair**, which is why the CSRF token is deliberately
   JS-readable while the auth token is not.

Mobile is the other shape: no cookies, `Authorization: Bearer`, `csrfToken` null — bearer auth is not
forgeable by CSRF, so it needs no second factor. Do not read this page for mobile behaviour.
3. Retries the failed request with the new token

## Guards

### `customerAuthGuard`

Protects routes that require authentication (orders, profile, disputes). Checks if the user has a valid session via `CustomerAuthService.isLoggedIn()`. Redirects to `/login` if not authenticated.

### `customerGuestGuard`

Prevents authenticated users from accessing login/register pages. Redirects to `/orders` if already logged in.

**Route configuration:**

```typescript
{
  path: 'login',
  loadChildren: () => import('@cleansia-customer/login').then(m => m.loginRoutes),
  canActivate: [customerGuestGuard],
},
{
  path: 'orders',
  loadChildren: () => import('@cleansia-customer/orders').then(m => m.ordersRoutes),
  canActivate: [customerAuthGuard],
},
```

## Session Management

- `isLoggedIn` -- a signal, backed by `hasValidSession()`: a CSRF token exists **and** the persisted
  refresh expiry is still in the future. JS cannot observe the auth cookie, so session existence is
  inferred from those two rather than read directly
- `setSession(authResult)` -- persists `role`, `refreshTokenExp` and `csrfToken` (see
  [Session tokens](#session-tokens))
- `logout()` -- POSTs so the server can revoke the refresh token it reads from the cookie, then wipes
  local state **even if that call fails**: the user's intent is unambiguous
- Nothing decodes the JWT. It is HttpOnly, so decoding is impossible by design; `role` is emitted in
  the response body precisely so no client needs to

::: tip
The order wizard works for both authenticated and guest users. Authenticated users get their profile data pre-filled; guest users must enter contact info manually. Guest orders are tracked via `GuestOrderService` using localStorage.
:::
