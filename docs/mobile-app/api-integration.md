# API Integration

Both mobile platforms talk to dedicated **mobile API hosts** — never to the web hosts — and both
build their networking the same way: a **generated** typed client for the business endpoints, and a
**hand-written** auth/session/header spine that the generator cannot express.

| App | Host project | Local port |
|---|---|---|
| Partner (Android + iOS) | `Cleansia.Web.Mobile.Partner` | `5002` |
| Customer (Android + iOS) | `Cleansia.Web.Mobile.Customer` | `5004` |

::: info Body token, never cookie
The web hosts authenticate with an HttpOnly cookie plus an `X-CSRF-Token` echo. A native client
cannot read an HttpOnly cookie, so the mobile hosts return the access and refresh tokens **in the
JSON response body** for the client to store in `EncryptedSharedPreferences` (Android) or the
Keychain (iOS). There is **no CSRF token on mobile** — `CsrfToken` comes back `null` and there is
nothing to echo, because a `Bearer` header is not forgeable by CSRF. Refresh and logout carry the
refresh token in the **request body**.
:::

The out-of-band rules that the OpenAPI document does *not* describe — the custom headers, the
no-`Bearer`-on-anonymous rule, refresh rotation with theft detection, and the empty-token
unconfirmed-email login — are specified once, for both platforms, in
**`src/cleansia_ios/docs/header-parity-contract.md`**. Read it before changing anything on this page's
subject matter; it cites the backend sources for every rule.

## Where the base URL comes from

::: code-group

```kotlin [Android]
// Baked into BuildConfig per build type by each app's build.gradle.kts.
// Override for every build type without editing a build file:
//   ./gradlew :partner-app:installDebug  -PAPI_BASE_URL=http://10.0.2.2:5002/
//   ./gradlew :customer-app:installDebug -PAPI_BASE_URL=http://10.0.2.2:5004/
Retrofit.Builder()
    .baseUrl(BuildConfig.API_BASE_URL.ensureTrailingSlash())
```

```swift [iOS]
// project.yml `info.properties` → the GENERATED Info.plist → read back at launch.
// To change it, edit project.yml and re-run `xcodegen generate` — NEVER Info.plist.
enum AppConfig {
    static let apiBaseURL: URL = {
        guard
            let raw = Bundle.main.object(forInfoDictionaryKey: "API_BASE_URL") as? String,
            let url = URL(string: raw)
        else {
            fatalError("API_BASE_URL missing or malformed in Info.plist")
        }
        return url
    }()
}
```

:::

