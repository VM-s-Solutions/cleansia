import SwiftUI

public enum CleansiaTypography {
    public static let scale = Scale()

    public static let displayLarge = scale.displayLarge
    public static let displayMedium = scale.displayMedium
    public static let headlineLarge = scale.headlineLarge
    public static let headlineMedium = scale.headlineMedium
    public static let headlineSmall = scale.headlineSmall

    public static let titleLarge = scale.titleLarge.font
    public static let titleMedium = scale.titleMedium.font

    public static let bodyLarge = scale.bodyLarge.font
    public static let bodyMedium = scale.bodyMedium.font

    public static let labelLarge = scale.labelLarge.font
    public static let labelMedium = scale.labelMedium.font
    public static let labelSmall = scale.labelSmall.font

    /// Poppins slots are `CleansiaTextStyle` rather than `Font` so they cannot reach `.font(_:)`,
    /// which resolves by name and would drop the Cyrillic fallback; they take `.cleansiaFont(_:)`.
    public struct Scale {
        public let displayLarge = CleansiaTextStyle.poppins(.bold, size: 32)
        public let displayMedium = CleansiaTextStyle.poppins(.bold, size: 28)
        public let headlineLarge = CleansiaTextStyle.poppins(.semibold, size: 24)
        public let headlineMedium = CleansiaTextStyle.poppins(.semibold, size: 22)
        public let headlineSmall = CleansiaTextStyle.poppins(.semibold, size: 18)

        public let titleLarge = CleansiaTextStyle.nunito(.bold, size: 16)
        public let titleMedium = CleansiaTextStyle.nunito(.bold, size: 15)

        public let bodyLarge = CleansiaTextStyle.nunito(.regular, size: 16)
        public let bodyMedium = CleansiaTextStyle.nunito(.regular, size: 14)

        public let labelLarge = CleansiaTextStyle.nunito(.bold, size: 14)
        public let labelMedium = CleansiaTextStyle.nunito(.semibold, size: 12)
        public let labelSmall = CleansiaTextStyle.nunito(.bold, size: 12)
    }
}
