package cz.cleansia.partner.core.market

import cz.cleansia.core.network.ApiResult
import cz.cleansia.partner.core.network.NetworkModule
import kotlinx.coroutines.test.runTest
import kotlinx.serialization.ExperimentalSerializationApi
import kotlinx.serialization.descriptors.SerialDescriptor
import okhttp3.MediaType.Companion.toMediaType
import okhttp3.mockwebserver.MockResponse
import okhttp3.mockwebserver.MockWebServer
import okhttp3.mockwebserver.RecordedRequest
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Test
import retrofit2.Retrofit
import retrofit2.converter.kotlinx.serialization.asConverterFactory

/**
 * The register form's picker is fed by this read and preselects off `isDefault`, so a silently
 * dropped or invented row is a cleaner registered with the wrong operating company (ADR-0061 D6).
 * The interface is hand-written until the partner spec carries `Market/GetOverview`; the property
 * names below are the customer spec's `MarketListItem`, restricted to what the picker renders.
 */
class MarketWireTest {

    private val json = NetworkModule.provideJson()

    private suspend fun <T> serving(
        body: String,
        code: Int = 200,
        onRequest: (RecordedRequest) -> Unit = {},
        call: suspend (MarketRepository) -> T,
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
            val api = Retrofit.Builder()
                .baseUrl(server.url("/"))
                .addConverterFactory(json.asConverterFactory("application/json".toMediaType()))
                .build()
                .create(MarketApi::class.java)
            call(MarketRepository(api, json)).also { onRequest(server.takeRequest()) }
        } finally {
            server.shutdown()
        }
    }

    @Test
    fun marketDtoSerialNamesAreTheSpecPropertiesThePickerRenders() {
        assertEquals(SPEC_PROPERTIES, serialNames(MarketListItem.serializer().descriptor))
    }

    @Test
    fun theReadKeepsThePathTheServerBinds() = runTest {
        var path: String? = null
        serving(CAPTURED, onRequest = { path = it.path }) { it.getMarkets() }
        assertEquals("/api/Market/GetOverview", path)
    }

    @Test
    fun everyListedMarketArrivesWithItsIdentityAndTheDefaultFlag() = runTest {
        val markets = (serving(CAPTURED) { it.getMarkets() } as ApiResult.Success).data

        assertEquals(listOf("cze-id", "svk-id"), markets.map { it.countryId })
        assertEquals(listOf("CZE", "SVK"), markets.map { it.isoCode })
        assertEquals(listOf(true, false), markets.map { it.isDefault })
        assertEquals("Česko", markets[0].translations?.get("cs")?.name)
        assertEquals("cze-id", markets.defaultOrFirst()?.countryId)
    }

    @Test
    fun theFieldsThePickerDoesNotRenderAreIgnoredNotRefused() = runTest {
        val markets = (serving(CAPTURED) { it.getMarkets() } as ApiResult.Success).data
        assertEquals(2, markets.size)
    }

    /** A row with no id cannot be sent, so the whole read is refused rather than the row invented. */
    @Test
    fun aMarketWithoutAnIdRefusesTheRead() = runTest {
        val result = serving(CAPTURED.replace("\"countryId\": \"svk-id\",", "")) { it.getMarkets() }
        assertTrue("expected Error but was $result", result is ApiResult.Error)
    }

    @Test
    fun aMarketWithoutTheDefaultFlagRefusesTheRead() = runTest {
        val result = serving(CAPTURED.replace("\"isDefault\": true,", "")) { it.getMarkets() }
        assertTrue("expected Error but was $result", result is ApiResult.Error)
    }

    @OptIn(ExperimentalSerializationApi::class)
    private fun serialNames(descriptor: SerialDescriptor): Set<String> =
        (0 until descriptor.elementsCount).map { descriptor.getElementName(it) }.toSet()

    private companion object {
        val SPEC_PROPERTIES = setOf("countryId", "isoCode", "isoAlpha2", "name", "translations", "isDefault")

        val CAPTURED = """
            [
              {
                "countryId": "cze-id",
                "isoCode": "CZE",
                "isoAlpha2": "CZ",
                "name": "Czech Republic",
                "translations": { "cs": { "name": "Česko", "description": null } },
                "currencyId": "cur-czk",
                "currencyCode": "CZK",
                "currencySymbol": "Kč",
                "isDefault": true,
                "noShowCredit": 200,
                "insuranceCoverageAmount": 1000000
              },
              {
                "countryId": "svk-id",
                "isoCode": "SVK",
                "isoAlpha2": "SK",
                "name": "Slovakia",
                "translations": null,
                "currencyId": "cur-eur",
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
