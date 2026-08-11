package cz.cleansia.core.network

import kotlinx.coroutines.test.runTest
import kotlinx.serialization.Serializable
import kotlinx.serialization.json.Json
import okhttp3.Protocol
import okhttp3.Request
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Test
import retrofit2.Response

/**
 * A 2xx with no body used to answer `Success(Unit as T)` under an unchecked cast, so a caller
 * expecting a body got a `ClassCastException` at the first read — in whatever mapper or composable
 * touched the value, arbitrarily far from the call that produced it.
 *
 * The endpoints that genuinely answer bodiless (`Response<Unit>`) still succeed; everyone else is
 * refused at the boundary, and the refusal names the endpoint.
 */
class SafeApiCallEmptyBodyTest {

    @Serializable
    data class Payload(val value: String)

    private val json = Json { ignoreUnknownKeys = true }

    private fun <T> emptySuccess(code: Int, path: String): Response<T> {
        val raw = okhttp3.Response.Builder()
            .code(code)
            .message("No Content")
            .protocol(Protocol.HTTP_1_1)
            .request(Request.Builder().url("https://mobile.cleansia.cz$path").build())
            .build()
        return Response.success(null, raw)
    }

    @Test
    fun givenUnitCaller_whenBodilessSuccess_thenSucceeds() = runTest {
        val result = safeApiCall(json) { emptySuccess<Unit>(204, "/api/Auth/ForgotPassword") }

        assertEquals(ApiResult.Success(Unit), result)
    }

    @Test
    fun givenBodyCaller_whenBodilessSuccess_thenRefusesInsteadOfCasting() = runTest {
        val result = safeApiCall(json) { emptySuccess<Payload>(204, "/api/Order/GetById") }

        val error = (result as ApiResult.Error).error
        assertTrue("expected Server but was $error", error is ApiError.Server)
        assertEquals(204, (error as ApiError.Server).statusCode)
    }

    @Test
    fun givenBodyCaller_whenBodilessSuccess_thenRefusalNamesTheEndpoint() = runTest {
        val result = safeApiCall(json) { emptySuccess<Payload>(200, "/api/EmployeePayroll/DownloadInvoice") }

        val message = (result as ApiResult.Error).error.getUserMessage()
        assertTrue(
            "expected the endpoint in \"$message\"",
            message.contains("GET") && message.contains("/api/EmployeePayroll/DownloadInvoice"),
        )
    }

    /**
     * The query string is deliberately absent: an order id or an email address in an error message
     * outlives the request in whatever log or crash report reads it.
     */
    @Test
    fun givenBodyCaller_whenBodilessSuccess_thenRefusalOmitsTheQueryString() = runTest {
        val raw = okhttp3.Response.Builder()
            .code(200)
            .message("OK")
            .protocol(Protocol.HTTP_1_1)
            .request(Request.Builder().url("https://mobile.cleansia.cz/api/Order/GetById?OrderId=secret").build())
            .build()

        val result = safeApiCall(json) { Response.success<Payload>(null, raw) }

        val message = (result as ApiResult.Error).error.getUserMessage()
        assertTrue("query string leaked into \"$message\"", !message.contains("secret"))
    }

    @Test
    fun givenBodyCaller_whenBodyPresent_thenStillSucceeds() = runTest {
        val result = safeApiCall(json) { Response.success(Payload("ok")) }

        assertEquals(ApiResult.Success(Payload("ok")), result)
    }
}
