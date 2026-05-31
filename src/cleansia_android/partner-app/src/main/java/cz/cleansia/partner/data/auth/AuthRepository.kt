package cz.cleansia.partner.data.auth

import cz.cleansia.core.auth.JwtDecoder
import cz.cleansia.core.auth.SessionScopedCache
import cz.cleansia.core.auth.TokenStore
import cz.cleansia.partner.api.client.AuthApi
import cz.cleansia.partner.api.client.EmployeeApi
import cz.cleansia.partner.api.model.ConfirmUserEmailCommand
import cz.cleansia.partner.api.model.JwtTokenResponse
import cz.cleansia.partner.api.model.LogoutCommand
import cz.cleansia.partner.api.model.PartnerLoginCommand
import cz.cleansia.partner.api.model.RegisterEmployeeCommand
import cz.cleansia.partner.api.model.RequestPasswordChangeCommand
import cz.cleansia.partner.api.model.ResendConfirmationEmailCommand
import cz.cleansia.partner.core.auth.UserProfileData
import cz.cleansia.partner.core.auth.UserProfileStore
import cz.cleansia.partner.core.network.ApiResult
import cz.cleansia.partner.core.network.safeApiCall
import cz.cleansia.partner.core.notifications.PushTokenRepository
import kotlinx.serialization.json.Json
import java.time.Instant
import javax.inject.Inject
import javax.inject.Provider
import javax.inject.Singleton

/**
 * Public contract for auth-related operations. Hides the OpenAPI-generated
 * client from feature ViewModels so DTO drift becomes a compile error here
 * (one file) instead of in every screen.
 */
interface AuthRepository {

    /** Performs the login → store tokens → fetch employee profile flow. */
    suspend fun login(email: String, password: String, rememberMe: Boolean = true): ApiResult<LoginOutcome>

    /** Partner employee registration. Returns true on success; the server emails a confirmation code. */
    suspend fun register(
        email: String,
        password: String,
        firstName: String,
        lastName: String,
        language: String,
    ): ApiResult<Boolean>

    /** Confirms an email via the 6-digit code emailed at registration. Returns the issued JWT. */
    suspend fun confirmEmail(code: String): ApiResult<LoginOutcome>

    /** Resends the confirmation code to the given address. */
    suspend fun resendConfirmation(email: String, language: String): ApiResult<Boolean>

    /** Triggers a "forgot my password" email containing a reset link. */
    suspend fun forgotPassword(email: String, language: String): ApiResult<Unit>

    /**
     * Server-side logout (revokes refresh token) + local wipe. Best-effort —
     * a failed network call still clears local state so the user is signed
     * out of the device even if the server didn't acknowledge it.
     */
    suspend fun logout()

    /** Wipes local state only — for forced sign-out flows where the server already revoked. */
    suspend fun signOutLocal()
}

