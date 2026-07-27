package cz.cleansia.partner.features.onboarding

import cz.cleansia.core.settings.SupportedLanguages
import cz.cleansia.partner.core.settings.AppSettingsRepository
import cz.cleansia.partner.core.settings.LanguageLabels
import cz.cleansia.partner.core.settings.LanguagePreference
import cz.cleansia.partner.testing.MainDispatcherRule
import io.mockk.coVerify
import io.mockk.mockk
import kotlinx.coroutines.ExperimentalCoroutinesApi
import kotlinx.coroutines.test.advanceUntilIdle
import kotlinx.coroutines.test.runTest
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNotNull
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Rule
import org.junit.Test

/**
 * The pre-auth intro's language chooser.
 *
 * It lives here, before signup, and not in the RegistrationLock chain, because
 * `RegisterViewModel` reads the language out of DataStore at the moment it calls
 * `register()` — by the time the lock chain renders, `RegisterEmployee` has
 * already stamped `PreferredLanguageCode` and queued the confirmation email, so
 * a choice made there would arrive too late for the only mail that matters.
 */
@OptIn(ExperimentalCoroutinesApi::class)
class OnboardingViewModelTest {

    @get:Rule
    val mainRule = MainDispatcherRule()

    private val appSettingsRepository: AppSettingsRepository = mockk(relaxed = true)

    private fun viewModel() = OnboardingViewModel(appSettingsRepository)

    @Test
    fun `setLanguage persists the choice to the settings store`() = runTest {
        viewModel().setLanguage(LanguagePreference.Czech)
        advanceUntilIdle()

        coVerify(exactly = 1) { appSettingsRepository.setLanguage(LanguagePreference.Czech) }
    }

    @Test
    fun `markSeen still records the intro as done`() = runTest {
        viewModel().markSeen()
        advanceUntilIdle()

        coVerify(exactly = 1) { appSettingsRepository.markOnboardingSeen() }
    }

    /**
     * The chooser offers `LanguagePreference` values, and the tag behind each one
     * has to be something `SupportedLanguages.resolve` will keep. If a row ever
     * carried a tag outside the five, the store would hand it straight to
     * `RegisterEmployee`, whose `LanguageValidator` fails the entire command with
     * `language.not_supported` — a cosmetic picker bug turning into a dead signup.
     */
    @Test
    fun `every offered language resolves to a backend-supported code`() {
        LanguageLabels.ordered.forEach { preference ->
            val resolved = SupportedLanguages.resolve(preference.tag, devicePreferred = emptyList())
            assertTrue(
                "$preference must resolve inside the supported set, got $resolved",
                resolved in SupportedLanguages.SUPPORTED,
            )
        }
    }

    /**
     * Guards the "System" row's shape. It is the only entry without a native
     * name — call sites elvis it into the translated `language_system` string —
     * and the only one whose tag is null, which is what makes the store fall
     * through to the device locale instead of pinning a language.
     */
    @Test
    fun `System is the only row without a native name or a tag`() {
        assertNull(LanguageLabels.nativeName(LanguagePreference.System))
        assertNull(LanguagePreference.System.tag)

        LanguagePreference.entries.filter { it != LanguagePreference.System }.forEach { preference ->
            assertNotNull("$preference must have a native name", LanguageLabels.nativeName(preference))
            assertNotNull("$preference must have a tag", preference.tag)
        }
    }

    /** Every enum value must be offered — a new language must not be droppable. */
    @Test
    fun `the picker order covers every language preference`() {
        assertEquals(LanguagePreference.entries.toSet(), LanguageLabels.ordered.toSet())
        assertEquals(LanguagePreference.System, LanguageLabels.ordered.first())
    }
}
