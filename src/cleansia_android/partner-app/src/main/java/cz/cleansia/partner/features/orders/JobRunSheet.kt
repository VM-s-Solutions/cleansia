package cz.cleansia.partner.features.orders

import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.ColumnScope
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.padding
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.outlined.FormatListBulleted
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableFloatStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.drawBehind
import androidx.compose.ui.geometry.CornerRadius
import androidx.compose.ui.geometry.Offset
import androidx.compose.ui.geometry.Size
import androidx.compose.ui.layout.FirstBaseline
import androidx.compose.ui.layout.onPlaced
import androidx.compose.ui.layout.positionInParent
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.semantics.CollectionInfo
import androidx.compose.ui.semantics.clearAndSetSemantics
import androidx.compose.ui.semantics.collectionInfo
import androidx.compose.ui.semantics.contentDescription
import androidx.compose.ui.semantics.heading
import androidx.compose.ui.semantics.semantics
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import cz.cleansia.core.ui.components.OrderTimelinePoint
import cz.cleansia.core.ui.components.OrderTimelineSegment
import cz.cleansia.core.ui.theme.capBandAligned
import cz.cleansia.core.ui.theme.primaryText
import cz.cleansia.partner.R
import java.time.Instant
import java.time.ZoneId
import java.time.format.DateTimeFormatter
import java.util.Locale

enum class RunMark { Done, Current, ToCome }

/** One step of the run sheet: its mark, the time at its right ("from 13:00", "13:22", "job 14:12") and how a done step is read. */
data class RunRow(val step: RunStep, val mark: RunMark, val at: BoardText?, val spoken: BoardText? = null)

/**
 * The run sheet of her own job: *Setting off → Getting in → Cleaning → Finishing*, the current step from
 * [OrdersBoardRules.runStep], so the head's status line and the sheet always name the same step.
 */
fun runSheet(
    status: Int?,
    start: Instant,
    minutes: Int,
    now: Instant,
    zone: ZoneId,
    locale: Locale,
    crew: Boolean = false,
    onWayAt: Instant? = null,
    startedAt: Instant? = null,
    myMarkAt: Instant? = null,
    firstAfterPhotoAt: Instant? = null,
    finishedAt: Instant? = null,
    weekdayPattern: String = "EEE",
    afterPhoto: Boolean = firstAfterPhotoAt != null,
): List<RunRow> {
    val rules = OrdersBoardRules
    val current = rules.runStep(status, crew, myMarkAt, firstAfterPhotoAt, afterPhoto)
    if (current == RunStep.Cancelled) return emptyList()
    val clock = { at: Instant -> rules.timeText(at, zone, locale) }
    val setOff = start.minusSeconds(OrdersBoardRules.START_GRACE_WINDOW_MINUTES * 60)
    val end = start.plusSeconds(minutes * 60L)
    val today = now.atZone(zone).toLocalDate()
    val steps = listOf(RunStep.SetOff, RunStep.GetIn, RunStep.Clean, RunStep.Finish)
    val currentAt = if (current == RunStep.Done) steps.size else steps.indexOf(current)

    fun stamp(at: Instant?, ofTheJob: Boolean): Pair<BoardText?, BoardText?> = when {
        at == null -> null to null
        ofTheJob -> BoardText.Res(R.string.run_job_stamp, listOf(clock(at))) to BoardText.Res(R.string.run_job_stamp_a11y, listOf(clock(at)))
        else -> BoardText.Literal(clock(at)) to BoardText.Res(R.string.run_step_done_at, listOf(clock(at)))
    }

    return steps.mapIndexed { index, step ->
        val mark = when {
            index < currentAt -> RunMark.Done
            index == currentAt -> RunMark.Current
            else -> RunMark.ToCome
        }
        if (mark == RunMark.Done) {
            val (at, spoken) = when (step) {
                RunStep.SetOff -> stamp(onWayAt, crew)
                RunStep.GetIn -> stamp(startedAt, crew)
                RunStep.Clean -> stamp(if (crew) myMarkAt ?: firstAfterPhotoAt else firstAfterPhotoAt, false)
                else -> stamp(finishedAt, false)
            }
            RunRow(step, mark, at, spoken)
        } else {
            val at = when (step) {
                RunStep.SetOff -> when {
                    start.atZone(zone).toLocalDate() != today ->
                        BoardText.Res(R.string.run_day_from, listOf(DateTimeFormatter.ofPattern(weekdayPattern, locale).format(setOff.atZone(zone)).trimEnd('.'), clock(setOff)))
                    now >= start -> BoardText.Res(R.string.active_hero_due, listOf(clock(start)))
                    else -> BoardText.Res(R.string.run_from, listOf(clock(setOff)))
                }
                RunStep.GetIn -> BoardText.Res(R.string.run_at, listOf(clock(start)))
                RunStep.Clean -> rules.window(start, minutes, zone, locale)?.let { BoardText.Literal(it) }
                else -> BoardText.Res(R.string.run_by, listOf(clock(end)))
            }
            RunRow(step, mark, at)
        }
    }
}

