package cz.cleansia.customer.core.auth

import android.content.Context
import cz.cleansia.core.network.ApiError
import cz.cleansia.customer.R
import kotlinx.serialization.Serializable
import kotlinx.serialization.json.Json
import kotlinx.serialization.json.JsonArray
import kotlinx.serialization.json.JsonElement
import kotlinx.serialization.json.JsonPrimitive
import kotlinx.serialization.json.contentOrNull
import okhttp3.ResponseBody

/**
 * Parses the ASP.NET ProblemDetails + `errors` dict produced by the backend
 * and turns it into a user-facing, translated message.
 *
 * Shape of a 400 from the backend:
 * ```json
 * {
 *   "type": "ValidationError",
 *   "title": "Validation Error",
 *   "status": 400,
 *   "detail": "A validation problem occurred.",
 *   "errors": { "Email": "user.not_existing_email" }
 * }
 * ```
 *
 * The resolution strategy:
 * 1. Parse the body → take the FIRST error code from `errors` (screens only
 *    show one message at a time).
 * 2. Transform `"user.not_existing_email"` → `error_user_not_existing_email`
 *    and look it up in strings.xml.
 * 3. Fall back to the backend's `title` + `detail` if no matching string.
 * 4. Fall back to a generic "Something went wrong" if the body is missing/malformed.
 */
object ApiErrorParser {

    private val json = Json { ignoreUnknownKeys = true; isLenient = true }

    // ASP.NET produces two shapes of `errors` depending on the source:
    //   - BusinessErrorMessage keys → Map<String, String>           ("Email": "user.not_existing_email")
    //   - ModelState validation     → Map<String, String[]>         ("Id": ["The Id field is required."])
    // JsonElement accepts either; firstErrorValue() flattens them to a single string.
    @Serializable
    private data class ProblemDetailsBody(
        val title: String? = null,
        val detail: String? = null,
        val status: Int? = null,
        val errors: Map<String, JsonElement>? = null,
    )

    fun parseToUserMessage(context: Context, error: ApiError): String = when (error) {
        is ApiError.BadRequest -> {
            val firstError = error.errorKey ?: error.validationErrors?.values?.firstOrNull()?.firstOrNull()
            firstError?.let { resolveStringByErrorKey(context, it) ?: it.takeIf { k -> k.isNotBlank() } }
                ?: error.message.takeIf { it.isNotBlank() }
                ?: context.getString(R.string.error_generic_unknown)
        }
        is ApiError.Unauthorized -> context.getString(R.string.error_generic_unauthorized)
        is ApiError.Server -> error.message.takeIf { it.isNotBlank() }
            ?: context.getString(R.string.error_generic_server)
        is ApiError.Network -> context.getString(R.string.error_generic_network)
        is ApiError.NotFound -> error.message.takeIf { it.isNotBlank() }
            ?: context.getString(R.string.error_generic_unknown)
        is ApiError.Unknown -> error.message.takeIf { it.isNotBlank() }
            ?: context.getString(R.string.error_generic_unknown)
    }

    fun parseToUserMessage(context: Context, body: ResponseBody?, httpCode: Int): String {
        val raw = runCatching { body?.string() }.getOrNull()
        if (raw.isNullOrBlank()) {
            return genericForStatus(context, httpCode)
        }

        val problem = runCatching { json.decodeFromString<ProblemDetailsBody>(raw) }.getOrNull()
        if (problem == null) {
            return genericForStatus(context, httpCode)
        }

        val firstError = problem.errors?.values?.firstNotNullOfOrNull { firstErrorValue(it) }
        if (firstError != null) {
            // Try key-based lookup first (BusinessErrorMessage pattern),
            // fall through to raw text (ASP.NET ModelState pattern).
            resolveStringByErrorKey(context, firstError)?.let { return it }
            if (firstError.isNotBlank()) return firstError
        }

        return problem.detail?.takeIf { it.isNotBlank() }
            ?: problem.title?.takeIf { it.isNotBlank() }
            ?: genericForStatus(context, httpCode)
    }

    private fun firstErrorValue(element: JsonElement): String? = when (element) {
        is JsonPrimitive -> element.contentOrNull
        is JsonArray -> element.firstOrNull()?.let { (it as? JsonPrimitive)?.contentOrNull }
        else -> null
    }

    private fun genericForStatus(context: Context, httpCode: Int): String = when (httpCode) {
        in 500..599 -> context.getString(R.string.error_generic_server)
        401 -> context.getString(R.string.error_generic_unauthorized)
        else -> context.getString(R.string.error_generic_unknown)
    }

    /**
     * Maps e.g. `"user.not_existing_email"` → looks up `R.string.error_user_not_existing_email`.
     * Returns null if no matching string — caller handles the fallback.
     */
    private fun resolveStringByErrorKey(context: Context, key: String): String? {
        val resName = "error_" + key.replace('.', '_').lowercase()
        val resId = context.resources.getIdentifier(resName, "string", context.packageName)
        return if (resId != 0) context.getString(resId) else null
    }
}
