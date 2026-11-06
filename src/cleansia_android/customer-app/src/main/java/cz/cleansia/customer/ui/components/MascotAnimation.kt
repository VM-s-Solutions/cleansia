package cz.cleansia.customer.ui.components

import androidx.compose.ui.geometry.Offset
import androidx.compose.ui.geometry.Rect
import androidx.compose.ui.geometry.Size
import cz.cleansia.core.ui.components.MASCOT_CANVAS_PX

/**
 * Where a mascot's figure stands on its 360 px canvas: the rows its alpha covers. The cleaning box is the still's,
 * the loop's first frame, which every other frame's height is within 2 % of; the welcoming box is the hero's.
 */
internal enum class MascotFigureBox(private val top: Float, private val height: Float) {
    Cleaning(top = 43f, height = 272f),
    Welcoming(top = 51f, height = 249f),
    ;

    fun canvasSide(figureHeight: Float): Float = figureHeight * MASCOT_CANVAS_PX / height

    fun figureHeight(canvasSide: Float): Float = canvasSide * height / MASCOT_CANVAS_PX

    /**
     * The canvas that stands this figure where [standingAs]'s stands on [on]: as tall, its top on the same line,
     * centred on the same vertical.
     */
    fun canvas(standingAs: MascotFigureBox, on: Rect): Rect {
        val side = canvasSide(standingAs.figureHeight(on.height))
        val line = on.top + on.height * standingAs.top / MASCOT_CANVAS_PX
        return Rect(Offset(on.center.x - side / 2, line - side * top / MASCOT_CANVAS_PX), Size(side, side))
    }
}
