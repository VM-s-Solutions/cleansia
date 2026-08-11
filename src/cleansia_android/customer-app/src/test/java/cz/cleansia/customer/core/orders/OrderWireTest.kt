package cz.cleansia.customer.core.orders

import cz.cleansia.core.network.WireContractViolation
import cz.cleansia.customer.core.network.IntEnumSerializersModule
import kotlinx.coroutines.test.runTest
import kotlinx.serialization.ExperimentalSerializationApi
import kotlinx.serialization.descriptors.SerialDescriptor
import kotlinx.serialization.json.Json
import kotlinx.serialization.json.JsonArray
import kotlinx.serialization.json.JsonElement
import kotlinx.serialization.json.JsonNull
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
import org.junit.Assert.assertTrue
import org.junit.Test
import retrofit2.Retrofit
import retrofit2.converter.kotlinx.serialization.asConverterFactory
import cz.cleansia.customer.api.client.OrderApi as GenOrderApi
import cz.cleansia.customer.api.model.CancelOrderResponse as GenCancelOrderResponse
import cz.cleansia.customer.api.model.CurrencyListItem as GenCurrencyListItem
import cz.cleansia.customer.api.model.GetCancellationFeePreviewResponse as GenGetCancellationFeePreviewResponse
import cz.cleansia.customer.api.model.OrderListItem as GenOrderListItem

/**
 * Every order surface a customer reads money from. No customer order schema declares a `required`
 * array, so the generator types every property optional-with-null regardless of `nullable: false` —
 * and unlike the partner app these mappers already existed, so an audit that asks "does it have a
 * `toAppDto()`?" scores the file clean while every price in it defaults to zero.
 *
 * Everything decodes a captured payload over a socket through the generated DTOs and the production
 * `Json` (including [IntEnumSerializersModule], without which the int-valued enums on this wire decode
 * differently than they do in the app).
 */
class OrderWireTest {

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

    private suspend fun listed(body: String): OrderListResponseDto =
        serving(body) { it.getMyOrders() }.body()!!

    private suspend fun detailed(body: String): OrderDetailDto = serving(body) { it.getById("o-1") }.body()!!

    private suspend fun cancelled(body: String) =
        serving(body) { it.cancel(CancelOrderRequest(orderId = "o-1", reason = "plans changed")) }.body()!!

    private suspend fun preview(body: String) = serving(body) { it.getCancellationPreview("o-1") }.body()!!

