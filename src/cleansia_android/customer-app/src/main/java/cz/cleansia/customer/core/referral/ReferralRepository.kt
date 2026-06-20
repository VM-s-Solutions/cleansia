package cz.cleansia.customer.core.referral
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
 * Cache + orchestrator for the signed-in user's referral state (Phase C).
 *
 * Lifetime: `@Singleton` — lives for the app process. Caches the user's own
 * code + counters ([account]) and the list of referrals they've invited
 * ([referrals]), refreshed lazily by [refresh] from MainShell prefetch.
 *
 * The [validate] method is a direct passthrough used by the signup form and
 * the booking wizard — it doesn't touch repo state because the caller may be
 * unauthenticated (signup) and the result is per-attempt UI feedback, not
 * shared state.
 *
 * Cleared on sign-out / account-delete so the next user doesn't inherit the
 * previous one's code — call sites are wired in [AuthAuthenticator],
 * [AuthRepository], and [UserRepository] alongside the Phase A
 * [LoyaltyRepository] hooks.
 *
 * Error model mirrors [cz.cleansia.customer.core.loyalty.LoyaltyRepository]:
 * foreground operations return [ApiResult.Success] on success and
 * [ApiResult.Error] carrying the parsed message on failure. The consuming
 * ViewModel surfaces the snackbar; an [ApiError.Network] failure stays silent
 * (NetworkErrorInterceptor owns the infra toast).
 */
@Singleton
class ReferralRepository @Inject constructor(
    private val api: ReferralApi,
    @ApplicationContext private val appContext: Context,
) : cz.cleansia.core.auth.SessionScopedCache {
    private val _account = MutableStateFlow<ReferralAccountDto?>(null)
    val account: StateFlow<ReferralAccountDto?> = _account.asStateFlow()

    private val _referrals = MutableStateFlow<List<ReferralListItemDto>>(emptyList())
    val referrals: StateFlow<List<ReferralListItemDto>> = _referrals.asStateFlow()

    private val _loading = MutableStateFlow(false)
    val loading: StateFlow<Boolean> = _loading.asStateFlow()

    private val _loaded = MutableStateFlow(false)
    val loaded: StateFlow<Boolean> = _loaded.asStateFlow()

    /**
     * Fetch both the account snapshot and the first page of invited referrals.
     * Backend's `EnsureCodeForUserAsync` lazily creates the code on first call,
     * so this also acts as the "issue my code" trigger. The referrals page is
     * best-effort (a missing list just renders the empty stats variant).
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

            // Best-effort — most users have <20 referrals; a failure here just
            // leaves the cached list as-is and the stats row falls back to the
            // counters from `account`.
            val referralsResp = networkCall { api.getMyReferrals(offset = 0, limit = 20) }
            if (referralsResp?.isSuccessful == true) {
                _referrals.value = referralsResp.body()?.data ?: emptyList()
            }

            _loaded.value = true
            return ApiResult.Success(Unit)
        } finally {
            _loading.value = false
        }
    }

    /**
     * Validate a code without touching repo state. Safe to call when no JWT
     * exists (signup form) — the auth interceptor skips the header when the
     * token store is empty and the backend endpoint is `[AllowAnonymous]`.
     *
     * Returns [ApiResult.Error] on network/HTTP failure; the caller treats
     * that as a generic "couldn't validate" state and falls back to the
     * informational `error_referral_generic` string.
     */
    suspend fun validate(code: String): ApiResult<ValidateReferralResponse> {
        val resp = networkCall { api.validate(ValidateReferralRequest(code.trim().uppercase())) }
            ?: return networkError()
        if (!resp.isSuccessful) {
            return httpError(resp.errorBody(), resp.code())
        }
        return resp.body()?.let { ApiResult.Success(it) } ?: networkError()
    }

    private fun networkError(): ApiResult<Nothing> =
        ApiResult.Error(ApiError.Network(appContext.getString(R.string.error_generic_network)))

    private fun httpError(errorBody: okhttp3.ResponseBody?, httpCode: Int): ApiResult<Nothing> {
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
        _referrals.value = emptyList()
        _loaded.value = false
    }
}
