import Foundation

/// Whether a customer's city is one of the serviced cities the server would accept.
///
/// **A port of the server's `Cleansia.Core.Domain.ServiceAreas.CityNameMatch`, and it has to stay one.**
/// The server is the authority — it runs this rule inside `CreateOrder` and refuses the booking. This
/// copy exists only so the customer is told at address-selection time rather than at payment, which is
/// where they used to find out.
///
/// **The danger of a copy is being STRICTER than the server, not looser.** A client that refuses a city
/// the server would accept tells a paying customer we do not serve them when we do. So the rule is
/// mirrored exactly and `CityNameMatchTests` pins the same table of cases as the C# and Kotlin suites;
/// a divergence reddens one of the three. Being looser is survivable — the customer proceeds and the
/// server refuses, which is exactly the behaviour that shipped before this existed.
public enum CityNameMatch {

    public static func matches(_ servicedCityName: String?, _ customerCity: String?) -> Bool {
        let row = fold(servicedCityName)
        let city = fold(customerCity)
        guard !row.isEmpty, !city.isEmpty else { return false }
        return row == city || row == stripDistrict(city)
    }

    /// True when ANY serviced city matches — the question a screen actually asks.
    public static func isServiced(_ servicedCityNames: [String], _ customerCity: String?) -> Bool {
        servicedCityNames.contains { matches($0, customerCity) }
    }

    /// Trim, strip diacritics, lowercase, collapse whitespace runs to one space.
    private static func fold(_ value: String?) -> String {
        guard let value else { return "" }
        let trimmed = value.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !trimmed.isEmpty else { return "" }

        // `folding(.diacriticInsensitive)` is the platform's own decompose-strip-recompose. Pinned to
        // POSIX so a Turkish device does not lowercase "I" to a dotless ı and stop matching.
        let stripped = trimmed
            .folding(options: [.diacriticInsensitive], locale: Locale(identifier: "en_US_POSIX"))
            .lowercased()

        return stripped.split(whereSeparator: \.isWhitespace).joined(separator: " ")
    }

    /// Drop a trailing 1–2 digit district, and any quarter that follows it after a dash: `praha 8` and
    /// `praha 4 - chodov` both reduce to `praha`.
    ///
    /// Written as a walk rather than a regex because `NSRegularExpression` has a throwing initialiser,
    /// and `try!` is a SwiftLint error in this tree — rightly, since a force-try inside shared
    /// validation crashes every caller over a typo nobody can see.
    ///
    /// **A dash with no district number before it is left alone.** `praha-zapad` and `brno-venkov` have
    /// that exact shape and are *okresy* — the rural rings around those cities, not parts of them.
    private static func stripDistrict(_ folded: String) -> String {
        var head = folded

        if let dashIndex = head.lastIndex(where: isDash) {
            let beforeDash = String(head[head.startIndex ..< dashIndex])
                .trimmingCharacters(in: .whitespaces)
            // Only a quarter if a district number sits immediately before the dash.
            guard endsWithDistrictNumber(beforeDash) else { return folded }
            head = beforeDash
        }

        guard let lastSpace = head.lastIndex(of: " ") else { return folded }
        let district = head[head.index(after: lastSpace)...]
        guard !district.isEmpty, district.count <= 2, district.allSatisfy(\.isNumber) else {
            return folded
        }

        let base = String(head[head.startIndex ..< lastSpace]).trimmingCharacters(in: .whitespaces)
        return base.isEmpty ? folded : base
    }

    private static func endsWithDistrictNumber(_ value: String) -> Bool {
        guard let lastSpace = value.lastIndex(of: " ") else { return false }
        let tail = value[value.index(after: lastSpace)...]
        return !tail.isEmpty && tail.count <= 2 && tail.allSatisfy(\.isNumber)
    }

    private static func isDash(_ character: Character) -> Bool {
        character == "-" || character == "\u{2013}" || character == "\u{2014}"
    }
}
