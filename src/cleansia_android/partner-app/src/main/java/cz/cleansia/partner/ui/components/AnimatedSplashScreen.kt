package cz.cleansia.partner.ui.components

import androidx.compose.animation.core.Animatable
import androidx.compose.animation.core.EaseInBack
import androidx.compose.animation.core.EaseOutBounce
import androidx.compose.animation.core.FastOutSlowInEasing
import androidx.compose.animation.core.animateFloatAsState
import androidx.compose.animation.core.tween
import androidx.compose.foundation.Canvas
import androidx.compose.foundation.background
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableIntStateOf
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.alpha
import androidx.compose.ui.geometry.Offset
import androidx.compose.ui.graphics.Brush
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.Path
import androidx.compose.ui.graphics.drawscope.DrawScope
import androidx.compose.ui.text.font.Font
import androidx.compose.ui.text.font.FontFamily
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import androidx.compose.foundation.isSystemInDarkTheme
import cz.cleansia.partner.R
import kotlinx.coroutines.delay
import kotlinx.coroutines.launch

private val DropletBlue = Color(0xFF4CB5E0)
private val DropletHighlight = Color(0xFF7DD3FC)
private val SplashRingBlue = Color(0xFF38BDF8)
private val SparkleGold = Color(0xFFFFD700)

