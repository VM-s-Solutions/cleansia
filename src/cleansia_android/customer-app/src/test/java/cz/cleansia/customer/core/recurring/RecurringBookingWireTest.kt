package cz.cleansia.customer.core.recurring

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
import cz.cleansia.customer.api.client.RecurringBookingApi as GenRecurringBookingApi
import cz.cleansia.customer.api.model.RecurringBookingTemplateDto as GenTemplate

/**
 * A recurring template is a standing instruction to charge, and this list is the only screen that can
 * pause or delete one. `RecurringBookingTemplateDto` declares no `required` array, so the generator
 * types every property optional-with-null regardless of `nullable: false`.
 */
class RecurringBookingWireTest {

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
        call: suspend (RecurringBookingApi) -> T,
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
            val api = RecurringBookingApi(
                Retrofit.Builder()
                    .baseUrl(server.url("/"))
                    .addConverterFactory(json.asConverterFactory("application/json".toMediaType()))
                    .build()
                    .create(GenRecurringBookingApi::class.java),
            )
            call(api).also { onRequest(server.takeRequest()) }
        } finally {
            server.shutdown()
        }
    }

    private suspend fun templates(body: String) = serving(body) { it.getMine() }.body()

    private suspend fun loadedTemplates(body: String): List<RecurringBookingTemplateDto> {
        val dto = templates(body)
        assertNotNull("expected the captured payload to map", dto)
        return dto!!
    }

    // --- the field-name contract ------------------------------------------------

    @Test
    fun templateDtoSerialNamesAreExactlyTheSpecProperties() {
        assertEquals(TEMPLATE_SPEC_PROPERTIES, serialNames(GenTemplate.serializer().descriptor))
    }

    @Test
    fun theRequestKeepsThePathTheServerBinds() = runTest {
        var path: String? = null
        serving(CAPTURED_TEMPLATES, onRequest = { path = it.path }) { it.getMine() }
        assertEquals("/api/RecurringBooking/GetMine", path)
    }

    // --- rule 1: money and quantities are never coerced --------------------------

    @Test
    fun everyScopeFigureArrivesWithItsLiteralValue() = runTest {
        val list = loadedTemplates(CAPTURED_TEMPLATES)

        assertEquals(4, list.first().rooms)
        assertEquals(2, list.first().bathrooms)
        assertEquals(2, list.first().frequency)
        assertEquals(3, list.first().dayOfWeek)
        assertEquals(2, list.first().paymentType)
    }

    /**
     * `CreateRecurringViewModel` copies these into the edit form, so a coerced zero is not merely
     * displayed — the next Update writes it back and the client's invention becomes the record.
     */
    @Test
    fun aMissingRoomCountRefusesTheListRatherThanBeingWrittenBackAsZero() = runTest {
        listOf("rooms", "bathrooms").forEach { field ->
            assertNull(
                "a missing $field must refuse the list rather than round-trip as zero",
                templates(templatesWithFirstRow { it - field }),
            )
        }
    }

    @Test
    fun aMissingScheduleFieldRefusesTheList() = runTest {
        TEMPLATE_REQUIRED_FIELDS.forEach { field ->
            assertNull(
                "a missing $field must refuse the list",
                templates(templatesWithFirstRow { it - field }),
            )
        }
    }

    // --- rule 2: booleans follow the money rule ---------------------------------

    @Test
    fun thePausedFlagArrivesWithTheValueTheServerSent() = runTest {
        val list = loadedTemplates(CAPTURED_TEMPLATES)

        assertEquals(true, list.first().isActive)
        assertEquals(false, list.last().isActive)
    }

    @Test
    fun aMissingActiveFlagRefusesTheList() = runTest {
        assertNull(templates(templatesWithFirstRow { it - "isActive" }))
    }

    // --- rule 3: identity is refused, never synthesized --------------------------

    /**
     * Refuses the list rather than dropping the row: a silently absent template keeps materialising
     * orders while the only screen that can stop it says it does not exist.
     */
    @Test
    fun anIdLessTemplateRefusesTheListRatherThanHidingAStandingCharge() = runTest {
        assertNull(templates(templatesWithFirstRow { it - "id" }))
    }

    // --- rule 4: collections do default -----------------------------------------

    @Test
    fun aTemplateWithNoSelectedServicesStillLoads() = runTest {
        val list = loadedTemplates(
            templatesWithFirstRow { (it - "selectedServiceIds") - "selectedPackageIds" },
        )

        assertEquals(emptyList<String>(), list.first().selectedServiceIds)
        assertEquals(emptyList<String>(), list.first().selectedPackageIds)
        assertEquals(4, list.first().rooms)
    }

    @Test
    fun aCustomerWithNoTemplatesGetsAnEmptyList() = runTest {
        assertEquals(emptyList<RecurringBookingTemplateDto>(), loadedTemplates("[]"))
    }

    // --- rule 5: nullable-by-design fields stay nullable ---------------------------

    /**
     * `endsOn`, `lastMaterializedFor` and `addressLine` are `nullable: true`: an open-ended schedule
     * has no end date and a brand-new one has never materialised.
     */
    @Test
    fun anOpenEndedTemplateKeepsItsNulls() = runTest {
        val list = loadedTemplates(
            templatesWithFirstRow { ((it - "endsOn") - "lastMaterializedFor") - "addressLine" },
        )

        assertNull(list.first().endsOn)
        assertNull(list.first().lastMaterializedFor)
        assertNull(list.first().addressLine)
        assertEquals("t-1", list.first().id)
    }

    // --- payload plumbing ---------------------------------------------------------

    private fun templatesWithFirstRow(transform: (JsonObject) -> JsonObject): String {
        val rows = Json.parseToJsonElement(CAPTURED_TEMPLATES).jsonArray.mapIndexed { index, row ->
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

        /** Every member non-zero and non-default, including all four nullable-by-design fields. */
        val CAPTURED_TEMPLATES = """
            [
              {
                "id": "t-1",
                "frequency": 2,
                "dayOfWeek": 3,
                "timeOfDay": "09:30",
                "rooms": 4,
                "bathrooms": 2,
                "savedAddressId": "a-1",
                "addressLine": "Korunni 12, Praha",
                "selectedServiceIds": ["s-1", "s-2"],
                "selectedPackageIds": ["p-1"],
                "paymentType": 2,
                "startsOn": "2026-08-03T09:30:00Z",
                "endsOn": "2026-12-21T09:30:00Z",
                "lastMaterializedFor": "2026-08-17T09:30:00Z",
                "isActive": true,
                "preferredEmployeeId": "e-9"
              },
              {
                "id": "t-2",
                "frequency": 1,
                "dayOfWeek": 5,
                "timeOfDay": "14:00",
                "rooms": 2,
                "bathrooms": 1,
                "savedAddressId": "a-2",
                "addressLine": "Vinohradska 5, Praha",
                "selectedServiceIds": ["s-3"],
                "selectedPackageIds": ["p-2"],
                "paymentType": 1,
                "startsOn": "2026-08-07T14:00:00Z",
                "endsOn": "2027-01-08T14:00:00Z",
                "lastMaterializedFor": "2026-08-14T14:00:00Z",
                "isActive": false,
                "preferredEmployeeId": "e-4"
              }
            ]
        """.trimIndent()

        val TEMPLATE_SPEC_PROPERTIES = setOf(
            "id",
            "frequency",
            "dayOfWeek",
            "timeOfDay",
            "rooms",
            "bathrooms",
            "savedAddressId",
            "addressLine",
            "selectedServiceIds",
            "selectedPackageIds",
            "paymentType",
            "startsOn",
            "endsOn",
            "lastMaterializedFor",
            "isActive",
            "preferredEmployeeId",
        )

        val TEMPLATE_REQUIRED_FIELDS =
            listOf("frequency", "dayOfWeek", "timeOfDay", "savedAddressId", "paymentType", "startsOn")
    }
}
