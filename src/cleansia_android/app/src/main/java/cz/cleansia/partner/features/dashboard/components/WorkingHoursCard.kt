package cz.cleansia.partner.features.dashboard.components

import androidx.compose.animation.core.animateFloatAsState
import androidx.compose.animation.core.tween
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
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.AccessTime
import androidx.compose.material.icons.filled.Check
import androidx.compose.material.icons.filled.CheckCircle
import androidx.compose.material.icons.filled.EventBusy
import androidx.compose.material.icons.filled.WbSunny
import androidx.compose.material3.Card
import androidx.compose.material3.CardDefaults
import androidx.compose.material3.Icon
import androidx.compose.material3.LinearProgressIndicator
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.unit.dp
import cz.cleansia.partner.R
import cz.cleansia.partner.domain.models.profile.AvailabilityUtils
import cz.cleansia.partner.domain.models.profile.TodayWorkingInfo
import cz.cleansia.partner.ui.theme.CleansiaColors

/**
 * Dashboard card showing today's working hours with progress
 */
@Composable
fun WorkingHoursCard(
    workingInfo: TodayWorkingInfo?,
    modifier: Modifier = Modifier
) {
    if (workingInfo == null) return

    when {
        workingInfo.isWorkingDay -> WorkingDayCard(workingInfo = workingInfo, modifier = modifier)
        workingInfo.isOverrideDay -> ExceptionDayOffCard(workingInfo = workingInfo, modifier = modifier)
        else -> RegularOffDayCard(modifier = modifier)
    }
}

@Composable
private fun WorkingDayCard(
    workingInfo: TodayWorkingInfo,
    modifier: Modifier = Modifier
) {
    val isCompleted = workingInfo.progressFraction >= 1.0f

    val animatedProgress by animateFloatAsState(
        targetValue = workingInfo.progressFraction,
        animationSpec = tween(durationMillis = 800),
        label = "progressAnimation"
    )

    val timeRangeText = AvailabilityUtils.formatTimeSlots(workingInfo.timeSlots)
    val totalText = AvailabilityUtils.formatMinutesToDisplay(workingInfo.totalMinutes)
    val elapsedText = AvailabilityUtils.formatMinutesToDisplay(workingInfo.elapsedMinutes)
    val remainingText = AvailabilityUtils.formatMinutesToDisplay(
        (workingInfo.totalMinutes - workingInfo.elapsedMinutes).coerceAtLeast(0)
    )

    Card(
        modifier = modifier.fillMaxWidth(),
        colors = CardDefaults.cardColors(
            containerColor = if (isCompleted)
                CleansiaColors.successContainer
            else
                MaterialTheme.colorScheme.surface
        ),
        elevation = CardDefaults.cardElevation(defaultElevation = 1.dp)
    ) {
        Column(
            modifier = Modifier.padding(16.dp)
        ) {
            Row(
                verticalAlignment = Alignment.CenterVertically
            ) {
                Box(
                    modifier = Modifier
                        .size(36.dp)
                        .clip(CircleShape)
                        .background(
                            if (isCompleted)
                                CleansiaColors.success.copy(alpha = 0.15f)
                            else
                                MaterialTheme.colorScheme.primaryContainer
                        ),
                    contentAlignment = Alignment.Center
                ) {
                    Icon(
                        imageVector = if (isCompleted)
                            Icons.Default.CheckCircle
                        else
                            Icons.Default.AccessTime,
                        contentDescription = null,
                        tint = if (isCompleted)
                            CleansiaColors.success
                        else
                            MaterialTheme.colorScheme.onPrimaryContainer,
                        modifier = Modifier.size(20.dp)
                    )
                }
                Spacer(modifier = Modifier.width(12.dp))
                Column {
                    Text(
                        text = stringResource(R.string.todays_working_hours),
                        style = MaterialTheme.typography.titleSmall,
                        fontWeight = FontWeight.SemiBold,
                        color = if (isCompleted)
                            CleansiaColors.onSuccessContainer
                        else
                            MaterialTheme.colorScheme.onSurface
                    )
                    Text(
                        text = timeRangeText,
                        style = MaterialTheme.typography.bodySmall,
                        color = if (isCompleted)
                            CleansiaColors.success
                        else
                            MaterialTheme.colorScheme.primary
                    )
                }
            }

            Spacer(modifier = Modifier.height(16.dp))

            if (isCompleted) {
                // Completed state
                Row(
                    modifier = Modifier.fillMaxWidth(),
                    verticalAlignment = Alignment.CenterVertically,
                    horizontalArrangement = Arrangement.Center
                ) {
                    Icon(
                        imageVector = Icons.Default.Check,
                        contentDescription = null,
                        tint = CleansiaColors.success,
                        modifier = Modifier.size(18.dp)
                    )
                    Spacer(modifier = Modifier.width(8.dp))
                    Text(
                        text = stringResource(R.string.day_complete),
                        style = MaterialTheme.typography.labelMedium,
                        fontWeight = FontWeight.SemiBold,
                        color = CleansiaColors.success
                    )
                    Spacer(modifier = Modifier.width(8.dp))
                    Text(
                        text = "($totalText)",
                        style = MaterialTheme.typography.labelSmall,
                        color = CleansiaColors.onSuccessContainer
                    )
                }
            } else {
                // In-progress state
                LinearProgressIndicator(
                    progress = { animatedProgress },
                    modifier = Modifier
                        .fillMaxWidth()
                        .height(8.dp)
                        .clip(RoundedCornerShape(4.dp)),
                    color = MaterialTheme.colorScheme.primary,
                    trackColor = MaterialTheme.colorScheme.surfaceVariant
                )

                Spacer(modifier = Modifier.height(8.dp))

                Row(
                    modifier = Modifier.fillMaxWidth(),
                    horizontalArrangement = Arrangement.SpaceBetween
                ) {
                    Text(
                        text = "$elapsedText / $totalText",
                        style = MaterialTheme.typography.labelSmall,
                        color = MaterialTheme.colorScheme.onSurfaceVariant
                    )
                    Text(
                        text = stringResource(R.string.remaining_time, remainingText),
                        style = MaterialTheme.typography.labelSmall,
                        color = MaterialTheme.colorScheme.primary
                    )
                }
            }

            // Override note if it's a custom hours day
            if (workingInfo.isOverrideDay && !workingInfo.overrideNote.isNullOrBlank()) {
                Spacer(modifier = Modifier.height(8.dp))
                Text(
                    text = workingInfo.overrideNote,
                    style = MaterialTheme.typography.bodySmall,
                    color = CleansiaColors.warning
                )
            }
        }
    }
}

