import CleansiaCore
import XCTest
@testable import CleansiaCustomer

/// A running trial carries every Plus benefit from day one; what it changes is the money. `hasMembership`
/// counts the trial, so each surface that names a charge or a date has to ask `trialEndsAtUtc` first.
final class MembershipCopyTests: XCTestCase {
    private static let now = Date(timeIntervalSince1970: 1_780_000_000)
    private static let trialEnd = now.addingTimeInterval(7 * 86400)
    private static let paidPeriodEnd = now.addingTimeInterval(30 * 86400)
    private static let languages = ["en", "cs", "sk", "uk", "ru"]

    private static func membership(
        hasMembership: Bool = true,
        trialEndsAtUtc: Date?,
        cancelRequested: Bool = false
    ) -> MyMembership {
        MyMembership(
            hasMembership: hasMembership,
            planCode: "plus_monthly",
            planName: "Cleansia Plus",
            discountPercentage: 5,
            freeCancellationWindowHours: 4,
            allowsExpressUpgrade: true,
            currentPeriodEnd: paidPeriodEnd,
            cancelRequested: cancelRequested,
            billingInterval: 1,
            trialEndsAtUtc: trialEndsAtUtc
        )
    }

    private static let trialing = MembershipCopy(membership(trialEndsAtUtc: trialEnd), now: now)
    private static let paid = MembershipCopy(membership(trialEndsAtUtc: nil), now: now)

    private var restoreBundle: Bundle?

    override func setUp() {
        super.setUp()
        restoreBundle = L10n.bundle
    }

    override func tearDown() {
        L10n.bundle = restoreBundle ?? .main
        super.tearDown()
    }

    // MARK: - Which members are trialing

    func testARunningTrialIsATrial() {
        XCTAssertTrue(Self.trialing.isTrial)
        XCTAssertEqual(Self.trialing.trialEndsOn, Self.trialEnd)
    }

    func testAnEndedTrialIsAPaidMembership() {
        let ended = MembershipCopy(Self.membership(trialEndsAtUtc: Self.now.addingTimeInterval(-60)), now: Self.now)
        XCTAssertFalse(ended.isTrial)
        XCTAssertNil(ended.trialEndsOn)
    }

    func testNoMembershipIsNoTrialWhateverTheDateSays() {
        let lapsed = MembershipCopy(
            Self.membership(hasMembership: false, trialEndsAtUtc: Self.trialEnd),
            now: Self.now
        )
        XCTAssertFalse(lapsed.isTrial)
        XCTAssertNil(lapsed.periodHeadline)
        XCTAssertFalse(MembershipCopy(nil, now: Self.now).isTrial)
    }

    // MARK: - What each surface says

    /// A trial names the day it ends and the first charge falls on; a paid month, the day it renews.
    func testTheCardsDateLineNamesTheTrialEndAndTheFirstCharge() {
        XCTAssertEqual(
            Self.trialing.periodHeadline,
            L10n.Membership.trialUntil(MembershipFormat.periodEnd(Self.trialEnd))
        )
        XCTAssertEqual(Self.trialing.periodHint, L10n.Membership.trialFirstChargeHint)

        XCTAssertEqual(
            Self.paid.periodHeadline,
            L10n.Membership.renewsOn(MembershipFormat.periodEnd(Self.paidPeriodEnd))
        )
        XCTAssertEqual(Self.paid.periodHint, L10n.Membership.autoRenewHint)
    }

    func testACancelledTrialRunsToItsEndAndChargesNothing() {
        let cancelledTrial = MembershipCopy(
            Self.membership(trialEndsAtUtc: Self.trialEnd, cancelRequested: true),
            now: Self.now
        )
        XCTAssertEqual(
            cancelledTrial.periodHeadline,
            L10n.Membership.activeUntil(MembershipFormat.periodEnd(Self.trialEnd))
        )
        XCTAssertEqual(cancelledTrial.periodHint, L10n.Membership.trialCancelledLead)

        let cancelledPaid = MembershipCopy(
            Self.membership(trialEndsAtUtc: nil, cancelRequested: true),
            now: Self.now
        )
        XCTAssertEqual(
            cancelledPaid.periodHeadline,
            L10n.Membership.activeUntil(MembershipFormat.periodEnd(Self.paidPeriodEnd))
        )
        XCTAssertEqual(cancelledPaid.periodHint, L10n.Membership.thenEndsHint)
    }

    func testCancellingDuringTheTrialChargesNothing() {
        let periodEnd = Self.trialEnd
        XCTAssertEqual(
            Self.trialing.cancelDialogMessage,
            L10n.Membership.cancelDialogMessageTrial(MembershipFormat.periodEnd(Self.trialEnd))
        )
        XCTAssertEqual(Self.trialing.cancelSuccess(activeUntil: periodEnd), L10n.Membership.cancelSuccessTrial)

        XCTAssertEqual(Self.paid.cancelDialogMessage, L10n.Membership.cancelDialogMessage)
        XCTAssertEqual(
            Self.paid.cancelSuccess(activeUntil: periodEnd),
            L10n.Membership.cancelledUntil(MembershipFormat.periodEnd(periodEnd))
        )
    }

