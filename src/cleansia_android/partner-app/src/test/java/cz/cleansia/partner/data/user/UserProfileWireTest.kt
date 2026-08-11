package cz.cleansia.partner.data.user

import cz.cleansia.core.network.ApiResult
import cz.cleansia.partner.api.client.UserApi
import cz.cleansia.partner.api.model.MyProfileDto
import kotlinx.coroutines.test.runTest
import kotlinx.serialization.ExperimentalSerializationApi
import kotlinx.serialization.descriptors.SerialDescriptor
import kotlinx.serialization.json.Json
import kotlinx.serialization.json.JsonElement
import kotlinx.serialization.json.JsonNull
import kotlinx.serialization.json.JsonObject
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
 * `preferredLanguageCode` is the only input to the language the period-closed mail and the payout
 * invoice PDF are rendered in, so a field renamed on the wire silently sends a Czech cleaner an
 * English invoice with nothing anywhere going red. `MyProfileDto` declares no `required` array, so
 * the generator types every property optional-with-null regardless of `nullable: false`.
 */
class UserProfileWireTest {

    private val json = Json { ignoreUnknownKeys = true; isLenient = true; explicitNulls = false }

    private fun repo(server: MockWebServer) = UserRepositoryImpl(
        Retrofit.Builder()
            .baseUrl(server.url("/"))
            .addConverterFactory(json.asConverterFactory("application/json".toMediaType()))
            .build()
            .create(UserApi::class.java),
        json,
    )

    private suspend fun fetch(
        body: String,
        code: Int = 200,
        onRequest: (RecordedRequest) -> Unit = {},
    ): ApiResult<CurrentUser> {
        val server = MockWebServer()
        server.start()
        return try {
            server.enqueue(
                MockResponse()
                    .setResponseCode(code)
                    .setHeader("Content-Type", "application/json")
                    .setBody(body),
            )
            repo(server).getCurrentUser().also { onRequest(server.takeRequest()) }
        } finally {
            server.shutdown()
        }
    }

    private suspend fun loaded(body: String): CurrentUser {
        val result = fetch(body)
        assertTrue("expected the captured payload to map; got $result", result is ApiResult.Success)
        return (result as ApiResult.Success).data
    }

    // --- the field-name contract ------------------------------------------------

    @Test
    fun profileDtoSerialNamesAreExactlyTheSpecProperties() {
        assertEquals(SPEC_PROPERTIES, serialNames(MyProfileDto.serializer().descriptor))
    }

    @Test
    fun theRequestKeepsThePathTheServerBinds() = runTest {
        var path: String? = null
        var method: String? = null
        fetch(CAPTURED_PROFILE) { request ->
            path = request.path
            method = request.method
        }

        assertEquals("GET", method)
        assertEquals("/api/User/GetCurrent", path)
    }

    // --- every mapped field arrives with the value the wire carried ---------------

    @Test
    fun everyMappedFieldArrivesWithItsLiteralValue() = runTest {
        val user = loaded(CAPTURED_PROFILE)

        assertEquals("jana@example.com", user.email)
        assertEquals("Jana", user.firstName)
        assertEquals("Novak", user.lastName)
        assertEquals("+420777123456", user.phoneNumber)
        assertEquals("1990-04-17", user.birthDate)
        assertEquals("cs", user.preferredLanguageCode)
    }

    /**
     * The rendering language of every payroll document this cleaner receives. A rename lands as null
     * here and the backend falls back to its own default, which is a different language, not an error.
     */
    @Test
    fun aRenamedLanguageKeyIsVisibleAsANullRatherThanASilentSubstitution() = runTest {
        assertNull(loaded(withoutKey(CAPTURED_PROFILE, "preferredLanguageCode")).preferredLanguageCode)
    }

    // --- rule 5: nullable-by-design fields stay nullable ---------------------------

    @Test
    fun aCleanerWhoHasSetNoOptionalFieldKeepsTheirNulls() = runTest {
        val user = loaded(
            NULLABLE_FIELDS.fold(CAPTURED_PROFILE) { body, key -> withoutKey(body, key) },
        )

        assertNull(user.phoneNumber)
        assertNull(user.birthDate)
        assertNull(user.preferredLanguageCode)
        assertEquals("jana@example.com", user.email)
    }

    @Test
    fun anExplicitNullBirthDateSurvivesAsNull() = runTest {
        assertNull(loaded(withKey(CAPTURED_PROFILE, "birthDate", JsonNull)).birthDate)
    }

    // --- the refused body ---------------------------------------------------------

    @Test
    fun aBodylessSuccessIsRefusedRatherThanMappedToABlankAccount() = runTest {
        val result = fetch("", code = 204)

        assertTrue("a 2xx with no profile in it is not a profile; got $result", result is ApiResult.Error)
    }

    // --- payload plumbing ---------------------------------------------------------

    private fun mutating(body: String, transform: (JsonObject) -> JsonObject): String =
        transform(Json.parseToJsonElement(body).jsonObject).toString()

    private fun withoutKey(body: String, key: String): String = mutating(body) { it - key }

    private fun withKey(body: String, key: String, value: JsonElement): String =
        mutating(body) { it + (key to value) }

    private operator fun JsonObject.minus(key: String) =
        JsonObject(toMutableMap().apply { remove(key) })

    private operator fun JsonObject.plus(entry: Pair<String, JsonElement>) =
        JsonObject(toMutableMap().apply { put(entry.first, entry.second) })

    @OptIn(ExperimentalSerializationApi::class)
    private fun serialNames(descriptor: SerialDescriptor): Set<String> =
        (0 until descriptor.elementsCount).map { descriptor.getElementName(it) }.toSet()

    private companion object {

        /**
         * Every member non-default, including the hero-stat block this app does not read — a payload
         * that leans on defaults cannot tell a mapped field from a forgotten one.
         */
        val CAPTURED_PROFILE = """
            {
              "email": "jana@example.com",
              "firstName": "Jana",
              "lastName": "Novak",
              "phoneNumber": "+420777123456",
              "profile": { "type": "Profile", "name": "Employee", "value": 2 },
              "authenticationType": { "type": "AuthenticationType", "name": "Internal", "value": 1 },
              "isEmailConfirmed": true,
              "birthDate": "1990-04-17",
              "profilePhoto": {
                "fileName": "avatar-9.jpg",
                "base64Content": "AAA=",
                "contentType": "image/jpeg",
                "blobUrl": "https://blob.example/avatar-9.jpg?sig=x"
              },
              "preferredLanguageCode": "cs",
              "preferredLanguageName": "Čeština",
              "memberSince": "2025-11-02T08:30:00Z",
              "totalBookings": 17,
              "totalSavings": 4820.50,
              "savingsCurrencyCode": "CZK"
            }
        """.trimIndent()

        val SPEC_PROPERTIES = setOf(
            "email",
            "firstName",
            "lastName",
            "phoneNumber",
            "profile",
            "authenticationType",
            "isEmailConfirmed",
            "birthDate",
            "profilePhoto",
            "preferredLanguageCode",
            "preferredLanguageName",
            "memberSince",
            "totalBookings",
            "totalSavings",
            "savingsCurrencyCode",
        )

        val NULLABLE_FIELDS = listOf("phoneNumber", "birthDate", "preferredLanguageCode")
    }
}
