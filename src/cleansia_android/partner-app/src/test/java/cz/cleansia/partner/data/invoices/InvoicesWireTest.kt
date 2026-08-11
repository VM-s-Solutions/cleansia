package cz.cleansia.partner.data.invoices

import cz.cleansia.core.network.ApiResult
import cz.cleansia.partner.api.client.EmployeePayrollApi
import cz.cleansia.partner.api.model.EmployeeInvoiceDetailDto
import cz.cleansia.partner.api.model.EmployeeInvoiceDto
import cz.cleansia.partner.api.model.EmployeeInvoiceStatus
import cz.cleansia.partner.data.payroll.OrderPayLine
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
import org.junit.Assert.assertTrue
import org.junit.Test
import retrofit2.Retrofit
import retrofit2.converter.kotlinx.serialization.asConverterFactory

/**
 * Neither invoice schema declares a `required` array, so the generator types every property
 * optional-with-null regardless of `nullable: false` and the whole contract lands in the mapper.
 * Everything here decodes a captured server payload over a socket through the generated DTOs and the
 * production `Json`; a mocked API would hand back Kotlin objects and could never notice a renamed
 * field.
 *
 * The list mapper is strict where the period-pay one drops: an invoice IS an addend of the hero
 * rollup, so a dropped row silently shrinks a total the cleaner reads as what they are owed.
 */
class InvoicesWireTest {

    private val json = Json { ignoreUnknownKeys = true; isLenient = true; explicitNulls = false }

    private fun repo(server: MockWebServer) = InvoicesRepositoryImpl(
        Retrofit.Builder()
            .baseUrl(server.url("/"))
            .addConverterFactory(json.asConverterFactory("application/json".toMediaType()))
            .build()
            .create(EmployeePayrollApi::class.java),
        json,
    )

