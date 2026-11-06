package cz.cleansia.core.ui.components

import androidx.compose.material3.darkColorScheme
import androidx.compose.material3.lightColorScheme
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.luminance
import androidx.compose.ui.unit.dp
import java.io.File
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Test

/**
 * The slide both apps confirm with. There is no Compose test harness in this module, so the rules the composable
 * follows are pure functions pinned here, and the locked face is read as source.
 */
class SwipeToConfirmButtonTest {

    @Test
    fun `a busy slide holds its thumb at the end`() {
        assertEquals(ThumbMove.ToEnd, thumbMoveOnBusy(wasBusy = false, isBusy = true))
    }

    @Test
    fun `the thumb springs back and re-arms when the busy state ends`() {
        assertEquals(ThumbMove.ToStart, thumbMoveOnBusy(wasBusy = true, isBusy = false))
    }

    @Test
    fun `a slide that is never busy keeps a confirmed thumb at the end with its check`() {
        assertEquals(ThumbMove.Stay, thumbMoveOnBusy(wasBusy = false, isBusy = false))
        assertEquals(ThumbGlyph.Check, thumbGlyph(isBusy = false, confirmed = true))
    }

    @Test
    fun `the thumb shows a spinner while busy, a check once confirmed, else the arrow`() {
        assertEquals(ThumbGlyph.Spinner, thumbGlyph(isBusy = true, confirmed = true))
        assertEquals(ThumbGlyph.Spinner, thumbGlyph(isBusy = true, confirmed = false))
        assertEquals(ThumbGlyph.Arrow, thumbGlyph(isBusy = false, confirmed = false))
    }

    @Test
    fun `the busy text replaces the label while busy, and nothing does without one`() {
        assertEquals("Slide to pay", slideWords("Slide to pay", busyText = "Paying…", isBusy = false))
        assertEquals("Paying…", slideWords("Slide to pay", busyText = "Paying…", isBusy = true))
        assertNull(slideWords("Slide to pay", busyText = null, isBusy = true))
    }

    @Test
    fun `nothing drags a disabled, busy or confirmed slide`() {
        assertTrue(slideAcceptsDrag(enabled = true, isBusy = false, confirmed = false))
        assertFalse(slideAcceptsDrag(enabled = false, isBusy = false, confirmed = false))
        assertFalse(slideAcceptsDrag(enabled = true, isBusy = true, confirmed = false))
        assertFalse(slideAcceptsDrag(enabled = true, isBusy = false, confirmed = true))
    }

    @Test
    fun `a release at nine tenths of the track or further confirms`() {
        assertTrue(slideConfirms(offsetPx = 270f, maxOffsetPx = 300f))
        assertTrue(slideConfirms(offsetPx = 300f, maxOffsetPx = 300f))
        assertFalse(slideConfirms(offsetPx = 269f, maxOffsetPx = 300f))
    }

    @Test
    fun `lanes are opt-in, and a label allowed two lines takes them`() {
        assertFalse(slideHasLanes(thumbLanes = false, maxLines = 1))
        assertTrue(slideHasLanes(thumbLanes = true, maxLines = 1))
        assertTrue(slideHasLanes(thumbLanes = false, maxLines = 2))
    }

    @Test
    fun `each lane keeps the thumb, its inset and a gap clear of the label`() {
        assertEquals(4.dp + 48.dp + 8.dp, SlideLane)
    }

    @Test
    fun `the track is sky-600 on a light scheme and sky-800 on a dark one`() {
        assertEquals(Color(0xFF0284C7), swipeTrackColor(lightColorScheme(surface = Color.White)))
        assertEquals(Color(0xFF075985), swipeTrackColor(darkColorScheme(surface = Color(0xFF1E293B))))
    }

    /** White on sky-600 is 4.10:1, the app-wide finding on the light brand fill; the dark track clears 4.5:1. */
    @Test
    fun `the white label reads 4_5 to 1 on the dark track`() {
        assertEquals(Color.White, SwipeLabelInk)
        val dark = contrast(SwipeLabelInk, swipeTrackColor(darkColorScheme(surface = Color(0xFF1E293B))))
        assertTrue("white on sky-800 is ${"%.2f".format(dark)}:1", dark >= 4.5)
    }

    @Test
    fun `the open slide writes its label in the label ink`() {
        val open = body(source, "fun SwipeToConfirmButton(")
        assertTrue(Regex("""color = SwipeLabelInk\.copy\(""").containsMatchIn(open))
    }

    @Test
    fun `a locked slide has no drag, no tap and no dimming`() {
        val locked = body(source, "fun LockedSwipeToConfirm(")
        listOf("pointerInput", "draggable", "detectDragGestures", "clickable", "alpha(").forEach { word ->
            assertFalse("LockedSwipeToConfirm uses $word", locked.contains(word))
        }
        assertTrue(locked.contains("Icons.Outlined.Lock"))
    }

    private val source: String by lazy {
        sequenceOf(File("."), File("core"), File("src/cleansia_android/core"))
            .map { File(it, "src/main/java/cz/cleansia/core/ui/components/SwipeToConfirmButton.kt") }
            .first { it.isFile }
            .readText()
            .replace("\r\n", "\n")
    }

    /** The body of the function declared by [signature], up to its matching closing brace. */
    private fun body(text: String, signature: String): String {
        val start = text.indexOf(signature)
        assertTrue("$signature not found", start >= 0)
        val open = text.indexOf('{', text.indexOf(')', start))
        var depth = 1
        var i = open + 1
        while (i < text.length && depth > 0) {
            when (text[i]) {
                '{' -> depth++
                '}' -> depth--
            }
            i++
        }
        return text.substring(open + 1, i - 1)
    }

    private fun contrast(a: Color, b: Color): Double {
        val (hi, lo) = listOf(a.luminance(), b.luminance()).sortedDescending()
        return (hi + 0.05) / (lo + 0.05)
    }
}
