package cz.cleansia.customer.core.payments

import cz.cleansia.core.network.ApiError
import cz.cleansia.core.network.ApiResult
import cz.cleansia.customer.core.network.IntEnumSerializersModule
import java.time.YearMonth
import kotlinx.coroutines.test.runTest
import kotlinx.serialization.ExperimentalSerializationApi
import kotlinx.serialization.descriptors.SerialDescriptor
import kotlinx.serialization.json.Json
import kotlinx.serialization.json.JsonArray
import kotlinx.serialization.json.JsonObject
import kotlinx.serialization.json.jsonArray
import kotlinx.serialization.json.jsonObject
import okhttp3.MediaType.Companion.toMediaType
import okhttp3.mockwebserver.MockResponse
import okhttp3.mockwebserver.MockWebServer
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Test
import retrofit2.Retrofit
import retrofit2.converter.kotlinx.serialization.asConverterFactory
import cz.cleansia.customer.api.client.SavedCardApi as GenSavedCardApi
import cz.cleansia.customer.api.model.CreateSavedCardSetupIntentResponse as GenCreateSavedCardSetupIntentResponse
import cz.cleansia.customer.api.model.SavedCardDto as GenSavedCardDto

/**
 * Every field of `SavedCardDto` and `CreateSavedCardSetupIntent.Response` is non-nullable in C#; the
 * spec declares no `required` array, so the generator types them optional and the mapper is the one
 * place the contract is held.
 */
class SavedCardWireTest {

    private val json = Json {
        ignoreUnknownKeys = true
        isLenient = true
        explicitNulls = false
        serializersModule = IntEnumSerializersModule
    }

    private suspend fun <T> withServer(vararg bodies: String, block: suspend (SavedCardRepository, MockWebServer) -> T): T {
        val server = MockWebServer()
        server.start()
        return try {
            bodies.forEach { body ->
                server.enqueue(MockResponse().setHeader("Content-Type", "application/json").setBody(body))
            }
            val repo = SavedCardRepository(
                SavedCardApi(
                    Retrofit.Builder()
                        .baseUrl(server.url("/"))
                        .addConverterFactory(json.asConverterFactory("application/json".toMediaType()))
                        .build()
                        .create(GenSavedCardApi::class.java),
                ),
                json,
            )
            block(repo, server)
        } finally {
            server.shutdown()
        }
    }

    private fun assertRefusesNaming(field: String, result: ApiResult<*>) {
        assertTrue("a missing $field must refuse; got $result", result is ApiResult.Error)
        val error = (result as ApiResult.Error).error
        assertTrue("a broken 2xx body is the server's fault; got $error", error is ApiError.Server)
        assertTrue(
            "the refusal must name $field, but carried \"${(error as ApiError.Server).diagnostic}\"",
            error.diagnostic!!.startsWith("$field "),
        )
    }

    // --- the field-name contract ------------------------------------------------

    @Test
    fun savedCardDtoSerialNamesAreExactlyTheSpecProperties() {
        assertEquals(CARD_PROPERTIES, serialNames(GenSavedCardDto.serializer().descriptor))
    }

    @Test
    fun setupIntentDtoSerialNamesAreExactlyTheSpecProperties() {
        assertEquals(SETUP_PROPERTIES, serialNames(GenCreateSavedCardSetupIntentResponse.serializer().descriptor))
    }

    // --- the three routes -----------------------------------------------------------

    @Test
    fun theCardsAreReadFromTheRouteTheServerBinds() = runTest {
        val request = withServer(CAPTURED_CARDS) { repo, server ->
            repo.refresh()
            server.takeRequest()
        }

        assertEquals("GET", request.method)
        assertEquals("/api/SavedCard/GetMine", request.path)
    }

    @Test
    fun theCaptureSendsTheConsentAndTheMarket() = runTest {
        val request = withServer(CAPTURED_SETUP) { repo, server ->
            repo.startCapture(consentAccepted = true, countryId = "cze-id")
            server.takeRequest()
        }

        assertEquals("POST", request.method)
        assertEquals("/api/SavedCard/CreateSetupIntent", request.path)
        val sent = Json.parseToJsonElement(request.body.readUtf8()).jsonObject
        assertEquals("true", sent["consentAccepted"].toString())
        assertEquals("\"cze-id\"", sent["countryId"].toString())
    }

    @Test
    fun theRemovalDeletesTheCardByItsId() = runTest {
        val request = withServer(REMOVED) { repo, server ->
            repo.remove("card-1")
            server.takeRequest()
        }

        assertEquals("DELETE", request.method)
        assertEquals("/api/SavedCard/Remove/card-1", request.path)
    }

