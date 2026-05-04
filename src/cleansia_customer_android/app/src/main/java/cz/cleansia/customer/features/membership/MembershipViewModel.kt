package cz.cleansia.customer.features.membership

import android.content.Context
import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import cz.cleansia.customer.R
import cz.cleansia.customer.core.memberships.GetMyMembershipResponse
import cz.cleansia.customer.core.memberships.MembershipPlanDto
import cz.cleansia.customer.core.memberships.MembershipRepository
import cz.cleansia.customer.ui.snackbar.SnackbarController
import dagger.hilt.android.lifecycle.HiltViewModel
import dagger.hilt.android.qualifiers.ApplicationContext
import javax.inject.Inject
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.launch

/**
 * Outcome of [MembershipViewModel.startSubscribe]. The screen maps these
 * to PaymentSheet launch (NeedsPaymentMethod), navigation (Subscribed),
 * or snackbar (Failed / AlreadyActive).
 */
sealed interface SubscribeOutcome {
    /**
     * Phase 1 success — open Stripe PaymentSheet in setup mode using the
     * returned client_secret + ephemeral key, then call [MembershipViewModel.confirmSubscribe]
     * once PaymentSheet returns Completed.
     */
    data class NeedsPaymentMethod(
        val setupIntentClientSecret: String,
        val ephemeralKey: String,
        val customerId: String,
    ) : SubscribeOutcome

    /** Phase 2 success — membership created, navigate back to profile / show success snackbar. */
    data class Subscribed(val membershipId: String) : SubscribeOutcome

    /** Backend rejected — idempotency guard fired (user already had Active). */
    data object AlreadyActive : SubscribeOutcome

    /** Snackbar already shown by the VM. */
    data object Failed : SubscribeOutcome
}

/**
 * Owns the My Membership card state plus the subscribe / cancel flows.
 * Scoped per-screen — both the management card on Profile and the standalone
 * subscribe page can pull a ViewModel instance, but they share the underlying
 * [MembershipRepository] cache so they observe the same state.
 */
@HiltViewModel
class MembershipViewModel @Inject constructor(
    private val repository: MembershipRepository,
    private val snackbar: SnackbarController,
    @ApplicationContext private val appContext: Context,
) : ViewModel() {

    val current: StateFlow<GetMyMembershipResponse?> = repository.current
    val loading: StateFlow<Boolean> = repository.loading

    private val _submitting = MutableStateFlow(false)
    val submitting: StateFlow<Boolean> = _submitting.asStateFlow()

    private val _plans = MutableStateFlow<List<MembershipPlanDto>>(emptyList())
    /** Plan catalog driving the monthly/yearly switcher on the subscribe page. */
    val plans: StateFlow<List<MembershipPlanDto>> = _plans.asStateFlow()

    init {
        viewModelScope.launch { repository.refresh() }
        viewModelScope.launch { _plans.value = repository.getPlans() }
    }

    fun refresh() {
        viewModelScope.launch { repository.refresh() }
    }

    /**
     * Phase 1 of the subscribe flow — request a SetupIntent. The screen
     * passes the result to PaymentSheet and calls [confirmSubscribe] when
     * payment-method capture succeeds.
     */
    suspend fun startSubscribe(planCode: String): SubscribeOutcome {
        if (_submitting.value) return SubscribeOutcome.Failed
        _submitting.value = true
        try {
            val resp = repository.subscribePhase1(planCode)
            if (resp == null) {
                snackbar.showError(appContext.getString(R.string.error_generic_network))
                return SubscribeOutcome.Failed
            }
            // Phase 1 always returns a SetupIntent; if membershipId is non-empty
            // the user already had an active sub (defensive, backend rejects).
            if (resp.membershipId.isNotEmpty()) {
                return SubscribeOutcome.AlreadyActive
            }
            return SubscribeOutcome.NeedsPaymentMethod(
                setupIntentClientSecret = resp.setupIntentClientSecret,
                ephemeralKey = resp.ephemeralKey,
                customerId = resp.stripeCustomerId,
            )
        } finally {
            _submitting.value = false
        }
    }

    /**
     * Phase 2 — called after PaymentSheet returns Completed. Creates the
     * actual Stripe subscription + local UserMembership row.
     */
    suspend fun confirmSubscribe(planCode: String): SubscribeOutcome {
        _submitting.value = true
        try {
            val resp = repository.subscribePhase2(planCode)
            if (resp == null || resp.membershipId.isEmpty()) {
                snackbar.showError(appContext.getString(R.string.error_generic_network))
                return SubscribeOutcome.Failed
            }
            return SubscribeOutcome.Subscribed(resp.membershipId)
        } finally {
            _submitting.value = false
        }
    }

    /**
     * Cancel the user's active membership at period end. UI refreshes from
     * [current] which reflects the cancellation request flag.
     */
    fun cancel(onSuccess: (effectiveEndDate: String) -> Unit = {}) {
        if (_submitting.value) return
        _submitting.value = true
        viewModelScope.launch {
            try {
                val resp = repository.cancel()
                if (resp == null) {
                    snackbar.showError(appContext.getString(R.string.error_generic_network))
                    return@launch
                }
                onSuccess(resp.effectiveEndDate)
            } finally {
                _submitting.value = false
            }
        }
    }

    /**
     * Swap to a different plan (typically monthly→yearly upgrade). Stripe
     * prorates and charges/credits the user's default payment method on
     * the spot — no PaymentSheet round-trip needed.
     */
    fun swapPlan(newPlanCode: String, onSuccess: () -> Unit = {}) {
        if (_submitting.value) return
        _submitting.value = true
        viewModelScope.launch {
            try {
                val resp = repository.swapPlan(newPlanCode)
                if (resp == null) {
                    snackbar.showError(appContext.getString(R.string.error_generic_network))
                    return@launch
                }
                onSuccess()
            } finally {
                _submitting.value = false
            }
        }
    }
}

