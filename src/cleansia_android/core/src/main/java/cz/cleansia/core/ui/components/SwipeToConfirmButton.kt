package cz.cleansia.core.ui.components

import androidx.compose.animation.core.Animatable
import androidx.compose.animation.core.spring
import androidx.compose.animation.core.tween
import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.gestures.detectDragGestures
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.BoxWithConstraints
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.heightIn
import androidx.compose.foundation.layout.offset
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.outlined.ArrowForward
import androidx.compose.material.icons.outlined.Check
import androidx.compose.material.icons.outlined.Lock
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.ColorScheme
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
import androidx.compose.ui.graphics.luminance
import androidx.compose.ui.hapticfeedback.HapticFeedbackType
import androidx.compose.ui.input.pointer.pointerInput
import androidx.compose.ui.platform.LocalDensity
import androidx.compose.ui.platform.LocalHapticFeedback
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.LineBreak
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.unit.Dp
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
 *
 * A caller that tracks its request passes [isBusy] instead: the thumb holds at the end with a spinner, [busyText]
 * shows if given, and when [isBusy] turns false the thumb springs back, ready again. [thumbLanes] keeps the label
 * clear of the thumb at both ends, and [maxLines] above 1 lets it take that many lines and grow the track; without
 * either the label is laid out across the whole track, as it always was. On that laned track a [detail] line, what the
 * slide records, stands under the label until the slide is busy.
 */
