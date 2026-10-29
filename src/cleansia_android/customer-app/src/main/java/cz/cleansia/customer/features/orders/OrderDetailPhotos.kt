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
import cz.cleansia.core.ui.theme.primaryText
import cz.cleansia.customer.R
import cz.cleansia.customer.core.orders.OrderPhotosResponse
import cz.cleansia.customer.core.orders.OrderPhotoDto
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.heightIn
import androidx.compose.ui.text.style.TextOverflow

internal data class OrderPhotoGroup(val phase: Int, val photos: List<OrderPhotoDto>) {
    fun indexOfPhoto(id: String?): Int? = id?.takeIf { it.isNotBlank() }
        ?.let { selected -> photos.indexOfFirst { it.id == selected }.takeIf { it >= 0 } }
}

internal fun groupOrderPhotos(photos: List<OrderPhotoDto>): List<OrderPhotoGroup> =
    listOf(1, 2, 3, 0).map { phase ->
        OrderPhotoGroup(phase, photos.filter { photo ->
            if (phase == 0) photo.photoType !in listOf(1, 2, 3) else photo.photoType == phase
        })
    }.filter { it.phase in 1..2 || it.photos.isNotEmpty() }

@Composable
internal fun photoPhaseTitle(phase: Int): String = stringResource(when (phase) {
    1 -> R.string.order_photos_tab_before
    2 -> R.string.order_photos_tab_after
    3 -> R.string.order_photos_tab_entrance
    else -> R.string.order_photos_tab_other
})

@Composable
internal fun PhotosSection(response: OrderPhotosResponse, onViewPhotos: (Int, String?) -> Unit) {
    Card {
        Row(verticalAlignment = Alignment.CenterVertically) {
            Text(
                text = stringResource(R.string.order_photos_section_title),
                style = MaterialTheme.typography.titleSmall.copy(fontWeight = FontWeight.SemiBold),
                color = MaterialTheme.colorScheme.onBackground,
                modifier = Modifier.weight(1f),
            )
            cz.cleansia.core.ui.components.CleansiaTextButton(onClick = { onViewPhotos(1, null) }) {
                Text(stringResource(R.string.order_photos_view_button), maxLines = 1)
            }
        }
        LazyRow(horizontalArrangement = Arrangement.spacedBy(12.dp)) {
            items(groupOrderPhotos(response.photos).filter { it.photos.isNotEmpty() }) { group ->
                val phaseTitle = photoPhaseTitle(group.phase)
                val photo = group.photos.first()
                Column(Modifier.width(88.dp)) {
                    Text(
                        text = "$phaseTitle · ${group.photos.size}",
                        style = MaterialTheme.typography.labelMedium,
                        color = MaterialTheme.colorScheme.onSurfaceVariant,
                        maxLines = 2,
                        overflow = TextOverflow.Ellipsis,
                        modifier = Modifier.heightIn(min = 36.dp).padding(bottom = 6.dp),
                    )
                    PhotoThumb(photo.blobUrl, 88.dp, phaseTitle) { onViewPhotos(group.phase, photo.id) }
                }
            }
        }
    }
}

@Composable
private fun PhotoThumb(url: String?, size: Dp, phaseTitle: String, onClick: () -> Unit) {
    Box(
        modifier = Modifier.size(size).clip(RoundedCornerShape(8.dp))
            .background(MaterialTheme.colorScheme.surfaceVariant).clickable(onClick = onClick),
    ) {
        AsyncImage(model = url, contentDescription = phaseTitle, contentScale = ContentScale.Crop, modifier = Modifier.fillMaxSize())
    }
}
