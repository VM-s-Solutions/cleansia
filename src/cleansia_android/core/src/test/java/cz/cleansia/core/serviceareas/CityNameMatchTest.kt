package cz.cleansia.core.serviceareas

import cz.cleansia.core.servicearea.ServicedCity
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test

/**
 * The same table the server's `CityNameMatchTests` pins, case for case, numbered and in the same order.
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
    fun `every case is answered as the server answers it`() {
        val wrong = cases.filter { it.answer() != it.rule }.map { it.number }

        assertEquals("cases answered differently from the server", emptyList<Int>(), wrong)
    }

    @Test
    fun `the table keeps every shared case in its order`() {
        assertEquals((1..119).toList(), cases.map { it.number })
    }

    /** The table cannot see these: Kotlin's `Char.isWhitespace` lacks U+0085 and adds U+001C to U+001F. */
    @Test
    fun `whitespace is the server's set wherever it is read`() {
        assertTrue(CityNameMatch.isServiced(fixture, "Прага", "110\u008500"))
        assertTrue(CityNameMatch.isServiced(fixture, "Praha", "602\u001F00"))
        assertTrue(CityNameMatch.isServiced(listOf(row("Praha", "\u00851")), "Kladno", "110 00"))
        assertFalse(CityNameMatch.matches("Praha", "Praha\u001F-Libeň"))
    }

    /** The server's `\d` is any decimal digit, as ICU's is on a device; the JVM's is ASCII. */
    @Test
    fun `a district number is any decimal digit`() {
        assertTrue(CityNameMatch.matches("Brno", "Brno \u0662"))
    }

    @Test
    fun `whitespace is trimmed before the marks are removed`() {
        assertFalse(CityNameMatch.matches("Praha", "Praha \u0301"))
        assertFalse(CityNameMatch.matches("Praha", "\u0301 Praha"))
    }

    private class Case(val number: Int, val rule: Boolean, val answer: () -> Boolean)

    private fun serviced(
        number: Int,
        city: String?,
        postcode: String?,
        rule: Boolean,
        rows: List<ServicedCity> = fixture,
    ) = Case(number, rule) { CityNameMatch.isServiced(rows, city, postcode) }

    private fun name(number: Int, row: String?, city: String?, rule: Boolean) =
        Case(number, rule) { CityNameMatch.matches(row, city) }

    private fun row(name: String, zipPrefix: String?) =
        ServicedCity(id = "city-$name", countryId = "country-cz", name = name, zipPrefix = zipPrefix)

    private val fixture: List<ServicedCity> =
        (listOf("Praha", "Prague") + (1..22).flatMap { listOf("Praha $it", "Prague $it") }).map { row(it, "1") } +
            listOf(
                "Brno", "Ostrava", "Plzeň", "Liberec", "Olomouc", "České Budějovice", "Hradec Králové",
                "Ústí nad Labem", "Pardubice",
            ).map { row(it, null) }

    private val cases = listOf(
        serviced(1, "Praha", "110 00", true),
        serviced(2, "Прага", "110 00", true),
        serviced(3, "Prague", "15000", true),
        serviced(4, "Praha 4 - Chodov", "149 00", true),
        serviced(5, "PRAHA", "110 00", true),
        serviced(6, "Praha 8", "180\u00A000", true),
        serviced(7, "", "120 00", true),
        serviced(8, "Praha", "252 42", false),
        serviced(9, "Praha", "602 00", false),
        serviced(10, "Praha", null, true),
        serviced(11, "Praha", "1100", true),
        serviced(12, "Praha-západ", "252 00", false),
        serviced(13, "Praha-západ", null, false),
        serviced(14, "Nová Praha", null, false),
        serviced(15, "Jesenice", "252 42", false),
        serviced(16, "Brno-střed", "602 00", true),
        serviced(17, "Brno - Královo Pole", "612 00", true),
        serviced(18, "Brno-venkov", "664 00", false),
        serviced(19, "Ostrava-Poruba", "708 00", true),
        serviced(20, "Pardubice II", "530 02", true),
        serviced(21, "Liberec XIV-Ruprechtice", "460 14", true),
        serviced(22, "Plzeň-jih", null, false),
        serviced(23, "Plzen", "301 00", true),
        serviced(24, "Pilsen", "301 00", false),
        serviced(25, "Ústí nad Labem-Střekov", "400 03", true),
        serviced(26, "Kladno", "272 01", false),
        serviced(27, "Brno", "110 00", true),
        serviced(28, "Praha 8 - Libeň", "180 00", true),
        serviced(29, "Прага", "110\u00A000", true),
        serviced(30, "Praha", "602\u00A000", false),
        serviced(31, "Прага", "1100", false),
        serviced(32, "Praha", "6020", true),
        serviced(33, "Прага", "1abcd", false),
        serviced(34, "Praha", "252\u00A042", false),
        serviced(35, "Praha", "602000", true),
        serviced(36, "Praha", "602 000", true),
        serviced(37, "Praha", "110 000", true),
        serviced(38, "Прага", "110 000", false),
        serviced(39, "Praha", "CZ-110 00", true),
        serviced(40, "Прага", "CZ-110 00", false),
        serviced(41, "Praha", "110-00", true),
        serviced(42, "Прага", "110-00", false),
        serviced(43, "Praha", "", true),
        serviced(44, "Praha", "   ", true),
        serviced(45, "Прага", "", false),
        serviced(46, "Прага", "   ", false),
        serviced(47, "Praha-západ", "", false),
        serviced(48, "Nová Praha", "", false),
        serviced(49, "Plzeň-jih", "", false),
        serviced(50, "Praha", "110 00", false, rows = emptyList()),
        serviced(51, "Kladno", "110 00", true, rows = listOf(row("Praha", " 1 "))),
        serviced(52, "Praha", "110 00", false, rows = listOf(row("Praha", "1 1"))),
        serviced(53, "Praha", "252 42", true, rows = listOf(row("Praha", " "))),
        serviced(54, "Прага", "110 00", false, rows = listOf(row("Praha", ""))),
        serviced(55, "Plzen", "301 00", true, rows = listOf(row("Plzeň", null))),
        name(56, "Plzeň", "Plzen", true),
        name(57, "Plzen", "Plzeň", true),
        name(58, "České Budějovice", "Ceske Budejovice", true),
        name(59, "Ústí nad Labem", "Usti nad Labem", true),
        name(60, "Hradec Králové", "Hradec Kralove", true),
        name(61, "Praha", "Praha 8", true),
        name(62, "Praha", "Praha 22", true),
        name(63, "Prague", "Prague 8", true),
        name(64, "Praha", "Praha 4 - Chodov", true),
        name(65, "Praha", "Praha 5 \u2013 Smíchov", true),
        name(66, "Praha", "Praha 4-Chodov", true),
        name(67, "Praha", "Praha 4\u2011Chodov", true),
        name(68, "Praha", "Praha\u00A08", true),
        name(69, "Praha", "Praha-Kunratice", true),
        name(70, "Praha", "Praha - Smíchov", true),
        name(71, "Brno", "Brno-střed", true),
        name(72, "Brno", "Brno-sever", true),
        name(73, "Brno", "Brno - Královo Pole", true),
        name(74, "Ostrava", "Ostrava-Poruba", true),
        name(75, "Ostrava", "Ostrava-Jih", true),
        name(76, "Ostrava", "Ostrava 3 - Ostrava-Jih", true),
        name(77, "Ústí nad Labem", "Ústí nad Labem-Střekov", true),
        name(78, "Pardubice", "Pardubice II", true),
        name(79, "Liberec", "Liberec XIV-Ruprechtice", true),
        name(80, "Plzeň", "Plzeň 2-Slovany", true),
        name(81, "Praha 8", "Praha 8 - Libeň", true),
        name(82, "Praha", "Praha-", false),
        name(83, "Brno", "Brno -", false),
        name(84, "Praha", "  PRAHA  ", true),
        name(85, "Hradec Králové", "Hradec  Kralove", true),
        name(86, "Ústí nad Labem", "Ústí\u00A0nad\u00A0Labem", true),
        name(87, "Praha", "Praha-západ", false),
        name(88, "Praha", "Praha-východ", false),
        name(89, "Brno", "Brno-venkov", false),
        name(90, "Brno", "Brno - venkov", false),
        name(91, "Plzeň", "Plzeň-sever", false),
        name(92, "Plzeň", "Plzeň-jih", false),
        name(93, "Praha", "Nová Praha", false),
        name(94, "Ústí nad Labem", "Ústí nad Orlicí", false),
        name(95, "Praha", "Kladno", false),
        name(96, "Praha", "Prague 8", false),
        name(97, "Praha", "Prague", false),
        name(98, "Plzeň", "Pilsen", false),
        name(99, "Praha", "Прага", false),
        name(100, "Praha 8", "Praha 22", false),
        name(101, "Praha 8", "Praha", false),
        name(102, "Praha", "8", false),
        name(103, "Praha", "", false),
        name(104, "", "Praha", false),
        name(105, "Praha", " ", false),
        name(106, null, "Praha", false),
        name(107, "Praha", null, false),
        name(108, "Praha", "Praha", true),
        name(109, "Praha", "PRAHA", true),
        name(110, "Praha", "  Praha ", true),
        name(111, "Brno", "Brno", true),
        name(112, "Brno", "BRNO", true),
        name(113, "Brno", "  Brno ", true),
        name(114, "Plzeň", "Plzeň", true),
        name(115, "Plzeň", "PLZEŇ", true),
        name(116, "Plzeň", "  Plzeň ", true),
        name(117, "České Budějovice", "České Budějovice", true),
        name(118, "České Budějovice", "ČESKÉ BUDĚJOVICE", true),
        name(119, "České Budějovice", "  České Budějovice ", true),
    )
}
