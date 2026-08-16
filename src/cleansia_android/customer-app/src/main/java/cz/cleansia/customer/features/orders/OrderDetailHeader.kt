package cz.cleansia.customer.features.orders

import androidx.compose.animation.AnimatedContent
import androidx.compose.animation.animateContentSize
import androidx.compose.animation.core.tween
import androidx.compose.animation.fadeIn
import androidx.compose.animation.fadeOut
import androidx.compose.animation.togetherWith
import androidx.compose.foundation.background
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.PaddingValues
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.outlined.CalendarToday
import androidx.compose.material3.Icon
import androidx.compose.material3.LinearProgressIndicator
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.ProgressIndicatorDefaults
import androidx.compose.material3.Surface
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableLongStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextDecoration
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import cz.cleansia.core.format.formatOrderDateTime
import cz.cleansia.core.format.formatOrderPrice
import cz.cleansia.core.format.formatOrderTime
import cz.cleansia.core.ui.components.OrderTrackerBar as CoreOrderTrackerBar
import cz.cleansia.customer.R
import cz.cleansia.customer.core.orders.OrderDetailDto
import cz.cleansia.customer.ui.format.orderStatusColor
import kotlinx.coroutines.delay
import java.time.Instant

/**
 * The sheet header, built to the same three-part shape the partner sheet uses — hero card, tracker bar
 * glued straight to its underside, identity row beneath — because a customer and a cleaner looking at
 * the same order were reading two different screens: the customer's had no phase tracker at all outside
 * the three live statuses, and put the cleaning date inside the hero where the partner puts the price.
 *
 * **The shape is shared; the words are not.** The partner's copy is instructional to a worker ("Slide
 * below to take this job"); the customer's answers "what is happening to my booking". Only the layout,
 * the type scale and the ordering are held in common.
 *
 * → /domain/order-lifecycle for what the five phases mean.
 */
private const val TotalSteps = 5

/* ── 1. Hero ── */

/**
 * Phase-appropriate big text with a one-line eyebrow above it — the customer twin of the partner's
 * `OrderTimerCard`. Bottom padding is deliberately 0: the tracker bar renders flush underneath, and the
 * two are one visual group.
 *
 * Cancelled renders nothing. There is no phase to narrate on a dead order, and the tracker below draws
 * its own red bar.
 */
@Composable
internal fun OrderStatusHeroCard(
    order: OrderDetailDto,
    status: OrderStatus?,
    modifier: Modifier = Modifier,
) {
    if (status == OrderStatus.Cancelled) return

    val scheduledMillis = remember(order.cleaningDateTime) {
        order.cleaningDateTime
            ?.takeIf { it.isNotBlank() }
            ?.let { runCatching { Instant.parse(it).toEpochMilli() }.getOrNull() }
    }
    val startedAtMillis = remember(order.statusHistory) {
        order.statusHistory.orEmpty()
            .firstOrNull { orderStatusFromValue(it.status?.value) == OrderStatus.InProgress }
            ?.createdOn
            ?.let { runCatching { Instant.parse(it).toEpochMilli() }.getOrNull() }
    }

    // Phase-aware ticker, matching the partner's: 1Hz while the clock is running, 1/min while a
    // countdown is ticking down, idle otherwise so a settled order costs no battery.
    val tickIntervalMs: Long? = when (status) {
        OrderStatus.InProgress -> 1_000L
        OrderStatus.Confirmed -> 60_000L
        else -> null
    }
    var nowMillis by remember { mutableLongStateOf(System.currentTimeMillis()) }
    LaunchedEffect(tickIntervalMs, status) {
        val interval = tickIntervalMs ?: return@LaunchedEffect
        while (true) {
            nowMillis = System.currentTimeMillis()
            delay(interval)
        }
    }

    val cleanerName = order.assignedEmployees
        ?.firstOrNull()
        ?.fullName
        ?.takeIf { it.isNotBlank() }

    Surface(
        modifier = modifier.fillMaxWidth(),
        shape = RoundedCornerShape(16.dp),
        color = MaterialTheme.colorScheme.surface,
        tonalElevation = 0.dp,
    ) {
        Column(
            modifier = Modifier
                // The 4dp inset matches [OrderMetadataRow] so the eyebrow, the big text, the order
                // number and the date all share one left edge. 0dp at the bottom keeps the big text
                // flush against the tracker bar drawn directly below in the parent column.
                .padding(PaddingValues(start = 4.dp, end = 4.dp, top = 12.dp, bottom = 0.dp))
                .animateContentSize(),
            verticalArrangement = Arrangement.Center,
        ) {
            HeroEyebrow(status = status, cleanerName = cleanerName)
            Spacer(Modifier.height(2.dp))
            HeroPrimaryText(
                status = status,
                scheduledMillis = scheduledMillis,
                startedAtMillis = startedAtMillis,
                nowMillis = nowMillis,
            )
            heroSubhead(status, order.estimatedTime)?.let { subhead ->
                Spacer(Modifier.height(2.dp))
                Text(
                    text = subhead,
                    style = MaterialTheme.typography.labelMedium,
                    color = MaterialTheme.colorScheme.onSurfaceVariant,
                )
            }
            // Elapsed-over-estimated. This answers a different question from the phase tracker below —
            // "how far through THIS clean are we" rather than "which phase is it in" — so it stays.
            if (status == OrderStatus.InProgress) {
                InProgressBar(order = order, nowMillis = nowMillis)
            }
            Spacer(Modifier.height(10.dp))
        }
    }
}