    /**
     * The refusal is a throw carrying the offending field name, which is the whole point of the
     * idiom: a mapping that answered `null` told triage only that *something* on this payload was
     * wrong.
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

    // --- the field-name contract ------------------------------------------------

    @Test
    fun orderListItemDtoSerialNamesAreExactlyTheSpecProperties() {
        assertEquals(LIST_ITEM_SPEC_PROPERTIES, serialNames(GenOrderListItem.serializer().descriptor))
    }

    @Test
    fun currencyListItemDtoSerialNamesAreExactlyTheSpecProperties() {
        assertEquals(CURRENCY_SPEC_PROPERTIES, serialNames(GenCurrencyListItem.serializer().descriptor))
    }

    @Test
    fun cancelResponseDtoSerialNamesAreExactlyTheSpecProperties() {
        assertEquals(CANCEL_SPEC_PROPERTIES, serialNames(GenCancelOrderResponse.serializer().descriptor))
    }

    @Test
    fun cancellationPreviewDtoSerialNamesAreExactlyTheSpecProperties() {
        assertEquals(
            PREVIEW_SPEC_PROPERTIES,
            serialNames(GenGetCancellationFeePreviewResponse.serializer().descriptor),
        )
    }

    @Test
    fun theRequestsKeepThePathsTheServerBinds() = runTest {
        var listPath: String? = null
        var detailPath: String? = null
        serving(CAPTURED_PAGE, onRequest = { listPath = it.path }) { it.getMyOrders() }
        serving(CAPTURED_ORDER, onRequest = { detailPath = it.path }) { it.getById("o-1") }

        assertEquals("/api/Order/GetMyOrders?Offset=0&Limit=20", listPath)
        assertEquals("/api/Order/GetById?OrderId=o-1", detailPath)
    }

    // --- rule 1: money is never coerced -----------------------------------------

    @Test
    fun everyListRowMoneyFieldArrivesWithItsLiteralValue() = runTest {
        val row = listed(CAPTURED_PAGE).data.first()

        assertEquals(4380.00, row.totalPrice, 0.0)
        assertEquals(3650.00, row.originalSubtotal, 0.0)
        assertEquals(2, row.appliedDiscountSource)
        assertEquals(24.75, row.currency?.exchangeRate)
    }

    @Test
    fun aMissingListRowPriceRefusesThePageRatherThanShowingAFreeOrder() = runTest {
        LIST_ROW_REQUIRED_MONEY.forEach { field ->
            refuses(field) { listed(pageWithFirstRow { it - field }) }
        }
    }

    @Test
    fun aMissingExchangeRateRefusesThePageRatherThanAssumingParity() = runTest {
        refuses("exchangeRate") {
            listed(pageWithFirstRow { row -> row + ("currency" to (row["currency"]!!.jsonObject - "exchangeRate")) })
        }
    }

    @Test
    fun everyDetailMoneyFieldArrivesWithItsLiteralValue() = runTest {
        val order = detailed(CAPTURED_ORDER)

        assertEquals(4380.00, order.totalPrice, 0.0)
        assertEquals(3650.00, order.originalSubtotal, 0.0)
        assertEquals(2, order.appliedDiscountSource)
        assertEquals(450.00, order.selectedPackages?.first()?.price)
    }

    @Test
    fun aMissingDetailPriceRefusesTheOrderRatherThanShowingAFreeOne() = runTest {
        DETAIL_REQUIRED_MONEY.forEach { field ->
            refuses(field) { detailed(withoutKey(CAPTURED_ORDER, field)) }
        }
    }

    @Test
    fun aMissingPackagePriceRefusesTheOrderRatherThanIncludingItForFree() = runTest {
        refuses("price") {
            detailed(
                mutating(CAPTURED_ORDER) { root ->
                    root + ("selectedPackages" to JsonArray(
                        root["selectedPackages"]!!.jsonArray.map { it.jsonObject - "price" },
                    ))
                },
            )
        }
    }

    @Test
    fun everyCancellationFeeFieldArrivesWithItsLiteralValue() = runTest {
        val quoted = preview(CAPTURED_PREVIEW)

        assertEquals(0.5, quoted?.feeRate)
        assertEquals(2190.00, quoted?.feeAmount)
        assertEquals(2190.00, quoted?.refundAmount)
        assertEquals(4380.00, quoted?.totalPrice)
        assertEquals(3, quoted?.tier)
    }

    /**
     * The cancellation quote is the one screen where a zero is actively attractive: a defaulted
     * `feeAmount` reads as a free cancellation, the customer confirms, and the real fee lands.
     */
    @Test
    fun aMissingCancellationFeeRefusesTheQuoteRatherThanPromisingAFreeCancellation() = runTest {
        PREVIEW_REQUIRED_MONEY.forEach { field ->
            refuses(field) { preview(withoutKey(CAPTURED_PREVIEW, field)) }
        }
    }

    @Test
    fun aMissingRefundAmountRefusesTheCancelReceiptRatherThanReportingNoRefund() = runTest {
        CANCEL_REQUIRED_MONEY.forEach { field ->
            refuses(field) { cancelled(withoutKey(CAPTURED_CANCEL, field)) }
        }
    }

    @Test
    fun everyCancelReceiptFieldArrivesWithItsLiteralValue() = runTest {
        val receipt = cancelled(CAPTURED_CANCEL)

        assertEquals(0.5, receipt?.feeRate)
        assertEquals(2190.00, receipt?.refundAmount)
        assertEquals(4380.00, receipt?.totalPrice)
    }

