package cz.cleansia.partner.features.invoices.screens

import android.content.ClipData
import android.content.ClipboardManager
import android.content.Context
import android.content.Intent
import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.clickable
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
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.verticalScroll
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.outlined.ArrowBack
import androidx.compose.material.icons.automirrored.outlined.Notes
import androidx.compose.material.icons.outlined.AccountBalanceWallet
import androidx.compose.material.icons.outlined.CalendarMonth
import androidx.compose.material.icons.outlined.ContentCopy
import androidx.compose.material.icons.outlined.PictureAsPdf
import androidx.compose.material.icons.outlined.VpnKey
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.collectAsState
import androidx.compose.runtime.getValue
import androidx.compose.runtime.remember
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.vector.ImageVector
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.text.font.FontFamily
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.hilt.navigation.compose.hiltViewModel
import cz.cleansia.core.ui.components.CleansiaPrimaryButton
import cz.cleansia.core.ui.theme.Spacing
import cz.cleansia.partner.R
import cz.cleansia.partner.api.model.EmployeeInvoiceDetailDto
import cz.cleansia.partner.features.invoices.components.InvoiceStatusBadge
import cz.cleansia.partner.features.invoices.viewmodels.InvoiceDetailsViewModel
import java.time.LocalDate
import java.time.ZonedDateTime
import java.time.format.DateTimeFormatter
import java.util.Currency
import java.util.Locale

/**
 * Invoice detail — matches the EarningsSummaryScreen / InvoicesListScreen
 * visual language:
 *  - Flat 16dp-rounded `surface` cards with 1dp `outlineVariant` border
 *    (no Material elevation).
 *  - 44dp circular `primaryContainer` IconHalo for the leading glyph.
 *  - `primary`-tinted micro-label on top of headline numbers.
 *
 * Card stack (top to bottom):
 *  1. Hero — IconHalo(wallet) + grand total (headlineMedium bold) + status pill.
 *  2. Breakdown — subTotal + bonus + deduction rows, divider, total bold.
 *  3. Period — IconHalo(calendar) + payPeriodLabel + jobs micro + dates list.
 *  4. References — IconHalo(key) + invoice #/variable symbol/payment ref,
 *     monospace value, copy-to-clipboard on tap.
 *  5. Notes (optional) — only when adminNotes or bankTransferNote present.
 *  6. CleansiaPrimaryButton — open PDF (unchanged handler).
 */
@Composable
fun InvoiceDetailsScreen(
    onNavigateBack: () -> Unit,
    viewModel: InvoiceDetailsViewModel = hiltViewModel(),
) {
    val uiState by viewModel.uiState.collectAsState()
    val context = LocalContext.current

    // Hand the downloaded PDF to the system viewer once it's on disk. Uses
    // Intent.ACTION_VIEW with the FileProvider URI per the design decision —
    // user sees it in their PDF reader, can save/share from there.
    LaunchedEffect(uiState.downloadedFileUri) {
        val uri = uiState.downloadedFileUri ?: return@LaunchedEffect
        val intent = Intent(Intent.ACTION_VIEW).apply {
            setDataAndType(uri, "application/pdf")
            addFlags(Intent.FLAG_GRANT_READ_URI_PERMISSION)
            addFlags(Intent.FLAG_ACTIVITY_NEW_TASK)
        }
        runCatching { context.startActivity(intent) }
            .onFailure { viewModel.notifyNoPdfViewer() }
        viewModel.clearDownloadedUri()
    }

    Column(
        modifier = Modifier
            .fillMaxSize()
            .background(MaterialTheme.colorScheme.background),
    ) {
        Row(
            modifier = Modifier
                .fillMaxWidth()
                .padding(horizontal = Spacing.XS, vertical = Spacing.XS),
            verticalAlignment = Alignment.CenterVertically,
        ) {
            IconButton(onClick = onNavigateBack) {
                Icon(
                    imageVector = Icons.AutoMirrored.Outlined.ArrowBack,
                    contentDescription = stringResource(R.string.back),
                    tint = MaterialTheme.colorScheme.onBackground,
                )
            }
            Spacer(Modifier.width(Spacing.S))
            Text(
                text = stringResource(R.string.invoice_details),
                style = MaterialTheme.typography.titleLarge,
                color = MaterialTheme.colorScheme.onBackground,
            )
        }

        when {
            uiState.isLoading && uiState.invoice == null -> {
                Box(Modifier.fillMaxSize(), contentAlignment = Alignment.Center) {
                    CircularProgressIndicator()
                }
            }
            uiState.invoice != null -> {
                val invoice = uiState.invoice!!
                val onCopy: (String, String) -> Unit = { label, value ->
                    copyToClipboard(context, label, value)
                    viewModel.notifyCopied()
                }

                Column(
                    modifier = Modifier
                        .fillMaxSize()
                        .verticalScroll(rememberScrollState())
                        .padding(horizontal = Spacing.M)
                        .padding(bottom = Spacing.L),
                    verticalArrangement = Arrangement.spacedBy(Spacing.M),
                ) {
                    HeroCard(invoice)
                    BreakdownCard(invoice)
                    PeriodCard(invoice)
                    ReferencesCard(invoice, onCopy = onCopy)
                    NotesCard(invoice)

                    Spacer(Modifier.height(Spacing.XS))

                    CleansiaPrimaryButton(
                        text = stringResource(R.string.open_invoice_pdf),
                        onClick = { viewModel.download() },
                        loading = uiState.isDownloading,
                        enabled = !uiState.isDownloading,
                        trailingIcon = Icons.Outlined.PictureAsPdf,
                    )
                }
            }
        }
    }
}

