package cz.cleansia.customer.ui.state

/**
 * Three-state machine for one-shot UI actions.
 *
 * A sealed type rather than a loading flag plus an error string, **so "submitting" and "errored" can no
 * longer co-exist** — the old shape allowed both-false-with-error and both-set transient races.
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
