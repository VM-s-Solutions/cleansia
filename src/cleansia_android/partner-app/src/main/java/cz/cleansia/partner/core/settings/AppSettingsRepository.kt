package cz.cleansia.partner.core.settings

import android.content.Context
import androidx.datastore.preferences.core.Preferences
import androidx.datastore.preferences.core.booleanPreferencesKey
import androidx.datastore.preferences.core.edit
import androidx.datastore.preferences.core.stringPreferencesKey
import androidx.datastore.preferences.preferencesDataStore
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
    }

    suspend fun hasSeenOnboarding(): Boolean =
        context.dataStore.data.map { it[Keys.ONBOARDING_SEEN] ?: false }.first()

    suspend fun markOnboardingSeen() {
        context.dataStore.edit { it[Keys.ONBOARDING_SEEN] = true }
    }

    val settings: Flow<AppSettings> = context.dataStore.data.map { it.toAppSettings() }

    suspend fun setTheme(theme: ThemePreference) {
        context.dataStore.edit { it[Keys.THEME] = theme.name }
    }

    suspend fun setLanguage(language: LanguagePreference) {
        context.dataStore.edit { it[Keys.LANGUAGE] = language.name }
    }

    private fun Preferences.toAppSettings(): AppSettings {
        val theme = this[Keys.THEME]?.let { runCatching { ThemePreference.valueOf(it) }.getOrNull() }
            ?: ThemePreference.System
        val language = this[Keys.LANGUAGE]?.let { runCatching { LanguagePreference.valueOf(it) }.getOrNull() }
            ?: LanguagePreference.System
        return AppSettings(theme = theme, language = language)
    }
}
