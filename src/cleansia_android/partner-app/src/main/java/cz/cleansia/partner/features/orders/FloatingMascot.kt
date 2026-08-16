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
import androidx.compose.ui.graphics.graphicsLayer
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
 * Mascot breaking out of the sheet's top edge on the right — half over the map, half over the sheet.
 *
 * **No card behind it**, so the character reads as part of the scene rather than as a widget.
 */
@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun FloatingMascot(
    status: OrderStatus?,
    sheetState: SheetState,
    contentScrollPx: () -> Float,
    modifier: Modifier = Modifier,
    size: Dp = 128.dp,
    rightPadding: Dp = 16.dp,
) {
    // Cancelled phase hides the mascot — no character makes sense on
    // a dead order.
    if (status == OrderStatus._6) return

    val density = LocalDensity.current
    val sizePx = with(density) { size.toPx() }
    // Gone by the time the cleaner has scrolled past the mascot's own overlap with the sheet — half its
    // height, the part that was ever over content in the first place.
    val fadeDistancePx = sizePx / 2f

    Box(
        modifier = modifier
            .padding(end = rightPadding)
            .offset {
                // Visual centre sits ON the sheet edge — half over the map, half over the panel — and
                // then travels up with the content. Anchoring to the sheet edge ALONE left it parked
                // over whatever the cleaner scrolled underneath it; it belongs to the top of the sheet
                // content, not to the viewport. Read inside the offset so a drag or a scroll re-runs
                // only this layer rather than recomposing the subtree.
                val sheetTopPx = runCatching { sheetState.requireOffset() }.getOrDefault(0f)
                val restingY = sheetTopPx - sizePx / 2f
                IntOffset(x = 0, y = (restingY - contentScrollPx()).roundToInt())
            }
            .graphicsLayer {
                alpha = (1f - contentScrollPx() / fadeDistancePx).coerceIn(0f, 1f)
            }
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

/**
 * The asset's real VP8X canvas. Without an explicit size Coil derives the decode size from layout —
 * 384 px at 3x, 512 px at 4x — and CPU-rescales every frame up from the native 360. Decoding at
 * native size hands the upscale to the GPU once per draw instead. At 24 fps that is the difference
 * between smooth and juddering, and it matters more since the asset went from 63 frames to 125.
 */
private const val MASCOT_CANVAS_PX = 360

@Composable
private fun CleaningWebpMascot(size: Dp) {
    val context = LocalContext.current
    val request = remember {
        ImageRequest.Builder(context)
            .data(R.raw.mascot_cleaning_in_progress)
            .size(MASCOT_CANVAS_PX)
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
