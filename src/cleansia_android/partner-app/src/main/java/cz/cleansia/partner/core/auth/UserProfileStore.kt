package cz.cleansia.partner.core.auth

import android.content.Context
import androidx.datastore.preferences.core.booleanPreferencesKey
import androidx.datastore.preferences.core.edit
import androidx.datastore.preferences.core.stringPreferencesKey
import androidx.datastore.preferences.preferencesDataStore
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.first
import kotlinx.coroutines.flow.map
import javax.inject.Inject
import javax.inject.Singleton

private val Context.userProfileDataStore by preferencesDataStore(name = "partner_user_profile")

/**
 * Non-sensitive profile fields persisted alongside the [:core TokenStore].
 * Tokens themselves stay in the encrypted store; this holds the human-readable
 * identity (userId / email / role flags) the UI needs without unsealing the
 * token on every read.
 */
@Singleton
class UserProfileStore @Inject constructor(
    @dagger.hilt.android.qualifiers.ApplicationContext private val context: Context,
) {

    val profile: Flow<UserProfileData?> = context.userProfileDataStore.data.map { prefs ->
        val userId = prefs[KEY_USER_ID] ?: return@map null
        val email = prefs[KEY_EMAIL] ?: return@map null
        UserProfileData(
            userId = userId,
            email = email,
            employeeId = prefs[KEY_EMPLOYEE_ID],
            isEmailConfirmed = prefs[KEY_EMAIL_CONFIRMED] ?: false,
            hasAdminAccess = prefs[KEY_HAS_ADMIN_ACCESS] ?: false,
            firstName = prefs[KEY_FIRST_NAME],
            lastName = prefs[KEY_LAST_NAME],
            role = prefs[KEY_ROLE],
        )
    }

    suspend fun current(): UserProfileData? = profile.first()

    suspend fun save(profile: UserProfileData) {
        context.userProfileDataStore.edit { prefs ->
            prefs[KEY_USER_ID] = profile.userId
            prefs[KEY_EMAIL] = profile.email
            prefs[KEY_EMAIL_CONFIRMED] = profile.isEmailConfirmed
            prefs[KEY_HAS_ADMIN_ACCESS] = profile.hasAdminAccess
            profile.employeeId?.let { prefs[KEY_EMPLOYEE_ID] = it } ?: prefs.remove(KEY_EMPLOYEE_ID)
            profile.firstName?.let { prefs[KEY_FIRST_NAME] = it } ?: prefs.remove(KEY_FIRST_NAME)
            profile.lastName?.let { prefs[KEY_LAST_NAME] = it } ?: prefs.remove(KEY_LAST_NAME)
            profile.role?.let { prefs[KEY_ROLE] = it } ?: prefs.remove(KEY_ROLE)
        }
    }

    suspend fun updateEmployeeId(employeeId: String?) {
        context.userProfileDataStore.edit { prefs ->
            employeeId?.let { prefs[KEY_EMPLOYEE_ID] = it } ?: prefs.remove(KEY_EMPLOYEE_ID)
        }
    }

    suspend fun updateName(firstName: String?, lastName: String?) {
        context.userProfileDataStore.edit { prefs ->
            firstName?.let { prefs[KEY_FIRST_NAME] = it } ?: prefs.remove(KEY_FIRST_NAME)
            lastName?.let { prefs[KEY_LAST_NAME] = it } ?: prefs.remove(KEY_LAST_NAME)
        }
    }

    suspend fun clear() {
        context.userProfileDataStore.edit { it.clear() }
    }

    private companion object {
        val KEY_USER_ID = stringPreferencesKey("user_id")
        val KEY_EMAIL = stringPreferencesKey("email")
        val KEY_EMPLOYEE_ID = stringPreferencesKey("employee_id")
        val KEY_EMAIL_CONFIRMED = booleanPreferencesKey("email_confirmed")
        val KEY_HAS_ADMIN_ACCESS = booleanPreferencesKey("has_admin_access")
        val KEY_FIRST_NAME = stringPreferencesKey("first_name")
        val KEY_LAST_NAME = stringPreferencesKey("last_name")
        val KEY_ROLE = stringPreferencesKey("role")
    }
}

data class UserProfileData(
    val userId: String,
    val email: String,
    val employeeId: String?,
    val isEmailConfirmed: Boolean,
    val hasAdminAccess: Boolean,
    val firstName: String?,
    val lastName: String?,
    val role: String?,
)
