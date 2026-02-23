package cz.cleansia.partner.ui.components

import androidx.compose.animation.core.LinearEasing
import androidx.compose.animation.core.RepeatMode
import androidx.compose.animation.core.animateFloat
import androidx.compose.animation.core.infiniteRepeatable
import androidx.compose.animation.core.rememberInfiniteTransition
import androidx.compose.animation.core.tween
import androidx.compose.foundation.background
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.geometry.Offset
import androidx.compose.ui.graphics.Brush
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.Shape
import androidx.compose.ui.unit.Dp
import androidx.compose.ui.unit.dp
import cz.cleansia.partner.ui.theme.LocalDarkTheme

/**
 * Returns an animated shimmer brush. Call once per skeleton composable
 * and pass to multiple [ShimmerBox] / [ShimmerCircle] for synchronized animation.
 */
@Composable
fun rememberShimmerBrush(): Brush {
    val isDark = LocalDarkTheme.current
    val baseColor = if (isDark) Color(0xFF334155) else Color(0xFFE2E8F0)
    val highlightColor = if (isDark) Color(0xFF475569) else Color(0xFFF1F5F9)

    val transition = rememberInfiniteTransition(label = "shimmer")
    val translateX by transition.animateFloat(
        initialValue = -400f,
        targetValue = 1200f,
        animationSpec = infiniteRepeatable(
            animation = tween(durationMillis = 1200, easing = LinearEasing),
            repeatMode = RepeatMode.Restart
        ),
        label = "shimmerTranslate"
    )

    return Brush.linearGradient(
        colors = listOf(baseColor, highlightColor, baseColor),
        start = Offset(translateX, 0f),
        end = Offset(translateX + 400f, 0f)
    )
}

/**
 * Rectangular shimmer placeholder. Use for text lines, buttons, badges.
 *
 * @param width explicit width, or null for fillMaxWidth
 * @param height placeholder height
 * @param shape corner shape (default 4.dp rounded)
 * @param brush pass a shared brush from [rememberShimmerBrush] for sync
 */
@Composable
fun ShimmerBox(
    modifier: Modifier = Modifier,
    width: Dp? = null,
    height: Dp = 16.dp,
    shape: Shape = RoundedCornerShape(4.dp),
    brush: Brush = rememberShimmerBrush()
) {
    Box(
        modifier = modifier
            .then(if (width != null) Modifier.width(width) else Modifier.fillMaxWidth())
            .height(height)
            .clip(shape)
            .background(brush)
    )
}

/**
 * Circular shimmer placeholder. Use for avatars, icon circles.
 */
@Composable
fun ShimmerCircle(
    size: Dp,
    modifier: Modifier = Modifier,
    brush: Brush = rememberShimmerBrush()
) {
    Box(
        modifier = modifier
            .size(size)
            .clip(CircleShape)
            .background(brush)
    )
}
