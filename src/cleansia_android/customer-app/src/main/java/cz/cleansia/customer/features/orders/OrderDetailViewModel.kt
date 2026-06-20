package cz.cleansia.customer.features.orders

import android.content.Context
import androidx.lifecycle.SavedStateHandle
import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import cz.cleansia.customer.R
import cz.cleansia.core.format.formatOrderPrice
import cz.cleansia.core.network.ApiError
import cz.cleansia.core.network.ApiResult
import cz.cleansia.customer.core.notifications.OrderEventBus
import cz.cleansia.customer.core.orders.CancelOrderResponse
import cz.cleansia.customer.core.orders.ConfirmRecurringOrderResponse
import cz.cleansia.customer.core.orders.OrderDetailDto
import cz.cleansia.customer.core.orders.OrderPhotosResponse
import cz.cleansia.customer.core.orders.OrderRepository
import cz.cleansia.customer.core.orders.OrderReviewDto
import cz.cleansia.core.snackbar.SnackbarController
import cz.cleansia.customer.ui.state.ActionState
import dagger.hilt.android.lifecycle.HiltViewModel
import dagger.hilt.android.qualifiers.ApplicationContext
import java.io.File
import javax.inject.Inject
import kotlinx.coroutines.Job
import kotlinx.coroutines.delay
import kotlinx.coroutines.flow.MutableSharedFlow
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.SharedFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asSharedFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.filter
import kotlinx.coroutines.flow.onEach
import kotlinx.coroutines.flow.launchIn
import kotlinx.coroutines.launch

/**
 * UI state for the Order detail screen. Drives a Loading → Loaded/Error
 * funnel; the screen mirrors these three branches one-to-one.
 *
 * The Error state carries a `canRetry` flag so we can distinguish between
 * "nav arg was missing" (user-facing fatal — no way to retry the same call)
 * and "the network call failed" (retry is meaningful). The repository already
 * surfaces transport/backend errors via SnackbarController, so we don't pass
 * a message here — the screen just shows a generic header + retry button.
 */
sealed interface OrderDetailUiState {
    data object Loading : OrderDetailUiState
    data class Error(val canRetry: Boolean) : OrderDetailUiState
    data class Loaded(val order: OrderDetailDto) : OrderDetailUiState
}

/**
 * Photos side-channel state. The main detail load does not include photos —
 * they're fetched lazily by [OrderDetailViewModel.ensurePhotosLoaded] once the
 * Loaded state is reached, so we don't block the primary render. Screens
 * observe this to render a summary "Photos" card only when we have a non-empty
 * response; Idle / Loading / Error all render nothing (snackbar owns errors).
 */
sealed interface PhotosUiState {
    data object Idle : PhotosUiState
    data object Loading : PhotosUiState
    data class Loaded(val response: OrderPhotosResponse) : PhotosUiState
    data object Error : PhotosUiState
}

/**
 * Fetches a single order's detail from [OrderRepository] and exposes the
 * result as a [StateFlow] of [OrderDetailUiState]. The `orderId` is read
 * from the navigation args — Compose Nav 2.8 typed routes serialize
 * `Routes.OrderDetail(orderId)` into the SavedStateHandle keyed by the
 * property name, so `savedStateHandle.get<String>("orderId")` keeps working
 * unchanged. (Equivalent: `savedStateHandle.toRoute<Routes.OrderDetail>().orderId`.)
 *
 * [OrderRepository.getById] already shows a snackbar on failure, so this VM
 * just needs to translate a null result into `Error(canRetry = true)` —
 * there's no need to double-surface the user-facing message.
 *
 * Wave 2 Phase 2 adds [cancel] + related state so the cancel sheet can drive
 * a backend-authoritative cancellation flow: the VM collects the submit
 * state, surfaces a success snackbar on the shared bus, and triggers a list
 * refresh on the singleton repo so the Orders tab reflects the new status.
 */
