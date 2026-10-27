package cz.cleansia.customer.features.addresses

import java.io.File
import org.junit.Assert.assertEquals
import org.junit.Test

/**
 * The address manager asks whether we serve an address with that address's own postcode. A Prague row
 * answers a valid postcode by itself, so a lookup without one flags "Прага" or "Prag" at 110 00, which the
 * server books. The postcode parameter has no default, but an explicit null still compiles, and the
 * matcher's and the provider's own tests cannot see the screen. There is no Compose harness here, so the
 * two lookups are pinned as source.
 */
class AddressServiceabilityCallSiteTest {

    private val screen: String = sequenceOf(File("."), File("customer-app"), File("src/cleansia_android/customer-app"))
        .map { File(it, "src/main/java/cz/cleansia/customer/features/addresses/AddressManagerScreen.kt") }
        .first { it.isFile }
        .readText()
        .replace("\r\n", "\n")

    @Test
    fun `the saved-address list asks with each address's postcode`() {
        val pane = screen.substringAfter("private fun ListPane(").substringBefore("private fun SavedAddressRow(")

        assertEquals(listOf("CityNameMatch.isServiced(cities, it.city, it.zipCode)"), lookups(pane))
    }

    @Test
    fun `the review pane asks with the picked address's postcode, and asks again when only the postcode changes`() {
        val pane = screen.substringAfter("private fun ReviewPane(").substringBefore("private fun SearchField(")

        assertEquals(listOf("serviceArea.isCityServiced(match.id, picked.city, picked.zipCode)"), lookups(pane))
        assertEquals(
            "the answer and the lookup are no longer keyed by the postcode",
            listOf("remember", "remember", "LaunchedEffect"),
            Regex("""\b(remember|LaunchedEffect)\(picked\.countryIsoCode, picked\.city, picked\.zipCode\)""")
                .findAll(pane).map { it.groupValues[1] }.toList(),
        )
    }

    @Test
    fun `the screen asks nowhere else`() {
        assertEquals(2, lookups(screen).size)
    }

    private fun lookups(text: String): List<String> =
        Regex("""\b(?:CityNameMatch\.isServiced|(?:\w+\.)?isCityServiced)\([^)]*\)""").findAll(text).map { it.value }.toList()
}