    // --- rule 2: booleans follow the money rule ---------------------------------

    @Test
    fun aMissingRefundInitiatedRefusesTheReceiptRatherThanReadingFalse() = runTest {
        refuses("refundInitiated") { cancelled(withoutKey(CAPTURED_CANCEL, "refundInitiated")) }
    }

    /**
     * `expressWaiverForfeitedOnCancel` is the only warning that cancelling burns a metered monthly
     * benefit. `false` deletes the warning from a sheet the customer is about to confirm.
     */
    @Test
    fun aMissingWaiverForfeitureWarningRefusesTheQuoteRatherThanDeletingTheWarning() = runTest {
        refuses("expressWaiverForfeitedOnCancel") {
            preview(withoutKey(CAPTURED_PREVIEW, "expressWaiverForfeitedOnCancel"))
        }
    }

    @Test
    fun anExplicitFalseForfeitureIsARealStateAndSurvives() = runTest {
        val quoted = preview(withKey(CAPTURED_PREVIEW, "expressWaiverForfeitedOnCancel", falseValue()))

        assertEquals(false, quoted?.expressWaiverForfeitedOnCancel)
        assertEquals(2190.00, quoted?.feeAmount)
    }

    @Test
    fun aMissingSpotAvailabilityRefusesTheListRatherThanReadingFalse() = runTest {
        refuses("hasAvailableSpots") { listed(pageWithFirstRow { it - "hasAvailableSpots" }) }
    }

    // --- rule 3: identity is refused, never synthesized --------------------------

    /**
     * Drop, not refuse: no customer surface sums this list — the paged `total` is the server's own
     * count and the tab badges count the rows actually shown — so a lost row cannot falsify a figure,
     * while refusing the page would hide every other order the server answered correctly. An id-less
     * row was already dead: the card navigates by id.
     */
    @Test
    fun anOrderWithoutAnIdIsDroppedAndTheRestOfTheListSurvives() = runTest {
        val page = listed(pageWithFirstRow { it - "id" })

        assertEquals(listOf("o-2"), page.data.map { it.id })
        assertEquals(2, page.total)
    }

    @Test
    fun everySurvivingOrderKeepsTheIdTheWireCarried() = runTest {
        assertEquals(listOf("o-1", "o-2"), listed(CAPTURED_PAGE).data.map { it.id })
    }

    @Test
    fun aDetailWithoutAnIdIsRefusedRatherThanRenderedAsAnOrderNothingCanActOn() = runTest {
        refuses("id") { detailed(withoutKey(CAPTURED_ORDER, "id")) }
    }

    // --- rule 4: collections do default -----------------------------------------

    @Test
    fun aCustomerWithNoOrdersGetsAnEmptyListRatherThanAnError() = runTest {
        val page = listed(withKey(CAPTURED_PAGE, "data", JsonArray(emptyList())))

        assertEquals(emptyList<OrderListItemDto>(), page.data)
        assertEquals(2, page.total)
    }

    @Test
    fun aPageWithNoDataKeyIsAnEmptyListRatherThanARefusal() = runTest {
        val page = listed(withoutKey(CAPTURED_PAGE, "data"))

        assertEquals(emptyList<OrderListItemDto>(), page.data)
        assertEquals(2, page.total)
    }

    @Test
    fun aPageWithAnExplicitNullDataListIsAnEmptyListRatherThanARefusal() = runTest {
        val page = listed(withKey(CAPTURED_PAGE, "data", JsonNull))

        assertEquals(emptyList<OrderListItemDto>(), page.data)
    }

    @Test
    fun anOrderWithNoPackagesOrServicesStillRenders() = runTest {
        val order = detailed(
            withoutKey(withoutKey(CAPTURED_ORDER, "selectedPackages"), "selectedServices"),
        )

        assertNull(order.selectedPackages)
        assertNull(order.selectedServices)
        assertEquals(4380.00, order.totalPrice, 0.0)
    }

