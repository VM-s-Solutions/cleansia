# Web Auth Implementation Current State

## Customer App (`apps/cleansia.app/`)

### 1. Auth Interceptor
- **File:** `libs/core/customer-services/src/lib/interceptors/auth.interceptor.ts`
- **Token source:** `CustomerAuthService.getToken()` → reads from **cookie** (`customer_token`)
- **Approach:** Reads token synchronously, adds `Authorization: Bearer <token>` header to `/api/*` requests
- **401 handling:** No, handled separately in `CustomerErrorInterceptorFn`

### 2. Token Storage
- **Storage type:** **Cookie** + localStorage (hybrid)
- **Cookie key:** `customer_token`
- **localStorage key for role:** `customer_role` (hardcoded in service)
- **NgRx state:** No auth state currently; just BehaviorSubject `isLoggedIn$`

### 3. Login Flow
- **Service:** `CustomerAuthService.login(email, password, rememberMe)`
- **Client call:** `customerClient.authClient.login(LoginCommand)`
- **Token handling:** Response calls `setSession(authResult)` → setCookieValue + localStorage role
- **Post-login route:** `CleansiaCustomerRoute.ORDERS` (or confirm email if not confirmed)

### 4. Logout Flow
- **Trigger:** `CustomerAuthService.logout()`
- **Behavior:** Calls `removeSession()` → removes cookie + clears `isLoggedIn$` → navigate to login
- **Backend call:** None (logout is local only)

### 5. App Bootstrap / Token Hydration
- **APP_INITIALIZER:** Only `initializeTranslations` (no auth hydration)
- **Token hydration:** None. Token is read fresh from cookie on each `getToken()` call
- **Startup check:** `isLoggedIn()` validates token expiration by decoding JWT

### 6. NSwag Client
- **File:** `libs/core/customer-services/src/lib/client/customer-client.ts`
- **Methods exposed:** ✓ `login()`, ✓ `refreshToken()`, ✓ `logout()`, ✓ `googleAuth()`, ✓ `confirmUserEmail()`
- **Auth client interface:** `IAuthClient` with all 5+ methods

---

## Partner App (`apps/cleansia-partner.app/`)

### 1. Auth Interceptor
- **File:** `libs/core/partner-services/src/lib/interceptors/auth.interceptor.ts`
- **Token source:** `PartnerAuthService.getToken()` → reads from **cookie** via `LocalStorageKey.TOKEN`
- **Approach:** Checks token existence, adds header only if present
- **401 handling:** No, Partner app has NO error interceptor (unlike Customer)

### 2. Token Storage
- **Storage type:** **Cookie** only
- **Cookie key:** `token` (from `LocalStorageKey.TOKEN` enum)
- **localStorage key for role:** `role` (from `LocalStorageKey.ROLE` enum)
- **NgRx state:** No auth state; BehaviorSubject `isLoggedIn$` + `isLoggedInAction$`

### 3. Login Flow
- **Service:** `PartnerAuthService.login(email, password, rememberMe)`
- **Client call:** `partnerClient.authClient.login(PartnerLoginCommand)`
- **Token handling:** `setSession(authResult)` → setCookieValue + localStorage role
- **Post-login route:** `CleansiaPartnerRoute.ORDERS` (or confirm email)

### 4. Logout Flow
- **Trigger:** `PartnerAuthService.logout()`
- **Behavior:** `removeSession()` → removes cookie + clears `isLoggedIn$` → navigate to login
- **Backend call:** None

### 5. App Bootstrap / Token Hydration
- **APP_INITIALIZER:** Only `initializeTranslations` (no auth hydration)
- **Token hydration:** None. Fresh cookie read on demand.
- **Startup check:** Token expiration validated by `isLoggedIn()`

### 6. NSwag Client
- **File:** `libs/core/partner-services/src/lib/client/partner-client.ts`
- **Methods exposed:** ✓ `login()`, ✓ `refreshToken()`, ✓ `logout()`, ✓ `googleAuth()`, ✓ `confirmUserEmail()`, ✓ `registerEmployee()`
- **Auth client interface:** `IAuthClient` with all methods

---

## Admin App (`apps/cleansia-admin.app/`)

### 1. Auth Interceptor
- **File:** `libs/core/admin-services/src/lib/interceptors/auth.interceptor.ts`
- **Token source:** `AdminAuthService.getToken()` → reads from **cookie** via `LocalStorageKey.TOKEN`
- **Approach:** Identical to Partner (sync read, header injection)
- **401 handling:** No, Admin has NO error interceptor

### 2. Token Storage
- **Storage type:** **Cookie** only
- **Cookie key:** `token` (from `LocalStorageKey.TOKEN` enum)
- **localStorage key for role:** `role` (from `LocalStorageKey.ROLE` enum)
- **NgRx state:** No auth state; BehaviorSubject `isLoggedIn$` + `isLoggedInAction$`

### 3. Login Flow
- **Service:** `AdminAuthService.login(email, password, rememberMe)`
- **Client call:** `adminClient.adminAuthClient.login(AdminLoginCommand)`
- **Post-login:** Checks `hasAdminAccess` flag; routes to `CleansiaAdminRoute.EMPLOYEE_MANAGEMENT`
- **Token handling:** `setSession(authResult)` → setCookieValue + localStorage role

### 4. Logout Flow
- **Trigger:** `AdminAuthService.logout()`
- **Behavior:** `removeSession()` → removes cookie + clears `isLoggedIn$` → navigate to login
- **Backend call:** None

### 5. App Bootstrap / Token Hydration
- **APP_INITIALIZER:** Only `initializeTranslations` (no auth hydration)
- **Token hydration:** None. Fresh cookie read on demand.
- **Startup check:** Token expiration validated by `isLoggedIn()`

### 6. NSwag Client
- **File:** `libs/core/admin-services/src/lib/client/admin-client.ts`
- **Methods exposed:** ✓ `login()`, ✓ `refreshToken()`, ✓ `logout()` (no googleAuth or email confirm)
- **Auth client interface:** `IAdminAuthClient` (minimal, admin-only)

---

## Error Handling Summary

| App      | Error Interceptor | 401 Behavior |
|----------|-------------------|--------------|
| Customer | ✓ YES             | `removeSession()` → redirect to login |
| Partner  | ✗ NO              | No 401 handling (error thrown) |
| Admin    | ✗ NO              | No 401 handling (error thrown) |

---

## Token Key Differences

- **Customer:** Custom `customer_token` (cookie) + `customer_role` (localStorage)
- **Partner:** Standard `token` (cookie) + `role` (localStorage) via `LocalStorageKey` enum
- **Admin:** Standard `token` (cookie) + `role` (localStorage) via `LocalStorageKey` enum
