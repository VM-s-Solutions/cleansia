package cz.cleansia.core.media

import android.content.Context
import android.net.Uri
import android.provider.OpenableColumns

/**
 * Content-URI metadata helpers shared by every upload path in both apps.
 *
 * [queryDisplayName] and [queryMimeType] both make a binder round trip to the
 * providing app, so they block: call them off the main thread.
 */

/**
 * The user-visible file name behind a content URI, or null when the provider
 * errors or reports no `DISPLAY_NAME`.
 *
 * This is what replaces `uri.lastPathSegment` at the picker call sites. For a
 * MediaStore pick that segment is a bare numeric row id with no extension,
 * which is why order-photo blobs are stored today with no file extension and a
 * server-guessed content type.
 */
fun queryDisplayName(context: Context, uri: Uri): String? = runCatching {
    context.contentResolver.query(
        uri,
        arrayOf(OpenableColumns.DISPLAY_NAME),
        null,
        null,
        null,
    )?.use { cursor -> if (cursor.moveToFirst()) cursor.getString(0) else null }
}.getOrNull()?.takeIf { it.isNotBlank() }

/** The provider-declared MIME type of [uri], or null if it declares none. */
fun queryMimeType(context: Context, uri: Uri): String? =
    runCatching { context.contentResolver.getType(uri) }.getOrNull()

/**
 * Whether a picked file is compressed or uploaded byte-for-byte. The single rule both mixed-content
 * pickers use.
 *
 * **Re-encoding a PDF as a JPEG would corrupt it**, so anything that is not an image passes through
 * untouched. Providers may return a type with parameters and are not required to lowercase it, so the
 * value is normalised before the prefix test. -> /architecture/backend#content-sniffing
 */
 * The name to upload a compressed image under: the user's own name with the extension replaced by
 * `.jpg`.
 *
 * **Everything the compressor returns is a JPEG whatever was picked, so the name has to say so** — the
 * backend derives the stored content type from the extension, and the evidence list routes a tap to the
 * image or PDF viewer on the same basis. -> /architecture/backend#content-sniffing
 */
 * or the PDF viewer on the same basis — a PNG name on JPEG bytes makes both
 * wrong. The user's own name is kept rather than collapsed to
 * [ImageCompressor.OUTPUT_FILE_NAME] because the partner documents list renders
 * it verbatim, and a screen of identical `photo.jpg` rows is useless.
 *
 * A provider-supplied name is untrusted: it is stripped of path segments and
 * truncated to the 255 the validators accept, and anything left blank (a
 * dotfile, a path with no leaf) falls back to the shared default.
 */
fun jpegFileName(displayName: String?): String {
    val base = displayName
        ?.trim()
        ?.substringAfterLast('/')
        ?.substringBeforeLast('.')
        ?.trim()
        ?.takeIf { it.isNotEmpty() }
        ?: return ImageCompressor.OUTPUT_FILE_NAME
    return base.take(MAX_UPLOAD_FILE_NAME_LENGTH - JPEG_EXTENSION.length) + JPEG_EXTENSION
}
