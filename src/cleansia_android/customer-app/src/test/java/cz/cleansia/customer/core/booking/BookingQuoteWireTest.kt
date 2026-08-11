package cz.cleansia.customer.core.booking

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
import cz.cleansia.customer.api.client.OrderApi as GenOrderApi
import cz.cleansia.customer.api.model.QuoteOrderResponse as GenQuoteOrderResponse

/**
 * The quote is the number a customer agrees to before they pay, and `QuoteOrder_Response` declares no
 * `required` array, so the generator types every property optional-with-null regardless of
 * `nullable: false`. The adapter already refused `currencyId`/`currencyCode` and defaulted everything
 * else to zero — which is a price, rendered as a price, that the server never sent.
 *
 * Everything here decodes a captured payload over a socket through the generated DTO and the
 * production `Json` (including [IntEnumSerializersModule], without which every int-valued enum on this
 * wire decodes differently than it does in the app).
 */
class BookingQuoteWireTest {

    private val json = Json {
        ignoreUnknownKeys = true
        isLenient = true
        explicitNulls = false
        serializersModule = IntEnumSerializersModule
    }

    private suspend fun quote(
        body: String,
        onRequest: (RecordedRequest) -> Unit = {},
    ): QuoteOrderResponse? {
        val server = MockWebServer()
        server.start()
        return try {
            server.enqueue(
                MockResponse()
                    .setResponseCode(200)
                    .setHeader("Content-Type", "application/json")
                    .setBody(body),
            )
            val api = BookingApi(
                Retrofit.Builder()
                    .baseUrl(server.url("/"))
                    .addConverterFactory(json.asConverterFactory("application/json".toMediaType()))
                    .build()
                    .create(GenOrderApi::class.java),
            )
            api.quote(COMMAND).body().also { onRequest(server.takeRequest()) }
        } finally {
            server.shutdown()
        }
    }

    private suspend fun quoted(body: String): QuoteOrderResponse {
        val response = quote(body)
        assertNotNull("expected the captured payload to map", response)
        return response!!
    }

    private suspend fun assertQuoteRefused(field: String, body: String) {
        assertNull(
            "a missing $field must refuse the quote rather than price it at zero",
            quote(body),
        )
    }

    // --- the field-name contract ------------------------------------------------

    @Test
    fun quoteDtoSerialNamesAreExactlyTheSpecProperties() {
        assertEquals(QUOTE_SPEC_PROPERTIES, serialNames(GenQuoteOrderResponse.serializer().descriptor))
    }

    @Test
    fun theRequestKeepsThePathTheServerBinds() = runTest {
        var path: String? = null
        var method: String? = null
        quote(CAPTURED_QUOTE) { request ->
            path = request.path
            method = request.method
        }

        assertEquals("POST", method)
        assertEquals("/api/Order/Quote", path)
    }

    // --- rule 1: money is never coerced -----------------------------------------

    @Test
    fun everyQuoteMoneyFieldArrivesWithItsLiteralValue() = runTest {
        val quote = quoted(CAPTURED_QUOTE)

        assertEquals(4380.00, quote.totalPrice, 0.0)
        assertEquals(3942.00, quote.finalPriceAfterDiscount, 0.0)
        assertEquals(3650.00, quote.originalSubtotal, 0.0)
        assertEquals(2900.00, quote.servicesSubtotal, 0.0)
        assertEquals(450.00, quote.packagesSubtotal, 0.0)
        assertEquals(300.00, quote.extrasSubtotal, 0.0)
        assertEquals(730.00, quote.expressSurchargeAmount, 0.0)
        assertEquals(24.75, quote.exchangeRate, 0.0)
    }

    @Test
    fun aMissingQuoteMoneyKeyRefusesTheQuoteRatherThanPricingItAtZero() = runTest {
        QUOTE_REQUIRED_MONEY.forEach { field ->
            assertQuoteRefused(field, withoutKey(CAPTURED_QUOTE, field))
        }
    }

