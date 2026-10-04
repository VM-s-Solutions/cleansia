package cz.cleansia.customer.features.booking

import cz.cleansia.customer.core.data.UserAddress
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
 * A resumed draft's address follows Home's choice only while it is still the one the sheet seeded, and
 * an open sheet re-checks its time when the app comes back to the foreground.
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
            "the open effect does more than report the sheet's visibility and re-check a plain open's time",
            flat.contains(
                "LaunchedEffect(visible) { bookingVm.setSheetVisible(visible) " +
                    "if (visible && rebookFromOrderId == null && prefillPackageId == null && prefillSize == null) { " +
                    "bookingVm.revalidateResumedTime() } }",
            ),
        )
        assertTrue("an open sends the draft back to its first step", !sheet.contains("returnToFirstStep()"))
    }

    /**
     * A swipe takes the sheet to Hidden before `visible` flips, so the content leaves composition without
     * composing closed: only leaving composition sees that close, and without it the express band is read
     * against an older close, or none.
     */
    @Test
    fun `every way out of the sheet, a swipe included, records the close`() {
        val flat = sheet.replace(Regex("\\s+"), " ")
        assertTrue(
            "leaving composition no longer records the close, so a swiped-away draft is never stamped",
            flat.contains("DisposableEffect(bookingVm) { onDispose { bookingVm.setSheetVisible(false) } }"),
        )
    }

    /**
     * A sheet left open in the background is re-checked on the way back (the view model's half is
     * BookingViewModelTest's `revalidate_onAnOpenSheet_…`). Only a start that follows a stop counts: the
     * ON_START a new observer is sent on entering composition would otherwise re-check every open,
     * seeded ones included, before their seed resets the draft.
     */
    @Test
    fun `an open sheet re-checks its time when the app comes back to the foreground`() {
        val flat = sheet.replace(Regex("\\s+"), " ")
        assertTrue(
            "a stop is no longer recorded, so the return cannot be told from the sheet opening",
            flat.contains("LifecycleEventEffect(Lifecycle.Event.ON_STOP) { stopped = true }"),
        )
        assertTrue(
            "a return to the foreground no longer re-checks the open sheet's time",
            flat.contains("LifecycleEventEffect(Lifecycle.Event.ON_START) {") &&
                flat.substringAfter("LifecycleEventEffect(Lifecycle.Event.ON_START) {")
                    .substringBefore("stopped = false }")
                    .contains("bookingVm.revalidateResumedTime()"),
        )
    }

    /**
     * A booking being placed or paid is not re-checked on the way back. The card path creates the order
     * and then shows Stripe's PaymentSheet, and a bank's 3-D Secure screen or app stops the host; a slot
     * boundary passing in that hop cleared the paid booking's time and told the customer to pick one
     * again. A cash submit left in flight is the same. The submit state is read from the view model, not
     * the composed `submitting`, which stops following the flow while the host is stopped.
     */
    @Test
    fun `a booking being placed or paid is not re-checked on the way back`() {
        val flat = sheet.replace(Regex("\\s+"), " ")
        val onStart = flat.substringAfter("LifecycleEventEffect(Lifecycle.Event.ON_START) {")
            .substringBefore("stopped = false }")
        assertTrue(
            "a return during a submit no longer reads the live submit state",
            onStart.contains(
                "val placing = bookingVm.submitState.value is cz.cleansia.customer.ui.state.ActionState.Submitting",
            ),
        )
        assertTrue(
            "a return while an order is in flight or being paid re-checks its time",
            onStart.contains(
                "if (stopped && visible && !placing && pendingCardOrder == null) bookingVm.revalidateResumedTime()",
            ),
        )
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
            2,
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

    private fun saved(serverId: String, street: String) = UserAddress(
        id = "local-$serverId",
        serverId = serverId,
        label = street,
        street = street,
        city = "Praha",
        zipCode = "11000",
        countryIsoCode = "cz",
    )

    private val homeA = saved("a", "Vinohradská 1")
    private val homeB = saved("b", "Karlova 2")
    private val homeC = saved("c", "Nerudova 4")

    @Test
    fun `a blank draft takes Home's address`() {
        val seeded = BookingState().hydratedWithPreferred(homeA)
        assertEquals("Vinohradská 1", seeded.street)
        assertEquals("a", seeded.savedAddressId)
        assertEquals("a", seeded.hydratedFromSavedId)
    }

    @Test
    fun `a resumed draft follows Home's address when the sheet seeded the one it holds`() {
        val resumed = BookingState(rooms = 3).hydratedWithPreferred(homeA).hydratedWithPreferred(homeB)
        assertEquals("Karlova 2", resumed.street)
        assertEquals("b", resumed.savedAddressId)
        assertEquals("b", resumed.hydratedFromSavedId)
        assertEquals("the rest of the draft is kept", 3, resumed.rooms)
    }

    @Test
    fun `an address picked in the sheet is kept when Home's address changes`() {
        val picked = BookingState().hydratedWithPreferred(homeC).copy(
            street = homeA.street,
            city = homeA.city,
            zipCode = homeA.zipCode,
            countryIsoCode = homeA.countryIsoCode,
            savedAddressId = homeA.serverId,
        )
        assertEquals(picked, picked.hydratedWithPreferred(homeB))
    }

    @Test
    fun `an address the sheet never seeded is kept`() {
        val rebooked = BookingState(street = "Vinohradská 1", savedAddressId = "a")
        assertEquals(rebooked, rebooked.hydratedWithPreferred(homeB))
        val typed = BookingState(street = "Na Příkopě 3")
        assertEquals(typed, typed.hydratedWithPreferred(homeB))
    }
}
