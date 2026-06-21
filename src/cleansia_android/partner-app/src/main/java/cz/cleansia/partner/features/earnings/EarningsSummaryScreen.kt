package cz.cleansia.partner.features.earnings

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
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.outlined.ArrowBack
import androidx.compose.material.icons.automirrored.outlined.KeyboardArrowRight
import androidx.compose.material.icons.automirrored.outlined.ReceiptLong
import androidx.compose.material.icons.outlined.AccountBalanceWallet
import androidx.compose.material.icons.outlined.CalendarMonth
import androidx.compose.material.icons.outlined.EventAvailable
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Scaffold
import androidx.compose.material3.Text
import androidx.compose.material3.TopAppBar
import androidx.compose.material3.TopAppBarDefaults
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.remember
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.vector.ImageVector
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.hilt.navigation.compose.hiltViewModel
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import cz.cleansia.core.ui.theme.Spacing
import cz.cleansia.partner.R
import cz.cleansia.partner.api.model.DashboardStatsDto
import java.time.LocalDate
import java.time.ZonedDateTime
import java.time.format.DateTimeFormatter
import java.util.Currency
import java.util.Locale

/**
 * "Pay & Earnings" summary — the destination from the dashboard's
 * earnings card.
 *
 * Why this exists: the old flow jumped straight to InvoicesListScreen,
 * which is empty for any cleaner whose first pay period hasn't closed
 * yet — confusing and unhelpful. This screen always has meaningful
 * content (today / week / month earnings, jobs done, pay-period
 * progress, next payout date) and offers "View all invoices" as a
 * deliberate drill-down for cleaners who do have history.
 */
@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun EarningsSummaryScreen(
    onNavigateBack: () -> Unit,
    onOpenInvoices: () -> Unit,
    viewModel: EarningsSummaryViewModel = hiltViewModel(),
) {
    val uiState by viewModel.uiState.collectAsStateWithLifecycle()

    Scaffold(
        topBar = {
            TopAppBar(
                title = {
                    Text(
                        text = stringResource(R.string.earnings_title),
                        style = MaterialTheme.typography.titleLarge,
                    )
                },
                navigationIcon = {
                    IconButton(onClick = onNavigateBack) {
                        Icon(
                            imageVector = Icons.AutoMirrored.Outlined.ArrowBack,
                            contentDescription = stringResource(R.string.back),
                        )
                    }
                },
                colors = TopAppBarDefaults.topAppBarColors(
                    containerColor = MaterialTheme.colorScheme.background,
                    titleContentColor = MaterialTheme.colorScheme.onBackground,
                    navigationIconContentColor = MaterialTheme.colorScheme.onBackground,
                ),
            )
        },
        containerColor = MaterialTheme.colorScheme.background,
    ) { paddingValues ->
        if (uiState is EarningsSummaryUiState.Loading) {
            Box(
                modifier = Modifier
                    .fillMaxSize()
                    .padding(paddingValues),
                contentAlignment = Alignment.Center,
            ) {
                CircularProgressIndicator()
            }
            return@Scaffold
        }

        val stats = (uiState as? EarningsSummaryUiState.Loaded)?.stats
        Column(
            modifier = Modifier
                .fillMaxSize()
                .padding(paddingValues)
                .padding(horizontal = Spacing.M)
                .padding(bottom = Spacing.L),
            verticalArrangement = Arrangement.spacedBy(Spacing.M),
        ) {
            Spacer(Modifier.height(Spacing.XS))
            HeadlineEarningsCard(stats = stats)
            BreakdownGrid(stats = stats)
            stats?.let { PayPeriodCard(it) }
            InvoicesEntryCard(onClick = onOpenInvoices)
        }
    }
}

/**
 * Hero card — current pay-period earnings, with "estimated" footnote
 * when the period is still open. The big number on this screen.
 */
