package cz.cleansia.core.ui.components

import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.clickable
import androidx.compose.foundation.interaction.MutableInteractionSource
import androidx.compose.foundation.interaction.collectIsFocusedAsState
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.fillMaxHeight
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.text.BasicTextField
import androidx.compose.foundation.text.KeyboardOptions
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.outlined.KeyboardArrowDown
import androidx.compose.material3.Icon
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.OutlinedTextFieldDefaults
import androidx.compose.material3.Text
import androidx.compose.material3.VerticalDivider
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.focus.FocusRequester
import androidx.compose.ui.focus.focusRequester
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.SolidColor
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.semantics.Role
import androidx.compose.ui.semantics.clearAndSetSemantics
import androidx.compose.ui.semantics.contentDescription
import androidx.compose.ui.semantics.semantics
import androidx.compose.ui.text.AnnotatedString
import androidx.compose.ui.text.TextRange
import androidx.compose.ui.text.input.ImeAction
import androidx.compose.ui.text.input.KeyboardType
import androidx.compose.ui.text.input.OffsetMapping
import androidx.compose.ui.text.input.TextFieldValue
import androidx.compose.ui.text.input.TransformedText
import androidx.compose.ui.text.input.VisualTransformation
import androidx.compose.ui.unit.dp
import com.google.i18n.phonenumbers.PhoneNumberUtil
import cz.cleansia.core.R
import cz.cleansia.core.ui.theme.Spacing
import cz.cleansia.core.ui.theme.primaryText
import java.util.Locale

/**
 * Phone input with region-aware format-as-you-type.
 *
 * **The stored value is the RAW input** — digits and an optional leading plus. The pretty formatting is a
 * visual transformation only. Storing raw avoids round-tripping spaces with the backend and lets
 * validators check digits without parsing.
 *
 * With [countryRegion], the field draws that country's calling code before the national number, and the value
 * stays `+` and digits.
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
    countryRegion: String? = null,
) {
    if (countryRegion != null) {
        PrefixedPhoneField(value, onValueChange, label, countryRegion, modifier, helper, errorText, enabled)
        return
    }
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
            focusedLabelColor = primaryText(),
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

@Composable
private fun PrefixedPhoneField(
    value: String,
    onValueChange: (String) -> Unit,
    label: String,
    region: String,
    modifier: Modifier,
    helper: String?,
    errorText: String?,
    enabled: Boolean,
) {
    val code = remember(region) { callingCode(region) }
    var freeForm by remember { mutableStateOf(false) }
    val prefix = code?.takeUnless { freeForm }
    val national = prefix?.let { nationalPart(value, it) }
    val transformation = remember(region) { PhoneVisualTransformation(PhoneNumberUtil.getInstance(), region.uppercase()) }
    val focus = remember { FocusRequester() }
    val interaction = remember { MutableInteractionSource() }
    val focused by interaction.collectIsFocusedAsState()
    val countryCode = stringResource(R.string.core_phone_country_code)
    val scheme = MaterialTheme.colorScheme
    val shown = national ?: value
    var lastEdit by remember { mutableStateOf(TextFieldValue(shown, TextRange(shown.length))) }
    // A text the field did not type itself, the switch to the free form or its seeded +, takes the cursor to its
    // end; kept where it was, it would sit before the + and the next digit would push the + out.
    val fieldValue = if (lastEdit.text == shown) lastEdit else TextFieldValue(shown, TextRange(shown.length))
    Column(modifier.fillMaxWidth()) {
        Row(
            Modifier
                .fillMaxWidth()
                .height(52.dp)
                .clip(MaterialTheme.shapes.small)
                .background(scheme.surface)
                .border(
                    width = if (focused) 2.dp else 1.dp,
                    color = when {
                        errorText != null -> scheme.error
                        focused -> scheme.primary
                        else -> scheme.outline
                    },
                    shape = MaterialTheme.shapes.small,
                ),
            verticalAlignment = Alignment.CenterVertically,
        ) {
            if (prefix != null && national != null) {
                Row(
                    Modifier
                        .fillMaxHeight()
                        .clickable(enabled = enabled, role = Role.Button) {
                            freeForm = true
                            if (value.isEmpty()) onValueChange("+")
                            focus.requestFocus()
                        }
                        .clearAndSetSemantics { contentDescription = "$countryCode $prefix" }
                        .padding(horizontal = Spacing.S),
                    horizontalArrangement = Arrangement.spacedBy(Spacing.XXS),
                    verticalAlignment = Alignment.CenterVertically,
                ) {
                    Text(
                        text = prefix,
                        style = MaterialTheme.typography.titleLarge.copy(fontFeatureSettings = "tnum"),
                        color = scheme.onSurface,
                    )
                    Icon(
                        Icons.Outlined.KeyboardArrowDown,
                        contentDescription = null,
                        modifier = Modifier.size(16.dp),
                        tint = scheme.onSurfaceVariant,
                    )
                }
                VerticalDivider(color = scheme.outline)
            }
            BasicTextField(
                value = fieldValue,
                onValueChange = { edit ->
                    lastEdit = edit
                    onValueChange(
                        if (prefix != null && national != null) wireFromTyped(edit.text, prefix) else sanitizePhoneInput(edit.text),
                    )
                },
                modifier = Modifier
                    .weight(1f)
                    .fillMaxHeight()
                    .focusRequester(focus)
                    .semantics { contentDescription = label },
                enabled = enabled,
                textStyle = MaterialTheme.typography.bodyLarge.copy(color = scheme.onSurface, fontFeatureSettings = "tnum"),
                keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Phone, imeAction = ImeAction.Done),
                singleLine = true,
                visualTransformation = transformation,
                interactionSource = interaction,
                cursorBrush = SolidColor(scheme.primary),
                decorationBox = { inner ->
                    Box(
                        Modifier
                            .fillMaxHeight()
                            .padding(horizontal = Spacing.M),
                        contentAlignment = Alignment.CenterStart,
                    ) { inner() }
                },
            )
        }
        (errorText ?: helper)?.let {
            Text(
                text = it,
                style = MaterialTheme.typography.bodySmall,
                color = if (errorText != null) scheme.error else scheme.onSurfaceVariant,
                modifier = Modifier.padding(top = 4.dp, start = 4.dp),
            )
        }
    }
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
 * International form, and a length libphonenumber holds possible for the country. Not whether the number is
 * valid: the server's metadata is newer than this library's, so a number in a range allocated since would be
 * refused here with no way past it. On the partner's Personal save the server (`PhoneNumberAttribute`) judges
 * validity; the customer's profile save checks only that no other account holds the number, so there a possible
 * length is the only check. The iOS twin is in `PhoneNumberKitDisplayFormatter`.
 */
