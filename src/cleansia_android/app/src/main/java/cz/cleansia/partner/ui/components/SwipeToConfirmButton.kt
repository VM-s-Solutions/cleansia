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
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxHeight
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.offset
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.filled.KeyboardArrowRight
import androidx.compose.material.icons.filled.Check
import androidx.compose.material.icons.filled.Close
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.Icon
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
import androidx.compose.ui.draw.clip
import androidx.compose.ui.draw.shadow
import androidx.compose.ui.geometry.Offset
import androidx.compose.ui.geometry.Rect
import androidx.compose.ui.geometry.Size
import androidx.compose.ui.graphics.Brush
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.Outline
import androidx.compose.ui.graphics.Shape
import androidx.compose.ui.graphics.StrokeCap
import androidx.compose.ui.layout.onSizeChanged
import androidx.compose.ui.platform.LocalDensity
import androidx.compose.ui.platform.LocalView
import androidx.compose.ui.text.TextStyle
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.Density
import androidx.compose.ui.unit.IntOffset
import androidx.compose.ui.unit.LayoutDirection
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import cz.cleansia.partner.ui.theme.Primary
import cz.cleansia.partner.ui.theme.PrimaryContainer
import cz.cleansia.partner.ui.theme.OnPrimaryContainer
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
 * Swipe to confirm button with validation support.
 *
 * @param text The text to display on the button
 * @param onConfirm Called when the user completes the swipe. Should return true if successful, false if validation failed.
 * @param modifier Modifier for the button
 * @param enabled Whether the button is enabled
 * @param validateBeforeConfirm Optional validation function called before confirming. If returns false, shows rejection state.
 */
