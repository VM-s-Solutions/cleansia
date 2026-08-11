package cz.cleansia.core.network

import cz.cleansia.core.R
import kotlinx.coroutines.CancellationException
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.withContext
import kotlinx.serialization.json.Json
import kotlinx.serialization.json.JsonArray
import kotlinx.serialization.json.JsonElement
import kotlinx.serialization.json.JsonPrimitive
import kotlinx.serialization.json.jsonArray
import kotlinx.serialization.json.jsonObject
import retrofit2.Response
import java.io.IOException
import java.net.SocketTimeoutException
import java.net.UnknownHostException

/**
 * Wraps a Retrofit suspending call into an [ApiResult]. Cancellation is
 * propagated (not turned into a "failure") so coroutine teardown after a
 * fast nav-away doesn't surface a phantom snackbar. Backend ProblemDetails
 * + bespoke error shapes both decode via [ApiErrorResponse].
 *
 * Reified so a 2xx with no body can be answered by [T]: only the endpoints
 * that genuinely return nothing succeed on it, and everyone else is refused
 * here rather than crashing at whatever mapper or composable reads first.
 */
suspend inline fun <reified T> safeApiCall(
    json: Json,
    crossinline apiCall: suspend () -> Response<T>,
): ApiResult<T> = safeApiCallExpecting(json, bodyless = T::class == Unit::class) { apiCall() }

@PublishedApi
internal suspend fun <T> safeApiCallExpecting(
    json: Json,
    bodyless: Boolean,
    apiCall: suspend () -> Response<T>,
): ApiResult<T> = withContext(Dispatchers.IO) {
    try {
        handleResponse(apiCall(), json, bodyless)
    } catch (ce: CancellationException) {
        throw ce
    } catch (violation: WireContractViolation) {
        // The adapters that map inside the Retrofit `Response` raise the refusal at the call, so it
        // arrives here rather than at `mapWire`. `catch (e: Exception)` below would report it as
        // Unknown, which loses the one thing the idiom exists to say: the server answered and the
        // answer was wrong.
        ApiResult.Error(wireViolationError(violation))
    } catch (e: SocketTimeoutException) {
        // Kept apart from the two below: the connection is fine and the action is "try again",
        // where no-connection asks the customer to go and fix something.
        ApiResult.Error(ApiError.Network(TIMEOUT, R.string.core_error_timeout))
    } catch (e: UnknownHostException) {
        ApiResult.Error(ApiError.Network(NO_CONNECTION, R.string.core_error_no_connection))
    } catch (e: IOException) {
        // Deliberately folded into no-connection: a reset or a broken pipe is the same fact to the
        // customer, and `e.message` is host/port detail they cannot act on — it stays as triage.
        ApiResult.Error(ApiError.Network("IO failure: ${e.message ?: "unknown"}", R.string.core_error_no_connection))
    } catch (e: Exception) {
        ApiResult.Error(ApiError.Unknown(e.message ?: UNEXPECTED, R.string.core_error_unknown))
    }
}

private fun <T> handleResponse(response: Response<T>, json: Json, bodyless: Boolean): ApiResult<T> {
    if (response.isSuccessful) {
        val body = response.body()
        return when {
            body != null -> ApiResult.Success(body)
            bodyless -> {
                @Suppress("UNCHECKED_CAST")
                ApiResult.Success(Unit as T)
            }
            else -> ApiResult.Error(
                // The endpoint that broke the contract is triage; the customer reads the resource.
                ApiError.Server(
                    statusCode = response.code(),
                    message = emptyBodyMessage(response),
                    diagnostic = emptyBodyMessage(response),
                    messageRes = R.string.core_error_server,
                ),
            )
        }
    }

    val errorBody = response.errorBody()?.string()
    val errorResponse = errorBody?.let {
        runCatching { json.decodeFromString<ApiErrorResponse>(it) }.getOrNull()
    }

    val validationErrors = errorResponse?.errors?.let { parseValidationErrors(it) }
    val firstErrorKey = validationErrors?.values?.firstOrNull()?.firstOrNull()

    val error = when (response.code()) {
        401 -> firstErrorKey
            ?.takeIf { BUSINESS_ERROR_KEY.matches(it) }
            ?.let { ApiError.AuthRejected(errorKey = it) }
            ?: ApiError.Unauthorized
        // Each arm: the server's own line passes through with no `messageRes`, because whoever
        // sent it owns the wording; only the `:core` floor beneath it carries a resource.
        404 -> errorResponse?.effectiveMessage
            ?.let { ApiError.NotFound(it) }
            ?: ApiError.NotFound(NOT_FOUND, R.string.core_error_not_found)
        400 -> ApiError.BadRequest(
            message = errorResponse?.effectiveMessage ?: BAD_REQUEST,
            code = errorResponse?.code ?: errorResponse?.type,
            validationErrors = validationErrors,
            errorKey = firstErrorKey,
            // Folded into the generic line on purpose, and it is the one fold here: a 400 whose body
            // we could not parse has no specific thing to tell the customer, so "Bad request" would
            // be developer-speak dressed as copy. The ordinary keyed 400 never reaches this floor —
            // `errorKey` is set above and the app localizers resolve it to its own sentence.
            messageRes = R.string.core_error_unknown.takeIf { errorResponse?.effectiveMessage == null },
        )
        in 500..599 -> ApiError.Server(
            statusCode = response.code(),
            message = errorResponse?.effectiveMessage ?: ApiError.SERVER_USER_MESSAGE,
            messageRes = R.string.core_error_server.takeIf { errorResponse?.effectiveMessage == null },
        )
        else -> errorResponse?.effectiveMessage
            ?.let { ApiError.Unknown(it) }
            ?: ApiError.Unknown(UNEXPECTED, R.string.core_error_unknown)
    }

    return ApiResult.Error(error)
}

/**
 * The path only — a query string on these endpoints carries order ids and email addresses, and this
 * string outlives the request in whatever log or crash report reads it.
 */
private fun emptyBodyMessage(response: Response<*>): String {
    val request = response.raw().request
    return "${request.method} ${request.url.encodedPath} answered ${response.code()} with no body " +
        "but the mobile API contract declares one"
}

/**
 * Tells a controller-authored 401 (which puts a BusinessErrorMessage key such
 * as `auth.internal_type_error` into `errors`) apart from the JwtBearer
 * middleware's own 401, which carries no body at all.
 *
 * Matched positively rather than by "contains no space": the backend joins
 * errors sharing a code with `"; "`, so a negative test would silently
 * reclassify a future multi-error group as a session failure — the one
 * misdiagnosis this split exists to prevent.
 */
private val BUSINESS_ERROR_KEY = Regex("^[a-z0-9_]+(\\.[a-z0-9_]+)+$")

// English floors. Each is paired with the resource that replaces it at render; the string itself is
// what reaches a log, and the fallback for any path that has no Context at all.
private const val TIMEOUT = "Connection timeout. Please try again."
private const val NO_CONNECTION = "Unable to connect to server. Please check your internet connection."
private const val NOT_FOUND = "Resource not found"
private const val BAD_REQUEST = "Bad request"
private const val UNEXPECTED = "An unexpected error occurred"

private fun parseValidationErrors(element: JsonElement): Map<String, List<String>>? = runCatching {
    element.jsonObject.entries.associate { (key, value) ->
        key to when (value) {
            is JsonArray -> value.jsonArray.mapNotNull { (it as? JsonPrimitive)?.content }
            is JsonPrimitive -> listOf(value.content)
            else -> emptyList()
        }
    }.ifEmpty { null }
}.getOrNull()
