package cz.cleansia.customer.features.addresses

import java.io.File
import org.junit.Assert.assertEquals
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
            "the two-line reservation is gone",
            flat.contains(
                "MaterialTheme.typography.titleSmall.lineHeight.toDp() + MaterialTheme.typography.bodySmall.lineHeight.toDp()",
            ),
        )
        assertTrue("the address block no longer holds that height", flat.contains(".heightIn(min = addressLines)"))
    }

    /**
     * The reservation holds only while no line wraps: the uk and ru "move the map" hints are wider than
     * a 360dp phone's address block, and a two-line hint grew the card by 12dp whenever a lookup found
     * nothing. Every line keeps to one, the hints ending in an ellipsis, as the iOS twin's lineLimit(1).
     */
    @Test
    fun `every line of the address block keeps to one in every state`() {
        val block = pane.substringAfter("val addr = resolved").substringBefore("if (lookingUp) {")
        val texts = Regex("\\bText\\(").findAll(block).count()
        assertTrue("the address block's states are no longer where this looks", texts >= 3)
        assertEquals("a line of the address block can wrap past its reservation", texts, Regex("maxLines = 1\\b").findAll(block).count())
        listOf("address_picker_looking_up", "address_picker_move_pin").forEach { hint ->
            val text = block.substringAfter("R.string.$hint").substringBefore(")\n")
            assertTrue("the $hint hint no longer ends in an ellipsis", text.contains("overflow = TextOverflow.Ellipsis"))
        }
    }

    @Test
    fun `the card's height lifts only the ornaments, never the camera`() {
        assertTrue("the logo no longer clears the card", pane.contains("Logo(contentPadding = PaddingValues(start = 4.dp, bottom = cardCoverHeight + 4.dp))"))
        assertTrue("the card's height reaches the camera", !Regex("cardCoverHeight[^\\n]*(padding\\(|center\\(|setCameraOptions)").containsMatchIn(pane.substringAfter("MapboxMap(").substringBefore("logo = {")))
    }
}
