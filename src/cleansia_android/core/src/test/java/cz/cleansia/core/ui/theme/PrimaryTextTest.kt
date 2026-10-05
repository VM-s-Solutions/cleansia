package cz.cleansia.core.ui.theme

import androidx.compose.material3.darkColorScheme
import androidx.compose.material3.lightColorScheme
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.compositeOver
import androidx.compose.ui.graphics.luminance
import cz.cleansia.core.ui.components.HtmlDocument
import java.io.File
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Test

/**
 * Text links and text buttons read in the text blue — sky-700 on a light scheme, the scheme's own primary
 * on a dark one — in both apps (owner, 2026-10-05). Filled buttons, fills, borders and standalone icons keep
 * the primary; an icon inside a link or a button takes its label's ink. There is no Compose test harness in
 * these modules, so the controls are read as source.
 */
class PrimaryTextTest {

    private val sky100 = Color(0xFFE0F2FE)
    private val sky400 = Color(0xFF38BDF8)
    private val sky600 = Color(0xFF0284C7)
    private val slate50 = Color(0xFFF8FAFC)
    private val slate800 = Color(0xFF1E293B)

    private val sky700 = Color(0xFF0369A1)

    private val light = lightColorScheme(
        primary = sky600, primaryContainer = sky100, onPrimaryContainer = Color(0xFF0C4A6E),
        surface = Color.White, background = slate50,
    )
    private val dark = darkColorScheme(
        primary = sky400, primaryContainer = sky700, onPrimaryContainer = sky100, surface = slate800,
    )

    @Test
    fun `the text blue is sky-700 on a light scheme and the primary on a dark one`() {
        assertEquals(PrimaryTextLight, light.primaryText)
        assertEquals(Color(0xFF0369A1), PrimaryTextLight)
        assertEquals(sky400, dark.primaryText)
    }

    /**
     * A badge, chip or initial on the primaryContainer wash (F-a): the dark text blue is 2.77:1 on its own
     * container, so dark mode writes onPrimaryContainer there; light mode keeps the text blue.
     */
    @Test
    fun `blue text on the primary container reads 4_5 to 1 in both schemes`() {
        assertEquals(PrimaryTextLight, light.primaryTextOnContainer)
        assertEquals(dark.onPrimaryContainer, dark.primaryTextOnContainer)
        assertTrue(contrast(dark.primaryText, dark.primaryContainer) < 4.5)
        listOf(light, dark).forEach { scheme ->
            val ratio = contrast(scheme.primaryTextOnContainer, scheme.primaryContainer)
            assertTrue("${"%.2f".format(ratio)}:1", ratio >= 4.5)
        }
    }

    /** No text drawn straight on a primaryContainer fill takes the text blue or the primary. */
    @Test
    fun `no text on a primary-container fill takes the text blue`() {
        val fill = Regex("""(?:background\(|color = )MaterialTheme\.colorScheme\.primaryContainer(?![.\w])""")
        val offenders = sources.flatMap { file ->
            fill.findAll(file.text).filter { match ->
                textsOnFill(file.text, match.range.first).any { BLUE_TEXT.containsMatchIn(it) }
            }.map { match -> "${file.module}/${file.rel}:${file.text.substring(0, match.range.first).count { it == '\n' } + 1}" }
        }
        assertEquals(emptyList<String>(), offenders)
    }

    @Test
    fun `the light primary is under 4_5 to 1 on white, which is why text does not use it`() {
        assertTrue(contrast(sky600, Color.White) < 4.5)
    }

    /** Where a link or a text button's label sits: cards, the page, a chip's wash, a dialog. */
    @Test
    fun `the text blue reads 4_5 to 1 on every ground a link or a label sits on`() {
        mapOf(
            "white" to Color.White,
            "the page (slate-50)" to slate50,
            "sky-100" to sky100,
            "a picked chip (primary 12 % on white)" to sky600.copy(alpha = 0.12f).compositeOver(Color.White),
            "an action chip (primary 10 % on white)" to sky600.copy(alpha = 0.10f).compositeOver(Color.White),
            "Material's dialog container (surfaceContainerHigh)" to light.surfaceContainerHigh,
        ).forEach { (name, ground) ->
            val ratio = contrast(light.primaryText, ground)
            assertTrue("sky-700 on $name is ${"%.2f".format(ratio)}:1", ratio >= 4.5)
        }
        val darkRatio = contrast(dark.primaryText, slate800)
        assertTrue("sky-400 on slate-800 is ${"%.2f".format(darkRatio)}:1", darkRatio >= 4.5)
    }

