package cz.cleansia.partner.core.gdpr

import cz.cleansia.core.network.ApiResult
import cz.cleansia.core.network.safeApiCall
import cz.cleansia.partner.api.client.GdprApi
import javax.inject.Inject
import kotlinx.serialization.json.Json

/**
 * `POST /api/v1/Gdpr/delete-account` for the partner app.
 *
 * The route is shared with the customer app and the name is the customer's, but for a caller who has
 * an `Employee` the server does something different: it FILES a Pending deletion request and changes
 * nothing else. Ending a working relationship needs signed paperwork and an in-person step, and the
 * records that survive it — invoices, pay rows, the self-billing agreement — are financial rather
 * than the subject's to delete. → /decisions/adr-0052
 *
 * Two consequences the caller must honour, and neither is visible in the response:
 *
 *  - **The session is not ended.** Unlike the customer flow, nothing is wiped and no forced sign-out
 *    is emitted. The cleaner keeps working until an admin fulfils the request.
 *  - **Success means "requested", not "deleted".** Copy that says otherwise is a lie the endpoint
 *    used to be able to tell truthfully.
 *
 * A second call while one is pending is refused server-side with `gdpr.deletion_already_pending`,
 * which is the idempotency this screen relies on rather than tracking state locally.
 */
class GdprDeletionClient @Inject constructor(
    private val gdprApi: GdprApi,
    private val json: Json,
) {
    suspend fun requestDeletion(): ApiResult<Unit> =
        safeApiCall(json) { gdprApi.gdprDeleteMyAccount() }
}
