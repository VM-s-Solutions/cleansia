package cz.cleansia.core.auth

import android.util.Base64
import org.json.JSONObject

/**
 * Lightweight JWT payload reader. We don't verify signatures client-side — the
 * server already did at issue time, and we trust our own token.
 */
object JwtDecoder {
    /** @return JWT `exp` claim as milliseconds-since-epoch, or null if unparseable. */
    fun extractExpiryMillis(jwt: String): Long? =
        payload(jwt)?.optLong("exp", -1L)?.takeIf { it > 0 }?.let { it * 1000L }

    /**
     * @return the user's email from the JWT. ASP.NET issues both the long
     * SOAP-style claim and a short "email" claim depending on config; check both.
     */
    fun extractEmail(jwt: String): String? {
        val json = payload(jwt) ?: return null
        return sequenceOf(
            "email",
            "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress",
        )
            .mapNotNull { key -> json.optString(key, "").takeIf { it.isNotBlank() } }
            .firstOrNull()
    }

    /**
     * @return the user's stable ID from the JWT (ASP.NET's `sub` or the SOAP nameidentifier
     * claim). Used for Sentry user context so crash reports can be correlated across
     * sessions without leaking PII.
     */
    fun extractUserId(jwt: String): String? {
        val json = payload(jwt) ?: return null
        return sequenceOf(
            "sub",
            "nameid",
            "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier",
        )
            .mapNotNull { key -> json.optString(key, "").takeIf { it.isNotBlank() } }
            .firstOrNull()
    }

    private fun payload(jwt: String): JSONObject? {
        val parts = jwt.split(".")
        if (parts.size != 3) return null
        return runCatching {
            val bytes = Base64.decode(parts[1], Base64.URL_SAFE or Base64.NO_PADDING)
            JSONObject(String(bytes))
        }.getOrNull()
    }
}
