# API Integration

The Cleansia Partner Android app communicates with the Mobile API (`Cleansia.Web.Mobile`) using Retrofit with OkHttp, secured by JWT bearer tokens stored in EncryptedSharedPreferences.

::: info Source Files
- Network layer: `src/cleansia_android/.../core/network/`
- Token storage: `src/cleansia_android/.../core/storage/TokenManager.kt`
- Repositories: `src/cleansia_android/.../domain/repositories/`
- DI modules: `src/cleansia_android/.../di/`
:::

## Retrofit Setup

The app uses Retrofit with kotlinx.serialization for JSON handling:

```kotlin
// Key dependencies
implementation(libs.retrofit)
implementation(libs.retrofit.kotlinx.serialization)
implementation(libs.okhttp)
implementation(libs.okhttp.logging)
```

The API base URL is set per build type via `BuildConfig`:

| Build Type | Base URL |
|------------|----------|
| `debug` | `http://10.0.2.2:5002/api` |
| `staging` | `https://staging-api.cleansia.cz/api` |
| `release` | `https://api.cleansia.cz/api` |

The API service interface and generated models come from the OpenAPI Generator (see [Overview - OpenAPI Code Generation](/mobile-app/overview#openapi-code-generation)). The `GeneratedApiAdapter` bridges generated code with the app's own API service layer.

## AuthInterceptor

The `AuthInterceptor` is an OkHttp `Interceptor` that automatically attaches JWT tokens to requests.

**Source:** `src/cleansia_android/.../core/network/AuthInterceptor.kt`

```kotlin
@Singleton
class AuthInterceptor @Inject constructor(
    private val tokenManager: TokenManager
) : Interceptor {

    override fun intercept(chain: Interceptor.Chain): Response {
        val originalRequest = chain.request()

        // Skip auth header for public endpoints
        val publicEndpoints = listOf(
            "/Auth/Login",
            "/Auth/RegisterEmployee",
            "/Auth/ConfirmUserEmail",
            "/Auth/ResendConfirmationEmail",
            "/Auth/ForgotPassword",
            "/Auth/ResetPassword"
        )

        val isPublicEndpoint = publicEndpoints.any {
            originalRequest.url.encodedPath.endsWith(it)
        }

        if (isPublicEndpoint) {
            return chain.proceed(originalRequest)
        }

        val token = tokenManager.getToken()
        val response = if (token != null) {
            val authenticatedRequest = originalRequest.newBuilder()
                .header("Authorization", "Bearer $token")
                .build()
            chain.proceed(authenticatedRequest)
        } else {
            chain.proceed(originalRequest)
        }

        // Detect session expiration
        if (response.code == 401) {
            tokenManager.onSessionExpired()
        }

        return response
    }
}
```

Key behaviors:
- Public endpoints (login, register, etc.) skip the auth header
- `401` responses trigger `tokenManager.onSessionExpired()` which clears credentials and emits a session-expired event to the UI

## TokenManager

Manages secure storage of JWT tokens and user metadata using Android's `EncryptedSharedPreferences` with AES-256 encryption.

**Source:** `src/cleansia_android/.../core/storage/TokenManager.kt`

### Stored Data

| Key | Type | Description |
|-----|------|-------------|
| `auth_token` | String | JWT bearer token |
| `user_id` | String | User ID from JWT claims |
| `user_email` | String | User email |
| `is_email_confirmed` | Boolean | Email confirmation status |
| `user_first_name` | String | For display/initials |
| `user_last_name` | String | For display/initials |

### Reactive State

```kotlin
val isLoggedIn: Flow<Boolean>        // Emits login state changes
val userFullName: Flow<String>       // Emits when name changes
val userInitials: Flow<String>       // Emits when name changes
val sessionExpiredEvent: SharedFlow<Unit>  // One-shot event for 401 detection
```

### Session Expiration

When a `401` is detected by the `AuthInterceptor`:

1. `onSessionExpired()` is called (thread-safe via `AtomicBoolean`)
2. All auth data is cleared
3. `sessionExpiredEvent` emits once
4. The UI layer collects this event and shows a session-expired dialog
5. User is redirected to the login screen

::: tip
The `AtomicBoolean` guard prevents duplicate session-expired events when multiple parallel API calls all return `401` simultaneously.
:::

## ApiResult Wrapper

All API calls return `ApiResult<T>`, a sealed class that safely wraps success/error states.

**Source:** `src/cleansia_android/.../core/network/ApiResult.kt`

```kotlin
sealed class ApiResult<out T> {
    data class Success<T>(val data: T) : ApiResult<T>()
    data class Error(val error: ApiError) : ApiResult<Nothing>()

    fun getOrNull(): T?
    fun getOrThrow(): T
    fun getOrDefault(default: T): T
    fun <R> map(transform: (T) -> R): ApiResult<R>
    fun onSuccess(action: (T) -> Unit): ApiResult<T>
    fun onError(action: (ApiError) -> Unit): ApiResult<T>
}
```

### Error Types

The `ApiError` hierarchy matches HTTP error categories:

| Type | Status Code | Description |
|------|-------------|-------------|
| `ApiError.Network` | N/A | Connection timeout, no internet |
| `ApiError.Unauthorized` | 401 | Token expired or missing |
| `ApiError.NotFound` | 404 | Resource not found |
| `ApiError.BadRequest` | 400 | Validation errors from backend |
| `ApiError.Server` | 5xx | Server-side errors |
| `ApiError.Unknown` | Other | Unexpected errors |

### Error Translation

The `ApiErrorTranslator` maps backend error keys (e.g., `user.not_existing_email`) to localized user-facing strings, mirroring the Angular frontend's error translation pattern.

## Safe API Call

The `safeApiCall` function wraps all Retrofit calls with error handling.

**Source:** `src/cleansia_android/.../core/network/SafeApiCall.kt`

```kotlin
suspend fun <T> safeApiCall(
    json: Json = Json { ignoreUnknownKeys = true },
    apiCall: suspend () -> Response<T>
): ApiResult<T> = withContext(Dispatchers.IO) {
    try {
        val response = apiCall()
        handleResponse(response, json)
    } catch (e: SocketTimeoutException) {
        ApiResult.Error(ApiError.Network("Connection timeout."))
    } catch (e: UnknownHostException) {
        ApiResult.Error(ApiError.Network("Unable to connect to server."))
    } catch (e: IOException) {
        ApiResult.Error(ApiError.Network("Network error: ${e.message}"))
    } catch (e: Exception) {
        ApiResult.Error(ApiError.Unknown(e.message ?: "An unexpected error occurred"))
    }
}
```

Error response parsing handles both:
- **ProblemDetails format:** `Map<String, String>` (joined error messages)
- **Custom format:** `Map<String, List<String>>` (multiple error messages per field)

## Repository Pattern

Each feature area has a repository that encapsulates API calls:

| Repository | Purpose |
|------------|---------|
| `AuthRepository` | Login, register, email confirmation |
| `OrdersRepository` | Order CRUD, photos, notes, issues |
| `DashboardRepository` | Analytics data |
| `InvoicesRepository` | Invoice list and details |
| `ProfileRepository` | Employee profile |

Repositories are defined as interfaces in `domain/repositories/` and implemented with Hilt-injected dependencies.

## Offline Support

The app uses Room database for local caching:

| Cached Entity | DAO | Use Case |
|---------------|-----|----------|
| `CachedOrder` | `OrderDao` | View orders list offline |
| `CachedInvoice` | `InvoiceDao` | View invoices offline |
| `CachedProfile` | `ProfileDao` | View profile offline |

The `NetworkMonitor` (`src/cleansia_android/.../core/network/NetworkMonitor.kt`) provides connectivity status so the app can switch between online and cached data.

## Network Architecture Diagram

```
ViewModel
    |
    v
Repository (interface)
    |
    v
safeApiCall { apiService.endpoint() }
    |
    v
Retrofit + OkHttp
    |
    v
AuthInterceptor (adds Bearer token)
    |
    v
Mobile API (Cleansia.Web.Mobile)
```
