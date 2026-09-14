package cz.cleansia.customer.core.consent

import cz.cleansia.core.consent.ConsentGrantOutcome
import cz.cleansia.core.consent.SignupConsentClient
import cz.cleansia.core.consent.SignupConsentType
import cz.cleansia.core.consent.toConsentGrantOutcome
import cz.cleansia.core.network.mapWire
import cz.cleansia.core.network.required
import cz.cleansia.core.network.safeApiCall
import cz.cleansia.customer.api.client.GdprApi
import cz.cleansia.customer.api.model.ConsentType as ApiConsentType
import cz.cleansia.customer.api.model.GrantConsentCommand
import javax.inject.Inject
import kotlinx.serialization.json.Json

/**
 * Customer-app binding of the shared [SignupConsentClient] over the OpenAPI-generated
 * [GdprApi] (`/api/v1/Gdpr/consents`), plus the booking review step's read of what is on record.
 */
class GdprConsentClient @Inject constructor(
    private val gdprApi: GdprApi,
    private val json: Json,
) : SignupConsentClient {

    /**
     * `UserConsentDto.ConsentType` is non-nullable in C#, so a null one is a broken row and the whole
     * answer is refused — a dropped row reads as "never answered", which re-asks for a consent the
     * user has since withdrawn and re-grants it. `fromWireValue` returning null is a different fact,
     * a `ConsentType` this app has no name for, and stays a drop.
     *
     * Deliberately not filtered by `isGranted`: a withdrawn row is an answer.
     */
    override suspend fun answeredTypes(): Set<SignupConsentType>? =
        safeApiCall(json) { gdprApi.gdprGetMyConsents() }
            .mapWire { consents ->
                consents
                    .map { it.consentType.required("consentType") }
                    .mapNotNull { SignupConsentType.fromWireValue(it.value) }
                    .toSet()
            }
            .getOrNull()

    /**
     * The consents currently in force — granted and not since withdrawn, the web wizard's own
     * predicate. Null when the read failed; the caller treats that as "ask", never as "none".
     */
    suspend fun grantedTypes(): Set<SignupConsentType>? =
        safeApiCall(json) { gdprApi.gdprGetMyConsents() }
            .mapWire { consents ->
                consents
                    .filter { it.isGranted == true && it.withdrawnAt == null }
                    .map { it.consentType.required("consentType") }
                    .mapNotNull { SignupConsentType.fromWireValue(it.value) }
                    .toSet()
            }
            .getOrNull()

    override suspend fun grant(type: SignupConsentType): ConsentGrantOutcome {
        val apiType = ApiConsentType.entries.firstOrNull { it.value == type.wireValue }
            ?: return ConsentGrantOutcome.Failed
        return safeApiCall(json) {
            gdprApi.gdprGrantConsent(GrantConsentCommand(consentType = apiType))
        }.toConsentGrantOutcome()
    }
}
