package cz.cleansia.customer.ui.components

import androidx.compose.foundation.border
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.unit.dp
import cz.cleansia.core.ui.components.CleansiaConsentCheckbox
import cz.cleansia.customer.R

/** A card kept here guarantees cash bookings too, so the guarantee wording it is saved under is shown with the tick. */
@Composable
fun CardSavingConsent(saved: Boolean, onSavedChange: (Boolean) -> Unit) {
    Column(
        modifier = Modifier
            .fillMaxWidth()
            .clip(RoundedCornerShape(14.dp))
            .border(1.dp, MaterialTheme.colorScheme.outlineVariant, RoundedCornerShape(14.dp))
            .padding(14.dp),
    ) {
        CleansiaConsentCheckbox(
            checked = saved,
            onCheckedChange = onSavedChange,
            html = stringResource(R.string.booking_save_card_label),
            modifier = Modifier.fillMaxWidth(),
        )
        Spacer(Modifier.height(4.dp))
        Text(
            stringResource(R.string.consent_card_guarantee_draft_2026_09_28),
            style = MaterialTheme.typography.bodySmall,
            color = MaterialTheme.colorScheme.onSurfaceVariant,
        )
    }
}
