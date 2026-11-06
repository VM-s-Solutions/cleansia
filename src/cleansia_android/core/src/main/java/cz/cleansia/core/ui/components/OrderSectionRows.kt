package cz.cleansia.core.ui.components

import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.width
import androidx.compose.material3.HorizontalDivider
import androidx.compose.material3.Icon
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.vector.ImageVector
import androidx.compose.ui.layout.Layout
import androidx.compose.ui.semantics.clearAndSetSemantics
import androidx.compose.ui.semantics.text
import androidx.compose.ui.text.AnnotatedString
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.unit.Constraints
import androidx.compose.ui.unit.dp

/**
 * An order section's title, with an optional glyph, over the rule that separates it from its content. A [subtitle],
 * such as a count, follows the title after a separator in the secondary ink; one that does not fit beside the title
 * starts the next line whole, without the separator.
 *
 * It emits its rows into the caller's column rather than one root of its own, so the caller's column spaces
 * the title, the rule and the content.
 */
@Composable
fun OrderSectionHeader(
    title: String,
    icon: ImageVector? = null,
    subtitle: String? = null,
) {
    if (subtitle != null) {
        SectionTitleAndSubtitle(title, icon, subtitle)
    } else {
        SectionTitle(title, icon)
    }
    Spacer(Modifier.height(8.dp))
    HorizontalDivider(color = MaterialTheme.colorScheme.outlineVariant)
    Spacer(Modifier.height(12.dp))
}

/** A label and its value on one row, the label at the start and the value at the end. */
@Composable
fun OrderInfoRow(label: String, value: String) {
    Row(
        modifier = Modifier.fillMaxWidth(),
        horizontalArrangement = Arrangement.SpaceBetween,
    ) {
        Text(
            text = label,
            style = MaterialTheme.typography.bodyMedium,
            color = MaterialTheme.colorScheme.onSurfaceVariant,
            modifier = Modifier.weight(1f),
        )
        Spacer(Modifier.width(12.dp))
        Text(
            text = value,
            style = MaterialTheme.typography.bodyMedium,
            color = MaterialTheme.colorScheme.onSurface,
            modifier = Modifier.weight(1f),
            textAlign = TextAlign.End,
        )
    }
}

/** The title alone, its glyph centred on the title's first line however many lines the title wraps to. */
@Composable
private fun SectionTitle(title: String, icon: ImageVector?) {
    Layout(
        content = {
            Text(
                text = title,
                style = MaterialTheme.typography.titleSmall.copy(fontWeight = FontWeight.SemiBold),
                color = MaterialTheme.colorScheme.onBackground,
            )
            if (icon != null) {
                Icon(
                    imageVector = icon,
                    contentDescription = null,
                    tint = MaterialTheme.colorScheme.primary,
                    modifier = Modifier.size(18.dp),
                )
            }
        },
    ) { measurables, constraints ->
        val glyph = measurables.getOrNull(1)?.measure(Constraints())
        val lead = glyph?.let { it.width + 8.dp.roundToPx() } ?: 0
        val room = if (constraints.hasBoundedWidth) (constraints.maxWidth - lead).coerceAtLeast(0) else Constraints.Infinity
        val titleLines = measurables[0].measure(Constraints(maxWidth = room))
        val firstLine = minOf(titleLines.height, measurables[0].minIntrinsicHeight(Constraints.Infinity))
        val band = maxOf(glyph?.height ?: 0, firstLine)
        val titleY = Alignment.CenterVertically.align(firstLine, band)
        val height = maxOf(band, titleY + titleLines.height)
        layout((lead + titleLines.width).coerceIn(constraints.minWidth, constraints.maxWidth), height.coerceIn(constraints.minHeight, constraints.maxHeight)) {
            glyph?.let { it.place(0, Alignment.CenterVertically.align(it.height, band)) }
            titleLines.place(lead, titleY)
        }
    }
}

