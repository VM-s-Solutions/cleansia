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
 * **The sheet position is a lambda, not a value** — read inside the offset, the drag recomposes only
 * the offset rather than the whole subtree.
 */
@Composable
internal fun OrderFloatingMascot(
    status: OrderStatus?,
    sheetTopPx: () -> Float,
    modifier: Modifier = Modifier,
    size: Dp = FloatingMascotSize,
    rightPadding: Dp = 16.dp,
) {
    // No character belongs on a cancelled order.
    if (status == OrderStatus.Cancelled) return

    val density = LocalDensity.current
    val sizePx = with(density) { size.toPx() }
    val statusBarPx = WindowInsets.statusBars.getTop(density).toFloat()

    Box(
        modifier = modifier
            .padding(end = rightPadding)
            .offset {
                val y = (sheetTopPx() - sizePx / 2f).coerceAtLeast(statusBarPx)
                IntOffset(x = 0, y = y.roundToInt())
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
