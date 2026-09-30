package cz.cleansia.customer.core.payments

import cz.cleansia.core.network.ApiError
import cz.cleansia.core.network.ApiResult
import cz.cleansia.customer.core.network.IntEnumSerializersModule
import kotlinx.coroutines.test.runTest
import kotlinx.datetime.Instant
import kotlinx.serialization.ExperimentalSerializationApi
import kotlinx.serialization.descriptors.SerialDescriptor
import kotlinx.serialization.json.Json
import kotlinx.serialization.json.JsonArray
import kotlinx.serialization.json.JsonObject
import kotlinx.serialization.json.jsonArray
import kotlinx.serialization.json.jsonObject
import okhttp3.MediaType.Companion.toMediaType
import okhttp3.mockwebserver.MockResponse
import okhttp3.mockwebserver.MockWebServer
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Test
import retrofit2.Retrofit
import retrofit2.converter.kotlinx.serialization.asConverterFactory
import cz.cleansia.customer.api.client.ReceivableApi as GenReceivableApi
import cz.cleansia.customer.api.model.CreateReceivablePayLinkResponse as GenCreateReceivablePayLinkResponse
import cz.cleansia.customer.api.model.MyReceivableDto as GenMyReceivableDto

/** Every field of `MyReceivableDto` is non-nullable in C#, the amount above all: a debt is never a zero. */
class ReceivableWireTest {

    private val json = Json {
        ignoreUnknownKeys = true
        isLenient = true
        explicitNulls = false
        serializersModule = IntEnumSerializersModule
    }

    private suspend fun <T> withServer(body: String, block: suspend (ReceivableRepository, MockWebServer) -> T): T {
        val server = MockWebServer()
        server.start()
        return try {
            server.enqueue(MockResponse().setHeader("Content-Type", "application/json").setBody(body))
            val repo = ReceivableRepository(
                ReceivableApi(
                    Retrofit.Builder()
                        .baseUrl(server.url("/"))
                        .addConverterFactory(json.asConverterFactory("application/json".toMediaType()))
                        .build()
                        .create(GenReceivableApi::class.java),
                ),
                json,
            )
            block(repo, server)
        } finally {
            server.shutdown()
        }
    }

    private fun assertRefusesNaming(field: String, result: ApiResult<*>) {
        assertTrue("a missing $field must refuse; got $result", result is ApiResult.Error)
        val error = (result as ApiResult.Error).error
        assertTrue("a broken 2xx body is the server's fault; got $error", error is ApiError.Server)
        assertTrue(
            "the refusal must name $field, but carried \"${(error as ApiError.Server).diagnostic}\"",
            error.diagnostic!!.startsWith("$field "),
        )
    }

    @Test
    fun receivableDtoSerialNamesAreExactlyTheSpecProperties() {
        assertEquals(RECEIVABLE_PROPERTIES, serialNames(GenMyReceivableDto.serializer().descriptor))
    }

    @Test
    fun payLinkDtoSerialNamesAreExactlyTheSpecProperties() {
        assertEquals(setOf("receivableId", "checkoutUrl"), serialNames(GenCreateReceivablePayLinkResponse.serializer().descriptor))
    }

    @Test
    fun theDebtsAreReadFromTheRouteTheServerBinds() = runTest {
        val request = withServer(CAPTURED_RECEIVABLES) { repo, server ->
            repo.getMine()
            server.takeRequest()
        }

        assertEquals("GET", request.method)
        assertEquals("/api/Receivable/GetMine", request.path)
    }

    @Test
    fun thePayLinkIsAskedForByTheReceivableId() = runTest {
        val request = withServer(CAPTURED_PAY_LINK) { repo, server ->
            repo.createPayLink("rcv-1")
            server.takeRequest()
        }

        assertEquals("POST", request.method)
        assertEquals("/api/Receivable/CreatePayLink/rcv-1", request.path)
    }

    @Test
    fun everyReceivableFieldArrivesWithItsLiteralValue() = runTest {
        val result = withServer(CAPTURED_RECEIVABLES) { repo, _ -> repo.getMine() }

        assertEquals(
            listOf(
                Receivable(
                    id = "rcv-1",
                    orderId = "ord-1",
                    displayOrderNumber = "CL-1042",
                    kind = 2,
                    amount = 1250.5,
                    currencyCode = "CZK",
                    createdOn = Instant.parse("2026-09-28T10:15:00Z"),
                ),
            ),
            (result as ApiResult.Success).data,
        )
    }

    /** A debt dropped from the list reads as "nothing to pay" while it keeps refusing cash bookings. */
    @Test
    fun aReceivableMissingAFieldRefusesThePageByName() = runTest {
        RECEIVABLE_PROPERTIES.forEach { field ->
            assertRefusesNaming(field, withServer(withoutReceivableKey(field)) { repo, _ -> repo.getMine() })
        }
    }

    @Test
    fun theKindIsItsOrdinalAndAKindWithoutOneRefuses() = runTest {
        val noOrdinal = CAPTURED_RECEIVABLES.replace(""""value": 2""", """"value": null""")

        assertRefusesNaming("kind.value", withServer(noOrdinal) { repo, _ -> repo.getMine() })
    }

    @Test
    fun thePayLinkIsTheCheckoutUrlAndOneWithoutItRefuses() = runTest {
        val link = withServer(CAPTURED_PAY_LINK) { repo, _ -> repo.createPayLink("rcv-1") }
        assertEquals("https://checkout.stripe.com/c/pay/cs_test_1", (link as ApiResult.Success).data)

        assertRefusesNaming(
            "checkoutUrl",
            withServer("""{ "receivableId": "rcv-1" }""") { repo, _ -> repo.createPayLink("rcv-1") },
        )
    }

    private fun withoutReceivableKey(key: String): String =
        JsonArray(
            Json.parseToJsonElement(CAPTURED_RECEIVABLES).jsonArray.map {
                JsonObject(it.jsonObject.toMutableMap().apply { remove(key) })
            },
        ).toString()

    @OptIn(ExperimentalSerializationApi::class)
    private fun serialNames(descriptor: SerialDescriptor): Set<String> =
        (0 until descriptor.elementsCount).map { descriptor.getElementName(it) }.toSet()

    private companion object {
        val CAPTURED_RECEIVABLES = """
            [
              {
                "id": "rcv-1",
                "orderId": "ord-1",
                "displayOrderNumber": "CL-1042",
                "kind": { "type": "ReceivableKind", "name": "Lockout", "value": 2 },
                "amount": 1250.5,
                "currencyCode": "CZK",
                "createdOn": "2026-09-28T10:15:00Z"
              }
            ]
        """.trimIndent()

        const val CAPTURED_PAY_LINK = """{ "receivableId": "rcv-1", "checkoutUrl": "https://checkout.stripe.com/c/pay/cs_test_1" }"""

        val RECEIVABLE_PROPERTIES = setOf("id", "orderId", "displayOrderNumber", "kind", "amount", "currencyCode", "createdOn")
    }
}
