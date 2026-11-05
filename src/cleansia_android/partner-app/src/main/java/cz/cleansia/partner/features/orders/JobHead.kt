package cz.cleansia.partner.features.orders

import androidx.annotation.StringRes
import androidx.compose.foundation.background
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.ExperimentalLayoutApi
import androidx.compose.foundation.layout.FlowRow
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.layout.LastBaseline
import androidx.compose.ui.layout.Layout
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.semantics.heading
import androidx.compose.ui.semantics.semantics
import androidx.compose.ui.text.AnnotatedString
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.Constraints
import androidx.compose.ui.unit.Dp
import androidx.compose.ui.unit.dp
import cz.cleansia.core.ui.components.OrderCalendarLine
import cz.cleansia.core.ui.components.OrderStatusPill
import cz.cleansia.core.ui.components.OrderTrackerBar
import cz.cleansia.core.ui.theme.orderStatusColor
import cz.cleansia.partner.R
import cz.cleansia.partner.api.model.OrderListItem
import cz.cleansia.partner.api.model.OrderStatus
import java.time.Instant
import java.time.ZoneId
import java.time.format.DateTimeFormatter

/** How far her own job has run: the step the head counts ("Step n / 4"), the tracker's current segment and its fill. */
internal data class HeadProgress(val step: Int, val currentIndex: Int, val fraction: Float?)

internal const val RUN_STEPS = 4

/**
 * Setting off fills from the set-off hour to the start; Cleaning and Finishing fill with the booked window; Getting in
 * has no clock of its own. A done job has all four segments past.
 */
internal fun headProgress(step: RunStep, start: Instant, minutes: Int, now: Instant): HeadProgress? = when (step) {
    RunStep.SetOff -> {
        val setOff = start.minusSeconds(OrdersBoardRules.START_GRACE_WINDOW_MINUTES * 60)
        HeadProgress(1, 0, if (now < setOff) null else OrdersBoardRules.windowRatio(setOff, OrdersBoardRules.START_GRACE_WINDOW_MINUTES.toInt(), now))
    }
    RunStep.GetIn -> HeadProgress(2, 1, null)
    RunStep.Clean -> HeadProgress(3, 2, OrdersBoardRules.windowRatio(start, minutes, now))
    RunStep.Finish -> HeadProgress(4, 3, OrdersBoardRules.windowRatio(start, minutes, now))
    RunStep.Done -> HeadProgress(4, RUN_STEPS, null)
    RunStep.Cancelled -> null
}

/** The status pill's words: a job she has not taken reads New, whatever the order's own status. */
@StringRes
internal fun statusPillRes(status: Int?): Int = when (status) {
    2 -> R.string.status_confirmed
    3 -> R.string.status_on_the_way
    4 -> R.string.status_in_progress
    5 -> R.string.status_completed
    6 -> R.string.status_cancelled
    else -> R.string.status_new
}

/** How far the mascot puck reaches into the sheet: a row that starts above [foot] keeps [width] clear at its end. */
internal data class PuckClearance(val width: Dp, val foot: Dp) {
    companion object {
        val None = PuckClearance(0.dp, 0.dp)
    }
}

/**
 * Rows stacked top-down with no gap of their own: each row that starts above the puck's foot is measured narrower, so
 * its words wrap clear of the figure; a row that starts at or under the foot runs full width.
 */
@Composable
internal fun MascotClearColumn(clearance: PuckClearance, modifier: Modifier = Modifier, content: @Composable () -> Unit) {
    Layout(content = content, modifier = modifier) { measurables, constraints ->
        val width = constraints.maxWidth
        val foot = clearance.foot.roundToPx()
        val narrow = (width - clearance.width.roundToPx()).coerceAtLeast(0)
        var y = 0
        val placed = measurables.map { measurable ->
            val placeable = measurable.measure(Constraints(maxWidth = if (y < foot) narrow else width))
            val top = y
            y += placeable.height
            placeable to top
        }
        layout(width, y) { placed.forEach { (placeable, top) -> placeable.place(0, top) } }
    }
}

/**
 * The head of her job, or of one she might take: #number and the status pill / the calendar line / the status line
 * with "Step n / 4" / the 4-step progress / street and area ‖ the pay pill; [below] adds the day line and the state's
 * finding under it. A cancelled job's progress is one error bar.
 */
