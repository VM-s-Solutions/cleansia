package cz.cleansia.customer.core.loyalty
import cz.cleansia.core.auth.SessionScopedCache
import cz.cleansia.core.auth.AuthAuthenticator

import android.content.Context
import cz.cleansia.customer.R
import cz.cleansia.customer.core.auth.ApiErrorParser
import cz.cleansia.core.network.ApiError
import cz.cleansia.core.network.ApiResult
import cz.cleansia.core.network.networkCall
import dagger.hilt.android.qualifiers.ApplicationContext
import javax.inject.Inject
import javax.inject.Singleton
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow

/**
 * Cache + orchestrator for the signed-in user's loyalty state.
 *
 * Lifetime: `@Singleton` — lives for the app process. Caches the account
 * snapshot ([account]) and the tier ladder ([tiers]), both refreshed
 * lazily by [refresh]. Activity history is *not* cached — the activity
 * screen pages from the network on demand.
 *
 * Cleared on sign-out / account-delete so the next user doesn't inherit
 * this one's tier — call sites are wired in [AuthAuthenticator],
 * [AuthRepository], and [UserRepository] alongside the matching
 * OrderRepository / DisputeRepository hooks.
 *
 * Error model mirrors [cz.cleansia.customer.core.orders.OrderRepository]:
 * foreground operations return [ApiResult.Success] on success and
 * [ApiResult.Error] carrying the parsed message on failure. The consuming
 * ViewModel surfaces the snackbar; an [ApiError.Network] failure stays
 * silent (NetworkErrorInterceptor owns the infra toast).
 */
@Singleton
class LoyaltyRepository @Inject constructor(
    private val api: LoyaltyApi,
    @ApplicationContext private val appContext: Context,
) : cz.cleansia.core.auth.SessionScopedCache {
    private val _account = MutableStateFlow<LoyaltyAccountDto?>(null)
    val account: StateFlow<LoyaltyAccountDto?> = _account.asStateFlow()

    private val _tiers = MutableStateFlow<List<TierInfoDto>>(emptyList())
    val tiers: StateFlow<List<TierInfoDto>> = _tiers.asStateFlow()

    private val _loading = MutableStateFlow(false)
    val loading: StateFlow<Boolean> = _loading.asStateFlow()

    private val _loaded = MutableStateFlow(false)
    val loaded: StateFlow<Boolean> = _loaded.asStateFlow()

    /**
     * Fetch the account + tier ladder in a single screen-load pass. Shown by
     * MainShell on first composition (lazy-prefetch alongside catalog/orders).
     */
    suspend fun refresh(): ApiResult<Unit> {
        if (_loading.value) return ApiResult.Success(Unit)
        _loading.value = true
        try {
            val accountResp = networkCall { api.getMy() } ?: return networkError()
            if (!accountResp.isSuccessful) {
                return httpError(accountResp.errorBody(), accountResp.code())
            }
            _account.value = accountResp.body()

            // Tiers are essentially static configuration — fetch once per
            // session is fine. Errors here don't fail the whole refresh; a
            // null tier ladder just means the ladder UI shows a fallback.
            val tiersResp = networkCall { api.getTiers() }
            if (tiersResp?.isSuccessful == true) {
                _tiers.value = tiersResp.body()?.tiers ?: emptyList()
            }

            _loaded.value = true
            return ApiResult.Success(Unit)
        } finally {
            _loading.value = false
        }
    }

    /**
     * Fetch one page of activity. Returns the full response so the caller can
     * paginate (offset/limit).
     */
    suspend fun loadActivity(offset: Int, limit: Int = 20): ApiResult<LoyaltyActivityResponseDto> {
        val resp = networkCall { api.getActivity(offset = offset, limit = limit) } ?: return networkError()
        if (!resp.isSuccessful) {
            return httpError(resp.errorBody(), resp.code())
        }
        return resp.body()?.let { ApiResult.Success(it) } ?: networkError()
    }

    private fun networkError(): ApiResult<Nothing> =
        ApiResult.Error(ApiError.Network(appContext.getString(R.string.error_generic_network)))

    private fun httpError(errorBody: okhttp3.ResponseBody?, httpCode: Int): ApiResult<Nothing> {
        // Carry the message [ApiErrorParser] already resolved from the body so
        // the surfacing ViewModel shows the identical string. The 401 object
        // would drop that message, so it folds into the message-carrying
        // [ApiError.Unknown] alongside the generic fallback.
        val message = ApiErrorParser.parseToUserMessage(appContext, errorBody, httpCode)
        val error = when (httpCode) {
            404 -> ApiError.NotFound(message)
            400 -> ApiError.BadRequest(message)
            in 500..599 -> ApiError.Server(statusCode = httpCode, message = message)
            else -> ApiError.Unknown(message)
        }
        return ApiResult.Error(error)
    }

    /** Wipe the in-memory cache — called on sign-out so the next user starts fresh. */
    override suspend fun clear() {
        _account.value = null
        _tiers.value = emptyList()
        _loaded.value = false
    }
}
