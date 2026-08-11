package cz.cleansia.customer.core.memberships

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
import kotlinx.serialization.json.JsonPrimitive
import kotlinx.serialization.json.jsonArray
import kotlinx.serialization.json.jsonObject
import okhttp3.MediaType.Companion.toMediaType
import okhttp3.mockwebserver.MockResponse
import okhttp3.mockwebserver.MockWebServer
import okhttp3.mockwebserver.RecordedRequest
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNotNull
import org.junit.Assert.assertTrue
import org.junit.Assert.assertNull
import org.junit.Test
import retrofit2.Retrofit
import retrofit2.converter.kotlinx.serialization.asConverterFactory
import cz.cleansia.customer.api.client.MembershipApi as GenMembershipApi
import cz.cleansia.customer.api.model.GetMembershipPlansResponse as GenGetMembershipPlansResponse
import cz.cleansia.customer.api.model.GetMyMembershipResponse as GenGetMyMembershipResponse

/**
 * Cleansia Plus: the plan card is a subscription the customer is about to buy, and `GetMine` is the
 * answer every discount, cancellation-window and express-waiver decision reads. Neither schema
 * declares a `required` array, so the generator types every property optional-with-null regardless of
 * `nullable: false`.
 */
class MembershipWireTest {

    private val json = Json {
        ignoreUnknownKeys = true
        isLenient = true
        explicitNulls = false
        serializersModule = IntEnumSerializersModule
    }

