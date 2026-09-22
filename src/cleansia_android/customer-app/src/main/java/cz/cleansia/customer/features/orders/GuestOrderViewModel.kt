package cz.cleansia.customer.features.orders

import android.content.Context
import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import cz.cleansia.core.format.formatOrderPrice
import cz.cleansia.core.network.ApiResult
import cz.cleansia.customer.R
import cz.cleansia.customer.core.auth.ApiErrorParser
import cz.cleansia.customer.core.orders.GuestOrderDto
import cz.cleansia.customer.core.orders.GuestOrderRepository
import cz.cleansia.customer.core.settings.AppSettingsRepository
import cz.cleansia.customer.ui.state.ActionState
import dagger.hilt.android.lifecycle.HiltViewModel
import dagger.hilt.android.qualifiers.ApplicationContext
import javax.inject.Inject
import kotlinx.coroutines.Job
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.launch

sealed interface GuestOrderUiState {
    data object Empty : GuestOrderUiState
    data object Loading : GuestOrderUiState
    data class Error(val message: String) : GuestOrderUiState
    data class Loaded(val order: GuestOrderDto) : GuestOrderUiState
    data class Cancelled(val message: String) : GuestOrderUiState
}

@HiltViewModel
class GuestOrderViewModel @Inject constructor(
    private val repository: GuestOrderRepository,
    private val settings: AppSettingsRepository,
    @ApplicationContext private val context: Context,
) : ViewModel() {
    private val _state = MutableStateFlow<GuestOrderUiState>(GuestOrderUiState.Empty)
    val state = _state.asStateFlow()
    private val _preview = MutableStateFlow<CancellationPreviewUiState>(CancellationPreviewUiState.Loading)
    val preview = _preview.asStateFlow()
    private val _cancelState = MutableStateFlow<ActionState>(ActionState.Idle)
    val cancelState = _cancelState.asStateFlow()
    private val _showCancellation = MutableStateFlow(false)
    val showCancellation = _showCancellation.asStateFlow()

    // The token lives only while this screen owns the lookup; never in a route or saved state.
    private var accessToken: String? = null
    private var generation = 0
    private var previewGeneration = 0
    private var lookupJob: Job? = null
    private var previewJob: Job? = null
    private var cancelJob: Job? = null

    fun clear() {
        generation++
        previewGeneration++
        lookupJob?.cancel()
        previewJob?.cancel()
        cancelJob?.cancel()
        accessToken = null
        _state.value = GuestOrderUiState.Empty
        _preview.value = CancellationPreviewUiState.Loading
        _cancelState.value = ActionState.Idle
        _showCancellation.value = false
    }

    fun onLinkChanged() {
        if (_cancelState.value !is ActionState.Submitting) clear()
    }

    fun lookup(pastedLink: String) {
        if (_cancelState.value is ActionState.Submitting || _state.value is GuestOrderUiState.Loading) return
        clear()
        val token = guestAccessTokenFrom(pastedLink)
        if (token.isBlank()) {
            _state.value = GuestOrderUiState.Error(context.getString(R.string.guest_order_required))
            return
        }
        accessToken = token
        val current = generation
        _state.value = GuestOrderUiState.Loading
        lookupJob = viewModelScope.launch {
            val result = repository.lookup(token)
            if (current != generation) return@launch
            _state.value = when (result) {
                is ApiResult.Success -> GuestOrderUiState.Loaded(result.data)
                is ApiResult.Error -> GuestOrderUiState.Error(ApiErrorParser.parseToUserMessage(context, result.error))
            }
        }
    }

    fun openCancellation() {
        val order = (_state.value as? GuestOrderUiState.Loaded)?.order ?: return
        if (!customerCanCancelOrder(order.status) || _cancelState.value is ActionState.Submitting) return
        _showCancellation.value = true
        loadPreview()
    }

    fun dismissCancellation() {
        if (_cancelState.value is ActionState.Submitting) return
        previewGeneration++
        previewJob?.cancel()
        _showCancellation.value = false
        _preview.value = CancellationPreviewUiState.Loading
        _cancelState.value = ActionState.Idle
    }

    fun loadPreview() {
        if (!_showCancellation.value || _cancelState.value is ActionState.Submitting) return
        val token = accessToken ?: return
        val order = (_state.value as? GuestOrderUiState.Loaded)?.order ?: return
        val current = generation
        val currentPreview = ++previewGeneration
        previewJob?.cancel()
        _preview.value = CancellationPreviewUiState.Loading
        _cancelState.value = ActionState.Idle
        previewJob = viewModelScope.launch {
            val result = repository.preview(token)
            if (current != generation || currentPreview != previewGeneration) return@launch
            _preview.value = when (result) {
                is ApiResult.Success -> {
                    val quote = result.data
                    if (quote.orderId == order.id && quote.currencyCode == order.currencyCode &&
                        cancellationFeeCallout(quote) != null) {
                        CancellationPreviewUiState.Loaded(quote)
                    } else {
                        CancellationPreviewUiState.Error
                    }
                }
                is ApiResult.Error -> {
                    _cancelState.value = ActionState.Error(ApiErrorParser.parseToUserMessage(context, result.error))
                    CancellationPreviewUiState.Error
                }
            }
        }
    }

    fun cancel(reason: String?) {
        val token = accessToken ?: return
        val order = (_state.value as? GuestOrderUiState.Loaded)?.order ?: return
        if (!_showCancellation.value || !customerCanCancelOrder(order.status) ||
            reason.isNullOrBlank() || reason.length > CANCEL_REASON_MAX_LENGTH ||
            !cancelConfirmEnabled(_preview.value, true, false, "", _cancelState.value is ActionState.Submitting, true)
        ) return
        val current = generation
        _cancelState.value = ActionState.Submitting
        cancelJob = viewModelScope.launch {
            val result = repository.cancel(token, reason, settings.emailLanguageTag())
            if (current != generation) return@launch
            when (result) {
                is ApiResult.Success -> {
                    val receipt = result.data
                    val amount = receipt.actualRefundAmount
                    val message = if (receipt.refundInitiated && amount != null && amount > 0) {
                        context.getString(R.string.order_cancel_success_with_refund, formatOrderPrice(amount, order.currencyCode))
                    } else {
                        context.getString(R.string.order_cancel_success_no_refund)
                    }
                    accessToken = null
                    _state.value = GuestOrderUiState.Cancelled(message)
                    _showCancellation.value = false
                    _cancelState.value = ActionState.Idle
                    _preview.value = CancellationPreviewUiState.Loading
                }
                is ApiResult.Error -> {
                    _cancelState.value = ActionState.Error(ApiErrorParser.parseToUserMessage(context, result.error))
                    _preview.value = CancellationPreviewUiState.Error
                }
            }
        }
    }
}
