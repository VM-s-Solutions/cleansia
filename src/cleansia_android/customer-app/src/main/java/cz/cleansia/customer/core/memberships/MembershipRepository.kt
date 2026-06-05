package cz.cleansia.customer.core.memberships
import cz.cleansia.core.auth.SessionScopedCache

import android.util.Log
import cz.cleansia.core.network.networkCall
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.sync.Mutex
import kotlinx.coroutines.sync.withLock
import javax.inject.Inject
import javax.inject.Singleton

/**
 * Wraps [MembershipApi] with a small in-memory cache of the user's current
 * membership status. Used by both the management screen (read) and the
 * subscribe flow (writes that invalidate the cache). Same swallow-and-log
 * pattern as the other customer repos.
 */
@Singleton
class MembershipRepository @Inject constructor(
    private val api: MembershipApi,
) : cz.cleansia.core.auth.SessionScopedCache {
    private val mutex = Mutex()

    private val _current = MutableStateFlow<GetMyMembershipResponse?>(null)
    val current: StateFlow<GetMyMembershipResponse?> = _current.asStateFlow()

    private val _loading = MutableStateFlow(false)
    val loading: StateFlow<Boolean> = _loading.asStateFlow()

    /**
     * Fetch the user's current membership state. Returns the response on
     * success, null on failure (with the cached value preserved for the
     * UI to keep rendering).
     */
    suspend fun refresh(): GetMyMembershipResponse? = mutex.withLock {
        _loading.value = true
        try {
            val response = networkCall { api.getMine() }
            if (response?.isSuccessful == true) {
                val body = response.body()
                if (body != null) {
                    _current.value = body
                    return@withLock body
                }
            }
            Log.w(TAG, "getMine failed: HTTP ${response?.code()}")
            return@withLock null
        } finally {
            _loading.value = false
        }
    }

    suspend fun subscribePhase1(planCode: String): CreateMembershipSubscriptionResponse? {
        return runWithLog("subscribePhase1") {
            api.subscribe(
                CreateMembershipSubscriptionRequest(
                    planCode = planCode,
                    paymentMethodConfirmed = false,
                ),
            )
        }
    }

    /**
     * Phase 2 — create the Stripe subscription. [idempotencyToken] is the
     * SAME token generated once at Phase-1 (see [MembershipViewModel.startSubscribe]);
     * it must be passed UNCHANGED on every retry so the backend
     * collapses concurrent/retried confirms onto one subscription.
     */
    suspend fun subscribePhase2(
        planCode: String,
        idempotencyToken: String?,
    ): CreateMembershipSubscriptionResponse? {
        val resp = runWithLog("subscribePhase2") {
            api.subscribe(
                CreateMembershipSubscriptionRequest(
                    planCode = planCode,
                    paymentMethodConfirmed = true,
                    idempotencyToken = idempotencyToken,
                ),
            )
        }
        // Phase 2 success — invalidate cache so the management card re-fetches.
        if (resp != null) refresh()
        return resp
    }

    suspend fun cancel(): CancelMembershipSubscriptionResponse? {
        val resp = runWithLog("cancel") { api.cancel() }
        if (resp != null) refresh()
        return resp
    }

    /**
     * Plan catalog. Cached per-process — plans don't change often, no need
     * to re-fetch on every screen open. Caller can pass [forceRefresh] to
     * bust the cache (e.g. an admin-side change).
     */
    suspend fun getPlans(forceRefresh: Boolean = false): List<MembershipPlanDto> {
        val cached = _plans.value
        if (!forceRefresh && cached.isNotEmpty()) return cached
        val response = runWithLog("getPlans") { api.getPlans() } ?: return cached
        _plans.value = response
        return response
    }
    private val _plans = MutableStateFlow<List<MembershipPlanDto>>(emptyList())

    /**
     * Swap to a different plan. Returns the swap response on success and
     * refreshes the active membership cache so the management UI shows the
     * new plan + period end without a follow-up call.
     */
    suspend fun swapPlan(newPlanCode: String): SwapMembershipPlanResponse? {
        val resp = runWithLog("swapPlan") {
            api.swapPlan(SwapMembershipPlanRequest(newPlanCode))
        }
        if (resp != null) refresh()
        return resp
    }

    /** Clear cache on sign-out so a re-login starts fresh. */
    override suspend fun clear() {
        _current.value = null
        _plans.value = emptyList()
    }

    private suspend inline fun <T> runWithLog(
        label: String,
        block: () -> retrofit2.Response<T>,
    ): T? {
        val response = networkCall(label) { block() } ?: return null
        return if (response.isSuccessful) {
            response.body()
        } else {
            Log.w(TAG, "$label failed: HTTP ${response.code()}")
            null
        }
    }

    private companion object {
        const val TAG = "MembershipRepository"
    }
}
