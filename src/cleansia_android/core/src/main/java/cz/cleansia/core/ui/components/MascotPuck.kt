package cz.cleansia.core.ui.components

import androidx.compose.foundation.Image
import androidx.compose.foundation.layout.size
import androidx.compose.runtime.Composable
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.scale
import androidx.compose.ui.graphics.painter.Painter
import androidx.compose.ui.unit.Dp

/**
 * A still mascot in its puck, drawn at its art's [MascotPuckScale] so every figure stands the same height. Which art
 * belongs to which status is each app's call, and the drawables are each app's own.
 *
 * Decorative: the screen already says the status in words.
 */
@Composable
fun MascotPuck(
    painter: Painter,
    scale: Float,
    modifier: Modifier = Modifier,
    size: Dp = SnapSheetDefaults.OrnamentSize,
) {
    Image(
        painter = painter,
        contentDescription = null,
        modifier = modifier
            .size(size)
            .scale(scale),
    )
}

/**
 * The scale of each still art in a [MascotPuck]: it draws the figure's ink at 13/16 of the puck's height, measured on
 * the art both apps ship under the same name.
 */
object MascotPuckScale {
    const val Leaning = 0.89752f
    const val Ready = 0.85773f
    const val Waving = 0.90632f
    const val Resting = 0.97767f
    const val SprayAndCloth = 1.11915f
    const val ThumbsUp = 0.98229f
    const val Invoice = 1.05718f

    /** The cleaning loop in [MascotAnimation], measured on its first frame. */
    const val CleaningLoop = 1.04839f

    /**
     * Where the waving figure's ledge runs, as a share of the puck's height: 0.797 of the art, carried through [Waving]
     * about the centre. A caller sits this line on an edge instead of centring the figure on it.
     */
    const val WavingLedge = 0.5f + (0.797f - 0.5f) * Waving
}
