package cz.cleansia.partner.features.orders

import androidx.compose.foundation.Canvas
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.material3.MaterialTheme
import androidx.compose.runtime.Composable
import androidx.compose.ui.Modifier
import androidx.compose.ui.geometry.CornerRadius
import androidx.compose.ui.geometry.Offset
import androidx.compose.ui.geometry.Size
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.PathEffect
import androidx.compose.ui.graphics.drawscope.DrawScope
import androidx.compose.ui.graphics.drawscope.Stroke
import androidx.compose.ui.platform.LocalDensity
import androidx.compose.ui.semantics.clearAndSetSemantics
import androidx.compose.ui.text.drawText
import androidx.compose.ui.text.rememberTextMeasurer
import androidx.compose.ui.unit.dp
import cz.cleansia.core.ui.theme.primaryText
import cz.cleansia.partner.ui.theme.Sky200
import cz.cleansia.partner.ui.theme.Sky900
import cz.cleansia.partner.ui.theme.Slate400
import cz.cleansia.partner.ui.theme.Slate500

/**
 * The day line: her booked time solid, open jobs dashed, a clash one red block, now an ink tick and the set-off hour a
 * pale band, over [range]; two marks drawn against each other share the track a half each. Decorative to TalkBack: the
 * words beside it say everything.
 */