@Composable
private fun HeadlineEarningsCard(stats: DashboardStatsDto?) {
    val symbol = remember(stats?.currencyCode) { currencySymbol(stats?.currencyCode) }
    val amount = stats?.currentPeriodEarnings?.toDouble() ?: 0.0
    val isEstimate = stats?.latestInvoiceStatus.isNullOrBlank()

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
                text = stringResource(R.string.earnings_current_period),
                style = MaterialTheme.typography.labelMedium.copy(fontWeight = FontWeight.SemiBold),
                color = MaterialTheme.colorScheme.primary,
            )
            Spacer(Modifier.height(2.dp))
            Text(
                text = formatMoney(amount, symbol),
                style = MaterialTheme.typography.headlineMedium.copy(fontWeight = FontWeight.Bold),
                color = MaterialTheme.colorScheme.onSurface,
            )
            if (isEstimate) {
                Text(
                    text = stringResource(R.string.earnings_estimate_helper),
                    style = MaterialTheme.typography.bodySmall,
                    color = MaterialTheme.colorScheme.onSurfaceVariant,
                )
            }
        }
    }
}

/** Today / Week / Last-month numbers in a single card. */
@Composable
private fun BreakdownGrid(stats: DashboardStatsDto?) {
    val symbol = remember(stats?.currencyCode) { currencySymbol(stats?.currencyCode) }
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
        BreakdownRow(
            label = stringResource(R.string.earnings_today),
            value = formatMoney(stats?.todayEarnings?.toDouble() ?: 0.0, symbol),
            secondary = stringResource(
                R.string.earnings_jobs_done_count,
                stats?.todayCompletedCount ?: 0,
            ),
        )
        Divider()
        BreakdownRow(
            label = stringResource(R.string.earnings_this_week),
            value = formatMoney(stats?.weekEarnings?.toDouble() ?: 0.0, symbol),
            secondary = stringResource(
                R.string.earnings_jobs_done_count,
                stats?.weekCompletedCount ?: 0,
            ),
        )
        Divider()
        BreakdownRow(
            label = stringResource(R.string.earnings_last_month),
            value = formatMoney(stats?.lastMonthEarnings?.toDouble() ?: 0.0, symbol),
            secondary = stringResource(
                R.string.earnings_jobs_done_count,
                stats?.lastMonthCompletedOrders ?: 0,
            ),
        )
    }
}

@Composable
private fun BreakdownRow(label: String, value: String, secondary: String) {
    Row(
        modifier = Modifier
            .fillMaxWidth()
            .padding(vertical = Spacing.S),
        verticalAlignment = Alignment.CenterVertically,
    ) {
        Column(modifier = Modifier.weight(1f)) {
            Text(
                text = label,
                style = MaterialTheme.typography.bodyLarge.copy(fontWeight = FontWeight.SemiBold),
                color = MaterialTheme.colorScheme.onSurface,
            )
            Text(
                text = secondary,
                style = MaterialTheme.typography.bodySmall,
                color = MaterialTheme.colorScheme.onSurfaceVariant,
            )
        }
        Text(
            text = value,
            style = MaterialTheme.typography.titleMedium.copy(fontWeight = FontWeight.Bold),
            color = MaterialTheme.colorScheme.onSurface,
        )
    }
}

/**
 * Pay-period progress card — start/end dates + days remaining and the
 * expected next payout date. Hidden if no active period (the backend
 * returns null while the cleaner is between periods).
 */
