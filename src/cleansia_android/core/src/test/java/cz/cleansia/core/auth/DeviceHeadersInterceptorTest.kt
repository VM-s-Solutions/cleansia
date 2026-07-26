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
import org.junit.Assert.assertTrue
import org.junit.Before
import org.junit.Test

/**
 * The device headers must ride on EVERY request, including the anonymous ones.
 *
 * `RefreshTokenService` stamps `RefreshToken.DeviceId` from `X-Device-Id` at
 * *issue* time - i.e. on Login/Register/GoogleAuth, which all go out on the
 * no-auth client. While these headers lived inside [AuthInterceptor] (installed
 * on the authenticated client only) the id was null on every token a phone ever
 * got, and "Your devices -> revoke" - which matches on `DeviceId` - could never
 * match. Hence a standalone interceptor that both clients install.
 *
 * `never sends authorization` is the pin on the other half of that split: this
 * interceptor is what goes onto the anonymous client, so it must never grow a
 * token-attaching branch.
 */
class DeviceHeadersInterceptorTest {

    private lateinit var server: MockWebServer
    private lateinit var deviceIdProvider: DeviceIdProvider

    @Before
    fun setUp() {
        server = MockWebServer()
        server.start()
        deviceIdProvider = mockk()
        every { deviceIdProvider.deviceId } returns DEVICE_ID
    }

    @After
    fun tearDown() {
        server.shutdown()
    }

    private fun get(path: String): RecordedRequest {
        server.enqueue(MockResponse().setResponseCode(200))
        OkHttpClient.Builder()
            .addInterceptor(DeviceHeadersInterceptor(deviceIdProvider))
            .build()
            .newCall(Request.Builder().url(server.url(path)).build())
            .execute()
            .close()
        return server.takeRequest()
    }

    @Test
    fun `sends device id and label on every request`() {
        // The anonymous token-issuing endpoints are the whole point: this is
        // where the server first learns which device the refresh token belongs to.
        val login = get("/api/Auth/Login")
        assertEquals(DEVICE_ID, login.getHeader("X-Device-Id"))
        assertTrue(
            "expected a non-blank X-Device-Label, was ${login.getHeader("X-Device-Label")}",
            login.getHeader("X-Device-Label").orEmpty().isNotBlank(),
        )

        val refresh = get("/api/Auth/RefreshToken")
        assertEquals(DEVICE_ID, refresh.getHeader("X-Device-Id"))

        val business = get("/api/Order/GetPagedOrders")
        assertEquals(DEVICE_ID, business.getHeader("X-Device-Id"))
    }

    @Test
    fun `never sends authorization`() {
        // This interceptor is installed on the NO-AUTH client too. If it ever
        // learned to attach a token, the refresh call would start carrying the
        // very access token it is trying to replace.
        assertNull(get("/api/Auth/Login").getHeader("Authorization"))
        assertNull(get("/api/Order/GetPagedOrders").getHeader("Authorization"))
    }

    @Test
    fun `filters non-ascii and truncates the fallback device id`() {
        // ANDROID_ID is hex so this is a no-op on the normal path; it guards the
        // "MANUFACTURER-MODEL" fallback, where OkHttp throws on a non-ASCII
        // header value and 64 chars is the server-side column width.
        every { deviceIdProvider.deviceId } returns "Xiaomi-Poco–X6" + "z".repeat(80)

        val sent = get("/api/Auth/Login").getHeader("X-Device-Id")

        assertEquals(64, sent?.length)
        assertEquals("Xiaomi-PocoX6" + "z".repeat(51), sent)
    }

    private companion object {
        const val DEVICE_ID = "9f1c4a7b2e0d3856"
    }
}
