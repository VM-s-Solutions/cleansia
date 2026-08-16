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
import cz.cleansia.core.format.formatOrderDateTime
import cz.cleansia.core.format.formatOrderPrice
import cz.cleansia.core.ui.components.OrderTrackerBar as CoreOrderTrackerBar
import cz.cleansia.core.ui.theme.Spacing
import cz.cleansia.customer.R
import cz.cleansia.customer.core.orders.OrderDetailDto
import cz.cleansia.customer.ui.format.orderStatusColor
import kotlinx.coroutines.delay
import java.time.Instant

/**
 * The sheet header, in four parts: a pinned identity row, then a headline, the five-phase tracker glued
 * to its underside, and a facts strip.
 *
 * **Android and iOS render the same four parts in the same order**, because they were two different
 * screens: iOS pinned the order number and date at the top while Android put the date inside the hero
 * and then repeated it in a metadata row below — the same timestamp twice, forty pixels apart.
 * `OrderDetailContent.swift` is the twin; keep the two in step.
 *
 * **Nothing here draws a container.** The headline used to sit in a surface with its own radius, which
 * fenced it off from the tracker bar it is supposed to read as one block with, and cost a band of
 * whitespace above and below. The sheet is the surface.
 *
 * → /domain/order-lifecycle for what the five phases mean.
 */
private const val TotalSteps = 5

/* ── 1. Pinned identity ── */

/**
 * `#ORD-…  (New)` over `📅 Sun, Aug 16 at 08:00`, pinned above the scroll area.
 *
 * The trailing half is deliberately empty: the mascot rides the sheet's top edge at the right, and this
 * is the row its lower half lands on.
 */
@Composable
internal fun OrderDetailCompactHeader(
    order: OrderDetailDto,
    modifier: Modifier = Modifier,
) {
    val dateLabel = order.cleaningDateTime
        ?.takeIf { it.isNotBlank() }
        ?.let { formatOrderDateTime(it) }

    Column(
        modifier = modifier
            .fillMaxWidth()
            .padding(start = Spacing.ML, end = Spacing.ML, bottom = Spacing.XS),
    ) {
        Row(
            verticalAlignment = Alignment.CenterVertically,
            horizontalArrangement = Arrangement.spacedBy(8.dp),
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
                StatusPill(
                    label = statusLabel,
                    color = orderStatusColor(order.orderStatus?.value),
                )
            }
        }
        if (dateLabel != null) {
            Spacer(Modifier.height(2.dp))
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
    }
}

/* ── 2. Hero ── */

/**
 * What is happening to the booking, in a sentence — plus a subhead and, while the clean is running, an
 * elapsed-over-estimated bar.
 *
 * It carries no date and no price. Both are already on screen: the date in the pinned header above, the
 * price in the facts strip below. Cancelled renders nothing at all — there is no phase to narrate on a
 * dead order, and the tracker below draws its own red bar.
 */
@Composable
internal fun OrderStatusHero(
    order: OrderDetailDto,
    status: OrderStatus?,
    modifier: Modifier = Modifier,
) {
    if (status == OrderStatus.Cancelled) return

    // Only the in-progress bar needs a clock, and only while the clean is running.
    var nowMillis by remember { mutableLongStateOf(System.currentTimeMillis()) }
    LaunchedEffect(status) {
        if (status != OrderStatus.InProgress) return@LaunchedEffect
        while (true) {
            nowMillis = System.currentTimeMillis()
            delay(30_000L)
        }
    }

    val cleanerName = order.assignedEmployees
        ?.firstOrNull()
        ?.fullName
        ?.takeIf { it.isNotBlank() }

    Column(
        modifier = modifier
            .fillMaxWidth()
            .padding(bottom = Spacing.S)
            .animateContentSize(),
    ) {
        AnimatedContent(
            targetState = headlineFor(status, cleanerName),
            transitionSpec = { fadeIn(tween(300)) togetherWith fadeOut(tween(300)) },
            label = "heroHeadlineCrossfade",
        ) { current ->
            Text(
                text = current,
                style = MaterialTheme.typography.titleLarge.copy(fontWeight = FontWeight.Bold),
                color = MaterialTheme.colorScheme.onSurface,
            )
        }
        subheadFor(status, order.estimatedTime)?.let { subhead ->
            Spacer(Modifier.height(2.dp))
            Text(
                text = subhead,
                style = MaterialTheme.typography.bodyMedium,
                color = MaterialTheme.colorScheme.onSurfaceVariant,
            )
        }
        // Elapsed over estimated. A different question from the phase tracker below — "how far through
        // THIS clean are we" rather than "which phase is it in" — so it stays.
        if (status == OrderStatus.InProgress) {
            InProgressBar(order = order, nowMillis = nowMillis)
        }
    }
}

@Composable
private fun headlineFor(status: OrderStatus?, cleanerName: String?): String = when (status) {
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

@Composable
private fun subheadFor(status: OrderStatus?, estimatedMinutes: Int): String? = when (status) {
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

/* ── 3. Tracker ── */

/**
 * The five-phase bar, drawn from `:core` off the customer's own status enum.
 *
 * Rendered for EVERY status, which is the point: before this, an order that was merely booked or already
 * finished showed no phase indicator at all, and the customer had no way to see where in the run it sat.
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

/* ── 4. Facts ── */

/**
 * `Code D23AF7 ......... 1 660 Kč` — the two facts the pinned header has no room for.
 *
 * The confirmation code is how the customer identifies the person at their door, so it stays reachable
 * at every status rather than only while a cleaner is on the way.
 */
@Composable
internal fun OrderFactsStrip(
    order: OrderDetailDto,
    modifier: Modifier = Modifier,
) {
    val currencyCode = order.currency?.code
    val hasDiscount = order.appliedDiscountSource != 0 &&
        order.originalSubtotal > order.totalPrice

    Row(
        modifier = modifier.fillMaxWidth(),
        verticalAlignment = Alignment.CenterVertically,
        horizontalArrangement = Arrangement.SpaceBetween,
    ) {
        order.confirmationCode?.takeIf { it.isNotBlank() }?.let { code ->
            Row(
                verticalAlignment = Alignment.CenterVertically,
                horizontalArrangement = Arrangement.spacedBy(6.dp),
            ) {
                Text(
                    text = stringResource(R.string.order_detail_code_label),
                    style = MaterialTheme.typography.labelSmall,
                    color = MaterialTheme.colorScheme.onSurfaceVariant,
                )
                Text(
                    text = code,
                    style = MaterialTheme.typography.titleMedium.copy(fontWeight = FontWeight.Bold),
                    color = MaterialTheme.colorScheme.onSurface,
                )
            }
        }
        Spacer(Modifier.width(Spacing.XS))
        Row(
            verticalAlignment = Alignment.CenterVertically,
            horizontalArrangement = Arrangement.spacedBy(6.dp),
        ) {
            if (hasDiscount) {
                Text(
                    text = formatOrderPrice(order.originalSubtotal, currencyCode),
                    style = MaterialTheme.typography.labelSmall.copy(
                        textDecoration = TextDecoration.LineThrough,
                    ),
                    color = MaterialTheme.colorScheme.onSurfaceVariant,
                )
            }
            Text(
                text = formatOrderPrice(order.totalPrice, currencyCode),
                style = MaterialTheme.typography.titleMedium.copy(fontWeight = FontWeight.Bold),
                color = MaterialTheme.colorScheme.primary,
            )
        }
    }
}

@Composable
private fun StatusPill(label: String, color: Color) {
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
