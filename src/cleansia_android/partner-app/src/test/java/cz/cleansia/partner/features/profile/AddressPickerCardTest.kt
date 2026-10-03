package cz.cleansia.partner.features.profile

import java.io.File
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Test

/**
 * The partner map picker's confirm card keeps one height while the camera moves, as the customer
 * picker's does. Every camera move starts a lookup ("Looking up…", one line) that ends with an address
 * (street over its city line, two lines); a card that followed its text grew and shrank at the bottom
 * of the map on each drag, carrying the Mapbox ornaments lifted above it. The camera itself never moves
 * for the card: only the ornaments are lifted. There is no Compose harness here, so the card is pinned
 * as source.
 */
class AddressPickerCardTest {

    private val screen: String = sequenceOf(File("."), File("partner-app"), File("src/cleansia_android/partner-app"))
        .map { File(it, "src/main/java/cz/cleansia/partner/features/profile/AddressPickerScreen.kt") }
        .first { it.isFile }
        .readText()

    private val card: String = screen
        .substringAfter("private fun ConfirmCard(")
        .substringBefore("private fun FloatingCircleButton(")

    @Test
    fun `the address block reserves the resolved address's two lines in every state`() {
        val flat = card.replace(Regex("\\s+"), " ")
        assertTrue(
            "the two-line reservation is gone",
            flat.contains(
                "MaterialTheme.typography.titleSmall.lineHeight.toDp() + MaterialTheme.typography.bodySmall.lineHeight.toDp()",
            ),
        )
        assertTrue("the address block no longer holds that height", flat.contains(".heightIn(min = addressLines)"))
    }

    /**
     * The reservation holds only while no line wraps. The first line carries both hints, so it ends in an
     * ellipsis rather than taking a second line in a long locale or at a large font scale.
     */
    @Test
    fun `every line of the address block keeps to one in every state`() {
        val block = card.substringAfter("val addressLines").substringBefore("if (lookingUp) {")
        val texts = Regex("\\bText\\(").findAll(block).count()
        assertTrue("the address block's lines are no longer where this looks", texts >= 2)
        assertEquals("a line of the address block can wrap past its reservation", texts, Regex("maxLines = 1\\b").findAll(block).count())
        val hints = block.substringAfter("R.string.address_picker_drag_to_pick").substringBefore("if (resolved != null")
        assertTrue("the hint line no longer ends in an ellipsis", hints.contains("overflow = TextOverflow.Ellipsis"))
    }

    @Test
    fun `the card's height lifts only the ornaments, never the camera`() {
        assertTrue("the logo no longer clears the card", screen.contains("Logo(contentPadding = PaddingValues(start = 4.dp, bottom = cardCoverHeight + 4.dp))"))
        assertTrue(
            "the attribution no longer clears the card",
            screen.contains("Attribution(contentPadding = PaddingValues(start = 92.dp, bottom = cardCoverHeight + 4.dp))"),
        )
        val map = screen.substringAfter("MapboxMap(").substringBefore("logo = {")
        assertTrue("the card's height reaches the camera", !Regex("cardCoverHeight[^\\n]*(padding\\(|center\\(|setCameraOptions)").containsMatchIn(map))
    }
}
