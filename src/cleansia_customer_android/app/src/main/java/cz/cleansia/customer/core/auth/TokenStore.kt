package cz.cleansia.customer.core.auth

import android.content.Context
import android.content.SharedPreferences
import androidx.security.crypto.EncryptedSharedPreferences
import androidx.security.crypto.MasterKey
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow

/**
 * Encrypted persistent store for auth tokens.
 *
 * Uses [EncryptedSharedPreferences] backed by the Android Keystore — values are
 * encrypted at rest with a hardware-backed key when the device supports it,
 * software-backed otherwise. Nothing here is queryable by other apps.
 *
 * We store:
 *  - [Tokens.accessToken]            — short-lived JWT (15 min)
 *  - [Tokens.accessTokenExpiresAt]   — derived from the JWT's `exp` claim, millis UTC
 *  - [Tokens.refreshToken]           — opaque 64-char string; 1d or 30d lifetime server-side
 *  - [Tokens.refreshTokenExpiresAt]  — returned by the server on login/refresh, ISO-8601
 *
 * The class exposes a [StateFlow] so UI can react to session loss without
 * polling. Writes are synchronous (SharedPreferences is fast enough for
 * something this small) but publish via the flow.
 */
class TokenStore(context: Context) {

    private val prefs: SharedPreferences = run {
        val masterKey = MasterKey.Builder(context, MasterKey.DEFAULT_MASTER_KEY_ALIAS)
            .setKeyScheme(MasterKey.KeyScheme.AES256_GCM)
            .build()

        EncryptedSharedPreferences.create(
            context,
            PREF_FILE_NAME,
            masterKey,
            EncryptedSharedPreferences.PrefKeyEncryptionScheme.AES256_SIV,
            EncryptedSharedPreferences.PrefValueEncryptionScheme.AES256_GCM,
        )
    }

    private val _state = MutableStateFlow(readFromPrefs())

    val tokens: StateFlow<Tokens?> = _state.asStateFlow()

    /** Most recent snapshot. Convenience for non-reactive call sites (interceptor). */
    fun current(): Tokens? = _state.value

    fun save(tokens: Tokens) {
        prefs.edit().apply {
            putString(KEY_ACCESS, tokens.accessToken)
            putLong(KEY_ACCESS_EXP, tokens.accessTokenExpiresAt)
            putString(KEY_REFRESH, tokens.refreshToken)
            putLong(KEY_REFRESH_EXP, tokens.refreshTokenExpiresAt)
            apply()
        }
        _state.value = tokens
    }

    fun clear() {
        prefs.edit().clear().apply()
        _state.value = null
    }

    private fun readFromPrefs(): Tokens? {
        val access = prefs.getString(KEY_ACCESS, null) ?: return null
        val accessExp = prefs.getLong(KEY_ACCESS_EXP, 0L)
        val refresh = prefs.getString(KEY_REFRESH, null) ?: return null
        val refreshExp = prefs.getLong(KEY_REFRESH_EXP, 0L)
        return Tokens(access, accessExp, refresh, refreshExp)
    }

    data class Tokens(
        val accessToken: String,
        /** Millis-since-epoch, UTC. Derived from JWT `exp` at save time. */
        val accessTokenExpiresAt: Long,
        val refreshToken: String,
        /** Millis-since-epoch, UTC. Returned by the server. */
        val refreshTokenExpiresAt: Long,
    ) {
        fun isAccessExpired(nowMs: Long = System.currentTimeMillis()): Boolean =
            nowMs >= accessTokenExpiresAt

        fun isRefreshExpired(nowMs: Long = System.currentTimeMillis()): Boolean =
            nowMs >= refreshTokenExpiresAt
    }

    private companion object {
        const val PREF_FILE_NAME = "cleansia_auth_tokens"
        const val KEY_ACCESS = "access_token"
        const val KEY_ACCESS_EXP = "access_token_exp"
        const val KEY_REFRESH = "refresh_token"
        const val KEY_REFRESH_EXP = "refresh_token_exp"
    }
}
