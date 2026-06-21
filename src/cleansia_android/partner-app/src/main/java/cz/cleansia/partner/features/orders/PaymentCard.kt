package cz.cleansia.partner.features.orders

import androidx.compose.foundation.background
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.outlined.CreditCard
import androidx.compose.material3.HorizontalDivider
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import cz.cleansia.partner.R
import cz.cleansia.partner.api.model.OrderItem

/**
 * Payment summary. When any discount applies (originalSubtotal !=
 * totalPrice) we render a breakdown:
 *
 *   Subtotal              1 250 Kč
 *   Tier discount         -125 Kč
 *   Membership discount    -25 Kč
 *   ─────────
 *   Total                 1 100 Kč
 *
 * Otherwise we show just Total + Method to keep the card terse.
 *
 * The payment-status pill (Paid / Pending / Failed / Refunded) is
 * color-coded so the cleaner can see at a glance whether to expect cash
 * at the door — failed cards are a real-world scenario where the
 * cleaner needs to know before they show up.
 */
@Composable
fun PaymentCard(
    order: OrderItem,
    modifier: Modifier = Modifier,
) {
    val currencyCode = order.currency?.code ?: order.currency?.symbol
    val subtotal = order.originalSubtotal
    val total = order.totalPrice
    val hasBreakdown = subtotal != null && total != null && subtotal != total

    OrderSectionCard(
        title = stringResource(R.string.payment_section_title),
        icon = Icons.Outlined.CreditCard,
        modifier = modifier,
    ) {
        if (hasBreakdown) {
            PaymentRow(
                label = stringResource(R.string.payment_subtotal),
                value = formatOrderMoney(subtotal!!, currencyCode),
                emphasis = false,
            )
            order.tierDiscountAmount?.takeIf { it > 0 }?.let {
                Spacer(Modifier.height(4.dp))
                PaymentRow(
                    label = stringResource(R.string.payment_tier_discount),
                    value = "-${formatOrderMoney(it, currencyCode)}",
                    emphasis = false,
                    valueColor = MaterialTheme.colorScheme.primary,
                )
            }
            order.membershipDiscountAmount?.takeIf { it > 0 }?.let {
                Spacer(Modifier.height(4.dp))
                PaymentRow(
                    label = stringResource(R.string.payment_membership_discount),
                    value = "-${formatOrderMoney(it, currencyCode)}",
                    emphasis = false,
                    valueColor = MaterialTheme.colorScheme.primary,
                )
            }
            order.promoDiscountAmount?.takeIf { it > 0 }?.let {
                Spacer(Modifier.height(4.dp))
                PaymentRow(
                    label = stringResource(R.string.payment_promo_discount),
                    value = "-${formatOrderMoney(it, currencyCode)}",
                    emphasis = false,
                    valueColor = MaterialTheme.colorScheme.primary,
                )
            }
            Spacer(Modifier.height(10.dp))
            HorizontalDivider(color = MaterialTheme.colorScheme.outlineVariant)
            Spacer(Modifier.height(10.dp))
        }

        total?.let {
            PaymentRow(
                label = stringResource(R.string.payment_total),
                value = formatOrderMoney(it, currencyCode),
                emphasis = true,
            )
        }

        Spacer(Modifier.height(10.dp))
        Row(
            modifier = Modifier.fillMaxWidth(),
            verticalAlignment = Alignment.CenterVertically,
            horizontalArrangement = Arrangement.SpaceBetween,
        ) {
            Text(
                text = stringResource(
                    R.string.payment_method_value,
                    order.paymentType?.name.orEmpty().ifBlank { "—" },
                ),
                style = MaterialTheme.typography.bodyMedium,
                color = MaterialTheme.colorScheme.onSurfaceVariant,
            )
            PaymentStatusPill(statusName = order.paymentStatus?.name)
        }
    }
}

@Composable
private fun PaymentRow(
    label: String,
    value: String,
    emphasis: Boolean,
    valueColor: Color = MaterialTheme.colorScheme.onSurface,
) {
    Row(
        modifier = Modifier.fillMaxWidth(),
        horizontalArrangement = Arrangement.SpaceBetween,
        verticalAlignment = Alignment.CenterVertically,
    ) {
        Text(
            text = label,
            style = if (emphasis)
                MaterialTheme.typography.titleMedium.copy(fontWeight = FontWeight.SemiBold)
            else
                MaterialTheme.typography.bodyMedium,
            color = if (emphasis) MaterialTheme.colorScheme.onSurface
                else MaterialTheme.colorScheme.onSurfaceVariant,
        )
        Text(
            text = value,
            style = if (emphasis)
                MaterialTheme.typography.titleMedium.copy(fontWeight = FontWeight.ExtraBold)
            else
                MaterialTheme.typography.bodyMedium.copy(fontWeight = FontWeight.Medium),
            color = if (emphasis) MaterialTheme.colorScheme.primary else valueColor,
        )
    }
}

/**
 * Color-coded status pill. Maps the Code DTO's `name` string (set by
 * the backend enum) to one of three buckets — green for the happy path,
 * amber for pending, red for failed/refunded. Anything we don't
 * recognise falls back to a neutral pill so the screen never crashes
 * on a future enum addition.
 */
@Composable
private fun PaymentStatusPill(statusName: String?) {
    val key = statusName?.lowercase().orEmpty()
    val (tint, label) = when {
        key.contains("paid") || key.contains("captured") || key.contains("succeed") ->
            Color(0xFF16A34A) to (statusName ?: stringResource(R.string.payment_status_paid))
        key.contains("pending") || key.contains("processing") ->
            Color(0xFFD97706) to (statusName ?: stringResource(R.string.payment_status_pending))
        key.contains("failed") || key.contains("refund") || key.contains("declined") ->
            Color(0xFFDC2626) to (statusName ?: stringResource(R.string.payment_status_failed))
        else ->
            MaterialTheme.colorScheme.onSurfaceVariant to (statusName ?: "—")
    }
    Box(
        modifier = Modifier
            .background(color = tint.copy(alpha = 0.12f), shape = RoundedCornerShape(999.dp))
            .padding(horizontal = 10.dp, vertical = 4.dp),
    ) {
        Text(
            text = label,
            style = MaterialTheme.typography.labelMedium.copy(fontWeight = FontWeight.SemiBold),
            color = tint,
        )
    }
}
