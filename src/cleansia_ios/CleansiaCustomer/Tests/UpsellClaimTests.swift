import XCTest
@testable import CleansiaCustomer

/// What the home carousel and the profile's Plus card promise every non-member, held to what the
/// platform delivers. Read through the BUILT bundle, so a promise in any shipped language fails.
/// `plus-trial-claim.spec.ts` guards the web's surfaces and Android's `UpsellClaimTest` the same rows.
final class UpsellClaimTests: XCTestCase {
    private static let languages = ["en", "cs", "sk", "uk", "ru"]

    /// These rows render to a non-member whether or not a trial is on offer, so none may promise one. The
    /// trial rows are separate keys, shown only while the customer can still get a trial.
    private static let ungatedPlusKeys = [
        "home_upsell_plus_top",
        "home_upsell_plus_title",
        "home_upsell_plus_cta",
        "membership_inactive_badge",
        "membership_inactive_title",
        "membership_inactive_perks_summary",
        "membership_inactive_cta"
    ]

    /// The trial length is set per plan in the admin console, so a trial row states the plan's days and no
    /// number of its own.
    private static let trialRows = [
        "home_upsell_plus_title_trial",
        "membership_inactive_cta_trial",
        "membership_hero_trial_price"
    ]

    /// A trialing member has every Plus benefit from day one, so no row may say one waits for a payment.
    private static let waitsForPaymentClaim = [
        "en": "first paid|paid membership",
        "cs": "prvním placen|prvního placen|placené členství|placeného členství",
        "sk": "prvým platen|prvého platen|platené členstvo|plateného členstva",
        "uk": "першого оплачен|оплачене членство|платну підписку",
        "ru": "первого оплаченн|оплаченное членство|платную подписку"
    ]

    /// The web spec's stems. A bare "free" is absent: free cancellation is a real, ungated perk.
    private static let trialClaim =
        #"trial|zkušeb|skúšob|пробн|\btry\b|vyzkouš|zkuste|vyskúš|skúste|спробу|попроб|\d+\s*(days?|dn|дн)"#

    /// Bookings carry cancellation fees, so "cancel anytime" in a perks line reads as a false perk.
    private static let cancelAnytimeClaim =
        #"cancel any\s?time|kdykoli|kedykoľvek|будь-коли|в любое время|в любой момент"#

    /// `ReferralPolicy`: both sides are paid once the friend's first order is completed.
    private static let completedStem = [
        "en": "completed",
        "cs": "dokončen",
        "sk": "dokončen",
        "uk": "завершен",
        "ru": "завершённ"
    ]

