package cz.cleansia.partner.ui.components

import androidx.compose.animation.core.Animatable
import androidx.compose.animation.core.FastOutSlowInEasing
import androidx.compose.animation.core.animateFloatAsState
import androidx.compose.animation.core.tween
import androidx.compose.foundation.Canvas
import androidx.compose.foundation.background
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.size
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
import androidx.compose.ui.graphics.Brush
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.text.font.Font
import androidx.compose.ui.text.font.FontFamily
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import com.airbnb.lottie.compose.LottieAnimation
import com.airbnb.lottie.compose.LottieCompositionSpec
import com.airbnb.lottie.compose.animateLottieCompositionAsState
import com.airbnb.lottie.compose.rememberLottieComposition
import cz.cleansia.partner.R
import kotlinx.coroutines.delay

@Composable
fun AnimatedSplashScreen(
    onFinished: () -> Unit
) {
    val quicksandFamily = FontFamily(Font(R.font.quicksand_bold, FontWeight.Bold))
    val brandName = "Cleansia"
    var visibleCharCount by remember { mutableIntStateOf(0) }
    var fadeOut by remember { mutableStateOf(false) }
    var showText by remember { mutableStateOf(false) }

    // Text reveal animation
    val textAlpha = remember { Animatable(0f) }
    // Shimmer on text
    val shimmerProgress = remember { Animatable(-0.3f) }

    val contentAlpha by animateFloatAsState(
        targetValue = if (fadeOut) 0f else 1f,
        animationSpec = tween(400, easing = FastOutSlowInEasing),
        label = "contentAlpha"
    )

    // Lottie composition
    val composition by rememberLottieComposition(
        LottieCompositionSpec.RawRes(R.raw.splash_animation)
    )
    val lottieProgress by animateLottieCompositionAsState(
        composition = composition,
        iterations = 1,
        speed = 1f
    )

    // Sequence: Lottie plays, rag wipes, then text types in, then fade out
    LaunchedEffect(lottieProgress) {
        // Start text animation after the rag has wiped across (~85% through)
        if (lottieProgress > 0.85f && !showText) {
            showText = true
        }
    }

    LaunchedEffect(showText) {
        if (!showText) return@LaunchedEffect

        // Text fades in
        textAlpha.animateTo(1f, tween(300))

        // Typewriter effect
        for (i in 1..brandName.length) {
            visibleCharCount = i
            delay(60L)
        }

        // Shimmer sweep
        delay(80)
        shimmerProgress.animateTo(
            targetValue = 1.3f,
            animationSpec = tween(500, easing = FastOutSlowInEasing)
        )

        // Wait for Lottie to finish, then fade out
        delay(500)
        fadeOut = true
        delay(420)
        onFinished()
    }

    Box(
        modifier = Modifier
            .fillMaxSize()
            .background(
                Brush.verticalGradient(
                    colors = listOf(
                        Color(0xFFF8FAFC),
                        Color(0xFFE0F2FE)
                    )
                )
            )
            .alpha(contentAlpha),
        contentAlignment = Alignment.Center
    ) {
        // Lottie animation
        LottieAnimation(
            composition = composition,
            progress = { lottieProgress },
            modifier = Modifier.size(220.dp)
        )

        // Brand text with shimmer - overlaid in center where rag wipes
        Box(
            modifier = Modifier.alpha(textAlpha.value),
            contentAlignment = Alignment.Center
        ) {
            Text(
                text = brandName.take(visibleCharCount),
                fontFamily = quicksandFamily,
                fontSize = 36.sp,
                fontWeight = FontWeight.Bold,
                color = Color(0xFF0C4A6E),
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
