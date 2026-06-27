import Foundation

/// Emoji + display name for a known extra slug — mirrors the customer wizard's
/// mapping so both surfaces show the same icon/name. Unknown slugs fall back to
/// ✨ + a title-cased slug (the `emojiForExtraSlug`/`nameForExtraSlug` parity).
enum OrderExtras {
    static func emoji(_ slug: String) -> String {
        switch slug {
        case "inside-oven": "🔥"
        case "inside-fridge": "❄️"
        case "interior-windows": "🪟"
        case "laundry-ironing": "🧺"
        case "pet-hair-supplement": "🐾"
        default: "✨"
        }
    }

    static func name(_ slug: String) -> String {
        switch slug {
        case "inside-oven": "Inside oven cleaning"
        case "inside-fridge": "Inside fridge cleaning"
        case "interior-windows": "Interior windows"
        case "laundry-ironing": "Laundry & ironing"
        case "pet-hair-supplement": "Pet hair deep-clean"
        default: slug.replacingOccurrences(of: "-", with: " ").capitalizedFirst
        }
    }
}

private extension String {
    var capitalizedFirst: String {
        guard let first else { return self }
        return first.uppercased() + dropFirst()
    }
}
