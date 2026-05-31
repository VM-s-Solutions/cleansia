package cz.cleansia.core.ui.components

import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.outlined.Visibility
import androidx.compose.material.icons.outlined.VisibilityOff
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.OutlinedTextFieldDefaults
import androidx.compose.material3.Text
import androidx.compose.ui.graphics.Color
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Modifier
import androidx.compose.ui.text.input.KeyboardType
import androidx.compose.ui.text.input.PasswordVisualTransformation
import androidx.compose.ui.text.input.VisualTransformation
import androidx.compose.ui.unit.dp
import androidx.compose.foundation.text.KeyboardOptions

/**
 * Float-label text field — matches the web's `cleansia-text-input [floatVariant]="'on'"` pattern.
 * Label sits inside the field at body size when empty, animates up to a 12sp label on focus/fill.
 * Material 3 OutlinedTextField gives us this behaviour for free; we just wrap it for consistency.
 */
@Composable
fun CleansiaTextField(
    value: String,
    onValueChange: (String) -> Unit,
    label: String,
    modifier: Modifier = Modifier,
    helper: String? = null,
    errorText: String? = null,
    keyboardType: KeyboardType = KeyboardType.Text,
    isPassword: Boolean = false,
    enabled: Boolean = true,
    singleLine: Boolean = true,
    /**
     * When true the field draws no background fill — relies on the
     * surrounding container's color instead. Use inside a FormSection
     * card (white-on-white would just look like one big fill block);
     * default false keeps standalone fields filled with surface so
     * they don't disappear on the page background.
     */
    transparentContainer: Boolean = false,
) {
    var passwordVisible by remember { mutableStateOf(false) }
    val visual = if (isPassword && !passwordVisible) PasswordVisualTransformation() else VisualTransformation.None
    val isError = errorText != null

    OutlinedTextField(
        value = value,
        onValueChange = onValueChange,
        modifier = modifier.fillMaxWidth(),
        label = { Text(label) },
        enabled = enabled,
        singleLine = singleLine,
        isError = isError,
        visualTransformation = visual,
        keyboardOptions = KeyboardOptions(
            keyboardType = if (isPassword) KeyboardType.Password else keyboardType,
        ),
        supportingText = if (errorText != null || helper != null) {
            { Text(errorText ?: helper.orEmpty()) }
        } else null,
        trailingIcon = if (isPassword) {
            {
                IconButton(onClick = { passwordVisible = !passwordVisible }) {
                    Icon(
                        imageVector = if (passwordVisible) Icons.Outlined.VisibilityOff else Icons.Outlined.Visibility,
                        contentDescription = if (passwordVisible) "Hide password" else "Show password",
                        modifier = Modifier.size(20.dp),
                    )
                }
            }
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
