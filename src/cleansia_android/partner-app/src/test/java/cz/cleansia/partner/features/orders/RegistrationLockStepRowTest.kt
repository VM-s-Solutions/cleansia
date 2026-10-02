package cz.cleansia.partner.features.orders

import java.io.File
import org.junit.Assert.assertTrue
import org.junit.Test

/**
 * A registration-lock row centres its icon, its title with the status line and its trailing status on
 * one line, whatever detail lines hang below. Centred on the whole row, the missing-fields list drew the
 * icon and the status down beside the list, away from the title. No Compose harness here, so the row's
 * shape is pinned as source.
 */
class RegistrationLockStepRowTest {

    private val row: String = sequenceOf(File("."), File("partner-app"), File("src/cleansia_android/partner-app"))
        .map { File(it, "src/main/java/cz/cleansia/partner/features/orders/RegistrationLockScreen.kt") }
        .first { it.isFile }
        .readText()
        .substringAfter("private fun StepRowView(")
        .substringBefore("private fun SignOutLink(")

    @Test
    fun `the icon, the title and the trailing status share one centred line`() {
        val header = row.substringAfter("Row(verticalAlignment = Alignment.CenterVertically) {")
            .substringBefore("val detailsModifier")
        assertTrue("the halo left the header line", header.contains("IconHalo(icon = categoryIcon)"))
        assertTrue("the title left the header line", header.contains("stringResource(categoryLabelRes)"))
        assertTrue("the status icon left the header line", header.contains("imageVector = statusIcon"))
        assertTrue("the chevron left the header line", header.contains("KeyboardArrowRight"))
    }

    @Test
    fun `the detail lines hang below the header, not inside it`() {
        val header = row.substringAfter("Row(verticalAlignment = Alignment.CenterVertically) {")
            .substringBefore("val detailsModifier")
        assertTrue("the missing fields moved back into the header", !header.contains("resolveDetail("))
        assertTrue("the rejection note moved back into the header", !header.contains("registration_lock_approval_rejected"))
        val details = row.substringAfter("val detailsModifier")
        assertTrue(details.contains("resolveDetail(context, rawKey)"))
        assertTrue(details.contains("R.string.registration_lock_approval_rejected"))
    }
}
