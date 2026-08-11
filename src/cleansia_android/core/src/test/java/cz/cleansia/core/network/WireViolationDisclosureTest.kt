package cz.cleansia.core.network

import kotlinx.coroutines.test.runTest
import kotlinx.serialization.json.Json
import okhttp3.MediaType.Companion.toMediaType
import okhttp3.Protocol
import okhttp3.Request
import okhttp3.ResponseBody.Companion.toResponseBody
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNotNull
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Test
import retrofit2.Response

/**
 * A diagnostic is carried for triage and never rendered, and the two must not be the same field.
 *
 * T-0588 / T-0589 minted dozens of `WireContractViolation` messages and routed them into
 * `ApiError.Server.message`, which `getUserMessage()` returns verbatim — so
 * *"totalPrice is null but the mobile API contract declares it non-nullable"* reached a customer's
 * snackbar. That is worse than the coercion the refusal replaced: it cannot be acted on, it reads as
 * a crash, and it puts an internal field name on screen.
 *
 * Both directions are pinned deliberately. Only the first would leave "make every error vaguer" as a
 * passing implementation, and the backend's `BusinessErrorMessage` keys are the customer's real
 * errors — *"This job is no longer available"* must keep saying that.
 */
class WireViolationDisclosureTest {

    private val json = Json { ignoreUnknownKeys = true }

    private fun violation() = WireContractViolation("totalPrice")

    private fun serverErrorFrom(result: ApiResult<*>): ApiError.Server {
        assertTrue("expected an Error, got $result", result is ApiResult.Error)
        val error = (result as ApiResult.Error).error
        assertTrue("a broken 2xx body is a server fault, got $error", error is ApiError.Server)
        return error as ApiError.Server
    }

    private fun assertRendersNothingInternal(error: ApiError) {
        val shown = error.getUserMessage()
        LEAKS.forEach { leak ->
            assertTrue(
                "the rendered string must not contain \"$leak\", but was \"$shown\"",
                !shown.contains(leak, ignoreCase = true),
            )
        }
    }

    // --- direction 1: the violation is carried, and not shown ----------------------

    @Test
    fun `mapWire carries the field name in the diagnostic and never in the rendered message`() {
        val error = serverErrorFrom(
            ApiResult.Success(Unit).mapWire<Unit, Unit> { throw violation() },
        )

        assertRendersNothingInternal(error)
        assertNotNull("the field name must survive for triage", error.diagnostic)
        assertTrue(
            "the diagnostic must still name the field, but was \"${error.diagnostic}\"",
            error.diagnostic!!.startsWith("totalPrice "),
        )
    }

    @Test
    fun `wireResult carries the field name the same way`() {
        val error = serverErrorFrom(wireResult<Unit> { throw violation() })

        assertRendersNothingInternal(error)
        assertTrue(error.diagnostic!!.startsWith("totalPrice "))
    }

    @Test
    fun `safeApiCall carries the field name the same way`() = runTest {
        val error = serverErrorFrom(
            safeApiCall<Unit>(json) { throw violation() },
        )

        assertRendersNothingInternal(error)
        assertTrue(error.diagnostic!!.startsWith("totalPrice "))
    }

    @Test
    fun `the violation still attributes to the server and not the connection`() {
        val error = serverErrorFrom(wireResult<Unit> { throw violation() })

        assertEquals(200, error.statusCode)
    }

    // --- direction 2: a real error is NOT flattened --------------------------------

    /**
     * The half that stops this reading as "make errors vaguer". A keyed business error is not a wire
     * violation: the key is the customer's actual error and the app localizers resolve it.
     */
    @Test
    fun `a keyed business error keeps its key and its own copy`() {
        val error = ApiError.BadRequest(
            message = "This job is no longer available.",
            errorKey = "order.not_takeable",
        )

        assertEquals("order.not_takeable", error.errorKey)
        assertEquals("This job is no longer available.", error.getUserMessage())
    }

    @Test
    fun `an ordinary server error still renders the line the server sent`() {
        val error = ApiError.Server(statusCode = 500, message = "Payment provider unavailable.")

        assertEquals("Payment provider unavailable.", error.getUserMessage())
        assertNull("only a wire violation carries a diagnostic", error.diagnostic)
    }

    @Test
    fun `a 4xx decoded from the wire keeps the server's own message and key`() = runTest {
        val body = """{"detail":"This job is no longer available.","errors":{"Order":"order.not_takeable"}}"""
        val result = safeApiCall<Unit>(json) {
            Response.error(
                body.toResponseBody("application/json".toMediaType()),
                okhttp3.Response.Builder()
                    .code(400)
                    .message("Bad Request")
                    .protocol(Protocol.HTTP_1_1)
                    .request(Request.Builder().url("https://example.test/api/Order/Take").build())
                    .build(),
            )
        }

        val error = (result as ApiResult.Error).error
        assertTrue("expected BadRequest, got $error", error is ApiError.BadRequest)
        assertEquals("order.not_takeable", (error as ApiError.BadRequest).errorKey)
        assertEquals("This job is no longer available.", error.getUserMessage())
    }

    private companion object {
        /**
         * The field name and the contract wording that identifies a violation. Every one of these
         * appeared in a customer-facing snackbar before this split.
         */
        val LEAKS = listOf("totalPrice", "contract", "nullable", "null but")
    }
}
