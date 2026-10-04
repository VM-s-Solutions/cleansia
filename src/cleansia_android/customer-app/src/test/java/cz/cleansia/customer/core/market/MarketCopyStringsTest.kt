package cz.cleansia.customer.core.market

import java.io.File
import kotlinx.serialization.json.Json
import kotlinx.serialization.json.jsonObject
import kotlinx.serialization.json.jsonPrimitive
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test

/**
 * ADR-0060 D0: a locale string never states a money figure or a currency name. The insurance ceiling
 * arrives on `MarketListItem` and is formatted on device in the market's currency; the no-cleaner push
 * announces the credit without a figure (D1); the seasonal card, which stated a promotion that does
 * not exist, is gone (D2). The catalogues have no Compose harness, so the copy is pinned by resource
 * and source assertions.
 */
class MarketCopyStringsTest {

    private val moduleDir: File = sequenceOf(
        File("."),
        File("customer-app"),
        File("src/cleansia_android/customer-app"),
    ).firstOrNull { File(it, "src/main/res").isDirectory }
        ?: error("customer-app not found from working dir ${File(".").absolutePath}")

    private val locales = listOf("values", "values-cs", "values-sk", "values-uk", "values-ru")

    private fun stringsXml(locale: String): String {
        val file = File(moduleDir, "src/main/res/$locale/strings.xml")
        assertTrue("missing $locale/strings.xml", file.isFile)
        return file.readText()
    }

    private fun valueOf(xml: String, key: String): String? =
        Regex("<string name=\"$key\">(.*?)</string>").find(xml)?.groupValues?.get(1)

    private fun withoutPlaceholders(value: String) = value.replace(PLACEHOLDER, "")

    private fun assertNoFigure(locale: String, key: String, value: String) {
        val bare = withoutPlaceholders(value)
        assertFalse("$locale/$key still states a number: \"$value\"", DIGIT.containsMatchIn(bare))
        assertFalse("$locale/$key still names a currency: \"$value\"", CURRENCY_WORD.containsMatchIn(bare))
    }

    @Test
    fun `the no-cleaner push names a credit but no figure in any locale`() {
        locales.forEach { locale ->
            val value = valueOf(stringsXml(locale), "notification_order_no_cleaner_refunded_body")
            assertTrue("$locale lost notification_order_no_cleaner_refunded_body", value != null)
            assertTrue("$locale dropped the order number", value!!.contains("%1\$s"))
            assertNoFigure(locale, "notification_order_no_cleaner_refunded_body", value)
            assertTrue("$locale no longer announces the credit: \"$value\"", CREDIT_WORD.getValue(locale).containsMatchIn(value))
        }
    }

    @Test
    fun `the trust badge and FAQ take the ceiling as a formatted amount and carry a no-figure twin`() {
        locales.forEach { locale ->
            val xml = stringsXml(locale)
            listOf("booking_trust_insured", "help_faq_a3").forEach { key ->
                val value = valueOf(xml, key)
                assertTrue("$locale lost $key", value != null)
                assertTrue("$locale/$key takes no formatted amount: \"$value\"", value!!.contains("%1\$s"))
                assertNoFigure(locale, key, value)

                val twin = valueOf(xml, "${key}_no_figure")
                assertTrue("$locale lost ${key}_no_figure", twin != null)
                assertFalse("$locale/${key}_no_figure carries a placeholder: \"$twin\"", PLACEHOLDER.containsMatchIn(twin!!))
                assertNoFigure(locale, "${key}_no_figure", twin)
            }
        }
    }

    @Test
    fun `the seasonal card and its strings are gone`() {
        locales.forEach { locale ->
            val xml = stringsXml(locale)
            assertFalse("$locale still carries home_seasonal_title", xml.contains("name=\"home_seasonal_title\""))
            assertFalse("$locale still carries home_seasonal_subtitle", xml.contains("name=\"home_seasonal_subtitle\""))
        }
        val homeTab = File(moduleDir, "src/main/java/cz/cleansia/customer/features/home/HomeTab.kt").readText()
        assertFalse("HomeTab still draws SeasonalCard", homeTab.contains("SeasonalCard"))
        assertFalse("HomeTab still reads home_seasonal_*", homeTab.contains("home_seasonal"))
    }

