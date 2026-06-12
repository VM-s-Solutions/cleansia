package cz.cleansia.core.ui.state

/**
 * Generic three-state machine for one-shot UI actions (revoke-device,
 * cancel-order, submit-review, …). The :core home of the type so both apps
 * share one definition; the customer app's `cz.cleansia.customer.ui.state.ActionState`
 * is the pre-existing identical copy, tracked for migration here.
 *
 * Structurally exclusive sealed type so "submitting" and "errored" can never
 * co-exist (the flag-bag `loading: Boolean` + `error: String?` shape allowed
 * both). Successful completion is intentionally NOT a state: success is an
 * effect that flips the action back to [Idle] and (when needed) emits on a
 * separate one-shot flow — keeping the same flow re-armable for a retry.
 */
sealed interface ActionState {
    /** No action in flight, no inline error to surface. The default + post-success state. */
    data object Idle : ActionState

    /** Action is in flight — drives spinners + disables submit buttons. */
    data object Submitting : ActionState

    /** Action failed. [message] is pre-localized — call sites render it directly. */
    data class Error(val message: String) : ActionState
}
