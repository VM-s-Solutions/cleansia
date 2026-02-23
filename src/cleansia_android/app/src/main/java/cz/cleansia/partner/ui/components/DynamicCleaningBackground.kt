package cz.cleansia.partner.ui.components

import androidx.compose.animation.core.LinearEasing
import androidx.compose.animation.core.RepeatMode
import androidx.compose.animation.core.animateFloat
import androidx.compose.animation.core.infiniteRepeatable
import androidx.compose.animation.core.rememberInfiniteTransition
import androidx.compose.animation.core.tween
import androidx.compose.foundation.Canvas
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.remember
import androidx.compose.ui.Modifier
import androidx.compose.ui.geometry.Offset
import androidx.compose.ui.geometry.Size
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.Path
import androidx.compose.ui.graphics.StrokeCap
import androidx.compose.ui.graphics.drawscope.DrawScope
import androidx.compose.ui.graphics.drawscope.Stroke
import androidx.compose.ui.graphics.drawscope.rotate
import kotlin.math.sin

/**
 * Floating cleaning icon configuration
 */
private data class FloatingIcon(
    val type: IconType,
    val startX: Float,       // 0..1 fraction of width
    val startY: Float,       // 0..1 fraction of height
    val driftX: Float,       // horizontal drift amplitude (fraction of width)
    val driftY: Float,       // vertical drift amplitude (fraction of height)
    val rotation: Float,     // rotation amplitude in degrees
    val durationMs: Int,     // animation cycle duration
    val phaseOffset: Float,  // 0..1 phase offset for variety
    val scale: Float         // size multiplier
)

private enum class IconType {
    BROOM, SOAP, SPRAY, BUCKET, SPARKLE, BUBBLES, DROPLET, SPONGE
}

/**
 * Dynamic background with floating cleaning-themed icons.
 * Inspired by the Angular cleansia-dynamic-background component with
 * 8 floating icons animated with continuous translate + rotate transforms.
 *
 * Icons are drawn using Canvas for performance (no icon fonts needed).
 *
 * @param modifier Modifier for the composable
 * @param iconColor Color for the floating icons
 * @param iconAlpha Overall opacity for the icons
 */
@Composable
fun DynamicCleaningBackground(
    modifier: Modifier = Modifier,
    iconColor: Color = Color.White,
    iconAlpha: Float = 0.18f
) {
    val icons = remember {
        listOf(
            FloatingIcon(IconType.BROOM,   0.12f, 0.15f, 0.04f, 0.06f, 15f, 17000, 0.0f, 1.0f),
            FloatingIcon(IconType.SOAP,    0.85f, 0.25f, 0.03f, 0.05f, 20f, 19000, 0.2f, 0.85f),
            FloatingIcon(IconType.SPRAY,   0.35f, 0.70f, 0.05f, 0.04f, 12f, 15000, 0.4f, 0.9f),
            FloatingIcon(IconType.BUCKET,  0.70f, 0.65f, 0.03f, 0.06f, 18f, 21000, 0.6f, 0.95f),
            FloatingIcon(IconType.SPARKLE, 0.50f, 0.20f, 0.04f, 0.05f, 25f, 16000, 0.1f, 0.8f),
            FloatingIcon(IconType.BUBBLES, 0.20f, 0.55f, 0.05f, 0.04f, 10f, 18000, 0.5f, 0.75f),
            FloatingIcon(IconType.DROPLET, 0.78f, 0.45f, 0.03f, 0.07f, 22f, 20000, 0.3f, 0.85f),
            FloatingIcon(IconType.SPONGE,  0.45f, 0.40f, 0.04f, 0.05f, 16f, 22000, 0.7f, 0.9f)
        )
    }

    val infiniteTransition = rememberInfiniteTransition(label = "floatingIcons")

    // Create a single animation value that drives all icons via their phase offsets
    val animProgress by infiniteTransition.animateFloat(
        initialValue = 0f,
        targetValue = 1f,
        animationSpec = infiniteRepeatable(
            animation = tween(60000, easing = LinearEasing),
            repeatMode = RepeatMode.Restart
        ),
        label = "globalProgress"
    )

    Canvas(modifier = modifier.fillMaxSize()) {
        val baseSize = size.minDimension * 0.08f

        icons.forEach { icon ->
            val phase = (animProgress * (60000f / icon.durationMs) + icon.phaseOffset) % 1f
            val angle = phase * 2f * Math.PI.toFloat()

            val x = size.width * icon.startX + size.width * icon.driftX * sin(angle)
            val y = size.height * icon.startY + size.height * icon.driftY * sin(angle * 0.7f + 1.2f)
            val rot = icon.rotation * sin(angle * 0.5f + 0.8f)
            val iconSize = baseSize * icon.scale

            val drawColor = iconColor.copy(alpha = iconAlpha)
            val strokeColor = iconColor.copy(alpha = (iconAlpha * 1.5f).coerceAtMost(0.35f))

            rotate(degrees = rot, pivot = Offset(x, y)) {
                when (icon.type) {
                    IconType.BROOM -> drawBroomIcon(x, y, iconSize, drawColor, strokeColor)
                    IconType.SOAP -> drawSoapIcon(x, y, iconSize, drawColor, strokeColor)
                    IconType.SPRAY -> drawSprayIcon(x, y, iconSize, drawColor, strokeColor)
                    IconType.BUCKET -> drawBucketIcon(x, y, iconSize, drawColor, strokeColor)
                    IconType.SPARKLE -> drawSparkleIcon(x, y, iconSize, drawColor, strokeColor)
                    IconType.BUBBLES -> drawBubblesIcon(x, y, iconSize, drawColor, strokeColor)
                    IconType.DROPLET -> drawDropletIcon(x, y, iconSize, drawColor, strokeColor)
                    IconType.SPONGE -> drawSpongeIcon(x, y, iconSize, drawColor, strokeColor)
                }
            }
        }
    }
}

