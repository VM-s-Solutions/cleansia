package cz.cleansia.partner.features.dashboard.components.analytics

import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.ExperimentalLayoutApi
import androidx.compose.foundation.layout.FlowRow
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.material3.FilterChip
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.ui.Modifier
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.unit.dp
import cz.cleansia.partner.R
import cz.cleansia.partner.features.dashboard.viewmodels.AnalyticsPeriod

@OptIn(ExperimentalLayoutApi::class)
@Composable
internal fun PeriodSelector(
    selectedPeriod: AnalyticsPeriod,
    onPeriodSelected: (AnalyticsPeriod) -> Unit
) {
    FlowRow(
        modifier = Modifier.fillMaxWidth(),
        horizontalArrangement = Arrangement.spacedBy(8.dp),
        verticalArrangement = Arrangement.spacedBy(4.dp)
    ) {
        FilterChip(
            selected = selectedPeriod == AnalyticsPeriod.THIS_WEEK,
            onClick = { onPeriodSelected(AnalyticsPeriod.THIS_WEEK) },
            label = { Text(stringResource(R.string.this_week)) }
        )
        FilterChip(
            selected = selectedPeriod == AnalyticsPeriod.THIS_MONTH,
            onClick = { onPeriodSelected(AnalyticsPeriod.THIS_MONTH) },
            label = { Text(stringResource(R.string.this_month)) }
        )
        FilterChip(
            selected = selectedPeriod == AnalyticsPeriod.LAST_MONTH,
            onClick = { onPeriodSelected(AnalyticsPeriod.LAST_MONTH) },
            label = { Text(stringResource(R.string.last_month)) }
        )
    }
}
