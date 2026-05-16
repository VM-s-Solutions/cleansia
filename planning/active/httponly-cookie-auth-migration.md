# HttpOnly Cookie Auth Migration — Spec

**Status:** Spec only. Not started.
**Severity:** MEDIUM (mitigates XSS-driven session theft).
**Carry-over from:** Phase D audit (see `refactor-plan.md` §D.3 "Local storage of sensitive data").

## The problem

The 3 web apps (customer / partner / admin) store the access + refresh tokens
in browser cookies set from JavaScript. JS-set cookies cannot have the
`HttpOnly` flag — that flag can only be applied by the server via a
`Set-Cookie` response header. Consequence: any XSS on the domain can read
`document.cookie` and exfiltrate the user's session tokens.

Native mobile (Android) is not affected — tokens live in
`EncryptedSharedPreferences` backed by the Android Keystore, separate from
web's surface entirely.

## Current shape

```
Login flow:
  Client → POST /api/auth/Login (email, password)
  Server → 200 OK { token, refreshToken, refreshTokenExpiresAt, ... }
  Client → setCookieValue('customer_token', token, ...)
           setCookieValue('customer_refresh_token', refreshToken, ...)
           localStorage.setItem('customer_refresh_exp', exp)

Authenticated call:
  Client (auth.interceptor) → reads cookie → adds 'Authorization: Bearer <token>'

Refresh flow:
  Client (error.interceptor) → 401 → POST /api/auth/RefreshToken { token: refresh }
  Server → 200 OK { token, refreshToken, refreshTokenExpiresAt }
  Client → setCookieValue(...)  (rotation)

Logout:
  Server → POST /api/auth/Logout { token: refresh }  (revokes server-side)
  Client → removeCookieValue(...) (forgets locally)
```

Note: cookies are set with `Secure;SameSite=Strict` already. Strict samesite
prevents cross-site reads (CSRF on auth), but does NOT prevent same-origin
XSS reads.

## Target shape

Server controls the auth cookies via response headers; client never reads or
sets them. Bearer tokens move out of `Authorization` header to come up via
the HTTP cookie automatically.

```
Login flow:
  Client → POST /api/auth/Login (email, password)         [credentials: 'include']
  Server → 200 OK
           Set-Cookie: customer_token=...; HttpOnly; Secure; SameSite=Strict
           Set-Cookie: customer_refresh_token=...; HttpOnly; Secure; SameSite=Strict
           Body: { csrfToken, refreshTokenExpiresAt, userProfile }

Authenticated call:
  Client → fetch(..., { credentials: 'include' })
           sends auth cookie automatically; auth.interceptor ONLY adds
           the X-CSRF-Token header (read from a non-HttpOnly companion
           cookie or from a Login-response field stored in memory).

Refresh flow:
  Client (error.interceptor) → 401 → POST /api/auth/RefreshToken (cookie carries refresh)
                                     X-CSRF-Token header
  Server → 200 OK
           Set-Cookie (rotation, two cookies)
           Body: { csrfToken, refreshTokenExpiresAt }

Logout:
  Server → POST /api/auth/Logout (cookie carries refresh)
                                  X-CSRF-Token header
  Server → 200 OK
           Set-Cookie: customer_token=; Max-Age=0; ...
           Set-Cookie: customer_refresh_token=; Max-Age=0; ...
```

## Why CSRF protection is required

`SameSite=Strict` blocks cross-site request forgery in modern browsers, BUT
older browsers, mis-configured CORS, and same-site-but-different-subdomain
scenarios can still allow CSRF. Industry standard pairs HttpOnly auth
cookies with **double-submit CSRF tokens**:

1. Server issues a CSRF token (non-HttpOnly cookie OR Login-response field).
2. Client sends it back as `X-CSRF-Token` header on every state-changing call.
3. Server validates header matches cookie / matches session.
4. Cross-site attackers can't forge the header (CORS blocks reading the cookie).

Without CSRF protection, switching to HttpOnly cookies is a security
DOWNGRADE for state-changing endpoints (any cross-site form POST would
auto-send the auth cookie).

## Scope

### Backend changes

1. **Auth controllers — return cookies, not body tokens.**
   `AuthController.Login`, `RefreshToken`, `GoogleAuth`, `ConfirmUserEmail`
   currently return `JwtTokenResponse { token, refreshToken, ... }`. After
   migration: set `token` + `refreshToken` as `Set-Cookie` headers, return
   only `{ refreshTokenExpiresAt, csrfToken, userProfile }` in the body.

2. **JWT bearer middleware — read from cookie.**
   ASP.NET's `AddJwtBearer` reads the `Authorization` header by default.
   Need to add an `OnMessageReceived` handler that falls back to reading
   the cookie when the header is absent:
   ```csharp
   options.Events = new JwtBearerEvents {
       OnMessageReceived = ctx => {
           if (string.IsNullOrEmpty(ctx.Token)
               && ctx.Request.Cookies.TryGetValue("customer_token", out var c)) {
               ctx.Token = c;
           }
           return Task.CompletedTask;
       },
   };
   ```
   Cookie name varies per host (`customer_token` / `partner_token` /
   `admin_token`) — wire from configuration.

