package cz.cleansia.partner.ui.components

import androidx.compose.foundation.text.KeyboardOptions
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.Text
import androidx.compose.material3.TextField
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Modifier
import androidx.compose.ui.focus.onFocusChanged
import androidx.compose.ui.graphics.Shape
import androidx.compose.ui.text.AnnotatedString
import androidx.compose.ui.text.input.KeyboardType
import androidx.compose.ui.text.input.OffsetMapping
import androidx.compose.ui.text.input.TransformedText
import androidx.compose.ui.text.input.VisualTransformation
import androidx.compose.material3.TextFieldColors

/**
 * Input mask matching PrimeNG p-inputMask behavior.
 * Mask characters:
 *   '9' = any digit (0-9)
 *   Any other character = literal (always displayed, not editable)
 *
 * The underlying value stores ONLY the user-entered digits.
 * The mask handles display formatting.
 *
 * Example: mask = "+420 999 999 999"
 *   - User types "123456789"
 *   - Displayed as "+420 123 456 789"
 *   - Value stored: "123456789"
 */
class MaskVisualTransformation(
    private val mask: String,
    private val maskChar: Char = '9'
) : VisualTransformation {

    private val maskSlots: Int = mask.count { it == maskChar }

    override fun filter(text: AnnotatedString): TransformedText {
        val digits = text.text.filter { it.isDigit() }.take(maskSlots)

        val formatted = buildString {
            var digitIndex = 0
            for (maskCh in mask) {
                if (digitIndex >= digits.length && maskCh == maskChar) break
                if (maskCh == maskChar) {
                    append(digits[digitIndex])
                    digitIndex++
                } else {
                    append(maskCh)
                }
            }
        }

        val offsetMapping = object : OffsetMapping {
            override fun originalToTransformed(offset: Int): Int {
                var digitsSeen = 0
                for (i in formatted.indices) {
                    if (digitsSeen >= offset) return i
                    if (mask.getOrNull(i) == maskChar) digitsSeen++
                }
                return formatted.length
            }

            override fun transformedToOriginal(offset: Int): Int {
                var digitsSeen = 0
                for (i in 0 until offset.coerceAtMost(formatted.length)) {
                    if (mask.getOrNull(i) == maskChar) digitsSeen++
                }
                return digitsSeen.coerceAtMost(digits.length)
            }
        }

        return TransformedText(AnnotatedString(formatted), offsetMapping)
    }
}

/**
 * Phone field composable matching the web app's cleansia-telephone behavior.
 *
 * - Value stores the FULL phone number with prefix (e.g. "+420608123456")
 * - On first focus when empty, auto-fills with "+420" prefix
 * - User can modify or remove the prefix to type any number
 * - Phone keyboard type, formatted display with spaces
 *
 * @param value The full phone number (e.g. "+420608123456")
 * @param onValueChange Called with the full phone number
 * @param label Field label text
 * @param defaultPrefix Auto-filled prefix on first focus when empty
 */
@Composable
fun CleansiaPhoneField(
    value: String,
    onValueChange: (String) -> Unit,
    label: String,
    modifier: Modifier = Modifier,
    defaultPrefix: String = "+420",
    isError: Boolean = false,
    supportingText: @Composable (() -> Unit)? = null
) {
    OutlinedTextField(
        value = value,
        onValueChange = { newValue ->
            // Allow +, digits and spaces; strip spaces for storage
            val cleaned = newValue.filter { it.isDigit() || it == '+' }
            onValueChange(cleaned)
        },
        label = { Text(label) },
        placeholder = { Text("+420 ___ ___ ___") },
        modifier = modifier.onFocusChanged { focusState ->
            if (focusState.isFocused && value.isBlank()) {
                onValueChange(defaultPrefix)
            }
        },
        singleLine = true,
        keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Phone),
        visualTransformation = PhoneVisualTransformation(defaultPrefix),
        isError = isError,
        supportingText = supportingText
    )
}

/**
 * Phone field variant using filled TextField (for ProfileScreen sections).
 */
@Composable
fun CleansiaPhoneTextField(
    value: String,
    onValueChange: (String) -> Unit,
    label: String,
    modifier: Modifier = Modifier,
    defaultPrefix: String = "+420",
    shape: Shape? = null,
    colors: TextFieldColors? = null,
    isError: Boolean = false,
    supportingText: @Composable (() -> Unit)? = null
) {
    TextField(
        value = value,
        onValueChange = { newValue ->
            val cleaned = newValue.filter { it.isDigit() || it == '+' }
            onValueChange(cleaned)
        },
        label = { Text(label) },
        placeholder = { Text("+420 ___ ___ ___") },
        modifier = modifier.onFocusChanged { focusState ->
            if (focusState.isFocused && value.isBlank()) {
                onValueChange(defaultPrefix)
            }
        },
        singleLine = true,
        keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Phone),
        visualTransformation = PhoneVisualTransformation(defaultPrefix),
        shape = shape ?: androidx.compose.material3.MaterialTheme.shapes.small,
        colors = colors ?: androidx.compose.material3.TextFieldDefaults.colors(),
        isError = isError,
        supportingText = supportingText
    )
}

