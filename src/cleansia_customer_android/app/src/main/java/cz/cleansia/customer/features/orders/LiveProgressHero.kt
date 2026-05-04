package cz.cleansia.customer.features.orders

import androidx.compose.foundation.background
import androidx.compose.foundation.border
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
import androidx.compose.material3.LinearProgressIndicator
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.ProgressIndicatorDefaults
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.Brush
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import cz.cleansia.customer.R
import cz.cleansia.customer.core.format.orderStatusColor
import cz.cleansia.customer.core.orders.OrderDetailDto
import cz.cleansia.customer.ui.components.MascotAnimation
import kotlinx.coroutines.delay
import kotlinx.datetime.Clock
import kotlinx.datetime.Instant

/**
 * Live progress hero for the order-detail screen. Replaces the static
 * [HeroCard] when the order is in an "active" status (`Confirmed` or
 * `InProgress`); for terminal states (`Completed`, `Cancelled`) and the
 * pre-acceptance phase (`New`, `Pending`) we keep the original [HeroCard].
 *
 * The hero combines four signals on one card:
 *  1. Status-aware mascot floating in the top-right corner — overlay style,
 *     not a separate block.
 *  2. Status pill + a contextual headline that mutates per state
 *     ("Marek accepted", "Cleaning in progress", etc).
 *  3. Live progress bar driven by `(now - startedAt) / estimatedDurationMin`
 *     when `InProgress`. Only rendered if we have both anchors.
 *  4. Step indicator at the bottom (Booked → Accepted → Started → Finished)
 *     with the current step highlighted.
 *
 * The "now" tick re-evaluates every 30 seconds via [LaunchedEffect] so the
 * progress bar advances without leaning on an external clock or push event.
 */