@Composable
fun AnimatedSplashScreen(
    onFinished: () -> Unit
) {
    val quicksandFamily = FontFamily(Font(R.font.quicksand_bold, FontWeight.Bold))
    val isDark = isSystemInDarkTheme()
    val brandName = "CLEANSIA"
    var visibleCharCount by remember { mutableIntStateOf(0) }
    var fadeOut by remember { mutableStateOf(false) }

    // Phase 1: Droplet falls from top to center
    val dropletY = remember { Animatable(0f) }
    // Phase 2: Droplet squash on impact
    val dropletScaleX = remember { Animatable(1f) }
    val dropletScaleY = remember { Animatable(1f) }
    // Phase 2: Droplet fades after splash
    val dropletAlpha = remember { Animatable(1f) }
    // Phase 2: Splash rings expand
    val splashRing1 = remember { Animatable(0f) }
    val splashRing2 = remember { Animatable(0f) }
    val splashRing1Alpha = remember { Animatable(0f) }
    val splashRing2Alpha = remember { Animatable(0f) }
    // Phase 2: Small droplets fly out
    val splashDroplets = remember { Animatable(0f) }
    val splashDropletsAlpha = remember { Animatable(0f) }
    // Phase 3: Sparkles
    val sparkleAlpha = remember { Animatable(0f) }
    val sparkleScale = remember { Animatable(0.5f) }
    // Phase 3: Text
    val textAlpha = remember { Animatable(0f) }
    // Shimmer
    val shimmerProgress = remember { Animatable(-0.3f) }

    val contentAlpha by animateFloatAsState(
        targetValue = if (fadeOut) 0f else 1f,
        animationSpec = tween(300, easing = FastOutSlowInEasing),
        label = "contentAlpha"
    )

    LaunchedEffect(Unit) {
        // Phase 1: Droplet falls (0-400ms)
        dropletY.animateTo(
            targetValue = 1f,
            animationSpec = tween(400, easing = EaseInBack)
        )

        // Phase 2: Impact - squash + splash rings (400-800ms)
        // Squash the droplet
        launch {
            dropletScaleX.animateTo(1.6f, tween(80))
            dropletScaleX.animateTo(0.8f, tween(100))
            dropletScaleX.animateTo(1f, tween(80))
        }
        launch {
            dropletScaleY.animateTo(0.4f, tween(80))
            dropletScaleY.animateTo(1.2f, tween(100))
            dropletScaleY.animateTo(1f, tween(80))
        }
        // Small droplets fly out
        launch {
            splashDropletsAlpha.animateTo(1f, tween(50))
            splashDroplets.animateTo(1f, tween(350, easing = FastOutSlowInEasing))
            splashDropletsAlpha.animateTo(0f, tween(150))
        }
        // Ring 1
        launch {
            splashRing1Alpha.animateTo(0.7f, tween(50))
            splashRing1.animateTo(1f, tween(400, easing = FastOutSlowInEasing))
            splashRing1Alpha.animateTo(0f, tween(200))
        }
        // Ring 2 (delayed)
        launch {
            delay(120)
            splashRing2Alpha.animateTo(0.5f, tween(50))
            splashRing2.animateTo(1f, tween(400, easing = FastOutSlowInEasing))
            splashRing2Alpha.animateTo(0f, tween(200))
        }

        // Droplet fades after splash
        delay(250)
        launch {
            dropletAlpha.animateTo(0f, tween(200))
        }

        // Phase 3: Sparkles appear (600-900ms)
        delay(100)
        launch {
            sparkleAlpha.animateTo(1f, tween(150))
            sparkleScale.animateTo(1f, tween(200, easing = EaseOutBounce))
            delay(300)
            sparkleAlpha.animateTo(0f, tween(200))
        }

        // Phase 3: Text types in (700ms-1400ms)
        delay(50)
        textAlpha.animateTo(1f, tween(150))
        for (i in 1..brandName.length) {
            visibleCharCount = i
            delay(40L)
        }

        // Shimmer sweep
        delay(50)
        shimmerProgress.animateTo(
            targetValue = 1.3f,
            animationSpec = tween(300, easing = FastOutSlowInEasing)
        )

        // Brief hold, then fade out
        delay(300)
        fadeOut = true
        delay(320)
        onFinished()
    }

    Box(
        modifier = Modifier
            .fillMaxSize()
            .background(
                Brush.verticalGradient(
                    colors = if (isDark) listOf(
                        Color(0xFF1E293B),
                        Color(0xFF0F172A)
                    ) else listOf(
                        Color(0xFFF8FAFC),
                        Color(0xFFE0F2FE)
                    )
                )
            )
            .alpha(contentAlpha),
        contentAlignment = Alignment.Center
    ) {
        // Canvas for all animations
        Canvas(modifier = Modifier.fillMaxSize()) {
            val centerX = size.width / 2f
            val centerY = size.height * 0.42f

            // Droplet: falls from top to center
            val dropletTopY = -size.height * 0.1f
            val dropletCurrentY = dropletTopY + (centerY - dropletTopY) * dropletY.value

            if (dropletAlpha.value > 0f) {
                drawDroplet(
                    centerX = centerX,
                    centerY = dropletCurrentY,
                    scaleX = dropletScaleX.value,
                    scaleY = dropletScaleY.value,
                    alpha = dropletAlpha.value
                )
            }

            // Splash rings
            if (splashRing1Alpha.value > 0f) {
                val radius1 = 20f + splashRing1.value * 80f
                drawCircle(
                    color = SplashRingBlue,
                    radius = radius1,
                    center = Offset(centerX, centerY),
                    alpha = splashRing1Alpha.value,
                    style = androidx.compose.ui.graphics.drawscope.Stroke(
                        width = 3f * (1f - splashRing1.value * 0.5f)
                    )
                )
            }
            if (splashRing2Alpha.value > 0f) {
                val radius2 = 15f + splashRing2.value * 120f
                drawCircle(
                    color = SplashRingBlue,
                    radius = radius2,
                    center = Offset(centerX, centerY),
                    alpha = splashRing2Alpha.value,
                    style = androidx.compose.ui.graphics.drawscope.Stroke(
                        width = 2f * (1f - splashRing2.value * 0.5f)
                    )
                )
            }

            // Small splash droplets flying outward
            if (splashDropletsAlpha.value > 0f) {
                val angles = listOf(-60f, -30f, 0f, 30f, 60f)
                val distances = listOf(50f, 70f, 60f, 65f, 55f)
                for (i in angles.indices) {
                    val angle = Math.toRadians(angles[i].toDouble() - 90.0)
                    val dist = distances[i] * splashDroplets.value
                    val dx = (kotlin.math.cos(angle) * dist).toFloat()
                    val dy = (kotlin.math.sin(angle) * dist).toFloat()
                    drawCircle(
                        color = DropletBlue,
                        radius = 4f * (1f - splashDroplets.value * 0.6f),
                        center = Offset(centerX + dx, centerY + dy),
                        alpha = splashDropletsAlpha.value
                    )
                }
            }

            // Sparkles
            if (sparkleAlpha.value > 0f) {
                val sparklePositions = listOf(
                    Offset(centerX - 60f, centerY - 50f),
                    Offset(centerX + 55f, centerY - 40f),
                    Offset(centerX - 40f, centerY + 45f),
                    Offset(centerX + 50f, centerY + 35f)
                )
                for (pos in sparklePositions) {
                    drawStar(
                        center = pos,
                        outerRadius = 8f * sparkleScale.value,
                        innerRadius = 3f * sparkleScale.value,
                        alpha = sparkleAlpha.value
                    )
                }
            }
        }

        // Brand text centered below the droplet
        Box(
            modifier = Modifier
                .align(Alignment.Center)
                .padding(top = 120.dp)
                .alpha(textAlpha.value),
            contentAlignment = Alignment.Center
        ) {
            Text(
                text = brandName.take(visibleCharCount),
                fontFamily = quicksandFamily,
                fontSize = 36.sp,
                fontWeight = FontWeight.Bold,
                color = if (isDark) Color(0xFFE0F2FE) else Color(0xFF0C4A6E),
                letterSpacing = 3.sp
            )

            // Shimmer overlay
            if (shimmerProgress.value > -0.3f && visibleCharCount == brandName.length) {
                Box(
                    modifier = Modifier
                        .fillMaxWidth(0.5f)
                        .height(50.dp)
                ) {
                    Canvas(modifier = Modifier.fillMaxSize()) {
                        val shimmerX = size.width * shimmerProgress.value
                        val shimmerWidth = size.width * 0.25f
                        drawRect(
                            brush = Brush.horizontalGradient(
                                colors = listOf(
                                    Color.Transparent,
                                    Color.White.copy(alpha = 0.5f),
                                    Color.Transparent
                                ),
                                startX = shimmerX - shimmerWidth,
                                endX = shimmerX + shimmerWidth
                            )
                        )
                    }
                }
            }
        }
    }
}

