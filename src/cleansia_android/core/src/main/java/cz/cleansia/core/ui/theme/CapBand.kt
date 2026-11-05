package cz.cleansia.core.ui.theme

import androidx.compose.foundation.layout.RowScope
import androidx.compose.ui.Modifier
import androidx.compose.ui.composed
import androidx.compose.ui.platform.LocalDensity
import androidx.compose.ui.text.TextStyle
import kotlin.math.roundToInt

/** Nunito's cap height as a share of its size: `OS/2.sCapHeight` 705 over 1000 units per em, in every bundled weight. */
const val NunitoCapHeight = 0.705f

/**
 * Centres a glyph on the cap band of the Nunito [style] line it sits beside in this Row: the baseline less half the
 * cap height, the visual centre of a capitalised word. The text aligns by `FirstBaseline` for a leading glyph, which
 * then sits on its first line, or by `LastBaseline` for a trailing one, which sits on its last.
 */
fun RowScope.capBandAligned(style: TextStyle): Modifier = Modifier.composed {
    val capHeightPx = with(LocalDensity.current) { style.fontSize.toPx() } * NunitoCapHeight
    Modifier.alignBy { capBandLine(it.measuredHeight, capHeightPx) }
}

/** The line of a [glyphHeightPx] glyph that meets the text's baseline, so the glyph's centre is half a cap height above it. */
internal fun capBandLine(glyphHeightPx: Int, capHeightPx: Float): Int = (glyphHeightPx / 2f + capHeightPx / 2f).roundToInt()
