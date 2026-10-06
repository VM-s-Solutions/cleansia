package cz.cleansia.customer.ui.theme

import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.compositeOver
import androidx.compose.ui.graphics.luminance
import cz.cleansia.core.ui.theme.primaryText
import java.io.File
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Test

/**
 * Brand-blue TEXT reads 4.5:1 in light mode (W-F2). The light primary, sky-600, is 4.10:1 on white and
 * under that on every tinted ground blue text sits on, so text — a text or outlined button's label too
 * (X1) — takes `primaryText()` from `:core`, sky-700 in light mode, while fills, borders, filled buttons
 * and standalone icons keep the primary. There is no Compose test harness in this module, so the screens
 * are read as source.
 */
class PrimaryTextContrastTest {

    /** The light grounds blue text sits on: cards, the page, the selected tint, and the primary washes behind badges and chips. */
    private val lightGrounds = mapOf(
        "white" to Color.White,
        "the page (slate-50)" to Slate50,
        "the selection tint (sky-100)" to Sky100,
        "primaryContainer at 35 % on the page" to Sky100.copy(alpha = 0.35f).compositeOver(Slate50),
        "the primary at 12 % on the page" to Sky600.copy(alpha = 0.12f).compositeOver(Slate50),
        "the primary at 20 % on white" to Sky600.copy(alpha = 0.20f).compositeOver(Color.White),
        "sky-400 at 12 % on the page" to Sky400.copy(alpha = 0.12f).compositeOver(Slate50),
    )

    @Test
    fun `sky-600 is under 4_5 to 1 on white, which is why text does not use the primary`() {
        assertTrue(contrast(Sky600, Color.White) < 4.5)
    }

    @Test
    fun `light-mode text blue reads 4_5 to 1 on every light ground`() {
        lightGrounds.forEach { (name, ground) ->
            val ratio = contrast(Sky700, ground)
            assertTrue("sky-700 on $name is ${"%.2f".format(ratio)}:1", ratio >= 4.5)
        }
    }

    @Test
    fun `the text token is sky-700 in light mode and the primary in dark`() {
        assertEquals(Sky700, LightColors.primaryText)
        assertEquals(DarkColors.primary, DarkColors.primaryText)
        assertTrue("dark mode's primary clears 4.5:1 on its surfaces", contrast(Sky400, Slate800) >= 4.5)
    }

    /**
     * No Text in the customer app takes the bare primary (or sky-600) as its colour, a button's label
     * included. The Cleansia wordmark is a logotype, which WCAG exempts.
     */
    @Test
    fun `no customer-app text is coloured with the bare primary`() {
        val offenders = mutableListOf<String>()
        sourceRoot().walkTopDown().filter { it.isFile && it.extension == "kt" }.forEach { file ->
            val rel = file.relativeTo(sourceRoot()).invariantSeparatorsPath
            if (rel in EXEMPT_FILES) return@forEach
            val text = file.readText()
            TEXT_CALL.findAll(text).forEach { call ->
                val args = argumentsFrom(text, call.range.last + 1)
                val color = COLOR_ARG.find(args)?.groupValues?.get(1).orEmpty()
                if (BARE_PRIMARY.containsMatchIn(color)) {
                    offenders += "$rel:${text.lineNumberAt(call.range.first)}"
                }
            }
        }
        assertTrue("blue text still on the bare primary (use primaryText()): $offenders", offenders.isEmpty())
    }

    /** The argument list of the call whose `(` ends just before [from], up to its matching `)`. */
    private fun argumentsFrom(text: String, from: Int): String {
        var depth = 1
        var i = from
        while (i < text.length && depth > 0) {
            when (text[i]) {
                '(' -> depth++
                ')' -> depth--
            }
            i++
        }
        return text.substring(from, (i - 1).coerceAtLeast(from))
    }

    private fun String.lineNumberAt(index: Int) = substring(0, index).count { it == '\n' } + 1

    private fun contrast(a: Color, b: Color): Double {
        val (light, dark) = listOf(a.luminance(), b.luminance()).sortedDescending()
        return (light + 0.05) / (dark + 0.05)
    }

    private fun sourceRoot(): File = sequenceOf(File("."), File("customer-app"), File("src/cleansia_android/customer-app"))
        .map { File(it, "src/main/java/cz/cleansia/customer") }
        .firstOrNull { it.isDirectory }
        ?: error("customer-app sources not found from working dir ${File(".").absolutePath}")

    private companion object {
        val EXEMPT_FILES = setOf("ui/components/CleansiaBrandWordmark.kt")
        val TEXT_CALL = Regex("""(?<![\w.])Text\(""")
        val COLOR_ARG = Regex("""(?:^|[\s,(])color\s*=\s*([^\n]*(?:\n\s*else[^\n]*)?)""")
        val BARE_PRIMARY = Regex("""colorScheme\.primary(?![A-Za-z])(?!\.copy)|\bSky600\b""")
    }
}
