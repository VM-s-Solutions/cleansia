package cz.cleansia.core.ui.components

import androidx.compose.animation.core.animateFloatAsState
import androidx.compose.animation.core.tween
import androidx.compose.foundation.background
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.offset
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.alpha
import androidx.compose.ui.graphics.Brush
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.semantics.contentDescription
import androidx.compose.ui.semantics.semantics
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import cz.cleansia.core.ui.theme.Poppins
import cz.cleansia.core.ui.theme.Spacing
import cz.cleansia.core.ui.theme.SplashGradientEnd
import cz.cleansia.core.ui.theme.SplashGradientStart

private const val BRAND = "Cleansia"
private const val PARTNER_LOCKUP = "PARTNER"
private const val LETTER_STAGGER_MILLIS = 60
private const val LETTER_DURATION_MILLIS = 320
private const val TRAILING_DURATION_MILLIS = 400

/**
 * The branded launch splash shared by both apps. "Cleansia" reveals one letter
 * at a time on the brand gradient, then the optional PARTNER lockup, the
 * tagline and the resolver spinner fade in beneath it — the same reveal the iOS
 * `WordmarkSplashView` plays, so a cold start reads identically on both
 * platforms.
 *
 * The tagline is a parameter because each app owns its own localized copy; the
 * shared component stays free of app-specific strings.
 */
@Composable
fun WordmarkSplash(
    tagline: String,
    modifier: Modifier = Modifier,
    showsPartnerLabel: Boolean = false,
) {
    var revealed by remember { mutableStateOf(false) }
    LaunchedEffect(Unit) { revealed = true }

    val trailingAlpha by animateFloatAsState(
        targetValue = if (revealed) 1f else 0f,
        animationSpec = tween(
            durationMillis = TRAILING_DURATION_MILLIS,
            delayMillis = BRAND.length * LETTER_STAGGER_MILLIS,
        ),
        label = "splashTrailingReveal",
    )

    Box(
        modifier = modifier
            .fillMaxSize()
            .background(Brush.linearGradient(listOf(SplashGradientStart, SplashGradientEnd))),
        contentAlignment = Alignment.Center,
    ) {
        Column(horizontalAlignment = Alignment.CenterHorizontally) {
            Wordmark(revealed = revealed)

            if (showsPartnerLabel) {
                Spacer(Modifier.height(Spacing.XXS))
                Text(
                    text = PARTNER_LOCKUP,
                    fontFamily = Poppins,
                    fontWeight = FontWeight.SemiBold,
                    fontSize = 15.sp,
                    letterSpacing = 6.sp,
                    color = Color.White,
                    modifier = Modifier.alpha(trailingAlpha),
                )
            }

            Spacer(Modifier.height(if (showsPartnerLabel) Spacing.S else Spacing.M))
            Text(
                text = tagline,
                style = MaterialTheme.typography.bodyLarge,
                color = Color.White.copy(alpha = 0.9f),
                textAlign = TextAlign.Center,
                modifier = Modifier
                    .alpha(trailingAlpha)
                    .padding(horizontal = Spacing.XL),
            )

            Spacer(Modifier.height(Spacing.XL))
            CircularProgressIndicator(
                modifier = Modifier
                    .size(28.dp)
                    .alpha(trailingAlpha),
                color = Color.White,
                strokeWidth = 3.dp,
            )
        }
    }
}

@Composable
private fun Wordmark(revealed: Boolean) {
    Row(
        modifier = Modifier.semantics(mergeDescendants = true) { contentDescription = BRAND },
    ) {
        BRAND.forEachIndexed { index, character ->
            val progress by animateFloatAsState(
                targetValue = if (revealed) 1f else 0f,
                animationSpec = tween(
                    durationMillis = LETTER_DURATION_MILLIS,
                    delayMillis = index * LETTER_STAGGER_MILLIS,
                ),
                label = "splashLetter$index",
            )
            Text(
                text = character.toString(),
                fontFamily = Poppins,
                fontWeight = FontWeight.Bold,
                fontSize = 44.sp,
                color = Color.White,
                modifier = Modifier
                    .alpha(progress)
                    .offset(y = ((1f - progress) * 16f).dp),
            )
        }
    }
}
