package cz.cleansia.customer.features.home

import java.io.File
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Test

/**
 * What the home carousel, the profile's Plus card, the recurring paywall, the referral surfaces, the
 * trust badges and the help FAQ promise, held to what the platform delivers. The trial claims are the
 * ones the web's `plus-trial-claim.spec.ts` guards on its own surfaces.
 */
class UpsellClaimTest {

    private val locales = listOf("values", "values-cs", "values-sk", "values-uk", "values-ru")

    private val moduleDir: File = sequenceOf(
        File("."),
        File("customer-app"),
        File("src/cleansia_android/customer-app"),
    ).firstOrNull { File(it, "src/main/res").isDirectory }
        ?: error("customer-app not found from working dir ${File(".").absolutePath}")

    private val solutionDir: File = generateSequence(moduleDir.absoluteFile) { it.parentFile }
        .firstOrNull { File(it, "Cleansia.Api.sln").isFile }
        ?: error("Cleansia.Api.sln not found above ${moduleDir.absolutePath}")

    /**
     * These rows render to a non-member whether or not a trial is on offer, so none may promise one. The
     * trial rows are separate keys, shown only while the customer can still get a trial.
     */
    private val ungatedPlusKeys = listOf(
        "home_upsell_plus_top",
        "home_upsell_plus_title",
        "home_upsell_plus_cta",
        "membership_inactive_badge",
        "membership_inactive_title",
        "membership_inactive_perks_summary",
        "membership_inactive_cta",
        "recurring_plus_gate_title",
        "recurring_plus_gate_subtitle",
        "recurring_plus_gate_cta",
    )

    /** The web spec's stems. A bare "free" is absent: free cancellation is a real, ungated perk. */
    private val trialClaim = Regex(
        "trial|zkušeb|skúšob|пробн|\\btry\\b|vyzkouš|zkuste|vyskúš|skúste|спробу|попроб|" +
            "\\d+\\s*(?:days?|dn|дн)",
        RegexOption.IGNORE_CASE,
    )

    /** Bookings carry cancellation fees, so "cancel anytime" in a perks line reads as a false perk. */
    private val cancelAnytimeClaim = Regex(
        "cancel any\\s?time|kdykoli|kedykoľvek|будь-коли|в любое время|в любой момент",
        RegexOption.IGNORE_CASE,
    )

    /** `ReferralPolicy`: both sides are paid once the friend's first order is completed. */
    private val completedStem = mapOf(
        "values" to "completed",
        "values-cs" to "dokončen",
        "values-sk" to "dokončen",
        "values-uk" to "завершен",
        "values-ru" to "завершённ",
    )

    /** Unused rows that described perks, guarantees or a trial the platform does not offer. */
    private val retiredPromises = listOf(
        "rewards_tier_unlocked",
        "rewards_tier_regular",
        "rewards_tier_regular_perk",
        "rewards_tier_vip",
        "rewards_tier_vip_perk",
        "rewards_tier_hero",
        "rewards_tier_hero_perk",
        "rewards_referral_subtitle",
        "home_hero3_top",
        "home_hero3_title",
        "home_hero3_cta",
        "home_hero_slide3_title",
        "home_hero_slide3_subtitle",
        "home_hero_slide3_cta",
        "membership_inactive_subtitle",
        "membership_trial_badge",
        "membership_trial_disclosure",
        "membership_subscribe_cta",
        "referral_code_valid",
        "home_upsell_welcome_top",
        "home_upsell_welcome_title",
        "home_upsell_welcome_cta",
        "home_trust_vetted",
        "booking_trust_vetted",
        "home_hero_slide1_subtitle",
    )

    /** No cleaner is background-checked: approval asks for an identity card and an insurance certificate. */
    private val vettingClaim = Regex(
        "vetted|background|prověř|prever|перевірен|перевірк|проверен",
        RegexOption.IGNORE_CASE,
    )

