package cz.cleansia.partner.data.auth

import cz.cleansia.core.auth.SessionScopedCache
import cz.cleansia.core.auth.TokenStore
import cz.cleansia.core.consent.SignupConsentRepository
import cz.cleansia.core.notifications.PushTokenRepository
import cz.cleansia.partner.api.client.AuthApi
import cz.cleansia.partner.api.client.EmployeeApi
import cz.cleansia.partner.api.model.ConfirmUserEmailCommand
import cz.cleansia.partner.api.model.JwtTokenResponse
import cz.cleansia.partner.api.model.MobilePartnerLoginCommand
import cz.cleansia.partner.api.model.RegisterEmployeeCommand
import cz.cleansia.partner.api.model.RequestPasswordChangeCommand
import cz.cleansia.partner.api.model.ResendConfirmationEmailCommand
import cz.cleansia.partner.core.auth.UserProfileStore
import cz.cleansia.partner.core.network.NetworkModule
import io.mockk.coEvery
import io.mockk.every
import io.mockk.mockk
import io.mockk.slot
import io.mockk.verify
import kotlinx.coroutines.test.runTest
import kotlinx.serialization.encodeToString
import kotlinx.serialization.json.Json
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Test
import retrofit2.Response

/**
 * A cleaner locked out after fumbling their own password on their own handset is let through by a
 * still-valid refresh token from a previous session — the same bypass the browser has always had via
 * its refresh cookie. The handset presents what it stored; the server hashes it and requires the row
 * to be alive and bound to the account being signed into, so nothing here decides trust.
 */
class AuthRepositoryTrustedDeviceTest {

    private val anonymousAuthApi = mockk<AuthApi>()
    private val authenticatedAuthApi = mockk<AuthApi>()
    private val employeeApi = mockk<EmployeeApi>(relaxed = true)
    private val tokenStore = mockk<TokenStore>(relaxed = true)
    private val userProfileStore = mockk<UserProfileStore>(relaxed = true)
    private val pushTokenRepository = mockk<PushTokenRepository>(relaxed = true)
    private val signupConsent = mockk<SignupConsentRepository>(relaxed = true)
    private val cache = mockk<SessionScopedCache>(relaxed = true)

    private val json = Json { ignoreUnknownKeys = true; isLenient = true }

    private fun newRepository() = AuthRepositoryImpl(
        authApi = anonymousAuthApi,
        authenticatedAuthApi = { authenticatedAuthApi },
        employeeApi = employeeApi,
        tokenStore = tokenStore,
        userProfileStore = userProfileStore,
        json = json,
        pushTokenRepository = pushTokenRepository,
        sessionScopedCaches = { setOf(cache) },
        signupConsent = { signupConsent },
    )

    private fun storedTokens(refreshToken: String = "stored-refresh") = TokenStore.Tokens(
        accessToken = "stored-access",
        accessTokenExpiresAt = 1L,
        refreshToken = refreshToken,
        refreshTokenExpiresAt = 2L,
    )

    private fun issuedTokens() = JwtTokenResponse(
        token = "h.p.s",
        isEmailConfirmed = true,
        userId = "u-1",
        email = "cleaner@example.com",
        refreshToken = "fresh-r",
        refreshTokenExpiresAt = "2099-01-01T00:00:00Z",
    )

    private suspend fun capturedLogin(): MobilePartnerLoginCommand {
        val captured = slot<MobilePartnerLoginCommand>()
        coEvery { anonymousAuthApi.authLogin(capture(captured)) } returns Response.success(issuedTokens())
        newRepository().login("cleaner@example.com", "submitted-password", rememberMe = true)
        return captured.captured
    }

    @Test
    fun login_sendsTheStoredRefreshTokenAsTheTrustedDeviceMarker() = runTest {
        every { tokenStore.current() } returns storedTokens()

        assertEquals("stored-refresh", capturedLogin().trustedDeviceToken)
    }

    @Test
    fun login_omitsTheMarkerWhenNoSessionIsStored() = runTest {
        every { tokenStore.current() } returns null

        assertNull(capturedLogin().trustedDeviceToken)
    }

    /**
     * [AuthRepositoryImpl.persistTokens] stores `refreshToken.orEmpty()`, so a blank value is
     * reachable. Blank must read as "no previous session", not as a marker that matches nothing.
     */
    @Test
    fun login_omitsTheMarkerWhenTheStoredRefreshTokenIsBlank() = runTest {
        every { tokenStore.current() } returns storedTokens(refreshToken = "")

        assertNull(capturedLogin().trustedDeviceToken)
    }

    @Test
    fun login_doesNotSourceTheMarkerFromTheAccessTokenOrTheSubmittedCredentials() = runTest {
        every { tokenStore.current() } returns storedTokens()

        val body = capturedLogin()

        assertEquals("stored-refresh", body.trustedDeviceToken)
        assertTrue(body.trustedDeviceToken != body.password)
        assertTrue(body.trustedDeviceToken != body.email)
        assertTrue(body.trustedDeviceToken != "stored-access")
    }

    /**
     * Serialized through the production converter config, because whether the key reaches the wire at
     * all is a property of that config and not of the generated command alone.
     */
    @Test
    fun loginCommand_serializesTheMarkerUnderTheNameTheBackendBinds() {
        val body = NetworkModule.provideJson().encodeToString(
            MobilePartnerLoginCommand(
                email = "cleaner@example.com",
                password = "pw",
                rememberMe = true,
                trustedDeviceToken = "stored-refresh",
            ),
        )

        assertTrue(body, body.contains("\"trustedDeviceToken\":\"stored-refresh\""))
    }

    @Test
    fun login_withNoStoredSession_leavesTheMarkerOffTheWireEntirely() = runTest {
        every { tokenStore.current() } returns null

        val body = NetworkModule.provideJson().encodeToString(capturedLogin())

        assertTrue(body, !body.contains("trustedDeviceToken"))
    }

    @Test
    fun theMarkerRidesTheLoginBodyAndNoOtherAuthRequest() = runTest {
        every { tokenStore.current() } returns storedTokens()
        val wireJson = NetworkModule.provideJson()

        val register = slot<RegisterEmployeeCommand>()
        val confirm = slot<ConfirmUserEmailCommand>()
        val resend = slot<ResendConfirmationEmailCommand>()
        val forgot = slot<RequestPasswordChangeCommand>()

        coEvery { anonymousAuthApi.authRegisterEmployee(capture(register)) } returns Response.success(true)
        coEvery { anonymousAuthApi.authConfirmUserEmail(capture(confirm)) } returns Response.success(issuedTokens())
        coEvery { anonymousAuthApi.authResendConfirmationEmail(capture(resend)) } returns Response.success(true)
        coEvery { anonymousAuthApi.authForgotPassword(capture(forgot)) } returns Response.success(Unit)

        val repo = newRepository()
        repo.register("cleaner@example.com", "pw", "Ada", "Lovelace", "en")
        repo.confirmEmail("cleaner@example.com", "123456")
        repo.resendConfirmation("cleaner@example.com", "en")
        repo.forgotPassword("cleaner@example.com", "en")

        listOf(
            wireJson.encodeToString(register.captured),
            wireJson.encodeToString(confirm.captured),
            wireJson.encodeToString(resend.captured),
            wireJson.encodeToString(forgot.captured),
        ).forEach { assertTrue(it, !it.contains("trustedDeviceToken")) }

        verify(exactly = 0) { tokenStore.current() }
    }
}
