package cz.cleansia.core.ui.theme

import androidx.compose.material3.ColorScheme
import androidx.compose.material3.MaterialTheme
import androidx.compose.runtime.Composable
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.luminance

private val Amber500 = Color(0xFFF59E0B)
private val Amber800 = Color(0xFF92400E)
private val Sky300 = Color(0xFF7DD3FC)
private val Sky800 = Color(0xFF075985)
private val Green400 = Color(0xFF4ADE80)
private val Green800 = Color(0xFF166534)
private val Slate300 = Color(0xFFCBD5E1)
private val Slate600 = Color(0xFF475569)

/**
 * Color keyed off backend `OrderStatus.value` — a status pill's label and the wash behind it, and the
 * timeline's dot:
 *   New=0 / Pending=1   → amber
 *   Confirmed=2         → the text blue
 *   OnTheWay=3 / InProgress=4 → a deeper blue (light) or a lighter one (dark) than Confirmed
 *   Completed=5         → green
 *   Cancelled=6         → slate
 */
@Composable
fun orderStatusColor(statusValue: Int?): Color = orderStatusInk(statusValue, MaterialTheme.colorScheme)

/**
 * The pill's ink for [scheme]. Each reads 4.5:1 or more on its own 14–16 % wash over the card in both
 * schemes, as the dispute pills do: the amber and sky-400 the pills used to take were 1.9:1 in light mode,
 * sky-600 was 3.4:1 and 3.1:1, and the green and slate were under 3:1 in dark mode, so light mode takes a
 * darker step and dark mode a lighter one.
 */
fun orderStatusInk(statusValue: Int?, scheme: ColorScheme): Color {
    val dark = scheme.surface.luminance() < 0.5f
    return when (statusValue) {
        0, 1 -> if (dark) Amber500 else Amber800 // New / Pending
        2 -> scheme.primaryText // Confirmed
        3, 4 -> if (dark) Sky300 else Sky800 // OnTheWay / InProgress
        5 -> if (dark) Green400 else Green800 // Completed
        else -> if (dark) Slate300 else Slate600 // Cancelled, and a status this build does not know
    }
}
