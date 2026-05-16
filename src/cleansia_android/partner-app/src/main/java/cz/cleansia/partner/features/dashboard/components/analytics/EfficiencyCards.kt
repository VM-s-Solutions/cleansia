package cz.cleansia.partner.features.dashboard.components.analytics

import androidx.compose.foundation.background
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material3.Card
import androidx.compose.material3.CardDefaults
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import cz.cleansia.partner.R
import cz.cleansia.partner.domain.models.dashboard.CompletionTimeEfficiency
import cz.cleansia.partner.domain.models.dashboard.ScheduleUtilization
import cz.cleansia.partner.ui.components.ProgressGauge
import cz.cleansia.partner.ui.theme.CleansiaColors

@Composable
internal fun ScheduleUtilizationCard(utilization: ScheduleUtilization) {
    val gaugeColor = when {
        utilization.utilizationRate > 0.8f -> CleansiaColors.success
        utilization.utilizationRate > 0.5f -> CleansiaColors.warning
        else -> MaterialTheme.colorScheme.error
    }

    Card(
        modifier = Modifier.fillMaxWidth(),
        colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surface),
        elevation = CardDefaults.cardElevation(defaultElevation = 1.dp)
    ) {
        Column(
            modifier = Modifier.padding(16.dp),
            horizontalAlignment = Alignment.CenterHorizontally
        ) {
            Text(
                text = stringResource(R.string.schedule_utilization),
                style = MaterialTheme.typography.titleMedium,
                fontWeight = FontWeight.SemiBold,
                color = MaterialTheme.colorScheme.onSurface,
                modifier = Modifier.fillMaxWidth()
            )

            Spacer(modifier = Modifier.height(16.dp))

            ProgressGauge(
                progress = utilization.utilizationRate,
                modifier = Modifier.size(140.dp),
                strokeWidth = 14.dp,
                progressColor = gaugeColor
            ) {
                Column(horizontalAlignment = Alignment.CenterHorizontally) {
                    Text(
                        text = "${(utilization.utilizationRate * 100).toInt()}%",
                        style = MaterialTheme.typography.headlineSmall,
                        fontWeight = FontWeight.Bold,
                        color = MaterialTheme.colorScheme.onSurface
                    )
                    Text(
                        text = stringResource(R.string.utilization_rate),
                        style = MaterialTheme.typography.labelSmall,
                        color = MaterialTheme.colorScheme.onSurfaceVariant
                    )
                }
            }

            Spacer(modifier = Modifier.height(16.dp))

            Row(
                modifier = Modifier.fillMaxWidth(),
                horizontalArrangement = Arrangement.SpaceEvenly
            ) {
                Column(horizontalAlignment = Alignment.CenterHorizontally) {
                    Text(
                        text = stringResource(R.string.hours_short, utilization.availableHours),
                        style = MaterialTheme.typography.titleSmall,
                        fontWeight = FontWeight.Bold,
                        color = MaterialTheme.colorScheme.onSurface
                    )
                    Text(
                        text = stringResource(R.string.available_hours_label),
                        style = MaterialTheme.typography.labelSmall,
                        color = MaterialTheme.colorScheme.onSurfaceVariant
                    )
                }
                Column(horizontalAlignment = Alignment.CenterHorizontally) {
                    Text(
                        text = stringResource(R.string.hours_short, utilization.bookedHours),
                        style = MaterialTheme.typography.titleSmall,
                        fontWeight = FontWeight.Bold,
                        color = gaugeColor
                    )
                    Text(
                        text = stringResource(R.string.booked_hours_label),
                        style = MaterialTheme.typography.labelSmall,
                        color = MaterialTheme.colorScheme.onSurfaceVariant
                    )
                }
            }
        }
    }
}

