package cz.cleansia.customer.ui.components

import java.io.File
import org.junit.Assert.assertTrue
import org.junit.Test

/**
 * Home, Profile and Subscribe Plus scroll their content under the status bar, and each fades it out
 * there. The modifier draws over whatever it is chained onto, so it only fades the viewport when it
 * sits directly before `verticalScroll` and reads the same scroll state. There is no Compose test
 * harness in this module, so the screens are read as source.
 */
class StatusBarFadeBindingTest {

    @Test
    fun `each screen fades the viewport its own scroll state drives`() {
        val chained = Regex("""\.statusBarFade\((\w+)\b[^\n]*\)\s*\.verticalScroll\(\1\)""")
        for (screen in SCREENS) {
            assertTrue(
                "$screen must chain .statusBarFade(state) directly before .verticalScroll(state)",
                chained.containsMatchIn(source(screen)),
            )
        }
    }

    @Test
    fun `home pads the status bar inside the scroll, so its content can pass under it`() {
        val home = source("features/home/HomeTab.kt").replace(Regex("""//[^\n]*"""), "")
        assertTrue(
            "HomeTab must pad the status bar after verticalScroll, not before it",
            Regex("""\.verticalScroll\(scrollState\)\s*\.windowInsetsPadding\(WindowInsets\.statusBars\)""")
                .containsMatchIn(home),
        )
    }

    /** The refresh box runs under the status bar with the content, so its indicator pads the inset too. */
    @Test
    fun `home's refresh indicator rests below the status bar`() {
        val indicator = source("features/home/HomeTab.kt")
            .substringAfter("SudsRefreshIndicator(")
            .substringBefore("},")
        assertTrue(
            "HomeTab's SudsRefreshIndicator must pad WindowInsets.statusBars",
            indicator.contains(".windowInsetsPadding(WindowInsets.statusBars)"),
        )
    }

    /**
     * Profile and Plus start their scroll content with a full-bleed hero, so the fade wears the hero's top
     * colour while the hero is under the status bar; it can only follow the hero it is told the height of.
     */
    @Test
    fun `the hero screens hand the fade their hero's colour and measured height`() {
        val heroes = mapOf(
            "features/profile/ProfileTab.kt" to "heroColors.first",
            "features/membership/SubscribePlusScreen.kt" to "Sky950",
        )
        for ((screen, tint) in heroes) {
            val text = source(screen)
            assertTrue(
                "$screen must pass heroTint = $tint and heroHeight = { heroHeight }",
                text.contains(".statusBarFade(scrollState, heroTint = $tint, heroHeight = { heroHeight })"),
            )
            assertTrue(
                "$screen must measure its hero into heroHeight",
                text.contains("onSizeChanged { heroHeight = it.height }"),
            )
        }
        assertTrue(
            "Home has no hero: its fade is the page colour",
            source("features/home/HomeTab.kt").contains(".statusBarFade(scrollState)\n"),
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
        val SCREENS = listOf(
            "features/home/HomeTab.kt",
            "features/profile/ProfileTab.kt",
            "features/membership/SubscribePlusScreen.kt",
        )
    }
}
