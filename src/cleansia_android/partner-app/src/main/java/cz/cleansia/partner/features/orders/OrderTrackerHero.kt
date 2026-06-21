package cz.cleansia.partner.features.orders

import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.runtime.Composable
import androidx.compose.ui.Modifier
import cz.cleansia.partner.api.model.OrderStatus

/**
 * In-sheet tracker. Background-less wrapper around
 * [ContinuousProgressBar] — the bar lives directly on the sheet
 * surface so the timer card above and the mascot overlay on the
 * right read as part of the same visual group (no competing pill
 * container fragmenting the hero area).
 *
 * Kept as its own composable rather than inlining the call so the
 * screen layout can keep a single tracker placement and we can swap
 * container chrome (or none) here without touching the screen.
 */
@Composable
fun OrderTrackerHero(
    status: OrderStatus?,
    modifier: Modifier = Modifier,
) {
    ContinuousProgressBar(
        status = status,
        modifier = modifier.fillMaxWidth(),
    )
}
