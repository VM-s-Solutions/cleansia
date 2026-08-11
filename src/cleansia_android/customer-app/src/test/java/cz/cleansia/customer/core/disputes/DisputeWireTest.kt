package cz.cleansia.customer.core.disputes

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
import cz.cleansia.core.network.WireContractViolation
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Test
import retrofit2.Retrofit
import retrofit2.converter.kotlinx.serialization.asConverterFactory
import cz.cleansia.customer.api.client.DisputeApi as GenDisputeApi
import cz.cleansia.customer.api.model.DisputeDetails as GenDisputeDetails
import cz.cleansia.customer.api.model.DisputeListItem as GenDisputeListItem
import cz.cleansia.customer.api.model.DisputeMessageDto as GenDisputeMessageDto

/**
 * A dispute is the customer asking for money back, and the thread is where they are told whether it
 * is coming. No dispute schema declares a `required` array, so the generator types every property
 * optional-with-null regardless of `nullable: false`.
 */
class DisputeWireTest {

    private val json = Json {
        ignoreUnknownKeys = true
        isLenient = true
        explicitNulls = false
        serializersModule = IntEnumSerializersModule
    }

    /**
     * The mapper is total now, so a refusal arrives as a throw carrying the field name rather than
     * as a null body — asserting the name is the point of the idiom.
     */
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

