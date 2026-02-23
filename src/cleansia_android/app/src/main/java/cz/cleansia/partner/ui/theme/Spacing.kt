package cz.cleansia.partner.ui.theme

import androidx.compose.ui.unit.Dp
import androidx.compose.ui.unit.dp

/**
 * 8dp grid spacing system. All spacing values are multiples of the base unit.
 * Use named constants instead of inline dp literals for consistency.
 */
object Spacing {
    /** 2.dp — hairline gaps, divider padding */
    val xxs: Dp = 2.dp
    /** 4.dp — tight inner padding, icon-to-text gaps */
    val xs: Dp = 4.dp
    /** 8.dp — base unit, compact card padding, small gaps */
    val sm: Dp = 8.dp
    /** 12.dp — common inter-element spacing */
    val md: Dp = 12.dp
    /** 16.dp — standard card padding, screen horizontal margin */
    val lg: Dp = 16.dp
    /** 20.dp — hero card padding */
    val xl: Dp = 20.dp
    /** 24.dp — section spacing, bottom nav horizontal padding */
    val xxl: Dp = 24.dp
    /** 32.dp — large section gaps, empty state padding */
    val xxxl: Dp = 32.dp

    /** Screen-level horizontal padding (left/right page margin) */
    val screenHorizontal: Dp = lg
    /** Vertical spacing between LazyColumn items */
    val listItemSpacing: Dp = md
    /** Standard card internal padding */
    val cardPadding: Dp = lg
    /** Bottom navigation safe-area bottom padding for content lists */
    val bottomNavPadding: Dp = 100.dp
    /** Top content padding (below floating header) */
    val topContentPadding: Dp = 64.dp
}
