package cz.cleansia.customer.features.main

import androidx.compose.ui.unit.dp
import java.io.File
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test

/**
 * Every tab root ends its scroll content above the Book FAB, not just above the pill. The four tabs
 * used to reserve a fixed 108dp that ignored the nav-bar inset, so with 3-button navigation about
 * 28dp of the last item sat under the pill and the FAB. There is no Compose test harness in this
 * module, so — as in [BottomNavLabelTruncationTest] — the tabs are read as source.
 */
class TabRootFabClearanceTest {

    /** The box is as tall as its tallest child, and the FAB is taller than the pill. */
    @Test
    fun `the clearance is the bar box plus a 16dp gap`() {
        val shell = source("features/main/MainShell.kt")
        val bar = shell.substringAfter("private fun CustomBottomBar(")
        val padding = Regex("""vertical = (\d+)\.dp""").find(bar)!!.groupValues[1].toInt()
        val pill = Regex("""\.height\((\d+)\.dp\)""").find(bar)!!.groupValues[1].toInt()
        val fab = Regex("""\.size\((\d+)\.dp\)""")
            .find(shell.substringAfter("private fun BookFab("))!!.groupValues[1].toInt()

        assertEquals((padding + maxOf(pill, fab) + padding + 16).dp, MainShellBottomClearance)
    }

    /** The host pads the nav-bar inset itself, so the shell's inset is the same clearance. */
    @Test
    fun `the shell snackbar clears the Book FAB by the same gap`() {
        assertTrue(
            "MainShell must lift the snackbar by MainShellBottomClearance, not into the Book FAB",
            source("features/main/MainShell.kt").contains("SnackbarInsetScope(MainShellBottomClearance)"),
        )
    }

    @Test
    fun `every tab root adds the nav-bar inset to the clearance`() {
        for (tab in TAB_ROOTS) {
            val text = source(tab)
            assertTrue(
                "$tab must end its scroll content with the nav-bar inset plus MainShellBottomClearance",
                text.contains("Spacer(Modifier.navigationBarsPadding().height(MainShellBottomClearance))"),
            )
            assertFalse(
                "$tab still reserves the fixed 108dp that ignores the nav-bar inset",
                text.contains("Spacer(Modifier.height(108.dp))"),
            )
        }
    }

    /** The pager runs under the bar, so a state centred in the full height would read low. */
    @Test
    fun `the orders empty and error states centre above the bottom chrome`() {
        val container = source("features/orders/OrdersTab.kt")
            .substringAfter("private fun ScrollableStateContainer(")
            .substringBefore("/* ── Content ── */")
        assertTrue(
            "ScrollableStateContainer must pad the nav inset and MainShellBottomClearance off the centred box",
            container.contains(".navigationBarsPadding()") &&
                container.contains(".padding(bottom = MainShellBottomClearance)"),
        )
    }

    private fun source(path: String): String = sequenceOf(
        File("."),
        File("customer-app"),
        File("src/cleansia_android/customer-app"),
    ).map { File(it, "src/main/java/cz/cleansia/customer/$path") }
        .firstOrNull { it.isFile }
        ?.readText()
        ?: error("$path not found from working dir ${File(".").absolutePath}")

    private companion object {
        val TAB_ROOTS = listOf(
            "features/home/HomeTab.kt",
            "features/orders/OrdersTab.kt",
            "features/rewards/RewardsTab.kt",
            "features/profile/ProfileTab.kt",
        )
    }
}
