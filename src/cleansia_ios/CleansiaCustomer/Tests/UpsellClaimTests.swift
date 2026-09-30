import XCTest
@testable import CleansiaCustomer

/// What the home carousel and the profile's Plus card promise every non-member, held to what the
/// platform delivers. Read through the BUILT bundle, so a promise in any shipped language fails.
/// `plus-trial-claim.spec.ts` guards the web's surfaces and Android's `UpsellClaimTest` the same rows.
final class UpsellClaimTests: XCTestCase {
    private static let languages = ["en", "cs", "sk", "uk", "ru"]

    /// `MembershipPlan.TrialPeriodDays` is 0 on every plan and the admin validators refuse any other
    /// value, so no plan has a trial. These rows render to every non-member with no trial gate.
    private static let ungatedPlusKeys = [
        "home_upsell_plus_top",
        "home_upsell_plus_title",
        "home_upsell_plus_cta",
        "membership_inactive_badge",
        "membership_inactive_title",
        "membership_inactive_perks_summary",
        "membership_inactive_cta"
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

    func testTheScansWouldHaveCaughtTheRemovedCopy() {
        for removed in ["Save on every cleaning. 14 days free.", "Try Plus free", "Vyzkoušet zdarma na 14 dní"] {
            XCTAssertTrue(Self.matches(removed, Self.trialClaim), "the trial scan cannot see \(removed)")
        }
        for removed in ["Save on every booking · cancel anytime · recurring cleanings", "отмена в любое время"] {
            XCTAssertTrue(Self.matches(removed, Self.cancelAnytimeClaim), "the scan cannot see \(removed)")
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
