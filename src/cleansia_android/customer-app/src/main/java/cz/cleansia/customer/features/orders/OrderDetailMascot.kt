package cz.cleansia.customer.features.orders

import androidx.compose.animation.AnimatedContent
import androidx.compose.animation.SizeTransform
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
import androidx.compose.ui.draw.scale
import androidx.compose.ui.graphics.graphicsLayer
import androidx.compose.ui.platform.LocalDensity
import androidx.compose.ui.res.painterResource
import androidx.compose.ui.unit.Dp
import androidx.compose.ui.unit.IntOffset
import androidx.compose.ui.unit.dp
import cz.cleansia.core.ui.components.SnapSheetDefaults
import cz.cleansia.customer.R
import cz.cleansia.customer.ui.components.MascotAnimation
import kotlin.math.roundToInt

/** Size the sheet content has to leave clear at its top-right for the mascot to land on. */
internal val FloatingMascotSize: Dp = SnapSheetDefaults.OrnamentSize

/**
 * The character breaking out of the sheet's top edge, following the drag and the scroll and fading out
 * rather than ever coming to rest.
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
            .offset { IntOffset(x = 0, y = mascotTopPx(sheetTopPx, contentScrollPx, sizePx).roundToInt()) }
            .graphicsLayer {
                // Two fades, whichever is further along. The scroll one retires the character once the
                // block it is drawn against has gone; the edge one retires it as it approaches the
                // status bar.
                //
                // The edge fade replaces a `coerceAtLeast(statusBarPx)` clamp, and that clamp was the
                // bug: the moment the sheet rose far enough for it to bite, the character stopped
                // tracking the sheet edge and pinned itself to a fixed screen position — it jumped
                // slightly DOWN as the clamp engaged and then rode the viewport instead of the panel,
                // which is exactly backwards. Untethered it follows the panel all the way and simply
                // fades out rather than ever standing still.
                val top = mascotTopPx(sheetTopPx, contentScrollPx, sizePx)
                val scrollFade = 1f - contentScrollPx() / fadeDistancePx
                val edgeFade = (top - statusBarPx) / fadeDistancePx
                alpha = minOf(scrollFade, edgeFade).coerceIn(0f, 1f)
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
                else -> {
                    val art = staticMascotFor(current)
                    Image(
                        painter = painterResource(art),
                        contentDescription = null,
                        modifier = Modifier
                            .size(size)
                            .scale(mascotArtScale[art] ?: 1f),
                    )
                }
            }
        }
    }
}

/**
 * Where the character's top edge sits: centred on the sheet's top edge, then carried up by however far
 * the sheet's content has scrolled.
 *
 * **Unclamped, deliberately.** It is anchored to the panel, so it must follow the panel wherever the
 * panel goes — including off the top of the screen. Holding it back at the status bar made it stick to
 * the viewport instead, which is the one thing it must never do.
 */
private fun mascotTopPx(sheetTopPx: () -> Float, contentScrollPx: () -> Float, sizePx: Float): Float =
    sheetTopPx() - sizePx / 2f - contentScrollPx()

/**
 * How much each character has to be blown up for all of them to be drawn the same size.
 *
 * **The box was never the problem — the artwork is.** Every asset is a square canvas the character
 * only partly fills, and it fills it by a different amount each time, so an identical box still
 * renders characters of visibly different sizes. Measured as the alpha bounding box's width over the
 * canvas width, the whole family reads: `thumbs_up` 0.57, `spray_and_cloth` 0.57, `leaning` 0.65,
 * `waving` 0.77, `ready` 0.82 — a 44% spread between the smallest and the largest.
 *
 * Each factor is `0.82 / fill`, i.e. normalised against the fullest asset, so nothing is ever drawn
 * smaller than it is today. Re-cutting the PNGs would be the real fix; this is the one that does not
 * touch binary assets. To remeasure after an asset changes, take the alpha bounding box of the PNG
 * and divide its width by the canvas width. The animated WebPs are not in the table — they are a
 * different set, cropped differently again — and anything unlisted simply draws unscaled.
 */
private val mascotArtScale: Map<Int, Float> = mapOf(
    R.drawable.mascot_ready to 1.00f,
    R.drawable.mascot_leaning to 1.26f,
)

/**
 *  - New / Pending: leaning — nothing to do yet, waiting on a cleaner
 *  - Completed: ready — the kit is packed up again
 */
private fun staticMascotFor(status: OrderStatus?): Int = when (status) {
    OrderStatus.Completed -> R.drawable.mascot_ready
    else -> R.drawable.mascot_leaning
}
