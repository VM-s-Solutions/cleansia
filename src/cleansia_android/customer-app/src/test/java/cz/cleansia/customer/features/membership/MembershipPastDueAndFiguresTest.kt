package cz.cleansia.customer.features.membership

import java.io.File
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Test

/**
 * The Plus discount, free window and express quota and the tier discount are set in admin, so the copy
 * takes them from the API instead of naming today's seed; and a past-due membership is shown as one,
 * never as a lapsed one offered a second subscription. There is no Compose harness here, so the screen
 * bindings are pinned by source.
 */
class MembershipPastDueAndFiguresTest {

    private val locales = listOf("values", "values-cs", "values-sk", "values-uk", "values-ru")

    private val figureKeys = listOf(
        "membership_perk_discount_title",
        "membership_perk_cancellation_desc",
        "membership_perk_express_desc",
    )

    private val pastDueKeys = listOf(
        "membership_status_past_due_badge",
        "membership_past_due_title",
        "membership_past_due_body",
        "membership_cancel_dialog_message_now",
        "membership_cancel_success_now",
    )

    /** The code constants the copy may still name: the 20 % express surcharge and its 2-4 h lead time. */
    private val codeConstants = listOf(Regex("\\b20\\s?%"), Regex("\\b2\\D{1,6}4\\b"))

    private val moduleDir: File = sequenceOf(
        File("."),
        File("customer-app"),
        File("src/cleansia_android/customer-app"),
    ).firstOrNull { File(it, "src/main/res").isDirectory }
        ?: error("customer-app not found from working dir ${File(".").absolutePath}")

    @Test
    fun `every admin-set figure is a placeholder in every locale, never a baked number`() {
        locales.forEach { locale ->
            val xml = stringsXml(locale)
            figureKeys.forEach { key ->
                val value = valueOf(xml, key) ?: error("$locale/$key is missing")
                assertTrue("$locale/$key lost its %1\$d placeholder — $value", value.contains("%1\$d"))
                val rest = codeConstants.fold(value.replace("%1\$d", "").replace("%%", "%")) { text, constant ->
                    constant.replace(text, "")
                }
                assertEquals("$locale/$key bakes a figure in — $value", "", rest.filter { it.isDigit() })
                assertEquals(
                    "$locale/$key has an unescaped % — String.format would throw",
                    0,
                    value.replace("%1\$d", "").replace("%%", "").count { it == '%' },
                )
            }
        }
    }

    @Test
    fun `the past-due card and its cancel are worded in every locale`() {
        locales.forEach { locale ->
            val xml = stringsXml(locale)
            pastDueKeys.forEach { key ->
                val value = valueOf(xml, key)
                assertTrue("$locale/$key is missing", value != null)
                assertTrue("$locale/$key is blank", value!!.isNotBlank())
            }
        }
    }

    @Test
    fun `the subscribe and success screens state the plan's own figures`() {
        val subscribe = source("membership/SubscribePlusScreen.kt")
        listOf(
            "selectedPlan?.discountPercentage",
            "selectedPlan?.freeCancellationWindowHours",
            "selectedPlan?.expressUpgradesPerMonth",
            "stringResource(R.string.membership_perk_discount_title, discountPercent)",
            "stringResource(R.string.membership_perk_cancellation_desc, freeCancellationHours)",
            "stringResource(R.string.membership_perk_express_desc, expressPerMonth)",
        ).forEach { binding -> assertTrue("SubscribePlusScreen lost `$binding`", subscribe.contains(binding)) }

        val success = source("membership/MembershipSuccessScreen.kt")
        listOf(
            "membership?.discountPercentage",
            "stringResource(R.string.membership_perk_discount_title, discountPercent)",
        ).forEach { binding -> assertTrue("MembershipSuccessScreen lost `$binding`", success.contains(binding)) }
    }

    @Test
    fun `a past-due membership gets its own card and is never offered a second subscription`() {
        val card = source("membership/MembershipManagementCard.kt")
        val inactive = card.indexOf("!membership.hasMembership -> InactiveCard(")
        val pastDue = card.indexOf("membership.benefitsPaused -> PastDueCard(")
        val active = card.indexOf("else -> ActiveCard(")
        assertTrue(
            "the card no longer branches inactive, then past-due, then active",
            inactive in 0 until pastDue && pastDue < active,
        )
        assertTrue(
            "the subscribe screen no longer bounces a live enrolment",
            source("membership/SubscribePlusScreen.kt").contains("if (current?.hasMembership == true && !navigatedAway)"),
        )
    }

    @Test
    fun `the current tier's discount perk states the account's figure`() {
        val rewards = source("rewards/RewardsTab.kt")
        assertTrue(
            "the current perks no longer read the account's discount",
            rewards.contains(
                "tierDiscount = composeDiscountSummary( account.currentDiscountPercent, " +
                    "account.currentDiscountMinOrderAmount,",
            ),
        )
        assertTrue(
            "the discount perk renders its seeded label key again",
            rewards.contains("perk.labelKey?.startsWith(TIER_DISCOUNT_PERK_KEY) == true) tierDiscount"),
        )
    }

    private fun source(path: String): String =
        File(moduleDir, "src/main/java/cz/cleansia/customer/features/$path")
            .also { assertTrue("$path not found at ${it.absolutePath}", it.isFile) }
            .readText()
            .replace(Regex("\\s+"), " ")

    private fun stringsXml(locale: String): String {
        val file = File(moduleDir, "src/main/res/$locale/strings.xml")
        assertTrue("missing $locale/strings.xml", file.isFile)
        return file.readText()
    }

    private fun valueOf(xml: String, key: String): String? =
        Regex("<string name=\"$key\"[^>]*>(.*?)</string>", RegexOption.DOT_MATCHES_ALL)
            .find(xml)
            ?.groupValues
            ?.get(1)
}
