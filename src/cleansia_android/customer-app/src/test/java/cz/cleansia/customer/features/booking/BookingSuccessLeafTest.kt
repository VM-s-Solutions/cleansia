package cz.cleansia.customer.features.booking

import androidx.compose.ui.unit.dp
import org.junit.Assert.assertEquals
import org.junit.Test

class BookingSuccessLeafTest {
    @Test
    fun `the date tile is the app's 64 day card at every size but the accessibility one`() {
        ConfirmationSize.entries.filter { it != ConfirmationSize.Ax }.forEach {
            assertEquals(it.name, 64.dp, confirmationLeafSide(it))
        }
    }

    @Test
    fun `at accessibility sizes the tile grows and stays square`() {
        assertEquals(92.dp, confirmationLeafSide(ConfirmationSize.Ax))
    }
}
