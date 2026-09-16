package cz.cleansia.customer.features.orders

import cz.cleansia.customer.navigation.Routes
import java.io.File
import javax.xml.parsers.DocumentBuilderFactory
import kotlinx.serialization.encodeToString
import kotlinx.serialization.json.Json
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test
import org.w3c.dom.Element

class GuestOrderStringsTest {
    private val locales = listOf("values", "values-cs", "values-sk", "values-uk", "values-ru")
    private val keys = listOf(
        "guest_order_entry", "guest_order_title", "guest_order_intro", "guest_order_number",
        "guest_order_code", "guest_order_lookup", "guest_order_required", "guest_order_empty",
        "guest_order_loading", "guest_order_cancel", "guest_order_cannot_cancel", "guest_order_preview_required",
        "guest_order_fee_estimate",
    )
    private val resDir = sequenceOf(
        File("src/main/res"), File("customer-app/src/main/res"),
        File("src/cleansia_android/customer-app/src/main/res"),
    ).first { it.isDirectory }

    private fun strings(locale: String): Map<String, String> {
        val nodes = DocumentBuilderFactory.newInstance().newDocumentBuilder()
            .parse(File(resDir, "$locale/strings.xml")).getElementsByTagName("string")
        return (0 until nodes.length).associate {
            val element = nodes.item(it) as Element
            element.getAttribute("name") to element.textContent
        }
    }

    @Test
    fun `guest empty loading error and cancellation copy exists in every locale`() {
        val english = strings("values")
        for (locale in locales) {
            val entries = strings(locale)
            for (key in keys) {
                assertTrue("$locale is missing $key", !entries[key].isNullOrBlank())
                if (locale != "values") assertFalse("$locale has English $key", entries[key] == english[key])
            }
            assertFalse(entries.getValue("guest_order_preview_required").contains("%"))
        }
    }

    @Test
    fun `guest route has no serialized credential arguments`() {
        assertEquals("{}", Json.encodeToString(Routes.GuestOrder))
    }

    @Test
    fun `guest fee copy qualifies maximum refund and collected payments in every locale`() {
        val words = mapOf(
            "values" to listOf("Maximum", "collected card", "actual amount"),
            "values-cs" to listOf("Maximální", "přijaté platby kartou", "skutečně vrácenou"),
            "values-sk" to listOf("Maximálne", "prijaté platby kartou", "skutočne vrátenú"),
            "values-uk" to listOf("Максимальне", "отримані платежі карткою", "фактично повернену"),
            "values-ru" to listOf("Максимальный", "полученные платежи картой", "фактически возвращённая"),
        )
        for ((locale, required) in words) {
            val copy = strings(locale).getValue("guest_order_fee_estimate")
            assertTrue(copy.contains("%1\$s"))
            assertTrue(copy.contains("%2\$s"))
            for (word in required) assertTrue("$locale misses $word", copy.contains(word))
        }
    }
}
