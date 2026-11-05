package cz.cleansia.partner.features.orders

import cz.cleansia.core.location.GeocodedAddress
import cz.cleansia.core.location.ReverseGeocodingService
import cz.cleansia.partner.api.model.OrderListItem
import io.mockk.coEvery
import io.mockk.coVerify
import io.mockk.mockk
import kotlinx.coroutines.test.runTest
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Test

/** What the detail's band and the taken panel's strip draw: the area before the take, the door after it. */
class JobMapRulesTest {

    private val prague = MapPoint(50.0755, 14.4378)

    @Test
    fun `before the take the band is the locked area map, even if coordinates arrived`() {
        assertEquals(JobMapMode.Area(locked = true), jobMapMode(taken = false, latitude = 50.1, longitude = 14.4))
        assertEquals(JobMapMode.Area(locked = true), jobMapMode(taken = false, latitude = null, longitude = null))
    }

    @Test
    fun `a taken job pins its address`() {
        assertEquals(JobMapMode.Pin(MapPoint(50.1, 14.4)), jobMapMode(taken = true, latitude = 50.1, longitude = 14.4))
    }

    @Test
    fun `a taken job with no usable coordinates shows its area without the lock`() {
        assertEquals(JobMapMode.Area(locked = false), jobMapMode(taken = true, latitude = null, longitude = 14.4))
        assertEquals(JobMapMode.Area(locked = false), jobMapMode(taken = true, latitude = 0.0, longitude = 0.0))
    }

    @Test
    fun `only a taken job's pinned door pans and zooms`() {
        assertTrue(jobMapMode(taken = true, latitude = 50.1, longitude = 14.4).pans)
        assertFalse(jobMapMode(taken = false, latitude = 50.1, longitude = 14.4).pans)
        assertFalse(jobMapMode(taken = true, latitude = null, longitude = null).pans)
    }

    /** At zoom 15 one dp is about 0.0000215 degrees of longitude, and about 0.0000138 of latitude at Prague. */
    @Test
    fun `the map is off the job once the pin is more than 8 dp from its framing`() {
        assertFalse(movedOffJob(prague, 15.0, prague, 15.0))
        assertFalse(movedOffJob(prague, 15.0, prague.copy(longitude = prague.longitude + 0.0001), 15.0))
        assertFalse(movedOffJob(prague, 15.0, prague.copy(latitude = prague.latitude - 0.00005), 15.0))
        assertTrue(movedOffJob(prague, 15.0, prague.copy(longitude = prague.longitude + 0.0003), 15.0))
        assertTrue(movedOffJob(prague, 15.0, prague.copy(latitude = prague.latitude - 0.0002), 15.0))
    }

    @Test
    fun `the map is off the job once its scale is more than 5 percent from the framing`() {
        assertFalse(movedOffJob(prague, 15.0, prague, 15.05))
        assertFalse(movedOffJob(prague, 15.0, prague, 14.95))
        assertTrue(movedOffJob(prague, 15.0, prague, 15.1))
        assertTrue(movedOffJob(prague, 15.0, prague, 14.9))
    }

    @Test
    fun `the taken panel draws its strip only from the taken row's coordinates`() {
        assertNull(takenStripPoint(null))
        assertNull(takenStripPoint(OrderListItem(customerAddressLatitude = 50.1, customerAddressLongitude = null)))
        assertEquals(MapPoint(50.1, 14.4), takenStripPoint(OrderListItem(customerAddressLatitude = 50.1, customerAddressLongitude = 14.4)))
    }

    @Test
    fun `the area is geocoded with its market's country`() = runTest {
        val geocoding = mockk<ReverseGeocodingService>()
        coEvery { geocoding.forwardGeocode("Praha 8", listOf("cz")) } returns listOf(place(50.11, 14.46))

        assertEquals(MapPoint(50.11, 14.46), JobAreaLocator(geocoding).centre("Praha 8", "CZK"))
        coVerify(exactly = 1) { geocoding.forwardGeocode("Praha 8", listOf("cz")) }
    }

    @Test
    fun `an area the geocoder cannot place, or no area, centres on the market's main city`() = runTest {
        val geocoding = mockk<ReverseGeocodingService>()
        coEvery { geocoding.forwardGeocode(any(), any()) } returns emptyList()

        assertEquals(prague, JobAreaLocator(geocoding).centre("Nowhere", "CZK"))
        assertEquals(prague, JobAreaLocator(geocoding).centre(null, null))
        coVerify(exactly = 1) { geocoding.forwardGeocode(any(), any()) }
    }

    private fun place(latitude: Double, longitude: Double) =
        GeocodedAddress(latitude, longitude, street = "", city = "Praha", zipCode = "", country = "Czechia", countryIsoCode = "cz", formatted = "Praha")
}
