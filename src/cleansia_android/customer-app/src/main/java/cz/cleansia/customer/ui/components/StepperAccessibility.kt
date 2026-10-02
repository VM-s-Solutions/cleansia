package cz.cleansia.customer.ui.components

import android.view.HapticFeedbackConstants
import androidx.compose.runtime.Composable
import androidx.compose.runtime.remember
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.LocalView
import androidx.compose.ui.semantics.ProgressBarRangeInfo
import androidx.compose.ui.semantics.clearAndSetSemantics
import androidx.compose.ui.semantics.contentDescription
import androidx.compose.ui.semantics.progressBarRangeInfo
import androidx.compose.ui.semantics.setProgress
import androidx.compose.ui.semantics.stateDescription
import kotlin.math.roundToInt

/**
 * The selection tick a size stepper plays on each step it takes, as iOS `PropertyStepper` does
 * (ADR-0018 D2). A bound disables the step, so a refused one plays nothing; the phone's touch
 * feedback setting turns it off.
 */
@Composable
internal fun rememberStepperTick(): () -> Unit {
    val view = LocalView.current
    return remember(view) { { view.performHapticFeedback(HapticFeedbackConstants.CLOCK_TICK) } }
}

/**
 * One TalkBack node for a −/+ stepper, read as [name] and [valueText] and adjusted like a slider
 * (swipe up or down, or the volume keys), instead of two unnamed glyph buttons around a bare number:
 * iOS `PropertyStepper`'s adjustable element. The range's steps are set so one adjustment moves the
 * value by one, and [onChange] receives the new value only when it moved.
 */
internal fun Modifier.adjustableStepper(
    name: String,
    valueText: String,
    value: Int,
    range: IntRange,
    onChange: (Int) -> Unit,
): Modifier = clearAndSetSemantics {
    contentDescription = name
    stateDescription = valueText
    progressBarRangeInfo = ProgressBarRangeInfo(
        current = value.toFloat(),
        range = range.first.toFloat()..range.last.toFloat(),
        steps = (range.last - range.first - 1).coerceAtLeast(0),
    )
    setProgress { target ->
        val next = target.roundToInt().coerceIn(range)
        if (next == value) return@setProgress false
        onChange(next)
        true
    }
}
