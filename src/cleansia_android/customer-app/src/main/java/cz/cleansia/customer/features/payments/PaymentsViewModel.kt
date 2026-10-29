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
import cz.cleansia.customer.core.payments.CardCaptureMarket
import cz.cleansia.customer.core.payments.SavedCardCapture
import cz.cleansia.customer.core.payments.SavedCardSetup
import cz.cleansia.customer.core.payments.StripeConfig
import cz.cleansia.customer.core.payments.needsCapture
import cz.cleansia.customer.core.market.MarketRepository
import cz.cleansia.customer.core.market.MarketState
import cz.cleansia.customer.core.market.selectedOrNull
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
import kotlinx.coroutines.flow.combine
import kotlinx.coroutines.flow.map
import kotlinx.coroutines.flow.stateIn
import kotlinx.coroutines.flow.SharingStarted
import java.util.Locale
import kotlinx.coroutines.launch

sealed interface PaymentsUiState {
    data object Loading : PaymentsUiState
    data object Error : PaymentsUiState
    data class Loaded(val receivables: List<Receivable>, val cards: List<SavedCard>) : PaymentsUiState
}

data class CardCapturePresentation(val market: CardCaptureMarket, val attempt: Long)

private fun MarketState.captureScope(): CardCaptureMarket {
    val market = selectedOrNull
    return CardCaptureMarket(market?.countryId, market?.currencyCode?.uppercase(Locale.ROOT))
}

