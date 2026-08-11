import XCTest
@testable import CleansiaCustomer

/// The tripwire that removed the express perk, now pointed at the claim's CONTENT instead of its
/// existence: the mechanism shipped (ADR-0035), so the perk is advertised again, but the two sentences
/// that made the old claim false must not come back with it. Express is a 2–4 h lead window and not
/// "same-day", and the monthly quota is per-plan configuration rather than a number the copy may know.
///
/// The catalog is read through the BUILT bundle rather than the `.xcstrings` source, so an assertion
/// here fails when the shipped app is wrong rather than when a file on disk is.
/// `membership-express-claim.spec.ts` holds the same line for web.
final class MembershipExpressClaimTests: XCTestCase {
    private static let languages = ["en", "cs", "sk", "uk", "ru"]

    private static let perkKeys = [
        "membership_perk_express_title",
        "membership_perk_express_desc",
        "membership_perk_pill_express",
        "membership_perk_pill_express_used",
        "membership_perk_pill_express_trial",
        "membership_success_perk_express"
    ]

    private static let bookingFlowKeys = [
        "booking_slot_express_waived",
        "booking_summary_express_surcharge_waived",
        "booking_express_waiver_available",
        "booking_express_waiver_used",
        "booking_express_waiver_trial"
    ]

    /// Every string in the catalog that names the perk, including the ones that predate it — the scans
    /// below have to cover the whole express surface, not only what this change added.
    private static let allExpressKeys = perkKeys + bookingFlowKeys + [
        "booking_slot_express",
        "booking_summary_express_surcharge",
        "order_cancel_express_waiver_forfeit"
    ]

    private static let sameDayClaims = [
        "same-day", "same day", "sameday",
        "týž den", "tentýž den", "ve stejný den",
        "ten istý deň", "v rovnaký deň",
        "того ж дня", "у той самий день",
        "в тот же день", "в этот же день"
    ]

    /// A bare allow-list of the digits 2/4/20 also permits "2 free express bookings a month", which is
    /// exactly the hardcoded quota this rule exists to stop — so the two numbers the copy MAY name are
    /// erased by SHAPE: the 20% rate and the 2…4 hour window.
    private static let placeholder = "%\\d+\\$[@d]|%[@d]"
    private static let surchargeRate = "\\b20\\s?%"
    private static let leadTimeWindow = "\\b2\\D{1,6}4\\b"

    private static let quotaWords = ["two", "dva", "dvě", "dve", "два", "дві"]

    private var restoreBundle: Bundle?

    override func setUp() {
        super.setUp()
        restoreBundle = L10n.bundle
    }

    override func tearDown() {
        L10n.bundle = restoreBundle ?? .main
        super.tearDown()
    }

    func testPlusAdvertisesTheExpressPerkInEveryLocale() throws {
        try forEachLanguage { language in
            for key in Self.perkKeys {
                let value = L10n.localized(key)
                XCTAssertFalse(value.isBlank, "\(key) is empty in \(language)")
                XCTAssertNotEqual(value, key, "\(key) is unlocalized in \(language)")
            }
        }
    }

    func testTheBookingFlowDisclosesTheWaiverInEveryLocale() throws {
        try forEachLanguage { language in
            for key in Self.bookingFlowKeys {
                let value = L10n.localized(key)
                XCTAssertFalse(value.isBlank, "\(key) is empty in \(language)")
                XCTAssertNotEqual(value, key, "\(key) is unlocalized in \(language)")
            }
        }
    }

    func testNoLocaleClaimsSameDay() throws {
        try forEachLanguage { language in
            for key in Self.allExpressKeys {
                let value = L10n.localized(key).lowercased()
                for claim in Self.sameDayClaims {
                    XCTAssertFalse(
                        value.contains(claim),
                        "\(key) promises \"\(claim)\" in \(language) — express is a 2-4h lead window"
                    )
                }
            }
        }
    }

    func testNoExpressStringHardcodesThePerPlanQuota() throws {
        try forEachLanguage { language in
            for key in Self.allExpressKeys {
                let residue = try Self.erasingTheNumbersTheCopyMayName(L10n.localized(key))
                XCTAssertNil(
                    residue.rangeOfCharacter(from: .decimalDigits),
                    "\(key) spells a count out in \(language): \(residue)"
                )
                for word in Self.quotaWords {
                    XCTAssertNil(
                        residue.range(of: "\\b\(word)\\b", options: [.regularExpression, .caseInsensitive]),
                        "\(key) spells the quota as a word in \(language): \(residue)"
                    )
                }
            }
        }
    }

    func testTheSellingCopyNamesTheTwoToFourHourWindow() throws {
        try forEachLanguage { language in
            let body = L10n.Membership.perkExpressDesc
            XCTAssertNotNil(
                body.range(of: Self.leadTimeWindow, options: .regularExpression),
                "the perk body drops the lead-time window in \(language): \(body)"
            )
        }
    }

    /// A resolver test does not cover the call site, and this claim is exactly the kind that outlives
    /// the condition it was gated on.
    func testEveryMembershipScreenGatesTheClaimOnAServerField() throws {
        let gates = [
            "CleansiaCustomer/Sources/Features/Membership/MembershipPerks.swift": "ExpressWaiverStatus.resolve",
            "CleansiaCustomer/Sources/Features/Membership/SubscribePlusScreen.swift":
                "selectedPlan?.allowsExpressUpgrade == true",
            "CleansiaCustomer/Sources/Features/Shell/CustomerShellView.swift":
                "showExpressPerk: membershipVM.expressWaiverAdvertised"
        ]
        for (path, gate) in gates {
            XCTAssertTrue(try read(path).contains(gate), "\(path) renders the express claim ungated")
        }
    }

    func testTheBookingFlowRendersTheWaiverOnTheServerVerdictAndNeverAdjustsTheCount() throws {
        let slots = try read("CleansiaCustomer/Sources/Features/Booking/WhenWhere/WhenWhereStep.swift")
        XCTAssertTrue(slots.contains("viewModel.expressWaiverStatus"), "the slot grid re-derives the waiver")
        XCTAssertTrue(slots.contains("remaining: viewModel.expressUpgradesRemaining"), "the count is not bound")
        XCTAssertNil(
            slots.range(of: "expressUpgradesRemaining\\s*[-+]", options: .regularExpression),
            "the client adjusts the server's count"
        )

        let pricing = try read("CleansiaCustomer/Sources/Features/Booking/BookingPricing.swift")
        XCTAssertTrue(
            pricing.contains("quote.expressSurchargeWaivedByMembership"),
            "the waived row is not read from the quote"
        )
    }

    private static func erasingTheNumbersTheCopyMayName(_ value: String) throws -> String {
        var residue = value
        for pattern in [placeholder, surchargeRate, leadTimeWindow] {
            residue = residue.replacingOccurrences(of: pattern, with: " ", options: .regularExpression)
        }
        return residue
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

    private func read(_ relativePath: String) throws -> String {
        let root = URL(fileURLWithPath: #filePath)
            .deletingLastPathComponent()
            .deletingLastPathComponent()
            .deletingLastPathComponent()
        return try String(contentsOf: root.appendingPathComponent(relativePath), encoding: .utf8)
    }
}
