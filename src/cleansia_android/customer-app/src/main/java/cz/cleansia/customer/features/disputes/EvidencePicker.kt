package cz.cleansia.customer.features.disputes

import android.net.Uri
import androidx.activity.compose.ManagedActivityResultLauncher
import androidx.activity.compose.rememberLauncherForActivityResult
import androidx.activity.result.contract.ActivityResultContracts
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.outlined.AttachFile
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.FilledTonalButton
import androidx.compose.material3.Icon
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.rememberCoroutineScope
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.unit.dp
import cz.cleansia.customer.R
import cz.cleansia.core.media.ImageCompressor
import cz.cleansia.core.media.isImageMimeType
import cz.cleansia.core.media.jpegFileName
import cz.cleansia.core.media.queryDisplayName
import cz.cleansia.core.media.queryMimeType
import cz.cleansia.core.snackbar.SnackbarController
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.launch
import kotlinx.coroutines.withContext

/**
 * The system file picker for dispute evidence, shared by the create form and the detail thread.
 * Launch it with the wildcard MIME filter so one picker session takes images and PDFs alike; the
 * ViewModel refuses anything the server would not accept.
 *
 * Every picked file is read off the main thread and handed to [onPrepared] as final bytes. Images
 * are downscaled to 1920px and re-encoded, which is what drops the EXIF block — capture GPS
 * included — and matches what iOS does. A PDF is passed byte-identical: re-encoding it as a JPEG
 * would destroy it. The size and type checks downstream run on these FINAL bytes, so the app never
 * approves one payload and uploads another.
 */
@Composable
fun rememberEvidencePicker(
    snackbar: SnackbarController,
    onPrepared: (bytes: ByteArray, fileName: String, mimeType: String) -> Unit,
): ManagedActivityResultLauncher<String, List<Uri>> {
    val context = LocalContext.current
    val coroutineScope = rememberCoroutineScope()
    val encodeFailedMessage = stringResource(R.string.dispute_evidence_encode_failed)

    return rememberLauncherForActivityResult(
        contract = ActivityResultContracts.GetMultipleContents(),
        onResult = { uris ->
            if (uris.isEmpty()) return@rememberLauncherForActivityResult
            coroutineScope.launch {
                for (uri in uris) {
                    val (mime, displayName) = withContext(Dispatchers.IO) {
                        queryMimeType(context, uri) to queryDisplayName(context, uri)
                    }
                    if (isImageMimeType(mime)) {
                        val compressed = ImageCompressor.compress(context.contentResolver, uri)
                        if (compressed == null) {
                            snackbar.showError(encodeFailedMessage)
                            continue
                        }
                        onPrepared(
                            compressed.bytes,
                            jpegFileName(displayName ?: fallbackEvidenceName()),
                            compressed.contentType,
                        )
                        continue
                    }
                    val bytes = withContext(Dispatchers.IO) {
                        runCatching {
                            context.contentResolver.openInputStream(uri)?.use { it.readBytes() }
                        }.getOrNull()
                    }
                    if (bytes == null) {
                        snackbar.showError(encodeFailedMessage)
                        continue
                    }
                    onPrepared(
                        bytes,
                        displayName ?: fallbackEvidenceName(),
                        mime ?: "application/octet-stream",
                    )
                }
            }
        },
    )
}

private fun fallbackEvidenceName(): String = "evidence-${System.currentTimeMillis()}"

@Composable
fun AddEvidenceButton(
    onClick: () -> Unit,
    enabled: Boolean = true,
    uploading: Boolean = false,
) {
    FilledTonalButton(
        onClick = onClick,
        enabled = enabled && !uploading,
        modifier = Modifier.fillMaxWidth(),
        shape = RoundedCornerShape(12.dp),
    ) {
        if (uploading) {
            CircularProgressIndicator(
                strokeWidth = 2.dp,
                modifier = Modifier.size(18.dp),
                color = MaterialTheme.colorScheme.onSecondaryContainer,
            )
            Spacer(Modifier.width(10.dp))
            Text(stringResource(R.string.dispute_evidence_uploading))
        } else {
            Icon(
                imageVector = Icons.Outlined.AttachFile,
                contentDescription = null,
                modifier = Modifier.size(18.dp),
            )
            Spacer(Modifier.width(8.dp))
            Text(stringResource(R.string.dispute_evidence_add_button))
        }
    }
}