@Composable
fun SwipeToConfirmButton(
    text: String,
    onConfirmed: () -> Unit,
    modifier: Modifier = Modifier,
    enabled: Boolean = true,
    resetTrigger: Any? = null,
    isBusy: Boolean = false,
    busyText: String? = null,
    maxLines: Int = 1,
    thumbLanes: Boolean = false,
    detail: String? = null,
) {
    val haptics = LocalHapticFeedback.current
    val density = LocalDensity.current
    val scope = rememberCoroutineScope()

    val trackColor = swipeTrackColor(MaterialTheme.colorScheme)
    val thumbColor = MaterialTheme.colorScheme.surface
    val thumbGlyphInk = MaterialTheme.colorScheme.primary
    val laned = slideHasLanes(thumbLanes, maxLines)

    BoxWithConstraints(
        modifier = modifier
            .fillMaxWidth()
            .then(if (laned) Modifier.heightIn(min = TrackHeight) else Modifier.height(TrackHeight))
            .clip(if (laned) CircleShape else RoundedCornerShape(TrackHeight / 2))
            .background(if (enabled) trackColor else trackColor.copy(alpha = 0.4f)),
    ) {
        val maxOffsetPx = with(density) { (maxWidth - ThumbSize - ThumbInset * 2).toPx() }
        val thumbOffset = remember { Animatable(0f) }
        var confirmed by remember { mutableStateOf(false) }
        var wasBusy by remember { mutableStateOf(false) }

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

        LaunchedEffect(isBusy) {
            val move = thumbMoveOnBusy(wasBusy, isBusy)
            wasBusy = isBusy
            when (move) {
                ThumbMove.ToEnd -> thumbOffset.animateTo(maxOffsetPx, ConfirmSpring)
                ThumbMove.ToStart -> {
                    thumbOffset.animateTo(0f, tween(220))
                    confirmed = false
                }
                ThumbMove.Stay -> Unit
            }
        }

        // Label — fades out as the thumb moves.
        val progress = if (maxOffsetPx > 0f) thumbOffset.value / maxOffsetPx else 0f
        val words = slideWords(text, busyText, isBusy)
        if (words != null) {
            val alpha = if (isBusy) 1f else (1f - progress).coerceIn(0f, 1f)
            if (laned && detail != null && !isBusy) {
                Column(
                    modifier = Modifier
                        .align(Alignment.Center)
                        .padding(horizontal = SlideLane, vertical = LanedLabelPadding),
                    horizontalAlignment = Alignment.CenterHorizontally,
                ) {
                    Text(
                        text = words,
                        style = MaterialTheme.typography.titleMedium.copy(fontWeight = FontWeight.SemiBold, lineBreak = LineBreak.Heading),
                        color = SwipeLabelInk.copy(alpha = alpha),
                        textAlign = TextAlign.Center,
                        maxLines = maxLines.coerceAtLeast(1),
                        overflow = TextOverflow.Ellipsis,
                    )
                    Text(
                        text = detail,
                        style = MaterialTheme.typography.bodySmall,
                        color = SwipeLabelInk.copy(alpha = alpha),
                        textAlign = TextAlign.Center,
                    )
                }
            } else if (laned) {
                Text(
                    text = words,
                    style = MaterialTheme.typography.titleMedium.copy(fontWeight = FontWeight.SemiBold, lineBreak = LineBreak.Heading),
                    color = SwipeLabelInk.copy(alpha = alpha),
                    textAlign = TextAlign.Center,
                    maxLines = maxLines.coerceAtLeast(1),
                    overflow = TextOverflow.Ellipsis,
                    modifier = Modifier
                        .align(Alignment.Center)
                        .padding(horizontal = SlideLane, vertical = LanedLabelPadding),
                )
            } else {
                Text(
                    text = words,
                    style = MaterialTheme.typography.titleMedium.copy(fontWeight = FontWeight.SemiBold),
                    color = SwipeLabelInk.copy(alpha = alpha),
                    modifier = Modifier.align(Alignment.Center),
                )
            }
        }

        // Thumb
        Box(
            modifier = Modifier
                .align(Alignment.CenterStart)
                .padding(horizontal = ThumbInset)
                .offset { IntOffset(thumbOffset.value.roundToInt(), 0) }
                .size(ThumbSize)
                .clip(CircleShape)
                .background(thumbColor)
                .pointerInput(enabled, isBusy, maxOffsetPx) {
                    if (!slideAcceptsDrag(enabled, isBusy, confirmed = false)) return@pointerInput
                    detectDragGestures(
                        onDragEnd = {
                            if (!slideAcceptsDrag(enabled, isBusy, confirmed)) return@detectDragGestures
                            scope.launch {
                                if (slideConfirms(thumbOffset.value, maxOffsetPx)) {
                                    // Snap to end and fire callback.
                                    thumbOffset.animateTo(maxOffsetPx, ConfirmSpring)
                                    confirmed = true
                                    haptics.performHapticFeedback(HapticFeedbackType.LongPress)
                                    onConfirmed()
                                } else {
                                    thumbOffset.animateTo(0f, tween(220))
                                }
                            }
                        },
                        onDrag = { change, drag ->
                            if (!slideAcceptsDrag(enabled, isBusy, confirmed)) return@detectDragGestures
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
            when (thumbGlyph(isBusy, confirmed)) {
                ThumbGlyph.Spinner -> CircularProgressIndicator(
                    modifier = Modifier.size(22.dp),
                    color = thumbGlyphInk,
                    strokeWidth = 2.5.dp,
                )
                ThumbGlyph.Check -> Icon(imageVector = Icons.Outlined.Check, contentDescription = null, tint = thumbGlyphInk)
                ThumbGlyph.Arrow -> Icon(
                    imageVector = Icons.AutoMirrored.Outlined.ArrowForward,
                    contentDescription = null,
                    tint = thumbGlyphInk,
                )
            }
        }
    }
}

/**
 * The slide that cannot be used yet: the track in the well colour, the thumb an outlined lock, and the [reason] with
 * its [detail] centred between the thumb's lanes, the track growing when they wrap. Nothing drags it.
 */
@Composable
fun LockedSwipeToConfirm(reason: String, detail: String? = null, modifier: Modifier = Modifier) {
    Box(
        modifier = modifier
            .fillMaxWidth()
            .heightIn(min = TrackHeight)
            .clip(CircleShape)
            .background(MaterialTheme.colorScheme.surfaceVariant),
        contentAlignment = Alignment.Center,
    ) {
        Column(
            modifier = Modifier.padding(horizontal = SlideLane, vertical = LanedLabelPadding),
            horizontalAlignment = Alignment.CenterHorizontally,
        ) {
            Text(
                text = reason,
                style = MaterialTheme.typography.labelLarge.copy(lineBreak = LineBreak.Heading),
                color = MaterialTheme.colorScheme.onSurface,
                textAlign = TextAlign.Center,
            )
            if (detail != null) {
                Text(
                    text = detail,
                    style = MaterialTheme.typography.bodySmall,
                    color = MaterialTheme.colorScheme.onSurfaceVariant,
                    textAlign = TextAlign.Center,
                )
            }
        }
        Box(
            modifier = Modifier
                .align(Alignment.CenterStart)
                .padding(horizontal = ThumbInset)
                .size(ThumbSize)
                .background(MaterialTheme.colorScheme.surface, CircleShape)
                .border(1.5.dp, MaterialTheme.colorScheme.outline, CircleShape),
            contentAlignment = Alignment.Center,
        ) {
            Icon(imageVector = Icons.Outlined.Lock, contentDescription = null, tint = MaterialTheme.colorScheme.onSurfaceVariant)
        }
    }
}

internal enum class ThumbMove { ToEnd, ToStart, Stay }

internal enum class ThumbGlyph { Arrow, Spinner, Check }

private val TrackHeight = 56.dp
private val ThumbSize = 48.dp
private val ThumbInset = 4.dp
private val LanedLabelPadding = 6.dp
private val ConfirmSpring = spring<Float>(dampingRatio = 0.7f, stiffness = 400f)

/** What each end of the track keeps clear of the label: the thumb, its inset and a gap. */
internal val SlideLane: Dp = ThumbInset + ThumbSize + 8.dp

internal val SwipeTrackLight = Color(0xFF0284C7) // sky-600
internal val SwipeTrackDark = Color(0xFF075985) // sky-800
internal val SwipeLabelInk = Color.White

/** The track colour of the scheme in force, read from `surface`'s luminance as [primaryText][cz.cleansia.core.ui.theme.primaryText] is. */
internal fun swipeTrackColor(scheme: ColorScheme): Color =
    if (scheme.surface.luminance() < 0.5f) SwipeTrackDark else SwipeTrackLight

/** Busy holds the thumb at the end; the end of busy sends it back to the start, ready again. */
internal fun thumbMoveOnBusy(wasBusy: Boolean, isBusy: Boolean): ThumbMove = when {
    isBusy -> ThumbMove.ToEnd
    wasBusy -> ThumbMove.ToStart
    else -> ThumbMove.Stay
}

internal fun thumbGlyph(isBusy: Boolean, confirmed: Boolean): ThumbGlyph = when {
    isBusy -> ThumbGlyph.Spinner
    confirmed -> ThumbGlyph.Check
    else -> ThumbGlyph.Arrow
}

internal fun slideWords(text: String, busyText: String?, isBusy: Boolean): String? = if (isBusy) busyText else text

internal fun slideAcceptsDrag(enabled: Boolean, isBusy: Boolean, confirmed: Boolean): Boolean = enabled && !isBusy && !confirmed

internal fun slideConfirms(offsetPx: Float, maxOffsetPx: Float): Boolean = offsetPx >= maxOffsetPx * 0.9f

internal fun slideHasLanes(thumbLanes: Boolean, maxLines: Int): Boolean = thumbLanes || maxLines > 1
