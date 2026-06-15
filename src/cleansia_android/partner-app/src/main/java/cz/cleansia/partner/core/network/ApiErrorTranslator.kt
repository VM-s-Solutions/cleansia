package cz.cleansia.partner.core.network

import android.content.Context
import cz.cleansia.core.network.ApiError
import cz.cleansia.partner.R
import dagger.hilt.android.qualifiers.ApplicationContext
import javax.inject.Inject
import javax.inject.Singleton

/**
 * Translates [ApiError] into user-facing localized strings. Backend often
 * returns translation keys in the `errors` payload (e.g.
 * `user.not_existing_email`); we look those up in `string.xml` so the user
 * sees a localized message instead of the raw key.
 *
 * Unknown error keys fall back to the server-supplied message; if the server
 * gave nothing useful, we use a generic "something went wrong" string so the
 * UI never shows an empty toast.
 */
@Singleton
class ApiErrorTranslator @Inject constructor(
    @ApplicationContext private val context: Context,
) {

    fun translate(error: ApiError): String = when (error) {
        is ApiError.Network -> context.getString(R.string.error_network)
        is ApiError.Server -> context.getString(R.string.error_server)
        is ApiError.Unauthorized -> context.getString(R.string.error_unauthorized)
        is ApiError.NotFound -> context.getString(R.string.error_not_found)
        is ApiError.BadRequest -> translateBadRequest(error)
        is ApiError.Unknown -> error.message.ifBlank { context.getString(R.string.error_generic) }
    }

    /**
     * Backend returns validation errors as `{ ValidatorName: "domain.key" }`
     * (sometimes a list). We want to surface the actual error keys to the
     * cleaner, not the generic ProblemDetails detail line ("A validation
     * problem occurred."). Order of preference:
     *
     *   1. Translate every key from the validation map and join them.
     *      Multiple validators usually fail together (e.g. "after photos
     *      required" AND "actual time must be positive") and the cleaner
     *      needs to see all of them to fix the order.
     *   2. Fall back to the single [ApiError.BadRequest.errorKey] if no
     *      structured validation map came through.
     *   3. Last resort: the server's `detail` line.
     *
     * Unknown keys (no `error_key_*` string in resources) render as the
     * key itself — at least the user sees "order.after_photos.required"
     * instead of "A validation problem occurred", which is more
     * actionable until translations get added.
     */
    private fun translateBadRequest(error: ApiError.BadRequest): String {
        val validationKeys = error.validationErrors
            ?.values
            ?.flatten()
            ?.filter { it.isNotBlank() }
            ?.distinct()
            .orEmpty()

        if (validationKeys.isNotEmpty()) {
            return validationKeys.joinToString(separator = "\n") { key ->
                lookupKey(key) ?: key
            }
        }

        val singleKey = error.errorKey
        if (!singleKey.isNullOrBlank()) {
            return lookupKey(singleKey) ?: singleKey
        }
        return error.message
    }

    /**
     * Looks up a server error key (`user.not_existing_email`,
     * `auth.invalid_credentials`, etc.) in string resources. Resource ids are
     * named `error_<dot-replaced-with-underscore>`, e.g.
     * `error_user_not_existing_email` — same convention as the customer app's
     * ApiErrorParser so translations can be cross-referenced.
     *
     * Falls back to the legacy `error_key_<...>` prefix for any historical
     * partner entries that haven't been renamed yet — keeps existing
     * translations working without a flag-day rename.
     *
     * Returns null if neither resource exists so the caller can fall back
     * to the server message.
     */
    private fun lookupKey(key: String): String? {
        val normalized = key.replace('.', '_').replace('-', '_').lowercase()
        val ids = listOf(
            "error_$normalized",
            "error_key_$normalized",
        )
        for (name in ids) {
            val id = context.resources.getIdentifier(name, "string", context.packageName)
            if (id != 0) return context.getString(id)
        }
        return null
    }
}
