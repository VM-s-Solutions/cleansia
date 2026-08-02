package cz.cleansia.partner.features.orders

import java.io.File
import org.junit.Assert.assertTrue
import org.junit.Assert.fail
import org.junit.Test

/**
 * `OrderStatusPill` shipped for months with zero call sites and a doc comment
 * claiming it was "used in list rows + the details header". Nothing catches
 * that: an unused public composable compiles, and the comment is the only
 * thing that reads like documentation of where it lives.
 *
 * It is now on the order-detail metadata row, where iOS puts it
 * (`OrderDetailContent.swift` compact header). This fails if the call site is
 * removed again without removing the component.
 */
class OrderStatusPillPlacementTest {

    private val sourceRoot: File = sequenceOf(
        File("src/main/java/cz/cleansia/partner"),
        File("partner-app/src/main/java/cz/cleansia/partner"),
        File("src/cleansia_android/partner-app/src/main/java/cz/cleansia/partner"),
    ).firstOrNull { it.isDirectory }
        ?: error("partner-app sources not found from working dir ${File(".").absolutePath}")

    @Test
    fun `the status pill is rendered somewhere outside its own file`() {
        val callers = sourceRoot.walkTopDown()
            .filter { it.isFile && it.extension == "kt" }
            .filterNot { it.name == "OrderStatusPill.kt" }
            .filter { it.readText().contains("OrderStatusPill(") }
            .map { it.name }
            .toList()

        if (callers.isEmpty()) {
            fail(
                "OrderStatusPill has no call site — either render it or delete it, " +
                    "but do not leave a component whose doc comment describes a placement " +
                    "that does not exist.",
            )
        }
    }

    @Test
    fun `it sits on the order detail metadata row, as on iOS`() {
        val metadataRow = File(sourceRoot, "features/orders/OrderMetadataRow.kt")
        assertTrue("OrderMetadataRow.kt not found at ${metadataRow.absolutePath}", metadataRow.isFile)
        assertTrue(
            "the order-detail header lost its status pill — iOS still shows one",
            metadataRow.readText().contains("OrderStatusPill("),
        )
    }
}
