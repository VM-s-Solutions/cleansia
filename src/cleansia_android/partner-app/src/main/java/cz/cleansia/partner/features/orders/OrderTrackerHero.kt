package cz.cleansia.partner.features.orders

import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.setValue
import androidx.compose.runtime.mutableLongStateOf
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.padding
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.unit.dp
import androidx.lifecycle.compose.LifecycleResumeEffect
import cz.cleansia.core.format.cleaningEstimateProgress
import cz.cleansia.core.ui.theme.warningText
import cz.cleansia.partner.R
import kotlinx.coroutines.delay
import androidx.compose.ui.Modifier
import cz.cleansia.partner.api.model.OrderStatus

/**
 * In-sheet tracker. Background-less wrapper around
 * [ContinuousProgressBar] — the bar lives directly on the sheet
 * surface so the timer card above and the mascot overlay on the
 * right read as part of the same visual group (no competing pill
 * container fragmenting the hero area).
 *
 * Kept as its own composable rather than inlining the call so the
 * screen layout can keep a single tracker placement and we can swap
 * container chrome (or none) here without touching the screen.
 */
@Composable
fun OrderTrackerHero(
    status: OrderStatus?,
    modifier: Modifier = Modifier,
    startedAtEpochMillis: Long? = null,
    estimatedMinutes: Int = 0,
) {
    var nowEpoch by remember { mutableLongStateOf(System.currentTimeMillis() / 1_000L) }
    var inForeground by remember { mutableStateOf(false) }
    LifecycleResumeEffect(Unit) {
        inForeground = true
        nowEpoch = System.currentTimeMillis() / 1_000L
        onPauseOrDispose { inForeground = false }
    }
    LaunchedEffect(status, inForeground) {
        if (status != OrderStatus._4 || !inForeground) return@LaunchedEffect
        while (true) {
            nowEpoch = System.currentTimeMillis() / 1_000L
            delay(1_000L)
        }
    }
    val progress = if (status == OrderStatus._4) {
        cleaningEstimateProgress(startedAtEpochMillis?.div(1_000L), estimatedMinutes, nowEpoch)
    } else null
    Column(modifier.fillMaxWidth()) {
        ContinuousProgressBar(status = status, progress = progress)
        if (progress != null) {
            val overdue = progress.overrunMinutes > 0
            Text(
                text = if (overdue) stringResource(R.string.tracker_estimate_overrun, progress.overrunMinutes)
                    else stringResource(R.string.tracker_estimate_remaining, progress.remainingMinutes),
                color = if (overdue) MaterialTheme.colorScheme.warningText else MaterialTheme.colorScheme.onSurfaceVariant,
                style = MaterialTheme.typography.labelMedium,
                modifier = Modifier.padding(top = 6.dp),
            )
        }
    }
}
