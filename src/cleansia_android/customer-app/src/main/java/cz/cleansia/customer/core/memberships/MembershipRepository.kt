package cz.cleansia.customer.core.memberships

import android.content.Context
import cz.cleansia.customer.R
import cz.cleansia.customer.core.auth.ApiErrorParser
import cz.cleansia.core.auth.SessionScopedCache
import cz.cleansia.core.network.ApiError
import cz.cleansia.core.network.ApiResult
import cz.cleansia.core.network.networkCall
import dagger.hilt.android.qualifiers.ApplicationContext
import javax.inject.Inject
import javax.inject.Singleton
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.sync.Mutex
import kotlinx.coroutines.sync.withLock

/**
 * Wraps [MembershipApi] with a small in-memory cache of the user's current
 * membership status. Used by both the management screen (read) and the
 * subscribe flow (writes that invalidate the cache).
 *
 * Each call returns [ApiResult.Success] with the body once warm and
 * [ApiResult.Error] carrying the parsed message on failure (the cache is left
 * untouched so the UI keeps rendering). The consuming ViewModel surfaces the
 * snackbar; an [ApiError.Network] failure stays silent (NetworkErrorInterceptor
 * owns the infra toast).
 */
@Singleton
class MembershipRepository @Inject constructor(
    private val api: MembershipApi,
    @ApplicationContext private val appContext: Context,
) : SessionScopedCache {
    private val mutex = Mutex()

    private val _current = MutableStateFlow<GetMyMembershipResponse?>(null)
    val current: StateFlow<GetMyMembershipResponse?> = _current.asStateFlow()

    private val _loading = MutableStateFlow(false)
    val loading: StateFlow<Boolean> = _loading.asStateFlow()

    suspend fun refresh(): ApiResult<GetMyMembershipResponse> = mutex.withLock {
        _loading.value = true
        try {
            val response = networkCall(TAG) { api.getMine() } ?: return@withLock networkError()
            if (!response.isSuccessful) {
                return@withLock httpError(response.errorBody(), response.code())
            }
            val body = response.body() ?: return@withLock httpError(null, response.code())
            _current.value = body
            return@withLock ApiResult.Success(body)
        } finally {
            _loading.value = false
        }
    }

    suspend fun subscribePhase1(planCode: String): ApiResult<CreateMembershipSubscriptionResponse> =
        call("subscribePhase1") {
            api.subscribe(
                CreateMembershipSubscriptionRequest(
                    planCode = planCode,
                    paymentMethodConfirmed = false,
                ),
            )
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
    ): ApiResult<CreateMembershipSubscriptionResponse> {
        val result = call("subscribePhase2") {
            api.subscribe(
                CreateMembershipSubscriptionRequest(
                    planCode = planCode,
                    paymentMethodConfirmed = true,
                    idempotencyToken = idempotencyToken,
                ),
            )
        }
        // Phase 2 success — invalidate cache so the management card re-fetches.
        result.onSuccess { refresh() }
        return result
    }

    suspend fun cancel(): ApiResult<CancelMembershipSubscriptionResponse> {
        val result = call("cancel") { api.cancel() }
        result.onSuccess { refresh() }
        return result
    }

    /**
     * Plan catalog. Cached per-process — plans don't change often, no need
     * to re-fetch on every screen open. Caller can pass [forceRefresh] to
     * bust the cache (e.g. an admin-side change).
     */
    suspend fun getPlans(forceRefresh: Boolean = false): ApiResult<List<MembershipPlanDto>> {
        val cached = _plans.value
        if (!forceRefresh && cached.isNotEmpty()) return ApiResult.Success(cached)
        return call("getPlans") { api.getPlans() }.onSuccess { _plans.value = it }
    }
    private val _plans = MutableStateFlow<List<MembershipPlanDto>>(emptyList())

    /**
     * Swap to a different plan. Returns the swap response on success and
     * refreshes the active membership cache so the management UI shows the
     * new plan + period end without a follow-up call.
     */
    suspend fun swapPlan(newPlanCode: String): ApiResult<SwapMembershipPlanResponse> {
        val result = call("swapPlan") {
            api.swapPlan(SwapMembershipPlanRequest(newPlanCode))
        }
        result.onSuccess { refresh() }
        return result
    }

    /** Clear cache on sign-out so a re-login starts fresh. */
    override suspend fun clear() {
        _current.value = null
        _plans.value = emptyList()
    }

    private suspend inline fun <T> call(
        label: String,
        block: () -> retrofit2.Response<T>,
    ): ApiResult<T> {
        val response = networkCall(label) { block() } ?: return networkError()
        return if (response.isSuccessful) {
            val body = response.body() ?: return httpError(null, response.code())
            ApiResult.Success(body)
        } else {
            httpError(response.errorBody(), response.code())
        }
    }

    private fun <T> networkError(): ApiResult<T> =
        ApiResult.Error(ApiError.Network(appContext.getString(R.string.error_generic_network)))

    private fun <T> httpError(errorBody: okhttp3.ResponseBody?, httpCode: Int): ApiResult<T> {
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
        const val TAG = "MembershipRepository"
    }
}
