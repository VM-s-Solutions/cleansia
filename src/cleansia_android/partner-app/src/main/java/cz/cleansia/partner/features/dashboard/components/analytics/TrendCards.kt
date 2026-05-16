package cz.cleansia.partner.features.dashboard.components.analytics

import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.width
import androidx.compose.material3.Card
import androidx.compose.material3.CardDefaults
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import cz.cleansia.partner.R
import cz.cleansia.partner.core.utils.CurrencyUtils
import cz.cleansia.partner.domain.models.dashboard.MonthlyEarningsTrend
import cz.cleansia.partner.domain.models.dashboard.ServiceRevenueBreakdown
import cz.cleansia.partner.ui.components.BarChartData
import cz.cleansia.partner.ui.components.HorizontalBarChart
import cz.cleansia.partner.ui.components.SparklineChart
import cz.cleansia.partner.ui.theme.CleansiaColors
import cz.cleansia.partner.ui.theme.LocalDarkTheme

@Composable
internal fun MonthlyEarningsTrendCard(trend: MonthlyEarningsTrend, currency: String) {
    val chartData = trend.months.map { it.amount.toFloat() }
    val changeColor = if (trend.monthOverMonthChange >= 0) CleansiaColors.success else MaterialTheme.colorScheme.error
    val changeSign = if (trend.monthOverMonthChange >= 0) "+" else ""

    Card(
        modifier = Modifier.fillMaxWidth(),
        colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surface),
        elevation = CardDefaults.cardElevation(defaultElevation = 1.dp)
    ) {
        Column(modifier = Modifier.padding(16.dp)) {
            Text(
                text = stringResource(R.string.monthly_earnings_trend),
                style = MaterialTheme.typography.titleMedium,
                fontWeight = FontWeight.SemiBold,
                color = MaterialTheme.colorScheme.onSurface
            )

            Spacer(modifier = Modifier.height(12.dp))

            Row(
                modifier = Modifier.fillMaxWidth(),
                horizontalArrangement = Arrangement.SpaceBetween,
                verticalAlignment = Alignment.Bottom
            ) {
                Column {
                    Text(
                        text = stringResource(R.string.total_this_year),
                        style = MaterialTheme.typography.labelSmall,
                        color = MaterialTheme.colorScheme.onSurfaceVariant
                    )
                    Text(
                        text = CurrencyUtils.formatCurrencyCompact(trend.totalThisYear, currency),
                        style = MaterialTheme.typography.titleLarge,
                        fontWeight = FontWeight.Bold,
                        color = MaterialTheme.colorScheme.onSurface
                    )
                    Row(verticalAlignment = Alignment.CenterVertically) {
                        Text(
                            text = "$changeSign${String.format("%.1f", trend.monthOverMonthChange)}%",
                            style = MaterialTheme.typography.labelMedium,
                            fontWeight = FontWeight.Bold,
                            color = changeColor
                        )
                        Spacer(modifier = Modifier.width(4.dp))
                        Text(
                            text = stringResource(R.string.month_over_month),
                            style = MaterialTheme.typography.labelSmall,
                            color = MaterialTheme.colorScheme.onSurfaceVariant
                        )
                    }
                }

                SparklineChart(
                    data = chartData,
                    lineColor = MaterialTheme.colorScheme.primary,
                    fillColor = MaterialTheme.colorScheme.primary.copy(alpha = 0.1f),
                    modifier = Modifier
                        .width(120.dp)
                        .height(48.dp)
                )
            }

            Spacer(modifier = Modifier.height(12.dp))

            // Month labels row
            Row(
                modifier = Modifier.fillMaxWidth(),
                horizontalArrangement = Arrangement.SpaceBetween
            ) {
                trend.months.forEach { month ->
                    Text(
                        text = month.month.take(1),
                        style = MaterialTheme.typography.labelSmall,
                        color = MaterialTheme.colorScheme.onSurfaceVariant,
                        fontSize = 9.sp
                    )
                }
            }
        }
    }
}

@Composable
internal fun ServiceRevenueCard(breakdown: ServiceRevenueBreakdown, currency: String) {
    val isDark = LocalDarkTheme.current
    val colors = if (isDark) {
        listOf(
            Color(0xFF0369A1),  // Darker primary blue
            Color(0xFF7C3AED),  // Darker purple
            Color(0xFFD97706),  // Darker amber
            Color(0xFF16A34A),  // Darker green
            Color(0xFFDC2626),  // Darker red
            Color(0xFF0891B2)   // Darker cyan
        )
    } else {
        listOf(
            MaterialTheme.colorScheme.primary,
            Color(0xFF8B5CF6),  // Purple
            CleansiaColors.Warning,
            CleansiaColors.Success,
            MaterialTheme.colorScheme.error,
            Color(0xFF06B6D4)   // Cyan
        )
    }

    val barData = breakdown.services.sortedByDescending { it.revenue }.mapIndexed { index, service ->
        BarChartData(
            label = service.serviceName,
            value = service.revenue.toFloat(),
            color = colors[index % colors.size]
        )
    }

    Card(
        modifier = Modifier.fillMaxWidth(),
        colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surface),
        elevation = CardDefaults.cardElevation(defaultElevation = 1.dp)
    ) {
        Column(modifier = Modifier.padding(16.dp)) {
            Text(
                text = stringResource(R.string.revenue_by_service),
                style = MaterialTheme.typography.titleMedium,
                fontWeight = FontWeight.SemiBold,
                color = MaterialTheme.colorScheme.onSurface
            )

            Spacer(modifier = Modifier.height(16.dp))

            HorizontalBarChart(
                data = barData,
                barHeight = 20.dp,
                barSpacing = 10.dp,
                valueFormatter = { value ->
                    CurrencyUtils.formatCurrencyCompact(value.toDouble(), currency)
                }
            )

            Spacer(modifier = Modifier.height(12.dp))

            // Order counts
            breakdown.services.sortedByDescending { it.revenue }.forEach { service ->
                Row(
                    modifier = Modifier
                        .fillMaxWidth()
                        .padding(vertical = 1.dp),
                    horizontalArrangement = Arrangement.SpaceBetween
                ) {
                    Text(
                        text = service.serviceName,
                        style = MaterialTheme.typography.labelSmall,
                        color = MaterialTheme.colorScheme.onSurfaceVariant
                    )
                    Text(
                        text = stringResource(R.string.orders_count, service.orderCount),
                        style = MaterialTheme.typography.labelSmall,
                        color = MaterialTheme.colorScheme.onSurfaceVariant
                    )
                }
            }
        }
    }
}
