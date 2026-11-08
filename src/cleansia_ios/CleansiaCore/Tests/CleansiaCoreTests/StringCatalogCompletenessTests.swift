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
        Exception(
            "Customer",
            "booking_dirtiness_heavy_rate",
            ["uk", "ru"],
            "a rate only; uk/ru write it unspaced, as Android does"
        ),
        Exception(
            "Customer",
            "booking_dirtiness_increased_rate",
            ["uk", "ru"],
            "a rate only; uk/ru write it unspaced, as Android does"
        ),
        Exception("Customer", "auth_email_placeholder", allLocalized, "an example address, not a word"),
        Exception("Customer", "devices_platform_ios", allLocalized, "Apple product name"),
        Exception("Customer", "dispute_create_char_count", allLocalized, "format specifiers and a literal count only"),
        Exception("Customer", "home_upsell_chip_credit", allLocalized, "a plus sign and a specifier only"),
        Exception("Customer", "home_upsell_chip_hours", ["cs", "sk"], "\"h\" is the same abbreviation in cs/sk"),
        Exception("Customer", "home_upsell_chip_minutes", ["cs", "sk"], "\"min\" is the same abbreviation in cs/sk"),
        Exception(
            "Customer",
            "home_upsell_chip_percent_off",
            ["uk", "ru"],
            "a figure only; uk/ru write it unspaced, as Android does"
        ),
        Exception("Customer", "home_upsell_chip_times", allLocalized, "a specifier and the multiplication sign only"),
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
        Exception("Customer", "order_market_label", allLocalized, "two specifiers and a middle dot only"),
        Exception("Customer", "orders_filter_count", allLocalized, "format specifiers only"),
        Exception("Customer", "payments_card_label", allLocalized, "the brand and the last four digits behind a mask"),
        Exception("Customer", "profile_tier_plus", allLocalized, "Cleansia Plus is the product name"),
        Exception("Partner", "action_sms", allLocalized, "SMS is the same initialism everywhere"),
        Exception("Partner", "auth_email_placeholder", allLocalized, "an example address, not a word"),
        Exception("Partner", "bonus", ["cs", "sk"], "\"Bonus\" is the Czech and Slovak word"),
        Exception("Partner", "cash_limit_at", ["cs", "sk"], "\"limit\" is the Czech and Slovak word"),
        Exception("Partner", "devices_platform_android", allLocalized, "platform name"),
        Exception("Partner", "devices_platform_ios", allLocalized, "platform name"),
        Exception("Partner", "devices_platform_web", allLocalized, "platform name"),
        Exception("Partner", "earnings_cash_held_limit", ["cs", "sk"], "\"Limit\" is the Czech and Slovak word"),
        Exception("Partner", "job_radius_value", ["cs", "sk"], "a specifier and the \"km\" symbol, Latin in cs/sk"),
        Exception("Partner", "orders_duration_h", ["cs", "sk"], "\"h\" is the same abbreviation in cs/sk"),
        Exception(
            "Partner",
            "orders_duration_h_min",
            ["cs", "sk"],
            "\"h\"/\"min\" are the same abbreviations in cs/sk"
        ),
        Exception("Partner", "orders_duration_min", ["cs", "sk"], "\"min\" is the same abbreviation in cs/sk"),
        Exception("Partner", "profile_iban", allLocalized, "IBAN is an international standard")
    ]

    /// Every locale other than the English source — for entries that genuinely are locale-invariant.
    private static let allLocalized = ["cs", "sk", "uk", "ru"]

    private static let partnerFacingCatalogs = ["Core", "Partner"]

    /// A cleaner is a partner paid a reward, never an employee paid a wage. Values only: keys are wire names.
    private static let employmentVocabulary = [
        "employee", "wage", "payroll", "zaměstn", "zamestn", "mzd", "сотрудник", "работник", "працівник",
        "співробітник", "зарплат", "заработн", "заробітн"
    ]

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

    /// The cause of those junk keys, caught in the source before a build can write one: a string literal handed to a
    /// SwiftUI view or modifier is a localization key, so the next IDE build adds it to the catalog untranslated. The
    /// apps localize through `L10n`, so shipped code hands these a `String` or a `Text(verbatim:)`.
    func testNoStringLiteralReachesSwiftUIAsAKey() throws {
        let root = try iosSourceRoot()
        let call = try NSRegularExpression(pattern: Self.localizedKeyCall)
        var literals: [String] = []
        for folder in Self.sourceFolders {
            let files = try swiftFiles(under: root.appendingPathComponent(folder))
            XCTAssertFalse(files.isEmpty, "\(folder): no Swift sources found")
            for file in files {
                let source = try String(contentsOf: file, encoding: .utf8)
                for match in call.matches(in: source, range: NSRange(source.startIndex..., in: source)) {
                    guard let opening = Range(match.range, in: source) else { continue }
                    let before = source[..<opening.lowerBound]
                    let lineStart = before.lastIndex(of: "\n").map { source.index(after: $0) } ?? source.startIndex
                    guard !source[lineStart ..< opening.lowerBound].contains("//"),
                          let closing = Self.literalEnd(in: source, from: opening.upperBound),
                          source[closing...].first(where: { !$0.isWhitespace }).map({ $0 == ")" || $0 == "," }) == true
                    else { continue }
                    let line = before.filter { $0 == "\n" }.count + 1
                    let literal = source[opening.lowerBound ..< closing]
                    literals.append("\(folder)/\(file.lastPathComponent):\(line) · \(literal)")
                }
            }
        }
        assertNoViolations(literals, "string literals SwiftUI takes as catalog keys (use Text(verbatim:) or a String)")
    }

    /// The other half of keeping an IDE build off the catalogs: it marks a key that has no `extractionState` stale,
    /// and writes the whole file back in Xcode's own layout to do it. A key the apps read through `L10n` is `manual`.
    func testEveryKeyCarriesAnExtractionState() throws {
        let root = try iosSourceRoot()
        var unmarked: [String] = []
        for catalog in Self.catalogs {
            let data = try Data(contentsOf: root.appendingPathComponent(catalog.path))
            let json = try JSONSerialization.jsonObject(with: data) as? [String: Any]
            let strings = try XCTUnwrap(json?["strings"] as? [String: Any], "\(catalog.name): no strings table")
            for (key, entry) in strings where (entry as? [String: Any])?["extractionState"] == nil {
                unmarked.append("\(catalog.name) · \(key)")
            }
        }
        assertNoViolations(unmarked, "keys with no extractionState (mark them \"manual\")")
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

    func testPartnerFacingCopyNeverSpeaksOfEmployment() throws {
        var employment: [String] = []
        for (catalog, entries) in try loadAll() where Self.partnerFacingCatalogs.contains(catalog) {
            for (key, byLocale) in entries {
                for (locale, value) in byLocale {
                    let copy = value.texts.joined(separator: "\n").lowercased()
                    for word in Self.employmentVocabulary where copy.contains(word) {
                        employment.append("\(catalog) · \(key) · \(locale) · \"\(word)\"")
                    }
                }
            }
        }
        assertNoViolations(employment, "partner-facing values that speak of employment")
    }

    /// Owner ruling 2026-10-04: Slovak says "balík" for a package, as the web does — never the diminutive
    /// "balíček" (that is the Czech word, and Czech keeps it). Every inflected form shares the "balíč" stem.
    func testSlovakCallsAPackageBalikNeverBalicek() throws {
        var diminutive: [String] = []
        for (catalog, entries) in try loadAll() {
            for (key, byLocale) in entries {
                let copy = byLocale["sk"]?.texts.joined(separator: "\n").lowercased() ?? ""
                if copy.contains("balíč") {
                    diminutive.append("\(catalog) · \(key) · \(copy)")
                }
            }
        }
        assertNoViolations(diminutive, "Slovak values that say \"balíček\" instead of \"balík\"")
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

    /// Every folder whose code builds against one of the catalogs; their `Tests` folders and projects are skipped.
    private static let sourceFolders = ["CleansiaCore/Sources", "CleansiaCustomer", "CleansiaPartner"]

    /// A SwiftUI view or modifier that takes a `LocalizedStringKey`, up to the opening quote of a literal first
    /// argument. The literal counts only when it is the whole argument: `Text(" " + name)` is a `String`.
    private static let localizedKeyCall = #"(?<![\w.])(?:Text|TextField|SecureField|DatePicker|Toggle|Button|Label|"#
        + #"Link|Picker|Section|Menu|Stepper|LabeledContent|ProgressView)\(\s*"|\.(?:accessibilityLabel|"#
        + #"accessibilityHint|accessibilityValue|navigationTitle|help)\(\s*""#

    private func swiftFiles(under folder: URL) throws -> [URL] {
        let keys: [URLResourceKey] = [.isDirectoryKey]
        guard let walk = FileManager.default.enumerator(at: folder, includingPropertiesForKeys: keys) else { return [] }
        var files: [URL] = []
        for case let url as URL in walk {
            if try url.resourceValues(forKeys: Set(keys)).isDirectory == true {
                if url.lastPathComponent == "Tests" || url.pathExtension == "xcodeproj" { walk.skipDescendants() }
            } else if url.pathExtension == "swift" {
                files.append(url)
            }
        }
        return files
    }

    /// Just past the closing quote of the literal whose text starts at `start`, stepping over escapes and over each
    /// interpolation, whose own literals hold quotes of their own.
    private static func literalEnd(in source: String, from start: String.Index) -> String.Index? {
        var index = start
        while index < source.endIndex {
            let character = source[index]
            index = source.index(after: index)
            if character == "\"" { return index }
            guard character == "\\", index < source.endIndex else { continue }
            let escaped = source[index]
            index = source.index(after: index)
            guard escaped == "(" else { continue }
            var depth = 1
            while depth > 0, index < source.endIndex {
                let inner = source[index]
                index = source.index(after: index)
                if inner == "(" {
                    depth += 1
                } else if inner == ")" {
                    depth -= 1
                } else if inner == "\"" {
                    guard let end = literalEnd(in: source, from: index) else { return nil }
                    index = end
                }
            }
        }
        return nil
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

    var texts: [String] {
        [plain].compactMap { $0 } + Array(plural.values)
    }

    var isComplete: Bool {
        !isEmpty && texts.allSatisfy { !$0.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty }
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
