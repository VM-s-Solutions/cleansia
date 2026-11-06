package cz.cleansia.core.ui.components

import androidx.compose.ui.graphics.Color
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Test

/**
 * A legal text reads in the app's body face, Nunito, with no inset of its own beside the page's. A web view cannot
 * see the fonts the app bundles, so each weight rides inside the document as a data URL.
 */
class HtmlDocumentTest {

    @Test
    fun `a legal text sets in Nunito with no side inset of its own`() {
        val html = HtmlDocument.wrap(
            "<p>x</p>",
            ink = Color.Black,
            accent = Color.Blue,
            link = Color.Blue,
            fontFaces = "@font-face { font-family: Nunito; }",
        )

        assertTrue(html, html.contains("@font-face { font-family: Nunito; }"))
        assertTrue(html, html.contains("font-family: Nunito, sans-serif;"))
        assertTrue(html, html.contains("body { margin: 0; padding: 0 0 16px;"))
    }

    @Test
    fun `each bundled weight rides in the document as a data URL`() {
        val faces = HtmlDocument.fontFaces(
            listOf(400 to byteArrayOf(1, 2, 3), 600 to byteArrayOf(4), 700 to byteArrayOf(5)),
        )

        assertEquals(
            listOf(
                "@font-face { font-family: Nunito; font-weight: 400; src: url(data:font/ttf;base64,AQID) format('truetype'); }",
                "@font-face { font-family: Nunito; font-weight: 600; src: url(data:font/ttf;base64,BA==) format('truetype'); }",
                "@font-face { font-family: Nunito; font-weight: 700; src: url(data:font/ttf;base64,BQ==) format('truetype'); }",
            ).joinToString("\n"),
            faces,
        )
    }
}