    private suspend fun <T> serve(
        body: String,
        onRequest: (RecordedRequest) -> Unit = {},
        call: suspend (InvoicesRepository) -> ApiResult<T>,
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

    private suspend fun fetchList(
        body: String,
        onRequest: (RecordedRequest) -> Unit = {},
    ): ApiResult<List<Invoice>> = serve(body, onRequest) { it.getMyInvoices(EMPLOYEE_ID) }

    private suspend fun fetchDetail(
        body: String,
        onRequest: (RecordedRequest) -> Unit = {},
    ): ApiResult<InvoiceDetail> = serve(body, onRequest) { it.getById(INVOICE_ID) }

    private suspend fun loadedList(body: String): List<Invoice> {
        val result = fetchList(body)
        assertTrue("expected the captured page to map; got $result", result is ApiResult.Success)
        return (result as ApiResult.Success).data
    }

    private suspend fun loadedDetail(body: String): InvoiceDetail {
        val result = fetchDetail(body)
        assertTrue("expected the captured invoice to map; got $result", result is ApiResult.Success)
        return (result as ApiResult.Success).data
    }

    private suspend fun assertListMappingFails(field: String, body: String) {
        val result = fetchList(body)
        assertTrue(
            "a missing $field must fail the page rather than read as a default; got $result",
            result is ApiResult.Error,
        )
    }

    private suspend fun assertDetailMappingFails(field: String, body: String) {
        val result = fetchDetail(body)
        assertTrue(
            "a missing $field must fail the mapping rather than read as a default; got $result",
            result is ApiResult.Error,
        )
    }

    // --- the field-name contract ------------------------------------------------

    @Test
    fun invoiceDtoSerialNamesAreExactlyTheSpecProperties() {
        assertEquals(LIST_SPEC_PROPERTIES, serialNames(EmployeeInvoiceDto.serializer().descriptor))
    }

    @Test
    fun invoiceDetailDtoSerialNamesAreExactlyTheSpecProperties() {
        assertEquals(DETAIL_SPEC_PROPERTIES, serialNames(EmployeeInvoiceDetailDto.serializer().descriptor))
    }

    @Test
    fun theDetailDtoCarriesEverythingTheListOneDoesPlusItsOwn() {
        assertEquals(
            setOf("specificSymbol", "orderPays"),
            DETAIL_SPEC_PROPERTIES - LIST_SPEC_PROPERTIES,
        )
        assertEquals(emptySet<String>(), LIST_SPEC_PROPERTIES - DETAIL_SPEC_PROPERTIES)
    }

    @Test
    fun theListRequestKeepsTheQueryNamesTheServerBinds() = runTest {
        var path: String? = null
        var method: String? = null
        fetchList(CAPTURED_PAGE) { request ->
            path = request.path
            method = request.method
        }

        assertEquals("GET", method)
        assertTrue("got $path", path!!.startsWith("/api/EmployeePayroll/GetPagedInvoices?"))
        assertTrue("got $path", path!!.contains("Filter.EmployeeId=$EMPLOYEE_ID"))
        assertTrue("got $path", path!!.contains("Offset=0"))
        assertTrue("got $path", path!!.contains("Limit=50"))
    }

    @Test
    fun theDetailRequestPutsTheInvoiceIdOnThePath() = runTest {
        var path: String? = null
        var method: String? = null
        fetchDetail(capturedInvoice().toString()) { request ->
            path = request.path
            method = request.method
        }

        assertEquals("GET", method)
        assertEquals("/api/EmployeePayroll/GetInvoiceById/$INVOICE_ID", path)
    }

    // --- rule 1: money is never coerced -----------------------------------------

    @Test
    fun everyListMoneyFieldArrivesWithItsLiteralValue() = runTest {
        val invoice = loadedList(CAPTURED_PAGE).first()

        assertEquals(1420.10, invoice.subTotal, 0.0)
        assertEquals(120.40, invoice.bonusAmount, 0.0)
        assertEquals(40.25, invoice.deductionAmount, 0.0)
        assertEquals(1500.25, invoice.totalAmount, 0.0)
        assertEquals(3, invoice.totalOrders)
    }

    @Test
    fun everyDetailMoneyFieldArrivesWithItsLiteralValue() = runTest {
        val invoice = loadedDetail(capturedInvoice().toString())

        assertEquals(1420.10, invoice.subTotal, 0.0)
        assertEquals(120.40, invoice.bonusAmount, 0.0)
        assertEquals(40.25, invoice.deductionAmount, 0.0)
        assertEquals(1500.25, invoice.totalAmount, 0.0)
        assertEquals(3, invoice.totalOrders)
    }

    @Test
    fun aMissingListMoneyKeyFailsTheMappingRatherThanReadingZero() = runTest {
        REQUIRED_NUMBERS.forEach { field ->
            assertListMappingFails(field, pageWithoutFirstInvoiceKey(field))
        }
    }

    @Test
    fun aMissingDetailMoneyKeyFailsTheMappingRatherThanReadingZero() = runTest {
        REQUIRED_NUMBERS.forEach { field ->
            assertDetailMappingFails(field, (capturedInvoice() - field).toString())
        }
    }

    @Test
    fun anExplicitNullMoneyValueFailsJustLikeAMissingKey() = runTest {
        assertListMappingFails(
            "totalAmount",
            pageWithFirstInvoice { it + ("totalAmount" to JsonNull) },
        )
    }

    @Test
    fun aGenuineZeroInvoiceIsARealStateAndSurvives() = runTest {
        val page = pageWithFirstInvoice {
            it + ("totalAmount" to JsonPrimitive(0.0)) +
                ("subTotal" to JsonPrimitive(0.0)) +
                ("bonusAmount" to JsonPrimitive(0.0)) +
                ("deductionAmount" to JsonPrimitive(0.0)) +
                ("totalOrders" to JsonPrimitive(0))
        }

        val invoices = loadedList(page)

        assertEquals(0.0, invoices.first().totalAmount, 0.0)
        assertEquals(0, invoices.first().totalOrders)
        assertEquals(2, invoices.size)
    }

    // --- the hero rollup ---------------------------------------------------------

    @Test
    fun theHealthyPageSumsToTheTotalTheCleanerIsOwed() = runTest {
        val invoices = loadedList(CAPTURED_PAGE)

        assertEquals(2, invoices.size)
        assertEquals(3901.00, invoices.sumOf { it.totalAmount }, 0.0001)
    }

    @Test
    fun oneBrokenInvoiceCannotSilentlyProduceASmallerRollup() = runTest {
        val result = fetchList(pageWithFirstInvoice { it - "totalAmount" })

        assertTrue(
            "a broken money field must not degrade to a partial page; got $result",
            result is ApiResult.Error,
        )
        assertNull(
            "no partial list may reach the rollup",
            (result as? ApiResult.Success)?.data,
        )
    }

    @Test
    fun aBrokenInvoiceIsNotDroppedFromTheListInstead() = runTest {
        val result = fetchList(pageWithFirstInvoice { it - "totalAmount" })
        val survivors = (result as? ApiResult.Success)?.data

        assertNull(
            "dropping the row would silently subtract ${'$'}1500.25 from the rollup; got $survivors",
            survivors,
        )
    }

    // --- rule 2: booleans follow the money rule ---------------------------------

    @Test
    fun aMissingPdfGenerationFailedFailsTheMappingRatherThanReadingFalse() = runTest {
        assertListMappingFails("pdfGenerationFailed", pageWithoutFirstInvoiceKey("pdfGenerationFailed"))
        assertDetailMappingFails("pdfGenerationFailed", (capturedInvoice() - "pdfGenerationFailed").toString())
    }

    @Test
    fun anExplicitPdfFailureIsARealStateAndSurvives() = runTest {
        val invoice = loadedDetail(
            (capturedInvoice() + ("pdfGenerationFailed" to JsonPrimitive(true))).toString(),
        )

        assertEquals(true, invoice.pdfGenerationFailed)
        assertEquals("blob store rejected the render", invoice.pdfGenerationError)
    }

    @Test
    fun anExplicitFalsePdfFlagIsARealStateAndSurvives() = runTest {
        val invoice = loadedDetail(capturedInvoice().toString())

        assertEquals(false, invoice.pdfGenerationFailed)
    }

    @Test
    fun aMissingStatusFailsTheMappingRatherThanReadingAPlaceholder() = runTest {
        assertListMappingFails("status", pageWithoutFirstInvoiceKey("status"))
        assertDetailMappingFails("status", (capturedInvoice() - "status").toString())
    }

    @Test
    fun theStatusTheWireCarriedSurvivesUnchanged() = runTest {
        val invoices = loadedList(CAPTURED_PAGE)

        assertEquals(EmployeeInvoiceStatus._3, invoices.first().status)
        assertEquals(EmployeeInvoiceStatus._1, invoices.last().status)
    }

    // --- rule 3: identity is refused, never synthesized --------------------------

    @Test
    fun anInvoiceWithoutAnIdFailsThePageRatherThanBeingDropped() = runTest {
        assertListMappingFails("id", pageWithoutFirstInvoiceKey("id"))
    }

    @Test
    fun anInvoiceWithAnExplicitNullIdFailsThePage() = runTest {
        assertListMappingFails("id", pageWithFirstInvoice { it + ("id" to JsonNull) })
    }

    @Test
    fun aDetailWithoutAnIdIsRefused() = runTest {
        assertDetailMappingFails("id", (capturedInvoice() - "id").toString())
    }

    @Test
    fun everyInvoiceKeepsTheIdTheWireCarried() = runTest {
        assertEquals(listOf("inv-1", "inv-2"), loadedList(CAPTURED_PAGE).map { it.id })
    }

    @Test
    fun aDetailOrderPayLineWithoutAnIdIsDroppedAndTheInvoiceTotalSurvives() = runTest {
        val invoice = loadedDetail(detailWithFirstOrderPay { it - "id" })

        assertEquals(listOf("line-2"), invoice.orderPays.map { it.id })
        assertEquals(1500.25, invoice.totalAmount, 0.0)
    }

    // --- rule 4: collections do default -----------------------------------------

    @Test
    fun aPageWithNoDataKeyIsAnEmptyRenderableState() = runTest {
        val invoices = loadedList((capturedPage() - "data").toString())

        assertEquals(emptyList<Invoice>(), invoices)
    }

    @Test
    fun aPageWithAnExplicitNullDataListIsAnEmptyRenderableState() = runTest {
        val invoices = loadedList((capturedPage() + ("data" to JsonNull)).toString())

        assertEquals(emptyList<Invoice>(), invoices)
    }

    @Test
    fun anInvoiceWithNoOrderPaysKeyIsAnEmptyRenderableState() = runTest {
        val invoice = loadedDetail((capturedInvoice() - "orderPays").toString())

        assertEquals(emptyList<OrderPayLine>(), invoice.orderPays)
        assertEquals(1500.25, invoice.totalAmount, 0.0)
    }

    @Test
    fun anInvoiceWithAnExplicitNullOrderPaysIsAnEmptyRenderableState() = runTest {
        val invoice = loadedDetail((capturedInvoice() + ("orderPays" to JsonNull)).toString())

        assertEquals(emptyList<OrderPayLine>(), invoice.orderPays)
    }

    // --- rule 5: nullable-by-design strings stay nullable -------------------------

    @Test
    fun nullableByDesignStringsStayNullRatherThanBecomingPlaceholders() = runTest {
        val stripped = NULLABLE_STRINGS.fold(capturedInvoice()) { obj, key -> obj - key }
        val invoice = loadedDetail(stripped.toString())

        assertNull(invoice.invoiceNumber)
        assertNull(invoice.payPeriodLabel)
        assertNull(invoice.currencyCode)
        assertNull(invoice.variableSymbol)
        assertNull(invoice.specificSymbol)
        assertNull(invoice.paymentReference)
        assertNull(invoice.adminNotes)
        assertNull(invoice.bankTransferNote)
        assertNull(invoice.pdfGenerationError)
        assertEquals(1500.25, invoice.totalAmount, 0.0)
    }

    /**
     * `generatedAt` is `nullable: false` in the spec, yet stays nullable in the domain: the card
     * already renders its absence, so keeping it null fabricates nothing. The rule is that coercion
     * must not invent a fact the cleaner reads — not that every spec-required field must be refused.
     */
    @Test
    fun theTimestampsStayNullableBecauseTheirAbsenceIsAlreadyRendered() = runTest {
        val invoice = loadedDetail(
            ((capturedInvoice() - "generatedAt") - "approvedAt" - "paidAt").toString(),
        )

        assertNull(invoice.generatedAt)
        assertNull(invoice.approvedAt)
        assertNull(invoice.paidAt)
        assertEquals(1500.25, invoice.totalAmount, 0.0)
    }

    // --- payload plumbing ---------------------------------------------------------

    private fun capturedPage(): JsonObject = Json.parseToJsonElement(CAPTURED_PAGE).jsonObject

    private fun capturedInvoice(): JsonObject =
        Json.parseToJsonElement(CAPTURED_INVOICE_DETAIL).jsonObject

    private fun pageWithFirstInvoice(transform: (JsonObject) -> JsonObject): String {
        val root = capturedPage()
        val rows = root["data"]!!.jsonArray.mapIndexed { index, row ->
            if (index == 0) transform(row.jsonObject) else row
        }
        return JsonObject(root.toMutableMap().apply { put("data", JsonArray(rows)) }).toString()
    }

    private fun pageWithoutFirstInvoiceKey(key: String): String = pageWithFirstInvoice { it - key }

    private fun detailWithFirstOrderPay(transform: (JsonObject) -> JsonObject): String {
        val root = capturedInvoice()
        val lines = root["orderPays"]!!.jsonArray.mapIndexed { index, line ->
            if (index == 0) transform(line.jsonObject) else line
        }
        return JsonObject(root.toMutableMap().apply { put("orderPays", JsonArray(lines)) }).toString()
    }

    private operator fun JsonObject.minus(key: String) =
        JsonObject(toMutableMap().apply { remove(key) })

    private operator fun JsonObject.plus(entry: Pair<String, JsonElement>) =
        JsonObject(toMutableMap().apply { put(entry.first, entry.second) })

    @OptIn(ExperimentalSerializationApi::class)
    private fun serialNames(descriptor: SerialDescriptor): Set<String> =
        (0 until descriptor.elementsCount).map { descriptor.getElementName(it) }.toSet()

    private companion object {
        const val EMPLOYEE_ID = "emp-7"
        const val INVOICE_ID = "inv-1"

        /**
         * Two invoices whose totals sum to 3901.00 — the rollup needs more than one addend for
         * "one row dropped" to be distinguishable from "the page failed".
         */
        val CAPTURED_PAGE = """
            {
              "pageNumber": 1,
              "pageSize": 50,
              "total": 2,
              "data": [
                {
                  "id": "inv-1",
                  "employeeId": "emp-7",
                  "employeeName": "Jana Novak",
                  "payPeriodId": "pp-9",
                  "payPeriodLabel": "1 - 15 Aug 2026",
                  "invoiceNumber": "2026-0042",
                  "variableSymbol": "20260042",
                  "paymentReference": "CLS-2026-0042",
                  "totalOrders": 3,
                  "subTotal": 1420.10,
                  "bonusAmount": 120.40,
                  "deductionAmount": 40.25,
                  "totalAmount": 1500.25,
                  "currencyCode": "CZK",
                  "status": 3,
                  "pdfBlobName": "invoices/2026-0042.pdf",
                  "pdfGenerationFailed": false,
                  "pdfGenerationError": null,
                  "generatedAt": "2026-08-16T06:00:00Z",
                  "approvedAt": "2026-08-17T09:30:00Z",
                  "approvedBy": "admin-3",
                  "paidAt": "2026-08-18T11:05:00Z",
                  "adminNotes": "approved after the dispute closed",
                  "bankTransferNote": "Cleansia invoice 2026-0042"
                },
                {
                  "id": "inv-2",
                  "employeeId": "emp-7",
                  "employeeName": "Jana Novak",
                  "payPeriodId": "pp-10",
                  "payPeriodLabel": "16 - 31 Aug 2026",
                  "invoiceNumber": "2026-0043",
                  "variableSymbol": "20260043",
                  "paymentReference": "CLS-2026-0043",
                  "totalOrders": 5,
                  "subTotal": 2300.00,
                  "bonusAmount": 180.75,
                  "deductionAmount": 80.00,
                  "totalAmount": 2400.75,
                  "currencyCode": "CZK",
                  "status": 1,
                  "pdfBlobName": "invoices/2026-0043.pdf",
                  "pdfGenerationFailed": false,
                  "pdfGenerationError": null,
                  "generatedAt": "2026-09-01T06:00:00Z",
                  "approvedAt": null,
                  "approvedBy": null,
                  "paidAt": null,
                  "adminNotes": null,
                  "bankTransferNote": "Cleansia invoice 2026-0043"
                }
              ]
            }
        """.trimIndent()

        /**
         * Every member non-default, including `specificSymbol` and `orderPays` which only the detail
         * schema carries — a payload that leans on defaults cannot tell a mapped field from a
         * forgotten one.
         */
        val CAPTURED_INVOICE_DETAIL = """
            {
              "id": "inv-1",
              "employeeId": "emp-7",
              "employeeName": "Jana Novak",
              "payPeriodId": "pp-9",
              "payPeriodLabel": "1 - 15 Aug 2026",
              "invoiceNumber": "2026-0042",
              "variableSymbol": "20260042",
              "specificSymbol": "778899",
              "paymentReference": "CLS-2026-0042",
              "totalOrders": 3,
              "subTotal": 1420.10,
              "bonusAmount": 120.40,
              "deductionAmount": 40.25,
              "totalAmount": 1500.25,
              "currencyCode": "CZK",
              "status": 3,
              "pdfBlobName": "invoices/2026-0042.pdf",
              "pdfGenerationFailed": false,
              "pdfGenerationError": "blob store rejected the render",
              "generatedAt": "2026-08-16T06:00:00Z",
              "approvedAt": "2026-08-17T09:30:00Z",
              "approvedBy": "admin-3",
              "paidAt": "2026-08-18T11:05:00Z",
              "adminNotes": "approved after the dispute closed",
              "bankTransferNote": "Cleansia invoice 2026-0042",
              "orderPays": [
                {
                  "id": "line-1",
                  "orderId": "ord-1",
                  "orderNumber": "ORD-2026-0001",
                  "employeeId": "emp-7",
                  "employeeName": "Jana Novak",
                  "payPeriodId": "pp-9",
                  "payPeriodLabel": "1 - 15 Aug 2026",
                  "basePay": 700.10,
                  "extrasPay": 50.00,
                  "expensesPay": 20.00,
                  "bonusPay": 60.20,
                  "deductionPay": 20.10,
                  "totalPay": 810.20,
                  "payBreakdown": "base 700.10 + extras 50.00",
                  "isApproved": true,
                  "createdOn": "2026-08-03T09:15:00Z"
                },
                {
                  "id": "line-2",
                  "orderId": "ord-2",
                  "orderNumber": "ORD-2026-0002",
                  "employeeId": "emp-7",
                  "employeeName": "Jana Novak",
                  "payPeriodId": "pp-9",
                  "payPeriodLabel": "1 - 15 Aug 2026",
                  "basePay": 720.00,
                  "extrasPay": 50.00,
                  "expensesPay": 20.00,
                  "bonusPay": 60.20,
                  "deductionPay": 20.15,
                  "totalPay": 830.05,
                  "payBreakdown": "base 720.00 + extras 50.00",
                  "isApproved": false,
                  "createdOn": "2026-08-07T14:40:00Z"
                }
              ]
            }
        """.trimIndent()

        val LIST_SPEC_PROPERTIES = setOf(
            "id",
            "employeeId",
            "employeeName",
            "payPeriodId",
            "payPeriodLabel",
            "invoiceNumber",
            "variableSymbol",
            "paymentReference",
            "totalOrders",
            "subTotal",
            "bonusAmount",
            "deductionAmount",
            "totalAmount",
            "currencyCode",
            "status",
            "pdfBlobName",
            "pdfGenerationFailed",
            "pdfGenerationError",
            "generatedAt",
            "approvedAt",
            "approvedBy",
            "paidAt",
            "adminNotes",
            "bankTransferNote",
        )

        val DETAIL_SPEC_PROPERTIES = LIST_SPEC_PROPERTIES + setOf("specificSymbol", "orderPays")

        val REQUIRED_NUMBERS = listOf(
            "totalOrders",
            "subTotal",
            "bonusAmount",
            "deductionAmount",
            "totalAmount",
        )

        val NULLABLE_STRINGS = listOf(
            "employeeId",
            "employeeName",
            "payPeriodId",
            "payPeriodLabel",
            "invoiceNumber",
            "variableSymbol",
            "specificSymbol",
            "paymentReference",
            "currencyCode",
            "pdfBlobName",
            "pdfGenerationError",
            "approvedBy",
            "adminNotes",
            "bankTransferNote",
        )
    }
}
