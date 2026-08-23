package cz.cleansia.customer.core.settings

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

private val Context.dataStore by preferencesDataStore(name = "app_settings")

/**
 * Reads and writes [AppSettings] from DataStore. Exposed as a [Flow] so
 * Compose can collect in the activity and propagate via CompositionLocal.
 */
class AppSettingsRepository(private val context: Context) {
    private object Keys {
        val THEME = stringPreferencesKey("theme")
        val LANGUAGE = stringPreferencesKey("language")
    }

    val settings: Flow<AppSettings> = context.dataStore.data.map { prefs -> prefs.toAppSettings() }

    suspend fun setTheme(theme: ThemePreference) {
        context.dataStore.edit { it[Keys.THEME] = theme.name }
    }

    suspend fun setLanguage(language: LanguagePreference) {
        context.dataStore.edit { it[Keys.LANGUAGE] = language.name }
    }

    /**
     * The language code to send with confirmation and reset emails.
     *
     * System is the default and carries a null tag, so a naive `?: "en"` sent a Czech phone English mail
     * on a fresh install. **Resolution goes through the shared helper** — explicit choice, then the
     * device's ordered locale list, then English.
     */
    suspend fun emailLanguageTag(): String = SupportedLanguages.resolve(
        persistedTag = settings.first().language.tag,
        devicePreferred = ConfigurationCompat.getLocales(context.resources.configuration)
            .let { locales -> (0 until locales.size()).mapNotNull { locales[it]?.toLanguageTag() } },
    )

    /**
     * "We already asked for a review" flag, keyed on BOTH the user and the order: per order so a
     * second completed clean still prompts, and per user so a second account on a shared device does
     * not inherit an answer about an order it cannot even see. iOS keys it the same way. Set when the prompt is shown, not when it is answered —
     * declining is an answer, and asking twice about the same clean is the behaviour customers hate
     * about this pattern everywhere else.
     *
     * Fresh install = fresh prompt, same accepted trade-off as onboarding above. The server's own
     * `hasReview` always wins over this flag, so a review left on another device suppresses it too.
     */
    private fun reviewPromptKey(userId: String, orderId: String) =
        booleanPreferencesKey("review_prompted_${userId}_$orderId")

    suspend fun hasPromptedForReview(userId: String, orderId: String): Boolean =
        context.dataStore.data.map { it[reviewPromptKey(userId, orderId)] ?: false }.first()

    suspend fun markReviewPrompted(userId: String, orderId: String) {
        context.dataStore.edit { it[reviewPromptKey(userId, orderId)] = true }
    }

    /**
     * Per-user "saw the post-signin onboarding" flag. Keyed on user id so a different
     * user signing in on the same device still gets prompted once. We don't persist
     * this server-side — fresh install = fresh onboarding, which is acceptable.
     */
    private fun onboardingKey(userId: String) = booleanPreferencesKey("onboarding_seen_$userId")

    suspend fun hasSeenOnboarding(userId: String): Boolean =
        context.dataStore.data.map { it[onboardingKey(userId)] ?: false }.first()

    suspend fun markOnboardingSeen(userId: String) {
        context.dataStore.edit { it[onboardingKey(userId)] = true }
    }

    private fun Preferences.toAppSettings(): AppSettings {
        val theme = this[Keys.THEME]?.let { runCatching { ThemePreference.valueOf(it) }.getOrNull() }
            ?: ThemePreference.System
        val language = this[Keys.LANGUAGE]?.let { runCatching { LanguagePreference.valueOf(it) }.getOrNull() }
            ?: LanguagePreference.System
        return AppSettings(theme = theme, language = language)
    }
}
