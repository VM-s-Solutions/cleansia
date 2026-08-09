package cz.cleansia.partner.core.settings

import android.content.Context
import androidx.datastore.preferences.core.Preferences
import androidx.datastore.preferences.core.booleanPreferencesKey
import androidx.datastore.preferences.core.edit
import androidx.datastore.preferences.core.stringPreferencesKey
import androidx.datastore.preferences.preferencesDataStore
import androidx.core.os.ConfigurationCompat
import cz.cleansia.core.settings.SupportedLanguages
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.first
import kotlinx.coroutines.flow.map

private val Context.dataStore by preferencesDataStore(name = "partner_app_settings")

/**
 * Persistent theme + locale preferences. Collected once in [MainActivity] and
 * propagated via CompositionLocal so any composable can react. Default values
 * apply when DataStore is empty (cold install).
 */
class AppSettingsRepository(private val context: Context) {

    private object Keys {
        val THEME = stringPreferencesKey("theme")
        val LANGUAGE = stringPreferencesKey("language")
        val ONBOARDING_SEEN = booleanPreferencesKey("onboarding_seen")
        val JOB_RADIUS_PROMPT_ANSWERED = booleanPreferencesKey("job_radius_prompt_answered")
    }

    suspend fun hasSeenOnboarding(): Boolean =
        context.dataStore.data.map { it[Keys.ONBOARDING_SEEN] ?: false }.first()

    suspend fun markOnboardingSeen() {
        context.dataStore.edit { it[Keys.ONBOARDING_SEEN] = true }
    }

    /**
     * Whether the one-time "how far do you want to hear about work" prompt has been answered. It
     * lives here rather than being derived from the stored radius because a null radius is itself a
     * valid answer — the country-wide board — and would otherwise re-trigger the prompt forever.
     */
    suspend fun hasAnsweredJobRadiusPrompt(): Boolean =
        context.dataStore.data.map { it[Keys.JOB_RADIUS_PROMPT_ANSWERED] ?: false }.first()

    suspend fun markJobRadiusPromptAnswered() {
        context.dataStore.edit { it[Keys.JOB_RADIUS_PROMPT_ANSWERED] = true }
    }

    val settings: Flow<AppSettings> = context.dataStore.data.map { it.toAppSettings() }

    suspend fun setTheme(theme: ThemePreference) {
        context.dataStore.edit { it[Keys.THEME] = theme.name }
    }

    suspend fun setLanguage(language: LanguagePreference) {
        context.dataStore.edit { it[Keys.LANGUAGE] = language.name }
    }

    /**
     * The language code to send with confirmation / password-reset emails.
     *
     * [LanguagePreference.System] is the default and carries a null tag, so every
     * caller used to `?: "en"` and a Czech phone got English mail on a fresh
     * install. Resolution now runs through [SupportedLanguages], which prefers an
     * explicit picker choice, then the device's ordered locale list, then English —
     * and clamps the answer to the five codes the backend's `LanguageValidator`
     * will accept, so an unsupported handset locale can never fail a registration.
     *
     * We read `context.resources.configuration` rather than
     * `AppCompatDelegate.getApplicationLocales()`: when the preference is System
     * there is no app override to read, and when there IS one the configuration
     * already reflects it. Reading both would be two sources of truth that
     * disagree right after a locale change.
     */
    suspend fun emailLanguageTag(): String = SupportedLanguages.resolve(
        persistedTag = settings.first().language.tag,
        devicePreferred = ConfigurationCompat.getLocales(context.resources.configuration)
            .let { locales -> (0 until locales.size()).mapNotNull { locales[it]?.toLanguageTag() } },
    )

    private fun Preferences.toAppSettings(): AppSettings {
        val theme = this[Keys.THEME]?.let { runCatching { ThemePreference.valueOf(it) }.getOrNull() }
            ?: ThemePreference.System
        val language = this[Keys.LANGUAGE]?.let { runCatching { LanguagePreference.valueOf(it) }.getOrNull() }
            ?: LanguagePreference.System
        return AppSettings(theme = theme, language = language)
    }
}
