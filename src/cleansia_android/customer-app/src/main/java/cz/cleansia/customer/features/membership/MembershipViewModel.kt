package cz.cleansia.customer.features.membership

import android.content.Context
import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import cz.cleansia.customer.R
import cz.cleansia.core.network.ApiError
import cz.cleansia.core.network.ApiResult
import cz.cleansia.customer.core.memberships.GetMyMembershipResponse
import cz.cleansia.customer.core.memberships.MembershipPlanDto
import cz.cleansia.customer.core.memberships.MembershipRepository
import cz.cleansia.customer.ui.state.ActionState
import cz.cleansia.core.snackbar.SnackbarController
import dagger.hilt.android.lifecycle.HiltViewModel
import dagger.hilt.android.qualifiers.ApplicationContext
import java.util.UUID
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

    private val _submitState = MutableStateFlow<ActionState>(ActionState.Idle)
    val submitState: StateFlow<ActionState> = _submitState.asStateFlow()

    private val _plans = MutableStateFlow<List<MembershipPlanDto>>(emptyList())
    /** Plan catalog driving the monthly/yearly switcher on the subscribe page. */
    val plans: StateFlow<List<MembershipPlanDto>> = _plans.asStateFlow()

    /**
     * Client idempotency token for the CURRENT logical subscribe attempt.
     * Generated ONCE in [startSubscribe] (Phase-1) and
     * replayed UNCHANGED on every [confirmSubscribe] (Phase-2) so the backend
     * collapses retried/double-tapped confirms — PaymentSheet returning
     * Completed twice, a network retry, or a double-tap surviving [submitState]
     * — onto a SINGLE Stripe subscription instead of double-charging. A fresh
     * subscribe attempt (a new [startSubscribe], e.g. after a cancellation)
     * generates a NEW token so a genuine re-subscribe is not blocked.
     */
    private var subscribeIdempotencyToken: String? = null

    init {
        // Background loads stay silent on failure (the repo used to swallow-and-log);
        // the management card just keeps rendering the cached/empty state.
        viewModelScope.launch { repository.refresh() }
        viewModelScope.launch { repository.getPlans().onSuccess { _plans.value = it } }
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
        if (_submitState.value is ActionState.Submitting) return SubscribeOutcome.Failed
        _submitState.value = ActionState.Submitting
        // New logical subscribe attempt → mint a fresh idempotency token. It is
        // held until the next startSubscribe and replayed on every confirm
        // (Phase-2) retry for THIS attempt.
        subscribeIdempotencyToken = UUID.randomUUID().toString()
        try {
            val resp = repository.subscribePhase1(planCode)
                .showErrorUnlessNetwork().getOrNull()
                ?: return SubscribeOutcome.Failed
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
            _submitState.value = ActionState.Idle
        }
    }

    /**
     * Phase 2 — called after PaymentSheet returns Completed. Creates the
     * actual Stripe subscription + local UserMembership row.
     */
    suspend fun confirmSubscribe(planCode: String): SubscribeOutcome {
        _submitState.value = ActionState.Submitting
        try {
            // Replay the SAME token minted at Phase-1. Do NOT regenerate here —
            // that is what lets the backend collapse a double-tap / network retry /
            // a second PaymentSheet Completed onto one Stripe subscription. Fall
            // back to a fresh token only if confirm is somehow reached without a
            // prior startSubscribe (defensive — should not happen in the UI flow).
            val token = subscribeIdempotencyToken
                ?: UUID.randomUUID().toString().also { subscribeIdempotencyToken = it }
            val resp = repository.subscribePhase2(planCode, token)
                .showErrorUnlessNetwork().getOrNull()
            if (resp == null || resp.membershipId.isEmpty()) {
                // A null result already snackbarred via showErrorUnlessNetwork;
                // a 2xx with an empty membershipId is a malformed success — keep
                // the original generic message so the user still sees a failure.
                if (resp != null) snackbar.showError(appContext.getString(R.string.error_generic_network))
                return SubscribeOutcome.Failed
            }
            return SubscribeOutcome.Subscribed(resp.membershipId)
        } finally {
            _submitState.value = ActionState.Idle
        }
    }

    /**
     * Cancel the user's active membership at period end. UI refreshes from
     * [current] which reflects the cancellation request flag.
     */
    fun cancel(onSuccess: (effectiveEndDate: String) -> Unit = {}) {
        if (_submitState.value is ActionState.Submitting) return
        _submitState.value = ActionState.Submitting
        viewModelScope.launch {
            try {
                val resp = repository.cancel().showErrorUnlessNetwork().getOrNull()
                    ?: return@launch
                onSuccess(resp.effectiveEndDate)
            } finally {
                _submitState.value = ActionState.Idle
            }
        }
    }

    /**
     * Swap to a different plan (typically monthly→yearly upgrade). Stripe
     * prorates and charges/credits the user's default payment method on
     * the spot — no PaymentSheet round-trip needed.
     */
    fun swapPlan(newPlanCode: String, onSuccess: () -> Unit = {}) {
        if (_submitState.value is ActionState.Submitting) return
        _submitState.value = ActionState.Submitting
        viewModelScope.launch {
            try {
                repository.swapPlan(newPlanCode).showErrorUnlessNetwork().getOrNull()
                    ?: return@launch
                onSuccess()
            } finally {
                _submitState.value = ActionState.Idle
            }
        }
    }

    private fun <T> ApiResult<T>.showErrorUnlessNetwork(): ApiResult<T> = onError { error ->
        if (error !is ApiError.Network) snackbar.showError(error.getUserMessage())
    }
}
