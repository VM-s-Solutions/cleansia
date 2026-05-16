package cz.cleansia.customer.core.auth

import cz.cleansia.core.auth.RefreshClient
import cz.cleansia.core.auth.SessionScopedCache
import cz.cleansia.core.auth.SessionManager
import cz.cleansia.core.auth.AuthAuthenticator
import cz.cleansia.core.auth.TokenStore
import cz.cleansia.core.auth.JwtDecoder
import cz.cleansia.core.auth.ForcedSignOutReason

import android.content.Context
import android.util.Log
import cz.cleansia.customer.R
import cz.cleansia.core.network.networkCall
import cz.cleansia.customer.core.notifications.PushTokenRepository
import kotlinx.coroutines.CancellationException
import kotlinx.datetime.Instant
import retrofit2.Response

/**
 * Orchestrates auth flows + token persistence on top of [AuthApi].
 *
 * Every method that returns a fresh [TokenStore.Tokens] bundle persists it
 * via [TokenStore.save] before returning, so callers don't have to remember.
 *
 * "Unconfirmed email" case: when a user logs in but hasn't confirmed their
 * email, the server returns 200 OK with `isEmailConfirmed = false` and an
 * empty `token`. We surface that as [AuthResult.EmailUnconfirmed] so the UI
 * can route to the verification screen without treating it as a failure.
 *
 * Sign-out wipes every [SessionScopedCache] in [sessionScopedCaches] — adding
 * a new cache is a one-line change in [SessionScopedModule], no edit here.
 */