// --- cards ---

@Composable
private fun HeroCard(invoice: EmployeeInvoiceDetailDto) {
    val symbol = remember(invoice.currencyCode) { currencySymbol(invoice.currencyCode) }
    Row(
        modifier = Modifier
            .fillMaxWidth()
            .clip(RoundedCornerShape(16.dp))
            .background(MaterialTheme.colorScheme.surface)
            .border(
                1.dp,
                MaterialTheme.colorScheme.outlineVariant,
                RoundedCornerShape(16.dp),
            )
            .padding(Spacing.L),
        verticalAlignment = Alignment.CenterVertically,
    ) {
        IconHalo(icon = Icons.Outlined.AccountBalanceWallet)
        Spacer(Modifier.width(Spacing.M))
        Column(modifier = Modifier.weight(1f)) {
            Text(
                text = stringResource(R.string.invoice_hero_total),
                style = MaterialTheme.typography.labelMedium.copy(fontWeight = FontWeight.SemiBold),
                color = MaterialTheme.colorScheme.primary,
            )
            Spacer(Modifier.height(2.dp))
            Text(
                text = formatMoney(invoice.totalAmount ?: 0.0, symbol),
                style = MaterialTheme.typography.headlineMedium.copy(fontWeight = FontWeight.Bold),
                color = MaterialTheme.colorScheme.onSurface,
            )
            invoice.invoiceNumber?.takeIf { it.isNotBlank() }?.let { number ->
                Spacer(Modifier.height(2.dp))
                Text(
                    text = number,
                    style = MaterialTheme.typography.bodySmall,
                    color = MaterialTheme.colorScheme.onSurfaceVariant,
                )
            }
        }
        InvoiceStatusBadge(status = invoice.status)
    }
}

@Composable
private fun BreakdownCard(invoice: EmployeeInvoiceDetailDto) {
    val symbol = remember(invoice.currencyCode) { currencySymbol(invoice.currencyCode) }
    Column(
        modifier = Modifier
            .fillMaxWidth()
            .clip(RoundedCornerShape(16.dp))
            .background(MaterialTheme.colorScheme.surface)
            .border(
                1.dp,
                MaterialTheme.colorScheme.outlineVariant,
                RoundedCornerShape(16.dp),
            )
            .padding(Spacing.L),
    ) {
        Text(
            text = stringResource(R.string.invoice_breakdown_section),
            style = MaterialTheme.typography.labelMedium.copy(fontWeight = FontWeight.SemiBold),
            color = MaterialTheme.colorScheme.primary,
        )
        Spacer(Modifier.height(Spacing.S))

        MoneyRow(stringResource(R.string.subtotal), invoice.subTotal, symbol)
        invoice.bonusAmount?.takeIf { it != 0.0 }?.let {
            MoneyRow(stringResource(R.string.bonus), it, symbol)
        }
        invoice.deductionAmount?.takeIf { it != 0.0 }?.let {
            MoneyRow(stringResource(R.string.deductions), -it, symbol)
        }

        Spacer(Modifier.height(Spacing.S))
        Divider()
        Spacer(Modifier.height(Spacing.S))

        MoneyRow(
            label = stringResource(R.string.total),
            amount = invoice.totalAmount,
            symbol = symbol,
            bold = true,
        )
    }
}

