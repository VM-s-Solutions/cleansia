import SwiftUI

public struct CleansiaTextStyle: Equatable {
    public let family: CleansiaFontFamily
    public let weight: Font.Weight
    public let size: CGFloat

    public init(family: CleansiaFontFamily, weight: Font.Weight, size: CGFloat) {
        self.family = family
        self.weight = weight
        self.size = size
    }

    public static func poppins(_ weight: Font.Weight, size: CGFloat) -> CleansiaTextStyle {
        CleansiaTextStyle(family: .poppins, weight: weight, size: size)
    }

    public static func nunito(_ weight: Font.Weight, size: CGFloat) -> CleansiaTextStyle {
        CleansiaTextStyle(family: .nunito, weight: weight, size: size)
    }

    /// Name-resolved, so SwiftUI scales it — but it carries no cascade. Only sound for a family
    /// that draws every script the app renders; anything else must go through `cleansiaFont`.
    var font: Font {
        guard family.glyphFallback == nil, let name = CleansiaFont.registeredName(family, weight: weight) else {
            return .system(size: size, weight: weight)
        }
        return .custom(name, size: size)
    }

    func uiFont(for dynamicTypeSize: DynamicTypeSize) -> UIFont {
        CleansiaFont.uiFont(self, size: CleansiaFont.scaledSize(size, for: dynamicTypeSize))
    }
}

private struct CleansiaFontModifier: ViewModifier {
    @Environment(\.dynamicTypeSize) private var dynamicTypeSize
    let style: CleansiaTextStyle

    func body(content: Content) -> some View {
        content.font(Font(style.uiFont(for: dynamicTypeSize) as CTFont))
    }
}

public extension View {
    func cleansiaFont(_ style: CleansiaTextStyle) -> some View {
        modifier(CleansiaFontModifier(style: style))
    }
}
