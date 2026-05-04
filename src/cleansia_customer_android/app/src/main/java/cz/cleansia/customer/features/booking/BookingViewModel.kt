package cz.cleansia.customer.features.booking

import android.content.Context
import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import cz.cleansia.customer.R
import cz.cleansia.customer.core.auth.ApiErrorParser
import cz.cleansia.customer.core.booking.BookingApi
import cz.cleansia.customer.core.booking.CreateOrderAddressDto
import cz.cleansia.customer.core.booking.CreateOrderCommand
import cz.cleansia.customer.core.booking.CreateOrderResponse
import cz.cleansia.customer.core.booking.QuoteOrderCommand
import cz.cleansia.customer.core.booking.QuoteOrderResponse
import cz.cleansia.customer.core.promo.PromoCodeApi
import cz.cleansia.customer.core.promo.PromoCodeError
import cz.cleansia.customer.core.promo.ValidatePromoCodeRequest
import cz.cleansia.customer.core.referral.ReferralRepository
import cz.cleansia.customer.core.referral.ReferralValidationError
import cz.cleansia.customer.core.user.UserRepository
import cz.cleansia.customer.ui.snackbar.SnackbarController
import dagger.hilt.android.lifecycle.HiltViewModel
import dagger.hilt.android.qualifiers.ApplicationContext
import javax.inject.Inject
import kotlinx.coroutines.FlowPreview
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.collectLatest
import kotlinx.coroutines.flow.debounce
import kotlinx.coroutines.flow.distinctUntilChanged
import kotlinx.coroutines.flow.map
import kotlinx.coroutines.launch

/**
 * Outcome of a submit attempt. The sheet maps these to snackbar + navigation.
 * We don't use a plain nullable response because "profile incomplete" needs
 * a distinct UI action (navigate to Edit Profile) from other silent failures.
 */
sealed interface BookingSubmitOutcome {
    /** Cash flow — order is created, ready to navigate straight to success. */
    data class Success(val response: CreateOrderResponse) : BookingSubmitOutcome

    /**
     * Card flow — order is created in Pending, but payment must be confirmed
     * via Stripe PaymentSheet before we navigate. Caller is expected to
     * launch PaymentSheet with these params, then call back with the result
     * (success → navigate to success screen; cancel/fail → stay on the
     * booking screen for retry).
     */
    data class CardPending(
        val response: CreateOrderResponse,
        val paymentSheet: PaymentSheetParams,
    ) : BookingSubmitOutcome

    /** Swallowed failure — snackbar already fired, UI should stay put. */
    data object Failed : BookingSubmitOutcome
    /** User profile is missing a required field; UI should deep-link to Edit Profile. */
    data object ProfileIncomplete : BookingSubmitOutcome
}

/**
 * Bundle of values PaymentSheet needs to render: the PaymentIntent
 * client_secret it confirms against, plus the Stripe customer id +
 * ephemeral key that let it surface saved cards.
 */
data class PaymentSheetParams(
    val clientSecret: String,
    val ephemeralKey: String,
    val customerId: String,
)

/**
 * UI state for the promo-code input on the Confirm step. Drives the inline
 * indicator (spinner / green check / red X) and the summary-stack discount line.
 *
 * Lifecycle: starts at [Idle]; the moment the dialog's Apply tap fires the
 * one-shot validator flips to [Validating], then resolves to [Valid] (renders the
 * green check + discount line) or [Invalid] (renders the red X + error message).
 * Clearing the applied code drops back to [Idle] and the wire payload omits `promoCode`.
 */
sealed interface PromoCodeUiState {
    data object Idle : PromoCodeUiState
    data object Validating : PromoCodeUiState
    data class Valid(val discountAmount: Double) : PromoCodeUiState
    /** [error] is null when the failure is generic (network/HTTP/unknown enum value). */
    data class Invalid(val error: PromoCodeError?) : PromoCodeUiState
}

/**
 * UI state for the referral-code input on the Confirm step (Phase C). Same
 * three-state shape as [PromoCodeUiState] — but the wire payload is sent even
 * when state is [Invalid], because the backend's late-acceptance path is
 * fail-soft (an unknown/duplicate code is a silent no-op server-side, not an
 * error). The state only drives inline validation feedback.
 */
sealed interface ReferralCodeUiState {
    data object Idle : ReferralCodeUiState
    data object Validating : ReferralCodeUiState
    data class Valid(val referrerFirstName: String?) : ReferralCodeUiState
    data class Invalid(val error: ReferralValidationError?) : ReferralCodeUiState
}

/**
 * Owns booking-wizard state across step recompositions and drives the two-call
 * submission (/Quote for an authoritative total, then /Create). Screens mutate
 * state via [update]; the bottom sheet calls [submit] on the final confirm.
 */
