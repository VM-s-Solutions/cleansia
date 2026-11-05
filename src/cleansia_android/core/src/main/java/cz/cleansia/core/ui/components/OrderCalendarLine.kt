package cz.cleansia.core.ui.components

import androidx.compose.foundation.layout.size
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.outlined.CalendarToday
import androidx.compose.material3.Icon
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.layout.Layout
import androidx.compose.ui.semantics.clearAndSetSemantics
import androidx.compose.ui.semantics.text
import androidx.compose.ui.text.AnnotatedString
import androidx.compose.ui.unit.Constraints
import androidx.compose.ui.unit.dp

private val IconSize = 14.dp
private val IconGap = 6.dp

/**
 * The day of a booking, and its time or window when it has one, beside a calendar glyph on its first line. It is
 * never shrunk to fit: a window that does not fit beside its day starts the next line whole, without the "·", and a
 * day too long for its line wraps. The one-line form is a single text, so it draws exactly as the two written out
 * together.
 *
 * A plain layout, not a subcomposition, so a parent may ask it for its intrinsic size.
 */
@Composable
fun OrderCalendarLine(
    day: String,
    modifier: Modifier = Modifier,
    window: String? = null,
) {
    val style = MaterialTheme.typography.bodyMedium
    val ink = MaterialTheme.colorScheme.onSurfaceVariant
    val oneLine = calendarLineRows(day, window, split = false)
    val split = calendarLineRows(day, window, split = true)
    Layout(
        content = {
            Icon(
                imageVector = Icons.Outlined.CalendarToday,
                contentDescription = null,
                tint = ink,
                modifier = Modifier.size(IconSize),
            )
            (oneLine + split).forEach { row -> Text(text = row, style = style, color = ink) }
        },
        modifier = modifier.clearAndSetSemantics { text = AnnotatedString(oneLine.single()) },
    ) { measurables, constraints ->
        val icon = measurables[0].measure(Constraints())
        val gap = IconGap.roundToPx()
        val room = if (constraints.hasBoundedWidth) {
            (constraints.maxWidth - icon.width - gap).coerceAtLeast(0)
        } else {
            Constraints.Infinity
        }
        val joined = measurables[1]
        val chosen = if (calendarLineSplits(split.size > 1, joined.maxIntrinsicWidth(Constraints.Infinity), room)) {
            measurables.drop(2)
        } else {
            listOf(joined)
        }
        val lineHeight = joined.minIntrinsicHeight(Constraints.Infinity)
        val rows = chosen.map { it.measure(Constraints(maxWidth = room)) }
        val band = minOf(rows.first().height, lineHeight)
        val top = maxOf(icon.height, band)
        val firstY = Alignment.CenterVertically.align(band, top)
        val width = icon.width + gap + rows.maxOf { it.width }
        val height = firstY + rows.sumOf { it.height }
        layout(
            width.coerceIn(constraints.minWidth, constraints.maxWidth),
            maxOf(top, height).coerceIn(constraints.minHeight, constraints.maxHeight),
        ) {
            icon.place(0, Alignment.CenterVertically.align(icon.height, top))
            var y = firstY
            rows.forEach { row ->
                row.place(icon.width + gap, y)
                y += row.height
            }
        }
    }
}

internal fun calendarLineRows(day: String, window: String?, split: Boolean): List<String> = when {
    window.isNullOrBlank() -> listOf(day)
    split -> listOf(day, window)
    else -> listOf("$day · $window")
}

/** A window starts its own line only when the day and window written out together are wider than their room. */
internal fun calendarLineSplits(hasWindow: Boolean, joinedWidthPx: Int, roomPx: Int): Boolean = hasWindow && joinedWidthPx > roomPx
