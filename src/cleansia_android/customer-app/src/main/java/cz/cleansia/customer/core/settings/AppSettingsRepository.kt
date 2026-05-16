package cz.cleansia.customer.core.settings

import android.content.Context
import androidx.datastore.preferences.core.Preferences
import androidx.datastore.preferences.core.booleanPreferencesKey
import androidx.datastore.preferences.core.edit
import androidx.datastore.preferences.core.stringPreferencesKey
import androidx.datastore.preferences.preferencesDataStore
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
