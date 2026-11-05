package cz.cleansia.partner.features.orders

import androidx.compose.foundation.background
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.BoxWithConstraints
import androidx.compose.foundation.layout.ExperimentalLayoutApi
import androidx.compose.foundation.layout.FlowRow
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.outlined.BrokenImage
import androidx.compose.material.icons.outlined.Close
import androidx.compose.material.icons.outlined.PhotoCamera
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.AlertDialog
import androidx.compose.material3.Icon
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.draw.drawBehind
import androidx.compose.ui.geometry.CornerRadius
import androidx.compose.ui.geometry.Offset
import androidx.compose.ui.geometry.Size
import androidx.compose.ui.graphics.PathEffect
import androidx.compose.ui.graphics.drawscope.Stroke
import androidx.compose.ui.layout.ContentScale
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.semantics.Role
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.unit.dp
import coil3.compose.AsyncImage
import coil3.compose.SubcomposeAsyncImage
import coil3.request.CachePolicy
import coil3.request.ImageRequest
import coil3.request.crossfade
import cz.cleansia.core.ui.theme.primaryText
import cz.cleansia.core.ui.components.CleansiaTextButton
import cz.cleansia.partner.BuildConfig
import cz.cleansia.partner.R
import cz.cleansia.partner.api.model.GetOrderPhotosOrderPhotoDto
import cz.cleansia.partner.api.model.OrderStatus
import cz.cleansia.partner.api.model.PhotoType
import cz.cleansia.partner.ui.theme.Slate400
import cz.cleansia.partner.ui.theme.Slate500
import cz.cleansia.core.format.formatOrderTime

/**
 * The job's photos: *Before · 2* and *After · 0* in two fixed columns when two 136 columns fit, stacked otherwise;
 * 64 thumbnails with their capture time, one she can still remove carrying its x on a disc; an empty after slot is the
 * one dashed box, *Needed before you can complete*.
 * Stateless: the screen holds [OrderPhotosViewModel] and opens the camera.
 */
@Composable
fun PhotosBlock(
    photos: List<GetOrderPhotosOrderPhotoDto>,
    mutation: PhotoMutationState,
    canUploadBefore: Boolean,
    canUploadAfter: Boolean,
    onAdd: (PhotoType) -> Unit,
    onDelete: (String) -> Unit,
    modifier: Modifier = Modifier,
    afterNeeded: Boolean = false,
) {
    Column(modifier.fillMaxWidth(), verticalArrangement = Arrangement.spacedBy(8.dp)) {
        PartHeading(stringResource(R.string.photos))
        val before: @Composable (Modifier) -> Unit = {
            PhotoGroup(stringResource(R.string.before), photos.filter { it.photoType == PhotoType._1 }, mutation, canUploadBefore, null, { onAdd(PhotoType._1) }, onDelete, it)
        }
        val after: @Composable (Modifier) -> Unit = {
            PhotoGroup(
                stringResource(R.string.after),
                photos.filter { it.photoType == PhotoType._2 },
                mutation,
                canUploadAfter,
                OrdersBoardRules.tie(stringResource(R.string.photos_after_needed)).takeIf { afterNeeded },
                { onAdd(PhotoType._2) },
                onDelete,
                it,
            )
        }
        BoxWithConstraints(Modifier.fillMaxWidth()) {
            if (maxWidth >= PhotoColumnsMinWidth) {
                Row(horizontalArrangement = Arrangement.spacedBy(PhotoColumnGap)) {
                    before(Modifier.weight(1f))
                    after(Modifier.weight(1f))
                }
            } else {
                Column(verticalArrangement = Arrangement.spacedBy(12.dp)) {
                    before(Modifier.fillMaxWidth())
                    after(Modifier.fillMaxWidth())
                }
            }
        }
    }
}

/** Before and After stand side by side from two 136 columns and their gap; Note and Issue share the grid. */
internal val PhotoColumnGap = 12.dp
internal val PhotoColumnsMinWidth = 136.dp * 2 + PhotoColumnGap

/**
 * The server's photo windows (`OrderPhoto.MayBeAddedAt`): before and entrance photos from
 * Confirmed through InProgress, after photos only while InProgress. Outside its window a rail is
 * read-only.
 */
internal fun photoWindowOpen(type: PhotoType, status: OrderStatus?): Boolean = when (type) {
    PhotoType._1, PhotoType._3 ->
        status == OrderStatus._2 || status == OrderStatus._3 || status == OrderStatus._4
    PhotoType._2 -> status == OrderStatus._4
}

