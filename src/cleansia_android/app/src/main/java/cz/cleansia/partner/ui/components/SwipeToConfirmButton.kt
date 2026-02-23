package cz.cleansia.partner.ui.components

import android.view.HapticFeedbackConstants
import androidx.compose.animation.AnimatedContent
import androidx.compose.animation.animateColorAsState
import androidx.compose.animation.core.Animatable
import androidx.compose.animation.core.FastOutSlowInEasing
import androidx.compose.animation.core.LinearEasing
import androidx.compose.animation.core.RepeatMode
import androidx.compose.animation.core.animateFloat
import androidx.compose.animation.core.infiniteRepeatable
import androidx.compose.animation.core.rememberInfiniteTransition
import androidx.compose.animation.core.tween
import androidx.compose.animation.fadeIn
import androidx.compose.animation.fadeOut
import androidx.compose.animation.scaleIn
import androidx.compose.animation.togetherWith
import androidx.compose.foundation.background
import androidx.compose.foundation.gestures.Orientation
import androidx.compose.foundation.gestures.draggable
import androidx.compose.foundation.gestures.rememberDraggableState
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.offset
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.filled.KeyboardArrowRight
import androidx.compose.material.icons.filled.Check
import androidx.compose.material.icons.filled.Close
import androidx.compose.ui.graphics.vector.ImageVector
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.Icon
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableFloatStateOf
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.rememberCoroutineScope
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.alpha
import androidx.compose.ui.draw.clip
import androidx.compose.ui.draw.shadow
import androidx.compose.ui.graphics.Brush
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.StrokeCap
import cz.cleansia.partner.ui.theme.LocalDarkTheme
import androidx.compose.ui.layout.onSizeChanged
import androidx.compose.ui.platform.LocalDensity
import androidx.compose.ui.platform.LocalView
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.IntOffset
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import kotlinx.coroutines.delay
import kotlinx.coroutines.launch
import kotlin.math.roundToInt

/**
 * Result state for SwipeToConfirmButton
 */
enum class SwipeConfirmResult {
    IDLE,       // Initial state
    LOADING,    // Processing the action
    SUCCESS,    // Action completed successfully
    REJECTED    // Action was rejected (validation failed)
}

/**
 * iOS-inspired swipe-to-confirm button adapted to the Cleansia design language.
 *
 * A large white circular thumb sits on a primary-colored track, closely matching
 * the proportions of Apple's "slide to answer". The track uses the app's primary
 * color, text shimmers with a directional hint, and the thumb features the primary
 * color icon with a subtle shadow for depth.
 */