    @Test
    fun `a legal text's links take the text blue while its quote rule keeps the primary`() {
        val html = HtmlDocument.wrap("<p>x</p>", ink = Color.Black, accent = sky600, link = PrimaryTextLight)
        assertTrue(html, html.contains("a { color: #0369A1; }"))
        assertTrue(html, html.contains("border-left: 3px solid #0284C7;"))
        assertTrue(source("core", "ui/components/HtmlContentView.kt").contains("val link = primaryText()"))
    }

    @Test
    fun `the sources are still found`() {
        assertTrue(sources.size > 100)
    }

    /** Material's default text-button ink is the primary, so a TextButton without its own colours is the bug. */
    @Test
    fun `no text button is left on Material's default ink`() {
        val offenders = calls("TextButton").filter { call -> !call.args.contains(COLORS_ARG) }
        assertEquals(emptyList<String>(), offenders.map { it.where })
    }

    /** An outlined button either passes its own colours or gives every label and icon in it its own ink. */
    @Test
    fun `no outlined button's label or icon is left on Material's default ink`() {
        val offenders = calls("OutlinedButton").filter { call ->
            !call.args.contains(COLORS_ARG) && (
                calls("Text", call.body).any { !it.args.contains(COLOR_ARG) } ||
                    calls("Icon", call.body).any { !it.args.contains(TINT_ARG) }
                )
        }
        assertEquals(emptyList<String>(), offenders.map { it.where })
    }

    @Test
    fun `no button's content is coloured with the bare primary`() {
        val offenders = BUTTONS.flatMap { name -> calls(name) }.filter { call ->
            BARE_PRIMARY_CONTENT.containsMatchIn(call.args) ||
                calls("Text", call.body).any { BARE_PRIMARY_COLOR.containsMatchIn(it.args) } ||
                calls("Icon", call.body).any { BARE_PRIMARY_TINT.containsMatchIn(it.args) }
        }
        assertEquals(emptyList<String>(), offenders.map { it.where })
    }

    /** The shared widgets both apps draw: no text in `:core` takes the bare primary, and the consent links do not either. */
    @Test
    fun `no shared widget draws text in the bare primary`() {
        val offenders = sources.filter { it.module == "core" }.flatMap { file ->
            calls("Text", file.text, file).filter { BARE_PRIMARY_COLOR.containsMatchIn(it.args) }
        }
        assertEquals(emptyList<String>(), offenders.map { it.where })
        assertTrue(source("core", "ui/components/CleansiaConsentCheckbox.kt").contains("val linkColor = primaryText()"))
        assertTrue(source("core", "ui/components/CleansiaChip.kt").contains("val labelColor = if (isSelected) primaryText()"))
    }

    /** A selected tab's icon and its label are one control, so they share one ink. */
    @Test
    fun `each app's selected tab draws its icon and label in the text blue`() {
        listOf(
            "customer-app" to "features/main/MainShell.kt",
            "partner-app" to "features/main/FloatingIslandBottomBar.kt",
        ).forEach { (module, path) ->
            val text = source(module, path)
            assertTrue(
                "$module $path",
                Regex("""val color = if \(isSelected\) MaterialTheme\.colorScheme\.primaryText\b""").containsMatchIn(text),
            )
            assertTrue("$module $path", !Regex("""isSelected\) MaterialTheme\.colorScheme\.primary\b""").containsMatchIn(text))
        }
    }

