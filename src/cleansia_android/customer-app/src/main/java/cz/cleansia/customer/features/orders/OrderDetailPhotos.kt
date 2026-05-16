package cz.cleansia.customer.features.orders

import androidx.compose.foundation.background
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.lazy.LazyRow
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.outlined.ArrowForward
import androidx.compose.material3.Icon
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.layout.ContentScale
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.Dp
import androidx.compose.ui.unit.dp
import coil3.compose.AsyncImage
import cz.cleansia.customer.R
import cz.cleansia.customer.core.orders.OrderPhotosResponse

/* ── Photos (Wave 2 Phase 5) ── */

/**
 * Summary card rendered on the detail screen when the order has photos.
 * Shows a Before/After count pill row and up to 6 thumbnail previews; the
 * entire card is tappable and delegates navigation to [onViewPhotos].
 *
 * `photoType` is serialized as an Int (1 = Before, 2 = After); anything
 * null/unknown is bucketed under Before on the gallery itself, but here we
 * trust the backend's `beforePhotoCount` / `afterPhotoCount` fields which
 * mirror that convention.
 */
@Composable
internal fun PhotosSection(
    response: OrderPhotosResponse,
    onViewPhotos: () -> Unit,
) {
    Box(
        modifier = Modifier
            .fillMaxWidth()
            .clip(RoundedCornerShape(16.dp))
            .clickable(onClick = onViewPhotos),
    ) {
        Card {
            Row(verticalAlignment = Alignment.CenterVertically) {
                Text(
                    text = stringResource(R.string.order_photos_section_title),
                    style = MaterialTheme.typography.titleSmall.copy(fontWeight = FontWeight.SemiBold),
                    color = MaterialTheme.colorScheme.onBackground,
                    modifier = Modifier.weight(1f),
                )
                Text(
                    text = stringResource(R.string.order_photos_view_button),
                    style = MaterialTheme.typography.labelLarge.copy(fontWeight = FontWeight.SemiBold),
                    color = MaterialTheme.colorScheme.primary,
                )
                Spacer(Modifier.width(4.dp))
                Icon(
                    Icons.AutoMirrored.Outlined.ArrowForward,
                    contentDescription = null,
                    tint = MaterialTheme.colorScheme.primary,
                    modifier = Modifier.size(16.dp),
                )
            }
            Spacer(Modifier.height(10.dp))
            Row {
                PhotoCountPill(
                    text = stringResource(R.string.order_photos_summary_before, response.beforePhotoCount),
                )
                Spacer(Modifier.width(8.dp))
                PhotoCountPill(
                    text = stringResource(R.string.order_photos_summary_after, response.afterPhotoCount),
                )
            }
            Spacer(Modifier.height(12.dp))
            val previewThumbs = response.photos.take(6)
            LazyRow(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                items(previewThumbs) { photo ->
                    PhotoThumb(url = photo.blobUrl, size = 72.dp)
                }
            }
        }
    }
}

@Composable
private fun PhotoCountPill(text: String) {
    Text(
        text = text,
        style = MaterialTheme.typography.labelSmall.copy(fontWeight = FontWeight.SemiBold),
        color = MaterialTheme.colorScheme.onSurfaceVariant,
        modifier = Modifier
            .background(
                MaterialTheme.colorScheme.surfaceVariant,
                RoundedCornerShape(999.dp),
            )
            .padding(horizontal = 10.dp, vertical = 4.dp),
    )
}

@Composable
private fun PhotoThumb(url: String?, size: Dp) {
    Box(
        modifier = Modifier
            .size(size)
            .clip(RoundedCornerShape(8.dp))
            .background(MaterialTheme.colorScheme.surfaceVariant),
    ) {
        AsyncImage(
            model = url,
            contentDescription = null,
            contentScale = ContentScale.Crop,
            modifier = Modifier.fillMaxSize(),
        )
    }
}
