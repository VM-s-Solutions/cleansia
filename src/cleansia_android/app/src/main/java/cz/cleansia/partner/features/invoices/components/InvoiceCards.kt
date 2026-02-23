package cz.cleansia.partner.features.invoices.components

import androidx.compose.foundation.background
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.CalendarMonth
import androidx.compose.material.icons.filled.CheckCircle
import androidx.compose.material.icons.filled.CreditCard
import androidx.compose.material.icons.filled.Description
import androidx.compose.material.icons.filled.Person
import androidx.compose.material.icons.filled.Receipt
import androidx.compose.material.icons.filled.Schedule
import androidx.compose.material3.Card
import androidx.compose.material3.CardDefaults
import androidx.compose.material3.HorizontalDivider
import androidx.compose.material3.Icon
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.Brush
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.vector.ImageVector
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.unit.dp
import cz.cleansia.partner.R
import cz.cleansia.partner.domain.models.invoices.InvoiceDetail
import cz.cleansia.partner.ui.components.InvoiceStatusBadge
import cz.cleansia.partner.ui.theme.CleansiaColors
import cz.cleansia.partner.core.utils.DateTimeUtils
import java.text.NumberFormat
import java.util.Currency
import java.util.Locale

// ── Header Card with gradient ──

@Composable
internal fun InvoiceHeaderCard(invoice: InvoiceDetail, isDarkTheme: Boolean) {
    val gradientColors = if (isDarkTheme) {
        listOf(Color(0xFF1E293B), Color(0xFF0F172A))
    } else {
        listOf(Color(0xFFE0F2FE), Color(0xFFF0F9FF))
    }
    val titleColor = if (isDarkTheme) Color(0xFFE0F2FE) else Color(0xFF0C4A6E)
    val amountColor = if (isDarkTheme) Color(0xFF7DD3FC) else Color(0xFF0284C7)
    val subtextColor = if (isDarkTheme) Color(0xFF94A3B8) else Color(0xFF475569)

    Card(
        modifier = Modifier.fillMaxWidth(),
        shape = RoundedCornerShape(16.dp),
        elevation = CardDefaults.cardElevation(defaultElevation = 2.dp)
    ) {
        Box(
            modifier = Modifier
                .fillMaxWidth()
                .background(Brush.linearGradient(colors = gradientColors))
                .padding(20.dp)
        ) {
            Column {
                Row(
                    modifier = Modifier.fillMaxWidth(),
                    horizontalArrangement = Arrangement.SpaceBetween,
                    verticalAlignment = Alignment.CenterVertically
                ) {
                    Text(
                        text = stringResource(R.string.invoice_number, invoice.invoiceNumber),
                        style = MaterialTheme.typography.titleLarge,
                        fontWeight = FontWeight.Bold,
                        color = titleColor,
                        modifier = Modifier.weight(1f)
                    )
                    InvoiceStatusBadge(status = invoice.status)
                }

                Spacer(modifier = Modifier.height(12.dp))

                Text(
                    text = formatCurrency(invoice.totalAmount, invoice.currency),
                    style = MaterialTheme.typography.headlineMedium,
                    fontWeight = FontWeight.Bold,
                    color = amountColor
                )

                if (invoice.period.isNotBlank()) {
                    Spacer(modifier = Modifier.height(4.dp))
                    Text(
                        text = invoice.period,
                        style = MaterialTheme.typography.bodyMedium,
                        color = subtextColor
                    )
                }
            }
        }
    }
}

// ── Quick Info Card (compact key details) ──

@Composable
internal fun QuickInfoCard(invoice: InvoiceDetail) {
    Card(
        modifier = Modifier.fillMaxWidth(),
        colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surface),
        elevation = CardDefaults.cardElevation(defaultElevation = 1.dp),
        shape = RoundedCornerShape(16.dp)
    ) {
        Column(modifier = Modifier.padding(16.dp)) {
            // Employee & Variable Symbol row
            if (!invoice.employeeName.isNullOrBlank()) {
                QuickInfoRow(
                    icon = Icons.Default.Person,
                    label = stringResource(R.string.employee),
                    value = invoice.employeeName
                )
            }
            if (!invoice.variableSymbol.isNullOrBlank()) {
                QuickInfoRow(
                    icon = Icons.Default.CreditCard,
                    label = stringResource(R.string.variable_symbol),
                    value = invoice.variableSymbol
                )
            }
            if (invoice.totalOrders != null && invoice.totalOrders > 0) {
                QuickInfoRow(
                    icon = Icons.Default.Description,
                    label = stringResource(R.string.total_orders),
                    value = invoice.totalOrders.toString()
                )
            }

            HorizontalDivider(
                modifier = Modifier.padding(vertical = 10.dp),
                color = MaterialTheme.colorScheme.outlineVariant.copy(alpha = 0.5f)
            )

            // Dates
            QuickInfoRow(
                icon = Icons.Default.CalendarMonth,
                label = stringResource(R.string.issue_date),
                value = DateTimeUtils.formatDate(invoice.issueDate)
            )
            QuickInfoRow(
                icon = Icons.Default.Schedule,
                label = stringResource(R.string.due_date),
                value = DateTimeUtils.formatDate(invoice.dueDate)
            )
            if (invoice.paidDate.isNotBlank()) {
                QuickInfoRow(
                    icon = Icons.Default.CheckCircle,
                    label = stringResource(R.string.paid_date),
                    value = DateTimeUtils.formatDate(invoice.paidDate)
                )
            }
        }
    }
}

