package cz.cleansia.partner.data.payroll

import cz.cleansia.core.network.ApiResult
import cz.cleansia.partner.api.client.EmployeePayrollApi
import cz.cleansia.partner.api.model.OrderEmployeePayDto
import cz.cleansia.partner.api.model.PeriodPaySummaryDto
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
 * The wire contract the deleted hand-written `PeriodPayApi` used to own implicitly. Neither payroll
 * schema declares a `required` array, so the generator types every property optional-with-null
 * regardless of `nullable: false` — which puts the whole contract in the mapper. Everything here
 * decodes a captured server payload over a socket through the generated DTOs and the production
 * `Json`; a mocked API would hand back Kotlin objects and could never notice a renamed field.
 */
class PeriodPayWireTest {

    private val json = Json { ignoreUnknownKeys = true; isLenient = true; explicitNulls = false }

    private fun repo(server: MockWebServer) = PeriodPayRepositoryImpl(
        Retrofit.Builder()
            .baseUrl(server.url("/"))
            .addConverterFactory(json.asConverterFactory("application/json".toMediaType()))
            .build()
            .create(EmployeePayrollApi::class.java),
        json,
    )

    private suspend fun fetch(
        body: String,
        onRequest: (RecordedRequest) -> Unit = {},
    ): ApiResult<PeriodPaySummary> {
        val server = MockWebServer()
        server.start()
        return try {
            server.enqueue(
                MockResponse()
                    .setResponseCode(200)
                    .setHeader("Content-Type", "application/json")
                    .setBody(body),
            )
            repo(server).getPeriodPays(EMPLOYEE_ID, PAY_PERIOD_ID).also { onRequest(server.takeRequest()) }
        } finally {
            server.shutdown()
        }
    }

    private suspend fun loaded(body: String): PeriodPaySummary {
        val result = fetch(body)
        assertTrue("expected the captured payload to map; got $result", result is ApiResult.Success)
        return (result as ApiResult.Success).data
    }

    private suspend fun assertMappingFails(field: String, body: String) {
        val result = fetch(body)
        assertTrue(
            "a missing $field must fail the mapping rather than read as a default; got $result",
            result is ApiResult.Error,
        )
    }

    // --- the field-name contract ------------------------------------------------

    @Test
    fun summaryDtoSerialNamesAreExactlyTheSpecProperties() {
        assertEquals(SUMMARY_SPEC_PROPERTIES, serialNames(PeriodPaySummaryDto.serializer().descriptor))
    }

    @Test
    fun linePayDtoSerialNamesAreExactlyTheSpecProperties() {
        assertEquals(LINE_SPEC_PROPERTIES, serialNames(OrderEmployeePayDto.serializer().descriptor))
    }

    @Test
    fun theRequestKeepsTheQueryNamesTheServerBinds() = runTest {
        var path: String? = null
        var method: String? = null
        fetch(CAPTURED_PAYLOAD) { request ->
            path = request.path
            method = request.method
        }

        assertEquals("GET", method)
        assertEquals(
            "/api/EmployeePayroll/GetPeriodPays?EmployeeId=$EMPLOYEE_ID&PayPeriodId=$PAY_PERIOD_ID",
            path,
        )
    }

    // --- rule 1: money is never coerced -----------------------------------------

    @Test
    fun everySummaryMoneyFieldArrivesWithItsLiteralValue() = runTest {
        val summary = loaded(CAPTURED_PAYLOAD)

        assertEquals(4200.50, summary.totalBasePay, 0.0)
        assertEquals(310.25, summary.totalExtrasPay, 0.0)
        assertEquals(128.75, summary.totalExpensesPay, 0.0)
        assertEquals(512.40, summary.totalBonusPay, 0.0)
        assertEquals(90.10, summary.totalDeductionPay, 0.0)
        assertEquals(4951.80, summary.grandTotal, 0.0)
        assertEquals(2, summary.totalOrders)
    }

    @Test
    fun everyLineMoneyFieldArrivesWithItsLiteralValue() = runTest {
        val line = loaded(CAPTURED_PAYLOAD).orderPays.first()

        assertEquals(1400.10, line.basePay, 0.0)
        assertEquals(103.20, line.extrasPay, 0.0)
        assertEquals(42.90, line.expensesPay, 0.0)
        assertEquals(170.80, line.bonusPay, 0.0)
        assertEquals(30.05, line.deductionPay, 0.0)
        assertEquals(1686.95, line.totalPay, 0.0)
    }

