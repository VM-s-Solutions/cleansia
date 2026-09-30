package cz.cleansia.customer.core.payments

import cz.cleansia.core.network.ApiResult
import cz.cleansia.core.network.safeApiCall
import javax.inject.Inject
import javax.inject.Singleton
import kotlinx.serialization.json.Json

// Stateless — nothing cached, so no SessionScopedCache.
@Singleton
class ReceivableRepository @Inject constructor(
    private val api: ReceivableApi,
    private val json: Json,
) {
    suspend fun getMine(): ApiResult<List<Receivable>> = safeApiCall(json) { api.getMine() }

    /** The Stripe Checkout page the customer pays the receivable on. */
    suspend fun createPayLink(receivableId: String): ApiResult<String> =
        safeApiCall(json) { api.createPayLink(receivableId) }
}
