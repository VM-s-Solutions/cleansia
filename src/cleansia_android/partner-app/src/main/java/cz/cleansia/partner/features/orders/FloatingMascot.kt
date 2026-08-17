package cz.cleansia.partner.features.orders

import androidx.compose.animation.AnimatedContent
import androidx.compose.animation.SizeTransform
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
import androidx.compose.ui.draw.scale
import androidx.compose.ui.graphics.graphicsLayer
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.platform.LocalDensity
import androidx.compose.ui.res.painterResource
import androidx.compose.ui.unit.Dp
import androidx.compose.ui.unit.IntOffset
import androidx.compose.ui.unit.dp
import coil3.compose.AsyncImage
import coil3.request.ImageRequest
import cz.cleansia.core.ui.components.SnapSheetDefaults
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
    size: Dp = SnapSheetDefaults.OrnamentSize,
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
            // `clip = false` because a normalised character (see [mascotArtScale]) is drawn larger
            // than the box that holds it, and AnimatedContent clips to its own bounds by default —
            // which would take the top off the head instead of the padding around it.
            transitionSpec = {
                fadeIn(tween(300)) togetherWith fadeOut(tween(300)) using SizeTransform(clip = false)
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
        modifier = Modifier
            .size(size)
            .scale(mascotArtScale[drawableRes] ?: 1f),
    )
}

/**
 * How much each character has to be blown up for all of them to be drawn the same size.
 *
 * **The box was never the problem — the artwork is.** Every asset is a square canvas the character
 * only partly fills, and it fills it by a different amount each time, so an identical box still
 * renders characters of visibly different sizes. Measured as the alpha bounding box's width over the
 * canvas width: `thumbs_up` 0.57, `spray_and_cloth` 0.57, `leaning` 0.65, `waving_friendly` 0.77,
 * `ready` 0.82 — a 44% spread between the smallest and the largest, at one shared 128dp box.
 *
 * Each factor is `0.82 / fill`, i.e. normalised against the fullest asset, so nothing is ever drawn
 * smaller than it is today. Re-cutting the PNGs would be the real fix; this is the one that does not
 * touch binary assets. To remeasure after an asset changes, take the alpha bounding box of the PNG
 * and divide its width by the canvas width. [CleaningWebpMascot] is not in the table — an animated
 * WebP is cropped to its own frame and was never measured — and anything unlisted draws unscaled.
 */
private val mascotArtScale: Map<Int, Float> = mapOf(
    R.drawable.mascot_ready to 1.00f,
    R.drawable.mascot_waving_friendly to 1.06f,
    R.drawable.mascot_leaning to 1.26f,
    R.drawable.mascot_thumbs_up to 1.44f,
    R.drawable.mascot_spray_and_cloth to 1.45f,
)

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
