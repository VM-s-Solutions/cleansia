package cz.cleansia.partner.navigation

import cz.cleansia.core.auth.TokenStore
import cz.cleansia.core.network.ApiError
import cz.cleansia.core.network.ApiResult
import cz.cleansia.partner.core.settings.AppSettingsRepository
import cz.cleansia.partner.data.auth.AuthRepository
import cz.cleansia.partner.data.profile.ProfileRepository
import cz.cleansia.partner.testing.MainDispatcherRule
import io.mockk.coEvery
import io.mockk.coVerify
import io.mockk.every
import io.mockk.mockk
import kotlinx.coroutines.CompletableDeferred
import kotlinx.coroutines.ExperimentalCoroutinesApi
import kotlinx.coroutines.test.advanceUntilIdle
import kotlinx.coroutines.test.runTest
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Rule
import org.junit.Test

/**
 * The unreachable splash's sign-out, while its wipe is still running.
 *
 * The token stays on disk until `logout()`'s two network calls give up, which on the hanging
 * server this screen exists for can take the full OkHttp timeouts. A resolve in that window passes
 * the session check, and if the network has come back it lands on an outcome that navigates —
 * popping the Splash entry and cancelling the wipe half-done.
 */
@OptIn(ExperimentalCoroutinesApi::class)
class SplashViewModelSignOutTest {

    @get:Rule
    val mainRule = MainDispatcherRule()

    private val tokenStore: TokenStore = mockk()
    private val appSettingsRepository: AppSettingsRepository = mockk()
    private val profileRepository: ProfileRepository = mockk()
    private val authRepository: AuthRepository = mockk()

    @Test
    fun `a retry while the sign-out wipe runs does not resolve again`() = runTest {
        every { tokenStore.current() } returns TokenStore.Tokens(
            accessToken = "access",
            accessTokenExpiresAt = 0L,
            refreshToken = "refresh",
            refreshTokenExpiresAt = Long.MAX_VALUE,
        )
        coEvery { profileRepository.getRegistrationStatus() } returns ApiResult.Error(ApiError.Network("down"))
        val wipe = CompletableDeferred<Unit>()
        coEvery { authRepository.logout() } coAnswers { wipe.await() }

        val viewModel = SplashViewModel(tokenStore, appSettingsRepository, profileRepository, authRepository)
        viewModel.resolve()
        advanceUntilIdle()
        assertEquals(SplashOutcome.Unreachable, viewModel.outcome.value)

        var signedOut = false
        viewModel.signOut { signedOut = true }
        advanceUntilIdle()
        assertTrue(viewModel.isSigningOut.value)

        viewModel.resolve()
        advanceUntilIdle()

        coVerify(exactly = 1) { profileRepository.getRegistrationStatus() }
        assertEquals(SplashOutcome.Unreachable, viewModel.outcome.value)

        wipe.complete(Unit)
        advanceUntilIdle()
        assertTrue(signedOut)
    }
}
