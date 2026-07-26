package cz.cleansia.core.media

import android.content.ContentResolver
import android.graphics.Bitmap
import android.graphics.BitmapFactory
import android.graphics.Matrix
import android.net.Uri
import android.util.Base64
import androidx.exifinterface.media.ExifInterface
import kotlinx.coroutines.CancellationException
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.sync.Mutex
import kotlinx.coroutines.sync.withLock
import kotlinx.coroutines.withContext
import java.io.ByteArrayOutputStream
import kotlin.math.max
import kotlin.math.roundToInt

/**
 * A JPEG produced by [ImageCompressor]. Deliberately a plain class and not a
 * `data class`: a generated `equals`/`hashCode` over a [ByteArray] compares
 * array identity, which reads as value equality and silently is not.
 */
class CompressedImage(
    val bytes: ByteArray,
    val contentType: String,
    val fileName: String,
)

/** The same JPEG, already base64-encoded for the JSON upload endpoints. */
class Base64Image(
    val base64: String,
    val contentType: String,
    val fileName: String,
)

/**
 * EXIF orientation codes, re-declared from the spec.
 *
 * They are numerically identical to `androidx.exifinterface.media.ExifInterface`'s
 * `ORIENTATION_*` constants (pinned by `ImageCompressorMathTest`). Declaring
 * them here is what keeps [exifRotationDegrees] / [exifIsMirrored] free of
 * android imports, so the orientation mapping — the part of this file most
 * likely to be wrong — is testable on a plain JVM.
 */
internal object ExifOrientation {
    const val UNDEFINED = 0
    const val NORMAL = 1
    const val FLIP_HORIZONTAL = 2
    const val ROTATE_180 = 3
    const val FLIP_VERTICAL = 4
    const val TRANSPOSE = 5
    const val ROTATE_90 = 6
    const val TRANSVERSE = 7
    const val ROTATE_270 = 8
}

/**
 * Shared image downscaler / re-encoder for every upload path in both apps.
 *
 * Mirrors the iOS twin `CleansiaCore/Sources/CleansiaCore/Media/ImageCompressor.swift`
 * — longest side 1920 (aspect preserved, never upscaled), JPEG quality 0.7,
 * output advertised as `image/jpeg` / `photo.jpg` — so the two platforms put
 * comparable files into the same endpoints.
 *
 * **Metadata is dropped by construction, not by erasure.** The source is
 * decoded into a fresh [Bitmap] and re-encoded through [Bitmap.compress],
 * which writes a bare JFIF stream with no EXIF segment at all. Nothing
 * enumerates or blanks individual tags, so there is no list of tags to keep in
 * sync and no way for a maker-note or GPS block to survive by being unlisted.
 * That is the security half of this class: today the cleaner's capture
 * coordinates travel with every job photo into blob storage.
 *
 * **Orientation is baked into the pixels first.** Dropping the EXIF segment
 * also drops `TAG_ORIENTATION`, and most Android cameras store a portrait shot
 * as a landscape buffer plus that tag rather than rotating the pixels. So the
 * tag is read before the re-encode and applied to the bitmap as a [Matrix]
 * transform; see [exifRotationDegrees] / [exifIsMirrored]. Skip that step and
 * every portrait photo uploads sideways — correctly previewed on the device,
 * wrong everywhere the image is consumed.
 *
 * **Threading.** Both entry points are `suspend` and hop to
 * [Dispatchers.Default]. Decoding, scaling, JPEG-encoding and base64-encoding
 * are CPU-bound, and `Dispatchers.Default` is capped at the core count;
 * `Dispatchers.IO` would be the wrong pool precisely because it is elastic to
 * 64 threads, and the customer dispute picker uploads a multi-select batch in
 * a loop — on IO that batch becomes dozens of concurrent multi-megapixel
 * decodes and an OOM. A process-wide [Mutex] narrows it further so at most one
 * bitmap pipeline is live at any moment, which is what actually bounds peak
 * memory for an N-file batch. The three `openInputStream` reads are the only
 * blocking work and they sit inside that same lock, so at most one `Default`
 * thread is ever parked on IO.
 */
