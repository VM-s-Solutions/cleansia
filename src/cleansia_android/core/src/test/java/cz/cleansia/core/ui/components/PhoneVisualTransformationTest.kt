package cz.cleansia.core.ui.components

import androidx.compose.ui.text.AnnotatedString
import com.google.i18n.phonenumbers.PhoneNumberUtil
import kotlin.random.Random
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Test

/**
 * Compose throws when a visual transformation's cursor mapping leaves either text or runs backwards, so the phone
 * field's mapping must hold for every input, also when libphonenumber regroups earlier digits as more arrive.
 */
class PhoneVisualTransformationTest {

    private val util = PhoneNumberUtil.getInstance()

    @Test
    fun `ten Czech digits, which the formatter shows ungrouped and shorter than nine, map inside the text`() {
        val transformed = PhoneVisualTransformation(util, "CZ").filter(AnnotatedString("7771234567"))
        assertEquals("7771234567", transformed.text.text)
        assertValid("CZ", "7771234567")
    }

    @Test
    fun `the cursor after a typed digit sits right after that digit in the grouped text`() {
        val mapping = PhoneVisualTransformation(util, "CZ").filter(AnnotatedString("777123456")).offsetMapping
        assertEquals(5, mapping.originalToTransformed(4))
        assertEquals(11, mapping.originalToTransformed(9))
        assertEquals(4, mapping.transformedToOriginal(5))
    }

    @Test
    fun `every length of every market's numbers, and over-long ones, keeps both mappings valid`() {
        val random = Random(42)
        val seeds = listOf("777123456789012345678901", "0671234567890123456789", "9123456789012345678901") +
            List(20) { (1..24).map { random.nextInt(10) }.joinToString("") }
        listOf("CZ", "SK", "UA", "PL", "DE", "RU", "US", "GB").forEach { region ->
            val code = callingCode(region)!!
            seeds.forEach { digits ->
                for (length in 0..digits.length) {
                    assertValid(region, digits.take(length))
                    assertValid(region, (code + digits).take(length))
                }
            }
        }
    }

    @Test
    fun `pasted text with separators and letters keeps both mappings valid`() {
        listOf("+420 777 123 456", "Tel: +420 777 123 456", "(+420) 777-123-456", "777 12a3 456 789 01", "++420", " ", "+")
            .forEach { pasted -> listOf("CZ", "SK", "UA", "US").forEach { assertValid(it, pasted, roundTrip = false) } }
    }

    private fun assertValid(region: String, raw: String, roundTrip: Boolean = true) {
        val transformed = PhoneVisualTransformation(util, region).filter(AnnotatedString(raw))
        val shown = transformed.text.length
        val mapping = transformed.offsetMapping
        val context = "$region '$raw' shown as '${transformed.text.text}'"
        var previous = 0
        for (offset in 0..raw.length) {
            val mapped = mapping.originalToTransformed(offset)
            assertTrue("$context: original $offset maps to $mapped", mapped in previous..shown)
            if (roundTrip) assertEquals("$context: round trip of $offset", offset, mapping.transformedToOriginal(mapped))
            previous = mapped
        }
        previous = 0
        for (offset in 0..shown) {
            val mapped = mapping.transformedToOriginal(offset)
            assertTrue("$context: transformed $offset maps to $mapped", mapped in previous..raw.length)
            previous = mapped
        }
    }
}
