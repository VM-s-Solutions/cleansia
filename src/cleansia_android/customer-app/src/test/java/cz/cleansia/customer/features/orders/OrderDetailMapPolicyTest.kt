package cz.cleansia.customer.features.orders

import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test

/**
 * When the order detail backs its sheet with a real map. Every false branch
 * here is a case where the map would otherwise render somewhere misleading —
 * the Gulf of Guinea for a failed geocode, an address for a visit that never
 * happened.
 */
class OrderDetailMapPolicyTest {

    private val prague = 50.0779 to 14.4680

    @Test
    fun `a geocoded live order shows the map`() {
        listOf(
            OrderStatus.New,
            OrderStatus.Pending,
            OrderStatus.Confirmed,
            OrderStatus.OnTheWay,
            OrderStatus.InProgress,
        ).forEach { status ->
            assertTrue(status.name, canShowOrderMap(prague.first, prague.second, status))
        }
    }

    @Test
    fun `completed keeps the map because where it happened is still context`() {
        assertTrue(canShowOrderMap(prague.first, prague.second, OrderStatus.Completed))
    }

    @Test
    fun `cancelled hides the map because the visit never happened`() {
        assertFalse(canShowOrderMap(prague.first, prague.second, OrderStatus.Cancelled))
    }

    @Test
    fun `an unknown status still shows a geocoded address`() {
        assertTrue(canShowOrderMap(prague.first, prague.second, null))
    }

    @Test
    fun `an order booked before geocoding existed has no map`() {
        assertFalse(canShowOrderMap(null, null, OrderStatus.Confirmed))
        assertFalse(canShowOrderMap(prague.first, null, OrderStatus.Confirmed))
        assertFalse(canShowOrderMap(null, prague.second, OrderStatus.Confirmed))
    }

    @Test
    fun `null island is a failed geocode, not an address`() {
        assertFalse(canShowOrderMap(0.0, 0.0, OrderStatus.Confirmed))
    }

    @Test
    fun `a single zero coordinate is still a real place`() {
        assertTrue(canShowOrderMap(0.0, 14.4680, OrderStatus.Confirmed))
        assertTrue(canShowOrderMap(50.0779, 0.0, OrderStatus.Confirmed))
    }

    @Test
    fun `out of range coordinates are rejected rather than clamped`() {
        assertFalse(canShowOrderMap(91.0, 14.0, OrderStatus.Confirmed))
        assertFalse(canShowOrderMap(-91.0, 14.0, OrderStatus.Confirmed))
        assertFalse(canShowOrderMap(50.0, 181.0, OrderStatus.Confirmed))
        assertFalse(canShowOrderMap(50.0, -181.0, OrderStatus.Confirmed))
    }
}
