package cz.cleansia.core.settings

import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Test

/**
 * [SupportedLanguages.resolve] is what decides which language the backend
 * renders a confirmation or password-reset email in. Two properties matter and
 * both are pinned here:
 *
 *  - it must never return English just because the user left the picker on
 *    "System" (the fresh-install default), and
 *  - it must never return anything outside the five supported codes, because
 *    `Register.Validator` rejects an unknown language outright and the whole
 *    registration fails.
 */
class SupportedLanguagesTest {

    @Test
    fun `an explicit picker choice beats the device list`() {
        assertEquals("cs", SupportedLanguages.resolve("cs", listOf("en-US")))
    }

    @Test
    fun `System falls through to the device's first supported language`() {
        assertEquals("cs", SupportedLanguages.resolve(null, listOf("cs-CZ", "en-US")))
    }

    @Test
    fun `device order is honoured, so second-choice Czech beats unsupported German`() {
        assertEquals("cs", SupportedLanguages.resolve(null, listOf("de-DE", "cs-CZ", "en-US")))
    }

    /**
     * The registration-failure guard. A German or Polish handset must still be
     * sent "en" and never "de-DE"/"pl" — `LanguageValidator` does an
     * `ExistsWithCodeAsync` lookup and fails the whole Register command with
     * `LanguageNotSupported` for anything outside the five.
     */
    @Test
    fun `an entirely unsupported device list falls back to English, never a raw tag`() {
        val resolved = SupportedLanguages.resolve(null, listOf("de-DE", "pl-PL"))
        assertEquals("en", resolved)
        assert(resolved in SupportedLanguages.SUPPORTED)
    }

    @Test
    fun `an empty device list falls back to English`() {
        assertEquals("en", SupportedLanguages.resolve(null, emptyList()))
    }

    /**
     * A persisted tag is clamped too. It can only get out of range via a
     * downgrade or hand-edited DataStore, but if it does we scan the device
     * rather than shipping the bad tag to the API.
     */
    @Test
    fun `an unsupported persisted tag falls through to the device scan`() {
        assertEquals("sk", SupportedLanguages.resolve("de", listOf("sk-SK")))
        assertEquals("en", SupportedLanguages.resolve("de", listOf("pl-PL")))
    }

    @Test
    fun `region and script qualifiers are narrowed before matching`() {
        assertEquals("uk", SupportedLanguages.bareCode("uk-UA"))
        assertEquals("sk", SupportedLanguages.bareCode("sk_SK"))
        assertEquals("ru", SupportedLanguages.bareCode("ru-Cyrl-RU"))
        assertEquals("cs", SupportedLanguages.bareCode("  CS-CZ  "))
    }

    @Test
    fun `blank and headless tags yield no code rather than an empty match`() {
        assertNull(SupportedLanguages.bareCode(""))
        assertNull(SupportedLanguages.bareCode("   "))
        assertNull(SupportedLanguages.bareCode("-CZ"))
    }

    @Test
    fun `garbage entries are skipped without derailing the scan`() {
        assertEquals("ru", SupportedLanguages.resolve(null, listOf("", "-CZ", "zz", "ru-RU")))
    }

    @Test
    fun `the supported set matches locales_config in both apps`() {
        assertEquals(listOf("en", "cs", "sk", "uk", "ru"), SupportedLanguages.SUPPORTED)
    }
}
