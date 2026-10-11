package cz.cleansia.customer.core.memberships

import android.content.Context
import cz.cleansia.customer.R
import cz.cleansia.customer.core.auth.ApiErrorParser
import cz.cleansia.core.auth.SessionScopedCache
import cz.cleansia.core.freshness.Staleness
import cz.cleansia.core.network.ApiError
import cz.cleansia.core.network.ApiResult
import cz.cleansia.core.network.networkCall
import cz.cleansia.core.network.wireResult
import dagger.hilt.android.qualifiers.ApplicationContext
import javax.inject.Inject
import javax.inject.Singleton
import kotlinx.coroutines.CancellationException
import kotlinx.coroutines.CoroutineStart
import kotlinx.coroutines.Deferred
import kotlinx.coroutines.NonCancellable
import kotlinx.coroutines.async
import kotlinx.coroutines.coroutineScope
import kotlinx.coroutines.currentCoroutineContext
import kotlinx.coroutines.ensureActive
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.sync.Mutex
import kotlinx.coroutines.sync.withLock
import kotlinx.coroutines.withContext

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

    /**
     * Freshness watermark for [current]. Screens that observe it typically warm
     * the cache with a "refresh only while null" effect, which never re-fires
     * once the first fetch lands — a subscription bought elsewhere would stay
     * invisible for the process lifetime. Consumers check [Staleness.isStale]
     * on screen entry to decide whether to re-fetch.
     */
    val staleness = Staleness()

    /**
     * A read that began before [clear] belongs to the session that ended: it publishes nothing and is
     * answered with the silent [ApiError.Network], so the previous user's membership never comes back.
     */
    suspend fun refresh(): ApiResult<GetMyMembershipResponse> = wireResult {
        val generation = plansLock.withLock { sessionGeneration }
        mutex.withLock {
            if (plansLock.withLock { generation != sessionGeneration }) return@withLock networkError()
            _loading.value = true
            try {
                val response = networkCall(TAG) { api.getMine() } ?: return@withLock networkError()
                if (!response.isSuccessful) {
                    return@withLock httpError(response.errorBody(), response.code())
                }
                val body = response.body() ?: return@withLock httpError(null, response.code())
                val published = plansLock.withLock {
                    currentCoroutineContext().ensureActive()
                    val sameSession = generation == sessionGeneration
                    if (sameSession) {
                        _current.value = body
                        staleness.markFresh()
                    }
                    sameSession
                }
                return@withLock if (published) ApiResult.Success(body) else networkError()
            } finally {
                _loading.value = false
            }
        }
    }

    /** [countryId] is the chosen market's — the subscription is created in its currency (ADR-0059 D2). */
    suspend fun subscribePhase1(planCode: String, countryId: String?): ApiResult<CreateMembershipSubscriptionResponse> =
        call("subscribePhase1") {
            api.subscribe(
                CreateMembershipSubscriptionRequest(
                    planCode = planCode,
                    paymentMethodConfirmed = false,
                    countryId = countryId,
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
        countryId: String?,
    ): ApiResult<CreateMembershipSubscriptionResponse> {
        val result = call("subscribePhase2") {
            api.subscribe(
                CreateMembershipSubscriptionRequest(
                    planCode = planCode,
                    paymentMethodConfirmed = true,
                    idempotencyToken = idempotencyToken,
                    countryId = countryId,
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
     * Plan catalog for one market, cached per-process until the market changes: the rows are priced in
     * the market's currency, so a list answered for another `countryId` is re-read rather than served
     * (the `CatalogRepository` idiom). An empty answer is cached like any other — it is the server
     * saying Plus is not on sale there. [forceRefresh] busts the cache (e.g. an admin-side change).
     * A caller superseded by a market change or a sign-out is answered with the silent
     * [ApiError.Network], never another market's or session's plans.
     */
    suspend fun getPlans(countryId: String?, forceRefresh: Boolean = false): ApiResult<List<MembershipPlanDto>> = coroutineScope {
        var obsoleteFlight: Deferred<ApiResult<List<MembershipPlanDto>>>? = null
        val generation = plansLock.withLock {
            currentCoroutineContext().ensureActive()
            if (plansRequestCountryId != countryId) {
                plansGeneration++
                plansRequestCountryId = countryId
                obsoleteFlight = plansFlight
                plansFlight = null
            }
            plansGeneration
        }
        obsoleteFlight?.cancel()
        var replaceFlight = forceRefresh
        var minimumFreshSerial = Long.MAX_VALUE
        while (true) {
            obsoleteFlight = null
            val flight = plansLock.withLock {
                currentCoroutineContext().ensureActive()
                if (generation != plansGeneration) return@coroutineScope networkError()
                val cached = _plans.value
                val freshEnough = !forceRefresh || (
                    !replaceFlight && plansFlight == null && plansPublishedSerial >= minimumFreshSerial
                )
                if (freshEnough && cached != null && plansCountryId == countryId) {
                    return@coroutineScope ApiResult.Success(cached)
                }
                if (replaceFlight) {
                    obsoleteFlight = plansFlight
                    plansFlight = null
                    replaceFlight = false
                }
                plansFlight?.takeUnless { it.isCancelled } ?: run {
                    val serial = ++plansFlightSerial
                    if (forceRefresh && minimumFreshSerial == Long.MAX_VALUE) minimumFreshSerial = serial
                    lateinit var ownedFlight: Deferred<ApiResult<List<MembershipPlanDto>>>
                    ownedFlight = async(start = CoroutineStart.LAZY) {
                        try {
                            val result = call("getPlans") { api.getPlans(countryId) }
                            currentCoroutineContext().ensureActive()
                            plansLock.withLock {
                                currentCoroutineContext().ensureActive()
                                if (generation == plansGeneration && plansFlight === ownedFlight) {
                                    result.onSuccess {
                                        _plans.value = it
                                        plansCountryId = countryId
                                        plansPublishedSerial = serial
                                    }
                                }
                            }
                            result
                        } finally {
                            withContext(NonCancellable) {
                                plansLock.withLock {
                                    if (plansFlight === ownedFlight) plansFlight = null
                                }
                            }
                        }
                    }
                    plansFlight = ownedFlight
                    ownedFlight
                }
            }
            obsoleteFlight?.cancel()
            flight.start()
            try {
                val result = flight.await()
                plansLock.withLock {
                    currentCoroutineContext().ensureActive()
                    if (generation != plansGeneration) return@coroutineScope networkError()
                }
                return@coroutineScope result
            } catch (_: CancellationException) {
                currentCoroutineContext().ensureActive()
                plansLock.withLock {
                    if (generation != plansGeneration) return@coroutineScope networkError()
                    if (plansFlight === flight) plansFlight = null
                }
            }
        }
        @Suppress("UNREACHABLE_CODE")
        error("Plan flight loop returned unexpectedly")
    }
    private val plansLock = Mutex()
    private val _plans = MutableStateFlow<List<MembershipPlanDto>?>(null)
    private var plansCountryId: String? = null
    private var plansRequestCountryId: String? = null
    private var plansGeneration = 0L
    private var plansFlightSerial = 0L
    private var plansPublishedSerial = 0L
    private var plansFlight: Deferred<ApiResult<List<MembershipPlanDto>>>? = null

    // Moved only by clear(): plansGeneration also moves on a market change, which must not drop a
    // membership read.
    private var sessionGeneration = 0L

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

    /**
     * Clear cache on sign-out so a re-login starts fresh. Never takes [mutex]: the authenticator runs
     * this on OkHttp's thread while a refresh may hold it across the very request being authenticated.
     * For the same reason no `plansLock` section may wait on a request.
     */
    override suspend fun clear() {
        val obsoleteFlight = plansLock.withLock {
            sessionGeneration++
            plansGeneration++
            val previous = plansFlight
            plansFlight = null
            plansRequestCountryId = null
            _current.value = null
            _plans.value = null
            plansCountryId = null
            plansPublishedSerial = 0L
            staleness.reset()
            previous
        }
        obsoleteFlight?.cancel()
    }

    private suspend inline fun <T> call(
        label: String,
        block: () -> retrofit2.Response<T>,
    ): ApiResult<T> = wireResult {
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