object ImageCompressor {

    /** Longest output side in pixels. Matches `ImageCompressor.swift:31`. */
    const val MAX_DIMENSION = 1920

    /** JPEG quality 0-100. Matches iOS's `0.7` (`ImageCompressor.swift:32`). */
    const val JPEG_QUALITY = 70

    /** Content type of every successful result. */
    const val OUTPUT_MIME = "image/jpeg"

    /**
     * File name of every successful result.
     *
     * Always a real `.jpg`, unlike the `uri.lastPathSegment` the call sites use
     * today — a MediaStore pick yields a bare numeric id with no extension,
     * which the server then has to guess a content type from.
     */
    const val OUTPUT_FILE_NAME = "photo.jpg"

    /**
     * Decode [uri], downscale it, bake in its EXIF orientation and re-encode it
     * as a metadata-free JPEG.
     *
     * Returns null when the source cannot be read or decoded — a corrupt file,
     * a zero-byte capture, a vector or video type that reached us by mistake,
     * or HEIC on API 26/27 (`BitmapFactory` gained HEIF at API 28). Callers
     * must surface that as a user-visible error rather than a silent no-op.
     */
    suspend fun compress(
        resolver: ContentResolver,
        uri: Uri,
        maxDimension: Int = MAX_DIMENSION,
        quality: Int = JPEG_QUALITY,
    ): CompressedImage? = withContext(Dispatchers.Default) {
        pipelineLock.withLock {
            guarded { encodeBlocking(resolver, uri, maxDimension, quality) }
        }
    }

    /**
     * [compress], plus the base64 encode, inside the same off-main hop.
     *
     * This entry point exists so no caller can reintroduce the bug this class
     * was written to fix: base64-encoding a multi-megabyte image is itself
     * expensive, and doing it to the returned [CompressedImage.bytes] back on
     * the main thread undoes half the win. There is deliberately no
     * `CompressedImage.toBase64()` extension — the JSON upload sites must use
     * this and only this.
     */
    suspend fun compressToBase64(
        resolver: ContentResolver,
        uri: Uri,
        maxDimension: Int = MAX_DIMENSION,
        quality: Int = JPEG_QUALITY,
    ): Base64Image? = withContext(Dispatchers.Default) {
        pipelineLock.withLock {
            guarded {
                val compressed = encodeBlocking(resolver, uri, maxDimension, quality) ?: return@guarded null
                Base64Image(
                    base64 = Base64.encodeToString(compressed.bytes, Base64.NO_WRAP),
                    contentType = compressed.contentType,
                    fileName = compressed.fileName,
                )
            }
        }
    }

    /**
     * Serialises the whole pipeline process-wide. See the class doc: this, not
     * the dispatcher, is what bounds peak memory for a multi-file batch.
     */
    private val pipelineLock = Mutex()