@Composable
private fun PeriodCard(invoice: EmployeeInvoiceDetailDto) {
    val period = invoice.payPeriodLabel?.takeIf { it.isNotBlank() } ?: "—"
    val orders = invoice.totalOrders ?: 0
    val generated = formatDate(invoice.generatedAt)
    val approved = formatDate(invoice.approvedAt)
    val paid = formatDate(invoice.paidAt)
    val hasAnyDate = generated != null || approved != null || paid != null

    Column(
        modifier = Modifier
            .fillMaxWidth()
            .clip(RoundedCornerShape(16.dp))
            .background(MaterialTheme.colorScheme.surface)
            .border(
                1.dp,
                MaterialTheme.colorScheme.outlineVariant,
                RoundedCornerShape(16.dp),
            )
            .padding(Spacing.L),
    ) {
        Row(verticalAlignment = Alignment.CenterVertically) {
            IconHalo(icon = Icons.Outlined.CalendarMonth)
            Spacer(Modifier.width(Spacing.M))
            Column(modifier = Modifier.weight(1f)) {
                Text(
                    text = stringResource(R.string.invoice_period_label),
                    style = MaterialTheme.typography.labelMedium.copy(fontWeight = FontWeight.SemiBold),
                    color = MaterialTheme.colorScheme.primary,
                )
                Text(
                    text = period,
                    style = MaterialTheme.typography.titleMedium.copy(fontWeight = FontWeight.SemiBold),
                    color = MaterialTheme.colorScheme.onSurface,
                )
                if (orders > 0) {
                    Text(
                        text = stringResource(R.string.invoice_period_jobs, orders),
                        style = MaterialTheme.typography.bodySmall,
                        color = MaterialTheme.colorScheme.onSurfaceVariant,
                    )
                }
            }
        }

        if (hasAnyDate) {
            Spacer(Modifier.height(Spacing.M))
            Divider()
            Spacer(Modifier.height(Spacing.M))
            generated?.let { DateRow(stringResource(R.string.invoice_period_generated), it) }
            approved?.let {
                if (generated != null) Spacer(Modifier.height(Spacing.XS))
                DateRow(stringResource(R.string.invoice_period_approved), it)
            }
            paid?.let {
                if (generated != null || approved != null) Spacer(Modifier.height(Spacing.XS))
                DateRow(stringResource(R.string.invoice_period_paid), it)
            }
        }
    }
}