    @Test
    fun aMissingSummaryMoneyKeyFailsTheMappingRatherThanReadingZero() = runTest {
        SUMMARY_REQUIRED_NUMBERS.forEach { field ->
            assertMappingFails(field, payloadWithoutSummaryKey(field))
        }
    }

    @Test
    fun aMissingLineMoneyKeyFailsTheMappingRatherThanReadingZero() = runTest {
        LINE_REQUIRED_NUMBERS.forEach { field ->
            assertMappingFails(field, payloadWithoutFirstLineKey(field))
        }
    }

    // --- rule 2: booleans follow the money rule ---------------------------------

    @Test
    fun aMissingHasInvoiceFailsTheMappingRatherThanReadingFalse() = runTest {
        assertMappingFails("hasInvoice", payloadWithoutSummaryKey("hasInvoice"))
    }

    @Test
    fun aMissingIsApprovedFailsTheMappingRatherThanReadingFalse() = runTest {
        assertMappingFails("isApproved", payloadWithoutFirstLineKey("isApproved"))
    }

    @Test
    fun anExplicitFalseIsARealPayrollStateAndSurvives() = runTest {
        val summary = loaded(payloadWithFirstLine { it + ("isApproved" to falseValue()) })

        assertEquals(false, summary.orderPays.first().isApproved)
    }

    // --- rule 3: identity is refused, never synthesized --------------------------

    @Test
    fun aLineWithoutAnIdIsDroppedAndTheRestOfThePeriodSurvives() = runTest {
        val summary = loaded(payloadWithFirstLine { it - "id" })

        assertEquals(listOf("line-2"), summary.orderPays.map { it.id })
        assertEquals(4951.80, summary.grandTotal, 0.0)
        assertEquals(2, summary.totalOrders)
    }

    @Test
    fun aLineWithAnExplicitNullIdIsDropped() = runTest {
        val summary = loaded(payloadWithFirstLine { it + ("id" to nullValue()) })

        assertEquals(listOf("line-2"), summary.orderPays.map { it.id })
    }

    @Test
    fun everySurvivingLineKeepsTheIdTheWireCarried() = runTest {
        val summary = loaded(CAPTURED_PAYLOAD)

        assertEquals(listOf("line-1", "line-2"), summary.orderPays.map { it.id })
    }

    // --- rule 4: collections do default -----------------------------------------

    @Test
    fun aPeriodWithNoJobsKeyIsAnEmptyRenderableState() = runTest {
        val summary = loaded(payloadWithoutSummaryKey("orderPays"))

        assertEquals(emptyList<OrderPayLine>(), summary.orderPays)
        assertEquals(4951.80, summary.grandTotal, 0.0)
    }

    @Test
    fun aPeriodWithAnExplicitNullJobsListIsAnEmptyRenderableState() = runTest {
        val summary = loaded(
            JsonObject(capturedObject().toMutableMap().apply { put("orderPays", nullValue()) }).toString(),
        )

        assertEquals(emptyList<OrderPayLine>(), summary.orderPays)
    }

    // --- rule 5: nullable-by-design strings stay nullable -------------------------

    @Test
    fun nullableByDesignStringsStayNullRatherThanBecomingPlaceholders() = runTest {
        val stripped = NULLABLE_STRINGS.fold(capturedObject()) { obj, key -> obj - key }
        val summary = loaded(
            JsonObject(
                stripped.toMutableMap().apply {
                    put(
                        "orderPays",
                        JsonArray(
                            stripped["orderPays"]!!.jsonArray.map { line ->
                                LINE_NULLABLE_STRINGS.fold(line.jsonObject) { obj, key -> obj - key }
                            },
                        ),
                    )
                },
            ).toString(),
        )

        assertNull(summary.payPeriodLabel)
        assertNull(summary.invoiceId)
        assertNull(summary.orderPays.first().orderNumber)
        assertNull(summary.orderPays.first().createdOn)
        assertEquals(4951.80, summary.grandTotal, 0.0)
    }

    // --- payload plumbing ---------------------------------------------------------

    private fun capturedObject(): JsonObject = Json.parseToJsonElement(CAPTURED_PAYLOAD).jsonObject

    private fun payloadWithoutSummaryKey(key: String): String = (capturedObject() - key).toString()

