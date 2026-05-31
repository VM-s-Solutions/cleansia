package cz.cleansia.core.ui.components

import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.OutlinedTextFieldDefaults
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.remember
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.text.input.KeyboardType
import androidx.compose.ui.text.input.OffsetMapping
import androidx.compose.ui.text.input.TransformedText
import androidx.compose.ui.text.input.VisualTransformation
import androidx.compose.ui.text.AnnotatedString
import androidx.compose.ui.unit.dp
import androidx.compose.foundation.text.KeyboardOptions
import com.google.i18n.phonenumbers.PhoneNumberUtil
import java.util.Locale

/**
 * Phone input with region-aware format-as-you-type. Wraps an
 * [OutlinedTextField] in the same chrome as [CleansiaTextField] so it
 * sits next to other fields without visual mismatch.
 *
 * The underlying stored value (what [onValueChange] emits, and what
 * [value] should contain) is the raw user input — digits and an
 * optional leading `+`. The pretty formatting (`+420 728 089 247`) is
 * applied through a [VisualTransformation] driven by libphonenumber's
 * [AsYouTypeFormatter]. Storing raw avoids round-tripping spaces with
 * the backend and lets validators check digits without parsing.
 *
 * Region selection: caller passes [defaultRegion] (ISO-3166 alpha-2)
 * as the fallback when the value doesn't start with `+`. The
 * formatter picks the region from the country code prefix as soon as
 * the user types one, so a CZ user pasting a Slovak number gets SK
 * formatting automatically.
 */
@Composable
fun CleansiaPhoneInput(
    value: String,
    onValueChange: (String) -> Unit,
    label: String,
    modifier: Modifier = Modifier,
    helper: String? = null,
    errorText: String? = null,
    enabled: Boolean = true,
    transparentContainer: Boolean = false,
    defaultRegion: String = Locale.getDefault().country.ifBlank { "US" },
) {
    val phoneUtil = remember { PhoneNumberUtil.getInstance() }
    val visualTransformation = remember(defaultRegion) {
        PhoneVisualTransformation(phoneUtil, defaultRegion.uppercase())
    }
    val isError = errorText != null

    OutlinedTextField(
        value = value,
        onValueChange = { input ->
            // Keep only digits and an optional leading +. Strips
            // accidental letters / format chars the user paste might
            // contain, so the stored value is always submission-ready.
            val cleaned = sanitizePhoneInput(input)
            onValueChange(cleaned)
        },
        modifier = modifier.fillMaxWidth(),
        label = { Text(label) },
        enabled = enabled,
        singleLine = true,
        isError = isError,
        visualTransformation = visualTransformation,
        keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Phone),
        supportingText = if (errorText != null || helper != null) {
            { Text(errorText ?: helper.orEmpty()) }
        } else null,
        shape = RoundedCornerShape(12.dp),
        colors = OutlinedTextFieldDefaults.colors(
            focusedBorderColor = MaterialTheme.colorScheme.primary,
            unfocusedBorderColor = MaterialTheme.colorScheme.outline,
            focusedLabelColor = MaterialTheme.colorScheme.primary,
            cursorColor = MaterialTheme.colorScheme.primary,
            focusedContainerColor = if (transparentContainer) Color.Transparent
            else MaterialTheme.colorScheme.surface,
            unfocusedContainerColor = if (transparentContainer) Color.Transparent
            else MaterialTheme.colorScheme.surface,
            disabledContainerColor = if (transparentContainer) Color.Transparent
            else MaterialTheme.colorScheme.surface,
        ),
    )
}

/**
 * Format-as-you-type for the visible text using libphonenumber's
 * [AsYouTypeFormatter]. The transformation has to keep cursor offset
 * mapping coherent — for an input of length N, [OffsetMapping] maps
 * each raw-text index to the corresponding index in the formatted
 * output (and back). We compute the mapping table once per format
 * call by replaying digits one at a time and recording where each
 * one lands in the formatted output.
 */
private class PhoneVisualTransformation(
    private val phoneUtil: PhoneNumberUtil,
    private val defaultRegion: String,
) : VisualTransformation {
    override fun filter(text: AnnotatedString): TransformedText {
        val raw = text.text
        if (raw.isEmpty()) {
            return TransformedText(AnnotatedString(""), OffsetMapping.Identity)
        }
        // Choose region from a leading + prefix when present; otherwise
        // fall back to caller-provided default. The formatter is
        // single-use — we instantiate per filter call.
        val region = if (raw.startsWith("+")) "ZZ" else defaultRegion
        val formatter = phoneUtil.getAsYouTypeFormatter(region)
        formatter.clear()

        val formattedBuilder = StringBuilder()
        // rawIndexToFormatted[i] = index in formattedBuilder.length right
        // after consuming raw[i]. Used for offset mapping.
        val rawIndexToFormatted = IntArray(raw.length + 1)
        rawIndexToFormatted[0] = 0
        for (i in raw.indices) {
            val ch = raw[i]
            if (ch == '+' || ch.isDigit()) {
                val partial = formatter.inputDigit(ch)
                formattedBuilder.clear()
                formattedBuilder.append(partial)
            } else {
                // Non-input chars shouldn't appear (we sanitize at the
                // text-field level), but if one slips through we just
                // keep the previous formatted snapshot.
            }
            rawIndexToFormatted[i + 1] = formattedBuilder.length
        }
        val formatted = formattedBuilder.toString()

        val offsetMapping = object : OffsetMapping {
            override fun originalToTransformed(offset: Int): Int {
                val clamped = offset.coerceIn(0, raw.length)
                return rawIndexToFormatted[clamped]
            }

            override fun transformedToOriginal(offset: Int): Int {
                val clamped = offset.coerceIn(0, formatted.length)
                // Find first raw index whose formatted-end >= clamped.
                for (i in rawIndexToFormatted.indices) {
                    if (rawIndexToFormatted[i] >= clamped) return i
                }
                return raw.length
            }
        }

        return TransformedText(AnnotatedString(formatted), offsetMapping)
    }
}

/**
 * Strip everything except digits and a single optional leading `+`.
 * Tolerates pasted "(420) 728 089 247" or "+420-728-089-247" by
 * normalizing to "+420728089247".
 */
private fun sanitizePhoneInput(input: String): String {
    if (input.isEmpty()) return ""
    val builder = StringBuilder()
    var seenPlus = false
    for ((i, ch) in input.withIndex()) {
        when {
            ch == '+' && i == 0 && !seenPlus -> {
                builder.append('+')
                seenPlus = true
            }
            ch.isDigit() -> builder.append(ch)
            else -> { /* drop */ }
        }
    }
    return builder.toString()
}
