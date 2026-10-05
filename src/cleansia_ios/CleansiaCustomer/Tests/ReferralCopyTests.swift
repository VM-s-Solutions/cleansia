import CleansiaCore
import Foundation
import XCTest
@testable import CleansiaCustomer

/// The referral reward is the chosen market's credit, formatted on the device in its currency: every line
/// that states it takes the figure as a slot, and the twin shown with no figure known promises nothing. The
/// catalog rows are read through the BUILT bundle, so a claim in any shipped language fails. Android's
/// `UpsellClaimTest` holds the same rows.
final class ReferralCopyTests: XCTestCase {
    private static let languages = ["en", "cs", "sk", "uk", "ru"]
    private static let credit = MarketMoney(amount: 175, currencyCode: "EUR")

    /// A row that states the reward, the slot the market's credit takes in it, and its no-figure twin.
    private struct RewardRow {
        let key: String
        let slot: String
        let twin: String
    }

    private static let rewardRows = [
        RewardRow(key: "home_upsell_referral_desc", slot: "%1$@", twin: "home_upsell_referral_desc_generic"),
        RewardRow(
            key: "booking_referral_code_dialog_helper",
            slot: "%1$@",
            twin: "booking_referral_code_dialog_helper_no_figure"
        ),
        RewardRow(
            key: "booking_referral_code_dialog_success",
            slot: "%1$@",
            twin: "booking_referral_code_dialog_success_no_figure"
        ),
        RewardRow(
            key: "booking_referral_code_dialog_success_named",
            slot: "%2$@",
            twin: "booking_referral_code_dialog_success_named_no_figure"
        ),
        RewardRow(key: "loyalty_referral_subtitle", slot: "%1$@", twin: "loyalty_referral_subtitle_no_figure")
    ]

    /// Both sides are credited once the friend's first order is completed.
    private static let completedStem = [
        "en": "completed",
        "cs": "dokončen",
        "sk": "dokončen",
        "uk": "завершен",
        "ru": "завершённ"
    ]

    private static let creditStem = ["en": "credit", "cs": "kredit", "sk": "kredit", "uk": "кредит", "ru": "кредит"]
    private static let pointsClaim = [
        "en": #"\bpoints?\b|\bpts\b"#,
        "cs": "bod",
        "sk": "bod",
        "uk": "бал",
        "ru": "балл"
    ]
    private static let currencyWord = "CZK|Kč|EUR|€|koru|euro|крон|євро|евро"