    private val trustKeys = listOf(
        "home_trust_insured",
        "booking_trust_insured",
        "booking_trust_insured_no_figure",
        "help_faq_q3",
        "help_faq_a3",
        "help_faq_a3_no_figure",
    )

    private val cancelStem = mapOf(
        "values" to "cancel",
        "values-cs" to "zruš",
        "values-sk" to "zruš",
        "values-uk" to "скасу",
        "values-ru" to "отмен",
    )

    @Test
    fun `no ungated Plus row promises a free trial`() {
        val claims = locales.flatMap { locale ->
            val declared = strings(locale)
            ungatedPlusKeys.mapNotNull { key ->
                val value = declared[key] ?: return@mapNotNull "$locale/$key is missing"
                if (trialClaim.containsMatchIn(value)) "$locale/$key: $value" else null
            }
        }
        assertEquals(emptyList<String>(), claims)
    }

    /** The trial length is set per plan in the admin console, so a trial row states the plan's days and no number of its own. */
    private val trialRows = listOf(
        "home_upsell_plus_title_trial",
        "membership_inactive_cta_trial",
        "membership_hero_trial_price",
    )

    @Test
    fun `every trial row states the plan's days rather than a number of its own`() {
        val placeholder = Regex("%\\d+\\$[sd]")
        locales.forEach { locale ->
            val declared = strings(locale)
            trialRows.forEach { key ->
                val value = declared[key] ?: error("$locale/$key is missing")
                assertTrue("$locale/$key does not carry the plan's days — $value", Regex("%\\d+\\\$d").containsMatchIn(value))
                assertTrue("$locale/$key names a number of its own — $value", placeholder.replace(value, "").none { it.isDigit() })
            }
        }
    }

    /** A trialing member has every Plus benefit from day one, so no row may say one waits for a payment. */
    private val waitsForPaymentClaim = mapOf(
        "values" to Regex("first paid|paid membership", RegexOption.IGNORE_CASE),
        "values-cs" to Regex("prvním placen|prvního placen|placené členství|placeného členství", RegexOption.IGNORE_CASE),
        "values-sk" to Regex("prvým platen|prvého platen|platené členstvo|plateného členstva", RegexOption.IGNORE_CASE),
        "values-uk" to Regex("першого оплачен|оплачене членство|платну підписку", RegexOption.IGNORE_CASE),
        "values-ru" to Regex("первого оплаченн|оплаченное членство|платную подписку", RegexOption.IGNORE_CASE),
    )

    @Test
    fun `no row tells a trialing member a benefit waits for the first payment`() {
        val claims = locales.flatMap { locale ->
            val claim = waitsForPaymentClaim.getValue(locale)
            strings(locale).filterValues { claim.containsMatchIn(it) }.map { (key, value) -> "$locale/$key: $value" }
        }
        assertEquals(emptyList<String>(), claims)
    }

    @Test
    fun `the non-member perks line does not promise cancelling anytime`() {
        val claims = locales.mapNotNull { locale ->
            val value = strings(locale)["membership_inactive_perks_summary"]
                ?: return@mapNotNull "$locale/membership_inactive_perks_summary is missing"
            if (cancelAnytimeClaim.containsMatchIn(value)) "$locale: $value" else null
        }
        assertEquals(emptyList<String>(), claims)
    }

    /** A paid-up member's cancel runs to the period end and a past-due one's ends it at once; "anytime" states neither. */
    @Test
    fun `the Plus disclosure and auto-renew hint do not promise cancelling anytime`() {
        val claims = locales.flatMap { locale ->
            val declared = strings(locale)
            listOf("membership_disclosure", "membership_auto_renew_hint").mapNotNull { key ->
                val value = declared[key] ?: return@mapNotNull "$locale/$key is missing"
                if (cancelAnytimeClaim.containsMatchIn(value)) "$locale/$key: $value" else null
            }
        }
        assertEquals(emptyList<String>(), claims)
    }

