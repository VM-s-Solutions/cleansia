package cz.cleansia.partner.features.orders

import cz.cleansia.core.auth.TokenStore
import cz.cleansia.core.freshness.Staleness
import cz.cleansia.core.network.ApiResult
import cz.cleansia.partner.core.network.ApiErrorTranslator
import cz.cleansia.partner.core.settings.AppSettingsRepository
import cz.cleansia.partner.core.settings.LanguagePreference
import cz.cleansia.partner.api.model.RegistrationCompletionStatus
import cz.cleansia.partner.core.settings.LiveLanguagePreferenceSync
import cz.cleansia.partner.data.auth.AuthRepository
import cz.cleansia.partner.data.profile.ProfileRepository
import cz.cleansia.partner.data.user.CurrentUser
import cz.cleansia.partner.data.user.UserRepository
import cz.cleansia.partner.testing.MainDispatcherRule
import io.mockk.coEvery
import io.mockk.coVerify
import io.mockk.every
import io.mockk.mockk
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.ExperimentalCoroutinesApi
import kotlinx.coroutines.SupervisorJob
import kotlinx.coroutines.test.StandardTestDispatcher
import kotlinx.coroutines.test.TestScope
import kotlinx.coroutines.test.advanceUntilIdle
import kotlinx.coroutines.test.runTest
import org.junit.Assert.assertFalse
import org.junit.Assert.assertNotNull
import org.junit.Before
import org.junit.Rule
import org.junit.Test

/**
 * The lock screen replaces the whole app between signup and approval, so it is
 * the only place a cleaner who skipped the intro can change the app's language
 * while they wait. It persists through the same store as the intro and the
 * profile picker — the display preference and the confirmation-email language
 * must not drift onto separate keys.
 *
 * A cleaner on this screen HAS a session — they just have no approval — so the push must go out.
 * `[RequireCompleteProfile]` gates the mobile partner host's Order, Dashboard and EmployeePayroll
 * controllers; `UserController`, which serves this push, is deliberately not among them, for the same
 * reason `EmployeeController` is not: it is what an unapproved cleaner needs in order to stop being
 * one.
 */
@OptIn(ExperimentalCoroutinesApi::class)
class RegistrationLockLanguageTest {

    @get:Rule
    val mainRule = MainDispatcherRule()

    private val profileRepository: ProfileRepository = mockk(relaxed = true)
    private val authRepository: AuthRepository = mockk(relaxed = true)
    private val errorTranslator: ApiErrorTranslator = mockk(relaxed = true)
    private val appSettingsRepository: AppSettingsRepository = mockk(relaxed = true)
    private val userRepository: UserRepository = mockk(relaxed = true)
    private val tokenStore: TokenStore = mockk(relaxed = true)

    @Before
    fun setUp() {
        coEvery { appSettingsRepository.emailLanguageTag() } returns "uk"
        every { tokenStore.current() } returns TokenStore.Tokens(
            accessToken = "access",
            accessTokenExpiresAt = Long.MAX_VALUE,
            refreshToken = "refresh",
            refreshTokenExpiresAt = Long.MAX_VALUE,
        )
        coEvery { userRepository.getCurrentUser() } returns ApiResult.Success(unapprovedCleaner())
        coEvery { userRepository.updateCurrentUser(any(), any(), any(), any(), any()) } returns
            ApiResult.Success(Unit)
        // init now always fetches on a view model that holds no status, so this has to answer.
        coEvery { profileRepository.getRegistrationStatus() } returns
            ApiResult.Success(RegistrationCompletionStatus(hasCompletedProfile = false))
    }

    private fun TestScope.viewModel(): RegistrationLockViewModel {
        // Warm watermark. It no longer suppresses init's fetch — see
        // `a warm watermark does not strand a fresh view model on the spinner` below.
        every { profileRepository.getRegistrationStatusStaleness() } returns
            Staleness().apply { markFresh() }
        return RegistrationLockViewModel(
            profileRepository = profileRepository,
            authRepository = authRepository,
            errorTranslator = errorTranslator,
            appSettingsRepository = appSettingsRepository,
            languageSync = LiveLanguagePreferenceSync(tokenStore, userRepository, appSettingsRepository, syncScope()),
        )
    }

    @Test
    fun `setLanguage persists the choice to the settings store`() = runTest {
        viewModel().setLanguage(LanguagePreference.Ukrainian)
        advanceUntilIdle()

        coVerify(exactly = 1) { appSettingsRepository.setLanguage(LanguagePreference.Ukrainian) }
    }

    /**
     * The half-filled profile is the point: this cleaner has no phone and no birth date yet, and both
     * are fields the handler preserves when the client has nothing to say about them. Dropping the
     * push for them would leave the one population that lives on this screen unsynced.
     */
    @Test
    fun `an unapproved cleaner's choice still reaches the server`() = runTest {
        viewModel().setLanguage(LanguagePreference.Ukrainian)
        advanceUntilIdle()

        coVerify(exactly = 1) {
            userRepository.updateCurrentUser(
                firstName = "Ondrej",
                lastName = "Novak",
                phoneNumber = "",
                birthDate = null,
                languageCode = "uk",
            )
        }
    }

    /**
     * THE ONBOARDING STALL. `ensureFreshOrCachedAsync` skipped the fetch on a warm watermark — but the
     * repository stores the WATERMARK, not the status, so nothing filled `status` and the screen sat on
     * its centered spinner (`!hasLoadedOnce && status == null`) until the cleaner pulled to refresh.
     * The watermark outlives the view model, so this reproduced on every entry into onboarding inside
     * the 15s window.
     */
    @Test
    fun `a warm watermark does not strand a fresh view model on the spinner`() = runTest {
        val vm = viewModel()
        advanceUntilIdle()

        coVerify(exactly = 1) { profileRepository.getRegistrationStatus() }
        assertNotNull("the screen never got a status", vm.uiState.value.status)
        assertFalse(
            "the centered spinner never cleared",
            !vm.uiState.value.hasLoadedOnce && vm.uiState.value.status == null,
        )
    }

    /** Stands in for the injected `@ApplicationScope`, on the scheduler `advanceUntilIdle` drives. */
    private fun TestScope.syncScope() =
        CoroutineScope(StandardTestDispatcher(testScheduler) + SupervisorJob())

    private fun unapprovedCleaner() = CurrentUser(
        email = "ondrej@example.com",
        firstName = "Ondrej",
        lastName = "Novak",
        phoneNumber = null,
        birthDate = null,
        preferredLanguageCode = "en",
    )
}
