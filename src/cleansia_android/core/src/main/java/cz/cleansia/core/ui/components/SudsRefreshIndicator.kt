package cz.cleansia.core.ui.components

import androidx.compose.animation.core.LinearEasing
import androidx.compose.animation.core.RepeatMode
import androidx.compose.animation.core.animateFloat
import androidx.compose.animation.core.infiniteRepeatable
import androidx.compose.animation.core.rememberInfiniteTransition
import androidx.compose.animation.core.tween
import androidx.compose.foundation.Image
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Surface
import androidx.compose.material3.pulltorefresh.PullToRefreshState
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.shadow
import androidx.compose.ui.graphics.graphicsLayer
import androidx.compose.ui.platform.LocalDensity
import androidx.compose.ui.res.painterResource
import androidx.compose.ui.unit.dp
import cz.cleansia.core.R

/**
 * Pull-to-refresh indicator shared by both customer + partner apps:
 * a suds-swirl rosette inside a white pill, matching the standard
 * Material refresh-pill chrome (white surface, soft shadow, circular).
 *
 * Animation choices:
 *
 *  - **Slides in from above the screen edge.** During the pull the
 *    indicator translates downward proportional to `distanceFraction`
 *    — same metaphor as the stock Material indicator. Earlier
 *    versions faded in via alpha, which made the icon look like it
 *    appeared from nowhere mid-screen; the slide-down reads as
 *    "you're pulling something down onto the page".
 *  - **Refreshing**: locked at its resting position, continuous 1.1s
 *    linear rotation loop. Replaces the default Material spinner.
 *  - **Pulling**: rotates a quarter-turn through the drag for a
 *    "spinning up" feel.
 *
 * The Box anchors to TopCenter so the caller can
 * `Modifier.padding(top = ...)` to clear the status bar.
 */
@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun SudsRefreshIndicator(
    state: PullToRefreshState,
    isRefreshing: Boolean,
    modifier: Modifier = Modifier,
) {
    // distanceFraction is 0f at rest, 1f at the trigger threshold,
    // overshoots > 1f if the user keeps pulling.
    val pullFraction = state.distanceFraction.coerceIn(0f, 1.4f)

    // Don't compose anything when there's no pull AND we're not
    // refreshing. Prevents the Surface + its shadow from painting a
    // faint halo at the top of the screen when idle.
    if (!isRefreshing && pullFraction <= 0f) return

    // Travel distance: ~64dp from "fully hidden above the indicator
    // box" to "resting position". When refreshing we want the pill to
    // sit at its resting position (0 translation); when pulling we
    // translate it up by (1 - pullFraction) * travelPx so the pill
    // appears to slide down from above as the finger drags.
    val density = LocalDensity.current
    val travelPx = with(density) { 64.dp.toPx() }
    val translationYPx = if (isRefreshing) {
        0f
    } else {
        // pullFraction can overshoot 1.0, but past the trigger the
        // pill is already at its resting position — clamp so the
        // indicator doesn't keep traveling downward.
        -travelPx * (1f - pullFraction.coerceAtMost(1f))
    }

    // Rotation: spin only while refreshing (i.e. after the user has
    // released past the trigger threshold and the network call is in
    // flight). During the pull itself the icon stays still and just
    // slides down — spinning during the drag read as "you're
    // dragging a spinner", which is the wrong mental model. Now the
    // icon is a static "I'm here, you can let go" until release,
    // then turns into a spinner the moment refresh actually starts.
    val rotation: Float = if (isRefreshing) {
        val transition = rememberInfiniteTransition(label = "sudsSpin")
        val angle by transition.animateFloat(
            initialValue = 0f,
            targetValue = 360f,
            animationSpec = infiniteRepeatable(
                animation = tween(durationMillis = 800, easing = LinearEasing),
                repeatMode = RepeatMode.Restart,
            ),
            label = "sudsSpinAngle",
        )
        angle
    } else {
        0f
    }

    Box(
        modifier = modifier
            .size(56.dp)
            // Single graphicsLayer on the outer container so the
            // Surface, its shadow, and the icon all translate as one
            // unit — otherwise the shadow would stay pinned at the
            // resting position while the pill slid in, leaving a
            // visible ghost halo at the top.
            .graphicsLayer { translationY = translationYPx },
        contentAlignment = Alignment.Center,
    ) {
        Surface(
            modifier = Modifier
                .size(48.dp)
                .shadow(elevation = 6.dp, shape = CircleShape, clip = false),
            shape = CircleShape,
            color = MaterialTheme.colorScheme.surface,
        ) {
            Box(contentAlignment = Alignment.Center) {
                Image(
                    painter = painterResource(R.drawable.refresh_suds),
                    contentDescription = null,
                    modifier = Modifier
                        .size(32.dp)
                        .graphicsLayer { rotationZ = rotation },
                )
            }
        }
    }
}
