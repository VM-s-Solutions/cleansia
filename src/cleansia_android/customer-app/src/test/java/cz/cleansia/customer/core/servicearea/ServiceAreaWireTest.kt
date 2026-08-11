package cz.cleansia.customer.core.servicearea

import cz.cleansia.customer.core.network.IntEnumSerializersModule
import cz.cleansia.core.servicearea.ServicedCountry
import kotlinx.coroutines.test.runTest
import kotlinx.serialization.ExperimentalSerializationApi
import kotlinx.serialization.descriptors.SerialDescriptor
import kotlinx.serialization.json.Json
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
import cz.cleansia.customer.api.client.CountryApi as GenCountryApi
import cz.cleansia.customer.api.client.ServiceCityApi as GenServiceCityApi
import cz.cleansia.customer.api.model.CountryListItem as GenCountryListItem
import cz.cleansia.customer.api.model.ServiceCityDto as GenServiceCityDto

/**
 * `ServiceAreaProvider` states the contract this source has to keep: *"Null = the fetch failed and
 * the answer is UNKNOWN — treat it as 'couldn't check', never as 'serves nothing'."* An empty list is
 * the opposite answer, and it is the one that tells a customer their address is outside the service
 * area. This source is also the reason the roster keys on the generated **client** package as well as
 * the model one — it maps `dto.id` without ever importing the DTO type, so a model-only sweep never
 * saw it.
 */
class ServiceAreaWireTest {

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
        call: suspend (CustomerServiceAreaDataSource) -> T,
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
            val retrofit = Retrofit.Builder()
                .baseUrl(server.url("/"))
                .addConverterFactory(json.asConverterFactory("application/json".toMediaType()))
                .build()
            call(
                CustomerServiceAreaDataSource(
                    retrofit.create(GenCountryApi::class.java),
                    retrofit.create(GenServiceCityApi::class.java),
                ),
            ).also { onRequest(server.takeRequest()) }
        } finally {
            server.shutdown()
        }
    }

    private suspend fun countries(body: String, code: Int = 200) =
        serving(body, code) { it.fetchServicedCountries() }

    private suspend fun cities(body: String, code: Int = 200) =
        serving(body, code) { it.fetchServiceCities(countryId = null) }

    // --- the field-name contract ------------------------------------------------

    @Test
    fun countryDtoSerialNamesAreExactlyTheSpecProperties() {
        assertEquals(COUNTRY_SPEC_PROPERTIES, serialNames(GenCountryListItem.serializer().descriptor))
    }

    @Test
    fun cityDtoSerialNamesAreExactlyTheSpecProperties() {
        assertEquals(CITY_SPEC_PROPERTIES, serialNames(GenServiceCityDto.serializer().descriptor))
    }

    @Test
    fun theRequestsKeepThePathsTheServerBinds() = runTest {
        var path: String? = null
        serving(CAPTURED_COUNTRIES, onRequest = { path = it.path }) { it.fetchServicedCountries() }
        assertEquals("/api/Country/GetServiced", path)

        serving(CAPTURED_CITIES, onRequest = { path = it.path }) { it.fetchServiceCities(countryId = null) }
        assertEquals("/api/ServiceCity", path)
    }

    // --- every mapped field arrives with the value the wire carried ---------------

    @Test
    fun everyServicedCountryArrivesWithItsLiteralValue() = runTest {
        val list = countries(CAPTURED_COUNTRIES)

        assertEquals(listOf("cnt-cz", "cnt-sk"), list?.map { it.id })
        assertEquals(listOf("Česko", "Slovensko"), list?.map { it.name })
    }

    /**
     * The backend stores alpha-3 and everything Mapbox-facing matches alpha-2, so a raw lowercase
     * would equality-match neither.
     */
    @Test
    fun theIsoCodeIsNormalisedToAlphaTwo() = runTest {
        assertEquals(listOf("cz", "sk"), countries(CAPTURED_COUNTRIES)?.map { it.isoCode })
    }

    @Test
    fun everyServicedCityArrivesWithItsLiteralValue() = runTest {
        val list = cities(CAPTURED_CITIES)

        assertEquals(listOf("city-1", "city-2"), list?.map { it.id })
        assertEquals(listOf("Praha", "Brno"), list?.map { it.name })
    }

    // --- the answer that must never be fabricated ---------------------------------

    /**
     * The one this file exists for. `orEmpty()` here reported "we serve nowhere" for a request the
     * server never answered — and the caller's own doc forbids exactly that reading, three lines
     * above the call.
     */
    @Test
    fun aBodylessSuccessIsUnknownRatherThanServesNowhere() = runTest {
        assertNull(countries("", code = 204))
        assertNull(cities("", code = 204))
    }

    @Test
    fun anHttpFailureIsUnknownTheSameWay() = runTest {
        assertNull(countries("{}", code = 500))
        assertNull(cities("{}", code = 500))
    }

    /**
     * An empty array IS an answer — a tenant that has switched every city off — and it stays
     * distinguishable from the unknown above.
     */
    @Test
    fun anEmptyArrayIsAnAnswerAndNotAFailure() = runTest {
        assertNotNull(countries("[]"))
        assertEquals(emptyList<ServicedCountry>(), countries("[]"))
    }

    // --- rule 3: identity is dropped where the row cannot be addressed -------------

    @Test
    fun aCountryWithoutAnIdIsDroppedAndTheRestSurvive() = runTest {
        val body = CAPTURED_COUNTRIES.replace("\"id\": \"cnt-cz\",", "")

        assertEquals(listOf("cnt-sk"), countries(body)?.map { it.id })
    }

    @OptIn(ExperimentalSerializationApi::class)
    private fun serialNames(descriptor: SerialDescriptor): Set<String> =
        (0 until descriptor.elementsCount).map { descriptor.getElementName(it) }.toSet()

    private companion object {

        /** Every member non-default, alpha-3 as the backend stores it. */
        val CAPTURED_COUNTRIES = """
            [
              { "id": "cnt-cz", "isoCode": "CZE", "name": "Česko" },
              { "id": "cnt-sk", "isoCode": "SVK", "name": "Slovensko" }
            ]
        """.trimIndent()

        val CAPTURED_CITIES = """
            [
              {
                "id": "city-1",
                "countryId": "cnt-cz",
                "countryName": "Česko",
                "countryIsoCode": "CZE",
                "name": "Praha",
                "zipPrefix": "1",
                "isActive": true
              },
              {
                "id": "city-2",
                "countryId": "cnt-cz",
                "countryName": "Česko",
                "countryIsoCode": "CZE",
                "name": "Brno",
                "zipPrefix": "6",
                "isActive": true
              }
            ]
        """.trimIndent()

        val COUNTRY_SPEC_PROPERTIES = setOf("id", "isoCode", "name", "translations")

        val CITY_SPEC_PROPERTIES = setOf(
            "id",
            "countryId",
            "countryName",
            "countryIsoCode",
            "name",
            "zipPrefix",
            "isActive",
        )
    }
}
