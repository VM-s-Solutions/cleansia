package cz.cleansia.partner.features.orders

import androidx.annotation.StringRes
import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import cz.cleansia.core.network.ApiResult
import cz.cleansia.core.snackbar.SnackbarController
import cz.cleansia.partner.R
import cz.cleansia.partner.api.model.OrderListItem
import cz.cleansia.partner.api.model.OrderStatus
import cz.cleansia.partner.api.model.PhotoType
import cz.cleansia.partner.core.auth.EmployeeIdResolver
import cz.cleansia.partner.core.network.ApiErrorTranslator
import cz.cleansia.partner.data.orders.BoardRows
import cz.cleansia.partner.data.orders.OrdersMutation
import cz.cleansia.partner.data.orders.OrdersPane
import cz.cleansia.partner.data.orders.OrdersRepository
import cz.cleansia.partner.data.orders.PendingOffer
import cz.cleansia.partner.data.payroll.CashHeld
import cz.cleansia.partner.data.payroll.PeriodPayRepository
import dagger.hilt.android.lifecycle.HiltViewModel
import kotlinx.coroutines.Job
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.distinctUntilChanged
import kotlinx.coroutines.flow.map
import kotlinx.coroutines.flow.update
import kotlinx.coroutines.launch
import java.time.Instant
import java.time.LocalDate
import java.time.ZoneId
import java.time.temporal.WeekFields
import java.util.Locale
import javax.inject.Inject

/**
 * Three tabs cover the partner UX:
 *  - [Available]  : available seats excluding my assignments — [New, Confirmed, OnTheWay, InProgress]
 *  - [MyActive]   : assigned to me + still active — [Confirmed, OnTheWay, InProgress]
 *  - [MyCompleted]: assigned to me + done — [Completed]
 */
enum class OrdersTab { Available, MyActive, MyCompleted }

/**
 * Soonest is the server's global order. Best pay is offered only when every Available row is loaded, and then
 * sorts locally: the server sorts pay within a page only.
 */
enum class AvailableSort(@StringRes val labelRes: Int) {
    SoonestFirst(R.string.sort_soonest_first),
    EarningsHighToLow(R.string.sort_earnings_high_to_low),
}

/** History period: this week, the current month, the previous month. */
enum class CompletedPeriod { ThisWeek, ThisMonth, LastMonth }

/** The statuses each pane asks for (pinned by `check-available-status-parity.mjs`). */
fun statusesFor(tab: OrdersTab): List<OrderStatus> = when (tab) {
    OrdersTab.Available -> listOf(OrderStatus._0, OrderStatus._2, OrderStatus._3, OrderStatus._4)
    OrdersTab.MyActive -> listOf(OrderStatus._2, OrderStatus._3, OrderStatus._4)
    OrdersTab.MyCompleted -> listOf(OrderStatus._5)
}

/**
 * One pane's rows and how they got there. Rows are kept per pane, so a tab switch never blanks a pane the
 * cleaner already saw; [failed] with rows held is the stale strip, without rows the error state.
 */
data class PaneState(
    val rows: List<OrderListItem> = emptyList(),
    val total: Int? = null,
    val loadedAt: Instant? = null,
    val isLoading: Boolean = false,
    val failed: Boolean = false,
    val isPaging: Boolean = false,
    val pagingFailed: Boolean = false,
    /** The index of the first row the last *Load more* appended, for the focus move. */
    val appendedFrom: Int? = null,
) {
    val hasLoaded: Boolean get() = loadedAt != null
    val complete: Boolean get() = total != null && rows.size >= total

    /**
     * D17: the last load failed and no rows are held, so the pane says it could not load, never "no jobs".
     * [heldElsewhere] is true while the pane still shows rows of another kind (Available's reserved cards).
     */
    fun showsError(heldElsewhere: Boolean = false): Boolean = failed && rows.isEmpty() && !heldElsewhere
}

/** Where the in-progress hero's run stands: her own mark on a crew job, the first after photo on a solo one. */
data class HeroProgress(val orderId: String, val crew: Boolean, val myMarkAt: Instant?, val firstAfterPhotoAt: Instant?)

