package cz.cleansia.customer.core.orders

import cz.cleansia.core.network.WireContractViolation
import cz.cleansia.customer.api.client.OrderApi as GenOrderApi
import cz.cleansia.customer.api.model.WorkContractAcceptanceDetails as GenWorkContractAcceptanceDetails
import cz.cleansia.customer.api.model.WorkContractAcceptanceDto as GenWorkContractAcceptanceDto
import cz.cleansia.customer.api.model.WorkContractDto as GenWorkContractDto
import cz.cleansia.customer.api.model.WorkContractFacts as GenWorkContractFacts
import cz.cleansia.customer.core.network.IntEnumSerializersModule
import kotlinx.coroutines.test.runTest
import kotlinx.serialization.ExperimentalSerializationApi
import kotlinx.serialization.descriptors.SerialDescriptor
import kotlinx.serialization.json.Json
import kotlinx.serialization.json.JsonArray
import kotlinx.serialization.json.JsonElement
import kotlinx.serialization.json.JsonNull
import kotlinx.serialization.json.JsonObject
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

/**
 * The customer's read of an accepted contract for work, and the acceptance rows the order detail
 * carries. Both decode a captured payload over a socket through the generated DTOs into the
 * hand-written ones; the read's mapper refuses a missing price, window or text rather than showing
 * a contract of nothing as the one the cleaner agreed to.
 */
class WorkContractWireTest {

    private val json = Json {
        ignoreUnknownKeys = true
        isLenient = true
        explicitNulls = false
        serializersModule = IntEnumSerializersModule
    }

