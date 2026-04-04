# Customer Authentication

The customer app supports two authentication methods: **email/password** login and **Google OAuth**. Authentication is managed by the `CustomerAuthService` and the `LoginFacade`.

## Login Flow

### Email + Password

1. User enters email and password on `/login`
2. `LoginFacade.login()` calls `CustomerAuthService.login(email, password, rememberMe)`
3. Backend returns a `JwtTokenResponse` containing:
   - `accessToken` -- JWT access token
   - `refreshToken` -- Refresh token
   - `isEmailConfirmed` -- Whether email is verified
4. If `isEmailConfirmed` is `false`, user is redirected to `/confirm-email?email=...`
5. If confirmed, `CustomerAuthService.setSession(authResult)` stores the tokens
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

## JWT Token Management

Tokens are stored via `CustomerAuthService.setSession()`:

- **Access token** -- Used in Authorization header via HTTP interceptor
- **Refresh token** -- Used to obtain new access tokens when expired
- The `rememberMe` flag determines storage persistence

The `CUSTOMER_INTERCEPTORS_FN` interceptor chain automatically:
1. Attaches the `Authorization: Bearer <token>` header to API requests
2. Handles 401 responses by attempting token refresh
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

- `CustomerAuthService.isLoggedIn()` -- Checks for valid stored tokens
- `CustomerAuthService.setSession(authResult)` -- Stores tokens from login response
- `CustomerAuthService.logout()` -- Clears stored tokens and state
- Token decoding uses the `jwt-decode` library

::: tip
The order wizard works for both authenticated and guest users. Authenticated users get their profile data pre-filled; guest users must enter contact info manually. Guest orders are tracked via `GuestOrderService` using localStorage.
:::
