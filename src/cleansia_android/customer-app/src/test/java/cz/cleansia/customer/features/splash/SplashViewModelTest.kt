package cz.cleansia.customer.features.splash

import cz.cleansia.core.auth.TokenStore
import io.mockk.every
import io.mockk.mockk
import org.junit.Assert.assertEquals
import org.junit.Test

class SplashViewModelTest {

    private val tokenStore: TokenStore = mockk()

    private fun tokens(refreshExpiresAt: Long) = TokenStore.Tokens(
        accessToken = "access",
        accessTokenExpiresAt = 0L,
        refreshToken = "refresh",
        refreshTokenExpiresAt = refreshExpiresAt,
    )

    @Test
    fun `no stored session is not a valid one`() {
        every { tokenStore.current() } returns null
        assertEquals(false, SplashViewModel(tokenStore).hasValidSession())
    }

    @Test
    fun `an expired refresh token is not a valid session`() {
        every { tokenStore.current() } returns tokens(refreshExpiresAt = System.currentTimeMillis() - 1_000L)
        assertEquals(false, SplashViewModel(tokenStore).hasValidSession())
    }

    @Test
    fun `a live refresh token is a valid session even with the access token expired`() {
        every { tokenStore.current() } returns tokens(refreshExpiresAt = System.currentTimeMillis() + 60_000L)
        assertEquals(true, SplashViewModel(tokenStore).hasValidSession())
    }
}