@Composable
private fun ExceptionDayOffCard(
    workingInfo: TodayWorkingInfo,
    modifier: Modifier = Modifier
) {
    Card(
        modifier = modifier.fillMaxWidth(),
        colors = CardDefaults.cardColors(
            containerColor = MaterialTheme.colorScheme.errorContainer.copy(alpha = 0.5f)
        ),
        elevation = CardDefaults.cardElevation(defaultElevation = 1.dp)
    ) {
        Row(
            modifier = Modifier.padding(16.dp),
            verticalAlignment = Alignment.CenterVertically
        ) {
            Box(
                modifier = Modifier
                    .size(36.dp)
                    .clip(CircleShape)
                    .background(MaterialTheme.colorScheme.errorContainer),
                contentAlignment = Alignment.Center
            ) {
                Icon(
                    imageVector = Icons.Default.EventBusy,
                    contentDescription = null,
                    tint = MaterialTheme.colorScheme.onErrorContainer,
                    modifier = Modifier.size(20.dp)
                )
            }
            Spacer(modifier = Modifier.width(12.dp))
            Column {
                Text(
                    text = stringResource(R.string.day_off_today),
                    style = MaterialTheme.typography.titleSmall,
                    fontWeight = FontWeight.SemiBold,
                    color = MaterialTheme.colorScheme.onErrorContainer
                )
                if (!workingInfo.overrideNote.isNullOrBlank()) {
                    Text(
                        text = workingInfo.overrideNote,
                        style = MaterialTheme.typography.bodySmall,
                        color = MaterialTheme.colorScheme.onErrorContainer.copy(alpha = 0.8f)
                    )
                }
            }
        }
    }
}

@Composable
private fun RegularOffDayCard(
    modifier: Modifier = Modifier
) {
    Card(
        modifier = modifier.fillMaxWidth(),
        colors = CardDefaults.cardColors(
            containerColor = MaterialTheme.colorScheme.surfaceVariant.copy(alpha = 0.5f)
        ),
        elevation = CardDefaults.cardElevation(defaultElevation = 0.dp)
    ) {
        Row(
            modifier = Modifier.padding(12.dp),
            verticalAlignment = Alignment.CenterVertically
        ) {
            Icon(
                imageVector = Icons.Default.WbSunny,
                contentDescription = null,
                tint = MaterialTheme.colorScheme.onSurfaceVariant,
                modifier = Modifier.size(20.dp)
            )
            Spacer(modifier = Modifier.width(8.dp))
            Text(
                text = stringResource(R.string.off_today),
                style = MaterialTheme.typography.bodyMedium,
                color = MaterialTheme.colorScheme.onSurfaceVariant
            )
        }
    }
}
