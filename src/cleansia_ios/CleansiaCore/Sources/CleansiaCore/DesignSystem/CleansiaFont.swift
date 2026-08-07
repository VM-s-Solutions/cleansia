import CoreText
import SwiftUI

public enum CleansiaFontFamily: String {
    case poppins = "Poppins"
    case nunito = "Nunito"

    /// Asserted against the shipped binaries by `BundledFontCoverageTests` in both app targets.
    var drawsCyrillic: Bool {
        switch self {
        case .poppins: false
        case .nunito: true
        }
    }

    var glyphFallback: CleansiaFontFamily? {
        drawsCyrillic ? nil : .nunito
    }
}

public enum CleansiaFont {
    enum BundledFace: String, CaseIterable {
        case poppinsMedium = "Poppins-Medium"
        case poppinsSemiBold = "Poppins-SemiBold"
        case poppinsBold = "Poppins-Bold"
        case nunitoRegular = "Nunito-Regular"
        case nunitoSemiBold = "Nunito-SemiBold"
        case nunitoBold = "Nunito-Bold"

        var family: CleansiaFontFamily {
            switch self {
            case .poppinsMedium, .poppinsSemiBold, .poppinsBold: .poppins
            case .nunitoRegular, .nunitoSemiBold, .nunitoBold: .nunito
            }
        }

        var weight: Font.Weight {
            switch self {
            case .poppinsMedium: .medium
            case .poppinsSemiBold, .nunitoSemiBold: .semibold
            case .poppinsBold, .nunitoBold: .bold
            case .nunitoRegular: .regular
            }
        }
    }

    public static func nunito(_ weight: Font.Weight, size: CGFloat) -> Font {
        CleansiaTextStyle(family: .nunito, weight: weight, size: size).font
    }

    public static func uiFont(_ family: CleansiaFontFamily, weight: Font.Weight, size: CGFloat) -> UIFont {
        uiFont(CleansiaTextStyle(family: family, weight: weight, size: size), size: size)
    }

    static func face(_ family: CleansiaFontFamily, weight: Font.Weight) -> BundledFace? {
        BundledFace.allCases.first { $0.family == family && $0.weight == normalized(weight) }
    }

    static func glyphFallback(for face: BundledFace) -> BundledFace? {
        guard let family = face.family.glyphFallback else { return nil }
        return self.face(family, weight: face.weight) ?? self.face(family, weight: .regular)
    }

    static func registeredName(_ family: CleansiaFontFamily, weight: Font.Weight) -> String? {
        guard let face = face(family, weight: weight), UIFont(name: face.rawValue, size: 1) != nil else { return nil }
        return face.rawValue
    }

    /// SwiftUI resolves `Font.custom(_:size:)` against the environment at render time; a `Font`
    /// built from a `CTFont` is fixed, so the cascaded path has to scale the point size itself.
    /// Rounding to whole points is what reproduces SwiftUI's own curve exactly.
    static func scaledSize(_ base: CGFloat, for dynamicTypeSize: DynamicTypeSize) -> CGFloat {
        let traits = UITraitCollection(preferredContentSizeCategory: contentSizeCategory(dynamicTypeSize))
        return UIFontMetrics.default.scaledValue(for: base, compatibleWith: traits).rounded()
    }

    /// The brand face plus, ahead of the system's own cascade, the bundled face that draws the
    /// glyphs it lacks. CoreText itemizes the line across that chain **per glyph**, so a Latin
    /// heading containing one Cyrillic word keeps Poppins for everything Poppins can draw.
    static func uiFont(_ style: CleansiaTextStyle, size: CGFloat) -> UIFont {
        guard let face = face(style.family, weight: style.weight),
              let primary = UIFont(name: face.rawValue, size: size)
        else { return .systemFont(ofSize: size, weight: uiWeight(style.weight)) }

        guard let fallback = glyphFallback(for: face), UIFont(name: fallback.rawValue, size: size) != nil else {
            return primary
        }
        let cascade = CTFontDescriptorCreateWithAttributes([
            kCTFontCascadeListAttribute: [CTFontDescriptorCreateWithNameAndSize(fallback.rawValue as CFString, size)]
        ] as CFDictionary)
        return CTFontCreateCopyWithAttributes(primary as CTFont, size, nil, cascade) as UIFont
    }

    private static func normalized(_ weight: Font.Weight) -> Font.Weight {
        switch weight {
        case .bold, .heavy, .black: .bold
        case .semibold: .semibold
        case .medium: .medium
        default: .regular
        }
    }

    private static func uiWeight(_ weight: Font.Weight) -> UIFont.Weight {
        switch normalized(weight) {
        case .bold: .bold
        case .semibold: .semibold
        case .medium: .medium
        default: .regular
        }
    }

    private static let contentSizeCategories: [DynamicTypeSize: UIContentSizeCategory] = [
        .xSmall: .extraSmall,
        .small: .small,
        .medium: .medium,
        .large: .large,
        .xLarge: .extraLarge,
        .xxLarge: .extraExtraLarge,
        .xxxLarge: .extraExtraExtraLarge,
        .accessibility1: .accessibilityMedium,
        .accessibility2: .accessibilityLarge,
        .accessibility3: .accessibilityExtraLarge,
        .accessibility4: .accessibilityExtraExtraLarge,
        .accessibility5: .accessibilityExtraExtraExtraLarge
    ]

    private static func contentSizeCategory(_ size: DynamicTypeSize) -> UIContentSizeCategory {
        contentSizeCategories[size] ?? .large
    }
}

public extension CleansiaFont {
    static func registerBundledFonts(in bundle: Bundle) {
        for face in BundledFace.allCases {
            guard let url = bundle.url(forResource: face.rawValue, withExtension: "ttf") else { continue }
            CTFontManagerRegisterFontsForURL(url as CFURL, .process, nil)
        }
    }
}
