package cz.cleansia.partner.features.payroll

import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
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
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.verticalScroll
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.outlined.ArrowBack
import androidx.compose.material.icons.automirrored.outlined.ReceiptLong
import androidx.compose.material.icons.outlined.AccountBalanceWallet
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
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
import cz.cleansia.core.ui.components.CleansiaPrimaryButton
import cz.cleansia.core.ui.theme.Spacing
import cz.cleansia.partner.R
import cz.cleansia.partner.data.payroll.OrderPayLine
import cz.cleansia.partner.data.payroll.PeriodPaySummary
import java.time.LocalDate
import java.time.ZoneId
import java.time.ZonedDateTime
import java.time.format.DateTimeFormatter
import java.util.Currency
import java.util.Locale

/**
 * Read-only period pay breakdown — matches the EarningsSummaryScreen /
 * InvoiceDetailScreen visual language (flat 16dp-rounded surface cards,
 * 44dp IconHalo, primary micro-labels). No settlement actions by design:
 * the cleaner sees their own numbers, settlement lives admin-side.
 */
@Composable
fun PeriodPayScreen(
    onNavigateBack: () -> Unit,
    viewModel: PeriodPayViewModel = hiltViewModel(),
) {
    val state by viewModel.state.collectAsStateWithLifecycle()

    PeriodPayScreenContent(
        state = state,
        currencyCode = viewModel.currencyCode,
        onNavigateBack = onNavigateBack,
        onRetry = viewModel::load,
    )
}

@Composable
fun PeriodPayScreenContent(
    state: PeriodPayUiState,
    currencyCode: String?,
    onNavigateBack: () -> Unit,
    onRetry: () -> Unit,
) {
    val statusBarTop = WindowInsets.statusBars.asPaddingValues().calculateTopPadding()

    Column(
        modifier = Modifier
            .fillMaxSize()
            .background(MaterialTheme.colorScheme.background),
    ) {
        Spacer(Modifier.height(statusBarTop))
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
                text = stringResource(R.string.period_pay_title),
                style = MaterialTheme.typography.titleLarge,
                color = MaterialTheme.colorScheme.onBackground,
            )
        }

        when (state) {
            PeriodPayUiState.Loading -> {
                Box(Modifier.fillMaxSize(), contentAlignment = Alignment.Center) {
                    CircularProgressIndicator()
                }
            }
            PeriodPayUiState.Error -> ErrorState(onRetry = onRetry)
            is PeriodPayUiState.Loaded -> {
                val symbol = remember(currencyCode) { currencySymbol(currencyCode) }
                Column(
                    modifier = Modifier
                        .fillMaxSize()
                        .verticalScroll(rememberScrollState())
                        .padding(horizontal = Spacing.M)
                        .padding(bottom = Spacing.L),
                    verticalArrangement = Arrangement.spacedBy(Spacing.M),
                ) {
                    HeroCard(summary = state.summary, symbol = symbol)
                    BreakdownCard(summary = state.summary, symbol = symbol)
                    JobsCard(orderPays = state.summary.orderPays, symbol = symbol)
                }
            }
        }
    }
}

@Composable
private fun HeroCard(summary: PeriodPaySummary, symbol: String) {
    Row(
        modifier = Modifier
            .fillMaxWidth()
            .clip(RoundedCornerShape(16.dp))
            .background(MaterialTheme.colorScheme.surface)
            .border(1.dp, MaterialTheme.colorScheme.outlineVariant, RoundedCornerShape(16.dp))
            .padding(Spacing.L),
        verticalAlignment = Alignment.CenterVertically,
    ) {
        IconHalo(icon = Icons.Outlined.AccountBalanceWallet)
        Spacer(Modifier.width(Spacing.M))
        Column(modifier = Modifier.weight(1f)) {
            Text(
                text = stringResource(R.string.period_pay_hero_label),
                style = MaterialTheme.typography.labelMedium.copy(fontWeight = FontWeight.SemiBold),
                color = MaterialTheme.colorScheme.primary,
            )
            Spacer(Modifier.height(2.dp))
            Text(
                text = formatMoney(summary.grandTotal, symbol),
                style = MaterialTheme.typography.headlineMedium.copy(fontWeight = FontWeight.Bold),
                color = MaterialTheme.colorScheme.onSurface,
            )
            summary.payPeriodLabel?.takeIf { it.isNotBlank() }?.let { label ->
                Spacer(Modifier.height(2.dp))
                Text(
                    text = label,
                    style = MaterialTheme.typography.bodySmall,
                    color = MaterialTheme.colorScheme.onSurfaceVariant,
                )
            }
            if (summary.totalOrders > 0) {
                Text(
                    text = stringResource(R.string.period_pay_jobs_count, summary.totalOrders),
                    style = MaterialTheme.typography.bodySmall,
                    color = MaterialTheme.colorScheme.onSurfaceVariant,
                )
            }
        }
    }
}

