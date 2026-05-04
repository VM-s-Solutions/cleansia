package cz.cleansia.customer.core.loyalty

import android.content.Context
import cz.cleansia.customer.R
import cz.cleansia.customer.core.auth.ApiErrorParser
import cz.cleansia.customer.ui.snackbar.SnackbarController
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
 * snackbar-shown failure on [refresh]/[loadActivity], silent return on
 * any background-friendly variants we add later.
 */
@Singleton
class LoyaltyRepository @Inject constructor(
    private val api: LoyaltyApi,
    private val snackbar: SnackbarController,
    @ApplicationContext private val appContext: Context,
) {
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
     *
     * @return null on success, localized error message on failure (snackbar
     *  surfaced automatically).
     */
    suspend fun refresh(): String? {
        if (_loading.value) return null
        _loading.value = true
        try {
            val accountResp = try {
                api.getMy()
            } catch (t: Throwable) {
                val msg = appContext.getString(R.string.error_generic_network)
                snackbar.showError(msg)
                return msg
            }
            if (!accountResp.isSuccessful) {
                val msg = ApiErrorParser.parseToUserMessage(appContext, accountResp.errorBody(), accountResp.code())
                snackbar.showError(msg)
                return msg
            }
            _account.value = accountResp.body()

            // Tiers are essentially static configuration — fetch once per
            // session is fine. Errors here don't fail the whole refresh; a
            // null tier ladder just means the ladder UI shows a fallback.
            val tiersResp = try {
                api.getTiers()
            } catch (t: Throwable) {
                null
            }
            if (tiersResp?.isSuccessful == true) {
                _tiers.value = tiersResp.body()?.tiers ?: emptyList()
            }

            _loaded.value = true
            return null
        } finally {
            _loading.value = false
        }
    }

    /**
     * Fetch one page of activity. Returns the full response so the caller can
     * paginate (offset/limit). Silent on failure beyond the snackbar.
     */
    suspend fun loadActivity(offset: Int, limit: Int = 20): LoyaltyActivityResponseDto? {
        return try {
            val resp = api.getActivity(offset = offset, limit = limit)
            if (!resp.isSuccessful) {
                val msg = ApiErrorParser.parseToUserMessage(appContext, resp.errorBody(), resp.code())
                snackbar.showError(msg)
                null
            } else {
                resp.body()
            }
        } catch (t: Throwable) {
            snackbar.showError(appContext.getString(R.string.error_generic_network))
            null
        }
    }

    /** Wipe the in-memory cache — called on sign-out so the next user starts fresh. */
    suspend fun clear() {
        _account.value = null
        _tiers.value = emptyList()
        _loaded.value = false
    }
}
