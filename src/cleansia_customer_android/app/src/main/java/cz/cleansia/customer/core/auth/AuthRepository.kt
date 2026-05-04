package cz.cleansia.customer.core.auth

import android.content.Context
import android.util.Log
import cz.cleansia.customer.R
import cz.cleansia.customer.core.data.AddressRepository
import cz.cleansia.customer.core.disputes.DisputeRepository
import cz.cleansia.customer.core.loyalty.LoyaltyRepository
import cz.cleansia.customer.core.orders.OrderRepository
import cz.cleansia.customer.core.referral.ReferralRepository
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
 */
class AuthRepository(
    private val api: AuthApi,
    private val tokenStore: TokenStore,
    private val sessionManager: SessionManager,
    private val addressRepository: AddressRepository,
    private val orderRepository: OrderRepository,
    private val disputeRepository: DisputeRepository,
    private val loyaltyRepository: LoyaltyRepository,
    private val referralRepository: ReferralRepository,
    private val membershipRepository: cz.cleansia.customer.core.memberships.MembershipRepository,
    private val recurringBookingRepository: cz.cleansia.customer.core.recurring.RecurringBookingRepository,
    private val appContext: Context,
) : RefreshClient {

    // ─── Login + register ───

    suspend fun login(email: String, password: String, rememberMe: Boolean): AuthResult {
        val response = try {
            api.login(LoginRequest(email, password, rememberMe))
        } catch (t: Throwable) {
            return AuthResult.Error(t.toFriendlyMessage())
        }
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
        val response = try {
            api.confirmUserEmail(ConfirmUserEmailRequest(code))
        } catch (t: Throwable) {
            return AuthResult.Error(t.toFriendlyMessage())
        }
        return handleAuthResponse(response)
    }

    suspend fun resendConfirmationEmail(email: String, language: String): Result<Boolean> = runCatching {
        val response = api.resendConfirmationEmail(ResendConfirmationEmailRequest(email, language))
        if (!response.isSuccessful) throw HttpException(response.code(), response.message())
        response.body() ?: true
    }

    suspend fun googleAuth(
        googleIdToken: String,
        googleId: String,
        email: String,
        firstName: String,
        lastName: String,
    ): AuthResult {
        val response = try {
            api.googleAuth(GoogleAuthRequest(googleIdToken, googleId, email, firstName, lastName))
        } catch (t: Throwable) {
            return AuthResult.Error(t.toFriendlyMessage())
        }
        return handleAuthResponse(response)
    }

    // ─── Logout ───

    /** Best-effort — if the backend call fails we still wipe local state. */
    suspend fun logout() {
        val refreshToken = tokenStore.current()?.refreshToken
        if (refreshToken != null) {
            runCatching { api.logout(LogoutRequest(refreshToken)) }
                .onFailure { Log.w(TAG, "Logout API call failed: ${it.message}") }
        }
        // Wipe session-scoped caches before the token so any future expansion of
        // clear() still sees a valid auth context if it ever needed one.
        addressRepository.clear()
        orderRepository.clear()
        disputeRepository.clear()
        loyaltyRepository.clear()
        referralRepository.clear()
        membershipRepository.clear()
        recurringBookingRepository.clear()
        tokenStore.clear()
        sessionManager.emitForcedSignOut(ForcedSignOutReason.UserInitiated)
    }

    // ─── RefreshClient impl (called by AuthAuthenticator) ───

    override suspend fun refresh(refreshToken: String): TokenStore.Tokens? {
        val response = try {
            api.refreshToken(RefreshTokenRequest(refreshToken))
        } catch (t: Throwable) {
            Log.w(TAG, "Refresh API call threw: ${t.message}")
            return null
        }

        if (!response.isSuccessful) {
            Log.i(TAG, "Refresh endpoint rejected token with ${response.code()}")
            return null
        }

        val body = response.body() ?: return null
        return body.toTokens()
    }

    // ─── Internal helpers ───

    private fun handleAuthResponse(response: Response<JwtTokenResponseDto>): AuthResult {
        if (!response.isSuccessful) {
            val message = ApiErrorParser.parseToUserMessage(appContext, response.errorBody(), response.code())
            return AuthResult.Error(message)
        }
        val body = response.body() ?: return AuthResult.Error(appContext.getString(R.string.error_generic_unknown))

        if (!body.isEmailConfirmed || body.token.isEmpty()) {
            return AuthResult.EmailUnconfirmed(body.email)
        }

        val tokens = body.toTokens() ?: return AuthResult.Error(appContext.getString(R.string.error_generic_unknown))
        tokenStore.save(tokens)
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

    /** Network-level failures (no connectivity, DNS, timeout). Translated. */
    private fun Throwable.toFriendlyMessage(): String =
        appContext.getString(R.string.error_generic_network)

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
