package cz.cleansia.core.network

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
    } catch (e: SocketTimeoutException) {
        ApiResult.Error(ApiError.Network("Connection timeout. Please try again."))
    } catch (e: UnknownHostException) {
        ApiResult.Error(ApiError.Network("Unable to connect to server. Please check your internet connection."))
    } catch (e: IOException) {
        ApiResult.Error(ApiError.Network("Network error: ${e.message ?: "unknown"}"))
    } catch (e: Exception) {
        ApiResult.Error(ApiError.Unknown(e.message ?: "An unexpected error occurred"))
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
                ApiError.Server(response.code(), emptyBodyMessage(response)),
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
        404 -> ApiError.NotFound(errorResponse?.effectiveMessage ?: "Resource not found")
        400 -> ApiError.BadRequest(
            message = errorResponse?.effectiveMessage ?: "Bad request",
            code = errorResponse?.code ?: errorResponse?.type,
            validationErrors = validationErrors,
            errorKey = firstErrorKey,
        )
        in 500..599 -> ApiError.Server(
            statusCode = response.code(),
            message = errorResponse?.effectiveMessage ?: "Server error. Please try again later.",
        )
        else -> ApiError.Unknown(errorResponse?.effectiveMessage ?: "An unexpected error occurred")
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

private fun parseValidationErrors(element: JsonElement): Map<String, List<String>>? = runCatching {
    element.jsonObject.entries.associate { (key, value) ->
        key to when (value) {
            is JsonArray -> value.jsonArray.mapNotNull { (it as? JsonPrimitive)?.content }
            is JsonPrimitive -> listOf(value.content)
            else -> emptyList()
        }
    }.ifEmpty { null }
}.getOrNull()
