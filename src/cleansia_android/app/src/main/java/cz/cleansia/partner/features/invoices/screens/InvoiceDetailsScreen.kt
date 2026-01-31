package cz.cleansia.partner.features.invoices.screens

import androidx.compose.foundation.background
import cz.cleansia.partner.ui.theme.LocalDarkTheme
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.statusBarsPadding
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.verticalScroll
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.CalendarMonth
import androidx.compose.material.icons.filled.CheckCircle
import androidx.compose.material.icons.filled.CreditCard
import androidx.compose.material.icons.filled.Description
import androidx.compose.material.icons.filled.Download
import androidx.compose.material.icons.filled.Info
import androidx.compose.material.icons.filled.Notes
import androidx.compose.material.icons.filled.Person
import androidx.compose.material.icons.filled.Receipt
import androidx.compose.material.icons.filled.Schedule
import androidx.compose.material3.Card
import androidx.compose.material3.CardDefaults
import androidx.compose.material3.HorizontalDivider
import androidx.compose.material3.Icon
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Scaffold
import androidx.compose.material3.SnackbarHost
import androidx.compose.material3.SnackbarHostState
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.collectAsState
import androidx.compose.runtime.getValue
import androidx.compose.runtime.remember
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
import androidx.hilt.navigation.compose.hiltViewModel
import cz.cleansia.partner.R
import cz.cleansia.partner.domain.models.invoices.InvoiceDetail
import cz.cleansia.partner.domain.models.invoices.InvoiceOrderItem
import cz.cleansia.partner.domain.models.invoices.InvoiceStatus
import cz.cleansia.partner.features.invoices.viewmodels.InvoiceDetailsViewModel
import cz.cleansia.partner.ui.components.CleansiaButton
import cz.cleansia.partner.ui.components.CleansiaButtonStyle
import cz.cleansia.partner.ui.components.ErrorView
import cz.cleansia.partner.ui.components.GlassBackButton
import cz.cleansia.partner.ui.components.InvoiceStatusBadge
import cz.cleansia.partner.ui.components.LoadingIndicator
import cz.cleansia.partner.ui.theme.CleansiaColors
import cz.cleansia.partner.core.utils.DateTimeUtils
import java.text.NumberFormat
import java.util.Currency
import java.util.Locale

@Composable
fun InvoiceDetailsScreen(
    invoiceId: String,
    onNavigateBack: () -> Unit,
    viewModel: InvoiceDetailsViewModel = hiltViewModel()
) {
    val uiState by viewModel.uiState.collectAsState()
    val snackbarHostState = remember { SnackbarHostState() }

    // Show error in snackbar
    LaunchedEffect(uiState.error, uiState.downloadError) {
        (uiState.error ?: uiState.downloadError)?.let { error ->
            snackbarHostState.showSnackbar(error)
            viewModel.clearError()
        }
    }

    // Show download success
    LaunchedEffect(uiState.downloadSuccess) {
        if (uiState.downloadSuccess) {
            snackbarHostState.showSnackbar("Invoice saved to Downloads folder")
            viewModel.clearDownloadSuccess()
        }
    }

    Scaffold(
        snackbarHost = { SnackbarHost(snackbarHostState) }
    ) { _ ->
        Box(
            modifier = Modifier
                .fillMaxSize()
        ) {
            when {
                uiState.isLoading -> {
                    LoadingIndicator(
                        modifier = Modifier.fillMaxSize()
                    )
                }
                uiState.error != null && uiState.invoice == null -> {
                    ErrorView(
                        message = uiState.error ?: "Unknown error",
                        onRetry = { viewModel.loadInvoiceDetails() },
                        modifier = Modifier.fillMaxSize()
                    )
                }
                uiState.invoice != null -> {
                    InvoiceDetailsContent(
                        invoice = uiState.invoice!!,
                        isDownloading = uiState.isDownloading,
                        onDownload = { viewModel.downloadInvoice() }
                    )
                }
            }

            GlassBackButton(
                onNavigateBack = onNavigateBack,
                title = stringResource(R.string.invoice_details),
                modifier = Modifier
                    .fillMaxWidth()
                    .background(MaterialTheme.colorScheme.background)
            )
        }
    }
}

