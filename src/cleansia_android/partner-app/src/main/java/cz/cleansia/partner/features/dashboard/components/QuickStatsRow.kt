package cz.cleansia.partner.features.dashboard.components

import androidx.compose.foundation.background
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.filled.ArrowForward
import androidx.compose.material.icons.automirrored.filled.ListAlt
import androidx.compose.material.icons.filled.AttachMoney
import androidx.compose.material.icons.filled.CheckCircle
import androidx.compose.material.icons.filled.KeyboardArrowDown
import androidx.compose.material.icons.filled.KeyboardArrowUp
import androidx.compose.material.icons.filled.PlayArrow
import androidx.compose.material.icons.filled.Remove
import androidx.compose.material3.Card
import androidx.compose.material3.CardDefaults
import androidx.compose.material3.Icon
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.vector.ImageVector
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import cz.cleansia.partner.R
import cz.cleansia.partner.core.utils.CurrencyUtils
import cz.cleansia.partner.domain.models.dashboard.TrendDirection
import cz.cleansia.partner.ui.components.clickableWithHaptic
import cz.cleansia.partner.ui.theme.CleansiaColors

@Composable
internal fun QuickStatsRow(
    availableOrders: Int,
    activeOrders: Int,
    completedThisMonth: Int,
    completionTrend: cz.cleansia.partner.domain.models.dashboard.TrendData?,
    pendingEarnings: Double,
    currency: String,
    onAvailableOrdersClick: () -> Unit,
    onActiveOrdersClick: () -> Unit,
    onCompletedClick: () -> Unit,
    onEarningsClick: () -> Unit
) {
    Column(
        verticalArrangement = Arrangement.spacedBy(12.dp)
    ) {
        // First row
        Row(
            modifier = Modifier.fillMaxWidth(),
            horizontalArrangement = Arrangement.spacedBy(12.dp)
        ) {
            StatCard(
                title = stringResource(R.string.available_orders),
                value = availableOrders.toString(),
                icon = Icons.AutoMirrored.Filled.ListAlt,
                iconBackgroundColor = CleansiaColors.infoContainer,
                iconColor = CleansiaColors.onInfoContainer,
                onClick = onAvailableOrdersClick,
                modifier = Modifier.weight(1f)
            )
            StatCard(
                title = stringResource(R.string.my_active_orders),
                value = activeOrders.toString(),
                icon = Icons.Default.PlayArrow,
                iconBackgroundColor = CleansiaColors.warningContainer,
                iconColor = CleansiaColors.onWarningContainer,
                onClick = onActiveOrdersClick,
                modifier = Modifier.weight(1f)
            )
        }

        // Second row
        Row(
            modifier = Modifier.fillMaxWidth(),
            horizontalArrangement = Arrangement.spacedBy(12.dp)
        ) {
            StatCard(
                title = stringResource(R.string.completed_this_month),
                value = completedThisMonth.toString(),
                icon = Icons.Default.CheckCircle,
                iconBackgroundColor = CleansiaColors.successContainer,
                iconColor = CleansiaColors.onSuccessContainer,
                onClick = onCompletedClick,
                trend = completionTrend,
                modifier = Modifier.weight(1f)
            )
            StatCard(
                title = stringResource(R.string.pending_earnings),
                value = CurrencyUtils.formatCurrency(pendingEarnings, currency),
                icon = Icons.Default.AttachMoney,
                iconBackgroundColor = MaterialTheme.colorScheme.primaryContainer,
                iconColor = MaterialTheme.colorScheme.onPrimaryContainer,
                onClick = onEarningsClick,
                modifier = Modifier.weight(1f)
            )
        }
    }
}

@Composable
private fun StatCard(
    title: String,
    value: String,
    icon: ImageVector,
    iconBackgroundColor: Color,
    iconColor: Color,
    onClick: () -> Unit,
    modifier: Modifier = Modifier,
    trend: cz.cleansia.partner.domain.models.dashboard.TrendData? = null
) {
    Card(
        modifier = modifier
            .height(120.dp)
            .clickableWithHaptic { onClick() },
        colors = CardDefaults.cardColors(
            containerColor = MaterialTheme.colorScheme.surface
        ),
        elevation = CardDefaults.cardElevation(defaultElevation = 2.dp)
    ) {
        Column(
            modifier = Modifier
                .fillMaxSize()
                .padding(12.dp),
            verticalArrangement = Arrangement.SpaceBetween
        ) {
            Row(
                modifier = Modifier.fillMaxWidth(),
                horizontalArrangement = Arrangement.SpaceBetween,
                verticalAlignment = Alignment.Top
            ) {
                Box(
                    modifier = Modifier
                        .size(36.dp)
                        .clip(CircleShape)
                        .background(iconBackgroundColor),
                    contentAlignment = Alignment.Center
                ) {
                    Icon(
                        imageVector = icon,
                        contentDescription = null,
                        tint = iconColor,
                        modifier = Modifier.size(20.dp)
                    )
                }

                if (trend != null) {
                    TrendIndicator(trend = trend)
                } else {
                    Icon(
                        imageVector = Icons.AutoMirrored.Filled.ArrowForward,
                        contentDescription = null,
                        tint = MaterialTheme.colorScheme.onSurfaceVariant.copy(alpha = 0.5f),
                        modifier = Modifier.size(16.dp)
                    )
                }
            }

            Column {
                Text(
                    text = value,
                    style = MaterialTheme.typography.titleLarge,
                    fontWeight = FontWeight.Bold,
                    color = MaterialTheme.colorScheme.onSurface,
                    maxLines = 1,
                    overflow = TextOverflow.Ellipsis,
                    fontSize = 18.sp
                )
                Text(
                    text = title,
                    style = MaterialTheme.typography.labelSmall,
                    color = MaterialTheme.colorScheme.onSurfaceVariant,
                    maxLines = 2,
                    overflow = TextOverflow.Ellipsis
                )
            }
        }
    }
}

@Composable
private fun TrendIndicator(trend: cz.cleansia.partner.domain.models.dashboard.TrendData) {
    val (icon, color) = when (trend.direction) {
        TrendDirection.UP -> Icons.Default.KeyboardArrowUp to CleansiaColors.success
        TrendDirection.DOWN -> Icons.Default.KeyboardArrowDown to MaterialTheme.colorScheme.error
        TrendDirection.NEUTRAL -> Icons.Default.Remove to MaterialTheme.colorScheme.onSurfaceVariant
    }

    Row(
        verticalAlignment = Alignment.CenterVertically,
        modifier = Modifier
            .clip(RoundedCornerShape(12.dp))
            .background(color.copy(alpha = 0.1f))
            .padding(horizontal = 6.dp, vertical = 2.dp)
    ) {
        Icon(
            imageVector = icon,
            contentDescription = null,
            tint = color,
            modifier = Modifier.size(14.dp)
        )
        if (trend.percentage > 0) {
            Text(
                text = "${trend.percentage.toInt()}%",
                style = MaterialTheme.typography.labelSmall,
                color = color,
                fontWeight = FontWeight.Medium
            )
        }
    }
}
