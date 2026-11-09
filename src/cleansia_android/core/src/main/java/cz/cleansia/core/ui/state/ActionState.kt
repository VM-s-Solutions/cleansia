package cz.cleansia.core.ui.state

/**
 * Three-state machine for one-shot UI actions — the `:core` home of the type so both apps share one
 * definition.
 */
sealed interface ActionState {
    /** No action in flight, no inline error to surface. The default + post-success state. */
    data object Idle : ActionState

    /** Action is in flight — drives spinners + disables submit buttons. */
    data object Submitting : ActionState

    /** Action failed. [message] is pre-localized — call sites render it directly. */
    data class Error(val message: String) : ActionState
}