    /** The renderers format the market's figure with the shared formatter and fall to the no-figure key. */
    @Test
    fun `both renderers bind the figure to the market and the no-figure twin to its absence`() {
        val confirm = source("features/booking/ConfirmStep.kt")
        assertTrue(confirm.contains("viewModel.insuranceCoverage.collectAsStateWithLifecycle()"))
        assertTrue(confirm.contains("R.string.booking_trust_insured, formatOrderPrice(coverage.amount, coverage.currencyCode)"))
        assertTrue(confirm.contains("R.string.booking_trust_insured_no_figure"))

        val help = source("features/profile/HelpSupportScreen.kt")
        assertTrue(help.contains("viewModel.insuranceCoverage.collectAsStateWithLifecycle()"))
        assertTrue(help.contains("R.string.help_faq_a3, formatOrderPrice(coverage.amount, coverage.currencyCode)"))
        assertTrue(help.contains("R.string.help_faq_a3_no_figure"))
        assertEquals("the FAQ must not read the figured key without a figure", 1, Regex("R\\.string\\.help_faq_a3\\b[^_]").findAll(help).count())
    }

    /**
     * Owner ruling 2026-10-02: support@cleansia.cz is the one support contact a customer is shown. The
     * e-mail row prints the address it opens, as iOS's prints `CleansiaWeb.contactEmail`; no locale
     * carries a copy of it that could drift.
     */
    @Test
    fun `the help screen names the one support address in every locale`() {
        val help = source("features/profile/HelpSupportScreen.kt")
        assertTrue("the e-mail row no longer prints the address it opens", help.contains("subtitle = SUPPORT_EMAIL,"))
        locales.forEach { locale ->
            assertEquals("$locale/help_email_desc outlived the address constant", null, valueOf(stringsXml(locale), "help_email_desc"))
        }
    }

    /**
     * K2: the contact rows did nothing — the screen's handlers defaulted to `{}` and the nav host passed
     * only `onBack`. E-mail opens the mail app on the support address, Call opens the dialer on the line
     * the customer web footer prints, and Live chat, which has no chat behind it, is gone in every locale.
     */
    @Test
    fun `the help rows open the support address and line, and the dead chat row is gone`() {
        val help = source("features/profile/HelpSupportScreen.kt")
        assertTrue(help.contains("private const val SUPPORT_EMAIL = \"support@cleansia.cz\""))
        assertTrue(help.contains("private const val SUPPORT_PHONE = \"+420739788108\""))
        assertTrue(help.contains("Intent(Intent.ACTION_SENDTO, Uri.parse(\"mailto:${'$'}SUPPORT_EMAIL\"))"))
        assertTrue(help.contains("Intent(Intent.ACTION_DIAL, Uri.parse(\"tel:${'$'}SUPPORT_PHONE\"))"))
        assertTrue(help.contains("viewModel.onEmailUnavailable()"))
        assertTrue(help.contains("viewModel.onCallUnavailable()"))
        assertFalse("the help screen still has a chat row", help.contains("onChat") || help.contains("help_chat"))
        locales.forEach { locale ->
            val xml = stringsXml(locale)
            listOf("help_chat", "help_chat_desc").forEach { key ->
                assertEquals("$locale/$key outlived the chat row", null, valueOf(xml, key))
            }
            listOf("help_email_unavailable", "help_call_unavailable").forEach { key ->
                assertTrue("$locale/$key missing", !valueOf(xml, key).isNullOrBlank())
            }
        }
    }

