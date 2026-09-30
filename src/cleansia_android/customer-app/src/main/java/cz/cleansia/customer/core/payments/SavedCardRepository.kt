package cz.cleansia.customer.core.payments

import cz.cleansia.core.auth.SessionScopedCache
import cz.cleansia.core.network.ApiResult
import cz.cleansia.core.network.safeApiCall
import javax.inject.Inject
import javax.inject.Singleton
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.update
import kotlinx.serialization.json.Json

/** [cards] is null until the first read answers; a capture Stripe has not confirmed yet is not in it. */
@Singleton
class SavedCardRepository @Inject constructor(
    private val api: SavedCardApi,
    private val json: Json,
) : SessionScopedCache {

    private val _cards = MutableStateFlow<List<SavedCard>?>(null)
    val cards: StateFlow<List<SavedCard>?> = _cards.asStateFlow()

    suspend fun refresh(): ApiResult<List<SavedCard>> =
        safeApiCall(json) { api.getMine() }.onSuccess { _cards.value = it }

    suspend fun startCapture(consentAccepted: Boolean, countryId: String?): ApiResult<SavedCardSetup> =
        safeApiCall(json) { api.createSetupIntent(consentAccepted, countryId) }

    suspend fun remove(savedCardId: String): ApiResult<Unit> =
        safeApiCall(json) { api.remove(savedCardId) }
            .map { }
            .onSuccess { _cards.update { cards -> cards?.filterNot { it.id == savedCardId } } }

    override suspend fun clear() {
        _cards.value = null
    }
}
