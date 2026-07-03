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

    public var colors: [Color] {
        switch self {
        case .blue:
            [
                .dynamic(light: Palette.sky600, dark: Palette.sky800),
                .dynamic(light: Palette.sky400, dark: Palette.sky700)
            ]
        case .purple:
            [
                .dynamic(light: Color(hex: 0x7C3AED), dark: Color(hex: 0x5B2AB0)),
                .dynamic(light: Color(hex: 0xA78BFA), dark: Color(hex: 0x7C5ABF))
            ]
        case .cyan:
            [
                .dynamic(light: Color(hex: 0x0891B2), dark: Color(hex: 0x0E6E88)),
                .dynamic(light: Color(hex: 0x67E8F9), dark: Color(hex: 0x4BAEC1))
            ]
        case .plusHero:
            [Palette.sky950, Palette.slate900]
        }
    }

    /// The `Brush.linearGradient(colors)` parity — Compose's default runs
    /// top-left → bottom-right.
    public var linearGradient: LinearGradient {
        LinearGradient(colors: colors, startPoint: .topLeading, endPoint: .bottomTrailing)
    }
}
