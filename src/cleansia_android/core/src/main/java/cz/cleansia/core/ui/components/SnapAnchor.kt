package cz.cleansia.core.ui.components

/**
 * Resting positions of a [SnapSheet] laid over a full-bleed backdrop, expressed
 * as the fraction of the container the sheet covers.
 *
 * Mirrors the iOS `SnapAnchor` (CleansiaCore/Components/SnapSheet.swift) value
 * for value, so an order detail parks in the same three places on both
 * platforms. [Peek] is the resting default; [MapFocus] is what the user drags
 * down to when they want the backdrop instead of the panel.
 */
enum class SnapAnchor(val coveredFraction: Float) {
    MapFocus(0.30f),
    Peek(0.75f),
    Expanded(0.95f),
}

/**
 * Y position of the sheet's top edge inside a container [containerHeightPx] tall.
 *
 * [minSheetHeightPx] is a floor, not a preference: a sheet with a sticky action
 * footer needs a certain height before the buttons stop fitting, and 30% of a
 * short display is below it. Rather than let the footer slide past the bottom
 * edge, the shallowest anchor stops early on such displays.
 */
fun snapSheetTopPx(
    anchor: SnapAnchor,
    containerHeightPx: Float,
    minSheetHeightPx: Float = 0f,
): Float {
    if (containerHeightPx <= 0f) return 0f
    val byFraction = containerHeightPx * (1f - anchor.coveredFraction)
    val byFloor = containerHeightPx - minSheetHeightPx
    return minOf(byFraction, byFloor).coerceIn(0f, containerHeightPx)
}
