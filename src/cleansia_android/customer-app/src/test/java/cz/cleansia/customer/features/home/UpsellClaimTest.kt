package cz.cleansia.customer.features.home

import java.io.File
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Test

/**
 * What the home carousel, the profile's Plus card and the recurring paywall promise every non-member,
 * held to what the platform delivers. The same claims the web's `plus-trial-claim.spec.ts` guards
 * on its own surfaces.
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

    @Test
    fun `the referral slide pays both sides after the friend's first completed cleaning`() {
        val points = Regex("public\\s+const\\s+int\\s+PointsPerSide\\s*=\\s*(\\d+)\\s*;")
            .find(File(solutionDir, "Cleansia.Core.AppServices/Features/Orders/ReferralPolicy.cs").readText())
            ?.groupValues?.get(1)
            ?: error("ReferralPolicy.PointsPerSide not found — the parser needs updating")
        locales.forEach { locale ->
            val value = strings(locale)["home_upsell_referral_title"]
            assertTrue("$locale/home_upsell_referral_title is missing", value != null)
            assertTrue("$locale does not state $points points — $value", value!!.contains(points))
            assertTrue(
                "$locale does not wait for a completed cleaning — $value",
                value.contains(completedStem.getValue(locale), ignoreCase = true),
            )
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
}
