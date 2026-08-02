package cz.cleansia.customer.features.orders

import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.width
import androidx.compose.material3.HorizontalDivider
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextDecoration
import androidx.compose.ui.unit.dp
import cz.cleansia.core.format.formatOrderDateRange
import cz.cleansia.core.format.formatOrderPrice
import cz.cleansia.customer.R
import cz.cleansia.customer.core.orders.OrderDetailDto

/**
 * Identity strip under the hero — the order number the top bar used to carry
 * before the map took the top of the screen.
 *
 * [showFacts] is on when the hero above is the live progress card, which shows
 * status but no schedule, price or confirmation code; the static [HeroCard]
 * carries those itself, so the strip stays down to the order number.
 */
@Composable
internal fun OrderMetaStrip(order: OrderDetailDto, showFacts: Boolean) {
    val currencyCode = order.currency?.code
    val hasDiscount = order.appliedDiscountSource != 0 &&
        order.originalSubtotal > order.totalPrice

    Row(
        modifier = Modifier.fillMaxWidth(),
        verticalAlignment = Alignment.CenterVertically,
    ) {
        Column(Modifier.weight(1f)) {
            Text(
                text = order.displayOrderNumber?.let { "#$it" } ?: "—",
                style = MaterialTheme.typography.labelLarge.copy(fontWeight = FontWeight.SemiBold),
                color = MaterialTheme.colorScheme.onSurfaceVariant,
            )
            if (showFacts) {
                Text(
                    text = formatOrderDateRange(
                        iso = order.cleaningDateTime,
                        estimatedMinutes = order.estimatedTime,
                    ),
                    style = MaterialTheme.typography.bodyMedium,
                    color = MaterialTheme.colorScheme.onSurface,
                )
                order.confirmationCode?.takeIf { it.isNotBlank() }?.let { code ->
                    Text(
                        text = stringResource(R.string.order_detail_code_label) + " " + code,
                        style = MaterialTheme.typography.labelSmall,
                        color = MaterialTheme.colorScheme.onSurfaceVariant,
                    )
                }
            }
        }
        if (showFacts) {
            Spacer(Modifier.width(8.dp))
            Column(horizontalAlignment = Alignment.End) {
                Text(
                    text = formatOrderPrice(order.totalPrice, currencyCode),
                    style = MaterialTheme.typography.titleMedium.copy(fontWeight = FontWeight.Bold),
                    color = MaterialTheme.colorScheme.primary,
                )
                if (hasDiscount) {
                    Text(
                        text = formatOrderPrice(order.originalSubtotal, currencyCode),
                        style = MaterialTheme.typography.labelSmall.copy(
                            textDecoration = TextDecoration.LineThrough,
                        ),
                        color = MaterialTheme.colorScheme.onSurfaceVariant,
                    )
                }
            }
        }
    }
}

/**
 * What the customer is charged and how. The per-source discount amounts are on
 * the wire but were never rendered, so a Plus member with a loyalty tier could
 * see a struck-through subtotal with no explanation of where the difference
 * went.
 */
@Composable
internal fun PriceBreakdownCard(order: OrderDetailDto) {
    val currencyCode = order.currency?.code
    val discounts = listOfNotNull(
        order.tierDiscountAmount
            ?.takeIf { it > 0.0 }
            ?.let { stringResource(R.string.order_detail_discount_tier) to it },
        order.membershipDiscountAmount
            ?.takeIf { it > 0.0 }
            ?.let { stringResource(R.string.order_detail_discount_membership) to it },
        order.promoDiscountAmount
            ?.takeIf { it > 0.0 }
            ?.let { stringResource(R.string.order_detail_discount_promo) to it },
    )
    val showSubtotal = order.originalSubtotal > 0.0 && order.originalSubtotal != order.totalPrice

    Card {
        SectionHeader(title = stringResource(R.string.order_detail_services))
        Spacer(Modifier.height(8.dp))

        if (showSubtotal) {
            InfoRow(
                label = stringResource(R.string.order_detail_subtotal),
                value = formatOrderPrice(order.originalSubtotal, currencyCode),
            )
            Spacer(Modifier.height(6.dp))
        }
        discounts.forEach { (label, amount) ->
            InfoRow(
                label = label,
                value = "−" + formatOrderPrice(amount, currencyCode),
            )
            Spacer(Modifier.height(6.dp))
        }

        HorizontalDivider(color = MaterialTheme.colorScheme.outlineVariant)
        Spacer(Modifier.height(8.dp))
        Row(
            modifier = Modifier.fillMaxWidth(),
            horizontalArrangement = Arrangement.SpaceBetween,
        ) {
            Text(
                text = stringResource(R.string.order_detail_total),
                style = MaterialTheme.typography.bodyLarge.copy(fontWeight = FontWeight.SemiBold),
                color = MaterialTheme.colorScheme.onSurface,
            )
            Text(
                text = formatOrderPrice(order.totalPrice, currencyCode),
                style = MaterialTheme.typography.titleMedium.copy(fontWeight = FontWeight.Bold),
                color = MaterialTheme.colorScheme.primary,
            )
        }

        paymentMethodLabel(order)?.let { method ->
            Spacer(Modifier.height(10.dp))
            InfoRow(label = stringResource(R.string.order_detail_payment_method), value = method)
        }
        paymentStatusLabel(order)?.let { status ->
            Spacer(Modifier.height(6.dp))
            InfoRow(label = stringResource(R.string.order_detail_payment_status), value = status)
        }
    }
}

/** Backend `PaymentType`: Cash = 1, Card = 2. */
@Composable
private fun paymentMethodLabel(order: OrderDetailDto): String? = when (order.paymentType?.value) {
    1 -> stringResource(R.string.booking_pay_cash)
    2 -> stringResource(R.string.booking_pay_card)
    else -> order.paymentType?.name
}

/** Backend `PaymentStatus`: Pending = 1 … Disputed = 5, PartiallyRefunded = 6. */
@Composable
private fun paymentStatusLabel(order: OrderDetailDto): String? = when (order.paymentStatus?.value) {
    1 -> stringResource(R.string.orders_payment_pending)
    2 -> stringResource(R.string.orders_payment_paid)
    3 -> stringResource(R.string.orders_payment_failed)
    4 -> stringResource(R.string.orders_payment_refunded)
    5 -> stringResource(R.string.orders_payment_disputed)
    else -> order.paymentStatus?.name
}
