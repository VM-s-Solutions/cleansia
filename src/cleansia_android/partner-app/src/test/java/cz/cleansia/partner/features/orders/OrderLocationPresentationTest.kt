package cz.cleansia.partner.features.orders

import cz.cleansia.partner.api.model.OrderAddress
import cz.cleansia.partner.api.model.OrderItem
import cz.cleansia.partner.api.model.OrderStatus
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Test

/**
 * The server redacts `Address` for a caller the order does not belong to and
 * sends `CustomerAddressApproximate` to everyone. The detail screen read only
 * the first, so a cleaner who saw "Praha · 120" on the board they tapped got no
 * location at all on the screen where they decide whether the travel is worth
 * it — and the same null hid Navigate.
 *
 * The predicate is the *arrival* of the street address, never a client-side
 * re-derivation of who is entitled to it: an employee who booked a cleaning for
 * their own home reaches this screen as the order's customer, with a full
 * address and `isAssignedToCurrentUser` false.
 */
class OrderLocationPresentationTest {

    private val precise = OrderAddress(
        street = "Korunní 810/104",
        city = "Praha",
        zipCode = "12000",
        latitude = 50.0755,
        longitude = 14.4378,
    )

    private val entitled = OrderItem(
        address = precise,
        customerAddressApproximate = "Praha · 120",
    )

    private val browsing = OrderItem(
        address = null,
        customerAddressApproximate = "Praha · 120",
    )

    @Test
    fun `a browsing cleaner gets the zone the server sent`() {
        assertEquals(OrderLocation.Approximate("Praha · 120"), browsing.orderLocation())
        assertEquals("Praha · 120", browsing.orderLocation().line)
    }

    @Test
    fun `a browsing cleaner gets no map and no Navigate`() {
        val location = browsing.orderLocation()
        assertNull(location.navigationTarget())
        assertNull(location.mapPoint())
    }

    @Test
    fun `an entitled reader keeps the street address, the map and Navigate`() {
        val location = entitled.orderLocation()
        assertEquals(OrderLocation.Precise("Korunní 810/104, Praha, 12000", 50.0755, 14.4378), location)
        assertEquals(location, location.navigationTarget())
        assertEquals(50.0755 to 14.4378, location.mapPoint())
    }

    /**
     * Owner ruling 2026-08-27, reversing 2026-08-24: a cancelled job still had a place, and
     * withholding the map read as a map that had failed to load rather than as a decision.
     */
    @Test
    fun `a cancelled order keeps its map for an entitled reader`() {
        val location = entitled.orderLocation()
        assertEquals(50.0755 to 14.4378, location.mapPoint())
        assertEquals(location, location.navigationTarget())
    }

    /** Orders that predate the geocoding backfill navigate by free-text query. */
    @Test
    fun `an entitled address without coordinates still navigates but shows no map`() {
        val location = OrderItem(address = precise.copy(latitude = null, longitude = null)).orderLocation()
        assertEquals("Korunní 810/104, Praha, 12000", location.line)
        assertEquals(location, location.navigationTarget())
        assertNull(location.mapPoint())
    }

    /**
     * `BuildApproximateAddress` returns an empty string, not null, for an order
     * with no city — so an absent zone arrives as `""` and must render nothing
     * rather than an empty location row.
     */
    @Test
    fun `an empty zone is not a location`() {
        val location = OrderItem(address = null, customerAddressApproximate = "").orderLocation()
        assertEquals(OrderLocation.None, location)
        assertNull(location.line)
        assertNull(location.navigationTarget())
        assertNull(location.mapPoint())
    }

    @Test
    fun `an address record with no usable text falls back to the zone`() {
        val location = OrderItem(
            address = OrderAddress(latitude = 50.0755, longitude = 14.4378),
            customerAddressApproximate = "Praha · 120",
        ).orderLocation()
        assertEquals(OrderLocation.Approximate("Praha · 120"), location)
        assertNull(location.navigationTarget())
        assertNull(location.mapPoint())
    }

    @Test
    fun `no address and no zone renders nothing`() {
        val location = OrderItem().orderLocation()
        assertEquals(OrderLocation.None, location)
        assertNull(location.line)
    }
}