@Composable
private fun InvoiceDetailsContent(
    invoice: InvoiceDetail,
    isDownloading: Boolean,
    onDownload: () -> Unit,
    modifier: Modifier = Modifier
) {
    val isDarkTheme = LocalDarkTheme.current

    Column(
        modifier = modifier
            .fillMaxSize()
            .verticalScroll(rememberScrollState())
            .statusBarsPadding()
            .padding(start = 16.dp, end = 16.dp, top = 72.dp, bottom = 32.dp),
        verticalArrangement = Arrangement.spacedBy(16.dp)
    ) {
        // ── Header Card with gradient ──
        InvoiceHeaderCard(invoice = invoice, isDarkTheme = isDarkTheme)

        // ── Quick Info Card (compact key details) ──
        QuickInfoCard(invoice = invoice)

        // ── Amount Breakdown ──
        AmountBreakdownCard(invoice = invoice)

        // ── Status Timeline ──
        StatusTimelineCard(invoice = invoice)

        // ── Notes (conditional) ──
        if (!invoice.adminNotes.isNullOrBlank() || !invoice.bankTransferNote.isNullOrBlank()) {
            NotesCard(invoice = invoice)
        }

        // ── Orders Included ──
        if (invoice.orders.isNotEmpty()) {
            OrdersIncludedCard(invoice = invoice)
        }

        // ── Download Button ──
        CleansiaButton(
            text = stringResource(R.string.download_invoice),
            onClick = onDownload,
            isLoading = isDownloading,
            icon = Icons.Default.Download,
            style = CleansiaButtonStyle.SECONDARY
        )

        Spacer(modifier = Modifier.height(16.dp))
    }
}

// ── Header Card with gradient ──

@Composable
private fun InvoiceHeaderCard(invoice: InvoiceDetail, isDarkTheme: Boolean) {
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
private fun QuickInfoCard(invoice: InvoiceDetail) {
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
private fun AmountBreakdownCard(invoice: InvoiceDetail) {
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

// ── Status Timeline Card ──

@Composable
private fun StatusTimelineCard(invoice: InvoiceDetail) {
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
                        imageVector = Icons.Default.Info,
                        contentDescription = null,
                        tint = MaterialTheme.colorScheme.primary,
                        modifier = Modifier.size(18.dp)
                    )
                }
                Spacer(modifier = Modifier.width(10.dp))
                Text(
                    text = stringResource(R.string.status_timeline),
                    style = MaterialTheme.typography.titleMedium,
                    fontWeight = FontWeight.SemiBold,
                    color = MaterialTheme.colorScheme.onSurface
                )
            }
            Spacer(modifier = Modifier.height(12.dp))

            TimelineItem(
                label = stringResource(R.string.generated),
                date = DateTimeUtils.formatDate(invoice.issueDate),
                isCompleted = true
            )
            if (invoice.approvedAt != null || invoice.status.apiValue >= InvoiceStatus.APPROVED.apiValue) {
                TimelineItem(
                    label = stringResource(R.string.approved),
                    date = DateTimeUtils.formatDate(invoice.approvedAt ?: ""),
                    isCompleted = invoice.status.apiValue >= InvoiceStatus.APPROVED.apiValue
                )
            }
            if (invoice.paidDate.isNotBlank() || invoice.status == InvoiceStatus.PAID) {
                TimelineItem(
                    label = stringResource(R.string.paid),
                    date = DateTimeUtils.formatDate(invoice.paidDate),
                    isCompleted = invoice.status == InvoiceStatus.PAID
                )
            }
        }
    }
}

// ── Notes Card ──

@Composable
private fun NotesCard(invoice: InvoiceDetail) {
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
                        imageVector = Icons.Default.Notes,
                        contentDescription = null,
                        tint = MaterialTheme.colorScheme.primary,
                        modifier = Modifier.size(18.dp)
                    )
                }
                Spacer(modifier = Modifier.width(10.dp))
                Text(
                    text = stringResource(R.string.invoice_notes),
                    style = MaterialTheme.typography.titleMedium,
                    fontWeight = FontWeight.SemiBold,
                    color = MaterialTheme.colorScheme.onSurface
                )
            }
            Spacer(modifier = Modifier.height(12.dp))

            if (!invoice.adminNotes.isNullOrBlank()) {
                NoteBlock(
                    label = stringResource(R.string.admin_notes),
                    text = invoice.adminNotes
                )
            }
            if (!invoice.bankTransferNote.isNullOrBlank()) {
                if (!invoice.adminNotes.isNullOrBlank()) {
                    Spacer(modifier = Modifier.height(8.dp))
                }
                NoteBlock(
                    label = stringResource(R.string.bank_transfer_note),
                    text = invoice.bankTransferNote
                )
            }
        }
    }
}

// ── Orders Included Card ──

