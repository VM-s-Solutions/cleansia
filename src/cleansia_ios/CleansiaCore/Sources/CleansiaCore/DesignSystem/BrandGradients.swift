import SwiftUI

/// Brand gradient pairs (the customer `BrandGradients.kt` parity). Light mode
/// uses the vivid brand hues; dark mode the muted variants so large gradient
/// surfaces don't scream against the slate-900 background. `plusHero` is the
/// fixed Sky950→Slate900 pair shared by the Plus upsell card and the Plus
/// subscribe hero — it does not vary with the color scheme.
public enum BrandGradient: CaseIterable {
    case blue
    case purple
    case cyan
    case plusHero

    /// The light/dark hex stops are the single source of truth — `colors` is
    /// derived from them. They are also the unit-testable surface: a SwiftUI
    /// `Color` → `UIColor` roundtrip FLATTENS dynamic providers on iOS 16
    /// (dynamic preservation arrived in iOS 17), so tests must assert these
    /// values, not an OS-dependent resolution. Values verbatim from
    /// `BrandGradients.kt` + `Color.kt` (blue = Sky600/800 → Sky400/700).
    public var stops: [(light: UInt32, dark: UInt32)] {
        switch self {
        case .blue:
            [(light: 0x0284C7, dark: 0x075985), (light: 0x38BDF8, dark: 0x0369A1)]
        case .purple:
            [(light: 0x7C3AED, dark: 0x5B2AB0), (light: 0xA78BFA, dark: 0x7C5ABF)]
        case .cyan:
            [(light: 0x0891B2, dark: 0x0E6E88), (light: 0x67E8F9, dark: 0x4BAEC1)]
        case .plusHero:
            [(light: 0x082F49, dark: 0x082F49), (light: 0x0F172A, dark: 0x0F172A)]
        }
    }

    public var colors: [Color] {
        stops.map { stop in
            stop.light == stop.dark
                ? Color(hex: stop.light)
                : .dynamic(light: Color(hex: stop.light), dark: Color(hex: stop.dark))
        }
    }

    /// The `Brush.linearGradient(colors)` parity — Compose's default runs
    /// top-left → bottom-right.
    public var linearGradient: LinearGradient {
        LinearGradient(colors: colors, startPoint: .topLeading, endPoint: .bottomTrailing)
    }
}