3. **CSRF middleware.** Validate `X-CSRF-Token` header against a server-side
   companion (either a separate non-HttpOnly cookie, or a per-session value
   in the JWT itself). ASP.NET has `Antiforgery` middleware for forms but
   API style is double-submit. Recommend writing a small custom middleware
   that runs after auth, checks the header on `POST`/`PUT`/`DELETE`/`PATCH`.

4. **CORS** — add `AllowCredentials()` to the policy. Without it browsers
   refuse to send cookies cross-origin. Existing policy uses a fixed origin
   list (good — `AllowAnyOrigin` is incompatible with credentials).

5. **Logout — set expired cookies in response.** `AuthController.Logout`
   currently just revokes server-side; needs to also emit
   `Set-Cookie: customer_token=; Max-Age=0; ...` to clear the browser.

### Frontend changes

1. **Drop `auth.interceptor.ts`'s `Authorization: Bearer` header.** Cookie
   auto-attaches; nothing for the client to do. KEEP the file but rewrite
   it to attach `X-CSRF-Token` instead.

2. **Set `withCredentials: true` on HttpClient.** Either globally via
   `provideHttpClient(withCredentialsInterceptor())` or per-request via the
   existing interceptor. NSwag's generated clients use `HttpClient`
   directly, so the interceptor approach is required.

3. **Drop cookie write/read from `customer-auth.service.ts` /
   `partner-auth.service.ts` / `admin-auth.service.ts`.** The auth service
   becomes much simpler:
   - No `setCookieValue` / `extractCookieValue` calls.
   - `getToken()` is gone (no client-side token access).
   - `isLoggedIn()` is no longer derivable from cookie presence — depends on
     either an in-memory `WritableSignal<boolean>` set by login/logout
     handlers, or a server-side `/api/auth/Session` ping.
   - `hasValidRefreshToken()` is also gone — server decides.

4. **Refresh flow** — `error.interceptor.ts` keeps the single-flight pattern
   but the refresh call no longer needs to pass the refresh token in the
   body (cookie carries it). Response no longer contains a `newToken` to
   stash — the new cookie is already set by the server. The retried
   request will just send the new cookie.

5. **`AUTH_COOKIE_KEYS` token** — becomes obsolete. The server determines
   cookie names; client just sends them all back via `credentials: 'include'`.

### Mobile changes — NONE

Mobile uses `EncryptedSharedPreferences` + `Authorization: Bearer` header.
Not on this migration path; stays as-is.

## Open questions

1. **CSRF token delivery.** Two reasonable options:
   - **Non-HttpOnly companion cookie** (`customer_csrf=...; Secure; SameSite=Strict`)
     plus an interceptor that reads it and sets the header. Simple but adds
     a third cookie to manage.
   - **Login-response body field** stored in JS memory (`signal<string|null>`).
     Survives in-tab but lost on tab close — paired with a refresh that
     returns a new csrfToken too. More architectural but cleaner.

   Prefer option 2 — keeps the cookie surface minimal.

2. **SSR support.** The customer app does SSR. The server-side render needs
   to forward the incoming cookies to its API calls. Angular Universal +
   `withFetch()` already supports cookie passthrough; verify.

3. **Subdomain consideration.** If the apps are ever deployed under
   `customer.cleansia.cz` / `partner.cleansia.cz` / `admin.cleansia.cz`,
   cookie `Domain` attribute needs to be `cleansia.cz` (parent) OR
   each app needs its own backend at its own subdomain. Currently the
   prod URLs are all under one apex; verify the deploy plan.

4. **API versioning.** Migration is a hard cut — server can't simultaneously
   issue cookies AND body tokens because the security model differs. Either
   ship to all 3 web apps in one go, or feature-flag the auth endpoints.

## Migration order

1. Backend: add cookie-fallback to JWT middleware. Doesn't break anything;
   header path still works.
2. Backend: add CSRF middleware behind a feature flag (off).
3. Backend: change `Login` / `RefreshToken` / etc. response shapes to also
   set cookies (in addition to returning body tokens — overlap period).
4. Frontend: add `withCredentials: true` interceptor.
5. Frontend: switch interceptor to attach `X-CSRF-Token` instead of
   `Authorization: Bearer`.
6. Backend: enable CSRF middleware feature flag.
7. Frontend: drop body-token storage; rely on cookies + memory CSRF.
8. Backend: stop returning body tokens.

Roughly 1 week of focused work + a careful staged rollout.

## Out of scope

- HttpOnly migration for Functions / webhooks — server-to-server, no XSS surface.
- HttpOnly migration for mobile — N/A.
- SAML / OAuth federation flows — these are largely cookie-based already.

---

## Implementation status — Steps 1–5, 7, 8 shipped. Step 6 awaiting deploy.

