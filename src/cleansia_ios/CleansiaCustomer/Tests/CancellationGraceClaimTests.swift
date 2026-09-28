import XCTest
@testable import CleansiaCustomer

/// Owner ruling 2026-09-28, reversing 2026-09-24: after booking, cancelling is free for 15 minutes, for 60 on a
/// customer's first booking and for 60 for an entitled Plus member. The figures are read from `BookingPolicy.cs`,
/// but this suite runs only when the iOS tree changes, so an edit to those constants alone is not caught here;
/// `cancellation-grace-claim.spec.ts` holds the same line for web.
///
/// The catalog is read through the BUILT bundle, so an assertion here fails when the shipped app is wrong
/// rather than when a file on disk is.
final class CancellationGraceClaimTests: XCTestCase {
    private static let languages = ["en", "cs", "sk", "uk", "ru"]

    /// "15 minutes … — 60 minutes on your first booking or with Cleansia Plus": the side that names Plus owes
    /// the longer figures and names the first booking.
    private static let bothAroundDash = ["booking_cancel_grace_note", "help_faq_a1"]
    /// "60 minutes after every booking …, instead of 15 minutes": a Plus perk, so what it replaces is the
    /// standard figure, and the first booking's hour is not the member's benefit.
    private static let bothAroundInsteadOf = ["membership_perk_grace_desc"]
    /// The sheet's line is the customer's OWN grace: the server's figure and no number of its own.
    private static let sheetNote = "order_cancel_fee_grace_note"