/** *Before · 2*: the thumbnails with their times, and the dashed slot while the server takes this kind of photo. */
@OptIn(ExperimentalLayoutApi::class)
@Composable
internal fun PhotoGroup(
    title: String,
    photos: List<GetOrderPhotosOrderPhotoDto>,
    mutation: PhotoMutationState,
    canUpload: Boolean,
    slotNote: String?,
    onAdd: () -> Unit,
    onDelete: (String) -> Unit,
    modifier: Modifier = Modifier,
) {
    var pendingDeleteId by remember { mutableStateOf<String?>(null) }
    pendingDeleteId?.let { photoId ->
        AlertDialog(
            onDismissRequest = { pendingDeleteId = null },
            title = { Text(stringResource(R.string.delete_photo)) },
            text = { Text(stringResource(R.string.delete_photo_confirm)) },
            confirmButton = {
                CleansiaTextButton(onClick = {
                    pendingDeleteId = null
                    if (canUpload && mutation.deletingId == null && photos.any { it.id == photoId }) onDelete(photoId)
                }) { Text(stringResource(R.string.delete_photo), color = MaterialTheme.colorScheme.error) }
            },
            dismissButton = {
                CleansiaTextButton(onClick = { pendingDeleteId = null }) { Text(stringResource(R.string.cancel)) }
            },
        )
    }
    Column(modifier, verticalArrangement = Arrangement.spacedBy(6.dp)) {
        WordsLabel("$title${OrdersBoardRules.SEP}${photos.size}")
        FlowRow(horizontalArrangement = Arrangement.spacedBy(8.dp), verticalArrangement = Arrangement.spacedBy(8.dp)) {
            photos.forEach { photo ->
                Column(Modifier.width(64.dp)) {
                    PhotoTile(photo = photo, isDeleting = mutation.deletingId == photo.id, onDelete = { pendingDeleteId = photo.id }, isReadOnly = !canUpload)
                    Text(
                        text = photo.capturedAt?.let { runCatching { formatOrderTime(it) }.getOrNull() }.orEmpty(),
                        style = MaterialTheme.typography.bodySmall.copy(fontFeatureSettings = "tnum"),
                        color = MaterialTheme.colorScheme.onSurfaceVariant,
                        textAlign = TextAlign.Center,
                        modifier = Modifier.fillMaxWidth().padding(top = 2.dp),
                    )
                }
            }
            if (canUpload && (photos.isEmpty() || slotNote == null)) AddSlot(isUploading = mutation.isUploading, note = slotNote.takeIf { photos.isEmpty() }, onClick = onAdd)
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
            .size(64.dp)
            .clip(RoundedCornerShape(12.dp))
            .background(MaterialTheme.colorScheme.surfaceVariant),
    ) {
        // SubcomposeAsyncImage so we can render distinct loading /
        // error states (vs the silent blank that AsyncImage falls
        // back to). Explicit ImageRequest with crossfade so the photo
        // fades in on load. listener logs failures to logcat — debug
        // builds only, SAS query stripped so the signed token never
        // hits the log.
        //
        // No disk cache, so a job photo never lands in the app's cache dir; Coil then also sends
        // `no-cache, no-store`. The network policy must stay enabled: with both disabled Coil asks
        // for `only-if-cached` and every tile fails with a 504.
        SubcomposeAsyncImage(
            model = ImageRequest.Builder(context)
                .data(photo.blobUrl)
                .diskCachePolicy(CachePolicy.DISABLED)
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
            Box(
                modifier = Modifier
                    .align(Alignment.TopEnd)
                    .size(30.dp)
                    .clickable(enabled = !isDeleting, role = Role.Button, onClickLabel = stringResource(R.string.delete_photo), onClick = onDelete),
                contentAlignment = Alignment.Center,
            ) {
                Box(Modifier.size(22.dp).background(MaterialTheme.colorScheme.surface, CircleShape), contentAlignment = Alignment.Center) {
                    if (isDeleting) {
                        CircularProgressIndicator(modifier = Modifier.size(14.dp), strokeWidth = 2.dp)
                    } else {
                        Icon(
                            imageVector = Icons.Outlined.Close,
                            contentDescription = stringResource(R.string.delete_photo),
                            tint = MaterialTheme.colorScheme.onSurface,
                            modifier = Modifier.size(12.dp),
                        )
                    }
                }
            }
        }
    }
}

/** The one dashed box (1.5 slate): camera and *Add* in the text blue, what it is needed for under it. */
@Composable
private fun AddSlot(isUploading: Boolean, note: String?, onClick: () -> Unit) {
    val edge = if (isDarkScheme()) Slate400 else Slate500
    val tint = primaryText()
    Column(Modifier.width(if (note != null) 136.dp else 64.dp), verticalArrangement = Arrangement.spacedBy(4.dp)) {
        Column(
            modifier = Modifier
                .size(64.dp)
                .clip(RoundedCornerShape(12.dp))
                .drawBehind {
                    val stroke = 1.5.dp.toPx()
                    drawRoundRect(
                        color = edge,
                        topLeft = Offset(stroke / 2, stroke / 2),
                        size = Size(size.width - stroke, size.height - stroke),
                        cornerRadius = CornerRadius(12.dp.toPx() - stroke / 2),
                        style = Stroke(width = stroke, pathEffect = PathEffect.dashPathEffect(floatArrayOf(4.dp.toPx(), 3.dp.toPx()))),
                    )
                }
                .clickable(enabled = !isUploading, role = Role.Button, onClick = onClick),
            horizontalAlignment = Alignment.CenterHorizontally,
            verticalArrangement = Arrangement.Center,
        ) {
            if (isUploading) {
                CircularProgressIndicator(modifier = Modifier.size(22.dp), color = tint, strokeWidth = 2.dp)
            } else {
                Icon(Icons.Outlined.PhotoCamera, contentDescription = null, tint = tint, modifier = Modifier.size(20.dp))
                Text(text = stringResource(R.string.photos_add), style = MaterialTheme.typography.labelLarge, color = tint)
            }
        }
        note?.let {
            Text(text = it, style = MaterialTheme.typography.bodySmall, color = MaterialTheme.colorScheme.onSurfaceVariant)
        }
    }
}