/** The title and its subtitle, laid out as a row while they fit one line; a plain layout, so a parent may ask its size. */
@Composable
private fun SectionTitleAndSubtitle(title: String, icon: ImageVector?, subtitle: String) {
    val subtitleStyle = MaterialTheme.typography.bodyMedium
    val subtitleInk = MaterialTheme.colorScheme.onSurfaceVariant
    Layout(
        content = {
            Text(
                text = title,
                style = MaterialTheme.typography.titleSmall.copy(fontWeight = FontWeight.SemiBold),
                color = MaterialTheme.colorScheme.onBackground,
            )
            Text(text = sectionSubtitleText(subtitle, stacked = false), style = subtitleStyle, color = subtitleInk)
            Text(text = sectionSubtitleText(subtitle, stacked = true), style = subtitleStyle, color = subtitleInk)
            if (icon != null) {
                Icon(
                    imageVector = icon,
                    contentDescription = null,
                    tint = MaterialTheme.colorScheme.primary,
                    modifier = Modifier.size(18.dp),
                )
            }
        },
        modifier = Modifier.clearAndSetSemantics { text = AnnotatedString("$title ${sectionSubtitleText(subtitle, stacked = false)}") },
    ) { measurables, constraints ->
        val glyph = measurables.getOrNull(3)?.measure(Constraints())
        val lead = glyph?.let { it.width + 8.dp.roundToPx() } ?: 0
        val gap = 6.dp.roundToPx()
        val room = if (constraints.hasBoundedWidth) (constraints.maxWidth - lead).coerceAtLeast(0) else Constraints.Infinity
        val stacked = sectionSubtitleStacks(
            titleWidthPx = measurables[0].maxIntrinsicWidth(Constraints.Infinity),
            gapPx = gap,
            subtitleWidthPx = measurables[1].maxIntrinsicWidth(Constraints.Infinity),
            roomPx = room,
        )
        val titleLine = measurables[0].measure(Constraints(maxWidth = room))
        val glyphHeight = glyph?.height ?: 0
        if (!stacked) {
            val after = measurables[1].measure(Constraints(maxWidth = sectionSubtitleRoomPx(room, titleLine.width, gap)))
            val height = maxOf(glyphHeight, titleLine.height, after.height)
            val width = lead + titleLine.width + gap + after.width
            layout(width.coerceIn(constraints.minWidth, constraints.maxWidth), height.coerceIn(constraints.minHeight, constraints.maxHeight)) {
                glyph?.let { it.place(0, Alignment.CenterVertically.align(it.height, height)) }
                titleLine.place(lead, Alignment.CenterVertically.align(titleLine.height, height))
                after.place(lead + titleLine.width + gap, Alignment.CenterVertically.align(after.height, height))
            }
        } else {
            val below = measurables[2].measure(Constraints(maxWidth = room))
            val firstLine = minOf(titleLine.height, measurables[0].minIntrinsicHeight(Constraints.Infinity))
            val band = maxOf(glyphHeight, firstLine)
            val titleY = Alignment.CenterVertically.align(firstLine, band)
            val height = maxOf(band, titleY + titleLine.height + below.height)
            val width = lead + maxOf(titleLine.width, below.width)
            layout(width.coerceIn(constraints.minWidth, constraints.maxWidth), height.coerceIn(constraints.minHeight, constraints.maxHeight)) {
                glyph?.let { it.place(0, Alignment.CenterVertically.align(it.height, band)) }
                titleLine.place(lead, titleY)
                below.place(lead, titleY + titleLine.height)
            }
        }
    }
}

internal fun sectionSubtitleText(subtitle: String, stacked: Boolean): String = if (stacked) subtitle else "· $subtitle"

/** The subtitle's room beside the title. Unbounded room stays unbounded: Compose cannot hold a width just under it. */
internal fun sectionSubtitleRoomPx(roomPx: Int, titleWidthPx: Int, gapPx: Int): Int =
    if (roomPx == Constraints.Infinity) Constraints.Infinity else (roomPx - titleWidthPx - gapPx).coerceAtLeast(0)

/** The subtitle leaves the title's line only when the title, the gap and the subtitle are wider than their room. */
internal fun sectionSubtitleStacks(titleWidthPx: Int, gapPx: Int, subtitleWidthPx: Int, roomPx: Int): Boolean =
    titleWidthPx + gapPx + subtitleWidthPx > roomPx
