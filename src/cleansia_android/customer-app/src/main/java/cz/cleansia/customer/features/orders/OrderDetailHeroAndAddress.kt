package cz.cleansia.customer.features.orders

import androidx.compose.foundation.background
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.outlined.LocationOn
import androidx.compose.material3.Icon
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextDecoration
import androidx.compose.ui.unit.dp
import cz.cleansia.customer.R
import cz.cleansia.core.format.formatOrderDateRange
import cz.cleansia.core.format.formatOrderPrice
import cz.cleansia.customer.core.orders.OrderAddressDto
import cz.cleansia.customer.core.orders.OrderDetailDto
import cz.cleansia.customer.ui.format.orderStatusColor
import cz.cleansia.core.ui.theme.Poppins

/* ── Address ── */

@Composable
internal fun AddressCard(address: OrderAddressDto) {
    val cityZip = buildString {
        address.zipCode?.takeIf { it.isNotBlank() }?.let { append(it) }
        address.city?.takeIf { it.isNotBlank() }?.let {
            if (isNotEmpty()) append(' ')
            append(it)
        }
    }
    Card {
        SectionHeader(
            icon = {
                Icon(
                    Icons.Outlined.LocationOn,
                    contentDescription = null,
                    tint = MaterialTheme.colorScheme.primary,
                    modifier = Modifier.size(18.dp),
                )
            },
            title = stringResource(R.string.order_detail_address),
        )
        Spacer(Modifier.height(6.dp))
        Text(
            text = address.street ?: "—",
            style = MaterialTheme.typography.bodyLarge,
            color = MaterialTheme.colorScheme.onSurface,
        )
        if (cityZip.isNotBlank()) {
            Text(
                text = cityZip,
                style = MaterialTheme.typography.bodyMedium,
                color = MaterialTheme.colorScheme.onSurfaceVariant,
            )
        }
        address.country?.takeIf { it.isNotBlank() }?.let {
            Text(
                text = it,
                style = MaterialTheme.typography.bodyMedium,
                color = MaterialTheme.colorScheme.onSurfaceVariant,
            )
        }
    }
}
