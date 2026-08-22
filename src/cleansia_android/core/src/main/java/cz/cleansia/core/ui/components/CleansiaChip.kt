package cz.cleansia.core.ui.components

import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.semantics.Role
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp

/**
 * A selectable pill. The one chip the customer app draws, wherever it offers a short closed set of
 * choices — cancellation reasons, review tags.
 *
 * **It takes a resolved [label], never a string resource id.** `:core` cannot reach an app's `R`, and
 * the two apps' copy is deliberately their own. Mirrors the iOS `CleansiaChip`.
 *
 * **Selection policy belongs to the caller, not here.** Cancellation reasons are radio-style with
 * tap-to-clear; review tags are multi-select with a cap. Both are one line at the call site, and
 * folding either into this component would make it wrong for the other — the original private version
 * hardwired single-select and could not be shared because of it.
 */
@Composable
fun CleansiaChip(
    label: String,
    isSelected: Boolean,
    onClick: () -> Unit,
    modifier: Modifier = Modifier,
    enabled: Boolean = true,
) {
    val border = if (isSelected) MaterialTheme.colorScheme.primary
                 else MaterialTheme.colorScheme.outlineVariant
    val bg = if (isSelected) MaterialTheme.colorScheme.primary.copy(alpha = 0.12f)
             else MaterialTheme.colorScheme.surface
    val labelColor = if (isSelected) MaterialTheme.colorScheme.primary
                     else MaterialTheme.colorScheme.onSurface
    Box(
        modifier = modifier
            .clip(RoundedCornerShape(999.dp))
            .background(bg)
            .border(
                width = if (isSelected) 1.5.dp else 1.dp,
                color = border,
                shape = RoundedCornerShape(999.dp),
            )
            // Role.Checkbox rather than Button: a screen reader has to announce the CURRENT state, not
            // just that the row is tappable. A multi-select chip that reads as a button gives a blind
            // customer no way to tell which tags they have already picked.
            .clickable(enabled = enabled, role = Role.Checkbox, onClick = onClick)
            .padding(horizontal = 14.dp, vertical = 8.dp),
    ) {
        Text(
            text = label,
            style = MaterialTheme.typography.bodyMedium.copy(
                fontWeight = if (isSelected) FontWeight.SemiBold else FontWeight.Normal,
            ),
            color = labelColor,
        )
    }
}
