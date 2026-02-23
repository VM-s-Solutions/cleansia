package cz.cleansia.partner.core.storage

import android.content.Context
import androidx.security.crypto.EncryptedSharedPreferences
import androidx.security.crypto.MasterKey
import dagger.hilt.android.qualifiers.ApplicationContext
import kotlinx.coroutines.channels.BufferOverflow
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.MutableSharedFlow
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.SharedFlow
import kotlinx.coroutines.flow.asSharedFlow
import kotlinx.coroutines.flow.asStateFlow
import java.util.concurrent.atomic.AtomicBoolean
import javax.inject.Inject
import javax.inject.Singleton

/**
 * Manages secure storage of authentication tokens using EncryptedSharedPreferences.
 */
@Singleton
class TokenManager @Inject constructor(
    @ApplicationContext private val context: Context
) {
    private companion object {
        const val PREFS_NAME = "cleansia_secure_prefs"
        const val KEY_AUTH_TOKEN = "auth_token"
        const val KEY_USER_ID = "user_id"
        const val KEY_USER_EMAIL = "user_email"
        const val KEY_IS_EMAIL_CONFIRMED = "is_email_confirmed"
        const val KEY_USER_FIRST_NAME = "user_first_name"
        const val KEY_USER_LAST_NAME = "user_last_name"
    }

    private val masterKey = MasterKey.Builder(context)
        .setKeyScheme(MasterKey.KeyScheme.AES256_GCM)
        .build()

    private val encryptedPrefs = EncryptedSharedPreferences.create(
        context,
        PREFS_NAME,
        masterKey,
        EncryptedSharedPreferences.PrefKeyEncryptionScheme.AES256_SIV,
        EncryptedSharedPreferences.PrefValueEncryptionScheme.AES256_GCM
    )

    private val _isLoggedIn = MutableStateFlow(hasToken())

    /**
     * Flow that emits the current login state
     */
    val isLoggedIn: Flow<Boolean> = _isLoggedIn.asStateFlow()

    private val _userFullName = MutableStateFlow(readUserFullName())

    /**
     * Flow that emits the current user's full name (updates reactively when name changes)
     */
    val userFullName: Flow<String> = _userFullName.asStateFlow()

    private val _userInitials = MutableStateFlow(readUserInitials())

    /**
     * Flow that emits the current user's initials (updates reactively when name changes)
     */
    val userInitials: Flow<String> = _userInitials.asStateFlow()

    private val _sessionExpiredEvent = MutableSharedFlow<Unit>(
        replay = 0,
        extraBufferCapacity = 1,
        onBufferOverflow = BufferOverflow.DROP_OLDEST
    )

    /**
     * One-shot event emitted when session expires (401 detected).
     * Collect this to show a session expired dialog.
     */
    val sessionExpiredEvent: SharedFlow<Unit> = _sessionExpiredEvent.asSharedFlow()

    private val sessionExpiredHandled = AtomicBoolean(false)

    /**
     * Save authentication data after successful login
     */
    fun saveAuthData(
        token: String,
        userId: String,
        email: String,
        isEmailConfirmed: Boolean
    ) {
        encryptedPrefs.edit().apply {
            putString(KEY_AUTH_TOKEN, token)
            putString(KEY_USER_ID, userId)
            putString(KEY_USER_EMAIL, email)
            putBoolean(KEY_IS_EMAIL_CONFIRMED, isEmailConfirmed)
            apply()
        }
        _isLoggedIn.value = true
        sessionExpiredHandled.set(false)
    }

    /**
     * Update the auth token (e.g., after refresh)
     */
    fun updateToken(token: String) {
        encryptedPrefs.edit().putString(KEY_AUTH_TOKEN, token).apply()
    }

    /**
     * Update email confirmation status
     */
    fun updateEmailConfirmed(isConfirmed: Boolean) {
        encryptedPrefs.edit().putBoolean(KEY_IS_EMAIL_CONFIRMED, isConfirmed).apply()
    }

    /**
     * Get the stored authentication token
     */
    fun getToken(): String? = encryptedPrefs.getString(KEY_AUTH_TOKEN, null)

    /**
     * Get the stored user ID
     */
    fun getUserId(): String? = encryptedPrefs.getString(KEY_USER_ID, null)?.takeIf { it.isNotBlank() }

    /**
     * Get the stored user email
     */
    fun getUserEmail(): String? = encryptedPrefs.getString(KEY_USER_EMAIL, null)

    /**
     * Save user name for display purposes (e.g., avatar initials)
     */
    fun saveUserName(firstName: String?, lastName: String?) {
        encryptedPrefs.edit().apply {
            putString(KEY_USER_FIRST_NAME, firstName)
            putString(KEY_USER_LAST_NAME, lastName)
            apply()
        }
        _userFullName.value = readUserFullName()
        _userInitials.value = readUserInitials()
    }

    /**
     * Get user initials for avatar display
     */
    fun getUserInitials(): String = readUserInitials()

    private fun readUserInitials(): String {
        val first = encryptedPrefs.getString(KEY_USER_FIRST_NAME, null)?.firstOrNull()?.uppercase() ?: ""
        val last = encryptedPrefs.getString(KEY_USER_LAST_NAME, null)?.firstOrNull()?.uppercase() ?: ""
        return if (first.isEmpty() && last.isEmpty()) "?" else "$first$last"
    }

    /**
     * Get user full name
     */
    fun getUserFullName(): String = readUserFullName()

    private fun readUserFullName(): String {
        val first = encryptedPrefs.getString(KEY_USER_FIRST_NAME, null) ?: ""
        val last = encryptedPrefs.getString(KEY_USER_LAST_NAME, null) ?: ""
        return "$first $last".trim().ifEmpty { encryptedPrefs.getString(KEY_USER_EMAIL, null) ?: "" }
    }

    /**
     * Check if the user's email is confirmed
     */
    fun isEmailConfirmed(): Boolean = encryptedPrefs.getBoolean(KEY_IS_EMAIL_CONFIRMED, false)

    /**
     * Check if a token is stored
     */
    fun hasToken(): Boolean = getToken() != null

    /**
     * Clear all stored authentication data (logout)
     */
    fun clearAuthData() {
        encryptedPrefs.edit().clear().apply()
        _isLoggedIn.value = false
        _userFullName.value = ""
        _userInitials.value = "?"
    }

    /**
     * Handle session expiration (401 from API).
     * Clears auth data and emits a one-shot event for UI notification.
     * Thread-safe: uses AtomicBoolean to prevent duplicate handling from parallel 401 responses.
     */
    fun onSessionExpired() {
        if (sessionExpiredHandled.compareAndSet(false, true)) {
            clearAuthData()
            _sessionExpiredEvent.tryEmit(Unit)
        }
    }
}
