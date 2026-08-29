package cz.cleansia.partner.data.auth

import cz.cleansia.core.auth.SessionScopedCache
import cz.cleansia.core.auth.TokenStore
import cz.cleansia.core.consent.SignupConsentRepository
import cz.cleansia.core.notifications.PushTokenRepository
import cz.cleansia.partner.api.client.AuthApi
import cz.cleansia.partner.api.client.EmployeeApi
import cz.cleansia.partner.api.model.LogoutCommand
import cz.cleansia.partner.core.auth.UserProfileStore
import io.mockk.coEvery
import io.mockk.coVerify
import io.mockk.every
import io.mockk.mockk
import io.mockk.verify
import kotlinx.coroutines.test.runTest
import kotlinx.serialization.json.Json
import org.junit.Test
import retrofit2.Response

/**
 * `/api/Auth/Logout` is `[Authorize]` server-side. Issued on the anonymous
 * client it is a guaranteed 401, and the surrounding `runCatching` swallows it —
 * so "Log out" wiped the local tokens while the refresh token stayed live
 * server-side and could still mint access tokens.
 *
 * These tests pin WHICH client the call goes out on, plus the deliberate
 * best-effort behaviour that must survive the fix: a failing logout still wipes
 * local state, so an offline user is not trapped in a session.
 */
class AuthRepositoryLogoutTest {

    private val anonymousAuthApi = mockk<AuthApi>()
    private val authenticatedAuthApi = mockk<AuthApi>()
    private val employeeApi = mockk<EmployeeApi>()
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

    private fun storedTokens() = TokenStore.Tokens(
        accessToken = "a",
        accessTokenExpiresAt = 1L,
        refreshToken = "r",
        refreshTokenExpiresAt = 1L,
    )

    @Test
    fun logout_callsTheAuthenticatedClientNotTheAnonymousOne() = runTest {
        every { tokenStore.current() } returns storedTokens()
        coEvery { authenticatedAuthApi.authLogout(any()) } returns Response.success(Unit)

        newRepository().logout()

        // Asserting the INSTANCE, not merely that a logout happened, is the
        // whole test — the old code called the identically-shaped method on the
        // anonymous client and "succeeded" by ignoring the 401.
        coVerify(exactly = 1) { authenticatedAuthApi.authLogout(LogoutCommand(token = "r")) }
        coVerify(exactly = 0) { anonymousAuthApi.authLogout(any()) }
        verify(exactly = 1) { tokenStore.clear() }
    }

    @Test
    fun logout_stillWipesLocalStateWhenTheServerCallFails() = runTest {
        every { tokenStore.current() } returns storedTokens()
        coEvery { authenticatedAuthApi.authLogout(any()) } throws java.io.IOException("offline")

        newRepository().logout()

        // Best-effort by design: a cleaner with no signal must still end up
        // signed out on the handset.
        verify(exactly = 1) { tokenStore.clear() }
        coVerify(exactly = 1) { cache.clear() }
    }

    @Test
    fun logout_skipsTheServerCallWhenNoRefreshTokenIsStored() = runTest {
        every { tokenStore.current() } returns null

        newRepository().logout()

        coVerify(exactly = 0) { authenticatedAuthApi.authLogout(any()) }
        coVerify(exactly = 0) { anonymousAuthApi.authLogout(any()) }
        verify(exactly = 1) { tokenStore.clear() }
    }
}