    @Test
    fun anExplicitNullQuoteMoneyKeyRefusesTheQuoteToo() = runTest {
        assertQuoteRefused("totalPrice", withKey(CAPTURED_QUOTE, "totalPrice", JsonNull))
    }

    /**
     * `exchangeRate` used to default to `1.0`, which is not a neutral fallback: every converted figure
     * on the confirm step would be off by the true rate — 24.75× for a CZK order priced in EUR.
     */
    @Test
    fun aMissingExchangeRateRefusesTheQuoteRatherThanAssumingParity() = runTest {
        assertQuoteRefused("exchangeRate", withoutKey(CAPTURED_QUOTE, "exchangeRate"))
    }

    /**
     * The discount source names which discount the customer is getting. Defaulting it to `0` reports
     * "no discount applied" over a total that already had one deducted, so the summary's rows and its
     * bottom line stop agreeing.
     */
    @Test
    fun aMissingDiscountSourceRefusesTheQuoteRatherThanReportingNoDiscount() = runTest {
        assertQuoteRefused("appliedDiscountSource", withoutKey(CAPTURED_QUOTE, "appliedDiscountSource"))
    }

    @Test
    fun theDiscountSourceArrivesWithItsLiteralValue() = runTest {
        assertEquals(2, quoted(CAPTURED_QUOTE).appliedDiscountSource)
    }

    // --- rule 2: booleans follow the money rule ---------------------------------

    @Test
    fun aMissingExpressSurchargeVerdictRefusesTheQuoteRatherThanReadingFalse() = runTest {
        assertQuoteRefused(
            "expressSurchargeApplied",
            withoutKey(CAPTURED_QUOTE, "expressSurchargeApplied"),
        )
    }

    /**
     * The waiver verdict decides whether a Plus member is charged the 20 % surcharge or nothing.
     * `false` re-charges every waived booking, and the customer reads the charge, not the flag.
     */
    @Test
    fun aMissingWaiverVerdictRefusesTheQuoteRatherThanReChargingAWaivedBooking() = runTest {
        assertQuoteRefused(
            "expressSurchargeWaivedByMembership",
            withoutKey(CAPTURED_QUOTE, "expressSurchargeWaivedByMembership"),
        )
    }

    @Test
    fun anExplicitFalseVerdictIsARealBookingStateAndSurvives() = runTest {
        val quote = quoted(withKey(CAPTURED_QUOTE, "expressSurchargeWaivedByMembership", falseValue()))

        assertEquals(false, quote.expressSurchargeWaivedByMembership)
        assertEquals(true, quote.expressSurchargeApplied)
    }

    // --- rule 3: identity is refused, never synthesized --------------------------

    @Test
    fun aQuoteWithoutACurrencyIsRefusedRatherThanPricedInNothing() = runTest {
        assertQuoteRefused("currencyId", withoutKey(CAPTURED_QUOTE, "currencyId"))
        assertQuoteRefused("currencyCode", withoutKey(CAPTURED_QUOTE, "currencyCode"))
    }

    @Test
    fun theCurrencyArrivesWithItsLiteralValue() = runTest {
        val quote = quoted(CAPTURED_QUOTE)

        assertEquals("cur-1", quote.currencyId)
        assertEquals("CZK", quote.currencyCode)
    }

    // --- rule 4: collections do default -----------------------------------------
    //
    // `QuoteOrder_Response` carries no collection; the rule is exercised on this app's other wires.

    // --- rule 5: nullable-by-design fields stay nullable ---------------------------

    /**
     * These three are the only `nullable: true` numbers on the quote, and each absence is a real state:
     * no tier discount, no membership discount, no unmet tier floor. Zeroing them would read as
     * "a 0 Kč discount applied", which is a different sentence from "none applied".
     */
    @Test
    fun aQuoteWithNoDiscountsKeepsThemNullRatherThanZero() = runTest {
        val quote = quoted(
            QUOTE_NULLABLE_MONEY.fold(CAPTURED_QUOTE) { body, key -> withoutKey(body, key) },
        )

        assertNull(quote.tierDiscountAmount)
        assertNull(quote.membershipDiscountAmount)
        assertNull(quote.tierDiscountMinOrderAmount)
        assertEquals(4380.00, quote.totalPrice, 0.0)
    }

