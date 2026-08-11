package cz.cleansia.core.auth

import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Test

/**
 * The marker a login presents to prove this handset already held a session. The server hashes it,
 * looks the refresh token up and requires it to be alive and bound to the account being signed into
 * — so the only thing that can be presented is the refresh token as stored, and a blank one has to
 * read as "no previous session" rather than as a value that matches nothing.
 */
class TrustedDeviceMarkerTest {

    private fun stored(refreshToken: String) = TokenStore.Tokens(
        accessToken = "access-value",
        accessTokenExpiresAt = 1L,
        refreshToken = refreshToken,
        refreshTokenExpiresAt = 2L,
    )

    @Test
    fun marker_isTheStoredRefreshTokenAndNotTheAccessToken() {
        assertEquals("refresh-value", stored("refresh-value").trustedDeviceToken)
    }

    @Test
    fun marker_isAbsentWhenTheStoredRefreshTokenIsEmpty() {
        assertNull(stored("").trustedDeviceToken)
    }

    @Test
    fun marker_isAbsentWhenTheStoredRefreshTokenIsBlank() {
        assertNull(stored("   ").trustedDeviceToken)
    }
}
