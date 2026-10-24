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
import androidx.compose.runtime.key
import androidx.compose.runtime.mutableIntStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.SolidColor
import androidx.compose.ui.text.input.KeyboardType
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp

/** Czech account maxima — prefix 6 digits, number 10, bank code 4. */
internal const val PrefixMaxLength = 6
internal const val NumberMaxLength = 10
internal const val BankCodeMaxLength = 4

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
 * **A whole account that overflows the segment it is pasted into is spread over all three, and a bare
 * number too long for that segment is the number** ([splitPastedAccount]), so an account copied off a
 * statement or a banking app lands where it belongs instead of being clamped into one box, while a
 * prefix or bank code copied on its own replaces what its own box held. A spread paste ends the edit,
 * so the keyboard closes, as on iOS.
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

    // Bumped by every paste that is spread over the segments, which rebuilds them (see the key below).
    var pasteRevision by remember { mutableIntStateOf(0) }

    val isError = errorText != null
    val borderColor = when {
        isError -> MaterialTheme.colorScheme.error
        focused -> MaterialTheme.colorScheme.primary
        else -> MaterialTheme.colorScheme.outline
    }

    fun segmentChange(maxLength: Int, onChange: (String) -> Unit): (String, String) -> Unit = { current, raw ->
        when (val edit = splitPastedAccount(raw, current, maxLength)) {
            SegmentEdit.Kept -> Unit
            is SegmentEdit.Changed -> onChange(edit.text)
            is SegmentEdit.Spread -> {
                edit.prefix?.let(onPrefixChange)
                onNumberChange(edit.number)
                edit.bankCode?.let(onBankCodeChange)
                pasteRevision++
            }
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
            // A spread paste rebuilds the three segments. BasicTextField drops a change whose text
            // equals the last one it reported for as long as its value is unchanged, and a paste
            // routed out of a box leaves that box's value as it was, so the same paste into the same
            // box a second time did nothing. The rebuild ends the edit and the keyboard goes with it,
            // as on iOS (`.id(pasteRevision)` in CleansiaBankAccountField).
            key(pasteRevision) {
                // Every segment reads from the left, placeholder and digits alike. Right-aligning the
                // prefix kept its digits against the dash, but it also right-aligned its hint, so the
                // three labels in an empty control started at three different places. Its width is set
                // by the longest placeholder we ship ("Predčíslie"), not by its six digits.
                AccountSegment(
                    value = prefix,
                    onValueChange = segmentChange(PrefixMaxLength, onPrefixChange),
                    interactionSource = prefixInteraction,
                    enabled = enabled,
                    placeholder = prefixPlaceholder,
                    modifier = Modifier.width(76.dp),
                )
                Separator("–")
                AccountSegment(
                    value = number,
                    onValueChange = segmentChange(NumberMaxLength, onNumberChange),
                    interactionSource = numberInteraction,
                    enabled = enabled,
                    placeholder = numberPlaceholder,
                    modifier = Modifier.weight(1f),
                )
                Separator("/")
                AccountSegment(
                    value = bankCode,
                    onValueChange = segmentChange(BankCodeMaxLength, onBankCodeChange),
                    interactionSource = bankCodeInteraction,
                    enabled = enabled,
                    placeholder = bankCodePlaceholder,
                    modifier = Modifier.width(52.dp),
                )
            }
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

/** What one edit of a segment does to the account. */
internal sealed interface SegmentEdit {
    /** The segment keeps the text it had. */
    data object Kept : SegmentEdit

    /** The segment's text becomes [text]. */
    data class Changed(val text: String) : SegmentEdit

    /** The account is spread over the three segments; a null part keeps what that segment holds. */
    data class Spread(val prefix: String?, val number: String, val bankCode: String?) : SegmentEdit
}

/**
 * What an edit of one segment does; [raw] is the segment's new text and [current] the text it replaces,
 * [segmentMaxLength] says which segment it is. New text that fits the segment is kept, as typed or pasted,
 * digits only. Text that overflows it is read by the run the edit put in, the new text less what it shares
 * with [current] at either end:
 * - one character is typing into a full segment, which keeps its text;
 * - a written-out account (`prefix-number/code`, `number/code`, `prefix-number`) or a CZ or SK IBAN, whose
 *   BBAN is `bankcode(4) prefix(6) number(10)` with the zero padding dropped, is spread over the three; one
 *   without a prefix clears the old one, and a missing bank code keeps it;
 * - bare digits that fit the segment become its text, so a bank code pasted beside the old one replaces it;
 * - bare digits too long for it, up to [NumberMaxLength], are the account number, and the segment keeps its
 *   own text;
 * - anything else keeps the segment's text.
 * Whitespace of every kind is dropped and an en or em dash reads as a hyphen, because that is what banking
 * apps put on the clipboard. Nothing is validated beyond shape: the server owns mod-11, the bank code and
 * the IBAN cross-check.
 *
 * The iOS twin is `CleansiaBankAccountField.splitPastedAccount` and the web's is `cleansia-bank-account`'s
 * paste handler; the three pin one case list. -> /partner-app/onboarding
 */
internal fun splitPastedAccount(raw: String, current: String, segmentMaxLength: Int): SegmentEdit {
    if (raw.length <= segmentMaxLength) return SegmentEdit.Changed(clampSegment(raw, segmentMaxLength))
    val run = insertedRun(raw, current)
    if (run.length == 1) return SegmentEdit.Kept
    val pasted = run.filterNot(Char::isWhitespace).replace('\u2013', '-').replace('\u2014', '-')
    val text = domesticFormOfIban(pasted) ?: pasted
    val account = domesticAccount.matchEntire(text)
    if (account != null && ('-' in text || '/' in text)) {
        return SegmentEdit.Spread(account.groups[1]?.value.orEmpty(), account.groupValues[2], account.groups[3]?.value)
    }
    if (pasted.isEmpty() || pasted.any { it !in '0'..'9' }) return SegmentEdit.Kept
    return when {
        pasted.length <= segmentMaxLength -> SegmentEdit.Changed(pasted)
        pasted.length <= NumberMaxLength -> SegmentEdit.Spread(null, pasted, null)
        else -> SegmentEdit.Kept
    }
}

/** [text] less the start and the end it shares with [current], which never overlap: what the edit put in. */
private fun insertedRun(text: String, current: String): String {
    val start = text.commonPrefixWith(current).length
    val end = minOf(text.commonSuffixWith(current).length, minOf(text.length, current.length) - start)
    return text.substring(start, text.length - end)
}

/** `CZ65 0800 0000 1920 0014 5399` reads as `19-2000145399/0800`; null for any other text. */
private fun domesticFormOfIban(text: String): String? {
    val bban = czSkIban.matchEntire(text.uppercase())?.groupValues?.get(1) ?: return null
    val prefix = bban.substring(4, 10).trimStart('0')
    val number = bban.substring(10).trimStart('0')
    return (if (prefix.isEmpty()) "" else "$prefix-") + "$number/${bban.substring(0, 4)}"
}

/**
 * What a segment keeps of an edit that is not an account: ASCII digits, up to [maxLength]. Not
 * `Char::isDigit`, which also takes Arabic-Indic and other Unicode digits a keyboard can type and the
 * server refuses; the paste splitter's `\d` is ASCII-only already.
 */
internal fun clampSegment(raw: String, maxLength: Int): String = raw.filter { it in '0'..'9' }.take(maxLength)

/**
 * For each text a segment composed with [value] reports, the text it reported before, and [value] for the
 * first: what the edit replaced when several arrive before the caller's [value] comes back, one
 * recomposition late (BasicTextField keeps its own `lastTextValue` for the same reason). Read against
 * [value], two edits in one frame read as one run: "0" and "1" typed into a bank code "080" put in "01",
 * which would replace the box's text, and a digit typed right after "0800" is pasted into an empty box
 * makes "08001", which would go to the number. A segment builds a new one at the composition after every
 * edit, the one in which BasicTextField shows [value] again, so the next edit is measured against the box's
 * text and not against an edit the segment kept out: one kept across a "5" typed into a full "0800" reads
 * "5500" pasted after it as "500".
 */
internal fun lastReported(value: String): (raw: String) -> String {
    var last = value
    return { raw -> last.also { last = raw } }
}

/**
 * One segment. The placeholder stays on screen while the segment is empty — including while it is
 * focused — because the person is mid-way through an account number and "which box am I in" is exactly
 * the question a caret does not answer. [onValueChange] gets the text an edit replaced and the new text.
 */
@Composable
private fun AccountSegment(
    value: String,
    onValueChange: (current: String, raw: String) -> Unit,
    interactionSource: MutableInteractionSource,
    enabled: Boolean,
    modifier: Modifier = Modifier,
    placeholder: String? = null,
) {
    var edits by remember { mutableIntStateOf(0) }
    val replaced = remember(value, edits) { lastReported(value) }
    BasicTextField(
        value = value,
        onValueChange = { raw ->
            onValueChange(replaced(raw), raw)
            edits++
        },
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
