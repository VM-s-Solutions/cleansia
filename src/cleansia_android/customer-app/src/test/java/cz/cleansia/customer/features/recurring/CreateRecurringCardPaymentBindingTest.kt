package cz.cleansia.customer.features.recurring

import java.io.File
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Test

/**
 * As on iOS, a build without a Stripe key offers no card when a recurring series is created: the card option
 * mounts only when the key that initialises Stripe is set, it is the one place the form selects card, and a
 * new schedule starts on card only with that key. Without it the form starts with no way to pay chosen, as
 * booking does. The wizard has no Compose harness, so the gate is pinned as source.
 */
class CreateRecurringCardPaymentBindingTest {

    @Test
    fun `the card option mounts only when card payment is configured`() {
        val screen = code(createRecurringScreen)
        assertTrue(
            "CreateRecurringScreen must read card availability from the key that initialises Stripe",
            screen.contains("val cardPaymentAvailable = BuildConfig.STRIPE_PUBLISHABLE_KEY.isNotBlank()"),
        )
        val gate = blockRange(screen, "if (cardPaymentAvailable) {")
        val cardOption = screen.indexOf("label = stringResource(R.string.recurring_create_pay_card)")
        assertTrue("CreateRecurringScreen lost its card option", cardOption >= 0)
        assertTrue("the card option must mount only inside if (cardPaymentAvailable)", cardOption in gate)
    }

    @Test
    fun `card is selected in exactly one place in the app, inside the gate`() {
        val selections = mainSources().flatMap { file ->
            CARD_SELECTION.findAll(code(file)).map { file.name to it.range.first }.toList()
        }
        assertEquals(
            "card must be selected only by the gated option in CreateRecurringScreen",
            listOf("CreateRecurringScreen.kt"),
            selections.map { it.first },
        )
        assertTrue(
            "the card selection must sit inside if (cardPaymentAvailable)",
            selections.single().second in blockRange(code(createRecurringScreen), "if (cardPaymentAvailable) {"),
        )
    }

    @Test
    fun `a new schedule starts on card only when card payment is configured`() {
        assertTrue(
            "the form's paymentType must default to card only when the key that initialises Stripe is set, else null",
            CARD_DEFAULT.containsMatchIn(code(createRecurringViewModel)),
        )
    }

    /** A card copied from a stored schedule or a past order is the same card the hidden option would have selected. */
    @Test
    fun `a way to pay copied from a schedule or an order holds card only when card payment is configured`() {
        val viewModel = code(createRecurringViewModel)
        val copies = COPIED_PAYMENT_TYPE.findAll(viewModel).toList()
        assertEquals(
            "the form copies a way to pay from the stored schedule and from the past order",
            listOf("order.paymentType", "template.paymentType"),
            copies.map { it.value }.sorted(),
        )
        val gated = GATED_COPY.findAll(viewModel).map { it.range }.toList()
        copies.forEach { copy ->
            assertTrue(
                "`${copy.value}` must reach the form through presentablePaymentType(…, cardAvailable)",
                gated.any { copy.range.first in it },
            )
        }
        assertTrue(
            "the view model must read card availability from the key that initialises Stripe",
            viewModel.contains("private val cardAvailable = BuildConfig.STRIPE_PUBLISHABLE_KEY.isNotBlank()"),
        )
    }

    private val mainDir: File = sequenceOf(
        File("."),
        File("customer-app"),
        File("src/cleansia_android/customer-app"),
    ).map { File(it, "src/main/java/cz/cleansia/customer") }
        .firstOrNull { it.isDirectory }
        ?: error("customer-app sources not found from working dir ${File(".").absolutePath}")

    private val createRecurringScreen = File(mainDir, "features/recurring/CreateRecurringScreen.kt")

    private val createRecurringViewModel = File(mainDir, "features/recurring/CreateRecurringViewModel.kt")

    private fun mainSources(): List<File> {
        val files = mainDir.walkTopDown().filter { it.isFile && it.extension == "kt" }.toList()
        assertTrue("the walk must reach CreateRecurringScreen.kt", files.any { it.name == "CreateRecurringScreen.kt" })
        assertTrue("the walk must reach CreateRecurringViewModel.kt", files.any { it.name == "CreateRecurringViewModel.kt" })
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
        val CARD_SELECTION = Regex(
            """(?:onSelect|setPaymentType)\(\s*(?:CreateRecurringViewModel\.)?PAYMENT_CARD\s*\)""" +
                """|\bpaymentType\s*=\s*(?:CreateRecurringViewModel\.)?PAYMENT_CARD\b""",
        )
        val CARD_DEFAULT = Regex(
            """val paymentType: Int\? =\s*if \(BuildConfig\.STRIPE_PUBLISHABLE_KEY\.isNotBlank\(\)\)\s*""" +
                """(?:CreateRecurringViewModel\.)?PAYMENT_CARD\s+else\s+null\b""",
        )
        val COPIED_PAYMENT_TYPE = Regex("""\b(?:template|order)\.paymentType\b""")
        val GATED_COPY = Regex(
            """presentablePaymentType\(\s*(?:template|order)\.paymentType(?:\?\.value)?\s*,\s*cardAvailable\s*\)""",
        )
    }
}
