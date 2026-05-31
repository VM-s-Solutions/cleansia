package cz.cleansia.partner.core.network

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
 */
suspend fun <T> safeApiCall(
    json: Json,
    apiCall: suspend () -> Response<T>,
): ApiResult<T> = withContext(Dispatchers.IO) {
    try {
        handleResponse(apiCall(), json)
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

private fun <T> handleResponse(response: Response<T>, json: Json): ApiResult<T> {
    if (response.isSuccessful) {
        val body = response.body()
        return if (body != null) {
            ApiResult.Success(body)
        } else {
            @Suppress("UNCHECKED_CAST")
            ApiResult.Success(Unit as T)
        }
    }

    val errorBody = response.errorBody()?.string()
    val errorResponse = errorBody?.let {
        runCatching { json.decodeFromString<ApiErrorResponse>(it) }.getOrNull()
    }

    val validationErrors = errorResponse?.errors?.let { parseValidationErrors(it) }
    val firstErrorKey = validationErrors?.values?.firstOrNull()?.firstOrNull()

    val error = when (response.code()) {
        401 -> ApiError.Unauthorized
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

private fun parseValidationErrors(element: JsonElement): Map<String, List<String>>? = runCatching {
    element.jsonObject.entries.associate { (key, value) ->
        key to when (value) {
            is JsonArray -> value.jsonArray.mapNotNull { (it as? JsonPrimitive)?.content }
            is JsonPrimitive -> listOf(value.content)
            else -> emptyList()
        }
    }.ifEmpty { null }
}.getOrNull()