    /**
     * The bitmap pipeline. Blocking; always called inside [pipelineLock] on
     * [Dispatchers.Default].
     *
     * Three passes over the stream, because a [ContentResolver] stream is not
     * reliably re-readable: bounds, then EXIF, then the downsampled decode.
     */
    private fun encodeBlocking(
        resolver: ContentResolver,
        uri: Uri,
        maxDimension: Int,
        quality: Int,
    ): CompressedImage? {
        // Pass 1 — header only, no pixels allocated. Note that decodeStream
        // returns null by design when inJustDecodeBounds is set, so the stream
        // has to be null-checked on its own; eliding the result into an elvis
        // would abort every call.
        val bounds = BitmapFactory.Options().apply { inJustDecodeBounds = true }
        val boundsStream = resolver.openInputStream(uri) ?: return null
        boundsStream.use { BitmapFactory.decodeStream(it, null, bounds) }
        val sourceWidth = bounds.outWidth
        val sourceHeight = bounds.outHeight
        if (sourceWidth <= 0 || sourceHeight <= 0) return null

        // Pass 2 — the orientation tag, read BEFORE the re-encode discards it.
        // A source with no readable EXIF (PNG, WebP, a stripped JPEG) is
        // upright by definition, so failure here degrades to NORMAL rather
        // than aborting the upload.
        val orientation = resolver.openInputStream(uri)?.use { stream ->
            guarded {
                ExifInterface(stream).getAttributeInt(
                    ExifInterface.TAG_ORIENTATION,
                    ExifOrientation.NORMAL,
                )
            }
        } ?: ExifOrientation.NORMAL

        // Pass 3 — the real decode, downsampled. inSampleSize keeps the
        // intermediate bitmap under 2x the target, so a 12MP source costs
        // ~12MB rather than ~48MB.
        val decodeOptions = BitmapFactory.Options().apply {
            inSampleSize = calculateInSampleSize(sourceWidth, sourceHeight, maxDimension)
            inPreferredConfig = Bitmap.Config.ARGB_8888
        }
        var working = resolver.openInputStream(uri)?.use {
            BitmapFactory.decodeStream(it, null, decodeOptions)
        } ?: return null

        try {
            // Exact scale down to the target. inSampleSize only halves, so this
            // is what actually lands the longest side on maxDimension.
            val (targetWidth, targetHeight) = targetDimensions(working.width, working.height, maxDimension)
            if (targetWidth != working.width || targetHeight != working.height) {
                val scaled = Bitmap.createScaledBitmap(working, targetWidth, targetHeight, true)
                // createScaledBitmap hands back the source instance when the
                // dimensions already match; recycling unconditionally would
                // then destroy the bitmap we are about to encode.
                if (scaled !== working) {
                    working.recycle()
                    working = scaled
                }
            }

            // Bake the orientation into the pixels. Order matters: rotate, then
            // mirror horizontally — see exifIsMirrored's doc for why that one
            // ordering expresses all four mirrored cases.
            if (needsExifTransform(orientation)) {
                val matrix = Matrix().apply {
                    setRotate(exifRotationDegrees(orientation).toFloat())
                    if (exifIsMirrored(orientation)) postScale(-1f, 1f)
                }
                val rotated = Bitmap.createBitmap(working, 0, 0, working.width, working.height, matrix, true)
                if (rotated !== working) {
                    working.recycle()
                    working = rotated
                }
            }

            // The metadata strip: a bare JFIF stream, no EXIF segment, no GPS.
            val output = ByteArrayOutputStream()
            if (!working.compress(Bitmap.CompressFormat.JPEG, quality, output)) return null
            return CompressedImage(
                bytes = output.toByteArray(),
                contentType = OUTPUT_MIME,
                fileName = OUTPUT_FILE_NAME,
            )
        } finally {
            working.recycle()
        }
    }
}

/**
 * Runs [block], turning any failure into null.
 *
 * Catches [Throwable] on purpose, [OutOfMemoryError] included: a pathological
 * source should degrade to an error snackbar, not a crash. Every bitmap in the
 * pipeline is function-local and recycled in a `finally`, so the heap is
 * already released by the time this returns.
 *
 * [CancellationException] is rethrown — swallowing it would leave a cancelled
 * upload coroutine looking like a failed compression and break structured
 * concurrency.
 */
private inline fun <T> guarded(block: () -> T?): T? =
    try {
        block()
    } catch (cancellation: CancellationException) {
        throw cancellation
    } catch (_: Throwable) {
        null
    }

/**
 * Power-of-two decode divisor that keeps the intermediate bitmap small without
 * ever going below the target size.
 *
 * Halves while the halved longest side is still at least [maxDimension], so the
 * decoded bitmap is between 1x and 2x the final dimensions — enough headroom
 * for [Bitmap.createScaledBitmap] to produce a clean result, far short of
 * decoding a 12MP source at full size on the way to a 1920px JPEG.
 *
 * Returns 1 for degenerate input; `BitmapFactory` treats anything below 1 as 1
 * anyway, but returning 0 here would be a divide-by-zero waiting to happen.
 */
