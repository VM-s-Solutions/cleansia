package cz.cleansia.partner.data.dashboard

import cz.cleansia.core.auth.SessionScopedCache
import cz.cleansia.core.freshness.Staleness
import cz.cleansia.partner.api.client.DashboardApi
import cz.cleansia.partner.api.model.AvailableJobsPreviewResponse
import cz.cleansia.partner.api.model.DashboardStatsDto
import cz.cleansia.partner.api.model.EarningsAnalyticsDto
import cz.cleansia.partner.api.model.OrderListItem
import cz.cleansia.partner.api.model.SortDefinition
import cz.cleansia.partner.api.model.SortDirection
import cz.cleansia.core.network.ApiError
import cz.cleansia.core.network.ApiResult
import cz.cleansia.core.network.safeApiCall
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
 * Cached snapshot of everything the dashboard renders. Held in the
 * singleton repository so the data survives tab swipes — the screen
 * observes [DashboardRepository.snapshot] and only sees a spinner on
 * the very first load, not on every return to the tab.
 */
data class DashboardSnapshot(
    val stats: DashboardStatsDto? = null,
    val upcoming: List<OrderListItem> = emptyList(),
    val availableJobsPreview: AvailableJobsPreviewResponse? = null,
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
    suspend fun getStats(employeeId: String?): ApiResult<DashboardStatsDto>

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

    override suspend fun getStats(employeeId: String?): ApiResult<DashboardStatsDto> =
        safeApiCall(json) { dashboardApi.dashboardGetStats(employeeId) }

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

    private suspend fun getAvailableJobsPreview(limit: Int): ApiResult<AvailableJobsPreviewResponse> =
        safeApiCall(json) { dashboardApi.dashboardGetAvailableJobsPreview(limit) }

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
