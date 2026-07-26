package cz.cleansia.core.auth

import okhttp3.Interceptor
import okhttp3.Response

/**
 * Attaches the stored access token to every outgoing request on the
 * authenticated client.
 *
 * `Authorization: Bearer <access>` — only if present. Skips the header entirely
 * on the anonymous auth endpoints (Login, Register, RefreshToken, etc.) since
 * they reject or ignore unexpected Bearer tokens. Note that `/api/Auth/Logout`
 * and `/api/Auth/ChangePassword` are NOT anonymous — they are `[Authorize]`
 * server-side — which is why this interceptor must stay off the no-auth client
 * rather than being installed on both.
 *
 * Device identity (`X-Device-Id` / `X-Device-Label`) deliberately does NOT live
 * here: those must ride on the anonymous token-issuing calls too, so they moved
 * to [DeviceHeadersInterceptor], which both clients install.
 *
 * 401 handling lives in [AuthAuthenticator], not here.
 */
class AuthInterceptor(
    private val tokenStore: TokenStore,
) : Interceptor {
    override fun intercept(chain: Interceptor.Chain): Response {
        val request = chain.request()
        val builder = request.newBuilder()

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

    private companion object {
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
