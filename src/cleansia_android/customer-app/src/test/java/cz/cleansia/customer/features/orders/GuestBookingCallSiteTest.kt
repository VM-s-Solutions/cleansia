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

    /** The server answers order.not_found to a dispute, a review and a receipt on a guest booking. */
    @Test
    fun `a guest booking offers no issue report, review or receipt`() {
        val screen = source("OrderDetailScreen.kt")
        val reportIssue = Regex("""val canReportIssue = [^\n]*""").find(screen)?.value.orEmpty()
        assertTrue("Report an issue must be withheld on a guest booking, got `$reportIssue`", reportIssue.contains("!isGuestBooking"))

        val sheet = braced(screen, "private fun OrderDetailSheetContent(")
        assertTrue(
            "the saved-review card must render on a completed account booking with a review only",
            braced(sheet, "if (status == OrderStatus.Completed && !order.isGuestBooking && order.review != null)")
                .contains("ReviewCard(order = order, onLeaveReview = onLeaveReview)"),
        )
        val receipt = Regex("""val showReceipt = [^\n]*""").find(sheet)?.value.orEmpty()
        assertTrue("the receipt card must render on an account booking only, got `$receipt`", receipt.contains("!order.isGuestBooking"))
    }

    @Test
    fun `the fixed rating action uses the guest-aware eligibility gate`() {
        val screen = source("OrderDetailScreen.kt")
        val sheet = braced(screen, "private fun OrderDetailSheetContent(")
        assertTrue(
            "the pending rating must use the tested gate with the order's guest and saved-review flags",
            sheet.contains(
                "val showLeaveReview = customerCanLeaveReview(order.orderStatus?.value, order.isGuestBooking, order.review != null)",
            ),
        )
        val footerCall = braced(sheet, "if (hasFooter)")
        assertTrue("the footer must receive the gated pending-rating value", footerCall.contains("showLeaveReview = showLeaveReview,"))

        val footer = braced(screen, "private fun ActionsFooter(")
        val ratingAction = braced(footer.substringBefore("if (showRebook)"), "if (showLeaveReview)")
        assertTrue("the gated action must open the rating sheet", ratingAction.contains("onClick = onLeaveReview,"))
        assertTrue("the gated action must be the rating button", ratingAction.contains("R.string.order_review_sheet_title"))
    }

    /** Function parameter defaults may contain lambdas before the body's opening brace. */
    private fun braced(text: String, marker: String): String {
        val start = text.indexOf(marker)
        assertTrue("no `$marker` found", start >= 0)
        assertTrue("`$marker` must be unique for this extraction to mean anything", start == text.lastIndexOf(marker))
        var bodyStart = start + marker.length
        if (marker.endsWith("(")) {
            var parameterDepth = 1
            while (parameterDepth > 0 && bodyStart < text.length) {
                when (text[bodyStart++]) {
                    '(' -> parameterDepth++
                    ')' -> parameterDepth--
                }
            }
            assertTrue("unbalanced parameters after `$marker`", parameterDepth == 0)
        }
        val open = text.indexOf('{', bodyStart)
        assertTrue("no body after `$marker`", open >= 0)
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
