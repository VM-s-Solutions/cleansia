package cz.cleansia.partner.data.auth

import cz.cleansia.core.auth.SessionScopedCache
import cz.cleansia.core.auth.TokenStore
import cz.cleansia.core.consent.SignupConsentRepository
import cz.cleansia.core.network.ApiError
import cz.cleansia.core.network.ApiResult
import cz.cleansia.core.notifications.PushTokenRepository
import cz.cleansia.partner.api.client.AuthApi
import cz.cleansia.partner.api.client.EmployeeApi
import cz.cleansia.partner.api.model.JwtTokenResponse
import cz.cleansia.partner.core.auth.UserProfileData
import cz.cleansia.partner.core.auth.UserProfileStore
import io.mockk.coEvery
import io.mockk.mockk
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
import org.junit.Assert.assertNotNull
import org.junit.Assert.assertTrue
import org.junit.Test
import retrofit2.Retrofit
import retrofit2.converter.kotlinx.serialization.asConverterFactory

/**
 * The login response is the whole session: the tokens it carries decide how long the cleaner stays
 * signed in, and `isEmailConfirmed` / `hasAdminAccess` decide what they may reach. `AuthRepositoryTest`
 * and the session-flags test drive the same refusals through a mocked `AuthApi`, which hands back
 * Kotlin objects and therefore cannot notice a renamed field — this decodes the payload over a socket
 * so it can.
 */
class JwtTokenWireTest {

    private val json = Json { ignoreUnknownKeys = true; isLenient = true; explicitNulls = false }

    private val authenticatedAuthApi = mockk<AuthApi>()
    private val employeeApi = mockk<EmployeeApi>(relaxed = true)
    private val pushTokenRepository = mockk<PushTokenRepository>(relaxed = true)
    private val cache = mockk<SessionScopedCache>(relaxed = true)
    private val signupConsent = mockk<SignupConsentRepository>(relaxed = true)

    private var storedTokens: TokenStore.Tokens? = null
    private val tokenStore = mockk<TokenStore>(relaxed = true).also { store ->
        coEvery { store.current() } answers { storedTokens }
        coEvery { store.save(any()) } answers { storedTokens = firstArg() }
    }

    private var storedProfile: UserProfileData? = null
    private val userProfileStore = mockk<UserProfileStore>().also { store ->
        coEvery { store.current() } answers { storedProfile }
        coEvery { store.save(any()) } answers { storedProfile = firstArg() }
        coEvery { store.updateEmployeeId(any()) } answers {
            storedProfile = storedProfile?.copy(employeeId = firstArg())
        }
    }

    private suspend fun login(
        body: String,
        onRequest: (RecordedRequest) -> Unit = {},
    ): ApiResult<LoginOutcome> {
        val server = MockWebServer()
        server.start()
        return try {
            server.enqueue(
                MockResponse()
                    .setResponseCode(200)
                    .setHeader("Content-Type", "application/json")
                    .setBody(body),
            )
            val authApi = Retrofit.Builder()
                .baseUrl(server.url("/"))
                .addConverterFactory(json.asConverterFactory("application/json".toMediaType()))
                .build()
                .create(AuthApi::class.java)
            AuthRepositoryImpl(
                authApi = authApi,
                authenticatedAuthApi = { authenticatedAuthApi },
                employeeApi = employeeApi,
                tokenStore = tokenStore,
                userProfileStore = userProfileStore,
                json = json,
                pushTokenRepository = pushTokenRepository,
                sessionScopedCaches = { setOf(cache) },
                signupConsent = { signupConsent },
            ).login(EMAIL, "pw", rememberMe = true).also { onRequest(server.takeRequest()) }
        } finally {
            server.shutdown()
        }
    }

    private suspend fun assertRefusesNaming(field: String, body: String) {
        val result = login(body)
        assertTrue("a missing $field must refuse the session; got $result", result is ApiResult.Error)
        val error = (result as ApiResult.Error).error
        assertTrue(
            "a broken 2xx body is the server's fault, not the connection's; got $error",
            error is ApiError.Server,
        )
        assertTrue(
            "the refusal must name $field, but said \"${(error as ApiError.Server).message}\"",
            error.message.startsWith("$field "),
        )
    }

    // --- the field-name contract ------------------------------------------------

