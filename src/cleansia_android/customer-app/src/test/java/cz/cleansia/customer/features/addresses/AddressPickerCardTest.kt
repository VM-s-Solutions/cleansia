package cz.cleansia.customer.features.addresses

import java.io.File
import org.junit.Assert.assertTrue
import org.junit.Test

/**
 * The map picker's address card keeps one height while the camera moves. Every camera move starts a
 * lookup ("Finding address…", one line) that ends with an address (street over city, two lines); a
 * card that followed its text grew and shrank at the bottom of the map on each drag, carrying the
 * Mapbox ornaments with it. The camera itself never moves for the card: only the ornaments are lifted.
 * There is no Compose harness here, so the card is pinned as source.
 */
class AddressPickerCardTest {

    private val pane: String = sequenceOf(File("."), File("customer-app"), File("src/cleansia_android/customer-app"))
        .map { File(it, "src/main/java/cz/cleansia/customer/features/addresses/AddressManagerScreen.kt") }
        .first { it.isFile }
        .readText()
        .substringAfter("private fun AddOnMapPane(")
        .substringBefore("private fun ReviewPane(")

    @Test
    fun `the address block reserves the resolved address's two lines in every state`() {
        val flat = pane.replace(Regex("\\s+"), " ")
        assertTrue(
            "the invisible two-line template is gone",
            flat.contains(
                "Column(modifier = Modifier.alpha(0f).clearAndSetSemantics {}) { " +
                    "Text(\" \", style = MaterialTheme.typography.titleSmall.copy(fontWeight = FontWeight.SemiBold), maxLines = 1) " +
                    "Text(\" \", style = MaterialTheme.typography.bodySmall, maxLines = 1) }",
            ),
        )
    }

    @Test
    fun `the card's height lifts only the ornaments, never the camera`() {
        assertTrue("the logo no longer clears the card", pane.contains("Logo(contentPadding = PaddingValues(start = 4.dp, bottom = cardCoverHeight + 4.dp))"))
        assertTrue("the card's height reaches the camera", !Regex("cardCoverHeight[^\\n]*(padding\\(|center\\(|setCameraOptions)").containsMatchIn(pane.substringAfter("MapboxMap(").substringBefore("logo = {")))
    }
}
