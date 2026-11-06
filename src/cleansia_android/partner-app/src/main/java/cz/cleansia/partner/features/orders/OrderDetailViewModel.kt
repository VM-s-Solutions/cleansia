package cz.cleansia.partner.features.orders

import androidx.lifecycle.SavedStateHandle
import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import cz.cleansia.core.snackbar.SnackbarController
import cz.cleansia.core.ui.state.ActionState
import cz.cleansia.partner.R
import cz.cleansia.core.network.ApiError
import cz.cleansia.partner.api.model.OrderItem
import cz.cleansia.partner.api.model.OrderListItem
import cz.cleansia.partner.api.model.OrderStatus
import cz.cleansia.partner.core.auth.EmployeeIdResolver
import cz.cleansia.partner.core.network.ApiErrorTranslator
import cz.cleansia.core.network.ApiResult
import cz.cleansia.partner.data.orders.OrdersRepository
import cz.cleansia.partner.data.orders.PendingOffer
import dagger.hilt.android.lifecycle.HiltViewModel
import kotlinx.coroutines.ExperimentalCoroutinesApi
import kotlinx.coroutines.async
import kotlinx.coroutines.coroutineScope
import kotlinx.coroutines.flow.MutableSharedFlow
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.SharedFlow
import kotlinx.coroutines.flow.asSharedFlow
import kotlinx.coroutines.flow.SharingStarted
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.combine
import kotlinx.coroutines.flow.distinctUntilChanged
import kotlinx.coroutines.flow.map
import kotlinx.coroutines.flow.mapLatest
import kotlinx.coroutines.flow.stateIn
import kotlinx.coroutines.launch
import java.time.Instant
import javax.inject.Inject

/** Per-action discriminator so individual buttons can show their own spinners. */
enum class OrderAction { Take, AcceptContract, Start, NotifyOnTheWay, MarkCashCollected, ReportCashNotPaid, MarkMyPartDone, Complete, DeclineOffer, ReportLockout, Drop }

sealed interface OrderDetailUiState {
    data object Loading : OrderDetailUiState

    /** [refused]: `GetById` answered `order.not_found`, so the page is never drawn from the board's row as hers. */
    data class Error(val refused: Boolean) : OrderDetailUiState

    /** `order.not_found`: a push opened after the job was taken or cancelled. A retry cannot change the answer. */
    data object Gone : OrderDetailUiState
    data class Loaded(val order: OrderItem) : OrderDetailUiState

    /** Off the job: `GetById` refuses her. [reason] is the administrator's, null when no removal was found. */
    data class Removed(val reason: String?) : OrderDetailUiState
}

