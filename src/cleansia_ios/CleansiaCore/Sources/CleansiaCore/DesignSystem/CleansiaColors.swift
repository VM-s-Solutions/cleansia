import SwiftUI

public enum CleansiaColors {
    public static let primary = Color.dynamic(light: Palette.sky600, dark: Palette.sky400)
    public static let onPrimary = Color.dynamic(light: .white, dark: Palette.sky900)
    public static let primaryContainer = Color.dynamic(light: Palette.sky100, dark: Palette.sky700)
    public static let onPrimaryContainer = Color.dynamic(light: Palette.sky900, dark: Palette.sky100)

    public static let secondary = Color.dynamic(light: Palette.sky400, dark: Palette.sky300)
    public static let onSecondary = Color.dynamic(light: .white, dark: Palette.sky900)
    public static let secondaryContainer = Color.dynamic(light: Palette.sky50, dark: Palette.sky800)
    public static let onSecondaryContainer = Color.dynamic(light: Palette.sky900, dark: Palette.sky100)

    /// The customer Android theme never overrides the tertiary slots, so its
    /// home milestone card renders the Material3 BASELINE tertiaryContainer
    /// (tertiary90/tertiary30) — mirrored verbatim for parity.
    public static let tertiaryContainer = Color.dynamic(light: Color(hex: 0xFFD8E4), dark: Color(hex: 0x633B48))

    public static let background = Color.dynamic(light: Palette.slate50, dark: Palette.slate900)
    public static let onBackground = Color.dynamic(light: Palette.slate900, dark: Palette.darkTextPrimary)

    public static let surface = Color.dynamic(light: .white, dark: Palette.slate800)
    public static let onSurface = Color.dynamic(light: Palette.slate900, dark: Palette.darkTextPrimary)
    public static let surfaceVariant = Color.dynamic(light: Palette.slate100, dark: Palette.darkSurfaceElevated)
    public static let onSurfaceVariant = Color.dynamic(light: Palette.slate700, dark: Palette.slate400)

    // Inverse (Material) surface — a high-contrast pill that opposes the app
    // background: a dark pill in light theme, a light pill in dark theme. Used
    // by the global snackbar so it reads as a distinct, deliberate chip in both
    // themes and both apps.
    public static let inverseSurface = Color.dynamic(light: Palette.slate900, dark: Palette.slate100)
    public static let onInverseSurface = Color.dynamic(light: .white, dark: Palette.slate900)
    public static let onInverseSurfaceVariant = Color.dynamic(light: Palette.slate400, dark: Palette.slate500)

    public static let outline = Color.dynamic(light: Palette.slate200, dark: Palette.slate700)
    public static let outlineVariant = Color.dynamic(light: Palette.slate200, dark: Palette.slate700)

    public static let error = Color.dynamic(light: Palette.errorText, dark: Palette.darkError)
    public static let onError = Color.dynamic(light: .white, dark: Palette.errorText)
    public static let errorContainer = Color.dynamic(light: Palette.errorBg, dark: Palette.darkErrorContainer)
    public static let onErrorContainer = Color.dynamic(light: Palette.errorText, dark: Palette.darkOnErrorContainer)

    public static let successText = Palette.successText
    public static let successBg = Palette.successBg
    public static let warningStar = Palette.warningStar

    // Fixed brand ramp for the splash gradient (sky-600 → sky-400), matching
    // Android's SplashScreen which does not vary with the color scheme.
    public static let splashGradientStart = Palette.sky600
    public static let splashGradientEnd = Palette.sky400
}
