package cz.cleansia.partner.data.dashboard

import cz.cleansia.core.auth.SessionScopedCache
import cz.cleansia.core.freshness.Staleness
import cz.cleansia.partner.api.client.DashboardApi
import cz.cleansia.partner.api.model.AvailableJobPreviewDto
import cz.cleansia.partner.api.model.AvailableJobsPreviewResponse
import cz.cleansia.partner.api.model.DashboardStatsDto
import cz.cleansia.partner.api.model.EarningsAnalyticsDto
import cz.cleansia.partner.api.model.OrderListItem
import cz.cleansia.partner.api.model.SortDefinition
import cz.cleansia.partner.api.model.SortDirection
import cz.cleansia.core.network.ApiError
import cz.cleansia.core.network.ApiResult
import cz.cleansia.core.network.safeApiCall
import cz.cleansia.partner.data.mapWire
import cz.cleansia.partner.data.required
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.update
import kotlinx.coroutines.sync.Mutex
import kotlinx.coroutines.sync.withLock
import kotlinx.serialization.json.Json
import javax.inject.Inject
import javax.inject.Singleton

/**
 * The cleaner's own money, as the dashboard and the Pay & Earnings screen render it.
 *
 * `averageRating` is the one figure that is genuinely absent for a cleaner nobody has rated yet, and
 * the spec says so; every other number here is `nullable: false`, so a null is a renamed or broken
 * wire field and never a zero. The counts are refused alongside the money because they are rendered
 * in the same breath — "0 jobs done this week" beside a real week total is as false a sentence as a
 * zeroed total.
 */
data class DashboardStats(
    val availableOrdersCount: Int,
    val myActiveOrdersCount: Int,
    val thisMonthCompletedOrders: Int,
    val lastMonthCompletedOrders: Int,
    val todayEarnings: Double,
    val todayCompletedCount: Int,
    val weekEarnings: Double,
    val weekCompletedCount: Int,
    val lastMonthEarnings: Double,
    val currentPeriodEarnings: Double,
    val currentPayPeriodStart: String?,
    val currentPayPeriodEnd: String?,
    val nextPayoutDate: String?,
    val averageRating: Double?,
    val ratingCount: Int,
    val latestInvoiceStatus: String?,
    val currencyCode: String?,
)

data class AvailableJobsPreview(
    val jobs: List<AvailableJobPreview>,
    val totalPotentialEarnings: Double,
    val totalAvailableCount: Int,
)

data class AvailableJobPreview(
    val id: String,
    val displayOrderNumber: String?,
    val customerAddressApproximate: String?,
    val cleaningDateTime: String?,
    val totalPrice: Double,
)

/**
 * Cached snapshot of everything the dashboard renders. Held in the
 * singleton repository so the data survives tab swipes — the screen
 * observes [DashboardRepository.snapshot] and only sees a spinner on
 * the very first load, not on every return to the tab.
 */
data class DashboardSnapshot(
    val stats: DashboardStats? = null,
    val upcoming: List<OrderListItem> = emptyList(),
    val availableJobsPreview: AvailableJobsPreview? = null,
    /** True once at least one successful load has populated the cache. */
    val loaded: Boolean = false,
    /** True while a network refresh is in flight. */
    val refreshing: Boolean = false,
)

interface DashboardRepository {
    /** Cached dashboard data. Screens observe this; never null. */
    val snapshot: StateFlow<DashboardSnapshot>

    /**
     * Loads the dashboard into [snapshot]. No-ops when the cache is
     * already loaded and fresher than the staleness window, UNLESS
     * [force] is true (pull-to-refresh). Returns the first error
     * encountered (stats is the critical call), or null on success.
     */
    suspend fun refresh(employeeId: String?, force: Boolean): ApiError?

    /**
     * Drops the freshness watermark so the next [refresh] call hits the
     * network even when [force] is false. Call after a mutation that
     * could affect dashboard stats (order taken / started / completed,
     * etc.) — followed by a non-forced [refresh] so the silent-stale
     * background path picks up the fresh data without a chunky pull
     * indicator (the mutation's own button-spinner already gave the
     * user feedback).
     */
    fun invalidate()