    /**
     * `review = review?.toAppDto() ?: return null` refused every order nobody had reviewed yet,
     * which is most of them — the `?:` fired on the absent review, not on a broken one.
     */
    @Test
    fun anUnreviewedOrderStillOpens() = runTest {
        val order = detailed(withoutKey(CAPTURED_ORDER, "review"))

        assertNull(order.review)
        assertEquals(4380.00, order.totalPrice, 0.0)
    }

    @Test
    fun anOrderWhoseReviewHasNoRatingIsStillRefused() = runTest {
        refuses("rating") {
            detailed(
                mutating(CAPTURED_ORDER) { root ->
                    root + ("review" to (root["review"]!!.jsonObject - "rating"))
                },
            )
        }
    }

    // --- rule 5: nullable-by-design fields stay nullable ---------------------------

    /**
     * The three discount amounts are the only `nullable: true` money on these schemas, and each
     * absence is a real state. Zeroing them reads as "a 0 Kč discount applied", which is a different
     * sentence from "none applied" — and the summary decides whether to draw the row from exactly that.
     */
    @Test
    fun anOrderWithNoDiscountsKeepsThemNullRatherThanZero() = runTest {
        val row = listed(
            pageWithFirstRow { r -> NULLABLE_DISCOUNTS.fold(r) { acc, key -> acc - key } },
        ).data.first()

        assertNull(row.tierDiscountAmount)
        assertNull(row.membershipDiscountAmount)
        assertNull(row.promoDiscountAmount)
        assertEquals(4380.00, row.totalPrice, 0.0)
    }

    @Test
    fun anOrderWithDiscountsCarriesTheirLiteralValues() = runTest {
        val row = listed(CAPTURED_PAGE).data.first()

        assertEquals(146.00, row.tierDiscountAmount)
        assertEquals(292.00, row.membershipDiscountAmount)
        assertEquals(88.00, row.promoDiscountAmount)
    }