@Composable
fun LiveProgressHero(order: OrderDetailDto) {
    val status = orderStatusFromValue(order.orderStatus?.value)

    // 30-second tick for the progress bar. We don't need second-level precision —
    // a cleaning is ~1-3 hours, so the bar moves <1% per minute and 30s polling
    // is plenty smooth. The tick also drives any "ETA" string the row renders.
    var nowEpoch by remember { mutableStateOf(Clock.System.now().epochSeconds) }
    LaunchedEffect(status) {
        // Only tick while the order is active. Terminal states never need
        // to move the bar; New/Pending have no progress to track.
        if (status == OrderStatus.InProgress) {
            while (true) {
                nowEpoch = Clock.System.now().epochSeconds
                delay(30_000L)
            }
        }
    }

    val cleanerName = order.assignedEmployees
        ?.firstOrNull()
        ?.fullName
        ?.takeIf { it.isNotBlank() }

    Box(
        modifier = Modifier
            .fillMaxWidth()
            .clip(RoundedCornerShape(16.dp))
            .background(
                Brush.verticalGradient(
                    listOf(
                        MaterialTheme.colorScheme.primary.copy(alpha = 0.10f),
                        MaterialTheme.colorScheme.surface,
                    ),
                ),
            )
            .border(
                1.dp,
                MaterialTheme.colorScheme.outlineVariant,
                RoundedCornerShape(16.dp),
            )
            .padding(16.dp),
    ) {
        // Mascot overlay — top-right, smaller than the standalone version,
        // positioned so it never collides with the headline column.
        if (status == OrderStatus.InProgress) {
            MascotAnimation(
                resId = R.raw.mascot_cleaning_in_progress,
                size = 140.dp,
                modifier = Modifier.align(Alignment.TopEnd),
            )
        } else if (status == OrderStatus.Confirmed) {
            // Confirmed = cleaner accepted but hasn't started. Use the welcoming
            // mascot here as a friendly "we're on it" cue. Plays once and
            // freezes on the final frame; the user is not on this screen
            // long enough for a loop to matter.
            MascotAnimation(
                resId = R.raw.mascot_welcoming,
                size = 140.dp,
                modifier = Modifier.align(Alignment.TopEnd),
                loop = false,
            )
        }

        Column(
            modifier = Modifier
                // Reserve room on the right for the mascot overlay. Without this
                // the headline can collide with the mascot at 360dp widths.
                .padding(end = 140.dp + 8.dp),
        ) {
            HeroStatusPill(
                label = order.orderStatus?.name ?: "—",
                color = orderStatusColor(order.orderStatus?.value),
            )

            Spacer(Modifier.height(10.dp))

            Text(
                text = headlineFor(status, cleanerName),
                style = MaterialTheme.typography.titleMedium.copy(
                    fontWeight = FontWeight.SemiBold,
                ),
                color = MaterialTheme.colorScheme.onSurface,
            )

            subheadFor(status, order.estimatedTime)?.let { subhead ->
                Spacer(Modifier.height(4.dp))
                Text(
                    text = subhead,
                    style = MaterialTheme.typography.bodySmall,
                    color = MaterialTheme.colorScheme.onSurfaceVariant,
                )
            }
        }

        // Progress bar + step indicator live below the headline column —
        // pulled out of the right-padded Column so they span full width.
        Column(modifier = Modifier.padding(top = if (status == OrderStatus.Confirmed) 130.dp else 140.dp)) {
            if (status == OrderStatus.InProgress) {
                val progress = computeInProgressProgress(
                    statusHistory = order.statusHistory,
                    estimatedMinutes = order.estimatedTime,
                    nowEpoch = nowEpoch,
                )
                if (progress != null) {
                    Spacer(Modifier.height(12.dp))
                    LinearProgressIndicator(
                        progress = { progress },
                        modifier = Modifier
                            .fillMaxWidth()
                            .height(6.dp)
                            .clip(RoundedCornerShape(3.dp)),
                        color = MaterialTheme.colorScheme.primary,
                        trackColor = MaterialTheme.colorScheme.outlineVariant.copy(alpha = 0.6f),
                        strokeCap = ProgressIndicatorDefaults.LinearStrokeCap,
                    )
                    Spacer(Modifier.height(4.dp))
                    Text(
                        text = stringResource(
                            R.string.order_detail_progress_percent,
                            (progress * 100).toInt().coerceIn(0, 100),
                        ),
                        style = MaterialTheme.typography.labelSmall,
                        color = MaterialTheme.colorScheme.onSurfaceVariant,
                    )
                }
            }

            Spacer(Modifier.height(14.dp))
            StepIndicator(currentStatus = status)
        }
    }
}

/**
 * Tightened pill — no surrounding container so it embeds cleanly in the hero
 * column. Visually identical to the screen's local `StatusPill` but lives
 * here so this composable is self-contained.
 */
@Composable
private fun HeroStatusPill(label: String, color: Color) {
    Row(
        modifier = Modifier
            .clip(RoundedCornerShape(999.dp))
            .background(color.copy(alpha = 0.16f))
            .padding(horizontal = 10.dp, vertical = 4.dp),
        verticalAlignment = Alignment.CenterVertically,
    ) {
        Text(
            text = label,
            style = MaterialTheme.typography.labelSmall.copy(fontWeight = FontWeight.SemiBold),
            color = color,
        )
    }
}

@Composable
private fun headlineFor(status: OrderStatus?, cleanerName: String?): String = when (status) {
    OrderStatus.Confirmed -> if (cleanerName != null) {
        stringResource(R.string.order_detail_headline_confirmed_named, cleanerName)
    } else {
        stringResource(R.string.order_detail_headline_confirmed)
    }
    OrderStatus.InProgress -> if (cleanerName != null) {
        stringResource(R.string.order_detail_headline_in_progress_named, cleanerName)
    } else {
        stringResource(R.string.order_detail_headline_in_progress)
    }
    else -> stringResource(R.string.order_detail_headline_default)
}

@Composable
private fun subheadFor(status: OrderStatus?, estimatedMinutes: Int): String? = when (status) {
    OrderStatus.Confirmed -> stringResource(R.string.order_detail_subhead_confirmed)
    OrderStatus.InProgress -> if (estimatedMinutes > 0) {
        stringResource(R.string.order_detail_subhead_in_progress_eta, estimatedMinutes)
    } else {
        stringResource(R.string.order_detail_subhead_in_progress)
    }
    else -> null
}

