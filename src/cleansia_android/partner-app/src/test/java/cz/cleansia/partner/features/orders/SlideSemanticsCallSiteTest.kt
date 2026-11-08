package cz.cleansia.partner.features.orders

import java.io.File
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test

/**
 * The Core slide commits from a drag only, so TalkBack could never take a job, set off or book. Every caller in both
 * apps — the take sheet, the Active hero, the detail's pinned move and the customer's booking slide — passes one
 * merged button with a click action through Core's `slideSemantics`; the slide itself gains no semantics. The partner
 * opts into the thumb lanes and two lines, and every locked slide is Core's own locked face. No Compose test harness
 * here, so the call sites are read as source, as `OrdersSegmentSelectionTest` does.
 */
class SlideSemanticsCallSiteTest {

    @Test
    fun `every slide in both apps passes the semantics into its modifier`() {
        val slides = appSources().flatMap { (path, text) ->
            (calls(text, "SwipeToConfirmButton(") + calls(text, "LockedSwipeToConfirm(")).map { path to it }
        }
        listOf(
            "partner/features/orders/WorkContractSheet.kt",
            "partner/features/orders/ActiveHero.kt",
            "partner/features/orders/OrderPrimaryAction.kt",
            "partner/features/orders/OrderDetailScreen.kt",
            "customer/features/booking/BookingBottomSheet.kt",
        ).forEach { file -> assertTrue("$file has no slide", slides.any { (path, _) -> path.endsWith(file) }) }
        slides.forEach { (path, args) -> assertTrue("$path passes no slideSemantics: $args", args.contains(".slideSemantics(")) }
    }

    /**
     * The customer's double tap books with the swipe's own action, and only while the swipe could. While the order is
     * placed, the slide says so with the busy overlay's words rather than repeating its label.
     */
    @Test
    fun `the booking slide's double tap places the order as its swipe does`() {
        val slide = appSources().single { (path, _) -> path.endsWith("booking/BookingBottomSheet.kt") }.second
            .let { calls(it, "SwipeToConfirmButton(").single() }
        listOf(
            "onConfirmed = confirm",
            "onCommit = confirm",
            "isBusy = submitting",
            "busyLabel = stringResource(R.string.busy_booking)",
        ).forEach { assertTrue("booking slide lacks `$it`: $slide", slide.contains(it)) }
        assertEquals("the semantics and the swipe are enabled alike: $slide", 2, Regex("""enabled = canContinue && !submitting""").findAll(slide).count())
    }

    /**
     * The detail's pinned move uses the Core slide in its lanes, a label that may take two lines and the same
     * semantics; the set-off lock and the card gate are Core's locked slide, announced as disabled with their reason.
     */
    @Test
    fun `the order detail's slides are the Core slide, the locked ones Core's locked face`() {
        val text = source("OrderPrimaryAction.kt")
        val locked = calls(text, "LockedSwipeToConfirm(")
        assertEquals(2, locked.size)
        locked.forEach { args ->
            listOf("enabled = false", "disabledReason =", "onCommit = {}").forEach { assertTrue("locked slide lacks `$it`: $args", args.contains(it)) }
        }
        calls(text, "SwipeToConfirmButton(").single().let { args ->
            listOf("thumbLanes = true", "maxLines = 2", ".slideSemantics(", "isBusy = isBusy").forEach { assertTrue("open slide lacks `$it`: $args", args.contains(it)) }
        }
        listOf("OrderDetailScreen.kt").forEach { file ->
            val slides = calls(source(file), "SwipeToConfirmButton(")
            assertTrue("$file has no slide", slides.isNotEmpty())
            slides.forEach { args ->
                listOf(".slideSemantics(", "thumbLanes = true", "maxLines = 2").forEach { assertTrue("$file slide lacks `$it`: $args", args.contains(it)) }
            }
        }
    }

    @Test
    fun `no partner slide of its own is left`() {
        val dir = featureDir()
        assertTrue("SlideToCommit.kt is still in the tree", !File(dir, "SlideToCommit.kt").exists())
        dir.listFiles { f -> f.extension == "kt" }!!.forEach { file ->
            assertTrue("${file.name} still calls SlideToCommit", !file.readText().contains("SlideToCommit("))
        }
    }

    @Test
    fun `the hero's locked slide is a disabled button that says its reason alone`() {
        val locked = calls(source("ActiveHero.kt"), "LockedSwipeToConfirm(").single()
        listOf("reason = reason", "enabled = false", "label = reason", "disabledReason = reason").forEach { assertTrue("hero locked slide lacks `$it`: $locked", locked.contains(it)) }
        assertFalse("the countdown is the status line's, not the slide's: $locked", locked.contains("detail ="))
    }

    private fun featureDir(): File = sequenceOf(File("."), File("partner-app"), File("src/cleansia_android/partner-app"))
        .map { File(it, "src/main/java/cz/cleansia/partner/features/orders") }
        .firstOrNull { it.isDirectory }
        ?: error("orders feature dir not found from working dir ${File(".").absolutePath}")

    private fun source(name: String): String = sequenceOf(File("."), File("partner-app"), File("src/cleansia_android/partner-app"))
        .map { File(it, "src/main/java/cz/cleansia/partner/features/orders/$name") }
        .firstOrNull { it.isFile }
        ?.readText()
        ?.let(::withoutComments)
        ?: error("$name not found from working dir ${File(".").absolutePath}")

    /** Every main Kotlin file of both apps, by its path from the Android root, comments removed. */
    private fun appSources(): List<Pair<String, String>> {
        val root = sequenceOf(File(".."), File("."), File("src/cleansia_android"))
            .map { it.absoluteFile.normalize() }
            .firstOrNull { File(it, "customer-app").isDirectory && File(it, "partner-app").isDirectory }
            ?: error("cleansia_android not found from ${File(".").absolutePath}")
        return listOf("partner-app", "customer-app").flatMap { module ->
            File(root, "$module/src/main/java").walkTopDown().filter { it.isFile && it.extension == "kt" }
                .map { it.relativeTo(root).invariantSeparatorsPath to withoutComments(it.readText()) }
                .toList()
        }
    }

    private fun withoutComments(text: String): String =
        text.replace("\r\n", "\n").replace(Regex("""/\*[\s\S]*?\*/|(?<![:"])//[^\n]*"""), "")

    /** The argument list of every [marker] call in [text]. */
    private fun calls(text: String, marker: String): List<String> {
        val out = mutableListOf<String>()
        var at = text.indexOf(marker)
        while (at >= 0) {
            val before = text.substring(0, at).trimEnd()
            if (!before.endsWith("fun")) out += matched(text, at + marker.length - 1, '(', ')')
            at = text.indexOf(marker, at + marker.length)
        }
        return out
    }

    private fun matched(text: String, open: Int, opener: Char, closer: Char): String {
        var depth = 0
        for (i in open until text.length) {
            when (text[i]) {
                opener -> depth++
                closer -> if (--depth == 0) return text.substring(open, i + 1)
            }
        }
        error("unbalanced `$opener` from offset $open")
    }
}
