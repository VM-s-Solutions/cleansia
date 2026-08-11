package cz.cleansia.customer.core.notifications

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
import org.junit.Assert.assertNotNull
import org.junit.Assert.assertNull
import org.junit.Test
import retrofit2.Retrofit
import retrofit2.converter.kotlinx.serialization.asConverterFactory
import cz.cleansia.customer.api.client.NotificationPreferencesApi as GenNotificationPreferencesApi
import cz.cleansia.customer.api.model.NotificationPreferencesDto as GenNotificationPreferencesDto

/**
 * The one surface where a coerced value does not stay a display bug: `update` is a replace-all PUT of
 * all eleven toggles, so the next one the customer touches writes back whatever the client invented
 * for the other ten. `NotificationPreferencesDto` declares no `required` array, so every
 * `nullable: false` boolean arrives typed `Boolean?`.
 */
class NotificationPreferencesWireTest {

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
        call: suspend (NotificationPreferencesApi) -> T,
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
            val api = NotificationPreferencesApi(
                Retrofit.Builder()
                    .baseUrl(server.url("/"))
                    .addConverterFactory(json.asConverterFactory("application/json".toMediaType()))
                    .build()
                    .create(GenNotificationPreferencesApi::class.java),
            )
            call(api).also { onRequest(server.takeRequest()) }
        } finally {
            server.shutdown()
        }
    }

    private suspend fun preferences(body: String) = serving(body) { it.getMine() }.body()

    private suspend fun loadedPreferences(body: String): NotificationPreferencesPayload {
        val dto = preferences(body)
        assertNotNull("expected the captured payload to map", dto)
        return dto!!
    }

    // --- the field-name contract ------------------------------------------------

    @Test
    fun preferencesDtoSerialNamesAreExactlyTheSpecProperties() {
        assertEquals(SPEC_PROPERTIES, serialNames(GenNotificationPreferencesDto.serializer().descriptor))
    }

    @Test
    fun theRequestKeepsThePathTheServerBinds() = runTest {
        var path: String? = null
        serving(CAPTURED, onRequest = { path = it.path }) { it.getMine() }
        assertEquals("/api/NotificationPreferences/GetMine", path)
    }

    // --- rule 2: booleans follow the money rule ---------------------------------

    @Test
    fun everyToggleArrivesWithTheValueTheServerSent() = runTest {
        val dto = loadedPreferences(CAPTURED)

        assertEquals(false, dto.orderUpdates)
        assertEquals(false, dto.cleanerOnTheWay)
        assertEquals(false, dto.orderCompleted)
        assertEquals(false, dto.orderCancelled)
        assertEquals(false, dto.refundIssued)
        assertEquals(false, dto.membershipExpiring)
        assertEquals(false, dto.membershipCancelled)
        assertEquals(false, dto.tierUpgrade)
        assertEquals(true, dto.promo)
        assertEquals(false, dto.disputeReply)
        assertEquals(false, dto.recurringScheduled)
    }

    /**
     * Every toggle, not just `promo`: a defaulted `true` re-subscribes someone who opted out and a
     * defaulted `false` unsubscribes someone who opted in — and because the write is replace-all,
     * the invention is persisted the next time any switch is touched.
     */
    @Test
    fun aMissingToggleRefusesThePayloadRatherThanDecidingConsent() = runTest {
        SPEC_PROPERTIES.forEach { field ->
            assertNull(
                "a missing $field must refuse rather than be written back as the app's guess",
                preferences(withoutKey(CAPTURED, field)),
            )
        }
    }

    @Test
    fun theUpdateEchoIsRefusedOnTheSameTerms() = runTest {
        val sent = loadedPreferences(CAPTURED)

        assertNull(serving(withoutKey(CAPTURED, "promo")) { it.update(sent) }.body())
        assertEquals(sent, serving(CAPTURED) { it.update(sent) }.body())
    }

    // --- the refused body ---------------------------------------------------------

    /**
     * The backend lazy-creates the row on read, so "no preferences exist" is not a state this
     * endpoint has — a body-less success used to produce a full set of invented opt-ins.
     */
    @Test
    fun aBodylessSuccessIsRefusedRatherThanFabricatingAFullSetOfOptIns() = runTest {
        assertNull(serving("", code = 204) { it.getMine() }.body())
    }

    // --- payload plumbing ---------------------------------------------------------

    private fun withoutKey(body: String, key: String): String =
        (Json.parseToJsonElement(body).jsonObject - key).toString()

    private operator fun JsonObject.minus(key: String) =
        JsonObject(toMutableMap().apply { remove(key) })

    @OptIn(ExperimentalSerializationApi::class)
    private fun serialNames(descriptor: SerialDescriptor): Set<String> =
        (0 until descriptor.elementsCount).map { descriptor.getElementName(it) }.toSet()

    private companion object {

        /**
         * Every member the opposite of the default the mapper used to supply, so a forgotten field
         * cannot pass as a mapped one: the ten that defaulted `true` arrive `false`, and `promo`,
         * which defaulted `false`, arrives `true`.
         */
        val CAPTURED = """
            {
              "orderUpdates": false,
              "cleanerOnTheWay": false,
              "orderCompleted": false,
              "orderCancelled": false,
              "refundIssued": false,
              "membershipExpiring": false,
              "membershipCancelled": false,
              "tierUpgrade": false,
              "promo": true,
              "disputeReply": false,
              "recurringScheduled": false
            }
        """.trimIndent()

        val SPEC_PROPERTIES = setOf(
            "orderUpdates",
            "cleanerOnTheWay",
            "orderCompleted",
            "orderCancelled",
            "refundIssued",
            "membershipExpiring",
            "membershipCancelled",
            "tierUpgrade",
            "promo",
            "disputeReply",
            "recurringScheduled",
        )
    }
}
