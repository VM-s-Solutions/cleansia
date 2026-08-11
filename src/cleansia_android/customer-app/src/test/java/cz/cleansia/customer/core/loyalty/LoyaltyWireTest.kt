package cz.cleansia.customer.core.loyalty

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
import org.junit.Test
import retrofit2.Retrofit
import retrofit2.converter.kotlinx.serialization.asConverterFactory
import cz.cleansia.customer.api.client.LoyaltyApi as GenLoyaltyApi
import cz.cleansia.customer.api.model.GetMyLoyaltyResponse as GenGetMyLoyaltyResponse

/**
 * Loyalty is a standing promise about money — "every booking from now on is 8 % off" — plus the ledger
 * that explains how the customer earned it. `GetMyLoyalty_Response` declares no `required` array, so
 * the generator types every property optional-with-null regardless of `nullable: false`.
 */
class LoyaltyWireTest {

    private val json = Json {
        ignoreUnknownKeys = true
        isLenient = true
        explicitNulls = false
        serializersModule = IntEnumSerializersModule
    }

    private suspend fun <T> serving(
        body: String,
        onRequest: (RecordedRequest) -> Unit = {},
        call: suspend (LoyaltyApi) -> T,
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
            val api = LoyaltyApi(
                Retrofit.Builder()
                    .baseUrl(server.url("/"))
                    .addConverterFactory(json.asConverterFactory("application/json".toMediaType()))
                    .build()
                    .create(GenLoyaltyApi::class.java),
            )
            call(api).also { onRequest(server.takeRequest()) }
        } finally {
            server.shutdown()
        }
    }

    private suspend fun account(body: String) = serving(body) { it.getMy() }.body()

    private suspend fun tiers(body: String) = serving(body) { it.getTiers() }.body()

    private suspend fun activity(body: String) = serving(body) { it.getActivity(0, 20) }.body()

    private suspend fun loadedAccount(body: String): LoyaltyAccountDto {
        val dto = account(body)
        assertNotNull("expected the captured payload to map", dto)
        return dto!!
    }

    // --- the field-name contract ------------------------------------------------

    @Test
    fun loyaltyDtoSerialNamesAreExactlyTheSpecProperties() {
        assertEquals(ACCOUNT_SPEC_PROPERTIES, serialNames(GenGetMyLoyaltyResponse.serializer().descriptor))
    }

    @Test
    fun theRequestsKeepThePathsTheServerBinds() = runTest {
        var path: String? = null
        serving(CAPTURED_ACCOUNT, onRequest = { path = it.path }) { it.getMy() }
        assertEquals("/api/Loyalty/GetMy", path)

        serving(CAPTURED_TIERS, onRequest = { path = it.path }) { it.getTiers() }
        assertEquals("/api/Loyalty/GetTiers", path)

        serving(CAPTURED_ACTIVITY, onRequest = { path = it.path }) { it.getActivity(0, 20) }
        assertEquals("/api/Loyalty/GetActivity?offset=0&limit=20", path)
    }

    // --- rule 1: money is never coerced -----------------------------------------

    @Test
    fun theStandingDiscountArrivesWithItsLiteralValue() = runTest {
        val dto = loadedAccount(CAPTURED_ACCOUNT)

        assertEquals(8.0, dto.currentDiscountPercent, 0.0)
        assertEquals(1500.0, dto.currentDiscountMinOrderAmount)
    }

    @Test
    fun aMissingDiscountRefusesTheAccountRatherThanSayingTheTierIsWorthNothing() = runTest {
        assertNull(account(withoutKey(CAPTURED_ACCOUNT, "currentDiscountPercent")))
    }

    /**
     * The tier and the two counters are refused with the discount because they are what explains it: a
     * defaulted `currentTier = 1` demotes a Gold member on screen and `lifetimePoints = 0` erases the
     * progress bar under it.
     */
    @Test
    fun aMissingTierOrCounterRefusesTheAccountRatherThanDemotingTheCustomer() = runTest {
        ACCOUNT_REQUIRED_NUMBERS.forEach { field ->
            assertNull(
                "a missing $field must refuse the account rather than demote the customer",
                account(withoutKey(CAPTURED_ACCOUNT, field)),
            )
        }
    }

    @Test
    fun everyAccountCounterArrivesWithItsLiteralValue() = runTest {
        val dto = loadedAccount(CAPTURED_ACCOUNT)

        assertEquals(3, dto.currentTier)
        assertEquals(2450, dto.lifetimePoints)
        assertEquals(17, dto.completedBookingsCount)
    }

    @Test
    fun aMissingTierThresholdOrDiscountRefusesTheWholeLadder() = runTest {
        listOf("lifetimePointsThreshold", "discountPercent", "tier").forEach { field ->
            assertNull(
                "a ladder with a rung missing reads as a complete one; $field must refuse it",
                tiers(tiersWithFirstRow { it - field }),
            )
        }
    }

    @Test
    fun aMissingActivityPointsRefusesTheLedgerRatherThanReportingNothingEarned() = runTest {
        assertNull(activity(activityWithFirstRow { it - "points" }))
    }

    @Test
    fun everyLedgerLineArrivesWithItsLiteralValue() = runTest {
        val page = activity(CAPTURED_ACTIVITY)

        assertEquals(2, page?.total)
        assertEquals(140, page?.data?.first()?.points)
        assertEquals(-60, page?.data?.last()?.points)
    }

    // --- rule 2: booleans follow the money rule ---------------------------------
    //
    // No loyalty schema carries a `nullable: false` boolean; the rule is exercised on the quote,
    // order and membership wires.

    // --- rule 3: identity is refused, never synthesized --------------------------

    @Test
    fun anActivityLineWithoutItsTypeOrSourceRefusesTheLedger() = runTest {
        listOf("type", "source").forEach { field ->
            assertNull(activity(activityWithFirstRow { it - field }))
        }
    }

    // --- rule 4: collections do default -----------------------------------------

    @Test
    fun aCustomerWithNoPerksOrActivityStillLoads() = runTest {
        val dto = loadedAccount(withoutKey(CAPTURED_ACCOUNT, "currentPerks"))

        assertEquals(emptyList<TierPerkDto>(), dto.currentPerks)
        assertEquals(8.0, dto.currentDiscountPercent, 0.0)
    }

    @Test
    fun anEmptyLedgerPageIsARenderableState() = runTest {
        val page = activity(withKey(CAPTURED_ACTIVITY, "data", JsonArray(emptyList())))

        assertEquals(emptyList<LoyaltyActivityItemDto>(), page?.data)
        assertEquals(2, page?.total)
    }

    // --- rule 5: nullable-by-design fields stay nullable ---------------------------

    /**
     * The top tier has no next tier and an unconditional discount has no floor, so both stay null
     * rather than becoming a zero the progress bar would divide by.
     */
    @Test
    fun aTopTierCustomerHasNoNextTierAndNoDiscountFloor() = runTest {
        val dto = loadedAccount(
            withoutKey(withoutKey(CAPTURED_ACCOUNT, "pointsToNextTier"), "currentDiscountMinOrderAmount"),
        )

        assertNull(dto.pointsToNextTier)
        assertNull(dto.currentDiscountMinOrderAmount)
        assertEquals(8.0, dto.currentDiscountPercent, 0.0)
    }

    @Test
    fun anExplicitNullDiscountFloorSurvivesAsNull() = runTest {
        val dto = loadedAccount(withKey(CAPTURED_ACCOUNT, "currentDiscountMinOrderAmount", JsonNull))

        assertNull(dto.currentDiscountMinOrderAmount)
    }

    // --- payload plumbing ---------------------------------------------------------

    private fun mutating(body: String, transform: (JsonObject) -> JsonObject): String =
        transform(Json.parseToJsonElement(body).jsonObject).toString()

    private fun withoutKey(body: String, key: String): String = mutating(body) { it - key }

    private fun withKey(body: String, key: String, value: JsonElement): String =
        mutating(body) { it + (key to value) }

    private fun tiersWithFirstRow(transform: (JsonObject) -> JsonObject): String =
        mutating(CAPTURED_TIERS) { root ->
            val rows = root["tiers"]!!.jsonArray.mapIndexed { index, row ->
                if (index == 0) transform(row.jsonObject) else row
            }
            root + ("tiers" to JsonArray(rows))
        }

    private fun activityWithFirstRow(transform: (JsonObject) -> JsonObject): String =
        mutating(CAPTURED_ACTIVITY) { root ->
            val rows = root["data"]!!.jsonArray.mapIndexed { index, row ->
                if (index == 0) transform(row.jsonObject) else row
            }
            root + ("data" to JsonArray(rows))
        }

    private operator fun JsonObject.minus(key: String) =
        JsonObject(toMutableMap().apply { remove(key) })

    private operator fun JsonObject.plus(entry: Pair<String, JsonElement>) =
        JsonObject(toMutableMap().apply { put(entry.first, entry.second) })

    @OptIn(ExperimentalSerializationApi::class)
    private fun serialNames(descriptor: SerialDescriptor): Set<String> =
        (0 until descriptor.elementsCount).map { descriptor.getElementName(it) }.toSet()

    private companion object {

        /** Every member non-zero and non-default, including both nullable-by-design figures. */
        val CAPTURED_ACCOUNT = """
            {
              "currentTier": 3,
              "lifetimePoints": 2450,
              "completedBookingsCount": 17,
              "tierAchievedOn": "2026-05-02T10:00:00Z",
              "pointsToNextTier": 550,
              "nextTier": 4,
              "currentDiscountPercent": 8.0,
              "currentDiscountMinOrderAmount": 1500.0,
              "currentPerks": [
                { "icon": "star", "labelKey": "loyalty.perk.priority" },
                { "icon": "gift", "labelKey": "loyalty.perk.birthday" }
              ]
            }
        """.trimIndent()

        val CAPTURED_TIERS = """
            {
              "tiers": [
                {
                  "tier": 1,
                  "lifetimePointsThreshold": 0,
                  "discountPercent": 2.0,
                  "minimumOrderAmountForDiscount": 800.0,
                  "perks": [ { "icon": "star", "labelKey": "loyalty.perk.basic" } ]
                },
                {
                  "tier": 3,
                  "lifetimePointsThreshold": 2000,
                  "discountPercent": 8.0,
                  "minimumOrderAmountForDiscount": 1500.0,
                  "perks": [ { "icon": "gift", "labelKey": "loyalty.perk.birthday" } ]
                }
              ]
            }
        """.trimIndent()

        val CAPTURED_ACTIVITY = """
            {
              "pageNumber": 1,
              "pageSize": 20,
              "total": 2,
              "data": [
                {
                  "type": 1,
                  "points": 140,
                  "source": 2,
                  "orderId": "o-1",
                  "orderDisplayNumber": "CL-2026-0042",
                  "occurredOn": "2026-08-12T14:00:00Z"
                },
                {
                  "type": 2,
                  "points": -60,
                  "source": 1,
                  "orderId": "o-2",
                  "orderDisplayNumber": "CL-2026-0043",
                  "occurredOn": "2026-08-13T09:00:00Z"
                }
              ]
            }
        """.trimIndent()

        val ACCOUNT_SPEC_PROPERTIES = setOf(
            "currentTier",
            "lifetimePoints",
            "completedBookingsCount",
            "tierAchievedOn",
            "pointsToNextTier",
            "nextTier",
            "currentDiscountPercent",
            "currentDiscountMinOrderAmount",
            "currentPerks",
        )

        val ACCOUNT_REQUIRED_NUMBERS =
            listOf("currentTier", "lifetimePoints", "completedBookingsCount")
    }
}
