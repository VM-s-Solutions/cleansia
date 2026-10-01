package cz.cleansia.core.ui.components

import androidx.compose.foundation.border
import androidx.compose.foundation.interaction.MutableInteractionSource
import androidx.compose.foundation.interaction.collectIsFocusedAsState
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.text.BasicTextField
import androidx.compose.foundation.text.KeyboardOptions
import androidx.compose.material3.LocalTextStyle
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.remember
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.SolidColor
import androidx.compose.ui.text.input.KeyboardType
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp

/** Czech account maxima — prefix 6 digits, number 10, bank code 4. */
private const val PrefixMaxLength = 6
private const val NumberMaxLength = 10
private const val BankCodeMaxLength = 4

/**
 * A Czech bank account entered as ONE control: `prefix – number / bank code`.
 *
 * Three fields, one border. The separators are drawn rather than typed, because the format belongs to
 * the bank and not to the person copying an account off a statement.
 *
 * **The border and the focus colour belong to the Row, never to a segment.** A focus outline around one
 * third of the control would undo the grouping the control exists to create — which is the whole point:
 * the account is one thing to the person entering it, even though it is three columns to the server.
 *
 * It takes and returns the three values separately rather than one joined string. They are three
 * columns server-side, each with its own validation, and joining them here would mean splitting them
 * again on save — a round trip that can only lose information.
 *
 * **Each segment carries its own placeholder**, because one border around three boxes removes the only
 * other cue for which box is which. Czech online banking (Raiffeisen among them) names all three in
 * place; without that, an empty control is three anonymous gaps around a dash and a slash. The segment
 * widths below are sized to the *placeholder*, not to the digits, for the same reason — a hint that is
 * clipped to "Předčí…" answers nothing.
 *
 * **A whole account pasted into any segment is spread over all three** ([splitPastedAccount]), so an
 * account copied off a statement or a banking app lands where it belongs instead of being clamped into
 * one box.
 *
 * The web twin is `cleansia-bank-account`; keep the two in step.
 */
@Composable
fun CleansiaBankAccountInput(
    prefix: String,
    number: String,
    bankCode: String,
    onPrefixChange: (String) -> Unit,
    onNumberChange: (String) -> Unit,
    onBankCodeChange: (String) -> Unit,
    label: String,
    modifier: Modifier = Modifier,
    prefixPlaceholder: String? = null,
    numberPlaceholder: String? = null,
    bankCodePlaceholder: String? = null,
    helper: String? = null,
    errorText: String? = null,
    enabled: Boolean = true,
) {
    // One interaction source per segment, but the focus state is OR-ed: any segment focused lights the
    // whole control, which is what makes three inputs read as one.
    val prefixInteraction = remember { MutableInteractionSource() }
    val numberInteraction = remember { MutableInteractionSource() }
    val bankCodeInteraction = remember { MutableInteractionSource() }

    val prefixFocused by prefixInteraction.collectIsFocusedAsState()
    val numberFocused by numberInteraction.collectIsFocusedAsState()
    val bankCodeFocused by bankCodeInteraction.collectIsFocusedAsState()
    val focused = prefixFocused || numberFocused || bankCodeFocused

    val isError = errorText != null
    val borderColor = when {
        isError -> MaterialTheme.colorScheme.error
        focused -> MaterialTheme.colorScheme.primary
        else -> MaterialTheme.colorScheme.outline
    }

    // A paste that reads as a whole account fills the segments it names; anything else is clamped into
    // the segment it went into, exactly as typing is.
    fun segmentChange(current: String, maxLength: Int, onChange: (String) -> Unit): (String) -> Unit = { raw ->
        val split = splitPastedAccount(raw, current)
        if (split == null) {
            onChange(raw.filter(Char::isDigit).take(maxLength))
        } else {
            split.first?.let(onPrefixChange)
            onNumberChange(split.second)
            split.third?.let(onBankCodeChange)
        }
    }

    Column(modifier = modifier.fillMaxWidth()) {
        Text(
            text = label,
            style = MaterialTheme.typography.bodySmall,
            color = if (isError) MaterialTheme.colorScheme.error else MaterialTheme.colorScheme.onSurfaceVariant,
            modifier = Modifier.padding(bottom = 4.dp),
        )

        Row(
            modifier = Modifier
                .fillMaxWidth()
                .border(
                    width = if (focused) 2.dp else 1.dp,
                    color = borderColor,
                    shape = RoundedCornerShape(12.dp),
                )
                .padding(horizontal = 12.dp, vertical = 14.dp),
            verticalAlignment = Alignment.CenterVertically,
            horizontalArrangement = Arrangement.Start,
        ) {
            // Every segment reads from the left, placeholder and digits alike. Right-aligning the
            // prefix kept its digits against the dash, but it also right-aligned its hint, so the three
            // labels in an empty control started at three different places. Its width is set by the
            // longest placeholder we ship ("Predčíslie"), not by its six digits.
            AccountSegment(
                value = prefix,
                onValueChange = segmentChange(prefix, PrefixMaxLength, onPrefixChange),
                interactionSource = prefixInteraction,
                enabled = enabled,
                placeholder = prefixPlaceholder,
                modifier = Modifier.width(76.dp),
            )
            Separator("–")
            AccountSegment(
                value = number,
                onValueChange = segmentChange(number, NumberMaxLength, onNumberChange),
                interactionSource = numberInteraction,
                enabled = enabled,
                placeholder = numberPlaceholder,
                modifier = Modifier.weight(1f),
            )
            Separator("/")
            AccountSegment(
                value = bankCode,
                onValueChange = segmentChange(bankCode, BankCodeMaxLength, onBankCodeChange),
                interactionSource = bankCodeInteraction,
                enabled = enabled,
                placeholder = bankCodePlaceholder,
                modifier = Modifier.width(52.dp),
            )
        }

        if (errorText != null || helper != null) {
            Text(
                text = errorText ?: helper.orEmpty(),
                style = MaterialTheme.typography.bodySmall,
                color = if (isError) MaterialTheme.colorScheme.error else MaterialTheme.colorScheme.onSurfaceVariant,
                modifier = Modifier.padding(top = 4.dp, start = 4.dp),
            )
        }
    }
}

