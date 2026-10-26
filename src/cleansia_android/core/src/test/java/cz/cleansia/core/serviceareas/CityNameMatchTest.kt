package cz.cleansia.core.serviceareas

import cz.cleansia.core.servicearea.ServicedCity
import org.junit.Assert.assertEquals
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
 * `src/Cleansia.Tests/Domain/ServiceAreas/CityNameMatchTests.cs`, in the iOS twin and in the customer
 * web's `city-name-match.spec.ts`.
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
        assertTrue(CityNameMatch.matches("Praha", "Praha\u00A08"))
        assertTrue(CityNameMatch.matches("Praha", "Praha 4\u2011Chodov"))
        assertTrue(CityNameMatch.matches("Ostrava", "Ostrava 3 - Ostrava-Jih"))
    }

    @Test
    fun `a city part written after a dash is the city`() {
        assertTrue(CityNameMatch.matches("Brno", "Brno-střed"))
        assertTrue(CityNameMatch.matches("Brno", "Brno-sever"))
        assertTrue(CityNameMatch.matches("Brno", "Brno - Královo Pole"))
        assertTrue(CityNameMatch.matches("Praha", "Praha-Kunratice"))
        assertTrue(CityNameMatch.matches("Praha", "Praha - Smíchov"))
        assertTrue(CityNameMatch.matches("Ostrava", "Ostrava-Poruba"))
        assertTrue(CityNameMatch.matches("Ostrava", "Ostrava-Jih"))
        assertTrue(CityNameMatch.matches("Ústí nad Labem", "Ústí nad Labem-Střekov"))
    }

    @Test
    fun `a district in Roman numerals is served by its city`() {
        assertTrue(CityNameMatch.matches("Pardubice", "Pardubice II"))
        assertTrue(CityNameMatch.matches("Liberec", "Liberec XIV-Ruprechtice"))
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
        assertFalse(CityNameMatch.matches("Brno", "Brno - venkov"))
        assertFalse(CityNameMatch.matches("Plzeň", "Plzeň-sever"))
        assertFalse(CityNameMatch.matches("Plzeň", "Plzeň-jih"))
    }

    @Test
    fun `a different city is refused`() {
        assertFalse(CityNameMatch.matches("Praha", "Nová Praha"))
        assertFalse(CityNameMatch.matches("Ústí nad Labem", "Ústí nad Orlicí"))
        assertFalse(CityNameMatch.matches("Praha", "Kladno"))
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
        assertTrue(CityNameMatch.matches("Praha 8", "Praha 8 - Libeň"))
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
    fun `everything the exact compare accepted still matches`() {
        listOf("Praha", "Brno", "Plzeň", "České Budějovice").forEach { name ->
            assertTrue(name, CityNameMatch.matches(name, name))
            assertTrue(name, CityNameMatch.matches(name, name.uppercase()))
            assertTrue(name, CityNameMatch.matches(name, "  $name "))
        }
    }

    /**
     * The shared case table, against the seeded rows: every Prague row carries ZIP prefix 1, and a valid
     * postcode decides a prefixed row by itself — a Prague postcode is Prague whatever the city is
     * called, and a valid postcode outside Prague is not Prague even when the name says so. Only an
     * address with no valid postcode falls back to the name, so nothing that booked before is refused for
     * a missing one.
     */
    @Test
    fun `the shared case table`() {
        val wrong = sharedCases.filter { (_, city, postcode, serviced) ->
            postcodesOf(postcode).any { CityNameMatch.isServiced(seededRows, city, it) != serviced }
        }

        assertEquals("cases answered differently from the server", emptyList<SharedCase>(), wrong)
    }

    /** Shared case 6 cannot see this: its name answers yes whether or not the postcode survives the space. */
    @Test
    fun `a postcode with a no-break space in it is still a postcode`() {
        assertFalse(CityNameMatch.isServiced(seededRows, "Praha", "252\u00A042"))
        assertTrue(CityNameMatch.isServiced(seededRows, "Прага", "110\u00A000"))
    }

    @Test
    fun `an empty list serves nowhere`() {
        assertFalse(CityNameMatch.isServiced(emptyList(), "Praha", "110 00"))
    }

    private data class SharedCase(val number: Int, val city: String?, val postcode: String?, val serviced: Boolean)

    /** A missing postcode reaches the matcher as null or blank, and either is "none". */
    private fun postcodesOf(postcode: String?) = if (postcode == null) listOf(null, "") else listOf(postcode)

    private val seededRows: List<ServicedCity> =
        (listOf("Praha", "Prague") + (1..22).flatMap { listOf("Praha $it", "Prague $it") }).map { row(it, "1") } +
            listOf(
                "Brno", "Ostrava", "Plzeň", "Liberec", "Olomouc", "České Budějovice", "Hradec Králové",
                "Ústí nad Labem", "Pardubice",
            ).map { row(it, null) }

    private fun row(name: String, zipPrefix: String?) =
        ServicedCity(id = "city-$name", countryId = "country-cz", name = name, zipPrefix = zipPrefix)

    private val sharedCases = listOf(
        SharedCase(1, "Praha", "110 00", serviced = true),
        SharedCase(2, "Прага", "110 00", serviced = true),
        SharedCase(3, "Prague", "15000", serviced = true),
        SharedCase(4, "Praha 4 - Chodov", "149 00", serviced = true),
        SharedCase(5, "PRAHA", "110 00", serviced = true),
        SharedCase(6, "Praha 8", "180\u00A000", serviced = true),
        SharedCase(7, "", "120 00", serviced = true),
        SharedCase(8, "Praha", "252 42", serviced = false),
        SharedCase(9, "Praha", "602 00", serviced = false),
        SharedCase(10, "Praha", null, serviced = true),
        SharedCase(11, "Praha", "1100", serviced = true),
        SharedCase(12, "Praha-západ", "252 00", serviced = false),
        SharedCase(13, "Praha-západ", null, serviced = false),
        SharedCase(14, "Nová Praha", null, serviced = false),
        SharedCase(15, "Jesenice", "252 42", serviced = false),
        SharedCase(16, "Brno-střed", "602 00", serviced = true),
        SharedCase(17, "Brno - Královo Pole", "612 00", serviced = true),
        SharedCase(18, "Brno-venkov", "664 00", serviced = false),
        SharedCase(19, "Ostrava-Poruba", "708 00", serviced = true),
        SharedCase(20, "Pardubice II", "530 02", serviced = true),
        SharedCase(21, "Liberec XIV-Ruprechtice", "460 14", serviced = true),
        SharedCase(22, "Plzeň-jih", null, serviced = false),
        SharedCase(23, "Plzen", "301 00", serviced = true),
        SharedCase(24, "Pilsen", "301 00", serviced = false),
        SharedCase(25, "Ústí nad Labem-Střekov", "400 03", serviced = true),
        SharedCase(26, "Kladno", "272 01", serviced = false),
        SharedCase(27, "Brno", "110 00", serviced = true),
        SharedCase(28, "Praha 8 - Libeň", "180 00", serviced = true),
    )
}
