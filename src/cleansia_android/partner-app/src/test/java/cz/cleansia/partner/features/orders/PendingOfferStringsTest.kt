package cz.cleansia.partner.features.orders

import java.io.File
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Test

/**
 * Copy is the whole feature here — a reservation is a promise, and the words are the promise. None of
 * these assertions is visible to the compiler: an untranslated row falls back to English, a dropped
 * placeholder throws only at render, and a sentence that says too much is a policy breach nothing
 * else in the tree can see.
 *
 * These read every locale's `strings.xml` off disk through a path Gradle does not track, so they are
 * a silent non-run unless the task is forced (`--rerun-tasks --no-build-cache`).
 */
class PendingOfferStringsTest {

    private val locales = listOf("values", "values-cs", "values-sk", "values-uk", "values-ru")

    private val resDir: File = sequenceOf(
        File("src/main/res"),
        File("partner-app/src/main/res"),
        File("src/cleansia_android/partner-app/src/main/res"),
    ).firstOrNull { it.isDirectory }
        ?: error("partner-app res/ not found from working dir ${File(".").absolutePath}")

    private fun strings(locale: String): Map<String, String> =
        Regex("<string name=\"([^\"]+)\">(.*?)</string>", RegexOption.DOT_MATCHES_ALL)
            .findAll(File(resDir, "$locale/strings.xml").readText())
            .associate { it.groupValues[1] to it.groupValues[2] }

    private val required = listOf(
        "offers_title",
        "offers_subtitle",
        "offer_reserved_until_today",
        "offer_reserved_until_tomorrow",
        "offer_reserved_until_date",
        "offer_reserved_ended",
        "offer_confirm",
        "offer_slide_to_confirm",
        "offer_confirming",
        "offer_decline",
        "offer_decline_title",
        "offer_decline_body",
        "offer_decline_cta",
        "offer_declined_toast",
        "offer_empty",
        "offer_blocked_title",
        "offer_blocked_body",
        "offer_release_failed_title",
        "offer_release_failed_body",
        "offers_card_title",
        "offers_card_more",
        "offers_card_cta",
    )

    @Test
    fun `every offer string is written in all five locales`() {
        locales.forEach { locale ->
            val declared = strings(locale)
            required.forEach { key ->
                assertTrue("$locale/strings.xml is missing $key", key in declared)
                assertTrue("$locale/strings.xml leaves $key empty", declared.getValue(key).isNotBlank())
            }
        }
    }

    /**
     * A dropped positional argument is a crash at render time, in one locale, on a screen a cleaner
     * only reaches when the platform has already broken a promise to them.
     */
    @Test
    fun `every placeholder survives translation`() {
        val expected = mapOf(
            "offer_reserved_until_today" to listOf("%1\$s"),
            "offer_reserved_until_tomorrow" to listOf("%1\$s"),
            "offer_reserved_until_date" to listOf("%1\$s", "%2\$s"),
            "offer_blocked_body" to listOf("%1\$s"),
            "offer_release_failed_body" to listOf("%1\$s"),
            "offers_card_more" to listOf("%1\$d"),
        )
        locales.forEach { locale ->
            val declared = strings(locale)
            expected.forEach { (key, placeholders) ->
                val value = declared.getValue(key)
                placeholders.forEach { placeholder ->
                    assertTrue("$locale/$key no longer carries $placeholder: \"$value\"", placeholder in value)
                }
            }
        }
    }

    /**
     * The customer hears ONE sentence whether the cleaner refused or simply never answered, and it
     * never says which. So the cleaner's decline copy may not describe what the customer will be
     * told — the moment it mentions them it is making a claim the platform has ruled it will not
     * make. Naming the customer at all is the cheapest observable form of that breach.
     */
    @Test
    fun `the decline copy never says what the customer will hear`() {
        val customerWords = mapOf(
            "values" to listOf("customer", "client"),
            "values-cs" to listOf("zákazník"),
            "values-sk" to listOf("zákazník"),
            "values-uk" to listOf("клієнт"),
            "values-ru" to listOf("клиент"),
        )
        listOf(
            "offer_decline_title",
            "offer_decline_body",
            "offer_decline_cta",
            "offer_declined_toast",
            "offer_release_failed_title",
            "offer_release_failed_body",
        )
            .forEach { key ->
                customerWords.forEach { (locale, words) ->
                    val value = strings(locale).getValue(key).lowercase()
                    words.forEach { word ->
                        assertTrue(
                            "$locale/$key tells the cleaner what the customer is told: \"$value\"",
                            !value.contains(word),
                        )
                    }
                }
            }
    }

    /**
     * Every refusal `TakeOrder`'s ordered chain can answer a confirm with. The screen quotes the
     * server's own reason inside the sentence that owns the failure, so a key with no resource would
     * put a raw `order.weekly_limit_reached` where that reason belongs.
     */
    @Test
    fun `every refusal a confirm can hit resolves to a sentence`() {
        val declared = strings("values")
        listOf(
            "order.not_found",
            "order.take.already_cancelled",
            "order.take.already_completed",
            "order.not_takeable",
            "order.no_available_spots",
            "order.employee_already_assigned",
            "order.weekly_limit_reached",
            "order.time_conflict",
        ).forEach { key ->
            val normalized = key.replace('.', '_').replace('-', '_').lowercase()
            assertTrue(
                "$key renders raw — no error_$normalized and no error_key_$normalized",
                "error_$normalized" in declared || "error_key_$normalized" in declared,
            )
        }
    }

    /**
     * No surface may state a time-to-assignment, and the deadline is an INSTANT rather than a
     * countdown for the same reason: the hold's real expiry is server-side. Copy that spells a
     * duration re-encodes it where nothing can check it.
     */
    @Test
    fun `no offer string promises a duration`() {
        val durationish = Regex("""\d+\s*(min|hour|hod|hodin|god|час|хвил|мин|minút|minut)""", RegexOption.IGNORE_CASE)
        locales.forEach { locale ->
            val declared = strings(locale)
            required.forEach { key ->
                val value = declared.getValue(key)
                assertEquals(
                    "$locale/$key states a duration where only an instant is honest: \"$value\"",
                    null,
                    durationish.find(value)?.value,
                )
            }
        }
    }
}
