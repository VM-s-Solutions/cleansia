import XCTest

/// The net under every shipped string. It reads the three `.xcstrings` catalogs straight off disk — Core,
/// Customer, Partner — and fails naming the exact catalog + key + locale, so an untranslated string is a
/// red build instead of something a user finds one screen at a time.
///
/// It also guards the shape of the catalogs, because the way they break is not "someone forgot a locale":
/// Xcode's string extractor cannot see keys that are looked up through a variable (`localized(key)`), so it
/// marks the whole catalog stale and offers to delete it, while auto-extracting the literals it CAN see —
/// `-%@`, `#%@`, `%@%@%%`. Those junk keys are the fingerprint of that deletion, so their presence fails
/// here too.
final class StringCatalogCompletenessTests: XCTestCase {
    private static let locales = ["en", "cs", "sk", "uk", "ru"]

    private static let catalogs = [
        Catalog(name: "Core", path: "CleansiaCore/Sources/CleansiaCore/Resources/Localizable.xcstrings"),
        Catalog(name: "Customer", path: "CleansiaCustomer/Resources/Localizable.xcstrings"),
        Catalog(name: "Partner", path: "CleansiaPartner/Resources/Localizable.xcstrings")
    ]

    /// Keys whose non-English value is IDENTICAL to English on purpose, **per locale**. Everything else
    /// that matches English is an untranslated string. Entries are asserted, not skipped: one whose key is
    /// gone, or whose named locale no longer echoes English, is a stale exception and fails
    /// `testTheAllowListCarriesNoStaleExceptions`.
    ///
    /// The locale list is the load-bearing half, and it used to be missing. Four of these reasons name
    /// **cs/sk specifically** — "min", "h"/"m", "Bonus" — and are simply false for uk/ru, which are
    /// Cyrillic. A whole-key exception suppressed every locale, so leaving `bonus` as the Latin "Bonus"
    /// in Ukrainian would have been swallowed by the very entry that says it is a Czech word, and the
    /// stale check would still have passed because cs and sk kept echoing. Naming the locales is what
    /// makes the reason and the enforcement the same statement.
    private static let sameAsEnglishByDesign: [Exception] = [
        Exception("Customer", "devices_platform_ios", allLocalized, "Apple product name"),
        Exception("Customer", "dispute_create_char_count", allLocalized, "format specifiers and a literal count only"),
        Exception("Customer", "home_upsell_plus_top", allLocalized, "Cleansia Plus is the product name"),
        Exception("Customer", "membership_inactive_badge", allLocalized, "Cleansia Plus is the product name"),
        Exception("Customer", "membership_inactive_title", allLocalized, "Cleansia Plus is the product name"),
        Exception("Customer", "membership_plus_title", allLocalized, "Cleansia Plus is the product name"),
        Exception(
            "Customer",
            "order_detail_duration_minutes",
            ["cs", "sk"],
            "\"min\" is the same abbreviation in cs/sk"
        ),
        Exception("Customer", "orders_filter_count", allLocalized, "format specifiers only"),
        Exception("Customer", "profile_tier_plus", allLocalized, "Cleansia Plus is the product name"),
        Exception("Partner", "action_sms", allLocalized, "SMS is the same initialism everywhere"),
        Exception("Partner", "bonus", ["cs", "sk"], "\"Bonus\" is the Czech and Slovak word"),
        Exception("Partner", "devices_platform_android", allLocalized, "platform name"),
        Exception("Partner", "devices_platform_ios", allLocalized, "platform name"),
        Exception("Partner", "devices_platform_web", allLocalized, "platform name"),
        Exception("Partner", "duration_hours_minutes", ["cs", "sk"], "\"h\"/\"m\" are the same abbreviations in cs/sk"),
        Exception("Partner", "duration_minutes_only", ["cs", "sk"], "\"m\" is the same abbreviation in cs/sk"),
        Exception("Partner", "job_radius_value", ["cs", "sk"], "a specifier and the \"km\" symbol, Latin in cs/sk"),
        Exception("Partner", "profile_iban", allLocalized, "IBAN is an international standard")
    ]

