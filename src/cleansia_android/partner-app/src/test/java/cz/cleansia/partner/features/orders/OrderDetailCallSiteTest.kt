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

    private fun source(name: String): String =
        File(featureDir, name)
            .also { assertTrue("$name not found at ${it.absolutePath}", it.isFile) }
            .readText()
}
