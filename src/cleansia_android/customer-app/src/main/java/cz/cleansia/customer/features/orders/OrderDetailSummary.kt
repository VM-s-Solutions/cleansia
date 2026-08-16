package cz.cleansia.customer.features.orders

import androidx.annotation.StringRes
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
import cz.cleansia.customer.BuildConfig
import cz.cleansia.customer.R
import cz.cleansia.customer.core.orders.OrderDetailDto

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
@StringRes
internal fun paymentMethodLabelRes(value: Int?): Int? = when (value) {
    1 -> R.string.booking_pay_cash
    2 -> R.string.booking_pay_card
    else -> null
}

/** Backend `PaymentStatus`: Pending = 1 … Disputed = 5, PartiallyRefunded = 6. */
@StringRes
internal fun paymentStatusLabelRes(value: Int?): Int? = when (value) {
    1 -> R.string.orders_payment_pending
    2 -> R.string.orders_payment_paid
    3 -> R.string.orders_payment_failed
    4 -> R.string.orders_payment_refunded
    5 -> R.string.orders_payment_disputed
    6 -> R.string.orders_payment_partially_refunded
    else -> null
}

/**
 * An ordinal the backend added after this build shipped. The `Code.name` beside
 * it is non-localized English, so production drops the row entirely rather than
 * printing it; a debug build shows the bare ordinal so the gap surfaces to us.
 * [isDebug] is a parameter, not a read of `BuildConfig`, so the production
 * branch stays assertable from a debug unit test.
 */
internal fun unknownPaymentLabel(value: Int?, isDebug: Boolean): String? =
    if (isDebug && value != null) "#$value" else null

@Composable
private fun paymentMethodLabel(order: OrderDetailDto): String? {
    val value = order.paymentType?.value
    return paymentMethodLabelRes(value)?.let { stringResource(it) }
        ?: unknownPaymentLabel(value, BuildConfig.DEBUG)
}

@Composable
private fun paymentStatusLabel(order: OrderDetailDto): String? {
    val value = order.paymentStatus?.value
    return paymentStatusLabelRes(value)?.let { stringResource(it) }
        ?: unknownPaymentLabel(value, BuildConfig.DEBUG)
}
