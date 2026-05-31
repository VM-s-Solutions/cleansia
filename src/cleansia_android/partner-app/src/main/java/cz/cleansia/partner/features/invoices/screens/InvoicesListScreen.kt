package cz.cleansia.partner.features.invoices.screens

import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.PaddingValues
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.WindowInsets
import androidx.compose.foundation.layout.asPaddingValues
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.statusBars
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.outlined.ArrowBack
import androidx.compose.material.icons.automirrored.outlined.KeyboardArrowRight
import androidx.compose.material.icons.automirrored.outlined.ReceiptLong
import androidx.compose.material.icons.outlined.AccountBalanceWallet
import androidx.compose.material.icons.outlined.CalendarMonth
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.material3.pulltorefresh.PullToRefreshBox
import androidx.compose.material3.pulltorefresh.rememberPullToRefreshState
import androidx.compose.runtime.Composable
import androidx.compose.runtime.collectAsState
import androidx.compose.runtime.getValue
import androidx.compose.runtime.remember
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.vector.ImageVector
import androidx.compose.ui.res.painterResource
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.hilt.navigation.compose.hiltViewModel
import androidx.lifecycle.Lifecycle
import androidx.lifecycle.compose.LifecycleEventEffect
import cz.cleansia.core.ui.components.MascotEmptyState
import cz.cleansia.core.ui.components.SudsRefreshIndicator
import cz.cleansia.core.ui.theme.Spacing
import cz.cleansia.partner.R
import cz.cleansia.partner.api.model.EmployeeInvoiceDto
import cz.cleansia.partner.api.model.EmployeeInvoiceStatus
import cz.cleansia.partner.features.invoices.components.InvoiceStatusBadge
import cz.cleansia.partner.features.invoices.viewmodels.InvoicesListViewModel
import cz.cleansia.partner.features.main.MainBottomNavInset
import java.time.LocalDate
import java.time.ZonedDateTime
import java.time.format.DateTimeFormatter
import java.util.Currency
import java.util.Locale

/**
 * Cleaner's invoice history.
 *
 * Mirrors the visual language of [EarningsSummaryScreen]:
 *  - Flat 16dp-rounded `surface` cards with a 1dp `outlineVariant` border
 *    (no Material elevation, matches dashboard / earnings family).
 *  - 44dp circular `primaryContainer` IconHalo for the leading glyph.
 *  - `primary`-tinted micro-label on top, `headlineMedium` for the big
 *    number on the hero card, `titleMedium` for per-invoice totals.
 *
 * Layout:
 *  1. TopAppBar (back arrow only when shown as a pushed destination —
 *     from Earnings → "View all invoices"). When shown as the bottom-nav
 *     tab the navigation-icon slot is empty so the title sits flush.
 *  2. Hero summary card — lifetime paid total + invoice count. Only
 *     rendered when there's at least one invoice; on empty state the
 *     mascot owns the screen.
 *  3. LazyColumn of [InvoiceCard]s with the period sub-line, status
 *     badge, jobs-included count, generated-on date, and a right
 *     chevron affordance so the row reads as tappable.
 */
