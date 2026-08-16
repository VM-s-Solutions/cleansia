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
import androidx.compose.ui.text.style.TextAlign
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
            // Prefix is right-aligned and narrow: it is optional and usually empty, so left-aligning it
            // would leave a gap before the dash and break the account's shape.
            AccountSegment(
                value = prefix,
                onValueChange = { onPrefixChange(it.filter(Char::isDigit).take(PrefixMaxLength)) },
                interactionSource = prefixInteraction,
                enabled = enabled,
                modifier = Modifier.width(56.dp),
                textAlign = TextAlign.End,
            )
            Separator("–")
            AccountSegment(
                value = number,
                onValueChange = { onNumberChange(it.filter(Char::isDigit).take(NumberMaxLength)) },
                interactionSource = numberInteraction,
                enabled = enabled,
                modifier = Modifier.weight(1f),
            )
            Separator("/")
            AccountSegment(
                value = bankCode,
                onValueChange = { onBankCodeChange(it.filter(Char::isDigit).take(BankCodeMaxLength)) },
                interactionSource = bankCodeInteraction,
                enabled = enabled,
                modifier = Modifier.width(44.dp),
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

@Composable
private fun AccountSegment(
    value: String,
    onValueChange: (String) -> Unit,
    interactionSource: MutableInteractionSource,
    enabled: Boolean,
    modifier: Modifier = Modifier,
    textAlign: TextAlign = TextAlign.Start,
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
            textAlign = textAlign,
        ),
        cursorBrush = SolidColor(MaterialTheme.colorScheme.primary),
        keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Number),
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
