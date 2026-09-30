package cz.cleansia.partner.data.profile

import cz.cleansia.core.network.ApiResult
import cz.cleansia.partner.api.client.EmployeeApi
import cz.cleansia.partner.api.model.AcceptLegalDocumentCommand
import cz.cleansia.partner.api.model.CleanerLegalDocumentDto
import cz.cleansia.partner.api.model.LegalDocumentType
import kotlinx.coroutines.test.runTest
import kotlinx.serialization.ExperimentalSerializationApi
import kotlinx.serialization.descriptors.SerialDescriptor
import kotlinx.serialization.json.Json
import kotlinx.serialization.json.JsonArray
import kotlinx.serialization.json.JsonNull
import kotlinx.serialization.json.JsonObject
import kotlinx.serialization.json.jsonArray
import kotlinx.serialization.json.jsonObject
import kotlinx.serialization.json.jsonPrimitive
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
 * Outbound, the acceptance carries the text row the cleaner read — the generated command's member is
 * optional-with-null, so a dropped mapper line would send an acceptance of nothing. Inbound, the list
 * refuses a document whose text, id or acceptance flag is missing rather than asking for, or hiding,
 * an acceptance on a guess.
 */
class CleanerLegalDocumentWireTest {

    private val json = Json { ignoreUnknownKeys = true; isLenient = true; explicitNulls = false }

    private fun repo(server: MockWebServer) = ProfileRepositoryImpl(
        Retrofit.Builder()
            .baseUrl(server.url("/"))
            .addConverterFactory(json.asConverterFactory("application/json".toMediaType()))
            .build()
            .create(EmployeeApi::class.java),
        json,
    )

    private suspend fun <T> serving(
        body: String,
        onRequest: (RecordedRequest) -> Unit = {},
        call: suspend (ProfileRepositoryImpl) -> ApiResult<T>,
    ): ApiResult<T> {
        val server = MockWebServer()
        server.start()
        return try {
            server.enqueue(
                MockResponse()
                    .setResponseCode(200)
                    .setHeader("Content-Type", "application/json")
                    .setBody(body),
            )
            call(repo(server)).also { onRequest(server.takeRequest()) }
        } finally {
            server.shutdown()
        }
    }

    @Test
    fun documentDtoSerialNamesAreExactlyTheSpecProperties() {
        assertEquals(DOCUMENT_SPEC_PROPERTIES, serialNames(CleanerLegalDocumentDto.serializer().descriptor))
    }

    @Test
    fun acceptCommandSerialNamesAreExactlyTheSpecProperties() {
        assertEquals(setOf("acceptedTextId"), serialNames(AcceptLegalDocumentCommand.serializer().descriptor))
    }

    @Test
    fun theReadAsksInTheAppLanguageOnThePathTheServerBinds() = runTest {
        var path: String? = null
        serving(CAPTURED_DOCUMENTS, onRequest = { path = it.path }) { it.getLegalDocuments("uk") }

        assertEquals("/api/Employee/GetMyLegalDocuments?Language=uk", path)
    }

    @Test
    fun theAcceptanceCarriesTheTextIdTheCleanerRead() = runTest {
        var path: String? = null
        var sent: JsonObject? = null
        val result = serving(
            """{ "type": 3, "version": "2026-12-01" }""",
            onRequest = { request ->
                path = request.path
                sent = Json.parseToJsonElement(request.body.readUtf8()).jsonObject
            },
        ) { it.acceptLegalDocument("text-1") }

        assertTrue("expected the acceptance to succeed; got $result", result is ApiResult.Success)
        assertEquals("/api/Employee/AcceptLegalDocument", path)
        assertEquals("text-1", sent?.get("acceptedTextId")?.jsonPrimitive?.content)
    }

    @Test
    fun everyFieldArrivesWithItsLiteralValue() = runTest {
        val documents = loaded(CAPTURED_DOCUMENTS)

        assertEquals(2, documents.size)
        val contract = documents[0]
        assertEquals(LegalDocumentType._3, contract.type)
        assertEquals("text-1", contract.legalDocumentTextId)
        assertEquals("2026-12-01", contract.version)
        assertEquals("Rámcová smlouva", contract.title)
        assertEquals("<h2>Strany</h2><p>Smlouva.</p>", contract.contentHtml)
        assertEquals(false, contract.isAccepted)
        assertEquals("2026-10-01", contract.acceptedVersion)
        assertEquals("2026-10-02T08:30:00+00:00", contract.acceptedAt)

        val dpa = documents[1]
        assertEquals(LegalDocumentType._5, dpa.type)
        assertEquals(true, dpa.isAccepted)
    }