| Step | What it changes | Status |
|---|---|---|
| 1 | JWT cookie-fallback in 3 web hosts | ✅ Done |
| 2 | CSRF middleware + AllowCredentials + Csrf:Enabled flag (default `false`) | ✅ Done |
| 3 | Auth controllers issue Set-Cookie + return csrfToken alongside body tokens | ✅ Done |
| 4 | Frontend `withCredentials: true` on every same-API request | ✅ Done |
| 5 | Frontend reads csrfToken, sends `X-CSRF-Token` header on POST/PUT/PATCH/DELETE | ✅ Done |
| 6 | Flip `Csrf:Enabled: true` in appsettings (per env) | ⏳ Owner / deploy-time |
| 7 | Frontend stops persisting body tokens — auth services + interceptors fully cookie-based | ✅ Done |
| 8 | Backend stops returning body tokens — cookies are the sole carrier | ✅ Done (web only — Mobile keeps body tokens) |

### Owner prerequisites before flipping Csrf:Enabled

1. **`Csrf:Secret` must be set** in user-secrets / environment for each of the
   3 web hosts. The HMAC key. Generate ≥32 random bytes, base64-encode, set
   as `Csrf__Secret`. **Without this the middleware can't derive expected
   tokens and every state-changing request will 403.**
2. **NSwag clients should be regenerated** so `JwtTokenResponse.csrfToken` is
   a typed property in the customer/partner/admin TS DTOs. Runtime works
   without regen (NSwag constructor copies unknown properties through), but
   typed access is cleaner. Run:
   ```
   npm run generate-customer-client
   npm run generate-partner-client
   npm run generate-admin-client
   ```
3. **In-flight sessions will need to re-login** the first time CSRF goes
   live — existing tokens were issued before csrfToken was a field. The
   user's next state-changing request will 403; the error interceptor's
   refresh path heals it (refresh response carries a new csrfToken).
   Flag this in the release notes.

### SSR consideration (customer app)

The customer app uses Angular SSR. The interceptor runs server-side too;
`withCredentials` is a no-op on the server (no browser to carry cookies).
The current SSR boundary doesn't issue authenticated state-changing
requests during pre-render — auth-gated routes are CSR. Verify in staging
that SSR pages don't trip the CSRF middleware (they shouldn't — SSR makes
only GETs to API).

### Step 6 rollout sequence

1. Set `Csrf:Secret` user-secrets in **dev** first.
2. Flip `Csrf:Enabled: true` in `appsettings.Development.json` for one
   web host (Customer first — smallest blast radius if it breaks).
3. Exercise login + state-changing operations (place order, save profile,
   add address). Verify `X-Csrf-Failure` header is **not** appearing on
   responses and that POSTs succeed.
4. Roll the same to Partner + Admin dev.
5. Set production `Csrf:Secret` in Azure App Settings.
6. Flip `appsettings.Production.json` for one host, redeploy that host only,
   monitor 403 rate for 30 min.
7. Roll the remaining two hosts.

### NSwag regeneration status

Step 7+8 added a `Role` field to `JwtTokenResponse.cs`. Each TS client picks
it up only after `npm run generate-<host>-client` runs against a backend
that has the new field.

**Twist:** all three auth services (`customer-auth.service.ts`,
`partner-auth.service.ts`, `admin-auth.service.ts`) import
`JwtTokenResponse` from `@cleansia/partner-services` — that's a pre-existing
shared-DTO source. So the **partner** client's regen is what unlocks
typed-access cleanup across all three.

- ✅ **Customer client** regenerated (`role` is typed locally).
- ⏳ **Partner client** — regen still needed. **This is the one that
  matters** for stripping defensive casts in all 3 auth services since
  they all import the partner-services `JwtTokenResponse`.
- ⏳ **Admin client** — independently uses partner-services for the auth
  DTOs, so partner regen handles it. Admin's own DTOs (admin login,
  loyalty admin, etc.) are a separate concern.

Defensive casts (`(authResult as unknown as { role?: string }).role`) still
in place in:

- `libs/core/customer-services/src/lib/services/customer-auth.service.ts:setSession`
- `libs/core/partner-services/src/lib/services/partner-auth.service.ts:setSession`
- `libs/core/admin-services/src/lib/services/admin-auth.service.ts:setSession`

After `npm run generate-partner-client`, replace each with `authResult.role`.
Runtime works fine in all cases — NSwag's constructor copies any extra
props through, so the wire payload arrives correctly. The regen only
unlocks typed access (compile-time check that the field exists).

### Step 8 deployment notes

Backend now returns `Token = ""` and `RefreshToken = null` in
`JwtTokenResponse` bodies from the 3 web hosts (`AuthCookieWriter.ApplyCookies`
blanks them after writing the cookies). Mobile is unaffected — Mobile's
`AuthController` does not go through `AuthCookieWriter` and keeps emitting
body tokens for `EncryptedSharedPreferences` storage.

`Logout.Command` and `RefreshToken.Command` controllers now read the
refresh token from the HttpOnly cookie before invoking MediatR — clients
send `{ token: '' }` in the body. `Logout.Validator` is empty (no
NotEmpty rule on Token) so logout-with-no-session is a no-op success.
