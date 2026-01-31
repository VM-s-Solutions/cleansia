package cz.cleansia.partner.ui.components

import androidx.compose.animation.animateColorAsState
import androidx.compose.animation.core.LinearEasing
import androidx.compose.animation.core.RepeatMode
import androidx.compose.animation.core.animateFloat
import androidx.compose.animation.core.infiniteRepeatable
import androidx.compose.animation.core.rememberInfiniteTransition
import androidx.compose.animation.core.tween
import androidx.compose.foundation.Canvas
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.size
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.geometry.Offset
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.StrokeCap
import androidx.compose.ui.graphics.drawscope.Stroke
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.platform.LocalDensity
import androidx.compose.ui.unit.Dp
import androidx.compose.ui.unit.dp
import cz.cleansia.partner.ui.theme.LocalDarkTheme
import cz.cleansia.partner.R
import cz.cleansia.partner.ui.theme.TimerColors
import kotlin.math.absoluteValue
import kotlin.math.cos
import kotlin.math.sin

/**
 * Timer status representing the urgency level based on remaining time percentage
 */
enum class TimerStatus {
    PLENTY,   // > 70% time remaining - Green
    CAUTION,  // 30-70% time remaining - Yellow/Orange
    URGENT,   // < 30% time remaining - Red
    OVERTIME  // Past estimated time - Dark Red with pulse
}

// Timer configuration
private const val STRIPE_COUNT = 120 // More stripes for denser ribbed texture
private const val STRIPE_WIDTH = 1.5f // Thin dark lines for ribbed texture
private const val RING_WIDTH_RATIO = 0.09f // Progress ring width (wider)
private const val BORDER_WIDTH = 16f // Bigger inner/outer black borders
private const val GAP_WIDTH = 8f // Width of section separator gaps
private const val START_ANGLE_OFFSET = 12f // Offset to leave space for top border and rounded cap
private const val END_ANGLE_OFFSET = 12f // Offset at end for top border and rounded cap
private const val STRIPE_EDGE_INSET = 5f // Inset stripes from arc edges to avoid rounded cap areas

/**
 * Wolt-style countdown timer that shows remaining time for cleaning with color-coded urgency.
 *
 * The timer displays a circular progress ring with a striped/ribbed texture effect:
 * - Green (#00FF00 lime green): Plenty of time remaining
 * - Yellow/Orange: Caution, time is passing
 * - Red: Urgent, hurry up
 * - Dark Red: Past the estimated time with pulsing effect
 *
 * The progress starts from the bottom-left (~7 o'clock) and sweeps clockwise.
 * The ring has a dark background (#1A1A1A) for unfilled portions.
 *
 * @param estimatedMinutes Total estimated time for the cleaning in minutes
 * @param elapsedSeconds How many seconds have passed since the order started
 * @param modifier Modifier for the composable
 * @param size Size of the timer ring
 * @param strokeWidth Width of the progress ring (deprecated, uses RING_WIDTH_RATIO)
 */
