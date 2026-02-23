package cz.cleansia.partner.core.network

import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.withContext
import kotlinx.serialization.json.Json
import kotlinx.serialization.json.JsonArray
import kotlinx.serialization.json.JsonObject
import kotlinx.serialization.json.JsonPrimitive
import kotlinx.serialization.json.jsonArray
import kotlinx.serialization.json.jsonObject
import kotlinx.serialization.json.jsonPrimitive
import retrofit2.Response
import java.io.IOException
import java.net.SocketTimeoutException
import java.net.UnknownHostException

/**
 * Safely executes an API call and wraps the result in ApiResult.
 * Handles common error cases and converts them to appropriate ApiError types.
 */
suspend fun <T> safeApiCall(
    json: Json = Json { ignoreUnknownKeys = true },
    apiCall: suspend () -> Response<T>
): ApiResult<T> = withContext(Dispatchers.IO) {
    try {
        val response = apiCall()
        handleResponse(response, json)
    } catch (e: SocketTimeoutException) {
        ApiResult.Error(ApiError.Network("Connection timeout. Please try again."))
    } catch (e: UnknownHostException) {
        ApiResult.Error(ApiError.Network("Unable to connect to server. Please check your internet connection."))
    } catch (e: IOException) {
        ApiResult.Error(ApiError.Network("Network error: ${e.message}"))
    } catch (e: Exception) {
        ApiResult.Error(ApiError.Unknown(e.message ?: "An unexpected error occurred"))
    }
}

/**
 * Handles the API response and converts it to ApiResult
 */
private fun <T> handleResponse(response: Response<T>, json: Json): ApiResult<T> {
    return if (response.isSuccessful) {
        val body = response.body()
        if (body != null) {
            ApiResult.Success(body)
        } else {
            // For responses with no body (like 204 No Content)
            @Suppress("UNCHECKED_CAST")
            ApiResult.Success(Unit as T)
        }
    } else {
        val errorBody = response.errorBody()?.string()
        handleErrorResponse(response.code(), errorBody, json)
    }
}

/**
 * Handles error responses and converts them to appropriate ApiError types
 */
private fun <T> handleErrorResponse(statusCode: Int, errorBody: String?, json: Json): ApiResult<T> {
    val errorResponse = errorBody?.let {
        try {
            json.decodeFromString<ApiErrorResponse>(it)
        } catch (e: Exception) {
            null
        }
    }

    // Parse errors from JsonElement, handling both Map<String, String> and Map<String, List<String>>
    val validationErrors = errorResponse?.errors?.let { parseValidationErrors(it) }

    // Extract the first validation error key for translation (mirrors Angular frontend pattern)
    val firstErrorKey = extractFirstErrorKey(validationErrors)

    val error = when (statusCode) {
        401 -> ApiError.Unauthorized
        404 -> ApiError.NotFound(errorResponse?.effectiveMessage ?: "Resource not found")
        400 -> ApiError.BadRequest(
            message = errorResponse?.effectiveMessage ?: "Bad request",
            code = errorResponse?.code ?: errorResponse?.type,
            validationErrors = validationErrors,
            errorKey = firstErrorKey
        )
        in 500..599 -> ApiError.Server(
            statusCode = statusCode,
            message = errorResponse?.effectiveMessage ?: "Server error. Please try again later."
        )
        else -> ApiError.Unknown(errorResponse?.effectiveMessage ?: "An unexpected error occurred")
    }

    return ApiResult.Error(error)
}

/**
 * Parses validation errors from JsonElement, handling both formats:
 * - ProblemDetails: Map<String, String> (joined error messages)
 * - Custom format: Map<String, List<String>> (list of error messages)
 */
private fun parseValidationErrors(element: kotlinx.serialization.json.JsonElement): Map<String, List<String>>? {
    return try {
        val jsonObj = element.jsonObject
        jsonObj.entries.associate { (key, value) ->
            key to when (value) {
                is JsonArray -> value.jsonArray.mapNotNull {
                    (it as? JsonPrimitive)?.content
                }
                is JsonPrimitive -> listOf(value.content)
                else -> emptyList()
            }
        }.ifEmpty { null }
    } catch (e: Exception) {
        null
    }
}

/**
 * Extracts the first validation error key from the parsed errors map.
 * Mirrors the Angular frontend pattern:
 *   const errorKey = getObjectValues(error.error.errors)[0]
 *
 * The backend sends translation keys as error values (e.g. "user.not_existing_email").
 * This function returns the first such key for use with [ApiErrorTranslator].
 */
private fun extractFirstErrorKey(validationErrors: Map<String, List<String>>?): String? {
    if (validationErrors.isNullOrEmpty()) return null
    return validationErrors.values.firstOrNull()?.firstOrNull()
}