    @Test
    fun aCleanerWhoNeverAcceptedHasNoAcceptedVersionRatherThanAPlaceholder() = runTest {
        val documents = loaded(withFirst { it - "acceptedVersion" - "acceptedAt" })

        assertNull(documents[0].acceptedVersion)
        assertNull(documents[0].acceptedAt)
    }

    @Test
    fun aDocumentMissingWhatTheAcceptanceBindsFailsTheWholeList() = runTest {
        listOf("type", "legalDocumentTextId", "version", "title", "contentHtml", "isAccepted").forEach { field ->
            val result = serving(withFirst { it - field }) { it.getLegalDocuments("cs") }
            assertTrue("a missing $field must fail the list rather than drop or default; got $result", result is ApiResult.Error)
        }
        val nullFlag = serving(withFirst { it + ("isAccepted" to JsonNull) }) { it.getLegalDocuments("cs") }
        assertTrue("a null isAccepted must fail the list; got $nullFlag", nullFlag is ApiResult.Error)
    }

    @Test
    fun nothingInForceIsAnEmptyList() = runTest {
        assertEquals(emptyList<CleanerLegalDocument>(), loaded("[]"))
    }

    private suspend fun loaded(body: String): List<CleanerLegalDocument> {
        val result = serving(body) { it.getLegalDocuments("cs") }
        assertTrue("expected the captured payload to map; got $result", result is ApiResult.Success)
        return (result as ApiResult.Success).data
    }

    private fun withFirst(transform: (JsonObject) -> JsonObject): String {
        val rows = Json.parseToJsonElement(CAPTURED_DOCUMENTS).jsonArray
        return JsonArray(listOf(transform(rows[0].jsonObject)) + rows.drop(1)).toString()
    }

    private operator fun JsonObject.minus(key: String) =
        JsonObject(toMutableMap().apply { remove(key) })

    private operator fun JsonObject.plus(entry: Pair<String, JsonNull>) =
        JsonObject(toMutableMap().apply { put(entry.first, entry.second) })

    @OptIn(ExperimentalSerializationApi::class)
    private fun serialNames(descriptor: SerialDescriptor): Set<String> =
        (0 until descriptor.elementsCount).map { descriptor.getElementName(it) }.toSet()

    private companion object {

        val CAPTURED_DOCUMENTS = """
            [
              {
                "type": 3,
                "legalDocumentId": "doc-1",
                "legalDocumentTextId": "text-1",
                "version": "2026-12-01",
                "effectiveFrom": "2026-12-01",
                "language": "cs",
                "title": "Rámcová smlouva",
                "contentHtml": "<h2>Strany</h2><p>Smlouva.</p>",
                "contentHash": "hash-1",
                "isAccepted": false,
                "acceptedVersion": "2026-10-01",
                "acceptedAt": "2026-10-02T08:30:00+00:00"
              },
              {
                "type": 5,
                "legalDocumentId": "doc-2",
                "legalDocumentTextId": "text-2",
                "version": "2026-12-01",
                "effectiveFrom": "2026-12-01",
                "language": "cs",
                "title": "Smlouva o zpracování osobních údajů",
                "contentHtml": "<p>Zpracování.</p>",
                "contentHash": "hash-2",
                "isAccepted": true,
                "acceptedVersion": "2026-12-01",
                "acceptedAt": "2026-12-02T09:00:00+00:00"
              }
            ]
        """.trimIndent()

        val DOCUMENT_SPEC_PROPERTIES = setOf(
            "type",
            "legalDocumentId",
            "legalDocumentTextId",
            "version",
            "effectiveFrom",
            "language",
            "title",
            "contentHtml",
            "contentHash",
            "isAccepted",
            "acceptedVersion",
            "acceptedAt",
        )
    }
}
