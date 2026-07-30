package cz.cleansia.core.ui

import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertNotNull
import org.junit.Assert.assertTrue
import org.junit.Test
import java.io.File
import kotlin.math.hypot
import kotlin.math.max

/**
 * Every way of breaking the launcher / notification marks is silent — the build
 * is green, the icon just looks wrong on a device nobody has to hand:
 *
 *  - put anything but a flat alpha silhouette in `ic_notification` and the
 *    status bar draws a solid white blob, because Android keeps only the alpha
 *    channel of a small icon;
 *  - let the launcher mark drift outside the 66dp safe circle and a circle-mask
 *    launcher shears the ends off the wordmark (a rounded-square launcher shows
 *    it intact, so it reproduces on some phones only);
 *  - re-point the notification icon at the 108dp launcher foreground and the
 *    glyph renders at ~40% of the canvas, half the size of a purpose-built one.
 *
 * So pin the geometry for both apps. Mirrors `ConsentCatalogTest`: the invariant
 * belongs to the brand, not to either app, so it is asserted once from `:core`.
 */
class BrandIconCatalogTest {
    private companion object {
        val APPS = listOf("customer-app", "partner-app")

        /** Google's adaptive-icon contract: 108dp canvas, 66dp guaranteed-visible circle. */
        const val FOREGROUND_VIEWPORT = 108.0
        const val SAFE_ZONE_RADIUS = 33.0

        /** Material's notification small icon: a 24dp canvas the glyph nearly fills. */
        const val NOTIFICATION_VIEWPORT = 24.0
        const val MIN_NOTIFICATION_FILL = 0.75

        val NUMBER = Regex("""[-+]?[0-9]*\.?[0-9]+""")
    }

    private fun androidRoot(): File {
        var dir: File? = File("").absoluteFile
        while (dir != null && !File(dir, "settings.gradle.kts").isFile) {
            dir = dir.parentFile
        }
        assertNotNull("could not locate the cleansia_android Gradle root", dir)
        return dir!!
    }

    private fun res(app: String, path: String) = File(androidRoot(), "$app/src/main/res/$path")

