package cz.cleansia.partner.features.orders

import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.outlined.Person
import androidx.compose.material3.Icon
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.ui.Modifier
import androidx.compose.ui.layout.FirstBaseline
import androidx.compose.ui.unit.dp
import cz.cleansia.core.ui.theme.capBandAligned

/**
 * The customer on her own job: *Jana Nováková ‖ +420 604 218 377*, then *Navigate · Call · SMS* as tonal chips. The
 * phone is whatever the server released to this caller ([OrderDisclosure.showsCustomerContact], never the assignment
 * flag), and Navigate needs a street address: a maps app opened on a district is worse than none. [closedNote] stands
 * under the line once the details close; [withNavigate] false on a finished job, where the section's title names her.
 */
@Composable
fun CustomerCard(
    customerName: String?,
    disclosure: OrderDisclosure,
    location: OrderLocation,
    closedNote: String?,
    modifier: Modifier = Modifier,
    withNavigate: Boolean = true,
) {
    val name = customerName?.trim()?.takeIf { it.isNotEmpty() }
    val phone = disclosure.customerPhone.takeIf { disclosure.showsCustomerContact }
    Column(modifier = modifier.fillMaxWidth()) {
        if (name != null || phone != null) CustomerLine(name = name, phone = phone, withIcon = withNavigate)
        closedNote?.let {
            Text(
                text = OrdersBoardRules.tie(it),
                style = MaterialTheme.typography.bodySmall,
                color = MaterialTheme.colorScheme.onSurfaceVariant,
                modifier = Modifier.padding(top = 8.dp),
            )
        }
        if (phone != null || (withNavigate && location.navigationTarget() != null)) {
            ContactPills(
                phone = phone,
                navigateTo = location.navigationTarget()?.takeIf { withNavigate },
                modifier = Modifier.padding(top = if (closedNote != null) 12.dp else 20.dp),
            )
        }
    }
}

/** *Jana Nováková ‖ +420 604 218 377*: the name in the row's line, the phone at the end in the secondary ink. */
@Composable
internal fun CustomerLine(name: String?, phone: String?, withIcon: Boolean, modifier: Modifier = Modifier) {
    val style = MaterialTheme.typography.bodyLarge
    Row(modifier.fillMaxWidth()) {
        if (withIcon) {
            Icon(
                imageVector = Icons.Outlined.Person,
                contentDescription = null,
                tint = MaterialTheme.colorScheme.onSurfaceVariant,
                modifier = capBandAligned(style).padding(end = 10.dp).size(18.dp),
            )
        }
        Text(text = name.orEmpty(), style = style, color = MaterialTheme.colorScheme.onSurface, modifier = Modifier.weight(1f).alignBy(FirstBaseline))
        phone?.let {
            Text(
                text = it,
                style = MaterialTheme.typography.bodyMedium.copy(fontFeatureSettings = "tnum"),
                color = MaterialTheme.colorScheme.onSurfaceVariant,
                softWrap = false,
                modifier = Modifier.padding(start = 10.dp).alignBy(FirstBaseline),
            )
        }
    }
}