    /** The rows known to state the referral reward, so the sweep below cannot pass by finding none. */
    private val referralRewardKeys = listOf(
        "home_upsell_referral_title",
        "booking_referral_code_dialog_helper",
        "booking_referral_code_dialog_success_named",
        "booking_referral_code_dialog_success",
        "loyalty_referral_subtitle",
        "loyalty_referral_share_text",
    )

    @Test
    fun `every referral reward waits for the friend's first completed cleaning`() {
        val points = Regex("public\\s+const\\s+int\\s+PointsPerSide\\s*=\\s*(\\d+)\\s*;")
            .find(File(solutionDir, "Cleansia.Core.AppServices/Features/Orders/ReferralPolicy.cs").readText())
            ?.groupValues?.get(1)
            ?: error("ReferralPolicy.PointsPerSide not found — the parser needs updating")
        locales.forEach { locale ->
            val completed = completedStem.getValue(locale)
            val rewards = strings(locale).filterValues { Regex("\\b$points\\b").containsMatchIn(it) }
            assertTrue(
                "$locale: the sweep missed a known referral reward, found only ${rewards.keys}",
                rewards.keys.containsAll(referralRewardKeys),
            )
            rewards.forEach { (key, value) ->
                assertTrue("$locale/$key does not wait for a completed cleaning — $value", value.contains(completed, ignoreCase = true))
            }
            val waiting = plurals(locale)["loyalty_referral_stats_waiting"] ?: error("$locale/loyalty_referral_stats_waiting is missing")
            waiting.forEach { item ->
                assertTrue("$locale/loyalty_referral_stats_waiting does not wait for a completed cleaning — $item", item.contains(completed, ignoreCase = true))
            }
        }
    }

    @Test
    fun `no trust badge or FAQ answer claims the cleaners are vetted`() {
        val claims = locales.flatMap { locale ->
            val declared = strings(locale)
            trustKeys.mapNotNull { key ->
                val value = declared[key] ?: return@mapNotNull "$locale/$key is missing"
                if (vettingClaim.containsMatchIn(value)) "$locale/$key: $value" else null
            }
        }
        assertEquals(emptyList<String>(), claims)
    }

    /** No booking can be moved: the customer cancels, free while no cleaner has taken it, and books again. */
    @Test
    fun `the FAQ promises no rescheduling and points to cancelling`() {
        locales.forEach { locale ->
            val answer = strings(locale)["help_faq_a4"] ?: error("$locale/help_faq_a4 is missing")
            assertTrue("$locale/help_faq_a4 states a figure — $answer", answer.none { it.isDigit() })
            assertTrue("$locale/help_faq_a4 names a button to tap — $answer", !answer.contains("\\\""))
            assertTrue("$locale/help_faq_a4 does not point to cancelling — $answer", answer.contains(cancelStem.getValue(locale), ignoreCase = true))
        }
    }

    @Test
    fun `the retired promises stay deleted in every locale`() {
        val back = locales.flatMap { locale ->
            val declared = strings(locale)
            retiredPromises.filter { it in declared }.map { "$locale/$it" }
        }
        assertEquals(emptyList<String>(), back)
    }

    /** The newer carousel slides; every one renders, so every one is in every locale. */
    private val carouselKeys = listOf(
        "home_upsell_notifications_top",
        "home_upsell_notifications_title",
        "home_upsell_notifications_cta",
        "home_upsell_credit_title",
        "home_upsell_express_top",
        "home_upsell_book_cta",
        "home_quick_size_title",
        "home_quick_size_cta",
        "home_quick_size_rooms_less",
        "home_quick_size_rooms_more",
        "home_quick_size_baths_less",
        "home_quick_size_baths_more",
    )

    /** Key → the placeholders it must carry; it may state no number of its own. */
    private val carouselFigures = mapOf(
        "home_upsell_credit_title" to listOf("%1\$s", "%2\$d"),
        "home_upsell_express_top" to listOf("%1\$d", "%2\$d"),
    )

