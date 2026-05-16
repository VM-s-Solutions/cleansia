package cz.cleansia.core.ui.theme

import androidx.compose.ui.graphics.Color

/**
 * Semantic color tokens used by `:core` widgets that need to convey
 * success / error meaning independently of the Material color scheme.
 *
 * These live in `:core` rather than in either app so widgets like
 * [cz.cleansia.core.ui.components.PasswordRuleList] can render identically
 * across customer-app and partner-app without each app having to provide
 * matching colour constants.
 *
 * Hue choices mirror Tailwind: green-700 for success, red-700 for error.
 * Material's `colorScheme.error` is reserved for hard-failure UI; these
 * tokens are used for live-feedback (e.g. password-rule check icons).
 */
val SuccessText = Color(0xFF15803D) // green-700
val ErrorText = Color(0xFFB91C1C)   // red-700