    /**
     * M5, R1: iOS's Help is the reference for this screen (owner, 2026-10-03; the whole screen,
     * 2026-10-04). Every Help string reads as iOS's in every locale — read from iOS's catalog, so the two
     * cannot drift apart unnoticed — with only the format placeholder adapted (`%1$@` → `%1$s`). The
     * Profile row that opens the screen counts: it must not name the screen differently from its title.
     */
    @Test
    fun `every help string reads as iOS's in every locale`() {
        val catalog = Json.parseToJsonElement(File(solutionDir, IOS_CATALOG).readText()).jsonObject.getValue("strings").jsonObject
        val iosKeys = catalog.keys.filter { it.startsWith("help_") || it == "profile_row_help" }.toSet()
        iosLanguages.forEach { (locale, language) ->
            val android = Regex("<string name=\"(help_[^\"]+|profile_row_help)\">(.*?)</string>").findAll(stringsXml(locale))
                .associate { it.groupValues[1] to unescape(it.groupValues[2]) }
            assertEquals("$locale: the Help keys differ from iOS's", iosKeys, android.keys)
            iosKeys.forEach { key ->
                val ios = catalog.getValue(key).jsonObject["localizations"]?.jsonObject?.get(language)?.jsonObject
                    ?.get("stringUnit")?.jsonObject?.get("value")?.jsonPrimitive?.content
                    ?: error("iOS has no $language/$key")
                assertEquals("$locale/$key reads differently from iOS", ios.replace("\$@", "\$s"), android[key])
            }
        }
    }

    /**
     * R1: iOS's grouping. Two labelled sections, contact first: Email us then Call support in ONE card,
     * split by a divider; then the five questions in iOS's order, each in a card of its own. No round
     * icon badge behind a contact icon and no help icon on a question — iOS draws neither.
     */
    @Test
    fun `the help screen keeps iOS's sections, rows and order`() {
        val help = source("features/profile/HelpSupportScreen.kt")
        val order = listOf(
            "HelpSection(stringResource(R.string.help_contact_title))",
            "title = stringResource(R.string.help_email),",
            "HorizontalDivider(",
            "title = stringResource(R.string.help_call),",
            "HelpSection(stringResource(R.string.help_faq_title))",
        ).map { it to help.indexOf(it) }
        order.forEach { (marker, at) -> assertTrue("the Help screen lost $marker", at >= 0) }
        assertEquals("the Help screen's sections and rows are out of iOS's order", order.sortedBy { it.second }, order)
        assertEquals("the contact rows are not one card split by one divider", 1, Regex("HorizontalDivider\\(").findAll(help).count())

        val questions = Regex("FaqItem\\(R\\.string\\.(help_faq_q\\d)\\)").findAll(help).map { it.groupValues[1] }.toList()
        assertEquals((1..5).map { "help_faq_q$it" }, questions)
        assertTrue("the questions are no longer one card each", help.contains("faqs.forEach { faq -> FaqRow(faq) }"))
        assertTrue("the section labels are not upper-case as iOS's", help.contains("title.uppercase()"))
        listOf("CircleShape", "HelpOutline").forEach { badge ->
            assertFalse("the Help screen still draws $badge, which iOS has not", help.contains(badge))
        }
    }

    private fun source(path: String): String =
        File(moduleDir, "src/main/java/cz/cleansia/customer/$path").readText().replace(Regex("\\s+"), " ")

    private fun unescape(value: String) =
        value.replace("\\'", "'").replace("\\\"", "\"").replace("&lt;", "<").replace("&gt;", ">").replace("&amp;", "&")

    private val solutionDir: File = generateSequence(moduleDir.absoluteFile) { it.parentFile }
        .firstOrNull { File(it, "Cleansia.Api.sln").isFile }
        ?: error("Cleansia.Api.sln not found above ${moduleDir.absolutePath}")

    private val iosLanguages = mapOf(
        "values" to "en",
        "values-cs" to "cs",
        "values-sk" to "sk",
        "values-uk" to "uk",
        "values-ru" to "ru",
    )

    private companion object {
        const val IOS_CATALOG = "cleansia_ios/CleansiaCustomer/Resources/Localizable.xcstrings"
        val PLACEHOLDER = Regex("%\\d+\\\$[sd]")
        val DIGIT = Regex("\\d")
        val CURRENCY_WORD = Regex("CZK|Kč|EUR|€|koru|euro|крон|євро|евро", RegexOption.IGNORE_CASE)
        val CREDIT_WORD = mapOf(
            "values" to Regex("credit", RegexOption.IGNORE_CASE),
            "values-cs" to Regex("kredit", RegexOption.IGNORE_CASE),
            "values-sk" to Regex("kredit", RegexOption.IGNORE_CASE),
            "values-uk" to Regex("кредит", RegexOption.IGNORE_CASE),
            "values-ru" to Regex("кредит", RegexOption.IGNORE_CASE),
        )
    }
}