    @Test
    fun anExplicitNullDiscountSurvivesAsNull() = runTest {
        val row = listed(pageWithFirstRow { it + ("tierDiscountAmount" to JsonNull) }).data.first()

        assertNull(row.tierDiscountAmount)
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

    private fun falseValue() = kotlinx.serialization.json.JsonPrimitive(false)

    private operator fun JsonObject.minus(key: String) =
        JsonObject(toMutableMap().apply { remove(key) })

    private operator fun JsonObject.plus(entry: Pair<String, JsonElement>) =
        JsonObject(toMutableMap().apply { put(entry.first, entry.second) })

    @OptIn(ExperimentalSerializationApi::class)
    private fun serialNames(descriptor: SerialDescriptor): Set<String> =
        (0 until descriptor.elementsCount).map { descriptor.getElementName(it) }.toSet()

    private companion object {

        private const val CURRENCY = """
            {
              "id": "cur-1",
              "code": "CZK",
              "symbol": "Kč",
              "name": "Czech koruna",
              "exchangeRate": 24.75,
              "isDefault": true
            }
        """

        private fun listRow(id: String, price: Double) = """
            {
              "id": "$id",
              "customerName": "Ada Lovelace",
              "customerEmail": "ada@example.com",
              "customerPhone": "+420600000000",
              "customerAddress": "Vodickova 1, Praha 1",
              "customerAddressApproximate": "Praha 1, 110 xx",
              "displayOrderNumber": "CL-2026-0${id.last()}",
              "rooms": 3,
              "bathrooms": 2,
              "extras": { "inside-oven": true },
              "cleaningDateTime": "2026-08-12T09:00:00Z",
              "paymentType": { "type": "PaymentType", "name": "Card", "value": 2 },
              "paymentStatus": { "type": "PaymentStatus", "name": "Paid", "value": 2 },
              "totalPrice": $price,
              "originalSubtotal": 3650.00,
              "appliedDiscountSource": 2,
              "tierDiscountAmount": 146.00,
              "membershipDiscountAmount": 292.00,
              "promoDiscountAmount": 88.00,
              "estimatedTime": 240,
              "orderStatus": { "type": "OrderStatus", "name": "Confirmed", "value": 2 },
              "confirmationCode": "ABC123",
              "selectedPackages": [
                { "id": "pkg-1", "name": "Deep clean", "description": "Everything", "price": 450.00 }
              ],
              "currencyId": "cur-1",
              "currency": $CURRENCY,
              "assignedEmployees": ["emp-1"],
              "selectedServices": [
                {
                  "id": "svc-1",
                  "name": "Standard clean",
                  "description": "Rooms and baths",
                  "category": {
                    "id": "cat-1",
                    "slug": "home",
                    "name": "Home cleaning",
                    "description": "Homes and flats",
                    "displayOrder": 3
                  },
                  "basePrice": 2900.00,
                  "perRoomPrice": 180.00
                }
              ],
              "requiredEmployees": 2,
              "maxEmployees": 2,
              "availableSpots": 1,
              "assignedEmployeesCount": 1,
              "hasAvailableSpots": true,
              "estimatedCleanerPay": 1650.00,
              "customerAddressLatitude": 50.0805,
              "customerAddressLongitude": 14.4249
            }
        """

        /** Every member non-zero and non-default, including the three nullable-by-design discounts. */
        val CAPTURED_PAGE = """
            {
              "pageNumber": 1,
              "pageSize": 20,
              "total": 2,
              "data": [ ${listRow("o-1", 4380.00)}, ${listRow("o-2", 2640.00)} ]
            }
        """.trimIndent()

        val CAPTURED_ORDER = """
            {
              "id": "o-1",
              "displayOrderNumber": "CL-2026-0042",
              "customerName": "Ada Lovelace",
              "customerEmail": "ada@example.com",
              "customerPhone": "+420600000000",
              "address": {
                "street": "Vodickova 1",
                "city": "Praha",
                "zipCode": "11000",
                "country": "CZ",
                "latitude": 50.0805,
                "longitude": 14.4249
              },
              "rooms": 3,
              "bathrooms": 2,
              "extras": { "inside-oven": true },
              "cleaningDateTime": "2026-08-12T09:00:00Z",
              "paymentType": { "type": "PaymentType", "name": "Card", "value": 2 },
              "paymentStatus": { "type": "PaymentStatus", "name": "Paid", "value": 2 },
              "totalPrice": 4380.00,
              "originalSubtotal": 3650.00,
              "appliedDiscountSource": 2,
              "tierDiscountAmount": 146.00,
              "membershipDiscountAmount": 292.00,
              "promoDiscountAmount": 88.00,
              "estimatedTime": 240,
              "actualCompletionTime": 235,
              "completedAt": "2026-08-12T13:55:00Z",
              "completionNotes": "All done.",
              "orderStatus": { "type": "OrderStatus", "name": "Completed", "value": 5 },
              "confirmationCode": "ABC123",
              "notes": "Ring twice.",
              "specialInstructions": "Gate code 1234.",
              "accessInstructions": "Side gate, key box 4417.",
              "recurringTemplateId": "rec-9",
              "selectedPackages": [
                {
                  "id": "pkg-1",
                  "name": "Deep clean",
                  "description": "Everything",
                  "price": 450.00,
                  "estimatedTime": 120,
                  "currencyCode": "CZK",
                  "includedServices": ["svc-1"]
                }
              ],
              "currency": $CURRENCY,
              "selectedServices": [
                {
                  "id": "svc-1",
                  "name": "Standard clean",
                  "description": "Rooms and baths",
                  "estimatedTime": 180,
                  "currencyCode": "CZK"
                }
              ],
              "statusHistory": [
                { "status": { "type": "OrderStatus", "name": "Confirmed", "value": 2 },
                  "createdOn": "2026-08-10T08:00:00Z" }
              ],
              "createdOn": "2026-08-09T18:20:00Z",
              "updatedOn": "2026-08-12T13:55:00Z",
              "assignedEmployees": [
                { "id": "a-1", "employeeId": "emp-1", "fullName": "Jana Novak", "phoneNumber": "+420700000000" }
              ],
              "receiptNumber": "R-2026-0042",
              "orderNotes": [
                { "id": "n-1", "employeeId": "emp-1", "content": "Oven cleaned.",
                  "createdOn": "2026-08-12T13:00:00Z" }
              ],
              "orderIssues": [
                { "id": "i-1", "reportedByEmployeeId": "emp-1", "description": "Broken tap",
                  "isResolved": true, "resolvedAt": "2026-08-12T13:30:00Z",
                  "createdOn": "2026-08-12T12:00:00Z" }
              ],
              "review": {
                "id": "rev-1",
                "orderId": "o-1",
                "rating": 5,
                "comment": "Spotless.",
                "createdOn": "2026-08-13T09:00:00Z",
                "updatedOn": "2026-08-13T09:05:00Z"
              },
              "requiredEmployees": 2,
              "maxEmployees": 2,
              "availableSpots": 1,
              "assignedEmployeesCount": 1,
              "hasAvailableSpots": true,
              "isAssignedToCurrentUser": true,
              "hasAfterPhotos": true
            }
        """.trimIndent()

        val CAPTURED_CANCEL = """
            {
              "orderId": "o-1",
              "feeRate": 0.5,
              "refundAmount": 2190.00,
              "totalPrice": 4380.00,
              "refundInitiated": true
            }
        """.trimIndent()

        val CAPTURED_PREVIEW = """
            {
              "orderId": "o-1",
              "tier": 3,
              "feeRate": 0.5,
              "feeAmount": 2190.00,
              "refundAmount": 2190.00,
              "totalPrice": 4380.00,
              "currencyCode": "CZK",
              "expressWaiverForfeitedOnCancel": true
            }
        """.trimIndent()

        val LIST_ITEM_SPEC_PROPERTIES = setOf(
            "id",
            "customerName",
            "customerEmail",
            "customerPhone",
            "customerAddress",
            "customerAddressApproximate",
            "displayOrderNumber",
            "rooms",
            "bathrooms",
            "extras",
            "cleaningDateTime",
            "paymentType",
            "paymentStatus",
            "totalPrice",
            "originalSubtotal",
            "appliedDiscountSource",
            "tierDiscountAmount",
            "membershipDiscountAmount",
            "promoDiscountAmount",
            "estimatedTime",
            "orderStatus",
            "confirmationCode",
            "selectedPackages",
            "currencyId",
            "currency",
            "assignedEmployees",
            "selectedServices",
            "requiredEmployees",
            "maxEmployees",
            "availableSpots",
            "assignedEmployeesCount",
            "hasAvailableSpots",
            "estimatedCleanerPay",
            "customerAddressLatitude",
            "customerAddressLongitude",
        )

        val CURRENCY_SPEC_PROPERTIES = setOf("id", "code", "symbol", "name", "exchangeRate", "isDefault")

        val CANCEL_SPEC_PROPERTIES =
            setOf("orderId", "feeRate", "refundAmount", "totalPrice", "refundInitiated")

        val PREVIEW_SPEC_PROPERTIES = setOf(
            "orderId",
            "tier",
            "feeRate",
            "feeAmount",
            "refundAmount",
            "totalPrice",
            "currencyCode",
            "expressWaiverForfeitedOnCancel",
        )

        val LIST_ROW_REQUIRED_MONEY = listOf("totalPrice", "originalSubtotal", "appliedDiscountSource")

        val DETAIL_REQUIRED_MONEY = listOf("totalPrice", "originalSubtotal", "appliedDiscountSource")

        val CANCEL_REQUIRED_MONEY = listOf("feeRate", "refundAmount", "totalPrice")

        val PREVIEW_REQUIRED_MONEY = listOf("feeRate", "feeAmount", "refundAmount", "totalPrice")

        val NULLABLE_DISCOUNTS =
            listOf("tierDiscountAmount", "membershipDiscountAmount", "promoDiscountAmount")
    }
}
