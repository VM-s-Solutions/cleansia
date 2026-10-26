import Foundation

/// Whether an address is in one of the serviced cities, the question `CreateOrder` answers.
///
/// **A port of the server's `Cleansia.Core.Domain.ServiceAreas.CityNameMatch`, and it has to stay one.** The server
/// refuses the booking; this copy only tells the customer earlier. A copy that is STRICTER than the server tells a
/// paying customer we do not serve them when we do, so the rule is mirrored exactly and `CityNameMatchTests` pins
/// the case table the C#, Kotlin and TypeScript suites pin. Change a case in all four.
/// → /product/business-rules#serviced-city
///
/// It reads Unicode scalars, as the server reads its UTF-16 characters, never `Character`s: a grapheme cluster
/// would glue a mark to the space or dash before it and hide that space or dash from the rule.
public enum CityNameMatch {
    private static let okresRings: [(city: String, ring: String)] = [
        ("praha", "zapad"), ("praha", "vychod"), ("brno", "venkov"), ("plzen", "sever"), ("plzen", "jih")
    ]

    public static func isServiced(_ cities: [ServicedCity], city: String?, zipCode: String?) -> Bool {
        let postcode = postcode(zipCode)
        return cities.contains { serves($0, city: city, postcode: postcode) }
    }

    /// The name rule: a row matches the customer's city, the city less the part after its first dash, or either
    /// less a trailing district, `Praha 8`, `Pardubice II`, `Brno-střed`, `Praha 4 - Chodov`. The five okres rings
    /// around a city share the dashed shape and are not the city. The rule runs on the customer's string only, so a
    /// row named for one district never claims the whole city.
    static func matches(_ servicedCityName: String?, _ customerCity: String?) -> Bool {
        let row = fold(servicedCityName)
        let city = fold(customerCity)
        guard !row.isEmpty, !city.isEmpty else { return false }
        return candidates(city).contains(row)
    }

    /// A row with a ZIP prefix is decided by the address's postcode whenever it has a valid one, whatever the city
    /// says; with no valid postcode it falls back to the name, like a row without a prefix. The prefix is trimmed,
    /// never compacted, so a prefix with a space inside starts no postcode.
    private static func serves(_ row: ServicedCity, city: String?, postcode: String?) -> Bool {
        let prefix = trimmingWhitespace(row.zipPrefix ?? "")
        if !prefix.isEmpty, let postcode {
            return postcode.hasPrefix(prefix)
        }
        return matches(row.name, city)
    }

    /// Five ASCII digits once every whitespace character, a no-break space included, is taken out.
    private static func postcode(_ zipCode: String?) -> String? {
        var digits = (zipCode ?? "").unicodeScalars
        digits.removeAll { $0.properties.isWhitespace }
        guard digits.count == 5, digits.allSatisfy({ ("0" ... "9").contains($0) }) else { return nil }
        return String(digits)
    }

    /// Trimmed, diacritics removed, every dash one `-`, lower case, every whitespace run one space, in the server's
    /// order: the trim comes before the marks go, so a space that only a removed mark leaves at an edge stays.
    private static func fold(_ value: String?) -> String {
        let unmarked = trimmingWhitespace(value ?? "").decomposedStringWithCanonicalMapping.unicodeScalars
            .filter { $0.properties.generalCategory != .nonspacingMark }
            .map { isDash($0) ? "-" : $0 }
        let lowered = String(String.UnicodeScalarView(unmarked)).precomposedStringWithCanonicalMapping.lowercased()
        var folded = String.UnicodeScalarView()
        for scalar in lowered.unicodeScalars {
            if !scalar.properties.isWhitespace {
                folded.append(scalar)
            } else if folded.last != " " {
                folded.append(" ")
            }
        }
        return String(folded)
    }

    private static func candidates(_ city: String) -> [String] {
        var forms = [city]
        if let head = head(of: city) {
            forms.append(head)
        }
        return forms + forms.compactMap { withoutDistrict($0) }
    }

    private static func head(of city: String) -> String? {
        let scalars = city.unicodeScalars
        guard let dash = scalars.firstIndex(of: "-") else { return nil }
        let head = trimmingWhitespace(String(scalars[..<dash]))
        let part = trimmingWhitespace(String(scalars[scalars.index(after: dash)...]))
        guard !head.isEmpty, !part.isEmpty else { return nil }
        let ring = String(part.unicodeScalars.prefix(while: { $0 != " " }))
        return okresRings.contains { $0.city == head && $0.ring == ring } ? nil : head
    }

    private static func withoutDistrict(_ form: String) -> String? {
        let scalars = form.unicodeScalars
        guard let space = scalars.lastIndex(of: " "), space > scalars.startIndex else { return nil }
        let district = scalars[scalars.index(after: space)...]
        let isNumber = (1 ... 2).contains(district.count)
            && district.allSatisfy { $0.properties.generalCategory == .decimalNumber }
        let isRoman = (1 ... 6).contains(district.count) && district.allSatisfy { "ivx".unicodeScalars.contains($0) }
        return isNumber || isRoman ? String(scalars[..<space]) : nil
    }

    private static func trimmingWhitespace(_ value: String) -> String {
        let scalars = value.unicodeScalars
        guard let first = scalars.firstIndex(where: { !$0.properties.isWhitespace }),
              let last = scalars.lastIndex(where: { !$0.properties.isWhitespace })
        else { return "" }
        return String(scalars[first ... last])
    }

    private static func isDash(_ scalar: Unicode.Scalar) -> Bool {
        (0x2010 ... 0x2015).contains(scalar.value) || scalar.value == 0x2212
    }
}
