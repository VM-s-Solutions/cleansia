package cz.cleansia.core.auth

import cz.cleansia.core.snackbar.SnackbarController
import io.mockk.mockk
import io.mockk.verify
import java.io.IOException
import java.util.concurrent.TimeUnit
import okhttp3.OkHttpClient
import okhttp3.Request
import okhttp3.mockwebserver.MockResponse
import okhttp3.mockwebserver.MockWebServer
import okhttp3.mockwebserver.SocketPolicy
import org.junit.After
import org.junit.Assert.assertThrows
import org.junit.Before
import org.junit.Test

/**
 * A timed-out request is a real failure: callers leave connection failures to this toast, so a
 * suppressed timeout left a save or a sign-in doing nothing visible on a slow server.
 */
class NetworkErrorInterceptorTest {

    private lateinit var server: MockWebServer
    private val snackbar = mockk<SnackbarController>(relaxed = true)

    @Before
    fun setUp() {
        server = MockWebServer()
        server.start()
    }

    @After
    fun tearDown() {
        server.shutdown()
    }

    private fun call() = OkHttpClient.Builder()
        .addInterceptor(NetworkErrorInterceptor(snackbar, NETWORK, SERVER))
        .readTimeout(200, TimeUnit.MILLISECONDS)
        .build()
        .newCall(Request.Builder().url(server.url("/api/User/UpdateCurrentUser")).build())

    @Test
    fun `a read timeout raises the network toast`() {
        server.enqueue(MockResponse().setSocketPolicy(SocketPolicy.NO_RESPONSE))

        assertThrows(IOException::class.java) { call().execute() }

        verify(exactly = 1) { snackbar.showErrorKey(NETWORK) }
    }

    @Test
    fun `a cancelled call raises no toast`() {
        server.enqueue(MockResponse().setSocketPolicy(SocketPolicy.NO_RESPONSE))
        val call = call()
        call.cancel()

        assertThrows(IOException::class.java) { call.execute() }

        verify(exactly = 0) { snackbar.showErrorKey(any()) }
    }

    @Test
    fun `a server error raises the server toast`() {
        server.enqueue(MockResponse().setResponseCode(503))

        call().execute().close()

        verify(exactly = 1) { snackbar.showErrorKey(SERVER) }
    }

    private companion object {
        const val NETWORK = 1
        const val SERVER = 2
    }
}
