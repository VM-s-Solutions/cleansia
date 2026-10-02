package cz.cleansia.customer.features.home

import androidx.compose.ui.unit.dp
import java.io.File
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Test

/**
 * The quick-size slide's mascot sits bottom-right under the steppers, and a title that wraps (uk and
 * ru on a 360dp phone, more locales on a narrower one) pushes the steppers down into it. There is no
 * Compose harness in this module, so the card's geometry is checked as arithmetic over the values the
 * card draws, and the wiring as source.
 */
class QuickSizeMascotTest {

    /** 196dp card less 20dp padding top and bottom. */
    private val innerHeight = 156

    /** Title lines of 24sp, a 2dp spacer, then the stepper row's 6dp padding and its 36dp capsule. */
    private fun capsuleBottom(titleLines: Int) = titleLines * 24 + 2 + 6 + 36

    @Test
    fun `the mascot stays below the steppers on one title line and on two`() {
        for (lines in 1..2) {
            val mascotTop = innerHeight - quickSizeMascotSize(lines).value.toInt()
            assertTrue(
                "$lines title line(s): the capsules end at ${capsuleBottom(lines)}dp, the mascot starts at ${mascotTop}dp",
                mascotTop > capsuleBottom(lines),
            )
        }
    }

    @Test
    fun `the mascot keeps its size while the title fits one line`() {
        assertEquals(72.dp, quickSizeMascotSize(1))
    }

    @Test
    fun `the card sizes the mascot from the title's line count`() {
        val home = File(
            sequenceOf(File("."), File("customer-app"), File("src/cleansia_android/customer-app"))
                .map { File(it, "src/main/java/cz/cleansia/customer/features/home/HomeTab.kt") }
                .first { it.isFile }
                .path,
        ).readText()
        val card = home.substringAfter("private fun QuickSizeSlideCard(").substringBefore("private fun QuickSizeStepper(")
        assertTrue(card.contains("onTextLayout = { titleLines = it.lineCount }"))
        assertTrue(card.contains(".size(quickSizeMascotSize(titleLines))"))
        assertTrue("the title must stay capped at two lines", card.contains("maxLines = 2"))
    }
}
