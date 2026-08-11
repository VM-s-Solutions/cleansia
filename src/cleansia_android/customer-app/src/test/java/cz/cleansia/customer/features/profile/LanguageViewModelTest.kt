package cz.cleansia.customer.features.profile

import cz.cleansia.customer.core.settings.AppSettings
import cz.cleansia.customer.core.settings.AppSettingsRepository
import cz.cleansia.customer.core.settings.LanguagePreference
import cz.cleansia.customer.core.settings.LanguagePreferenceSync
import cz.cleansia.customer.testing.MainDispatcherRule
import io.mockk.coEvery
import io.mockk.coVerify
import io.mockk.coVerifyOrder
import io.mockk.every
import io.mockk.mockk
import kotlinx.coroutines.ExperimentalCoroutinesApi
import kotlinx.coroutines.flow.flowOf
import kotlinx.coroutines.test.advanceUntilIdle
import kotlinx.coroutines.test.runTest
import org.junit.Assert.assertEquals
import org.junit.Before
import org.junit.Rule
import org.junit.Test

@OptIn(ExperimentalCoroutinesApi::class)
class LanguageViewModelTest {

    @get:Rule
    val mainRule = MainDispatcherRule()

    private lateinit var settings: AppSettingsRepository
    private lateinit var languageSync: LanguagePreferenceSync
    private val sent = mutableListOf<String>()

    @Before
    fun setUp() {
        settings = mockk(relaxed = true)
        languageSync = mockk(relaxed = true)
        every { settings.settings } returns flowOf(AppSettings())
        coEvery { settings.emailLanguageTag() } returns "en"
        coEvery { languageSync.send(capture(sent)) } returns Unit
    }

    private fun viewModel() = LanguageViewModel(settings, languageSync)

    @Test
    fun `choosing a language persists it and pushes it to the server`() = runTest {
        coEvery { settings.emailLanguageTag() } returns "uk"

        viewModel().setLanguage(LanguagePreference.Ukrainian)
        advanceUntilIdle()

        coVerifyOrder {
            settings.setLanguage(LanguagePreference.Ukrainian)
            languageSync.send("uk")
        }
    }

    /**
     * "System" persists as a null tag, and the server cannot see this device's locale — it needs one of
     * the five supported codes to pick an email template. What reaches the API is therefore always the
     * resolved tag, never the sentinel.
     */
    @Test
    fun `following the system pushes the resolved tag not the sentinel`() = runTest {
        coEvery { settings.emailLanguageTag() } returns "cs"

        viewModel().setLanguage(LanguagePreference.System)
        advanceUntilIdle()

        assertEquals(listOf("cs"), sent)
        assertEquals(null, LanguagePreference.System.tag)
    }

    @Test
    fun `every choice pushes its own resolved tag`() = runTest {
        val vm = viewModel()

        coEvery { settings.emailLanguageTag() } returns "cs"
        vm.setLanguage(LanguagePreference.Czech)
        advanceUntilIdle()
        coEvery { settings.emailLanguageTag() } returns "ru"
        vm.setLanguage(LanguagePreference.Russian)
        advanceUntilIdle()

        assertEquals(listOf("cs", "ru"), sent)
        coVerify(exactly = 1) { settings.setLanguage(LanguagePreference.Czech) }
        coVerify(exactly = 1) { settings.setLanguage(LanguagePreference.Russian) }
    }
}