@Composable
fun CleaningCountdownTimer(
    estimatedMinutes: Int,
    elapsedSeconds: Long,
    modifier: Modifier = Modifier,
    size: Dp = 200.dp,
    strokeWidth: Dp = 10.dp
) {
    val totalSeconds = estimatedMinutes * 60L
    val remainingSeconds = (totalSeconds - elapsedSeconds).coerceAtLeast(-totalSeconds)
    val isOvertime = remainingSeconds < 0

    // Calculate percentage (0.0 to 1.0, where 1.0 = full time remaining)
    val percentageRemaining = if (totalSeconds > 0) {
        (remainingSeconds.toFloat() / totalSeconds).coerceIn(0f, 1f)
    } else {
        0f
    }

    // Calculate sweep angle for the solid arc (starts from top, goes clockwise)
    val sweepAngle = if (isOvertime) {
        360f
    } else {
        360f * percentageRemaining
    }

    // Determine timer status based on percentage
    val timerStatus = when {
        isOvertime -> TimerStatus.OVERTIME
        percentageRemaining > 0.70f -> TimerStatus.PLENTY
        percentageRemaining > 0.30f -> TimerStatus.CAUTION
        else -> TimerStatus.URGENT
    }

    // Get color based on status - using TimerColors for consistency
    val targetColor = when (timerStatus) {
        TimerStatus.PLENTY -> TimerColors.Plenty
        TimerStatus.CAUTION -> TimerColors.Caution
        TimerStatus.URGENT -> TimerColors.Urgent
        TimerStatus.OVERTIME -> TimerColors.Overtime
    }

    // Animate color transitions
    val progressColor by animateColorAsState(
        targetValue = targetColor,
        animationSpec = tween(500),
        label = "timerColor"
    )

    // Pulse animation for overtime
    val infiniteTransition = rememberInfiniteTransition(label = "overtimePulse")
    val pulseAlpha by infiniteTransition.animateFloat(
        initialValue = 1f,
        targetValue = if (isOvertime) 0.5f else 1f,
        animationSpec = infiniteRepeatable(
            animation = tween(800, easing = LinearEasing),
            repeatMode = RepeatMode.Reverse
        ),
        label = "pulseAlpha"
    )

    // Calculate display time
    val displaySeconds = remainingSeconds.absoluteValue
    val displayMinutes = (displaySeconds / 60).toInt()
    val displaySecs = (displaySeconds % 60).toInt()

    // Always show MM:SS format
    val timeText = "$displayMinutes:${displaySecs.toString().padStart(2, '0')}"

    val statusText = if (isOvertime) {
        stringResource(R.string.timer_overtime)
    } else {
        stringResource(R.string.timer_remaining)
    }

    // Theme-aware colors
    val isDarkTheme = LocalDarkTheme.current

    // Ring border color - adapts to theme
    val ringBackgroundColor = if (isDarkTheme) Color(0xFF1A1A1A) else Color.Black
    // Grey color for unfilled area (after progress passes)
    val unfilledColor = if (isDarkTheme) Color(0xFF4A4A4A) else Color(0xFF3A3A3A)

    // Inner fill color - white in light, dark surface in dark
    val innerFillColor = if (isDarkTheme) Color(0xFF1E293B) else Color.White

    // Text color - adapts to theme, red pulse for overtime
    val textColor = MaterialTheme.colorScheme.onSurface
    val secondaryTextColor = MaterialTheme.colorScheme.onSurfaceVariant

    // Calculate responsive font sizes based on component size
    val density = LocalDensity.current
    val sizeInPx = with(density) { size.toPx() }
    // Calculate inner content area (inside the ring)
    val ringWidthPx = sizeInPx * RING_WIDTH_RATIO
    val innerContentRadius = (sizeInPx / 2) - BORDER_WIDTH - 4f - ringWidthPx - BORDER_WIDTH
    val innerContentDiameter = innerContentRadius * 2
    // Font sizes as ratios of inner content area
    val mainFontSize = with(density) { (innerContentDiameter * 0.28f).toSp() }
    val statusFontSize = with(density) { (innerContentDiameter * 0.12f).toSp() }
    val overtimeIndicatorSize = with(density) { (innerContentDiameter * 0.16f).toSp() }

    Box(
        modifier = modifier.size(size),
        contentAlignment = Alignment.Center
    ) {
        Canvas(modifier = Modifier.size(size)) {
            val canvasSize = this.size.width
            val centerX = canvasSize / 2
            val centerY = canvasSize / 2
            val ringWidth = canvasSize * RING_WIDTH_RATIO
            val outerRadius = (canvasSize / 2) - BORDER_WIDTH - 4f
            val innerRadius = outerRadius - ringWidth
            val middleRadius = (outerRadius + innerRadius) / 2

            // Draw OUTER black border circle
            drawCircle(
                color = Color.Black,
                radius = outerRadius + BORDER_WIDTH / 2,
                center = Offset(centerX, centerY),
                style = Stroke(width = BORDER_WIDTH)
            )

            // Draw INNER black border circle
            drawCircle(
                color = Color.Black,
                radius = innerRadius - BORDER_WIDTH / 2,
                center = Offset(centerX, centerY),
                style = Stroke(width = BORDER_WIDTH)
            )

            // Fill the inner area (where time text is displayed)
            drawCircle(
                color = innerFillColor,
                radius = innerRadius - BORDER_WIDTH,
                center = Offset(centerX, centerY)
            )

            // Draw the dark background ring (full circle) between borders
            drawCircle(
                color = ringBackgroundColor,
                radius = middleRadius,
                center = Offset(centerX, centerY),
                style = Stroke(width = ringWidth)
            )

            // Available arc space (leaving room for top border: 1% at start, 1% at end = 98% usable)
            val usableArc = 360f - START_ANGLE_OFFSET - END_ANGLE_OFFSET

            // Scale the sweep angle to fit within usable arc
            val scaledSweepAngle = (sweepAngle / 360f) * usableArc
            val scaledPassedAngle = usableArc - scaledSweepAngle

            // COUNTER-CLOCKWISE TICKING direction
            // Grey (passed time) starts from 12 o'clock and grows counter-clockwise (to the left)
            // Colored progress (remaining time) is drawn AFTER grey, on the right side clockwise

            // Draw GREY arc for time that has PASSED first (from 12 o'clock going counter-clockwise/left)
            if (scaledPassedAngle > 0f && !isOvertime) {
                drawArc(
                    color = unfilledColor,
                    startAngle = -90f - START_ANGLE_OFFSET, // Start just after 12 o'clock
                    sweepAngle = -scaledPassedAngle, // Negative = counter-clockwise (going left)
                    useCenter = false,
                    topLeft = Offset(centerX - middleRadius, centerY - middleRadius),
                    size = androidx.compose.ui.geometry.Size(middleRadius * 2, middleRadius * 2),
                    style = Stroke(width = ringWidth, cap = StrokeCap.Round)
                )
            }

            // Draw SOLID colored arc for remaining time (clockwise from 12 o'clock, going right)
            if (scaledSweepAngle > 0f) {
                drawArc(
                    color = progressColor.copy(alpha = pulseAlpha),
                    startAngle = -90f + START_ANGLE_OFFSET, // Start just after 12 o'clock (going clockwise/right)
                    sweepAngle = scaledSweepAngle, // Positive = clockwise (going right)
                    useCenter = false,
                    topLeft = Offset(centerX - middleRadius, centerY - middleRadius),
                    size = androidx.compose.ui.geometry.Size(middleRadius * 2, middleRadius * 2),
                    style = Stroke(width = ringWidth, cap = StrokeCap.Round) // Rounded ends
                )

                // Draw thin dark stripe lines ON TOP of the solid arc for ribbed texture
                // Stripes go clockwise (following the colored progress)
                // Smaller inset at end to ensure stripes reach closer to the edge
                val stripeArcStart = -90f + START_ANGLE_OFFSET + STRIPE_EDGE_INSET - 8f
                val stripeArcEnd = -90f + START_ANGLE_OFFSET + scaledSweepAngle + 2f
                val stripeArcLength = stripeArcEnd - stripeArcStart

                if (stripeArcLength > 0) {
                    val filledStripes = ((STRIPE_COUNT * (scaledSweepAngle / usableArc)).toInt()).coerceAtLeast(5)
                    val stripeSpacing = stripeArcLength / (filledStripes - 1).coerceAtLeast(1)

                    for (i in 0 until filledStripes) {
                        val angleDegrees = stripeArcStart + (i * stripeSpacing)
                        val angleRadians = Math.toRadians(angleDegrees.toDouble())

                        val startX = centerX + (innerRadius * cos(angleRadians)).toFloat()
                        val startY = centerY + (innerRadius * sin(angleRadians)).toFloat()
                        val endX = centerX + (outerRadius * cos(angleRadians)).toFloat()
                        val endY = centerY + (outerRadius * sin(angleRadians)).toFloat()

                        drawLine(
                            color = ringBackgroundColor.copy(alpha = 0.3f),
                            start = Offset(startX, startY),
                            end = Offset(endX, endY),
                            strokeWidth = STRIPE_WIDTH,
                            cap = StrokeCap.Butt
                        )
                    }
                }
            }

            // Draw section separator gaps at 70% and 30% marks
            // Grey goes counter-clockwise (left) from 12 o'clock, colored goes clockwise (right)
            // Gap at 70% mark - 30% of usable arc clockwise from start (on the right/colored side)
            val gap70Angle = -90f + START_ANGLE_OFFSET + (usableArc * 0.30f)
            val gap70Radians = Math.toRadians(gap70Angle.toDouble())
            drawLine(
                color = Color.Black,
                start = Offset(
                    centerX + ((innerRadius - BORDER_WIDTH) * cos(gap70Radians)).toFloat(),
                    centerY + ((innerRadius - BORDER_WIDTH) * sin(gap70Radians)).toFloat()
                ),
                end = Offset(
                    centerX + ((outerRadius + BORDER_WIDTH) * cos(gap70Radians)).toFloat(),
                    centerY + ((outerRadius + BORDER_WIDTH) * sin(gap70Radians)).toFloat()
                ),
                strokeWidth = GAP_WIDTH,
                cap = StrokeCap.Butt
            )

            // Gap at 30% mark - 70% of usable arc clockwise from start (on the right/colored side)
            val gap30Angle = -90f + START_ANGLE_OFFSET + (usableArc * 0.70f)
            val gap30Radians = Math.toRadians(gap30Angle.toDouble())
            drawLine(
                color = Color.Black,
                start = Offset(
                    centerX + ((innerRadius - BORDER_WIDTH) * cos(gap30Radians)).toFloat(),
                    centerY + ((innerRadius - BORDER_WIDTH) * sin(gap30Radians)).toFloat()
                ),
                end = Offset(
                    centerX + ((outerRadius + BORDER_WIDTH) * cos(gap30Radians)).toFloat(),
                    centerY + ((outerRadius + BORDER_WIDTH) * sin(gap30Radians)).toFloat()
                ),
                strokeWidth = GAP_WIDTH,
                cap = StrokeCap.Butt
            )
        }

        // Center text content
        Column(
            horizontalAlignment = Alignment.CenterHorizontally,
            verticalArrangement = Arrangement.Center
        ) {
            // Overtime indicator
            if (isOvertime) {
                Text(
                    text = "+",
                    style = MaterialTheme.typography.headlineMedium,
                    fontWeight = FontWeight.Bold,
                    color = progressColor.copy(alpha = pulseAlpha),
                    fontSize = overtimeIndicatorSize
                )
            }

            // Main time display in MM:SS format
            Text(
                text = timeText,
                style = MaterialTheme.typography.displayLarge,
                fontWeight = FontWeight.Bold,
                color = if (isOvertime) progressColor.copy(alpha = pulseAlpha) else textColor,
                fontSize = mainFontSize,
                textAlign = TextAlign.Center
            )

            // Status text (remaining/overtime)
            Text(
                text = statusText,
                style = MaterialTheme.typography.bodyMedium,
                color = if (isOvertime) progressColor.copy(alpha = pulseAlpha) else secondaryTextColor,
                fontWeight = if (isOvertime) FontWeight.SemiBold else FontWeight.Normal,
                fontSize = statusFontSize
            )
        }
    }
}

