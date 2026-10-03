package cz.cleansia.customer.features.profile

import java.io.File
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Assert.fail
import org.junit.Test

/**
 * The delete-account confirmation dialog is the last gate before an
 * irreversible action, so every word on it must be readable in the user's own
 * language. Android resource resolution silently falls back to `values/` when a
 * locale row is missing, and this module has no lint step in CI — a dropped
 * `values-uk` row compiles, ships, and renders English on the one screen where
 * a misunderstanding costs the user their account.
 *
 * This parses the actual strings.xml files rather than touching `R`, because
 * `R.string.*` is a single locale-independent id: it cannot tell you whether
 * the Ukrainian translation exists.
 */
class DeleteAccountDialogStringsTest {

    private val locales = listOf("values", "values-cs", "values-sk", "values-uk", "values-ru")

    /** The dialog's own copy plus the shared dismiss label it reuses. */
    private val dialogKeys = listOf(
        "delete_account_dialog_title",
        "delete_account_dialog_message",
        "delete_account_dialog_confirm",
        "common_cancel",
    )

    private val resDir: File = sequenceOf(
        File("src/main/res"),
        File("customer-app/src/main/res"),
        File("src/cleansia_android/customer-app/src/main/res"),
    ).firstOrNull { it.isDirectory }
        ?: error("customer-app res/ not found from working dir ${File(".").absolutePath}")

    private fun stringsXml(locale: String): String {
        val file = File(resDir, "$locale/strings.xml")
        assertTrue("missing $locale/strings.xml", file.isFile)
        return file.readText()
    }

    @Test
    fun `dialog copy is present in all five locales`() {
        val missing = mutableListOf<String>()
        locales.forEach { locale ->
            val xml = stringsXml(locale)
            dialogKeys.forEach { key ->
                if (!xml.contains("name=\"$key\"")) missing += "$locale/$key"
            }
        }
        if (missing.isNotEmpty()) fail("missing string rows: ${missing.joinToString()}")
    }

    /**
     * A row that exists but was copy-pasted from `values/` untranslated is the
     * same defect wearing a disguise, and it is exactly what happens when a
     * translator is skipped. The Cyrillic locales cannot legitimately share the
     * English wording, so require them to differ.
     */
    @Test
    fun `Cyrillic locales do not reuse the English wording`() {
        val english = stringsXml("values")
        listOf("values-uk", "values-ru").forEach { locale ->
            val xml = stringsXml(locale)
            dialogKeys.forEach { key ->
                val en = valueOf(english, key)
                val translated = valueOf(xml, key)
                assertTrue(
                    "$locale/$key is still the English text: $translated",
                    translated != en,
                )
            }
        }
    }

    /**
     * A completed deletion forfeits every unused credit balance; it is not paid out and cannot be
     * restored. Each locale's own credit word, its own "forfeited" and its own "paid out" — so a
     * translation that drops any of the three fails in the locale that dropped it.
     */
    private val forfeitureClaim = mapOf(
        "values" to listOf("credit", "forfeit", "paid out"),
        "values-cs" to listOf("kredit", "propadá", "vyplatit"),
        "values-sk" to listOf("kredit", "prepadá", "vyplatiť"),
        "values-uk" to listOf("кредит", "анулю", "виплатити"),
        "values-ru" to listOf("кредит", "аннулир", "выплатить"),
    )

    /** The screen body the customer reads before typing their e-mail, and the dialog they accept. */
    private val forfeitureKeys = listOf("delete_account_subtitle", "delete_account_dialog_message")

    @Test
    fun `the deletion warns that unused credit is forfeited and cannot be paid out, in every locale`() {
        locales.forEach { locale ->
            val xml = stringsXml(locale)
            forfeitureKeys.forEach { key ->
                val value = valueOf(xml, key)
                forfeitureClaim.getValue(locale).forEach { word ->
                    assertTrue("$locale/$key does not say \"$word\": $value", value.contains(word, ignoreCase = true))
                }
            }
        }
    }

    /**
     * Orders, receipts, the consent record, the action log and dispute text all outlive the account
     * by design (→ /flows/gdpr-and-audit), so "everything is deleted" is a promise the erasure does
     * not keep. The web's delete page states what is kept; this screen states the same list.
     */
    private val everythingDeletedClaim = Regex(
        "all associated data|account and data|and all data|všechna související data|účet a data|" +
            "všetky súvisiace údaje|účet a údaje|всі пов’язані дані|запис і дані|" +
            "все связанные данные|запись и все|аккаунт и данные",
        RegexOption.IGNORE_CASE,
    )

    private val keptKeys = listOf(
        "delete_account_what_is_kept",
        "delete_account_kept_bookings",
        "delete_account_kept_receipts",
        "delete_account_kept_consents",
        "delete_account_kept_audit",
        "delete_account_kept_disputes",
    )

    @Test
    fun `no locale says every piece of data is deleted`() {
        val claims = locales.flatMap { locale ->
            Regex("<string name=\"(delete_account_[^\"]+)\"[^>]*>(.*?)</string>", RegexOption.DOT_MATCHES_ALL)
                .findAll(stringsXml(locale))
                .filter { everythingDeletedClaim.containsMatchIn(it.groupValues[2]) }
                .map { "$locale/${it.groupValues[1]}: ${it.groupValues[2]}" }
                .toList()
        }
        assertEquals(emptyList<String>(), claims)
    }

    @Test
    fun `the screen lists what is kept, in every locale`() {
        locales.forEach { locale ->
            val xml = stringsXml(locale)
            keptKeys.forEach { key ->
                assertTrue("$locale/$key is blank", valueOf(xml, key).isNotBlank())
            }
        }
        val screen = File(resDir.parentFile, "java/cz/cleansia/customer/features/profile/DeleteAccountScreen.kt")
        val source = screen.readText()
        keptKeys.forEach { key ->
            assertTrue("DeleteAccountScreen.kt does not render $key", source.contains("R.string.$key"))
        }
    }

    @Test
    fun `the screen renders the keys that carry the warning`() {
        val screen = File(resDir.parentFile, "java/cz/cleansia/customer/features/profile/DeleteAccountScreen.kt")
        assertTrue("DeleteAccountScreen.kt not found next to res/", screen.isFile)
        val source = screen.readText()
        assertTrue(source.contains("text = { Text(stringResource(R.string.delete_account_dialog_message)) }"))
        assertTrue(source.contains("stringResource(R.string.delete_account_subtitle)"))
    }

    private fun valueOf(xml: String, key: String): String {
        val match = Regex("<string name=\"$key\"[^>]*>(.*?)</string>", RegexOption.DOT_MATCHES_ALL)
            .find(xml)
        return match?.groupValues?.get(1) ?: fail("no <string name=\"$key\"> found").let { "" }
    }
}