    /// Every locale other than the English source — for entries that genuinely are locale-invariant.
    private static let allLocalized = ["cs", "sk", "uk", "ru"]

    func testEveryKeyCarriesAValueInAllFiveLocales() throws {
        var gaps: [String] = []
        for (catalog, entries) in try loadAll() {
            for (key, byLocale) in entries {
                let missing = Self.locales.filter { !(byLocale[$0]?.isComplete ?? false) }
                if !missing.isEmpty {
                    gaps.append("\(catalog) · \(key) · missing \(missing.joined(separator: ", "))")
                }
            }
        }
        assertNoViolations(gaps, "untranslated string catalog entries")
    }

    func testNoAutoExtractedJunkKeys() throws {
        var junk: [String] = []
        for (catalog, entries) in try loadAll() {
            for key in entries.keys where isJunk(key) {
                junk.append("\(catalog) · \(key.debugDescription)")
            }
        }
        assertNoViolations(
            junk,
            "auto-extracted junk keys (delete them; they are the fingerprint of Xcode dropping real keys)"
        )
    }

    func testOnlyAllowListedKeysMayReadIdenticalToEnglish() throws {
        let allowed = Set(Self.sameAsEnglishByDesign.flatMap(\.localeIdentities))
        var untranslated: [String] = []
        for (catalog, entries) in try loadAll() {
            for (key, byLocale) in entries {
                let echoed = Self.locales
                    .dropFirst()
                    .filter { byLocale[$0]?.isEmpty == false && byLocale[$0] == byLocale["en"] }
                    .filter { !allowed.contains("\(catalog)/\(key)/\($0)") }
                if !echoed.isEmpty {
                    untranslated.append("\(catalog) · \(key) · still English in \(echoed.joined(separator: ", "))")
                }
            }
        }
        assertNoViolations(untranslated, "values that never got translated off the English source")
    }

    func testTheAllowListCarriesNoStaleExceptions() throws {
        let all = try loadAll()
        var stale: [String] = []
        for exception in Self.sameAsEnglishByDesign {
            guard let byLocale = all[exception.catalog]?[exception.key] else {
                stale.append("\(exception.identity) · no such key — drop the exception")
                continue
            }
            // Per LOCALE, not per key: an exception that names four locales while only two still echo
            // is two-thirds a fiction, and the untranslated half of it is exactly what the whole-key
            // form used to hide.
            let noLongerEchoing = exception.locales.filter {
                byLocale[$0]?.isEmpty != false || byLocale[$0] != byLocale["en"]
            }
            if !noLongerEchoing.isEmpty {
                stale.append(
                    "\(exception.identity) · now translated in \(noLongerEchoing.joined(separator: ", "))"
                        + " — narrow or drop the exception (\(exception.why))"
                )
            }

            let unknown = exception.locales.filter { !Self.locales.contains($0) }
            if !unknown.isEmpty {
                stale.append("\(exception.identity) · names locales we do not ship: \(unknown.joined(separator: ", "))")
            }
        }
        assertNoViolations(stale, "exceptions that no longer describe the catalog")
    }

    // MARK: - Loading

    private func loadAll() throws -> [String: [String: [String: LocalizedValue]]] {
        let root = try iosSourceRoot()
        var loaded: [String: [String: [String: LocalizedValue]]] = [:]
        for catalog in Self.catalogs {
            let data = try Data(contentsOf: root.appendingPathComponent(catalog.path))
            let json = try JSONSerialization.jsonObject(with: data) as? [String: Any]
            let strings = try XCTUnwrap(json?["strings"] as? [String: Any], "\(catalog.name): no strings table")
            XCTAssertFalse(strings.isEmpty, "\(catalog.name): the catalog parsed to zero keys")
            loaded[catalog.name] = strings.mapValues(Self.localizations)
        }
        return loaded
    }