    @Test
    fun `every new carousel slide is written in all five locales and promises no trial`() {
        val english = strings("values")
        locales.forEach { locale ->
            val declared = strings(locale)
            carouselKeys.forEach { key ->
                val value = declared[key]
                assertTrue("$locale/$key is missing or blank", value?.isNotBlank() == true)
                assertTrue("$locale/$key promises a trial — $value", !trialClaim.containsMatchIn(value!!))
                if (locale == "values-uk" || locale == "values-ru") {
                    assertTrue("$locale/$key is still English", value != english[key])
                }
            }
            val express = plurals(locale)["home_upsell_express_title"]
            assertTrue("$locale/home_upsell_express_title is missing", !express.isNullOrEmpty())
        }
    }

    /**
     * The credit balance and its share are the server's (`GetMyCredit`), the waivers left are the
     * membership's, and the express window is the booking policy's — so no locale states a number.
     */
    @Test
    fun `the credit and express slides state the server's figures, never their own`() {
        val placeholder = Regex("%\\d+\\$[sd]")
        locales.forEach { locale ->
            val declared = strings(locale)
            carouselFigures.forEach { (key, required) ->
                val value = declared[key] ?: error("$locale/$key is missing")
                required.forEach { assertTrue("$locale/$key lost its $it placeholder — $value", value.contains(it)) }
                assertTrue("$locale/$key names a number of its own — $value", placeholder.replace(value, "").none { it.isDigit() })
            }
            plurals(locale).getValue("home_upsell_express_title").forEach { item ->
                assertTrue("$locale/home_upsell_express_title lost its count — $item", item.contains("%1\$d"))
                assertTrue("$locale/home_upsell_express_title names a number of its own — $item", placeholder.replace(item, "").none { it.isDigit() })
            }
        }
    }

    /** The quick-size slide showed "2 bath": English `other` had been left on the singular. */
    @Test
    fun `the size steppers count rooms and baths in the English plural`() {
        val english = plurals("values")
        assertEquals(listOf("%1\$d room", "%1\$d rooms"), english["booking_rooms_short"])
        assertEquals(listOf("%1\$d bath", "%1\$d baths"), english["booking_bath_short"])
    }

    /** The express slide states the 2–4 h window from the client's bands, so those must be the server's. */
    @Test
    fun `the express window the slide states is the booking policy's`() {
        val policy = File(solutionDir, "Cleansia.Core.AppServices/Features/Orders/BookingPolicy.cs").readText()
        fun hours(name: String): Int = Regex("public\\s+const\\s+int\\s+$name\\s*=\\s*(\\d+)\\s*;")
            .find(policy)?.groupValues?.get(1)?.toInt()
            ?: error("BookingPolicy.$name not found — the parser needs updating")
        assertEquals(hours("ExpressLeadTimeHours"), cz.cleansia.customer.features.booking.BookingPricing.EXPRESS_LEAD_HOURS.toInt())
        assertEquals(hours("StandardLeadTimeHours"), cz.cleansia.customer.features.booking.BookingPricing.STANDARD_LEAD_HOURS.toInt())
    }

    private fun strings(locale: String): Map<String, String> {
        val file = File(moduleDir, "src/main/res/$locale/strings.xml")
        assertTrue("missing $locale/strings.xml", file.isFile)
        return Regex("<string name=\"([^\"]+)\"[^>]*>(.*?)</string>", RegexOption.DOT_MATCHES_ALL)
            .findAll(file.readText())
            .associate { it.groupValues[1] to it.groupValues[2] }
    }

    private fun plurals(locale: String): Map<String, List<String>> {
        val file = File(moduleDir, "src/main/res/$locale/strings.xml")
        assertTrue("missing $locale/strings.xml", file.isFile)
        return Regex("<plurals name=\"([^\"]+)\">(.*?)</plurals>", RegexOption.DOT_MATCHES_ALL)
            .findAll(file.readText())
            .associate { match ->
                match.groupValues[1] to Regex("<item quantity=\"[^\"]+\">(.*?)</item>", RegexOption.DOT_MATCHES_ALL)
                    .findAll(match.groupValues[2])
                    .map { it.groupValues[1] }
                    .toList()
            }
    }
}
