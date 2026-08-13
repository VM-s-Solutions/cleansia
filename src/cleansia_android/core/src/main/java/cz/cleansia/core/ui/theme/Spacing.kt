package cz.cleansia.core.ui.theme

import androidx.compose.ui.unit.dp

/**
 * Cleansia spacing scale — the canonical layout spacing values used across the
 * app. Backed by an 8-pt grid with two 4-pt extensions for tight clusters.
 *
 * Why a global object instead of a CompositionLocal: layout values never differ
 * between themes (light vs dark vs Plus), so a `MaterialTheme.local…` lookup
 * adds runtime cost for zero benefit. A plain `object` keeps usage identical
 * to `MaterialTheme.colorScheme.*` semantics from a caller's POV — they read
 * `Spacing.M` instead of `16.dp` — without paying for indirection.
 *
 * Migration policy: new code must use this scale. Existing screens continue to
 * use literals until they're modified for unrelated reasons; we don't run a
 * blanket find-and-replace because regressions are visual + hard to QA.
 *
 * Legend:
 *  - XXS / XS — within-cluster tightening (icon + label, hint text under field)
 *  - S        — paragraph rhythm, default Spacer between siblings
 *  - M        — section padding, card content padding
 *  - L        — between major regions on the same screen
 *  - XL / XXL — between hero blocks, around bottom-sheet content
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