fun isPossiblePhoneNumber(wire: String): Boolean = wire.startsWith("+") && runCatching {
    val util = PhoneNumberUtil.getInstance()
    util.isPossibleNumber(util.parse(wire, null))
}.getOrDefault(false)

/** "+420" for "CZ"; null for a region libphonenumber does not know. */
internal fun callingCode(region: String): String? =
    PhoneNumberUtil.getInstance().getCountryCodeForRegion(region.uppercase()).takeIf { it != 0 }?.let { "+$it" }

/** The digits shown behind the prefix, or null when the value is a number the prefix cannot carry. */
internal fun nationalPart(value: String, code: String): String? = when {
    value.isEmpty() -> ""
    value.startsWith(code) -> value.removePrefix(code)
    else -> null
}

/** A national number goes out behind its code; one that arrives with its own + is whole; an emptied field is empty. */
internal fun wireFromTyped(typed: String, code: String): String {
    val clean = sanitizePhoneInput(typed)
    return when {
        clean.startsWith("+") -> clean
        clean.isEmpty() -> ""
        else -> code + clean
    }
}

/** The number grouped as it is typed, for showing beside the field. */
fun formatPhoneAsTyped(wire: String): String =
    PhoneVisualTransformation(PhoneNumberUtil.getInstance(), "ZZ").filter(AnnotatedString(wire)).text.text

/**
 * Digits, and a `+` only while nothing has been kept before it: a pasted " +420 728 089 247",
 * "Tel: +420 728 089 247" or "(+420) 728 089 247" keeps its country code as "+420728089247", and
 * "(420) 728 089 247" stays "420728089247". The iOS twin is `PhoneNumberSanitizer.sanitize`.
 */
fun sanitizePhoneInput(input: String): String {
    val builder = StringBuilder()
    for (ch in input) {
        when {
            ch == '+' && builder.isEmpty() -> builder.append(ch)
            ch.isDigit() -> builder.append(ch)
        }
    }
    return builder.toString()
}