private fun DrawScope.drawDroplet(
    centerX: Float,
    centerY: Float,
    scaleX: Float,
    scaleY: Float,
    alpha: Float
) {
    val dropletPath = Path().apply {
        // Water droplet shape: pointed top, round bottom
        val w = 28f * scaleX
        val h = 42f * scaleY
        moveTo(centerX, centerY - h)
        // Left curve
        cubicTo(
            centerX - w * 0.15f, centerY - h * 0.5f,
            centerX - w, centerY + h * 0.1f,
            centerX - w * 0.7f, centerY + h * 0.5f
        )
        // Bottom curve
        cubicTo(
            centerX - w * 0.35f, centerY + h,
            centerX + w * 0.35f, centerY + h,
            centerX + w * 0.7f, centerY + h * 0.5f
        )
        // Right curve
        cubicTo(
            centerX + w, centerY + h * 0.1f,
            centerX + w * 0.15f, centerY - h * 0.5f,
            centerX, centerY - h
        )
        close()
    }

    drawPath(
        path = dropletPath,
        color = DropletBlue,
        alpha = alpha
    )

    // Highlight
    val highlightPath = Path().apply {
        val w = 28f * scaleX
        val h = 42f * scaleY
        moveTo(centerX - w * 0.25f, centerY - h * 0.3f)
        cubicTo(
            centerX - w * 0.4f, centerY - h * 0.1f,
            centerX - w * 0.4f, centerY + h * 0.2f,
            centerX - w * 0.2f, centerY + h * 0.35f
        )
    }

    drawPath(
        path = highlightPath,
        color = DropletHighlight,
        alpha = alpha * 0.6f,
        style = androidx.compose.ui.graphics.drawscope.Stroke(width = 3f)
    )
}

private fun DrawScope.drawStar(
    center: Offset,
    outerRadius: Float,
    innerRadius: Float,
    alpha: Float
) {
    val path = Path()
    val points = 4
    for (i in 0 until points * 2) {
        val radius = if (i % 2 == 0) outerRadius else innerRadius
        val angle = Math.toRadians((i * 360.0 / (points * 2)) - 90.0)
        val x = center.x + (kotlin.math.cos(angle) * radius).toFloat()
        val y = center.y + (kotlin.math.sin(angle) * radius).toFloat()
        if (i == 0) path.moveTo(x, y) else path.lineTo(x, y)
    }
    path.close()
    drawPath(path = path, color = SparkleGold, alpha = alpha)
}
