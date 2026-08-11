package cz.cleansia.customer.core.payments

import cz.cleansia.core.network.ApiError
import cz.cleansia.core.network.ApiResult
import cz.cleansia.customer.core.network.IntEnumSerializersModule
import kotlinx.coroutines.test.runTest
import kotlinx.serialization.ExperimentalSerializationApi
import kotlinx.serialization.descriptors.SerialDescriptor
import kotlinx.serialization.json.Json
import kotlinx.serialization.json.JsonObject
import kotlinx.serialization.json.jsonObject
import okhttp3.MediaType.Companion.toMediaType
import okhttp3.mockwebserver.MockResponse
import okhttp3.mockwebserver.MockWebServer
import okhttp3.mockwebserver.RecordedRequest
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Test
import retrofit2.Retrofit
import retrofit2.converter.kotlinx.serialization.asConverterFactory
import cz.cleansia.customer.api.client.PaymentApi as GenPaymentApi
import cz.cleansia.customer.api.model.CreatePaymentIntentResponse as GenCreatePaymentIntentResponse

/**
 * The four strings on this wire are the credentials the Stripe PaymentSheet is opened with, all four
 * non-nullable on `CreatePaymentIntent.Response`. The schema declares no `required` array, so the
 * generator types them optional-with-null regardless — and the spec's `nullable: true` says nothing
 * either, because it says that about every string on this wire.
 */
class PaymentWireTest {

    private val json = Json {
        ignoreUnknownKeys = true
        isLenient = true
        explicitNulls = false
        serializersModule = IntEnumSerializersModule
    }

    private suspend fun intent(
        body: String,
        code: Int = 200,
        onRequest: (RecordedRequest) -> Unit = {},
    ): ApiResult<CreatePaymentIntentResponse> {
        val server = MockWebServer()
        server.start()
        return try {
            server.enqueue(
                MockResponse()
                    .setResponseCode(code)
                    .setHeader("Content-Type", "application/json")
                    .setBody(body),
            )
            val repo = PaymentRepository(
                PaymentApi(
                    Retrofit.Builder()
                        .baseUrl(server.url("/"))
                        .addConverterFactory(json.asConverterFactory("application/json".toMediaType()))
                        .build()
                        .create(GenPaymentApi::class.java),
                ),
                json,
            )
            repo.createPaymentIntent(ORDER_ID).also { onRequest(server.takeRequest()) }
        } finally {
            server.shutdown()
        }
    }

    private suspend fun loaded(body: String): CreatePaymentIntentResponse {
        val result = intent(body)
        assertTrue("expected the captured payload to map; got $result", result is ApiResult.Success)
        return (result as ApiResult.Success).data
    }

    private suspend fun assertRefusesNaming(field: String, body: String) {
        val result = intent(body)
        assertTrue("a missing $field must refuse; got $result", result is ApiResult.Error)
        val error = (result as ApiResult.Error).error
        assertTrue(
            "a broken 2xx body is the server's fault, not the connection's; got $error",
            error is ApiError.Server,
        )
        assertTrue(
            "the refusal must name $field, but said \"${(error as ApiError.Server).message}\"",
            error.message.startsWith("$field "),
        )
    }

    // --- the field-name contract ------------------------------------------------

    @Test
    fun paymentIntentDtoSerialNamesAreExactlyTheSpecProperties() {
        assertEquals(
            SPEC_PROPERTIES,
            serialNames(GenCreatePaymentIntentResponse.serializer().descriptor),
        )
    }

    @Test
    fun theRequestKeepsThePathTheServerBinds() = runTest {
        var path: String? = null
        var method: String? = null
        intent(CAPTURED_INTENT) { request ->
            path = request.path
            method = request.method
        }

        assertEquals("POST", method)
        assertEquals("/api/Payment/CreatePaymentIntent", path)
    }

    // --- rule 1 applied to credentials: nothing is supplied for a missing one ------

    @Test
    fun everyCredentialArrivesWithItsLiteralValue() = runTest {
        val sheet = loaded(CAPTURED_INTENT)

        assertEquals("pi_3ABC_secret_XYZ", sheet.clientSecret)
        assertEquals("pi_3ABC", sheet.paymentIntentId)
        assertEquals("cus_ABC123", sheet.stripeCustomerId)
        assertEquals("ek_test_ABC", sheet.ephemeralKey)
    }

    /**
     * Dropping the body instead reported "the server sent no body" for a body it did send, on the
     * screen where a customer is trying to pay — so the one fact worth having, which credential was
     * missing, was the one thing thrown away.
     */
    @Test
    fun aMissingCredentialRefusesByNameRatherThanReportingAnEmptyBody() = runTest {
        SPEC_PROPERTIES.forEach { field ->
            assertRefusesNaming(field, withoutKey(CAPTURED_INTENT, field))
        }
    }

    @Test
    fun aBodylessSuccessIsRefusedRatherThanOpeningASheetWithNoCredentials() = runTest {
        val result = intent("", code = 204)

        assertTrue("a 2xx with no body cannot open a PaymentSheet; got $result", result is ApiResult.Error)
    }

    // --- payload plumbing ---------------------------------------------------------

    private fun withoutKey(body: String, key: String): String =
        JsonObject(Json.parseToJsonElement(body).jsonObject.toMutableMap().apply { remove(key) })
            .toString()

    @OptIn(ExperimentalSerializationApi::class)
    private fun serialNames(descriptor: SerialDescriptor): Set<String> =
        (0 until descriptor.elementsCount).map { descriptor.getElementName(it) }.toSet()

    private companion object {
        const val ORDER_ID = "ord-7"

        /** Every member non-empty, so a forgotten field cannot pass as a mapped one. */
        val CAPTURED_INTENT = """
            {
              "clientSecret": "pi_3ABC_secret_XYZ",
              "paymentIntentId": "pi_3ABC",
              "stripeCustomerId": "cus_ABC123",
              "ephemeralKey": "ek_test_ABC"
            }
        """.trimIndent()

        val SPEC_PROPERTIES = setOf(
            "clientSecret",
            "paymentIntentId",
            "stripeCustomerId",
            "ephemeralKey",
        )
    }
}