/**
 * The run sheet, the section *How the job goes*: each step's point on the name's first line, joined to the next by a
 * connector; the name, the current one emphasised, with its time at the end in the text blue on the current step;
 * under a step to come what it will need ([subs]), under the current step its body, under a done step the work's
 * record it made ([records]).
 */
@Composable
fun JobRunSheet(
    rows: List<RunRow>,
    subs: Map<RunStep, @Composable ColumnScope.() -> Unit>,
    bodies: Map<RunStep, @Composable ColumnScope.() -> Unit>,
    modifier: Modifier = Modifier,
    records: Map<RunStep, @Composable ColumnScope.() -> Unit> = emptyMap(),
) {
    DetailSection(title = stringResource(R.string.run_a11y), icon = Icons.AutoMirrored.Outlined.FormatListBulleted, modifier = modifier) {
        Column(Modifier.semantics { collectionInfo = CollectionInfo(rowCount = rows.size, columnCount = 1) }) {
            rows.forEachIndexed { index, row ->
                val body = when (row.mark) {
                    RunMark.ToCome -> subs[row.step]
                    RunMark.Current -> bodies[row.step]
                    RunMark.Done -> records[row.step]
                }
                RunStepRow(row = row, last = index == rows.lastIndex, body = body)
            }
        }
    }
}

@Composable
private fun RunStepRow(
    row: RunRow,
    last: Boolean,
    body: (@Composable ColumnScope.() -> Unit)?,
) {
    val current = row.mark == RunMark.Current
    val nameStyle = if (current) MaterialTheme.typography.bodyLarge.copy(fontWeight = FontWeight.SemiBold) else MaterialTheme.typography.bodyLarge
    val connector = MaterialTheme.colorScheme.outlineVariant
    var pointTop by remember { mutableFloatStateOf(Float.NaN) }
    Row(
        Modifier
            .fillMaxWidth()
            .drawBehind {
                if (!last && !pointTop.isNaN()) {
                    val top = pointTop + (PointSize + ConnectorGap).toPx()
                    val bottom = size.height + pointTop - ConnectorGap.toPx()
                    if (bottom > top) {
                        val width = ConnectorWidth.toPx()
                        drawRoundRect(
                            color = connector,
                            topLeft = Offset(LaneWidth.toPx() / 2 - width / 2, top),
                            size = Size(width, bottom - top),
                            cornerRadius = CornerRadius(width / 2),
                        )
                    }
                }
            }
            .padding(bottom = if (last) 0.dp else 20.dp),
    ) {
        OrderTimelinePoint(
            segment = row.mark.segment(),
            modifier = Modifier
                .onPlaced { pointTop = it.positionInParent().y }
                .then(capBandAligned(nameStyle))
                .padding(horizontal = (LaneWidth - PointSize) / 2),
        )
        Column(Modifier.weight(1f).padding(start = 12.dp).alignBy(FirstBaseline)) {
            Row(Modifier.fillMaxWidth()) {
                Text(
                    text = stringResource(stepName(row.step)),
                    style = nameStyle,
                    color = if (row.mark == RunMark.Done) MaterialTheme.colorScheme.onSurfaceVariant else MaterialTheme.colorScheme.onSurface,
                    modifier = Modifier.weight(1f).alignByBaseline().semantics { heading() },
                )
                row.at?.let { at ->
                    val spoken = row.spoken?.text()
                    Text(
                        text = at.text(),
                        style = MaterialTheme.typography.bodyMedium.copy(fontFeatureSettings = "tnum"),
                        color = if (row.mark == RunMark.Current) primaryText() else MaterialTheme.colorScheme.onSurfaceVariant,
                        softWrap = false,
                        modifier = Modifier
                            .padding(start = 10.dp)
                            .alignByBaseline()
                            .then(if (spoken != null) Modifier.clearAndSetSemantics { contentDescription = spoken } else Modifier),
                    )
                }
            }
            body?.let {
                if (row.mark == RunMark.ToCome) {
                    Column(Modifier.padding(top = 2.dp), verticalArrangement = Arrangement.spacedBy(2.dp), content = it)
                } else {
                    Column(Modifier.padding(top = 12.dp), verticalArrangement = Arrangement.spacedBy(20.dp), content = it)
                }
            }
        }
    }
}

private fun stepName(step: RunStep): Int = when (step) {
    RunStep.SetOff -> R.string.run_step_set_off
    RunStep.GetIn -> R.string.run_step_get_in
    RunStep.Clean -> R.string.run_step_clean
    else -> R.string.run_step_finish
}

/** The Core point of a run mark: a done step past, the current one current, a step to come still to come. */
internal fun RunMark.segment(): OrderTimelineSegment = when (this) {
    RunMark.Done -> OrderTimelineSegment.Past
    RunMark.Current -> OrderTimelineSegment.Current
    RunMark.ToCome -> OrderTimelineSegment.Future
}

private val PointSize = 20.dp
private val LaneWidth = 24.dp
private val ConnectorGap = 4.dp
private val ConnectorWidth = 2.dp