    /** The partner app's own text links and text buttons, each with the icon that sits in it. */
    @Test
    fun `the partner app's text links read in the text blue, icon and label alike`() {
        listOf(
            "features/invoices/InvoiceDetailScreen.kt" to "R.string.invoice_view_period_pay",
            "features/dashboard/DashboardScreen.kt" to "R.string.dash_earnings_view_details",
            "features/orders/PendingOffersCard.kt" to "R.string.offers_card_cta",
            "features/orders/OrdersListScreen.kt" to "currentSort.labelRes",
            "features/orders/RegistrationLockScreen.kt" to "ctaLabelRes",
        ).forEach { (path, label) ->
            val text = source("partner-app", path)
            val call = calls("Text", text).single { it.args.contains(label) }
            assertTrue("$path $label", call.args.contains("color = primaryText()"))
            val next = text.substring(call.end, TEXT_CALL.find(text, call.end)?.range?.first ?: text.length)
            if (next.contains("Icon(")) assertTrue("$path icon after $label", next.contains("tint = primaryText()"))
        }
        assertTrue(source("partner-app", "features/orders/CustomerCard.kt").contains("tint = primaryText()"))
        assertTrue(source("partner-app", "features/settings/LanguageChooser.kt").contains("val tint = primaryText()"))
        assertTrue(source("partner-app", "features/orders/PhotosSection.kt").contains("val tint = primaryText()"))
    }

    /**
     * The partner app's informational blue text — card eyebrows, pay amounts, the selected segment, step
     * counters, initials, status words — reads in the text blue as its links do (owner, 2026-10-05, Y3); an
     * icon on the same line takes the same ink. Fills, borders and standalone icons keep the primary.
     */
    @Test
    fun `no partner-app text draws in the bare primary`() {
        val offenders = sources.filter { it.module == "partner-app" }.flatMap { file ->
            calls("Text", file.text, file).filter { BARE_PRIMARY_COLOR.containsMatchIn(it.args) }
        }
        assertEquals(emptyList<String>(), offenders.map { it.where })
        // Texts whose ink arrives through a value rather than their own `color =`.
        listOf(
            "features/orders/PaymentCard.kt" to Regex("""valueColor = primaryText\(\)"""),
            "features/orders/OrdersListScreen.kt" to Regex("""starts_soon\),\s*tint = primaryText\(\)"""),
            "features/dashboard/DashboardScreen.kt" to Regex("""val color = if \(up\) primaryText\(\)"""),
            "features/profile/DocumentsSectionScreen.kt" to Regex("""document_status_approved\) to primaryText\(\)"""),
            "features/orders/OrderStatusProgressBar.kt" to Regex("""StepState\.Current -> primaryText\(\)"""),
            "features/profile/LegalDocumentsScreen.kt" to Regex("""tint = if \(accepted\) primaryText\(\)"""),
            "features/orders/PendingOffersCard.kt" to Regex("""Icons\.Outlined\.Schedule,\s*contentDescription = null,\s*tint = primaryText\(\)"""),
            "features/orders/PendingOffersScreen.kt" to Regex("""Icons\.Outlined\.Schedule,\s*contentDescription = null,\s*tint = primaryText\(\)"""),
        ).forEach { (path, ink) -> assertTrue(path, ink.containsMatchIn(source("partner-app", path))) }
    }

    // ── source reading ──

    private class SourceFile(val module: String, val rel: String, val text: String)

    private class Call(val where: String, val args: String, val body: String, val end: Int)

    private val androidRoot: File = sequenceOf(File(".."), File("."), File("src/cleansia_android"))
        .map { it.absoluteFile.normalize() }
        .firstOrNull { File(it, "customer-app").isDirectory && File(it, "partner-app").isDirectory }
        ?: error("cleansia_android not found from ${File(".").absolutePath}")

    private val sources: List<SourceFile> by lazy {
        listOf("core", "customer-app", "partner-app").flatMap { module ->
            val root = File(androidRoot, "$module/src/main/java")
            root.walkTopDown().filter { it.isFile && it.extension == "kt" }
                .map { SourceFile(module, it.relativeTo(root).path, it.readText()) }
                .toList()
        }
    }

    private fun source(module: String, path: String): String =
        sources.single { it.module == module && it.rel.endsWith(path) }.text

    private fun calls(name: String): List<Call> = sources.flatMap { calls(name, it.text, it) }

