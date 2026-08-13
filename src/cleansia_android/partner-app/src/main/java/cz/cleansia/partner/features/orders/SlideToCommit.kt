package cz.cleansia.partner.features.orders

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
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.outlined.ArrowForward
import androidx.compose.material3.CircularProgressIndicator
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
import androidx.compose.ui.input.pointer.pointerInput
import androidx.compose.ui.platform.LocalDensity
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.IntOffset
import androidx.compose.ui.unit.dp
import kotlinx.coroutines.launch
import kotlin.math.roundToInt

/**
 * Slide-to-commit primary CTA. The cleaner drags the thumb across the
 * track to commit; releasing past 90% triggers [onCommit], releasing
 * earlier snaps the thumb back. The deliberate gesture (vs a one-tap
 * button) is the point — taking an offer or starting a multi-hour
 * cleaning is a big commitment and we don't want mis-taps.
 *
 * While the request is in flight ([isBusy] = true) the thumb is locked at
 * the right and shows a spinner, the label flips to [busyLabel], and
 * drag gestures are ignored. If the request fails (parent flips [isBusy]
 * back to false without the card disappearing) the thumb animates back
 * to 0 so the cleaner can try again.
 *
 * Originally lived inline in `OrdersListScreen.kt` as `SlideToTake`;
 * extracted so the OrderDetail screen can reuse it for both Take and
 * Start (per the redesign — Take and Start are both "commit" actions
 * with the same UX weight).
 */
@Composable
fun SlideToCommit(
    idleLabel: String,
    busyLabel: String,
    onCommit: () -> Unit,
    modifier: Modifier = Modifier,
    isBusy: Boolean = false,
) {
    val density = LocalDensity.current
    val scope = rememberCoroutineScope()

    val trackHeight = 56.dp
    val thumbSize = 48.dp
    val thumbInset = 4.dp

    BoxWithConstraints(
        modifier = modifier
            .fillMaxWidth()
            .height(trackHeight)
            .clip(RoundedCornerShape(trackHeight / 2))
            .background(MaterialTheme.colorScheme.primary),
    ) {
        val maxOffsetPx = with(density) { (maxWidth - thumbSize - thumbInset * 2).toPx() }
        val thumbOffset = remember { Animatable(0f) }
        var consumed by remember { mutableStateOf(false) }

        // When the parent flips `isBusy`, drive the thumb position
        // explicitly so the UI matches the request lifecycle:
        //  - true  → lock thumb at the end (spinner replaces arrow)
        //  - false (after having been true) → spring back to 0 and
        //    re-arm so the cleaner can retry on failure
        LaunchedEffect(isBusy) {
            if (isBusy) {
                thumbOffset.animateTo(
                    maxOffsetPx,
                    spring(dampingRatio = 0.7f, stiffness = 400f),
                )
            } else if (consumed) {
                thumbOffset.animateTo(0f, tween(220))
                consumed = false
            }
        }

        val progress = if (maxOffsetPx > 0f) thumbOffset.value / maxOffsetPx else 0f
        // Idle label fades as the thumb crosses the track. While busy
        // the spinner inside the thumb is the only indicator — no
        // text needed (and visually cleaner without a duplicate
        // "Completing…" line next to a spinning thumb).
        if (!isBusy) {
            Text(
                text = idleLabel,
                style = MaterialTheme.typography.labelLarge,
                color = MaterialTheme.colorScheme.onPrimary.copy(
                    alpha = (1f - progress).coerceIn(0f, 1f),
                ),
                fontWeight = FontWeight.SemiBold,
                modifier = Modifier.align(Alignment.Center),
            )
        }

        Box(
            modifier = Modifier
                .align(Alignment.CenterStart)
                .padding(horizontal = thumbInset)
                .offset { IntOffset(thumbOffset.value.roundToInt(), 0) }
                .size(thumbSize)
                .clip(RoundedCornerShape(thumbSize / 2))
                .background(MaterialTheme.colorScheme.onPrimary)
                .pointerInput(maxOffsetPx, isBusy) {
                    if (isBusy) return@pointerInput
                    detectDragGestures(
                        onDragEnd = {
                            if (consumed) return@detectDragGestures
                            scope.launch {
                                if (thumbOffset.value >= maxOffsetPx * 0.9f) {
                                    thumbOffset.animateTo(
                                        maxOffsetPx,
                                        spring(dampingRatio = 0.7f, stiffness = 400f),
                                    )
                                    consumed = true
                                    onCommit()
                                } else {
                                    thumbOffset.animateTo(0f, tween(220))
                                }
                            }
                        },
                        onDrag = { change, drag ->
                            if (consumed) return@detectDragGestures
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
            if (isBusy) {
                // Brand-blue spinner on the white thumb — readable
                // against the white background, matches the idle-state
                // arrow icon's tint so the visual identity persists
                // through the state change. The busy label was dropped
                // entirely; this spinner is now the only indicator.
                CircularProgressIndicator(
                    modifier = Modifier.size(22.dp),
                    color = MaterialTheme.colorScheme.primary,
                    strokeWidth = 2.5.dp,
                )
            } else {
                Icon(
                    imageVector = Icons.AutoMirrored.Outlined.ArrowForward,
                    contentDescription = null,
                    tint = MaterialTheme.colorScheme.primary,
                )
            }
        }
    }
}

