package cz.cleansia.customer.core.referral

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
import cz.cleansia.customer.api.client.ReferralApi as GenReferralApi
import cz.cleansia.customer.api.model.GetMyReferralResponse as GenGetMyReferralResponse
import cz.cleansia.customer.api.model.GetMyReferralsReferralListItem as GenReferralListItem
import cz.cleansia.customer.api.model.ValidateReferralResponse as GenValidateReferralResponse

/**
 * Referrals are money one person is owed by another's booking: the counters on the Rewards tab are
 * what the customer reads as "the programme owes me this much", and `status` is the per-invite payout
 * state behind them. No referral schema declares a `required` array, so the generator types every
 * property optional-with-null regardless of `nullable: false`.
 */
class ReferralWireTest {

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
        call: suspend (ReferralApi) -> T,
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
            val api = ReferralApi(
                Retrofit.Builder()
                    .baseUrl(server.url("/"))
                    .addConverterFactory(json.asConverterFactory("application/json".toMediaType()))
                    .build()
                    .create(GenReferralApi::class.java),
            )
            call(api).also { onRequest(server.takeRequest()) }
        } finally {
            server.shutdown()
        }
    }

    private suspend fun account(body: String) = serving(body) { it.getMy() }.body()

    private suspend fun referrals(body: String) = serving(body) { it.getMyReferrals(0, 20) }.body()

    private suspend fun validation(body: String) =
        serving(body) { it.validate(ValidateReferralRequest("FRIEND10")) }.body()

    private suspend fun loadedAccount(body: String): ReferralAccountDto {
        val dto = account(body)
        assertNotNull("expected the captured payload to map", dto)
        return dto!!
    }

    // --- the field-name contract ------------------------------------------------

    @Test
    fun referralDtoSerialNamesAreExactlyTheSpecProperties() {
        assertEquals(ACCOUNT_SPEC_PROPERTIES, serialNames(GenGetMyReferralResponse.serializer().descriptor))
        assertEquals(LIST_ITEM_SPEC_PROPERTIES, serialNames(GenReferralListItem.serializer().descriptor))
        assertEquals(VALIDATE_SPEC_PROPERTIES, serialNames(GenValidateReferralResponse.serializer().descriptor))
    }

    @Test
    fun theRequestsKeepThePathsTheServerBinds() = runTest {
        var path: String? = null
        serving(CAPTURED_ACCOUNT, onRequest = { path = it.path }) { it.getMy() }
        assertEquals("/api/Referral/GetMy", path)

        serving(CAPTURED_REFERRALS, onRequest = { path = it.path }) { it.getMyReferrals(0, 20) }
        assertEquals("/api/Referral/GetMyReferrals?offset=0&limit=20", path)
    }

    // --- rule 1: money and quantities are never coerced --------------------------

    @Test
    fun everyAccountCounterArrivesWithItsLiteralValue() = runTest {
        val dto = loadedAccount(CAPTURED_ACCOUNT)

        assertEquals("R7QK2M", dto.code)
        assertEquals(7, dto.timesUsed)
        assertEquals(3, dto.qualifiedCount)
        assertEquals(5, dto.acceptedCount)
        assertEquals(200, dto.pointsPerReferral)
    }

    /**
     * Zero here is the programme reporting that it owes the customer nothing — "nobody you invited
     * ever signed up" over five people who did.
     */
    @Test
    fun aMissingCounterRefusesTheAccountRatherThanSayingNobodyJoined() = runTest {
        ACCOUNT_REQUIRED_NUMBERS.forEach { field ->
            assertNull(
                "a missing $field must refuse the account rather than report nothing earned",
                account(withoutKey(CAPTURED_ACCOUNT, field)),
            )
        }
    }

    /**
     * The invite card is gated on `code.isNotBlank()`, so a blanked code does not render as an error
     * — it removes the whole feature from the screen.
     */
    @Test
    fun aMissingCodeRefusesTheAccountRatherThanHidingTheInviteCard() = runTest {
        assertNull(account(withoutKey(CAPTURED_ACCOUNT, "code")))
    }

    @Test
    fun everyPagedCounterArrivesWithItsLiteralValue() = runTest {
        val page = referrals(CAPTURED_REFERRALS)

        assertEquals(1, page?.pageNumber)
        assertEquals(20, page?.pageSize)
        assertEquals(2, page?.total)
        assertEquals(120, page?.data?.first()?.pointsAwardedToReferrer)
    }

    @Test
    fun aMissingPagedCounterRefusesThePage() = runTest {
        listOf("pageNumber", "pageSize", "total").forEach { field ->
            assertNull(
                "a missing $field must refuse the page",
                referrals(withoutKey(CAPTURED_REFERRALS, field)),
            )
        }
    }

    // --- rule 2: booleans follow the money rule ---------------------------------

    @Test
    fun theValidationVerdictArrivesWithItsLiteralValue() = runTest {
        val dto = validation(CAPTURED_VALIDATION)

        assertEquals(true, dto?.isValid)
        assertEquals("Petra", dto?.referrerFirstName)
    }

    /**
     * `false` is the answer that rejects a code, so a default costs both sides the joining bonus and
     * states a reason the server never gave.
     */
    @Test
    fun aMissingVerdictRefusesTheValidationRatherThanRejectingTheCode() = runTest {
        assertNull(validation(withoutKey(CAPTURED_VALIDATION, "isValid")))
    }

    // --- rule 3: identity is refused, never synthesized --------------------------

    /**
     * The page is refused rather than the row dropped, because this list is the itemisation of the
     * accepted/qualified counters rendered beside it — and those come from `GetMy`, computed over
     * every referral rather than over this page.
     */
    @Test
    fun aReferralWithoutAnIdRefusesThePageRatherThanVanishingFromTheBreakdown() = runTest {
        assertNull(referrals(referralsWithFirstRow { it - "id" }))
    }

    @Test
    fun aMissingStatusRefusesThePageRatherThanReportingAnUnpaidInvite() = runTest {
        assertNull(referrals(referralsWithFirstRow { it - "status" }))
    }

    @Test
    fun eachReferralKeepsTheStatusTheServerSent() = runTest {
        val page = referrals(CAPTURED_REFERRALS)

        assertEquals(ReferralStatus.Qualified.value, page?.data?.first()?.status)
        assertEquals(ReferralStatus.Reversed.value, page?.data?.last()?.status)
    }

    // --- rule 4: collections do default -----------------------------------------

    @Test
    fun aCustomerWhoHasInvitedNobodyStillLoadsThePage() = runTest {
        val page = referrals(withKey(CAPTURED_REFERRALS, "data", JsonArray(emptyList())))

        assertEquals(emptyList<ReferralListItemDto>(), page?.data)
        assertEquals(2, page?.total)
    }

    // --- rule 5: nullable-by-design fields stay nullable ---------------------------

    /**
     * `pointsAwardedToReferrer` and `firstQualifyingOrderOn` are `nullable: true`: a referral that
     * has not qualified genuinely has no award and no qualifying order.
     */
    @Test
    fun aReferralThatHasNotQualifiedYetKeepsItsNulls() = runTest {
        val page = referrals(
            referralsWithFirstRow { (it - "pointsAwardedToReferrer") - "firstQualifyingOrderOn" },
        )

        assertNull(page?.data?.first()?.pointsAwardedToReferrer)
        assertNull(page?.data?.first()?.firstQualifyingOrderOn)
        assertEquals(2, page?.total)
    }

    @Test
    fun anExplicitNullAwardSurvivesAsNull() = runTest {
        val page = referrals(referralsWithFirstRow { it + ("pointsAwardedToReferrer" to JsonNull) })

        assertNull(page?.data?.first()?.pointsAwardedToReferrer)
    }

    // --- the refused body ---------------------------------------------------------

    /**
     * A body-less 2xx used to produce a fully invented account — code `""`, every counter zero —
     * because the mapper hung off a nullable receiver and supplied a value for each field.
     */
    @Test
    fun aBodylessSuccessIsRefusedRatherThanFabricated() = runTest {
        assertNull(serving("", code = 204) { it.getMy() }.body())
        assertNull(serving("", code = 204) { it.getMyReferrals(0, 20) }.body())
        assertNull(serving("", code = 204) { it.validate(ValidateReferralRequest("FRIEND10")) }.body())
    }

    // --- payload plumbing ---------------------------------------------------------

    private fun mutating(body: String, transform: (JsonObject) -> JsonObject): String =
        transform(Json.parseToJsonElement(body).jsonObject).toString()

    private fun withoutKey(body: String, key: String): String = mutating(body) { it - key }

    private fun withKey(body: String, key: String, value: JsonElement): String =
        mutating(body) { it + (key to value) }

    private fun referralsWithFirstRow(transform: (JsonObject) -> JsonObject): String =
        mutating(CAPTURED_REFERRALS) { root ->
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

        /** Every member non-zero and non-default, so a forgotten field cannot pass as a mapped one. */
        val CAPTURED_ACCOUNT = """
            {
              "code": "R7QK2M",
              "timesUsed": 7,
              "qualifiedCount": 3,
              "acceptedCount": 5,
              "pointsPerReferral": 200
            }
        """.trimIndent()

        val CAPTURED_REFERRALS = """
            {
              "pageNumber": 1,
              "pageSize": 20,
              "total": 2,
              "data": [
                {
                  "id": "ref-1",
                  "referredFirstName": "Anna",
                  "status": 2,
                  "acceptedOn": "2026-06-02T10:00:00Z",
                  "firstQualifyingOrderOn": "2026-06-11T09:30:00Z",
                  "pointsAwardedToReferrer": 120
                },
                {
                  "id": "ref-2",
                  "referredFirstName": "Marek",
                  "status": 4,
                  "acceptedOn": "2026-06-04T18:20:00Z",
                  "firstQualifyingOrderOn": "2026-06-14T08:00:00Z",
                  "pointsAwardedToReferrer": 200
                }
              ]
            }
        """.trimIndent()

        val CAPTURED_VALIDATION = """
            {
              "isValid": true,
              "referrerFirstName": "Petra",
              "errorCode": "NotFound"
            }
        """.trimIndent()

        val ACCOUNT_SPEC_PROPERTIES = setOf(
            "code",
            "timesUsed",
            "qualifiedCount",
            "acceptedCount",
            "pointsPerReferral",
        )

        val LIST_ITEM_SPEC_PROPERTIES = setOf(
            "id",
            "referredFirstName",
            "status",
            "acceptedOn",
            "firstQualifyingOrderOn",
            "pointsAwardedToReferrer",
        )

        val VALIDATE_SPEC_PROPERTIES = setOf("isValid", "referrerFirstName", "errorCode")

        val ACCOUNT_REQUIRED_NUMBERS =
            listOf("timesUsed", "qualifiedCount", "acceptedCount", "pointsPerReferral")
    }
}
