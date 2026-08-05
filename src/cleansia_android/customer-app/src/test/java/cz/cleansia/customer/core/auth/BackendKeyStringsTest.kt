package cz.cleansia.customer.core.auth

import java.io.File
import org.junit.Assert.assertTrue
import org.junit.Assert.fail
import org.junit.Test

/**
 * `ApiErrorParser` resolves a backend key through `Resources.getIdentifier` and,
 * on a miss, returns the raw key — so an unmapped refusal reaches the snackbar as
 * "recurring_booking.membership_required". Nothing breaks the build, and the key
 * set staying consistent across the five locales does not help either: a key
 * missing from *all five* is perfectly consistent. Only naming the keys the app
 * can actually receive catches that.
 */
class BackendKeyStringsTest {

    private val locales = listOf("values", "values-cs", "values-sk", "values-uk", "values-ru")

    /**
     * `BusinessErrorMessage.RecurringTemplate*` — every refusal
     * `CreateRecurringBooking` can answer the customer app with.
     */
    private val recurringBookingKeys = listOf(
        "recurring_booking.ends_on_before_start",
        "recurring_booking.membership_required",
        "recurring_booking.no_services_or_packages",
        "recurring_booking.not_found",
        "recurring_booking.not_owned_by_user",
        "recurring_booking.saved_address_not_found",
        "recurring_booking.starts_on_in_past",
    )

    private val resDir: File = sequenceOf(
        File("src/main/res"),
        File("customer-app/src/main/res"),
        File("src/cleansia_android/customer-app/src/main/res"),
    ).firstOrNull { it.isDirectory }
        ?: error("customer-app res/ not found from working dir ${File(".").absolutePath}")

    private fun declared(locale: String): Set<String> {
        val file = File(resDir, "$locale/strings.xml")
        assertTrue("missing $locale/strings.xml", file.isFile)
        return Regex("<string name=\"([^\"]+)\"")
            .findAll(file.readText())
            .map { it.groupValues[1] }
            .toSet()
    }

    /** The transform in `ApiErrorParser.resolveStringByErrorKey`, kept in step by its own test. */
    private fun resourceName(key: String) = "error_" + key.replace('.', '_').lowercase()

    @Test
    fun `every recurring-booking refusal resolves to a sentence in all five locales`() {
        val raw = locales.flatMap { locale ->
            val declared = declared(locale)
            recurringBookingKeys
                .filterNot { resourceName(it) in declared }
                .map { "$locale/${resourceName(it)} ($it)" }
        }
        if (raw.isNotEmpty()) {
            fail(
                "these refusals reach the snackbar as their raw backend key: " +
                    raw.joinToString(", "),
            )
        }
    }
}
