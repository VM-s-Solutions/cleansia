import CoreText
import SwiftUI

public enum CleansiaFontFamily: String {
    case poppins = "Poppins"
    case nunito = "Nunito"
}

public enum CleansiaFont {
    public static func poppins(_ weight: Font.Weight, size: CGFloat) -> Font {
        custom(.poppins, weight: weight, size: size)
    }

    public static func nunito(_ weight: Font.Weight, size: CGFloat) -> Font {
        custom(.nunito, weight: weight, size: size)
    }

    static func custom(_ family: CleansiaFontFamily, weight: Font.Weight, size: CGFloat) -> Font {
        guard let name = registeredName(family, weight: weight) else {
            return .system(size: size, weight: weight)
        }
        return .custom(name, size: size)
    }

    private static func registeredName(_ family: CleansiaFontFamily, weight: Font.Weight) -> String? {
        let candidate = "\(family.rawValue)-\(suffix(for: weight))"
        return UIFont(name: candidate, size: 1) != nil ? candidate : nil
    }

    private static func suffix(for weight: Font.Weight) -> String {
        switch weight {
        case .bold, .heavy, .black: return "Bold"
        case .semibold: return "SemiBold"
        case .medium: return "Medium"
        default: return "Regular"
        }
    }
}

public extension CleansiaFont {
    static func registerBundledFonts(in bundle: Bundle) {
        let names = [
            "Poppins-Medium", "Poppins-SemiBold", "Poppins-Bold",
            "Nunito-Regular", "Nunito-SemiBold", "Nunito-Bold",
        ]
        for name in names {
            guard let url = bundle.url(forResource: name, withExtension: "ttf") else { continue }
            CTFontManagerRegisterFontsForURL(url as CFURL, .process, nil)
        }
    }
}
