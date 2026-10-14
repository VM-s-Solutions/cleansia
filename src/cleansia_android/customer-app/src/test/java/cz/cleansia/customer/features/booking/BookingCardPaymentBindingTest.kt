package cz.cleansia.customer.features.booking

import java.io.File
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Test

/**
 * As on iOS, a build without a Stripe key offers no card payment: `submit()` creates the order and then its
 * PaymentIntent for a sheet that cannot open. The card option and its saving consent mount only when the
 * key that initialises Stripe is set, and the option is the one place card is selected, so the card branch
 * is unreachable without it. The booking steps have no Compose harness, so the gate is pinned as source.
 */
class BookingCardPaymentBindingTest {

    @Test
    fun `the card option and its saving consent mount only when card payment is configured`() {
        val confirm = code(confirmStep)
        assertTrue(
            "ConfirmStep must read card availability from the key that initialises Stripe",
            confirm.contains("val cardPaymentAvailable = viewModel.cardPaymentAvailable"),
        )
        val gate = blockRange(confirm, "if (cardPaymentAvailable) {")
        val cardOption = confirm.indexOf("title = stringResource(R.string.booking_pay_card)")
        assertTrue("ConfirmStep lost its card option", cardOption >= 0)
        assertTrue("the card option must mount only inside if (cardPaymentAvailable)", cardOption in gate)
        val consents = Regex("""CardSavingConsent\(""").findAll(confirm).map { it.range.first }.toList()
        assertEquals("ConfirmStep must mount the card-saving consent exactly once", 1, consents.size)
        assertTrue("the card-saving consent must mount only inside if (cardPaymentAvailable)", consents.single() in gate)
    }

    @Test
    fun `card is selected in exactly one place in the app, inside the gate`() {
        val selections = mainSources().flatMap { file ->
            CARD_SELECTION.findAll(code(file)).map { file.name to it.range.first }.toList()
        }
        assertEquals(
            "card must be selected only by the gated option in ConfirmStep",
            listOf("ConfirmStep.kt"),
            selections.map { it.first },
        )
        val confirm = code(confirmStep)
        assertTrue(
            "the card selection must sit inside if (cardPaymentAvailable)",
            selections.single().second in blockRange(confirm, "if (cardPaymentAvailable) {"),
        )
    }

    private val mainDir: File = sequenceOf(
        File("."),
        File("customer-app"),
        File("src/cleansia_android/customer-app"),
    ).map { File(it, "src/main/java/cz/cleansia/customer") }
        .firstOrNull { it.isDirectory }
        ?: error("customer-app sources not found from working dir ${File(".").absolutePath}")

    private val confirmStep = File(mainDir, "features/booking/ConfirmStep.kt")

    private fun mainSources(): List<File> {
        val files = mainDir.walkTopDown().filter { it.isFile && it.extension == "kt" }.toList()
        assertTrue("the walk must reach ConfirmStep.kt", files.any { it.name == "ConfirmStep.kt" })
        assertTrue("the walk must reach BookingViewModel.kt", files.any { it.name == "BookingViewModel.kt" })
        return files
    }

    /** The source without comments, so prose naming a call is never read as the call. */
    private fun code(file: File): String =
        file.readText().replace("\r\n", "\n").replace(COMMENTS, "")

    /** From the unique [marker], which ends with the block's opening brace, to its matching closing brace. */
    private fun blockRange(text: String, marker: String): IntRange {
        val start = text.indexOf(marker)
        assertTrue("no `$marker` in the source", start >= 0)
        assertTrue("`$marker` must be unique for the gate to mean anything", start == text.lastIndexOf(marker))
        var depth = 0
        for (i in start + marker.length - 1 until text.length) {
            when (text[i]) {
                '{' -> depth++
                '}' -> if (--depth == 0) return start..i
            }
        }
        error("unbalanced braces after `$marker`")
    }

    private companion object {
        val COMMENTS = Regex("""/\*[\s\S]*?\*/|(?<![:"])//[^\n]*""")
        val CARD_SELECTION = Regex("""selectPaymentMethod\(\s*(?:BookingViewModel\.)?PAYMENT_CARD\s*\)""")
    }
}
