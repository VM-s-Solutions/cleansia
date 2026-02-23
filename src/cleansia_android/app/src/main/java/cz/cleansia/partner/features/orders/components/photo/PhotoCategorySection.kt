package cz.cleansia.partner.features.orders.components.photo

import android.net.Uri
import androidx.activity.compose.rememberLauncherForActivityResult
import androidx.activity.result.contract.ActivityResultContracts
import androidx.compose.animation.animateColorAsState
import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.PaddingValues
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.lazy.LazyRow
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.Add
import androidx.compose.material.icons.filled.CheckCircle
import androidx.compose.material.icons.filled.Image
import androidx.compose.material3.AlertDialog
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.FilledTonalButton
import androidx.compose.material3.Icon
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.unit.dp
import cz.cleansia.partner.R
import cz.cleansia.partner.domain.models.orders.OrderPhoto
import cz.cleansia.partner.domain.models.orders.PhotoType
import cz.cleansia.partner.ui.theme.CleansiaColors

@Composable
internal fun PhotoCategorySection(
    title: String,
    photos: List<OrderPhoto>,
    photoType: PhotoType,
    isUploading: Boolean,
    canUpload: Boolean,
    isMandatory: Boolean,
    showValidationError: Boolean,
    onUploadPhoto: (ByteArray, String, PhotoType) -> Unit,
    onUploadMultiplePhotos: (List<Pair<ByteArray, String>>, PhotoType) -> Unit,
    onDeletePhoto: (String) -> Unit
) {
    val context = LocalContext.current
    var showSourceDialog by remember { mutableStateOf(false) }
    var selectedPhotoForPreview by remember { mutableStateOf<OrderPhoto?>(null) }
    var photoToDelete by remember { mutableStateOf<OrderPhoto?>(null) }

    val hasPhotos = photos.isNotEmpty()

    // Border color based on validation state
    val borderColor by animateColorAsState(
        targetValue = when {
            showValidationError -> MaterialTheme.colorScheme.error
            hasPhotos -> CleansiaColors.success
            else -> MaterialTheme.colorScheme.outlineVariant
        },
        label = "borderColor"
    )

    // Multi-photo gallery picker
    val galleryLauncher = rememberLauncherForActivityResult(
        contract = ActivityResultContracts.GetMultipleContents()
    ) { uris: List<Uri> ->
        if (uris.size == 1) {
            // Single photo - use single upload
            uris.first().let { uri ->
                context.contentResolver.openInputStream(uri)?.use { inputStream ->
                    val bytes = inputStream.readBytes()
                    val fileName = uri.lastPathSegment ?: "photo_${System.currentTimeMillis()}.jpg"
                    onUploadPhoto(bytes, fileName, photoType)
                }
            }
        } else if (uris.size > 1) {
            // Multiple photos - use batch upload
            val photosData = uris.mapNotNull { uri ->
                context.contentResolver.openInputStream(uri)?.use { inputStream ->
                    val bytes = inputStream.readBytes()
                    val fileName = uri.lastPathSegment ?: "photo_${System.currentTimeMillis()}.jpg"
                    Pair(bytes, fileName)
                }
            }
            if (photosData.isNotEmpty()) {
                onUploadMultiplePhotos(photosData, photoType)
            }
        }
    }

    // Camera capture
    val cameraLauncher = rememberLauncherForActivityResult(
        contract = ActivityResultContracts.TakePicturePreview()
    ) { bitmap ->
        bitmap?.let {
            val stream = java.io.ByteArrayOutputStream()
            it.compress(android.graphics.Bitmap.CompressFormat.JPEG, 90, stream)
            val bytes = stream.toByteArray()
            val fileName = "photo_${System.currentTimeMillis()}.jpg"
            onUploadPhoto(bytes, fileName, photoType)
        }
    }

    // Source selection dialog
    if (showSourceDialog) {
        PhotoSourceDialog(
            onGallerySelected = {
                showSourceDialog = false
                galleryLauncher.launch("image/*")
            },
            onCameraSelected = {
                showSourceDialog = false
                cameraLauncher.launch(null)
            },
            onDismiss = { showSourceDialog = false }
        )
    }

    // Photo preview dialog
    selectedPhotoForPreview?.let { photo ->
        PhotoPreviewDialog(
            photo = photo,
            onDismiss = { selectedPhotoForPreview = null },
            onDelete = if (canUpload) {
                {
                    photoToDelete = photo
                    selectedPhotoForPreview = null
                }
            } else null
        )
    }

    // Delete confirmation dialog
    photoToDelete?.let { photo ->
        AlertDialog(
            onDismissRequest = { photoToDelete = null },
            title = { Text(stringResource(R.string.delete_photo)) },
            text = { Text(stringResource(R.string.delete_photo_confirm)) },
            confirmButton = {
                TextButton(
                    onClick = {
                        onDeletePhoto(photo.id)
                        photoToDelete = null
                    }
                ) {
                    Text(stringResource(R.string.delete), color = MaterialTheme.colorScheme.error)
                }
            },
            dismissButton = {
                TextButton(onClick = { photoToDelete = null }) {
                    Text(stringResource(R.string.cancel))
                }
            }
        )
    }

    Column(
        modifier = Modifier
            .fillMaxWidth()
            .border(
                width = 1.dp,
                color = borderColor,
                shape = RoundedCornerShape(12.dp)
            )
            .clip(RoundedCornerShape(12.dp))
            .background(MaterialTheme.colorScheme.surfaceVariant.copy(alpha = 0.3f))
            .padding(12.dp)
    ) {
        // Section Header
        Row(
            modifier = Modifier.fillMaxWidth(),
            horizontalArrangement = Arrangement.SpaceBetween,
            verticalAlignment = Alignment.CenterVertically
        ) {
            Row(verticalAlignment = Alignment.CenterVertically) {
                Text(
                    text = title,
                    style = MaterialTheme.typography.titleSmall,
                    fontWeight = FontWeight.SemiBold,
                    color = MaterialTheme.colorScheme.onSurface
                )

                if (isMandatory) {
                    Text(
                        text = " *",
                        style = MaterialTheme.typography.titleSmall,
                        color = MaterialTheme.colorScheme.error
                    )
                }

                Spacer(modifier = Modifier.width(8.dp))

                // Status indicator
                if (hasPhotos) {
                    Icon(
                        imageVector = Icons.Default.CheckCircle,
                        contentDescription = null,
                        tint = CleansiaColors.success,
                        modifier = Modifier.size(18.dp)
                    )
                    Text(
                        text = " (${photos.size})",
                        style = MaterialTheme.typography.bodySmall,
                        color = MaterialTheme.colorScheme.onSurfaceVariant
                    )
                }
            }

            if (canUpload) {
                FilledTonalButton(
                    onClick = { showSourceDialog = true },
                    enabled = !isUploading,
                    contentPadding = PaddingValues(horizontal = 12.dp, vertical = 6.dp)
                ) {
                    if (isUploading) {
                        CircularProgressIndicator(
                            modifier = Modifier.size(14.dp),
                            strokeWidth = 2.dp
                        )
                    } else {
                        Icon(
                            imageVector = Icons.Default.Add,
                            contentDescription = null,
                            modifier = Modifier.size(16.dp)
                        )
                    }
                    Spacer(modifier = Modifier.width(4.dp))
                    Text(
                        text = stringResource(R.string.add_photo),
                        style = MaterialTheme.typography.labelMedium
                    )
                }
            }
        }

        Spacer(modifier = Modifier.height(8.dp))

        if (photos.isEmpty()) {
            // Empty state
            Box(
                modifier = Modifier
                    .fillMaxWidth()
                    .height(80.dp)
                    .clip(RoundedCornerShape(8.dp))
                    .background(
                        if (showValidationError)
                            MaterialTheme.colorScheme.errorContainer.copy(alpha = 0.3f)
                        else
                            MaterialTheme.colorScheme.surfaceVariant.copy(alpha = 0.5f)
                    ),
                contentAlignment = Alignment.Center
            ) {
                Column(
                    horizontalAlignment = Alignment.CenterHorizontally
                ) {
                    Icon(
                        imageVector = Icons.Default.Image,
                        contentDescription = null,
                        tint = if (showValidationError)
                            MaterialTheme.colorScheme.error.copy(alpha = 0.6f)
                        else
                            MaterialTheme.colorScheme.onSurfaceVariant.copy(alpha = 0.5f),
                        modifier = Modifier.size(24.dp)
                    )
                    Spacer(modifier = Modifier.height(4.dp))
                    Text(
                        text = if (photoType == PhotoType.BEFORE)
                            stringResource(R.string.no_before_photos)
                        else
                            stringResource(R.string.no_after_photos),
                        style = MaterialTheme.typography.bodySmall,
                        color = if (showValidationError)
                            MaterialTheme.colorScheme.error.copy(alpha = 0.8f)
                        else
                            MaterialTheme.colorScheme.onSurfaceVariant,
                        textAlign = TextAlign.Center
                    )
                }
            }
        } else {
            // Photo row
            LazyRow(
                horizontalArrangement = Arrangement.spacedBy(8.dp)
            ) {
                items(photos, key = { it.id }) { photo ->
                    PhotoThumbnail(
                        photo = photo,
                        onClick = { selectedPhotoForPreview = photo },
                        onDelete = if (canUpload) {
                            { photoToDelete = photo }
                        } else null
                    )
                }
            }
        }
    }
}
