package cz.cleansia.partner.features.media

import android.content.Intent
import android.content.pm.PackageManager
import android.net.Uri
import android.provider.Settings
import androidx.activity.compose.rememberLauncherForActivityResult
import androidx.activity.result.PickVisualMediaRequest
import androidx.activity.result.contract.ActivityResultContracts
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.navigationBarsPadding
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.width
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.outlined.PhotoCamera
import androidx.compose.material.icons.outlined.PhotoLibrary
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.Icon
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.ModalBottomSheet
import androidx.compose.material3.Text
import androidx.compose.material3.rememberModalBottomSheetState
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.vector.ImageVector
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.unit.dp
import androidx.core.content.FileProvider
import cz.cleansia.core.ui.components.CleansiaDialog
import cz.cleansia.partner.R
import java.io.File

/**
 * Opens the photo-source sheet. Held by the caller and invoked from a button's onClick.
 */
fun interface PhotoSourcePicker {
    fun open()
}

/**
 * Camera-or-gallery picking, shared by the two partner surfaces that take a photo: the job
 * before/after rails and the profile avatar.
 *
 * Extracted rather than inlined because there are two callers today and the interesting part is
 * not the sheet — it is the four states underneath it that are easy to get wrong once and
 * impossible to keep right twice: no camera hardware, permission not yet asked, permission
 * permanently denied, and a capture the user backed out of.
 *
 * **The camera writes to a file, not to the intent.** `TakePicture` hands back only a success
 * flag; the image arrives at the [FileProvider] URI passed in. The legacy `Bitmap` in the intent
 * extra is a thumbnail, which is why it is not used. The file lives in `cacheDir/camera/` and is
 * deliberately not deleted here — the avatar screen renders its preview from this same URI until
 * the cleaner saves. Android reclaims the cache dir under storage pressure.
 *
 * **The device may have no camera.** `required="false"` on the manifest's `uses-feature` keeps the
 * app installable on such a device, so the sheet is skipped entirely and the gallery opens
 * directly rather than offering an option that would fail.
 */
@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun rememberPhotoSourcePicker(
    onPickerUnavailable: () -> Unit = {},
    onPicked: (Uri) -> Unit,
): PhotoSourcePicker {
    val context = LocalContext.current
    val hasCamera = remember(context) {
        context.packageManager.hasSystemFeature(PackageManager.FEATURE_CAMERA_ANY)
    }

    var showSheet by remember { mutableStateOf(false) }
    var showPermissionDialog by remember { mutableStateOf(false) }
    var pendingCaptureUri by remember { mutableStateOf<Uri?>(null) }

    val pickFromGallery = rememberLauncherForActivityResult(
        contract = ActivityResultContracts.PickVisualMedia(),
    ) { uri: Uri? -> uri?.let(onPicked) }

    // A device with neither a photo picker nor a document provider THROWS rather than returning
    // empty, and an uncaught throw here takes the screen down. The avatar screen already carried
    // this guard; it lives here now so the job-photo rail gets it too.
    fun openGallery() {
        runCatching {
            pickFromGallery.launch(
                PickVisualMediaRequest(ActivityResultContracts.PickVisualMedia.ImageOnly),
            )
        }.onFailure { onPickerUnavailable() }
    }

    val capture = rememberLauncherForActivityResult(
        contract = ActivityResultContracts.TakePicture(),
    ) { saved: Boolean ->
        val uri = pendingCaptureUri
        pendingCaptureUri = null
        // false means the user backed out of the camera. The empty placeholder file stays behind
        // in the cache dir, which Android clears on its own — deleting it here would race the
        // system camera process, which may still hold the descriptor.
        if (saved && uri != null) onPicked(uri)
    }

    fun launchCamera() {
        val uri = runCatching { newCaptureUri(context) }.getOrNull()
        if (uri == null) {
            showPermissionDialog = true
            return
        }
        pendingCaptureUri = uri
        capture.launch(uri)
    }

    val requestCamera = rememberLauncherForActivityResult(
        contract = ActivityResultContracts.RequestPermission(),
    ) { granted: Boolean ->
        // A denial and a permanent denial are indistinguishable here, and both leave the cleaner
        // with no camera, so both get the same dialog pointing at Settings.
        if (granted) launchCamera() else showPermissionDialog = true
    }

    if (showSheet) {
        ModalBottomSheet(
            onDismissRequest = { showSheet = false },
            sheetState = rememberModalBottomSheetState(),
            containerColor = MaterialTheme.colorScheme.surface,
        ) {
            Column(Modifier.navigationBarsPadding().padding(bottom = 12.dp)) {
                Text(
                    text = stringResource(R.string.photo_source_title),
                    style = MaterialTheme.typography.labelSmall,
                    color = MaterialTheme.colorScheme.onSurfaceVariant,
                    modifier = Modifier.padding(horizontal = 24.dp, vertical = 8.dp),
                )
                SourceOption(
                    icon = Icons.Outlined.PhotoCamera,
                    label = stringResource(R.string.take_photo),
                    onClick = {
                        showSheet = false
                        requestCamera.launch(android.Manifest.permission.CAMERA)
                    },
                )
                SourceOption(
                    icon = Icons.Outlined.PhotoLibrary,
                    label = stringResource(R.string.choose_from_gallery),
                    onClick = {
                        showSheet = false
                        openGallery()
                    },
                )
            }
        }
    }

    if (showPermissionDialog) {
        CleansiaDialog(
            onDismiss = { showPermissionDialog = false },
            title = stringResource(R.string.camera_permission_title),
            message = stringResource(R.string.camera_permission_message),
            confirmLabel = stringResource(R.string.open_settings),
            dismissLabel = stringResource(R.string.cancel),
            onConfirm = {
                showPermissionDialog = false
                context.startActivity(
                    Intent(
                        Settings.ACTION_APPLICATION_DETAILS_SETTINGS,
                        Uri.fromParts("package", context.packageName, null),
                    ),
                )
            },
        )
    }

    return PhotoSourcePicker {
        if (hasCamera) showSheet = true else openGallery()
    }
}

/**
 * A fresh file in `cacheDir/camera/`, exposed through the FileProvider the invoice viewer already
 * uses. The authority must match `AndroidManifest.xml`, which builds it from `${applicationId}` —
 * so `packageName`, not a literal, or debug builds break on their `.debug` suffix.
 */
private fun newCaptureUri(context: android.content.Context): Uri {
    val directory = File(context.cacheDir, "camera").apply { mkdirs() }
    val file = File(directory, "capture-${System.currentTimeMillis()}.jpg")
    return FileProvider.getUriForFile(context, "${context.packageName}.fileprovider", file)
}

@Composable
private fun SourceOption(
    icon: ImageVector,
    label: String,
    onClick: () -> Unit,
    tint: Color = MaterialTheme.colorScheme.onSurface,
) {
    Row(
        modifier = Modifier
            .fillMaxWidth()
            .clickable(onClick = onClick)
            .padding(horizontal = 24.dp, vertical = 16.dp),
        verticalAlignment = Alignment.CenterVertically,
    ) {
        Icon(icon, contentDescription = null, tint = tint, modifier = Modifier.size(22.dp))
        Spacer(Modifier.width(16.dp))
        Text(label, style = MaterialTheme.typography.bodyLarge, color = tint)
    }
}
