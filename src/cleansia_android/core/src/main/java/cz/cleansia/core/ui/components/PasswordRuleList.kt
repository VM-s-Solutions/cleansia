package cz.cleansia.core.ui.components

import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.outlined.Check
import androidx.compose.material.icons.outlined.Close
import androidx.compose.material.icons.outlined.RadioButtonUnchecked
import androidx.compose.material3.Icon
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.unit.dp
import cz.cleansia.core.ui.theme.ErrorText
import cz.cleansia.core.ui.theme.SuccessText

/**
 * Live-feedback list of password rules: green check when the rule passes, red close once the user has
 * typed and it still fails, neutral circle while untouched.
 *
 * **The untouched state matters** — showing every rule as failing before the user types reads as an
 * error they caused.
 */
@Composable
fun PasswordRuleList(
    rules: List<Pair<String, Boolean>>,
    hasInput: Boolean,
    modifier: Modifier = Modifier,
) {
    Column(
        modifier = modifier
            .fillMaxWidth()
            .padding(horizontal = 4.dp, vertical = 4.dp),
        verticalArrangement = Arrangement.spacedBy(2.dp),
    ) {
        rules.forEach { (text, valid) ->
            PasswordRuleRow(text = text, valid = valid, hasInput = hasInput)
        }
    }
}

@Composable
private fun PasswordRuleRow(text: String, valid: Boolean, hasInput: Boolean) {
    val (icon, tint) = when {
        valid -> Icons.Outlined.Check to SuccessText
        hasInput -> Icons.Outlined.Close to ErrorText
        else -> Icons.Outlined.RadioButtonUnchecked to MaterialTheme.colorScheme.onSurfaceVariant
    }
    Row(
        verticalAlignment = Alignment.CenterVertically,
        horizontalArrangement = Arrangement.spacedBy(6.dp),
    ) {
        Icon(
            imageVector = icon,
            contentDescription = null,
            tint = tint,
            modifier = Modifier.size(14.dp),
        )
        Text(
            text = text,
            style = MaterialTheme.typography.bodySmall,
            color = if (valid || !hasInput) MaterialTheme.colorScheme.onSurfaceVariant else ErrorText,
        )
    }
}
