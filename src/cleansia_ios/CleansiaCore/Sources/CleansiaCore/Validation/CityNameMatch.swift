import Foundation

/// Whether an address is in one of the serviced cities, the question `CreateOrder` answers.
///
/// **A port of the server's `Cleansia.Core.Domain.ServiceAreas.CityNameMatch`, and it has to stay one.** The server
/// refuses the booking; this copy only tells the customer earlier. A copy that is STRICTER than the server tells a
/// paying customer we do not serve them when we do, so the rule is mirrored exactly and `CityNameMatchTests` pins
/// the case table the C#, Kotlin and TypeScript suites pin. Change a case in all four.
public enum CityNameMatch {
    private static let okresRings: [(city: String, ring: String)] = [
        ("praha", "zapad"), ("praha", "vychod"), ("brno", "venkov"), ("plzen", "sever"), ("plzen", "jih")
    ]

    public static func isServiced(_ cities: [ServicedCity], city: String?, zipCode: String?) -> Bool {
        let postcode = postcode(zipCode)
        return cities.contains { matches($0, city: city, postcode: postcode) }
    }

    /// The name rule: a row matches the customer's city, the city less a dash-separated part, or either less a
    /// trailing district, `Praha 8`, `Pardubice II`, `Brno-střed`, `Praha 4 - Chodov`. The five okres rings around a
    /// city share the dashed shape and are not the city. The rule runs on the customer's string only, so a row named
    /// for one district never claims the whole city.
    public static func matches(_ servicedCityName: String?, _ customerCity: String?) -> Bool {
        let row = fold(servicedCityName)
        guard !row.isEmpty else { return false }
        return candidates(fold(customerCity)).contains(row)
    }

    /// A row with a ZIP prefix is decided by the address's postcode whenever it has a valid one, whatever the city
    /// says; with no valid postcode it falls back to the name, like a row without a prefix.
    private static func matches(_ row: ServicedCity, city: String?, postcode: String?) -> Bool {
        let prefix = withoutWhitespace(row.zipPrefix)
        if !prefix.isEmpty, let postcode {
            return postcode.hasPrefix(prefix)
        }
        return matches(row.name, city)
    }

    /// Five digits once every space, a no-break one included, is taken out.
    private static func postcode(_ zipCode: String?) -> String? {
        let digits = withoutWhitespace(zipCode)
        guard digits.count == 5, digits.allSatisfy({ ("0" ... "9").contains($0) }) else { return nil }
        return digits
    }

    private static func withoutWhitespace(_ value: String?) -> String {
        (value ?? "").filter { !$0.isWhitespace }
    }

    /// Diacritics stripped, lower case, every dash one `-`, every run of spaces one space. Pinned to POSIX so a
    /// Turkish device does not lowercase "I" to a dotless ı.
    private static func fold(_ value: String?) -> String {
        guard let value else { return "" }
        let folded = value
            .folding(options: [.diacriticInsensitive], locale: Locale(identifier: "en_US_POSIX"))
            .lowercased()
            .map { isDash($0) ? "-" : $0 }
        return String(folded).split(whereSeparator: { $0.isWhitespace }).joined(separator: " ")
    }

    private static func candidates(_ city: String) -> [String] {
        guard !city.isEmpty else { return [] }
        var heads = [city]
        if let dash = city.firstIndex(of: "-") {
            let head = city[..<dash].trimmingCharacters(in: .whitespaces)
            let part = city[city.index(after: dash)...].trimmingCharacters(in: .whitespaces)
            let ring = part.split(separator: " ").first.map { String($0) } ?? ""
            if !head.isEmpty, !okresRings.contains(where: { $0.city == head && $0.ring == ring }) {
                heads.append(head)
            }
        }
        return heads + heads.map { withoutDistrict($0) }
    }

    /// The last word dropped when it is a 1–2 digit or Roman-numeral district; a lone word keeps its own shape.
    private static func withoutDistrict(_ value: String) -> String {
        guard let space = value.lastIndex(of: " ") else { return value }
        let district = value[value.index(after: space)...]
        let base = value[..<space].trimmingCharacters(in: .whitespaces)
        let isNumber = (1 ... 2).contains(district.count) && district.allSatisfy(\.isNumber)
        let isRoman = (1 ... 6).contains(district.count) && district.allSatisfy { "ivx".contains($0) }
        return !base.isEmpty && (isNumber || isRoman) ? base : value
    }

    private static func isDash(_ character: Character) -> Bool {
        guard let scalar = character.unicodeScalars.first, character.unicodeScalars.count == 1 else { return false }
        return scalar == "-" || (0x2010 ... 0x2015).contains(scalar.value) || scalar.value == 0x2212
    }
}
