package cz.cleansia.customer.features.membership

import java.io.File
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Test

/**
 * [MembershipViewModelTest] proves which code the Plus surfaces resolve; nothing there can see a
 * composable that still prints "Kč" on its own. The membership screens have no Compose harness, so
 * the call sites are pinned by source assertions, like the booking wizard's.
 */
class MembershipCurrencyBindingTest {

    private val moduleDir: File = sequenceOf(
        File("."),
        File("customer-app"),
        File("src/cleansia_android/customer-app"),
    ).firstOrNull { File(it, "src/main/res").isDirectory }
        ?: error("customer-app not found from working dir ${File(".").absolutePath}")

    private val membershipDir = File(moduleDir, "src/main/java/cz/cleansia/customer/features/membership")

    private fun source(file: File): String =
        file.readText()
            .lines()
            .filterNot { it.trimStart().startsWith("//") || it.trimStart().startsWith("*") }
            .joinToString("\n")

    @Test
    fun `no membership screen prints the koruna or keeps its own formatter`() {
        val offenders = membershipDir.listFiles().orEmpty()
            .filter { it.extension == "kt" }
            .flatMap { file ->
                source(file).lines().withIndex()
                    .filter { (_, line) -> KORUNA.containsMatchIn(line) }
                    .map { (index, _) -> "${file.name}:${index + 1}" }
            }
        assertEquals("these lines label a plan price by hand", emptyList<String>(), offenders)
    }

    /**
     * ADR-0059 D3: every plan figure is labelled with the plan's own `currencyCode` (the market's), the
     * Google Pay sheet is gated on the same code, and a swap price is quoted only in the membership's
     * own currency — never the catalogue default, never a literal.
     */
    @Test
    fun `every plan price goes through the shared formatter with the payload's currency`() {
        val subscribe = source(File(membershipDir, "SubscribePlusScreen.kt")).replace(Regex("\\s+"), " ")
        assertTrue(
            "the hero no longer labels with the selected plan's currency",
            subscribe.contains("val currencyCode = selectedPlan?.currencyCode"),
        )
        assertTrue(
            "the plan price no longer goes through formatOrderPrice",
            subscribe.contains("formatOrderPrice(amount, currencyCode)"),
        )
        assertTrue(
            "the CTA disclosure no longer formats with the plan's currency",
            subscribe.contains("formatPlanPrice(plan.price, plan.currencyCode)"),
        )
        assertTrue(
            "the Google Pay sheet is no longer gated on the plan's currency",
            subscribe.contains("currencyCode = selectedPlan?.currencyCode"),
        )
        assertTrue(
            "the Google Pay country is the merchant's, not the shopper's market",
            subscribe.contains("countryCode = \"CZ\"") && !subscribe.contains("countryCode = market"),
        )
        assertTrue(
            "the subscribe screen reads the catalogue default again",
            !subscribe.contains("catalogRepository") && !subscribe.contains("viewModel.currencyCode"),
        )
        val card = source(File(membershipDir, "MembershipManagementCard.kt")).replace(Regex("\\s+"), " ")
        assertTrue(
            "the switch-to-annual dialog no longer formats with the yearly plan's own currency",
            card.contains("formatOrderPrice(yearlyPlan.price, yearlyPlan.currencyCode)"),
        )
        assertTrue(
            "the switch-to-annual CTA no longer requires the plan to be priced in the membership's currency",
            card.contains("it.currencyCode == membership?.currencyCode"),
        )
    }

    /** The not-on-sale state names no price and no button (ADR-0059 D3). */
    @Test
    fun `the not-on-sale state renders the empty pattern with no price and no button`() {
        val subscribe = source(File(membershipDir, "SubscribePlusScreen.kt"))
        val start = subscribe.indexOf("private fun NotAvailableInMarket(")
        assertTrue("the subscribe screen lost its not-on-sale state", start >= 0)
        val body = subscribe.substring(start, subscribe.indexOf("\n}\n", start))
        assertTrue("the empty state must show the not-available copy", body.contains("R.string.plus_not_available_in_market"))
        assertTrue("the empty state must draw the leaning mascot", body.contains("R.drawable.mascot_leaning"))
        assertTrue("the empty state must not offer a CTA", !body.contains("StickyCtaBar") && !body.contains("CleansiaPrimaryButton"))
        assertTrue("the empty state must not print a price", !body.contains("formatPlanPrice") && !body.contains("formatOrderPrice"))
        assertTrue(
            "the screen must only render the empty state once the server has answered with no plans",
            subscribe.contains("val notOnSaleInMarket = plansLoaded && plans.isEmpty()"),
        )
    }

