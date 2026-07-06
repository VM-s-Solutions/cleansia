package cz.cleansia.partner.features.orders

import android.net.Uri
import android.util.Base64
import androidx.activity.compose.rememberLauncherForActivityResult
import androidx.activity.result.contract.ActivityResultContracts
import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
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
import androidx.compose.material.icons.outlined.AddAPhoto
import androidx.compose.material.icons.outlined.BrokenImage
import androidx.compose.material.icons.outlined.Close
import androidx.compose.material.icons.outlined.PhotoCamera
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.layout.ContentScale
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.hilt.navigation.compose.hiltViewModel
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import coil3.compose.AsyncImage
import coil3.compose.SubcomposeAsyncImage
import coil3.request.ImageRequest
import coil3.request.crossfade
import cz.cleansia.core.ui.theme.Spacing
import cz.cleansia.partner.BuildConfig
import cz.cleansia.partner.R
import cz.cleansia.partner.api.model.GetOrderPhotosOrderPhotoDto
import cz.cleansia.partner.api.model.PhotoType

/**
 * Photos block embedded in [OrderDetailScreen]. Two horizontal rails
 * (Before / After), each shows existing photos and an "add" tile that
 * opens the system image picker. Per-photo delete via the close icon.
 *
 * Uses its own ViewModel keyed on the same orderId from the parent
 * navigation arguments so this composable can be dropped in without
 * the parent screen needing to know about photo state.
 */
@Composable
fun PhotosSection(
    onPhotosChanged: () -> Unit = {},
    canUploadBefore: Boolean = false,
    canUploadAfter: Boolean = false,
    viewModel: OrderPhotosViewModel = hiltViewModel(),
) {
    val uiState by viewModel.uiState.collectAsStateWithLifecycle()
    val mutation by viewModel.mutation.collectAsStateWithLifecycle()
    val mutationVersion by viewModel.mutationVersion.collectAsStateWithLifecycle()

    // Errors come through the global SnackbarController bus (pushed
    // by the VM) — no local SnackbarHostState here. Just keep the
    // upload/delete bump going to the parent so the order refreshes
    // and `hasAfterPhotos` stays live.
    LaunchedEffect(mutationVersion) {
        if (mutationVersion > 0) {
            onPhotosChanged()
        }
    }

    OrderSectionCard(
        title = stringResource(R.string.photos),
        icon = Icons.Outlined.PhotoCamera,
    ) {
        val photos = (uiState as? OrderPhotosUiState.Loaded)?.photos.orEmpty()

        if (uiState is OrderPhotosUiState.Loading) {
            Box(
                modifier = Modifier
                    .fillMaxWidth()
                    .padding(Spacing.M),
                contentAlignment = Alignment.Center,
            ) {
                CircularProgressIndicator()
            }
            return@OrderSectionCard
        }

        Column(verticalArrangement = Arrangement.spacedBy(Spacing.M)) {
            PhotoRail(
                title = stringResource(R.string.before),
                type = PhotoType._1,
                photos = photos.filter { it.photoType == PhotoType._1 },
                isUploading = mutation.isUploading,
                deletingId = mutation.deletingId,
                onUpload = viewModel::upload,
                onDelete = viewModel::delete,
                isReadOnly = !canUploadBefore,
            )
            PhotoRail(
                title = stringResource(R.string.after),
                type = PhotoType._2,
                photos = photos.filter { it.photoType == PhotoType._2 },
                isUploading = mutation.isUploading,
                deletingId = mutation.deletingId,
                onUpload = viewModel::upload,
                onDelete = viewModel::delete,
                isReadOnly = !canUploadAfter,
            )
        }
    }
}

@Composable
private fun PhotoRail(
    title: String,
    type: PhotoType,
    photos: List<GetOrderPhotosOrderPhotoDto>,
    isUploading: Boolean,
    deletingId: String?,
    onUpload: (PhotoType, String, String, String) -> Unit,
    onDelete: (String) -> Unit,
    isReadOnly: Boolean,
) {
    val context = LocalContext.current
    var pickingForType by remember { mutableStateOf<PhotoType?>(null) }

    val pickImage = rememberLauncherForActivityResult(
        contract = ActivityResultContracts.GetContent(),
    ) { uri: Uri? ->
        val target = pickingForType
        pickingForType = null
        if (uri == null || target == null) return@rememberLauncherForActivityResult
        val resolver = context.contentResolver
        val name = uri.lastPathSegment?.substringAfterLast('/') ?: "photo.jpg"
        val contentType = resolver.getType(uri) ?: "image/jpeg"
        val bytes = runCatching { resolver.openInputStream(uri)?.use { it.readBytes() } }.getOrNull()
            ?: return@rememberLauncherForActivityResult
        // Base64 encoding can be slow for multi-MB images — keep it off the main thread.
        // The VM caller will hit the network anyway; this is just decode prep.
        val base64 = Base64.encodeToString(bytes, Base64.NO_WRAP)
        onUpload(target, name, contentType, base64)
    }

    // Group label — same labelSmall + onSurfaceVariant treatment used
    // by [CleaningChecklist]'s "Services / Packages / Extras" group
    // labels so all sectioned cards look consistent.
    Text(
        text = title,
        style = MaterialTheme.typography.labelSmall,
        color = MaterialTheme.colorScheme.onSurfaceVariant,
    )
    Spacer(Modifier.height(8.dp))

    // Read-only + empty: nothing to render in the rail. Show a tiny
    // muted placeholder instead of a bare gap so the section still
    // reads as intentional (vs broken / missing content).
    if (isReadOnly && photos.isEmpty()) {
        Text(
            text = stringResource(R.string.photos_none_recorded),
            style = MaterialTheme.typography.bodySmall,
            color = MaterialTheme.colorScheme.onSurfaceVariant,
        )
        return
    }

    LazyRow(
        modifier = Modifier.fillMaxWidth(),
        horizontalArrangement = Arrangement.spacedBy(Spacing.S),
    ) {
        // Add tile sits FIRST in the row — the cleaner shouldn't have
        // to scroll past existing photos to find the upload affordance,
        // and "+" being the first thing they see also reads as the
        // primary action of the row. Hidden in read-only mode (order
        // is terminal — Completed / Cancelled).
        if (!isReadOnly) {
            item {
                AddPhotoTile(
                    isUploading = isUploading,
                    onClick = {
                        pickingForType = type
                        pickImage.launch("image/*")
                    },
                )
            }
        }
        items(photos, key = { it.id.orEmpty() }) { photo ->
            PhotoTile(
                photo = photo,
                isDeleting = deletingId == photo.id,
                onDelete = { photo.id?.let(onDelete) },
                isReadOnly = isReadOnly,
            )
        }
    }
}

