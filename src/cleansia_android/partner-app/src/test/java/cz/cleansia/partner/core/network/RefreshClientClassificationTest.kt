package cz.cleansia.partner.core.network

import cz.cleansia.core.auth.RefreshResult
import cz.cleansia.partner.api.client.AuthApi
import cz.cleansia.partner.api.model.JwtTokenResponse
import io.mockk.coEvery
import io.mockk.mockk
import kotlinx.coroutines.test.runTest
import okhttp3.MediaType.Companion.toMediaType
import okhttp3.ResponseBody.Companion.toResponseBody
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Test
import retrofit2.Response

/**
 * The partner RefreshClient impl maps refresh outcomes through the shared
 * cross-platform classification: auth rejection is terminal, transport/5xx/429
 * is retryable.
 */
class RefreshClientClassificationTest {

    private val api = mockk<AuthApi>()
    private val client = NetworkModule.provideRefreshClient(api)

    private fun errorBody(payload: String = "") =
        payload.toResponseBody("application/json".toMediaType())

    @Test
    fun refresh_given401_returnsRejected() = runTest {
        coEvery { api.authRefreshToken(any()) } returns Response.error(401, errorBody())
        assertEquals(RefreshResult.Rejected, client.refresh("r"))
    }

    @Test
    fun refresh_given429_returnsUnavailable() = runTest {
        coEvery { api.authRefreshToken(any()) } returns Response.error(429, errorBody())
        assertEquals(RefreshResult.Unavailable, client.refresh("r"))
    }

    @Test
    fun refresh_given500_returnsUnavailable() = runTest {
        coEvery { api.authRefreshToken(any()) } returns Response.error(500, errorBody())
        assertEquals(RefreshResult.Unavailable, client.refresh("r"))
    }

    @Test
    fun refresh_whenTransportThrows_returnsUnavailable() = runTest {
        coEvery { api.authRefreshToken(any()) } throws java.io.IOException("boom")
        assertEquals(RefreshResult.Unavailable, client.refresh("r"))
    }

    @Test
    fun refresh_givenBusinessRejectionBody_returnsRejected() = runTest {
        coEvery { api.authRefreshToken(any()) } returns Response.error(
            400,
            errorBody("""{"code":"Token","message":"auth.invalid_refresh_token"}"""),
        )
        assertEquals(RefreshResult.Rejected, client.refresh("r"))
    }

    @Test
    fun refresh_givenSuccess_returnsSuccessTokens() = runTest {
        coEvery { api.authRefreshToken(any()) } returns Response.success(
            JwtTokenResponse(
                token = "h.p.s",
                refreshToken = "new-r",
                refreshTokenExpiresAt = "2099-01-01T00:00:00Z",
            ),
        )

        val result = client.refresh("r")

        assertTrue("expected Success but was $result", result is RefreshResult.Success)
        assertEquals("new-r", (result as RefreshResult.Success).tokens.refreshToken)
    }

    @Test
    fun refresh_givenSuccessWithBlankAccessToken_returnsUnavailable() = runTest {
        coEvery { api.authRefreshToken(any()) } returns Response.success(JwtTokenResponse(token = ""))
        assertEquals(RefreshResult.Unavailable, client.refresh("r"))
    }
}
