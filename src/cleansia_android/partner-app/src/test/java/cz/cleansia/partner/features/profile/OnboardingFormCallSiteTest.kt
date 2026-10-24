package cz.cleansia.partner.features.profile

import java.io.File
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Test

/**
 * Every onboarding form validates on the tap and names each problem under its field, so its primary
 * button is held only while a request is in flight. A button disabled until the form is valid made
 * those field errors unreachable — the cleaner saw a dead button and no reason. There is no Compose
 * harness in this module, so the call sites are pinned as source.
 */
class OnboardingFormCallSiteTest {

    private val featuresDir: File = sequenceOf(File("."), File("partner-app"), File("src/cleansia_android/partner-app"))
        .map { File(it, "src/main/java/cz/cleansia/partner/features") }
        .first { it.isDirectory }

    private fun source(path: String) = File(featuresDir, path).readText().replace("\r\n", "\n")

    /** The whole argument list of the first [call] (which ends in its open paren) that mentions [marker]. */
    private fun callBlock(source: String, call: String, marker: String = call): String {
        var from = 0
        while (true) {
            val start = source.indexOf(call, from)
            check(start >= 0) { "no $call mentioning $marker" }
            var depth = 0
            var end = start + call.length - 1
            while (true) {
                when (source[end]) {
                    '(' -> depth++
                    ')' -> if (--depth == 0) break
                }
                end++
            }
            val block = source.substring(start, end + 1)
            if (marker in block) return block
            from = end
        }
    }

    private fun argument(block: String, name: String): String =
        Regex("""\b$name = ([^\n]+?),?\n""").find(block)?.groupValues?.get(1)
            ?: error("no $name argument in:\n$block")

    @Test
    fun `sign-up's submit is held only while the registration is in flight`() {
        val button = callBlock(source("auth/RegisterScreen.kt"), "CleansiaPrimaryButton(", "R.string.register")
        assertEquals("!uiState.isLoading", argument(button, "enabled"))
    }

    @Test
    fun `the personal step's Next is held only while saving, and the phone is named under its field`() {
        val screen = source("profile/PersonalSectionScreen.kt")
        assertEquals("!saving", argument(callBlock(screen, "SectionSaveRow("), "enabled"))
        assertEquals("form.phoneError", argument(callBlock(screen, "CleansiaPhoneInput("), "errorText"))
    }

    @Test
    fun `the identification step's Next is held only while saving, and each field is named under it`() {
        val screen = source("profile/IdentificationSectionScreen.kt")
        assertEquals("!saving", argument(callBlock(screen, "SectionSaveRow("), "enabled"))
        mapOf(
            "R.string.nationality" to "form.nationalityError",
            "R.string.passport_id" to "form.passportError",
            "R.string.business_country" to "form.businessCountryError",
            "R.string.registration_number_label" to "form.registrationNumberError",
        ).forEach { (label, error) ->
            val field = listOf("PickerDropdown(", "CleansiaTextField(")
                .firstNotNullOf { call -> runCatching { callBlock(screen, call, label) }.getOrNull() }
            assertEquals(label, error, argument(field, "errorText"))
        }
    }

    /**
     * The legal name is locked, and a disabled Material field draws its supporting text in the disabled
     * grey, so the reason it is missing is its own line in the error colour.
     */
    @Test
    fun `a legal-entity row always shows its locked name, and says in the error colour when it is missing`() {
        val block = source("profile/IdentificationSectionScreen.kt")
            .substringAfter("if (form.isLegalEntity) {")
            .substringBefore("SectionSaveRow(")
        assertEquals("form.storedLegalEntityName.orEmpty()", argument(callBlock(block, "CleansiaTextField("), "value"))
        val reason = callBlock(block.substringAfter("form.legalEntityNameError?.let"), "Text(")
        assertEquals("MaterialTheme.colorScheme.error", argument(reason, "color"))
    }

    @Test
    fun `the bank step's Save is held only while saving, and each refusal is named under its field`() {
        val screen = source("profile/BankSectionScreen.kt")
        assertEquals("!saving", argument(callBlock(screen, "SectionSaveRow("), "enabled"))
        assertEquals(
            "form.bankCountryError",
            argument(callBlock(screen, "PickerDropdown(", "R.string.bank_country"), "errorText"),
        )
        assertEquals("form.accountError", argument(callBlock(screen, "CleansiaBankAccountInput("), "errorText"))
        val iban = callBlock(screen, "CleansiaTextField(", "R.string.iban_helper").replace(Regex("\\s+"), " ")
        assertTrue(
            "the IBAN's own check comes first and the server's refusal after it:\n$iban",
            iban.contains(
                "errorText = form.ibanProblem?.takeIf { form.ibanChecked }?.let { ibanProblemMessage(it) } " +
                    "?: form.ibanServerError,",
            ),
        )
        assertEquals(
            "form.swiftError",
            argument(callBlock(screen, "CleansiaTextField(", "R.string.swift_code_helper"), "errorText"),
        )
    }

    @Test
    fun `the address step's Next is held only while saving, and the card names an incomplete pick`() {
        val screen = source("profile/AddressSectionScreen.kt")
        assertEquals("!saving", argument(callBlock(screen, "SectionSaveRow("), "enabled"))
        assertEquals("form.addressError", argument(callBlock(screen, "AddressSummaryCard(", "onClick"), "errorText"))
        val card = screen.substringAfter("private fun AddressSummaryCard(").substringBefore("private fun ServiceAreaRow(")
        val flat = card.replace(Regex("\\s+"), " ")
        assertTrue("the card's border no longer turns red", flat.contains("if (errorText != null) MaterialTheme.colorScheme.error"))
        assertEquals("MaterialTheme.colorScheme.error", argument(callBlock(card, "Text" + "(", "text = errorText"), "color"))
    }
}
