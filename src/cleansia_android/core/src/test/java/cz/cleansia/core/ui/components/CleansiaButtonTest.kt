package cz.cleansia.core.ui.components

import java.io.File
import org.junit.Assert.assertTrue
import org.junit.Test

/**
 * There is no Compose test harness in this module, so the buttons' opt-in states are read as source: each one a caller
 * leaves out draws exactly what it drew before.
 */
class CleansiaButtonTest {

    @Test
    fun `a text link takes an enabled state and hands it to the button it draws`() {
        val link = body(source, "fun CleansiaTextLink(")
        assertTrue(signature(source, "fun CleansiaTextLink(").contains("enabled: Boolean = true,"))
        assertTrue(Regex("""CleansiaTextButton\(\s*onClick = onClick,\s*modifier = modifier,\s*enabled = enabled,""").containsMatchIn(link))
    }

    @Test
    fun `the primary button's disabled colours are Material's own unless the caller gives them`() {
        val parameters = signature(source, "fun CleansiaPrimaryButton(")
        assertTrue(parameters.contains("disabledContainerColor: Color = Color.Unspecified,"))
        assertTrue(parameters.contains("disabledContentColor: Color = Color.Unspecified,"))
        assertTrue(
            Regex("""ButtonDefaults\.buttonColors\([\s\S]*?disabledContainerColor = disabledContainerColor,\s*disabledContentColor = disabledContentColor,""")
                .containsMatchIn(body(source, "fun CleansiaPrimaryButton(")),
        )
    }

    @Test
    fun `the outlined button draws one line unless the caller allows more, and centres the lines it wraps`() {
        assertTrue(signature(source, "fun CleansiaOutlinedButton(").contains("maxLines: Int = 1,"))
        val button = body(source, "fun CleansiaOutlinedButton(")
        assertTrue(button.contains("maxLines = maxLines,"))
        assertTrue(button.contains("textAlign = if (maxLines > 1) TextAlign.Center else null,"))
    }

    @Test
    fun `a link that may wrap keeps its glyphs and words in one row, which the button centres`() {
        assertTrue(Regex("""LinkGlyphPlacement\.CapBand -> Row\(Modifier\.weight\(1f, fill = false\)\) \{""").containsMatchIn(body(source, "fun CleansiaTextLink(")))
    }

    private val source: String by lazy {
        sequenceOf(File("."), File("core"), File("src/cleansia_android/core"))
            .map { File(it, "src/main/java/cz/cleansia/core/ui/components/CleansiaButton.kt") }
            .first { it.isFile }
            .readText()
            .replace("\r\n", "\n")
    }

    private fun signature(text: String, declaration: String): String {
        val start = text.indexOf(declaration)
        assertTrue("$declaration not found", start >= 0)
        return text.substring(start, text.indexOf(") {", start))
    }

    private fun body(text: String, declaration: String): String {
        val start = text.indexOf(declaration)
        assertTrue("$declaration not found", start >= 0)
        val open = text.indexOf(") {", start) + 2
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
}
