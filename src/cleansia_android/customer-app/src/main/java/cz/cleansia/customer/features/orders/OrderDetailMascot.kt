package cz.cleansia.customer.features.orders

import androidx.compose.animation.AnimatedContent
import androidx.compose.animation.core.tween
import androidx.compose.animation.fadeIn
import androidx.compose.animation.fadeOut
import androidx.compose.animation.togetherWith
import androidx.compose.foundation.Image
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.WindowInsets
import androidx.compose.foundation.layout.offset
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.statusBars
import androidx.compose.runtime.Composable
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.graphicsLayer
import androidx.compose.ui.platform.LocalDensity
import androidx.compose.ui.res.painterResource
import androidx.compose.ui.unit.Dp
import androidx.compose.ui.unit.IntOffset
import androidx.compose.ui.unit.dp
import cz.cleansia.customer.R
import cz.cleansia.customer.ui.components.MascotAnimation
import kotlin.math.roundToInt

/** Size the sheet content has to leave clear at its top-right for the mascot to land on. */
internal val FloatingMascotSize: Dp = 120.dp

/**
 * The character breaking out of the sheet's top edge, following the drag and clamped under the status
 * bar so the deepest anchor cannot push it off screen.
 *
 * **It belongs to the top of the sheet CONTENT, not to the sheet.** It is drawn in the overlay — above
 * everything, clipped by nothing — so anchoring it to the sheet's edge alone left it parked over
 * whatever the reader scrolled underneath it: on Android it came to rest on the cleaning-details card,
 * on iOS across the date row. Subtracting the content's scroll offset makes it travel with the block it
 * is drawn against, and the fade retires it once that block is off screen rather than leaving it
 * hovering at the sheet edge with unrelated content sliding beneath.
 *
 * **Both positions are lambdas, not values** — read inside `offset`/`graphicsLayer`, a drag or a scroll
 * re-runs only that layer rather than recomposing the subtree.
 */
@Composable
internal fun OrderFloatingMascot(
    status: OrderStatus?,
    sheetTopPx: () -> Float,
    contentScrollPx: () -> Float,
    modifier: Modifier = Modifier,
    size: Dp = FloatingMascotSize,
    rightPadding: Dp = 16.dp,
) {
    // No character belongs on a cancelled order.
    if (status == OrderStatus.Cancelled) return

    val density = LocalDensity.current
    val sizePx = with(density) { size.toPx() }
    val statusBarPx = WindowInsets.statusBars.getTop(density).toFloat()
    // Gone by the time the reader has scrolled past the mascot's own overlap with the sheet — half its
    // height, the part that was ever over content in the first place.
    val fadeDistancePx = sizePx / 2f

    Box(
        modifier = modifier
            .padding(end = rightPadding)
            .offset {
                // The clamp keeps the sheet's own drag from pushing the mascot under the status bar. The
                // scroll is applied AFTER it, on purpose: scrolling should be able to carry the mascot
                // off the top of the screen, which is the whole point of it not being sticky.
                val restingY = (sheetTopPx() - sizePx / 2f).coerceAtLeast(statusBarPx)
                IntOffset(x = 0, y = (restingY - contentScrollPx()).roundToInt())
            }
            .graphicsLayer {
                alpha = (1f - contentScrollPx() / fadeDistancePx).coerceIn(0f, 1f)
            }
            .size(size),
    ) {
        AnimatedContent(
            targetState = status,
            transitionSpec = { fadeIn(tween(300)) togetherWith fadeOut(tween(300)) },
            label = "orderDetailMascot",
        ) { current ->
            when (current) {
                OrderStatus.InProgress -> MascotAnimation(
                    resId = R.raw.mascot_cleaning_in_progress,
                    size = size,
                )
                OrderStatus.Confirmed, OrderStatus.OnTheWay -> MascotAnimation(
                    resId = R.raw.mascot_welcoming,
                    size = size,
                    loop = false,
                )
                else -> Image(
                    painter = painterResource(staticMascotFor(current)),
                    contentDescription = null,
                    modifier = Modifier.size(size),
                )
            }
        }
    }
}

/**
 *  - New / Pending: leaning — nothing to do yet, waiting on a cleaner
 *  - Completed: ready — the kit is packed up again
 */
private fun staticMascotFor(status: OrderStatus?): Int = when (status) {
    OrderStatus.Completed -> R.drawable.mascot_ready
    else -> R.drawable.mascot_leaning
}
