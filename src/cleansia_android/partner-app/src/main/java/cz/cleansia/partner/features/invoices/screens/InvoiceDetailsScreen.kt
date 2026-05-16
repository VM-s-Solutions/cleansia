package cz.cleansia.partner.features.invoices.screens

import androidx.compose.foundation.background
import cz.cleansia.partner.ui.theme.LocalDarkTheme
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.statusBarsPadding
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.verticalScroll
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.Download
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Scaffold
import androidx.compose.material3.SnackbarHostState
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.collectAsState
import androidx.compose.runtime.getValue
import androidx.compose.runtime.remember
import androidx.compose.ui.Modifier
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.unit.dp
import androidx.hilt.navigation.compose.hiltViewModel
import cz.cleansia.partner.R
import cz.cleansia.partner.ui.components.CleansiaSnackbarHost
import cz.cleansia.partner.domain.models.invoices.InvoiceDetail
import cz.cleansia.partner.features.invoices.components.AmountBreakdownCard
import cz.cleansia.partner.features.invoices.components.InvoiceHeaderCard
import cz.cleansia.partner.features.invoices.components.NotesCard
import cz.cleansia.partner.features.invoices.components.OrdersIncludedCard
import cz.cleansia.partner.features.invoices.components.QuickInfoCard
import cz.cleansia.partner.features.invoices.components.StatusTimelineCard
import cz.cleansia.partner.features.invoices.viewmodels.InvoiceDetailsViewModel
import cz.cleansia.partner.ui.components.CleansiaButton
import cz.cleansia.partner.ui.components.CleansiaButtonStyle
import cz.cleansia.partner.ui.components.ErrorView
import cz.cleansia.partner.ui.components.GlassBackButton
import cz.cleansia.partner.features.invoices.components.InvoiceDetailsSkeleton

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

    Scaffold { _ ->
        Box(
            modifier = Modifier
                .fillMaxSize()
        ) {
            when {
                uiState.isLoading -> {
                    InvoiceDetailsSkeleton(
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

            CleansiaSnackbarHost(hostState = snackbarHostState)
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
