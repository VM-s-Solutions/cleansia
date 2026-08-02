package cz.cleansia.customer.features.membership

import java.io.File
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Test

/**
 * The management card dropped its express pill one release before the subscribe and success screens
 * dropped theirs, and the claim went on shipping on both of them plus every locale bundle. So the
 * absence is pinned across all three surfaces and the resource set at once, not card by card.
 */
class MembershipExpressClaimTest {

    private val locales = listOf("values", "values-cs", "values-sk", "values-uk", "values-ru")

    /** Every stem the perk was worded with across the five locales. */
    private val expressStems = listOf("express", "expres", "експрес", "экспресс")

    private val screens = listOf(
        "MembershipManagementCard.kt",
        "SubscribePlusScreen.kt",
        "MembershipSuccessScreen.kt",
    )

    private val moduleDir: File = sequenceOf(
        File("."),
        File("customer-app"),
        File("src/cleansia_android/customer-app"),
    ).firstOrNull { File(it, "src/main/res").isDirectory }
        ?: error("customer-app not found from working dir ${File(".").absolutePath}")

    @Test
    fun `no membership screen renders an express perk`() {
        screens.forEach { screen ->
            val source = File(
                moduleDir,
                "src/main/java/cz/cleansia/customer/features/membership/$screen",
            ).also { assertTrue("$screen not found at ${it.absolutePath}", it.isFile) }.readText()

            assertEquals(
                "$screen still renders the express perk — no pricing code waives the surcharge",
                emptyList<String>(),
                source.lines().filter { it.contains("membership_perk_express") },
            )
        }
    }

    @Test
    fun `no membership screen branches on the unenforced express flag`() {
        screens.forEach { screen ->
            val source = File(
                moduleDir,
                "src/main/java/cz/cleansia/customer/features/membership/$screen",
            ).readText()
            assertTrue(
                "$screen reads allowsExpressUpgrade, which no pricing code honours",
                !source.contains("allowsExpressUpgrade"),
            )
        }
    }

    @Test
    fun `no membership string in any locale names the express perk`() {
        locales.forEach { locale ->
            val file = File(moduleDir, "src/main/res/$locale/strings.xml")
            assertTrue("missing $locale/strings.xml", file.isFile)
            val offending = Regex("<string name=\"(membership_[^\"]+)\"[^>]*>(.*?)</string>", RegexOption.DOT_MATCHES_ALL)
                .findAll(file.readText())
                .filter { match -> expressStems.any { match.groupValues[2].contains(it, ignoreCase = true) } }
                .map { "${it.groupValues[1]} = ${it.groupValues[2]}" }
                .toList()
            assertEquals("$locale advertises the express perk", emptyList<String>(), offending)
        }
    }
}