    private suspend fun <T> serving(
        body: String,
        onRequest: (RecordedRequest) -> Unit = {},
        call: suspend (MembershipApi) -> T,
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
            val api = MembershipApi(
                Retrofit.Builder()
                    .baseUrl(server.url("/"))
                    .addConverterFactory(json.asConverterFactory("application/json".toMediaType()))
                    .build()
                    .create(GenMembershipApi::class.java),
            )
            call(api).also { onRequest(server.takeRequest()) }
        } finally {
            server.shutdown()
        }
    }

    private suspend fun plans(body: String) = serving(body) { it.getPlans() }.body()

    private suspend fun mine(body: String) = serving(body) { it.getMine() }.body()

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

    private suspend fun loadedPlans(body: String): List<MembershipPlanDto> {
        val list = plans(body)
        assertNotNull("expected the captured payload to map", list)
        return list!!
    }

    // --- the field-name contract ------------------------------------------------

    @Test
    fun planDtoSerialNamesAreExactlyTheSpecProperties() {
        assertEquals(PLAN_SPEC_PROPERTIES, serialNames(GenGetMembershipPlansResponse.serializer().descriptor))
    }

    @Test
    fun myMembershipDtoSerialNamesAreExactlyTheSpecProperties() {
        assertEquals(MINE_SPEC_PROPERTIES, serialNames(GenGetMyMembershipResponse.serializer().descriptor))
    }

    @Test
    fun theRequestsKeepThePathsTheServerBinds() = runTest {
        var path: String? = null
        serving(CAPTURED_PLANS, onRequest = { path = it.path }) { it.getPlans() }
        assertEquals("/api/Membership/GetPlans", path)

        serving(CAPTURED_MINE, onRequest = { path = it.path }) { it.getMine() }
        assertEquals("/api/Membership/GetMine", path)
    }

    // --- rule 1: money is never coerced -----------------------------------------

    @Test
    fun everyPlanNumberArrivesWithItsLiteralValue() = runTest {
        val plan = loadedPlans(CAPTURED_PLANS).first()

        assertEquals(2030.00, plan.price, 0.0)
        assertEquals(169.17, plan.monthlyEquivalentPrice, 0.0)
        assertEquals(12.0, plan.discountPercentage, 0.0)
        assertEquals(18.5, plan.savingsPercentVsMonthly, 0.0)
    }

    @Test
    fun aMissingPlanPriceRefusesTheListRatherThanSellingASubscriptionForNothing() = runTest {
        PLAN_REQUIRED_MONEY.forEach { field ->
            refuses(field) { plans(plansWithFirstRow { it - field }) }
        }
    }

    /**
     * The sharpest one: `billingInterval` defaulting to `1` (monthly) reframes an annual plan's
     * 2 030 Kč as a monthly charge, and the hero renders "per month" off exactly this field.
     */
    @Test
    fun aMissingBillingIntervalRefusesThePlanRatherThanCallingItMonthly() = runTest {
        refuses("billingInterval") { plans(plansWithFirstRow { it - "billingInterval" }) }
    }

    @Test
    fun aMissingTrialLengthRefusesThePlanRatherThanDeletingTheFreeTrial() = runTest {
        refuses("trialPeriodDays") { plans(plansWithFirstRow { it - "trialPeriodDays" }) }
    }

    @Test
    fun aMissingCancellationWindowRefusesThePlanRatherThanPromisingNone() = runTest {
        refuses("freeCancellationWindowHours") {
            plans(plansWithFirstRow { it - "freeCancellationWindowHours" })
        }
    }

    /**
     * A 200 with no plan list in it is not "Plus is unavailable today" — defaulted to empty it took
     * the subscribe CTA off the screen with nothing saying why.
     */
    @Test
    fun aBodylessPlanListRefusesRatherThanDeletingTheSubscribeCta() = runTest {
        refuses("GetMembershipPlansResponse") {
            val server = MockWebServer()
            server.start()
            try {
                server.enqueue(MockResponse().setResponseCode(204))
                MembershipApi(
                    Retrofit.Builder()
                        .baseUrl(server.url("/"))
                        .addConverterFactory(json.asConverterFactory("application/json".toMediaType()))
                        .build()
                        .create(GenMembershipApi::class.java),
                ).getPlans()
            } finally {
                server.shutdown()
            }
        }
    }

    // --- rule 2: booleans follow the money rule ---------------------------------

    @Test
    fun aMissingExpressUpgradeFlagRefusesThePlanRatherThanDeletingTheBenefit() = runTest {
        refuses("allowsExpressUpgrade") { plans(plansWithFirstRow { it - "allowsExpressUpgrade" }) }
    }

    /**
     * `hasMembership = false` hides the discount from a customer who is paying for it, and
     * `cancelRequested = false` hides "your plan ends on the 3rd" from one who asked to cancel.
     */
    @Test
    fun aMissingMembershipVerdictRefusesTheAnswerRatherThanReadingNotAMember() = runTest {
        refuses("hasMembership") { mine(withoutKey(CAPTURED_MINE, "hasMembership")) }
        refuses("cancelRequested") { mine(withoutKey(CAPTURED_MINE, "cancelRequested")) }
    }

    @Test
    fun anExplicitFalseMembershipIsARealStateAndSurvives() = runTest {
        val answer = mine(withKey(CAPTURED_MINE, "hasMembership", JsonPrimitive(false)))

        assertEquals(false, answer?.hasMembership)
    }

    // --- rule 3: identity is refused, never synthesized --------------------------

    @Test
    fun aPlanWithoutItsCodeIsRefusedBecauseTheCodeIsWhatSubscribeSends() = runTest {
        refuses("code") { plans(plansWithFirstRow { it - "code" }) }
    }

    /**
     * `MembershipStatus?` in C#, and the handler's non-member branch sends it null beside every other
     * plan field. Refusing on it turned "you are not a member yet" into an error screen for everyone
     * who has never subscribed — the exact audience the upgrade CTA exists for.
     */
    @Test
    fun aCustomerWhoHasNeverSubscribedGetsTheNonMemberAnswerRatherThanAnError() = runTest {
        val answer = mine(CAPTURED_NON_MEMBER)

        assertEquals(false, answer?.hasMembership)
        assertNull(answer?.status)
        assertNull(answer?.planCode)
        assertEquals(false, answer?.cancelRequested)
    }

    @Test
    fun everySurvivingPlanKeepsTheCodeTheWireCarried() = runTest {
        assertEquals(listOf("plus-annual", "plus-monthly"), loadedPlans(CAPTURED_PLANS).map { it.code })
    }

    // --- rule 4: collections do default -----------------------------------------

    @Test
    fun aTenantWithNoPlansGetsAnEmptyListRatherThanARefusal() = runTest {
        assertEquals(emptyList<MembershipPlanDto>(), plans("[]"))
    }

    // --- rule 5: nullable-by-design fields stay nullable ---------------------------

    /**
     * Everything on `GetMine` except the two verdicts and the status is `nullable: true` by design —
     * a customer who is not a member has no plan, no price and no renewal date.
     */
    @Test
    fun aNonMemberKeepsEveryPlanFieldNullWithoutRefusingTheAnswer() = runTest {
        val answer = mine(
            MINE_NULLABLE_FIELDS.fold(CAPTURED_MINE) { body, key -> withoutKey(body, key) },
        )

        assertNotNull(answer)
        assertNull(answer?.planCode)
        assertNull(answer?.monthlyPriceCzk)
        assertNull(answer?.expressUpgradesRemaining)
        assertNull(answer?.trialEndsAtUtc)
        assertEquals(true, answer?.hasMembership)
    }

    @Test
    fun aMemberCarriesTheirLiteralPlanFigures() = runTest {
        val answer = mine(CAPTURED_MINE)

        assertEquals("plus-annual", answer?.planCode)
        assertEquals(169.17, answer?.monthlyPriceCzk)
        assertEquals(12.0, answer?.discountPercentage)
        assertEquals(2, answer?.expressUpgradesRemaining)
    }

    // --- the Stripe credentials and the two non-nullable period ends ---------------

    @Test
    fun everySubscribeCredentialArrivesWithItsLiteralValue() = runTest {
        val started = serving(CAPTURED_SUBSCRIBE) {
            it.subscribe(CreateMembershipSubscriptionRequest(planCode = "plus-annual"))
        }.body()!!

        assertEquals("seti_1ABC_secret_XYZ", started.setupIntentClientSecret)
        assertEquals("cus_ABC123", started.stripeCustomerId)
        assertEquals("ek_test_ABC", started.ephemeralKey)
    }

    /**
     * A blanked client secret is not a display bug: the PaymentSheet opens, fails, and leaves nothing
     * on screen or in a log to say which of the four credentials was missing. All four are
     * non-nullable on `CreateMembershipSubscription.Response`; the spec's `nullable: true` is what it
     * says about every string on this wire.
     */
    @Test
    fun aMissingStripeCredentialRefusesRatherThanOpeningASheetThatCannotSucceed() = runTest {
        SUBSCRIBE_REQUIRED_CREDENTIALS.forEach { field ->
            refuses(field) {
                serving(withoutKey(CAPTURED_SUBSCRIBE, field)) {
                    it.subscribe(CreateMembershipSubscriptionRequest(planCode = "plus-annual"))
                }
            }
        }
    }

    @Test
    fun aCancelWithoutAnEndDateRefusesRatherThanSayingTheMembershipEndsOnNothing() = runTest {
        refuses("effectiveEndDate") {
            serving(withoutKey(CAPTURED_CANCEL, "effectiveEndDate")) { it.cancel() }
        }
    }

    @Test
    fun aSwapWithoutItsTermsRefusesRatherThanConfirmingAPlanChangeWithNoPlanAndNoDate() = runTest {
        listOf("newPlanCode", "currentPeriodEnd").forEach { field ->
            refuses(field) {
                serving(withoutKey(CAPTURED_SWAP, field)) {
                    it.swapPlan(SwapMembershipPlanRequest(newPlanCode = "plus-monthly"))
                }
            }
        }
    }

    // --- payload plumbing ---------------------------------------------------------

    private fun mutating(body: String, transform: (JsonObject) -> JsonObject): String =
        transform(Json.parseToJsonElement(body).jsonObject).toString()

    private fun withoutKey(body: String, key: String): String = mutating(body) { it - key }

    private fun withKey(body: String, key: String, value: JsonElement): String =
        mutating(body) { it + (key to value) }

    private fun plansWithFirstRow(transform: (JsonObject) -> JsonObject): String {
        val rows = Json.parseToJsonElement(CAPTURED_PLANS).jsonArray.mapIndexed { index, row ->
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

        val SUBSCRIBE_REQUIRED_CREDENTIALS = listOf(
            "membershipId",
            "setupIntentClientSecret",
            "stripeCustomerId",
            "ephemeralKey",
        )

        val CAPTURED_SUBSCRIBE = """
            {
              "membershipId": "mem-1",
              "setupIntentClientSecret": "seti_1ABC_secret_XYZ",
              "stripeCustomerId": "cus_ABC123",
              "ephemeralKey": "ek_test_ABC"
            }
        """.trimIndent()

        val CAPTURED_CANCEL = """
            { "effectiveEndDate": "2026-09-11T00:00:00Z" }
        """.trimIndent()

        val CAPTURED_SWAP = """
            { "newPlanCode": "plus-monthly", "currentPeriodEnd": "2026-09-11T00:00:00Z" }
        """.trimIndent()


        /** Every member non-zero and non-default. */
        val CAPTURED_PLANS = """
            [
              {
                "code": "plus-annual",
                "name": "Cleansia Plus (annual)",
                "price": 2030.00,
                "monthlyEquivalentPrice": 169.17,
                "billingInterval": 2,
                "discountPercentage": 12.0,
                "freeCancellationWindowHours": 24,
                "allowsExpressUpgrade": true,
                "trialPeriodDays": 14,
                "savingsPercentVsMonthly": 18.5
              },
              {
                "code": "plus-monthly",
                "name": "Cleansia Plus (monthly)",
                "price": 249.00,
                "monthlyEquivalentPrice": 249.00,
                "billingInterval": 1,
                "discountPercentage": 10.0,
                "freeCancellationWindowHours": 12,
                "allowsExpressUpgrade": true,
                "trialPeriodDays": 7,
                "savingsPercentVsMonthly": 0.5
              }
            ]
        """.trimIndent()

        /** The handler's non-member branch verbatim: every plan field null, both verdicts present. */
        val CAPTURED_NON_MEMBER = """
            {
              "hasMembership": false,
              "planCode": null,
              "planName": null,
              "monthlyPriceCzk": null,
              "discountPercentage": null,
              "freeCancellationWindowHours": null,
              "allowsExpressUpgrade": null,
              "status": null,
              "currentPeriodEnd": null,
              "cancelRequested": false,
              "billingInterval": null,
              "monthlyEquivalentPriceCzk": null,
              "trialEligible": true
            }
        """.trimIndent()

        val CAPTURED_MINE = """
            {
              "hasMembership": true,
              "planCode": "plus-annual",
              "planName": "Cleansia Plus (annual)",
              "monthlyPriceCzk": 169.17,
              "discountPercentage": 12.0,
              "freeCancellationWindowHours": 24,
              "allowsExpressUpgrade": true,
              "status": 1,
              "currentPeriodEnd": "2027-08-10T00:00:00Z",
              "cancelRequested": true,
              "billingInterval": 2,
              "monthlyEquivalentPriceCzk": 169.17,
              "expressUpgradesPerMonth": 3,
              "expressUpgradesRemaining": 2,
              "trialEndsAtUtc": "2026-08-24T00:00:00Z",
              "trialEligible": false
            }
        """.trimIndent()

        val PLAN_SPEC_PROPERTIES = setOf(
            "code",
            "name",
            "price",
            "monthlyEquivalentPrice",
            "billingInterval",
            "discountPercentage",
            "freeCancellationWindowHours",
            "allowsExpressUpgrade",
            "trialPeriodDays",
            "savingsPercentVsMonthly",
        )

        val MINE_SPEC_PROPERTIES = setOf(
            "hasMembership",
            "planCode",
            "planName",
            "monthlyPriceCzk",
            "discountPercentage",
            "freeCancellationWindowHours",
            "allowsExpressUpgrade",
            "status",
            "currentPeriodEnd",
            "cancelRequested",
            "billingInterval",
            "monthlyEquivalentPriceCzk",
            "expressUpgradesPerMonth",
            "expressUpgradesRemaining",
            "trialEndsAtUtc",
            "trialEligible",
        )

        val PLAN_REQUIRED_MONEY = listOf(
            "price",
            "monthlyEquivalentPrice",
            "discountPercentage",
            "savingsPercentVsMonthly",
        )

        val MINE_NULLABLE_FIELDS = listOf(
            "planCode",
            "planName",
            "monthlyPriceCzk",
            "discountPercentage",
            "freeCancellationWindowHours",
            "allowsExpressUpgrade",
            "currentPeriodEnd",
            "billingInterval",
            "monthlyEquivalentPriceCzk",
            "expressUpgradesPerMonth",
            "expressUpgradesRemaining",
            "trialEndsAtUtc",
        )
    }
}
