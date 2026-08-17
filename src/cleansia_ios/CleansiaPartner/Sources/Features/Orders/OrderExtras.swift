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

    /// Extras reach the app as bare slugs — unlike services and packages, which carry the server's
    /// translations map — so the name is resolved against the app catalogue instead. A slug seeded
    /// after this build has no key, and `localized` hands the key straight back; the readable slug is
    /// shown rather than `extra_name_…` at a cleaner.
    static func name(_ slug: String) -> String {
        let key = "extra_name_" + slug.replacingOccurrences(of: "-", with: "_")
        let translated = L10n.localized(key)
        guard translated != key else {
            return slug.replacingOccurrences(of: "-", with: " ").capitalizedFirst
        }
        return translated
    }
}

private extension String {
    var capitalizedFirst: String {
        guard let first else { return self }
        return first.uppercased() + dropFirst()
    }
}
