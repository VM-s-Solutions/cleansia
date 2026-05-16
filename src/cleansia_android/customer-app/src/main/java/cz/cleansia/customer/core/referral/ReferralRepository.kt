package cz.cleansia.customer.core.referral
import cz.cleansia.core.auth.SessionScopedCache
import cz.cleansia.core.auth.AuthAuthenticator

import android.content.Context
import cz.cleansia.customer.R
import cz.cleansia.customer.core.auth.ApiErrorParser
import cz.cleansia.core.network.networkCall
import cz.cleansia.core.snackbar.SnackbarController
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
 */
@Singleton
class ReferralRepository @Inject constructor(
    private val api: ReferralApi,
    private val snackbar: SnackbarController,
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
     * so this also acts as the "issue my code" trigger. Snackbar surfaced on
     * the account fetch failure; the referrals page is best-effort (a missing
     * list just renders the empty stats variant).
     *
     * @return null on success, localized error message on the account-fetch failure.
     */
    suspend fun refresh(): String? {
        if (_loading.value) return null
        _loading.value = true
        try {
            val accountResp = networkCall { api.getMy() }
                ?: return appContext.getString(R.string.error_generic_network)
            if (!accountResp.isSuccessful) {
                val msg = ApiErrorParser.parseToUserMessage(appContext, accountResp.errorBody(), accountResp.code())
                snackbar.showError(msg)
                return msg
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
            return null
        } finally {
            _loading.value = false
        }
    }

    /**
     * Validate a code without touching repo state. Safe to call when no JWT
     * exists (signup form) — the auth interceptor skips the header when the
     * token store is empty and the backend endpoint is `[AllowAnonymous]`.
     *
     * Returns null on network/HTTP failure; the caller treats that as a
     * generic "couldn't validate" state and falls back to the informational
     * `error_referral_generic` string.
     */
    suspend fun validate(code: String): ValidateReferralResponse? {
        val resp = networkCall { api.validate(ValidateReferralRequest(code.trim().uppercase())) }
            ?: return null
        return if (resp.isSuccessful) resp.body() else null
    }

    /** Wipe the in-memory cache — called on sign-out so the next user starts fresh. */
    override suspend fun clear() {
        _account.value = null
        _referrals.value = emptyList()
        _loaded.value = false
    }
}
