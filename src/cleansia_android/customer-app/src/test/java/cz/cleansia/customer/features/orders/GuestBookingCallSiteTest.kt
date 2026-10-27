package cz.cleansia.customer.features.orders

import java.io.File
import org.junit.Assert.assertTrue
import org.junit.Test

/**
 * A wire flag nothing renders decodes green forever, and these composables have no test harness, so the
 * call sites are read off the source. Each assertion is scoped to the block the flag gates inside the
 * one composable that owns it, so the key named anywhere else cannot satisfy it.
 *
 * Evidence here is Kotlin source read off disk, which Gradle does not track as an input — this file only
 * means something under `testDebugUnitTest --rerun-tasks`.
 */
class GuestBookingCallSiteTest {

    private val featureDir: File = sequenceOf(
        File("."),
        File("customer-app"),
        File("src/cleansia_android/customer-app"),
    ).map { File(it, "src/main/java/cz/cleansia/customer/features/orders") }
        .firstOrNull { it.isDirectory }
        ?: error("orders feature dir not found from working dir ${File(".").absolutePath}")

    private fun source(name: String) = File(featureDir, name).readText()

    @Test
    fun `the My orders card labels a guest booking`() {
        val card = braced(source("OrdersTab.kt"), "private fun OrderCard(")
        assertTrue(
            "OrderCard must label a guest booking",
            braced(card, "if (order.isGuestBooking)").contains("R.string.orders_guest_booking"),
        )
    }

    @Test
    fun `the pinned header labels a guest booking`() {
        val header = braced(source("OrderDetailHeader.kt"), "internal fun OrderDetailCompactHeader(")
        assertTrue(
            "the compact header must label a guest booking",
            braced(header, "if (order.isGuestBooking)").contains("R.string.orders_guest_booking"),
        )
    }

    @Test
    fun `the sheet says what a guest booking can and cannot do`() {
        val sheet = braced(source("OrderDetailScreen.kt"), "private fun OrderDetailSheetContent(")
        assertTrue(
            "the sheet must carry the guest booking note",
            braced(sheet, "if (order.isGuestBooking)").contains("R.string.order_detail_guest_booking_note"),
        )
    }

    /** The body of the first `{ … }` that follows [marker], which must occur exactly once in [text]. */
    private fun braced(text: String, marker: String): String {
        val start = text.indexOf(marker)
        assertTrue("no `$marker` found", start >= 0)
        assertTrue("`$marker` must be unique for this extraction to mean anything", start == text.lastIndexOf(marker))
        val open = text.indexOf('{', start + marker.length)
        var depth = 0
        for (i in open until text.length) {
            when (text[i]) {
                '{' -> depth++
                '}' -> if (--depth == 0) return text.substring(open, i + 1)
            }
        }
        error("unbalanced braces after `$marker`")
    }
}
