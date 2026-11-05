package cz.cleansia.partner.features.orders

import cz.cleansia.partner.R
import cz.cleansia.partner.api.model.Code
import cz.cleansia.partner.api.model.OrderListItem
import cz.cleansia.partner.api.model.OrderStatus
import java.time.Instant
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Test

/** Which short panel, pinned area, head figure and sheet close a page draws, decided from what it holds. */
class OrdersPageChoicesTest {

    private val loadedAt = Instant.parse("2026-11-12T08:14:00Z")
    private fun row(id: String) = OrderListItem(id = id, orderStatus = Code(value = 0))

    @Test
    fun `Available's short panel follows the rows on show, not the raw page`() {
        val expiredOnly = PaneState(rows = listOf(row("V9")), total = 1, loadedAt = loadedAt)
        assertEquals(AvailableShort.Empty, availableShort(expiredOnly, cards = emptyList(), reservedShown = false))
        assertEquals(AvailableShort.Empty, availableShort(expiredOnly.copy(failed = true), cards = emptyList(), reservedShown = false))
        assertEquals(AvailableShort.Error, availableShort(PaneState(failed = true), cards = emptyList(), reservedShown = false))
        assertNull(availableShort(PaneState(failed = true), cards = emptyList(), reservedShown = true))
        assertNull(availableShort(expiredOnly, cards = listOf(row("V1")), reservedShown = false))
    }

    @Test
    fun `Close does nothing while the take is in flight, and closes the sheet otherwise`() {
        var closed = 0
        closeWhenIdle(submitting = true) { closed++ }()
        assertEquals(0, closed)
        closeWhenIdle(submitting = false) { closed++ }()
        assertEquals(1, closed)
    }

    @Test
    fun `the head's figure is the state's own, from the row while the job loads, and a cancelled job's rests`() {
        assertEquals(R.drawable.mascot_waving, detailMascot(OrderStatus._2, isMine = true))
        assertEquals(R.drawable.mascot_ready, detailMascot(OrderStatus._3, isMine = true))
        assertEquals(R.drawable.mascot_resting, detailMascot(OrderStatus._3, isMine = true, reported = true))
        assertEquals(R.drawable.mascot_invoice, detailMascot(OrderStatus._4, isMine = true, cash = true))
        assertEquals(R.drawable.mascot_spray_and_cloth, detailMascot(OrderStatus._4, isMine = true))
        assertEquals(R.drawable.mascot_thumbs_up, detailMascot(OrderStatus._5, isMine = true))
        assertEquals(R.drawable.mascot_leaning, detailMascot(OrderStatus._0, isMine = false))
        assertEquals(R.drawable.mascot_waving, detailMascot(OrderStatus._0, isMine = false, offered = true))
        assertEquals(R.drawable.mascot_resting, detailMascot(OrderStatus._6, isMine = true))
    }

    @Test
    fun `from the board's row, loading holds the move's slot and an error pins only the locked set-off`() {
        val locked = PinnedMove.SetOffLocked(loadedAt)
        assertEquals(RowPin.Move, rowDetailPin(locked, failed = false))
        assertEquals(RowPin.Move, rowDetailPin(locked, failed = true))
        assertEquals(RowPin.Move, rowDetailPin(PinnedMove.TakeWaiting(preferred = true), failed = false))
        assertEquals(RowPin.None, rowDetailPin(PinnedMove.TakeWaiting(preferred = false), failed = true))
        assertEquals(RowPin.SlotBar, rowDetailPin(PinnedMove.Start, failed = false))
        assertEquals(RowPin.None, rowDetailPin(PinnedMove.Start, failed = true))
        assertEquals(RowPin.None, rowDetailPin(PinnedMove.SetOff(late = false), failed = true))
    }
}