@OptIn(ExperimentalLayoutApi::class)
@Composable
internal fun JobHead(
    number: String?,
    status: Int?,
    day: String?,
    window: String?,
    statusItems: List<String>,
    progress: HeadProgress?,
    street: String?,
    sub: List<String>,
    pay: String?,
    caption: String?,
    clearance: PuckClearance,
    modifier: Modifier = Modifier,
    cancelled: Boolean = false,
    below: @Composable () -> Unit = {},
) {
    MascotClearColumn(clearance, modifier.fillMaxWidth()) {
        FlowRow(
            horizontalArrangement = Arrangement.spacedBy(8.dp),
            verticalArrangement = Arrangement.spacedBy(4.dp),
            itemVerticalAlignment = Alignment.CenterVertically,
        ) {
            number?.let {
                Text(
                    text = "#$it",
                    style = MaterialTheme.typography.titleMedium.copy(fontFeatureSettings = "tnum"),
                    color = MaterialTheme.colorScheme.onSurface,
                    modifier = Modifier.semantics { heading() },
                )
            }
            OrderStatusPill(label = stringResource(statusPillRes(status)), ink = orderStatusColor(status))
        }
        if (day != null) {
            Spacer(Modifier.height(2.dp))
            OrderCalendarLine(day = day, window = window)
        }
        if (statusItems.isNotEmpty()) {
            Spacer(Modifier.height(12.dp))
            Row(Modifier.fillMaxWidth(), verticalAlignment = Alignment.Bottom) {
                DotFlow(
                    items = statusItems.map { AnnotatedString(it) },
                    style = MaterialTheme.typography.titleLarge,
                    color = MaterialTheme.colorScheme.onSurface,
                    modifier = Modifier.weight(1f).alignBy(LastBaseline),
                )
                progress?.let {
                    Text(
                        text = stringResource(R.string.tracker_step_counter, it.step, RUN_STEPS),
                        style = MaterialTheme.typography.labelSmall.copy(fontWeight = FontWeight.SemiBold, fontFeatureSettings = "tnum"),
                        color = MaterialTheme.colorScheme.onSurfaceVariant,
                        softWrap = false,
                        modifier = Modifier.padding(start = 8.dp).alignBy(LastBaseline),
                    )
                }
            }
        }
        if (progress != null || cancelled) {
            Spacer(Modifier.height(8.dp))
            OrderTrackerBar(
                currentStep = progress?.currentIndex ?: 0,
                stepCounterLabel = null,
                totalSteps = RUN_STEPS,
                cancelled = cancelled,
                currentFraction = progress?.fraction,
            )
        }
        Spacer(Modifier.height(16.dp))
        Row(Modifier.fillMaxWidth(), verticalAlignment = Alignment.Top) {
            Column(Modifier.weight(1f)) {
                street?.let { Text(text = it, style = MaterialTheme.typography.bodyLarge, color = MaterialTheme.colorScheme.onSurface) }
                if (sub.isNotEmpty()) {
                    DotFlow(
                        items = sub.map { AnnotatedString(it) },
                        style = MaterialTheme.typography.bodyMedium,
                        color = MaterialTheme.colorScheme.onSurfaceVariant,
                        modifier = Modifier.padding(top = 2.dp),
                        tieLast = true,
                    )
                }
            }
            if (pay != null) PayPill(pay = pay, caption = caption, modifier = Modifier.padding(start = 12.dp))
        }
        below()
    }
}

/** Her reward as one string on the app's tint, its caption under it. */
@Composable
internal fun PayPill(pay: String, caption: String?, modifier: Modifier = Modifier) {
    Column(modifier, horizontalAlignment = Alignment.End) {
        Text(
            text = pay,
            style = MaterialTheme.typography.titleMedium.copy(fontFeatureSettings = "tnum"),
            color = MaterialTheme.colorScheme.onSurface,
            softWrap = false,
            modifier = Modifier
                .clip(CircleShape)
                .background(MaterialTheme.colorScheme.primary.copy(alpha = 0.12f))
                .padding(horizontal = 12.dp, vertical = 6.dp),
        )
        caption?.let {
            Text(
                text = it,
                style = MaterialTheme.typography.bodySmall,
                color = MaterialTheme.colorScheme.onSurfaceVariant,
                modifier = Modifier.padding(top = 2.dp),
            )
        }
    }
}

/** The head's calendar day, month first in English as everywhere in the app: "Thu, Nov 12". */
@Composable
internal fun calendarDay(start: Instant?, zone: ZoneId): String? {
    val locale = appLocale()
    return start?.let { DateTimeFormatter.ofPattern(android.text.format.DateFormat.getBestDateTimePattern(locale, "EEEMMMd"), locale).format(it.atZone(zone)) }
}

/** Confirmed and on the way: the day line with now and the set-off band, 16 under the head's facts. */
@Composable
internal fun OwnDayLine(row: OrderListItem, status: OrderStatus?, start: Instant, now: Instant) {
    if (status != OrderStatus._2 && status != OrderStatus._3) return
    val rules = OrdersBoardRules
    val zone = ZoneId.systemDefault()
    val locale = appLocale()
    val day = start.atZone(zone).toLocalDate()
    val today = now.atZone(zone).toLocalDate()
    val span = rules.daySpan(row, day, zone) ?: return
    val setOffAt = start.minusSeconds(OrdersBoardRules.START_GRACE_WINDOW_MINUTES * 60)
    val setOff = DaySpan(span.start - OrdersBoardRules.START_GRACE_WINDOW_MINUTES.toInt(), span.start).takeIf { status == OrderStatus._2 }
    val nowMinutes = now.atZone(zone).let { it.hour * 60 + it.minute }.takeIf { day == today }
    Spacer(Modifier.height(16.dp))
    OrdersDayLine(
        range = rules.dayRange(listOfNotNull(span, setOff)),
        mine = listOf(span),
        open = emptyList(),
        now = nowMinutes,
        setOff = setOff,
        nowLabel = nowMinutes?.let { rules.timeText(now, zone, locale) },
        setOffLabel = setOff?.takeIf { day != today || now < setOffAt }?.let { rules.timeText(setOffAt, zone, locale) },
    )
}