    /**
     * One-shot stats fetch, bypassing the dashboard cache. Used by the
     * standalone Pay & Earnings screen, which wants its own load
     * independent of the dashboard tab's cached snapshot.
     */
    suspend fun getStats(employeeId: String?): ApiResult<DashboardStats>

    /**
     * Unmapped on purpose: nothing reads this yet, so it has no coercion site and no reader to protect.
     * The generated return type is the tell — the first screen that renders it owns the mapper and the
     * domain vocabulary for the three schemas behind it. Note that `highestMonth` / `lowestMonth` carry
     * no `nullable: true` because OpenAPI 3.0 cannot hang sibling keywords off a bare `$ref`; the
     * backing record declares them nullable and the handler fills them by `MaxBy` over a list that is
     * empty for a cleaner with no invoices.
     */
    suspend fun getEarningsAnalytics(
        employeeId: String,
        startDate: String,
        endDate: String,
    ): ApiResult<EarningsAnalyticsDto>
}

@Singleton
class DashboardRepositoryImpl @Inject constructor(
    private val dashboardApi: DashboardApi,
    private val json: Json,
) : DashboardRepository, SessionScopedCache {

    private val _snapshot = MutableStateFlow(DashboardSnapshot())
    override val snapshot: StateFlow<DashboardSnapshot> = _snapshot.asStateFlow()

    // Dedups concurrent refreshes (ON_RESUME + pull + init can race on
    // cold start). Whoever holds the lock loads; the others then see the
    // fresh cache via the staleness check and bail.
    private val refreshLock = Mutex()

    // Per-cache freshness watermark — replaces the old manual nanoTime
    // tracking. Background paths (init / ON_RESUME) consult
    // [Staleness.isStale] via the check below; user pulls bypass it via
    // [force]. 60s window preserves the original behavior: long enough
    // that swiping tabs back and forth never re-fetches, short enough
    // that returning after taking/completing an order shows fresh
    // numbers on resume.
    private val staleness = Staleness()

    override suspend fun refresh(employeeId: String?, force: Boolean): ApiError? {
        refreshLock.withLock {
            val fresh = _snapshot.value.loaded && !staleness.isStale(STALE_WINDOW_MS)
            if (fresh && !force) return null

            _snapshot.update { it.copy(refreshing = true) }
            var firstError: ApiError? = null

            when (val statsResult = getStats(employeeId)) {
                is ApiResult.Success -> _snapshot.update { it.copy(stats = statsResult.data) }
                is ApiResult.Error -> firstError = statsResult.error
            }

            if (!employeeId.isNullOrBlank()) {
                when (val upcoming = getUpcomingOrders(employeeId, 10)) {
                    is ApiResult.Success -> _snapshot.update { it.copy(upcoming = upcoming.data) }
                    is ApiResult.Error -> { /* non-critical — keep dashboard usable */ }
                }
            } else {
                _snapshot.update { it.copy(upcoming = emptyList()) }
            }

            when (val preview = getAvailableJobsPreview(5)) {
                is ApiResult.Success -> _snapshot.update { it.copy(availableJobsPreview = preview.data) }
                is ApiResult.Error -> { /* non-critical */ }
            }

            // Mark fresh even when non-critical sub-calls fail — stats is
            // the load-bearing call, and the snapshot already preserves
            // last-known-good values for the optional sections. If even
            // stats failed, firstError surfaces and the caller can react,
            // but we still stamp so we don't hammer the network on every
            // resume; the next user pull will force-bypass anyway.
            staleness.markFresh()
            _snapshot.update { it.copy(loaded = true, refreshing = false) }
            return firstError
        }
    }

    override fun invalidate() {
        staleness.reset()
    }

    override suspend fun clear() {
        _snapshot.value = DashboardSnapshot()
        staleness.reset()
    }

    override suspend fun getStats(employeeId: String?): ApiResult<DashboardStats> =
        safeApiCall(json) { dashboardApi.dashboardGetStats(employeeId) }.mapWire { it.toDomain() }

    private suspend fun getUpcomingOrders(
        employeeId: String,
        limit: Int,
    ): ApiResult<List<OrderListItem>> =
        safeApiCall(json) {
            dashboardApi.dashboardGetUpcomingOrders(
                filterEmployeeId = employeeId,
                filterIsActive = true,
                sort = listOf(SortDefinition(field = "cleaningDateTime", direction = SortDirection._0)),
                offset = 0,
                limit = limit,
            )
        }.map { it.data.orEmpty() }

    private suspend fun getAvailableJobsPreview(limit: Int): ApiResult<AvailableJobsPreview> =
        safeApiCall(json) { dashboardApi.dashboardGetAvailableJobsPreview(limit) }
            .mapWire { it.toDomain() }

    override suspend fun getEarningsAnalytics(
        employeeId: String,
        startDate: String,
        endDate: String,
    ): ApiResult<EarningsAnalyticsDto> = safeApiCall(json) {
        dashboardApi.dashboardGetEarningsAnalytics(employeeId, startDate, endDate)
    }

    private companion object {
        // 60s: long enough that swiping tabs back and forth never
        // re-fetches, short enough that returning after taking/completing
        // an order shows fresh numbers on resume. Intentionally longer
        // than [Staleness.DEFAULT_MAX_AGE_MS] (30s) — dashboard stats
        // change less frequently than per-order list state.
        const val STALE_WINDOW_MS = 60_000L
    }
}

