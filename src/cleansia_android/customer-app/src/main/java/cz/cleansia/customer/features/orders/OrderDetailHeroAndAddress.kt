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

/* ── Hero ── */

@Composable
internal fun HeroCard(order: OrderDetailDto) {
    Card {
        Row(verticalAlignment = Alignment.CenterVertically) {
            val statusLabelRes = orderStatusLabelRes(order.orderStatus?.value)
            val statusLabel = statusLabelRes?.let { stringResource(it) }
                ?: order.orderStatus?.name ?: "—"
            StatusPill(
                label = statusLabel,
                color = orderStatusColor(order.orderStatus?.value),
            )
            Spacer(Modifier.weight(1f))
            order.confirmationCode?.takeIf { it.isNotBlank() }?.let { code ->
                Column(horizontalAlignment = Alignment.End) {
                    Text(
                        stringResource(R.string.order_detail_code_label),
                        style = MaterialTheme.typography.labelSmall,
                        color = MaterialTheme.colorScheme.onSurfaceVariant,
                    )
                    Text(
                        code,
                        style = MaterialTheme.typography.labelLarge.copy(fontWeight = FontWeight.SemiBold),
                        color = MaterialTheme.colorScheme.onSurface,
                    )
                }
            }
        }
        Spacer(Modifier.height(12.dp))
        Text(
            text = formatOrderDateRange(
                iso = order.cleaningDateTime,
                estimatedMinutes = order.estimatedTime,
            ),
            style = MaterialTheme.typography.titleLarge.copy(
                fontFamily = Poppins,
                fontWeight = FontWeight.Bold,
            ),
            color = MaterialTheme.colorScheme.onBackground,
        )
        Spacer(Modifier.height(8.dp))
        val currencyCode = order.currency?.code
        val hasDiscount = order.appliedDiscountSource != 0 &&
            order.originalSubtotal > order.totalPrice
        Row(verticalAlignment = Alignment.Bottom) {
            Text(
                text = formatOrderPrice(order.totalPrice, currencyCode),
                style = MaterialTheme.typography.headlineMedium.copy(
                    fontFamily = Poppins,
                    fontWeight = FontWeight.Bold,
                ),
                color = MaterialTheme.colorScheme.primary,
            )
            if (hasDiscount) {
                Spacer(Modifier.width(8.dp))
                Text(
                    text = formatOrderPrice(order.originalSubtotal, currencyCode),
                    style = MaterialTheme.typography.titleMedium.copy(
                        textDecoration = TextDecoration.LineThrough,
                    ),
                    color = MaterialTheme.colorScheme.onSurfaceVariant,
                    modifier = Modifier.padding(bottom = 4.dp),
                )
            }
        }
        if (hasDiscount) {
            // LOY-003 — `appliedDiscountSource == 4` (Combined) means both
            // Plus and tier applied additively. Render both chips so the user
            // sees each saving source. Single-source enums (Tier/Membership/
            // Promo) render their one chip as before.
            when (order.appliedDiscountSource) {
                1 -> {
                    Spacer(Modifier.height(6.dp))
                    DiscountChip(stringResource(R.string.order_detail_discount_tier))
                }
                2 -> {
                    Spacer(Modifier.height(6.dp))
                    DiscountChip(stringResource(R.string.order_detail_discount_membership))
                }
                3 -> {
                    Spacer(Modifier.height(6.dp))
                    DiscountChip(stringResource(R.string.order_detail_discount_promo))
                }
                4 -> {
                    Spacer(Modifier.height(6.dp))
                    Row(
                        verticalAlignment = Alignment.CenterVertically,
                    ) {
                        DiscountChip(stringResource(R.string.order_detail_discount_membership))
                        Spacer(Modifier.width(6.dp))
                        DiscountChip(stringResource(R.string.order_detail_discount_tier))
                    }
                }
            }
        }
    }
}

@Composable
private fun DiscountChip(label: String) {
    Row(
        modifier = Modifier
            .clip(RoundedCornerShape(999.dp))
            .background(MaterialTheme.colorScheme.tertiaryContainer)
            .padding(horizontal = 10.dp, vertical = 4.dp),
        verticalAlignment = Alignment.CenterVertically,
    ) {
        Text(
            text = label,
            style = MaterialTheme.typography.labelSmall.copy(fontWeight = FontWeight.SemiBold),
            color = MaterialTheme.colorScheme.onTertiaryContainer,
        )
    }
}

@Composable
private fun StatusPill(label: String, color: Color) {
    Row(
        modifier = Modifier
            .clip(RoundedCornerShape(999.dp))
            .background(color.copy(alpha = 0.14f))
            .padding(horizontal = 10.dp, vertical = 4.dp),
        verticalAlignment = Alignment.CenterVertically,
    ) {
        Text(
            text = label,
            style = MaterialTheme.typography.labelSmall.copy(fontWeight = FontWeight.SemiBold),
            color = color,
        )
    }
}

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