@Composable
private fun HeroEyebrow(status: OrderStatus?, cleanerName: String?) {
    val text = when (status) {
        OrderStatus.Confirmed -> if (cleanerName != null) {
            stringResource(R.string.order_detail_headline_confirmed_named, cleanerName)
        } else {
            stringResource(R.string.order_detail_headline_confirmed)
        }
        OrderStatus.OnTheWay -> if (cleanerName != null) {
            stringResource(R.string.order_detail_headline_on_the_way_named, cleanerName)
        } else {
            stringResource(R.string.order_detail_headline_on_the_way)
        }
        OrderStatus.InProgress -> if (cleanerName != null) {
            stringResource(R.string.order_detail_headline_in_progress_named, cleanerName)
        } else {
            stringResource(R.string.order_detail_headline_in_progress)
        }
        OrderStatus.Completed -> stringResource(R.string.order_detail_headline_completed)
        else -> stringResource(R.string.order_detail_headline_default)
    }
    AnimatedContent(
        targetState = text,
        transitionSpec = { fadeIn(tween(300)) togetherWith fadeOut(tween(300)) },
        label = "heroEyebrowCrossfade",
    ) { current ->
        Text(
            text = current,
            style = MaterialTheme.typography.labelSmall.copy(fontWeight = FontWeight.SemiBold),
            color = MaterialTheme.colorScheme.onSurfaceVariant,
        )
    }
}

/**
 * The one number the phase is actually about: when it starts, how long until it does, when the cleaner
 * arrives, how long they have been working, how long it took.
 */
@Composable
private fun HeroPrimaryText(
    status: OrderStatus?,
    scheduledMillis: Long?,
    startedAtMillis: Long?,
    nowMillis: Long,
) {
    val text: String = when (status) {
        OrderStatus.Confirmed -> {
            val remainingMs = (scheduledMillis ?: 0L) - nowMillis
            if (scheduledMillis != null && remainingMs > 0) {
                stringResource(
                    R.string.tracker_countdown_starts_in,
                    formatHoursMinutesDuration((remainingMs / 60_000L).toInt()),
                )
            } else {
                formatOrderDateTime(toIso(scheduledMillis))
            }
        }
        OrderStatus.OnTheWay -> stringResource(
            R.string.tracker_arriving_at,
            formatOrderTime(toIso(scheduledMillis)),
        )
        OrderStatus.InProgress ->
            formatElapsedClock((nowMillis - (startedAtMillis ?: nowMillis)).coerceAtLeast(0L))
        OrderStatus.Completed -> {
            val durationMillis = if (startedAtMillis != null) {
                (nowMillis.coerceAtLeast(startedAtMillis) - startedAtMillis).coerceAtLeast(0L)
            } else {
                0L
            }
            stringResource(
                R.string.tracker_completed_in,
                formatHoursMinutesDuration((durationMillis / 60_000L).toInt()),
            )
        }
        // New / Pending: nothing has happened yet, so the date it is booked for IS the headline.
        else -> formatOrderDateTime(toIso(scheduledMillis))
    }
    Text(
        text = text,
        style = MaterialTheme.typography.headlineMedium.copy(
            fontWeight = FontWeight.SemiBold,
            letterSpacing = (-0.5).sp,
        ),
        color = MaterialTheme.colorScheme.onSurface,
        maxLines = 1,
    )
}