@Composable
private fun PayPeriodCard(stats: DashboardStatsDto) {
    val start = parseIsoDate(stats.currentPayPeriodStart) ?: return
    val end = parseIsoDate(stats.currentPayPeriodEnd) ?: return
    val payout = parseIsoDate(stats.nextPayoutDate)
    val today = LocalDate.now()
    val daysLeft = (java.time.temporal.ChronoUnit.DAYS.between(today, end)).toInt().coerceAtLeast(0)

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
                    text = stringResource(R.string.earnings_pay_period),
                    style = MaterialTheme.typography.labelMedium.copy(fontWeight = FontWeight.SemiBold),
                    color = MaterialTheme.colorScheme.primary,
                )
                Text(
                    text = "${formatShort(start)} – ${formatShort(end)}",
                    style = MaterialTheme.typography.titleMedium.copy(fontWeight = FontWeight.SemiBold),
                    color = MaterialTheme.colorScheme.onSurface,
                )
                Text(
                    text = stringResource(R.string.earnings_days_remaining, daysLeft),
                    style = MaterialTheme.typography.bodySmall,
                    color = MaterialTheme.colorScheme.onSurfaceVariant,
                )
            }
        }
        if (payout != null) {
            Spacer(Modifier.height(Spacing.M))
            Divider()
            Spacer(Modifier.height(Spacing.M))
            Row(verticalAlignment = Alignment.CenterVertically) {
                Icon(
                    imageVector = Icons.Outlined.EventAvailable,
                    contentDescription = null,
                    tint = MaterialTheme.colorScheme.onSurfaceVariant,
                    modifier = Modifier.size(18.dp),
                )
                Spacer(Modifier.width(8.dp))
                Text(
                    text = stringResource(R.string.earnings_next_payout),
                    style = MaterialTheme.typography.bodyMedium,
                    color = MaterialTheme.colorScheme.onSurfaceVariant,
                    modifier = Modifier.weight(1f),
                )
                Text(
                    text = formatShort(payout),
                    style = MaterialTheme.typography.bodyMedium.copy(fontWeight = FontWeight.SemiBold),
                    color = MaterialTheme.colorScheme.onSurface,
                )
            }
        }
    }
}

/**
 * Entry-point row to InvoicesListScreen. Renders as a regular flat
 * card row so it sits in family with the earnings cards above; tap
 * navigates to the dedicated invoice list.
 */
@Composable
private fun InvoicesEntryCard(onClick: () -> Unit) {
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
            .clickable { onClick() }
            .padding(Spacing.L),
        verticalAlignment = Alignment.CenterVertically,
    ) {
        IconHalo(icon = Icons.AutoMirrored.Outlined.ReceiptLong)
        Spacer(Modifier.width(Spacing.M))
        Column(modifier = Modifier.weight(1f)) {
            Text(
                text = stringResource(R.string.earnings_view_invoices),
                style = MaterialTheme.typography.bodyLarge.copy(fontWeight = FontWeight.SemiBold),
                color = MaterialTheme.colorScheme.onSurface,
            )
            Text(
                text = stringResource(R.string.earnings_view_invoices_subtitle),
                style = MaterialTheme.typography.bodySmall,
                color = MaterialTheme.colorScheme.onSurfaceVariant,
            )
        }
        Icon(
            imageVector = Icons.AutoMirrored.Outlined.KeyboardArrowRight,
            contentDescription = null,
            tint = MaterialTheme.colorScheme.onSurfaceVariant,
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

private val shortDateFormatter = DateTimeFormatter.ofPattern("d MMM", Locale.getDefault())

private fun formatShort(date: LocalDate): String = date.format(shortDateFormatter)

private fun parseIsoDate(iso: String?): LocalDate? {
    if (iso.isNullOrBlank()) return null
    return runCatching { ZonedDateTime.parse(iso).toLocalDate() }
        .getOrNull()
        ?: runCatching { LocalDate.parse(iso) }.getOrNull()
}

/**
 * Best-effort ISO → symbol lookup. Falls back to the ISO code itself
 * when JDK doesn't know it, which is acceptable on this screen — the
 * raw code ("USD") is still readable.
 */
private fun currencySymbol(code: String?): String {
    if (code.isNullOrBlank()) return ""
    return runCatching { Currency.getInstance(code).getSymbol(Locale.getDefault()) }
        .getOrNull()
        ?: code
}

private fun formatMoney(amount: Double, symbol: String): String {
    val formatted = String.format(Locale.getDefault(), "%,.0f", amount)
    return if (symbol.isBlank()) formatted else "$formatted $symbol"
}
