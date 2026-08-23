package cz.cleansia.core.servicearea

import kotlinx.coroutines.test.runTest
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Test

/**
 * Pins the provider's caching contract: only SUCCESSFUL fetches are cached,
 * a failed fetch (null) means UNKNOWN and must retry on the next access —
 * caching the failure used to pin "serves nothing" until force-stop.
 */
class ServiceAreaProviderTest {

    private class FakeDataSource : ServiceAreaDataSource {
        val countryResults = ArrayDeque<List<ServicedCountry>?>()
        val cityResults = ArrayDeque<List<ServicedCity>?>()
        var countryFetches = 0
        var cityFetches = 0

        override suspend fun fetchServicedCountries(): List<ServicedCountry>? {
            countryFetches++
            return countryResults.removeFirstOrNull()
        }

        override suspend fun fetchServiceCities(countryId: String?): List<ServicedCity>? {
            cityFetches++
            return cityResults.removeFirstOrNull()
        }
    }

    private val czechia = ServicedCountry(id = "country-cz", isoCode = "cz", name = "Czech Republic")
    private val prague = ServicedCity(id = "city-prg", countryId = "country-cz", name = "Prague")

    @Test
    fun `failed country fetch returns null, is not cached, and the next access retries`() = runTest {
        val dataSource = FakeDataSource()
        dataSource.countryResults.addLast(null)
        dataSource.countryResults.addLast(listOf(czechia))
        val provider = ServiceAreaProvider(dataSource)

        assertNull(provider.loadCountries())
        assertEquals(listOf(czechia), provider.loadCountries())
        assertEquals(2, dataSource.countryFetches)
    }

    @Test
    fun `successful country fetch is cached across accesses`() = runTest {
        val dataSource = FakeDataSource()
        dataSource.countryResults.addLast(listOf(czechia))
        val provider = ServiceAreaProvider(dataSource)

        assertEquals(listOf(czechia), provider.loadCountries())
        assertEquals(listOf(czechia), provider.loadCountries())
        assertEquals(1, dataSource.countryFetches)
    }

    @Test
    fun `isCityServiced returns null when the city list cannot be loaded, then retries`() = runTest {
        val dataSource = FakeDataSource()
        dataSource.cityResults.addLast(null)
        dataSource.cityResults.addLast(listOf(prague))
        val provider = ServiceAreaProvider(dataSource)

        assertNull(provider.isCityServiced("country-cz", "Prague"))
        assertEquals(true, provider.isCityServiced("country-cz", "Prague"))
        assertEquals(2, dataSource.cityFetches)
    }

    /**
     * The warning must never be STRICTER than the server. It tells a customer "we do not operate here"
     * before they book, so a false alarm turns away someone the platform would have served — and the
     * geocoder returning "Plzen" for a serviced "Plzeň", or "Praha 8" for "Praha", is the ordinary case
     * rather than the exotic one. Same rule as `CreateOrder`, via the same shared matcher.
     */
    @Test
    fun `isCityServiced accepts everything the server would accept`() = runTest {
        val dataSource = FakeDataSource()
        dataSource.cityResults.addLast(listOf(prague.copy(name = "Praha"), prague.copy(name = "Plzeň")))
        val provider = ServiceAreaProvider(dataSource)

        assertEquals(true, provider.isCityServiced("country-cz", "Praha 8"))
        assertEquals(true, provider.isCityServiced("country-cz", "Praha 4 - Chodov"))
        assertEquals(true, provider.isCityServiced("country-cz", "Plzen"))

        // Still refuses what the server refuses — an okres is the ring AROUND a city, not the city.
        assertEquals(false, provider.isCityServiced("country-cz", "Praha-západ"))
        assertEquals(false, provider.isCityServiced("country-cz", "Kladno"))
    }

    @Test
    fun `isCityServiced answers per country with case and whitespace tolerance`() = runTest {
        val dataSource = FakeDataSource()
        dataSource.cityResults.addLast(listOf(prague))
        val provider = ServiceAreaProvider(dataSource)

        assertEquals(true, provider.isCityServiced("country-cz", "  prague "))
        assertEquals(false, provider.isCityServiced("country-cz", "Brno"))
        assertEquals(false, provider.isCityServiced("country-sk", "Prague"))
        assertEquals(1, dataSource.cityFetches)
    }

    @Test
    fun `refresh clears the cache so the next access fetches again`() = runTest {
        val dataSource = FakeDataSource()
        dataSource.countryResults.addLast(listOf(czechia))
        dataSource.countryResults.addLast(listOf(czechia))
        val provider = ServiceAreaProvider(dataSource)

        provider.loadCountries()
        provider.refresh()
        provider.loadCountries()
        assertEquals(2, dataSource.countryFetches)
    }
}
