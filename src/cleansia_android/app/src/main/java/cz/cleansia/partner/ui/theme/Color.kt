package cz.cleansia.partner.ui.theme

import androidx.compose.ui.graphics.Color

// Primary - Sky Blue
val Primary = Color(0xFF0284C7)
val PrimaryLight = Color(0xFF38BDF8)
val PrimaryDark = Color(0xFF0369A1)
val PrimaryContainer = Color(0xFFE0F2FE)
val OnPrimary = Color.White
val OnPrimaryContainer = Color(0xFF0C4A6E)

// Secondary - Slate
val Secondary = Color(0xFF64748B)
val SecondaryLight = Color(0xFF94A3B8)
val SecondaryDark = Color(0xFF475569)
val SecondaryContainer = Color(0xFFF1F5F9)
val OnSecondary = Color.White
val OnSecondaryContainer = Color(0xFF1E293B)

// Background
val Background = Color(0xFFF8FAFC)
val Surface = Color.White
val SurfaceVariant = Color(0xFFF1F5F9)
val OnBackground = Color(0xFF0F172A)
val OnSurface = Color(0xFF1E293B)
val OnSurfaceVariant = Color(0xFF64748B)

// Status colors
val Success = Color(0xFF22C55E)
val SuccessLight = Color(0xFF86EFAC)
val SuccessContainer = Color(0xFFDCFCE7)
val OnSuccess = Color.White
val OnSuccessContainer = Color(0xFF166534)

val Warning = Color(0xFFF59E0B)
val WarningLight = Color(0xFFFCD34D)
val WarningContainer = Color(0xFFFEF3C7)
val OnWarning = Color.White
val OnWarningContainer = Color(0xFF92400E)

val Error = Color(0xFFEF4444)
val ErrorLight = Color(0xFFFCA5A5)
val ErrorContainer = Color(0xFFFEE2E2)
val OnError = Color.White
val OnErrorContainer = Color(0xFF991B1B)

val Info = Color(0xFF3B82F6)
val InfoLight = Color(0xFF93C5FD)
val InfoContainer = Color(0xFFDBEAFE)
val OnInfo = Color.White
val OnInfoContainer = Color(0xFF1E40AF)

// Outline
val Outline = Color(0xFFCBD5E1)
val OutlineVariant = Color(0xFFE2E8F0)

// Scrim
val Scrim = Color(0xFF000000)

// Dark theme colors
val PrimaryDark_Theme = Color(0xFF7DD3FC)
val OnPrimaryDark_Theme = Color(0xFF003A57)
val PrimaryContainerDark = Color(0xFF004D73)
val OnPrimaryContainerDark = Color(0xFFCAE6FF)

val SecondaryDark_Theme = Color(0xFFBBC7DB)
val OnSecondaryDark_Theme = Color(0xFF263141)
val SecondaryContainerDark = Color(0xFF3C4858)
val OnSecondaryContainerDark = Color(0xFFD7E3F8)

val BackgroundDark = Color(0xFF0F172A)
val SurfaceDark = Color(0xFF1E293B)
val SurfaceVariantDark = Color(0xFF334155)
val OnBackgroundDark = Color(0xFFE2E8F0)
val OnSurfaceDark = Color(0xFFE2E8F0)
val OnSurfaceVariantDark = Color(0xFFCAD3E0)

val OutlineDark = Color(0xFF475569)
val OutlineVariantDark = Color(0xFF334155)

// Dark theme error colors - for proper contrast on dark backgrounds
val ErrorDark = Color(0xFFF87171)              // Lighter red for dark theme
val ErrorContainerDark = Color(0xFF7F1D1D)     // Dark red container
val OnErrorContainerDark = Color(0xFFFECACA)   // Light text on dark red container

// Dark theme warning colors
val WarningDark = Color(0xFFFBBF24)            // Lighter amber for dark theme
val WarningContainerDark = Color(0xFF92400E)   // Visible amber container for dark surfaces
val OnWarningContainerDark = Color(0xFFFEF3C7) // Light text on dark amber container

// Dark theme success colors
val SuccessDark = Color(0xFF4ADE80)            // Lighter green for dark theme
val SuccessContainerDark = Color(0xFF166534)   // Visible green container for dark surfaces
val OnSuccessContainerDark = Color(0xFFBBF7D0) // Light text on dark green container

// Dark theme info colors
val InfoDark = Color(0xFF60A5FA)               // Lighter blue for dark theme
val InfoContainerDark = Color(0xFF1E40AF)      // Visible blue container for dark surfaces
val OnInfoContainerDark = Color(0xFFBFDBFE)    // Light text on dark blue container

// Purple accent
val Purple = Color(0xFF8B5CF6)
val PurpleDark = Color(0xFFA78BFA)             // Softer purple for dark theme

// Cyan accent
val Cyan = Color(0xFF06B6D4)
val CyanDark = Color(0xFF22D3EE)              // Softer cyan for dark theme

// Timer colors - for cleaning countdown timer
object TimerColors {
    val Plenty = Color(0xFF16A34A)      // Darker Green - >70% time remaining
    val Caution = Color(0xFFD68904)     // Yellow/Orange #D68904 - 30-70% time remaining
    val Urgent = Color(0xFFB91C1C)      // Darker Red - <30% time remaining
    val Overtime = Color(0xFF7F1D1D)    // Even darker Red - past estimated time
    val RingBackground = Color(0xFFE8E8E8)       // Light mode ring background
    val RingBackgroundDark = Color(0xFF3A3A3A)   // Dark mode ring background
}

// Workflow/Stepper colors - consistent with timer
object WorkflowColors {
    val Completed = Color(0xFF16A34A)   // Same darker green as TimerColors.Plenty
}