// --- Icon drawing functions ---

private fun DrawScope.drawBroomIcon(
    cx: Float, cy: Float, s: Float,
    fill: Color, stroke: Color
) {
    // Handle
    drawLine(
        color = fill,
        start = Offset(cx, cy - s * 0.7f),
        end = Offset(cx, cy + s * 0.1f),
        strokeWidth = s * 0.08f,
        cap = StrokeCap.Round
    )
    // Bristle head
    val bristlePath = Path().apply {
        moveTo(cx - s * 0.3f, cy + s * 0.1f)
        lineTo(cx + s * 0.3f, cy + s * 0.1f)
        lineTo(cx + s * 0.25f, cy + s * 0.5f)
        lineTo(cx - s * 0.25f, cy + s * 0.5f)
        close()
    }
    drawPath(bristlePath, fill)
    drawPath(bristlePath, stroke, style = Stroke(width = s * 0.04f))
    // Bristle lines
    for (i in -2..2) {
        val bx = cx + i * s * 0.1f
        drawLine(
            color = stroke,
            start = Offset(bx, cy + s * 0.15f),
            end = Offset(bx, cy + s * 0.45f),
            strokeWidth = s * 0.02f,
            cap = StrokeCap.Round
        )
    }
}

private fun DrawScope.drawSoapIcon(
    cx: Float, cy: Float, s: Float,
    fill: Color, stroke: Color
) {
    // Soap bar body
    drawRoundRect(
        color = fill,
        topLeft = Offset(cx - s * 0.3f, cy - s * 0.2f),
        size = Size(s * 0.6f, s * 0.5f),
        cornerRadius = androidx.compose.ui.geometry.CornerRadius(s * 0.08f)
    )
    drawRoundRect(
        color = stroke,
        topLeft = Offset(cx - s * 0.3f, cy - s * 0.2f),
        size = Size(s * 0.6f, s * 0.5f),
        cornerRadius = androidx.compose.ui.geometry.CornerRadius(s * 0.08f),
        style = Stroke(width = s * 0.04f)
    )
    // Bubble on top
    drawCircle(color = fill, radius = s * 0.1f, center = Offset(cx + s * 0.15f, cy - s * 0.3f))
    drawCircle(color = stroke, radius = s * 0.1f, center = Offset(cx + s * 0.15f, cy - s * 0.3f), style = Stroke(width = s * 0.03f))
    drawCircle(color = fill, radius = s * 0.06f, center = Offset(cx - s * 0.1f, cy - s * 0.35f))
    drawCircle(color = stroke, radius = s * 0.06f, center = Offset(cx - s * 0.1f, cy - s * 0.35f), style = Stroke(width = s * 0.03f))
}