@Composable
fun SwipeToConfirmButton(
    text: String,
    onConfirm: () -> Unit,
    modifier: Modifier = Modifier,
    enabled: Boolean = true,
    validateBeforeConfirm: (() -> Boolean)? = null
) {
    val density = LocalDensity.current
    val coroutineScope = rememberCoroutineScope()
    val view = LocalView.current

    // Thumb dimensions
    val thumbHeight = 52.dp
    val thumbWidth = 64.dp
    val thumbWidthPx = with(density) { thumbWidth.toPx() }
    val horizontalPadding = 4.dp
    val paddingPx = with(density) { horizontalPadding.toPx() }

    var containerWidthPx by remember { mutableFloatStateOf(0f) }
    val maxDragPx = remember(containerWidthPx) {
        (containerWidthPx - thumbWidthPx - paddingPx * 2).coerceAtLeast(0f)
    }

    val offsetX = remember { Animatable(0f) }
    var resultState by remember { mutableStateOf(SwipeConfirmResult.IDLE) }

    val progress = if (maxDragPx > 0f) (offsetX.value / maxDragPx).coerceIn(0f, 1f) else 0f
    val confirmThreshold = 0.85f

    // Track shape - pill/stadium shape for more rounded look
    val trackShape = RoundedCornerShape(30.dp)
    // Thumb shape - pill shape matching the track
    val thumbShape = RoundedCornerShape(26.dp)

    // Colors based on result state
    val isRejected = resultState == SwipeConfirmResult.REJECTED
    val isSuccess = resultState == SwipeConfirmResult.SUCCESS
    val isLoading = resultState == SwipeConfirmResult.LOADING
    val isProcessing = isLoading || isSuccess || isRejected

    // Darker rejected colors for better visibility
    val rejectedSolid = Color(0xFFB91C1C)       // Dark red
    val rejectedTrack = Color(0xFFFCA5A5)        // Muted light red track
    val rejectedText = Color(0xFF7F1D1D)         // Very dark red text

    // Animated colors for smooth transitions
    val solidColor by animateColorAsState(
        targetValue = when {
            isRejected -> rejectedSolid
            else -> Primary
        },
        animationSpec = tween(300),
        label = "solidColor"
    )

    val trackBackgroundColor by animateColorAsState(
        targetValue = when {
            isRejected -> rejectedTrack
            else -> PrimaryContainer
        },
        animationSpec = tween(300),
        label = "trackBackground"
    )

    val textColor by animateColorAsState(
        targetValue = when {
            isRejected -> rejectedText
            else -> OnPrimaryContainer
        },
        animationSpec = tween(300),
        label = "textColor"
    )

    // Shimmer animation for text
    val infiniteTransition = rememberInfiniteTransition(label = "textShimmer")
    val shimmerProgress by infiniteTransition.animateFloat(
        initialValue = 0f,
        targetValue = 1f,
        animationSpec = infiniteRepeatable(
            animation = tween(2000, easing = LinearEasing),
            repeatMode = RepeatMode.Restart
        ),
        label = "shimmerProgress"
    )

    // Calculate the thumb's right edge position for text clipping
    val thumbRightEdgePx = offsetX.value + thumbWidthPx + paddingPx

    Box(
        modifier = modifier
            .fillMaxWidth()
            .height(60.dp)
            .onSizeChanged { containerWidthPx = it.width.toFloat() }
            .shadow(
                elevation = 2.dp,
                shape = trackShape
            )
            .clip(trackShape)
            .background(trackBackgroundColor),
        contentAlignment = Alignment.CenterStart
    ) {
        // Progressive fill overlay that follows the thumb position
        val fillWidth = if (isProcessing) {
            containerWidthPx
        } else {
            // Fill up to the right edge of the thumb
            (offsetX.value + thumbWidthPx + paddingPx).coerceAtMost(containerWidthPx)
        }

        Box(
            modifier = Modifier
                .fillMaxHeight()
                .width(with(density) { fillWidth.toDp() })
                .clip(trackShape)
                .background(solidColor)
        )

        // Centered loading/check/reject icon when processing
        if (isProcessing) {
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
                    label = "centerIcon"
                ) { state ->
                    when (state) {
                        SwipeConfirmResult.LOADING -> {
                            CircularProgressIndicator(
                                modifier = Modifier.size(28.dp),
                                color = Color.White,
                                strokeWidth = 3.dp,
                                strokeCap = StrokeCap.Round
                            )
                        }
                        SwipeConfirmResult.SUCCESS -> {
                            Icon(
                                imageVector = Icons.Default.Check,
                                contentDescription = null,
                                tint = Color.White,
                                modifier = Modifier.size(32.dp)
                            )
                        }
                        SwipeConfirmResult.REJECTED -> {
                            Icon(
                                imageVector = Icons.Default.Close,
                                contentDescription = null,
                                tint = Color.White,
                                modifier = Modifier.size(32.dp)
                            )
                        }
                        else -> {}
                    }
                }
            }
        }

        // Text area - clipped to not show behind thumb (hidden when processing)
        if (!isProcessing) {
            Box(
                modifier = Modifier
                    .fillMaxSize()
                    .clip(
                        TextClipShape(
                            clipFromLeft = thumbRightEdgePx
                        )
                    ),
                contentAlignment = Alignment.Center
            ) {
                // Text with arrows and shimmer gradient
                Row(
                    verticalAlignment = Alignment.CenterVertically
                ) {
                    Icon(
                        imageVector = Icons.AutoMirrored.Filled.KeyboardArrowRight,
                        contentDescription = null,
                        tint = textColor.copy(alpha = 0.6f),
                        modifier = Modifier.size(20.dp)
                    )
                    Icon(
                        imageVector = Icons.AutoMirrored.Filled.KeyboardArrowRight,
                        contentDescription = null,
                        tint = textColor.copy(alpha = 0.8f),
                        modifier = Modifier.size(20.dp)
                    )
                    Spacer(modifier = Modifier.width(4.dp))
                    Text(
                        text = text,
                        fontSize = 16.sp,
                        fontWeight = FontWeight.Medium,
                        style = TextStyle(
                            brush = Brush.linearGradient(
                                colorStops = arrayOf(
                                    0f to textColor,
                                    (shimmerProgress - 0.1f).coerceIn(0f, 1f) to textColor,
                                    shimmerProgress to solidColor,
                                    (shimmerProgress + 0.1f).coerceIn(0f, 1f) to textColor,
                                    1f to textColor
                                ),
                                start = Offset.Zero,
                                end = Offset(400f, 0f)
                            )
                        )
                    )
                    Spacer(modifier = Modifier.width(4.dp))
                    Icon(
                        imageVector = Icons.AutoMirrored.Filled.KeyboardArrowRight,
                        contentDescription = null,
                        tint = textColor.copy(alpha = 0.8f),
                        modifier = Modifier.size(20.dp)
                    )
                    Icon(
                        imageVector = Icons.AutoMirrored.Filled.KeyboardArrowRight,
                        contentDescription = null,
                        tint = textColor.copy(alpha = 0.6f),
                        modifier = Modifier.size(20.dp)
                    )
                }
            }
        }

        // Draggable thumb with chevron (hidden when processing)
        if (!isProcessing) {
            Box(
                modifier = Modifier
                    .padding(horizontal = horizontalPadding, vertical = 4.dp)
                    .offset { IntOffset(offsetX.value.roundToInt(), 0) }
                    .size(width = thumbWidth, height = thumbHeight)
                    .clip(thumbShape)
                    .background(solidColor)
                    .draggable(
                        orientation = Orientation.Horizontal,
                        enabled = enabled,
                        state = rememberDraggableState { delta ->
                            coroutineScope.launch {
                                // Easier swiping - higher multiplier
                                val baseDragMultiplier = 1f

                                // Apply very light progressive resistance as we drag further
                                val currentProgress = if (maxDragPx > 0f) offsetX.value / maxDragPx else 0f

                                // Very light resistance only toward the end
                                val progressResistance = when {
                                    currentProgress < 0.7f -> 1f       // 0-70%: no resistance
                                    currentProgress < 0.85f -> 0.95f  // 70-85%: very light resistance
                                    else -> 0.9f                       // 85-100%: light resistance
                                }

                                val adjustedDelta = delta * baseDragMultiplier * progressResistance
                                val newValue = (offsetX.value + adjustedDelta).coerceIn(0f, maxDragPx)
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

                                    // Check validation if provided
                                    val isValid = validateBeforeConfirm?.invoke() ?: true

                                    if (isValid) {
                                        // Validation passed - show loading, call onConfirm, then show success
                                        resultState = SwipeConfirmResult.LOADING
                                        view.performHapticFeedback(HapticFeedbackConstants.LONG_PRESS)
                                        onConfirm()
                                        delay(800)
                                        resultState = SwipeConfirmResult.SUCCESS
                                    } else {
                                        // Validation failed - show rejection
                                        resultState = SwipeConfirmResult.REJECTED
                                        // Use LONG_PRESS for haptic feedback (REJECT requires API 30+)
                                        view.performHapticFeedback(HapticFeedbackConstants.LONG_PRESS)

                                        // After showing rejection, reset after a delay
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
                                        animationSpec = tween(400, easing = FastOutSlowInEasing)
                                    )
                                }
                            }
                        }
                    ),
                contentAlignment = Alignment.Center
            ) {
                Icon(
                    imageVector = Icons.AutoMirrored.Filled.KeyboardArrowRight,
                    contentDescription = null,
                    tint = Color.White,
                    modifier = Modifier.size(28.dp)
                )
            }
        }
    }
}

/**
 * Custom shape that clips content from the left side
 * Used to hide text that would appear behind the thumb
 */
private class TextClipShape(
    private val clipFromLeft: Float
) : Shape {
    override fun createOutline(
        size: Size,
        layoutDirection: LayoutDirection,
        density: Density
    ): Outline {
        return Outline.Rectangle(
            Rect(
                left = clipFromLeft,
                top = 0f,
                right = size.width,
                bottom = size.height
            )
        )
    }
}
