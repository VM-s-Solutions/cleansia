package cz.cleansia.core.ui.components

import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.text.BasicTextField
import androidx.compose.foundation.text.KeyboardOptions
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.remember
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.focus.FocusRequester
import androidx.compose.ui.focus.focusRequester
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.text.TextStyle
import androidx.compose.ui.text.input.KeyboardType
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import cz.cleansia.core.ui.theme.Poppins

/**
 * OTP-style numeric code input — `length` separate digit boxes with a hidden
 * BasicTextField underneath capturing the actual input. Focused box gets a
 * 2dp primary-coloured border; the rest get 1dp outline. Digits render in
 * Poppins 24sp.
 *
 * Behavior:
 *  - Hidden input is auto-focused on first composition.
 *  - Callers should filter [onCodeChange] to digits + cap at [length]; this
 *    composable doesn't enforce either (some flows might want alphanumeric).
 *  - Auto-submission on completion (length-reached) is the caller's job —
 *    wrap with `LaunchedEffect(code) { if (code.length == LENGTH) onSubmit(code) }`.
 *
 * Originated as a private composable in customer-app's EmailVerifyScreen;
 * extracted to `:core` so partner-app's ConfirmEmailScreen can render the
 * same widget.
 */
@Composable
fun CodeInput(
    code: String,
    onCodeChange: (String) -> Unit,
    length: Int = 6,
    modifier: Modifier = Modifier,
) {
    val focusRequester = remember { FocusRequester() }

    Box(modifier = modifier.fillMaxWidth()) {
        BasicTextField(
            value = code,
            onValueChange = onCodeChange,
            modifier = Modifier
                .focusRequester(focusRequester)
                .size(1.dp),
            keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Number),
            // Transparent so the BasicTextField's caret + own glyph don't show
            // through behind the boxed digits.
            textStyle = TextStyle(color = Color.Transparent),
        )

        Row(
            modifier = Modifier.fillMaxWidth(),
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

    LaunchedEffect(Unit) { focusRequester.requestFocus() }
}