private val domesticAccount = Regex("""^(?:(\d{1,6})-)?(\d{1,10})(?:/(\d{1,4}))?$""")
private val czSkIban = Regex("""^(?:CZ|SK)\d{2}(\d{20})$""")

/**
 * Reads a whole Czech/Slovak account out of pasted text, as (prefix, number, bank code); a null part
 * means "leave that segment as it is", and a null result means the change is not a pasted account.
 *
 * - Only a change that lands two or more characters over [current] at once is a paste — a number pad
 *   types one at a time, and typing must never jump segments.
 * - Whitespace of every kind (NBSP included) is dropped, en and em dashes count as `-`.
 * - `[prefix-]number[/bank code]` with a separator is an account written out: the parts it names are
 *   taken and a missing prefix means none (cleared); a missing bank code keeps the one already entered.
 * - A bare number goes to the number segment and leaves prefix and bank code alone.
 * - A CZ or SK IBAN is decomposed from its BBAN — bank code, prefix, number — with the leading zeros
 *   the IBAN pads them with removed.
 *
 * It only splits text. Whether the account is valid (mod-11, the bank code) stays with the server.
 * The iOS twin is `CleansiaBankAccountField`; keep the rules identical. -> /partner-app/onboarding
 */
internal fun splitPastedAccount(raw: String, current: String = ""): Triple<String?, String, String?>? {
    if (raw.length - current.length < 2) return null
    val text = raw.filterNot(Char::isWhitespace).replace('\u2013', '-').replace('\u2014', '-').uppercase()
    czSkIban.matchEntire(text)?.let { iban ->
        val bban = iban.groupValues[1]
        return Triple(bban.substring(4, 10).trimStart('0'), bban.substring(10).trimStart('0'), bban.substring(0, 4))
    }
    val account = domesticAccount.matchEntire(text) ?: return null
    val prefix = account.groups[1]?.value
    val bankCode = account.groups[3]?.value
    val writtenOut = prefix != null || bankCode != null
    return Triple(if (writtenOut) prefix.orEmpty() else null, account.groupValues[2], bankCode)
}

/**
 * One segment. The placeholder stays on screen while the segment is empty — including while it is
 * focused — because the person is mid-way through an account number and "which box am I in" is exactly
 * the question a caret does not answer.
 */
@Composable
private fun AccountSegment(
    value: String,
    onValueChange: (String) -> Unit,
    interactionSource: MutableInteractionSource,
    enabled: Boolean,
    modifier: Modifier = Modifier,
    placeholder: String? = null,
) {
    BasicTextField(
        value = value,
        onValueChange = onValueChange,
        modifier = modifier,
        enabled = enabled,
        singleLine = true,
        interactionSource = interactionSource,
        textStyle = LocalTextStyle.current.copy(
            color = MaterialTheme.colorScheme.onSurface,
            fontSize = 16.sp,
        ),
        cursorBrush = SolidColor(MaterialTheme.colorScheme.primary),
        keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Number),
        decorationBox = { innerTextField ->
            Box(contentAlignment = Alignment.CenterStart) {
                if (value.isEmpty() && !placeholder.isNullOrBlank()) {
                    Text(
                        text = placeholder,
                        style = MaterialTheme.typography.bodyMedium,
                        color = MaterialTheme.colorScheme.onSurfaceVariant,
                        maxLines = 1,
                        overflow = TextOverflow.Ellipsis,
                    )
                }
                innerTextField()
            }
        },
    )
}

@Composable
private fun Separator(text: String) {
    Box(modifier = Modifier.padding(horizontal = 6.dp)) {
        Text(
            text = text,
            style = MaterialTheme.typography.bodyLarge,
            color = MaterialTheme.colorScheme.onSurfaceVariant,
        )
    }
}