internal fun DashboardStatsDto.toDomain() = DashboardStats(
    availableOrdersCount = availableOrdersCount.required("availableOrdersCount"),
    myActiveOrdersCount = myActiveOrdersCount.required("myActiveOrdersCount"),
    thisMonthCompletedOrders = thisMonthCompletedOrders.required("thisMonthCompletedOrders"),
    lastMonthCompletedOrders = lastMonthCompletedOrders.required("lastMonthCompletedOrders"),
    todayEarnings = todayEarnings.required("todayEarnings"),
    todayCompletedCount = todayCompletedCount.required("todayCompletedCount"),
    weekEarnings = weekEarnings.required("weekEarnings"),
    weekCompletedCount = weekCompletedCount.required("weekCompletedCount"),
    lastMonthEarnings = lastMonthEarnings.required("lastMonthEarnings"),
    currentPeriodEarnings = currentPeriodEarnings.required("currentPeriodEarnings"),
    currentPayPeriodStart = currentPayPeriodStart,
    currentPayPeriodEnd = currentPayPeriodEnd,
    nextPayoutDate = nextPayoutDate,
    averageRating = averageRating,
    ratingCount = ratingCount.required("ratingCount"),
    latestInvoiceStatus = latestInvoiceStatus,
    currencyCode = currencyCode,
)

/**
 * `jobs` keeps the period-pay ruling — drop the unidentifiable row — because the hero renders
 * `totalPotentialEarnings` and `totalAvailableCount`, which the server supplies independently over
 * every available job while `jobs` is only the first few. The list is not the addends of the figure
 * on screen, so a lost row cannot falsify it, while failing the mapping would blank a hero the server
 * answered correctly. Where a collection *is* the addends the ruling inverts; see
 * [cz.cleansia.partner.data.invoices.toDomain].
 *
 * A surviving row's own `totalPrice` is still refused, because that is the money rule and not the
 * identity one: the row is either faithful or absent, never a zero-priced job.
 */
internal fun AvailableJobsPreviewResponse.toDomain() = AvailableJobsPreview(
    jobs = jobs.orEmpty().mapNotNull { it.toDomainOrNull() },
    totalPotentialEarnings = totalPotentialEarnings.required("totalPotentialEarnings"),
    totalAvailableCount = totalAvailableCount.required("totalAvailableCount"),
)

internal fun AvailableJobPreviewDto.toDomainOrNull(): AvailableJobPreview? {
    val jobId = id ?: return null
    return AvailableJobPreview(
        id = jobId,
        displayOrderNumber = displayOrderNumber,
        customerAddressApproximate = customerAddressApproximate,
        cleaningDateTime = cleaningDateTime,
        totalPrice = totalPrice.required("totalPrice"),
    )
}
