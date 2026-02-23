package cz.cleansia.partner.core.network

import kotlinx.serialization.Serializable
import kotlinx.serialization.json.JsonElement

/**
 * Handles both custom API error format and ASP.NET ProblemDetails format.
 * ProblemDetails uses: title, type, detail, status, errors
 * Custom format uses: message, code, title, errors
 *
 * The errors field uses JsonElement to handle both Map<String, String> (ProblemDetails)
 * and Map<String, List<String>> (custom format) without deserialization failures.
 */
@Serializable
data class ApiErrorResponse(
    val message: String? = null,
    val code: String? = null,
    val title: String? = null,
    val errors: JsonElement? = null,
    // ProblemDetails fields
    val detail: String? = null,
    val type: String? = null,
    val status: Int? = null
) {
    /** Returns the best available error message from either format */
    val effectiveMessage: String?
        get() = detail ?: message ?: title
}

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
     * Bad request error (400) with validation errors.
     * [errorKey] is the first validation error key from the backend (e.g. "user.not_existing_email")
     * which can be used for localized translation via [ApiErrorTranslator].
     */
    data class BadRequest(
        override val message: String,
        val code: String? = null,
        val validationErrors: Map<String, List<String>>? = null,
        val errorKey: String? = null
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
