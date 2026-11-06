package cz.cleansia.core.ui.theme

import cz.cleansia.core.R
import cz.cleansia.core.ui.components.LinkGlyphPlacement
import cz.cleansia.core.ui.components.linkGlyphPlacement
import cz.cleansia.core.ui.components.trailingGlyphPlacement
import org.junit.Assert.assertEquals
import org.junit.Test

/** An icon beside a word sits on the word's cap band: its centre at the baseline less half the cap height. */
class CapBandTest {

    @Test
    fun `the cap height is the bundled Nunito's own, in every weight`() {
        listOf(R.font.nunito_regular, R.font.nunito_semibold, R.font.nunito_bold).forEach { font ->
            val file = BundledFontFiles.file(font)
            assertEquals(file.name, BundledFontFiles.capHeightShare(file), NunitoCapHeight, 0.0001f)
        }
    }

    @Test
    fun `a glyph aligned on the cap band has its centre half a cap height above the baseline`() {
        val glyphHeight = 54
        val capHeight = 29.6f
        val line = capBandLine(glyphHeight, capHeight)

        assertEquals(42, line)
        assertEquals(capHeight / 2, line - glyphHeight / 2f, 0.5f)
    }

    @Test
    fun `a one-line link centres its icons on the line, and a link that may wrap puts them on the cap band`() {
        assertEquals(LinkGlyphPlacement.LineCentre, linkGlyphPlacement(maxLines = 1))
        assertEquals(LinkGlyphPlacement.CapBand, linkGlyphPlacement(maxLines = 2))
    }

    @Test
    fun `a link that may wrap puts its trailing icon after its last line's end, however long the line above`() {
        assertEquals(213 to 267, trailingGlyphPlacement(lineRightsPx = listOf(200.4f), gapPx = 12, glyphWidthPx = 54))
        assertEquals(132 to 301, trailingGlyphPlacement(lineRightsPx = listOf(300.2f, 120f), gapPx = 12, glyphWidthPx = 54))
        assertEquals(262 to 316, trailingGlyphPlacement(lineRightsPx = listOf(100f, 250f), gapPx = 12, glyphWidthPx = 54))
    }
}