@HiltViewModel
class OrderDetailViewModel @Inject constructor(
    savedStateHandle: SavedStateHandle,
    private val ordersRepository: OrdersRepository,
    private val errorTranslator: ApiErrorTranslator,
    private val snackbar: SnackbarController,
    private val employeeIdResolver: EmployeeIdResolver,
    private val areaLocator: JobAreaLocator,
) : ViewModel() {

    private val orderId: String = savedStateHandle.get<String>("orderId")
        ?: error("orderId required for OrderDetail route")

    private val showRemovalReason: Boolean = savedStateHandle.get<Boolean>("showRemovalReason") == true

    private val _uiState = MutableStateFlow<OrderDetailUiState>(OrderDetailUiState.Loading)
    val uiState: StateFlow<OrderDetailUiState> = _uiState.asStateFlow()

    private val _actionState = MutableStateFlow<ActionState>(ActionState.Idle)
    val actionState: StateFlow<ActionState> = _actionState.asStateFlow()

    private val _inFlightAction = MutableStateFlow<OrderAction?>(null)
    val inFlightAction: StateFlow<OrderAction?> = _inFlightAction.asStateFlow()

    /**
     * The reservation held for this cleaner on this order, if there is one. The partner order DTO
     * carries no reservation block — that field is customer-only, so no cleaner ever learns an order
     * was reserved for someone else — so the disclosure is composed from the cleaner's own offers.
     * Absent means an ordinary job, which is exactly right for the short-lead band, where the push
     * fires but nothing is withheld.
     */
    val preferredOffer: StateFlow<PendingOffer?> = ordersRepository.pendingOffers
        .map { offers -> offers.firstOrNull { it.id == orderId } }
        .stateIn(viewModelScope, SharingStarted.Eagerly, null)

    private val _offerRefusal = MutableStateFlow<OfferRefusal?>(null)
    val offerRefusal: StateFlow<OfferRefusal?> = _offerRefusal.asStateFlow()

    private val _removalReason = MutableStateFlow<String?>(null)
    val removalReason: StateFlow<String?> = _removalReason.asStateFlow()

    /** The server has said why she is off the job, or that it found no removal; a read that failed is asked again. */
    private var removalAnswered = !showRemovalReason

    /**
     * The board's row for this id: the loading and error states draw from it, and the band, the clash
     * check and the board's other rows of that day come with it. Null when the board holds none (a push, the Dashboard).
     */
    val boardRow: StateFlow<OrderListItem?> = ordersRepository.boardRows
        .map { it.row(orderId) }
        .stateIn(viewModelScope, SharingStarted.Eagerly, ordersRepository.boardRows.value.row(orderId))

    /** The job the removal page names, read once at open: the board's next load no longer lists it. */
    val removedJob: OrderListItem? = if (showRemovalReason) ordersRepository.boardRows.value.row(orderId) else null

    private val ownRead = MutableStateFlow(PaneState())
    private val openRead = MutableStateFlow(PaneState())

    /**
     * Where the band's area map is centred: the area string geocoded on device, only while the page has no usable
     * door to pin and only while the page is shown. Never derived from a coordinate.
     */
    @OptIn(ExperimentalCoroutinesApi::class)
    val areaCentre: StateFlow<MapPoint?> = combine(_uiState, boardRow) { state, row -> areaQuery(state, row) }
        .distinctUntilChanged()
        .mapLatest { query -> query?.let { areaLocator.centre(it.area, it.currency) } }
        .stateIn(viewModelScope, SharingStarted.WhileSubscribed(5_000), null)

    /** Her own Active rows: the page's own read once it has answered, else the board's, else the read in flight. */
    val ownRows: StateFlow<PaneState> = combine(ordersRepository.boardRows, ownRead) { board, read -> read.orBoard(board.own, board.own?.size) }
        .stateIn(viewModelScope, SharingStarted.Eagerly, PaneState())

    /** The open jobs, for the removal page: the same rule over the Available tab's first page. */
    val openRows: StateFlow<PaneState> = combine(ordersRepository.boardRows, openRead) { board, read -> read.orBoard(board.available, board.availableTotal) }
        .stateIn(viewModelScope, SharingStarted.Eagerly, PaneState())

    /** The contract sheet the screen is showing, if any: a take, a standalone acceptance or a read. */
    private val _contractRequest = MutableStateFlow<WorkContractRequest?>(null)
    val contractRequest: StateFlow<WorkContractRequest?> = _contractRequest.asStateFlow()

    private val takeTaken = MutableStateFlow(false)

    /**
     * The job the sheet's taken panel shows once a take succeeded, from the order as it is read back: the door and its
     * pin arrive with the read. Null while no take has succeeded or once the panel is closed.
     */
    val takenRow: StateFlow<OrderListItem?> = combine(takeTaken, _uiState, boardRow) { taken, state, row ->
        if (!taken) null else (state as? OrderDetailUiState.Loaded)?.order?.asBoardRow(band = row?.homeDistanceBand)
    }.stateIn(viewModelScope, SharingStarted.Eagerly, null)

    /** Fires once the cleaner is off the job. */
    private val _left = MutableSharedFlow<Unit>(extraBufferCapacity = 1)
    val left: SharedFlow<Unit> = _left.asSharedFlow()

    private val _myEmployeeId = MutableStateFlow<String?>(null)
    val myEmployeeId: StateFlow<String?> = _myEmployeeId.asStateFlow()

    /**
     * The caller's own acceptance, paired through their crew entry. Resolved here rather than in the
     * screen because the pairing needs the signed-in employee id, which only the resolver can supply.
     */
    val contractStanding: StateFlow<WorkContractStanding> =
        combine(_uiState, _myEmployeeId) { state, employeeId ->
            (state as? OrderDetailUiState.Loaded)?.order?.workContractStanding(employeeId)
                ?: WorkContractStanding.None
        }.stateIn(viewModelScope, SharingStarted.Eagerly, WorkContractStanding.None)

    val completionStep: StateFlow<CompletionStep> =
        combine(_uiState, _myEmployeeId) { state, employeeId ->
            (state as? OrderDetailUiState.Loaded)?.order?.completionStep(employeeId) ?: CompletionStep.Complete
        }.stateIn(viewModelScope, SharingStarted.Eagerly, CompletionStep.Complete)

    init {
        ensureFreshOrCachedAsync()
        ensureOffersFresh()
        viewModelScope.launch { _myEmployeeId.value = employeeIdResolver.resolve() }
    }

    fun showAvailable() = ordersRepository.requestAvailableTab()

    /** *Check again* on the removal page: both lists read afresh. */
    fun checkAgain() {
        viewModelScope.launch { readOwnRows() }
        viewModelScope.launch { readOpenRows() }
    }

    /**
     * Background-freshness gate used by init, ON_RESUME, and post-mutation
     * callbacks. Skips the network entirely when the per-order cache is still
     * warm — keeps the sheet stable and avoids a needless round-trip every
     * time the cleaner pops back from a sub-screen. A loaded order stays
     * mounted through a background re-fetch (no spinner flash).
     */
    fun ensureFreshOrCachedAsync() {
        // The repository stores a per-order WATERMARK, not the order: `fetch()` is the only thing that
        // puts one into [uiState], which starts at Loading and does not survive this view model. So a
        // warm watermark may only suppress the fetch once the sheet is already showing the order —
        // otherwise opening a job inside the freshness window left it spinning forever.
        if (uiState.value is OrderDetailUiState.Loaded && !ordersRepository.isOrderStale(orderId)) return
        viewModelScope.launch { fetch() }
    }

    fun refresh() {
        viewModelScope.launch { fetch() }
    }

    fun onResume() {
        ensureFreshOrCachedAsync()
        ensureOffersFresh()
    }

    private fun ensureOffersFresh() {
        if (!ordersRepository.arePendingOffersStale()) return
        viewModelScope.launch { ordersRepository.refreshPendingOffers() }
    }

    /** Refusing the reservation from the job it belongs to; the same one write the offers list makes. */
    fun declinePreferredOffer() = runAction(OrderAction.DeclineOffer) {
        ordersRepository.declinePreferredOffer(orderId)
    }

    fun dismissOfferRefusal() {
        _offerRefusal.value = null
    }

    /**
     * @param notifyOnError raise the translated failure on the snackbar. False
     * only for the reconciling fetch that follows a *rejected* action: the
     * cleaner has already been told why the action failed, and the partner app
     * has no `NetworkErrorInterceptor` (only the customer app wires one) to
     * collapse the duplicate for us. A silent failure is fine there because the
     * fetch is a best-effort reconciliation, not something the user asked for.
     */
    private suspend fun fetch(notifyOnError: Boolean = true) = coroutineScope {
        val removal = if (removalAnswered) null else async { readRemoval() }
        when (val result = ordersRepository.getById(orderId)) {
            is ApiResult.Success -> {
                _uiState.value = OrderDetailUiState.Loaded(result.data)
                if (result.data.isAssignedToCurrentUser != true || result.data.orderStatus?.value == OrderStatus._6.value) readOwnRowsUnlessHeld()
            }
            is ApiResult.Error -> {
                if (_uiState.value is OrderDetailUiState.Loaded) {
                    if (notifyOnError) snackbar.showError(errorTranslator.translate(result.error))
                    return@coroutineScope
                }
                val refused = result.error.isNotFound()
                if (showRemovalReason && refused && removal?.await() != false) {
                    _uiState.value = OrderDetailUiState.Removed(_removalReason.value)
                    readOwnRowsUnlessHeld()
                    if (ordersRepository.boardRows.value.available == null && !openRead.value.hasLoaded) readOpenRows()
                    return@coroutineScope
                }
                if (!showRemovalReason && refused) {
                    _uiState.value = OrderDetailUiState.Gone
                    return@coroutineScope
                }
                if (notifyOnError && !showRemovalReason) snackbar.showError(errorTranslator.translate(result.error))
                _uiState.value = OrderDetailUiState.Error(refused)
            }
        }
    }

    /** True once the server answered: the administrator's reason, or no removal found. */
    private suspend fun readRemoval(): Boolean {
        when (val result = ordersRepository.getMyAssignmentRemovalReason(orderId)) {
            is ApiResult.Success -> _removalReason.value = result.data
            is ApiResult.Error -> if (!result.error.isNotFound()) return false
        }
        removalAnswered = true
        return true
    }

    private fun ApiError.isNotFound(): Boolean = hasKey(NOT_FOUND) || this is ApiError.NotFound

    /**
     * Her own rows, the board's when it holds them, else the Active tab's call: a job not taken waits for them before
     * Take, and a cancelled one draws them on its day line and in her next jobs.
     */
    private suspend fun readOwnRowsUnlessHeld() {
        if (ordersRepository.boardRows.value.own != null || ownRead.value.hasLoaded || ownRead.value.isLoading) return
        readOwnRows()
    }

    /** `GetPaged` statuses 2–4, mine, every page: the Active tab's read. */
    private suspend fun readOwnRows() {
        ownRead.value = ownRead.value.copy(isLoading = true, failed = false)
        val me = employeeIdResolver.resolve()?.takeIf { it.isNotBlank() }
        var rows = emptyList<OrderListItem>()
        while (me != null) {
            val page = (
                ordersRepository.getPaged(
                    statuses = statusesFor(OrdersTab.MyActive),
                    employeeId = me,
                    offset = rows.size,
                    limit = OrdersBoardRules.PAGE_SIZE,
                ) as? ApiResult.Success
                )?.data ?: break
            val held = rows.size
            rows = OrdersBoardRules.mergePage(rows, page.data.orEmpty())
            if (page.data.isNullOrEmpty() || rows.size == held || rows.size >= (page.total ?: 0)) {
                ownRead.value = PaneState(rows = rows, total = rows.size, loadedAt = Instant.now())
                return
            }
        }
        ownRead.value = ownRead.value.copy(isLoading = false, failed = true)
    }

    /** The Available tab's first page. */
    private suspend fun readOpenRows() {
        openRead.value = openRead.value.copy(isLoading = true, failed = false)
        val me = employeeIdResolver.resolve()?.takeIf { it.isNotBlank() }
        val page = me?.let {
            ordersRepository.getPaged(
                statuses = statusesFor(OrdersTab.Available),
                hasAvailableSpots = true,
                excludeEmployeeId = it,
                limit = OrdersBoardRules.PAGE_SIZE,
            ) as? ApiResult.Success
        }?.data
        openRead.value = if (page != null) {
            PaneState(rows = page.data.orEmpty(), total = page.total, loadedAt = Instant.now())
        } else {
            openRead.value.copy(isLoading = false, failed = true)
        }
    }

    /** Taking is accepting the contract: the take happens inside the sheet, on the swipe. */
    fun take() {
        _contractRequest.value = WorkContractRequest.Take(orderId)
    }

    fun openContract(request: WorkContractRequest) {
        _contractRequest.value = request
    }

    fun dismissContract() {
        _contractRequest.value = null
        takeTaken.value = false
    }

    /**
     * The sheet's verdict. A take keeps the sheet up on its taken panel while the order is read back; any other
     * outcome closes it. A success refreshes the order, a refusal is framed (on a disclosed offer) or snackbarred and
     * reconciled.
     */
    fun onWorkContractOutcome(outcome: WorkContractOutcome) {
        if (outcome is WorkContractOutcome.Taken) takeTaken.value = true else _contractRequest.value = null
        val action = if (outcome.request is WorkContractRequest.Accept) OrderAction.AcceptContract else OrderAction.Take
        runAction(action) { outcome.asResult() }
    }

    fun start() = runAction(OrderAction.Start) { ordersRepository.startOrder(orderId) }
    fun notifyOnTheWay() = runAction(OrderAction.NotifyOnTheWay) { ordersRepository.notifyOnTheWay(orderId) }

    fun markCashCollected() = runAction(OrderAction.MarkCashCollected) {
        ordersRepository.markCashCollected(orderId)
    }

    fun reportCashNotPaid() = runAction(OrderAction.ReportCashNotPaid) {
        ordersRepository.reportCashNotPaid(orderId)
    }

    fun markMyPartDone() = runAction(OrderAction.MarkMyPartDone) { ordersRepository.markMyPartDone(orderId) }

    fun complete(actualMinutes: Int?, notes: String?) = runAction(OrderAction.Complete) {
        ordersRepository.completeOrder(orderId, actualMinutes, notes)
    }

    fun reportLockout(callAttempts: String) {
        val note = callAttempts.trim()
        if (note.isEmpty()) return
        runAction(OrderAction.ReportLockout) { ordersRepository.reportLockout(orderId, note) }
    }

    /**
     * Leaving the crew, offered only while the job is [droppable]. The confirm can outlive the state it was opened
     * on, so the gate is read again here. A success re-reads nothing, because the job is no longer the cleaner's to
     * read: the screen goes back through [left], to tabs and a dashboard the repository has expired.
     */
    fun dropOrder() {
        if (_actionState.value is ActionState.Submitting) return
        if ((_uiState.value as? OrderDetailUiState.Loaded)?.order?.droppable(Instant.now()) != true) return
        _actionState.value = ActionState.Submitting
        _inFlightAction.value = OrderAction.Drop
        viewModelScope.launch {
            when (val result = ordersRepository.dropOrder(orderId)) {
                is ApiResult.Success -> {
                    snackbar.showSuccessKey(
                        if (result.data == 0) R.string.order_dropped_toast_no_crew else R.string.order_dropped_toast,
                    )
                    _actionState.value = ActionState.Idle
                    _inFlightAction.value = null
                    _left.tryEmit(Unit)
                }
                is ApiResult.Error -> {
                    val reason = errorTranslator.translate(result.error)
                    snackbar.showError(reason)
                    _actionState.value = ActionState.Error(reason)
                    _inFlightAction.value = null
                    fetch(notifyOnError = false)
                }
            }
        }
    }

    fun onContentMutated() = ensureFreshOrCachedAsync()

    private fun runAction(action: OrderAction, block: suspend () -> ApiResult<Unit>) {
        if (_actionState.value is ActionState.Submitting) return
        _actionState.value = ActionState.Submitting
        _inFlightAction.value = action
        viewModelScope.launch {
            when (val result = block()) {
                is ApiResult.Success -> {
                    if (action == OrderAction.Complete) {
                        snackbar.showSuccessKey(R.string.order_completed_toast)
                    }
                    if (action == OrderAction.MarkMyPartDone) {
                        snackbar.showSuccessKey(R.string.order_my_part_done_toast)
                    }
                    if (action == OrderAction.DeclineOffer) {
                        snackbar.showSuccessKey(R.string.offer_declined_toast)
                    }
                    if (action == OrderAction.ReportLockout) {
                        snackbar.showSuccessKey(R.string.lockout_reported_toast)
                    }
                    if (action == OrderAction.ReportCashNotPaid) {
                        snackbar.showSuccessKey(R.string.order_cash_not_paid_reported_toast)
                    }
                    _actionState.value = ActionState.Idle
                    _inFlightAction.value = null
                    _offerRefusal.value = null
                    fetch()
                }
                is ApiResult.Error -> {
                    // A start or a completion on a seat with no acceptance is not an error to read but
                    // a contract to accept: the same sheet opens, in accept mode, and the gesture
                    // springs back to be retried once the row exists.
                    if ((action == OrderAction.Start || action == OrderAction.Complete) &&
                        result.error.hasKey(ACCEPTANCE_REQUIRED)
                    ) {
                        _actionState.value = ActionState.Idle
                        _inFlightAction.value = null
                        _contractRequest.value = WorkContractRequest.Accept(orderId)
                        return@launch
                    }
                    // A refusal ON A DISCLOSED OFFER is framed by the screen in its own words — the
                    // handover we could not make, or the release that changed nothing — so the bare
                    // reason must not also arrive as a snackbar on top of it. Everything else on this
                    // screen still snackbars exactly as before.
                    val reason = errorTranslator.translate(result.error)
                    val refusedOfferAction = preferredOffer.value?.let {
                        when (action) {
                            OrderAction.Take -> OfferAction.Confirm
                            OrderAction.DeclineOffer -> OfferAction.Decline
                            else -> null
                        }
                    }
                    if (refusedOfferAction == null) {
                        snackbar.showError(reason)
                    } else {
                        _offerRefusal.value = OfferRefusal(
                            refusedOfferAction,
                            preferredOffer.value?.displayOrderNumber,
                            reason,
                        )
                    }
                    _actionState.value = ActionState.Error(reason)
                    _inFlightAction.value = null
                    // A clean reject almost always means the order moved on
                    // without us — another cleaner took it, or the status
                    // advanced from a different device. Without this the footer
                    // keeps offering the exact action the server just refused,
                    // so the cleaner taps it again and again. Straight to
                    // fetch(), not ensureFreshOrCachedAsync(): the cached copy
                    // is known to disagree with the server, which is precisely
                    // when a warm cache must not win. That also makes
                    // invalidateOrder() redundant here.
                    fetch(notifyOnError = false)
                }
            }
        }
    }

    private companion object {
        const val ACCEPTANCE_REQUIRED = "contract.acceptance_required"
        const val NOT_FOUND = "order.not_found"
    }
}

/** The page's own read once it has answered; until then the board's rows when it holds them. */
private fun PaneState.orBoard(board: List<OrderListItem>?, total: Int?): PaneState =
    if (hasLoaded || board == null) this else PaneState(rows = board, total = total, loadedAt = Instant.EPOCH)

private data class AreaQuery(val area: String?, val currency: String?)

/** The area to place while the page has no usable door: a job not hers, or hers without a geocode. */
private fun areaQuery(state: OrderDetailUiState, row: OrderListItem?): AreaQuery? {
    val order = (state as? OrderDetailUiState.Loaded)?.order
    if (order != null) {
        val door = (order.orderLocation() as? OrderLocation.Precise)?.let { usablePoint(it.latitude, it.longitude) }
        if (order.isAssignedToCurrentUser == true && door != null) return null
        return AreaQuery(order.customerAddressApproximate, order.currency?.code)
    }
    row ?: return null
    if (row.customerAddress != null && usablePoint(row.customerAddressLatitude, row.customerAddressLongitude) != null) return null
    return AreaQuery(row.customerAddressApproximate, row.currency?.code)
}