/**
 * Compact version of the countdown timer for use in cards or smaller spaces
 */
@Composable
fun CompactCountdownTimer(
    estimatedMinutes: Int,
    elapsedSeconds: Long,
    modifier: Modifier = Modifier
) {
    CleaningCountdownTimer(
        estimatedMinutes = estimatedMinutes,
        elapsedSeconds = elapsedSeconds,
        modifier = modifier,
        size = 120.dp,
        strokeWidth = 8.dp
    )
}

/**
 * Get the timer status based on remaining time percentage
 */
fun getTimerStatus(estimatedMinutes: Int, elapsedSeconds: Long): TimerStatus {
    val totalSeconds = estimatedMinutes * 60L
    val remainingSeconds = totalSeconds - elapsedSeconds

    return when {
        remainingSeconds < 0 -> TimerStatus.OVERTIME
        remainingSeconds.toFloat() / totalSeconds > 0.70f -> TimerStatus.PLENTY
        remainingSeconds.toFloat() / totalSeconds > 0.30f -> TimerStatus.CAUTION
        else -> TimerStatus.URGENT
    }
}

/**
 * Get the color for a given timer status
 */
fun getTimerColor(status: TimerStatus): Color {
    return when (status) {
        TimerStatus.PLENTY -> TimerColors.Plenty
        TimerStatus.CAUTION -> TimerColors.Caution
        TimerStatus.URGENT -> TimerColors.Urgent
        TimerStatus.OVERTIME -> TimerColors.Overtime
    }
}
