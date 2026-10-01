package cz.cleansia.customer.core.loyalty

import cz.cleansia.customer.core.network.IntEnumSerializersModule
import kotlinx.coroutines.test.runTest
import kotlinx.datetime.Instant
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
import org.junit.Assert.assertNull
import org.junit.Test
import retrofit2.Retrofit
import retrofit2.converter.kotlinx.serialization.asConverterFactory
import cz.cleansia.customer.api.client.CreditApi as GenCreditApi
import cz.cleansia.customer.api.client.LoyaltyApi as GenLoyaltyApi
import cz.cleansia.customer.api.model.GetMyCreditCurrencyBalance as GenCreditBalance
import cz.cleansia.customer.api.model.GetMyCreditResponse as GenGetMyCreditResponse

/**
 * Credit is money the platform owes the customer, read from `GET api/Credit/GetMy`. Neither
 * `GetMyCredit_Response` nor `GetMyCredit_CurrencyBalance` declares a `required` array, so the generator
 * types every property optional — and a defaulted balance would tell a customer they are owed nothing.
 */
class CreditWireTest {

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
            val retrofit = Retrofit.Builder()
                .baseUrl(server.url("/"))
                .addConverterFactory(json.asConverterFactory("application/json".toMediaType()))
                .build()
            val api = LoyaltyApi(
                retrofit.create(GenLoyaltyApi::class.java),
                retrofit.create(GenCreditApi::class.java),
            )
            call(api).also { onRequest(server.takeRequest()) }
        } finally {
            server.shutdown()
        }
    }

    private suspend fun credit(body: String) = serving(body) { it.getCredit() }.body()

    private suspend fun loaded(body: String): CreditDto {
        val dto = credit(body)
        assertNotNull("expected the captured payload to map", dto)
        return dto!!
    }

    // --- the field-name contract ------------------------------------------------

    @Test
    fun theCreditSerialNamesAreExactlyTheSpecProperties() {
        assertEquals(RESPONSE_SPEC_PROPERTIES, serialNames(GenGetMyCreditResponse.serializer().descriptor))
        assertEquals(BALANCE_SPEC_PROPERTIES, serialNames(GenCreditBalance.serializer().descriptor))
    }

    @Test
    fun theRequestKeepsThePathTheServerBinds() = runTest {
        var path: String? = null
        serving(CAPTURED, onRequest = { path = it.path }) { it.getCredit() }
        assertEquals("/api/Credit/GetMy", path)
    }

    // --- money is never coerced -------------------------------------------------

    @Test
    fun everyBalanceArrivesWithItsLiteralValueLargestFirst() = runTest {
        val dto = loaded(CAPTURED)

        assertEquals(2, dto.balances.size)
        assertEquals(CreditBalanceDto(250.0, "CZK", Instant.parse("2027-09-30T10:00:00Z")), dto.balances[0])
        assertEquals(CreditBalanceDto(12.5, "EUR", Instant.parse("2027-03-01T08:00:00Z")), dto.balances[1])
        assertEquals(0.7, dto.maxShareOfOrder, 0.0)
        assertEquals(dto.balances[0], dto.primary)
    }

    @Test
    fun aMissingBalanceCurrencyOrShareRefusesTheCredit() = runTest {
        listOf("balance", "currencyCode", "maxShareOfOrder").forEach { field ->
            assertNull("a missing $field must refuse the credit", credit(withoutKey(NEVER_CREDITED, field)))
        }
    }

    @Test
    fun aBalanceRowWithoutItsAmountOrCurrencyRefusesTheWholeAnswer() = runTest {
        listOf("balance", "currencyCode").forEach { field ->
            assertNull(
                "a row missing $field could promote the wrong currency to the Profile row",
                credit(withFirstBalance { it - field }),
            )
        }
    }

    /** No account is the ordinary case: one zero in the platform default currency, shown as "0 Kč". */
    @Test
    fun aNeverCreditedCustomerGetsTheServersZeroAsTheirOneBalance() = runTest {
        val dto = loaded(NEVER_CREDITED)

        assertEquals(listOf(CreditBalanceDto(0.0, "CZK", null)), dto.balances)
        assertEquals(0.7, dto.maxShareOfOrder, 0.0)
    }

    // --- payload plumbing ---------------------------------------------------------

    private fun mutating(body: String, transform: (JsonObject) -> JsonObject): String =
        transform(Json.parseToJsonElement(body).jsonObject).toString()

    private fun withoutKey(body: String, key: String): String = mutating(body) { it - key }

    private fun withFirstBalance(transform: (JsonObject) -> JsonObject): String =
        mutating(CAPTURED) { root ->
            val rows = root["balances"]!!.jsonArray.mapIndexed { index, row ->
                if (index == 0) transform(row.jsonObject) else row
            }
            root + ("balances" to JsonArray(rows))
        }

    private operator fun JsonObject.minus(key: String) =
        JsonObject(toMutableMap().apply { remove(key) })

    private operator fun JsonObject.plus(entry: Pair<String, JsonElement>) =
        JsonObject(toMutableMap().apply { put(entry.first, entry.second) })

    @OptIn(ExperimentalSerializationApi::class)
    private fun serialNames(descriptor: SerialDescriptor): Set<String> =
        (0 until descriptor.elementsCount).map { descriptor.getElementName(it) }.toSet()

    private companion object {

        /** Two currencies held; the scalars are the first (largest) row, as the handler derives them. */
        val CAPTURED = """
            {
              "balance": 250.0,
              "currencyCode": "CZK",
              "maxShareOfOrder": 0.7,
              "appliesAutomatically": true,
              "expiresOn": "2027-09-30T10:00:00Z",
              "balances": [
                { "balance": 250.0, "currencyCode": "CZK", "expiresOn": "2027-09-30T10:00:00Z" },
                { "balance": 12.5, "currencyCode": "EUR", "expiresOn": "2027-03-01T08:00:00Z" }
              ]
            }
        """.trimIndent()

        val NEVER_CREDITED = """
            {
              "balance": 0.0,
              "currencyCode": "CZK",
              "maxShareOfOrder": 0.7,
              "appliesAutomatically": true,
              "expiresOn": null,
              "balances": []
            }
        """.trimIndent()

        val RESPONSE_SPEC_PROPERTIES = setOf(
            "balance",
            "currencyCode",
            "maxShareOfOrder",
            "appliesAutomatically",
            "expiresOn",
            "balances",
        )

        val BALANCE_SPEC_PROPERTIES = setOf("balance", "currencyCode", "expiresOn")
    }
}