private fun DrawScope.drawSprayIcon(
    cx: Float, cy: Float, s: Float,
    fill: Color, stroke: Color
) {
    // Bottle body
    drawRoundRect(
        color = fill,
        topLeft = Offset(cx - s * 0.2f, cy - s * 0.1f),
        size = Size(s * 0.4f, s * 0.6f),
        cornerRadius = androidx.compose.ui.geometry.CornerRadius(s * 0.06f)
    )
    drawRoundRect(
        color = stroke,
        topLeft = Offset(cx - s * 0.2f, cy - s * 0.1f),
        size = Size(s * 0.4f, s * 0.6f),
        cornerRadius = androidx.compose.ui.geometry.CornerRadius(s * 0.06f),
        style = Stroke(width = s * 0.04f)
    )
    // Nozzle
    drawRoundRect(
        color = fill,
        topLeft = Offset(cx - s * 0.12f, cy - s * 0.3f),
        size = Size(s * 0.24f, s * 0.22f),
        cornerRadius = androidx.compose.ui.geometry.CornerRadius(s * 0.04f)
    )
    drawRoundRect(
        color = stroke,
        topLeft = Offset(cx - s * 0.12f, cy - s * 0.3f),
        size = Size(s * 0.24f, s * 0.22f),
        cornerRadius = androidx.compose.ui.geometry.CornerRadius(s * 0.04f),
        style = Stroke(width = s * 0.03f)
    )
    // Trigger
    drawLine(
        color = stroke,
        start = Offset(cx + s * 0.12f, cy - s * 0.2f),
        end = Offset(cx + s * 0.3f, cy - s * 0.15f),
        strokeWidth = s * 0.05f,
        cap = StrokeCap.Round
    )
    // Spray mist dots
    drawCircle(color = fill, radius = s * 0.03f, center = Offset(cx - s * 0.3f, cy - s * 0.4f))
    drawCircle(color = fill, radius = s * 0.025f, center = Offset(cx - s * 0.4f, cy - s * 0.35f))
    drawCircle(color = fill, radius = s * 0.02f, center = Offset(cx - s * 0.35f, cy - s * 0.5f))
}

private fun DrawScope.drawBucketIcon(
    cx: Float, cy: Float, s: Float,
    fill: Color, stroke: Color
) {
    // Bucket body (trapezoid)
    val bucketPath = Path().apply {
        moveTo(cx - s * 0.35f, cy - s * 0.15f)
        lineTo(cx + s * 0.35f, cy - s * 0.15f)
        lineTo(cx + s * 0.28f, cy + s * 0.4f)
        lineTo(cx - s * 0.28f, cy + s * 0.4f)
        close()
    }
    drawPath(bucketPath, fill)
    drawPath(bucketPath, stroke, style = Stroke(width = s * 0.04f))
    // Handle (arc)
    drawArc(
        color = stroke,
        startAngle = 180f,
        sweepAngle = 180f,
        useCenter = false,
        topLeft = Offset(cx - s * 0.2f, cy - s * 0.5f),
        size = Size(s * 0.4f, s * 0.35f),
        style = Stroke(width = s * 0.05f, cap = StrokeCap.Round)
    )
}

private fun DrawScope.drawSparkleIcon(
    cx: Float, cy: Float, s: Float,
    fill: Color, stroke: Color
) {
    // 4-point star sparkle
    val starPath = Path().apply {
        moveTo(cx, cy - s * 0.45f)
        lineTo(cx + s * 0.1f, cy - s * 0.1f)
        lineTo(cx + s * 0.45f, cy)
        lineTo(cx + s * 0.1f, cy + s * 0.1f)
        moveTo(cx, cy + s * 0.45f)
        lineTo(cx - s * 0.1f, cy + s * 0.1f)
        lineTo(cx - s * 0.45f, cy)
        lineTo(cx - s * 0.1f, cy - s * 0.1f)
        close()
    }
    drawPath(starPath, fill)
    drawPath(starPath, stroke, style = Stroke(width = s * 0.03f))
    // Center dot
    drawCircle(color = stroke, radius = s * 0.05f, center = Offset(cx, cy))
}

