package cz.cleansia.partner.features.settings

import cz.cleansia.core.auth.TokenStore
import cz.cleansia.core.network.ApiError
import cz.cleansia.core.network.ApiResult
import cz.cleansia.partner.core.settings.AppSettings
import cz.cleansia.partner.core.settings.AppSettingsRepository
import cz.cleansia.partner.core.settings.LanguagePreference
import cz.cleansia.partner.core.settings.LiveLanguagePreferenceSync
import cz.cleansia.partner.data.auth.AuthRepository
import cz.cleansia.partner.data.user.CurrentUser
import cz.cleansia.partner.data.user.UserRepository
import cz.cleansia.partner.testing.MainDispatcherRule
import io.mockk.coEvery
import io.mockk.coVerify
import io.mockk.coVerifyOrder
import io.mockk.every
import io.mockk.mockk
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.ExperimentalCoroutinesApi
import kotlinx.coroutines.SupervisorJob
import kotlinx.coroutines.flow.flowOf
import kotlinx.coroutines.test.StandardTestDispatcher
import kotlinx.coroutines.test.TestScope
import kotlinx.coroutines.test.advanceUntilIdle
import kotlinx.coroutines.test.runTest
import org.junit.Assert.assertEquals
import org.junit.Before
import org.junit.Rule
import org.junit.Test

/**
 * The signed-in chooser. This is the site that decides which language a cleaner's payout invoice PDF
 * is rendered in, so the local write alone is the whole defect: the app switches and the tax document
 * they file keeps whatever signup sent.
 *
 * Runs against the real [LiveLanguagePreferenceSync] rather than a mock so the site's own answer to
 * "is there a session" is exercised, not assumed.
 */
@OptIn(ExperimentalCoroutinesApi::class)
class SettingsLanguageSyncTest {

    @get:Rule
    val mainRule = MainDispatcherRule()

    private val appSettingsRepository: AppSettingsRepository = mockk(relaxed = true)
    private val authRepository: AuthRepository = mockk(relaxed = true)
    private val userRepository: UserRepository = mockk(relaxed = true)
    private val tokenStore: TokenStore = mockk(relaxed = true)

    @Before
    fun setUp() {
        every { appSettingsRepository.settings } returns flowOf(AppSettings())
        coEvery { appSettingsRepository.emailLanguageTag() } returns "cs"
        every { tokenStore.current() } returns tokens()
        coEvery { userRepository.getCurrentUser() } returns ApiResult.Success(profile())
        coEvery { userRepository.updateCurrentUser(any(), any(), any(), any(), any()) } returns
            ApiResult.Success(Unit)
    }

    private fun TestScope.viewModel() = SettingsViewModel(
        appSettingsRepository = appSettingsRepository,
        authRepository = authRepository,
        languageSync = LiveLanguagePreferenceSync(tokenStore, userRepository, appSettingsRepository, syncScope()),
    )

    @Test
    fun `choosing a language persists it and then pushes it to the server`() = runTest {
        viewModel().setLanguage(LanguagePreference.Czech)
        advanceUntilIdle()

        coVerifyOrder {
            appSettingsRepository.setLanguage(LanguagePreference.Czech)
            userRepository.updateCurrentUser(
                firstName = "Ondrej",
                lastName = "Novak",
                phoneNumber = "+420777111222",
                birthDate = "1982-09-04",
                languageCode = "cs",
            )
        }
    }

    /**
     * "System" persists as a null tag and the server cannot see this handset's locale, so what goes on
     * the wire is the resolved tag — a raw one would fail the backend's language validator outright.
     */
    @Test
    fun `following the system pushes the resolved tag not the sentinel`() = runTest {
        coEvery { appSettingsRepository.emailLanguageTag() } returns "sk"

        viewModel().setLanguage(LanguagePreference.System)
        advanceUntilIdle()

        coVerify(exactly = 1) {
            userRepository.updateCurrentUser(any(), any(), any(), any(), languageCode = "sk")
        }
        assertEquals(null, LanguagePreference.System.tag)
    }

    /**
     * The person asked for this language now, and the app is already speaking it. A server that will
     * not take it must not walk that back — the local write stands, exactly once, un-reverted.
     */
    @Test
    fun `a failed push leaves the local preference applied`() = runTest {
        val persisted = mutableListOf<LanguagePreference>()
        coEvery { appSettingsRepository.setLanguage(capture(persisted)) } returns Unit
        coEvery { userRepository.updateCurrentUser(any(), any(), any(), any(), any()) } returns
            ApiResult.Error(ApiError.BadRequest("nope"))

        viewModel().setLanguage(LanguagePreference.Czech)
        advanceUntilIdle()

        assertEquals(listOf(LanguagePreference.Czech), persisted)
    }

    /** Stands in for the injected `@ApplicationScope`, on the scheduler `advanceUntilIdle` drives. */
    private fun TestScope.syncScope() =
        CoroutineScope(StandardTestDispatcher(testScheduler) + SupervisorJob())

    private fun tokens() = TokenStore.Tokens(
        accessToken = "access",
        accessTokenExpiresAt = Long.MAX_VALUE,
        refreshToken = "refresh",
        refreshTokenExpiresAt = Long.MAX_VALUE,
    )

    private fun profile() = CurrentUser(
        email = "ondrej@example.com",
        firstName = "Ondrej",
        lastName = "Novak",
        phoneNumber = "+420777111222",
        birthDate = "1982-09-04",
        preferredLanguageCode = "en",
        profilePhotoUrl = null,
    )
}
