package cz.cleansia.partner.features.orders

import androidx.compose.animation.AnimatedContent
import androidx.compose.animation.core.tween
import androidx.compose.animation.fadeIn
import androidx.compose.animation.fadeOut
import androidx.compose.animation.togetherWith
import androidx.compose.foundation.Image
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.offset
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.SheetState
import androidx.compose.runtime.Composable
import androidx.compose.runtime.remember
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.platform.LocalDensity
import androidx.compose.ui.res.painterResource
import androidx.compose.ui.unit.Dp
import androidx.compose.ui.unit.IntOffset
import androidx.compose.ui.unit.dp
import coil3.compose.AsyncImage
import coil3.request.ImageRequest
import cz.cleansia.partner.R
import cz.cleansia.partner.api.model.OrderStatus
import kotlin.math.roundToInt

/**
 * Foodora-style floating mascot that breaks out of the bottom-sheet
 * top edge on the RIGHT side. Half above (over the map), half below
 * (over the sheet's first strip). No card behind it — just the
 * PNG/WebP directly so the character reads as part of the scene.
 *
 * Tracks the sheet's top-edge Y via [SheetState.requireOffset] so it
 * follows when the cleaner drags the sheet up or down.
 *
 * Caller must place this inside the same `Box(fillMaxSize)` that wraps
 * the BottomSheetScaffold and align it to TopEnd so it sits on the
 * right side. Right-padding pulls it in from the screen edge.
 */
@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun FloatingMascot(
    status: OrderStatus?,
    sheetState: SheetState,
    modifier: Modifier = Modifier,
    size: Dp = 128.dp,
    rightPadding: Dp = 16.dp,
) {
    // Cancelled phase hides the mascot — no character makes sense on
    // a dead order.
    if (status == OrderStatus._6) return

    val density = LocalDensity.current
    val sizePx = with(density) { size.toPx() }
    val sheetTopPx = runCatching { sheetState.requireOffset() }.getOrDefault(0f)
    // Visual center of the mascot sits ON the sheet edge — half over
    // the map, half over the panel.
    val offsetY = (sheetTopPx - sizePx / 2f).roundToInt()

    Box(
        modifier = modifier
            .padding(end = rightPadding)
            .offset { IntOffset(x = 0, y = offsetY) }
            .size(size),
    ) {
        AnimatedContent(
            targetState = status,
            transitionSpec = {
                fadeIn(tween(300)) togetherWith fadeOut(tween(300))
            },
            label = "floatingMascotCrossfade",
        ) { current ->
            if (current == OrderStatus._4) {
                CleaningWebpMascot(size = size)
            } else {
                StaticPngMascot(drawableRes = mascotForStatus(current), size = size)
            }
        }
    }
}

@Composable
private fun CleaningWebpMascot(size: Dp) {
    val context = LocalContext.current
    val request = remember {
        ImageRequest.Builder(context)
            .data(R.raw.mascot_cleaning_in_progress)
            .build()
    }
    AsyncImage(
        model = request,
        contentDescription = null,
        modifier = Modifier.size(size),
    )
}

@Composable
private fun StaticPngMascot(drawableRes: Int, size: Dp) {
    Image(
        painter = painterResource(drawableRes),
        contentDescription = null,
        modifier = Modifier.size(size),
    )
}

/**
 * Per-status mascot art. Picked from the shared web mascot set so
 * each phase reads emotionally distinct.
 *
 *  - New / Pending:  leaning — "ready when you're ready"
 *  - Confirmed:      waving  — "see you soon" energy
 *  - OnTheWay:       spray + cloth — equipped, "I'm coming with my kit"
 *  - InProgress:     animated WebP (handled in [CleaningWebpMascot])
 *  - Completed:      thumbs up — "great job"
 */
private fun mascotForStatus(status: OrderStatus?): Int = when (status) {
    OrderStatus._0, OrderStatus._1 -> R.drawable.mascot_leaning
    OrderStatus._2 -> R.drawable.mascot_waving_friendly
    OrderStatus._3 -> R.drawable.mascot_spray_and_cloth
    OrderStatus._5 -> R.drawable.mascot_thumbs_up
    else -> R.drawable.mascot_thumbs_up
}