    // --- mapping ------------------------------------------------------------------------

    @Test
    fun everyCardFieldArrivesWithItsLiteralValue() = runTest {
        val result = withServer(CAPTURED_CARDS) { repo, _ -> repo.refresh() }

        assertEquals(
            listOf(SavedCard(id = "card-1", brand = "visa", last4 = "4242", expMonth = 4, expYear = 2029, currencyCode = "CZK")),
            (result as ApiResult.Success).data,
        )
    }

    @Test
    fun everySetupCredentialArrivesWithItsLiteralValue() = runTest {
        val result = withServer(CAPTURED_SETUP) { repo, _ -> repo.startCapture(consentAccepted = true, countryId = null) }

        assertEquals(
            SavedCardSetup(
                savedCardId = "card-9",
                setupIntentClientSecret = "seti_1_secret_2",
                stripeCustomerId = "cus_ABC",
                ephemeralKey = "ek_test_ABC",
            ),
            (result as ApiResult.Success).data,
        )
    }

    /** A card dropped from the list reads as "no card", and the booking would capture a second one. */
    @Test
    fun aCardMissingAFieldRefusesThePageByName() = runTest {
        CARD_PROPERTIES.forEach { field ->
            assertRefusesNaming(field, withServer(withoutCardKey(field)) { repo, _ -> repo.refresh() })
        }
    }

    @Test
    fun aSetupMissingACredentialRefusesByName() = runTest {
        SETUP_PROPERTIES.forEach { field ->
            assertRefusesNaming(
                field,
                withServer(withoutKey(CAPTURED_SETUP, field)) { repo, _ -> repo.startCapture(consentAccepted = true, countryId = null) },
            )
        }
    }

    // --- the cache ---------------------------------------------------------------------

    @Test
    fun aReadIsCachedARemovalLeavesItAndASignOutForgetsIt() = runTest {
        withServer(CAPTURED_CARDS, REMOVED) { repo, _ ->
            assertNull(repo.cards.value)

            repo.refresh()
            assertEquals(listOf("card-1"), repo.cards.value!!.map { it.id })

            repo.remove("card-1")
            assertEquals(emptyList<SavedCard>(), repo.cards.value)

            repo.clear()
            assertNull(repo.cards.value)
        }
    }

    // --- the server's IsUsableOn -------------------------------------------------------

    @Test
    fun aCardIsUsableThroughItsExpiryMonthAndOnlyInItsCurrency() {
        val card = SavedCard(id = "c", brand = "visa", last4 = "4242", expMonth = 4, expYear = 2029, currencyCode = "CZK")

        assertEquals(card, listOf(card).usableIn("CZK", YearMonth.of(2029, 4)))
        assertEquals(card, listOf(card).usableIn("czk", YearMonth.of(2028, 12)))
        assertNull(listOf(card).usableIn("CZK", YearMonth.of(2029, 5)))
        assertNull(listOf(card).usableIn("CZK", YearMonth.of(2030, 1)))
        assertNull(listOf(card).usableIn("EUR", YearMonth.of(2029, 4)))
    }

    // --- payload plumbing ----------------------------------------------------------------

    private fun withoutKey(body: String, key: String): String =
        JsonObject(Json.parseToJsonElement(body).jsonObject.toMutableMap().apply { remove(key) }).toString()

    private fun withoutCardKey(key: String): String =
        JsonArray(
            Json.parseToJsonElement(CAPTURED_CARDS).jsonArray.map {
                JsonObject(it.jsonObject.toMutableMap().apply { remove(key) })
            },
        ).toString()

    @OptIn(ExperimentalSerializationApi::class)
    private fun serialNames(descriptor: SerialDescriptor): Set<String> =
        (0 until descriptor.elementsCount).map { descriptor.getElementName(it) }.toSet()

    private companion object {
        val CAPTURED_CARDS = """
            [
              {
                "id": "card-1",
                "brand": "visa",
                "last4": "4242",
                "expMonth": 4,
                "expYear": 2029,
                "currencyCode": "CZK"
              }
            ]
        """.trimIndent()

        val CAPTURED_SETUP = """
            {
              "savedCardId": "card-9",
              "setupIntentClientSecret": "seti_1_secret_2",
              "stripeCustomerId": "cus_ABC",
              "ephemeralKey": "ek_test_ABC"
            }
        """.trimIndent()

        const val REMOVED = """{ "savedCardId": "card-1" }"""

        val CARD_PROPERTIES = setOf("id", "brand", "last4", "expMonth", "expYear", "currencyCode")
        val SETUP_PROPERTIES = setOf("savedCardId", "setupIntentClientSecret", "stripeCustomerId", "ephemeralKey")
    }
}