@Composable
private fun OrdersIncludedCard(invoice: InvoiceDetail) {
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
                        imageVector = Icons.Default.Description,
                        contentDescription = null,
                        tint = MaterialTheme.colorScheme.primary,
                        modifier = Modifier.size(18.dp)
                    )
                }
                Spacer(modifier = Modifier.width(10.dp))
                Text(
                    text = stringResource(R.string.orders_included),
                    style = MaterialTheme.typography.titleMedium,
                    fontWeight = FontWeight.SemiBold,
                    color = MaterialTheme.colorScheme.onSurface
                )
            }
            Spacer(modifier = Modifier.height(12.dp))

            invoice.orders.forEachIndexed { index, order ->
                OrderItem(order = order)
                if (index < invoice.orders.size - 1) {
                    HorizontalDivider(
                        modifier = Modifier.padding(vertical = 8.dp),
                        color = MaterialTheme.colorScheme.outlineVariant.copy(alpha = 0.5f)
                    )
                }
            }
        }
    }
}

// ── Shared Components ──

@Composable
private fun NoteBlock(label: String, text: String) {
    Column {
        Text(
            text = label,
            style = MaterialTheme.typography.labelMedium,
            color = MaterialTheme.colorScheme.onSurfaceVariant
        )
        Spacer(modifier = Modifier.height(4.dp))
        Box(
            modifier = Modifier
                .fillMaxWidth()
                .clip(RoundedCornerShape(8.dp))
                .background(MaterialTheme.colorScheme.surfaceVariant.copy(alpha = 0.5f))
                .padding(12.dp)
        ) {
            Text(
                text = text,
                style = MaterialTheme.typography.bodyMedium,
                color = MaterialTheme.colorScheme.onSurface
            )
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
private fun TimelineItem(
    label: String,
    date: String,
    isCompleted: Boolean
) {
    Row(
        modifier = Modifier
            .fillMaxWidth()
            .padding(vertical = 6.dp),
        verticalAlignment = Alignment.CenterVertically
    ) {
        Box(
            modifier = Modifier
                .size(28.dp)
                .clip(CircleShape)
                .background(
                    if (isCompleted) CleansiaColors.successContainer
                    else MaterialTheme.colorScheme.surfaceVariant
                ),
            contentAlignment = Alignment.Center
        ) {
            Icon(
                imageVector = if (isCompleted) Icons.Filled.CheckCircle else Icons.Filled.Schedule,
                contentDescription = null,
                tint = if (isCompleted) CleansiaColors.success else MaterialTheme.colorScheme.onSurfaceVariant,
                modifier = Modifier.size(16.dp)
            )
        }
        Spacer(modifier = Modifier.width(12.dp))
        Text(
            text = label,
            style = MaterialTheme.typography.bodyMedium,
            fontWeight = if (isCompleted) FontWeight.Medium else FontWeight.Normal,
            color = MaterialTheme.colorScheme.onSurface,
            modifier = Modifier.weight(1f)
        )
        if (date.isNotBlank()) {
            Text(
                text = date,
                style = MaterialTheme.typography.bodySmall,
                color = MaterialTheme.colorScheme.onSurfaceVariant
            )
        }
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

@Composable
private fun OrderItem(order: InvoiceOrderItem) {
    Row(
        modifier = Modifier.fillMaxWidth(),
        horizontalArrangement = Arrangement.SpaceBetween,
        verticalAlignment = Alignment.CenterVertically
    ) {
        Column(modifier = Modifier.weight(1f)) {
            Text(
                text = "#${order.orderNumber}",
                style = MaterialTheme.typography.bodyMedium,
                fontWeight = FontWeight.Medium,
                color = MaterialTheme.colorScheme.onSurface
            )
            Text(
                text = DateTimeUtils.formatDate(order.completedDate),
                style = MaterialTheme.typography.bodySmall,
                color = MaterialTheme.colorScheme.onSurfaceVariant
            )
        }
        Text(
            text = formatCurrency(order.amount, order.currency),
            style = MaterialTheme.typography.bodyMedium,
            fontWeight = FontWeight.SemiBold,
            color = MaterialTheme.colorScheme.primary
        )
    }
}

private fun formatCurrency(amount: Double, currency: String = "CZK"): String {
    return try {
        val format = NumberFormat.getCurrencyInstance(Locale.getDefault())
        format.currency = Currency.getInstance(currency)
        format.format(amount)
    } catch (e: Exception) {
        "$currency ${String.format("%.2f", amount)}"
    }
}
