package cz.cleansia.customer.features.orders

import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test

/**
 * When the order detail backs its sheet with a real map. Every false branch here is a case where
 * the map would otherwise render somewhere misleading — the Gulf of Guinea for a failed geocode,
 * or a pin for an order that carries no coordinates at all.
 *
 * Status is deliberately not an input. It was until 2026-08-27, when the owner overruled the
 * Cancelled exception: the job still had a place, and the placeholder read as a broken map.
 */
class OrderDetailMapPolicyTest {

    private val prague = 50.0779 to 14.4680

    /**
     * The whole enum, Cancelled included. This replaces four separate status tests, one of which
     * asserted that Cancelled hid the map; keeping the loop makes a future re-introduction of a
     * status rule fail here rather than pass silently.
     */
    @Test
    fun `a geocoded order shows the map in every status`() {
        OrderStatus.values().forEach { status ->
            assertTrue(status.name, canShowOrderMap(prague.first, prague.second))
        }
    }

    @Test
    fun `an order booked before geocoding existed has no map`() {
        assertFalse(canShowOrderMap(null, null))
        assertFalse(canShowOrderMap(prague.first, null))
        assertFalse(canShowOrderMap(null, prague.second))
    }

    @Test
    fun `null island is a failed geocode, not an address`() {
        assertFalse(canShowOrderMap(0.0, 0.0))
    }

    @Test
    fun `a single zero coordinate is still a real place`() {
        assertTrue(canShowOrderMap(0.0, 14.4680))
        assertTrue(canShowOrderMap(50.0779, 0.0))
    }

    @Test
    fun `out of range coordinates are rejected rather than clamped`() {
        assertFalse(canShowOrderMap(91.0, 14.0))
        assertFalse(canShowOrderMap(-91.0, 14.0))
        assertFalse(canShowOrderMap(50.0, 181.0))
        assertFalse(canShowOrderMap(50.0, -181.0))
    }
}
