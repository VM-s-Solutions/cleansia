package cz.cleansia.core.ui.components

import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.text.BasicTextField
import androidx.compose.foundation.text.KeyboardOptions
import androidx.compose.foundation.text.selection.LocalTextSelectionColors
import androidx.compose.foundation.text.selection.TextSelectionColors
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.CompositionLocalProvider
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.runtime.snapshotFlow
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.focus.FocusRequester
import androidx.compose.ui.focus.focusRequester
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.SolidColor
import androidx.compose.ui.platform.LocalClipboardManager
import androidx.compose.ui.platform.LocalWindowInfo
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.semantics.clearAndSetSemantics
import androidx.compose.ui.text.TextRange
import androidx.compose.ui.text.TextStyle
import androidx.compose.ui.text.input.KeyboardType
import androidx.compose.ui.text.input.TextFieldValue
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import cz.cleansia.core.R
import cz.cleansia.core.ui.theme.Poppins

/**
 * One-time-code entry: one real text field drawn as [length] digit boxes, so a tap anywhere on the boxes brings
 * the keyboard back and a long-press offers Paste. The code it reports holds ASCII digits only, up to [length]
 * ([acceptOneTimeCode]). A Paste button under the boxes is shown while the clipboard holds text, because not every
 * keyboard offers the clipboard on a number pad.
 */
@Composable
fun CodeInput(
    code: String,
    onCodeChange: (String) -> Unit,
    length: Int = 6,
    modifier: Modifier = Modifier,
) {
    val focusRequester = remember { FocusRequester() }
    val clipboard = LocalClipboardManager.current
    val windowInfo = LocalWindowInfo.current
    var clipboardHasText by remember { mutableStateOf(false) }

    // Android 10+ lets only the app holding window focus read the clipboard, and an app coming back from the mail
    // app gets focus back after ON_RESUME, so the check runs on focus. hasText() reads only the clip's description,
    // so it raises no "pasted from your clipboard" notice; getText() on the tap does, which is the paste itself.
    LaunchedEffect(windowInfo, clipboard) {
        snapshotFlow { windowInfo.isWindowFocused }.collect { focused ->
            if (focused) clipboardHasText = clipboard.hasText()
        }
    }

    Column(modifier = modifier.fillMaxWidth(), horizontalAlignment = Alignment.CenterHorizontally) {
        CompositionLocalProvider(LocalTextSelectionColors provides invisibleSelection) {
            BasicTextField(
                // The text is invisible, so the caret is held at its end: a tap on the first box would otherwise
                // put it inside a partial code, and the next digit typed would land in the middle of it.
                value = TextFieldValue(code, selection = TextRange(code.length)),
                onValueChange = { edit ->
                    if (edit.text != code) onCodeChange(acceptOneTimeCode(edit.text, code, length))
                },
                modifier = Modifier
                    .fillMaxWidth()
                    .focusRequester(focusRequester),
                keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Number),
                textStyle = TextStyle(color = Color.Transparent),
                cursorBrush = SolidColor(Color.Transparent),
                decorationBox = { innerTextField ->
                    Box {
                        CodeBoxes(code = code, length = length)
                        Box(modifier = Modifier.matchParentSize()) { innerTextField() }
                    }
                },
            )
        }

        if (clipboardHasText) {
            CleansiaTextButton(
                onClick = {
                    val pasted = acceptOneTimeCode(clipboard.getText()?.text.orEmpty(), "", length)
                    if (pasted.isNotEmpty()) onCodeChange(pasted)
                },
            ) {
                Text(stringResource(R.string.core_code_paste))
            }
        }
    }

    LaunchedEffect(Unit) { focusRequester.requestFocus() }
}

private val invisibleSelection = TextSelectionColors(handleColor = Color.Transparent, backgroundColor = Color.Transparent)

/** Hidden from accessibility: the field itself reads the code out, so the boxes would only repeat it digit by digit. */
@Composable
private fun CodeBoxes(code: String, length: Int) {
    Row(
        modifier = Modifier
            .fillMaxWidth()
            .clearAndSetSemantics { },
        horizontalArrangement = Arrangement.spacedBy(8.dp, Alignment.CenterHorizontally),
    ) {
        repeat(length) { index ->
            val char = code.getOrNull(index)?.toString() ?: ""
            val focused = index == code.length
            val borderColor = if (focused) MaterialTheme.colorScheme.primary
            else MaterialTheme.colorScheme.outline
            Box(
                modifier = Modifier
                    .width(44.dp)
                    .height(56.dp)
                    .border(
                        width = if (focused) 2.dp else 1.dp,
                        color = borderColor,
                        shape = RoundedCornerShape(12.dp),
                    )
                    .background(
                        color = MaterialTheme.colorScheme.surface,
                        shape = RoundedCornerShape(12.dp),
                    ),
                contentAlignment = Alignment.Center,
            ) {
                Text(
                    text = char,
                    style = MaterialTheme.typography.headlineMedium.copy(
                        fontFamily = Poppins,
                        fontSize = 24.sp,
                    ),
                    color = MaterialTheme.colorScheme.onSurface,
                )
            }
        }
    }
}

/**
 * The code after an edit that turned [current] into [raw]. A run of at least [length] digits arriving in one edit
 * is a whole code (pasted, or dropped in by the keyboard's clipboard chip) and replaces the code; any other edit
 * is applied as typed. Either way only ASCII digits are kept, up to [length]: the server refuses Arabic-Indic and
 * full-width ones a keyboard can type. Typing never brings [length] digits in one edit, even several digits
 * between two recompositions, so it cannot take the replace path.
 *
 * The iOS twin is `OneTimeCode.accept`; the two are held to one case list.
 */
internal fun acceptOneTimeCode(raw: String, current: String, length: Int): String {
    val kept = raw.commonPrefixWith(current).length
    val rest = raw.substring(kept)
    val inserted = rest.dropLast(rest.commonSuffixWith(current.substring(kept)).length).asciiDigits()
    return if (inserted.length >= length) inserted.take(length) else raw.asciiDigits().take(length)
}

private fun String.asciiDigits(): String = filter { it in '0'..'9' }
