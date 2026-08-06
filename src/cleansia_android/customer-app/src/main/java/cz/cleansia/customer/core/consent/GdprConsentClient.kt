package cz.cleansia.customer.core.consent

import cz.cleansia.core.consent.ConsentGrantOutcome
import cz.cleansia.core.consent.SignupConsentClient
import cz.cleansia.core.consent.SignupConsentType
import cz.cleansia.core.consent.toConsentGrantOutcome
import cz.cleansia.core.network.ApiResult
import cz.cleansia.core.network.safeApiCall
import cz.cleansia.customer.api.client.GdprApi
import cz.cleansia.customer.api.model.ConsentType as ApiConsentType
import cz.cleansia.customer.api.model.GrantConsentCommand
import javax.inject.Inject
import kotlinx.serialization.json.Json

/**
 * Customer-app binding of the shared [SignupConsentClient] over the OpenAPI-generated
 * [GdprApi] (`/api/v1/Gdpr/consents`).
 */
class GdprConsentClient @Inject constructor(
    private val gdprApi: GdprApi,
    private val json: Json,
) : SignupConsentClient {

    override suspend fun answeredTypes(): Set<SignupConsentType>? =
        when (val result = safeApiCall(json) { gdprApi.gdprGetMyConsents() }) {
            // Deliberately not filtered by `isGranted`: a withdrawn row is an answer, and
            // re-granting it would resurrect a consent the user has since taken back.
            is ApiResult.Success -> result.data
                .mapNotNull { SignupConsentType.fromWireValue(it.consentType?.value ?: return@mapNotNull null) }
                .toSet()
            is ApiResult.Error -> null
        }

    override suspend fun grant(type: SignupConsentType): ConsentGrantOutcome {
        val apiType = ApiConsentType.entries.firstOrNull { it.value == type.wireValue }
            ?: return ConsentGrantOutcome.Failed
        return safeApiCall(json) {
            gdprApi.gdprGrantConsent(GrantConsentCommand(consentType = apiType))
        }.toConsentGrantOutcome()
    }
}