@HiltViewModel
class OrderDetailViewModel @Inject constructor(
    private val orderRepository: OrderRepository,
    private val snackbar: SnackbarController,
    @ApplicationContext private val appContext: Context,
    savedStateHandle: SavedStateHandle,
    val membershipRepository: cz.cleansia.customer.core.memberships.MembershipRepository,
    orderEventBus: OrderEventBus,
) : ViewModel() {

    /** Captured once in init so downstream calls (cancel, refresh) don't reread the handle. */
    private val orderId: String? = savedStateHandle.get<String>("orderId")

    private val _state = MutableStateFlow<OrderDetailUiState>(OrderDetailUiState.Loading)
    val state: StateFlow<OrderDetailUiState> = _state.asStateFlow()

    /**
     * Wave 4 — collapsed `_cancelling: Boolean` + `_cancelError: String?` into
     * a single sealed [ActionState]. Idle / Submitting / Error are mutually
     * exclusive, so the screen can't see "submitting AND errored" transients.
     * The one-shot success channel ([cancelResult]) is intentionally separate:
     * success is an effect, not a state, and a successful cancel returns the
     * flow to Idle so a hypothetical retry path could re-arm cleanly.
     */
    private val _cancelState = MutableStateFlow<ActionState>(ActionState.Idle)
    val cancelState: StateFlow<ActionState> = _cancelState.asStateFlow()

    /**
     * One-shot success channel for the cancel flow. The sheet observes this to
     * close itself + clear inline error; the screen also uses it to push the
     * success snackbar (templated with the refund amount when applicable).
     *
     * `extraBufferCapacity = 1` so emits from the VM never suspend if there's
     * no collector in the short window between submit + sheet dismiss.
     */
    private val _cancelResult = MutableSharedFlow<CancelOrderResponse>(extraBufferCapacity = 1)
    val cancelResult: SharedFlow<CancelOrderResponse> = _cancelResult.asSharedFlow()

    /** Wave 4 — collapsed `_submittingReview: Boolean` + `_reviewError: String?` into [ActionState]. */
    private val _reviewState = MutableStateFlow<ActionState>(ActionState.Idle)
    val reviewState: StateFlow<ActionState> = _reviewState.asStateFlow()

    /**
     * One-shot success channel for the review flow. Same pattern as
     * [cancelResult]: the screen observes this to flip the sheet closed. The
     * VM also pushes the success snackbar itself and re-fetches the detail so
     * the review section flips to its read-only form on next composition.
     */
    private val _reviewResult = MutableSharedFlow<OrderReviewDto>(extraBufferCapacity = 1)
    val reviewResult: SharedFlow<OrderReviewDto> = _reviewResult.asSharedFlow()

    /**
     * Wave 3.3 — Pending recurring-order confirm flow. Submitting hides the
     * Confirm CTA + spins a loader; success flows through [confirmResult] so
     * the screen can branch (Card → open PaymentSheet, Cash → snackbar +
     * refetch). Repo surfaces failure snackbars; we keep the state simple
     * (no inline error message) so the user just retries via the same CTA.
     */
    private val _confirmRecurringState = MutableStateFlow<ActionState>(ActionState.Idle)
    val confirmRecurringState: StateFlow<ActionState> = _confirmRecurringState.asStateFlow()

    private val _confirmResult = MutableSharedFlow<ConfirmRecurringOrderResponse>(extraBufferCapacity = 1)
    val confirmResult: SharedFlow<ConfirmRecurringOrderResponse> = _confirmResult.asSharedFlow()

    init {
        load()
        // Push-triggered refetch — when an order.* FCM event arrives for this
        // orderId, refetch immediately so the UI reflects the new status without
        // waiting for the next poll tick. The poller below stays as a safety
        // net for missed pushes (FCM rate-limit, app killed too long, no token).
        orderId?.let { id ->
            orderEventBus.events
                .filter { it.orderId == id }
                .onEach { load() }
                .launchIn(viewModelScope)
        }
    }

    /** Public hook for the Error state's retry button. */
    fun refresh() {
        load()
    }

    private fun load() {
        val id = orderId
        if (id.isNullOrBlank()) {
            // No nav arg → there is nothing to retry against. Fatal for this route.
            _state.value = OrderDetailUiState.Error(canRetry = false)
            return
        }
        _state.value = OrderDetailUiState.Loading
        viewModelScope.launch {
            val dto = orderRepository.getById(id).surfaceError().getOrNull()
            _state.value = if (dto != null) {
                OrderDetailUiState.Loaded(dto)
            } else {
                // Error already surfaced as a snackbar (non-network); offer a retry.
                OrderDetailUiState.Error(canRetry = true)
            }
            // Whenever a fresh detail comes in, re-evaluate whether to keep the
            // background status poller running. Active orders (Confirmed /
            // InProgress) get a quiet 30s tick so the LiveProgressHero stays
            // current without the user pulling to refresh; everything else
            // cancels the poller so we don't burn battery on a finished order.
            evaluateAutoRefresh()
        }
    }

    /**
     * Safety-net poller for active orders. Push-triggered refetch (init block,
     * via [OrderEventBus]) handles the common case; this 5-minute timer only
     * catches missed pushes (FCM rate-limit, app killed before token register,
     * etc.). Cancellable: when status leaves Confirmed/OnTheWay/InProgress
     * the job stops by itself. The poller silently re-fetches via the
     * repository — any error surfaces as a snackbar (repo behavior) but we
     * don't flip the UI state into Error, since the previous Loaded state is
     * still valid.
     */
    private var autoRefreshJob: Job? = null

    private fun evaluateAutoRefresh() {
        val current = state.value
        val statusValue = (current as? OrderDetailUiState.Loaded)?.order?.orderStatus?.value
        val status = orderStatusFromValue(statusValue)
        val isActive = status == OrderStatus.Confirmed
            || status == OrderStatus.OnTheWay
            || status == OrderStatus.InProgress
        if (isActive) {
            if (autoRefreshJob?.isActive == true) return
            autoRefreshJob = viewModelScope.launch {
                while (true) {
                    delay(POLL_INTERVAL_MS)
                    val id = orderId ?: break
                    val fresh = orderRepository.getById(id).surfaceError().getOrNull()
                    if (fresh != null) {
                        _state.value = OrderDetailUiState.Loaded(fresh)
                        // Status may have flipped to Completed/Cancelled — let
                        // the next pass cancel us.
                        if (fresh.orderStatus?.value !in listOf(2, 3)) break
                    }
                }
                autoRefreshJob = null
            }
        } else {
            autoRefreshJob?.cancel()
            autoRefreshJob = null
        }
    }

    override fun onCleared() {
        autoRefreshJob?.cancel()
        super.onCleared()
    }

    private companion object {
        // 5 minutes — safety net only. Push events from [OrderEventBus] handle
        // the common case at near-zero latency.
        const val POLL_INTERVAL_MS = 5L * 60L * 1000L
    }

    /**
     * Submit a cancellation for the current order. Reason is optional and
     * trimmed+blank-coerced-to-null before the network call (backend treats
     * empty string and null equivalently, but sending null is tidier).
     *
     * On failure, the repo already surfaces a snackbar; we only set an inline
     * hint on the sheet so the user understands the sheet stayed open for
     * retry. On success we emit to [cancelResult], push a success snackbar,
     * and kick off a list refresh + detail re-fetch so the Cancelled status
     * is visible everywhere the moment the user closes the sheet.
     */
    fun cancel(reason: String?) {
        val id = orderId
        if (id.isNullOrBlank()) return
        if (_cancelState.value is ActionState.Submitting) return
        viewModelScope.launch {
            _cancelState.value = ActionState.Submitting
            val result = orderRepository.cancel(id, reason?.trim()?.ifBlank { null })
                .surfaceError().getOrNull()
            if (result == null) {
                // Snackbar already surfaced (non-network) — inline hint tells the
                // user the sheet is still live and ready for another try.
                _cancelState.value = ActionState.Error(
                    appContext.getString(R.string.order_cancel_retry_hint),
                )
            } else {
                // Build the success snackbar text here — the VM has both the
                // currency code (from the currently loaded detail, if any) and
                // the wire values. Fallbacks keep us safe if state is Loaded-less.
                val currencyCode = (state.value as? OrderDetailUiState.Loaded)?.order?.currency?.code
                val message = if (result.refundInitiated && result.refundAmount > 0.0) {
                    appContext.getString(
                        R.string.order_cancel_success_with_refund,
                        formatOrderPrice(result.refundAmount, currencyCode),
                    )
                } else {
                    appContext.getString(R.string.order_cancel_success_no_refund)
                }
                snackbar.showSuccess(message)

                // Drop back to Idle on success — the sheet is about to close
                // (driven by [cancelResult]) and any future re-open should
                // start with a clean slate, not a stale Submitting flag.
                _cancelState.value = ActionState.Idle
                _cancelResult.emit(result)
                // Invalidate the shared orders cache so OrdersTab picks up the
                // Cancelled status on its next composition. Silent on failure —
                // the user can pull-to-refresh if the list is stale.
                orderRepository.refresh()
                // Re-fetch the current detail so this screen also reflects the
                // new status (status pill, timeline, footer visibility).
                load()
            }
        }
    }

    /**
     * Clear any inline cancel-error hint — called when the user edits the
     * reason field. Only flips Error→Idle; Submitting / Idle are no-ops so a
     * mid-flight edit can't accidentally cancel the spinner.
     */
    fun dismissCancelError() {
        if (_cancelState.value is ActionState.Error) {
            _cancelState.value = ActionState.Idle
        }
    }

    /**
     * Wave 3.3 — confirm a Pending recurring-template order. Backend branches
     * on payment type:
     *   * Cash response (clientSecret == null) → backend already flipped the
     *     order to Confirmed + Paid; we push a success snackbar + refetch.
     *   * Card response (clientSecret != null) → screen consumes [confirmResult]
     *     and opens the Stripe PaymentSheet with the returned client secret.
     *
     * Repo surfaces snackbar on failure. Idempotent: a second tap while
     * Submitting is a no-op.
     */
    fun confirmRecurring() {
        val id = orderId
        if (id.isNullOrBlank()) return
        if (_confirmRecurringState.value is ActionState.Submitting) return
        viewModelScope.launch {
            _confirmRecurringState.value = ActionState.Submitting
            val resp = orderRepository.confirmRecurring(id).surfaceError().getOrNull()
            if (resp == null) {
                _confirmRecurringState.value = ActionState.Idle
                return@launch
            }
            _confirmResult.emit(resp)
            _confirmRecurringState.value = ActionState.Idle

            // Cash path: backend already moved the order to Confirmed + Paid.
            // Push the success snackbar + refetch so the screen reflects the
            // new status. Card path's snackbars fire from the PaymentSheet
            // result callback (via [notifyCardPaymentResult]) since only the
            // screen sees the Stripe outcome.
            if (resp.clientSecret.isNullOrBlank()) {
                snackbar.showSuccess(
                    appContext.getString(R.string.recurring_confirm_success),
                )
                orderRepository.refresh()
                load()
            }
        }
    }

    /**
     * Wave 3.3 — screen-side hook for the Stripe PaymentSheet result on the
     * Card confirm path. Success refetches the order; cancel/failure surface
     * the matching snackbar. Kept on the VM so the snackbar wiring stays
     * centralized and the screen doesn't need its own SnackbarController.
     */
    fun notifyCardPaymentResult(success: Boolean, errorMessage: String? = null) {
        viewModelScope.launch {
            if (success) {
                snackbar.showSuccess(appContext.getString(R.string.recurring_confirm_success))
                orderRepository.refresh()
                load()
            } else {
                snackbar.showError(
                    errorMessage ?: appContext.getString(R.string.error_payment_failed),
                )
            }
        }
    }

    /**
     * Submit (create or edit) a review for the current order. The backend
     * `SubmitOrderReview` is upsert by design — same endpoint handles both
     * paths. The repo already surfaces a snackbar on network/HTTP failure;
     * on success we push our own snackbar (variant depending on [isEdit]),
     * emit on [reviewResult] so the screen can close the sheet, and re-fetch
     * the detail so the `order.review` card re-renders.
     *
     * Caller-supplied rating is validated (1..5); anything else is a no-op.
     * The comment is trimmed, truncated at 2000 chars (matching the backend),
     * and coerced to null if it comes out empty so the wire payload is tidy.
     *
     * @param isEdit true when the user is updating an existing review; drives
     *  the success snackbar copy ("Review updated." vs "Thanks — review submitted.").
     */
    fun submitReview(rating: Int, comment: String?, isEdit: Boolean = false) {
        val id = orderId
        if (id.isNullOrBlank()) return
        if (rating !in 1..5) return
        if (_reviewState.value is ActionState.Submitting) return
        viewModelScope.launch {
            _reviewState.value = ActionState.Submitting
            val trimmed = comment?.trim()?.take(2000)?.ifBlank { null }
            val result = orderRepository.submitReview(id, rating, trimmed).surfaceError().getOrNull()
            if (result == null) {
                _reviewState.value = ActionState.Error(
                    appContext.getString(R.string.order_review_retry_hint),
                )
            } else {
                val successMessage = appContext.getString(
                    if (isEdit) R.string.order_review_updated
                    else R.string.order_review_success,
                )
                snackbar.showSuccess(successMessage)
                _reviewState.value = ActionState.Idle
                _reviewResult.emit(result)
                // Re-fetch the detail so the review section reflects the new
                // values on next composition.
                load()
            }
        }
    }

    /** Clear any inline review-error hint — mirrors [dismissCancelError]. Error→Idle only. */
    fun dismissReviewError() {
        if (_reviewState.value is ActionState.Error) {
            _reviewState.value = ActionState.Idle
        }
    }

    /**
     * Wave 4 — formerly `_downloadingReceipt: Boolean`. There's no error
     * sub-state today (the repository surfaces failures via snackbar), so the
     * Error variant is unused for this flow. Kept on the [ActionState] sealed
     * shape for symmetry with cancel + review and to leave the door open for
     * future inline-error treatment without another shape change.
     */
    private val _receiptDownloadState = MutableStateFlow<ActionState>(ActionState.Idle)
    val receiptDownloadState: StateFlow<ActionState> = _receiptDownloadState.asStateFlow()

    /**
     * One-shot channel that hands the downloaded File back to the screen so it
     * can launch the PDF viewer Intent. The VM deliberately stays Intent-free
     * (no Android activity coupling here) — the screen is the right layer to
     * own the chooser launch.
     *
     * `extraBufferCapacity = 1` so emits don't suspend if there's no collector
     * attached the exact moment the download finishes.
     */
    private val _receiptFile = MutableSharedFlow<File>(extraBufferCapacity = 1)
    val receiptFile: SharedFlow<File> = _receiptFile.asSharedFlow()

    /**
     * Kick off a receipt download. On success we emit the saved File on
     * [receiptFile] for the screen to hand to [openReceiptPdf]. On failure the
     * repo already showed a snackbar — we just flip loading off. Guarded
     * against re-entry: tapping the button twice while a download is in flight
     * is a no-op.
     */
    fun downloadReceipt() {
        val id = orderId
        if (id.isNullOrBlank()) return
        if (_receiptDownloadState.value is ActionState.Submitting) return
        viewModelScope.launch {
            _receiptDownloadState.value = ActionState.Submitting
            val file = orderRepository.downloadReceipt(id).surfaceError().getOrNull()
            _receiptDownloadState.value = ActionState.Idle
            if (file != null) {
                _receiptFile.emit(file)
            }
            // On failure the snackbar is already surfaced (non-network) — nothing more to do.
        }
    }

    /**
     * Show a "no PDF viewer" snackbar. Called from the screen's LaunchedEffect
     * when [openReceiptPdf] returns [ReceiptOpenResult.NoViewer]. Routing this
     * through the VM keeps all snackbar ownership in one layer, matching the
     * Phase 2 / Phase 3 pattern.
     */
    fun emitReceiptNoViewer() {
        snackbar.showError(appContext.getString(R.string.order_receipt_no_viewer))
    }

    /** Show a generic "couldn't open receipt" snackbar — mirrors [emitReceiptNoViewer]. */
    fun emitReceiptOpenError() {
        snackbar.showError(appContext.getString(R.string.order_receipt_open_error))
    }

    /**
     * Photos side-channel (Wave 2 Phase 5). Fetched lazily after the main
     * detail load so the primary render isn't blocked on a second network
     * round-trip. The repo already surfaces a snackbar on failure — we only
     * transition to `Error` so the screen can skip rendering the section.
     *
     * SAS URLs carry a 1h TTL, so we don't cache across re-opens — each detail
     * visit re-enters Idle → Loading → Loaded/Error.
     */
    private val _photos = MutableStateFlow<PhotosUiState>(PhotosUiState.Idle)
    val photos: StateFlow<PhotosUiState> = _photos.asStateFlow()

    /**
     * Fetch photos if we haven't already (or retry if the previous fetch
     * errored). No-op once a successful Loaded result is held — screens
     * navigating away and back to the detail get fresh data via the screen-
     * level [OrderPhotosViewModel] rather than through this summary channel.
     */
    fun ensurePhotosLoaded() {
        val id = orderId ?: return
        if (_photos.value !is PhotosUiState.Idle && _photos.value !is PhotosUiState.Error) return
        viewModelScope.launch {
            _photos.value = PhotosUiState.Loading
            val resp = orderRepository.getPhotos(id).surfaceError().getOrNull()
            _photos.value = if (resp != null) PhotosUiState.Loaded(resp) else PhotosUiState.Error
        }
    }

    /**
     * Surface a repository failure as a single snackbar, skipping
     * [ApiError.Network] (NetworkErrorInterceptor owns the infra toast). Returns
     * the result so callers can `.getOrNull()` for the success branch.
     */
    private fun <T> ApiResult<T>.surfaceError(): ApiResult<T> = onError { error ->
        if (error !is ApiError.Network) snackbar.showError(error.getUserMessage())
    }
}