data class OrdersListUiState(
    val tab: OrdersTab = OrdersTab.Available,
    val panes: Map<OrdersTab, PaneState> = emptyMap(),
    /** Only a user pull sets this; background refreshes never drive the pull indicator. */
    val isUserRefreshing: Boolean = false,
    val availableSort: AvailableSort = AvailableSort.SoonestFirst,
    val completedPeriod: CompletedPeriod = CompletedPeriod.ThisMonth,
    val offers: List<PendingOffer> = emptyList(),
    /** Reserved rows that were not on the loaded Available page, fetched by id. */
    val reservedRows: Map<String, OrderListItem> = emptyMap(),
    /** The first cash-held row whose cash jobs are hidden; null when none, or the read failed. */
    val cashHidden: CashHeld? = null,
    /** The balance the cash panel shows: a hidden one first, else the first; null with no balance. */
    val cashHeld: CashHeld? = null,
    /** Last month's rows, read only when the selected month is empty. */
    val historyReference: PaneState? = null,
    /** The in-progress hero's own run, from its GetById and GetPhotos. */
    val heroProgress: HeroProgress? = null,
    /** The order whose hero slide is in flight. */
    val inFlightActionOrderId: String? = null,
    /** The contract sheet open over the board, or null. */
    val contractRequest: WorkContractRequest.Take? = null,
    val takeTaken: Boolean = false,
    /** The cleaner's own row for the taken job (D13), with the street; null until it lands. */
    val takenRow: OrderListItem? = null,
    /** The refusal's existing translation; the sheet stays open and shows it. */
    val takeRefusal: String? = null,
    /** The index the taken row held among the job cards on show: where focus goes after *Back to jobs*. */
    val takenIndex: Int? = null,
    val decliningOrderId: String? = null,
    val declineRefusal: String? = null,
) {
    fun pane(tab: OrdersTab): PaneState = panes[tab] ?: PaneState()
}

