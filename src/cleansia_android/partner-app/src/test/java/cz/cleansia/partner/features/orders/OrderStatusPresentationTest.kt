package cz.cleansia.partner.features.orders

import cz.cleansia.partner.R
import cz.cleansia.partner.api.model.Code
import cz.cleansia.partner.api.model.OrderStatus
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Test

/**
 * The timeline used to print the backend's English enum name verbatim, so a
 * Czech cleaner read "OnTheWay" on a screen where every other word was Czech.
 * Nothing failed: the wire name is not a string resource, so key-parity checks
 * and `R` itself are blind to it.
 *
 * These pin the two halves of the fix — the ordinal always resolves to a
 * translated resource, and the raw wire name is reachable only as a DEBUG
 * diagnostic. `isDebug` is a parameter rather than a read of `BuildConfig`
 * precisely so the production branch is assertable from a debug unit test.
 */
class OrderStatusPresentationTest {

    private val labels = mapOf(
        OrderStatus._0 to R.string.status_new,
        OrderStatus._1 to R.string.status_pending,
        OrderStatus._2 to R.string.status_confirmed,
        OrderStatus._3 to R.string.status_on_the_way,
        OrderStatus._4 to R.string.status_in_progress,
        OrderStatus._5 to R.string.status_completed,
        OrderStatus._6 to R.string.status_cancelled,
    )

    @Test
    fun `every backend status resolves to a translated label`() {
        labels.forEach { (status, res) ->
            assertEquals("$status", res, orderStatusLabelRes(status))
        }
    }

    /** Cancelled used to have no branch at all and rendered as "—". */
    @Test
    fun `cancelled is one of them`() {
        assertEquals(R.string.status_cancelled, orderStatusLabelRes(OrderStatus._6))
    }

    @Test
    fun `no two statuses share a label`() {
        assertEquals(labels.size, labels.values.toSet().size)
    }

    @Test
    fun `an absent status has no label resource`() {
        assertNull(orderStatusLabelRes(null))
    }

    @Test
    fun `every backend ordinal maps onto a typed status`() {
        (0..6).forEach { ordinal ->
            assertEquals("ordinal $ordinal", ordinal, Code(value = ordinal).toOrderStatus()?.value)
        }
        assertNull(Code(value = 99).toOrderStatus())
        assertNull((null as Code?).toOrderStatus())
    }

    /** The defect: no wire name may reach a translated build. */
    @Test
    fun `a status the app does not know renders a dash in production`() {
        listOf("New", "Pending", "Confirmed", "OnTheWay", "InProgress", "Completed", "Cancelled", "Rescheduled")
            .forEach { wireName ->
                assertEquals(wireName, "—", unknownOrderStatusLabel(wireName, isDebug = false))
            }
    }

    @Test
    fun `the wire name is prettified for us in debug only`() {
        assertEquals("On the way", unknownOrderStatusLabel("OnTheWay", isDebug = true))
        assertEquals("In progress", unknownOrderStatusLabel("InProgress", isDebug = true))
        assertEquals("Confirmed", unknownOrderStatusLabel("Confirmed", isDebug = true))
    }

    @Test
    fun `a blank wire name is a dash even in debug`() {
        assertEquals("—", unknownOrderStatusLabel(null, isDebug = true))
        assertEquals("—", unknownOrderStatusLabel("   ", isDebug = true))
    }
}
