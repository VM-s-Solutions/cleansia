package cz.cleansia.customer.core.auth

import cz.cleansia.core.auth.RefreshClient
import cz.cleansia.core.auth.RefreshResult
import cz.cleansia.core.auth.SessionScopedCache
import cz.cleansia.core.auth.SessionManager
import cz.cleansia.core.auth.AuthAuthenticator
import cz.cleansia.core.auth.TokenStore
import cz.cleansia.core.auth.JwtDecoder
import cz.cleansia.core.auth.ForcedSignOutReason
import cz.cleansia.core.consent.SignupConsentRepository

import android.util.Log
import cz.cleansia.core.network.ApiError
import cz.cleansia.core.network.ApiResult
import cz.cleansia.core.network.networkCall
import cz.cleansia.core.network.safeApiCall
import cz.cleansia.core.notifications.PushTokenRepository
import kotlinx.coroutines.CancellationException
import kotlinx.datetime.Instant
import kotlinx.serialization.json.Json

/**
 * Orchestrates auth flows + token persistence on top of [AuthApi].
 *
 * Every method that returns a fresh [TokenStore.Tokens] bundle persists it
 * via [TokenStore.save] before returning, so callers don't have to remember.
 *
 * "Unconfirmed email" case: when a user logs in but hasn't confirmed their
 * email, the server returns 200 OK with `isEmailConfirmed = false` and an
 * empty `token`. We surface that as [AuthSuccess.EmailUnconfirmed] so the UI
 * can route to the verification screen without treating it as a failure.
 *
 * Sign-out wipes every [SessionScopedCache] in [sessionScopedCaches] — adding
 * a new cache is a one-line change in [SessionScopedModule], no edit here.
 *
 * Two [AuthApi] instances on purpose:
 *  - [api] is built on the anonymous client and serves the token-ISSUING calls
 *    (login / register / confirm / refresh), which must not carry a Bearer.
 *  - [authenticatedApi] is built on the authenticated client and serves
 *    `/api/Auth/Logout`, which is `[Authorize]` server-side.
 * See [logout] for why the split exists at all.
 */
