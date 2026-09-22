package cz.cleansia.partner.features.orders

import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.outlined.Description
import androidx.compose.material3.Icon
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Surface
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import cz.cleansia.core.format.formatOrderDateTime
import cz.cleansia.core.ui.components.CleansiaPrimaryButton
import cz.cleansia.core.ui.components.CleansiaTextLink
import cz.cleansia.core.ui.theme.Spacing
import cz.cleansia.partner.R

/**
 * The cleaner's standing on the contract for work: the acceptance line with a way to read the
 * accepted text, or — for a seat an administrator placed — the prompt to accept before starting.
 * Nothing renders for a cleaner who is not on the crew; the take path carries its own contract.
 */
@Composable
fun WorkContractCard(
    standing: WorkContractStanding,
    onAccept: () -> Unit,
    onRead: (acceptanceId: String) -> Unit,
    modifier: Modifier = Modifier,
) {
    when (standing) {
        WorkContractStanding.None -> Unit
        WorkContractStanding.Pending -> ContractSurface(
            container = MaterialTheme.colorScheme.tertiaryContainer,
            content = MaterialTheme.colorScheme.onTertiaryContainer,
            modifier = modifier,
        ) {
            Text(
                text = stringResource(R.string.work_contract_pending_banner),
                style = MaterialTheme.typography.bodyMedium.copy(fontWeight = FontWeight.SemiBold),
                color = MaterialTheme.colorScheme.onTertiaryContainer,
            )
            Spacer(Modifier.height(Spacing.S))
            CleansiaPrimaryButton(
                text = stringResource(R.string.work_contract_accept_cta),
                onClick = onAccept,
            )
        }
        is WorkContractStanding.Accepted -> ContractSurface(
            container = MaterialTheme.colorScheme.surface,
            content = MaterialTheme.colorScheme.onSurfaceVariant,
            modifier = modifier,
        ) {
            Text(
                text = stringResource(
                    R.string.work_contract_accepted_line,
                    formatOrderDateTime(standing.acceptedOn),
                    standing.documentVersion,
                ),
                style = MaterialTheme.typography.bodyMedium,
                color = MaterialTheme.colorScheme.onSurface,
            )
            CleansiaTextLink(
                text = stringResource(R.string.work_contract_read),
                onClick = { onRead(standing.acceptanceId) },
            )
        }
    }
}

@Composable
private fun ContractSurface(
    container: androidx.compose.ui.graphics.Color,
    content: androidx.compose.ui.graphics.Color,
    modifier: Modifier = Modifier,
    body: @Composable () -> Unit,
) {
    Surface(
        modifier = modifier.fillMaxWidth(),
        shape = RoundedCornerShape(16.dp),
        color = container,
        tonalElevation = 0.dp,
    ) {
        Column(modifier = Modifier.padding(vertical = Spacing.M)) {
            Row(
                verticalAlignment = Alignment.CenterVertically,
                horizontalArrangement = Arrangement.spacedBy(Spacing.XS),
            ) {
                Icon(
                    imageVector = Icons.Outlined.Description,
                    contentDescription = null,
                    modifier = Modifier.size(18.dp),
                    tint = content,
                )
                Text(
                    text = stringResource(R.string.work_contract_title),
                    style = MaterialTheme.typography.titleSmall.copy(fontWeight = FontWeight.SemiBold),
                    color = content,
                )
            }
            Spacer(Modifier.height(Spacing.XS))
            body()
        }
    }
}
