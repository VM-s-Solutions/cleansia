package cz.cleansia.core.servicearea

import org.junit.Assert.assertEquals
import org.junit.Test

/**
 * Pins the alpha-3 → alpha-2 normalisation contract the whole service-area
 * seam relies on: backend country codes ("CZE") and Mapbox codes ("cz")
 * must land on the same value or every comparison silently fails.
 */
class IsoCountryCodesTest {

    @Test
    fun `maps backend alpha-3 codes to lowercase alpha-2`() {
        assertEquals("cz", IsoCountryCodes.toAlpha2("CZE"))
        assertEquals("sk", IsoCountryCodes.toAlpha2("SVK"))
        assertEquals("pl", IsoCountryCodes.toAlpha2("POL"))
        assertEquals("ua", IsoCountryCodes.toAlpha2("UKR"))
    }

    @Test
    fun `is case-insensitive and trims whitespace`() {
        assertEquals("cz", IsoCountryCodes.toAlpha2("cze"))
        assertEquals("cz", IsoCountryCodes.toAlpha2(" CZE "))
        assertEquals("sk", IsoCountryCodes.toAlpha2("svk"))
    }

    @Test
    fun `passes alpha-2 input through lowercased`() {
        assertEquals("cz", IsoCountryCodes.toAlpha2("cz"))
        assertEquals("cz", IsoCountryCodes.toAlpha2("CZ"))
        assertEquals("sk", IsoCountryCodes.toAlpha2("sk"))
    }

    @Test
    fun `passes unknown codes through lowercased`() {
        assertEquals("xyz", IsoCountryCodes.toAlpha2("XYZ"))
        assertEquals("q1", IsoCountryCodes.toAlpha2("q1"))
    }

    @Test
    fun `null and blank input normalise to empty`() {
        assertEquals("", IsoCountryCodes.toAlpha2(null))
        assertEquals("", IsoCountryCodes.toAlpha2(""))
        assertEquals("", IsoCountryCodes.toAlpha2("   "))
    }
}
