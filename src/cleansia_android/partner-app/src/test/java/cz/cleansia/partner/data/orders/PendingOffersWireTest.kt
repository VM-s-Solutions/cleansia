package cz.cleansia.partner.data.orders

import cz.cleansia.core.network.ApiResult
import cz.cleansia.partner.api.client.OrderApi
import cz.cleansia.partner.api.model.PendingOfferItem
import kotlinx.coroutines.test.runTest
import kotlinx.serialization.ExperimentalSerializationApi
import kotlinx.serialization.descriptors.SerialDescriptor
import kotlinx.serialization.json.Json
import kotlinx.serialization.json.JsonArray
import kotlinx.serialization.json.JsonElement
import kotlinx.serialization.json.JsonNull
import kotlinx.serialization.json.JsonObject
import kotlinx.serialization.json.jsonArray
import kotlinx.serialization.json.jsonObject
import okhttp3.MediaType.Companion.toMediaType
import okhttp3.mockwebserver.MockResponse
import okhttp3.mockwebserver.MockWebServer
import okhttp3.mockwebserver.RecordedRequest
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Test
import retrofit2.Retrofit
import retrofit2.converter.kotlinx.serialization.asConverterFactory

/**
 * A pending offer is a job the platform has already put this cleaner's name on, and the card is the
 * only place they see its price and its deadline. `PendingOfferItem` declares no `required` array, so
 * the generator types every property optional-with-null regardless of `nullable: false` and the whole
 * contract lands in the mapper. Everything here decodes a captured payload over a socket through the
 * generated DTO and the production `Json`.
 */
class PendingOffersWireTest {

    private val json = Json { ignoreUnknownKeys = true; isLenient = true; explicitNulls = false }

    private fun repo(server: MockWebServer) = OrdersRepositoryImpl(
        Retrofit.Builder()
            .baseUrl(server.url("/"))
            .addConverterFactory(json.asConverterFactory("application/json".toMediaType()))
            .build()
            .create(OrderApi::class.java),
        json,
    )

    private suspend fun fetch(
        body: String,
        onRequest: (RecordedRequest) -> Unit = {},
    ): ApiResult<List<PendingOffer>> {
        val server = MockWebServer()
        server.start()
        return try {
            server.enqueue(
                MockResponse()
                    .setResponseCode(200)
                    .setHeader("Content-Type", "application/json")
                    .setBody(body),
            )
            repo(server).refreshPendingOffers().also { onRequest(server.takeRequest()) }
        } finally {
            server.shutdown()
        }
    }

    private suspend fun loaded(body: String): List<PendingOffer> {
        val result = fetch(body)
        assertTrue("expected the captured payload to map; got $result", result is ApiResult.Success)
        return (result as ApiResult.Success).data
    }

    private suspend fun assertMappingFails(field: String, body: String) {
        val result = fetch(body)
        assertTrue(
            "a missing $field must fail the mapping rather than read as a default; got $result",
            result is ApiResult.Error,
        )
    }

    // --- the field-name contract ------------------------------------------------

    @Test
    fun offerDtoSerialNamesAreExactlyTheSpecProperties() {
        assertEquals(OFFER_SPEC_PROPERTIES, serialNames(PendingOfferItem.serializer().descriptor))
    }

    @Test
    fun theRequestKeepsThePathTheServerBinds() = runTest {
        var path: String? = null
        var method: String? = null
        fetch(CAPTURED_PAYLOAD) { request ->
            path = request.path
            method = request.method
        }

        assertEquals("GET", method)
        assertEquals("/api/Order/MyPendingOffers", path)
    }

    // --- rule 1: money is never coerced -----------------------------------------

    @Test
    fun everyOfferMoneyFieldArrivesWithItsLiteralValue() = runTest {
        val offers = loaded(CAPTURED_PAYLOAD)

        assertEquals(1850.50, offers.first().totalPrice, 0.0)
        assertEquals(2640.00, offers.last().totalPrice, 0.0)
    }

    @Test
    fun aMissingPriceFailsTheMappingRatherThanOfferingTheJobForNothing() = runTest {
        assertMappingFails("totalPrice", payloadWithoutFirstOfferKey("totalPrice"))
    }

    @Test
    fun anExplicitNullPriceFailsTheMappingToo() = runTest {
        assertMappingFails("totalPrice", payloadWithFirstOffer { it + ("totalPrice" to JsonNull) })
    }

    /**
     * Scope and duration are the two numbers a cleaner weighs the price against, and both are gates:
     * `rooms`/`bathrooms` at zero delete the scope line from the card entirely, and a zeroed
     * `estimatedTime` shortens the rendered date range. Each one silently understates the job.
     */
    @Test
    fun aMissingScopeOrDurationKeyFailsTheMappingRatherThanUnderstatingTheJob() = runTest {
        OFFER_REQUIRED_NUMBERS.forEach { field ->
            assertMappingFails(field, payloadWithoutFirstOfferKey(field))
        }
    }

    @Test
    fun everyOfferScopeFieldArrivesWithItsLiteralValue() = runTest {
        val offer = loaded(CAPTURED_PAYLOAD).first()

        assertEquals(3, offer.rooms)
        assertEquals(2, offer.bathrooms)
        assertEquals(240, offer.estimatedTime)
    }

    // --- rule 2: booleans follow the money rule ---------------------------------
    //
    // `PendingOfferItem` carries no boolean; the rule is exercised against this app's payroll wires in
    // PeriodPayWireTest and InvoicesWireTest.

