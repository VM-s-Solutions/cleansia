package cz.cleansia.partner.core.network

import cz.cleansia.partner.core.storage.TokenManager
import okhttp3.Interceptor
import okhttp3.Response
import javax.inject.Inject
import javax.inject.Singleton

/**
 * OkHttp Interceptor that adds the Authorization header to requests.
 */
@Singleton
class AuthInterceptor @Inject constructor(
    private val tokenManager: TokenManager
) : Interceptor {

    override fun intercept(chain: Interceptor.Chain): Response {
        val originalRequest = chain.request()

        // Skip auth header for login, register, and other public endpoints
        val publicEndpoints = listOf(
            "/Auth/Login",
            "/Auth/RegisterEmployee",
            "/Auth/ConfirmUserEmail",
            "/Auth/ResendConfirmationEmail",
            "/Auth/ForgotPassword",
            "/Auth/ResetPassword"
        )

        val isPublicEndpoint = publicEndpoints.any { originalRequest.url.encodedPath.endsWith(it) }

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

        // Detect session expiration and trigger centralized handling
        if (response.code == 401) {
            tokenManager.onSessionExpired()
        }

        return response
    }
}