@Composable
private fun heroSubhead(status: OrderStatus?, estimatedMinutes: Int): String? = when (status) {
    OrderStatus.Confirmed -> stringResource(R.string.order_detail_subhead_confirmed)
    OrderStatus.OnTheWay -> stringResource(R.string.order_detail_subhead_on_the_way)
    OrderStatus.InProgress -> if (estimatedMinutes > 0) {
        stringResource(R.string.order_detail_subhead_in_progress_eta, estimatedMinutes)
    } else {
        stringResource(R.string.order_detail_subhead_in_progress)
    }
    else -> null
}

@Composable
private fun InProgressBar(order: OrderDetailDto, nowMillis: Long) {
    val progress = computeInProgressProgress(
        statusHistory = order.statusHistory,
        estimatedMinutes = order.estimatedTime,
        nowEpoch = nowMillis / 1_000L,
    ) ?: return

    Spacer(Modifier.height(10.dp))
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

/* ── 2. Tracker ── */

/**
 * The five-phase bar, drawn from `:core` off the customer's own status enum. Rendered for EVERY status,
 * which is the point: before this, an order that was merely booked or already finished showed no phase
 * indicator at all, and the customer had no way to see where in the run it sat.
 */
@Composable
internal fun OrderTrackerBar(status: OrderStatus?, modifier: Modifier = Modifier) {
    val currentStep = when (status) {
        OrderStatus.Confirmed -> 1
        OrderStatus.OnTheWay -> 2
        OrderStatus.InProgress -> 3
        OrderStatus.Completed -> 4
        else -> 0
    }
    val isCompleted = status == OrderStatus.Completed

    CoreOrderTrackerBar(
        currentStep = if (isCompleted) TotalSteps else currentStep,
        stepCounterLabel = stringResource(
            R.string.tracker_step_counter,
            if (isCompleted) TotalSteps else currentStep + 1,
            TotalSteps,
        ),
        modifier = modifier,
        totalSteps = TotalSteps,
        cancelled = status == OrderStatus.Cancelled,
        cancelledLabel = stringResource(R.string.orders_status_cancelled),
    )
}

/* ── 3. Identity row ── */

/**
 * `#ORD-…  (Confirmed)                    [1 200 Kč]` / `📅 22 May 2026, 9:00` / `Code 4821`
 *
 * The partner's `OrderMetadataRow`, plus the confirmation code — which the partner has no equivalent of,
 * and which is how the customer identifies the person at their door. Transparent, no card: it reads as
 * metadata between the hero above and the section cards below.
 */
@Composable
internal fun OrderMetadataRow(
    order: OrderDetailDto,
    modifier: Modifier = Modifier,
) {
    val currencyCode = order.currency?.code
    val dateLabel = order.cleaningDateTime
        ?.takeIf { it.isNotBlank() }
        ?.let { formatOrderDateTime(it) }
    val hasDiscount = order.appliedDiscountSource != 0 &&
        order.originalSubtotal > order.totalPrice

    Column(
        modifier = modifier
            .fillMaxWidth()
            .padding(horizontal = 4.dp),
    ) {
        Row(
            modifier = Modifier.fillMaxWidth(),
            verticalAlignment = Alignment.CenterVertically,
            horizontalArrangement = Arrangement.SpaceBetween,
        ) {
            Row(
                verticalAlignment = Alignment.CenterVertically,
                horizontalArrangement = Arrangement.spacedBy(8.dp),
                modifier = Modifier.weight(1f, fill = false),
            ) {
                Text(
                    text = order.displayOrderNumber?.let { "#$it" } ?: "—",
                    style = MaterialTheme.typography.titleMedium.copy(fontWeight = FontWeight.Bold),
                    color = MaterialTheme.colorScheme.onSurface,
                )
                val statusLabel = orderStatusLabelRes(order.orderStatus?.value)
                    ?.let { stringResource(it) }
                    ?: order.orderStatus?.name
                if (statusLabel != null) {
                    MetadataStatusPill(
                        label = statusLabel,
                        color = orderStatusColor(order.orderStatus?.value),
                    )
                }
            }
            Column(horizontalAlignment = Alignment.End) {
                PriceChip(formatOrderPrice(order.totalPrice, currencyCode))
                if (hasDiscount) {
                    Text(
                        text = formatOrderPrice(order.originalSubtotal, currencyCode),
                        style = MaterialTheme.typography.labelSmall.copy(
                            textDecoration = TextDecoration.LineThrough,
                        ),
                        color = MaterialTheme.colorScheme.onSurfaceVariant,
                    )
                }
            }
        }
        if (dateLabel != null) {
            Spacer(Modifier.height(4.dp))
            Row(verticalAlignment = Alignment.CenterVertically) {
                Icon(
                    imageVector = Icons.Outlined.CalendarToday,
                    contentDescription = null,
                    tint = MaterialTheme.colorScheme.onSurfaceVariant,
                    modifier = Modifier.size(14.dp),
                )
                Spacer(Modifier.width(6.dp))
                Text(
                    text = dateLabel,
                    style = MaterialTheme.typography.bodyMedium,
                    color = MaterialTheme.colorScheme.onSurfaceVariant,
                )
            }
        }
        order.confirmationCode?.takeIf { it.isNotBlank() }?.let { code ->
            Spacer(Modifier.height(4.dp))
            Text(
                text = stringResource(R.string.order_detail_code_label) + " " + code,
                style = MaterialTheme.typography.labelSmall,
                color = MaterialTheme.colorScheme.onSurfaceVariant,
            )
        }
    }
}

@Composable
private fun MetadataStatusPill(label: String, color: Color) {
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
private fun PriceChip(label: String) {
    Surface(
        shape = RoundedCornerShape(999.dp),
        color = MaterialTheme.colorScheme.primaryContainer,
    ) {
        Text(
            text = label,
            modifier = Modifier.padding(horizontal = 12.dp, vertical = 6.dp),
            style = MaterialTheme.typography.labelLarge.copy(
                fontWeight = FontWeight.ExtraBold,
                letterSpacing = (-0.2).sp,
            ),
            color = MaterialTheme.colorScheme.onPrimaryContainer,
        )
    }
}

/* ── Format helpers ── */

/**
 * Progress as a 0..1 fraction of elapsed-since-`InProgress` over the cleaner's estimate.
 *
 * Returns null when either anchor is missing — never render a guess. Caps at 0.97 so the bar cannot
 * visually complete before the cleaner marks the order done; that flip should be the customer's "it's
 * finished" moment, not a passive timer reaching the end.
 */
private fun computeInProgressProgress(
    statusHistory: List<cz.cleansia.customer.core.orders.OrderStatusTrackDto>?,
    estimatedMinutes: Int,
    nowEpoch: Long,
): Float? {
    if (estimatedMinutes <= 0) return null
    val startedIso = statusHistory
        ?.firstOrNull { orderStatusFromValue(it.status?.value) == OrderStatus.InProgress }
        ?.createdOn
        ?: return null
    val startedEpoch = runCatching { Instant.parse(startedIso).epochSecond }.getOrNull() ?: return null

    val totalSec = estimatedMinutes * 60L
    if (totalSec <= 0L) return null
    val elapsedSec = (nowEpoch - startedEpoch).coerceAtLeast(0L)

    return (elapsedSec.toFloat() / totalSec.toFloat()).coerceIn(0f, 0.97f)
}

private fun toIso(epochMillis: Long?): String? =
    epochMillis?.let { Instant.ofEpochMilli(it).toString() }

/** "1:42:08" past an hour, "02:08" for shorter jobs. */
private fun formatElapsedClock(millis: Long): String {
    val totalSeconds = millis / 1_000L
    val hours = totalSeconds / 3_600L
    val minutes = (totalSeconds % 3_600L) / 60L
    val seconds = totalSeconds % 60L
    return if (hours > 0) {
        "%d:%02d:%02d".format(hours, minutes, seconds)
    } else {
        "%02d:%02d".format(minutes, seconds)
    }
}

@Composable
private fun formatHoursMinutesDuration(totalMinutes: Int): String {
    val hours = totalMinutes / 60
    val minutes = totalMinutes % 60
    return if (hours > 0) {
        stringResource(R.string.duration_hours_minutes, hours, minutes)
    } else {
        stringResource(R.string.duration_minutes_only, minutes)
    }
}