/** What the customer owes and the card that guarantees their cash bookings. */
@HiltViewModel
class PaymentsViewModel @Inject constructor(
    private val savedCardRepository: SavedCardRepository,
    private val receivableRepository: ReceivableRepository,
    private val snackbar: SnackbarController,
    @ApplicationContext private val appContext: Context,
    private val marketRepository: MarketRepository,
    stripeConfig: StripeConfig,
) : ViewModel() {

    private val sessionEpoch = savedCardRepository.sessionEpoch
    val cardSavingAvailable = stripeConfig.isCardPaymentAvailable
    val captureMarket = marketRepository.state.map { it.captureScope() }
        .stateIn(viewModelScope, SharingStarted.Eagerly, marketRepository.state.value.captureScope())
    val capture = combine(captureMarket, savedCardRepository.captures) { market, captures -> captures[market] }
        .stateIn(viewModelScope, SharingStarted.Eagerly, null)
    val offersCardCapture = combine(captureMarket, savedCardRepository.cards) { market, cards ->
        cardSavingAvailable && savedCardRepository.sessionEpoch == sessionEpoch && cards?.needsCapture(market.currencyCode) == true
    }.stateIn(viewModelScope, SharingStarted.Eagerly, false)
    private val _consent = MutableStateFlow(false)
    val consent: StateFlow<Boolean> = _consent.asStateFlow()
    private val _capturePresentation = MutableSharedFlow<CardCapturePresentation>(extraBufferCapacity = 1)
    val capturePresentation = _capturePresentation.asSharedFlow()
    private var presentedCapture: Pair<CardCaptureMarket, Long>? = null

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
        viewModelScope.launch {
            var previous = captureMarket.value
            captureMarket.collect { market ->
                if (market != previous) {
                    _consent.value = false
                    savedCardRepository.pauseVerification(previous)
                }
                previous = market
            }
        }
        viewModelScope.launch {
            savedCardRepository.cards.collect { cards ->
                if (cards != null) {
                    (_state.value as? PaymentsUiState.Loaded)?.let { _state.value = it.copy(cards = cards) }
                } else {
                    _consent.value = false
                    presentedCapture = null
                    _state.value = PaymentsUiState.Loading
                }
            }
        }
    }

    fun load() {
        viewModelScope.launch {
            val loaded = _state.value as? PaymentsUiState.Loaded
            if (loaded == null) _state.value = PaymentsUiState.Loading
            _state.value = read() ?: loaded ?: PaymentsUiState.Error
            verifyPending()
        }
    }

    fun onResumed() {
        verifyPending()
        if (!awaitingPayment) return
        awaitingPayment = false
        viewModelScope.launch { read()?.let { _state.value = it } }
    }

    fun setConsent(accepted: Boolean) { _consent.value = accepted && savedCardRepository.sessionEpoch == sessionEpoch }

    fun saveCard() {
        if (!offersCardCapture.value || !_consent.value) return
        val market = captureMarket.value
        if (savedCardRepository.captures.value.containsKey(market)) return
        viewModelScope.launch {
            when (val result = savedCardRepository.beginCapture(market, consentAccepted = true, expectedSessionEpoch = sessionEpoch)) {
                is ApiResult.Success -> if (captureMarket.value == market) continueCapture()
                is ApiResult.Error -> surfaceError(result.error)
                null -> Unit
            }
        }
    }

    fun continueCapture() {
        if (savedCardRepository.sessionEpoch != sessionEpoch) return
        if (presentedCapture != null || _capturePresentation.subscriptionCount.value == 0) return
        val market = captureMarket.value
        val sheet = savedCardRepository.captures.value[market] as? SavedCardCapture.Sheet ?: return
        _capturePresentation.tryEmit(CardCapturePresentation(market, sheet.attempt))
    }

    fun claimCapturePresentation(presentation: CardCapturePresentation): SavedCardSetup? {
        if (presentedCapture != null || marketRepository.state.value.captureScope() != presentation.market) return null
        val setup = savedCardRepository.setupForPresentation(presentation.market, presentation.attempt, sessionEpoch) ?: return null
        presentedCapture = presentation.market to presentation.attempt
        return setup
    }

    fun onCaptureResult(presentation: CardCapturePresentation, completed: Boolean, failed: Boolean = false) {
        if (presentedCapture == (presentation.market to presentation.attempt)) presentedCapture = null
        if (savedCardRepository.captures.value[presentation.market]?.attempt != presentation.attempt) return
        if (completed) {
            if (savedCardRepository.sheetCompleted(presentation.market, presentation.attempt) && captureMarket.value == presentation.market) verifyPending()
        } else {
            savedCardRepository.abandonCapture(presentation.market, presentation.attempt)
            if (failed && captureMarket.value == presentation.market) snackbar.showErrorKey(R.string.payments_card_setup_failed)
        }
    }

    /** ActivityResult can be delivered to a new composable after configuration recreation. */
    fun onPresentedCaptureResult(completed: Boolean, failed: Boolean = false) {
        val (market, attempt) = presentedCapture ?: return
        val sheet = savedCardRepository.captures.value[market] as? SavedCardCapture.Sheet
        if (sheet == null || sheet.attempt != attempt) {
            presentedCapture = null
            return
        }
        onCaptureResult(CardCapturePresentation(market, attempt), completed, failed)
    }

    fun verifyPending() {
        val market = captureMarket.value
        if (savedCardRepository.captures.value[market] !is SavedCardCapture.Pending) return
        viewModelScope.launch {
            when (val result = savedCardRepository.verifyCapture(market, expectedSessionEpoch = sessionEpoch)) {
                is ApiResult.Success -> if (result.data && captureMarket.value == market) {
                    _consent.value = false
                    snackbar.showSuccessKey(R.string.payments_card_saved)
                }
                is ApiResult.Error -> if (captureMarket.value == market) {
                    // Inline pending survives the failure, including when the network interceptor also reports it.
                    snackbar.showError(ApiErrorParser.parseToUserMessage(appContext, result.error))
                }
                null -> Unit
            }
        }
    }

    fun restartCapture() {
        if (savedCardRepository.sessionEpoch != sessionEpoch) return
        val market = captureMarket.value
        savedCardRepository.captures.value[market]?.let { savedCardRepository.abandonCapture(market, it.attempt) }
        _consent.value = false
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
