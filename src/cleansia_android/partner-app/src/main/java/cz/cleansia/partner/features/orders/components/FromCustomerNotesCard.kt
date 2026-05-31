package cz.cleansia.partner.features.orders.components

import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.outlined.ChatBubbleOutline
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.ui.Modifier
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.unit.dp
import cz.cleansia.partner.R

/**
 * Read-only display of any free-text the customer provided when placing
 * the order. Today the partner DTO carries three optional fields and we
 * render them as labelled paragraphs:
 *
 *  - `notes` → "From customer"
 *  - `accessInstructions` → "Access"
 *  - `specialInstructions` → "Special instructions"
 *
 * The whole card is hidden by the parent when all three are blank.
 */
@Composable
fun FromCustomerNotesCard(
    customerNotes: String?,
    accessInstructions: String?,
    specialInstructions: String?,
    modifier: Modifier = Modifier,
) {
    OrderSectionCard(
        title = stringResource(R.string.from_customer_section_title),
        icon = Icons.Outlined.ChatBubbleOutline,
        modifier = modifier,
    ) {
        Column(verticalArrangement = Arrangement.spacedBy(10.dp)) {
            customerNotes?.takeIf { it.isNotBlank() }?.let {
                NoteBlock(label = stringResource(R.string.note_general_label), body = it)
            }
            accessInstructions?.takeIf { it.isNotBlank() }?.let {
                NoteBlock(label = stringResource(R.string.note_access_label), body = it)
            }
            specialInstructions?.takeIf { it.isNotBlank() }?.let {
                NoteBlock(label = stringResource(R.string.note_special_label), body = it)
            }
        }
    }
}

@Composable
private fun NoteBlock(label: String, body: String) {
    Column(modifier = Modifier.fillMaxWidth()) {
        Text(
            text = label,
            style = MaterialTheme.typography.labelSmall,
            color = MaterialTheme.colorScheme.onSurfaceVariant,
        )
        Spacer(Modifier.height(2.dp))
        Text(
            text = body,
            style = MaterialTheme.typography.bodyMedium,
            color = MaterialTheme.colorScheme.onSurface,
        )
    }
}
