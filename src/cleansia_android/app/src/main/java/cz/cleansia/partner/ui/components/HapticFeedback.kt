package cz.cleansia.partner.ui.components

import android.view.HapticFeedbackConstants
import androidx.compose.foundation.clickable
import androidx.compose.foundation.interaction.MutableInteractionSource
import androidx.compose.material3.ripple
import androidx.compose.runtime.Composable
import androidx.compose.runtime.remember
import androidx.compose.ui.Modifier
import androidx.compose.ui.composed
import androidx.compose.ui.platform.LocalView
import androidx.compose.ui.semantics.Role

/**
 * Returns a lambda that performs a light haptic tick.
 * Use inside composable scope for manual haptic triggers
 * (e.g. on swipe threshold, timer events).
 */
@Composable
fun rememberHapticFeedback(): () -> Unit {
    val view = LocalView.current
    return remember(view) {
        { view.performHapticFeedback(HapticFeedbackConstants.CLOCK_TICK) }
    }
}

/**
 * Drop-in replacement for [Modifier.clickable] that adds a subtle
 * haptic tick on every tap. Includes ripple indication.
 */
fun Modifier.clickableWithHaptic(
    enabled: Boolean = true,
    role: Role? = null,
    onClick: () -> Unit
): Modifier = composed {
    val view = LocalView.current
    this.clickable(
        enabled = enabled,
        role = role,
        indication = ripple(bounded = true),
        interactionSource = remember { MutableInteractionSource() }
    ) {
        view.performHapticFeedback(HapticFeedbackConstants.CLOCK_TICK)
        onClick()
    }
}

/**
 * Clickable with haptic tick but no ripple indication.
 * Use for tabs, toggles, and subtle interactions.
 */
fun Modifier.clickableWithHapticNoRipple(
    enabled: Boolean = true,
    onClick: () -> Unit
): Modifier = composed {
    val view = LocalView.current
    this.clickable(
        enabled = enabled,
        indication = null,
        interactionSource = remember { MutableInteractionSource() }
    ) {
        view.performHapticFeedback(HapticFeedbackConstants.CLOCK_TICK)
        onClick()
    }
}
