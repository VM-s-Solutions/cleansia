package cz.cleansia.customer.features.payments

import android.content.Context
import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import cz.cleansia.core.network.ApiError
import cz.cleansia.core.network.ApiResult
import cz.cleansia.core.snackbar.SnackbarController
import cz.cleansia.core.ui.state.ActionState
import cz.cleansia.customer.R
import cz.cleansia.customer.core.auth.ApiErrorParser
import cz.cleansia.customer.core.payments.Receivable
import cz.cleansia.customer.core.payments.ReceivableRepository
import cz.cleansia.customer.core.payments.SavedCard
import cz.cleansia.customer.core.payments.SavedCardRepository
import dagger.hilt.android.lifecycle.HiltViewModel
import dagger.hilt.android.qualifiers.ApplicationContext
import javax.inject.Inject
import kotlinx.coroutines.async
import kotlinx.coroutines.flow.MutableSharedFlow
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.SharedFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asSharedFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.launch

sealed interface PaymentsUiState {
    data object Loading : PaymentsUiState
    data object Error : PaymentsUiState
    data class Loaded(val receivables: List<Receivable>, val cards: List<SavedCard>) : PaymentsUiState
}

/** What the customer owes and the card that guarantees their cash bookings. */
@HiltViewModel
class PaymentsViewModel @Inject constructor(
    private val savedCardRepository: SavedCardRepository,
    private val receivableRepository: ReceivableRepository,
    private val snackbar: SnackbarController,
    @ApplicationContext private val appContext: Context,
) : ViewModel() {

    private val _state = MutableStateFlow<PaymentsUiState>(PaymentsUiState.Loading)
    val state: StateFlow<PaymentsUiState> = _state.asStateFlow()

    private val _removeState = MutableStateFlow<ActionState>(ActionState.Idle)
    val removeState: StateFlow<ActionState> = _removeState.asStateFlow()

    private val _payState = MutableStateFlow<ActionState>(ActionState.Idle)
    val payState: StateFlow<ActionState> = _payState.asStateFlow()

    private val _payLinks = MutableSharedFlow<String>(extraBufferCapacity = 1)

    /** The Stripe Checkout page to open; the screen hands it to the browser. */
    val payLinks: SharedFlow<String> = _payLinks.asSharedFlow()

    /** A pay link went to the browser, so the list is re-read when the customer comes back. */
    private var awaitingPayment = false

    init {
        load()
    }

    fun load() {
        viewModelScope.launch {
            _state.value = PaymentsUiState.Loading
            _state.value = read() ?: PaymentsUiState.Error
        }
    }

    fun onResumed() {
        if (!awaitingPayment) return
        awaitingPayment = false
        viewModelScope.launch { read()?.let { _state.value = it } }
    }

    fun remove(card: SavedCard) {
        if (_removeState.value is ActionState.Submitting) return
        _removeState.value = ActionState.Submitting
        viewModelScope.launch {
            when (val result = savedCardRepository.remove(card.id)) {
                is ApiResult.Success -> {
                    _removeState.value = ActionState.Idle
                    snackbar.showSuccessKey(R.string.payments_card_removed)
                    (_state.value as? PaymentsUiState.Loaded)?.let { loaded ->
                        _state.value = loaded.copy(cards = loaded.cards.filterNot { it.id == card.id })
                    }
                }
                is ApiResult.Error -> {
                    // The confirm closed on the tap, so the snackbar is where a refusal is said; the card stays.
                    surfaceError(result.error)
                    _removeState.value = ActionState.Idle
                }
            }
        }
    }

    fun pay(receivable: Receivable) {
        if (_payState.value is ActionState.Submitting) return
        _payState.value = ActionState.Submitting
        viewModelScope.launch {
            when (val result = receivableRepository.createPayLink(receivable.id)) {
                is ApiResult.Success -> {
                    _payState.value = ActionState.Idle
                    awaitingPayment = true
                    _payLinks.emit(result.data)
                }
                is ApiResult.Error -> {
                    surfaceError(result.error)
                    _payState.value = ActionState.Idle
                }
            }
        }
    }

    private suspend fun read(): PaymentsUiState.Loaded? {
        val receivables = viewModelScope.async { receivableRepository.getMine() }
        val cards = viewModelScope.async { savedCardRepository.refresh() }
        val owed = receivables.await()
        val held = cards.await()
        return when {
            owed is ApiResult.Error -> {
                surfaceError(owed.error)
                null
            }
            held is ApiResult.Error -> {
                surfaceError(held.error)
                null
            }
            else -> PaymentsUiState.Loaded(
                receivables = (owed as ApiResult.Success).data,
                cards = (held as ApiResult.Success).data,
            )
        }
    }

    private fun surfaceError(error: ApiError) {
        if (error is ApiError.Network) return
        snackbar.showError(ApiErrorParser.parseToUserMessage(appContext, error))
    }
}
