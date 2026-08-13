package cz.cleansia.core.ui.theme

import androidx.compose.ui.graphics.Color

/**
 * Semantic success/error tokens for `:core` widgets, independent of the Material scheme.
 *
 * **They live in `:core` rather than either app** so shared widgets render identically in both without
 * each app redefining them.
 */
val SuccessText = Color(0xFF15803D) // green-700
val ErrorText = Color(0xFFB91C1C)   // red-700
