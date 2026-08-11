package cz.cleansia.customer.core.promo

import cz.cleansia.customer.core.network.IntEnumSerializersModule
import kotlinx.coroutines.test.runTest
import kotlinx.serialization.ExperimentalSerializationApi
import kotlinx.serialization.descriptors.SerialDescriptor
import kotlinx.serialization.json.Json
import kotlinx.serialization.json.JsonElement
import kotlinx.serialization.json.JsonNull
import kotlinx.serialization.json.JsonObject
import kotlinx.serialization.json.jsonObject
import okhttp3.MediaType.Companion.toMediaType
import okhttp3.mockwebserver.MockResponse
import okhttp3.mockwebserver.MockWebServer
import okhttp3.mockwebserver.RecordedRequest
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNotNull
import org.junit.Assert.assertNull
import org.junit.Test
import retrofit2.Retrofit
import retrofit2.converter.kotlinx.serialization.asConverterFactory
import cz.cleansia.customer.api.client.PromoCodeApi as GenPromoCodeApi
import cz.cleansia.customer.api.model.ValidatePromoCodeResponse as GenValidatePromoCodeResponse

/**
 * The validation verdict decides whether the customer is charged the discounted price or the full
 * one. `ValidatePromoCode_Response` declares no `required` array, so `isValid` arrives typed
 * `Boolean?` regardless of its `nullable: false`.
 */
class PromoCodeWireTest {

    private val json = Json {
        ignoreUnknownKeys = true
        isLenient = true
        explicitNulls = false
        serializersModule = IntEnumSerializersModule
    }

    private suspend fun <T> serving(
        body: String,
        code: Int = 200,
        onRequest: (RecordedRequest) -> Unit = {},
        call: suspend (PromoCodeApi) -> T,
    ): T {
        val server = MockWebServer()
        server.start()
        return try {
            server.enqueue(
                MockResponse()
                    .setResponseCode(code)
                    .setHeader("Content-Type", "application/json")
                    .setBody(body),
            )
            val api = PromoCodeApi(
                Retrofit.Builder()
                    .baseUrl(server.url("/"))
                    .addConverterFactory(json.asConverterFactory("application/json".toMediaType()))
                    .build()
                    .create(GenPromoCodeApi::class.java),
            )
            call(api).also { onRequest(server.takeRequest()) }
        } finally {
            server.shutdown()
        }
    }

    private suspend fun validation(body: String) =
        serving(body) { it.validate(ValidatePromoCodeRequest("SPRING20", 2400.0)) }.body()

    private suspend fun loadedValidation(body: String): ValidatePromoCodeResponse {
        val dto = validation(body)
        assertNotNull("expected the captured payload to map", dto)
        return dto!!
    }

    // --- the field-name contract ------------------------------------------------

    @Test
    fun promoCodeDtoSerialNamesAreExactlyTheSpecProperties() {
        assertEquals(SPEC_PROPERTIES, serialNames(GenValidatePromoCodeResponse.serializer().descriptor))
    }

    @Test
    fun theRequestKeepsThePathTheServerBinds() = runTest {
        var path: String? = null
        serving(CAPTURED_VALID, onRequest = { path = it.path }) {
            it.validate(ValidatePromoCodeRequest("SPRING20", 2400.0))
        }
        assertEquals("/api/PromoCode/Validate", path)
    }

    // --- rule 1: money is never coerced -----------------------------------------

    @Test
    fun theDiscountArrivesWithItsLiteralValue() = runTest {
        val dto = loadedValidation(CAPTURED_VALID)

        assertEquals(true, dto.isValid)
        assertEquals(480.0, dto.discountAmount)
    }

    // --- rule 2: booleans follow the money rule ---------------------------------

    /**
     * `false` is the verdict that rejects the code, so a default charges the customer full price for
     * a discount the server just granted, and the sheet states a reason the server never sent.
     */
    @Test
    fun aMissingVerdictRefusesTheAnswerRatherThanRejectingTheCode() = runTest {
        assertNull(validation(withoutKey(CAPTURED_VALID, "isValid")))
    }

    @Test
    fun aRejectedCodeKeepsItsReason() = runTest {
        val dto = loadedValidation(CAPTURED_INVALID)

        assertEquals(false, dto.isValid)
        assertEquals(PromoCodeError.Expired, PromoCodeError.fromString(dto.errorCode))
    }

    // --- rule 5: nullable-by-design fields stay nullable ---------------------------

    /**
     * `discountAmount` is `nullable: true` — an invalid code has no discount — so it stays null
     * rather than becoming a zero the sheet would apply as a discount of nothing. `BookingViewModel`
     * already requires it to be non-null before applying one.
     */
    @Test
    fun anInvalidCodeHasNoDiscountRatherThanADiscountOfZero() = runTest {
        assertNull(loadedValidation(CAPTURED_INVALID).discountAmount)
        assertNull(loadedValidation(withKey(CAPTURED_VALID, "discountAmount", JsonNull)).discountAmount)
    }

    // --- the refused body ---------------------------------------------------------

    @Test
    fun aBodylessSuccessIsRefusedRatherThanReadingAsInvalid() = runTest {
        assertNull(
            serving("", code = 204) { it.validate(ValidatePromoCodeRequest("SPRING20", 2400.0)) }.body(),
        )
    }

    // --- payload plumbing ---------------------------------------------------------

    private fun mutating(body: String, transform: (JsonObject) -> JsonObject): String =
        transform(Json.parseToJsonElement(body).jsonObject).toString()

    private fun withoutKey(body: String, key: String): String = mutating(body) { it - key }

    private fun withKey(body: String, key: String, value: JsonElement): String =
        mutating(body) { it + (key to value) }

    private operator fun JsonObject.minus(key: String) =
        JsonObject(toMutableMap().apply { remove(key) })

    private operator fun JsonObject.plus(entry: Pair<String, JsonElement>) =
        JsonObject(toMutableMap().apply { put(entry.first, entry.second) })

    @OptIn(ExperimentalSerializationApi::class)
    private fun serialNames(descriptor: SerialDescriptor): Set<String> =
        (0 until descriptor.elementsCount).map { descriptor.getElementName(it) }.toSet()

    private companion object {

        /** Every member non-zero and non-default, including the nullable-by-design discount. */
        val CAPTURED_VALID = """
            {
              "isValid": true,
              "discountAmount": 480.0,
              "errorCode": "Expired"
            }
        """.trimIndent()

        val CAPTURED_INVALID = """
            {
              "isValid": false,
              "errorCode": "Expired"
            }
        """.trimIndent()

        val SPEC_PROPERTIES = setOf("isValid", "discountAmount", "errorCode")
    }
}
