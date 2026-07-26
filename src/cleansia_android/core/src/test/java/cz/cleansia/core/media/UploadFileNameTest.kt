package cz.cleansia.core.media

import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Test

/**
 * Pins [jpegFileName] — the rule the mixed-content upload paths use to name a
 * file that went through [ImageCompressor].
 *
 * Every compressed upload is a JPEG regardless of what was picked, so the name
 * has to say `.jpg` or the server records a content type that contradicts the
 * bytes (`SaveOrderPhotos` derives the type from the extension) and the
 * evidence list routes a JPEG to the PDF viewer. The user's own name is kept
 * because the partner documents list renders it verbatim — naming every
 * uploaded scan `photo.jpg` would make that list unreadable.
 */
class UploadFileNameTest {

    @Test
    fun `a missing display name falls back to the shared default`() {
        assertEquals(ImageCompressor.OUTPUT_FILE_NAME, jpegFileName(null))
    }

    @Test
    fun `a blank display name falls back to the shared default`() {
        assertEquals(ImageCompressor.OUTPUT_FILE_NAME, jpegFileName("   "))
    }

    @Test
    fun `the extension is replaced, not appended`() {
        assertEquals("passport.jpg", jpegFileName("passport.png"))
        assertEquals("passport.jpg", jpegFileName("passport.HEIC"))
        assertEquals("passport.jpg", jpegFileName("passport.webp"))
    }

    @Test
    fun `a name with no extension gains one`() {
        assertEquals("passport.jpg", jpegFileName("passport"))
    }

    @Test
    fun `an already-correct name is left alone`() {
        assertEquals("photo.jpg", jpegFileName("photo.jpg"))
        assertEquals("photo.jpg", jpegFileName("photo.JPG"))
    }

    @Test
    fun `only the last dot is treated as the extension separator`() {
        assertEquals("scan.v2.jpg", jpegFileName("scan.v2.png"))
    }

    @Test
    fun `surrounding whitespace is trimmed`() {
        assertEquals("shot.jpg", jpegFileName("  shot.png  "))
    }

    /**
     * A provider is not supposed to put a path in `DISPLAY_NAME`, but the code
     * this replaces derived the name from `uri.lastPathSegment` and stripped
     * one anyway. Keeping the strip means a hostile or sloppy provider cannot
     * steer the blob name the server builds from it.
     */
    @Test
    fun `any path segments are stripped`() {
        assertEquals("c.jpg", jpegFileName("a/b/c.png"))
        assertEquals(ImageCompressor.OUTPUT_FILE_NAME, jpegFileName("../../"))
    }

    /**
     * A dotfile has no base name once the extension is removed, and `.jpg`
     * alone is a hidden file rather than a usable name.
     */
    @Test
    fun `a name that is only an extension falls back to the default`() {
        assertEquals(ImageCompressor.OUTPUT_FILE_NAME, jpegFileName(".png"))
    }

    /**
     * Both server validators cap `FileName` at 255 (`SaveOrderPhotos.cs`,
     * `SaveMyDocuments.cs`). A provider-supplied name is untrusted input, so
     * the cap is enforced here rather than discovered as a 400.
     */
    @Test
    fun `an over-long name is truncated to the server's 255 limit`() {
        val result = jpegFileName("x".repeat(400) + ".png")
        assertEquals(255, result.length)
        assertTrue(result.endsWith(".jpg"))
    }
}