/**
 * Compute progress as a 0..1 fraction based on the time elapsed since the
 * `InProgress` status was recorded vs. the cleaner's estimated duration.
 *
 * Returns null when we don't have both anchors — never render a guess. Caps
 * at 0.97 so the bar never visually completes before the cleaner actually
 * marks the order Completed; that flip should be the user's "yay, done"
 * moment, not a passive timer reaching 100%.
 */
private fun computeInProgressProgress(
    statusHistory: List<cz.cleansia.customer.core.orders.OrderStatusTrackDto>?,
    estimatedMinutes: Int,
    nowEpoch: Long,
): Float? {
    if (estimatedMinutes <= 0) return null
    // Find the InProgress entry's createdOn. statusHistory is ordered by the
    // backend, but we're defensive and scan rather than assume.
    val inProgressEntry = statusHistory
        ?.firstOrNull { it.status?.value == OrderStatus.InProgress.ordinal }
        ?: return null
    val startedIso = inProgressEntry.createdOn ?: return null
    val startedEpoch = runCatching { Instant.parse(startedIso).epochSeconds }
        .getOrNull() ?: return null

    val elapsedSec = (nowEpoch - startedEpoch).coerceAtLeast(0L)
    val totalSec = estimatedMinutes * 60L
    if (totalSec <= 0L) return null

    return (elapsedSec.toFloat() / totalSec.toFloat()).coerceIn(0f, 0.97f)
}

/**
 * Step indicator — four dots connected by short lines, the active step
 * highlighted. Maps directly to the order status:
 *  - New / Pending → step 0 (Booked)
 *  - Confirmed → step 1 (Accepted)
 *  - InProgress → step 2 (Started)
 *  - Completed → step 3 (Finished)
 *  - Cancelled → no indicator (the parent renders the original hero in this case)
 */
@Composable
private fun StepIndicator(currentStatus: OrderStatus?) {
    val steps = listOf(
        stringResource(R.string.order_detail_step_booked),
        stringResource(R.string.order_detail_step_accepted),
        stringResource(R.string.order_detail_step_started),
        stringResource(R.string.order_detail_step_finished),
    )
    val activeIdx = when (currentStatus) {
        OrderStatus.New, OrderStatus.Pending -> 0
        OrderStatus.Confirmed -> 1
        OrderStatus.InProgress -> 2
        OrderStatus.Completed -> 3
        else -> -1
    }

    Column {
        Row(
            modifier = Modifier.fillMaxWidth(),
            verticalAlignment = Alignment.CenterVertically,
        ) {
            steps.forEachIndexed { idx, _ ->
                val active = idx <= activeIdx
                Box(
                    modifier = Modifier
                        .size(10.dp)
                        .clip(CircleShape)
                        .background(
                            if (active) MaterialTheme.colorScheme.primary
                            else MaterialTheme.colorScheme.outlineVariant,
                        ),
                )
                if (idx < steps.lastIndex) {
                    Box(
                        modifier = Modifier
                            .weight(1f)
                            .height(2.dp)
                            .background(
                                if (idx < activeIdx) MaterialTheme.colorScheme.primary
                                else MaterialTheme.colorScheme.outlineVariant,
                            ),
                    )
                }
            }
        }
        Spacer(Modifier.height(6.dp))
        Row(
            modifier = Modifier.fillMaxWidth(),
            horizontalArrangement = Arrangement.SpaceBetween,
        ) {
            steps.forEachIndexed { idx, label ->
                Text(
                    text = label,
                    style = MaterialTheme.typography.labelSmall,
                    color = if (idx == activeIdx) {
                        MaterialTheme.colorScheme.primary
                    } else {
                        MaterialTheme.colorScheme.onSurfaceVariant
                    },
                    fontWeight = if (idx == activeIdx) FontWeight.SemiBold else FontWeight.Normal,
                    modifier = Modifier.width(72.dp),
                )
            }
        }
    }
}
