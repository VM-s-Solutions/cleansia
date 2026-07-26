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
 * Whether a picked file should go through [ImageCompressor] or be uploaded
 * byte-for-byte as-is.
 *
 * This is the single rule both mixed-content pickers use — the partner
 * documents picker (which accepts any type, so PDFs and Word files arrive
 * here) and the customer dispute evidence picker. Re-encoding a PDF as a JPEG
 * would corrupt it, so anything that is not an image type must pass through
 * untouched.
 *
 * Providers are allowed to return a type with parameters
 * (`image/jpeg; charset=binary`) and are not required to lowercase it, so both
 * are normalised before the prefix test. A null or blank type — which is what
 * a provider that declares nothing returns — is not an image, so it passes
 * through; that is the fail-safe direction, since a passthrough of an image
 * still uploads successfully whereas a JPEG re-encode of a document does not.
 */
fun isImageMimeType(mimeType: String?): Boolean =
    mimeType?.substringBefore(';')?.trim()?.lowercase()?.startsWith("image/") == true

/** Server-side cap on `FileName` — `SaveOrderPhotos.cs` and `SaveMyDocuments.cs`. */
private const val MAX_UPLOAD_FILE_NAME_LENGTH = 255

private const val JPEG_EXTENSION = ".jpg"

/**
 * The name to upload a compressed image under: the user's own [displayName]
 * with its extension replaced by `.jpg`.
 *
 * Everything [ImageCompressor] returns is a JPEG whatever was picked, so the
 * name has to say so. `SaveOrderPhotos.cs` derives the stored content type from
 * the file extension, and the dispute evidence list routes a tap to the image
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