@Composable
private fun ReferencesCard(
    invoice: EmployeeInvoiceDetailDto,
    onCopy: (label: String, value: String) -> Unit,
) {
    val invoiceNumber = invoice.invoiceNumber?.takeIf { it.isNotBlank() }
    val variableSymbol = invoice.variableSymbol?.takeIf { it.isNotBlank() }
    val paymentReference = invoice.paymentReference?.takeIf { it.isNotBlank() }
    val anyRef = invoiceNumber != null || variableSymbol != null || paymentReference != null
    if (!anyRef) return

    val invoiceNumberLabel = stringResource(R.string.invoice_field_invoice_number)
    val variableSymbolLabel = stringResource(R.string.invoice_field_variable_symbol)
    val paymentReferenceLabel = stringResource(R.string.invoice_field_payment_reference)

    Column(
        modifier = Modifier
            .fillMaxWidth()
            .clip(RoundedCornerShape(16.dp))
            .background(MaterialTheme.colorScheme.surface)
            .border(
                1.dp,
                MaterialTheme.colorScheme.outlineVariant,
                RoundedCornerShape(16.dp),
            )
            .padding(Spacing.L),
    ) {
        Row(verticalAlignment = Alignment.CenterVertically) {
            IconHalo(icon = Icons.Outlined.VpnKey)
            Spacer(Modifier.width(Spacing.M))
            Text(
                text = stringResource(R.string.invoice_references_section),
                style = MaterialTheme.typography.labelMedium.copy(fontWeight = FontWeight.SemiBold),
                color = MaterialTheme.colorScheme.primary,
            )
        }
        Spacer(Modifier.height(Spacing.M))

        invoiceNumber?.let { value ->
            CopyableField(
                label = invoiceNumberLabel,
                value = value,
                onClick = { onCopy(invoiceNumberLabel, value) },
            )
        }
        if (invoiceNumber != null && (variableSymbol != null || paymentReference != null)) {
            Spacer(Modifier.height(Spacing.S))
            Divider()
            Spacer(Modifier.height(Spacing.S))
        }
        variableSymbol?.let { value ->
            CopyableField(
                label = variableSymbolLabel,
                value = value,
                onClick = { onCopy(variableSymbolLabel, value) },
            )
        }
        if (variableSymbol != null && paymentReference != null) {
            Spacer(Modifier.height(Spacing.S))
            Divider()
            Spacer(Modifier.height(Spacing.S))
        }
        paymentReference?.let { value ->
            CopyableField(
                label = paymentReferenceLabel,
                value = value,
                onClick = { onCopy(paymentReferenceLabel, value) },
            )
        }
    }
}

@Composable
private fun NotesCard(invoice: EmployeeInvoiceDetailDto) {
    val adminNotes = invoice.adminNotes?.takeIf { it.isNotBlank() }
    val bankNote = invoice.bankTransferNote?.takeIf { it.isNotBlank() }
    if (adminNotes == null && bankNote == null) return

    Column(
        modifier = Modifier
            .fillMaxWidth()
            .clip(RoundedCornerShape(16.dp))
            .background(MaterialTheme.colorScheme.surface)
            .border(
                1.dp,
                MaterialTheme.colorScheme.outlineVariant,
                RoundedCornerShape(16.dp),
            )
            .padding(Spacing.L),
    ) {
        Row(verticalAlignment = Alignment.CenterVertically) {
            IconHalo(icon = Icons.AutoMirrored.Outlined.Notes)
            Spacer(Modifier.width(Spacing.M))
            Text(
                text = stringResource(R.string.invoice_notes),
                style = MaterialTheme.typography.labelMedium.copy(fontWeight = FontWeight.SemiBold),
                color = MaterialTheme.colorScheme.primary,
            )
        }
        Spacer(Modifier.height(Spacing.M))

        adminNotes?.let { text ->
            Text(
                text = stringResource(R.string.invoice_notes_admin),
                style = MaterialTheme.typography.bodySmall.copy(fontWeight = FontWeight.SemiBold),
                color = MaterialTheme.colorScheme.onSurfaceVariant,
            )
            Spacer(Modifier.height(2.dp))
            Text(
                text = text,
                style = MaterialTheme.typography.bodyMedium,
                color = MaterialTheme.colorScheme.onSurface,
            )
        }
        if (adminNotes != null && bankNote != null) {
            Spacer(Modifier.height(Spacing.M))
            Divider()
            Spacer(Modifier.height(Spacing.M))
        }
        bankNote?.let { text ->
            Text(
                text = stringResource(R.string.invoice_notes_bank),
                style = MaterialTheme.typography.bodySmall.copy(fontWeight = FontWeight.SemiBold),
                color = MaterialTheme.colorScheme.onSurfaceVariant,
            )
            Spacer(Modifier.height(2.dp))
            Text(
                text = text,
                style = MaterialTheme.typography.bodyMedium,
                color = MaterialTheme.colorScheme.onSurface,
            )
        }
    }
}

// --- shared row helpers ---

@Composable
private fun MoneyRow(label: String, amount: Double?, symbol: String, bold: Boolean = false) {
    if (amount == null) return
    Row(
        modifier = Modifier
            .fillMaxWidth()
            .padding(vertical = 2.dp),
        horizontalArrangement = Arrangement.SpaceBetween,
    ) {
        Text(
            text = label,
            style = MaterialTheme.typography.bodyMedium,
            fontWeight = if (bold) FontWeight.SemiBold else FontWeight.Normal,
            color = MaterialTheme.colorScheme.onSurface,
        )
        Text(
            text = formatMoney(amount, symbol),
            style = MaterialTheme.typography.bodyMedium,
            fontWeight = if (bold) FontWeight.SemiBold else FontWeight.Normal,
            color = MaterialTheme.colorScheme.onSurface,
        )
    }
}

