package cz.cleansia.partner.features.orders

import cz.cleansia.partner.api.model.Translation
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Test

class ScopeTranslationTest {

    private val translations = mapOf(
        "cs" to Translation(name = "Hloubkové čištění", description = "Popis"),
        "sk" to Translation(name = "Hĺbkové čistenie"),
        "ru" to Translation(name = null),
        "pl" to Translation(name = "  "),
    )

    @Test
    fun resolvesTranslatedNameForCurrentLocale() {
        assertEquals("Hloubkové čištění", resolveTranslatedName(translations, "cs", "Deep Cleaning"))
    }

    @Test
    fun fallsBackToStoredNameWhenLocaleMissing() {
        assertEquals("Deep Cleaning", resolveTranslatedName(translations, "uk", "Deep Cleaning"))
    }

    @Test
    fun fallsBackToStoredNameWhenTranslationNameIsNull() {
        assertEquals("Deep Cleaning", resolveTranslatedName(translations, "ru", "Deep Cleaning"))
    }

    @Test
    fun fallsBackToStoredNameWhenTranslationNameIsBlank() {
        assertEquals("Deep Cleaning", resolveTranslatedName(translations, "pl", "Deep Cleaning"))
    }

    @Test
    fun fallsBackToStoredNameWhenTranslationsAbsent() {
        assertEquals("Deep Cleaning", resolveTranslatedName(null, "cs", "Deep Cleaning"))
        assertEquals("Deep Cleaning", resolveTranslatedName(emptyMap(), "cs", "Deep Cleaning"))
    }

    @Test
    fun fallsBackToStoredNameWhenLocaleBlank() {
        assertEquals("Deep Cleaning", resolveTranslatedName(translations, null, "Deep Cleaning"))
        assertEquals("Deep Cleaning", resolveTranslatedName(translations, "", "Deep Cleaning"))
    }

    @Test
    fun preservesNullFallbackWhenNothingResolves() {
        assertNull(resolveTranslatedName(null, "cs", null))
    }
}
