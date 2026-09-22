package cz.cleansia.customer.core.market

import android.content.Context
import cz.cleansia.customer.R
import cz.cleansia.customer.core.auth.ApiErrorParser
import cz.cleansia.customer.core.settings.AppSettingsRepository
import cz.cleansia.core.freshness.Staleness
import cz.cleansia.core.network.ApiError
import cz.cleansia.core.network.ApiResult
import cz.cleansia.core.network.networkCall
import cz.cleansia.core.network.requiredBody
import cz.cleansia.core.network.wireResult
import dagger.hilt.android.qualifiers.ApplicationContext
import javax.inject.Inject
import javax.inject.Singleton
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.first
import kotlinx.coroutines.sync.Mutex
import kotlinx.coroutines.sync.withLock

/**
 * The market directory and the customer's chosen market (ADR-0058). The directory is public and the
 * choice is a device preference, so nothing here is per-user and it stays out of the session-wipe set,
 * like [cz.cleansia.customer.core.catalog.CatalogRepository].
 *
 * Failures are silent by design: a directory that cannot be read leaves the last known list in place
 * (or [MarketState.Unavailable] when there was none), and every reader carries on without a market.
 */
@Singleton
class MarketRepository @Inject constructor(
    private val api: MarketApi,
    private val settings: AppSettingsRepository,
    @ApplicationContext private val appContext: Context,
) {
    private val mutex = Mutex()
    private var attempted = false

    private val _state = MutableStateFlow<MarketState>(MarketState.Unavailable)
    val state: StateFlow<MarketState> = _state.asStateFlow()

    /** Stamped only by a successful read, so a failed one is retried on the next Home entry. */
    val staleness = Staleness()

    /** The first reader pays for the read; everyone after it gets the answer already on hand. */
    suspend fun ensureLoaded(): MarketState = mutex.withLock {
        if (!attempted) load()
        _state.value
    }

    suspend fun refresh(): ApiResult<MarketState> = mutex.withLock { load() }

    /**
     * The stored code is matched against the list and never trusted on its own: a code the directory
     * no longer lists falls to the default and is overwritten.
     */
    private suspend fun load(): ApiResult<MarketState> = wireResult {
        attempted = true
        val response = networkCall(TAG) { api.getMarkets() } ?: return networkError()
        if (!response.isSuccessful) return httpError(response.errorBody(), response.code())
        val markets = response.requiredBody()
        staleness.markFresh()
        if (markets.isEmpty()) {
            _state.value = MarketState.Unavailable
            return ApiResult.Success(MarketState.Unavailable)
        }
        val stored = settings.settings.first().market
        val selected = markets.firstOrNull { it.isoCode == stored }
            ?: markets.firstOrNull { it.isDefault }
            ?: markets.first()
        if (selected.isoCode != stored) settings.setMarket(selected.isoCode)
        val resolved = MarketState.Resolved(markets, selected)
        _state.value = resolved
        ApiResult.Success(resolved)
    }

    suspend fun select(isoCode: String) {
        val resolved = _state.value as? MarketState.Resolved ?: return
        val market = resolved.markets.firstOrNull { it.isoCode == isoCode } ?: return
        settings.setMarket(market.isoCode)
        _state.value = resolved.copy(selected = market)
    }

    private fun networkError(): ApiResult<MarketState> =
        ApiResult.Error(ApiError.Network(appContext.getString(R.string.error_generic_network)))

    private fun httpError(errorBody: okhttp3.ResponseBody?, httpCode: Int): ApiResult<MarketState> {
        val message = ApiErrorParser.parseToUserMessage(appContext, errorBody, httpCode)
        val error = when (httpCode) {
            404 -> ApiError.NotFound(message)
            400 -> ApiError.BadRequest(message)
            in 500..599 -> ApiError.Server(statusCode = httpCode, message = message)
            else -> ApiError.Unknown(message)
        }
        return ApiResult.Error(error)
    }

    private companion object {
        const val TAG = "MarketRepository"
    }
}
