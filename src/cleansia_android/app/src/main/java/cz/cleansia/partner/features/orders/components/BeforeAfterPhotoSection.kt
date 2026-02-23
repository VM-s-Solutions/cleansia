package cz.cleansia.partner.features.orders.components

import androidx.compose.foundation.background
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.PhotoLibrary
import androidx.compose.material.icons.filled.Warning
import androidx.compose.material3.Card
import androidx.compose.material3.CardDefaults
import androidx.compose.material3.Icon
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import cz.cleansia.partner.R
import cz.cleansia.partner.domain.models.orders.OrderPhoto
import cz.cleansia.partner.domain.models.orders.PhotoType
import cz.cleansia.partner.features.orders.components.photo.PhotoCategorySection

/**
 * A photo section component that displays before and after photos separately
 * with mandatory upload indicators for order completion
 */
@Composable
fun BeforeAfterPhotoSection(
    beforePhotos: List<OrderPhoto>,
    afterPhotos: List<OrderPhoto>,
    isUploading: Boolean,
    canUpload: Boolean,
    showValidation: Boolean,
    onUploadPhoto: (ByteArray, String, PhotoType) -> Unit,
    onUploadMultiplePhotos: (List<Pair<ByteArray, String>>, PhotoType) -> Unit = { _, _ -> },
    onDeletePhoto: (String) -> Unit,
    modifier: Modifier = Modifier
) {
    val hasBeforePhotos = beforePhotos.isNotEmpty()
    val hasAfterPhotos = afterPhotos.isNotEmpty()

    Card(
        modifier = modifier.fillMaxWidth(),
        colors = CardDefaults.cardColors(
            containerColor = MaterialTheme.colorScheme.surface
        ),
        elevation = CardDefaults.cardElevation(defaultElevation = 1.dp)
    ) {
        Column(
            modifier = Modifier.padding(16.dp),
            verticalArrangement = Arrangement.spacedBy(16.dp)
        ) {
            // Header
            Row(
                modifier = Modifier.fillMaxWidth(),
                verticalAlignment = Alignment.CenterVertically
            ) {
                Icon(
                    imageVector = Icons.Default.PhotoLibrary,
                    contentDescription = null,
                    tint = MaterialTheme.colorScheme.primary,
                    modifier = Modifier.size(20.dp)
                )
                Spacer(modifier = Modifier.width(8.dp))
                Text(
                    text = stringResource(R.string.photos),
                    style = MaterialTheme.typography.titleMedium,
                    fontWeight = FontWeight.SemiBold,
                    color = MaterialTheme.colorScheme.onSurface
                )
            }

            // Validation warning
            if (showValidation && (!hasBeforePhotos || !hasAfterPhotos)) {
                Row(
                    modifier = Modifier
                        .fillMaxWidth()
                        .clip(RoundedCornerShape(8.dp))
                        .background(MaterialTheme.colorScheme.errorContainer)
                        .padding(12.dp),
                    verticalAlignment = Alignment.CenterVertically
                ) {
                    Icon(
                        imageVector = Icons.Default.Warning,
                        contentDescription = null,
                        tint = MaterialTheme.colorScheme.onErrorContainer,
                        modifier = Modifier.size(20.dp)
                    )
                    Spacer(modifier = Modifier.width(8.dp))
                    Text(
                        text = stringResource(R.string.photos_required_warning),
                        style = MaterialTheme.typography.bodySmall,
                        color = MaterialTheme.colorScheme.onErrorContainer
                    )
                }
            }

            // Before Photos Section
            PhotoCategorySection(
                title = stringResource(R.string.photos_before),
                photos = beforePhotos,
                photoType = PhotoType.BEFORE,
                isUploading = isUploading,
                canUpload = canUpload,
                isMandatory = true,
                showValidationError = showValidation && !hasBeforePhotos,
                onUploadPhoto = onUploadPhoto,
                onUploadMultiplePhotos = onUploadMultiplePhotos,
                onDeletePhoto = onDeletePhoto
            )

            // After Photos Section
            PhotoCategorySection(
                title = stringResource(R.string.photos_after),
                photos = afterPhotos,
                photoType = PhotoType.AFTER,
                isUploading = isUploading,
                canUpload = canUpload,
                isMandatory = true,
                showValidationError = showValidation && !hasAfterPhotos,
                onUploadPhoto = onUploadPhoto,
                onUploadMultiplePhotos = onUploadMultiplePhotos,
                onDeletePhoto = onDeletePhoto
            )
        }
    }
}
