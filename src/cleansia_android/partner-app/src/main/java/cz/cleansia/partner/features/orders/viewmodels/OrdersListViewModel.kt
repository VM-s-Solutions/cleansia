package cz.cleansia.partner.features.orders.viewmodels

import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import cz.cleansia.core.location.LocationService
import cz.cleansia.core.location.UserLocation
import cz.cleansia.core.snackbar.SnackbarController
import cz.cleansia.partner.api.model.OrderListItem
import cz.cleansia.partner.api.model.OrderStatus
import cz.cleansia.partner.core.auth.UserProfileStore
import cz.cleansia.partner.core.location.haversineKm
import cz.cleansia.partner.core.network.ApiErrorTranslator
import cz.cleansia.core.network.ApiResult
import cz.cleansia.partner.data.orders.OrdersMutation
import cz.cleansia.partner.data.orders.OrdersPane
import cz.cleansia.partner.data.orders.OrdersRepository
import dagger.hilt.android.lifecycle.HiltViewModel
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.update
import kotlinx.coroutines.launch
import java.time.Instant
import java.time.LocalDate
import java.time.LocalTime
import java.time.ZoneId
import java.time.ZoneOffset
import java.time.temporal.WeekFields
import java.util.Locale
import javax.inject.Inject

/**
 * Three tabs cover the partner UX:
 *  - [Available]  : unassigned, not yet taken — `IsUnassigned=true`, statuses
 *                   [New, Confirmed]
 *  - [MyActive]   : assigned to me + still active — [Confirmed, OnTheWay, InProgress]
 *  - [MyCompleted]: assigned to me + done — [Completed]
 */
enum class OrdersTab(val label: Int) {
    Available(cz.cleansia.partner.R.string.available),
    MyActive(cz.cleansia.partner.R.string.active),
    MyCompleted(cz.cleansia.partner.R.string.completed);
}

/** Available-tab sort options. Backend supports cleaningDateTime, totalPrice, estimatedCleanerPay. */
enum class AvailableSort(val labelRes: Int, val field: String, val ascending: Boolean) {
    EarningsHighToLow(
        cz.cleansia.partner.R.string.sort_earnings_high_to_low,
        "estimatedCleanerPay",
        ascending = false,
    ),
    SoonestFirst(
        cz.cleansia.partner.R.string.sort_soonest_first,
        "cleaningDateTime",
        ascending = true,
    ),
    PriceHighToLow(
        cz.cleansia.partner.R.string.sort_price_high_to_low,
        "totalPrice",
        ascending = false,
    ),
}

/** Completed-tab period filter. Drives date-range query + summary card scope. */
enum class CompletedPeriod(val labelRes: Int) {
    ThisWeek(cz.cleansia.partner.R.string.period_this_week),
    ThisMonth(cz.cleansia.partner.R.string.period_this_month),
    LastMonth(cz.cleansia.partner.R.string.period_last_month),
    All(cz.cleansia.partner.R.string.period_all);
}

/**
 * Three independent loading flags drive the silent-stale refresh pattern.
 * Splitting them is the whole point: the chunky PullToRefreshBox indicator
 * must NEVER fire from auto-refresh paths, only from a user pull.
 *
 *  - [isInitialLoad]:     first-ever load before any fetch has finished. The
 *                         screen renders a full-page CircularProgressIndicator
 *                         here; nothing else is on screen yet.
 *  - [isUserRefreshing]:  the cleaner pulled to refresh. ONLY this flag drives
 *                         PullToRefreshBox.isRefreshing. Always sets force=true
 *                         on the underlying fetch.
 *  - [isBackgroundRefreshing]: an auto-triggered fetch (init / tab switch /
 *                         ON_RESUME / post-mutation). Invisible to the user;
 *                         exposed on state for tests + potential subtle UI
 *                         hooks (a thin progress line, etc.) but the screen
 *                         currently shows nothing for this.
 */
