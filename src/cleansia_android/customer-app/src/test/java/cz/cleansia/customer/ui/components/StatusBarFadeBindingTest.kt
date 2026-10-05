package cz.cleansia.customer.ui.components

import java.io.File
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test

/**
 * Home, Profile and Subscribe Plus scroll their content under the status bar, and each fades it out
 * there. The modifier draws over whatever it is chained onto, so it only fades the viewport when it
 * sits directly before `verticalScroll` and reads the same scroll state. There is no Compose test
 * harness in this module, so the screens, the fade and the theme are read as source.
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

    /**
     * The fade ends at the camera hole's bottom, the line the clock and icons are centred on, not at the
     * status bar's inset, which runs 30 px further down on a Pixel 8 and read as a band under the clock.
     */
    @Test
    fun `the fade ends at the cutout's line, not the status bar's inset`() {
        assertTrue(
            "statusBarFade must size the fade with statusBarFadeHeight(statusBar, cutoutExtent())",
            fadeBody().contains("statusBarFadeHeight(statusBar, cutoutExtent())"),
        )
    }

    /** W-F3: the hero's share is moved out of the shades no icon reads on before it colours the fade. */
    @Test
    fun `the fade's colour takes the legible share, judged once per hero and page`() {
        val body = fadeBody()
        assertTrue(
            "statusBarFade must remember statusBarFadeIllegibleShares(page, it) for its hero",
            body.contains("remember(heroTint, page) { heroTint?.let { statusBarFadeIllegibleShares(page, it) } }"),
        )
        assertTrue(
            "statusBarFade must colour the fade with statusBarFadeLegibleShare(statusBarFadeHeroShare(…), illegible)",
            Regex("""heroShare = statusBarFadeLegibleShare\(\s*statusBarFadeHeroShare\(currentHeroHeight\(\) - scrollState\.value, height\),\s*illegible,\s*\)""")
                .containsMatchIn(body),
        )
    }

    /** Dark icons on the navy Plus hero read 1.35:1, so a hero screen sets them for the colour behind them. */
    @Test
    fun `a hero screen sets the icons to read on the colour behind them`() {
        assertTrue(
            "statusBarFade must call StatusBarIcons(statusBarIconsLight(behind)) when heroTint is set",
            Regex("""if \(heroTint != null\) \{[\s\S]*?statusBarIconsLight\(behind\.value\)[\s\S]*?StatusBarIcons\(light\)""")
                .containsMatchIn(fadeBody()),
        )
    }

    /**
     * The icons are set on entering composition and on resume, and handed back to the theme's on pause and
     * on leaving, or a screen with no hero would inherit light icons on a light page.
     */
    @Test
    fun `the icons are held while the screen shows and handed back when it pauses or leaves`() {
        val icons = code(FADE).substringAfter("fun StatusBarIcons(", "")
        assertTrue(
            "StatusBarIcons' set() must write isAppearanceLightStatusBars = !light",
            Regex("""fun set\(\) \{[^}]*isAppearanceLightStatusBars = !light\b""").containsMatchIn(icons),
        )
        assertTrue(
            "StatusBarIcons' release() must hand back isAppearanceLightStatusBars = !themeDark",
            Regex("""fun release\(\) \{[^}]*isAppearanceLightStatusBars = !themeDark\b""").containsMatchIn(icons),
        )
        assertTrue(
            "StatusBarIcons must set() in DisposableEffect(light, themeDark) and release() on dispose",
            Regex("""DisposableEffect\(light, themeDark\) \{\s*set\(\)\s*onDispose \{ release\(\) \}""")
                .containsMatchIn(icons),
        )
        assertTrue(
            "StatusBarIcons must set() in LifecycleResumeEffect(light, themeDark) and release() on pause or dispose",
            Regex("""LifecycleResumeEffect\(light, themeDark\) \{\s*set\(\)\s*onPauseOrDispose \{ release\(\) \}""")
                .containsMatchIn(icons),
        )
    }

    /**
     * The theme sets the icons when the theme changes, not after every recomposition: a SideEffect runs
     * after every other effect in its frame, so it undid the icons a hero screen had just set.
     */
    @Test
    fun `the theme sets the icons only when the theme changes, so a hero screen's icons stand`() {
        val theme = code("ui/theme/Theme.kt")
        val effect = theme.substringAfter("DisposableEffect(darkTheme", "").substringBefore("onDispose", "")
        assertTrue(
            "CleansiaTheme must set isAppearanceLightStatusBars = !darkTheme inside DisposableEffect(darkTheme, …)",
            effect.contains("isAppearanceLightStatusBars = !darkTheme"),
        )
        assertFalse("CleansiaTheme must not set the bars in a SideEffect", theme.contains("SideEffect"))
    }

    /** statusBarFade's own body, up to where it draws: the effects it runs and the height it draws to. */
    private fun fadeBody(): String =
        code(FADE).substringAfter("fun Modifier.statusBarFade(", "").substringBefore("drawWithContent", "")

    /** [source] without its comments, so prose that names a call is never read as the call. */
    private fun code(path: String): String = source(path).replace(Regex("""/\*[\s\S]*?\*/|//[^\n]*"""), "")

    private fun source(path: String): String = sequenceOf(
        File("."),
        File("customer-app"),
        File("src/cleansia_android/customer-app"),
    ).map { File(it, "src/main/java/cz/cleansia/customer/$path") }
        .firstOrNull { it.isFile }
        ?.readText()
        ?: error("$path not found from working dir ${File(".").absolutePath}")

    private companion object {
        const val FADE = "ui/components/StatusBarFade.kt"

        val SCREENS = listOf(
            "features/home/HomeTab.kt",
            "features/profile/ProfileTab.kt",
            "features/membership/SubscribePlusScreen.kt",
        )
    }
}
