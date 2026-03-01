package cz.cleansia.partner.core.storage

import android.content.Context
import androidx.datastore.core.DataStore
import androidx.datastore.preferences.core.Preferences
import androidx.datastore.preferences.core.booleanPreferencesKey
import androidx.datastore.preferences.core.edit
import androidx.datastore.preferences.core.stringPreferencesKey
import androidx.datastore.preferences.preferencesDataStore
import dagger.hilt.android.qualifiers.ApplicationContext
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.map
import java.util.Locale
import javax.inject.Inject
import javax.inject.Singleton

private val Context.dataStore: DataStore<Preferences> by preferencesDataStore(name = "cleansia_preferences")

/**
 * Manages non-sensitive user preferences using DataStore.
 */
@Singleton
class PreferencesManager @Inject constructor(
    @ApplicationContext private val context: Context
) {
    private object PreferencesKeys {
        val REMEMBER_ME = booleanPreferencesKey("remember_me")
        val SAVED_EMAIL = stringPreferencesKey("saved_email")
        val LANGUAGE = stringPreferencesKey("language")
        val THEME = stringPreferencesKey("theme")
        val ONBOARDING_COMPLETED = booleanPreferencesKey("onboarding_completed")
        val NOTIFICATION_ENABLED = booleanPreferencesKey("notification_enabled")
        val BIOMETRIC_ENABLED = booleanPreferencesKey("biometric_enabled")
        val PROFILE_COMPLETED = booleanPreferencesKey("profile_completed")
        val ORDERS_HELP_DISMISSED = booleanPreferencesKey("orders_help_dismissed")
        val INVOICES_HELP_DISMISSED = booleanPreferencesKey("invoices_help_dismissed")
    }

    /**
     * Remember me preference flow
     */
    val rememberMe: Flow<Boolean> = context.dataStore.data.map { preferences ->
        preferences[PreferencesKeys.REMEMBER_ME] ?: false
    }

    /**
     * Saved email flow (for remember me feature)
     */
    val savedEmail: Flow<String?> = context.dataStore.data.map { preferences ->
        preferences[PreferencesKeys.SAVED_EMAIL]
    }

    private val systemDefaultLanguage: String
        get() {
            val lang = Locale.getDefault().language
            return when (lang) {
                "cs" -> "cs"
                "pl" -> "pl"
                else -> "en"
            }
        }

    /**
     * Language preference flow
     */
    val language: Flow<String> = context.dataStore.data.map { preferences ->
        preferences[PreferencesKeys.LANGUAGE] ?: systemDefaultLanguage
    }

    /**
     * Theme preference flow
     */
    val theme: Flow<String> = context.dataStore.data.map { preferences ->
        preferences[PreferencesKeys.THEME] ?: "system"
    }

    /**
     * Onboarding completed flow
     */
    val onboardingCompleted: Flow<Boolean> = context.dataStore.data.map { preferences ->
        preferences[PreferencesKeys.ONBOARDING_COMPLETED] ?: false
    }

    /**
     * Notification enabled flow
     */
    val notificationEnabled: Flow<Boolean> = context.dataStore.data.map { preferences ->
        preferences[PreferencesKeys.NOTIFICATION_ENABLED] ?: true
    }

    /**
     * Biometric enabled flow
     */
    val biometricEnabled: Flow<Boolean> = context.dataStore.data.map { preferences ->
        preferences[PreferencesKeys.BIOMETRIC_ENABLED] ?: false
    }

    /**
     * Set remember me preference
     */
    suspend fun setRememberMe(remember: Boolean, email: String? = null) {
        context.dataStore.edit { preferences ->
            preferences[PreferencesKeys.REMEMBER_ME] = remember
            if (remember && email != null) {
                preferences[PreferencesKeys.SAVED_EMAIL] = email
            } else if (!remember) {
                preferences.remove(PreferencesKeys.SAVED_EMAIL)
            }
        }
    }

    /**
     * Set language preference
     */
    suspend fun setLanguage(language: String) {
        context.dataStore.edit { preferences ->
            preferences[PreferencesKeys.LANGUAGE] = language
        }
    }

    /**
     * Set theme preference
     */
    suspend fun setTheme(theme: String) {
        context.dataStore.edit { preferences ->
            preferences[PreferencesKeys.THEME] = theme
        }
    }

    /**
     * Set onboarding completed
     */
    suspend fun setOnboardingCompleted(completed: Boolean) {
        context.dataStore.edit { preferences ->
            preferences[PreferencesKeys.ONBOARDING_COMPLETED] = completed
        }
    }

    /**
     * Set notification enabled
     */
    suspend fun setNotificationEnabled(enabled: Boolean) {
        context.dataStore.edit { preferences ->
            preferences[PreferencesKeys.NOTIFICATION_ENABLED] = enabled
        }
    }

    /**
     * Set biometric enabled
     */
    suspend fun setBiometricEnabled(enabled: Boolean) {
        context.dataStore.edit { preferences ->
            preferences[PreferencesKeys.BIOMETRIC_ENABLED] = enabled
        }
    }

    /**
     * Profile completed flow
     */
    val profileCompleted: Flow<Boolean> = context.dataStore.data.map { preferences ->
        preferences[PreferencesKeys.PROFILE_COMPLETED] ?: false
    }

    /**
     * Set profile completed
     */
    suspend fun setProfileCompleted(completed: Boolean) {
        context.dataStore.edit { preferences ->
            preferences[PreferencesKeys.PROFILE_COMPLETED] = completed
        }
    }

    /**
     * Orders help card dismissed state
     */
    val ordersHelpDismissed: Flow<Boolean> = context.dataStore.data.map { preferences ->
        preferences[PreferencesKeys.ORDERS_HELP_DISMISSED] ?: false
    }

    /**
     * Invoices help card dismissed state
     */
    val invoicesHelpDismissed: Flow<Boolean> = context.dataStore.data.map { preferences ->
        preferences[PreferencesKeys.INVOICES_HELP_DISMISSED] ?: false
    }

    /**
     * Set orders help dismissed
     */
    suspend fun setOrdersHelpDismissed(dismissed: Boolean) {
        context.dataStore.edit { preferences ->
            preferences[PreferencesKeys.ORDERS_HELP_DISMISSED] = dismissed
        }
    }

    /**
     * Set invoices help dismissed
     */
    suspend fun setInvoicesHelpDismissed(dismissed: Boolean) {
        context.dataStore.edit { preferences ->
            preferences[PreferencesKeys.INVOICES_HELP_DISMISSED] = dismissed
        }
    }

    /**
     * Clear all preferences
     */
    suspend fun clearAll() {
        context.dataStore.edit { preferences ->
            preferences.clear()
        }
    }
}