    /// Each side is paid the credit of the currency it books in, so the two figures can differ.
    private static let sharedFigureClaim = [
        "en": "each|both",
        "cs": "oba|obě|každý|každá",
        "sk": "obaja|obe|každý|každá",
        "uk": "обоє|обидва|обидві|обом|кожен|кожна|кожному",
        "ru": "оба|обе|каждый|каждая|каждому"
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

    // MARK: The renderers

    func testEveryLineStatesTheMarketsCreditInItsCurrency() {
        let amount = OrdersFormat.price(175, currencyCode: "EUR")
        XCTAssertEqual(ReferralCopy.amount(Self.credit), amount)
        let lines = [
            (ReferralCopy.dialogHelper(Self.credit), L10n.Booking.referralDialogHelper(amount)),
            (
                ReferralCopy.dialogSuccess(referrer: nil, credit: Self.credit),
                L10n.Booking.referralDialogSuccess(amount)
            ),
            (
                ReferralCopy.dialogSuccess(referrer: "Eva", credit: Self.credit),
                L10n.Booking.referralDialogSuccessNamed("Eva", amount)
            ),
            (ReferralCopy.rewardsSubtitle(Self.credit), L10n.Rewards.referralSubtitle(amount)),
            (ReferralCopy.upsellDescription(Self.credit), L10n.Home.upsellReferralDesc(amount)),
            (ReferralCopy.upsellChip(Self.credit) ?? "", L10n.Home.upsellChipCredit(amount))
        ]
        for (rendered, expected) in lines {
            XCTAssertEqual(rendered, expected)
            XCTAssertTrue(rendered.contains(amount), rendered)
        }
    }

    func testWithNoFigureKnownEveryLineFallsToTheTwinThatPromisesNothing() {
        XCTAssertEqual(ReferralCopy.dialogHelper(nil), L10n.Booking.referralDialogHelperNoFigure)
        XCTAssertEqual(
            ReferralCopy.dialogSuccess(referrer: nil, credit: nil),
            L10n.Booking.referralDialogSuccessNoFigure
        )
        XCTAssertEqual(
            ReferralCopy.dialogSuccess(referrer: "Eva", credit: nil),
            L10n.Booking.referralDialogSuccessNamedNoFigure("Eva")
        )
        XCTAssertEqual(ReferralCopy.rewardsSubtitle(nil), L10n.Rewards.referralSubtitleNoFigure)
        XCTAssertEqual(ReferralCopy.upsellDescription(nil), L10n.Home.upsellReferralDescGeneric)
        XCTAssertNil(ReferralCopy.upsellChip(nil))
    }

    func testABlankReferrerNameReadsAsNoName() {
        XCTAssertEqual(
            ReferralCopy.dialogSuccess(referrer: "  ", credit: Self.credit),
            L10n.Booking.referralDialogSuccess(ReferralCopy.amount(Self.credit))
        )
        XCTAssertEqual(
            ReferralCopy.dialogSuccess(referrer: "", credit: nil),
            L10n.Booking.referralDialogSuccessNoFigure
        )
    }

    func testTheShareTextCarriesTheCodeAndTheLandingLinkButNeverTheInvitersFigure() throws {
        let link = CleansiaWeb.referralLink(code: "ABC123")
        let message = ReferralCopy.shareMessage(code: "ABC123")
        XCTAssertEqual(message, L10n.Rewards.referralShareTextNoFigure("ABC123", link))
        XCTAssertTrue(message.contains("ABC123") && message.contains(link), message)

        for language in Self.languages {
            XCTAssertNil(
                try catalog(language)["loyalty_referral_share_text"],
                "\(language) still ships a share text that tells the friend the inviter's figure"
            )
        }
    }

    // MARK: The catalog

    func testEveryReferralRewardIsTheMarketsCreditAndWaitsForTheFriendsFirstCompletedCleaning() throws {
        try forEachLanguage { language in
            let completed = try XCTUnwrap(Self.completedStem[language])
            let credit = try XCTUnwrap(Self.creditStem[language])
            for row in Self.rewardRows {
                let value = L10n.localized(row.key)
                let site = "\(language)/\(row.key)"
                let bare = Self.withoutSlots(value)
                XCTAssertTrue(value.contains(row.slot), "\(site) does not take the credit as \(row.slot): \(value)")
                XCTAssertNil(bare.rangeOfCharacter(from: .decimalDigits), "\(site) names a figure: \(value)")
                XCTAssertFalse(Self.matches(bare, Self.currencyWord), "\(site) names a currency: \(value)")
                XCTAssertTrue(Self.matches(value, credit), "\(site) does not call the reward credit: \(value)")
                XCTAssertTrue(Self.matches(value, completed), "\(site) does not wait for the cleaning: \(value)")
            }
        }
    }

    func testEachLinePromisesTheReaderOnlyTheirOwnFigure() throws {
        try forEachLanguage { language in
            let shared = try XCTUnwrap(Self.sharedFigureClaim[language])
            for row in Self.rewardRows {
                let value = L10n.localized(row.key)
                XCTAssertFalse(
                    Self.matches(value, Self.wholeWord(shared)),
                    "\(language)/\(row.key) promises one figure to both sides: \(value)"
                )
            }
        }
    }

    func testWithNoFigureKnownNoLinePromisesAReward() throws {
        try forEachLanguage { language in
            let credit = try XCTUnwrap(Self.creditStem[language])
            let points = try XCTUnwrap(Self.pointsClaim[language])
            for row in Self.rewardRows {
                let value = L10n.localized(row.twin)
                let site = "\(language)/\(row.twin)"
                XCTAssertNotEqual(value, row.twin, "\(site) is unlocalized")
                XCTAssertNil(Self.withoutSlots(value).rangeOfCharacter(from: .decimalDigits), "\(site): \(value)")
                XCTAssertFalse(Self.matches(value, credit), "\(site) promises credit: \(value)")
                XCTAssertFalse(Self.matches(value, points), "\(site) promises points: \(value)")
            }
        }
    }

    func testNoReferralRowStillPromisesPointsOrStatesAFigureOfItsOwn() throws {
        for language in Self.languages {
            let points = try XCTUnwrap(Self.pointsClaim[language])
            let rows = try catalog(language).filter { $0.key.contains("referral") && $0.key != "loyalty_tx_referral" }
            XCTAssertFalse(rows.isEmpty, "\(language) has no referral rows to check")
            for (key, value) in rows {
                let site = "\(language)/\(key)"
                XCTAssertNil(Self.withoutSlots(value).rangeOfCharacter(from: .decimalDigits), "\(site): \(value)")
                XCTAssertFalse(Self.matches(value, points), "\(site) still promises points: \(value)")
            }
        }
    }

    /// A reversed referral took its points back as a negative row under the same source, which read "+-150".
    func testTheReferralLedgerLineTakesItsSignFromThePoints() throws {
        try forEachLanguage { language in
            let reversed = L10n.Rewards.txReferral(-150)
            XCTAssertTrue(reversed.contains("-150"), "\(language): \(reversed)")
            XCTAssertFalse(reversed.contains("+"), "\(language): \(reversed)")
            XCTAssertFalse(L10n.Rewards.txReferral(150).contains("+"), "\(language) signs the points itself")
        }
    }

    // MARK: Helpers

    private static func withoutSlots(_ text: String) -> String {
        text.replacingOccurrences(of: #"%\d+\$(ll)?[@d]"#, with: "", options: .regularExpression)
    }

    private static func wholeWord(_ words: String) -> String {
        #"(?<!\p{L})(?:"# + words + #")(?!\p{L})"#
    }

    private static func matches(_ text: String, _ pattern: String) -> Bool {
        text.range(of: pattern, options: [.regularExpression, .caseInsensitive]) != nil
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
