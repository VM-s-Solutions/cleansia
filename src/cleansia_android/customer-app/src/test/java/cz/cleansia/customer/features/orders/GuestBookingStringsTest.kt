package cz.cleansia.customer.features.orders

import java.io.File
import org.junit.Assert.assertTrue
import org.junit.Test

/**
 * The label and the note are the only way a customer can tell a booking made without an account from
 * their own, and a missing locale row falls back to English on it.
 */
class GuestBookingStringsTest {

    private val locales = listOf("values", "values-cs", "values-sk", "values-uk", "values-ru")

    private val keys = listOf("orders_guest_booking", "order_detail_guest_booking_note")

    private val resDir: File = sequenceOf(
        File("src/main/res"),
        File("customer-app/src/main/res"),
        File("src/cleansia_android/customer-app/src/main/res"),
    ).firstOrNull { it.isDirectory }
        ?: error("customer-app res/ not found from working dir ${File(".").absolutePath}")

    private fun valueOf(locale: String, key: String): String? =
        Regex("<string name=\"$key\">(.*?)</string>", RegexOption.DOT_MATCHES_ALL)
            .find(File(resDir, "$locale/strings.xml").readText())
            ?.groupValues
            ?.get(1)

    @Test
    fun `the guest booking label and note are written in all five locales`() {
        locales.forEach { locale ->
            keys.forEach { key ->
                assertTrue("$locale/$key is missing or blank", valueOf(locale, key)?.isNotBlank() == true)
            }
        }
    }

    @Test
    fun `no translation is the English copy`() {
        locales.drop(1).forEach { locale ->
            keys.forEach { key ->
                assertTrue("$locale/$key is still English", valueOf(locale, key) != valueOf("values", key))
            }
        }
    }
}