    private static let minuteFigure = #"(\d+)\s*-?\s*(?:minutes?|minut|minút|хвилин|минут)"#
    private static let insteadOf = #"instead of|namiesto|místo|замість|вместо"#
    private static let cancelStems = [#"cancel"#, #"zruš"#, #"storn"#, #"скасув"#, #"отмен"#]
    /// A grace sentence need not name cancelling ("Within 15 minutes of booking you pay nothing").
    private static let graceStems = [
        #"of booking"#, #"after booking"#, #"pay nothing"#,
        #"od objedn"#, #"po objedn"#, #"neplatíte"#,
        #"після замовлення"#, #"после заказа"#, #"не платите"#
    ]
    /// The cleaner's wait at the door, whose figure only happens to equal the standard grace.
    private static let notGrace: Set<String> = ["help_faq_a2"]
    /// The oops tier sits beside the customer's own grace, which can be an hour.
    private static let momentClaims = [
        #"moments? ago"#, #"just (?:now|booked)"#, #"před malou chvíl"#, #"pred malou chvíľ"#,
        #"щойно"#, #"только что"#
    ]
    private static let firstBookingClaims = [
        #"first[-\s]time"#, #"first (?:booking|order)"#, #"new customer"#,
        #"prvn\S* (?:objedn|zákazn|úklid)"#, #"prv\S* (?:objedn|zákazn|upratov)"#, #"nov\S* zákazn"#,
        #"перш\S* (?:замовлен|прибиран)"#, #"нов\S* клієнт"#, #"перв\S* (?:заказ|уборк)"#, #"нов\S* клиент"#
    ]

    private var restoreBundle: Bundle?

    override func setUp() {
        super.setUp()
        restoreBundle = L10n.bundle
    }

    override func tearDown() {
        L10n.bundle = restoreBundle ?? .main
        super.tearDown()
    }

    func testThePolicyHasAStandardFigureAndTwoLongerOnes() throws {
        let policy = try Self.policyMinutes()
        XCTAssertGreaterThan(policy.standard, 0)
        XCTAssertGreaterThan(policy.firstBooking, policy.standard)
        XCTAssertGreaterThan(policy.plus, policy.standard)
    }

    func testEveryGraceSentenceStatesEachFigureOnItsOwnSide() throws {
        let policy = try Self.policyMinutes()
        let longer = Self.distinct([policy.firstBooking, policy.plus])
        try forEachLanguage { language in
            for key in Self.bothAroundDash {
                let parts = L10n.localized(key).components(separatedBy: "—")
                let longerSide = parts.filter { $0.contains("Cleansia Plus") }.joined(separator: " ")
                let standardSide = parts.filter { !$0.contains("Cleansia Plus") }.joined(separator: " ")
                XCTAssertEqual(Self.distinct(Self.minutes(in: longerSide)), longer, "\(key) longer side in \(language)")
                XCTAssertEqual(Self.minutes(in: standardSide), [policy.standard], "\(key) standard side in \(language)")
                XCTAssertTrue(
                    Self.firstBookingClaims.contains { Self.matches($0, longerSide) },
                    "\(key) no longer names the first booking beside Plus in \(language)"
                )
            }
            for key in Self.bothAroundInsteadOf {
                let value = L10n.localized(key)
                let split = try XCTUnwrap(
                    value.range(of: Self.insteadOf, options: [.regularExpression, .caseInsensitive]),
                    "\(key) no longer says what it replaces in \(language)"
                )
                let plusSide = String(value[..<split.lowerBound])
                let standardSide = String(value[split.lowerBound...])
                XCTAssertEqual(Self.minutes(in: plusSide), [policy.plus], "\(key) Plus side in \(language)")
                XCTAssertEqual(Self.minutes(in: standardSide), [policy.standard], "\(key) standard side in \(language)")
            }
        }
    }

    func testTheSheetStatesTheServersFigureAndNoneOfItsOwn() throws {
        try forEachLanguage { language in
            let value = L10n.localized(Self.sheetNote)
            XCTAssertTrue(value.contains("%d"), "\(Self.sheetNote) has no slot for the server's figure in \(language)")
            let baked = value.replacingOccurrences(of: "%d", with: "")
            XCTAssertNil(baked.rangeOfCharacter(from: .decimalDigits), "the sheet bakes a number in \(language)")
        }
    }

    /// Every sentence that pairs a minute figure with cancelling or with the time after booking, anywhere
    /// in the catalog. Only the rule sentences name the first booking: a Plus perk speaks of every booking,
    /// and the sheet states the server's figure for this one.
    func testNoLocaleStatesAnotherGraceOrMisplacesTheFirstBooking() throws {
        let policy = try Self.policyMinutes()
        let figures: Set<Int> = [policy.standard, policy.firstBooking, policy.plus]
        let stems = Self.cancelStems + Self.graceStems
        for language in Self.languages {
            let catalog = try Self.catalog(language)
            let graceSentences = catalog.filter { key, value in
                !Self.notGrace.contains(key) && !Self.minutes(in: value).isEmpty
                    && stems.contains { Self.matches($0, value) }
            }
            for key in ["membership_perk_grace_desc", "booking_cancel_grace_note"] {
                XCTAssertNotNil(
                    graceSentences[key],
                    "the scan no longer sees \(key) in \(language), so it proves nothing"
                )
            }
            for (key, value) in graceSentences {
                let strays = Self.minutes(in: value).filter { !figures.contains($0) }
                XCTAssertEqual(strays, [], "\(key) states another grace in \(language): \(value)")
            }
            for key in Self.bothAroundDash + Self.bothAroundInsteadOf + [Self.sheetNote] {
                let value = catalog[key] ?? ""
                XCTAssertFalse(value.isEmpty, "\(key) is missing in \(language)")
                let namesFirstBooking = Self.firstBookingClaims.contains { Self.matches($0, value) }
                XCTAssertEqual(
                    namesFirstBooking,
                    Self.bothAroundDash.contains(key),
                    "\(key) misplaces the first booking in \(language): \(value)"
                )
            }
        }
    }

    func testTheGraceTierClaimsNoMomentThePlusGraceOutlasts() throws {
        for language in Self.languages {
            let value = try XCTUnwrap(Self.catalog(language)["order_cancel_fee_oops"], "no oops tier in \(language)")
            for claim in Self.momentClaims {
                XCTAssertFalse(Self.matches(claim, value), "the oops tier says just now in \(language): \(value)")
            }
        }
    }

    /// A resolver test does not cover the call site.
    func testEverySurfaceRendersTheGrace() throws {
        let card = try read("CleansiaCustomer/Sources/Features/Orders/CancellationFeeCard.swift")
        XCTAssertTrue(card.contains("if let minutes = callout.graceMinutes"), "the sheet ignores the preview's grace")
        XCTAssertTrue(card.contains("L10n.OrderCancel.feeGraceNote(minutes)"), "the sheet drops the grace")
        let policy = try read("CleansiaCustomer/Sources/Features/Booking/Confirm/CancellationPolicyCard.swift")
        XCTAssertTrue(policy.contains("L10n.Booking.cancelGraceNote"), "the booking policy card drops the grace")
        let plus = try read("CleansiaCustomer/Sources/Features/Membership/SubscribePlusScreen.swift")
        XCTAssertTrue(plus.contains("L10n.Membership.perkGraceDesc"), "the Plus perks drop the grace")
        let help = try read("CleansiaCustomer/Sources/Features/Profile/HelpSupportView.swift")
        XCTAssertTrue(help.contains("L10n.Help.faqA1"), "the cancellation FAQ answer is no longer shown")
    }

    // MARK: - Reading

    private static func policyMinutes() throws -> (standard: Int, firstBooking: Int, plus: Int) {
        let policy = repoRoot().appendingPathComponent("src/Cleansia.Core.AppServices/Features/Orders/BookingPolicy.cs")
        let source = try String(contentsOf: policy, encoding: .utf8)
        return try (
            constant("OopsWindowMinutesStandard", in: source),
            constant("OopsWindowMinutesFirstBooking", in: source),
            constant("OopsWindowMinutesPlus", in: source)
        )
    }

    private static func constant(_ name: String, in source: String) throws -> Int {
        let pattern = #"public\s+const\s+int\s+"# + name + #"\s*=\s*(\d+)\s*;"#
        let regex = try NSRegularExpression(pattern: pattern)
        let match = try XCTUnwrap(
            regex.firstMatch(in: source, range: NSRange(source.startIndex..., in: source)),
            "BookingPolicy.\(name) not found — the parser needs updating"
        )
        let digits = try XCTUnwrap(Range(match.range(at: 1), in: source))
        return try XCTUnwrap(Int(source[digits]))
    }

    private static func minutes(in text: String) -> [Int] {
        guard let regex = try? NSRegularExpression(pattern: minuteFigure, options: .caseInsensitive) else { return [] }
        return regex.matches(in: text, range: NSRange(text.startIndex..., in: text)).compactMap { match in
            Range(match.range(at: 1), in: text).flatMap { Int(text[$0]) }
        }
    }

    private static func distinct(_ figures: [Int]) -> [Int] {
        Array(Set(figures)).sorted()
    }

    private static func matches(_ pattern: String, _ text: String) -> Bool {
        text.range(of: pattern, options: [.regularExpression, .caseInsensitive]) != nil
    }

    private static func catalog(_ language: String) throws -> [String: String] {
        let bundle = try localeBundle(language)
        let path = try XCTUnwrap(
            bundle.path(forResource: "Localizable", ofType: "strings"),
            "no compiled Localizable.strings in \(language).lproj"
        )
        return try XCTUnwrap(NSDictionary(contentsOfFile: path) as? [String: String])
    }

    private func forEachLanguage(_ body: (String) throws -> Void) throws {
        for language in Self.languages {
            L10n.bundle = try Self.localeBundle(language)
            try body(language)
        }
    }

    private static func localeBundle(_ tag: String) throws -> Bundle {
        let hosts = [Bundle.main, Bundle(for: Self.self)]
        let path = hosts.lazy.compactMap { $0.path(forResource: tag, ofType: "lproj") }.first
        let resolved = try XCTUnwrap(path, "no \(tag).lproj in the built bundle")
        return try XCTUnwrap(Bundle(path: resolved), "\(tag).lproj at \(resolved) is not a bundle")
    }

    private static func repoRoot() -> URL {
        URL(fileURLWithPath: #filePath)
            .deletingLastPathComponent()
            .deletingLastPathComponent()
            .deletingLastPathComponent()
            .deletingLastPathComponent()
            .deletingLastPathComponent()
    }

    private func read(_ relativePath: String) throws -> String {
        let root = URL(fileURLWithPath: #filePath)
            .deletingLastPathComponent()
            .deletingLastPathComponent()
            .deletingLastPathComponent()
        return try String(contentsOf: root.appendingPathComponent(relativePath), encoding: .utf8)
    }
}
