package cz.cleansia.core.consent

import cz.cleansia.core.network.ApiError
import cz.cleansia.core.network.ApiResult

/**
 * Per-app binding seam for the GDPR consent endpoints, mirroring
 * [cz.cleansia.core.notifications.DeviceRegistrationClient]: each app implements it
 * over its own OpenAPI-generated `GdprApi` so the parking and delivery rules live
 * once, in [SignupConsentRepository].
 */
interface SignupConsentClient {

    /**
     * Every consent type the account has already answered — granted **or withdrawn**.
     * Null when the read itself failed, which is not the same as "answered nothing".
     */
    suspend fun answeredTypes(): Set<SignupConsentType>?

    suspend fun grant(type: SignupConsentType): ConsentGrantOutcome
}

enum class ConsentGrantOutcome {
    Recorded,

    /** The backend refused a duplicate. The record it refuses to duplicate is the one we came to write. */
    AlreadyOnFile,

    Failed,
}

fun ApiResult<*>.toConsentGrantOutcome(): ConsentGrantOutcome = when (this) {
    is ApiResult.Success -> ConsentGrantOutcome.Recorded
    is ApiResult.Error ->
        if (error.namesConsentAlreadyGranted()) ConsentGrantOutcome.AlreadyOnFile else ConsentGrantOutcome.Failed
}

/**
 * Read off [ApiError.BadRequest.errorKey] — the first VALUE in the ProblemDetails `errors` bag.
 * `CleansiaApiController.CreateProblemDetails` keys that bag by `Error.Code` (the offending field,
 * `ConsentType`) and values it with `Error.Message` (the business key), which is the same slot the
 * web and iOS clients resolve their translations from. Matching the bag KEY or the `type` instead
 * would be matching the field name.
 */
private fun ApiError.namesConsentAlreadyGranted(): Boolean {
    val badRequest = this as? ApiError.BadRequest ?: return false
    return badRequest.errorKey == CONSENT_ALREADY_GRANTED
}

private const val CONSENT_ALREADY_GRANTED = "gdpr.consent_already_granted"
