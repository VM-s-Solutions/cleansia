import SwiftUI

public enum CleansiaColors {
    public static let primary = Color.dynamic(light: Palette.sky600, dark: Palette.sky400)
    /// The brand blue for TEXT on a light surface: sky-600 reads 4.10:1 on white, under the 4.5:1 floor,
    /// so text takes sky-700 in light mode (5.93:1); dark mode keeps the primary's sky-400. Fills, borders,
    /// icons and buttons keep `primary`.
    public static let primaryText = Color.dynamic(light: Palette.sky700, dark: Palette.sky400)
    /// Dark mode takes sky-950, 6.48:1 on the dark primary's sky-400, where sky-900 read 4.42:1 — every filled
    /// button's label. Light mode keeps white on the brand blue.
    public static let onPrimary = Color.dynamic(light: .white, dark: Palette.sky950)
    public static let primaryContainer = Color.dynamic(light: Palette.sky100, dark: Palette.sky700)
    /// The brand blue for TEXT on `primaryContainer` — a badge, a chip, an initial on its disc: sky-700 in
    /// light mode, as `primaryText` (5.17:1 on sky-100), and sky-100 in dark mode, where the container is
    /// sky-700 and `primaryText`'s sky-400 reads 2.77:1 (5.17:1).
    public static let primaryTextOnContainer = Color.dynamic(light: Palette.sky700, dark: Palette.sky100)
    /// The brand blue for an ICON on `primaryContainer` — a halo, a row's leading disc, a round call button:
    /// the primary in light mode (sky-600 on sky-100, 3.57:1), sky-100 in dark mode, where the container is
    /// sky-700 and the primary's sky-400 reads 2.77:1, under the 3:1 a graphic needs (5.17:1). Android's
    /// `primaryIconOnContainer` is the same pair.
    public static let primaryIconOnContainer = Color.dynamic(light: Palette.sky600, dark: Palette.sky100)
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

    /// Deliberately CONTRASTING with `surface`: dark in light mode, light in dark mode. For transient
    /// overlays (the snackbar) that must read as "on top of" the page rather than "part of" it — a
    /// surface-coloured pill on a surface-coloured page is nearly invisible whichever theme you're in.
    public static let inverseSurface = Color.dynamic(light: Palette.slate900, dark: Palette.slate100)
    public static let onInverseSurface = Color.dynamic(light: Palette.slate50, dark: Palette.slate900)

    public static let outline = Color.dynamic(light: Palette.slate200, dark: Palette.slate700)

    /// Ink and hairline for a control the user may read but not change.
    ///
    /// 0.38 is Material 3's disabled opacity, which Android already applies for free through the
    /// M3 defaults — matching it here is what makes a disabled field look the same on both
    /// platforms rather than looking editable on one of them.
    public static let disabledInk = onSurface.opacity(0.38)
    public static let disabledOutline = outline.opacity(0.38)
    public static let outlineVariant = Color.dynamic(light: Palette.slate200, dark: Palette.slate700)

    public static let error = Color.dynamic(light: Palette.errorText, dark: Palette.darkError)
    public static let onError = Color.dynamic(light: .white, dark: Palette.errorText)
    public static let errorContainer = Color.dynamic(light: Palette.errorBg, dark: Palette.darkErrorContainer)
    public static let onErrorContainer = Color.dynamic(light: Palette.errorText, dark: Palette.darkOnErrorContainer)

    /// The text ink for a surface that is deliberately theme-INVARIANT — the avatar disc, which reads as
    /// a cut-out in the brand gradient and stays white in both schemes. Because the surface never adapts,
    /// its ink must not either: `primary` resolves to sky400 in dark and measures 2.14:1 on white, and the
    /// light-mode brand blue is 4.10:1, under the 4.5:1 floor for the 18pt initials. So this pins sky-700,
    /// 5.93:1 in both schemes, in both apps; Android's customer `ProfileTab.kt` pins the same colour. The
    /// hexes are the source of truth and the testable surface — a `Color` → `UIColor` roundtrip is
    /// trait-dependent on the iOS-16 floor, they are not.
    public static let primaryTextOnFixedWhite = Color(hex: primaryTextOnFixedWhiteHex)

    static let fixedWhiteHex: UInt32 = 0xFFFFFF
    static let primaryTextOnFixedWhiteHex: UInt32 = 0x0369A1

    public static let successText = Palette.successText
    public static let successBg = Palette.successBg
    public static let warningStar = Palette.warningStar

    /// A status word drawn in its own amber or green — on the surface, or as a pill's label on a 12 % wash of
    /// itself — reads 4.5:1 or more in both modes: amber-800 and green-800 in light mode, the warning star's
    /// amber-500 and green-400 in dark. `warningStar` read 2.15:1 on white (1.96:1 on its wash) and
    /// `successText`'s green-700 2.92:1 on the dark surface (2.64:1 on its wash). The customer apps' dispute
    /// pills and Android's partner `pendingInk` take the same pairs.
    public static let pendingInk = Color.dynamic(light: Palette.amber800, dark: Palette.warningStar)
    public static let successInk = Color.dynamic(light: Palette.green800, dark: Palette.green400)

    // Fixed brand ramp for the splash gradient (sky-600 → sky-400), matching
    // Android's SplashScreen which does not vary with the color scheme.
    public static let splashGradientStart = Palette.sky600
    public static let splashGradientEnd = Palette.sky400
}
