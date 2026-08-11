package cz.cleansia.customer.features.membership

import java.io.File
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Test

/**
 * This guard used to assert that no express claim existed anywhere, because the surcharge was charged
 * to members and non-members alike. The waiver now ships, so the claim is true and the guard is
 * narrowed to the false versions of it rather than deleted: the promise must exist in every locale,
 * must not name a quota the admin can change, and must not describe the window as same-day.
 *
 * It walks VALUES, not key names — the claim that shipped wrong shipped under keys that read fine.
 */
class MembershipExpressClaimTest {

    private val locales = listOf("values", "values-cs", "values-sk", "values-uk", "values-ru")

    /** Every stem the perk is worded with across the five locales. */
    private val expressStems = listOf("express", "expres", "експрес", "экспресс")

    /** Keys that carry the affirmative promise or the booking-flow disclosure. */
    private val expressKeys = listOf(
        "membership_perk_express_title",
        "membership_perk_express_desc",
        "membership_perk_pill_express",
        "membership_perk_pill_express_used",
        "membership_perk_pill_express_trial",
        "membership_success_perk_express",
        "booking_slot_express_waived",
        "booking_summary_express_surcharge_waived",
        "booking_express_waiver_available",
        "booking_express_waiver_used",
        "booking_express_waiver_trial",
    )

    /**
     * `BookingPolicy` is a 2-4 h lead time. A 09:00 booking for 18:00 is same-day and already free for
     * everyone, so a same-day promise is the false claim the perk was removed for the first time.
     */
    private val sameDayClaim = Regex(
        "same[-\\s]?day|t[ýe]ž den|ten sam[ýá] den|tent[ýy]ž den|stejn[ýé] den|" +
            "ist[ýá] deň|rovnak[ýá] deň|того ж дня|той самий день|тот же день|тот самый день",
        RegexOption.IGNORE_CASE,
    )

    /** The two numbers the copy MAY name, erased by shape so only a hardcoded quota survives. */
    private val surchargeRate = Regex("\\b20\\s?%")
    private val leadTimeWindow = Regex("\\b2\\D{1,6}4\\b")
    private val formatPlaceholder = Regex("%\\d+\\\$[a-z]|%[a-z]")

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
    fun `every express string is present and non-empty in every locale`() {
        locales.forEach { locale ->
            val xml = stringsXml(locale)
            expressKeys.forEach { key ->
                val value = valueOf(xml, key)
                assertTrue("$locale/$key is missing", value != null)
                assertTrue("$locale/$key is blank", value!!.isNotBlank())
            }
        }
    }

    /**
     * `booking_slot_express` shipped as the bare English literal "Express +20%" in every locale while
     * its waived twin was translated, so the chip flipped between an English and a Ukrainian label
     * mid-flow. A Cyrillic locale cannot express any of this copy in Latin script.
     */
    @Test
    fun `every express string in a Cyrillic locale is written in Cyrillic`() {
        val cyrillic = Regex("\\p{IsCyrillic}")
        expressStrings()
            .filter { (locale, _, _) -> locale == "values-uk" || locale == "values-ru" }
            .forEach { (locale, key, value) ->
                assertTrue(
                    "$locale/$key is untranslated Latin script — $value",
                    cyrillic.containsMatchIn(value),
                )
            }
    }

    @Test
    fun `no locale describes the express window as same-day`() {
        expressStrings().forEach { (locale, key, value) ->
            assertTrue(
                "$locale/$key says same-day; BookingPolicy is a 2-4h lead time — $value",
                !sameDayClaim.containsMatchIn(value),
            )
        }
    }

    /**
     * `ExpressUpgradesPerMonth` is per-plan and admin-editable, so the promise must not name it. The
     * two numbers the copy legitimately carries — the 20 % rate and the 2-4 h window — are erased by
     * SHAPE rather than allow-listed as digits, because allow-listing 2 also permits
     * "2 free express bookings a month", which is the hardcoded quota this rule exists to stop.
     */
    @Test
    fun `no express string hardcodes the monthly quota`() {
        expressStrings().forEach { (locale, key, value) ->
            val stripped = leadTimeWindow.replace(
                surchargeRate.replace(
                    formatPlaceholder.replace(value, "").replace("%%", "%"),
                    "",
                ),
                "",
            )
            assertEquals(
                "$locale/$key names a number that is neither the rate nor the window — $value",
                emptyList<Char>(),
                stripped.filter { it.isDigit() }.toList(),
            )
        }
    }

    /**
     * The count is the server's answer before the booking under composition. A client that adds or
     * subtracts from it disagrees with the server the first time an order is cancelled.
     */
    @Test
    fun `every counted express string keeps its placeholder and escapes its percent sign`() {
        listOf("membership_perk_pill_express", "booking_express_waiver_available").forEach { key ->
            locales.forEach { locale ->
                val value = valueOf(stringsXml(locale), key)!!
                assertTrue("$locale/$key lost its %1\$d placeholder", value.contains("%1\$d"))
                assertEquals(
                    "$locale/$key has an unescaped % — String.format would throw",
                    0,
                    value.replace("%1\$d", "").replace("%%", "").count { it == '%' },
                )
            }
        }
    }

    @Test
    fun `every membership screen gates the express claim on a server field`() {
        assertTrue(
            "the management card stopped rendering the resolved express perk",
            screenSource("MembershipManagementCard.kt").contains("MembershipPerk.Express"),
        )
        listOf("SubscribePlusScreen.kt", "MembershipSuccessScreen.kt").forEach { screen ->
            assertTrue(
                "$screen no longer advertises the express perk",
                screenSource(screen).contains("membership_perk_express_title") ||
                    screenSource(screen).contains("membership_success_perk_express"),
            )
        }
    }

    /**
     * The plan flag is not the gate — `GetMyMembership` already reports a zero quota for a plan whose
     * flag is off, so reading the flag as well would give one fact two sources of truth.
     */
    @Test
    fun `no membership screen branches on the bare express flag`() {
        screens.forEach { screen ->
            assertTrue(
                "$screen reads allowsExpressUpgrade instead of the server's quota",
                !screenSource(screen).contains("allowsExpressUpgrade"),
            )
        }
    }

    private fun expressStrings(): List<Triple<String, String, String>> =
        locales.flatMap { locale ->
            Regex("<string name=\"([^\"]+)\"[^>]*>(.*?)</string>", RegexOption.DOT_MATCHES_ALL)
                .findAll(stringsXml(locale))
                .filter { match -> expressStems.any { match.groupValues[2].contains(it, ignoreCase = true) } }
                .map { Triple(locale, it.groupValues[1], it.groupValues[2]) }
                .toList()
        }.also {
            assertTrue(
                "the walk found ${it.size} express strings, fewer than the ${expressKeys.size} keys × " +
                    "${locales.size} locales it must cover",
                it.size >= expressKeys.size * locales.size,
            )
        }

    private fun screenSource(screen: String): String =
        File(moduleDir, "src/main/java/cz/cleansia/customer/features/membership/$screen")
            .also { assertTrue("$screen not found at ${it.absolutePath}", it.isFile) }
            .readText()

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
