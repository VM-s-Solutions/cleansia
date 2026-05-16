package cz.cleansia.customer.features.orders.photos

import androidx.activity.compose.BackHandler
import androidx.compose.foundation.background
import androidx.compose.foundation.gestures.detectTapGestures
import androidx.compose.foundation.gestures.detectTransformGestures
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.PaddingValues
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.navigationBarsPadding
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.statusBarsPadding
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.pager.HorizontalPager
import androidx.compose.foundation.pager.rememberPagerState
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.outlined.Close
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Surface
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableFloatStateOf
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.geometry.Offset
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.graphicsLayer
import androidx.compose.ui.input.pointer.pointerInput
import androidx.compose.ui.layout.ContentScale
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.compose.ui.platform.LocalConfiguration
import androidx.compose.ui.platform.LocalDensity
import coil3.compose.AsyncImage
import coil3.request.ImageRequest
import coil3.size.Size
import cz.cleansia.customer.R
import cz.cleansia.core.format.formatOrderDateTime
import cz.cleansia.customer.core.orders.OrderPhotoDto

/**
 * Fullscreen pager overlay — renders over the gallery on top of a near-black
 * scrim. Swipe horizontally to change photos; pinch to zoom on each page; tap
 * the close button (or press system back) to dismiss.
 *
 * The pager state is pinned to [startIndex] on first composition. If the
 * source list is empty we bail out with a close callback so the parent
 * doesn't have to null-check.
 */
@Composable
fun FullscreenPager(
    photos: List<OrderPhotoDto>,
    startIndex: Int,
    onClose: () -> Unit,
) {
    if (photos.isEmpty()) {
        // Defensive: the parent only opens this when the current tab is non-empty,
        // but if the list flipped to empty mid-composition (e.g. tab switch), close
        // via a LaunchedEffect to avoid mutating state inside a composition.
        androidx.compose.runtime.LaunchedEffect(Unit) { onClose() }
        return
    }
    val initial = startIndex.coerceIn(0, photos.size - 1)
    val pagerState = rememberPagerState(initialPage = initial) { photos.size }

    Box(
        modifier = Modifier
            .fillMaxSize()
            .background(Color.Black.copy(alpha = 0.95f)),
    ) {
        HorizontalPager(
            state = pagerState,
            modifier = Modifier.fillMaxSize(),
            contentPadding = PaddingValues(0.dp),
        ) { page ->
            ZoomableAsyncImage(url = photos[page].blobUrl)
        }

        // Top bar: close + "current / total" counter.
        Row(
            modifier = Modifier
                .fillMaxWidth()
                .statusBarsPadding()
                .padding(12.dp),
            verticalAlignment = Alignment.CenterVertically,
        ) {
            IconButton(onClick = onClose) {
                Icon(
                    Icons.Outlined.Close,
                    contentDescription = stringResource(R.string.common_close),
                    tint = Color.White,
                )
            }
            Spacer(Modifier.width(8.dp))
            Text(
                text = "${pagerState.currentPage + 1} / ${photos.size}",
                color = Color.White,
                style = MaterialTheme.typography.labelLarge,
            )
        }

        // Bottom metadata overlay — shows the cleaner's name and capture time
        // when either is present. Skipped entirely if both are null/blank.
        val current = photos.getOrNull(pagerState.currentPage)
        if (current != null) {
            PhotoMetadataRow(
                photo = current,
                modifier = Modifier
                    .align(Alignment.BottomStart)
                    .navigationBarsPadding()
                    .padding(16.dp),
            )
        }
    }
    BackHandler(onBack = onClose)
}

/**
 * A pinch-zoomable, pan-able image. Scale is clamped [1f, 4f]; offset is
 * unconstrained at scale > 1 (user can pan freely) and force-zeroed back
 * at scale 1 so the image recenters on release. Double-tap toggles between
 * 1x and 2x — the cheap way out of an accidental deep zoom.
 *
 * No hard pan clamping — the image can be panned off-edge at higher zoom.
 * That's a deliberate simplification for Wave 2; the double-tap reset keeps
 * the UX forgiving.
 */
@Composable
private fun ZoomableAsyncImage(url: String?) {
    var scale by remember { mutableFloatStateOf(1f) }
    var offset by remember { mutableStateOf(Offset.Zero) }
    val minScale = 1f
    val maxScale = 4f

    // Cap decode resolution at ~ screen size scaled up by maxScale. Without
    // this hint Coil decodes the full source bitmap (often a 12MP phone
    // photo, ~25MB once decoded) which paged the heap heavily and froze the
    // RenderThread on bitmap upload — see ANR fa8ceb1 (2026-04-25).
    val config = LocalConfiguration.current
    val density = LocalDensity.current
    val maxPixels = remember(config, density) {
        val screenWidthPx = with(density) { config.screenWidthDp.dp.toPx().toInt() }
        val screenHeightPx = with(density) { config.screenHeightDp.dp.toPx().toInt() }
        val side = maxOf(screenWidthPx, screenHeightPx) * maxScale.toInt()
        side.coerceAtLeast(1024)
    }

    Box(
        modifier = Modifier
            .fillMaxSize()
            .pointerInput(Unit) {
                detectTransformGestures { _, pan, zoom, _ ->
                    val newScale = (scale * zoom).coerceIn(minScale, maxScale)
                    scale = newScale
                    offset = if (newScale == 1f) Offset.Zero else offset + pan
                }
            }
            .pointerInput(Unit) {
                detectTapGestures(
                    onDoubleTap = {
                        scale = if (scale > 1f) 1f else 2f
                        if (scale == 1f) offset = Offset.Zero
                    },
                )
            },
        contentAlignment = Alignment.Center,
    ) {
        val context = androidx.compose.ui.platform.LocalContext.current
        val request = remember(url, maxPixels) {
            ImageRequest.Builder(context)
                .data(url)
                .size(Size(maxPixels, maxPixels))
                .build()
        }
        AsyncImage(
            model = request,
            contentDescription = null,
            contentScale = ContentScale.Fit,
            modifier = Modifier
                .fillMaxSize()
                .graphicsLayer(
                    scaleX = scale,
                    scaleY = scale,
                    translationX = offset.x,
                    translationY = offset.y,
                ),
        )
    }
}

/**
 * Bottom-left rounded pill showing the cleaner who captured the photo and
 * the capture time. Renders nothing if both fields are missing — Wave 2
 * photos from the customer side may not carry metadata.
 */
@Composable
private fun PhotoMetadataRow(
    photo: OrderPhotoDto,
    modifier: Modifier = Modifier,
) {
    val name = photo.capturedByEmployeeName?.takeIf { it.isNotBlank() }
    val time = photo.capturedAt?.takeIf { it.isNotBlank() }?.let { formatOrderDateTime(it) }
    if (name == null && time == null) return

    val text = buildString {
        if (name != null) append(name)
        if (time != null) {
            if (isNotEmpty()) append(" · ")
            append(time)
        }
    }
    Surface(
        modifier = modifier.clip(RoundedCornerShape(999.dp)),
        color = Color.White.copy(alpha = 0.15f),
        contentColor = Color.White,
    ) {
        Text(
            text = text,
            style = MaterialTheme.typography.labelMedium.copy(fontWeight = FontWeight.Medium),
            modifier = Modifier.padding(horizontal = 12.dp, vertical = 6.dp),
        )
    }
}