@Composable
private fun QuickInfoRow(
    icon: ImageVector,
    label: String,
    value: String
) {
    Row(
        modifier = Modifier
            .fillMaxWidth()
            .padding(vertical = 5.dp),
        verticalAlignment = Alignment.CenterVertically
    ) {
        Icon(
            imageVector = icon,
            contentDescription = null,
            tint = MaterialTheme.colorScheme.primary,
            modifier = Modifier.size(18.dp)
        )
        Spacer(modifier = Modifier.width(10.dp))
        Text(
            text = label,
            style = MaterialTheme.typography.bodyMedium,
            color = MaterialTheme.colorScheme.onSurfaceVariant,
            modifier = Modifier.weight(1f)
        )
        Text(
            text = value,
            style = MaterialTheme.typography.bodyMedium,
            fontWeight = FontWeight.Medium,
            color = MaterialTheme.colorScheme.onSurface,
            maxLines = 1,
            overflow = TextOverflow.Ellipsis
        )
    }
}

// ── Amount Breakdown Card ──

@Composable
internal fun AmountBreakdownCard(invoice: InvoiceDetail) {
    Card(
        modifier = Modifier.fillMaxWidth(),
        colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surface),
        elevation = CardDefaults.cardElevation(defaultElevation = 1.dp),
        shape = RoundedCornerShape(16.dp)
    ) {
        Column(modifier = Modifier.padding(16.dp)) {
            Row(verticalAlignment = Alignment.CenterVertically) {
                Box(
                    modifier = Modifier
                        .size(32.dp)
                        .clip(RoundedCornerShape(8.dp))
                        .background(MaterialTheme.colorScheme.primaryContainer.copy(alpha = 0.5f)),
                    contentAlignment = Alignment.Center
                ) {
                    Icon(
                        imageVector = Icons.Default.Receipt,
                        contentDescription = null,
                        tint = MaterialTheme.colorScheme.primary,
                        modifier = Modifier.size(18.dp)
                    )
                }
                Spacer(modifier = Modifier.width(10.dp))
                Text(
                    text = stringResource(R.string.amount_breakdown),
                    style = MaterialTheme.typography.titleMedium,
                    fontWeight = FontWeight.SemiBold,
                    color = MaterialTheme.colorScheme.onSurface
                )
            }
            Spacer(modifier = Modifier.height(12.dp))

            DetailRow(
                label = stringResource(R.string.subtotal),
                value = formatCurrency(invoice.subtotal, invoice.currency)
            )
            if (invoice.bonusAmount != null && invoice.bonusAmount > 0) {
                AmountRow(
                    label = stringResource(R.string.bonus),
                    amount = invoice.bonusAmount,
                    currency = invoice.currency,
                    color = CleansiaColors.success
                )
            }
            if (invoice.deductionAmount != null && invoice.deductionAmount > 0) {
                AmountRow(
                    label = stringResource(R.string.deductions),
                    amount = -invoice.deductionAmount,
                    currency = invoice.currency,
                    color = MaterialTheme.colorScheme.error
                )
            }
            HorizontalDivider(
                modifier = Modifier.padding(vertical = 10.dp),
                color = MaterialTheme.colorScheme.outlineVariant
            )
            Row(
                modifier = Modifier.fillMaxWidth(),
                horizontalArrangement = Arrangement.SpaceBetween
            ) {
                Text(
                    text = stringResource(R.string.total),
                    style = MaterialTheme.typography.titleMedium,
                    fontWeight = FontWeight.Bold,
                    color = MaterialTheme.colorScheme.onSurface
                )
                Text(
                    text = formatCurrency(invoice.totalAmount, invoice.currency),
                    style = MaterialTheme.typography.titleMedium,
                    fontWeight = FontWeight.Bold,
                    color = MaterialTheme.colorScheme.primary
                )
            }
        }
    }
}

@Composable
private fun AmountRow(
    label: String,
    amount: Double,
    currency: String,
    color: Color
) {
    Row(
        modifier = Modifier
            .fillMaxWidth()
            .padding(vertical = 4.dp),
        horizontalArrangement = Arrangement.SpaceBetween
    ) {
        Text(
            text = label,
            style = MaterialTheme.typography.bodyMedium,
            color = MaterialTheme.colorScheme.onSurfaceVariant
        )
        Text(
            text = (if (amount >= 0) "+" else "") + formatCurrency(kotlin.math.abs(amount), currency),
            style = MaterialTheme.typography.bodyMedium,
            fontWeight = FontWeight.Medium,
            color = color
        )
    }
}

@Composable
private fun DetailRow(
    label: String,
    value: String
) {
    Row(
        modifier = Modifier
            .fillMaxWidth()
            .padding(vertical = 4.dp),
        horizontalArrangement = Arrangement.SpaceBetween
    ) {
        Text(
            text = label,
            style = MaterialTheme.typography.bodyMedium,
            color = MaterialTheme.colorScheme.onSurfaceVariant
        )
        Text(
            text = value,
            style = MaterialTheme.typography.bodyMedium,
            fontWeight = FontWeight.Medium,
            color = MaterialTheme.colorScheme.onSurface
        )
    }
}

internal fun formatCurrency(amount: Double, currency: String = "CZK"): String {
    return try {
        val format = NumberFormat.getCurrencyInstance(Locale.getDefault())
        format.currency = Currency.getInstance(currency)
        format.format(amount)
    } catch (e: Exception) {
        "$currency ${String.format("%.2f", amount)}"
    }
}
