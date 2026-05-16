package cz.cleansia.partner.features.dashboard.components.analytics

import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.material3.Card
import androidx.compose.material3.CardDefaults
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import cz.cleansia.partner.R
import cz.cleansia.partner.core.utils.CurrencyUtils
import cz.cleansia.partner.core.utils.DateTimeUtils
import cz.cleansia.partner.features.dashboard.viewmodels.AnalyticsUiState

@Composable
internal fun StatsRow(
    uiState: AnalyticsUiState
) {
    val analytics = uiState.analytics ?: return

    Column(verticalArrangement = Arrangement.spacedBy(12.dp)) {
        Row(
            modifier = Modifier.fillMaxWidth(),
            horizontalArrangement = Arrangement.spacedBy(12.dp)
        ) {
            StatMiniCard(
                label = stringResource(R.string.total),
                value = CurrencyUtils.formatCurrencyCompact(analytics.totalEarnings, analytics.currency),
                modifier = Modifier.weight(1f)
            )
            StatMiniCard(
                label = stringResource(R.string.daily_average),
                value = CurrencyUtils.formatCurrencyCompact(uiState.averageDaily, analytics.currency),
                modifier = Modifier.weight(1f)
            )
        }
        Row(
            modifier = Modifier.fillMaxWidth(),
            horizontalArrangement = Arrangement.spacedBy(12.dp)
        ) {
            StatMiniCard(
                label = stringResource(R.string.best_day),
                value = uiState.bestDay?.let {
                    CurrencyUtils.formatCurrencyCompact(it.amount, analytics.currency)
                } ?: "-",
                subtitle = uiState.bestDay?.let { DateTimeUtils.formatDate(it.date) },
                modifier = Modifier.weight(1f)
            )
            StatMiniCard(
                label = stringResource(R.string.worst_day),
                value = uiState.worstDay?.let {
                    CurrencyUtils.formatCurrencyCompact(it.amount, analytics.currency)
                } ?: "-",
                subtitle = uiState.worstDay?.let { DateTimeUtils.formatDate(it.date) },
                modifier = Modifier.weight(1f)
            )
        }
    }
}

@Composable
private fun StatMiniCard(
    label: String,
    value: String,
    modifier: Modifier = Modifier,
    subtitle: String? = null
) {
    Card(
        modifier = modifier,
        colors = CardDefaults.cardColors(
            containerColor = MaterialTheme.colorScheme.surface
        ),
        elevation = CardDefaults.cardElevation(defaultElevation = 1.dp)
    ) {
        Column(
            modifier = Modifier
                .fillMaxWidth()
                .padding(10.dp),
            horizontalAlignment = Alignment.CenterHorizontally
        ) {
            Text(
                text = value,
                style = MaterialTheme.typography.titleSmall,
                fontWeight = FontWeight.Bold,
                color = MaterialTheme.colorScheme.onSurface,
                textAlign = TextAlign.Center,
                maxLines = 1,
                overflow = TextOverflow.Ellipsis,
                fontSize = 13.sp
            )
            if (subtitle != null) {
                Text(
                    text = subtitle,
                    style = MaterialTheme.typography.labelSmall,
                    color = MaterialTheme.colorScheme.primary,
                    textAlign = TextAlign.Center,
                    fontSize = 10.sp,
                    maxLines = 1
                )
            }
            Spacer(modifier = Modifier.height(4.dp))
            Text(
                text = label,
                style = MaterialTheme.typography.labelSmall,
                color = MaterialTheme.colorScheme.onSurfaceVariant,
                textAlign = TextAlign.Center,
                maxLines = 1,
                overflow = TextOverflow.Ellipsis
            )
        }
    }
}