@Composable
private fun PhotoTile(
    photo: GetOrderPhotosOrderPhotoDto,
    isDeleting: Boolean,
    onDelete: () -> Unit,
    isReadOnly: Boolean = false,
) {
    val context = LocalContext.current
    Box(
        modifier = Modifier
            .size(80.dp)
            .clip(RoundedCornerShape(12.dp))
            .background(MaterialTheme.colorScheme.surfaceVariant),
    ) {
        // SubcomposeAsyncImage so we can render distinct loading /
        // error states (vs the silent blank that AsyncImage falls
        // back to). Explicit ImageRequest with crossfade so the photo
        // fades in on load. listener logs failures to logcat — debug
        // builds only, SAS query stripped so the signed token never
        // hits the log.
        SubcomposeAsyncImage(
            model = ImageRequest.Builder(context)
                .data(photo.blobUrl)
                .crossfade(true)
                .listener(
                    onError = { _, result ->
                        if (BuildConfig.DEBUG) {
                            android.util.Log.w(
                                "PhotoTile",
                                "Photo load failed url=${photo.blobUrl?.substringBefore('?')}",
                                result.throwable,
                            )
                        }
                    },
                )
                .build(),
            contentDescription = null,
            modifier = Modifier.fillMaxSize(),
            contentScale = ContentScale.Crop,
            loading = {
                Box(Modifier.fillMaxSize(), contentAlignment = Alignment.Center) {
                    CircularProgressIndicator(
                        modifier = Modifier.size(20.dp),
                        strokeWidth = 2.dp,
                    )
                }
            },
            error = {
                // Visible failure state so the cleaner sees the tile
                // didn't load (instead of an invisible empty square).
                Box(Modifier.fillMaxSize(), contentAlignment = Alignment.Center) {
                    Icon(
                        imageVector = Icons.Outlined.BrokenImage,
                        contentDescription = null,
                        tint = MaterialTheme.colorScheme.onSurfaceVariant,
                        modifier = Modifier.size(28.dp),
                    )
                }
            },
        )
        // Delete affordance is hidden once the order is terminal —
        // photos at that point are a record of the visit, not user-
        // editable content. Cleaner can't accidentally remove proof
        // of work after the fact.
        if (!isReadOnly) {
            IconButton(
                onClick = onDelete,
                modifier = Modifier
                    .align(Alignment.TopEnd)
                    .size(28.dp),
                enabled = !isDeleting,
            ) {
                if (isDeleting) {
                    CircularProgressIndicator(modifier = Modifier.size(16.dp), strokeWidth = 2.dp)
                } else {
                    Icon(
                        imageVector = Icons.Outlined.Close,
                        contentDescription = stringResource(R.string.delete_photo),
                        tint = MaterialTheme.colorScheme.onSurface,
                    )
                }
            }
        }
    }
}

/**
 * Add-photo tile. Sized to match [PhotoTile] (80dp square) so the rail
 * has a consistent grid rhythm. Reads as a tap target via the
 * brand-tinted background + brand outline + camera icon + label —
 * but the chrome is intentionally restrained so it sits comfortably
 * next to existing photo thumbnails rather than dominating them.
 */
@Composable
private fun AddPhotoTile(isUploading: Boolean, onClick: () -> Unit) {
    val borderColor = MaterialTheme.colorScheme.primary
    val tint = MaterialTheme.colorScheme.primary
    val bg = MaterialTheme.colorScheme.primary.copy(alpha = 0.08f)

    Box(
        modifier = Modifier
            .size(80.dp)
            .clip(RoundedCornerShape(12.dp))
            .background(bg)
            .border(
                width = 1.dp,
                color = borderColor.copy(alpha = 0.5f),
                shape = RoundedCornerShape(12.dp),
            )
            .clickable(enabled = !isUploading) { onClick() },
        contentAlignment = Alignment.Center,
    ) {
        if (isUploading) {
            CircularProgressIndicator(
                modifier = Modifier.size(24.dp),
                color = tint,
                strokeWidth = 2.dp,
            )
        } else {
            Column(
                horizontalAlignment = Alignment.CenterHorizontally,
                verticalArrangement = Arrangement.spacedBy(2.dp),
            ) {
                Icon(
                    imageVector = Icons.Outlined.AddAPhoto,
                    contentDescription = stringResource(R.string.add_photo),
                    tint = tint,
                    modifier = Modifier.size(22.dp),
                )
                Text(
                    text = stringResource(R.string.add_photo),
                    style = MaterialTheme.typography.labelSmall.copy(fontWeight = FontWeight.SemiBold),
                    color = tint,
                )
            }
        }
    }
}
