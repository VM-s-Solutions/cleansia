package cz.cleansia.partner.core.network

import kotlinx.serialization.Serializable
import kotlinx.serialization.json.JsonElement

/**
 * Wire shape covering both ASP.NET `ProblemDetails` (`detail`/`type`/`status`)
 * and the bespoke `{message,code,errors}` shape some endpoints still return.
 * `errors` is a [JsonElement] so we can lazily parse either
 * `Map<String,String>` or `Map<String,List<String>>` at the call site.
 */
@Serializable
data class ApiErrorResponse(
    val message: String? = null,
    val code: String? = null,
    val title: String? = null,
    val errors: JsonElement? = null,
    val detail: String? = null,
    val type: String? = null,
    val status: Int? = null,
) {
    val effectiveMessage: String?
        get() = detail ?: message ?: title
}

sealed class ApiError : Exception() {

    data class Network(override val message: String) : ApiError()

    data class Server(val statusCode: Int, override val message: String) : ApiError()

    data object Unauthorized : ApiError() {
        private fun readResolve(): Any = Unauthorized
        override val message: String = "Session expired. Please login again."
    }

    data class NotFound(override val message: String = "Resource not found") : ApiError()

    /**
     * 400 with optional structured validation errors. `errorKey` is the first
     * server-supplied translation key (e.g. `user.not_existing_email`) that
     * [ApiErrorTranslator] can map to a localized string at the UI layer.
     */
    data class BadRequest(
        override val message: String,
        val code: String? = null,
        val validationErrors: Map<String, List<String>>? = null,
        val errorKey: String? = null,
    ) : ApiError()

    data class Unknown(override val message: String = "An unexpected error occurred") : ApiError()

    fun getUserMessage(): String = message ?: "An unexpected error occurred"
}
