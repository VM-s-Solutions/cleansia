package cz.cleansia.core.media

import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test

/**
 * Pins the pure arithmetic behind [ImageCompressor] — the sizing math and the
 * EXIF-orientation -> transform mapping.
 *
 * These are the only parts of the pipeline that can be tested in this harness:
 * `:core` runs plain-JVM unit tests with `unitTests.isReturnDefaultValues = true`
 * (core/build.gradle.kts), so every `BitmapFactory` / `Bitmap` call returns
 * null or zero and there is no Robolectric on the classpath. That is exactly
 * why the sizing and orientation logic is factored out of the bitmap plumbing:
 * the arithmetic is where the regressions live, and it is testable properly.
 *
 * The sizing expectations mirror the iOS twin
 * `CleansiaCore/Sources/CleansiaCore/Media/ImageCompressor.swift:53-56`
 * so both platforms upload comparably sized files.
 */
class ImageCompressorMathTest {

    // ---- shared constants -------------------------------------------------

    @Test
    fun `output constants match the iOS compressor`() {
        assertEquals(1920, ImageCompressor.MAX_DIMENSION)
        assertEquals(70, ImageCompressor.JPEG_QUALITY)
        assertEquals("image/jpeg", ImageCompressor.OUTPUT_MIME)
        assertEquals("photo.jpg", ImageCompressor.OUTPUT_FILE_NAME)
    }

    // ---- calculateInSampleSize -------------------------------------------

    @Test
    fun `a 12MP source is decoded at half size`() {
        // 4032x3024 -> sample 2 -> 2016x1512 intermediate, never the full 12MP
        // bitmap. This is the OOM bound.
        assertEquals(2, calculateInSampleSize(4032, 3024, 1920))
    }

    @Test
    fun `a source at or below the target is decoded at full size`() {
        assertEquals(1, calculateInSampleSize(1920, 1080, 1920))
        assertEquals(1, calculateInSampleSize(800, 600, 1920))
    }

    @Test
    fun `a 48MP source is decoded at a quarter size`() {
        assertEquals(4, calculateInSampleSize(8000, 6000, 1920))
    }

    @Test
    fun `sample size never drops below one for degenerate bounds`() {
        // BitmapFactory reports -1 for outWidth/outHeight when it cannot parse
        // the header; inSampleSize < 1 is treated as 1 by the framework anyway,
        // but returning 0 here would be a divide-by-zero waiting to happen.
        assertEquals(1, calculateInSampleSize(0, 0, 1920))
        assertEquals(1, calculateInSampleSize(-1, -1, 1920))
        assertEquals(1, calculateInSampleSize(4032, 3024, 0))
    }

    @Test
    fun `sample size is driven by the longest side regardless of orientation`() {
        // Portrait 3024x4032 must sample identically to landscape 4032x3024.
        assertEquals(2, calculateInSampleSize(3024, 4032, 1920))
    }

    // ---- targetDimensions -------------------------------------------------

    @Test
    fun `landscape is scaled so the longest side is the max dimension`() {
        assertEquals(1920 to 1440, targetDimensions(2016, 1512, 1920))
    }

    @Test
    fun `portrait is scaled so the longest side is the max dimension`() {
        assertEquals(1440 to 1920, targetDimensions(1512, 2016, 1920))
    }

    @Test
    fun `a small source is never upscaled`() {
        // Direct analogue of ImageCompressor.swift:54 — scale is 1 when the
        // longest side already fits. Upscaling would inflate the upload for
        // zero visual gain.
        assertEquals(800 to 600, targetDimensions(800, 600, 1920))
        assertEquals(1 to 1, targetDimensions(1, 1, 1920))
    }

    @Test
    fun `a source exactly at the max dimension is left alone`() {
        assertEquals(1920 to 1920, targetDimensions(1920, 1920, 1920))
    }

    @Test
    fun `an extreme aspect ratio clamps the short side to one pixel`() {
        // Mirrors the max(1, ...) clamp at ImageCompressor.swift:55-56.
        // Without it the short side rounds to 0 and Bitmap.createScaledBitmap
        // throws IllegalArgumentException.
        assertEquals(1920 to 1, targetDimensions(3000, 1, 1920))
        assertEquals(1 to 1920, targetDimensions(1, 3000, 1920))
    }

    @Test
    fun `degenerate dimensions collapse to a single pixel rather than zero`() {
        assertEquals(1 to 1, targetDimensions(0, 0, 1920))
        assertEquals(1 to 1, targetDimensions(1024, 768, 0))
    }

    // ---- exifRotationDegrees ---------------------------------------------

    @Test
    fun `upright orientations need no rotation`() {
        assertEquals(0, exifRotationDegrees(ExifOrientation.NORMAL))
        assertEquals(0, exifRotationDegrees(ExifOrientation.UNDEFINED))
        assertEquals(0, exifRotationDegrees(ExifOrientation.FLIP_HORIZONTAL))
        // Anything the EXIF spec does not define is treated as upright.
        assertEquals(0, exifRotationDegrees(99))
    }

