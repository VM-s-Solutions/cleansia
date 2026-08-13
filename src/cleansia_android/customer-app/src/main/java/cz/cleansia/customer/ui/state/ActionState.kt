package cz.cleansia.customer.ui.state

/**
 * Wave 4 — generic three-state machine for one-shot UI actions
 * (cancel-order, submit-review, download-receipt, booking-submit, …).
 *
 * Promotes the historical `loading: Boolean` + `error: String?` pair into a
 * structurally exclusive sealed type so "submitting" and "errored" can no
 * longer co-exist (the old shape allowed both flags false-with-error or
 * both-set transient races). Consumers pattern-match on the variant rather
 * than reading two flags.
 *
 * Successful completion is intentionally NOT a state here: success is an
 * effect that flips the action back to [Idle] and (when needed) emits on a
 * separate one-shot channel — keeping `ActionState` cycle-friendly so the
 * same flow can be re-armed for a retry without ever holding stale data.
 */
sealed interface ActionState {
    /** No action in flight, no inline error to surface. The default + post-success state. */
    data object Idle : ActionState

    /** Action is in flight — drives spinners + disables submit buttons. */
    data object Submitting : ActionState

    /**
     * Action failed. [message] is non-null and pre-localized — call sites
     * render it directly. Repository-level snackbars often fire alongside
     * (per existing repo conventions); the inline copy here is the "sheet
     * stayed open, retry available" hint, not a full error description.
     */
    data class Error(val message: String) : ActionState
}