@HiltViewModel
class OrdersListViewModel @Inject constructor(
    private val ordersRepository: OrdersRepository,
    private val employeeIdResolver: EmployeeIdResolver,
    private val errorTranslator: ApiErrorTranslator,
    private val snackbar: SnackbarController,
    private val periodPayRepository: PeriodPayRepository,
) : ViewModel() {

    private val _uiState = MutableStateFlow(OrdersListUiState())
    val uiState: StateFlow<OrdersListUiState> = _uiState.asStateFlow()

    private val paneJobs = HashMap<OrdersTab, Job>()
    private val reservedLookups = HashSet<String>()

    init {
        viewModelScope.launch {
            ordersRepository.pendingOffers.collect { offers ->
                _uiState.update { it.copy(offers = offers) }
                joinReserved()
            }
        }
        viewModelScope.launch {
            _uiState.map(::boardRowsOf).distinctUntilChanged().collect(ordersRepository::publishBoardRows)
        }
        viewModelScope.launch { ordersRepository.availableTabRequests.collect { selectTab(OrdersTab.Available) } }
        // Orders entry: Available page 1, Active to completion (the hero, the badge and the clash check),
        // pending offers when stale, cash held. History waits for its tab.
        launchPane(OrdersTab.Available)
        launchPane(OrdersTab.MyActive)
        refreshOffers(force = false)
    }

    fun selectTab(tab: OrdersTab) {
        if (tab == _uiState.value.tab) return
        _uiState.update { it.copy(tab = tab) }
        ensureFresh(tab)
    }

    fun setAvailableSort(sort: AvailableSort) = _uiState.update { it.copy(availableSort = sort) }

    fun setCompletedPeriod(period: CompletedPeriod) {
        if (period == _uiState.value.completedPeriod) return
        paneJobs.remove(OrdersTab.MyCompleted)?.cancel()
        _uiState.update { it.copy(completedPeriod = period, historyReference = null) }
        updatePane(OrdersTab.MyCompleted) { PaneState() }
        launchPane(OrdersTab.MyCompleted)
    }

    /** A user pull, *Check again* and *Retry* on a tab all do the same: refetch what that tab shows. */
    fun onRefresh() {
        when (_uiState.value.tab) {
            OrdersTab.Available -> {
                launchPane(OrdersTab.Available, user = true)
                launchPane(OrdersTab.MyActive)
                refreshOffers(force = true)
            }
            OrdersTab.MyActive -> launchPane(OrdersTab.MyActive, user = true)
            OrdersTab.MyCompleted -> launchPane(OrdersTab.MyCompleted, user = true)
        }
    }

    fun onResume() {
        ensureFresh(_uiState.value.tab)
        if (_uiState.value.tab != OrdersTab.MyActive) ensureFresh(OrdersTab.MyActive)
        refreshOffers(force = false)
    }

    private fun ensureFresh(tab: OrdersTab) {
        val pane = _uiState.value.pane(tab)
        if (pane.hasLoaded && !pane.failed && !ordersRepository.isPaneStale(tab.toPane())) return
        launchPane(tab)
    }

    /**
     * Fetches a pane unless that pane's fetch is already running; a pull during one waits for it, then ends.
     * Available is page 1 (paging resets); Active and History page to completion.
     */
    private fun launchPane(tab: OrdersTab, user: Boolean = false) {
        val running = paneJobs[tab]
        if (running?.isActive == true) {
            if (user) {
                viewModelScope.launch {
                    _uiState.update { it.copy(isUserRefreshing = true) }
                    running.join()
                    _uiState.update { it.copy(isUserRefreshing = false) }
                }
            }
            return
        }
        paneJobs[tab] = viewModelScope.launch {
            if (user) _uiState.update { it.copy(isUserRefreshing = true) }
            updatePane(tab) { it.copy(isLoading = true) }
            if (tab == OrdersTab.Available) {
                reservedLookups.clear()
                launch { refreshCashHeld() }
                loadAvailableFirstPage()
            } else {
                loadToCompletion(tab)
            }
            updatePane(tab) { it.copy(isLoading = false) }
            if (user) _uiState.update { it.copy(isUserRefreshing = false) }
            if (tab == OrdersTab.Available) joinReserved()
        }
    }

    private suspend fun loadAvailableFirstPage() {
        val me = employeeIdResolver.resolve()?.takeIf { it.isNotBlank() }
        if (me == null) return failPane(OrdersTab.Available)
        when (val result = availablePage(me, offset = 0, pane = OrdersPane.Available)) {
            is ApiResult.Success -> updatePane(OrdersTab.Available) {
                it.copy(
                    rows = result.data.data.orEmpty(),
                    total = result.data.total,
                    loadedAt = Instant.now(),
                    failed = false,
                    pagingFailed = false,
                    appendedFrom = null,
                )
            }
            is ApiResult.Error -> failPane(OrdersTab.Available)
        }
    }

    private suspend fun availablePage(me: String, offset: Int, pane: OrdersPane?, orderId: String? = null, limit: Int = OrdersBoardRules.PAGE_SIZE) =
        ordersRepository.getPaged(
            statuses = statusesFor(OrdersTab.Available),
            hasAvailableSpots = true,
            excludeEmployeeId = me,
            sortField = "cleaningDateTime",
            sortAscending = true,
            offset = offset,
            limit = limit,
            pane = pane,
            orderId = orderId,
        )

    /** Active and History page to completion: the clash check and the History totals need every row. */
    private suspend fun loadToCompletion(tab: OrdersTab) {
        val me = employeeIdResolver.resolve()?.takeIf { it.isNotBlank() }
        // Never unscoped: without the cleaner's own id there is no "mine" to ask for.
        if (me == null) return failPane(tab)
        val (from, to) = if (tab == OrdersTab.MyCompleted) periodToDateRange(_uiState.value.completedPeriod) else null to null
        var rows = emptyList<OrderListItem>()
        var total: Int? = null
        while (true) {
            val result = ordersRepository.getPaged(
                statuses = statusesFor(tab),
                employeeId = me,
                cleaningDateFrom = from,
                cleaningDateTo = to,
                sortField = "cleaningDateTime",
                sortAscending = tab == OrdersTab.MyActive,
                offset = rows.size,
                limit = OrdersBoardRules.PAGE_SIZE,
                pane = tab.toPane(),
            )
            when (result) {
                is ApiResult.Success -> {
                    val page = result.data.data.orEmpty()
                    total = result.data.total
                    val held = rows.size
                    rows = OrdersBoardRules.mergePage(rows, page)
                    // A page of rows already held (the list shifted between pages) would ask for the same offset again.
                    if (page.isEmpty() || rows.size == held || rows.size >= (total ?: 0)) break
                }
                is ApiResult.Error -> return failPane(tab)
            }
        }
        updatePane(tab) { it.copy(rows = rows, total = total ?: rows.size, loadedAt = Instant.now(), failed = false) }
        when (tab) {
            OrdersTab.MyActive -> readHeroProgress(me, rows)
            OrdersTab.MyCompleted -> if (rows.isEmpty() && _uiState.value.completedPeriod == CompletedPeriod.ThisMonth) readLastMonth(me)
            OrdersTab.Available -> Unit
        }
    }

    /** An empty month shows last month as a reference: one read, only when the month is empty, and none when last month is empty too. */
    private suspend fun readLastMonth(me: String) {
        val (from, to) = periodToDateRange(CompletedPeriod.LastMonth)
        var rows = emptyList<OrderListItem>()
        while (true) {
            val result = ordersRepository.getPaged(
                statuses = statusesFor(OrdersTab.MyCompleted),
                employeeId = me,
                cleaningDateFrom = from,
                cleaningDateTo = to,
                sortField = "cleaningDateTime",
                sortAscending = false,
                offset = rows.size,
                limit = OrdersBoardRules.PAGE_SIZE,
            )
            val page = (result as? ApiResult.Success)?.data ?: return
            val held = rows.size
            rows = OrdersBoardRules.mergePage(rows, page.data.orEmpty())
            if (page.data.isNullOrEmpty() || rows.size == held || rows.size >= (page.total ?: 0)) break
        }
        _uiState.update { state ->
            if (state.completedPeriod != CompletedPeriod.ThisMonth) state
            else state.copy(historyReference = rows.takeIf { it.isNotEmpty() }?.let { PaneState(rows = it, total = it.size, loadedAt = Instant.now()) })
        }
    }

    /** The in-progress hero reads its one job and its photos, so it names the run's step as the detail does. */
    private suspend fun readHeroProgress(me: String, rows: List<OrderListItem>) {
        val hero = OrdersBoardRules.heroPick(rows)?.takeIf { OrdersBoardRules.status(it) == 4 }
        val id = hero?.id
        if (id == null) {
            _uiState.update { it.copy(heroProgress = null) }
            return
        }
        val order = (ordersRepository.getById(id) as? ApiResult.Success)?.data
        val photos = (ordersRepository.getPhotos(id) as? ApiResult.Success)?.data?.photos.orEmpty()
        val seats = order?.assignedEmployees.orEmpty()
        val progress = HeroProgress(
            orderId = id,
            crew = if (order != null) seats.size >= 2 else (hero.requiredEmployees ?: 1) > 1,
            myMarkAt = seats.firstOrNull { it.employeeId == me }?.completionConfirmedAt?.let(::instantOrNull),
            firstAfterPhotoAt = photos.filter { it.photoType == PhotoType._2 }.mapNotNull { it.capturedAt?.let(::instantOrNull) }.minOrNull(),
        )
        _uiState.update { it.copy(heroProgress = progress) }
    }

    /** List failures raise no snackbar: the error state or the stale strip says it, and is announced. */
    private fun failPane(tab: OrdersTab) = updatePane(tab) { it.copy(failed = true) }

    fun loadMore() {
        val pane = _uiState.value.pane(OrdersTab.Available)
        if (pane.isPaging || pane.complete || !pane.hasLoaded) return
        viewModelScope.launch {
            updatePane(OrdersTab.Available) { it.copy(isPaging = true, pagingFailed = false) }
            val me = employeeIdResolver.resolve()?.takeIf { it.isNotBlank() }
            val result = me?.let { availablePage(it, offset = pane.rows.size, pane = null) }
            if (result is ApiResult.Success) {
                updatePane(OrdersTab.Available) {
                    it.copy(
                        rows = OrdersBoardRules.mergePage(it.rows, result.data.data.orEmpty()),
                        total = result.data.total ?: it.total,
                        isPaging = false,
                        appendedFrom = it.rows.size,
                    )
                }
                joinReserved()
            } else {
                updatePane(OrdersTab.Available) { it.copy(isPaging = false, pagingFailed = true) }
            }
        }
    }

    private suspend fun refreshCashHeld() {
        val held = (periodPayRepository.getCashHeld() as? ApiResult.Success)?.data.orEmpty()
        _uiState.update { it.copy(cashHidden = OrdersBoardRules.cashNotice(held), cashHeld = OrdersBoardRules.cashPanelRow(held)) }
    }

    private fun refreshOffers(force: Boolean) {
        if (!force && !ordersRepository.arePendingOffersStale()) return
        viewModelScope.launch { ordersRepository.refreshPendingOffers() }
    }

    /**
     * A live offer whose row is not on the loaded page is fetched by id with the Available query, once per
     * Available load: a row read earlier keeps showing until the new read replaces it, or drops it when not found.
     */
    private fun joinReserved() {
        val state = _uiState.value
        val available = state.pane(OrdersTab.Available)
        if (!available.hasLoaded) return
        OrdersBoardRules.reservedCards(state.offers, available.rows, Instant.now())
            .filter { (offer, row) -> row == null && reservedLookups.add(offer.id) }
            .forEach { (offer, _) ->
                viewModelScope.launch {
                    val me = employeeIdResolver.resolve()?.takeIf { it.isNotBlank() } ?: return@launch
                    val result = availablePage(me, offset = 0, pane = null, orderId = offer.id, limit = 1)
                    if (result !is ApiResult.Success) return@launch
                    val row = result.data.data?.firstOrNull { it.id == offer.id }
                    _uiState.update {
                        it.copy(reservedRows = if (row == null) it.reservedRows - offer.id else it.reservedRows + (offer.id to row))
                    }
                }
            }
    }

    // ── the take ───────────────────────────────────────────────────────

    /** Take and a reserved card's Confirm open the sheet with the row as its board context. */
    fun takeOrderInline(orderId: String) {
        val state = _uiState.value
        if (state.inFlightActionOrderId != null || state.contractRequest != null) return
        val row = state.pane(OrdersTab.Available).rows.firstOrNull { it.id == orderId } ?: state.reservedRows[orderId]
        _uiState.update {
            it.copy(
                contractRequest = WorkContractRequest.Take(orderId, row?.let(::BoardTake)),
                takeTaken = false,
                takenRow = null,
                takeRefusal = null,
                takenIndex = null,
            )
        }
    }

    fun dismissContract() = _uiState.update {
        it.copy(contractRequest = null, takeTaken = false, takenRow = null, takeRefusal = null)
    }

    /**
     * The sheet's verdict. A take keeps the sheet up in its taken state while the board drops the row, refetches
     * and reads the cleaner's own row for the street; a refusal keeps it up with the reason. Pending offers are
     * refreshed after every outcome: a take that clashes with a reservation ends it.
     */
    fun onWorkContractOutcome(outcome: WorkContractOutcome) {
        val orderId = (outcome.request as? WorkContractRequest.Take)?.orderId ?: return
        when (outcome) {
            is WorkContractOutcome.Taken -> {
                _uiState.update { state ->
                    val available = state.pane(OrdersTab.Available)
                    val index = available.rows.indexOfFirst { it.id == orderId }
                    // Focus after *Back to jobs* counts the cards on show, as the screen lays them out.
                    val now = Instant.now()
                    val reservedIds = OrdersBoardRules.reservedShown(state.offers, available.rows + state.reservedRows.values, now)
                        .map { it.second.id }.toSet()
                    val sort = state.availableSort.takeIf { it in OrdersBoardRules.sortOptions(available.rows.size, available.total) }
                        ?: AvailableSort.SoonestFirst
                    val cardIndex = OrdersBoardRules.visibleCards(available.rows, reservedIds, sort, now).indexOfFirst { it.id == orderId }
                    state.copy(
                        takeTaken = true,
                        takenIndex = cardIndex.takeIf { it >= 0 },
                        reservedRows = state.reservedRows - orderId,
                        panes = state.panes + (
                            OrdersTab.Available to available.copy(
                                rows = available.rows.filterNot { it.id == orderId },
                                total = available.total?.let { t -> if (index >= 0) (t - 1).coerceAtLeast(0) else t },
                            )
                            ),
                    )
                }
                fetchTakenRow(orderId)
            }
            is WorkContractOutcome.Refused ->
                _uiState.update { it.copy(takeRefusal = errorTranslator.translate(outcome.error)) }
            is WorkContractOutcome.Accepted -> return
        }
        ordersRepository.invalidatePanesFor(OrdersMutation.TakeOrder)
        launchPane(OrdersTab.Available)
        launchPane(OrdersTab.MyActive)
        refreshOffers(force = true)
    }

    /** D13: one list read scoped to the cleaner, never GetById (its first read is audited). */
    private fun fetchTakenRow(orderId: String) {
        viewModelScope.launch {
            val me = employeeIdResolver.resolve()?.takeIf { it.isNotBlank() }
            val result = me?.let {
                ordersRepository.getPaged(
                    statuses = statusesFor(OrdersTab.MyActive),
                    employeeId = it,
                    limit = 1,
                    orderId = orderId,
                )
            }
            val row = (result as? ApiResult.Success)?.data?.data?.firstOrNull { it.id == orderId }
            _uiState.update {
                if (it.contractRequest?.orderId != orderId) it else it.copy(takenRow = row)
            }
        }
    }

    // ── reserved card ──────────────────────────────────────────────────

    fun declineOffer(orderId: String) {
        if (_uiState.value.decliningOrderId != null) return
        _uiState.update { it.copy(decliningOrderId = orderId, declineRefusal = null) }
        viewModelScope.launch {
            when (val result = ordersRepository.declinePreferredOffer(orderId)) {
                is ApiResult.Success -> {
                    snackbar.showSuccessKey(R.string.offer_declined_toast)
                    _uiState.update { it.copy(decliningOrderId = null, reservedRows = it.reservedRows - orderId) }
                    launchPane(OrdersTab.Available)
                }
                is ApiResult.Error -> _uiState.update {
                    it.copy(decliningOrderId = null, declineRefusal = errorTranslator.translate(result.error))
                }
            }
        }
    }

    fun dismissDeclineRefusal() = _uiState.update { it.copy(declineRefusal = null) }

    // ── the hero's slides ──────────────────────────────────────────────

    fun notifyOnTheWayInline(orderId: String) =
        runInlineAction(orderId, OrdersMutation.NotifyOnTheWay) { ordersRepository.notifyOnTheWay(orderId) }

    fun startOrderInline(orderId: String) =
        runInlineAction(orderId, OrdersMutation.StartOrder) { ordersRepository.startOrder(orderId) }

    /** A slide's failure is an action failure and still raises the snackbar; either way the panes reconcile. */
    private fun runInlineAction(orderId: String, mutation: OrdersMutation, block: suspend () -> ApiResult<Unit>) {
        if (_uiState.value.inFlightActionOrderId != null) return
        _uiState.update { it.copy(inFlightActionOrderId = orderId) }
        viewModelScope.launch {
            val result = block()
            _uiState.update { it.copy(inFlightActionOrderId = null) }
            if (result is ApiResult.Error) snackbar.showError(errorTranslator.translate(result.error))
            ordersRepository.invalidatePanesFor(mutation)
            launchPane(OrdersTab.MyActive)
            if (mutation == OrdersMutation.NotifyOnTheWay) launchPane(OrdersTab.Available)
        }
    }

    private fun updatePane(tab: OrdersTab, transform: (PaneState) -> PaneState) =
        _uiState.update { it.copy(panes = it.panes + (tab to transform(it.pane(tab)))) }

    companion object {
        /** Local-time period → ISO instants for the API, half-open [from, to). */
        fun periodToDateRange(
            period: CompletedPeriod,
            today: LocalDate = LocalDate.now(ZoneId.systemDefault()),
            zone: ZoneId = ZoneId.systemDefault(),
            locale: Locale = Locale.getDefault(),
        ): Pair<String, String> {
            val first = when (period) {
                CompletedPeriod.ThisWeek -> today.with(WeekFields.ISO.dayOfWeek(), 1)
                CompletedPeriod.ThisMonth -> today.withDayOfMonth(1)
                CompletedPeriod.LastMonth -> today.withDayOfMonth(1).minusMonths(1)
            }
            val end = if (period == CompletedPeriod.ThisWeek) first.plusDays(7) else first.plusMonths(1)
            return first.atStartOfDay(zone).toInstant().toString() to end.atStartOfDay(zone).toInstant().toString()
        }
    }
}

/** The Available rows (reserved rows read by id included) and her own rows the board holds, for the detail. */
private fun boardRowsOf(state: OrdersListUiState): BoardRows {
    val available = state.pane(OrdersTab.Available)
    val own = state.pane(OrdersTab.MyActive)
    return BoardRows(
        available = if (available.hasLoaded) OrdersBoardRules.mergePage(available.rows, state.reservedRows.values.toList()) else null,
        availableTotal = available.total.takeIf { available.hasLoaded },
        own = own.rows.takeIf { own.hasLoaded },
    )
}

private fun instantOrNull(text: String): Instant? = runCatching { Instant.parse(text) }.getOrNull()

private fun OrdersTab.toPane(): OrdersPane = when (this) {
    OrdersTab.Available -> OrdersPane.Available
    OrdersTab.MyActive -> OrdersPane.Active
    OrdersTab.MyCompleted -> OrdersPane.History
}
