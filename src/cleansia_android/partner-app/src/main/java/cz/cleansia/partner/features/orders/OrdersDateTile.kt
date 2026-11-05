package cz.cleansia.partner.features.orders

import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.layout.RowScope
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.ReadOnlyComposable
import androidx.compose.ui.Modifier
import androidx.compose.ui.composed
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.compositeOver
import androidx.compose.ui.graphics.luminance
import androidx.compose.ui.layout.Layout
import androidx.compose.ui.platform.LocalConfiguration
import androidx.compose.ui.platform.LocalDensity
import androidx.compose.ui.semantics.clearAndSetSemantics
import androidx.compose.ui.text.TextStyle
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.Constraints
import androidx.compose.ui.unit.Dp
import androidx.compose.ui.unit.dp
import cz.cleansia.core.ui.theme.primaryText
import cz.cleansia.partner.ui.theme.pendingInk
import java.time.Instant
import java.time.ZoneId
import java.time.format.DateTimeFormatter
import java.util.Locale
import kotlin.math.roundToInt

/** The leaf's tiers: a day header (month, day) and a row (with the weekday). */
enum class TileSize(val minSide: Dp) { Header(48.dp), Row(64.dp) }

/**
 * The calendar leaf: one per day, a square at every text size. Grey for a closed job (completed or cancelled) whatever
 * its day, otherwise the app's tint, today with the selected stroke. Hidden from TalkBack: the sentence beside it
 * carries the date.
 */
@Composable
fun OrdersDateTile(start: Instant?, size: TileSize, now: Instant, modifier: Modifier = Modifier, closed: Boolean = false) {
    val locale = appLocale()
    val zone = ZoneId.systemDefault()
    val local = start?.atZone(zone)
    val day = local?.toLocalDate()
    val isToday = !closed && day == now.atZone(zone).toLocalDate()
    val scheme = MaterialTheme.colorScheme
    val label = MaterialTheme.typography.labelMedium.copy(fontWeight = FontWeight.SemiBold)
    val shape = MaterialTheme.shapes.small
    Layout(
        content = {
            Text(text = day?.let { OrdersBoardRules.tileMonth(it, locale) }.orEmpty(), style = label, color = if (closed) scheme.onSurfaceVariant else primaryText(), maxLines = 1, softWrap = false)
            Text(
                text = day?.dayOfMonth?.toString().orEmpty(),
                style = MaterialTheme.typography.titleMedium.copy(fontFeatureSettings = "tnum"),
                color = if (closed) scheme.onSurfaceVariant else scheme.onSurface,
                maxLines = 1,
                softWrap = false,
            )
            if (size == TileSize.Row) {
                Text(
                    text = local?.let { DateTimeFormatter.ofPattern("EEE", locale).format(it).trimEnd('.') }.orEmpty(),
                    style = label,
                    color = scheme.onSurfaceVariant,
                    maxLines = 1,
                    softWrap = false,
                )
            }
        },
        modifier = modifier
            .clearAndSetSemantics {}
            .background(if (closed) scheme.surfaceVariant else tonalFill(), shape)
            .then(if (isToday) Modifier.border(1.5.dp, scheme.primary, shape) else Modifier),
    ) { measurables, _ ->
        val tiers = measurables.map { it.measure(Constraints()) }
        val stack = tiers.sumOf { it.height }
        val side = leafSide(size.minSide.roundToPx(), stack, tiers.maxOf { it.width }, LeafInset.roundToPx())
        layout(side, side) {
            var y = (side - stack) / 2
            tiers.forEach {
                it.place((side - it.width) / 2, y)
                y += it.height
            }
        }
    }
}

/** A square's side: its least size, or the stack with [inset] above and below, or its widest line with twice that each side. */
internal fun leafSide(minSide: Int, stackHeight: Int, widestLine: Int, inset: Int): Int =
    maxOf(minSide, stackHeight + inset * 2, widestLine + inset * 4)

/** The least room between a leaf's stack and its edge: above and below it, and twice that beside its widest line. */
private val LeafInset = 3.dp

// ── shared visual roles (spec §4.1); every value is an existing token ─────────

/** The app's locale as resources resolve it (per-app language included). */
@Composable
@ReadOnlyComposable
fun appLocale(): Locale = LocalConfiguration.current.locales.get(0) ?: Locale.getDefault()

@Composable
@ReadOnlyComposable
fun isDarkScheme(): Boolean = MaterialTheme.colorScheme.surface.luminance() < 0.5f

/** The app's tint: the primary at 12 %, in both themes. Text on it is `primaryText()` or the surface's own ink. */
@Composable
@ReadOnlyComposable
fun tonalFill(): Color = MaterialTheme.colorScheme.primary.copy(alpha = 0.12f)

/** The cash notice: the house status-pill wash of `pendingInk`. */
@Composable
@ReadOnlyComposable
fun amberFill(): Color = MaterialTheme.colorScheme.pendingInk.copy(alpha = 0.12f).compositeOver(MaterialTheme.colorScheme.surface)

/** Nunito's x-height as a share of its size: `OS/2.sxHeight` 484 over 1000 units per em, in every bundled weight. */
private const val NunitoXHeight = 0.484f

/**
 * Centres a small mark (a dot, a bullet) on the x-height band of the first line of the Nunito [style] text beside it in
 * this Row, which aligns by `FirstBaseline`: the baseline less half the x-height.
 */
internal fun RowScope.xBandAligned(style: TextStyle): Modifier = Modifier.composed {
    val xHeightPx = with(LocalDensity.current) { style.fontSize.toPx() } * NunitoXHeight
    Modifier.alignBy { (it.measuredHeight / 2f + xHeightPx / 2f).roundToInt() }
}
