package cz.cleansia.customer.core.auth

import android.os.Build
import okhttp3.Interceptor
import okhttp3.Response

/**
 * Attaches the stored access token + device metadata to every outgoing request.
 *
 * - `Authorization: Bearer <access>` — only if present and not expired client-side.
 *    Skips the header entirely on auth endpoints (Login, Register, RefreshToken, etc.)
 *    since they're anonymous and some reject unexpected Bearer tokens.
 * - `X-Device-Label`                 — best-effort device identifier stored server-side
 *    against the refresh-token record for audit purposes ("signed in from Pixel 9 Pro").
 *
 * 401 handling lives in [AuthAuthenticator], not here.
 */
class AuthInterceptor(
    private val tokenStore: TokenStore,
) : Interceptor {
    override fun intercept(chain: Interceptor.Chain): Response {
        val request = chain.request()

        val builder = request.newBuilder()
            .header(DEVICE_LABEL_HEADER, deviceLabel())

        // Anonymous endpoints — don't send stale/revoked tokens to /api/auth/*.
        val path = request.url.encodedPath
        val isAnonEndpoint = ANON_ENDPOINTS.any { path.contains(it, ignoreCase = true) }

        if (!isAnonEndpoint) {
            tokenStore.current()?.accessToken?.let { access ->
                builder.header("Authorization", "Bearer $access")
            }
        }

        return chain.proceed(builder.build())
    }

    private fun deviceLabel(): String {
        // e.g. "Pixel 9 Pro - Android 15". ASCII-only because HTTP header values
        // reject non-ASCII (OkHttp throws on middle-dot, em-dash, etc).
        // Kept under 120 chars to match the server-side column.
        val device = "${Build.MANUFACTURER} ${Build.MODEL}".trim()
        return "$device - Android ${Build.VERSION.RELEASE}".take(120)
    }

    private companion object {
        const val DEVICE_LABEL_HEADER = "X-Device-Label"
        val ANON_ENDPOINTS = listOf(
            "/api/auth/login",
            "/api/auth/register",
            "/api/auth/refreshtoken",
            "/api/auth/googleauth",
            "/api/auth/confirmuseremail",
            "/api/auth/resendconfirmationemail",
        )
    }
}
