package cz.cleansia.core.ui.theme

import androidx.compose.ui.unit.dp

/**
 * The canonical spacing scale — an 8-pt grid with two 4-pt extensions for tight clusters.
 *
 * A plain object rather than a CompositionLocal: layout values never differ between themes, so a theme
 * lookup costs runtime for nothing.
 *
 * **New code must use this scale; existing screens keep their literals until touched for another
 * reason** — a blanket find-and-replace produces visual regressions that are hard to QA.
 * -> /mobile-app/patterns#spacing
 */
object Spacing {
    /** 2dp — hairline; tooltip arrow, divider thickness. */
    val Hair = 2.dp

    /** 4dp — within-cluster spacing (icon ↔ label). */
    val XXS = 4.dp

    /** 8dp — default Spacer between siblings; row gaps in dense lists. */
    val XS = 8.dp

    /** 12dp — small section internal padding; chip gaps. */
    val S = 12.dp

    /** 16dp — default screen edge padding, card content padding. */
    val M = 16.dp

    /** 20dp — common screen-content horizontal padding (legacy 20-pt usage). */
    val ML = 20.dp

    /** 24dp — between major regions on the same screen. */
    val L = 24.dp

    /** 32dp — between hero blocks, after a section header. */
    val XL = 32.dp

    /** 40dp — hero spacing around bottom-sheet content / paywall CTAs. */
    val XXL = 40.dp
}
