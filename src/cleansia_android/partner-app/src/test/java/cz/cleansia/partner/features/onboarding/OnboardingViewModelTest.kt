package cz.cleansia.partner.features.onboarding

import cz.cleansia.core.auth.TokenStore
import cz.cleansia.core.settings.SupportedLanguages
import cz.cleansia.partner.core.settings.AppSettingsRepository
import cz.cleansia.partner.core.settings.LanguageLabels
import cz.cleansia.partner.core.settings.LanguagePreference
import cz.cleansia.partner.core.settings.LiveLanguagePreferenceSync
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
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNotNull
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Before
import org.junit.Rule
import org.junit.Test

/**
 * The pre-auth intro's language chooser.
 *
 * This is the only one of the two chooser surfaces that still reaches the
 * confirmation email: `RegisterViewModel` reads the language out of DataStore
 * at the moment it calls `register()`, so by the time the RegistrationLock
 * chain renders its copy, `RegisterEmployee` has already stamped
 * `PreferredLanguageCode` and queued the mail. The lock screen's chooser is a
 * display preference only.
 *
 * Nobody is signed in for this carousel, so there is no row to update and nothing to push. The seam
 * has to reach that conclusion without a request: an unauthenticated call would 401, and the shared
 * authenticator's first act on a 401 is to reach for a refresh token this handset does not have.
 */
@OptIn(ExperimentalCoroutinesApi::class)
class OnboardingViewModelTest {

    @get:Rule
    val mainRule = MainDispatcherRule()

    private val appSettingsRepository: AppSettingsRepository = mockk(relaxed = true)
    private val userRepository: UserRepository = mockk(relaxed = true)
    private val tokenStore: TokenStore = mockk(relaxed = true)

    @Before
    fun setUp() {
        coEvery { appSettingsRepository.emailLanguageTag() } returns "cs"
        every { tokenStore.current() } returns null
    }

    private fun TestScope.viewModel() = OnboardingViewModel(
        appSettingsRepository = appSettingsRepository,
        languageSync = LiveLanguagePreferenceSync(tokenStore, userRepository, syncScope()),
    )

    /** Stands in for the injected `@ApplicationScope`, on the scheduler `advanceUntilIdle` drives. */
    private fun TestScope.syncScope() =
        CoroutineScope(StandardTestDispatcher(testScheduler) + SupervisorJob())

    @Test
    fun `setLanguage persists the choice to the settings store`() = runTest {
        viewModel().setLanguage(LanguagePreference.Czech)
        advanceUntilIdle()

        coVerify(exactly = 1) { appSettingsRepository.setLanguage(LanguagePreference.Czech) }
    }

    @Test
    fun `the pre-login chooser pushes nothing and does not throw`() = runTest {
        viewModel().setLanguage(LanguagePreference.Czech)
        advanceUntilIdle()

        // Called again directly: the detached push is what has to reach "no session" without a
        // request, and the ViewModel's own launch would hide anything thrown inside it.
        LiveLanguagePreferenceSync(tokenStore, userRepository, syncScope()).send("cs")
        advanceUntilIdle()

        coVerify(exactly = 0) { userRepository.getCurrentUser() }
        coVerify(exactly = 0) { userRepository.updateCurrentUser(any(), any(), any(), any(), any()) }
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