internal fun calculateInSampleSize(srcWidth: Int, srcHeight: Int, maxDimension: Int): Int {
    if (srcWidth <= 0 || srcHeight <= 0 || maxDimension <= 0) return 1
    val longest = max(srcWidth, srcHeight)
    var sampleSize = 1
    while (longest / (sampleSize * 2) >= maxDimension) {
        sampleSize *= 2
    }
    return sampleSize
}

/**
 * Output size with the aspect ratio preserved and the longest side capped at
 * [maxDimension].
 *
 * Never upscales — a source already within the cap is returned unchanged, the
 * direct analogue of `ImageCompressor.swift:54`. Both sides are clamped to at
 * least 1 (`ImageCompressor.swift:55-56`) so an extreme aspect ratio cannot
 * round the short side to zero and make [Bitmap.createScaledBitmap] throw.
 */
internal fun targetDimensions(srcWidth: Int, srcHeight: Int, maxDimension: Int): Pair<Int, Int> {
    if (srcWidth <= 0 || srcHeight <= 0 || maxDimension <= 0) return 1 to 1
    val longest = max(srcWidth, srcHeight)
    if (longest <= maxDimension) return srcWidth to srcHeight
    val scale = maxDimension.toDouble() / longest.toDouble()
    val width = (srcWidth * scale).roundToInt().coerceAtLeast(1)
    val height = (srcHeight * scale).roundToInt().coerceAtLeast(1)
    return width to height
}

/**
 * Clockwise rotation, in degrees, that an EXIF [orientation] asks a viewer to
 * apply.
 *
 * The four mirrored orientations carry a rotation too — `TRANSPOSE` is a 90
 * turn plus a horizontal mirror, `TRANSVERSE` a 270 turn plus one, and
 * `FLIP_VERTICAL` is a 180 turn plus one. Handling only the mirror half of
 * those leaves the photo sideways.
 *
 * Unknown values are treated as upright, matching how viewers behave.
 */
internal fun exifRotationDegrees(orientation: Int): Int = when (orientation) {
    ExifOrientation.ROTATE_90, ExifOrientation.TRANSPOSE -> 90
    ExifOrientation.ROTATE_180, ExifOrientation.FLIP_VERTICAL -> 180
    ExifOrientation.ROTATE_270, ExifOrientation.TRANSVERSE -> 270
    else -> 0
}

/**
 * True when [orientation] additionally needs a horizontal mirror, applied
 * *after* [exifRotationDegrees].
 *
 * Rotate-then-mirror-horizontally expresses all four mirrored cases with one
 * ordering: 0 + mirror is `FLIP_HORIZONTAL`, 180 + mirror is `FLIP_VERTICAL`,
 * 90 + mirror is `TRANSPOSE` (a reflection in the main diagonal) and 270 +
 * mirror is `TRANSVERSE` (a reflection in the anti-diagonal). Applying the
 * mirror first instead would invert the sense of the 90 and 270 cases.
 */
internal fun exifIsMirrored(orientation: Int): Boolean = when (orientation) {
    ExifOrientation.FLIP_HORIZONTAL,
    ExifOrientation.FLIP_VERTICAL,
    ExifOrientation.TRANSPOSE,
    ExifOrientation.TRANSVERSE,
    -> true

    else -> false
}

/**
 * True when [orientation] requires a bitmap transform at all.
 *
 * Checked separately so upright photos — the common case — skip an entire
 * extra full-size bitmap allocation. Note it cannot be `degrees != 0`:
 * `FLIP_HORIZONTAL` rotates by zero and still needs the mirror.
 */
internal fun needsExifTransform(orientation: Int): Boolean =
    exifRotationDegrees(orientation) != 0 || exifIsMirrored(orientation)
