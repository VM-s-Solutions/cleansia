package cz.cleansia.core.snackbar

import androidx.compose.runtime.Composable
import androidx.compose.runtime.DisposableEffect
import androidx.compose.ui.unit.Dp
import androidx.compose.ui.unit.dp
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow

/**
 * How far above the bottom the snackbar sits on the current screen.
 *
 * A CompositionLocal cannot work here: the host lives at the root of the composition, outside the nav
 * graph, so locals provided further down do not flow UP to it.
 *
 * **The state is a STACK of owned entries, not a single value** — several scopes can be alive at once, so
 * a scope going away must restore whatever is still active underneath rather than resetting to the
 * default. -> /mobile-app/patterns#snackbar-inset
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
 * Apply while a screen with persistent bottom chrome is visible.
 *
 * **On dispose the inset falls back to whichever OTHER scope is still active**, not to the default — so
 * closing a sheet that sat on top of the bottom-nav shell restores the shell's inset.
 * -> /mobile-app/patterns#snackbar-inset
 */
@Composable
fun SnackbarInsetScope(bottomInset: Dp) {
    DisposableEffect(bottomInset) {
        val token = SnackbarInsetState.push(bottomInset)
        onDispose { SnackbarInsetState.pop(token) }
    }
}
