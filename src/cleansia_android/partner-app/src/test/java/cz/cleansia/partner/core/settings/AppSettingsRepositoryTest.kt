package cz.cleansia.partner.core.settings

import android.content.Context
import androidx.datastore.core.DataStore
import androidx.datastore.preferences.core.PreferenceDataStoreFactory
import androidx.datastore.preferences.core.Preferences
import androidx.datastore.preferences.core.booleanPreferencesKey
import androidx.datastore.preferences.core.edit
import io.mockk.mockk
import kotlinx.coroutines.flow.first
import kotlinx.coroutines.test.runTest
import org.junit.Assert.assertFalse
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Before
import org.junit.Rule
import org.junit.Test
import org.junit.rules.TemporaryFolder
import org.junit.rules.TestName

/**
 * The job-radius ask is a property of the cleaner; the theme, the language and the pre-login
 * carousel are properties of the handset. A cleaning company's phone is shared, so a device-global
 * ask is spent by whoever signs in first and nobody after them is ever asked — and a question that
 * stops being asked reports nothing, exactly like a notification that stops arriving.
 */
class AppSettingsRepositoryTest {

    @get:Rule
    val tempFolder = TemporaryFolder()

    @get:Rule
    val testName = TestName()

    private lateinit var dataStore: DataStore<Preferences>

    /** The literal key the first shipped build wrote, spelled out rather than derived. */
    private val deviceGlobalKey = booleanPreferencesKey("job_radius_prompt_answered")

    @Before
    fun setUp() {
        dataStore = PreferenceDataStoreFactory.create(
            produceFile = { tempFolder.newFile("app_settings_${testName.methodName}.preferences_pb") },
        )
    }

    private fun repository() = AppSettingsRepository(dataStore, mockk<Context>(relaxed = true))

    @Test
    fun `answering for one cleaner leaves the next cleaner on the device unasked`() = runTest {
        val repository = repository()

        repository.markJobRadiusPromptAnswered("emp-1")

        assertTrue(repository.hasAnsweredJobRadiusPrompt("emp-1"))
        assertFalse(repository.hasAnsweredJobRadiusPrompt("emp-2"))
    }

    @Test
    fun `the answer is stored under the cleaner's own key`() = runTest {
        repository().markJobRadiusPromptAnswered("emp-1")

        assertTrue(
            dataStore.data.first()[booleanPreferencesKey("job_radius_prompt_answered_emp-1")] == true,
        )
    }

    /**
     * The device-global answer the shipped build wrote is dropped, not migrated: it names no
     * cleaner, so honouring it would hand one cleaner's answer to whoever opens this build first.
     */
    @Test
    fun `a device carrying only the shipped device-global answer still asks every cleaner`() = runTest {
        dataStore.edit { it[deviceGlobalKey] = true }
        val repository = repository()

        assertFalse(repository.hasAnsweredJobRadiusPrompt("emp-1"))
        assertFalse(repository.hasAnsweredJobRadiusPrompt("emp-2"))
    }

    @Test
    fun `answering drops the device-global key instead of leaving it to be read back`() = runTest {
        dataStore.edit { it[deviceGlobalKey] = true }

        repository().markJobRadiusPromptAnswered("emp-1")

        assertNull(dataStore.data.first()[deviceGlobalKey])
    }

    /** The pre-login carousel has no cleaner to key on, so it stays a property of the handset. */
    @Test
    fun `the pre-login onboarding flag stays device-global`() = runTest {
        val repository = repository()

        repository.markOnboardingSeen()

        assertTrue(repository.hasSeenOnboarding())
    }

    @Test
    fun `theme and language stay device-global`() = runTest {
        val repository = repository()

        repository.setTheme(ThemePreference.Dark)
        repository.setLanguage(LanguagePreference.Czech)

        val settings = repository.settings.first()
        assertTrue(settings.theme == ThemePreference.Dark)
        assertTrue(settings.language == LanguagePreference.Czech)
    }
}