@Composable
private fun BreakdownCard(summary: PeriodPaySummary, symbol: String) {
    Column(
        modifier = Modifier
            .fillMaxWidth()
            .clip(RoundedCornerShape(16.dp))
            .background(MaterialTheme.colorScheme.surface)
            .border(1.dp, MaterialTheme.colorScheme.outlineVariant, RoundedCornerShape(16.dp))
            .padding(Spacing.L),
    ) {
        Text(
            text = stringResource(R.string.period_pay_breakdown_section),
            style = MaterialTheme.typography.labelMedium.copy(fontWeight = FontWeight.SemiBold),
            color = MaterialTheme.colorScheme.primary,
        )
        Spacer(Modifier.height(Spacing.S))

        MoneyRow(stringResource(R.string.period_pay_base), summary.totalBasePay, symbol)
        if (summary.totalExtrasPay != 0.0) {
            MoneyRow(stringResource(R.string.period_pay_extras), summary.totalExtrasPay, symbol)
        }
        if (summary.totalExpensesPay != 0.0) {
            MoneyRow(stringResource(R.string.period_pay_expenses), summary.totalExpensesPay, symbol)
        }
        if (summary.totalBonusPay != 0.0) {
            MoneyRow(stringResource(R.string.bonus), summary.totalBonusPay, symbol)
        }
        if (summary.totalDeductionPay != 0.0) {
            MoneyRow(stringResource(R.string.deductions), -summary.totalDeductionPay, symbol)
        }

        Spacer(Modifier.height(Spacing.S))
        Divider()
        Spacer(Modifier.height(Spacing.S))

        MoneyRow(
            label = stringResource(R.string.total),
            amount = summary.grandTotal,
            symbol = symbol,
            bold = true,
        )
    }
}

@Composable
private fun JobsCard(orderPays: List<OrderPayLine>, symbol: String) {
    Column(
        modifier = Modifier
            .fillMaxWidth()
            .clip(RoundedCornerShape(16.dp))
            .background(MaterialTheme.colorScheme.surface)
            .border(1.dp, MaterialTheme.colorScheme.outlineVariant, RoundedCornerShape(16.dp))
            .padding(Spacing.L),
    ) {
        Text(
            text = stringResource(R.string.period_pay_jobs_section),
            style = MaterialTheme.typography.labelMedium.copy(fontWeight = FontWeight.SemiBold),
            color = MaterialTheme.colorScheme.primary,
        )
        Spacer(Modifier.height(Spacing.S))

        if (orderPays.isEmpty()) {
            Text(
                text = stringResource(R.string.period_pay_empty),
                style = MaterialTheme.typography.bodyMedium,
                color = MaterialTheme.colorScheme.onSurfaceVariant,
            )
        } else {
            orderPays.forEachIndexed { index, line ->
                if (index > 0) {
                    Spacer(Modifier.height(Spacing.S))
                    Divider()
                    Spacer(Modifier.height(Spacing.S))
                }
                JobRow(line = line, symbol = symbol)
            }
        }
    }
}

@Composable
private fun JobRow(line: OrderPayLine, symbol: String) {
    Row(verticalAlignment = Alignment.CenterVertically) {
        Icon(
            imageVector = Icons.AutoMirrored.Outlined.ReceiptLong,
            contentDescription = null,
            tint = MaterialTheme.colorScheme.onSurfaceVariant,
            modifier = Modifier.size(18.dp),
        )
        Spacer(Modifier.width(Spacing.S))
        Column(modifier = Modifier.weight(1f)) {
            Text(
                text = line.orderNumber?.takeIf { it.isNotBlank() } ?: "—",
                style = MaterialTheme.typography.bodyLarge.copy(fontWeight = FontWeight.SemiBold),
                color = MaterialTheme.colorScheme.onSurface,
            )
            formatDate(line.createdOn)?.let { date ->
                Text(
                    text = date,
                    style = MaterialTheme.typography.bodySmall,
                    color = MaterialTheme.colorScheme.onSurfaceVariant,
                )
            }
        }
        Text(
            text = formatMoney(line.totalPay, symbol),
            style = MaterialTheme.typography.titleMedium.copy(fontWeight = FontWeight.SemiBold),
            color = MaterialTheme.colorScheme.onSurface,
        )
    }
}

@Composable
private fun MoneyRow(label: String, amount: Double?, symbol: String, bold: Boolean = false) {
    Row(
        modifier = Modifier
            .fillMaxWidth()
            .padding(vertical = 4.dp),
        horizontalArrangement = Arrangement.SpaceBetween,
        verticalAlignment = Alignment.CenterVertically,
    ) {
        Text(
            text = label,
            style = if (bold) {
                MaterialTheme.typography.bodyLarge.copy(fontWeight = FontWeight.Bold)
            } else {
                MaterialTheme.typography.bodyMedium
            },
            color = if (bold) MaterialTheme.colorScheme.onSurface else MaterialTheme.colorScheme.onSurfaceVariant,
        )
        Text(
            text = formatMoney(amount ?: 0.0, symbol),
            style = if (bold) {
                MaterialTheme.typography.titleMedium.copy(fontWeight = FontWeight.Bold)
            } else {
                MaterialTheme.typography.bodyMedium
            },
            color = MaterialTheme.colorScheme.onSurface,
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

@Composable
private fun ErrorState(onRetry: () -> Unit) {
    Column(
        modifier = Modifier
            .fillMaxSize()
            .padding(32.dp),
        horizontalAlignment = Alignment.CenterHorizontally,
        verticalArrangement = Arrangement.Center,
    ) {
        Text(
            text = stringResource(R.string.period_pay_error),
            style = MaterialTheme.typography.bodyMedium,
            color = MaterialTheme.colorScheme.onSurfaceVariant,
        )
        Spacer(Modifier.height(Spacing.L))
        CleansiaPrimaryButton(
            text = stringResource(R.string.retry),
            onClick = onRetry,
        )
    }
}

// --- formatting helpers (mirror EarningsSummaryScreen / InvoicesListScreen) ---

private val shortDateFormatter = DateTimeFormatter.ofPattern("d MMM yyyy", Locale.getDefault())

private fun formatDate(iso: String?): String? {
    if (iso.isNullOrBlank()) return null
    val date = runCatching {
        ZonedDateTime.parse(iso).withZoneSameInstant(ZoneId.systemDefault()).toLocalDate()
    }.getOrNull()
        ?: runCatching { java.time.LocalDateTime.parse(iso).toLocalDate() }.getOrNull()
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