@Singleton
class AuthRepositoryImpl @Inject constructor(
    private val authApi: AuthApi,
    private val employeeApi: EmployeeApi,
    private val tokenStore: TokenStore,
    private val userProfileStore: UserProfileStore,
    private val json: Json,
    private val pushTokenRepository: PushTokenRepository,
    private val sessionScopedCaches: Provider<Set<@JvmSuppressWildcards SessionScopedCache>>,
) : AuthRepository {

    override suspend fun login(
        email: String,
        password: String,
        rememberMe: Boolean,
    ): ApiResult<LoginOutcome> {
        val result = safeApiCall(json) {
            authApi.authLogin(
                PartnerLoginCommand(email = email, password = password, rememberMe = rememberMe),
            )
        }

        if (result is ApiResult.Error) return ApiResult.Error(result.error)
        val body = (result as ApiResult.Success).data

        // Persist tokens FIRST so the follow-up GetCurrentEmployee call has
        // an Authorization header. If the token came back, the email is
        // either confirmed (full session) or unconfirmed (intermediate state
        // — UI should route to ConfirmEmail). Both store tokens so the
        // confirm-email screen's call can include the bearer.
        val token = body.token
        if (token.isNullOrBlank()) {
            // Server returned 200 with no token — shouldn't happen, but guard
            // against it rather than swallow.
            return ApiResult.Success(
                LoginOutcome.UnverifiedEmail(email = email, hasToken = false),
            )
        }

        persistTokens(body)
        persistProfile(body, fallbackEmail = email)

        // Unconfirmed email → caller routes to ConfirmEmailScreen. The
        // backend issues a token in this case so resendConfirmation can be
        // called from there without re-logging-in.
        if (body.isEmailConfirmed != true) {
            return ApiResult.Success(
                LoginOutcome.UnverifiedEmail(email = email, hasToken = true),
            )
        }

        // Best-effort employee fetch. If it fails we still return success
        // because the token is good; profile data lazy-loads later anyway.
        val employee = safeApiCall(json) { employeeApi.employeeGetCurrentEmployee() }
            .getOrNull()
        if (employee != null) {
            userProfileStore.save(
                userProfileStore.current()?.copy(
                    employeeId = employee.id,
                    firstName = employee.firstName,
                    lastName = employee.lastName,
                ) ?: UserProfileData(
                    userId = body.userId.orEmpty(),
                    email = body.email ?: email,
                    employeeId = employee.id,
                    isEmailConfirmed = true,
                    hasAdminAccess = body.hasAdminAccess ?: false,
                    firstName = employee.firstName,
                    lastName = employee.lastName,
                    role = body.role,
                ),
            )
        }

        // Device registration is driven by PushTokenSessionObserver,
        // which reacts to the auth-token flow flipping null→non-null
        // (which the tokenStore.save() above just triggered). No
        // explicit hook needed here.

        return ApiResult.Success(LoginOutcome.Authenticated)
    }

    override suspend fun register(
        email: String,
        password: String,
        firstName: String,
        lastName: String,
        language: String,
    ): ApiResult<Boolean> {
        val response = safeApiCall(json) {
            authApi.authRegisterEmployee(
                RegisterEmployeeCommand(
                    email = email,
                    password = password,
                    firstName = firstName,
                    lastName = lastName,
                    language = language,
                ),
            )
        }
        return response.map { it ?: false }
    }

    override suspend fun confirmEmail(code: String): ApiResult<LoginOutcome> {
        val result = safeApiCall(json) {
            authApi.authConfirmUserEmail(ConfirmUserEmailCommand(code = code))
        }
        if (result is ApiResult.Error) return ApiResult.Error(result.error)
        val body = (result as ApiResult.Success).data

        if (body.token.isNullOrBlank()) {
            // Server returned 200 but no token — treat as failure so UI shows
            // a generic error rather than navigating into the app with no
            // session.
            return ApiResult.Success(LoginOutcome.UnverifiedEmail(email = body.email.orEmpty(), hasToken = false))
        }

        persistTokens(body)
        persistProfile(body, fallbackEmail = body.email.orEmpty())

        // Device registration is driven by PushTokenSessionObserver
        // (see login() — same reasoning).

        // After a fresh confirmation the email is by definition confirmed,
        // so we can proceed straight into the app.
        return ApiResult.Success(LoginOutcome.Authenticated)
    }

    override suspend fun resendConfirmation(email: String, language: String): ApiResult<Boolean> {
        val response = safeApiCall(json) {
            authApi.authResendConfirmationEmail(
                ResendConfirmationEmailCommand(email = email, language = language),
            )
        }
        return response.map { it ?: false }
    }

    override suspend fun forgotPassword(email: String, language: String): ApiResult<Unit> {
        return safeApiCall(json) {
            authApi.authForgotPassword(
                RequestPasswordChangeCommand(email = email, language = language),
            )
        }
    }

    override suspend fun logout() {
        // Delete the device row server-side BEFORE wiping the token — the
        // unregister call needs the bearer. Best-effort; a failure still
        // proceeds to local wipe (and the row is GC'd server-side anyway when
        // the token next reports NotRegistered).
        runCatching { pushTokenRepository.unregisterDevice() }

        val refreshToken = tokenStore.current()?.refreshToken
        if (!refreshToken.isNullOrBlank()) {
            // Best-effort — ignore the result. If it fails we still wipe locally.
            runCatching {
                safeApiCall(json) { authApi.authLogout(LogoutCommand(token = refreshToken)) }
            }
        }
        signOutLocal()
    }

    override suspend fun signOutLocal() {
        tokenStore.clear()
        userProfileStore.clear()
        sessionScopedCaches.get().forEach { it.clear() }
    }

    private fun persistTokens(body: JwtTokenResponse) {
        val access = body.token ?: return
        val refresh = body.refreshToken.orEmpty()
        val accessExp = JwtDecoder.extractExpiryMillis(access)
            ?: (System.currentTimeMillis() + 15 * 60 * 1000L)
        val refreshExp = body.refreshTokenExpiresAt
            ?.let { runCatching { Instant.parse(it).toEpochMilli() }.getOrNull() }
            ?: (System.currentTimeMillis() + 24 * 60 * 60 * 1000L)

        tokenStore.save(
            TokenStore.Tokens(
                accessToken = access,
                accessTokenExpiresAt = accessExp,
                refreshToken = refresh,
                refreshTokenExpiresAt = refreshExp,
            ),
        )
    }

    private suspend fun persistProfile(body: JwtTokenResponse, fallbackEmail: String) {
        val existing = userProfileStore.current()
        userProfileStore.save(
            UserProfileData(
                userId = body.userId ?: existing?.userId.orEmpty(),
                email = body.email ?: fallbackEmail,
                employeeId = existing?.employeeId,
                isEmailConfirmed = body.isEmailConfirmed ?: existing?.isEmailConfirmed ?: false,
                hasAdminAccess = body.hasAdminAccess ?: existing?.hasAdminAccess ?: false,
                firstName = existing?.firstName,
                lastName = existing?.lastName,
                role = body.role ?: existing?.role,
            ),
        )
    }
}

/** Outcome of a login or confirm-email call. */
sealed interface LoginOutcome {
    data object Authenticated : LoginOutcome
    /**
     * Login succeeded but email isn't confirmed yet. UI routes to
     * ConfirmEmailScreen and lets the user enter the 6-digit code or resend.
     * [hasToken] indicates whether resend can be called without re-login.
     */
    data class UnverifiedEmail(val email: String, val hasToken: Boolean) : LoginOutcome
}
