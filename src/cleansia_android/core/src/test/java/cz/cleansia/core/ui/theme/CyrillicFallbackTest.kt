package cz.cleansia.core.ui.theme

import androidx.compose.ui.text.font.Font
import androidx.compose.ui.text.font.FontListFontFamily
import androidx.compose.ui.text.font.ResourceFont
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Test
import kotlin.math.abs

/**
 * Rendering is not assertable off-device, so what is pinned here is the data the renderer is handed:
 * which face each brand family draws with, which face it defers to, and that no type-scale slot is
 * left without one. The measurement those answers depend on lives in [BundledFontCoverageTest].
 */
class CyrillicFallbackTest {

    private companion object {
        /** Poppins Medium(500) has no Nunito counterpart; 400 is the closest Nunito ships. */
        const val MAX_FALLBACK_WEIGHT_DRIFT = 100
    }

    private fun drawsAllCyrillic(resId: Int): Boolean =
        (BundledFontFiles.cyrillic - BundledFontFiles.codePoints(BundledFontFiles.file(resId))).isEmpty()

    private fun drawsCyrillic(font: Font): Boolean = when (font) {
        is GlyphFallbackFont -> drawsAllCyrillic(font.fallbackResId) || drawsAllCyrillic(font.resId)
        is ResourceFont -> drawsAllCyrillic(font.resId)
        else -> false
    }

    @Test
    fun `every type-scale slot renders Cyrillic from a bundled face`() {
        val slots = BundledFontFiles.typeScaleSlots(CleansiaTypography)
        assertEquals("reflection over Typography found the wrong number of slots", 15, slots.size)

        val offenders = slots.filterNot { (_, style) ->
            val family = style.fontFamily as? FontListFontFamily ?: return@filterNot false
            family.fonts.isNotEmpty() && family.fonts.all(::drawsCyrillic)
        }
        assertTrue(
            "These slots hand Cyrillic to the platform substitute instead of a bundled face: " +
                offenders.map { it.first },
            offenders.isEmpty(),
        )
    }

    @Test
    fun `every Poppins weight draws Poppins and defers to a Cyrillic-capable Nunito of the same weight`() {
        val fonts = (Poppins as FontListFontFamily).fonts
        assertEquals(3, fonts.size)

        fonts.forEach { font ->
            assertTrue("$font is not a ${GlyphFallbackFont::class.simpleName}", font is GlyphFallbackFont)
            font as GlyphFallbackFont

            val primary = BundledFontFiles.file(font.resId)
            val fallback = BundledFontFiles.file(font.fallbackResId)
            val primaryWeight = BundledFontFiles.declaredWeight(primary)
            val fallbackWeight = BundledFontFiles.declaredWeight(fallback)

            assertTrue(
                "the Poppins family draws ${primary.name}, so Latin would not render as Poppins",
                primary.name.startsWith("poppins"),
            )
            assertTrue(
                "${fallback.name} cannot draw every Cyrillic code point, so ${primary.name} " +
                    "has no usable fallback",
                drawsAllCyrillic(font.fallbackResId),
            )
            assertEquals(
                "${primary.name} is declared ${font.weight.weight} to Compose but " +
                    "$primaryWeight in its own OS/2 table",
                primaryWeight,
                font.weight.weight,
            )
            assertTrue(
                "${fallback.name} ($fallbackWeight) is too far from ${primary.name} " +
                    "($primaryWeight) in weight to substitute for it",
                abs(fallbackWeight - primaryWeight) <= MAX_FALLBACK_WEIGHT_DRIFT,
            )
        }
    }

    @Test
    fun `Nunito draws Nunito and needs no fallback`() {
        val fonts = (Nunito as FontListFontFamily).fonts
        assertEquals(3, fonts.size)

        fonts.forEach { font ->
            assertTrue("$font is not a plain resource font", font is ResourceFont)
            val file = BundledFontFiles.file((font as ResourceFont).resId)
            assertTrue("the Nunito family draws ${file.name}", file.name.startsWith("nunito"))
            assertTrue("${file.name} cannot draw every Cyrillic code point", drawsAllCyrillic(font.resId))
        }
    }
}
