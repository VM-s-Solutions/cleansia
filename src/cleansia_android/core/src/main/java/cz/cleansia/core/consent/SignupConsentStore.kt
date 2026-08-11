package cz.cleansia.core.consent

import androidx.datastore.core.DataStore
import androidx.datastore.preferences.core.Preferences
import androidx.datastore.preferences.core.edit
import androidx.datastore.preferences.core.stringPreferencesKey
import androidx.datastore.preferences.core.stringSetPreferencesKey
import javax.inject.Inject
import javax.inject.Qualifier
import javax.inject.Singleton
import kotlinx.coroutines.flow.first

/**
 * Qualifies the preferences `DataStore` backing [SignupConsentStore]. Each app provides
 * one under its own file name, the same arrangement
 * [cz.cleansia.core.notifications.PushTokenDataStore] uses.
 */
@Qualifier
@Retention(AnnotationRetention.BINARY)
annotation class SignupConsentDataStore

/**
 * Persists the tick across the process death that can sit between signup and the first
 * sign-in — the user reads the confirmation mail on another device and comes back a day
 * later. Survives sign-out on purpose: the tick belongs to the account, not the session.
 */
@Singleton
class SignupConsentStore @Inject constructor(
    @SignupConsentDataStore private val dataStore: DataStore<Preferences>,
) {

    suspend fun save(pending: PendingSignupConsent) {
        dataStore.edit { prefs ->
            prefs[KEY_EMAIL] = pending.email
            prefs[KEY_TYPES] = pending.types.map(SignupConsentType::name).toSet()
        }
    }

    suspend fun read(): PendingSignupConsent? {
        val prefs = dataStore.data.first()
        val email = prefs[KEY_EMAIL]?.takeIf { it.isNotBlank() } ?: return null
        val names = prefs[KEY_TYPES].orEmpty()
        val types = SignupConsentType.entries.filter { it.name in names }
        return if (types.isEmpty()) null else PendingSignupConsent(email, types)
    }

    suspend fun settle(type: SignupConsentType) {
        dataStore.edit { prefs ->
            val remaining = prefs[KEY_TYPES].orEmpty() - type.name
            if (remaining.isEmpty()) {
                prefs.remove(KEY_EMAIL)
                prefs.remove(KEY_TYPES)
            } else {
                prefs[KEY_TYPES] = remaining
            }
        }
    }

    private companion object {
        val KEY_EMAIL = stringPreferencesKey("pending_email")
        val KEY_TYPES = stringSetPreferencesKey("pending_types")
    }
}
