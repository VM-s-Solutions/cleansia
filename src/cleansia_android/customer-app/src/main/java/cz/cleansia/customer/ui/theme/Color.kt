package cz.cleansia.customer.ui.theme

import androidx.compose.ui.graphics.Color

// ─── Primary — Tailwind sky scale (matches web's PrimeNG Aura preset) ───
val Sky50 = Color(0xFFF0F9FF)
val Sky100 = Color(0xFFE0F2FE)
val Sky200 = Color(0xFFBAE6FD)
val Sky300 = Color(0xFF7DD3FC)
val Sky400 = Color(0xFF38BDF8) // dark-mode primary
val Sky500 = Color(0xFF0EA5E9)
val Sky600 = Color(0xFF0284C7) // brand primary (light mode)
val Sky700 = Color(0xFF0369A1) // brand secondary, top-bar title
val Sky800 = Color(0xFF075985)
val Sky900 = Color(0xFF0C4A6E)
val Sky950 = Color(0xFF082F49)

// ─── Neutrals — slate scale (web tokens) ───
val Slate50 = Color(0xFFF8FAFC)
val Slate100 = Color(0xFFF1F5F9)
val Slate200 = Color(0xFFE2E8F0)
val Slate300 = Color(0xFFCBD5E1)
val Slate400 = Color(0xFF94A3B8)
val Slate500 = Color(0xFF64748B)
val Slate600 = Color(0xFF475569)
val Slate700 = Color(0xFF334155)
val Slate800 = Color(0xFF1E293B)
val Slate900 = Color(0xFF0F172A)

// ─── Light surfaces ───
val LightBackground = Slate50         // #F8FAFC — page bg
val LightSurface = Color.White        // cards
val LightSurfaceVariant = Slate100    // input fill, segmented track
val LightBorder = Slate200
val LightTextPrimary = Slate900       // headings near-black
val LightTextBody = Slate700
val LightTextSecondary = Slate500
val LightTextMuted = Slate400         // placeholders, inactive

// ─── Dark surfaces ───
val DarkBackground = Slate900         // #0F172A
val DarkSurface = Slate800            // #1E293B
val DarkSurfaceElevated = Color(0xFF283548)
val DarkBorder = Slate700
val DarkTextPrimary = Color(0xFFE2E8F0) // slate-200
val DarkTextSecondary = Slate400

// ─── Semantic ───
val SuccessBg = Color(0xFFDCFCE7)  // green-100
val SuccessText = Color(0xFF15803D) // green-700
val ErrorBg = Color(0xFFFEE2E2)    // red-100
val ErrorText = Color(0xFFB91C1C)  // red-700
val WarningStar = Color(0xFFF59E0B) // amber-500

// ─── Order status pills ───
val StatusPendingBg = Sky100
val StatusPendingText = Sky700
val StatusConfirmedBg = Sky600
val StatusConfirmedText = Color.White
val StatusInProgressBg = Sky400
val StatusInProgressText = Sky900
val StatusCompletedBg = SuccessBg
val StatusCompletedText = SuccessText
val StatusCancelledBg = Slate100
val StatusCancelledText = Slate500
val StatusFailedBg = ErrorBg
val StatusFailedText = ErrorText
