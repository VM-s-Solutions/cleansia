package cz.cleansia.core.network

import cz.cleansia.core.R
import cz.cleansia.core.snackbar.SnackbarMessage
import cz.cleansia.core.snackbar.toErrorSnackbar
import java.io.IOException
import java.net.SocketTimeoutException
import java.net.UnknownHostException
import kotlinx.coroutines.test.runTest
import kotlinx.serialization.json.Json
import okhttp3.MediaType.Companion.toMediaType
import okhttp3.Protocol
import okhttp3.Request
import okhttp3.ResponseBody.Companion.toResponseBody
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Test
import retrofit2.Response

/**
 * `safeApiCall` builds its errors in `:core`, which has no `Context`, so every fallback it writes is
 * English — *"Connection timeout. Please try again."* to a customer reading Czech. Same shape as the
 * wire-violation leak one layer over, and closed the same way: [ApiError.messageRes] marks the
 * strings `:core` built, and the render resolves them.
 *
 * Two guards, and the second is why this is not simply "make errors generic":
 *  - a timeout, a 404 and a 5xx are three sentences, not one bucket; and
 *  - an error whose text the server sent passes through untouched, on every arm.
 */
class ApiErrorLocalizationTest {

    private val json = Json { ignoreUnknownKeys = true }

    private fun resOf(error: ApiError): Int? {
        val message = error.toErrorSnackbar()
        return (message as? SnackbarMessage.FromRes)?.stringRes
    }

    private suspend fun failingWith(t: Throwable): ApiError =
        (safeApiCall<Unit>(json) { throw t } as ApiResult.Error).error

    private suspend fun http(code: Int, body: String): ApiError {
        val result = safeApiCall<Unit>(json) {
            Response.error(
                body.toResponseBody("application/json".toMediaType()),
                okhttp3.Response.Builder()
                    .code(code)
                    .message("error")
                    .protocol(Protocol.HTTP_1_1)
                    .request(Request.Builder().url("https://example.test/api/Order/Take").build())
                    .build(),
            )
        }
        return (result as ApiResult.Error).error
    }

    // --- guard 1: each arm gets its own sentence ----------------------------------

    @Test
    fun `a timeout, a lost connection, a 404 and a 5xx are four different sentences`() = runTest {
        val resources = listOf(
            resOf(failingWith(SocketTimeoutException())),
            resOf(failingWith(UnknownHostException("no dns"))),
            resOf(http(404, "")),
            resOf(http(503, "")),
        )

        assertTrue("every arm must resolve a resource, got $resources", resources.none { it == null })
        assertEquals("the arms must not collapse into one bucket", resources.size, resources.toSet().size)
    }

    @Test
    fun `each core-built arm resolves the resource that names it`() = runTest {
        assertEquals(R.string.core_error_timeout, resOf(failingWith(SocketTimeoutException())))
        assertEquals(R.string.core_error_no_connection, resOf(failingWith(UnknownHostException("no dns"))))
        assertEquals(R.string.core_error_not_found, resOf(http(404, "")))
        assertEquals(R.string.core_error_server, resOf(http(503, "")))
        assertEquals(R.string.core_error_unknown, resOf(failingWith(IllegalStateException("boom"))))
    }

    /**
     * A reset socket and an unresolvable host are one fact to a customer — "we could not reach the
     * server" — so they share a line deliberately. `e.message` is host/port detail nobody can act on
     * and stays behind as triage.
     */
    @Test
    fun `a generic IO failure folds into the connection line and never renders its own text`() = runTest {
        val error = failingWith(IOException("failed to connect to 10.0.2.2 port 8080"))

        assertEquals(R.string.core_error_no_connection, resOf(error))
        assertTrue("the host must stay out of the render", error.messageRes != null)
        assertTrue("triage keeps the detail", error.message!!.contains("10.0.2.2"))
    }

    @Test
    fun `an unexpected exception never renders its own text either`() = runTest {
        val error = failingWith(IllegalStateException("Lateinit property viewModel not initialized"))

        assertEquals(R.string.core_error_unknown, resOf(error))
        assertTrue(error.message!!.contains("Lateinit"))
    }

    // --- guard 2: the server's own copy is never replaced --------------------------

    @Test
    fun `a message the server sent passes through on every arm`() = runTest {
        val sent = """{"detail":"This job is no longer available."}"""

        listOf(400, 404, 503).forEach { code ->
            val error = http(code, sent)
            assertNull(
                "a $code the server explained must not be overwritten, got ${error.messageRes}",
                error.messageRes,
            )
            assertEquals("This job is no longer available.", error.getUserMessage())
            assertTrue(error.toErrorSnackbar() is SnackbarMessage.FromString)
        }
    }

    @Test
    fun `a keyed 400 keeps its key alongside the server's sentence`() = runTest {
        val error = http(
            400,
            """{"detail":"This job is no longer available.","errors":{"Order":"order.not_takeable"}}""",
        )

        assertEquals("order.not_takeable", (error as ApiError.BadRequest).errorKey)
        assertNull(error.messageRes)
    }

    /**
     * The one deliberate fold: a 400 whose body did not parse has nothing specific to say, so
     * "Bad request" — developer-speak dressed as copy — becomes the generic line instead. The keyed
     * 400 above never reaches this floor.
     */
    @Test
    fun `an unparseable 400 folds into the generic line rather than saying Bad request`() = runTest {
        val error = http(400, "")

        assertEquals(R.string.core_error_unknown, resOf(error))
    }

    @Test
    fun `a session rejection resolves the session line rather than a raw key`() {
        val error = ApiError.AuthRejected(errorKey = "auth.social_account_not_found")

        assertEquals(R.string.core_error_unauthorized, resOf(error))
        assertEquals("auth.social_account_not_found", error.errorKey)
    }
}