    /// Every line that tells the customer the referral points, so every line that says when they arrive.
    private static let referralRewardKeys = [
        "home_upsell_referral_title",
        "booking_referral_code_dialog_helper",
        "booking_referral_code_dialog_success",
        "booking_referral_code_dialog_success_named",
        "loyalty_referral_subtitle",
        "loyalty_referral_share_text"
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

    func testNoUngatedPlusRowPromisesAFreeTrial() throws {
        try forEachLanguage { language in
            for key in Self.ungatedPlusKeys {
                let value = L10n.localized(key)
                XCTAssertNotEqual(value, key, "\(key) is unlocalized in \(language)")
                XCTAssertFalse(Self.matches(value, Self.trialClaim), "\(key) promises a trial in \(language): \(value)")
            }
        }
    }

    func testEveryTrialRowStatesThePlansDaysRatherThanANumberOfItsOwn() throws {
        try forEachLanguage { language in
            for key in Self.trialRows {
                let value = L10n.localized(key)
                XCTAssertNotEqual(value, key, "\(key) is unlocalized in \(language)")
                XCTAssertTrue(Self.matches(value, #"%\d+\$d"#), "\(key) drops the plan's days in \(language)")
                let residue = value.replacingOccurrences(of: #"%\d+\$[@d]"#, with: "", options: .regularExpression)
                XCTAssertNil(residue.rangeOfCharacter(from: .decimalDigits), "\(key) names a number in \(language)")
            }
        }
    }

    func testNoRowTellsATrialingMemberABenefitWaitsForTheFirstPayment() throws {
        for language in Self.languages {
            let claim = try XCTUnwrap(Self.waitsForPaymentClaim[language])
            let offenders = try catalog(language).filter { Self.matches($0.value, claim) }.keys.sorted()
            XCTAssertEqual(offenders, [], "\(language) holds a benefit back until a payment")
        }
    }

    func testTheNonMemberPerksLineDoesNotPromiseCancellingAnytime() throws {
        try forEachLanguage { language in
            let value = L10n.localized("membership_inactive_perks_summary")
            XCTAssertFalse(Self.matches(value, Self.cancelAnytimeClaim), "\(language): \(value)")
        }
    }

    func testEveryReferralRewardWaitsForTheFriendsFirstCompletedCleaning() throws {
        let points = try Self.referralPointsPerSide()
        try forEachLanguage { language in
            let stem = try XCTUnwrap(Self.completedStem[language])
            for key in Self.referralRewardKeys {
                let value = L10n.localized(key)
                XCTAssertTrue(
                    value.contains(String(points)),
                    "\(key) does not state \(points) points in \(language): \(value)"
                )
                XCTAssertNotNil(
                    value.range(of: stem, options: .caseInsensitive),
                    "\(key) does not wait for a completed cleaning in \(language): \(value)"
                )
            }
        }
    }

    /// The newer carousel slides; every one renders, so every one is in every language.
    private static let carouselKeys = [
        "home_upsell_notifications_top",
        "home_upsell_notifications_title",
        "home_upsell_notifications_cta",
        "home_upsell_credit_title",
        "home_upsell_express_top",
        "home_upsell_book_cta",
        "home_quick_size_title",
        "home_quick_size_cta",
        "home_quick_size_rooms_less",
        "home_quick_size_rooms_more",
        "home_quick_size_baths_less",
        "home_quick_size_baths_more"
    ]

    func testEveryNewCarouselSlideIsWrittenInEveryLanguageAndPromisesNoTrial() throws {
        try forEachLanguage { language in
            for key in Self.carouselKeys {
                let value = L10n.localized(key)
                XCTAssertNotEqual(value, key, "\(key) is unlocalized in \(language)")
                XCTAssertFalse(Self.matches(value, Self.trialClaim), "\(key) promises a trial in \(language): \(value)")
            }
            XCTAssertFalse(Self.matches(L10n.Home.upsellExpressTitle(2), Self.trialClaim), language)
        }
    }

    /// The credit balance and its share are the server's (`GetMyCredit`), the waivers left are the
    /// membership's, and the express window is the booking policy's — so no language states a number.
    func testTheCreditAndExpressSlidesStateTheServersFiguresNeverTheirOwn() throws {
        try forEachLanguage { language in
            let credit = L10n.Home.upsellCreditTitle("§", share: 0.55)
            XCTAssertTrue(credit.contains("§") && credit.contains("55"), "\(language): \(credit)")
            XCTAssertNil(Self.digitsBeyond(credit, "55"), "the credit slide names a number in \(language)")

            let window = L10n.Home.upsellExpressTop(7, 9)
            XCTAssertTrue(window.contains("7") && window.contains("9"), "\(language): \(window)")
            XCTAssertNil(Self.digitsBeyond(window, "7", "9"), "the express window names a number in \(language)")

            for remaining in [1, 3, 6] {
                let title = L10n.Home.upsellExpressTitle(remaining)
                XCTAssertTrue(title.contains(String(remaining)), "\(language) drops the count: \(title)")
                XCTAssertNil(Self.digitsBeyond(title, String(remaining)), "\(language) names a number: \(title)")
            }
        }
    }

    /// The quick-size slide showed "2 bath": English `other` had been left on the singular.
    func testTheSizeSteppersCountRoomsAndBathsInTheEnglishPlural() throws {
        L10n.bundle = try localeBundle("en")
        XCTAssertEqual([L10n.Booking.roomsShort(1), L10n.Booking.roomsShort(2)], ["1 room", "2 rooms"])
        XCTAssertEqual([L10n.Booking.bathShort(1), L10n.Booking.bathShort(2)], ["1 bath", "2 baths"])
    }

    /// The express slide states the 2–4 h window from the client's booking bands, so those must be the
    /// server's.
    func testTheExpressWindowTheSlideStatesIsTheBookingPolicys() throws {
        XCTAssertEqual(Int(BookingPricing.expressLeadHours), try Self.bookingPolicyHours("ExpressLeadTimeHours"))
        XCTAssertEqual(Int(BookingPricing.standardLeadHours), try Self.bookingPolicyHours("StandardLeadTimeHours"))
    }

    private static func digitsBeyond(_ text: String, _ allowed: String...) -> Range<String.Index>? {
        var residue = text
        for figure in allowed {
            residue = residue.replacingOccurrences(of: figure, with: "")
        }
        return residue.rangeOfCharacter(from: .decimalDigits)
    }

    private static func bookingPolicyHours(_ name: String) throws -> Int {
        let policy = URL(fileURLWithPath: #filePath)
            .deletingLastPathComponent()
            .deletingLastPathComponent()
            .deletingLastPathComponent()
            .deletingLastPathComponent()
            .appendingPathComponent("Cleansia.Core.AppServices/Features/Orders/BookingPolicy.cs")
        let source = try String(contentsOf: policy, encoding: .utf8)
        let regex = try NSRegularExpression(pattern: #"public\s+const\s+int\s+"# + name + #"\s*=\s*(\d+)\s*;"#)
        let match = try XCTUnwrap(
            regex.firstMatch(in: source, range: NSRange(source.startIndex..., in: source)),
            "BookingPolicy.\(name) not found — the parser needs updating"
        )
        let digits = try XCTUnwrap(Range(match.range(at: 1), in: source))
        return try XCTUnwrap(Int(source[digits]))
    }

    func testTheScansWouldHaveCaughtTheRemovedCopy() {
        for removed in ["Save on every cleaning. 14 days free.", "Try Plus free", "Vyzkoušet zdarma na 14 dní"] {
            XCTAssertTrue(Self.matches(removed, Self.trialClaim), "the trial scan cannot see \(removed)")
        }
        for removed in ["Save on every booking · cancel anytime · recurring cleanings", "отмена в любое время"] {
            XCTAssertTrue(Self.matches(removed, Self.cancelAnytimeClaim), "the scan cannot see \(removed)")
        }
        let removedWaits = [
            ("en", "Express bookings with the surcharge waived — from your first paid month"),
            ("cs", "Expresní termíny bez příplatku — od prvního placeného měsíce"),
            ("sk", "Až 3 každý kalendárny mesiac počas aktívneho plateného členstva"),
            ("uk", "Що входить у платну підписку"),
            ("ru", "Экспресс-заказы без доплаты — с первого оплаченного месяца")
        ]
        for (language, removed) in removedWaits {
            let claim = Self.waitsForPaymentClaim[language] ?? ""
            XCTAssertTrue(Self.matches(removed, claim), "the \(language) scan cannot see \(removed)")
        }
    }

    private static func matches(_ text: String, _ pattern: String) -> Bool {
        text.range(of: pattern, options: [.regularExpression, .caseInsensitive]) != nil
    }

    private static func referralPointsPerSide() throws -> Int {
        let policy = URL(fileURLWithPath: #filePath)
            .deletingLastPathComponent()
            .deletingLastPathComponent()
            .deletingLastPathComponent()
            .deletingLastPathComponent()
            .appendingPathComponent("Cleansia.Core.AppServices/Features/Orders/ReferralPolicy.cs")
        let source = try String(contentsOf: policy, encoding: .utf8)
        let regex = try NSRegularExpression(pattern: #"public\s+const\s+int\s+PointsPerSide\s*=\s*(\d+)\s*;"#)
        let match = try XCTUnwrap(
            regex.firstMatch(in: source, range: NSRange(source.startIndex..., in: source)),
            "ReferralPolicy.PointsPerSide not found — the parser needs updating"
        )
        let digits = try XCTUnwrap(Range(match.range(at: 1), in: source))
        return try XCTUnwrap(Int(source[digits]))
    }

    private func catalog(_ language: String) throws -> [String: String] {
        let bundle = try localeBundle(language)
        let path = try XCTUnwrap(
            bundle.path(forResource: "Localizable", ofType: "strings"),
            "no compiled Localizable.strings in \(language).lproj"
        )
        return try XCTUnwrap(NSDictionary(contentsOfFile: path) as? [String: String])
    }

    private func forEachLanguage(_ body: (String) throws -> Void) throws {
        for language in Self.languages {
            L10n.bundle = try localeBundle(language)
            try body(language)
        }
    }

    private func localeBundle(_ tag: String) throws -> Bundle {
        let hosts = [Bundle.main, Bundle(for: Self.self)]
        let path = hosts.lazy.compactMap { $0.path(forResource: tag, ofType: "lproj") }.first
        let resolved = try XCTUnwrap(path, "no \(tag).lproj in the built bundle")
        return try XCTUnwrap(Bundle(path: resolved), "\(tag).lproj at \(resolved) is not a bundle")
    }
}
