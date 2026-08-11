package cz.cleansia.partner.features.orders

import java.io.File
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test

/**
 * A resolver test cannot see the call site, and the call site is where this
 * defect lived: the wire carried the coarse address and the seat block, and the
 * three composables that own the detail's location and crew simply never read
 * them. So these pin that the screen renders *through* the resolvers and never
 * reaches around them into the raw `address` record.
 *
 * Evidence here is Kotlin source read off disk, which Gradle does not track as
 * an input — this file only means something under
 * `testDebugUnitTest --rerun-tasks --no-build-cache`.
 */
class OrderDetailCallSiteTest {

    private val featureDir: File = sequenceOf(
        File("."),
        File("partner-app"),
        File("src/cleansia_android/partner-app"),
    ).map { File(it, "src/main/java/cz/cleansia/partner/features/orders") }
        .firstOrNull { it.isDirectory }
        ?: error("orders feature dir not found from working dir ${File(".").absolutePath}")

    private val customerCard = source("CustomerCard.kt")
    private val detailScreen = source("OrderDetailScreen.kt")
    private val scopeCard = source("ScopeCard.kt")

    @Test
    fun `the customer card renders whichever location arrived`() {
        assertTrue(
            "CustomerCard must render OrderLocation.line — the coarse zone is a location too",
            customerCard.contains("location.line"),
        )
        assertFalse(
            "CustomerCard must not format the address itself; orderLocation() decides " +
                "what arrived and the card renders it",
            customerCard.contains("formatSingleLine("),
        )
    }

    @Test
    fun `Navigate is offered only for a street address`() {
        assertTrue(
            "CustomerCard must gate Navigate on OrderLocation.navigationTarget() — a maps " +
                "app opened on a city name is worse than no button",
            customerCard.contains("location.navigationTarget()"),
        )
        assertTrue(
            "the Navigate chip must still exist for the entitled reader",
            customerCard.contains("R.string.action_navigate"),
        )
    }

    @Test
    fun `the map backdrop is resolved, not re-derived from the raw address`() {
        assertTrue(
            "OrderDetailScreen must take its map point from OrderLocation.mapPoint(status)",
            detailScreen.contains(".mapPoint("),
        )
        val rawReads = detailScreen.lines().filter { it.contains("order.address") }
        assertTrue(
            "OrderDetailScreen must not read order.address directly: " +
                rawReads.joinToString(" | ") { it.trim() },
            rawReads.isEmpty(),
        )
    }

    @Test
    fun `the scope card renders the seat facts`() {
        assertTrue(
            "ScopeCard must resolve the crew through orderCrew() — TakeOrder's free-seat " +
                "conjunct refuses a take this screen otherwise never warned about",
            scopeCard.contains("orderCrew("),
        )
    }

    /**
     * Positive claims only. "The view does not consult a flag" is an absence assertion that goes
     * green when the view is renamed away and says nothing about the claim's content; that the view
     * renders *through* the resolver is the checkable half, and it is what
     * [OrderDisclosurePresentationTest] then drives at the divergent shape.
     */
    @Test
    fun `the redacted fields are rendered through the disclosure resolver`() {
        assertTrue(
            "CustomerCard must take the phone from OrderDisclosure — the server already withheld " +
                "it from anyone it meant to",
            customerCard.contains("disclosure.customerPhone") &&
                customerCard.contains("disclosure.showsCustomerContact"),
        )
        assertTrue(
            "the access card must gate on the door code's own arrival",
            detailScreen.contains("disclosure.showsAccessCard(status)"),
        )
        assertTrue(
            "the notes & issues section must gate on the record's own arrival",
            detailScreen.contains("disclosure.showsWorkRecordSection(canAddNotesOrIssues)"),
        )
        val rawReads = detailScreen.lines()
            .filter { it.contains("order.accessInstructions") || it.contains("order.customerPhone") }
        assertTrue(
            "OrderDetailScreen must not reach around orderDisclosure() into the raw fields: " +
                rawReads.joinToString(" | ") { it.trim() },
            rawReads.isEmpty(),
        )
    }

    /**
     * The scope line of the rule. Withdrawing these would offer "Slide to start" and the checklist
     * on a stranger's order, which is the failure the entitlement flag is the right input to.
     */
    @Test
    fun `the action gates still read the assignment flag`() {
        assertTrue(
            "the checklist and photo rails are the assignee's work tools",
            detailScreen.contains("val showWorkSections = isMine &&"),
        )
        assertTrue(
            "adding a note is a write, and a write is the assignee's",
            detailScreen.contains("isMine && (status == OrderStatus._3 || status == OrderStatus._4)"),
        )
        assertTrue(
            "the primary action decides what is OFFERED, which is not this rule's subject",
            source("OrderPrimaryAction.kt").contains("isAssignedToCurrentUser"),
        )
    }

    private fun source(name: String): String =
        File(featureDir, name)
            .also { assertTrue("$name not found at ${it.absolutePath}", it.isFile) }
            .readText()
}