    /// Walks up from this source file until a directory holds ALL three catalogs — that directory is the
    /// iOS source root. A catalog that moved or was renamed therefore fails loudly instead of skipping.
    private func iosSourceRoot() throws -> URL {
        var directory = URL(fileURLWithPath: #filePath).deletingLastPathComponent()
        while directory.path != "/" {
            let resolvesAll = Self.catalogs.allSatisfy {
                FileManager.default.fileExists(atPath: directory.appendingPathComponent($0.path).path)
            }
            if resolvesAll {
                return directory
            }
            directory = directory.deletingLastPathComponent()
        }
        throw CatalogLookupError.rootNotFound(from: #filePath, expecting: Self.catalogs.map(\.path))
    }

    private static func localizations(_ entry: Any) -> [String: LocalizedValue] {
        guard let entry = entry as? [String: Any],
              let byLocale = entry["localizations"] as? [String: Any]
        else {
            return [:]
        }
        return byLocale.mapValues(LocalizedValue.init)
    }

    // MARK: - Rules

    /// Xcode auto-extracts SwiftUI string literals it happens to see, which yields keys made of nothing but
    /// format specifiers and punctuation. A key with no letter left once the specifiers are removed is one
    /// of those.
    private func isJunk(_ key: String) -> Bool {
        let specifierTerminators = Set("@dDiuUxXoOfeEgGcCsSpaAF%")
        var characters = Array(key)[...]
        var carriesAWord = false
        while let head = characters.first {
            characters = characters.dropFirst()
            guard head == "%" else {
                carriesAWord = carriesAWord || head.isLetter
                continue
            }
            characters = characters.drop(while: { !specifierTerminators.contains($0) }).dropFirst()
        }
        return !carriesAWord
    }

    private func assertNoViolations(
        _ violations: [String],
        _ what: String,
        file: StaticString = #filePath,
        line: UInt = #line
    ) {
        guard !violations.isEmpty else { return }
        let listing = violations.sorted().map { "  • \($0)" }.joined(separator: "\n")
        XCTFail("\(violations.count) \(what):\n\(listing)", file: file, line: line)
    }
}

// MARK: - Model

private struct Catalog {
    let name: String
    let path: String
}

private struct Exception {
    let catalog: String
    let key: String
    let locales: [String]
    let why: String

    var identity: String {
        "\(catalog)/\(key)"
    }

    /// One entry per locale this exception actually covers — the unit both rules are checked in.
    var localeIdentities: [String] {
        locales.map { "\(catalog)/\(key)/\($0)" }
    }

    init(_ catalog: String, _ key: String, _ locales: [String], _ why: String) {
        self.catalog = catalog
        self.key = key
        self.locales = locales
        self.why = why
    }
}

/// One locale's copy for one key: a plain string, or the plural cases when the key varies by count.
private struct LocalizedValue: Equatable {
    let plain: String?
    let plural: [String: String]

    var isEmpty: Bool {
        plain == nil && plural.isEmpty
    }

    var isComplete: Bool {
        !isEmpty && ([plain].compactMap { $0 } + Array(plural.values))
            .allSatisfy { !$0.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty }
    }

    init(_ localization: Any) {
        let localization = localization as? [String: Any] ?? [:]
        plain = (localization["stringUnit"] as? [String: Any])?["value"] as? String
        let cases = ((localization["variations"] as? [String: Any])?["plural"] as? [String: Any]) ?? [:]
        plural = cases.compactMapValues { ($0 as? [String: Any])?["stringUnit"] as? [String: Any] }
            .compactMapValues { $0["value"] as? String }
    }
}

private enum CatalogLookupError: Error, CustomStringConvertible {
    case rootNotFound(from: String, expecting: [String])

    var description: String {
        switch self {
        case let .rootNotFound(from, expecting):
            "no directory above \(from) holds all of \(expecting.joined(separator: ", ")) — a string catalog "
                + "was moved or renamed; point this test at its new home"
        }
    }
}
