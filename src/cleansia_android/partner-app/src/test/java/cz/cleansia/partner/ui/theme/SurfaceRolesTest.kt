package cz.cleansia.partner.ui.theme

import androidx.compose.material3.ColorScheme
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.luminance
import androidx.compose.ui.graphics.toArgb
import cz.cleansia.core.ui.theme.primaryText
import java.io.File
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Test

/**
 * Material draws a dialog and a date picker on surfaceContainerHigh, a menu on surfaceContainer, a bottom
 * sheet on surfaceContainerLow and a switch's off track on surfaceContainerHighest. Left unset, those are
 * M3's purple-tinted baseline greys (#ECE6F0 for a dialog, #F3EDF7 for a menu), so every surface role comes
 * from the app's slate family instead (F4), and the text a dialog or a menu carries reads 4.5:1 on it.
 */
class SurfaceRolesTest {

    private val slates = setOf(
        Color.White, Slate50, Slate100, Slate200, Slate300, Slate400, Slate500, Slate600, Slate700, Slate800,
        Slate900, DarkSurfaceElevated,
    )

    private fun roles(scheme: ColorScheme) = mapOf(
        "surface" to scheme.surface,
        "surfaceContainerLowest" to scheme.surfaceContainerLowest,
        "surfaceContainerLow" to scheme.surfaceContainerLow,
        "surfaceContainer" to scheme.surfaceContainer,
        "surfaceContainerHigh" to scheme.surfaceContainerHigh,
        "surfaceContainerHighest" to scheme.surfaceContainerHighest,
        "surfaceBright" to scheme.surfaceBright,
        "surfaceDim" to scheme.surfaceDim,
    )

    @Test
    fun `every surface role is one of the app's slates, not Material's purple baseline`() {
        listOf("light" to LightColors, "dark" to DarkColors).forEach { (name, scheme) ->
            roles(scheme).forEach { (role, color) ->
                assertTrue("$name $role is #${"%08X".format(color.toArgb())}", color in slates)
            }
        }
    }

    @Test
    fun `a dialog and a menu sit on slate-100 in light mode and on the elevated slate in dark`() {
        assertEquals(Slate100, LightColors.surfaceContainerHigh)
        assertEquals(Slate100, LightColors.surfaceContainer)
        assertEquals(DarkSurfaceElevated, DarkColors.surfaceContainerHigh)
        assertEquals(DarkSurfaceElevated, DarkColors.surfaceContainer)
    }

    @Test
    fun `a dialog's and a menu's text reads 4_5 to 1 on them in both schemes`() {
        listOf("light" to LightColors, "dark" to DarkColors).forEach { (name, scheme) ->
            listOf("dialog" to scheme.surfaceContainerHigh, "menu" to scheme.surfaceContainer).forEach { (where, ground) ->
                mapOf(
                    "onSurface" to scheme.onSurface,
                    "onSurfaceVariant" to scheme.onSurfaceVariant,
                    "the text blue" to scheme.primaryText,
                    "error" to scheme.error,
                ).forEach { (ink, color) ->
                    val ratio = contrast(color, ground)
                    assertTrue("$name $where $ink: ${"%.2f".format(ratio)}:1", ratio >= 4.5)
                }
            }
        }
    }

    /**
     * Material draws an off switch's thumb and border in `outline` and its track in surfaceContainerHighest,
     * and here those are the same slate (slate-200 light, slate-700 dark), so a switch left on the default off
     * colours shows a plain pill with no thumb. Every Switch takes onSurfaceVariant for its off thumb and
     * border instead, which reads 3:1 on that track. There is no Compose test harness in this module, so the
     * switches are read as source.
     */
    @Test
    fun `an off switch's thumb reads 3 to 1 on its track in both schemes`() {
        listOf("light" to LightColors, "dark" to DarkColors).forEach { (name, scheme) ->
            val ratio = contrast(scheme.onSurfaceVariant, scheme.surfaceContainerHighest)
            assertTrue("$name off thumb on surfaceContainerHighest: ${"%.2f".format(ratio)}:1", ratio >= 3.0)
        }
    }

    @Test
    fun `every switch passes its own off thumb and border`() {
        val root = sourceRoot()
        val offenders = mutableListOf<String>()
        root.walkTopDown().filter { it.isFile && it.extension == "kt" }.forEach { file ->
            val text = file.readText()
            SWITCH_CALL.findAll(text).forEach { call ->
                val args = argumentsFrom(text, call.range.last + 1)
                if (!OFF_THUMB.containsMatchIn(args) || !OFF_BORDER.containsMatchIn(args)) {
                    offenders += "${file.relativeTo(root).path}:${text.substring(0, call.range.first).count { it == '\n' } + 1}"
                }
            }
        }
        assertTrue("a Switch on the default off thumb and border: $offenders", offenders.isEmpty())
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

    private fun sourceRoot(): File = sequenceOf(File("."), File("partner-app"), File("src/cleansia_android/partner-app"))
        .map { File(it, "src/main/java/cz/cleansia/partner") }
        .firstOrNull { it.isDirectory }
        ?: error("partner-app sources not found from working dir ${File(".").absolutePath}")

    private fun contrast(a: Color, b: Color): Double {
        val (hi, lo) = listOf(a.luminance(), b.luminance()).sortedDescending()
        return (hi + 0.05) / (lo + 0.05)
    }

    private companion object {
        val SWITCH_CALL = Regex("""(?<![\w.])Switch\(""")
        val OFF_THUMB = Regex("""uncheckedThumbColor\s*=\s*MaterialTheme\.colorScheme\.onSurfaceVariant\b""")
        val OFF_BORDER = Regex("""uncheckedBorderColor\s*=\s*MaterialTheme\.colorScheme\.onSurfaceVariant\b""")
    }
}