class AuthRepository(
    private val api: AuthApi,
    /**
     * Lazy on purpose. The authenticated Retrofit hangs off the OkHttp client
     * that owns [AuthAuthenticator], and the authenticator holds a
     * `Provider<AuthRepository>` for its refresh call — resolving this eagerly
     * would close that loop into a Dagger dependency cycle. `.get()` is called
     * inside [logout] only, by which time the whole graph is built.
     */
    private val authenticatedApi: javax.inject.Provider<AuthApi>,
    private val tokenStore: TokenStore,
    private val sessionManager: SessionManager,
    private val sessionScopedCaches: Set<@JvmSuppressWildcards SessionScopedCache>,
    private val pushTokenRepository: PushTokenRepository,
    /**
     * Lazy for the same reason as [authenticatedApi]: it reaches the GDPR endpoints
     * through the authenticated Retrofit, and that graph owns [AuthAuthenticator],
     * which holds a `Provider<AuthRepository>` back to this class.
     */
    private val signupConsent: javax.inject.Provider<SignupConsentRepository>,
    private val json: Json,
) : RefreshClient {

    // ─── Login + register ───

    suspend fun login(email: String, password: String, rememberMe: Boolean): ApiResult<AuthSuccess> {
        val body = LoginRequest(
            email = email,
            password = password,
            rememberMe = rememberMe,
            trustedDeviceToken = tokenStore.current()?.trustedDeviceToken,
        )
        return when (val result = safeApiCall(json) { api.login(body) }) {
            is ApiResult.Success -> handleAuthBody(result.data)
            is ApiResult.Error -> result
        }
    }

    suspend fun register(
        email: String,
        password: String,
        firstName: String,
        lastName: String,
        language: String,
        referralCode: String? = null,
    ): ApiResult<Unit> = safeApiCall(json) {
        api.register(
            RegisterRequest(
                email = email,
                password = password,
                firstName = firstName,
                lastName = lastName,
                language = language,
                referralCode = referralCode,
            ),
        )
    }.map { }

    suspend fun confirmEmail(email: String, code: String): ApiResult<AuthSuccess> =
        when (val result = safeApiCall(json) { api.confirmUserEmail(ConfirmUserEmailRequest(code, email)) }) {
            is ApiResult.Success -> handleAuthBody(result.data)
            is ApiResult.Error -> result
        }

    suspend fun resendConfirmationEmail(email: String, language: String): ApiResult<Unit> =
        safeApiCall(json) { api.resendConfirmationEmail(ResendConfirmationEmailRequest(email, language)) }.map { }

    suspend fun requestPasswordChange(email: String, language: String): ApiResult<Unit> =
        safeApiCall(json) { api.requestPasswordChange(RequestPasswordChangeRequest(email, language)) }

    suspend fun changePassword(email: String, code: String, newPassword: String): ApiResult<Unit> =
        safeApiCall(json) { api.changePassword(ChangePasswordRequest(email, newPassword, code)) }.map { }

    suspend fun googleAuth(
        googleIdToken: String,
        googleId: String,
        email: String,
        firstName: String,
        lastName: String,
        termsAccepted: Boolean,
    ): ApiResult<AuthSuccess> = when (
        val result = safeApiCall(json) {
            api.googleAuth(
                GoogleAuthRequest(
                    token = googleIdToken,
                    googleId = googleId,
                    email = email,
                    firstName = firstName,
                    lastName = lastName,
                    termsAccepted = termsAccepted,
                ),
            )
        }
    ) {
        is ApiResult.Success -> handleAuthBody(result.data)
        is ApiResult.Error -> result
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
            // MUST go out on the authenticated client: /api/Auth/Logout is
            // [Authorize], so on the anonymous client this was a guaranteed 401
            // that runCatching swallowed — the user saw "signed out" while the
            // refresh token stayed live server-side for up to 30 days.
            // runCatching stays: an offline logout must still wipe locally, and
            // the authenticated client's AuthAuthenticator means an expired
            // access token refreshes-and-retries rather than failing outright.
            runCatching { authenticatedApi.get().logout(LogoutRequest(refreshToken)) }
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

    /**
     * Wipes local state only and routes to SignIn via the forced-sign-out bus — for flows where
     * the server ALREADY revoked this device's session (e.g. self-revoke on the Devices page).
     * Deliberately skips [logout]'s server calls: the revoke deactivated the device row and
     * revoked its refresh tokens, so the push unregister + logout POST would be redundant round
     * trips that only delay the sign-out (offline they'd block on two full timeouts).
     */
    suspend fun signOutLocal() {
        sessionScopedCaches.forEach { it.clear() }
        tokenStore.clear()
        sessionManager.emitForcedSignOut(ForcedSignOutReason.UserInitiated)
    }

    // ─── RefreshClient impl (called by AuthAuthenticator) ───

    override suspend fun refresh(refreshToken: String): RefreshResult {
        val response = networkCall(TAG) { api.refreshToken(RefreshTokenRequest(refreshToken)) }
            ?: return RefreshResult.Unavailable

        if (!response.isSuccessful) {
            Log.i(TAG, "Refresh endpoint answered ${response.code()}")
            val errorBody = runCatching { response.errorBody()?.string() }.getOrNull()
            return RefreshResult.classifyHttpFailure(response.code(), errorBody)
        }

        val tokens = response.body()?.toTokens() ?: return RefreshResult.Unavailable
        return RefreshResult.Success(tokens)
    }

    // ─── Internal helpers ───

    private suspend fun handleAuthBody(body: JwtTokenResponseDto): ApiResult<AuthSuccess> {
        if (!body.isEmailConfirmed || body.token.isEmpty()) {
            return ApiResult.Success(AuthSuccess.EmailUnconfirmed(body.email))
        }

        val tokens = body.toTokens() ?: return ApiResult.Error(ApiError.Unknown(""))

        // Defensive: clear every session-scoped cache BEFORE saving the new
        // tokens. Voluntary sign-out already does this via `logout()`, but a
        // force-killed app (or a crash before logout completed) leaves stale
        // user-A state in memory for user B to inherit. Wiping here is
        // belt-and-braces — on the normal flow caches are already empty.
        sessionScopedCaches.forEach { it.clear() }

        tokenStore.save(tokens)

        // The signup tick predates any session, and this is the first point at which the
        // SERVER has named the account it belongs to. Best-effort inside — sign-in must
        // not be breakable by the bookkeeping call that rides it — and it returns before
        // any network call when nothing is parked, which is every sign-in but the first.
        signupConsent.get().deliverFor(body.email)

        // Device registration is driven by PushTokenSessionObserver,
        // which reacts to the auth-token flow flipping null→non-null
        // (which the tokenStore.save above just triggered). No explicit
        // hook needed here — see PushTokenSessionObserver for rationale.

        return ApiResult.Success(AuthSuccess.Authenticated(tokens))
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

/** Successful auth body outcome — mirrors partner-app's [cz.cleansia.partner.data.auth.LoginOutcome]. */
sealed class AuthSuccess {
    data class Authenticated(val tokens: TokenStore.Tokens) : AuthSuccess()
    data class EmailUnconfirmed(val email: String?) : AuthSuccess()
}
