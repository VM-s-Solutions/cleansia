package cz.cleansia.partner.data.auth

import cz.cleansia.core.auth.SessionScopedCache
import cz.cleansia.core.auth.TokenStore
import cz.cleansia.core.consent.SignupConsentRepository
import cz.cleansia.core.network.ApiError
import cz.cleansia.core.network.ApiResult
import cz.cleansia.core.notifications.PushTokenRepository
import cz.cleansia.partner.api.client.AuthApi
import cz.cleansia.partner.api.client.EmployeeApi
import cz.cleansia.partner.api.model.JwtTokenResponse
import cz.cleansia.partner.core.auth.UserProfileData
import cz.cleansia.partner.core.auth.UserProfileStore
import io.mockk.coEvery
import io.mockk.mockk
import kotlinx.coroutines.test.runTest
import kotlinx.serialization.json.Json
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Test
import retrofit2.Response

/**
 * `isEmailConfirmed` and `hasAdminAccess` are both non-nullable `bool` on the C# `JwtTokenResponse`,
 * and the generator types them optional anyway. The old `?: false` fell the wrong way on each: a
 * confirmed cleaner is bounced back to the confirm-email screen they already cleared, and the server's
 * own default for `HasAdminAccess` is `true`. Failing closed is the safe direction; it is not the same
 * as knowing the value, and neither reading leaves a trace.
 */
class AuthRepositorySessionFlagsTest {

    private val authApi = mockk<AuthApi>()
    private val authenticatedAuthApi = mockk<AuthApi>()
    private val employeeApi = mockk<EmployeeApi>(relaxed = true)
    private val tokenStore = mockk<TokenStore>(relaxed = true)
    private val pushTokenRepository = mockk<PushTokenRepository>(relaxed = true)
    private val cache = mockk<SessionScopedCache>(relaxed = true)
    private val signupConsent = mockk<SignupConsentRepository>(relaxed = true)

    private val json = Json { ignoreUnknownKeys = true; isLenient = true }

    private var storedProfile: UserProfileData? = null
    private val userProfileStore = mockk<UserProfileStore>().also { store ->
        coEvery { store.current() } answers { storedProfile }
        coEvery { store.save(any()) } answers { storedProfile = firstArg() }
        coEvery { store.updateEmployeeId(any()) } answers {
            storedProfile = storedProfile?.copy(employeeId = firstArg())
        }
    }

    private fun newRepository() = AuthRepositoryImpl(
        authApi = authApi,
        authenticatedAuthApi = { authenticatedAuthApi },
        employeeApi = employeeApi,
        tokenStore = tokenStore,
        userProfileStore = userProfileStore,
        json = json,
        pushTokenRepository = pushTokenRepository,
        sessionScopedCaches = { setOf(cache) },
        signupConsent = { signupConsent },
    )

    private fun tokenResponse(
        isEmailConfirmed: Boolean? = true,
        hasAdminAccess: Boolean? = false,
    ) = Response.success(
        JwtTokenResponse(
            token = "header.eyJleHAiOjQxMDI0NDQ4MDB9.sig",
            isEmailConfirmed = isEmailConfirmed,
            hasAdminAccess = hasAdminAccess,
            userId = "user-9",
            email = "jana@cleansia.cz",
            refreshToken = "refresh-9",
            role = "Employee",
        ),
    )

    private fun refusalFor(result: ApiResult<LoginOutcome>, field: String) {
        val error = (result as ApiResult.Error).error
        assertTrue("expected Server but was $error", error is ApiError.Server)
        assertTrue(
            "the refusal must name $field, but carried \"${(error as ApiError.Server).diagnostic}\"",
            error.diagnostic!!.startsWith("$field "),
        )
        assertTrue(
            "the rendered line must not leak $field, but was \"${error.getUserMessage()}\"",
            !error.getUserMessage().contains(field),
        )
    }

    @Test
    fun login_givenNoIsEmailConfirmed_refusesNamingTheFieldRatherThanBouncingToConfirmEmail() = runTest {
        coEvery { authApi.authLogin(any()) } returns tokenResponse(isEmailConfirmed = null)

        refusalFor(newRepository().login("jana@cleansia.cz", "pw", rememberMe = true), "isEmailConfirmed")
    }

    @Test
    fun login_givenNoHasAdminAccess_refusesNamingTheField() = runTest {
        coEvery { authApi.authLogin(any()) } returns tokenResponse(hasAdminAccess = null)

        refusalFor(newRepository().login("jana@cleansia.cz", "pw", rememberMe = true), "hasAdminAccess")
    }

    @Test
    fun login_givenARefusedFlag_persistsNoProfileAtAll() = runTest {
        coEvery { authApi.authLogin(any()) } returns tokenResponse(isEmailConfirmed = null)

        newRepository().login("jana@cleansia.cz", "pw", rememberMe = true)

        assertNull(storedProfile)
    }

    @Test
    fun confirmEmail_givenNoHasAdminAccess_refusesNamingTheField() = runTest {
        coEvery { authApi.authConfirmUserEmail(any()) } returns tokenResponse(hasAdminAccess = null)

        refusalFor(newRepository().confirmEmail("jana@cleansia.cz", "123456"), "hasAdminAccess")
    }

    @Test
    fun login_givenBothFlags_signsInAndKeepsTheValuesTheServerSent() = runTest {
        coEvery { authApi.authLogin(any()) } returns tokenResponse(isEmailConfirmed = true, hasAdminAccess = true)

        val outcome = newRepository().login("jana@cleansia.cz", "pw", rememberMe = true)

        assertTrue(outcome is ApiResult.Success)
        assertTrue(storedProfile!!.hasAdminAccess)
        assertTrue(storedProfile!!.isEmailConfirmed)
    }
}
