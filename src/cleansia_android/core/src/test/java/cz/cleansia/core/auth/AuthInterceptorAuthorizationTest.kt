package cz.cleansia.core.auth

import io.mockk.every
import io.mockk.mockk
import okhttp3.OkHttpClient
import okhttp3.Request
import okhttp3.mockwebserver.MockResponse
import okhttp3.mockwebserver.MockWebServer
import okhttp3.mockwebserver.RecordedRequest
import org.junit.After
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Before
import org.junit.Test

/**
 * [AuthInterceptor] is now the Bearer, and only the Bearer.
 *
 * The `ANON_ENDPOINTS` guard is the reason the anonymous client is a separate
 * client at all, so it is pinned here: splitting the device headers out must not
 * quietly widen or narrow which paths get a token. `does not stamp the device
 * headers` pins the other direction — the headers moved to
 * [DeviceHeadersInterceptor], and re-adding them here would double-stamp on the
 * authenticated client while still leaving login/refresh bare.
 */
class AuthInterceptorAuthorizationTest {

    private lateinit var server: MockWebServer
    private lateinit var tokenStore: TokenStore

    private val tokens = TokenStore.Tokens(
        accessToken = "access-1",
        accessTokenExpiresAt = System.currentTimeMillis() + 15 * 60_000L,
        refreshToken = "refresh-1",
        refreshTokenExpiresAt = System.currentTimeMillis() + 24 * 3_600_000L,
    )

    @Before
    fun setUp() {
        server = MockWebServer()
        server.start()
        tokenStore = mockk()
        every { tokenStore.current() } returns tokens
    }

    @After
    fun tearDown() {
        server.shutdown()
    }

    private fun get(path: String): RecordedRequest {
        server.enqueue(MockResponse().setResponseCode(200))
        OkHttpClient.Builder()
            .addInterceptor(AuthInterceptor(tokenStore))
            .build()
            .newCall(Request.Builder().url(server.url(path)).build())
            .execute()
            .close()
        return server.takeRequest()
    }

    @Test
    fun `skips the bearer on anonymous endpoints`() {
        // Case-insensitive: the generated Retrofit clients spell these "/api/Auth/Login".
        assertNull(get("/api/Auth/Login").getHeader("Authorization"))
        assertNull(get("/api/Auth/Register").getHeader("Authorization"))
        assertNull(get("/api/Auth/RefreshToken").getHeader("Authorization"))
        assertNull(get("/api/Auth/GoogleAuth").getHeader("Authorization"))
        assertNull(get("/api/Auth/ConfirmUserEmail").getHeader("Authorization"))
        assertNull(get("/api/Auth/ResendConfirmationEmail").getHeader("Authorization"))
    }

    @Test
    fun `sends the bearer everywhere else, including the authenticated auth endpoints`() {
        assertEquals("Bearer access-1", get("/api/Order/GetPagedOrders").getHeader("Authorization"))
        // Logout and ChangePassword are [Authorize] server-side and deliberately
        // NOT in ANON_ENDPOINTS — this is why the anonymous client must not get
        // an AuthInterceptor bolted onto it.
        assertEquals("Bearer access-1", get("/api/Auth/Logout").getHeader("Authorization"))
        assertEquals("Bearer access-1", get("/api/Auth/ChangePassword").getHeader("Authorization"))
    }

    @Test
    fun `sends no bearer when signed out`() {
        every { tokenStore.current() } returns null

        assertNull(get("/api/Order/GetPagedOrders").getHeader("Authorization"))
    }

    @Test
    fun `does not stamp the device headers`() {
        val request = get("/api/Order/GetPagedOrders")

        assertNull(request.getHeader("X-Device-Id"))
        assertNull(request.getHeader("X-Device-Label"))
    }
}