@Composable
internal fun CompletionEfficiencyCard(efficiency: CompletionTimeEfficiency) {
    val fasterColor = CleansiaColors.success
    val slowerColor = MaterialTheme.colorScheme.error
    val estimatedColor = CleansiaColors.info

    Card(
        modifier = Modifier.fillMaxWidth(),
        colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surface),
        elevation = CardDefaults.cardElevation(defaultElevation = 1.dp)
    ) {
        Column(modifier = Modifier.padding(16.dp)) {
            Text(
                text = stringResource(R.string.completion_efficiency),
                style = MaterialTheme.typography.titleMedium,
                fontWeight = FontWeight.SemiBold,
                color = MaterialTheme.colorScheme.onSurface
            )

            Spacer(modifier = Modifier.height(12.dp))

            val maxMinutes = efficiency.services.maxOf {
                maxOf(it.estimatedMinutes, it.actualMinutes)
            }.toFloat()

            efficiency.services.forEach { service ->
                val isFaster = service.actualMinutes <= service.estimatedMinutes
                val barColor = if (isFaster) fasterColor else slowerColor
                val diffPercent = if (service.estimatedMinutes > 0) {
                    kotlin.math.abs(service.estimatedMinutes - service.actualMinutes) * 100 / service.estimatedMinutes
                } else 0

                Column(modifier = Modifier.padding(vertical = 6.dp)) {
                    // Service name + diff label
                    Row(
                        modifier = Modifier.fillMaxWidth(),
                        horizontalArrangement = Arrangement.SpaceBetween,
                        verticalAlignment = Alignment.CenterVertically
                    ) {
                        Text(
                            text = service.serviceName,
                            style = MaterialTheme.typography.bodySmall,
                            fontWeight = FontWeight.Medium,
                            color = MaterialTheme.colorScheme.onSurface,
                            modifier = Modifier.weight(1f)
                        )
                        Text(
                            text = if (isFaster)
                                stringResource(R.string.faster_than_estimate, diffPercent)
                            else
                                stringResource(R.string.slower_than_estimate, diffPercent),
                            style = MaterialTheme.typography.labelSmall,
                            fontWeight = FontWeight.Medium,
                            color = barColor
                        )
                    }

                    Spacer(modifier = Modifier.height(6.dp))

                    // Single bar with estimated marker
                    Box(
                        modifier = Modifier
                            .fillMaxWidth()
                            .height(16.dp)
                            .clip(RoundedCornerShape(8.dp))
                            .background(MaterialTheme.colorScheme.surfaceVariant)
                    ) {
                        // Actual time bar
                        val actualFraction = if (maxMinutes > 0) service.actualMinutes / maxMinutes else 0f
                        Box(
                            modifier = Modifier
                                .fillMaxWidth(actualFraction.coerceIn(0f, 1f))
                                .height(16.dp)
                                .clip(RoundedCornerShape(8.dp))
                                .background(barColor.copy(alpha = 0.7f))
                        )

                        // Estimated time marker line
                        val estimatedFraction = if (maxMinutes > 0) service.estimatedMinutes / maxMinutes else 0f
                        Box(
                            modifier = Modifier
                                .fillMaxWidth(estimatedFraction.coerceIn(0f, 1f))
                                .height(16.dp)
                        ) {
                            Box(
                                modifier = Modifier
                                    .width(2.dp)
                                    .height(16.dp)
                                    .align(Alignment.CenterEnd)
                                    .background(estimatedColor)
                            )
                        }
                    }

                    // Time labels below bar
                    Spacer(modifier = Modifier.height(4.dp))
                    Row(
                        modifier = Modifier.fillMaxWidth(),
                        horizontalArrangement = Arrangement.SpaceBetween
                    ) {
                        Text(
                            text = stringResource(R.string.actual_label) + ": " +
                                stringResource(R.string.minutes_short, service.actualMinutes),
                            style = MaterialTheme.typography.labelSmall,
                            color = barColor
                        )
                        Text(
                            text = stringResource(R.string.estimated_label) + ": " +
                                stringResource(R.string.minutes_short, service.estimatedMinutes),
                            style = MaterialTheme.typography.labelSmall,
                            color = estimatedColor
                        )
                    }
                }
            }
        }
    }
}
