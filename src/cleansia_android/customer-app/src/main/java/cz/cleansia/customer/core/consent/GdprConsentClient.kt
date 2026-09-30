package cz.cleansia.customer.core.consent

import cz.cleansia.core.network.mapWire
import cz.cleansia.core.network.required
import cz.cleansia.core.network.safeApiCall
import cz.cleansia.customer.api.client.GdprApi
import javax.inject.Inject
import kotlinx.serialization.json.Json

/**
 * The booking review step's read of what is on record, over the OpenAPI-generated [GdprApi]
 * (`/api/v1/Gdpr/consents`). The signup tick itself travels on the registration request and is
 * granted by the server in the same commit as the account, so nothing here writes.
 */
class GdprConsentClient @Inject constructor(
    private val gdprApi: GdprApi,
    private val json: Json,
) {

    /**
     * The consents currently in force — granted, not since withdrawn, and given to the version of
     * the text in force now. A consent to an older version does not count, so the tick reappears
     * until the customer accepts the new text. Null when the read failed; the caller treats that as
     * "ask", never as "none".
     *
     * `UserConsentDto.ConsentType` and `CoversCurrentVersion` are non-nullable in C#, so a null one
     * on a granted row is a broken row and the whole answer is refused — a dropped row reads as
     * "never granted", which re-asks for a consent the user already holds. `fromWireValue`
     * returning null is a different fact, a `ConsentType` this app has no name for, and stays a drop.
     */
    suspend fun grantedTypes(): Set<SignupConsentType>? =
        safeApiCall(json) { gdprApi.gdprGetMyConsents() }
            .mapWire { consents ->
                consents
                    .filter { it.isGranted == true && it.withdrawnAt == null }
                    .filter { it.coversCurrentVersion.required("coversCurrentVersion") }
                    .map { it.consentType.required("consentType") }
                    .mapNotNull { SignupConsentType.fromWireValue(it.value) }
                    .toSet()
            }
            .getOrNull()
}
