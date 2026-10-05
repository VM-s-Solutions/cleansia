package cz.cleansia.customer.features.recurring

import java.io.File
import org.junit.Assert.assertEquals
import org.junit.Test

/**
 * The owner's own words for a schedule that holds an entry its market no longer offers (2026-10-05), on
 * every client alike. A missing locale falls back to English, so each is pinned verbatim in all five.
 */
class RecurringNoLongerOfferedCopyTest {

    private val copy = mapOf(
        "recurring_card_item_no_longer_offered" to mapOf(
            "values" to "Includes a service no longer offered — edit to update",
            "values-cs" to "Obsahuje službu, kterou už nenabízíme — upravte objednávku",
            "values-sk" to "Obsahuje službu, ktorú už neponúkame — upravte objednávku",
            "values-uk" to "Містить послугу, яку ми більше не пропонуємо — змініть бронювання",
            "values-ru" to "Содержит услугу, которую мы больше не предлагаем — измените бронирование",
        ),
    )

    private val resDir: File = sequenceOf(
        File("src/main/res"),
        File("customer-app/src/main/res"),
        File("src/cleansia_android/customer-app/src/main/res"),
    ).firstOrNull { it.isDirectory }
        ?: error("customer-app res/ not found from working dir ${File(".").absolutePath}")

    @Test
    fun `the copy is the owner's in all five locales`() {
        copy.forEach { (key, byLocale) ->
            byLocale.forEach { (locale, expected) ->
                val xml = File(resDir, "$locale/strings.xml").readText()
                val value = Regex("<string name=\"$key\">(.*?)</string>").find(xml)?.groupValues?.get(1)
                assertEquals("$locale/$key", expected, value?.replace("\\'", "'"))
            }
        }
    }
}
