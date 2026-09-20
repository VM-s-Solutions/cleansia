package cz.cleansia.partner.data.orders

import cz.cleansia.core.network.ApiResult
import cz.cleansia.partner.api.client.OrderApi
import cz.cleansia.partner.api.model.AcceptWorkContractCommand
import cz.cleansia.partner.api.model.TakeOrderCommand
import cz.cleansia.partner.api.model.WorkContractAcceptanceDetails
import cz.cleansia.partner.api.model.WorkContractAcceptanceDto
import cz.cleansia.partner.api.model.WorkContractDto
import cz.cleansia.partner.api.model.WorkContractFacts
import kotlinx.coroutines.test.runTest
import kotlinx.serialization.ExperimentalSerializationApi
import kotlinx.serialization.descriptors.SerialDescriptor
import kotlinx.serialization.json.Json
import kotlinx.serialization.json.JsonElement
import kotlinx.serialization.json.JsonNull
import kotlinx.serialization.json.JsonObject
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
 * Two contracts on one wire. Outbound: the take and the standalone acceptance each carry the text
 * row the cleaner was shown — every generated command member is optional-with-null, so a dropped
 * mapper line sends a take with no text and the server refuses `contract.not_accepted` with nothing
 * going red here. Inbound: the preview and the read decode through the generated DTO into the
 * mapper, which refuses a missing price, window or text rather than rendering a contract of nothing.
 */
class WorkContractWireTest {

    private val json = Json { ignoreUnknownKeys = true; isLenient = true; explicitNulls = false }

    private fun repo(server: MockWebServer) = OrdersRepositoryImpl(
        Retrofit.Builder()
            .baseUrl(server.url("/"))
            .addConverterFactory(json.asConverterFactory("application/json".toMediaType()))
            .build()
            .create(OrderApi::class.java),
        json,
    )