    private fun payloadWithFirstLine(transform: (JsonObject) -> JsonObject): String {
        val root = capturedObject()
        val lines = root["orderPays"]!!.jsonArray.mapIndexed { index, line ->
            if (index == 0) transform(line.jsonObject) else line
        }
        return JsonObject(root.toMutableMap().apply { put("orderPays", JsonArray(lines)) }).toString()
    }

    private fun payloadWithoutFirstLineKey(key: String): String = payloadWithFirstLine { it - key }

    private operator fun JsonObject.minus(key: String) =
        JsonObject(toMutableMap().apply { remove(key) })

    private operator fun JsonObject.plus(entry: Pair<String, JsonElement>) =
        JsonObject(toMutableMap().apply { put(entry.first, entry.second) })

    private fun nullValue() = JsonNull

    private fun falseValue() = JsonPrimitive(false)

    @OptIn(ExperimentalSerializationApi::class)
    private fun serialNames(descriptor: SerialDescriptor): Set<String> =
        (0 until descriptor.elementsCount).map { descriptor.getElementName(it) }.toSet()

    private companion object {
        const val EMPLOYEE_ID = "emp-7"
        const val PAY_PERIOD_ID = "pp-9"

        /**
         * Every member non-zero and non-default, including the five line properties the domain model
         * deliberately drops — a payload that leans on defaults cannot tell a mapped field from a
         * forgotten one.
         */
        val CAPTURED_PAYLOAD = """
            {
              "payPeriodId": "pp-9",
              "payPeriodLabel": "1 - 15 Aug 2026",
              "employeeId": "emp-7",
              "employeeName": "Jana Novak",
              "totalOrders": 2,
              "totalBasePay": 4200.50,
              "totalExtrasPay": 310.25,
              "totalExpensesPay": 128.75,
              "totalBonusPay": 512.40,
              "totalDeductionPay": 90.10,
              "grandTotal": 4951.80,
              "hasInvoice": true,
              "invoiceId": "inv-42",
              "orderPays": [
                {
                  "id": "line-1",
                  "orderId": "ord-1",
                  "orderNumber": "ORD-2026-0001",
                  "employeeId": "emp-7",
                  "employeeName": "Jana Novak",
                  "payPeriodId": "pp-9",
                  "payPeriodLabel": "1 - 15 Aug 2026",
                  "basePay": 1400.10,
                  "extrasPay": 103.20,
                  "expensesPay": 42.90,
                  "bonusPay": 170.80,
                  "deductionPay": 30.05,
                  "totalPay": 1686.95,
                  "payBreakdown": "base 1400.10 + extras 103.20",
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
                  "basePay": 2800.40,
                  "extrasPay": 207.05,
                  "expensesPay": 85.85,
                  "bonusPay": 341.60,
                  "deductionPay": 60.05,
                  "totalPay": 3374.85,
                  "payBreakdown": "base 2800.40 + extras 207.05",
                  "isApproved": true,
                  "createdOn": "2026-08-07T14:40:00Z"
                }
              ]
            }
        """.trimIndent()

        val SUMMARY_SPEC_PROPERTIES = setOf(
            "payPeriodId",
            "payPeriodLabel",
            "employeeId",
            "employeeName",
            "totalOrders",
            "totalBasePay",
            "totalExtrasPay",
            "totalExpensesPay",
            "totalBonusPay",
            "totalDeductionPay",
            "grandTotal",
            "hasInvoice",
            "invoiceId",
            "orderPays",
            "currencyCode",
        )

        val LINE_SPEC_PROPERTIES = setOf(
            "id",
            "orderId",
            "orderNumber",
            "employeeId",
            "employeeName",
            "payPeriodId",
            "payPeriodLabel",
            "basePay",
            "extrasPay",
            "expensesPay",
            "bonusPay",
            "deductionPay",
            "totalPay",
            "payBreakdown",
            "isApproved",
            "createdOn",
        )

        val SUMMARY_REQUIRED_NUMBERS = listOf(
            "totalOrders",
            "totalBasePay",
            "totalExtrasPay",
            "totalExpensesPay",
            "totalBonusPay",
            "totalDeductionPay",
            "grandTotal",
        )

        val LINE_REQUIRED_NUMBERS = listOf(
            "basePay",
            "extrasPay",
            "expensesPay",
            "bonusPay",
            "deductionPay",
            "totalPay",
        )

        val NULLABLE_STRINGS = listOf("payPeriodLabel", "invoiceId", "employeeName", "payPeriodId", "employeeId")

        val LINE_NULLABLE_STRINGS = listOf("orderNumber", "createdOn", "orderId")
    }
}
