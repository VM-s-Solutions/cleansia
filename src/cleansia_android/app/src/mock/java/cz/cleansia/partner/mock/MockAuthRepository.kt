package cz.cleansia.partner.mock

import cz.cleansia.partner.core.network.ApiResult
import cz.cleansia.partner.core.storage.TokenManager
import cz.cleansia.partner.domain.models.auth.LoginResponse
import cz.cleansia.partner.domain.models.auth.RegisterRequest
import cz.cleansia.partner.domain.models.auth.RegisterResponse
import cz.cleansia.partner.domain.repositories.AuthRepository
import kotlinx.coroutines.delay

class MockAuthRepository(
    private val tokenManager: TokenManager
) : AuthRepository {

    private var loggedIn = true
    private var emailConfirmed = true

    init {
        // Pre-seed TokenManager so the app auto-navigates to Main
        tokenManager.saveAuthData(
            token = MockDataProvider.MOCK_TOKEN,
            userId = MockDataProvider.MOCK_EMPLOYEE_ID,
            email = MockDataProvider.MOCK_EMAIL,
            isEmailConfirmed = true
        )
        tokenManager.saveUserName(MockDataProvider.MOCK_FIRST_NAME, MockDataProvider.MOCK_LAST_NAME)
    }

    override suspend fun login(email: String, password: String): ApiResult<LoginResponse> {
        delay(500)
        loggedIn = true
        emailConfirmed = true
        tokenManager.saveAuthData(
            token = MockDataProvider.MOCK_TOKEN,
            userId = MockDataProvider.MOCK_EMPLOYEE_ID,
            email = email,
            isEmailConfirmed = true
        )
        tokenManager.saveUserName(MockDataProvider.MOCK_FIRST_NAME, MockDataProvider.MOCK_LAST_NAME)
        return ApiResult.Success(MockDataProvider.loginResponse())
    }

    override suspend fun register(request: RegisterRequest): ApiResult<RegisterResponse> {
        delay(800)
        return ApiResult.Success(MockDataProvider.registerResponse())
    }

    override suspend fun register(
        email: String,
        password: String,
        firstName: String,
        lastName: String,
        phoneNumber: String
    ): ApiResult<RegisterResponse> {
        delay(800)
        return ApiResult.Success(MockDataProvider.registerResponse())
    }

    override suspend fun confirmEmail(email: String, token: String): ApiResult<LoginResponse> {
        delay(500)
        emailConfirmed = true
        tokenManager.saveAuthData(
            token = MockDataProvider.MOCK_TOKEN,
            userId = MockDataProvider.MOCK_EMPLOYEE_ID,
            email = email,
            isEmailConfirmed = true
        )
        return ApiResult.Success(MockDataProvider.loginResponse())
    }

    override suspend fun resendConfirmationEmail(email: String): ApiResult<Unit> {
        delay(300)
        return ApiResult.Success(Unit)
    }

    override suspend fun forgotPassword(email: String): ApiResult<Unit> {
        delay(300)
        return ApiResult.Success(Unit)
    }

    override suspend fun refreshToken(): ApiResult<LoginResponse> {
        delay(200)
        return ApiResult.Success(MockDataProvider.loginResponse())
    }

    override fun logout() {
        loggedIn = false
        tokenManager.clearAuthData()
    }

    override fun isLoggedIn(): Boolean = loggedIn

    override fun isEmailConfirmed(): Boolean = emailConfirmed
}