data class OrdersListUiState(
    val isInitialLoad: Boolean = true,
    val isUserRefreshing: Boolean = false,
    val isBackgroundRefreshing: Boolean = false,
    val tab: OrdersTab = OrdersTab.Available,
    val orders: List<OrderListItem> = emptyList(),
    val error: String? = null,
    // Available-tab controls
    val searchQuery: String = "",
    val availableSort: AvailableSort = AvailableSort.EarningsHighToLow,
    // Completed-tab controls
    val completedPeriod: CompletedPeriod = CompletedPeriod.ThisMonth,
    // Distance state (shared across tabs but only displayed on Available)
    val currentLocation: UserLocation? = null,
    val hasLocationPermission: Boolean = false,
    /**
     * Order whose inline action (Take / OnTheWay / Start / Complete) is
     * currently in flight. Drives the SlideToTake loading state so the
     * user can't double-fire and so the thumb shows a spinner instead of
     * silently re-arming after release. Null when no action is running.
     */
    val inFlightActionOrderId: String? = null,
    /**
     * True once the first load (success or error) has completed.
     * Lets the screen distinguish "we're still showing the initial full-page
     * spinner" from "the user pulled to refresh on an empty list" — the
     * latter must keep the empty state visible so PullToRefreshBox can
     * detect the gesture and show its indicator. Mirrors the
     * InvoicesListUiState pattern.
     */
    val hasLoadedOnce: Boolean = false,
)

