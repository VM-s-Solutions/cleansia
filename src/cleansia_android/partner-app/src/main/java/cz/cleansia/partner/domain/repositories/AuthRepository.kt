package cz.cleansia.partner.domain.repositories

import cz.cleansia.partner.api.client.AuthApi
import cz.cleansia.partner.api.client.EmployeeApi
import cz.cleansia.partner.api.model.ConfirmUserEmailCommand
import cz.cleansia.partner.api.model.JwtTokenResponse
import cz.cleansia.partner.api.model.PartnerLoginCommand
import cz.cleansia.partner.api.model.RegisterEmployeeCommand
import cz.cleansia.partner.api.model.RequestPasswordChangeCommand
import cz.cleansia.partner.api.model.ResendConfirmationEmailCommand
import cz.cleansia.partner.core.network.ApiResult
import cz.cleansia.partner.core.network.safeApiCall
import cz.cleansia.partner.core.storage.TokenManager
import cz.cleansia.partner.domain.models.auth.LoginResponse
import cz.cleansia.partner.domain.models.auth.RegisterRequest
import cz.cleansia.partner.domain.models.auth.RegisterResponse
import kotlinx.serialization.json.Json
import java.util.Locale
import javax.inject.Inject
import javax.inject.Singleton

/**
 * Auth repository — abstracts the JWT/refresh/confirm flow over the
 * OpenAPI-generated [AuthApi] + [EmployeeApi]. The hand-written
 * `ApiService` is no longer used for auth; future endpoint drift
 * (e.g. the "Language field is required" 400 we hit on registration
 * pre-migration) becomes a compile error rather than a 400 at runtime.
 *
 * The return types still use partner-domain shapes ([LoginResponse],
 * [RegisterResponse]) so caller VMs are unaffected. The impl maps the
 * generated [JwtTokenResponse] / `Boolean` into those shapes.
 */
interface AuthRepository {
    suspend fun login(email: String, password: String): ApiResult<LoginResponse>
    suspend fun register(request: RegisterRequest): ApiResult<RegisterResponse>
    suspend fun register(
        email: String,
        password: String,
        firstName: String,
        lastName: String,
        phoneNumber: String,
    ): ApiResult<RegisterResponse>
    suspend fun confirmEmail(email: String, token: String): ApiResult<LoginResponse>
    suspend fun resendConfirmationEmail(email: String): ApiResult<Unit>
    suspend fun forgotPassword(email: String): ApiResult<Unit>
    suspend fun refreshToken(): ApiResult<LoginResponse>
    fun logout()
    fun isLoggedIn(): Boolean
    fun isEmailConfirmed(): Boolean
}

