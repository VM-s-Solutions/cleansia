package cz.cleansia.partner.domain.repositories

import cz.cleansia.partner.core.network.ApiResult
import cz.cleansia.partner.core.network.ApiService
import cz.cleansia.partner.core.network.safeApiCall
import cz.cleansia.partner.core.storage.TokenManager
import cz.cleansia.partner.domain.models.auth.ConfirmEmailRequest
import cz.cleansia.partner.domain.models.auth.ForgotPasswordRequest
import cz.cleansia.partner.domain.models.auth.LoginRequest
import cz.cleansia.partner.domain.models.auth.LoginResponse
import cz.cleansia.partner.domain.models.auth.RegisterRequest
import cz.cleansia.partner.domain.models.auth.RegisterResponse
import cz.cleansia.partner.domain.models.auth.ResendConfirmationRequest
import kotlinx.serialization.json.Json
import javax.inject.Inject
import javax.inject.Singleton

interface AuthRepository {
    suspend fun login(email: String, password: String): ApiResult<LoginResponse>
    suspend fun register(request: RegisterRequest): ApiResult<RegisterResponse>
    suspend fun register(
        email: String,
        password: String,
        firstName: String,
        lastName: String,
        phoneNumber: String
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
    private val apiService: ApiService,
    private val tokenManager: TokenManager,
    private val json: Json
) : AuthRepository {

    override suspend fun login(email: String, password: String): ApiResult<LoginResponse> {
        val result = safeApiCall(json) {
            apiService.login(LoginRequest(email, password))
        }

        if (result is ApiResult.Success && result.data.isEmailConfirmed) {
            // First save the token so we can make authenticated requests
            tokenManager.saveAuthData(
                token = result.data.token,
                userId = "", // Will be updated after fetching employee
                email = email,
                isEmailConfirmed = result.data.isEmailConfirmed
            )

            // Fetch the current employee to get the employee ID
            val employeeResult = safeApiCall(json) {
                apiService.getCurrentEmployee()
            }

            if (employeeResult is ApiResult.Success) {
                // Update the stored user ID with the actual employee ID
                tokenManager.saveAuthData(
                    token = result.data.token,
                    userId = employeeResult.data.id,
                    email = employeeResult.data.email,
                    isEmailConfirmed = result.data.isEmailConfirmed
                )
            }
        }

        return result
    }

    override suspend fun register(request: RegisterRequest): ApiResult<RegisterResponse> {
        return safeApiCall(json) {
            apiService.register(request)
        }
    }

    override suspend fun register(
        email: String,
        password: String,
        firstName: String,
        lastName: String,
        phoneNumber: String
    ): ApiResult<RegisterResponse> {
        val request = RegisterRequest(
            email = email,
            password = password,
            confirmPassword = password,
            firstName = firstName,
            lastName = lastName,
            phoneNumber = phoneNumber
        )
        return register(request)
    }

    override suspend fun confirmEmail(email: String, token: String): ApiResult<LoginResponse> {
        val result = safeApiCall(json) {
            apiService.confirmEmail(ConfirmEmailRequest(email, token))
        }

        if (result is ApiResult.Success) {
            tokenManager.saveAuthData(
                token = result.data.token,
                userId = result.data.actualUserId,
                email = result.data.email,
                isEmailConfirmed = true
            )
        }

        return result
    }

    override suspend fun resendConfirmationEmail(email: String): ApiResult<Unit> {
        return safeApiCall(json) {
            apiService.resendConfirmation(ResendConfirmationRequest(email))
        }
    }

    override suspend fun forgotPassword(email: String): ApiResult<Unit> {
        return safeApiCall(json) {
            apiService.forgotPassword(ForgotPasswordRequest(email))
        }
    }

    override suspend fun refreshToken(): ApiResult<LoginResponse> {
        // For biometric login, we verify the current token is still valid
        // by checking if we can access a protected endpoint
        val employeeResult = safeApiCall(json) {
            apiService.getCurrentEmployee()
        }

        return when (employeeResult) {
            is ApiResult.Success -> {
                val email = tokenManager.getUserEmail() ?: ""
                val isEmailConfirmed = tokenManager.isEmailConfirmed()
                val token = tokenManager.getToken() ?: ""
                ApiResult.Success(
                    LoginResponse(
                        token = token,
                        email = email,
                        userId = employeeResult.data.id,
                        isEmailConfirmed = isEmailConfirmed
                    )
                )
            }
            is ApiResult.Error -> ApiResult.Error(employeeResult.error)
        }
    }

    override fun logout() {
        tokenManager.clearAuthData()
    }

    override fun isLoggedIn(): Boolean = tokenManager.hasToken()

    override fun isEmailConfirmed(): Boolean = tokenManager.isEmailConfirmed()
}