Both platforms default to the Azure DEV host so a fresh clone of either one hits the same backend
with no local setup. Full table of defaults per build type: [Overview](/mobile-app/overview#android).

## The generated clients

### Two committed specs, one contract

Android and iOS generate from the **same two committed OpenAPI documents**, which live in the
Android tree:

| Spec | App | Android output | iOS output |
|---|---|---|---|
| `src/cleansia_android/openapi/partner-mobile-api.json` | Partner | `cz.cleansia.partner.api.*` under `build/generated/openapi/` | SPM package `CleansiaPartnerApi/` |
| `src/cleansia_android/openapi/customer-mobile-api.json` | Customer | `cz.cleansia.customer.api.*` under `build/generated/openapi/` | SPM package `CleansiaCustomerApi/` |

The **specs are committed artifacts**; the **generated clients are not**. Android's output lands
under `build/` and iOS's `Cleansia{Partner,Customer}Api/` directories are gitignored. Both platforms
pin **openapi-generator 7.10.0** — Android through the Gradle plugin version in
`libs.versions.toml`, iOS through a checksum-verified jar in `ios-ci.yml` — so one backend contract
produces clients that cannot drift apart across platforms.

::: danger Refreshing a spec is an owner-only step; never hand-edit generated output
Re-dumping `partner-mobile-api.json` / `customer-mobile-api.json` from a running host is
**`manual_step: mobile-spec-regen`** — the owner does it. Do not run a regeneration as part of
ordinary work, and do not hand-edit the generated client to make a shape fit: the next run silently
overwrites it. Change the spec (owner) or the generator config, then regenerate.

Generating rather than hand-writing is what makes spec drift *loud*: an endpoint the spec does not
carry, or a field whose shape changed, becomes a **compile error** instead of the silent runtime
mismatch that produced "blank screen" bugs when the DTOs were hand-written.
:::

### Per-platform generator setup

| | Android | iOS |
|---|---|---|
| Generator | `kotlin`, library `jvm-retrofit2` | `swift5`, library `urlsession` (async/await) |
| Serialization | kotlinx.serialization, `enumPropertyNaming = UPPERCASE` | `Codable` |
| Config | `openApiGenerate { … }` in each app's `build.gradle.kts` | `openapi/openapi-generator-config.{partner,customer}.yaml` |
| Invocation | automatic — every `compile*Kotlin` and `ksp*Kotlin` task `dependsOn("openApiGenerate")` | `scripts/generate-api-clients.sh [partner\|customer]`, run before `xcodegen generate` |
| Output | `build/generated/openapi/src/main/kotlin`, added to the `main` source set | a local SPM package each app target links |

The Android wiring uses `dependsOn` rather than `preBuild` because KSP and Hilt resolve sources
earlier than `preBuild` runs. iOS deliberately does **not** put the generator in an Xcode run-script
phase — a per-build codegen step is fragile — so the script is out-of-band and CI runs it explicitly.

### What is *not* generated

The **auth/session/header spine** is hand-written and excluded from codegen on both platforms:

- Android — `core/src/main/java/cz/cleansia/core/auth/`
- iOS — `CleansiaCore/Sources/CleansiaCore/Auth/`

The body-token transport, single-use refresh with theft detection, the no-`Bearer`-on-anonymous
allow-list and the empty-token unconfirmed-email gate cannot be expressed by the generated surface.
The Auth and Device endpoints in the spec are therefore out of scope for the generated client.

## Two clients, one for tokens and one for everything else

Both platforms run **two** HTTP stacks, for the same reason: if the stack that calls
`RefreshToken` also carried the 401-refresh hook, a 401 on refresh would recursively trigger another
refresh.

::: code-group

```kotlin [Android — the OkHttp chain]
// Authenticated client — every business call.
OkHttpClient.Builder()
    .addInterceptor(retryAfterInterceptor)      // OUTERMOST: 429 + Retry-After, retries once,
                                                //   so the retry re-enters auth and picks up a fresh token
    .addInterceptor(authInterceptor)            // Bearer, skipped on anonymous paths
    .addInterceptor(deviceHeadersInterceptor)   // X-Device-Id / X-Device-Label
    .addInterceptor(timeZoneInterceptor)        // X-Time-Zone, read fresh per request
    .addInterceptor(networkErrorInterceptor)    // customer app: global snackbar for IOException + 5xx
    .addInterceptor(logging)                    // HEADERS in debug, redacts Authorization
    .authenticator(authAuthenticator)           // single-flight 401 → refresh → retry once
    .build()

// No-auth client — the refresh + token-issuing endpoints ONLY.
OkHttpClient.Builder()
    .addInterceptor(deviceHeadersInterceptor)   // still needed: the server stamps RefreshToken.DeviceId
    .addInterceptor(timeZoneInterceptor)        //   from X-Device-Id at ISSUE time
    .addInterceptor(networkErrorInterceptor)    // customer app only
    .addInterceptor(logging)
    .build()                                    // deliberately NO AuthInterceptor, NO Authenticator
```

```swift [iOS — the URLSession seam]
// AuthNetworkBoundary holds a lazily-built refresh session and authed session.
// HeaderAdapter stamps the headers and decides whether a Bearer goes on at all.
public func apply(to request: inout URLRequest, accessToken: String?) {
    request.setValue(headerSafeDeviceId(), forHTTPHeaderField: "X-Device-Id")
    request.setValue(headerSafe(deviceLabel, max: 120), forHTTPHeaderField: "X-Device-Label")
    request.setValue(timeZoneIdentifier(), forHTTPHeaderField: "X-Time-Zone")

    if let accessToken, !accessToken.isEmpty, shouldAttachBearer(request.url) {
        request.setValue("Bearer \(accessToken)", forHTTPHeaderField: "Authorization")
    }
}

// GeneratedClientAuthBridge splices the spine into the GENERATED client:
// each app installs a RequestBuilderFactory subclass that calls authorize(&request)
// and wraps execute() in executeWithRetry { … } (401 → SessionRefresher → one retry).
```

:::

::: warning `X-Device-Id` must be identical everywhere it appears
It is the load-bearing header. The backend stamps the incoming `X-Device-Id` onto
`RefreshToken.DeviceId` at issue **and** across rotation, and remote "sign out this device" revokes
by **string-matching** that column against the `deviceId` the app sent to `POST /api/Device/Register`.
A second id source, a per-launch random value, or a header that drifts from the register body means
**revoke matches nothing and silently no-ops** — the revoked device keeps refreshing forever.

There is exactly one producer per platform: `cz.cleansia.core.auth.DeviceIdProvider` and
`CleansiaCore/Auth/DeviceIdProvider`. Both the header path and the push-registration path read it
from there.

This is also why the device headers ride the **no-auth** client too: token issuance happens on
anonymous endpoints, and while those headers lived on the auth interceptor alone, every Android
refresh token was issued with a null device id and could never be revoked.
:::

The three headers are ASCII-filtered and length-capped on both platforms (`X-Device-Id` 64 chars,
`X-Device-Label` 120) because HTTP header values reject non-ASCII and the server columns are
bounded. `X-Time-Zone` is read **per request** so a system time-zone change is picked up without an
app restart — the server uses it to compute day/week/month boundaries in the user's wall clock, and
without it a cleaner who finishes at 00:30 local sees the job under "yesterday".

## The anonymous allow-list

`/api/Auth/*` endpoints are anonymous and some **reject** an unexpected `Authorization: Bearer`, so
the Bearer is withheld by path match. The device and time-zone headers are still sent.

| | Android | iOS |
|---|---|---|
| Where | one shared constant, `ANON_ENDPOINTS` in `:core`'s `AuthInterceptor` | `CleansiaCore/Auth/AnonymousAllowList`, with `.partner` and `.customer` values |
| Match | `path.contains(...)`, case-insensitive | `path.lowercased().contains(...)` |

iOS carries a per-app list because the **customer host's anonymous surface is wider**: a guest can
price and place an order before signing in, so `/api/Order/Quote`, `/api/Order/CreateOrder`,
`/api/Payment/CreateOrder`, the catalogue `GetOverview` endpoints, `/api/Membership/GetPlans`,
`/api/Order/Lookup(Batch)` and `/api/Referral/Validate` are anonymous there and on no other host. Of
those, `Quote`, `CreateOrder` and `Payment/CreateOrder` are **dual-use** — anonymous *and* meaningful
for a signed-in user — so the customer allow-list marks them and the Bearer is attached when one
exists.

> `/api/Auth/Logout` is **`[Authorize]`**, not anonymous: it needs the Bearer to identify the session
> and carries the refresh token in the body to revoke it. It must never join the allow-list, and it
> must go on the **authenticated** client — sending logout on the anonymous client made it a
> guaranteed 401, so the refresh token stayed alive server-side while the user believed they had
> signed out.

**Do not diff the two allow-lists against each other and conclude one is missing paths.** Android
reaches the outcome by two mechanisms: the path skip *and* client selection — the token-issuing
`AuthApi` is bound to the no-auth Retrofit, which has no `AuthInterceptor` installed at all, so those
calls cannot carry a Bearer whatever the list says. iOS routes business traffic through one authed
session, so its list carries the full set explicitly. The authoritative, host-by-host surface is §3
of `src/cleansia_ios/docs/header-parity-contract.md` — compare against that, not against the other
platform.

## Refresh: single-use, rotating, single-flight

Every successful refresh returns a **new** refresh token and revokes the old one. Presenting a
rotated token a second time is read by the server as **theft** and revokes the entire chain, signing
the user out on every device. Two client rules follow:

1. **Always overwrite the stored refresh token** with the one in the response. Keeping the old one
   trips the theft detector on the *next* refresh.
2. **Single-flight the refresh.** N concurrent 401s must produce **one** network refresh; two
   parallel refreshes present the same rotated token twice and self-trigger the revoke. Android
   serialises inside `AuthAuthenticator` with `synchronized(this)`; iOS uses
   `actor SessionRefresher` with an `inFlight` task. Both first re-check whether the stored token
   already changed and reuse it without a round trip.

A failed refresh is **classified**, not assumed fatal. Access tokens are short-lived, so the refresh
path runs many times a day per device — a flaky network moment or a 429 from the shared anonymous
rate bucket must not sign the user out:

| Outcome | Android `RefreshResult` | iOS `RefreshCallResult` / `RefreshOutcome` | Effect |
|---|---|---|---|
| Auth rejection — HTTP 401/403, or a body containing `auth.invalid_refresh_token` / `auth.refresh_token_reused` | `Rejected` | `.rejected` → `.signedOut` | **Terminal.** Session-scoped caches cleared, token store wiped, `ForcedSignOutReason.SessionExpired` emitted |
| Everything else — IOException/timeout/DNS/TLS, 5xx, 429, any unparseable non-auth answer | `Unavailable` | `.retryable` → `.unavailable` | **Retryable.** Tokens kept, only the original request fails, the next 401 retries |
| Stored refresh token already past `refreshTokenExpiresAt` (or empty) | — | — | Straight to forced sign-out; calling the endpoint would only buy a rejection round trip |

Treating the unknown case as retryable is fail-open for the **session** only, never for access: every
API call re-validates the access token server-side, so a genuinely revoked session still reaches
nothing — it just gets signed out on the next refresh the server actually answers. The classification
rule is cross-platform by design; `RefreshResult.classifyHttpFailure` and `RefreshCallResult.classify`
cite each other in their doc comments and are tested on both platforms.

### Forced sign-out fans out automatically

A teardown clears **every** session-scoped cache without a hand-maintained list: Android iterates a
`Set<SessionScopedCache>` Hilt multibinding, iOS a `SessionScopedCacheRegistry`. Both the normal
logout path and the forced-sign-out path iterate the same set, so they cannot drift. Any repository
holding per-user state joins the set and is wiped for free.

## Result and error envelope

::: code-group

```kotlin [Android]
sealed class ApiResult<out T> {
    data class Success<T>(val data: T) : ApiResult<T>()
    data class Error(val error: ApiError) : ApiResult<Nothing>()
    // getOrNull / errorOrNull / map / onSuccess / onError
}

sealed class ApiError : Exception() {
    data class Network(override val message: String) : ApiError()
    data class Server(val statusCode: Int, override val message: String) : ApiError()
    data object Unauthorized : ApiError()
    data class AuthRejected(val errorKey: String, /* … */) : ApiError()
    data class NotFound(override val message: String = "Resource not found") : ApiError()
    data class BadRequest(
        override val message: String,
        val code: String? = null,
        val validationErrors: Map<String, List<String>>? = null,
        val errorKey: String? = null,
    ) : ApiError()
    data class Unknown(override val message: String = "An unexpected error occurred") : ApiError()
}
```

```swift [iOS]
public typealias ApiResult<T> = Result<T, ApiError>

public struct ApiError: Error, Equatable {
    public let code: String?        // the business error key, e.g. "user.not_existing_email"
    public let message: String?
    public let httpStatus: Int?
}

// Built from a ProblemDetails body, mirroring Android's ApiErrorParser:
ApiError.fromProblemDetails(httpStatus: 400, body: data)
```

:::

`ApiError.AuthRejected` on Android is deliberately **not** a widened `Unauthorized`. `Unauthorized`
means "the session layer rejected you" and is what session-teardown logic keys off; a login failure
that looked identical would sign users out at the wrong moment.

Both platforms treat **cancellation** as non-displayable — a tab switch or a pull-to-refresh that
supersedes an in-flight load must not raise a snackbar. iOS marks it with
`ApiError.cancelledCode == "network.cancelled"` and drops it in `showApiError`; on Android coroutine
cancellation never reaches the snackbar in the first place.

### Error keys become user-facing text

The backend returns translation keys (`BusinessErrorMessage`, e.g. `user.not_existing_email`) in the
ProblemDetails `errors` bag. Each client resolves them in **its own namespace** — the web apps'
`api.*` prefix does **not** apply to mobile:

| Client | Lookup | Resolver |
|---|---|---|
| Android partner | string resource `error_` + the key with dots and hyphens replaced by underscores, lowercased — `user.not_existing_email` → `R.string.error_user_not_existing_email`; falls back to the legacy `error_key_…` prefix for un-renamed entries | `partner-app/.../core/network/ApiErrorTranslator.kt` |
| Android customer | the same `error_…` convention, dots only, so translations cross-reference between the two apps | `customer-app/.../core/auth/ApiErrorParser.kt` |
| iOS | `"error." + key` in the `Localizable.xcstrings` catalog — `error.user.not_existing_email` | `CleansiaCore/Snackbar/ApiErrorLocalizer.swift` |

The resolution order is the same on all three: catalog hit → the raw key → the server's `detail`/
`title` → a status-based generic. Two arms are **deliberately asymmetric**, and the asymmetry is
load-bearing:

- An unmapped key on a **validation** (400) path renders **raw** — `order.after_photos.required` is
  more actionable to a cleaner than *"A validation problem occurred."*
- An unmapped key on the **auth-rejection** path degrades to the generic unauthorized line, because
  that arm fires on the sign-in screen and `auth.internal_type_error` is the worst thing that could
  be on that page.

Transport-level `network.*` codes are never rendered raw on either platform.

### 429 and infrastructure failures

- **`429 Too Many Requests`** — Android's `RetryAfterInterceptor` honours `Retry-After`, adds a
  random 0–15 s jitter so rejected clients desync instead of re-spiking at the window rollover, and
  retries **exactly once**; a second 429 is returned unchanged. It sits outermost so the retry
  re-enters the auth interceptor and carries a fresh token. The refresh/login client deliberately
  does not carry it.
- **Connectivity and 5xx** — the Android customer app installs `NetworkErrorInterceptor` on both
  clients to raise one global snackbar for infrastructure failures that every screen would handle
  identically. It never swallows: the caller still receives the original exception or response.
  Business 400s are excluded, because screens show those inline next to the field that caused them.

## The `200 OK` that is not a session

`POST /api/Auth/Login` and `PUT /api/Auth/ConfirmUserEmail` return **`200`** even when the user's
email is unconfirmed. The body is then a special shape, **not** an error (field names as the
generated clients bind them):

```jsonc
{
  "token": "",                  // may be EMPTY on the unconfirmed path
  "isEmailConfirmed": false,
  "refreshToken": null,         // null/absent on the unconfirmed path
  "refreshTokenExpiresAt": null,
  "email": "…", "userId": "…", "role": "…"
}
```

`isEmailConfirmed == false` **or** an empty `token` is the *email-unconfirmed outcome*: do not store
a session as authenticated, surface a distinct state and route to the verification screen. Only
`isEmailConfirmed == true` **and** a non-empty token is a full session. The partner apps additionally
persist a token when one *is* present, so the verification screen can call `ResendConfirmationEmail`
with a Bearer — and still route to verify. A `ConfirmUserEmail` that returns `200` with no token is
an "unverified, no token" outcome, not a licence to navigate into the app with no session.

## Related

- [Overview](/mobile-app/overview) — module layout, build configuration, CI, and the toolchain traps.
- [Structure](/mobile-app/features) — where a feature's client, view model and tests live.
- [API Reference → Authentication](/api/authentication) — the backend side of the auth surface.
- `src/cleansia_ios/docs/header-parity-contract.md` — the canonical out-of-band contract both
  platforms implement.
- `src/cleansia_ios/openapi/README.md` — the Swift codegen config and the never-hand-edit discipline.