    private fun attribute(xml: String, name: String): String? =
        Regex("""android:$name="([^"]*)"""").find(xml)?.groupValues?.get(1)

    /**
     * Every coordinate the path mentions, control points included. That is a
     * superset of the drawn outline, so a safe-zone assertion over it can only
     * err towards rejecting a mark that would in fact have fitted.
     */
    private fun coordinates(pathData: String): List<Pair<Double, Double>> {
        val tokens = Regex("""[A-Za-z]|$NUMBER""").findAll(pathData).map { it.value }.toList()
        val points = mutableListOf<Pair<Double, Double>>()
        var command = ' '
        var x = 0.0
        var y = 0.0
        var startX = 0.0
        var startY = 0.0
        var i = 0
        while (i < tokens.size) {
            if (tokens[i].first().isLetter()) {
                command = tokens[i].first()
                i++
            } else {
                check(command.uppercaseChar() != 'Z') { "malformed pathData near `${tokens[i]}`" }
            }
            if (i >= tokens.size && command.uppercaseChar() != 'Z') break
            val relative = command.isLowerCase()
            when (command.uppercaseChar()) {
                'Z' -> {
                    x = startX
                    y = startY
                }
                'H', 'V' -> {
                    val v = tokens[i++].toDouble()
                    if (command.uppercaseChar() == 'H') x = if (relative) x + v else v
                    else y = if (relative) y + v else v
                    points += x to y
                }
                else -> {
                    val arity = when (command.uppercaseChar()) {
                        'C' -> 3
                        'Q', 'S' -> 2
                        else -> 1
                    }
                    repeat(arity) {
                        val px = tokens[i++].toDouble()
                        val py = tokens[i++].toDouble()
                        val ax = if (relative) x + px else px
                        val ay = if (relative) y + py else py
                        points += ax to ay
                        if (it == arity - 1) {
                            x = ax
                            y = ay
                        }
                    }
                    if (command.uppercaseChar() == 'M') {
                        startX = x
                        startY = y
                        command = if (relative) 'l' else 'L'
                    }
                }
            }
        }
        return points
    }

    @Test
    fun `both apps declare the same adaptive icon layers`() {
        for (wrapper in listOf("ic_launcher.xml", "ic_launcher_round.xml")) {
            val declarations = APPS.map {
                res(it, "mipmap-anydpi-v26/$wrapper").readText().replace(Regex("""\s+"""), " ").trim()
            }
            assertEquals(
                "$wrapper differs between the apps — the customer/partner difference belongs in " +
                    "the mark itself, not in how the icon is assembled",
                declarations[0],
                declarations[1],
            )
            for (layer in listOf("background", "foreground", "monochrome")) {
                assertTrue(
                    "$wrapper is missing the $layer layer",
                    declarations[0].contains("""<$layer android:drawable="@drawable/ic_launcher_$layer" />"""),
                )
            }
        }
    }

    @Test
    fun `the launcher mark stays inside the adaptive icon safe zone`() {
        for (app in APPS) {
            val xml = res(app, "drawable/ic_launcher_foreground.xml").readText()
            assertEquals(
                "$app foreground is not authored on the 108dp adaptive-icon canvas",
                FOREGROUND_VIEWPORT,
                attribute(xml, "viewportWidth")!!.toDouble(),
                0.0,
            )
            val centre = FOREGROUND_VIEWPORT / 2
            val reach = coordinates(attribute(xml, "pathData")!!)
                .maxOf { hypot(it.first - centre, it.second - centre) }
            assertTrue(
                "$app foreground reaches ${"%.2f".format(reach)}dp from centre — outside the " +
                    "${SAFE_ZONE_RADIUS}dp safe radius, so a circle-mask launcher will clip it",
                reach <= SAFE_ZONE_RADIUS,
            )
        }
    }

    @Test
    fun `the notification icon is a flat silhouette that fills its canvas`() {
        for (app in APPS) {
            val file = res(app, "drawable/ic_notification.xml")
            val xml = file.readText()
            assertEquals(
                "$app notification icon is not on the 24dp small-icon canvas — reusing the 108dp " +
                    "launcher foreground renders the mark at roughly half size",
                NOTIFICATION_VIEWPORT,
                attribute(xml, "viewportWidth")!!.toDouble(),
                0.0,
            )
            assertFalse(
                "$app notification icon carries a gradient — Android keeps only the alpha channel " +
                    "of a small icon, so it would flatten into a white blob",
                xml.contains("<gradient"),
            )
            assertFalse(
                "$app notification icon carries a stroke — small icons are fills only",
                xml.contains("android:strokeColor"),
            )
            for (fill in Regex("""android:fillColor="([^"]*)"""").findAll(xml).map { it.groupValues[1] }) {
                assertTrue(
                    "$app notification icon fills with `$fill`; a small icon must be a single " +
                        "opaque silhouette that the system tints",
                    fill.equals("@android:color/white", ignoreCase = true) ||
                        fill.equals("#FFFFFF", ignoreCase = true) ||
                        fill.equals("#FFFFFFFF", ignoreCase = true),
                )
            }

            val points = coordinates(attribute(xml, "pathData")!!)
            val width = points.maxOf { it.first } - points.minOf { it.first }
            val height = points.maxOf { it.second } - points.minOf { it.second }
            assertTrue(
                "$app notification glyph spans only ${"%.2f".format(max(width, height))} of the " +
                    "${NOTIFICATION_VIEWPORT}dp canvas — it will render smaller than every other " +
                    "icon in the status bar",
                max(width, height) >= NOTIFICATION_VIEWPORT * MIN_NOTIFICATION_FILL,
            )
            assertTrue(
                "$app notification glyph overflows its viewport and will be clipped square",
                points.all { it.first in 0.0..NOTIFICATION_VIEWPORT && it.second in 0.0..NOTIFICATION_VIEWPORT },
            )
        }
    }

    @Test
    fun `the monochrome layer draws the same mark as the foreground`() {
        for (app in APPS) {
            assertEquals(
                "$app themed icon would show a different mark than its launcher icon",
                attribute(res(app, "drawable/ic_launcher_foreground.xml").readText(), "pathData"),
                attribute(res(app, "drawable/ic_launcher_monochrome.xml").readText(), "pathData"),
            )
        }
    }

    @Test
    fun `both apps carry the splash tagline in every locale`() {
        for (app in APPS) {
            for (locale in listOf("values", "values-cs", "values-sk", "values-uk", "values-ru")) {
                val file = res(app, "$locale/strings.xml")
                assertTrue("$file does not exist", file.isFile)
                assertTrue(
                    "$app/$locale is missing `splash_tagline` — the splash would render English",
                    file.readText().contains("""<string name="splash_tagline">"""),
                )
            }
        }
    }
}