    private suspend fun <T> serving(
        body: String,
        code: Int = 200,
        onRequest: (RecordedRequest) -> Unit = {},
        call: suspend (DisputeApi) -> T,
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
            val api = DisputeApi(
                Retrofit.Builder()
                    .baseUrl(server.url("/"))
                    .addConverterFactory(json.asConverterFactory("application/json".toMediaType()))
                    .build()
                    .create(GenDisputeApi::class.java),
            )
            call(api).also { onRequest(server.takeRequest()) }
        } finally {
            server.shutdown()
        }
    }

    private suspend fun page(body: String) = serving(body) { it.getPaged(0, 20) }.body()

    private suspend fun detail(body: String) = serving(body) { it.getById("d-1") }.body()

    private suspend fun loadedPage(body: String): DisputeListResponseDto {
        val dto = page(body)
        assertNotNull("expected the captured payload to map", dto)
        return dto!!
    }

    // --- the field-name contract ------------------------------------------------

    @Test
    fun disputeDtoSerialNamesAreExactlyTheSpecProperties() {
        assertEquals(LIST_ITEM_SPEC_PROPERTIES, serialNames(GenDisputeListItem.serializer().descriptor))
        assertEquals(DETAILS_SPEC_PROPERTIES, serialNames(GenDisputeDetails.serializer().descriptor))
        assertEquals(MESSAGE_SPEC_PROPERTIES, serialNames(GenDisputeMessageDto.serializer().descriptor))
    }

    @Test
    fun theRequestsKeepThePathsTheServerBinds() = runTest {
        var path: String? = null
        serving(CAPTURED_PAGE, onRequest = { path = it.path }) { it.getPaged(0, 20) }
        assertEquals("/api/Dispute/GetPaged?Offset=0&Limit=20", path)

        serving(CAPTURED_DETAIL, onRequest = { path = it.path }) { it.getById("d-1") }
        assertEquals("/api/Dispute/GetById/d-1", path)
    }

    // --- rule 1: money and quantities are never coerced --------------------------

    @Test
    fun everyPagedCounterArrivesWithItsLiteralValue() = runTest {
        val dto = loadedPage(CAPTURED_PAGE)

        assertEquals(1, dto.pageNumber)
        assertEquals(20, dto.pageSize)
        assertEquals(37, dto.total)
        assertEquals(2, dto.data.size)
    }

    /**
     * `total` is the stop condition `loadNextPage` pages against as well as the count the list screen
     * renders, so a defaulted zero ends pagination while claiming there is nothing to page through.
     */
    @Test
    fun aMissingPagedCounterRefusesThePageRatherThanEndingPagination() = runTest {
        listOf("pageNumber", "pageSize", "total").forEach { field ->
            refuses(field) { page(withoutKey(CAPTURED_PAGE, field)) }
        }
    }

    @Test
    fun aResolvedDisputeKeepsItsRefundAmount() = runTest {
        assertEquals(1450.0, loadedPage(CAPTURED_PAGE).data.first().refundAmount)
    }

    // --- rule 2: booleans follow the money rule ---------------------------------

    @Test
    fun theThreadKeepsTheAuthorTheServerSent() = runTest {
        val messages = detail(CAPTURED_DETAIL)?.messages

        assertEquals(false, messages?.first()?.isStaffMessage)
        assertEquals(true, messages?.last()?.isStaffMessage)
    }

    /**
     * `false` draws the message as the customer's own, so the reply telling them their money is on
     * the way arrives looking like something they wrote themselves.
     */
    @Test
    fun anUnattributableMessageRefusesTheDisputeRatherThanBecomingTheCustomers() = runTest {
        refuses("isStaffMessage") { detail(detailWithLastMessage { it - "isStaffMessage" }) }
    }

    // --- rule 3: identity is refused, never synthesized --------------------------

    /**
     * Drops the unidentifiable row rather than refusing the page — nothing sums this list, `total` is
     * the server's own count, and the card navigates by id — while a surviving row whose own status
     * code is broken refuses, and because the row is an element of the page that refuses the page.
     */
    @Test
    fun anIdLessDisputeIsDroppedAndTheRestOfThePageSurvives() = runTest {
        val dto = loadedPage(pageWithFirstRow { it - "id" })

        assertEquals(1, dto.data.size)
        assertEquals("d-2", dto.data.first().id)
        assertEquals(37, dto.total)
    }

    @Test
    fun aBrokenStatusCodeRefusesThePageRatherThanReadingAsNotStarted() = runTest {
        refuses("status") { page(pageWithFirstRow { it.withCodeMissingValue("status") }) }
        refuses("reason") { page(pageWithFirstRow { it.withCodeMissingValue("reason") }) }
    }

    @Test
    fun aMissingStatusObjectRefusesThePage() = runTest {
        refuses("status") { page(pageWithFirstRow { it - "status" }) }
    }

    @Test
    fun everyStatusCodeArrivesWithItsLiteralValue() = runTest {
        val dto = loadedPage(CAPTURED_PAGE)

        assertEquals(3, dto.data.first().status?.value)
        assertEquals(2, dto.data.first().reason?.value)
    }

    // --- rule 4: collections do default -----------------------------------------

    @Test
    fun aDisputeWithNoMessagesOrEvidenceStillLoads() = runTest {
        val dto = detail(withKey(withKey(CAPTURED_DETAIL, "messages", JsonArray(emptyList())), "evidence", JsonArray(emptyList())))

        assertEquals(emptyList<DisputeMessageDto>(), dto?.messages)
        assertEquals("d-1", dto?.id)
    }

    @Test
    fun anEmptyDisputePageIsARenderableState() = runTest {
        val dto = loadedPage(withKey(CAPTURED_PAGE, "data", JsonArray(emptyList())))

        assertEquals(emptyList<DisputeListItemDto>(), dto.data)
        assertEquals(37, dto.total)
    }

    // --- rule 5: nullable-by-design fields stay nullable ---------------------------

    /**
     * `refundAmount` and `resolvedOn` are `nullable: true`: an open dispute genuinely has no refund
     * and no resolution date, so neither becomes a zero the screen would render as "0 Kč refunded".
     */
    @Test
    fun anUnresolvedDisputeKeepsItsNulls() = runTest {
        val dto = loadedPage(pageWithFirstRow { (it - "refundAmount") - "resolvedOn" })

        assertNull(dto.data.first().refundAmount)
        assertNull(dto.data.first().resolvedOn)
        assertEquals("d-1", dto.data.first().id)
    }

    // --- the refused body ---------------------------------------------------------

    @Test
    fun aBodylessSuccessIsRefusedRatherThanFabricated() = runTest {
        refuses("PagedDataOfDisputeListItem") { serving("", code = 204) { it.getPaged(0, 20) } }
    }

    // --- payload plumbing ---------------------------------------------------------

    private fun mutating(body: String, transform: (JsonObject) -> JsonObject): String =
        transform(Json.parseToJsonElement(body).jsonObject).toString()

    private fun withoutKey(body: String, key: String): String = mutating(body) { it - key }

    private fun withKey(body: String, key: String, value: JsonElement): String =
        mutating(body) { it + (key to value) }

    private fun pageWithFirstRow(transform: (JsonObject) -> JsonObject): String =
        mutating(CAPTURED_PAGE) { root ->
            val rows = root["data"]!!.jsonArray.mapIndexed { index, row ->
                if (index == 0) transform(row.jsonObject) else row
            }
            root + ("data" to JsonArray(rows))
        }

    private fun detailWithLastMessage(transform: (JsonObject) -> JsonObject): String =
        mutating(CAPTURED_DETAIL) { root ->
            val rows = root["messages"]!!.jsonArray.toMutableList()
            rows[rows.lastIndex] = transform(rows.last().jsonObject)
            root + ("messages" to JsonArray(rows))
        }

    private fun JsonObject.withCodeMissingValue(key: String): JsonObject =
        this + (key to (this[key]!!.jsonObject - "value"))

    private operator fun JsonObject.minus(key: String) =
        JsonObject(toMutableMap().apply { remove(key) })

    private operator fun JsonObject.plus(entry: Pair<String, JsonElement>) =
        JsonObject(toMutableMap().apply { put(entry.first, entry.second) })

    @OptIn(ExperimentalSerializationApi::class)
    private fun serialNames(descriptor: SerialDescriptor): Set<String> =
        (0 until descriptor.elementsCount).map { descriptor.getElementName(it) }.toSet()

    private companion object {

        /** Every member non-zero and non-default, including both nullable-by-design figures. */
        val CAPTURED_PAGE = """
            {
              "pageNumber": 1,
              "pageSize": 20,
              "total": 37,
              "data": [
                {
                  "id": "d-1",
                  "orderId": "o-1",
                  "displayOrderNumber": "CL-2026-0042",
                  "customerName": "Anna Novakova",
                  "customerEmail": "anna@example.com",
                  "reason": { "type": "DisputeReason", "name": "QualityIssue", "value": 2 },
                  "status": { "type": "DisputeStatus", "name": "Resolved", "value": 3 },
                  "createdOn": "2026-07-02T10:00:00Z",
                  "resolvedOn": "2026-07-09T16:30:00Z",
                  "refundAmount": 1450.0
                },
                {
                  "id": "d-2",
                  "orderId": "o-2",
                  "displayOrderNumber": "CL-2026-0043",
                  "customerName": "Marek Dvorak",
                  "customerEmail": "marek@example.com",
                  "reason": { "type": "DisputeReason", "name": "NotCompleted", "value": 4 },
                  "status": { "type": "DisputeStatus", "name": "UnderReview", "value": 2 },
                  "createdOn": "2026-07-04T11:15:00Z",
                  "resolvedOn": "2026-07-12T09:00:00Z",
                  "refundAmount": 890.0
                }
              ]
            }
        """.trimIndent()

        val CAPTURED_DETAIL = """
            {
              "id": "d-1",
              "orderId": "o-1",
              "displayOrderNumber": "CL-2026-0042",
              "customerName": "Anna Novakova",
              "customerEmail": "anna@example.com",
              "reason": { "type": "DisputeReason", "name": "QualityIssue", "value": 2 },
              "description": "The bathroom was not cleaned.",
              "status": { "type": "DisputeStatus", "name": "Resolved", "value": 3 },
              "resolutionNotes": "Partial refund issued.",
              "refundAmount": 1450.0,
              "resolvedOn": "2026-07-09T16:30:00Z",
              "messages": [
                {
                  "id": "m-1",
                  "message": "The bathroom was skipped.",
                  "authorId": "u-1",
                  "authorName": "Anna",
                  "isStaffMessage": false,
                  "createdOn": "2026-07-02T10:05:00Z"
                },
                {
                  "id": "m-2",
                  "message": "We have refunded 1450 Kc.",
                  "authorId": "s-1",
                  "authorName": "Cleansia Support",
                  "isStaffMessage": true,
                  "createdOn": "2026-07-09T16:31:00Z"
                }
              ],
              "evidence": [
                {
                  "id": "e-1",
                  "fileName": "bathroom.jpg",
                  "filePath": "disputes/d-1/bathroom.jpg",
                  "blobUrl": "https://blob.example.com/bathroom.jpg",
                  "uploadedBy": "u-1",
                  "uploadedOn": "2026-07-02T10:06:00Z"
                }
              ],
              "createdOn": "2026-07-02T10:00:00Z",
              "updatedOn": "2026-07-09T16:31:00Z"
            }
        """.trimIndent()

        val LIST_ITEM_SPEC_PROPERTIES = setOf(
            "id",
            "orderId",
            "displayOrderNumber",
            "customerName",
            "customerEmail",
            "reason",
            "status",
            "createdOn",
            "resolvedOn",
            "refundAmount",
        )

        val DETAILS_SPEC_PROPERTIES = setOf(
            "id",
            "orderId",
            "displayOrderNumber",
            "customerName",
            "customerEmail",
            "reason",
            "description",
            "status",
            "resolutionNotes",
            "refundAmount",
            "resolvedOn",
            "messages",
            "evidence",
            "createdOn",
            "updatedOn",
        )

        val MESSAGE_SPEC_PROPERTIES = setOf(
            "id",
            "message",
            "authorId",
            "authorName",
            "isStaffMessage",
            "createdOn",
        )
    }
}
