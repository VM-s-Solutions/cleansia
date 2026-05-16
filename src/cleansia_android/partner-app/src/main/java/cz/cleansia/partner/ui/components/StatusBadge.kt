package cz.cleansia.partner.ui.components

import androidx.compose.foundation.background
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.unit.dp
import cz.cleansia.partner.R
import cz.cleansia.partner.domain.models.invoices.InvoiceStatus
import cz.cleansia.partner.domain.models.orders.OrderStatus
import cz.cleansia.partner.domain.models.orders.PaymentStatus
import cz.cleansia.partner.ui.theme.CleansiaColors

@Composable
fun StatusBadge(
    text: String,
    backgroundColor: Color,
    textColor: Color,
    modifier: Modifier = Modifier
) {
    Text(
        text = text,
        style = MaterialTheme.typography.labelSmall,
        color = textColor,
        maxLines = 1,
        overflow = TextOverflow.Ellipsis,
        softWrap = false,
        modifier = modifier
            .clip(RoundedCornerShape(16.dp))
            .background(backgroundColor)
            .padding(horizontal = 10.dp, vertical = 4.dp)
    )
}

@Composable
fun OrderStatusBadge(
    status: OrderStatus,
    modifier: Modifier = Modifier
) {
    val (text, backgroundColor, textColor) = when (status) {
        OrderStatus.PENDING -> Triple(
            stringResource(R.string.status_pending),
            CleansiaColors.infoContainer,
            CleansiaColors.onInfoContainer
        )
        OrderStatus.CONFIRMED -> Triple(
            stringResource(R.string.status_confirmed),
            CleansiaColors.warningContainer,
            CleansiaColors.onWarningContainer
        )
        OrderStatus.ON_THE_WAY -> Triple(
            stringResource(R.string.status_on_the_way),
            MaterialTheme.colorScheme.tertiaryContainer,
            MaterialTheme.colorScheme.onTertiaryContainer
        )
        OrderStatus.IN_PROGRESS -> Triple(
            stringResource(R.string.status_in_progress),
            MaterialTheme.colorScheme.primaryContainer,
            MaterialTheme.colorScheme.onPrimaryContainer
        )
        OrderStatus.COMPLETED -> Triple(
            stringResource(R.string.status_completed),
            CleansiaColors.successContainer,
            CleansiaColors.onSuccessContainer
        )
        OrderStatus.CANCELLED -> Triple(
            stringResource(R.string.status_cancelled),
            MaterialTheme.colorScheme.errorContainer,
            MaterialTheme.colorScheme.onErrorContainer
        )
    }

    StatusBadge(
        text = text,
        backgroundColor = backgroundColor,
        textColor = textColor,
        modifier = modifier
    )
}

@Composable
fun PaymentStatusBadge(
    status: PaymentStatus,
    modifier: Modifier = Modifier
) {
    val (text, backgroundColor, textColor) = when (status) {
        PaymentStatus.PENDING -> Triple(
            stringResource(R.string.payment_pending),
            CleansiaColors.warningContainer,
            CleansiaColors.onWarningContainer
        )
        PaymentStatus.PAID -> Triple(
            stringResource(R.string.payment_paid),
            CleansiaColors.successContainer,
            CleansiaColors.onSuccessContainer
        )
        PaymentStatus.FAILED -> Triple(
            stringResource(R.string.payment_failed),
            MaterialTheme.colorScheme.errorContainer,
            MaterialTheme.colorScheme.onErrorContainer
        )
        PaymentStatus.REFUNDED -> Triple(
            stringResource(R.string.payment_refunded),
            MaterialTheme.colorScheme.secondaryContainer,
            MaterialTheme.colorScheme.onSecondaryContainer
        )
    }

    StatusBadge(
        text = text,
        backgroundColor = backgroundColor,
        textColor = textColor,
        modifier = modifier
    )
}

@Composable
fun InvoiceStatusBadge(
    status: InvoiceStatus,
    modifier: Modifier = Modifier
) {
    val (text, backgroundColor, textColor) = when (status) {
        InvoiceStatus.PENDING -> Triple(
            stringResource(R.string.invoice_pending),
            CleansiaColors.warningContainer,
            CleansiaColors.onWarningContainer
        )
        InvoiceStatus.APPROVED -> Triple(
            stringResource(R.string.invoice_approved),
            CleansiaColors.infoContainer,
            CleansiaColors.onInfoContainer
        )
        InvoiceStatus.PAID -> Triple(
            stringResource(R.string.invoice_paid),
            CleansiaColors.successContainer,
            CleansiaColors.onSuccessContainer
        )
        InvoiceStatus.DISPUTED -> Triple(
            stringResource(R.string.invoice_disputed),
            MaterialTheme.colorScheme.errorContainer,
            MaterialTheme.colorScheme.onErrorContainer
        )
        InvoiceStatus.REJECTED -> Triple(
            stringResource(R.string.invoice_rejected),
            MaterialTheme.colorScheme.errorContainer,
            MaterialTheme.colorScheme.onErrorContainer
        )
        InvoiceStatus.CANCELLED -> Triple(
            stringResource(R.string.invoice_cancelled),
            MaterialTheme.colorScheme.secondaryContainer,
            MaterialTheme.colorScheme.onSecondaryContainer
        )
    }

    StatusBadge(
        text = text,
        backgroundColor = backgroundColor,
        textColor = textColor,
        modifier = modifier
    )
}