    /**
     * Every `name(` call in [text], written bare or fully qualified as `androidx.compose.material3.name(`: its
     * argument list and, when it has one, its trailing lambda.
     */
    private fun calls(name: String, text: String, file: SourceFile? = null): List<Call> =
        Regex("""(?<![\w.])(?:androidx\.compose\.material3\.)?$name\(""").findAll(text).mapNotNull { match ->
            val lineStart = text.lastIndexOf('\n', match.range.first) + 1
            if (text.substring(lineStart, match.range.first).trimStart().startsWith("import")) return@mapNotNull null
            val argsEnd = closing(text, match.range.last + 1, '(', ')')
            val args = text.substring(match.range.last + 1, argsEnd - 1)
            val after = text.substring(argsEnd).trimStart()
            val body = if (after.startsWith("{")) {
                val open = text.indexOf('{', argsEnd)
                text.substring(open + 1, closing(text, open + 1, '{', '}') - 1)
            } else {
                ""
            }
            val line = text.substring(0, match.range.first).count { it == '\n' } + 1
            Call("${file?.module}/${file?.rel}:$line", args, body, argsEnd)
        }.toList()

    /**
     * The argument lists of the texts drawn on the fill at [at]: the Text calls in the trailing lambda of the
     * call whose argument list holds it — or, when that call is a `Text` wearing the fill in its own
     * `Modifier.background(...)` (CleansiaSectionHeader's badge), that Text itself.
     */
    private fun textsOnFill(text: String, at: Int): List<String> {
        var open = enclosingParen(text, at)
        if (callee(text, open) == "background") open = enclosingParen(text, open)
        val argsEnd = closing(text, open + 1, '(', ')')
        if (callee(text, open) == "Text") return listOf(text.substring(open + 1, argsEnd - 1))
        if (!text.substring(argsEnd).trimStart().startsWith("{")) return emptyList()
        val brace = text.indexOf('{', argsEnd)
        return calls("Text", text.substring(brace + 1, closing(text, brace + 1, '{', '}') - 1)).map { it.args }
    }

    /** The `(` of the call whose argument list holds [at]. */
    private fun enclosingParen(text: String, at: Int): Int {
        var depth = 0
        var i = at
        while (i > 0) {
            i--
            when (text[i]) {
                ')' -> depth++
                '(' -> if (depth == 0) break else depth--
            }
        }
        return i
    }

    /** The name called at the `(` at [open]. */
    private fun callee(text: String, open: Int): String = text.take(open).takeLastWhile { it.isLetterOrDigit() || it == '_' }

    private fun closing(text: String, from: Int, open: Char, close: Char): Int {
        var depth = 1
        var i = from
        while (i < text.length && depth > 0) {
            when (text[i]) {
                open -> depth++
                close -> depth--
            }
            i++
        }
        return i
    }

    private fun contrast(a: Color, b: Color): Double {
        val (hi, lo) = listOf(a.luminance(), b.luminance()).sortedDescending()
        return (hi + 0.05) / (lo + 0.05)
    }

    private companion object {
        val TEXT_CALL = Regex("""(?<![\w.])Text\(""")
        val BUTTONS = listOf("TextButton", "OutlinedButton", "CleansiaTextButton")
        val COLORS_ARG = Regex("""\bcolors\s*=""")
        val COLOR_ARG = Regex("""(?:^|[\s,(])color\s*=""")
        val TINT_ARG = Regex("""\btint\s*=""")
        const val BARE = """MaterialTheme\.colorScheme\.primary(?![A-Za-z])(?!\.copy)"""
        val BARE_PRIMARY_CONTENT = Regex("""contentColor\s*=\s*$BARE""")
        val BARE_PRIMARY_COLOR = Regex("""(?:^|[\s,(])color\s*=\s*[^\n]*$BARE""")
        val BARE_PRIMARY_TINT = Regex("""\btint\s*=\s*$BARE""")
        val BLUE_TEXT = Regex("""(?:^|[\s,(])color\s*=\s*[^\n]*(?:$BARE|(?<![\w.])primaryText\(\)|colorScheme\.primaryText\b)""")
    }
}