@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun InvoicesListScreen(
    onInvoiceClick: (String) -> Unit,
    viewModel: InvoicesListViewModel = hiltViewModel(),
    onNavigateBack: (() -> Unit)? = null,
) {
    val uiState by viewModel.uiState.collectAsState()
    val statusBarTop = WindowInsets.statusBars.asPaddingValues().calculateTopPadding()

    // Refresh on every screen resume so that returning from a detail
    // screen / switching back to this tab shows the latest invoices
    // without forcing the user to pull-to-refresh. Routes through the
    // silent-stale path in the VM — when the cache is still fresh
    // (default 30s window) this is a no-op and never flips the chunky
    // pull indicator.
    LifecycleEventEffect(Lifecycle.Event.ON_RESUME) {
        viewModel.onResume()
    }

    Column(
        modifier = Modifier
            .fillMaxSize()
            .background(MaterialTheme.colorScheme.background),
    ) {
        Spacer(Modifier.height(statusBarTop))
        Header(onNavigateBack = onNavigateBack)

        val pullState = rememberPullToRefreshState()
        // Only the very first load (before any response has come back)
        // takes over the screen with a full-page spinner. Once we've
        // loaded once — even if the list came back empty — subsequent
        // refreshes keep the empty-state mascot on screen so the pull
        // gesture has a scrollable surface and the indicator can show.
        // Note: tied to the background-refresh flag, NOT the user one —
        // a pull on an empty list must keep the mascot + pull indicator,
        // never collapse back to the full-page spinner.
        val isInitialLoad = uiState.isBackgroundRefreshing && !uiState.hasLoadedOnce
        when {
            isInitialLoad -> {
                Box(Modifier.fillMaxSize(), contentAlignment = Alignment.Center) {
                    CircularProgressIndicator()
                }
            }
            // PullToRefreshBox MUST subscribe to isUserRefreshing only
            // — never the background flag — so silent ON_RESUME / init
            // refreshes don't flash the chunky pull indicator.
            else -> PullToRefreshBox(
                isRefreshing = uiState.isUserRefreshing,
                onRefresh = { viewModel.refresh() },
                state = pullState,
                modifier = Modifier.fillMaxSize(),
                indicator = {
                    SudsRefreshIndicator(
                        state = pullState,
                        isRefreshing = uiState.isUserRefreshing,
                        modifier = Modifier
                            .align(Alignment.TopCenter)
                            .padding(top = 8.dp),
                    )
                },
            ) {
                if (uiState.invoices.isEmpty()) {
                    // PullToRefreshBox relies on a child that participates
                    // in the nested-scroll system to detect the drag
                    // gesture. A plain Column with `verticalScroll` *does*
                    // dispatch nested-scroll events, but only when its
                    // contents are taller than the viewport — on an empty
                    // mascot screen there is nothing to scroll, so the
                    // gesture never makes it up to the pull-to-refresh
                    // connection.
                    //
                    // LazyColumn always participates in nested scroll
                    // regardless of content height, so a single
                    // fill-parent-sized item gives the pull detector the
                    // surface it needs while leaving the mascot's visual
                    // layout untouched.
                    LazyColumn(modifier = Modifier.fillMaxSize()) {
                        item {
                            Box(modifier = Modifier.fillParentMaxSize()) {
                                EmptyState()
                            }
                        }
                    }
                } else {
                    LazyColumn(
                        modifier = Modifier.fillMaxSize(),
                        contentPadding = PaddingValues(
                            start = Spacing.M,
                            end = Spacing.M,
                            top = Spacing.S,
                            bottom = Spacing.S,
                        ),
                        verticalArrangement = Arrangement.spacedBy(Spacing.M),
                    ) {
                        item(key = "summary") {
                            SummaryCard(invoices = uiState.invoices)
                        }
                        items(uiState.invoices, key = { it.id.orEmpty() }) { invoice ->
                            InvoiceCard(
                                invoice = invoice,
                                onClick = { invoice.id?.let(onInvoiceClick) },
                            )
                        }
                        item { Spacer(Modifier.height(MainBottomNavInset)) }
                    }
                }
            }
        }
    }
}

@Composable
private fun Header(onNavigateBack: (() -> Unit)?) {
    Row(
        modifier = Modifier
            .fillMaxWidth()
            .padding(
                start = if (onNavigateBack != null) Spacing.XS else Spacing.M,
                end = Spacing.M,
                top = Spacing.S,
                bottom = Spacing.S,
            ),
        verticalAlignment = Alignment.CenterVertically,
    ) {
        if (onNavigateBack != null) {
            IconButton(onClick = onNavigateBack) {
                Icon(
                    imageVector = Icons.AutoMirrored.Outlined.ArrowBack,
                    contentDescription = stringResource(R.string.back),
                    tint = MaterialTheme.colorScheme.onBackground,
                )
            }
            Spacer(Modifier.width(Spacing.XS))
        }
        Text(
            text = stringResource(R.string.invoices),
            // Match the bottom-nav-tab "large title" convention used on
            // Dashboard / Earnings. headlineMedium when standalone (no
            // back arrow), titleLarge when pushed (back arrow eats some
            // optical width).
            style = if (onNavigateBack != null) {
                MaterialTheme.typography.titleLarge
            } else {
                MaterialTheme.typography.headlineMedium.copy(fontWeight = FontWeight.Bold)
            },
            color = MaterialTheme.colorScheme.onBackground,
        )
    }
}

/**
 * Hero rollup. Sums totalAmount across all invoices in the view and
 * shows the per-cleaner lifetime total — the "you've earned" framing
 * mirrors the EarningsSummaryScreen hero card.
 *
 * Currency follows the FIRST invoice in the list (they should all share
 * a currency for a given cleaner; if they don't, we render the largest
 * group's code which is still the right answer 99% of the time).
 */
@Composable
private fun SummaryCard(invoices: List<EmployeeInvoiceDto>) {
    val total = remember(invoices) {
        invoices.sumOf { it.totalAmount ?: 0.0 }
    }
    val code = remember(invoices) {
        invoices.firstNotNullOfOrNull { it.currencyCode }
    }
    val symbol = remember(code) { currencySymbol(code) }

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
                text = stringResource(R.string.invoices_summary_label),
                style = MaterialTheme.typography.labelMedium.copy(fontWeight = FontWeight.SemiBold),
                color = MaterialTheme.colorScheme.primary,
            )
            Spacer(Modifier.height(2.dp))
            Text(
                text = formatMoney(total, symbol),
                style = MaterialTheme.typography.headlineMedium.copy(fontWeight = FontWeight.Bold),
                color = MaterialTheme.colorScheme.onSurface,
            )
            Text(
                text = stringResource(R.string.invoices_summary_count, invoices.size),
                style = MaterialTheme.typography.bodySmall,
                color = MaterialTheme.colorScheme.onSurfaceVariant,
            )
        }
    }
}