private fun DrawScope.drawBubblesIcon(
    cx: Float, cy: Float, s: Float,
    fill: Color, stroke: Color
) {
    // Three bubbles of different sizes
    drawCircle(color = fill, radius = s * 0.22f, center = Offset(cx - s * 0.1f, cy + s * 0.08f))
    drawCircle(color = stroke, radius = s * 0.22f, center = Offset(cx - s * 0.1f, cy + s * 0.08f), style = Stroke(width = s * 0.03f))

    drawCircle(color = fill, radius = s * 0.15f, center = Offset(cx + s * 0.2f, cy - s * 0.05f))
    drawCircle(color = stroke, radius = s * 0.15f, center = Offset(cx + s * 0.2f, cy - s * 0.05f), style = Stroke(width = s * 0.03f))

    drawCircle(color = fill, radius = s * 0.1f, center = Offset(cx + s * 0.05f, cy - s * 0.3f))
    drawCircle(color = stroke, radius = s * 0.1f, center = Offset(cx + s * 0.05f, cy - s * 0.3f), style = Stroke(width = s * 0.03f))

    // Highlight dots
    drawCircle(color = stroke, radius = s * 0.04f, center = Offset(cx - s * 0.16f, cy))
    drawCircle(color = stroke, radius = s * 0.03f, center = Offset(cx + s * 0.16f, cy - s * 0.1f))
}

private fun DrawScope.drawDropletIcon(
    cx: Float, cy: Float, s: Float,
    fill: Color, stroke: Color
) {
    // Water droplet shape
    val dropPath = Path().apply {
        moveTo(cx, cy - s * 0.45f)
        cubicTo(
            cx + s * 0.35f, cy - s * 0.05f,
            cx + s * 0.35f, cy + s * 0.25f,
            cx, cy + s * 0.45f
        )
        cubicTo(
            cx - s * 0.35f, cy + s * 0.25f,
            cx - s * 0.35f, cy - s * 0.05f,
            cx, cy - s * 0.45f
        )
        close()
    }
    drawPath(dropPath, fill)
    drawPath(dropPath, stroke, style = Stroke(width = s * 0.04f))
    // Highlight
    drawLine(
        color = stroke,
        start = Offset(cx - s * 0.08f, cy),
        end = Offset(cx - s * 0.05f, cy + s * 0.15f),
        strokeWidth = s * 0.04f,
        cap = StrokeCap.Round
    )
}

private fun DrawScope.drawSpongeIcon(
    cx: Float, cy: Float, s: Float,
    fill: Color, stroke: Color
) {
    // Sponge body (rounded rectangle)
    drawRoundRect(
        color = fill,
        topLeft = Offset(cx - s * 0.3f, cy - s * 0.2f),
        size = Size(s * 0.6f, s * 0.45f),
        cornerRadius = androidx.compose.ui.geometry.CornerRadius(s * 0.1f)
    )
    drawRoundRect(
        color = stroke,
        topLeft = Offset(cx - s * 0.3f, cy - s * 0.2f),
        size = Size(s * 0.6f, s * 0.45f),
        cornerRadius = androidx.compose.ui.geometry.CornerRadius(s * 0.1f),
        style = Stroke(width = s * 0.04f)
    )
    // Sponge holes/pores
    drawCircle(color = stroke, radius = s * 0.04f, center = Offset(cx - s * 0.12f, cy - s * 0.02f))
    drawCircle(color = stroke, radius = s * 0.035f, center = Offset(cx + s * 0.1f, cy + s * 0.05f))
    drawCircle(color = stroke, radius = s * 0.03f, center = Offset(cx, cy + s * 0.1f))
    drawCircle(color = stroke, radius = s * 0.035f, center = Offset(cx + s * 0.15f, cy - s * 0.08f))
    drawCircle(color = stroke, radius = s * 0.025f, center = Offset(cx - s * 0.08f, cy + s * 0.12f))
}
