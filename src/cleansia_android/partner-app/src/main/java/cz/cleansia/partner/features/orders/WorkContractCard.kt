package cz.cleansia.partner.features.orders

import androidx.compose.foundation.background
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.IntrinsicSize
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.offset
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.outlined.KeyboardArrowRight
import androidx.compose.material.icons.outlined.Description
import androidx.compose.material.icons.outlined.ErrorOutline
import androidx.compose.material3.Icon
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.ui.Modifier
import androidx.compose.ui.layout.FirstBaseline
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.unit.dp
import cz.cleansia.core.ui.components.CleansiaButtonSize
import cz.cleansia.core.ui.components.CleansiaPrimaryButton
import cz.cleansia.core.ui.components.CleansiaTextLink
import cz.cleansia.core.ui.components.OrderInfoRow
import cz.cleansia.core.ui.theme.capBandAligned
import cz.cleansia.partner.R
import cz.cleansia.partner.ui.theme.pendingInk
import java.time.Instant
import java.time.ZoneId
import java.time.format.DateTimeFormatter

/**
 * The cleaner's standing on the contract for work. Accepted: the *Contract for work* section, *Accepted on · Nov 10*,
 * *Version · 2026-10-27* and *Read the contract ›*. A seat an administrator placed: the amber notice under the head to
 * accept it before starting. Nothing for a cleaner who is not on the crew.
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
        WorkContractStanding.Pending -> {
            val ink = MaterialTheme.colorScheme.pendingInk
            val style = MaterialTheme.typography.bodyMedium
            Row(
                modifier = modifier
                    .fillMaxWidth()
                    .background(amberFill(), RoundedCornerShape(16.dp))
                    .padding(horizontal = 16.dp, vertical = 12.dp),
            ) {
                Icon(Icons.Outlined.ErrorOutline, contentDescription = null, tint = ink, modifier = capBandAligned(style).padding(end = 10.dp).size(18.dp))
                Column(Modifier.alignBy(FirstBaseline)) {
                    Text(text = OrdersBoardRules.tie(stringResource(R.string.work_contract_pending_banner)), style = style, color = ink)
                    CleansiaPrimaryButton(
                        text = stringResource(R.string.work_contract_accept_cta),
                        onClick = onAccept,
                        size = CleansiaButtonSize.Small,
                        modifier = Modifier.padding(top = 8.dp).width(IntrinsicSize.Max),
                    )
                }
            }
        }
        is WorkContractStanding.Accepted -> DetailSection(
            title = stringResource(R.string.work_contract_title),
            icon = Icons.Outlined.Description,
            modifier = modifier,
        ) {
            val locale = appLocale()
            val acceptedOn = runCatching { Instant.parse(standing.acceptedOn) }.getOrNull()?.let {
                DateTimeFormatter.ofPattern(android.text.format.DateFormat.getBestDateTimePattern(locale, "dMMM"), locale).format(it.atZone(ZoneId.systemDefault()))
            } ?: standing.acceptedOn
            Box(Modifier.padding(vertical = 3.dp)) { OrderInfoRow(label = stringResource(R.string.contract_accepted_on_label), value = acceptedOn) }
            standing.documentVersion.takeIf { it.isNotBlank() }?.let {
                Box(Modifier.padding(vertical = 3.dp)) { OrderInfoRow(label = stringResource(R.string.contract_version_label), value = it) }
            }
            CleansiaTextLink(
                text = stringResource(R.string.work_contract_read),
                onClick = { onRead(standing.acceptanceId) },
                trailingIcon = Icons.AutoMirrored.Outlined.KeyboardArrowRight,
                modifier = Modifier.offset(x = (-4).dp),
            )
        }
    }
}