    @Test
    fun `rotated orientations map to their clockwise angle`() {
        assertEquals(90, exifRotationDegrees(ExifOrientation.ROTATE_90))
        assertEquals(180, exifRotationDegrees(ExifOrientation.ROTATE_180))
        assertEquals(270, exifRotationDegrees(ExifOrientation.ROTATE_270))
    }

    @Test
    fun `mirrored-and-rotated orientations carry their rotation too`() {
        // TRANSPOSE  = rotate 90 then mirror horizontally.
        // TRANSVERSE = rotate 270 then mirror horizontally.
        // FLIP_VERTICAL = rotate 180 then mirror horizontally.
        // Dropping the rotation half of these leaves the photo sideways.
        assertEquals(90, exifRotationDegrees(ExifOrientation.TRANSPOSE))
        assertEquals(270, exifRotationDegrees(ExifOrientation.TRANSVERSE))
        assertEquals(180, exifRotationDegrees(ExifOrientation.FLIP_VERTICAL))
    }

    // ---- exifIsMirrored ---------------------------------------------------

    @Test
    fun `only the four mirrored orientations are mirrored`() {
        assertTrue(exifIsMirrored(ExifOrientation.FLIP_HORIZONTAL))
        assertTrue(exifIsMirrored(ExifOrientation.FLIP_VERTICAL))
        assertTrue(exifIsMirrored(ExifOrientation.TRANSPOSE))
        assertTrue(exifIsMirrored(ExifOrientation.TRANSVERSE))

        assertFalse(exifIsMirrored(ExifOrientation.UNDEFINED))
        assertFalse(exifIsMirrored(ExifOrientation.NORMAL))
        assertFalse(exifIsMirrored(ExifOrientation.ROTATE_90))
        assertFalse(exifIsMirrored(ExifOrientation.ROTATE_180))
        assertFalse(exifIsMirrored(ExifOrientation.ROTATE_270))
        assertFalse(exifIsMirrored(99))
    }

    // ---- needsExifTransform ----------------------------------------------

    @Test
    fun `only upright orientations skip the transform`() {
        assertFalse(needsExifTransform(ExifOrientation.NORMAL))
        assertFalse(needsExifTransform(ExifOrientation.UNDEFINED))

        // FLIP_HORIZONTAL rotates by 0 degrees; if the "needs work" check only
        // looked at the angle, mirrored-only photos would ship un-un-mirrored.
        assertTrue(needsExifTransform(ExifOrientation.FLIP_HORIZONTAL))
        assertTrue(needsExifTransform(ExifOrientation.ROTATE_90))
        assertTrue(needsExifTransform(ExifOrientation.ROTATE_180))
        assertTrue(needsExifTransform(ExifOrientation.ROTATE_270))
        assertTrue(needsExifTransform(ExifOrientation.TRANSPOSE))
        assertTrue(needsExifTransform(ExifOrientation.TRANSVERSE))
        assertTrue(needsExifTransform(ExifOrientation.FLIP_VERTICAL))
    }

    // ---- ExifOrientation codes -------------------------------------------

    @Test
    fun `orientation codes are the EXIF spec values`() {
        // These MUST match androidx.exifinterface.media.ExifInterface's
        // ORIENTATION_* constants; they are re-declared here so the mapping
        // functions stay free of android imports and remain JVM-testable.
        assertEquals(0, ExifOrientation.UNDEFINED)
        assertEquals(1, ExifOrientation.NORMAL)
        assertEquals(2, ExifOrientation.FLIP_HORIZONTAL)
        assertEquals(3, ExifOrientation.ROTATE_180)
        assertEquals(4, ExifOrientation.FLIP_VERTICAL)
        assertEquals(5, ExifOrientation.TRANSPOSE)
        assertEquals(6, ExifOrientation.ROTATE_90)
        assertEquals(7, ExifOrientation.TRANSVERSE)
        assertEquals(8, ExifOrientation.ROTATE_270)
    }

    // ---- isImageMimeType --------------------------------------------------

    @Test
    fun `image mime types are routed to the compressor`() {
        assertTrue(isImageMimeType("image/jpeg"))
        assertTrue(isImageMimeType("image/png"))
        assertTrue(isImageMimeType("image/webp"))
        assertTrue(isImageMimeType("image/heic"))
        // Providers are allowed to return parameters and mixed case.
        assertTrue(isImageMimeType("IMAGE/JPEG"))
        assertTrue(isImageMimeType("image/jpeg; charset=binary"))
        assertTrue(isImageMimeType("  image/jpeg  "))
    }

    @Test
    fun `non-image mime types pass through untouched`() {
        // A PDF re-encoded as a JPEG is a corrupted document, so the document
        // picker must never hand these to the compressor.
        assertFalse(isImageMimeType("application/pdf"))
        assertFalse(isImageMimeType("application/msword"))
        assertFalse(isImageMimeType("application/octet-stream"))
        assertFalse(isImageMimeType("text/plain"))
        assertFalse(isImageMimeType("video/mp4"))
        assertFalse(isImageMimeType(null))
        assertFalse(isImageMimeType(""))
        // Not a prefix match on the bare word — "imagex/foo" is not an image.
        assertFalse(isImageMimeType("imagex/foo"))
    }
}