    @Test
    fun aQuoteWithDiscountsCarriesTheirLiteralValues() = runTest {
        val quote = quoted(CAPTURED_QUOTE)

        assertEquals(146.00, quote.tierDiscountAmount)
        assertEquals(292.00, quote.membershipDiscountAmount)
        assertEquals(1500.00, quote.tierDiscountMinOrderAmount)
    }

    // --- payload plumbing ---------------------------------------------------------

    private fun mutating(body: String, transform: (JsonObject) -> JsonObject): String =
        transform(Json.parseToJsonElement(body).jsonObject).toString()

    private fun withoutKey(body: String, key: String): String = mutating(body) { it - key }

    private fun withKey(body: String, key: String, value: JsonElement): String =
        mutating(body) { it + (key to value) }

    private fun falseValue() = kotlinx.serialization.json.JsonPrimitive(false)

    private operator fun JsonObject.minus(key: String) =
        JsonObject(toMutableMap().apply { remove(key) })

    private operator fun JsonObject.plus(entry: Pair<String, JsonElement>) =
        JsonObject(toMutableMap().apply { put(entry.first, entry.second) })

    @OptIn(ExperimentalSerializationApi::class)
    private fun serialNames(descriptor: SerialDescriptor): Set<String> =
        (0 until descriptor.elementsCount).map { descriptor.getElementName(it) }.toSet()

    private companion object {
        val COMMAND = QuoteOrderCommand(
            selectedServiceIds = listOf("svc-1"),
            selectedPackageIds = listOf("pkg-1"),
            rooms = 3,
            bathrooms = 2,
            selectedExtraSlugs = listOf("inside-oven"),
            cleaningDate = "2026-08-11T13:00:00Z",
        )

        /** Every member non-zero and non-default, including the three nullable-by-design discounts. */
        val CAPTURED_QUOTE = """
            {
              "totalPrice": 4380.00,
              "finalPriceAfterDiscount": 3942.00,
              "originalSubtotal": 3650.00,
              "appliedDiscountSource": 2,
              "tierDiscountAmount": 146.00,
              "membershipDiscountAmount": 292.00,
              "tierDiscountMinOrderAmount": 1500.00,
              "currencyId": "cur-1",
              "currencyCode": "CZK",
              "servicesSubtotal": 2900.00,
              "packagesSubtotal": 450.00,
              "extrasSubtotal": 300.00,
              "expressSurchargeApplied": true,
              "expressSurchargeAmount": 730.00,
              "exchangeRate": 24.75,
              "expressSurchargeWaivedByMembership": false,
              "expressUpgradesRemaining": 2
            }
        """.trimIndent()

        val QUOTE_SPEC_PROPERTIES = setOf(
            "totalPrice",
            "finalPriceAfterDiscount",
            "originalSubtotal",
            "appliedDiscountSource",
            "tierDiscountAmount",
            "membershipDiscountAmount",
            "tierDiscountMinOrderAmount",
            "currencyId",
            "currencyCode",
            "servicesSubtotal",
            "packagesSubtotal",
            "extrasSubtotal",
            "expressSurchargeApplied",
            "expressSurchargeAmount",
            "exchangeRate",
            "expressSurchargeWaivedByMembership",
            "expressUpgradesRemaining",
        )

        val QUOTE_REQUIRED_MONEY = listOf(
            "totalPrice",
            "finalPriceAfterDiscount",
            "originalSubtotal",
            "servicesSubtotal",
            "packagesSubtotal",
            "extrasSubtotal",
            "expressSurchargeAmount",
        )

        val QUOTE_NULLABLE_MONEY = listOf(
            "tierDiscountAmount",
            "membershipDiscountAmount",
            "tierDiscountMinOrderAmount",
        )
    }
}
