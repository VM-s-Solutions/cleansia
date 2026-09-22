package cz.cleansia.customer.core.orders

import cz.cleansia.core.network.WireContractViolation
import cz.cleansia.customer.core.network.IntEnumSerializersModule
import kotlinx.coroutines.test.runTest
import kotlinx.serialization.json.Json
import kotlinx.serialization.json.jsonObject
import kotlinx.serialization.json.jsonPrimitive
import okhttp3.MediaType.Companion.toMediaType
import okhttp3.mockwebserver.MockResponse
import okhttp3.mockwebserver.MockWebServer
import okhttp3.mockwebserver.RecordedRequest
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Test
import retrofit2.Retrofit
import retrofit2.converter.kotlinx.serialization.asConverterFactory

class GuestOrderWireTest {
    private val json = Json {
        ignoreUnknownKeys = true
        isLenient = true
        explicitNulls = false
        serializersModule = IntEnumSerializersModule
    }

    private suspend fun <T> serving(
        body: String,
        onRequest: (RecordedRequest) -> Unit = {},
        call: suspend (GuestOrderApi) -> T,
    ): T {
        val server = MockWebServer()
        server.start()
        return try {
            server.enqueue(MockResponse().setHeader("Content-Type", "application/json").setBody(body))
            val api = OrderModule.provideGuestOrderApi(
                Retrofit.Builder().baseUrl(server.url("/"))
                    .addConverterFactory(json.asConverterFactory("application/json".toMediaType())).build(),
            )
            call(api).also { onRequest(server.takeRequest()) }
        } finally {
            server.shutdown()
        }
    }

    private fun credential(request: RecordedRequest, path: String) {
        assertEquals("POST", request.method)
        assertEquals(path, request.path)
        assertNull(request.requestUrl!!.query)
        assertNull(request.getHeader("Authorization"))
        val body = json.parseToJsonElement(request.body.readUtf8()).jsonObject
        assertEquals(TOKEN, body.getValue("accessToken").jsonPrimitive.content)
    }

    @Test
    fun `lookup posts the access token in the body and maps the order without carrying it back`() = runTest {
        val order = serving(LOOKUP, { credential(it, "/api/Order/Lookup") }) {
            it.lookup(TOKEN).body()!!
        }
        assertEquals("o-1", order.id)
        assertEquals("EUR", order.currencyCode)
        assertEquals(90.0, order.totalPrice, 0.0)
        assertFalse(order.toString().contains(TOKEN))
    }

    @Test
    fun `preview posts the access token and preserves assessed amounts and tier`() = runTest {
        val quote = serving(PREVIEW, { credential(it, "/api/Order/GuestCancellationPreview") }) {
            it.preview(TOKEN).body()!!
        }
        assertEquals(3, quote.tier)
        assertEquals(22.5, quote.feeAmount, 0.0)
        assertEquals("EUR", quote.currencyCode)
    }

    @Test
    fun `cancellation sends reason and language and retains actual refund separately from policy`() = runTest {
        val receipt = serving(RECEIPT, {
            val body = json.parseToJsonElement(it.body.clone().readUtf8()).jsonObject
            assertEquals("schedule_changed", body.getValue("reason").jsonPrimitive.content)
            assertEquals("sk", body.getValue("language").jsonPrimitive.content)
            credential(it, "/api/Order/CancelGuest")
        }) {
            it.cancel(TOKEN, "schedule_changed", "sk").body()!!
        }
        assertEquals(67.5, receipt.refundAmount, 0.0)
        assertEquals(12.0, receipt.actualRefundAmount!!, 0.0)
    }

    @Test
    fun `nullable actual refund remains unknown instead of borrowing policy amount`() = runTest {
        val receipt = serving(RECEIPT.replace("12.0", "null")) {
            it.cancel(TOKEN, "schedule_changed", "en").body()!!
        }
        assertNull(receipt.actualRefundAmount)
        assertEquals(67.5, receipt.refundAmount, 0.0)
    }

    @Test
    fun `lookup refuses missing price and currency instead of inventing money`() = runTest {
        for (body in listOf(LOOKUP.replace("90.0", "null"), LOOKUP.replace("\"EUR\"", "null"))) {
            var refused = false
            try {
                serving(body) { it.lookup(TOKEN) }
            } catch (_: WireContractViolation) {
                refused = true
            }
            assertTrue(refused)
        }
    }

    companion object {
        private const val TOKEN = "P8Jw-2hQ_xTokenFromTheEmail"
        private const val LOOKUP = """{"id":"o-1","displayOrderNumber":"CZ-123","cleaningDateTime":"2026-09-19T10:00:00Z","totalPrice":90.0,"orderStatus":{"value":2},"currency":{"code":"EUR"},"confirmationCode":"printed-reference"}"""
        private const val PREVIEW = """{"orderId":"o-1","tier":3,"feeRate":0.25,"feeAmount":22.5,"refundAmount":67.5,"totalPrice":90.0,"currencyCode":"EUR","expressWaiverForfeitedOnCancel":false}"""
        private const val RECEIPT = """{"orderId":"o-1","feeRate":0.25,"refundAmount":67.5,"actualRefundAmount":12.0,"totalPrice":90.0,"refundInitiated":true}"""
    }
}
