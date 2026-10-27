package cz.cleansia.partner.features.orders

import java.io.File
import org.junit.Assert.assertTrue
import org.junit.Test

/**
 * [CompletionStepTest] proves the resolver; this proves the footer renders through it. The footer is a
 * composable with no seam, so the source is read, scoped to the In progress branch of
 * `OrderPrimaryAction` and to the screen's own wiring.
 *
 * Evidence here is Kotlin source read off disk, which Gradle does not track as an input — this file only
 * means something under `testDebugUnitTest --rerun-tasks --no-build-cache`.
 */
class CrewCloseCallSiteTest {

    private val featureDir: File = sequenceOf(
        File("."),
        File("partner-app"),
        File("src/cleansia_android/partner-app"),
    ).map { File(it, "src/main/java/cz/cleansia/partner/features/orders") }
        .firstOrNull { it.isDirectory }
        ?: error("orders feature dir not found from working dir ${File(".").absolutePath}")

    private val primaryAction = File(featureDir, "OrderPrimaryAction.kt").readText()
    private val detailScreen = File(featureDir, "OrderDetailScreen.kt").readText()

    @Test
    fun `on a crew job the slide marks the cleaner's part, and a marked cleaner waits`() {
        val inProgress = body(primaryAction, "OrderStatus._4 ->")
        val mark = body(inProgress, "if (isAssignedToCurrentUser && completionStep == CompletionStep.MarkMyPart)")
        assertTrue("the mark slide must commit through onMarkMyPartDone", mark.contains("onCommit = onMarkMyPartDone"))
        assertTrue("the mark slide must read its own label", mark.contains("R.string.slide_to_mark_my_part_done"))

        val waiting = body(inProgress, "else if (isAssignedToCurrentUser && completionStep == CompletionStep.WaitingForCrew)")
        assertTrue("a marked cleaner must see the waiting line", waiting.contains("R.string.order_my_part_done_waiting"))
    }

    @Test
    fun `the screen hands the footer the view model's step and its mark`() {
        assertTrue(
            "the screen must collect the step from the view model",
            detailScreen.contains("val completionStep by viewModel.completionStep.collectAsStateWithLifecycle()"),
        )
        assertTrue(
            "the screen must wire the mark to the view model",
            detailScreen.contains("onMarkMyPartDone = viewModel::markMyPartDone"),
        )
        val footer = body(detailScreen, "private fun StickyActionFooter(")
        val action = arguments(footer, "OrderPrimaryAction(")
        assertTrue("the footer must pass the step to the action", action.contains("completionStep = completionStep"))
        assertTrue("the footer must pass the mark to the action", action.contains("onMarkMyPartDone = onMarkMyPartDone"))
    }

    /** The `{…}` that opens first after [marker], which must occur exactly once in [text]. */
    private fun body(text: String, marker: String): String = enclosed(text, marker, '{', '}', from = unique(text, marker) + marker.length)

    /** The `(…)` of the call [marker] names, which must end with its opening parenthesis. */
    private fun arguments(text: String, marker: String): String =
        enclosed(text, marker, '(', ')', from = unique(text, marker) + marker.length - 1)

    private fun unique(text: String, marker: String): Int {
        val start = text.indexOf(marker)
        assertTrue("no `$marker` found", start >= 0)
        assertTrue("`$marker` must be unique for this extraction to mean anything", start == text.lastIndexOf(marker))
        return start
    }

    private fun enclosed(text: String, marker: String, opening: Char, closing: Char, from: Int): String {
        val open = text.indexOf(opening, from)
        var depth = 0
        for (i in open until text.length) {
            when (text[i]) {
                opening -> depth++
                closing -> if (--depth == 0) return text.substring(open, i + 1)
            }
        }
        error("unbalanced `$opening` after `$marker`")
    }
}