    private suspend fun <T> serving(
        body: String,
        onRequest: (RecordedRequest) -> Unit = {},
        call: suspend (OrderApi) -> T,
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
            val api = OrderApi(
                Retrofit.Builder()
                    .baseUrl(server.url("/"))
                    .addConverterFactory(json.asConverterFactory("application/json".toMediaType()))
                    .build()
                    .create(GenOrderApi::class.java),
            )
            call(api).also { onRequest(server.takeRequest()) }
        } finally {
            server.shutdown()
        }
    }

    private suspend fun read(body: String): WorkContractDto =
        serving(body) { it.getWorkContract("acceptance-1", "en") }.body()!!

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

    // --- the field-name contract ------------------------------------------------

    @Test
    fun contractDtoSerialNamesAreExactlyTheSpecProperties() {
        assertEquals(CONTRACT_SPEC_PROPERTIES, serialNames(GenWorkContractDto.serializer().descriptor))
        assertEquals(FACTS_SPEC_PROPERTIES, serialNames(GenWorkContractFacts.serializer().descriptor))
        assertEquals(ACCEPTANCE_DETAILS_SPEC_PROPERTIES, serialNames(GenWorkContractAcceptanceDetails.serializer().descriptor))
        assertEquals(ACCEPTANCE_ROW_SPEC_PROPERTIES, serialNames(GenWorkContractAcceptanceDto.serializer().descriptor))
    }

    @Test
    fun theReadKeepsThePathAndQueryTheServerBinds() = runTest {
        var path: String? = null
        serving(CAPTURED_READ, onRequest = { path = it.path }) { it.getWorkContract("acceptance-1", "en") }

        assertEquals("/api/Order/GetWorkContract?AcceptanceId=acceptance-1&Language=en", path)
    }

    // --- the read: every fact arrives literally --------------------------------

    @Test
    fun everyFactAndTheAcceptanceArriveWithTheirLiteralValues() = runTest {
        val contract = read(CAPTURED_READ)

        assertEquals("text-1", contract.legalDocumentTextId)
        assertEquals("2026-09-20", contract.version)
        assertEquals("en", contract.language)
        assertEquals("Contract for Work", contract.title)
        assertEquals("<h2>Formation</h2><p>The contract is formed.</p>", contract.contentHtml)
        assertEquals("CL-2026-0042", contract.facts.orderNumber)
        assertEquals("2026-08-12T09:00:00Z", contract.facts.cleaningDateTimeUtc)
        assertEquals(240, contract.facts.estimatedMinutes)
        assertEquals(1850.50, contract.facts.totalPrice, 0.0)
        assertEquals("CZK", contract.facts.currencyCode)
        assertEquals("Praha 4, 140 xx", contract.facts.locationApproximate)
        assertEquals(3, contract.facts.rooms)
        assertEquals(2, contract.facts.bathrooms)
        assertEquals(listOf("Standard clean"), contract.facts.services)
        assertEquals(listOf("Deep clean"), contract.facts.packages)
        assertEquals(listOf("inside-oven", "inside-fridge"), contract.facts.extraSlugs)
        assertEquals("2026-08-10T18:40:00Z", contract.acceptance?.acceptedOn)
        assertEquals("2026-09-20", contract.acceptance?.documentVersion)
        assertEquals("cs", contract.acceptance?.acceptedLanguage)
    }

    @Test
    fun aMissingPriceWindowOrScopeRefusesTheContractRatherThanBindingNothing() = runTest {
        listOf("totalPrice", "cleaningDateTimeUtc", "estimatedMinutes", "rooms", "bathrooms").forEach { field ->
            refuses(field) { read(readWithFacts { it - field }) }
        }
        refuses("totalPrice") { read(readWithFacts { it + ("totalPrice" to JsonNull) }) }
    }

    @Test
    fun aContractWithoutItsTextIdTextVersionOrFactsRefusesTheMapping() = runTest {
        listOf("legalDocumentTextId", "contentHtml", "version", "facts").forEach { field ->
            refuses(field) { read(readWith { it - field }) }
        }
    }

    @Test
    fun anAcceptanceWithoutItsInstantOrVersionRefusesTheMapping() = runTest {
        refuses("acceptedOn") { read(readWithAcceptance { it - "acceptedOn" }) }
        refuses("documentVersion") { read(readWithAcceptance { it - "documentVersion" }) }
    }

    @Test
    fun absentScopeListsAreEmptyAndTheLabelsStayNull() = runTest {
        val contract = read(
            readWithFacts { it - "services" - "packages" - "extraSlugs" - "orderNumber" - "currencyCode" - "locationApproximate" },
        )

        assertEquals(emptyList<String>(), contract.facts.services)
        assertEquals(emptyList<String>(), contract.facts.packages)
        assertEquals(emptyList<String>(), contract.facts.extraSlugs)
        assertNull(contract.facts.orderNumber)
        assertNull(contract.facts.currencyCode)
        assertNull(contract.facts.locationApproximate)
    }

    @Test
    fun aScopeLineWithNoNameIsDroppedRatherThanRenderedBlank() = runTest {
        val contract = read(
            readWithFacts { facts ->
                facts + ("services" to JsonArray(listOf(json.parseToJsonElement("""{ "id": "svc-9" }"""))))
            },
        )

        assertEquals(emptyList<String>(), contract.facts.services)
    }

    // --- the detail: the acceptance rows ---------------------------------------

    @Test
    fun theOrderDetailCarriesEachAcceptanceRowKeyedOnItsSeat() = runTest {
        val order = serving(CAPTURED_ORDER) { it.getById("o-1") }.body()!!

        assertEquals(
            listOf(
                WorkContractAcceptanceDto(
                    id = "acc-1",
                    orderEmployeeId = "a-1",
                    employeeId = "emp-1",
                    acceptedOn = "2026-08-10T18:40:00Z",
                    documentVersion = "2026-09-20",
                    language = "cs",
                ),
            ),
            order.workContractAcceptances,
        )
        assertEquals("a-1", order.assignedEmployees?.single()?.id)
    }

    @Test
    fun anOrderWithNoAcceptanceRowsReadsAsNoneRatherThanFailing() = runTest {
        val order = serving(readOrderWith { it - "workContractAcceptances" }) { it.getById("o-1") }.body()!!

        assertNull(order.workContractAcceptances)
    }

    // --- payload plumbing ---------------------------------------------------------

    private fun readWith(transform: (JsonObject) -> JsonObject): String =
        transform(Json.parseToJsonElement(CAPTURED_READ).jsonObject).toString()

    private fun readWithFacts(transform: (JsonObject) -> JsonObject): String =
        readWith { root -> root + ("facts" to transform(root.getValue("facts").jsonObject)) }

    private fun readWithAcceptance(transform: (JsonObject) -> JsonObject): String =
        readWith { root -> root + ("acceptance" to transform(root.getValue("acceptance").jsonObject)) }

    private fun readOrderWith(transform: (JsonObject) -> JsonObject): String =
        transform(Json.parseToJsonElement(CAPTURED_ORDER).jsonObject).toString()

    private operator fun JsonObject.minus(key: String) =
        JsonObject(toMutableMap().apply { remove(key) })

    private operator fun JsonObject.plus(entry: Pair<String, JsonElement>) =
        JsonObject(toMutableMap().apply { put(entry.first, entry.second) })

    @OptIn(ExperimentalSerializationApi::class)
    private fun serialNames(descriptor: SerialDescriptor): Set<String> =
        (0 until descriptor.elementsCount).map { descriptor.getElementName(it) }.toSet()

    private companion object {

        /** Every member non-zero and non-default, so a forgotten field cannot pass as a mapped one. */
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
                "services": [{ "id": "svc-1", "name": "Standard clean" }],
                "packages": [{ "id": "pkg-1", "name": "Deep clean" }],
                "extraSlugs": ["inside-oven", "inside-fridge"]
              },
              "acceptance": {
                "acceptedOn": "2026-08-10T18:40:00Z",
                "documentVersion": "2026-09-20",
                "acceptedLanguage": "cs",
                "orderEmployeeId": "a-1",
                "employeeId": "emp-1"
              }
            }
        """.trimIndent()

        /** The detail's money members are pinned by `OrderWireTest`; this payload carries the crew and its rows. */
        val CAPTURED_ORDER = """
            {
              "id": "o-1",
              "rooms": 3,
              "bathrooms": 2,
              "paymentType": { "type": "PaymentType", "name": "Card", "value": 2 },
              "paymentStatus": { "type": "PaymentStatus", "name": "Paid", "value": 2 },
              "totalPrice": 4380.00,
              "originalSubtotal": 3650.00,
              "appliedDiscountSource": 0,
              "estimatedTime": 240,
              "orderStatus": { "type": "OrderStatus", "name": "Confirmed", "value": 2 },
              "assignedEmployees": [
                { "id": "a-1", "employeeId": "emp-1", "fullName": "Jana", "phoneNumber": "+420700000000" }
              ],
              "workContractAcceptances": [
                {
                  "id": "acc-1",
                  "orderEmployeeId": "a-1",
                  "employeeId": "emp-1",
                  "acceptedOn": "2026-08-10T18:40:00Z",
                  "documentVersion": "2026-09-20",
                  "language": "cs"
                }
              ]
            }
        """.trimIndent()

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
