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
 * Default: 16.dp — enough to sit above the gesture bar with no other bottom UI.
 */
object SnackbarInsetState {
    val DEFAULT_INSET: Dp = 96.dp

    private val _insetDp = MutableStateFlow(DEFAULT_INSET)
    val insetDp: StateFlow<Dp> = _insetDp.asStateFlow()

    internal fun push(value: Dp) {
        _insetDp.value = value
    }

    internal fun reset() {
        _insetDp.value = DEFAULT_INSET
    }
}

/**
 * Apply while a screen with persistent bottom chrome is visible. On dispose,
 * the inset resets to default so the next screen starts with a clean value.
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
        SnackbarInsetState.push(bottomInset)
        onDispose { SnackbarInsetState.reset() }
    }
}
