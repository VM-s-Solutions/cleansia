package cz.cleansia.customer.core.market

import cz.cleansia.core.network.WireContractViolation
import cz.cleansia.customer.core.network.IntEnumSerializersModule
import kotlinx.coroutines.test.runTest
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
import okhttp3.mockwebserver.RecordedRequest
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNotNull
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Test
import retrofit2.Retrofit
import retrofit2.converter.kotlinx.serialization.asConverterFactory
import cz.cleansia.customer.api.client.MarketApi as GenMarketApi
import cz.cleansia.customer.api.model.MarketListItem as GenMarketListItem

/**
 * The market directory is what every pre-address read is keyed by and what the chip prints, so a
 * row that cannot name its id, its codes or its default flag refuses the page rather than being
 * dropped: the markets are alternatives to each other, and the default is picked off this list.
 */
class MarketWireTest {

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
        call: suspend (MarketApi) -> T,
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
            val api = MarketApi(
                Retrofit.Builder()
                    .baseUrl(server.url("/"))
                    .addConverterFactory(json.asConverterFactory("application/json".toMediaType()))
                    .build()
                    .create(GenMarketApi::class.java),
            )
            call(api).also { onRequest(server.takeRequest()) }
        } finally {
            server.shutdown()
        }
    }

    private suspend fun markets(body: String) = serving(body) { it.getMarkets() }.body()

    private suspend fun refuses(field: String, mapping: suspend () -> Any?) {
        val violation = try {
            mapping()
            null
        } catch (v: WireContractViolation) {
            v
        }
        assertNotNull("a missing $field must refuse the mapping", violation)
        assertTrue(
            "the refusal must name $field, but said \"${violation!!.message}\"",
            violation.message!!.startsWith("$field "),
        )
    }

    @Test
    fun dtoSerialNamesAreExactlyTheSpecProperties() {
        assertEquals(SPEC_PROPERTIES, serialNames(GenMarketListItem.serializer().descriptor))
    }

    @Test
    fun theRequestKeepsThePathTheServerBinds() = runTest {
        var path: String? = null
        serving(CAPTURED, onRequest = { path = it.path }) { it.getMarkets() }
        assertEquals("/api/Market/GetOverview", path)
    }

    @Test
    fun everyFieldArrivesWithItsLiteralValue() = runTest {
        val cze = markets(CAPTURED)!!.first()

        assertEquals("01J0CZE", cze.countryId)
        assertEquals("CZE", cze.isoCode)
        assertEquals("CZ", cze.isoAlpha2)
        assertEquals("Czechia", cze.name)
        assertEquals("Česko", cze.translations?.get("cs")?.name)
        assertEquals("01J0CZK", cze.currencyId)
        assertEquals("CZK", cze.currencyCode)
        assertEquals("Kč", cze.currencySymbol)
        assertEquals(true, cze.isDefault)
        assertEquals(250.0, cze.noShowCredit!!, 0.0)
        assertEquals(1_000_000.0, cze.insuranceCoverageAmount!!, 0.0)
    }

    @Test
    fun aMissingIdentityOrLabelRefusesThePage() = runTest {
        REQUIRED.forEach { field -> refuses(field) { markets(withFirstRow { it - field }) } }
    }

    /** ADR-0060: both copy figures are nullable by design — null means the no-figure copy renders. */
    @Test
    fun theCopyFiguresStayNullableWithoutRefusing() = runTest {
        val list = markets(withFirstRow { it - "noShowCredit" - "insuranceCoverageAmount" })

        assertNotNull(list)
        assertNull(list!!.first().noShowCredit)
        assertNull(list.first().insuranceCoverageAmount)
    }

    @Test
    fun anEmptyDirectoryIsAnEmptyListNotARefusal() = runTest {
        assertEquals(emptyList<MarketListItem>(), markets("[]"))
    }

    @Test
    fun aBodylessAnswerRefusesRatherThanReadingNoMarketIsOpen() = runTest {
        refuses("MarketListItem[]") {
            val server = MockWebServer()
            server.start()
            try {
                server.enqueue(MockResponse().setResponseCode(204))
                MarketApi(
                    Retrofit.Builder()
                        .baseUrl(server.url("/"))
                        .addConverterFactory(json.asConverterFactory("application/json".toMediaType()))
                        .build()
                        .create(GenMarketApi::class.java),
                ).getMarkets()
            } finally {
                server.shutdown()
            }
        }
    }

    private fun withFirstRow(transform: (JsonObject) -> JsonObject): String {
        val rows = Json.parseToJsonElement(CAPTURED).jsonArray.mapIndexed { index, row ->
            if (index == 0) transform(row.jsonObject) else row
        }
        return JsonArray(rows).toString()
    }

    private operator fun JsonObject.minus(key: String) =
        JsonObject(toMutableMap().apply { remove(key) })

    @OptIn(ExperimentalSerializationApi::class)
    private fun serialNames(descriptor: SerialDescriptor): Set<String> =
        (0 until descriptor.elementsCount).map { descriptor.getElementName(it) }.toSet()

    private companion object {
        val SPEC_PROPERTIES = setOf(
            "countryId",
            "isoCode",
            "isoAlpha2",
            "name",
            "translations",
            "currencyId",
            "currencyCode",
            "currencySymbol",
            "isDefault",
            "noShowCredit",
            "insuranceCoverageAmount",
        )

        val REQUIRED = listOf(
            "countryId",
            "isoCode",
            "isoAlpha2",
            "name",
            "currencyId",
            "currencyCode",
            "currencySymbol",
            "isDefault",
        )

        val CAPTURED = """
            [
              {
                "countryId": "01J0CZE",
                "isoCode": "CZE",
                "isoAlpha2": "CZ",
                "name": "Czechia",
                "translations": { "cs": { "name": "Česko", "description": null, "tagline": null } },
                "currencyId": "01J0CZK",
                "currencyCode": "CZK",
                "currencySymbol": "Kč",
                "isDefault": true,
                "noShowCredit": 250.00,
                "insuranceCoverageAmount": 1000000.00
              },
              {
                "countryId": "01J0SVK",
                "isoCode": "SVK",
                "isoAlpha2": "SK",
                "name": "Slovakia",
                "translations": { "sk": { "name": "Slovensko", "description": null, "tagline": null } },
                "currencyId": "01J0EUR",
                "currencyCode": "EUR",
                "currencySymbol": "€",
                "isDefault": false,
                "noShowCredit": null,
                "insuranceCoverageAmount": null
              }
            ]
        """.trimIndent()
    }
}
