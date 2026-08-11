package cz.cleansia.customer.core.consent

import cz.cleansia.core.consent.SignupConsentType
import cz.cleansia.customer.core.network.IntEnumSerializersModule
import kotlinx.coroutines.test.runTest
import kotlinx.serialization.ExperimentalSerializationApi
import kotlinx.serialization.descriptors.SerialDescriptor
import kotlinx.serialization.json.Json
import kotlinx.serialization.json.JsonArray
import kotlinx.serialization.json.JsonElement
import kotlinx.serialization.json.JsonNull
import kotlinx.serialization.json.JsonObject
import kotlinx.serialization.json.JsonPrimitive
import kotlinx.serialization.json.jsonArray
import kotlinx.serialization.json.jsonObject
import okhttp3.MediaType.Companion.toMediaType
import okhttp3.mockwebserver.MockResponse
import okhttp3.mockwebserver.MockWebServer
import okhttp3.mockwebserver.RecordedRequest
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Test
import retrofit2.Retrofit
import retrofit2.converter.kotlinx.serialization.asConverterFactory
import cz.cleansia.customer.api.client.GdprApi as GenGdprApi
import cz.cleansia.customer.api.model.UserConsentDto as GenUserConsentDto

/**
 * `answeredTypes` decides which signup consents to ask for again. A row lost on the way in reads as
 * "never answered", so the app re-asks for a consent the user has since withdrawn and writes it back
 * — which makes a dropped row here a GDPR record the user did not make. `UserConsentDto.ConsentType`
 * is non-nullable in C#; the spec carries it as a bare `$ref`, whose silence means nothing.
 */
class GdprConsentWireTest {

    private val json = Json {
        ignoreUnknownKeys = true
        isLenient = true
        explicitNulls = false
        serializersModule = IntEnumSerializersModule
    }

    private suspend fun answered(
        body: String,
        onRequest: (RecordedRequest) -> Unit = {},
    ): Set<SignupConsentType>? {
        val server = MockWebServer()
        server.start()
        return try {
            server.enqueue(
                MockResponse()
                    .setResponseCode(200)
                    .setHeader("Content-Type", "application/json")
                    .setBody(body),
            )
            val client = GdprConsentClient(
                Retrofit.Builder()
                    .baseUrl(server.url("/"))
                    .addConverterFactory(json.asConverterFactory("application/json".toMediaType()))
                    .build()
                    .create(GenGdprApi::class.java),
                json,
            )
            client.answeredTypes().also { onRequest(server.takeRequest()) }
        } finally {
            server.shutdown()
        }
    }

    // --- the field-name contract ------------------------------------------------

    @Test
    fun consentDtoSerialNamesAreExactlyTheSpecProperties() {
        assertEquals(SPEC_PROPERTIES, serialNames(GenUserConsentDto.serializer().descriptor))
    }

    @Test
    fun theRequestKeepsThePathTheServerBinds() = runTest {
        var path: String? = null
        answered(CAPTURED_CONSENTS) { path = it.path }

        assertEquals("/api/v1/Gdpr/consents", path)
    }

    // --- rule 3: identity is refused, never synthesized --------------------------

    @Test
    fun everyAnsweredTypeArrivesWithTheValueTheWireCarried() = runTest {
        assertEquals(
            setOf(SignupConsentType.TermsOfService, SignupConsentType.PrivacyPolicy),
            answered(CAPTURED_CONSENTS),
        )
    }

    /**
     * A withdrawn row is still an answer — the whole reason the read is not filtered by `isGranted`.
     */
    @Test
    fun aWithdrawnConsentStillCountsAsAnswered() = runTest {
        val types = answered(consentsWithFirstRow { it + ("isGranted" to JsonPrimitive(false)) })

        assertEquals(setOf(SignupConsentType.TermsOfService, SignupConsentType.PrivacyPolicy), types)
    }

    @Test
    fun aRowWithoutItsTypeRefusesTheAnswerRatherThanReadingAsNeverAsked() = runTest {
        assertNull(answered(consentsWithFirstRow { it - "consentType" }))
    }

    @Test
    fun aRowWithAnExplicitNullTypeRefusesTheAnswer() = runTest {
        assertNull(answered(consentsWithFirstRow { it + ("consentType" to JsonNull) }))
    }

    /**
     * An int outside the enum is the same fact one layer down, and `IntValueEnumSerializer` refuses
     * it rather than picking an entry — a consent the app cannot name is not a consent it may treat
     * as unasked.
     */
    @Test
    fun aTypeOutsideTheEnumRefusesTheAnswerRatherThanBeingSkipped() = runTest {
        assertNull(answered(consentsWithFirstRow { it + ("consentType" to JsonPrimitive(99)) }))
    }

    // --- rule 4: collections do default -----------------------------------------

    @Test
    fun anAccountThatHasAnsweredNothingIsAnEmptySetRatherThanARefusal() = runTest {
        assertEquals(emptySet<SignupConsentType>(), answered("[]"))
    }

    // --- payload plumbing ---------------------------------------------------------

    private fun consentsWithFirstRow(transform: (JsonObject) -> JsonObject): String {
        val rows = Json.parseToJsonElement(CAPTURED_CONSENTS).jsonArray.mapIndexed { index, row ->
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

        /** Every member non-default, including the two dates a still-granted row would leave null. */
        val CAPTURED_CONSENTS = """
            [
              {
                "id": "con-1",
                "consentType": 0,
                "isGranted": true,
                "grantedAt": "2026-06-02T10:00:00Z",
                "withdrawnAt": "2026-07-02T10:00:00Z",
                "createdOn": "2026-06-02T10:00:00Z"
              },
              {
                "id": "con-2",
                "consentType": 1,
                "isGranted": true,
                "grantedAt": "2026-06-02T10:00:01Z",
                "withdrawnAt": "2026-07-02T10:00:01Z",
                "createdOn": "2026-06-02T10:00:01Z"
              }
            ]
        """.trimIndent()

        val SPEC_PROPERTIES = setOf(
            "id",
            "consentType",
            "isGranted",
            "grantedAt",
            "withdrawnAt",
            "createdOn",
        )
    }
}