@HiltViewModel
class BookingViewModel @Inject constructor(
    private val bookingApi: BookingApi,
    private val userRepository: UserRepository,
    private val promoCodeApi: PromoCodeApi,
    private val referralRepository: ReferralRepository,
    private val paymentRepository: cz.cleansia.customer.core.payments.PaymentRepository,
    private val snackbar: SnackbarController,
    @ApplicationContext private val appContext: Context,
) : ViewModel() {

    private val _state = MutableStateFlow(BookingState())
    val state: StateFlow<BookingState> = _state.asStateFlow()

    private val _submitting = MutableStateFlow(false)
    val submitting: StateFlow<Boolean> = _submitting.asStateFlow()

    private val _quote = MutableStateFlow<QuoteOrderResponse?>(null)
    val quote: StateFlow<QuoteOrderResponse?> = _quote.asStateFlow()

    private val _quoting = MutableStateFlow(false)
    val quoting: StateFlow<Boolean> = _quoting.asStateFlow()

    // Inputs that produced the current _quote value. Submit() consults this
    // to decide whether the cached quote can be reused without re-calling /Quote.
    private var lastQuoteInputs: QuoteInputs? = null

    @OptIn(FlowPreview::class)
    private val quoteWatcher = viewModelScope.launch {
        _state
            .map { it.toQuoteInputs() }
            .distinctUntilChanged()
            .debounce(400L)
            .collectLatest { refreshQuote() }
    }

    private val _promoCodeState = MutableStateFlow<PromoCodeUiState>(PromoCodeUiState.Idle)
    val promoCodeState: StateFlow<PromoCodeUiState> = _promoCodeState.asStateFlow()

    private val _referralCodeState = MutableStateFlow<ReferralCodeUiState>(ReferralCodeUiState.Idle)
    val referralCodeState: StateFlow<ReferralCodeUiState> = _referralCodeState.asStateFlow()

    /**
     * Loyalty Phase B — explicit one-shot validation triggered by the promo
     * dialog's Apply button. No debounce, no auto-rerun on subtotal change.
     * Caller passes the raw code; we normalize, hit /Validate once, update
     * [_promoCodeState], and return the resolved state.
     *
     * On [PromoCodeUiState.Valid] we also persist the canonical code into
     * [BookingState.promoCode] so [submit] picks it up. Mid-typing the dialog
     * mutates only its local input state — [BookingState] is touched only on
     * Apply success or via [clearPromoCode].
     */
    suspend fun validatePromoCodeNow(code: String): PromoCodeUiState {
        val normalized = code.trim().uppercase()
        if (normalized.isBlank()) {
            _promoCodeState.value = PromoCodeUiState.Idle
            return PromoCodeUiState.Idle
        }
        _promoCodeState.value = PromoCodeUiState.Validating
        val subtotal = _quote.value?.totalPrice ?: 0.0
        val newState = try {
            val resp = promoCodeApi.validate(ValidatePromoCodeRequest(normalized, subtotal))
            if (!resp.isSuccessful) {
                PromoCodeUiState.Invalid(null)
            } else {
                val body = resp.body()
                when {
                    body == null -> PromoCodeUiState.Invalid(null)
                    body.isValid && body.discountAmount != null -> PromoCodeUiState.Valid(body.discountAmount)
                    else -> PromoCodeUiState.Invalid(PromoCodeError.fromString(body.errorCode))
                }
            }
        } catch (t: Throwable) {
            PromoCodeUiState.Invalid(null)
        }
        _promoCodeState.value = newState
        if (newState is PromoCodeUiState.Valid) {
            _state.value = _state.value.copy(promoCode = normalized)
        }
        return newState
    }

    /**
     * Loyalty Phase C — explicit one-shot validation triggered by the referral
     * dialog's Apply button. Mirrors [validatePromoCodeNow] in shape and
     * lifecycle — persists the canonical code into [BookingState.referralCode]
     * only on Valid (so submit() forwards the user's actual successful intent).
     */
    suspend fun validateReferralCodeNow(code: String): ReferralCodeUiState {
        val normalized = code.trim().uppercase()
        if (normalized.isBlank()) {
            _referralCodeState.value = ReferralCodeUiState.Idle
            return ReferralCodeUiState.Idle
        }
        _referralCodeState.value = ReferralCodeUiState.Validating
        val resp = referralRepository.validate(normalized)
        val newState: ReferralCodeUiState = when {
            resp == null -> ReferralCodeUiState.Invalid(null)
            resp.isValid -> ReferralCodeUiState.Valid(resp.referrerFirstName)
            else -> ReferralCodeUiState.Invalid(ReferralValidationError.fromString(resp.errorCode))
        }
        _referralCodeState.value = newState
        if (newState is ReferralCodeUiState.Valid) {
            _state.value = _state.value.copy(referralCode = normalized)
        }
        return newState
    }

    /** Drop the applied promo code from both UI state and the booking payload. */
    fun clearPromoCode() {
        _promoCodeState.value = PromoCodeUiState.Idle
        _state.value = _state.value.copy(promoCode = "")
    }

    /** Drop the applied referral code from both UI state and the booking payload. */
    fun clearReferralCode() {
        _referralCodeState.value = ReferralCodeUiState.Idle
        _state.value = _state.value.copy(referralCode = "")
    }

    /**
     * Reset the entire wizard to a clean slate. Called on submit success and
     * on every fresh open of the sheet (when not in a rebook flow). Clears
     * services/packages/dates/address selections, the cached quote, in-flight
     * indicators, and both code-dialog UI states so the next open starts
     * blank — preserving rebook pre-fill, which runs in a separate effect
     * AFTER this reset.
     */
    fun reset() {
        _state.value = BookingState()
        _quote.value = null
        _quoting.value = false
        _submitting.value = false
        _promoCodeState.value = PromoCodeUiState.Idle
        _referralCodeState.value = ReferralCodeUiState.Idle
        lastQuoteInputs = null
    }

    fun update(transform: (BookingState) -> BookingState) {
        _state.value = transform(_state.value)
    }

    /**
     * Calls /Quote then /Create. Returns an outcome the sheet uses to decide
     * navigation: Success → confirmation screen; ProfileIncomplete → deep-link
     * to Edit Profile with a helpful snackbar; Failed → stay put (snackbar fired).
     */
    suspend fun submit(): BookingSubmitOutcome {
        if (_submitting.value) return BookingSubmitOutcome.Failed
        _submitting.value = true
        try {
            // Force a fresh profile fetch before the gate so we don't trust
            // a stale cache. Without this, a user with a recent server-side
            // phone update (or a failed onboarding flow) could slip through
            // and hit the backend's silent-rejection path. Best-effort —
            // network failure here just means we fall back to the cached
            // snapshot, which is still better than no check at all.
            userRepository.refreshCurrentUser()
            val user = userRepository.currentUser.value
            if (user == null) {
                snackbar.showError(appContext.getString(R.string.error_booking_sign_in_required))
                return BookingSubmitOutcome.Failed
            }

            // Pre-flight: backend validator requires non-empty CustomerName/Email/Phone.
            // Compute completeness directly from the just-refreshed snapshot
            // rather than reading the derived StateFlow (which may not have
            // propagated yet on the same coroutine tick).
            val phoneOk = !user.phoneNumber.isNullOrBlank()
            val nameOk = user.firstName.isNotBlank() && user.lastName.isNotBlank()
            val emailOk = user.email.isNotBlank()
            if (!(phoneOk && nameOk && emailOk)) {
                snackbar.showError(appContext.getString(R.string.error_booking_profile_incomplete))
                return BookingSubmitOutcome.ProfileIncomplete
            }

            val s = _state.value
            val instant = s.selectedInstant
            if (instant == null) {
                snackbar.showError(appContext.getString(R.string.error_booking_pick_time))
                return BookingSubmitOutcome.Failed
            }

            // Reuse the live-quote cache when its inputs match current state — saves a
            // round trip and guarantees the user submits exactly the number they saw.
            val currentInputs = s.toQuoteInputs()
            val cached = _quote.value
            val quoted: QuoteOrderResponse = if (cached != null && lastQuoteInputs == currentInputs) {
                cached
            } else {
                val quoteCmd = QuoteOrderCommand(
                    selectedServiceIds = s.selectedServiceIds.toList(),
                    selectedPackageIds = s.selectedPackageIds.toList(),
                    rooms = s.rooms,
                    bathrooms = s.bathrooms,
                    currencyId = null,
                )
                val quoteResp = try {
                    bookingApi.quote(quoteCmd)
                } catch (t: Throwable) {
                    snackbar.showError(appContext.getString(R.string.error_generic_network))
                    return BookingSubmitOutcome.Failed
                }
                if (!quoteResp.isSuccessful) {
                    val msg = ApiErrorParser.parseToUserMessage(appContext, quoteResp.errorBody(), quoteResp.code())
                    snackbar.showError(msg)
                    return BookingSubmitOutcome.Failed
                }
                quoteResp.body() ?: run {
                    snackbar.showError(appContext.getString(R.string.error_generic_network))
                    return BookingSubmitOutcome.Failed
                }
            }

            // Apply express surcharge client-side so the displayed total matches
            // what we send to /Create. Backend recomputes authoritatively, so a
            // wrong client clock can't underpay — see CreateOrder.Handler.
            val finalTotal = BookingPricing.finalTotal(quoted.totalPrice, instant)

            val createCmd = CreateOrderCommand(
                customerName = listOfNotNull(user.firstName, user.lastName)
                    .filter { it.isNotBlank() }
                    .joinToString(" "),
                customerEmail = user.email,
                customerPhone = user.phoneNumber.orEmpty(),
                // Backend expects XOR — send the inline address OR the savedAddressId.
                customerAddress = if (s.savedAddressId == null) {
                    CreateOrderAddressDto(
                        street = s.street,
                        city = s.city,
                        zipCode = s.zipCode,
                    )
                } else null,
                savedAddressId = s.savedAddressId,
                selectedPackageIds = s.selectedPackageIds.toList(),
                selectedServiceIds = s.selectedServiceIds.toList(),
                rooms = s.rooms,
                bathrooms = s.bathrooms,
                extras = emptyMap(),
                cleaningDate = instant.toString(),
                paymentType = if (s.paymentMethod.equals("card", ignoreCase = true)) 2 else 1,
                currencyId = quoted.currencyId,
                // Send the promo only when the live validation said it was valid;
                // an Invalid/Idle state means we don't trust it and let the backend
                // ignore. Backend re-validates anyway and applies best-discount-wins
                // between tier and promo, so client never claims the discount amount.
                promoCode = if (
                    _promoCodeState.value is PromoCodeUiState.Valid &&
                    s.promoCode.isNotBlank()
                ) {
                    s.promoCode.trim().uppercase()
                } else {
                    null
                },
                // Loyalty Phase C — fail-soft on the backend, so we send whatever
                // the user typed even when the client-side Validate call said
                // invalid. The handler checks for an existing Referral row and
                // silently skips on duplicate / unknown / self-referral.
                referralCode = s.referralCode.trim().uppercase().ifBlank { null },
                totalPrice = finalTotal,
                preferredEmployeeId = s.preferredEmployeeId,
            )

            val createResp = try {
                bookingApi.create(createCmd)
            } catch (t: Throwable) {
                snackbar.showError(appContext.getString(R.string.error_generic_network))
                return BookingSubmitOutcome.Failed
            }
            if (!createResp.isSuccessful) {
                val msg = ApiErrorParser.parseToUserMessage(appContext, createResp.errorBody(), createResp.code())
                snackbar.showError(msg)
                return BookingSubmitOutcome.Failed
            }
            val body = createResp.body() ?: run {
                snackbar.showError(appContext.getString(R.string.error_generic_network))
                return BookingSubmitOutcome.Failed
            }

            // Cash flow ends here — order is created and ready to display.
            // Card flow needs a second hop: create a Stripe PaymentIntent so
            // PaymentSheet can confirm payment before we navigate.
            if (!s.paymentMethod.equals("card", ignoreCase = true)) {
                return BookingSubmitOutcome.Success(body)
            }

            val intent = paymentRepository.createPaymentIntent(body.id)
            if (intent == null) {
                // Order is created but PaymentIntent failed — leave the order
                // in Pending. The stale-pending sweeper will clean it up
                // within an hour. Snackbar tells the user; they can retry.
                snackbar.showError(appContext.getString(R.string.error_generic_network))
                return BookingSubmitOutcome.Failed
            }

            return BookingSubmitOutcome.CardPending(
                response = body,
                paymentSheet = PaymentSheetParams(
                    clientSecret = intent.clientSecret,
                    ephemeralKey = intent.ephemeralKey,
                    customerId = intent.stripeCustomerId,
                ),
            )
        } finally {
            _submitting.value = false
        }
    }

    private suspend fun refreshQuote() {
        val s = _state.value
        val inputs = s.toQuoteInputs()
        if (inputs.serviceIds.isEmpty() && inputs.packageIds.isEmpty()) {
            _quote.value = null
            lastQuoteInputs = null
            return
        }
        _quoting.value = true
        try {
            val resp = try {
                bookingApi.quote(
                    QuoteOrderCommand(
                        selectedServiceIds = inputs.serviceIds.toList(),
                        selectedPackageIds = inputs.packageIds.toList(),
                        rooms = inputs.rooms,
                        bathrooms = inputs.bathrooms,
                        currencyId = null,
                    ),
                )
            } catch (t: Throwable) {
                null
            }
            if (resp?.isSuccessful == true) {
                val body = resp.body()
                if (body != null) {
                    _quote.value = body
                    lastQuoteInputs = inputs
                }
            }
            // Failures are silent during editing — leave _quote as-is so the
            // last known-good total stays on screen through transient network issues.
        } finally {
            _quoting.value = false
        }
    }

    private data class QuoteInputs(
        val serviceIds: Set<String>,
        val packageIds: Set<String>,
        val rooms: Int,
        val bathrooms: Int,
    )

    private fun BookingState.toQuoteInputs() = QuoteInputs(
        serviceIds = selectedServiceIds,
        packageIds = selectedPackageIds,
        rooms = rooms,
        bathrooms = bathrooms,
    )
}
