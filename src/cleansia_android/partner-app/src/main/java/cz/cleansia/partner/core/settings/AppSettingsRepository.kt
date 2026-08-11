package cz.cleansia.partner.core.settings

import android.content.Context
import androidx.datastore.core.DataStore
import androidx.datastore.preferences.core.Preferences
import androidx.datastore.preferences.core.booleanPreferencesKey
import androidx.datastore.preferences.core.edit
import androidx.datastore.preferences.core.stringPreferencesKey
import androidx.core.os.ConfigurationCompat
import cz.cleansia.core.settings.SupportedLanguages
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.first
import kotlinx.coroutines.flow.map

/**
 * Persistent theme + locale preferences. Collected once in [MainActivity] and
 * propagated via CompositionLocal so any composable can react. Default values
 * apply when DataStore is empty (cold install).
 */
class AppSettingsRepository(
    private val dataStore: DataStore<Preferences>,
    private val context: Context,
) {

    private object Keys {
        private const val JOB_RADIUS_PROMPT_ANSWERED = "job_radius_prompt_answered"

        val THEME = stringPreferencesKey("theme")
        val LANGUAGE = stringPreferencesKey("language")
        val ONBOARDING_SEEN = booleanPreferencesKey("onboarding_seen")

        /** Unsuffixed, the name is the device-global answer the first shipped build wrote. */
        val LEGACY_JOB_RADIUS_PROMPT_ANSWERED = booleanPreferencesKey(JOB_RADIUS_PROMPT_ANSWERED)

        fun jobRadiusPromptAnswered(employeeId: String) =
            booleanPreferencesKey("${JOB_RADIUS_PROMPT_ANSWERED}_$employeeId")
    }

    /** The pre-login carousel: nobody is signed in for it, so the handset is the right owner. */
    suspend fun hasSeenOnboarding(): Boolean =
        dataStore.data.map { it[Keys.ONBOARDING_SEEN] ?: false }.first()

    suspend fun markOnboardingSeen() {
        dataStore.edit { it[Keys.ONBOARDING_SEEN] = true }
    }

    /**
     * Whether [employeeId] has answered the one-time "how far do you want to hear about work"
     * prompt. It is stored rather than derived from the radius because a null radius is itself a
     * valid answer — the country-wide board — and would otherwise re-trigger the prompt forever.
     *
     * Keyed per cleaner, like the customer app's post-signin onboarding flag: a cleaning company's
     * phone is shared, and a device-global flag asks whoever signs in first and never asks anyone
     * else.
     */
    suspend fun hasAnsweredJobRadiusPrompt(employeeId: String): Boolean =
        dataStore.data.map { it[Keys.jobRadiusPromptAnswered(employeeId)] ?: false }.first()

    /**
     * Also drops the device-global answer the first shipped build wrote: it names no cleaner, so
     * migrating it would hand one cleaner's answer to whoever opens this build first.
     */
    suspend fun markJobRadiusPromptAnswered(employeeId: String) {
        dataStore.edit {
            it[Keys.jobRadiusPromptAnswered(employeeId)] = true
            it.remove(Keys.LEGACY_JOB_RADIUS_PROMPT_ANSWERED)
        }
    }

    val settings: Flow<AppSettings> = dataStore.data.map { it.toAppSettings() }

    suspend fun setTheme(theme: ThemePreference) {
        dataStore.edit { it[Keys.THEME] = theme.name }
    }

    suspend fun setLanguage(language: LanguagePreference) {
        dataStore.edit { it[Keys.LANGUAGE] = language.name }
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
    /**
     * The tag the cleaner actually picked, or null when they never did.
     *
     * Deliberately not [emailLanguageTag]: that one falls through to the handset's locale, which is a
     * fact about the device rather than a decision by the person holding it. Anything that writes to
     * the server without a tap behind it reads this instead, so a phone set to Czech cannot overwrite
     * a `PreferredLanguageCode` the cleaner chose on another surface.
     */
    suspend fun chosenLanguageTag(): String? = settings.first().language.tag

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