    // --- rule 3: identity is refused, never synthesized --------------------------

    /**
     * Drop, not refuse: no surface sums these rows, so a lost one cannot falsify a figure, while
     * failing the page would hide every other offer the server answered correctly. An id-less offer was
     * already inert — confirm and decline are both keyed by it — so the card was untappable anyway.
     */
    @Test
    fun anOfferWithoutAnIdIsDroppedAndTheRestOfTheBoardSurvives() = runTest {
        val offers = loaded(payloadWithFirstOffer { it - "id" })

        assertEquals(listOf("offer-2"), offers.map { it.id })
        assertEquals(2640.00, offers.first().totalPrice, 0.0)
    }

    @Test
    fun anOfferWithAnExplicitNullIdIsDropped() = runTest {
        val offers = loaded(payloadWithFirstOffer { it + ("id" to JsonNull) })

        assertEquals(listOf("offer-2"), offers.map { it.id })
    }

    @Test
    fun everySurvivingOfferKeepsTheIdTheWireCarried() = runTest {
        assertEquals(listOf("offer-1", "offer-2"), loaded(CAPTURED_PAYLOAD).map { it.id })
    }

    // --- rule 4: collections do default -----------------------------------------

    @Test
    fun aCleanerWithNothingReservedGetsAnEmptyBoardRatherThanAnError() = runTest {
        assertEquals(emptyList<PendingOffer>(), loaded("[]"))
    }

    // --- rule 5: nullable-by-design fields stay nullable ---------------------------

    @Test
    fun nullableByDesignOfferFieldsStayNullRatherThanBecomingPlaceholders() = runTest {
        val offer = loaded(
            payloadWithFirstOffer { row -> OFFER_NULLABLE_FIELDS.fold(row) { it, key -> it - key } },
        ).first()

        assertNull(offer.displayOrderNumber)
        assertNull(offer.customerAddressApproximate)
        assertNull(offer.currencyCode)
        assertEquals(1850.50, offer.totalPrice, 0.0)
    }

    /**
     * The deadline and the cleaning slot are not nullable-by-design the way an order number is: there
     * is no state in which a live reservation has no expiry, and leaving them nullable kept a silent
     * drop in [cz.cleansia.partner.features.orders.soonestOffer], where an offer with no parseable
     * deadline quietly stopped being the one the dashboard card names.
     */
    @Test
    fun aMissingDeadlineOrCleaningSlotFailsTheMappingRatherThanRenderingAnOfferWithNoExpiry() = runTest {
        assertMappingFails("respondByUtc", payloadWithoutFirstOfferKey("respondByUtc"))
        assertMappingFails("cleaningDateTime", payloadWithoutFirstOfferKey("cleaningDateTime"))
    }

    // --- payload plumbing ---------------------------------------------------------

    private fun payloadWithFirstOffer(transform: (JsonObject) -> JsonObject): String {
        val rows = Json.parseToJsonElement(CAPTURED_PAYLOAD).jsonArray.mapIndexed { index, row ->
            if (index == 0) transform(row.jsonObject) else row
        }
        return JsonArray(rows).toString()
    }

    private fun payloadWithoutFirstOfferKey(key: String): String = payloadWithFirstOffer { it - key }

    private operator fun JsonObject.minus(key: String) =
        JsonObject(toMutableMap().apply { remove(key) })

    private operator fun JsonObject.plus(entry: Pair<String, JsonElement>) =
        JsonObject(toMutableMap().apply { put(entry.first, entry.second) })

    @OptIn(ExperimentalSerializationApi::class)
    private fun serialNames(descriptor: SerialDescriptor): Set<String> =
        (0 until descriptor.elementsCount).map { descriptor.getElementName(it) }.toSet()

    private companion object {

        /** Every member non-zero and non-default, so a forgotten field cannot pass as a mapped one. */
        val CAPTURED_PAYLOAD = """
            [
              {
                "id": "offer-1",
                "displayOrderNumber": "CL-2026-0042",
                "cleaningDateTime": "2026-08-12T09:00:00Z",
                "estimatedTime": 240,
                "respondByUtc": "2026-08-10T18:40:00Z",
                "customerAddressApproximate": "Praha 4, 140 xx",
                "rooms": 3,
                "bathrooms": 2,
                "totalPrice": 1850.50,
                "currencyCode": "CZK"
              },
              {
                "id": "offer-2",
                "displayOrderNumber": "CL-2026-0043",
                "cleaningDateTime": "2026-08-13T14:00:00Z",
                "estimatedTime": 360,
                "respondByUtc": "2026-08-11T09:15:00Z",
                "customerAddressApproximate": "Brno-stred, 602 xx",
                "rooms": 5,
                "bathrooms": 3,
                "totalPrice": 2640.00,
                "currencyCode": "CZK"
              }
            ]
        """.trimIndent()

        val OFFER_SPEC_PROPERTIES = setOf(
            "id",
            "displayOrderNumber",
            "cleaningDateTime",
            "estimatedTime",
            "respondByUtc",
            "customerAddressApproximate",
            "rooms",
            "bathrooms",
            "totalPrice",
            "currencyCode",
        )

        val OFFER_REQUIRED_NUMBERS = listOf("estimatedTime", "rooms", "bathrooms")

        val OFFER_NULLABLE_FIELDS = listOf(
            "displayOrderNumber",
            "customerAddressApproximate",
            "currencyCode",
        )
    }
}
