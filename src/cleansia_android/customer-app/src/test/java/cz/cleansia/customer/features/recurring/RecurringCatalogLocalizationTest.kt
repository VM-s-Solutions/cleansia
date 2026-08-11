package cz.cleansia.customer.features.recurring

import cz.cleansia.customer.core.catalog.TranslationDto
import cz.cleansia.customer.features.booking.pickTranslatedDescription
import cz.cleansia.customer.features.booking.pickTranslatedName
import java.io.File
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Test

/**
 * The catalog is served in one language with a per-language map beside it, so a
 * screen that renders the raw `name` shows a Czech user "Deep cleaning" in the
 * middle of otherwise-translated chrome. The wizard's three catalog renders are
 * the ones that shipped that way.
 */
class RecurringCatalogLocalizationTest {

    private val translations = mapOf(
        "cs" to TranslationDto(name = "Hloubkový úklid", description = "Důkladné čištění"),
        "ru" to TranslationDto(name = "Генеральная уборка", description = null),
    )

    @Test
    fun `the active language wins over the catalog's own language`() {
        assertEquals("Hloubkový úklid", pickTranslatedName(translations, "cs", "Deep cleaning"))
        assertEquals("Генеральная уборка", pickTranslatedName(translations, "ru", "Deep cleaning"))
    }

    @Test
    fun `an untranslated language falls back to the raw name, never to blank`() {
        assertEquals("Deep cleaning", pickTranslatedName(translations, "uk", "Deep cleaning"))
        assertEquals("Deep cleaning", pickTranslatedName(null, "cs", "Deep cleaning"))
        assertEquals("Deep cleaning", pickTranslatedName(emptyMap(), "cs", "Deep cleaning"))
        assertEquals("Deep cleaning", pickTranslatedName(translations, null, "Deep cleaning"))
        assertEquals("Deep cleaning", pickTranslatedName(translations, "", "Deep cleaning"))
    }

    /**
     * A translation may carry a name and omit the description; the description
     * must then fall back on its own rather than inheriting the name's verdict.
     */
    @Test
    fun `description falls back independently of the name`() {
        assertEquals("Důkladné čištění", pickTranslatedDescription(translations, "cs", "Thorough clean"))
        assertEquals("Thorough clean", pickTranslatedDescription(translations, "ru", "Thorough clean"))
        assertNull(pickTranslatedDescription(translations, "ru", null))
    }

    /**
     * The resolver above proves the fallback chain and says nothing about whether
     * the wizard still calls it. Asserted as whole call expressions — a bare
     * `localizedName` substring is satisfied by the import line alone, and the
     * argument list is exactly what the defect dropped.
     */
    @Test
    fun `the wizard's what-step renders every catalog name through the resolver`() {
        val calls = listOf(
            "localizedName(pkg.translations, pkg.name)",
            "localizedName(svc.translations, svc.name)",
            "localizedName(it.translations, it.name)",
        )
        val missing = calls.filterNot { screenSource.contains(it) }
        assertTrue(
            "the what-step stopped resolving these through the translations map, so the catalog " +
                "renders in whatever language the backend sent: $missing",
            missing.isEmpty(),
        )
    }

    private val screenSource: String = sequenceOf(
        File("."),
        File("customer-app"),
        File("src/cleansia_android/customer-app"),
    ).map { File(it, "src/main/java/cz/cleansia/customer/features/recurring/CreateRecurringScreen.kt") }
        .firstOrNull { it.isFile }
        ?.readText()
        ?: error("CreateRecurringScreen.kt not found from working dir ${File(".").absolutePath}")
}
