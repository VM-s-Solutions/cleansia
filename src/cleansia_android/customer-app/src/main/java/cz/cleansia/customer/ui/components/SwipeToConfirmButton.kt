package cz.cleansia.customer.ui.components

import androidx.compose.animation.core.Animatable
import androidx.compose.animation.core.spring
import androidx.compose.animation.core.tween
import androidx.compose.foundation.background
import androidx.compose.foundation.gestures.detectDragGestures
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.BoxWithConstraints
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.offset
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.outlined.ArrowForward
import androidx.compose.material.icons.outlined.Check
import androidx.compose.material3.Icon
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.rememberCoroutineScope
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.hapticfeedback.HapticFeedbackType
import androidx.compose.ui.input.pointer.pointerInput
import androidx.compose.ui.platform.LocalDensity
import androidx.compose.ui.platform.LocalHapticFeedback
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.IntOffset
import androidx.compose.ui.unit.dp
import kotlinx.coroutines.launch
import kotlin.math.roundToInt

/**
 * Wolt/Bolt-style "slide to confirm" button. The user drags the circular thumb
 * from left to right; crossing ~90% of the track fires [onConfirmed].
 *
 * Disabled state renders the track greyed out; drag events are ignored.
 * After a successful confirm the thumb stays at the end with a check icon —
 * the parent is expected to navigate away immediately on success.
 *
 * On submission failure, the parent can flip [resetTrigger] (any new value) to
 * snap the thumb back to the start so the user can swipe again. Without this
 * the component locks at the end after the first confirm and is unrecoverable.
 */
@Composable
fun SwipeToConfirmButton(
    text: String,
    onConfirmed: () -> Unit,
    modifier: Modifier = Modifier,
    enabled: Boolean = true,
    resetTrigger: Any? = null,
) {
    val haptics = LocalHapticFeedback.current
    val density = LocalDensity.current
    val scope = rememberCoroutineScope()

    // Track height is fixed at 56dp; thumb is 48dp with 4dp inset.
    val trackHeight = 56.dp
    val thumbSize = 48.dp
    val thumbInset = 4.dp

    // Track uses the muted-dark variant of the blue gradient start in dark mode,
    // plain primary in light. Keeps the CTA readable without blaring.
    val trackColor = cz.cleansia.customer.ui.theme.BrandGradients.blue().first
    val thumbColor = MaterialTheme.colorScheme.surface
    val thumbGlyph = MaterialTheme.colorScheme.primary
    val labelColor = Color.White

    BoxWithConstraints(
        modifier = modifier
            .fillMaxWidth()
            .height(trackHeight)
            .clip(RoundedCornerShape(trackHeight / 2))
            .background(if (enabled) trackColor else trackColor.copy(alpha = 0.4f)),
    ) {
        val maxOffsetPx = with(density) { (maxWidth - thumbSize - thumbInset * 2).toPx() }
        val thumbOffset = remember { Animatable(0f) }
        var confirmed by remember { mutableStateOf(false) }

        // Reset thumb when the enabled state flips back.
        LaunchedEffect(enabled) {
            if (!enabled && !confirmed) thumbOffset.snapTo(0f)
        }

        // Parent-driven reset: when [resetTrigger] changes, animate the thumb
        // back to the start and clear the confirmed flag so the next swipe
        // re-fires [onConfirmed]. Used by booking when submission fails — the
        // user needs to be able to retry without losing the slide affordance.
        LaunchedEffect(resetTrigger) {
            if (resetTrigger != null) {
                confirmed = false
                thumbOffset.animateTo(0f, tween(220))
            }
        }

        // Label — fades out as the thumb moves.
        val progress = if (maxOffsetPx > 0f) thumbOffset.value / maxOffsetPx else 0f
        Text(
            text = text,
            style = MaterialTheme.typography.titleMedium.copy(fontWeight = FontWeight.SemiBold),
            color = labelColor.copy(alpha = (1f - progress).coerceIn(0f, 1f)),
            modifier = Modifier.align(Alignment.Center),
        )

        // Thumb
        Box(
            modifier = Modifier
                .align(Alignment.CenterStart)
                .padding(horizontal = thumbInset)
                .offset { IntOffset(thumbOffset.value.roundToInt(), 0) }
                .size(thumbSize)
                .clip(CircleShape)
                .background(thumbColor)
                .pointerInput(enabled, maxOffsetPx) {
                    if (!enabled) return@pointerInput
                    detectDragGestures(
                        onDragEnd = {
                            if (confirmed) return@detectDragGestures
                            scope.launch {
                                if (thumbOffset.value >= maxOffsetPx * 0.9f) {
                                    // Snap to end and fire callback.
                                    thumbOffset.animateTo(
                                        maxOffsetPx,
                                        spring(dampingRatio = 0.7f, stiffness = 400f),
                                    )
                                    confirmed = true
                                    haptics.performHapticFeedback(HapticFeedbackType.LongPress)
                                    onConfirmed()
                                } else {
                                    thumbOffset.animateTo(0f, tween(220))
                                }
                            }
                        },
                        onDrag = { change, drag ->
                            if (confirmed) return@detectDragGestures
                            change.consume()
                            scope.launch {
                                val next = (thumbOffset.value + drag.x).coerceIn(0f, maxOffsetPx)
                                thumbOffset.snapTo(next)
                            }
                        },
                    )
                },
            contentAlignment = Alignment.Center,
        ) {
            Icon(
                imageVector = if (confirmed) Icons.Outlined.Check else Icons.AutoMirrored.Outlined.ArrowForward,
                contentDescription = null,
                tint = thumbGlyph,
            )
        }
    }
}