class AuthRepository(
    private val api: AuthApi,
    private val tokenStore: TokenStore,
    private val sessionManager: SessionManager,
    private val sessionScopedCaches: Set<@JvmSuppressWildcards SessionScopedCache>,
    private val pushTokenRepository: PushTokenRepository,
    private val appContext: Context,
) : RefreshClient {

    // ─── Login + register ───

    suspend fun login(email: String, password: String, rememberMe: Boolean): AuthResult {
        val response = networkCall { api.login(LoginRequest(email, password, rememberMe)) }
            ?: return AuthResult.Error(appContext.getString(R.string.error_generic_network))
        return handleAuthResponse(response)
    }

    suspend fun register(
        email: String,
        password: String,
        firstName: String,
        lastName: String,
        language: String,
        referralCode: String? = null,
    ): Result<Boolean> = runCatching {
        val response = api.register(
            RegisterRequest(
                email = email,
                password = password,
                firstName = firstName,
                lastName = lastName,
                language = language,
                referralCode = referralCode,
            ),
        )
        if (!response.isSuccessful) throw HttpException(response.code(), response.message())
        response.body() ?: true
    }

    suspend fun confirmEmail(code: String): AuthResult {
        val response = networkCall { api.confirmUserEmail(ConfirmUserEmailRequest(code)) }
            ?: return AuthResult.Error(appContext.getString(R.string.error_generic_network))
        return handleAuthResponse(response)
    }

    suspend fun resendConfirmationEmail(email: String, language: String): Result<Boolean> = runCatching {
        val response = api.resendConfirmationEmail(ResendConfirmationEmailRequest(email, language))
        if (!response.isSuccessful) throw HttpException(response.code(), response.message())
        response.body() ?: true
    }

    suspend fun requestPasswordChange(email: String, language: String): Result<Boolean> = runCatching {
        val response = api.requestPasswordChange(RequestPasswordChangeRequest(email, language))
        if (!response.isSuccessful) throw HttpException(response.code(), response.message())
        true
    }

    suspend fun changePassword(email: String, code: String, newPassword: String): Result<Boolean> = runCatching {
        val response = api.changePassword(ChangePasswordRequest(email, newPassword, code))
        if (!response.isSuccessful) throw HttpException(response.code(), response.message())
        true
    }

    suspend fun googleAuth(
        googleIdToken: String,
        googleId: String,
        email: String,
        firstName: String,
        lastName: String,
    ): AuthResult {
        val response = networkCall {
            api.googleAuth(GoogleAuthRequest(googleIdToken, googleId, email, firstName, lastName))
        } ?: return AuthResult.Error(appContext.getString(R.string.error_generic_network))
        return handleAuthResponse(response)
    }

    // ─── Logout ───

    /** Best-effort — if the backend call fails we still wipe local state. */
    suspend fun logout() {
        val refreshToken = tokenStore.current()?.refreshToken

        // Unregister the device row server-side BEFORE clearing the
        // access token — the API call needs the JWT. Best-effort: if
        // the call fails (no network) the row stays orphaned and we'll
        // clean it up the next time FCM rejects its token as 410.
        try {
            pushTokenRepository.unregisterDevice()
        } catch (ce: CancellationException) {
            throw ce
        } catch (t: Throwable) {
            Log.w(TAG, "Push token unregister failed during logout: ${t.message}")
        }

        if (refreshToken != null) {
            runCatching { api.logout(LogoutRequest(refreshToken)) }
                .onFailure { Log.w(TAG, "Logout API call failed: ${it.message}") }
        }
        // Wipe session-scoped caches before the token so any future expansion of
        // clear() still sees a valid auth context if it ever needed one. Iterates
        // the multibinding so adding a new cache is a one-line edit in
        // SessionScopedModule rather than touching this class + AuthAuthenticator.
        sessionScopedCaches.forEach { it.clear() }
        tokenStore.clear()
        sessionManager.emitForcedSignOut(ForcedSignOutReason.UserInitiated)
    }

    // ─── RefreshClient impl (called by AuthAuthenticator) ───

    override suspend fun refresh(refreshToken: String): TokenStore.Tokens? {
        val response = networkCall(TAG) { api.refreshToken(RefreshTokenRequest(refreshToken)) }
            ?: return null

        if (!response.isSuccessful) {
            Log.i(TAG, "Refresh endpoint rejected token with ${response.code()}")
            return null
        }

        val body = response.body() ?: return null
        return body.toTokens()
    }

    // ─── Internal helpers ───

    private suspend fun handleAuthResponse(response: Response<JwtTokenResponseDto>): AuthResult {
        if (!response.isSuccessful) {
            val message = ApiErrorParser.parseToUserMessage(appContext, response.errorBody(), response.code())
            return AuthResult.Error(message)
        }
        val body = response.body() ?: return AuthResult.Error(appContext.getString(R.string.error_generic_unknown))

        if (!body.isEmailConfirmed || body.token.isEmpty()) {
            return AuthResult.EmailUnconfirmed(body.email)
        }

        val tokens = body.toTokens() ?: return AuthResult.Error(appContext.getString(R.string.error_generic_unknown))

        // Defensive: clear every session-scoped cache BEFORE saving the new
        // tokens. Voluntary sign-out already does this via `logout()`, but a
        // force-killed app (or a crash before logout completed) leaves stale
        // user-A state in memory for user B to inherit. Wiping here is
        // belt-and-braces — on the normal flow caches are already empty.
        sessionScopedCaches.forEach { it.clear() }

        tokenStore.save(tokens)

        // Register the device's current FCM token now that we have an
        // access token to authenticate the call. Failure is silent
        // (logged inside PushTokenRepository) — push isn't critical to
        // an otherwise-successful sign-in, and onNewToken will retry the
        // next time FCM rotates the token.
        try {
            pushTokenRepository.registerCurrentToken()
        } catch (ce: CancellationException) {
            throw ce
        } catch (t: Throwable) {
            Log.w(TAG, "Push token register failed after login: ${t.message}")
        }

        return AuthResult.Success(tokens)
    }

    /** @return null if the DTO is missing required fields (refreshToken / expiry / parseable access). */
    private fun JwtTokenResponseDto.toTokens(): TokenStore.Tokens? {
        val refresh = refreshToken ?: return null
        val refreshExpMillis = refreshTokenExpiresAt
            ?.let { runCatching { Instant.parse(it).toEpochMilliseconds() }.getOrNull() }
            ?: return null

        val accessExp = JwtDecoder.extractExpiryMillis(token)
            ?: (System.currentTimeMillis() + DEFAULT_ACCESS_EXP_MS) // Fall back to 15 min from now

        return TokenStore.Tokens(
            accessToken = token,
            accessTokenExpiresAt = accessExp,
            refreshToken = refresh,
            refreshTokenExpiresAt = refreshExpMillis,
        )
    }

    private companion object {
        const val TAG = "AuthRepository"
        const val DEFAULT_ACCESS_EXP_MS = 15L * 60_000L
    }
}

sealed class AuthResult {
    data class Success(val tokens: TokenStore.Tokens) : AuthResult()
    data class EmailUnconfirmed(val email: String?) : AuthResult()
    data class Error(val message: String) : AuthResult()
}

class HttpException(val code: Int, message: String) : RuntimeException("HTTP $code: $message")
