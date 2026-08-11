package cz.cleansia.customer.core.user

import cz.cleansia.customer.core.network.IntEnumSerializersModule
import kotlinx.coroutines.test.runTest
import kotlinx.serialization.ExperimentalSerializationApi
import kotlinx.serialization.descriptors.SerialDescriptor
import kotlinx.serialization.json.Json
import kotlinx.serialization.json.JsonArray
import kotlinx.serialization.json.JsonElement
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
import org.junit.Test
import retrofit2.Retrofit
import retrofit2.converter.kotlinx.serialization.asConverterFactory
import cz.cleansia.customer.api.client.SavedAddressApi as GenSavedAddressApi
import cz.cleansia.customer.api.model.SavedAddressDto as GenSavedAddressDto

/**
 * A saved address is where a cleaner is sent. Both `HomeTab` and `BookingBottomSheet` preselect
 * `firstOrNull { it.isDefault } ?: firstOrNull()`, so a lost or unflagged default does not empty the
 * picker — it substitutes a different home, plausibly and silently.
 */
class SavedAddressWireTest {

    private val json = Json {
        ignoreUnknownKeys = true
        isLenient = true
        explicitNulls = false
        serializersModule = IntEnumSerializersModule
    }

    private suspend fun <T> serving(
        body: String,
        onRequest: (RecordedRequest) -> Unit = {},
        call: suspend (SavedAddressApi) -> T,
    ): T {
        val server = MockWebServer()
        server.start()
        return try {
            server.enqueue(
                MockResponse()
                    .setResponseCode(200)
                    .setHeader("Content-Type", "application/json")
                    .setBody(body),
            )
            val api = SavedAddressApi(
                Retrofit.Builder()
                    .baseUrl(server.url("/"))
                    .addConverterFactory(json.asConverterFactory("application/json".toMediaType()))
                    .build()
                    .create(GenSavedAddressApi::class.java),
            )
            call(api).also { onRequest(server.takeRequest()) }
        } finally {
            server.shutdown()
        }
    }

    private suspend fun addresses(body: String) = serving(body) { it.getMine() }.body()

    private suspend fun loadedAddresses(body: String): List<SavedAddressDto> {
        val dto = addresses(body)
        assertNotNull("expected the captured payload to map", dto)
        return dto!!
    }

    // --- the field-name contract ------------------------------------------------

    @Test
    fun savedAddressDtoSerialNamesAreExactlyTheSpecProperties() {
        assertEquals(ADDRESS_SPEC_PROPERTIES, serialNames(GenSavedAddressDto.serializer().descriptor))
    }

    @Test
    fun theRequestKeepsThePathTheServerBinds() = runTest {
        var path: String? = null
        serving(CAPTURED_ADDRESSES, onRequest = { path = it.path }) { it.getMine() }
        assertEquals("/api/SavedAddress/GetMine", path)
    }

    // --- rule 2: booleans follow the money rule ---------------------------------

    @Test
    fun theDefaultFlagArrivesWithTheValueTheServerSent() = runTest {
        val list = loadedAddresses(CAPTURED_ADDRESSES)

        assertEquals(true, list.first().isDefault)
        assertEquals(false, list.last().isDefault)
    }

    /**
     * At `false` on every row the preselection falls through to whichever address happens to be
     * first, so the booking wizard silently proposes a different home than the customer's default.
     */
    @Test
    fun aMissingDefaultFlagRefusesTheListRatherThanMovingTheBooking() = runTest {
        assertNull(addresses(addressesWithFirstRow { it - "isDefault" }))
    }

    // --- rule 3: identity is refused, never synthesized --------------------------

    /**
     * Refuses the list rather than dropping the row for the same reason: a dropped default does not
     * leave the picker empty, it leaves a plausible substitute selected.
     */
    @Test
    fun anIdLessAddressRefusesTheListRatherThanSubstitutingAnother() = runTest {
        assertNull(addresses(addressesWithFirstRow { it - "id" }))
    }

    @Test
    fun aMissingAddressLineRefusesTheList() = runTest {
        ADDRESS_REQUIRED_FIELDS.forEach { field ->
            assertNull(
                "a missing $field must refuse the list",
                addresses(addressesWithFirstRow { it - field }),
            )
        }
    }

    // --- rule 4: collections do default -----------------------------------------

    @Test
    fun aCustomerWithNoSavedAddressesGetsAnEmptyList() = runTest {
        assertEquals(emptyList<SavedAddressDto>(), loadedAddresses("[]"))
    }

    // --- rule 5: nullable-by-design fields stay nullable ---------------------------

    /**
     * `state` is `nullable: true` and empty for CZ/SK/UA/RU/DE/PL; `latitude`/`longitude` are absent
     * until an address is geocoded, and a zeroed pair is the Atlantic rather than an unknown.
     */
    @Test
    fun anUngeocodedCzechAddressKeepsItsNulls() = runTest {
        val list = loadedAddresses(
            addressesWithFirstRow { ((it - "state") - "latitude") - "longitude" },
        )

        assertNull(list.first().state)
        assertNull(list.first().latitude)
        assertNull(list.first().longitude)
        assertEquals("a-1", list.first().id)
    }

    // --- payload plumbing ---------------------------------------------------------

    private fun addressesWithFirstRow(transform: (JsonObject) -> JsonObject): String {
        val rows = Json.parseToJsonElement(CAPTURED_ADDRESSES).jsonArray.mapIndexed { index, row ->
            if (index == 0) transform(row.jsonObject) else row
        }
        return JsonArray(rows).toString()
    }

    private operator fun JsonObject.minus(key: String) =
        JsonObject(toMutableMap().apply { remove(key) })

    private operator fun JsonObject.plus(entry: Pair<String, JsonElement>) =
        JsonObject(toMutableMap().apply { put(entry.first, entry.second) })

    @OptIn(ExperimentalSerializationApi::class)
    private fun serialNames(descriptor: SerialDescriptor): Set<String> =
        (0 until descriptor.elementsCount).map { descriptor.getElementName(it) }.toSet()

    private companion object {

        /** Every member non-zero and non-default, including the three nullable-by-design ones. */
        val CAPTURED_ADDRESSES = """
            [
              {
                "id": "a-1",
                "label": "Home",
                "street": "Korunni 12",
                "city": "Praha",
                "zipCode": "12000",
                "state": "Praha",
                "countryId": "c-cz",
                "country": "Czechia",
                "latitude": 50.0755,
                "longitude": 14.4378,
                "isDefault": true
              },
              {
                "id": "a-2",
                "label": "Office",
                "street": "Vinohradska 5",
                "city": "Praha",
                "zipCode": "13000",
                "state": "Praha",
                "countryId": "c-cz",
                "country": "Czechia",
                "latitude": 50.0781,
                "longitude": 14.4501,
                "isDefault": false
              }
            ]
        """.trimIndent()

        val ADDRESS_SPEC_PROPERTIES = setOf(
            "id",
            "label",
            "street",
            "city",
            "zipCode",
            "state",
            "countryId",
            "country",
            "latitude",
            "longitude",
            "isDefault",
        )

        val ADDRESS_REQUIRED_FIELDS = listOf("label", "street", "city", "zipCode", "countryId")
    }
}
