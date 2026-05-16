package cz.cleansia.partner.features.dashboard.components.analytics

import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
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
import cz.cleansia.partner.R
import cz.cleansia.partner.domain.models.dashboard.OrderStatusDistribution
import cz.cleansia.partner.ui.components.ChartLegendItem
import cz.cleansia.partner.ui.components.DonutChart
import cz.cleansia.partner.ui.components.DonutChartData
import cz.cleansia.partner.ui.theme.CleansiaColors
import cz.cleansia.partner.ui.theme.LocalDarkTheme

@Composable
internal fun OrderStatusCard(distribution: OrderStatusDistribution) {
    val total = distribution.completed + distribution.inProgress + distribution.cancelled + distribution.pending
    val isDark = LocalDarkTheme.current
    val completedColor = if (isDark) Color(0xFF16A34A) else CleansiaColors.Success
    val inProgressColor = if (isDark) Color(0xFF2563EB) else CleansiaColors.Info
    val cancelledColor = if (isDark) Color(0xFFDC2626) else MaterialTheme.colorScheme.error
    val pendingColor = if (isDark) Color(0xFFD97706) else CleansiaColors.Warning

    val chartData = listOf(
        DonutChartData(stringResource(R.string.completed_orders), distribution.completed.toFloat(), completedColor),
        DonutChartData(stringResource(R.string.in_progress_orders), distribution.inProgress.toFloat(), inProgressColor),
        DonutChartData(stringResource(R.string.cancelled_orders), distribution.cancelled.toFloat(), cancelledColor),
        DonutChartData(stringResource(R.string.pending_orders), distribution.pending.toFloat(), pendingColor)
    )

    Card(
        modifier = Modifier.fillMaxWidth(),
        colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surface),
        elevation = CardDefaults.cardElevation(defaultElevation = 1.dp)
    ) {
        Column(modifier = Modifier.padding(16.dp)) {
            Text(
                text = stringResource(R.string.order_status_title),
                style = MaterialTheme.typography.titleMedium,
                fontWeight = FontWeight.SemiBold,
                color = MaterialTheme.colorScheme.onSurface
            )

            Spacer(modifier = Modifier.height(16.dp))

            Row(
                modifier = Modifier.fillMaxWidth(),
                horizontalArrangement = Arrangement.Center
            ) {
                DonutChart(
                    data = chartData,
                    modifier = Modifier.size(160.dp),
                    strokeWidth = 24.dp
                ) {
                    Column(horizontalAlignment = Alignment.CenterHorizontally) {
                        Text(
                            text = "$total",
                            style = MaterialTheme.typography.headlineSmall,
                            fontWeight = FontWeight.Bold,
                            color = MaterialTheme.colorScheme.onSurface
                        )
                        Text(
                            text = stringResource(R.string.total),
                            style = MaterialTheme.typography.labelSmall,
                            color = MaterialTheme.colorScheme.onSurfaceVariant
                        )
                    }
                }
            }

            Spacer(modifier = Modifier.height(16.dp))

            chartData.forEach { item ->
                ChartLegendItem(
                    color = item.color,
                    label = item.label,
                    value = "${item.value.toInt()}",
                    modifier = Modifier
                        .fillMaxWidth()
                        .padding(vertical = 2.dp)
                )
            }
        }
    }
}