@HiltViewModel
class OrdersListViewModel @Inject constructor(
    private val ordersRepository: OrdersRepository,
    private val userProfileStore: UserProfileStore,
    private val errorTranslator: ApiErrorTranslator,
    private val snackbar: SnackbarController,
    private val locationService: LocationService,
) : ViewModel() {

    private val _uiState = MutableStateFlow(OrdersListUiState(
        hasLocationPermission = locationService.hasPermission(),
        currentLocation = null,
    ))
    val uiState: StateFlow<OrdersListUiState> = _uiState.asStateFlow()

    init {
        // Cached/stale-checking path — first ever launch will treat the
        // cache as stale and fetch; subsequent VM recreations within the
        // freshness window (process death + rapid restart) will skip the
        // network and render the cached singleton-repo state immediately.
        ensureFreshOrCachedAsync(OrdersTab.Available)
        refreshLocation()
    }

    fun selectTab(tab: OrdersTab) {
        if (tab == _uiState.value.tab) return
        // Clear the list so the new pane doesn't briefly show the previous
        // pane's rows during the brief gap before paint. The pane's cache
        // still lives on the singleton repo, so a fresh pane will refill
        // in a frame or two; a stale pane will refetch silently.
        _uiState.update { it.copy(tab = tab, orders = emptyList()) }
        ensureFreshOrCachedAsync(tab)
    }

    fun setSearchQuery(query: String) {
        _uiState.update { it.copy(searchQuery = query) }
    }

    fun setAvailableSort(sort: AvailableSort) {
        if (sort == _uiState.value.availableSort) return
        _uiState.update { it.copy(availableSort = sort) }
        // Sort changes the *server-side ordering*, so we do need a refetch
        // — but only silently. The user just tapped a sort chip; the chunky
        // pull indicator would feel wrong here.
        if (_uiState.value.tab == OrdersTab.Available) {
            fetchAsync(OrdersTab.Available, background = true)
        }
    }

    fun setCompletedPeriod(period: CompletedPeriod) {
        if (period == _uiState.value.completedPeriod) return
        _uiState.update { it.copy(completedPeriod = period) }
        if (_uiState.value.tab == OrdersTab.MyCompleted) {
            fetchAsync(OrdersTab.MyCompleted, background = true)
        }
    }

    /** Called from the screen after the system permission dialog returns. */
    fun onLocationPermissionResult(granted: Boolean) {
        _uiState.update { it.copy(hasLocationPermission = granted) }
        if (granted) refreshLocation()
    }

    fun refreshLocation() {
        viewModelScope.launch {
            val loc = locationService.getCurrentLocation()
            if (loc != null) _uiState.update { it.copy(currentLocation = loc) }
        }
    }

    /**
     * User pulled to refresh. Always fetches (no staleness check) and is
     * the ONLY entry point that may flip [OrdersListUiState.isUserRefreshing]
     * true — that flag is what PullToRefreshBox subscribes to.
     */
    fun onRefresh() {
        fetchAsync(_uiState.value.tab, background = false)
    }

    /**
     * Hooked from `LifecycleEventEffect(ON_RESUME)`. Returning from the
     * details screen after a take/start/complete used to fire a visible
     * pull indicator on every resume — now it routes through the cached
     * path so a warm cache means a no-op, and a cold cache means a silent
     * background fetch with no chunky indicator.
     */
    fun onResume() {
        ensureFreshOrCachedAsync(_uiState.value.tab)
    }

    /**
     * If the [tab]'s pane is stale (or has never been fetched), kick a
     * silent background refresh. Fresh cache => no-op. Never sets
     * [OrdersListUiState.isUserRefreshing] — that's reserved for [onRefresh].
     */
    private fun ensureFreshOrCachedAsync(tab: OrdersTab) {
        if (!ordersRepository.isPaneStale(tab.toPane())) return
        fetchAsync(tab, background = true)
    }

    private fun fetchAsync(tab: OrdersTab, background: Boolean) {
        viewModelScope.launch {
            _uiState.update {
                it.copy(
                    isUserRefreshing = if (background) it.isUserRefreshing else true,
                    isBackgroundRefreshing = if (background) true else it.isBackgroundRefreshing,
                    error = null,
                )
            }

            val employeeId = userProfileStore.current()?.employeeId
            val state = _uiState.value

            val (statuses, isUnassigned, scopedEmployeeId) = when (tab) {
                OrdersTab.Available -> Triple(
                    listOf(OrderStatus._0, OrderStatus._2),
                    true,
                    null,
                )
                OrdersTab.MyActive -> Triple(
                    listOf(OrderStatus._2, OrderStatus._3, OrderStatus._4),
                    null,
                    employeeId,
                )
                OrdersTab.MyCompleted -> Triple(
                    listOf(OrderStatus._5),
                    null,
                    employeeId,
                )
            }

            val (dateFrom, dateTo) = when (tab) {
                OrdersTab.MyCompleted -> periodToDateRange(state.completedPeriod)
                else -> null to null
            }

            val (sortField, ascending) = when (tab) {
                OrdersTab.Available -> state.availableSort.field to state.availableSort.ascending
                else -> "cleaningDateTime" to (tab == OrdersTab.MyActive)
            }

            when (val result = ordersRepository.getPaged(
                statuses = statuses,
                isUnassigned = isUnassigned,
                employeeId = scopedEmployeeId,
                cleaningDateFrom = dateFrom,
                cleaningDateTo = dateTo,
                sortField = sortField,
                sortAscending = ascending,
                pane = tab.toPane(),
            )) {
                is ApiResult.Success -> _uiState.update {
                    // Only adopt the fetched list if the user hasn't
                    // already swapped tabs while the call was in flight —
                    // otherwise we'd flash the wrong rows for a frame.
                    val sameTab = it.tab == tab
                    it.copy(
                        isUserRefreshing = false,
                        isBackgroundRefreshing = false,
                        isInitialLoad = false,
                        orders = if (sameTab) result.data.data.orEmpty() else it.orders,
                        hasLoadedOnce = true,
                    )
                }
                is ApiResult.Error -> {
                    snackbar.showError(errorTranslator.translate(result.error))
                    _uiState.update {
                        it.copy(
                            isUserRefreshing = false,
                            isBackgroundRefreshing = false,
                            isInitialLoad = false,
                            hasLoadedOnce = true,
                        )
                    }
                }
            }
        }
    }

    fun clearError() = _uiState.update { it.copy(error = null) }

    /**
     * Inline row actions for the Active/Available rows. After success the
     * repo's pane watermarks for the affected panes are reset, then a
     * silent background refresh fills the new state in. The user already
     * saw the slider's spinner so no chunky pull indicator is needed.
     * Errors come back through the snackbar like every other failed call.
     */
    fun takeOrderInline(orderId: String) =
        runInlineAction(orderId, OrdersMutation.TakeOrder) { ordersRepository.takeOrder(orderId) }
    fun notifyOnTheWayInline(orderId: String) =
        runInlineAction(orderId, OrdersMutation.NotifyOnTheWay) { ordersRepository.notifyOnTheWay(orderId) }
    fun startOrderInline(orderId: String) =
        runInlineAction(orderId, OrdersMutation.StartOrder) { ordersRepository.startOrder(orderId) }
    fun completeOrderInline(orderId: String) =
        runInlineAction(orderId, OrdersMutation.CompleteOrder) {
            ordersRepository.completeOrder(orderId, actualCompletionMinutes = null, completionNotes = null)
        }

    private fun runInlineAction(
        orderId: String,
        mutation: OrdersMutation,
        block: suspend () -> ApiResult<Unit>,
    ) {
        // Refuse a second action while one is already in flight — guards
        // against accidental double-swipes and any "the row re-armed
        // itself" scenarios in the SlideToTake.
        if (_uiState.value.inFlightActionOrderId != null) return
        _uiState.update { it.copy(inFlightActionOrderId = orderId) }
        viewModelScope.launch {
            val result = block()
            when (result) {
                is ApiResult.Success -> {
                    // Clear the in-flight marker first so the SlideToTake
                    // hides its spinner before the list redraws with the
                    // new status. Then invalidate the affected panes and
                    // kick a silent background refresh — no chunky pull
                    // indicator because the slider already gave feedback.
                    _uiState.update { it.copy(inFlightActionOrderId = null) }
                    ordersRepository.invalidatePanesFor(mutation)
                    fetchAsync(_uiState.value.tab, background = true)
                }
                is ApiResult.Error -> {
                    _uiState.update { it.copy(inFlightActionOrderId = null) }
                    snackbar.showError(errorTranslator.translate(result.error))
                }
            }
        }
    }

    companion object {
        /** Haversine-derived distance for the given order, given current location. Null if either coord is missing. */
        fun distanceKmFor(order: OrderListItem, current: UserLocation?): Double? {
            if (current == null) return null
            val lat = order.customerAddressLatitude ?: return null
            val lon = order.customerAddressLongitude ?: return null
            return haversineKm(current.latitude, current.longitude, lat, lon)
        }

        /**
         * Local-time start/end → ISO instants for the API. The half-open range
         * [from, to) avoids day-boundary double counting; passing null skips
         * the bound entirely.
         */
        private fun periodToDateRange(period: CompletedPeriod): Pair<String?, String?> {
            val zone = ZoneId.systemDefault()
            val today = LocalDate.now(zone)
            return when (period) {
                CompletedPeriod.ThisWeek -> {
                    val weekFields = WeekFields.of(Locale.getDefault())
                    val monday = today.with(weekFields.dayOfWeek(), 1)
                    monday.atStartOfDay(zone).toInstant().toString() to
                        monday.plusDays(7).atStartOfDay(zone).toInstant().toString()
                }
                CompletedPeriod.ThisMonth -> {
                    val first = today.withDayOfMonth(1)
                    first.atStartOfDay(zone).toInstant().toString() to
                        first.plusMonths(1).atStartOfDay(zone).toInstant().toString()
                }
                CompletedPeriod.LastMonth -> {
                    val first = today.withDayOfMonth(1).minusMonths(1)
                    first.atStartOfDay(zone).toInstant().toString() to
                        first.plusMonths(1).atStartOfDay(zone).toInstant().toString()
                }
                CompletedPeriod.All -> null to null
            }
        }
    }
}

