package cz.cleansia.customer.features.orders

import android.content.Context
import androidx.lifecycle.SavedStateHandle
import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import cz.cleansia.customer.R
import cz.cleansia.customer.core.format.formatOrderPrice
import cz.cleansia.customer.core.orders.CancelOrderResponse
import cz.cleansia.customer.core.orders.OrderDetailDto
import cz.cleansia.customer.core.orders.OrderPhotosResponse
import cz.cleansia.customer.core.orders.OrderRepository
import cz.cleansia.customer.core.orders.OrderReviewDto
import cz.cleansia.customer.ui.snackbar.SnackbarController
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
 * from the navigation args (`Routes.OrderDetail = "orders/{orderId}"`).
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
) : ViewModel() {

    /** Captured once in init so downstream calls (cancel, refresh) don't reread the handle. */
    private val orderId: String? = savedStateHandle.get<String>("orderId")

    private val _state = MutableStateFlow<OrderDetailUiState>(OrderDetailUiState.Loading)
    val state: StateFlow<OrderDetailUiState> = _state.asStateFlow()

    private val _cancelling = MutableStateFlow(false)
    val cancelling: StateFlow<Boolean> = _cancelling.asStateFlow()

    private val _cancelError = MutableStateFlow<String?>(null)
    val cancelError: StateFlow<String?> = _cancelError.asStateFlow()

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

    private val _submittingReview = MutableStateFlow(false)
    val submittingReview: StateFlow<Boolean> = _submittingReview.asStateFlow()

    private val _reviewError = MutableStateFlow<String?>(null)
    val reviewError: StateFlow<String?> = _reviewError.asStateFlow()

    /**
     * One-shot success channel for the review flow. Same pattern as
     * [cancelResult]: the screen observes this to flip the sheet closed. The
     * VM also pushes the success snackbar itself and re-fetches the detail so
     * the review section flips to its read-only form on next composition.
     */
    private val _reviewResult = MutableSharedFlow<OrderReviewDto>(extraBufferCapacity = 1)
    val reviewResult: SharedFlow<OrderReviewDto> = _reviewResult.asSharedFlow()

    init {
        load()
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
            val dto = orderRepository.getById(id)
            _state.value = if (dto != null) {
                OrderDetailUiState.Loaded(dto)
            } else {
                // Repository already showed a snackbar; offer a retry.
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
     * Lightweight 30s background poller for active orders. Cancellable: when
     * status leaves Confirmed/InProgress (cleaner finishes, user cancels) the
     * job stops by itself. The poller silently re-fetches via the repository —
     * any error surfaces as a snackbar (repo behavior) but we don't flip the
     * UI state into Error, since the previous Loaded state is still valid.
     */
    private var autoRefreshJob: Job? = null

    private fun evaluateAutoRefresh() {
        val current = state.value
        val statusValue = (current as? OrderDetailUiState.Loaded)?.order?.orderStatus?.value
        val isActive = statusValue == 2 || statusValue == 3 // Confirmed / InProgress
        if (isActive) {
            if (autoRefreshJob?.isActive == true) return
            autoRefreshJob = viewModelScope.launch {
                while (true) {
                    delay(POLL_INTERVAL_MS)
                    val id = orderId ?: break
                    val fresh = orderRepository.getById(id)
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
        const val POLL_INTERVAL_MS = 30_000L
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
        if (_cancelling.value) return
        viewModelScope.launch {
            _cancelling.value = true
            _cancelError.value = null
            val result = orderRepository.cancel(id, reason?.trim()?.ifBlank { null })
            _cancelling.value = false
            if (result == null) {
                // Snackbar already shown by repo — inline hint tells the user the
                // sheet is still live and ready for another try.
                _cancelError.value = appContext.getString(R.string.order_cancel_retry_hint)
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

    /** Clear any inline cancel-error hint — called when the user edits the reason field. */
    fun dismissCancelError() {
        _cancelError.value = null
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
        if (_submittingReview.value) return
        viewModelScope.launch {
            _submittingReview.value = true
            _reviewError.value = null
            val trimmed = comment?.trim()?.take(2000)?.ifBlank { null }
            val result = orderRepository.submitReview(id, rating, trimmed)
            _submittingReview.value = false
            if (result == null) {
                _reviewError.value = appContext.getString(R.string.order_review_retry_hint)
            } else {
                val successMessage = appContext.getString(
                    if (isEdit) R.string.order_review_updated
                    else R.string.order_review_success,
                )
                snackbar.showSuccess(successMessage)
                _reviewResult.emit(result)
                // Re-fetch the detail so the review section reflects the new
                // values on next composition.
                load()
            }
        }
    }

    /** Clear any inline review-error hint — mirrors [dismissCancelError]. */
    fun dismissReviewError() {
        _reviewError.value = null
    }

    private val _downloadingReceipt = MutableStateFlow(false)
    val downloadingReceipt: StateFlow<Boolean> = _downloadingReceipt.asStateFlow()

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
        if (_downloadingReceipt.value) return
        viewModelScope.launch {
            _downloadingReceipt.value = true
            val file = orderRepository.downloadReceipt(id)
            _downloadingReceipt.value = false
            if (file != null) {
                _receiptFile.emit(file)
            }
            // On failure the repo already surfaced a snackbar — nothing more to do.
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
            val resp = orderRepository.getPhotos(id)
            _photos.value = if (resp != null) PhotosUiState.Loaded(resp) else PhotosUiState.Error
        }
    }
}
