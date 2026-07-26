package cz.cleansia.core.snackbar

import androidx.compose.runtime.Composable
import androidx.compose.runtime.DisposableEffect
import androidx.compose.ui.unit.Dp
import androidx.compose.ui.unit.dp
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow

/**
 * Shared state for "how far above the bottom should the snackbar sit on the
 * currently-visible screen". Screens that draw persistent bottom chrome
 * (bottom nav, sticky CTA, anchored sheet) call [SnackbarInsetScope] with a
 * dp value large enough to clear that chrome.
 *
 * A CompositionLocal doesn't work here because [GlobalSnackbarHost] lives at
 * the root of the composition — outside the NavHost / feature screens — so
 * locals provided further down don't flow UP to it. A shared flow does.
 *
 * The state is a **stack of owned entries**, not a single value. Several scopes
 * can be alive at once (a bottom-nav shell underneath a modal sheet is the
 * everyday case), so a scope going away must restore whatever is still active
 * underneath it rather than slamming the value back to the default.
 *
 * Default: 96.dp. That is not a "nothing on screen" figure — it is what the
 * modal sheets that push no inset of their own (cancel order, submit review,
 * promo code) rely on to keep an error snackbar clear of their own bottom CTA.
 * Lowering it is a separate change that has to give those sheets an explicit
 * scope first.
 */
object SnackbarInsetState {
    val DEFAULT_INSET: Dp = 96.dp

    /** Active scopes, oldest first. The last entry wins. */
    private val entries = mutableListOf<Pair<Long, Dp>>()
    private var nextToken = 0L

    private val _insetDp = MutableStateFlow(DEFAULT_INSET)
    val insetDp: StateFlow<Dp> = _insetDp.asStateFlow()

    /**
     * Registers an inset and returns the token that owns it. Every call must be
     * paired with exactly one [pop] of the returned token — [SnackbarInsetScope]
     * is the only caller precisely because `DisposableEffect` guarantees that
     * pairing. An unpaired push leaks an entry that pins the inset forever.
     */
    @Synchronized
    internal fun push(value: Dp): Long {
        val token = nextToken++
        entries += token to value
        publish()
        return token
    }

    /**
     * Removes the entry **by token, not by position**. This is the entire reason
     * the token exists and it must not be "simplified" to a `removeLast()`.
     *
     * During a NavHost transition Compose composes the incoming destination
     * *before* it disposes the outgoing one, so the outgoing screen's `onDispose`
     * routinely fires while the incoming screen's entry already sits on top of
     * the stack. A positional pop would then delete the live entry and leave the
     * dead one publishing — strictly worse than having no stack at all.
     *
     * Popping an unknown or already-popped token is a no-op, which keeps the
     * call idempotent under `DisposableEffect` re-entry.
     */
    @Synchronized
    internal fun pop(token: Long) {
        entries.removeAll { it.first == token }
        publish()
    }

    private fun publish() {
        _insetDp.value = entries.lastOrNull()?.second ?: DEFAULT_INSET
    }
}

/**
 * Apply while a screen with persistent bottom chrome is visible. On dispose the
 * inset falls back to whichever *other* scope is still active — or to
 * [SnackbarInsetState.DEFAULT_INSET] when none is — so closing a sheet that sat
 * on top of the bottom-nav shell restores the shell's inset instead of the
 * default.
 *
 * Usage:
 * ```
 * @Composable
 * fun MyScreen() {
 *     SnackbarInsetScope(88.dp)
 *     // ... normal screen content
 * }
 * ```
 */
@Composable
fun SnackbarInsetScope(bottomInset: Dp) {
    DisposableEffect(bottomInset) {
        val token = SnackbarInsetState.push(bottomInset)
        onDispose { SnackbarInsetState.pop(token) }
    }
}
