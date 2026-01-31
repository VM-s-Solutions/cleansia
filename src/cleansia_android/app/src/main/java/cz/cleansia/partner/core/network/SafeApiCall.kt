package cz.cleansia.partner.core.network

import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.withContext
import kotlinx.serialization.json.Json
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

    val error = when (statusCode) {
        401 -> ApiError.Unauthorized
        404 -> ApiError.NotFound(errorResponse?.message ?: "Resource not found")
        400 -> ApiError.BadRequest(
            message = errorResponse?.message ?: "Bad request",
            code = errorResponse?.code,
            validationErrors = errorResponse?.errors
        )
        in 500..599 -> ApiError.Server(
            statusCode = statusCode,
            message = errorResponse?.message ?: "Server error. Please try again later."
        )
        else -> ApiError.Unknown(errorResponse?.message ?: "An unexpected error occurred")
    }

    return ApiResult.Error(error)
}
