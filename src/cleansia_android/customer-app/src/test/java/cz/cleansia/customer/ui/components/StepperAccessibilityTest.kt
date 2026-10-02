package cz.cleansia.customer.ui.components

import androidx.compose.ui.Modifier
import androidx.compose.ui.semantics.SemanticsActions
import androidx.compose.ui.semantics.SemanticsConfiguration
import androidx.compose.ui.semantics.SemanticsModifier
import androidx.compose.ui.semantics.SemanticsProperties
import androidx.compose.ui.semantics.getOrNull
import java.io.File
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test

/**
 * The booking and schedule size steppers are each one TalkBack node, read as a name and a value and
 * adjusted like a slider, as iOS `PropertyStepper` is one adjustable element. There is no Compose
 * test harness in this module, so the node's semantics are read off the modifier, and the screens
 * as source.
 */
class StepperAccessibilityTest {

    private fun semantics(value: Int, range: IntRange, moved: MutableList<Int>): SemanticsConfiguration =
        (Modifier.adjustableStepper("Your home", "3 rooms", value, range) { moved += it } as SemanticsModifier)
            .semanticsConfiguration

    @Test
    fun `the stepper is one node read as its name and value`() {
        val config = semantics(3, 1..8, mutableListOf())

        assertTrue("the glyph buttons must not be nodes of their own", config.isClearingSemantics)
        assertEquals(listOf("Your home"), config.getOrNull(SemanticsProperties.ContentDescription))
        assertEquals("3 rooms", config.getOrNull(SemanticsProperties.StateDescription))
    }

    /** Compose moves a range node by (end - start) / (steps + 1), so one adjustment must be one step. */
    @Test
    fun `one adjustment moves the value by one`() {
        val info = semantics(3, 1..8, mutableListOf())[SemanticsProperties.ProgressBarRangeInfo]

        assertEquals(3f, info.current)
        assertEquals(1f..8f, info.range)
        assertEquals(1f, (info.range.endInclusive - info.range.start) / (info.steps + 1))
    }

    @Test
    fun `an adjustment reports the new value inside the range, and a refused one nothing`() {
        val moved = mutableListOf<Int>()
        val setProgress = semantics(8, 1..8, moved)[SemanticsActions.SetProgress].action!!

        assertTrue(setProgress(7f))
        assertFalse("past the cap", setProgress(9f))
        assertFalse("the same value", setProgress(8f))
        assertEquals(listOf(7), moved)
    }

    @Test
    fun `both size steppers are adjustable nodes that tick`() {
        listOf("features/booking/ServicesStep.kt", "features/recurring/CreateRecurringScreen.kt").forEach { file ->
            val text = source(file)
            assertTrue("$file must give its stepper the adjustable node", text.contains(".adjustableStepper("))
            assertTrue("$file must tick on each step", text.contains("rememberStepperTick()"))
        }
    }

    /** The one-off counter's glyphs are 28dp to fit the size row on a 360dp phone; its targets are 48dp. */
    @Test
    fun `the booking counter's steps are 48dp targets`() {
        val services = source("features/booking/ServicesStep.kt")
        assertTrue(services.contains("private val StepTarget = 48.dp"))
        assertTrue(services.contains("measurable.measure(Constraints.fixed(target, target))"))
    }

    private fun source(path: String): String = sequenceOf(
        File("."),
        File("customer-app"),
        File("src/cleansia_android/customer-app"),
    ).map { File(it, "src/main/java/cz/cleansia/customer/$path") }
        .firstOrNull { it.isFile }
        ?.readText()
        ?: error("$path not found from working dir ${File(".").absolutePath}")
}
