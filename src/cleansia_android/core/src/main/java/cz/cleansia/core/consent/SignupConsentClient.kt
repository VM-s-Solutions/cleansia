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
 * Read off the ProblemDetails `type` and the `errors` bag KEY, never off
 * [ApiError.BadRequest.errorKey]: `CleansiaApiController.CreateProblemDetails` keys that
 * bag by `Error.Code` and values it with `Error.Message`, and this handler's message is
 * prose ("Consent already granted"), so the value never equals the code.
 */
private fun ApiError.namesConsentAlreadyGranted(): Boolean {
    val badRequest = this as? ApiError.BadRequest ?: return false
    return badRequest.code == CONSENT_ALREADY_GRANTED ||
        badRequest.validationErrors?.containsKey(CONSENT_ALREADY_GRANTED) == true
}

private const val CONSENT_ALREADY_GRANTED = "gdpr.consent_already_granted"
