package cz.cleansia.partner.features.orders

import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.width
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.outlined.Home
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import cz.cleansia.core.format.formatOrderPrice
import cz.cleansia.partner.R
import cz.cleansia.partner.api.model.OrderItem

/**
 * Property + selected services + selected packages + extras card. This is
 * the cleaner's "what am I doing here?" reference: rooms / baths on top
 * for a one-glance scope read, then the named services they're paid to
 * deliver, package adds with their listed prices, and finally the
 * emoji-tagged extras the customer ticked.
 *
 * Services from the partner DTO don't currently carry a per-service
 * price (only estimated time + currency code), so we render just the
 * name. Packages do carry `price`, so we surface it. Extras come back
 * as a slug→bool map; we filter to active slugs and map them to the
 * same emoji + name pair the customer wizard uses.
 *
 * Service/package names resolve to the device locale via
 * [localizedScopeName], degrading to the stored English name when the
 * backend didn't send that translation.
 */
@Composable
fun ScopeCard(
    order: OrderItem,
    modifier: Modifier = Modifier,
) {
    val rooms = order.rooms ?: 0
    val baths = order.bathrooms ?: 0
    val services = order.selectedServices.orEmpty()
        .mapNotNull { svc ->
            localizedScopeName(svc.translations, svc.name)?.takeIf { it.isNotBlank() }
        }
    val packages = order.selectedPackages.orEmpty()
    val activeExtras = order.extras.orEmpty()
        .filterValues { it }
        .keys
        .toList()
    val currencyCode = order.currency?.code ?: order.currency?.symbol

    OrderSectionCard(
        title = stringResource(R.string.scope_section_title),
        icon = Icons.Outlined.Home,
        modifier = modifier,
    ) {
        Text(
            text = buildString {
                if (rooms > 0) append("$rooms rooms")
                if (baths > 0) {
                    if (isNotEmpty()) append(" · ")
                    append("$baths bathrooms")
                }
                if (isEmpty()) append("—")
            },
            style = MaterialTheme.typography.bodyLarge.copy(fontWeight = FontWeight.SemiBold),
            color = MaterialTheme.colorScheme.onSurface,
        )

        if (services.isNotEmpty() || packages.isNotEmpty()) {
            Spacer(Modifier.height(12.dp))
            Text(
                text = stringResource(R.string.scope_services_label),
                style = MaterialTheme.typography.labelSmall,
                color = MaterialTheme.colorScheme.onSurfaceVariant,
            )
            Spacer(Modifier.height(4.dp))
            Column(verticalArrangement = Arrangement.spacedBy(4.dp)) {
                services.forEach { name -> ScopeLineItem(label = name, value = null) }
                packages.forEach { pkg ->
                    val priceLabel = pkg.price?.let { formatOrderPrice(it, currencyCode) }
                    ScopeLineItem(
                        label = localizedScopeName(pkg.translations, pkg.name)
                            ?.takeIf { it.isNotBlank() } ?: "—",
                        value = priceLabel,
                    )
                }
            }
        }

        if (activeExtras.isNotEmpty()) {
            Spacer(Modifier.height(12.dp))
            Text(
                text = stringResource(R.string.scope_extras_label),
                style = MaterialTheme.typography.labelSmall,
                color = MaterialTheme.colorScheme.onSurfaceVariant,
            )
            Spacer(Modifier.height(4.dp))
            Column(verticalArrangement = Arrangement.spacedBy(4.dp)) {
                activeExtras.forEach { slug ->
                    ScopeExtraRow(slug = slug)
                }
            }
        }
    }
}

@Composable
private fun ScopeLineItem(
    label: String,
    value: String?,
) {
    Row(
        modifier = Modifier.fillMaxWidth(),
        verticalAlignment = Alignment.CenterVertically,
        horizontalArrangement = Arrangement.SpaceBetween,
    ) {
        Text(
            text = label,
            style = MaterialTheme.typography.bodyMedium,
            color = MaterialTheme.colorScheme.onSurface,
            modifier = Modifier.padding(end = 8.dp).fillMaxWidth(0.7f),
        )
        if (value != null) {
            Text(
                text = value,
                style = MaterialTheme.typography.bodyMedium.copy(fontWeight = FontWeight.SemiBold),
                color = MaterialTheme.colorScheme.onSurface,
            )
        }
    }
}

@Composable
private fun ScopeExtraRow(slug: String) {
    Row(
        modifier = Modifier.fillMaxWidth().padding(vertical = 2.dp),
        verticalAlignment = Alignment.CenterVertically,
    ) {
        Text(
            text = emojiForExtraSlug(slug),
            style = MaterialTheme.typography.bodyLarge,
        )
        Spacer(Modifier.width(10.dp))
        Text(
            text = nameForExtraSlug(slug),
            style = MaterialTheme.typography.bodyMedium,
            color = MaterialTheme.colorScheme.onSurface,
        )
    }
}
