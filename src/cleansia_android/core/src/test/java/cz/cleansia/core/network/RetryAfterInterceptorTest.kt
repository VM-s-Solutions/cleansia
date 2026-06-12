package cz.cleansia.core.network

import java.io.IOException
import java.util.concurrent.CountDownLatch
import java.util.concurrent.TimeUnit
import okhttp3.Call
import okhttp3.Callback
import okhttp3.OkHttpClient
import okhttp3.Request
import okhttp3.Response
import okhttp3.mockwebserver.MockResponse
import okhttp3.mockwebserver.MockWebServer
import org.junit.After
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNotEquals
import org.junit.Assert.assertTrue
import org.junit.Before
import org.junit.Test

class RetryAfterInterceptorTest {

    private lateinit var server: MockWebServer

    @Before
    fun setUp() {
        server = MockWebServer()
        server.start()
    }

    @After
    fun tearDown() {
        server.shutdown()
    }

    private fun client(jitterMillis: () -> Long = { 0L }): OkHttpClient =
        OkHttpClient.Builder()
            .addInterceptor(RetryAfterInterceptor(jitterMillis))
            .build()

    private fun request(): Request = Request.Builder().url(server.url("/")).build()

    private fun tooManyRequests(retryAfter: String? = null): MockResponse =
        MockResponse().setResponseCode(429).apply {
            if (retryAfter != null) setHeader("Retry-After", retryAfter)
        }

    @Test
    fun `AC5 - 429 then 200 - retries once after the Retry-After delay and returns the 200`() {
        server.enqueue(tooManyRequests(retryAfter = "1"))
        server.enqueue(MockResponse().setResponseCode(200).setBody("ok"))

        val startedAt = System.nanoTime()
        val response = client().newCall(request()).execute()
        val elapsedMillis = TimeUnit.NANOSECONDS.toMillis(System.nanoTime() - startedAt)

        assertEquals(200, response.code)
        assertEquals(2, server.requestCount)
        assertTrue("expected >=1s back-off, was ${elapsedMillis}ms", elapsedMillis >= 1_000)
        response.close()
    }

    @Test
    fun `AC5 AC8 - 429 then 429 - second 429 returned unchanged, max one retry`() {
        server.enqueue(tooManyRequests(retryAfter = "0"))
        server.enqueue(tooManyRequests(retryAfter = "0"))
        server.enqueue(MockResponse().setResponseCode(200))

        val response = client().newCall(request()).execute()

        assertEquals(429, response.code)
        assertEquals(2, server.requestCount)
        response.close()
    }

    @Test
    fun `non-429 responses pass through untouched with a single request`() {
        server.enqueue(MockResponse().setResponseCode(500))

        var jitterAsked = false
        val response = client(jitterMillis = { jitterAsked = true; 0L })
            .newCall(request())
            .execute()

        assertEquals(500, response.code)
        assertEquals(1, server.requestCount)
        assertEquals(false, jitterAsked)
        response.close()
    }

    @Test
    fun `successful responses pass through untouched`() {
        server.enqueue(MockResponse().setResponseCode(200).setBody("ok"))

        val response = client().newCall(request()).execute()

        assertEquals(200, response.code)
        assertEquals(1, server.requestCount)
        response.close()
    }

    @Test
    fun `AC6 - cancelling the call during the back-off aborts the wait and issues no retry`() {
        server.enqueue(tooManyRequests(retryAfter = "10"))
        server.enqueue(MockResponse().setResponseCode(200))

        val failed = CountDownLatch(1)
        val call = client().newCall(request())
        call.enqueue(object : Callback {
            override fun onFailure(call: Call, e: IOException) = failed.countDown()
            override fun onResponse(call: Call, response: Response) = response.close()
        })

        Thread.sleep(400)
        call.cancel()

        assertTrue("cancel must abort the back-off wait", failed.await(3, TimeUnit.SECONDS))
        assertEquals(1, server.requestCount)
    }

    @Test
    fun `AC5 - Retry-After parsed as delta seconds`() {
        assertEquals(7_000L, RetryAfterInterceptor { 0L }.backoffMillis("7"))
    }

    @Test
    fun `AC5 - absent Retry-After falls back to the 60s default window`() {
        assertEquals(60_000L, RetryAfterInterceptor { 0L }.backoffMillis(null))
    }

    @Test
    fun `AC5 - malformed or negative Retry-After falls back to the default window`() {
        assertEquals(60_000L, RetryAfterInterceptor { 0L }.backoffMillis("soon"))
        assertEquals(60_000L, RetryAfterInterceptor { 0L }.backoffMillis("-3"))
        assertEquals(60_000L, RetryAfterInterceptor { 0L }.backoffMillis(""))
    }

    @Test
    fun `AC8 - Retry-After exceeding the default window is honored, not capped`() {
        assertEquals(120_000L, RetryAfterInterceptor { 0L }.backoffMillis("120"))
    }

    @Test
    fun `AC8 - jitter is added on top of the base delay and desyncs equal headers`() {
        val first = RetryAfterInterceptor { 2_000L }.backoffMillis("30")
        val second = RetryAfterInterceptor { 9_000L }.backoffMillis("30")
        assertEquals(32_000L, first)
        assertEquals(39_000L, second)
        assertNotEquals(first, second)
    }

    @Test
    fun `AC8 - default jitter stays inside the 0-15s range`() {
        repeat(50) {
            val delay = RetryAfterInterceptor().backoffMillis("0")
            assertTrue("jitter out of range: $delay", delay in 0 until 15_000L)
        }
    }
}