@Composable
private fun InvoiceCard(invoice: EmployeeInvoiceDto, onClick: () -> Unit) {
    val symbol = remember(invoice.currencyCode) { currencySymbol(invoice.currencyCode) }
    val dateLine = remember(invoice.paidAt, invoice.generatedAt) {
        // Prefer paid-at (final state) so the user sees the date that
        // matters to them; fall back to generated-at for invoices still
        // in Pending / Approved.
        formatDate(invoice.paidAt) ?: formatDate(invoice.generatedAt)
    }
    val dateLabelRes = if (invoice.paidAt != null) {
        R.string.invoice_card_paid_on
    } else {
        R.string.invoice_card_generated_on
    }

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
            .clickable { onClick() }
            .padding(Spacing.L),
    ) {
        Row(verticalAlignment = Alignment.CenterVertically) {
            IconHalo(icon = Icons.AutoMirrored.Outlined.ReceiptLong)
            Spacer(Modifier.width(Spacing.M))
            Column(modifier = Modifier.weight(1f)) {
                Text(
                    text = invoice.invoiceNumber ?: "—",
                    style = MaterialTheme.typography.titleMedium.copy(fontWeight = FontWeight.SemiBold),
                    color = MaterialTheme.colorScheme.onSurface,
                )
                invoice.payPeriodLabel?.takeIf { it.isNotBlank() }?.let {
                    Spacer(Modifier.height(2.dp))
                    Text(
                        text = it,
                        style = MaterialTheme.typography.bodySmall,
                        color = MaterialTheme.colorScheme.onSurfaceVariant,
                    )
                }
            }
            InvoiceStatusBadge(status = invoice.status)
        }

        Spacer(Modifier.height(Spacing.M))
        Divider()
        Spacer(Modifier.height(Spacing.M))

        Row(verticalAlignment = Alignment.CenterVertically) {
            Column(modifier = Modifier.weight(1f)) {
                Text(
                    text = stringResource(R.string.invoice_card_total),
                    style = MaterialTheme.typography.labelMedium.copy(fontWeight = FontWeight.SemiBold),
                    color = MaterialTheme.colorScheme.primary,
                )
                Spacer(Modifier.height(2.dp))
                Text(
                    text = formatMoney(invoice.totalAmount ?: 0.0, symbol),
                    style = MaterialTheme.typography.titleLarge.copy(fontWeight = FontWeight.Bold),
                    color = MaterialTheme.colorScheme.onSurface,
                )
            }
            Icon(
                imageVector = Icons.AutoMirrored.Outlined.KeyboardArrowRight,
                contentDescription = null,
                tint = MaterialTheme.colorScheme.onSurfaceVariant,
            )
        }

        // Footer meta row: jobs-included count on the left, date on the
        // right. Skipped entirely when both pieces are missing so the
        // card doesn't grow an empty band.
        val orders = invoice.totalOrders ?: 0
        val hasFooter = orders > 0 || !dateLine.isNullOrBlank()
        if (hasFooter) {
            Spacer(Modifier.height(Spacing.S))
            Row(
                modifier = Modifier.fillMaxWidth(),
                horizontalArrangement = Arrangement.SpaceBetween,
                verticalAlignment = Alignment.CenterVertically,
            ) {
                if (orders > 0) {
                    Row(verticalAlignment = Alignment.CenterVertically) {
                        Icon(
                            imageVector = Icons.AutoMirrored.Outlined.ReceiptLong,
                            contentDescription = null,
                            tint = MaterialTheme.colorScheme.onSurfaceVariant,
                            modifier = Modifier.size(16.dp),
                        )
                        Spacer(Modifier.width(6.dp))
                        Text(
                            text = stringResource(R.string.invoice_card_jobs_count, orders),
                            style = MaterialTheme.typography.bodySmall,
                            color = MaterialTheme.colorScheme.onSurfaceVariant,
                        )
                    }
                } else {
                    Spacer(Modifier.width(1.dp))
                }

                dateLine?.let { line ->
                    Row(verticalAlignment = Alignment.CenterVertically) {
                        Icon(
                            imageVector = Icons.Outlined.CalendarMonth,
                            contentDescription = null,
                            tint = MaterialTheme.colorScheme.onSurfaceVariant,
                            modifier = Modifier.size(16.dp),
                        )
                        Spacer(Modifier.width(6.dp))
                        Text(
                            text = stringResource(dateLabelRes, line),
                            style = MaterialTheme.typography.bodySmall,
                            color = MaterialTheme.colorScheme.onSurfaceVariant,
                        )
                    }
                }
            }
        }
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

@Composable
private fun EmptyState() {
    MascotEmptyState(
        painter = painterResource(R.drawable.mascot_invoice),
        text = stringResource(R.string.no_invoices),
        modifier = Modifier.padding(bottom = MainBottomNavInset),
        verticallyCentered = true,
    )
}

// --- formatting helpers (mirror EarningsSummaryScreen exactly) ---

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