    /// A past-due membership has no paid period left to run out: the cancel ends it now, so neither the
    /// dialog nor the confirmation may name a date the benefits last until.
    func testCancellingAPastDueMembershipEndsItNow() {
        let pastDue = MembershipCopy(MembershipFixtures.pastDue, now: Self.now)
        XCTAssertTrue(pastDue.benefitsPaused)
        XCTAssertEqual(pastDue.cancelDialogMessage, L10n.Membership.cancelDialogMessagePastDue)
        XCTAssertEqual(pastDue.cancelSuccess(activeUntil: Self.now), L10n.Membership.cancelSuccessPastDue)
        XCTAssertFalse(Self.paid.benefitsPaused)
        XCTAssertFalse(MembershipCopy(MembershipFixtures.inactive, now: Self.now).benefitsPaused)
    }

    func testSwitchingDuringTheTrialChargesNothingNow() {
        XCTAssertEqual(
            Self.trialing.switchDialogMessage(price: "2 030 Kč"),
            L10n.Membership.switchDialogMessageTrial(
                trialEndsOn: MembershipFormat.periodEnd(Self.trialEnd),
                price: "2 030 Kč"
            )
        )
        XCTAssertEqual(
            Self.paid.switchDialogMessage(price: "2 030 Kč"),
            L10n.Membership.switchDialogMessage("2 030 Kč")
        )
    }

    func testTheWelcomeTellsATrialingMemberWhenTheirTrialEnds() {
        XCTAssertEqual(
            Self.trialing.successSubtitle,
            L10n.Membership.successSubtitleTrial(MembershipFormat.periodEnd(Self.trialEnd))
        )
        XCTAssertEqual(Self.paid.successSubtitle, L10n.Membership.successSubtitle)
    }

    // MARK: - The copy itself

    func testEveryTrialAndPastDueSentenceIsWrittenInAllFiveLanguages() throws {
        let keys = [
            "membership_cancel_dialog_message_trial",
            "membership_cancel_success_trial",
            "membership_switch_dialog_message_trial",
            "membership_success_subtitle_trial",
            "membership_trial_cancelled_lead",
            "membership_trial_until",
            "membership_trial_first_charge_hint",
            "membership_status_past_due_badge",
            "membership_past_due_body",
            "membership_past_due_cancel_hint",
            "membership_cancel_dialog_message_past_due",
            "membership_cancel_success_past_due"
        ]
        for language in Self.languages {
            L10n.bundle = try localeBundle(language)
            for key in keys {
                let value = L10n.localized(key)
                XCTAssertNotEqual(value, key, "\(key) is unlocalized in \(language)")
                XCTAssertFalse(value.isBlank, "\(key) is empty in \(language)")
            }
        }
    }

    /// A dropped placeholder renders as literal text, and the date is the one fact these sentences exist
    /// to state.
    func testTheTrialSentencesCarryTheDateAndThePriceInEveryLanguage() throws {
        for language in Self.languages {
            L10n.bundle = try localeBundle(language)
            let cancel = L10n.Membership.cancelDialogMessageTrial("DATE")
            let swap = L10n.Membership.switchDialogMessageTrial(trialEndsOn: "DATE", price: "PRICE")
            XCTAssertTrue(cancel.contains("DATE"), "the cancel dialog drops the trial end in \(language)")
            XCTAssertTrue(swap.contains("DATE"), "the switch dialog drops the trial end in \(language)")
            XCTAssertTrue(swap.contains("PRICE"), "the switch dialog drops the price in \(language)")
            XCTAssertTrue(L10n.Membership.trialUntil("DATE").contains("DATE"), "the card drops it in \(language)")
            XCTAssertTrue(
                L10n.Membership.successSubtitleTrial("DATE").contains("DATE"),
                "the welcome drops the trial end in \(language)"
            )
        }
    }

    // MARK: - The surfaces read it

    func testTheCardTheWelcomeAndTheShellReadTheTrialCopy() throws {
        let card = try read("CleansiaCustomer/Sources/Features/Membership/MembershipManagementCard.swift")
        for binding in [
            "} else if membership.benefitsPaused {",
            "InactiveCard(trialDays: vm.headlineTrialDays",
            "message: vm.copy.cancelDialogMessage",
            "message: vm.copy.switchDialogMessage(",
            "copy.cancelSuccess(activeUntil: date)",
            "if let periodHeadline = copy.periodHeadline",
            "Text(copy.periodHint)"
        ] {
            XCTAssertTrue(card.contains(binding), "the membership card lost `\(binding)`")
        }
        let welcome = try read("CleansiaCustomer/Sources/Features/Membership/MembershipSuccessScreen.swift")
        for binding in ["Text(copy.successSubtitle)", "L10n.Membership.successCtaSetupRecurring"] {
            XCTAssertTrue(welcome.contains(binding), "the welcome screen lost `\(binding)`")
        }
        XCTAssertNil(
            welcome.range(of: #"if\s+(let\s+\w+\s*=\s*)?copy\."#, options: .regularExpression),
            "the welcome holds a perk or the schedule back from a trialing member"
        )
        let shell = try read("CleansiaCustomer/Sources/Features/Shell/CustomerShellView.swift")
        XCTAssertTrue(shell.contains("copy: membershipVM.copy"), "the welcome screen is handed no trial state")
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