@Composable
fun OrdersDayLine(
    range: DayRange,
    mine: List<DaySpan>,
    open: List<OpenSlot>,
    modifier: Modifier = Modifier,
    now: Int? = null,
    setOff: DaySpan? = null,
    nowLabel: String? = null,
    setOffLabel: String? = null,
    slotLabel: String? = null,
) {
    val measurer = rememberTextMeasurer()
    val scheme = MaterialTheme.colorScheme
    val caption = MaterialTheme.typography.bodySmall.copy(fontFeatureSettings = "tnum")
    val hasAnnot = nowLabel != null || setOffLabel != null || slotLabel != null
    val density = LocalDensity.current
    val annotHeight = if (hasAnnot) with(density) { caption.lineHeight.toDp() } + 3.dp else 0.dp
    val hoursHeight = with(density) { caption.lineHeight.toDp() }
    val dark = isDarkScheme()
    val mineColor = scheme.primary
    val linkInk = primaryText()
    val openEdge = if (dark) Slate400 else Slate500
    val setOffColor = if (dark) Sky900 else Sky200
    Canvas(
        modifier = modifier
            .fillMaxWidth()
            .height(annotHeight + TrackHeight + 2.dp + hoursHeight)
            .clearAndSetSemantics {},
    ) {
        val span = (range.to - range.from).coerceAtLeast(1).toFloat()
        val x = { minutes: Int -> ((minutes.coerceIn(range.from, range.to) - range.from) / span) * size.width }
        val trackTop = annotHeight.toPx()
        val trackHeight = TrackHeight.toPx()

        if (hasAnnot) {
            val nowMark = now?.let { at -> nowLabel?.let { Triple(at, it, scheme.onSurface) } }
            val marks = listOfNotNull(
                nowMark,
                setOff?.let { band -> setOffLabel?.let { Triple(band.start, it, linkInk) } },
                open.firstOrNull()?.let { slot -> slotLabel?.let { Triple((slot.span.start + slot.span.end) / 2, it, linkInk) } },
            )
            val layouts = marks.map { (_, text, ink) -> measurer.measure(text, caption.copy(color = ink)) }
            val lefts = OrdersBoardRules.dayLineLabelLefts(
                marks.zip(layouts) { (at, _, _), layout -> x(at) to layout.size.width.toFloat() },
                nowIndex = if (nowMark != null) 0 else null,
                track = size.width,
                gap = LabelGap.toPx(),
            )
            layouts.zip(lefts).forEach { (layout, left) -> left?.let { drawText(layout, topLeft = Offset(it, 0f)) } }
        }

        val capsule = CornerRadius(trackHeight / 2f)
        drawRoundRect(scheme.surfaceVariant, topLeft = Offset(0f, trackTop), size = Size(size.width, trackHeight), cornerRadius = capsule)
        if (now != null && now > range.from) {
            drawRoundRect(scheme.outline, topLeft = Offset(0f, trackTop), size = Size(x(now), trackHeight), cornerRadius = capsule)
        }
        val inset = 1.dp.toPx()
        val block = { bounds: Pair<Float, Float>, top: Float, height: Float, color: Color ->
            drawRoundRect(color, topLeft = Offset(bounds.first, top), size = Size(bounds.second.coerceAtLeast(1f), height), cornerRadius = CornerRadius(height / 2f))
        }
        val mark = { from: Int, to: Int -> OrdersBoardRules.markBounds(x(from) + inset, x(to) - inset, size.width, MIN_MARK.toPx()) }
        val lanes = OrdersBoardRules.dayLineLanes(
            mine.map { DayMark(mine = true, span = it, lane = SlotLane.Full) } + open.map { DayMark(mine = false, span = it.span, lane = it.lane) },
            range,
            size.width,
            MIN_MARK.toPx(),
            inset,
        )
        val band = { lane: SlotLane ->
            when (lane) {
                SlotLane.Full -> trackTop to trackHeight
                SlotLane.Top -> trackTop to trackHeight / 2f - inset / 2f
                SlotLane.Bottom -> trackTop + trackHeight / 2f + inset / 2f to trackHeight / 2f - inset / 2f
            }
        }
        setOff?.let { block(x(it.start) + inset to x(it.end) - x(it.start) - inset * 2, trackTop, trackHeight, setOffColor) }
        open.forEachIndexed { index, slot ->
            val (top, height) = band(lanes[mine.size + index])
            val bounds = mark(slot.span.start, slot.span.end)
            block(bounds, top, height, scheme.surface)
            dashedOutline(bounds.first, top, bounds.second, height, openEdge)
        }
        mine.forEachIndexed { index, it ->
            val (top, height) = band(lanes[index])
            block(mark(it.start, it.end), top, height, mineColor)
        }
        open.forEach { slot -> slot.clash?.let { block(mark(it.start, it.end), trackTop - 2.dp.toPx(), trackHeight + 4.dp.toPx(), scheme.error) } }
        if (now != null && now > range.from && now < range.to) {
            drawRoundRect(
                scheme.onSurface,
                topLeft = Offset(x(now) - 1.dp.toPx(), trackTop - 4.dp.toPx()),
                size = Size(2.dp.toPx(), trackHeight + 8.dp.toPx()),
                cornerRadius = CornerRadius(1.dp.toPx()),
            )
        }

        val hoursTop = trackTop + trackHeight + 2.dp.toPx()
        OrdersBoardRules.dayLineHours(range).forEach { hour ->
            val layout = measurer.measure(hour.toString(), caption.copy(color = scheme.onSurfaceVariant))
            drawText(layout, topLeft = Offset(x(hour * 60) - layout.size.width / 2f, hoursTop))
        }
    }
}

private val TrackHeight = 10.dp

private val LabelGap = 4.dp

/** The least width of her block, an open slot and a clash: a 7-minute job is a 3 dp sliver at its true width. */
private val MIN_MARK = 12.dp

private fun DrawScope.dashedOutline(left: Float, top: Float, width: Float, height: Float, color: Color) {
    val stroke = 1.5.dp.toPx()
    drawRoundRect(
        color = color,
        topLeft = Offset(left + stroke / 2, top + stroke / 2),
        size = Size((width - stroke).coerceAtLeast(1f), (height - stroke).coerceAtLeast(1f)),
        cornerRadius = CornerRadius((height - stroke).coerceAtLeast(1f) / 2f),
        style = Stroke(width = stroke, pathEffect = PathEffect.dashPathEffect(floatArrayOf(4.dp.toPx(), 3.dp.toPx()))),
    )
}
