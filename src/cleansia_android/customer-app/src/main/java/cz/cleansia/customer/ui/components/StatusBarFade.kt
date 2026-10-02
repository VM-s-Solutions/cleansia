package cz.cleansia.customer.ui.components

import androidx.compose.animation.core.animateFloatAsState
import androidx.compose.foundation.ScrollState
import androidx.compose.foundation.layout.WindowInsets
import androidx.compose.foundation.layout.asPaddingValues
import androidx.compose.foundation.layout.statusBars
import androidx.compose.material3.MaterialTheme
import androidx.compose.runtime.derivedStateOf
import androidx.compose.runtime.getValue
import androidx.compose.runtime.remember
import androidx.compose.ui.Modifier
import androidx.compose.ui.composed
import androidx.compose.ui.draw.drawWithContent
import androidx.compose.ui.geometry.Size
import androidx.compose.ui.graphics.Brush
import androidx.compose.ui.unit.dp

/**
 * Fades scrolled content out under the status bar on a screen that draws edge to edge with no top
 * bar (Home, Profile, Subscribe Plus): the band is the page background over the status bar, then
 * fades to clear over [FadeTail]. Place it directly before `verticalScroll(scrollState)` so it draws
 * over the viewport, not over the scrolled content.
 *
 * It shows only once the content has left its top. At rest the full-bleed Profile and Plus heroes
 * paint the status-bar strip themselves, and a pull-to-refresh never moves the scroll value. Drawing
 * only, so it takes no touches and adds nothing to the semantics tree.
 */
fun Modifier.statusBarFade(scrollState: ScrollState): Modifier = composed {
    val scrolled by remember(scrollState) { derivedStateOf { scrollState.value > 0 } }
    val alpha by animateFloatAsState(if (scrolled) 1f else 0f, label = "statusBarFade")
    val statusBar = WindowInsets.statusBars.asPaddingValues().calculateTopPadding()
    val color = MaterialTheme.colorScheme.background

    drawWithContent {
        drawContent()
        if (alpha > 0f) {
            val solid = statusBar.toPx()
            val height = solid + FadeTail.toPx()
            drawRect(
                brush = Brush.verticalGradient(
                    0f to color,
                    solid / height to color,
                    // The background at zero alpha, not Color.Transparent (transparent black): the
                    // gradient interpolates unpremultiplied, so a fade to black greys the light theme.
                    1f to color.copy(alpha = 0f),
                    endY = height,
                ),
                size = Size(size.width, height),
                alpha = alpha,
            )
        }
    }
}

private val FadeTail = 24.dp
