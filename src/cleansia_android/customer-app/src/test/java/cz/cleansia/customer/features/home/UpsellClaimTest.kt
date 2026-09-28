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
     * `MembershipPlan.TrialPeriodDays` is 0 on every plan and the admin validators refuse any other
     * value, so no plan has a trial. These rows render to every non-member with no trial gate.
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

    @Test
    fun `the non-member perks line does not promise cancelling anytime`() {
        val claims = locales.mapNotNull { locale ->
            val value = strings(locale)["membership_inactive_perks_summary"]
                ?: return@mapNotNull "$locale/membership_inactive_perks_summary is missing"
            if (cancelAnytimeClaim.containsMatchIn(value)) "$locale: $value" else null
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
