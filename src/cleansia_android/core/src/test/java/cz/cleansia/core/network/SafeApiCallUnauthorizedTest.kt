package cz.cleansia.core.network

import kotlinx.coroutines.test.runTest
import kotlinx.serialization.json.Json
import okhttp3.MediaType.Companion.toMediaType
import okhttp3.ResponseBody.Companion.toResponseBody
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Test
import retrofit2.Response

/**
 * The 401 split. A controller-authored rejection carries a BusinessErrorMessage
 * key and becomes [ApiError.AuthRejected] so the UI can name the real reason;
 * everything else — the bodiless JwtBearer challenge, prose from ModelState,
 * garbage — stays [ApiError.Unauthorized], which is the only arm the session
 * layer's "you were signed out" reasoning is allowed to key off.
 */
class SafeApiCallUnauthorizedTest {

    private val json = Json { ignoreUnknownKeys = true; isLenient = true }

    private suspend fun errorFor(code: Int, body: String): ApiError {
        val response = Response.error<Unit>(code, body.toResponseBody("application/problem+json".toMediaType()))
        val result = safeApiCall(json) { response }
        return (result as ApiResult.Error).error
    }

    // ── Controller-authored: the business key survives ──

    @Test
    fun handle401_givenBusinessErrorKey_returnsAuthRejectedCarryingTheKey() = runTest {
        val payload = """
            {
              "title": "Unauthorized",
              "type": "auth.internal_type_error",
              "detail": "auth.internal_type_error",
              "status": 401,
              "errors": { "auth.internal_type_error": "auth.internal_type_error" }
            }
        """.trimIndent()

        val error = errorFor(401, payload)

        assertTrue("expected AuthRejected but was $error", error is ApiError.AuthRejected)
        assertEquals("auth.internal_type_error", (error as ApiError.AuthRejected).errorKey)
    }

    @Test
    fun handle401_givenBusinessErrorKey_keepsTheRawKeyOutOfGetUserMessage() = runTest {
        val payload = """
            { "detail": "auth.insufficient_privileges",
              "errors": { "auth.insufficient_privileges": "auth.insufficient_privileges" } }
        """.trimIndent()

        val error = errorFor(401, payload)

        assertEquals(ApiError.Unauthorized.message, error.getUserMessage())
    }

    // ── Everything else stays a session failure ──

    @Test
    fun handle401_givenNoBody_returnsUnauthorized() = runTest {
        assertEquals(ApiError.Unauthorized, errorFor(401, ""))
    }

    @Test
    fun handle401_givenMalformedJson_returnsUnauthorized() = runTest {
        assertEquals(ApiError.Unauthorized, errorFor(401, "{not really json"))
    }

    @Test
    fun handle401_givenBodyWithoutErrorsDictionary_returnsUnauthorized() = runTest {
        val payload = """{ "title": "Unauthorized", "status": 401 }"""
        assertEquals(ApiError.Unauthorized, errorFor(401, payload))
    }

    @Test
    fun handle401_givenModelStateProse_returnsUnauthorized() = runTest {
        val payload = """{ "errors": { "Id": ["The Id field is required."] } }"""
        assertEquals(ApiError.Unauthorized, errorFor(401, payload))
    }

    @Test
    fun handle401_givenJoinedMultiErrorGroup_returnsUnauthorizedRatherThanAHalfKey() = runTest {
        // The backend joins errors sharing a code with "; ". Nothing sends two
        // errors on the 401 arm today, but if it ever does we fall back to the
        // status-quo message instead of shipping "a; b" to a resource lookup.
        val payload = """{ "errors": { "Auth": "auth.internal_type_error; auth.apple_type_error" } }"""
        assertEquals(ApiError.Unauthorized, errorFor(401, payload))
    }

    @Test
    fun handle401_givenSingleWordWithoutDot_returnsUnauthorized() = runTest {
        val payload = """{ "errors": { "Auth": "Unauthorized" } }"""
        assertEquals(ApiError.Unauthorized, errorFor(401, payload))
    }

    // ── The other status arms are untouched ──

    @Test
    fun handle400_givenBusinessErrorKey_stillReturnsBadRequestWithErrorKey() = runTest {
        val payload = """
            { "detail": "A validation problem occurred.",
              "errors": { "Email": "user.not_existing_email" } }
        """.trimIndent()

        val error = errorFor(400, payload)

        assertTrue("expected BadRequest but was $error", error is ApiError.BadRequest)
        assertEquals("user.not_existing_email", (error as ApiError.BadRequest).errorKey)
    }
}