    private suspend fun <T> exchange(
        body: String,
        call: suspend (OrdersRepositoryImpl) -> ApiResult<T>,
        onRequest: (RecordedRequest) -> Unit = {},
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

    // --- outbound: the echo -------------------------------------------------------

    @Test
    fun takeCommandSerialNamesAreExactlyTheSpecProperties() {
        assertEquals(setOf("orderId", "acceptedWorkContractTextId"), serialNames(TakeOrderCommand.serializer().descriptor))
    }

    @Test
    fun acceptCommandSerialNamesAreExactlyTheSpecProperties() {
        assertEquals(setOf("orderId", "acceptedWorkContractTextId"), serialNames(AcceptWorkContractCommand.serializer().descriptor))
    }

    @Test
    fun theTakeCarriesTheAcceptedTextIdOnTheWire() = runTest {
        var sent: JsonObject? = null
        var path: String? = null
        exchange(TAKE_RESPONSE, { it.takeOrder("order-1", "text-1") }) { request ->
            path = request.path
            sent = Json.parseToJsonElement(request.body.readUtf8()).jsonObject
        }

        assertEquals("/api/Order/TakeOrder", path)
        assertEquals("order-1", sent?.get("orderId")?.jsonPrimitive?.content)
        assertEquals("text-1", sent?.get("acceptedWorkContractTextId")?.jsonPrimitive?.content)
    }

    @Test
    fun theStandaloneAcceptanceCarriesTheAcceptedTextIdOnTheWire() = runTest {
        var sent: JsonObject? = null
        var path: String? = null
        exchange(ACCEPT_RESPONSE, { it.acceptWorkContract("order-1", "text-1") }) { request ->
            path = request.path
            sent = Json.parseToJsonElement(request.body.readUtf8()).jsonObject
        }

        assertEquals("/api/Order/AcceptWorkContract", path)
        assertEquals("order-1", sent?.get("orderId")?.jsonPrimitive?.content)
        assertEquals("text-1", sent?.get("acceptedWorkContractTextId")?.jsonPrimitive?.content)
    }

    // --- inbound: the field-name contract -----------------------------------------

    @Test
    fun contractDtoSerialNamesAreExactlyTheSpecProperties() {
        assertEquals(CONTRACT_SPEC_PROPERTIES, serialNames(WorkContractDto.serializer().descriptor))
        assertEquals(FACTS_SPEC_PROPERTIES, serialNames(WorkContractFacts.serializer().descriptor))
        assertEquals(ACCEPTANCE_DETAILS_SPEC_PROPERTIES, serialNames(WorkContractAcceptanceDetails.serializer().descriptor))
        assertEquals(ACCEPTANCE_ROW_SPEC_PROPERTIES, serialNames(WorkContractAcceptanceDto.serializer().descriptor))
    }

    @Test
    fun thePreviewAndTheReadKeepThePathsAndQueriesTheServerBinds() = runTest {
        var previewPath: String? = null
        exchange(CAPTURED_PREVIEW, { it.getWorkContractPreview("order-1", "cs") }) { previewPath = it.path }
        var readPath: String? = null
        exchange(CAPTURED_READ, { it.getWorkContract("acceptance-1", "en") }) { readPath = it.path }

        assertEquals("/api/Order/GetWorkContractPreview?OrderId=order-1&Language=cs", previewPath)
        assertEquals("/api/Order/GetWorkContract?AcceptanceId=acceptance-1&Language=en", readPath)
    }

    // --- rule 1: money and quantities are never coerced ---------------------------

    @Test
    fun everyFactArrivesWithItsLiteralValue() = runTest {
        val contract = loaded(CAPTURED_PREVIEW)

        assertEquals("text-1", contract.legalDocumentTextId)
        assertEquals("2026-09-20", contract.version)
        assertEquals("cs", contract.language)
        assertEquals("Smlouva o dílo", contract.title)
        assertEquals("<h2>Vznik</h2><p>Smlouva vzniká.</p>", contract.contentHtml)
        assertEquals(1850.50, contract.facts.totalPrice, 0.0)
        assertEquals(240, contract.facts.estimatedMinutes)
        assertEquals(3, contract.facts.rooms)
        assertEquals(2, contract.facts.bathrooms)
        assertEquals("2026-08-12T09:00:00Z", contract.facts.cleaningDateTimeUtc)
        assertEquals("CL-2026-0042", contract.facts.orderNumber)
        assertEquals("CZK", contract.facts.currencyCode)
        assertEquals("Praha 4, 140 xx", contract.facts.locationApproximate)
        assertEquals(listOf("Standardní úklid"), contract.facts.services)
        assertEquals(listOf("Balíček Plus"), contract.facts.packages)
        assertEquals(listOf("inside-oven", "inside-fridge"), contract.facts.extraSlugs)
        assertNull(contract.acceptance)
    }

    @Test
    fun aMissingPriceFailsTheMappingRatherThanBindingAContractForNothing() = runTest {
        assertMappingFails("totalPrice", previewWithFacts { it - "totalPrice" })
        assertMappingFails("totalPrice", previewWithFacts { it + ("totalPrice" to JsonNull) })
    }

    @Test
    fun aMissingWindowOrScopeFailsTheMapping() = runTest {
        listOf("cleaningDateTimeUtc", "estimatedMinutes", "rooms", "bathrooms").forEach { field ->
            assertMappingFails(field, previewWithFacts { it - field })
        }
    }

    // --- rule 3: identity is refused, never synthesized ---------------------------

    @Test
    fun aContractWithoutItsTextIdOrTextFailsTheMapping() = runTest {
        assertMappingFails("legalDocumentTextId", previewWith { it - "legalDocumentTextId" })
        assertMappingFails("contentHtml", previewWith { it - "contentHtml" })
        assertMappingFails("version", previewWith { it - "version" })
        assertMappingFails("facts", previewWith { it - "facts" })
    }

    // --- rule 4: collections default ---------------------------------------------

    @Test
    fun absentScopeListsAreEmptyRatherThanAnError() = runTest {
        val contract = loaded(previewWithFacts { it - "services" - "packages" - "extraSlugs" })

        assertEquals(emptyList<String>(), contract.facts.services)
        assertEquals(emptyList<String>(), contract.facts.packages)
        assertEquals(emptyList<String>(), contract.facts.extraSlugs)
    }

    // --- rule 5: nullable-by-design stays nullable ---------------------------------

    @Test
    fun theLabelsStayNullRatherThanBecomingPlaceholders() = runTest {
        val contract = loaded(previewWithFacts { it - "orderNumber" - "currencyCode" - "locationApproximate" })

        assertNull(contract.facts.orderNumber)
        assertNull(contract.facts.currencyCode)
        assertNull(contract.facts.locationApproximate)
        assertEquals(1850.50, contract.facts.totalPrice, 0.0)
    }

    // --- the read -----------------------------------------------------------------

    @Test
    fun theReadCarriesTheStoredAcceptance() = runTest {
        val result = exchange(CAPTURED_READ, { it.getWorkContract("acceptance-1", "en") })
        val contract = (result as ApiResult.Success).data

        assertEquals("2026-08-10T18:40:00Z", contract.acceptance?.acceptedOn)
        assertEquals("2026-09-20", contract.acceptance?.documentVersion)
        assertEquals("cs", contract.acceptance?.acceptedLanguage)
        assertEquals("en", contract.language)
    }

    @Test
    fun anAcceptanceWithoutItsInstantFailsTheMapping() = runTest {
        val body = Json.parseToJsonElement(CAPTURED_READ).jsonObject.let { root ->
            root + ("acceptance" to (root.getValue("acceptance").jsonObject - "acceptedOn"))
        }.toString()
        val result = exchange(body, { it.getWorkContract("acceptance-1", "en") })

        assertTrue("a read with no acceptance instant must fail; got $result", result is ApiResult.Error)
    }

    // --- payload plumbing ---------------------------------------------------------

    private suspend fun loaded(body: String): WorkContract {
        val result = exchange(body, { it.getWorkContractPreview("order-1", "cs") })
        assertTrue("expected the captured payload to map; got $result", result is ApiResult.Success)
        return (result as ApiResult.Success).data
    }

    private suspend fun assertMappingFails(field: String, body: String) {
        val result = exchange(body, { it.getWorkContractPreview("order-1", "cs") })
        assertTrue(
            "a missing $field must fail the mapping rather than read as a default; got $result",
            result is ApiResult.Error,
        )
    }

    private fun previewWith(transform: (JsonObject) -> JsonObject): String =
        transform(Json.parseToJsonElement(CAPTURED_PREVIEW).jsonObject).toString()

    private fun previewWithFacts(transform: (JsonObject) -> JsonObject): String =
        previewWith { root -> root + ("facts" to transform(root.getValue("facts").jsonObject)) }

    private operator fun JsonObject.minus(key: String) =
        JsonObject(toMutableMap().apply { remove(key) })

    private operator fun JsonObject.plus(entry: Pair<String, JsonElement>) =
        JsonObject(toMutableMap().apply { put(entry.first, entry.second) })

    @OptIn(ExperimentalSerializationApi::class)
    private fun serialNames(descriptor: SerialDescriptor): Set<String> =
        (0 until descriptor.elementsCount).map { descriptor.getElementName(it) }.toSet()

    private companion object {

        /** Every member non-zero and non-default, so a forgotten field cannot pass as a mapped one. */
        val CAPTURED_PREVIEW = """
            {
              "legalDocumentTextId": "text-1",
              "legalDocumentId": "doc-1",
              "version": "2026-09-20",
              "effectiveFrom": "2026-09-20",
              "language": "cs",
              "title": "Smlouva o dílo",
              "contentHtml": "<h2>Vznik</h2><p>Smlouva vzniká.</p>",
              "facts": {
                "orderNumber": "CL-2026-0042",
                "cleaningDateTimeUtc": "2026-08-12T09:00:00Z",
                "estimatedMinutes": 240,
                "totalPrice": 1850.50,
                "currencyCode": "CZK",
                "locationApproximate": "Praha 4, 140 xx",
                "countryId": "CZ",
                "rooms": 3,
                "bathrooms": 2,
                "services": [{ "id": "svc-1", "name": "Standardní úklid" }],
                "packages": [{ "id": "pkg-1", "name": "Balíček Plus" }],
                "extraSlugs": ["inside-oven", "inside-fridge"]
              },
              "acceptance": null
            }
        """.trimIndent()

        val CAPTURED_READ = """
            {
              "legalDocumentTextId": "text-1",
              "legalDocumentId": "doc-1",
              "version": "2026-09-20",
              "effectiveFrom": "2026-09-20",
              "language": "en",
              "title": "Contract for Work",
              "contentHtml": "<h2>Formation</h2><p>The contract is formed.</p>",
              "facts": {
                "orderNumber": "CL-2026-0042",
                "cleaningDateTimeUtc": "2026-08-12T09:00:00Z",
                "estimatedMinutes": 240,
                "totalPrice": 1850.50,
                "currencyCode": "CZK",
                "locationApproximate": "Praha 4, 140 xx",
                "countryId": "CZ",
                "rooms": 3,
                "bathrooms": 2,
                "services": [],
                "packages": [],
                "extraSlugs": []
              },
              "acceptance": {
                "acceptedOn": "2026-08-10T18:40:00Z",
                "documentVersion": "2026-09-20",
                "acceptedLanguage": "cs",
                "orderEmployeeId": "seat-1",
                "employeeId": "employee-1"
              }
            }
        """.trimIndent()

        val TAKE_RESPONSE = """{ "orderId": "order-1", "employeeId": "employee-1" }"""
        val ACCEPT_RESPONSE =
            """{ "orderId": "order-1", "acceptanceId": "acceptance-1", "acceptedOn": "2026-08-10T18:40:00Z", "documentVersion": "2026-09-20" }"""

        val CONTRACT_SPEC_PROPERTIES = setOf(
            "legalDocumentTextId",
            "legalDocumentId",
            "version",
            "effectiveFrom",
            "language",
            "title",
            "contentHtml",
            "facts",
            "acceptance",
        )

        val FACTS_SPEC_PROPERTIES = setOf(
            "orderNumber",
            "cleaningDateTimeUtc",
            "estimatedMinutes",
            "totalPrice",
            "currencyCode",
            "locationApproximate",
            "countryId",
            "rooms",
            "bathrooms",
            "services",
            "packages",
            "extraSlugs",
        )

        val ACCEPTANCE_DETAILS_SPEC_PROPERTIES = setOf(
            "acceptedOn",
            "documentVersion",
            "acceptedLanguage",
            "orderEmployeeId",
            "employeeId",
        )

        val ACCEPTANCE_ROW_SPEC_PROPERTIES = setOf(
            "id",
            "orderEmployeeId",
            "employeeId",
            "acceptedOn",
            "documentVersion",
            "language",
        )
    }
}