    /**
     * As on iOS, a build without a Stripe key offers no purchase it cannot complete: the bar is the only
     * caller of `startSubscribe`, so hiding it also keeps the server from creating a SetupIntent for a
     * sheet that cannot open. Without the bar, the scroll content pads the navigation bar itself.
     */
    @Test
    fun `the purchase bar mounts only when card payment is configured`() {
        val subscribe = source(File(membershipDir, "SubscribePlusScreen.kt")).replace(Regex("\\s+"), " ")
        assertTrue(
            "the screen must read card availability from the key that initialises Stripe",
            subscribe.contains("val canSubscribe = BuildConfig.STRIPE_PUBLISHABLE_KEY.isNotBlank()"),
        )
        assertTrue(
            "the purchase bar must mount only inside if (canSubscribe)",
            subscribe.contains("if (canSubscribe) { StickyCtaBar("),
        )
        val callers = customerMainSources().flatMap { file ->
            START_SUBSCRIBE_CALL.findAll(code(file)).map { file.name to it.range.first }.toList()
        }
        assertEquals(
            "startSubscribe must have exactly one caller in the app, the gated bar",
            listOf("SubscribePlusScreen.kt"),
            callers.map { it.first },
        )
        assertTrue(
            "startSubscribe must be reachable only inside if (canSubscribe)",
            callers.single().second in blockRange(code(File(membershipDir, "SubscribePlusScreen.kt")), "if (canSubscribe) {"),
        )
        assertTrue(
            "without the bar the last perk must still clear the navigation bar",
            subscribe.contains("if (!canSubscribe) Spacer(Modifier.navigationBarsPadding())"),
        )
    }

    private fun customerMainSources(): List<File> {
        val files = File(moduleDir, "src/main/java").walkTopDown().filter { it.isFile && it.extension == "kt" }.toList()
        assertTrue("the walk must reach SubscribePlusScreen.kt", files.any { it.name == "SubscribePlusScreen.kt" })
        assertTrue("the walk must reach MembershipViewModel.kt", files.any { it.name == "MembershipViewModel.kt" })
        return files
    }

    /** The source without comments, so prose naming a call is never read as the call. */
    private fun code(file: File): String =
        file.readText().replace("\r\n", "\n").replace(COMMENTS, "")

    /** From the unique [marker], which ends with the block's opening brace, to its matching closing brace. */
    private fun blockRange(text: String, marker: String): IntRange {
        val start = text.indexOf(marker)
        assertTrue("no `$marker` in the source", start >= 0)
        assertTrue("`$marker` must be unique for the gate to mean anything", start == text.lastIndexOf(marker))
        var depth = 0
        for (i in start + marker.length - 1 until text.length) {
            when (text[i]) {
                '{' -> depth++
                '}' -> if (--depth == 0) return start..i
            }
        }
        error("unbalanced braces after `$marker`")
    }

    /** TalkBack names the hero's back arrow, in the offer and in the not-on-sale state alike. */
    @Test
    fun `the hero's back arrow is announced as back`() {
        val subscribe = source(File(membershipDir, "SubscribePlusScreen.kt")).replace(Regex("\\s+"), " ")
        val descriptions = Regex("""Icons\.AutoMirrored\.Outlined\.ArrowBack, contentDescription = (.+?), tint""")
            .findAll(subscribe)
            .map { it.groupValues[1] }
            .toList()
        assertEquals("expected the offer's and the not-on-sale state's back arrows", 2, descriptions.size)
        descriptions.forEach { description ->
            assertEquals("stringResource(R.string.common_back)", description)
        }
    }

    /** The trial line takes a formatted amount; it used to hard-code "0 Kč" in five locales. */
    @Test
    fun `the trial price string takes a formatted amount in every locale`() {
        val resDir = File(moduleDir, "src/main/res")
        val locales = resDir.listFiles().orEmpty()
            .filter { it.name == "values" || it.name.startsWith("values-") }
            .map { File(it, "strings.xml") }
            .filter { it.isFile }
        assertTrue("no locale files found under ${resDir.absolutePath}", locales.size >= 5)

        locales.forEach { file ->
            val text = file.readText()
            val line = Regex("<string name=\"membership_hero_trial_price\">(.*?)</string>").find(text)?.groupValues?.get(1)
            assertTrue("${file.parentFile.name} lost membership_hero_trial_price", line != null)
            assertTrue("${file.parentFile.name} takes a formatted amount", line!!.contains("%1\$s"))
            assertTrue("${file.parentFile.name} takes the day count second", line.contains("%2\$d"))
            assertTrue("${file.parentFile.name} still names a currency: $line", !CURRENCY_LITERAL.containsMatchIn(line))
            assertTrue("${file.parentFile.name} still carries the dead per-month price", !text.contains("membership_plus_price_per_month"))
        }
    }

    private companion object {
        val KORUNA = Regex("""Kč|formatPriceCzk""")
        val CURRENCY_LITERAL = Regex("""CZK|Kč|EUR|€""")
        val COMMENTS = Regex("""/\*[\s\S]*?\*/|(?<![:"])//[^\n]*""")
        val START_SUBSCRIBE_CALL = Regex("""(?<!fun )\bstartSubscribe\(|::startSubscribe\b""")
    }
}
