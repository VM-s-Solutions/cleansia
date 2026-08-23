package cz.cleansia.core.serviceareas

import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test

/**
 * The same table the server's `CityNameMatchTests` pins, case for case.
 *
 * **That duplication is the point.** This is a port of a server rule, and the failure mode of a port is
 * silent divergence — specifically a client that is STRICTER than the server, which tells a customer we
 * do not serve a city we do serve. Two suites asserting one table means a divergence reddens something.
 *
 * If you change a case here, change it in
 * `src/Cleansia.Tests/Domain/ServiceAreas/CityNameMatchTests.cs` and in the iOS twin.
 */
class CityNameMatchTest {

    @Test
    fun `a spelling without diacritics is the same city`() {
        assertTrue(CityNameMatch.matches("Plzeň", "Plzen"))
        assertTrue(CityNameMatch.matches("Plzen", "Plzeň"))
        assertTrue(CityNameMatch.matches("České Budějovice", "Ceske Budejovice"))
        assertTrue(CityNameMatch.matches("Ústí nad Labem", "Usti nad Labem"))
        assertTrue(CityNameMatch.matches("Hradec Králové", "Hradec Kralove"))
    }

    @Test
    fun `a district is served by its city`() {
        assertTrue(CityNameMatch.matches("Praha", "Praha 8"))
        assertTrue(CityNameMatch.matches("Praha", "Praha 22"))
        assertTrue(CityNameMatch.matches("Prague", "Prague 8"))
        assertTrue(CityNameMatch.matches("Praha", "Praha 4 - Chodov"))
        assertTrue(CityNameMatch.matches("Praha", "Praha 5 – Smíchov"))
        assertTrue(CityNameMatch.matches("Praha", "Praha 4-Chodov"))
    }

    @Test
    fun `case and spacing are not a difference`() {
        assertTrue(CityNameMatch.matches("Praha", "  PRAHA  "))
        assertTrue(CityNameMatch.matches("Hradec Králové", "Hradec  Kralove"))
    }

    /** An okres is the rural ring AROUND a city, not part of it — same syntax, opposite answer. */
    @Test
    fun `the okres around a city is not the city`() {
        assertFalse(CityNameMatch.matches("Praha", "Praha-západ"))
        assertFalse(CityNameMatch.matches("Praha", "Praha-východ"))
        assertFalse(CityNameMatch.matches("Brno", "Brno-venkov"))
    }

    @Test
    fun `a different city is refused`() {
        assertFalse(CityNameMatch.matches("Praha", "Nová Praha"))
        assertFalse(CityNameMatch.matches("Ústí nad Labem", "Ústí nad Orlicí"))
        assertFalse(CityNameMatch.matches("Praha", "Kladno"))
        assertFalse(CityNameMatch.matches("Brno", "Brno-střed"))
    }

    /** Exonyms are DATA — a row, never an algorithm. */
    @Test
    fun `an exonym matches nothing without its own row`() {
        assertFalse(CityNameMatch.matches("Praha", "Prague 8"))
        assertFalse(CityNameMatch.matches("Praha", "Prague"))
        assertFalse(CityNameMatch.matches("Plzeň", "Pilsen"))
        assertFalse(CityNameMatch.matches("Praha", "Прага"))
    }

    /** The district strip runs on the CUSTOMER's string only. */
    @Test
    fun `a row naming one district does not claim the city`() {
        assertFalse(CityNameMatch.matches("Praha 8", "Praha 22"))
        assertFalse(CityNameMatch.matches("Praha 8", "Praha"))
    }

    @Test
    fun `nothing matches nothing`() {
        assertFalse(CityNameMatch.matches("Praha", "8"))
        assertFalse(CityNameMatch.matches("Praha", ""))
        assertFalse(CityNameMatch.matches("", "Praha"))
        assertFalse(CityNameMatch.matches("Praha", " "))
        assertFalse(CityNameMatch.matches(null, "Praha"))
        assertFalse(CityNameMatch.matches("Praha", null))
    }

    @Test
    fun `isServiced answers over the whole list`() {
        val serviced = listOf("Praha", "Brno", "Plzeň")
        assertTrue(CityNameMatch.isServiced(serviced, "Praha 4 - Chodov"))
        assertTrue(CityNameMatch.isServiced(serviced, "Plzen"))
        assertFalse(CityNameMatch.isServiced(serviced, "Kladno"))

        // An empty list is "we know of nowhere", which must not read as "everywhere".
        assertFalse(CityNameMatch.isServiced(emptyList(), "Praha"))
    }
}