private fun OrdersTab.toPane(): OrdersPane = when (this) {
    OrdersTab.Available -> OrdersPane.Available
    OrdersTab.MyActive -> OrdersPane.Active
    OrdersTab.MyCompleted -> OrdersPane.History
}

/**
 * Pure helpers exposed as top-level extensions so the screen + tests can use
 * them without touching the VM internals.
 */
fun OrderListItem.matchesSearch(query: String): Boolean {
    if (query.isBlank()) return true
    val q = query.trim().lowercase(Locale.getDefault())
    return (displayOrderNumber?.lowercase(Locale.getDefault())?.contains(q) == true) ||
        (customerName?.lowercase(Locale.getDefault())?.contains(q) == true) ||
        (customerAddress?.lowercase(Locale.getDefault())?.contains(q) == true)
}

/** Returns the cleaning instant as a LocalDate in the device zone, or null. */
fun OrderListItem.cleaningLocalDate(): LocalDate? = runCatching {
    cleaningDateTime?.let { Instant.parse(it).atZone(ZoneId.systemDefault()).toLocalDate() }
}.getOrNull()

/** Buckets used by the Active tab for day-grouping. */
enum class ActiveDayBucket(val labelRes: Int) {
    Today(cz.cleansia.partner.R.string.day_today),
    Tomorrow(cz.cleansia.partner.R.string.day_tomorrow),
    Later(cz.cleansia.partner.R.string.day_later);

    companion object {
        fun forDate(date: LocalDate?): ActiveDayBucket {
            if (date == null) return Later
            val today = LocalDate.now(ZoneId.systemDefault())
            return when {
                date == today -> Today
                date == today.plusDays(1) -> Tomorrow
                else -> Later
            }
        }
    }
}

/** Local-zone midnight, exposed for tests. */
@Suppress("UnusedReceiverParameter")
fun LocalDate.startOfDayInstant(): Instant = atTime(LocalTime.MIDNIGHT).toInstant(ZoneOffset.UTC)