@Composable
fun SwipeToConfirmButton(
    text: String,
    onConfirm: suspend () -> Boolean,
    modifier: Modifier = Modifier,
    enabled: Boolean = true,
    compact: Boolean = false,
    icon: ImageVector = Icons.AutoMirrored.Filled.KeyboardArrowRight,
    validateBeforeConfirm: (() -> Boolean)? = null
) {
    val density = LocalDensity.current
    val coroutineScope = rememberCoroutineScope()
    val view = LocalView.current

    // iOS-like proportions: thumb nearly fills the track height
    val trackHeight = if (compact) 62.dp else 72.dp
    val thumbPadding = 4.dp
    val thumbSize = trackHeight - thumbPadding * 2  // thumb is track - 8dp
    val thumbSizePx = with(density) { thumbSize.toPx() }
    val thumbPaddingPx = with(density) { thumbPadding.toPx() }

    var containerWidthPx by remember { mutableFloatStateOf(0f) }
    val maxDragPx = remember(containerWidthPx) {
        (containerWidthPx - thumbSizePx - thumbPaddingPx * 2).coerceAtLeast(0f)
    }

    val offsetX = remember { Animatable(0f) }
    var resultState by remember { mutableStateOf(SwipeConfirmResult.IDLE) }

    val progress = if (maxDragPx > 0f) (offsetX.value / maxDragPx).coerceIn(0f, 1f) else 0f
    val confirmThreshold = 0.75f

    val trackShape = RoundedCornerShape(50)

    // State flags
    val isRejected = resultState == SwipeConfirmResult.REJECTED
    val isSuccess = resultState == SwipeConfirmResult.SUCCESS
    val isLoading = resultState == SwipeConfirmResult.LOADING
    val isProcessing = isLoading || isSuccess || isRejected

    // Theme colors — use richer primary in dark mode for better contrast
    val isDarkTheme = LocalDarkTheme.current
    val primaryColor = MaterialTheme.colorScheme.primary
    val errorColor = MaterialTheme.colorScheme.error

    // In dark theme, the primary (#7DD3FC) is too light/washed out for a track bg.
    // Use a deeper blue that still feels primary but has more saturation.
    val trackColor = if (isDarkTheme) Color(0xFF0284C7) else primaryColor
    val trackColorEnd = if (isDarkTheme) Color(0xFF0369A1) else primaryColor.copy(alpha = 0.9f)

    // Thumb: white in light mode, slightly warm white in dark mode for contrast
    val thumbColor = if (isDarkTheme) Color(0xFFF0F4F8) else Color.White
    // Chevron color inside thumb
    val chevronColor = if (isDarkTheme) Color(0xFF0284C7) else primaryColor

    // Track gradient
    val trackGradient = Brush.horizontalGradient(
        colors = listOf(trackColor, trackColorEnd)
    )

    val processingTrackColor by animateColorAsState(
        targetValue = when {
            isRejected -> errorColor
            else -> trackColor
        },
        animationSpec = tween(300),
        label = "processingTrackColor"
    )

    // Shimmer animation for text — iOS-like glinting effect
    val infiniteTransition = rememberInfiniteTransition(label = "swipeHint")
    val textShimmer by infiniteTransition.animateFloat(
        initialValue = 0.45f,
        targetValue = 0.85f,
        animationSpec = infiniteRepeatable(
            animation = tween(1500, easing = LinearEasing),
            repeatMode = RepeatMode.Reverse
        ),
        label = "textShimmer"
    )

    // Text fades as thumb approaches
    val textAlpha = (1f - progress * 2.2f).coerceIn(0f, 1f)

    Box(
        modifier = modifier
            .fillMaxWidth()
            .height(trackHeight)
            .alpha(if (enabled) 1f else 0.4f)
            .shadow(
                elevation = 3.dp,
                shape = trackShape,
                ambientColor = trackColor.copy(alpha = 0.15f),
                spotColor = trackColor.copy(alpha = 0.2f)
            )
            .clip(trackShape)
            .background(trackGradient)
            .onSizeChanged { containerWidthPx = it.width.toFloat() },
        contentAlignment = Alignment.CenterStart
    ) {
        // ── Processing state ──
        if (isProcessing) {
            Box(
                modifier = Modifier
                    .fillMaxSize()
                    .clip(trackShape)
                    .background(processingTrackColor)
            )

            Box(
                modifier = Modifier.fillMaxSize(),
                contentAlignment = Alignment.Center
            ) {
                AnimatedContent(
                    targetState = resultState,
                    transitionSpec = {
                        (fadeIn(tween(200)) + scaleIn(initialScale = 0.8f, animationSpec = tween(200)))
                            .togetherWith(fadeOut(tween(150)))
                    },
                    label = "statusIcon"
                ) { state ->
                    when (state) {
                        SwipeConfirmResult.LOADING -> {
                            CircularProgressIndicator(
                                modifier = Modifier.size(if (compact) 24.dp else 28.dp),
                                color = Color.White,
                                strokeWidth = 2.5.dp,
                                strokeCap = StrokeCap.Round
                            )
                        }
                        SwipeConfirmResult.SUCCESS -> {
                            Icon(
                                imageVector = Icons.Default.Check,
                                contentDescription = null,
                                tint = Color.White,
                                modifier = Modifier.size(if (compact) 28.dp else 32.dp)
                            )
                        }
                        SwipeConfirmResult.REJECTED -> {
                            Icon(
                                imageVector = Icons.Default.Close,
                                contentDescription = null,
                                tint = Color.White,
                                modifier = Modifier.size(if (compact) 28.dp else 32.dp)
                            )
                        }
                        else -> {}
                    }
                }
            }
        }

        // ── Idle state ──
        if (!isProcessing) {
            // Centered label — white text on primary track, with shimmer
            Box(
                modifier = Modifier
                    .fillMaxSize()
                    .alpha(textAlpha),
                contentAlignment = Alignment.Center
            ) {
                Text(
                    text = text,
                    fontSize = if (compact) 15.sp else 16.sp,
                    fontWeight = FontWeight.SemiBold,
                    color = Color.White.copy(alpha = textShimmer),
                    letterSpacing = 0.5.sp
                )
            }

            // Large draggable white thumb — iOS-style
            Box(
                modifier = Modifier
                    .padding(thumbPadding)
                    .offset { IntOffset(offsetX.value.roundToInt(), 0) }
                    .size(thumbSize)
                    .shadow(
                        elevation = 4.dp,
                        shape = CircleShape,
                        ambientColor = Color.Black.copy(alpha = 0.1f),
                        spotColor = Color.Black.copy(alpha = 0.15f)
                    )
                    .clip(CircleShape)
                    .background(thumbColor)
                    .draggable(
                        orientation = Orientation.Horizontal,
                        enabled = enabled,
                        state = rememberDraggableState { delta ->
                            coroutineScope.launch {
                                val newValue = (offsetX.value + delta).coerceIn(0f, maxDragPx)
                                offsetX.snapTo(newValue)
                            }
                        },
                        onDragStopped = {
                            if (progress >= confirmThreshold) {
                                coroutineScope.launch {
                                    offsetX.animateTo(
                                        maxDragPx,
                                        animationSpec = tween(200, easing = FastOutSlowInEasing)
                                    )

                                    val isValid = validateBeforeConfirm?.invoke() ?: true

                                    if (isValid) {
                                        resultState = SwipeConfirmResult.LOADING
                                        view.performHapticFeedback(HapticFeedbackConstants.LONG_PRESS)
                                        val success = onConfirm()
                                        if (success) {
                                            resultState = SwipeConfirmResult.SUCCESS
                                        } else {
                                            resultState = SwipeConfirmResult.REJECTED
                                            view.performHapticFeedback(HapticFeedbackConstants.LONG_PRESS)
                                            delay(1500)
                                            resultState = SwipeConfirmResult.IDLE
                                            offsetX.animateTo(
                                                0f,
                                                animationSpec = tween(400, easing = FastOutSlowInEasing)
                                            )
                                        }
                                    } else {
                                        resultState = SwipeConfirmResult.REJECTED
                                        view.performHapticFeedback(HapticFeedbackConstants.LONG_PRESS)
                                        delay(1500)
                                        resultState = SwipeConfirmResult.IDLE
                                        offsetX.animateTo(
                                            0f,
                                            animationSpec = tween(400, easing = FastOutSlowInEasing)
                                        )
                                    }
                                }
                            } else {
                                coroutineScope.launch {
                                    offsetX.animateTo(
                                        0f,
                                        animationSpec = tween(300, easing = FastOutSlowInEasing)
                                    )
                                }
                            }
                        }
                    ),
                contentAlignment = Alignment.Center
            ) {
                Icon(
                    imageVector = icon,
                    contentDescription = null,
                    tint = chevronColor,
                    modifier = Modifier.size(if (compact) 26.dp else 30.dp)
                )
            }
        }
    }
}