/**
 * Visual transformation for phone numbers with multi-region support.
 * Auto-detects country code length (2 or 3 digits) and groups remaining digits in 3s.
 *
 * Examples:
 * - +420 123 456 789 (CZ, 3-digit code)
 * - +421 123 456 789 (SK, 3-digit code)
 * - +49 123 456 789  (DE, 2-digit code)
 * - +48 123 456 789  (PL, 2-digit code)
 */
class PhoneVisualTransformation(
    private val defaultPrefix: String = "+420"
) : VisualTransformation {
    override fun filter(text: AnnotatedString): TransformedText {
        val raw = text.text.filter { it.isDigit() || it == '+' }

        val formatted = buildString {
            if (raw.isEmpty()) return@buildString

            val hasPlus = raw.startsWith("+")
            if (hasPlus) {
                append("+")
                val digits = raw.drop(1)
                val codeLength = detectCountryCodeLength(digits)

                for (i in digits.indices) {
                    if (i == codeLength || (i > codeLength && (i - codeLength) % 3 == 0)) {
                        append(' ')
                    }
                    append(digits[i])
                }
            } else {
                // No plus: group every 3 digits
                for (i in raw.indices) {
                    if (i > 0 && i % 3 == 0) {
                        append(' ')
                    }
                    append(raw[i])
                }
            }
        }

        val offsetMapping = object : OffsetMapping {
            override fun originalToTransformed(offset: Int): Int {
                if (offset <= 0) return 0
                var original = 0
                var transformed = 0
                for (char in formatted) {
                    if (original >= offset) break
                    transformed++
                    if (char != ' ') {
                        original++
                    }
                }
                return transformed.coerceAtMost(formatted.length)
            }

            override fun transformedToOriginal(offset: Int): Int {
                if (offset <= 0) return 0
                var transformed = 0
                var original = 0
                for (char in formatted) {
                    if (transformed >= offset) break
                    transformed++
                    if (char != ' ') {
                        original++
                    }
                }
                return original.coerceAtMost(text.text.length)
            }
        }

        return TransformedText(AnnotatedString(formatted), offsetMapping)
    }

    companion object {
        // Known 2-digit country codes for European countries
        private val twoDigitCodes = setOf(
            "49", // Germany
            "48", // Poland
            "44", // UK
            "43", // Austria
            "41", // Switzerland
            "33", // France
            "31", // Netherlands
            "39", // Italy
            "34", // Spain
            "32", // Belgium
            "36", // Hungary
            "30", // Greece
            "45", // Denmark
            "46", // Sweden
            "47"  // Norway
        )

        fun detectCountryCodeLength(digits: String): Int {
            if (digits.length < 2) return digits.length
            val firstTwo = digits.take(2)
            return if (twoDigitCodes.contains(firstTwo)) 2 else 3
        }
    }
}

/**
 * Visual transformation for IBAN numbers.
 * Formats as groups of 4 characters separated by spaces: XXXX XXXX XXXX XXXX ...
 * Automatically uppercases the input.
 */
class IbanVisualTransformation : VisualTransformation {
    override fun filter(text: AnnotatedString): TransformedText {
        // Remove all spaces and uppercase
        val cleaned = text.text.replace(" ", "").uppercase()

        val formatted = buildString {
            for (i in cleaned.indices) {
                if (i > 0 && i % 4 == 0) {
                    append(' ')
                }
                append(cleaned[i])
            }
        }

        val offsetMapping = object : OffsetMapping {
            override fun originalToTransformed(offset: Int): Int {
                if (offset <= 0) return 0
                // Count how many spaces are inserted before offset
                val cleanedOffset = offset.coerceAtMost(cleaned.length)
                val spaces = if (cleanedOffset > 0) (cleanedOffset - 1) / 4 else 0
                return (cleanedOffset + spaces).coerceAtMost(formatted.length)
            }

            override fun transformedToOriginal(offset: Int): Int {
                if (offset <= 0) return 0
                var transformed = 0
                var original = 0
                for (char in formatted) {
                    if (transformed >= offset) break
                    transformed++
                    if (char != ' ') {
                        original++
                    }
                }
                return original.coerceAtMost(text.text.length)
            }
        }

        return TransformedText(AnnotatedString(formatted), offsetMapping)
    }
}
