package cz.cleansia.customer.features.booking

import java.io.File
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test

/**
 * The busy card draws its cleaner as tall as the hero's welcoming figure from its first frame. On a compact sheet the
 * hero takes the ticket's slack, so no size tier can say its height: the sheet lays the draft's confirmation out
 * unseen while the review step shows and sizes the busy card from that hero. There is no Compose harness here, so the
 * wiring is pinned as source.
 */
class BookingHandOverCallSiteTest {

    private val sheet: String = sequenceOf(File("."), File("customer-app"), File("src/cleansia_android/customer-app"))
        .map { File(it, "src/main/java/cz/cleansia/customer/features/booking/BookingBottomSheet.kt") }
        .first { it.isFile }
        .readText()
        .replace("\r\n", "\n")

    private val probe = sheet.substringAfter("private fun DraftConfirmation(", "").substringBefore("\n}\n")

    @Test
    fun `the draft and the booked order become one confirmation through one builder`() {
        assertTrue(sheet.contains("val booked = successFor(response)"))
        assertTrue(sheet.contains("successFor(CreateOrderResponse(id = \"\", confirmationCode = \"\"))"))
        assertEquals(1, Regex("""bookingSuccessOf\(""").findAll(sheet).count())
    }

    @Test
    fun `the draft's confirmation is laid out unseen in the confirmation's own slot while the review step shows`() {
        assertTrue(sheet.contains("if (confirmation == null && currentStep == TOTAL_STEPS) {"))
        assertTrue(probe.contains(".graphicsLayer { alpha = 0f }"))
        assertTrue(probe.contains(".clearAndSetSemantics {}"))
        assertTrue("keyboard and D-pad focus never enters the unseen buttons", probe.contains(".focusProperties { onEnter = { cancelFocusChange() } }\n                .focusGroup()"))
        assertTrue(probe.contains("Spacer(Modifier.height(handle))"))
        assertTrue(probe.contains("BookingSuccessUiState.Loading, {}, {}, Modifier.weight(1f), arrival = true, revealed = false,"))
    }

    @Test
    fun `the busy card takes the draft's hero until the booked one reports, and only the booked one releases the flight`() {
        assertTrue(sheet.contains("mascotFigureHeight = (heroMascot ?: draftHero)?.let {"))
        assertTrue(sheet.contains("onHero = { draftHero = it }"))
        assertFalse(probe.contains("mascotPlaced"))
    }
}
