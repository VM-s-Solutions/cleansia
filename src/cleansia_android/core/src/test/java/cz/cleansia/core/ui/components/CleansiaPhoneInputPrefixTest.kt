package cz.cleansia.core.ui.components

import java.io.File
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Test

/**
 * With a country, the shared phone field draws its calling code before the national digits while the value stays
 * `+` and digits. A stored number the code cannot carry, or a pasted one with its own `+`, shows whole, so a stored
 * number is never rewritten.
 */
class CleansiaPhoneInputPrefixTest {

    @Test
    fun `a region gives its calling code, and one libphonenumber does not know gives none`() {
        assertEquals("+420", callingCode("CZ"))
        assertEquals("+421", callingCode("sk"))
        assertNull(callingCode("ZZ"))
    }

    @Test
    fun `the digits behind the code are shown, and a number the code cannot carry is not cut`() {
        assertEquals("", nationalPart("", "+420"))
        assertEquals("77712", nationalPart("+42077712", "+420"))
        assertNull(nationalPart("+421912345678", "+420"))
        assertNull(nationalPart("777123456", "+420"))
    }

    @Test
    fun `typed national digits go out behind the code, and a pasted number with its own plus stays whole`() {
        assertEquals("", wireFromTyped("", "+420"))
        assertEquals("+420777123456", wireFromTyped("777 123 456", "+420"))
        assertEquals("+420777123456", wireFromTyped("+420 777 123 456", "+420"))
        val pasted = wireFromTyped("+447700900123", "+420")
        assertEquals("+447700900123", pasted)
        assertNull("a foreign number leaves the prefix", nationalPart(pasted, "+420"))
    }

    @Test
    fun `a number is grouped as the free form field shows it`() {
        assertEquals("+420 777 12", formatPhoneAsTyped("+42077712"))
        assertEquals("+420 777 123 456", formatPhoneAsTyped("+420777123456"))
        assertEquals("", formatPhoneAsTyped(""))
    }

    /** A key missing from a locale compiles and reads English, and `:core` has no key parity test of its own. */
    @Test
    fun `the prefix's spoken name exists in every core locale`() {
        listOf("values", "values-cs", "values-sk", "values-uk", "values-ru").forEach { localeDir ->
            val file = File(coreDir, "src/main/res/$localeDir/strings.xml")
            assertTrue("$file does not exist", file.isFile)
            assertTrue(
                "core/$localeDir is missing `core_phone_country_code`",
                file.readText().contains("""<string name="core_phone_country_code">"""),
            )
        }
    }

    private val coreDir: File = sequenceOf(File("."), File("core"), File("src/cleansia_android/core"))
        .firstOrNull { File(it, "src/main/res/values/strings.xml").isFile }
        ?: error("core module not found from working dir ${File(".").absolutePath}")
}
