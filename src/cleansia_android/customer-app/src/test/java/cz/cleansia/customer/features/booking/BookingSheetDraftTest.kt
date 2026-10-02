package cz.cleansia.customer.features.booking

import java.io.File
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Test

/**
 * The booking sheet's own rules: which way a step change slides, and when it keeps the draft and when
 * it starts afresh. The sheet has no Compose harness,
 * so its open effects are pinned as source: a plain open (the Book FAB) resumes the draft, and each
 * open that seeds a booking — Order again, a popular package, the quick-size slide — resets it before
 * filling. The view model's half is BookingViewModelTest's `closingTheSheet_keepsTheDraftAndItsStep`.
 */
class BookingSheetDraftTest {

    private val sheet: String = sequenceOf(File("."), File("customer-app"), File("src/cleansia_android/customer-app"))
        .map { File(it, "src/main/java/cz/cleansia/customer/features/booking/BookingBottomSheet.kt") }
        .first { it.isFile }
        .readText()
        .lines()
        .filterNot { it.trimStart().startsWith("//") || it.trimStart().startsWith("*") }
        .joinToString("\n")

    @Test
    fun `a plain open resumes the draft`() {
        val flat = sheet.replace(Regex("\\s+"), " ")
        assertTrue(
            "the open effect does more than report the sheet's visibility",
            flat.contains("LaunchedEffect(visible) { bookingVm.setSheetVisible(visible) }"),
        )
        assertTrue("an open sends the draft back to its first step", !sheet.contains("returnToFirstStep()"))
    }

    @Test
    fun `every seeded open starts its booking afresh before filling it`() {
        listOf(
            "lastRebookedFrom = target" to "orderRepo.getById(target)",
            "lastPrefilledPackage = target" to "selectedPackageIds = current.selectedPackageIds + target",
            "lastPrefilledSize = target" to "bookingVm.setRooms(target.first)",
        ).forEach { (guard, fill) ->
            val effect = sheet.substringAfter(guard).substringBefore(fill)
            assertTrue("the seed after `$guard` no longer resets the draft first", effect.contains("bookingVm.reset()"))
        }
    }

    @Test
    fun `a booking that goes through clears the draft before navigating`() {
        assertEquals(
            "a success path no longer resets before onComplete",
            3,
            Regex("bookingVm\\.reset\\(\\)\\s*onComplete\\(").findAll(sheet).count(),
        )
    }

    /** Forward enters from the trailing edge and back from the leading one, mirrored right to left. */
    @Test
    fun `a step slides in from the trailing edge going forward and from the leading edge going back`() {
        assertEquals(1, stepSlideDirection(forward = true, rtl = false))
        assertEquals(-1, stepSlideDirection(forward = false, rtl = false))
        assertEquals(-1, stepSlideDirection(forward = true, rtl = true))
        assertEquals(1, stepSlideDirection(forward = false, rtl = true))
    }
}
