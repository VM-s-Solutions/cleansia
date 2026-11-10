package cz.cleansia.customer.ui.format

import java.io.File
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNotNull
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Test

class DisputeLabelsTest {

    @Test
    fun `every reason value maps to its own label`() {
        val labels = (1..8).map { disputeReasonLabelRes(it).also { r -> assertNotNull("reason $it", r) } }
        assertEquals(8, labels.toSet().size)
    }

    @Test
    fun `every status value maps to its own label`() {
        val labels = (1..6).map { disputeStatusLabelRes(it).also { r -> assertNotNull("status $it", r) } }
        assertEquals(6, labels.toSet().size)
    }

    @Test
    fun `an unknown or missing value has no label`() {
        listOf(0, 99, null).forEach {
            assertNull(disputeReasonLabelRes(it))
            assertNull(disputeStatusLabelRes(it))
        }
    }

    @Test
    fun `the new keys exist in all five locales`() {
        val keys = listOf(
            "dispute_reason_chargeback",
            "dispute_status_pending", "dispute_status_under_review", "dispute_status_waiting_for_response",
            "dispute_status_resolved", "dispute_status_closed", "dispute_status_escalated",
            "error_dispute_cleaning_not_started",
        )
        val res = sequenceOf(File("src/main/res"), File("customer-app/src/main/res")).first { it.isDirectory }
        listOf("values", "values-cs", "values-sk", "values-uk", "values-ru").forEach { locale ->
            val text = File(res, "$locale/strings.xml").readText()
            keys.forEach { assertTrue("$locale missing $it", text.contains("name=\"$it\"")) }
        }
    }
}