    @Test
    fun tokenResponseSerialNamesAreExactlyTheSpecProperties() {
        assertEquals(SPEC_PROPERTIES, serialNames(JwtTokenResponse.serializer().descriptor))
    }

    @Test
    fun theRequestKeepsThePathTheServerBinds() = runTest {
        var path: String? = null
        var method: String? = null
        login(CAPTURED_TOKEN) { request ->
            path = request.path
            method = request.method
        }

        assertEquals("POST", method)
        assertEquals("/api/Auth/Login", path)
    }

    // --- every field the session is built from arrives with its literal value ------

    @Test
    fun aConfirmedLoginPersistsTheSessionTheWireDescribed() = runTest {
        val result = login(CAPTURED_TOKEN)

        assertEquals(ApiResult.Success(LoginOutcome.Authenticated), result)
        assertEquals("user-9", storedProfile?.userId)
        assertEquals("jana@cleansia.cz", storedProfile?.email)
        assertEquals("Employee", storedProfile?.role)
        assertEquals(true, storedProfile?.isEmailConfirmed)
        assertEquals(true, storedProfile?.hasAdminAccess)
        assertEquals("refresh-9", storedTokens?.refreshToken)
        assertNotNull(storedTokens?.accessToken)
    }

    // --- rule 2: booleans follow the money rule ---------------------------------

    /**
     * `false` on either is a real state and the wrong one to invent: a defaulted `isEmailConfirmed`
     * bounces a confirmed cleaner back to the screen they already cleared, and the server's own
     * default for `HasAdminAccess` is `true`, so `false` contradicts it rather than falling short of
     * it. Both are non-nullable `bool` on `JwtTokenResponse`.
     */
    @Test
    fun aMissingSessionFlagRefusesByNameRatherThanFallingClosedSilently() = runTest {
        REQUIRED_FLAGS.forEach { field ->
            assertRefusesNaming(field, withoutKey(CAPTURED_TOKEN, field))
        }
    }

    @Test
    fun anExplicitNullSessionFlagRefusesTheSameWay() = runTest {
        REQUIRED_FLAGS.forEach { field ->
            assertRefusesNaming(field, withKey(CAPTURED_TOKEN, field, JsonNull))
        }
    }

    @Test
    fun aRefusedFlagPersistsNoTokensAndNoProfile() = runTest {
        storedTokens = null
        storedProfile = null

        login(withoutKey(CAPTURED_TOKEN, "isEmailConfirmed"))

        assertEquals(null, storedProfile)
    }

    // --- rule 5: nullable-by-design fields stay nullable ---------------------------

    /**
     * `UserId`, `Email`, `RefreshToken`, `RefreshTokenExpiresAt` and `Role` are all genuinely
     * nullable on the C# record, so their absence is a session that still opens.
     */
    @Test
    fun aLoginWithOnlyTheRequiredFieldsStillOpensASession() = runTest {
        val result = login(
            NULLABLE_FIELDS.fold(CAPTURED_TOKEN) { body, key -> withoutKey(body, key) },
        )

        assertEquals(ApiResult.Success(LoginOutcome.Authenticated), result)
        assertEquals(EMAIL, storedProfile?.email)
        assertEquals(null, storedProfile?.role)
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
        const val EMAIL = "jana@cleansia.cz"

        /** Every member non-default; the token carries an `exp` far enough out to decode cleanly. */
        val CAPTURED_TOKEN = """
            {
              "token": "header.eyJleHAiOjQxMDI0NDQ4MDB9.sig",
              "isEmailConfirmed": true,
              "hasAdminAccess": true,
              "userId": "user-9",
              "email": "jana@cleansia.cz",
              "refreshToken": "refresh-9",
              "refreshTokenExpiresAt": "2026-09-11T00:00:00Z",
              "csrfToken": "csrf-9",
              "role": "Employee"
            }
        """.trimIndent()

        val SPEC_PROPERTIES = setOf(
            "token",
            "isEmailConfirmed",
            "hasAdminAccess",
            "userId",
            "email",
            "refreshToken",
            "refreshTokenExpiresAt",
            "csrfToken",
            "role",
        )

        val REQUIRED_FLAGS = listOf("isEmailConfirmed", "hasAdminAccess")

        val NULLABLE_FIELDS = listOf(
            "userId",
            "email",
            "refreshToken",
            "refreshTokenExpiresAt",
            "csrfToken",
            "role",
        )
    }
}
