package cz.cleansia.partner.features.orders

import cz.cleansia.core.freshness.Staleness
import cz.cleansia.partner.core.network.ApiErrorTranslator
import cz.cleansia.partner.core.settings.AppSettingsRepository
import cz.cleansia.partner.core.settings.LanguagePreference
import cz.cleansia.partner.data.auth.AuthRepository
import cz.cleansia.partner.data.profile.ProfileRepository
import cz.cleansia.partner.testing.MainDispatcherRule
import io.mockk.coVerify
import io.mockk.every
import io.mockk.mockk
import kotlinx.coroutines.ExperimentalCoroutinesApi
import kotlinx.coroutines.test.advanceUntilIdle
import kotlinx.coroutines.test.runTest
import org.junit.Rule
import org.junit.Test

/**
 * The lock screen replaces the whole app between signup and approval, so it is
 * the only place a cleaner who skipped the intro can change the app's language
 * while they wait. It persists through the same store as the intro and the
 * profile picker — the display preference and the confirmation-email language
 * must not drift onto separate keys.
 */
@OptIn(ExperimentalCoroutinesApi::class)
class RegistrationLockLanguageTest {

    @get:Rule
    val mainRule = MainDispatcherRule()

    private val profileRepository: ProfileRepository = mockk(relaxed = true)
    private val authRepository: AuthRepository = mockk(relaxed = true)
    private val errorTranslator: ApiErrorTranslator = mockk(relaxed = true)
    private val appSettingsRepository: AppSettingsRepository = mockk(relaxed = true)

    private fun viewModel(): RegistrationLockViewModel {
        // Fresh watermark → init's silent-stale path is a true no-op, so the
        // test never has to stand up the status-fetch machinery.
        every { profileRepository.getRegistrationStatusStaleness() } returns
            Staleness().apply { markFresh() }
        return RegistrationLockViewModel(
            profileRepository = profileRepository,
            authRepository = authRepository,
            errorTranslator = errorTranslator,
            appSettingsRepository = appSettingsRepository,
        )
    }

    @Test
    fun `setLanguage persists the choice to the settings store`() = runTest {
        viewModel().setLanguage(LanguagePreference.Ukrainian)
        advanceUntilIdle()

        coVerify(exactly = 1) { appSettingsRepository.setLanguage(LanguagePreference.Ukrainian) }
    }
}
