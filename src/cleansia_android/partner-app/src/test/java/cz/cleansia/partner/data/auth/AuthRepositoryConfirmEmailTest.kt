package cz.cleansia.partner.data.auth

import cz.cleansia.core.auth.SessionScopedCache
import cz.cleansia.core.auth.TokenStore
import cz.cleansia.core.consent.SignupConsentRepository
import cz.cleansia.core.network.ApiResult
import cz.cleansia.core.notifications.PushTokenRepository
import cz.cleansia.partner.api.client.AuthApi
import cz.cleansia.partner.api.client.EmployeeApi
import cz.cleansia.partner.api.model.EmployeeItem
import cz.cleansia.partner.api.model.JwtTokenResponse
import cz.cleansia.partner.core.auth.UserProfileData
import cz.cleansia.partner.core.auth.UserProfileStore
import io.mockk.coEvery
import io.mockk.coVerify
import io.mockk.mockk
import kotlinx.coroutines.test.runTest
import kotlinx.serialization.json.Json
import okhttp3.MediaType.Companion.toMediaType
import okhttp3.ResponseBody.Companion.toResponseBody
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Test
import retrofit2.Response

/**
 * A cleaner who registers *and* confirms on the same device never runs
 * [AuthRepositoryImpl.login] — the confirm-email response issues the session
 * directly. `JwtTokenResponse` carries the **user** id, never the employee id,
 * so before this fix that session held `employeeId = null` for its whole 30-day
 * life: empty invoices, an unscoped dashboard, empty My-Active/My-Completed
 * order tabs and a hard error screen on Period Pay.
 *
 * These tests pin that confirm-email hydrates the employee exactly like login
 * does, and that a failed hydration is tolerated rather than blocking the
 * session (the id is back-filled lazily by
 * [cz.cleansia.partner.core.auth.EmployeeIdResolver]).
 */
class AuthRepositoryConfirmEmailTest {

    private val authApi = mockk<AuthApi>()
    private val authenticatedAuthApi = mockk<AuthApi>()
    private val employeeApi = mockk<EmployeeApi>()
    private val tokenStore = mockk<TokenStore>(relaxed = true)
    private val pushTokenRepository = mockk<PushTokenRepository>(relaxed = true)
    private val cache = mockk<SessionScopedCache>(relaxed = true)

    private val json = Json { ignoreUnknownKeys = true; isLenient = true }

    /**
     * In-memory stand-in for the DataStore-backed store: the assertion is about
     * what ends up persisted, so a relaxed mock that always answers `null` from
     * `current()` would make it vacuous.
     */
    private val signupConsent = mockk<SignupConsentRepository>(relaxed = true)

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

    private fun confirmResponse() = Response.success(
        JwtTokenResponse(
            // Unsigned but structurally valid — JwtDecoder only reads `exp`,
            // and a decode failure only changes the local expiry fallback.
            token = "header.eyJleHAiOjQxMDI0NDQ4MDB9.sig",
            isEmailConfirmed = true,
            hasAdminAccess = false,
            userId = "user-9",
            email = "fresh@cleansia.cz",
            refreshToken = "refresh-9",
            role = "Employee",
        ),
    )

    private fun ApiResult<LoginOutcome>.isAuthenticated(): Boolean =
        this is ApiResult.Success && data is LoginOutcome.Authenticated

    @Test
    fun confirmEmail_hydratesEmployeeId() = runTest {
        coEvery { authApi.authConfirmUserEmail(any()) } returns confirmResponse()
        coEvery { employeeApi.employeeGetCurrentEmployee() } returns Response.success(
            EmployeeItem(id = "emp-9", firstName = "Jana", lastName = "Nováková"),
        )

        val outcome = newRepository().confirmEmail("fresh@cleansia.cz", "123456")

        assertTrue(outcome.isAuthenticated())
        // The whole point: a session created by confirm-email is scoped.
        assertEquals("emp-9", storedProfile?.employeeId)
        assertEquals("Jana", storedProfile?.firstName)
        coVerify(exactly = 1) { employeeApi.employeeGetCurrentEmployee() }
    }

    @Test
    fun confirmEmail_stillSignsInWhenTheEmployeeFetchFails() = runTest {
        coEvery { authApi.authConfirmUserEmail(any()) } returns confirmResponse()
        coEvery { employeeApi.employeeGetCurrentEmployee() } returns Response.error(
            500,
            "boom".toResponseBody("text/plain".toMediaType()),
        )

        val outcome = newRepository().confirmEmail("fresh@cleansia.cz", "123456")

        // Best-effort by design — the token is good, so blocking the sign-in on
        // a secondary call would be worse than a lazily back-filled id.
        assertTrue(outcome.isAuthenticated())
        assertNull(storedProfile?.employeeId)
    }
}
