package cz.cleansia.partner.core.network

import kotlinx.serialization.Serializable

/**
 * Represents an error response from the API
 */
@Serializable
data class ApiErrorResponse(
    val message: String? = null,
    val code: String? = null,
    val title: String? = null,
    val errors: Map<String, List<String>>? = null
)

/**
 * Sealed class representing different types of API errors
 */
sealed class ApiError : Exception() {

    /**
     * Network error (no internet, timeout, etc.)
     */
    data class Network(override val message: String) : ApiError()

    /**
     * Server error (5xx responses)
     */
    data class Server(val statusCode: Int, override val message: String) : ApiError()

    /**
     * Unauthorized error (401)
     */
    data object Unauthorized : ApiError() {
        private fun readResolve(): Any = Unauthorized
        override val message: String = "Session expired. Please login again."
    }

    /**
     * Not found error (404)
     */
    data class NotFound(override val message: String = "Resource not found") : ApiError()

    /**
     * Bad request error (400) with validation errors
     */
    data class BadRequest(
        override val message: String,
        val code: String? = null,
        val validationErrors: Map<String, List<String>>? = null
    ) : ApiError()

    /**
     * Unknown error
     */
    data class Unknown(override val message: String = "An unexpected error occurred") : ApiError()

    /**
     * Get a user-friendly error message
     */
    fun getUserMessage(): String = message ?: "An unexpected error occurred"

    /**
     * Get validation errors for a specific field
     */
    fun getFieldErrors(field: String): List<String>? {
        return if (this is BadRequest) {
            validationErrors?.get(field)
        } else {
            null
        }
    }
}
