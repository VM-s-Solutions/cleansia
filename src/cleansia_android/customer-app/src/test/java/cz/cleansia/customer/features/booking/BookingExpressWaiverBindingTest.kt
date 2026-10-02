package cz.cleansia.customer.features.booking

import java.io.File
import org.junit.Assert.assertTrue
import org.junit.Test

/**
 * [BookingPricingTest] proves the math and [cz.cleansia.customer.core.memberships.ExpressWaiverTest]
 * proves the verdict; neither can see whether the wizard still draws either of them. The booking steps
 * have no Compose harness, so the call sites are pinned by source assertions scoped to the one thing
 * each screen must do.
 */
class BookingExpressWaiverBindingTest {

    private val moduleDir: File = sequenceOf(
        File("."),
        File("customer-app"),
        File("src/cleansia_android/customer-app"),
    ).firstOrNull { File(it, "src/main/res").isDirectory }
        ?: error("customer-app not found from working dir ${File(".").absolutePath}")

    /** Comments are stripped: a prose mention of `BookingPricing.finalTotal()` is not a call site. */
    private fun source(name: String): String =
        File(moduleDir, "src/main/java/cz/cleansia/customer/features/booking/$name")
            .also { assertTrue("$name not found at ${it.absolutePath}", it.isFile) }
            .readText()
            .lines()
            .filterNot { it.trimStart().startsWith("//") || it.trimStart().startsWith("*") }
            .joinToString("\n")

    @Test
    fun `the slot chip flips to the waived label when a waiver is available`() {
        val step = source("WhenWhereStep.kt")
        assertTrue(
            "the express chip no longer offers a waived variant",
            step.contains("booking_slot_express_waived"),
        )
        assertTrue(
            "the waived chip is not gated on the server's verdict",
            step.contains("ExpressWaiverStatus.Available"),
        )
    }

    @Test
    fun `the slot grid discloses both server-decided waiver states`() {
        val step = source("WhenWhereStep.kt")
        listOf(
            "booking_express_waiver_available",
            "booking_express_waiver_used",
        ).forEach {
            assertTrue("the slot grid dropped $it", step.contains(it))
        }
    }

    @Test
    fun `the disclosure maps each waiver state to its own sentence`() {
        val flat = source("WhenWhereStep.kt").replace(Regex("\\s+"), " ")
        assertTrue(
            "an exhausted member is not told their waivers are used up",
            flat.contains("ExpressWaiverStatus.Exhausted -> stringResource(R.string.booking_express_waiver_used)"),
        )
    }

    @Test
    fun `the summary renders the waived row from the server verdict`() {
        val confirm = source("ConfirmStep.kt")
        assertTrue(
            "the summary lost its waived row",
            confirm.contains("booking_summary_express_surcharge_waived"),
        )
        assertTrue(
            "the summary decides the waiver itself instead of reading the resolved line",
            confirm.contains("BookingPriceSummary.ExpressLine.Waived"),
        )
    }

    /**
     * `totalPrice` already contains the surcharge, so any rate applied on top of it inflates the screen
     * against the number the order is created with. No booking source may name a rate at all.
     */
    @Test
    fun `no booking screen re-applies an express rate`() {
        listOf("ConfirmStep.kt", "BookingBottomSheet.kt", "WhenWhereStep.kt", "BookingViewModel.kt").forEach { name ->
            val text = source(name)
            assertTrue(
                "$name multiplies by an express rate instead of reading the server amount",
                !Regex("0\\.2\\b|0\\.20\\b|1\\.2\\b|EXPRESS_SURCHARGE_RATE|\\* *20\\b")
                    .containsMatchIn(text),
            )
        }
    }

    /**
     * Two surfaces, one number: the receipt and the slide-to-confirm label must resolve identically.
     * Asserted on the **call**, not on the symbols — the sticky bar once passed its own zero discount
     * while still importing and naming everything this test would otherwise look for.
     */
    @Test
    fun `both money surfaces resolve through the one summary and the one discount`() {
        listOf("ConfirmStep.kt", "BookingBottomSheet.kt").forEach { name ->
            val flat = source(name).replace(Regex("\\s+"), " ")
            assertTrue(
                "$name does not spend the view model's discount at its BookingPriceSummary call",
                Regex("BookingPriceSummary\\.resolve\\(\\w+, effectiveDiscount, payByCard\\)").containsMatchIn(flat),
            )
            // Credit is a card-only tender: both surfaces must ask the one question the same way, or
            // the receipt and the slide-to-confirm label disagree about what the card is charged.
            assertTrue(
                "$name decides card payment differently from the other money surface",
                flat.contains("val payByCard = state.paymentMethod == BookingViewModel.PAYMENT_CARD"),
            )
            assertTrue(
                "$name derives its own discount instead of the view model's",
                flat.contains("bookingVm.effectiveDiscount"),
            )
        }
    }

    /**
     * AC4 — the remaining count is the server's field, rendered verbatim. A client that counts the
     * member's own orders disagrees with the server the first time one is cancelled.
     */
    @Test
    fun `no booking screen does arithmetic on the remaining count`() {
        listOf("WhenWhereStep.kt", "ConfirmStep.kt", "BookingBottomSheet.kt", "BookingViewModel.kt").forEach { name ->
            val text = source(name)
            assertTrue(
                "$name adjusts the server's remaining count",
                !Regex("remaining\\s*[-+]|[-+]\\s*\\w*[Rr]emaining|remaining\\.(plus|minus|inc|dec)")
                    .containsMatchIn(text),
            )
        }
    }

    @Test
    fun `the wizard reads the waiver from the membership endpoint, not from a clock`() {
        // Not a bare symbol search: the import line survives every plausible mutation of the body.
        val flat = source("BookingViewModel.kt").replace(Regex("\\s+"), " ")
        assertTrue(
            "the wizard stopped resolving the waiver from the membership response",
            flat.contains("membershipRepository.current .map { resolveExpressWaiver(it) }"),
        )
        assertTrue(
            "the wizard no longer warms the membership read it depends on",
            flat.contains("membershipRepository.refresh()"),
        )
    }
}