@Singleton
class AuthRepositoryImpl @Inject constructor(
    private val authApi: AuthApi,
    private val employeeApi: EmployeeApi,
    private val tokenManager: TokenManager,
    private val json: Json,
) : AuthRepository {

    override suspend fun login(email: String, password: String): ApiResult<LoginResponse> {
        val result = safeApiCall(json) {
            authApi.authLogin(PartnerLoginCommand(email = email, password = password, rememberMe = true))
        }

        if (result is ApiResult.Success && result.data.isEmailConfirmed == true) {
            val token = result.data.token.orEmpty()
            val responseUserId = result.data.userId.orEmpty()
            val isConfirmed = result.data.isEmailConfirmed == true

            // Persist what login returned first so the follow-up
            // GetCurrentEmployee call has an Authorization header.
            tokenManager.saveAuthData(
                token = token,
                userId = responseUserId,
                email = email,
                isEmailConfirmed = isConfirmed,
            )

            // Fetch the employee profile to get the canonical id + name.
            // Stays best-effort — if this fails we still have a valid
            // session, just without the cached name strings.
            val employeeResult = safeApiCall(json) {
                employeeApi.employeeGetCurrentEmployee()
            }
            if (employeeResult is ApiResult.Success) {
                tokenManager.saveAuthData(
                    token = token,
                    userId = employeeResult.data.id.orEmpty(),
                    email = employeeResult.data.email.orEmpty(),
                    isEmailConfirmed = isConfirmed,
                )
                tokenManager.saveUserName(
                    firstName = employeeResult.data.firstName,
                    lastName = employeeResult.data.lastName,
                )
            }
        }

        return result.toDomainLoginResponse(fallbackEmail = email)
    }

    override suspend fun register(request: RegisterRequest): ApiResult<RegisterResponse> {
        // Backend's RegisterEmployeeCommand has no confirmPassword/phoneNumber
        // fields (intentional — see Auth/PartnerRegister.cs). We discard those
        // here rather than failing them at the wire. `language` is required
        // and was the cause of the "Language field is required" 400 we hit
        // pre-migration; we pass the device locale.
        val command = RegisterEmployeeCommand(
            email = request.email,
            password = request.password,
            firstName = request.firstName,
            lastName = request.lastName,
            language = currentLanguage(),
        )
        val result = safeApiCall(json) { authApi.authRegisterEmployee(command) }
        return result.toDomainRegisterResponse(email = request.email)
    }

    override suspend fun register(
        email: String,
        password: String,
        firstName: String,
        lastName: String,
        phoneNumber: String,
    ): ApiResult<RegisterResponse> = register(
        RegisterRequest(
            email = email,
            password = password,
            confirmPassword = password,
            firstName = firstName,
            lastName = lastName,
            phoneNumber = phoneNumber,
        ),
    )

    override suspend fun confirmEmail(email: String, token: String): ApiResult<LoginResponse> {
        // Backend ConfirmUserEmailCommand only carries `code` — it pulls the
        // user from the JWT context (or unauth flow uses the code alone).
        // The `email` param stays in the interface for API stability; we
        // pass it on to the cached session metadata below.
        val result = safeApiCall(json) {
            authApi.authConfirmUserEmail(ConfirmUserEmailCommand(code = token))
        }

        if (result is ApiResult.Success) {
            val data = result.data
            tokenManager.saveAuthData(
                token = data.token.orEmpty(),
                userId = data.userId.orEmpty(),
                email = data.email ?: email,
                isEmailConfirmed = true,
            )
            // JwtTokenResponse doesn't carry firstName/lastName in partner's
            // contract — names come from the EmployeeItem fetched on first
            // login. Skip the saveUserName here; the next sign-in will populate.
        }

        return result.toDomainLoginResponse(fallbackEmail = email)
    }

    override suspend fun resendConfirmationEmail(email: String): ApiResult<Unit> {
        val result = safeApiCall(json) {
            authApi.authResendConfirmationEmail(
                ResendConfirmationEmailCommand(email = email, language = currentLanguage()),
            )
        }
        return result.mapToUnit()
    }

    override suspend fun forgotPassword(email: String): ApiResult<Unit> {
        val result = safeApiCall(json) {
            authApi.authForgotPassword(
                RequestPasswordChangeCommand(email = email, language = currentLanguage()),
            )
        }
        return result.mapToUnit()
    }

    override suspend fun refreshToken(): ApiResult<LoginResponse> {
        // Biometric path: we validate the current token by hitting a protected
        // endpoint instead of explicitly refreshing — the partner host does
        // its own refresh internally via the AuthAuthenticator on 401. If
        // GetCurrentEmployee returns OK the token is still valid; we
        // reconstruct a LoginResponse from cached state so callers see the
        // same shape as the login() success path.
        val employeeResult = safeApiCall(json) { employeeApi.employeeGetCurrentEmployee() }
        return when (employeeResult) {
            is ApiResult.Success -> ApiResult.Success(
                LoginResponse(
                    token = tokenManager.getToken().orEmpty(),
                    email = tokenManager.getUserEmail().orEmpty(),
                    userId = employeeResult.data.id.orEmpty(),
                    isEmailConfirmed = tokenManager.isEmailConfirmed(),
                ),
            )
            is ApiResult.Error -> ApiResult.Error(employeeResult.error)
        }
    }

    override fun logout() {
        tokenManager.clearAuthData()
    }

    override fun isLoggedIn(): Boolean = tokenManager.hasToken()

    override fun isEmailConfirmed(): Boolean = tokenManager.isEmailConfirmed()

    private fun currentLanguage(): String = Locale.getDefault().language.ifEmpty { "en" }
}

// ─── Generated DTO → partner-domain shape mappers ─────────────────────
//
// JwtTokenResponse fields are all nullable per OpenAPI; we coalesce to
// the LoginResponse defaults (empty string + false) so VMs never see a
// null on a field they expect to be present.

private fun ApiResult<JwtTokenResponse>.toDomainLoginResponse(
    fallbackEmail: String,
): ApiResult<LoginResponse> = when (this) {
    is ApiResult.Success -> ApiResult.Success(
        LoginResponse(
            token = data.token.orEmpty(),
            userId = data.userId.orEmpty(),
            email = data.email ?: fallbackEmail,
            isEmailConfirmed = data.isEmailConfirmed == true,
        ),
    )
    is ApiResult.Error -> ApiResult.Error(error)
}

private fun ApiResult<Boolean>.toDomainRegisterResponse(
    email: String,
): ApiResult<RegisterResponse> = when (this) {
    is ApiResult.Success -> ApiResult.Success(
        // Backend returns `true` on success; userId comes back on subsequent
        // confirm-email step. Email is the only thing we can carry forward.
        RegisterResponse(email = email),
    )
    is ApiResult.Error -> ApiResult.Error(error)
}

private fun <T> ApiResult<T>.mapToUnit(): ApiResult<Unit> = when (this) {
    is ApiResult.Success -> ApiResult.Success(Unit)
    is ApiResult.Error -> ApiResult.Error(error)
}