@Composable
private fun DateRow(label: String, formatted: String) {
    Row(
        modifier = Modifier.fillMaxWidth(),
        horizontalArrangement = Arrangement.SpaceBetween,
        verticalAlignment = Alignment.CenterVertically,
    ) {
        Text(
            text = label,
            style = MaterialTheme.typography.bodyMedium,
            color = MaterialTheme.colorScheme.onSurfaceVariant,
        )
        Text(
            text = formatted,
            style = MaterialTheme.typography.bodyMedium.copy(fontWeight = FontWeight.SemiBold),
            color = MaterialTheme.colorScheme.onSurface,
        )
    }
}

@Composable
private fun CopyableField(label: String, value: String, onClick: () -> Unit) {
    Row(
        modifier = Modifier
            .fillMaxWidth()
            .clip(RoundedCornerShape(8.dp))
            .clickable { onClick() }
            .padding(vertical = Spacing.XS),
        verticalAlignment = Alignment.CenterVertically,
    ) {
        Column(modifier = Modifier.weight(1f)) {
            Text(
                text = label,
                style = MaterialTheme.typography.bodySmall,
                color = MaterialTheme.colorScheme.onSurfaceVariant,
            )
            Spacer(Modifier.height(2.dp))
            Text(
                text = value,
                style = MaterialTheme.typography.bodyMedium.copy(
                    fontFamily = FontFamily.Monospace,
                    fontWeight = FontWeight.SemiBold,
                ),
                color = MaterialTheme.colorScheme.onSurface,
            )
        }
        Spacer(Modifier.width(Spacing.S))
        Icon(
            imageVector = Icons.Outlined.ContentCopy,
            contentDescription = stringResource(R.string.invoice_field_copy),
            tint = MaterialTheme.colorScheme.primary,
            modifier = Modifier.size(18.dp),
        )
    }
}

@Composable
private fun IconHalo(icon: ImageVector) {
    Box(
        modifier = Modifier
            .size(44.dp)
            .background(MaterialTheme.colorScheme.primaryContainer, CircleShape),
        contentAlignment = Alignment.Center,
    ) {
        Icon(
            imageVector = icon,
            contentDescription = null,
            tint = MaterialTheme.colorScheme.primary,
            modifier = Modifier.size(22.dp),
        )
    }
}

@Composable
private fun Divider() {
    Box(
        modifier = Modifier
            .fillMaxWidth()
            .height(1.dp)
            .background(MaterialTheme.colorScheme.outlineVariant.copy(alpha = 0.5f)),
    )
}

// --- helpers (mirror InvoicesListScreen / EarningsSummaryScreen) ---

private val shortDateFormatter = DateTimeFormatter.ofPattern("d MMM yyyy", Locale.getDefault())

private fun formatDate(iso: String?): String? {
    if (iso.isNullOrBlank()) return null
    val date = runCatching { ZonedDateTime.parse(iso).toLocalDate() }
        .getOrNull()
        ?: runCatching { LocalDate.parse(iso) }.getOrNull()
        ?: return null
    return date.format(shortDateFormatter)
}

private fun currencySymbol(code: String?): String {
    if (code.isNullOrBlank()) return ""
    return runCatching { Currency.getInstance(code).getSymbol(Locale.getDefault()) }
        .getOrNull()
        ?: code
}

private fun formatMoney(amount: Double, symbol: String): String {
    val formatted = String.format(Locale.getDefault(), "%,.2f", amount)
    return if (symbol.isBlank()) formatted else "$formatted $symbol"
}

private fun copyToClipboard(context: Context, label: String, value: String) {
    val clipboard = context.getSystemService(Context.CLIPBOARD_SERVICE) as? ClipboardManager
        ?: return
    clipboard.setPrimaryClip(ClipData.newPlainText(label, value))
}
