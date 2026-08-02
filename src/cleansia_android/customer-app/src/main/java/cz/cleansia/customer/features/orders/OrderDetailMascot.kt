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
 * The character that breaks out of the sheet's top edge on the right, half over
 * the map and half over the panel. Follows the sheet as it is dragged, and is
 * clamped under the status bar so the deepest anchor doesn't push it off the
 * top of the screen.
 *
 * [sheetTopPx] is a lambda, not a value: read inside `offset` the drag moves
 * the mascot in the layout phase, where reading it as a parameter would
 * recompose this subtree — animated mascot included — on every frame of the
 * gesture.
 *
 * Caller places it in the [cz.cleansia.core.ui.components.SnapSheet] overlay
 * slot aligned to TopEnd.
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
