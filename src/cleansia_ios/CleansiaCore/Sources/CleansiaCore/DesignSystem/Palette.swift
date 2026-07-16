import SwiftUI

enum Palette {
    static let sky50 = Color(hex: 0xF0F9FF)
    static let sky100 = Color(hex: 0xE0F2FE)
    static let sky200 = Color(hex: 0xBAE6FD)
    static let sky300 = Color(hex: 0x7DD3FC)
    static let sky400 = Color(hex: 0x38BDF8)
    static let sky500 = Color(hex: 0x0EA5E9)
    static let sky600 = Color(hex: 0x0284C7)
    static let sky700 = Color(hex: 0x0369A1)
    static let sky800 = Color(hex: 0x075985)
    static let sky900 = Color(hex: 0x0C4A6E)
    static let sky950 = Color(hex: 0x082F49)

    static let slate50 = Color(hex: 0xF8FAFC)
    static let slate100 = Color(hex: 0xF1F5F9)
    static let slate200 = Color(hex: 0xE2E8F0)
    static let slate300 = Color(hex: 0xCBD5E1)
    static let slate400 = Color(hex: 0x94A3B8)
    static let slate500 = Color(hex: 0x64748B)
    static let slate600 = Color(hex: 0x475569)
    static let slate700 = Color(hex: 0x334155)
    static let slate800 = Color(hex: 0x1E293B)
    static let slate900 = Color(hex: 0x0F172A)

    static let darkSurfaceElevated = Color(hex: 0x283548)
    static let darkTextPrimary = Color(hex: 0xE2E8F0)

    static let successBg = Color(hex: 0xDCFCE7)
    static let successText = Color(hex: 0x15803D)
    static let errorBg = Color(hex: 0xFEE2E2)
    static let errorText = Color(hex: 0xB91C1C)
    static let warningStar = Color(hex: 0xF59E0B)
    static let darkError = Color(hex: 0xFCA5A5)
    /// Deep error-family red for dark-mode "container" surfaces (Material-3 dark
    /// errorContainer). Pairs with darkError as the on-color. Replaces the stray
    /// sky800 (blue) that dark errorContainer used to resolve to (T-0396).
    static let darkErrorContainer = Color(hex: 0x8C1D18)
}
