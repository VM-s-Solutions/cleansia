import CleansiaCore
import XCTest
@testable import CleansiaCustomer

final class UpsellSlideTests: XCTestCase {
    private typealias Inputs = UpsellSlide.Inputs

    private let heldCredit = CustomerCredit.Balance(amount: 250, currencyCode: "CZK", expiresOn: nil)

    /// Every combination of the five predicates that decide the set — 32 of them, the Android
    /// `UpsellSlidesTest` sweep.
    private var everyInputs: [Inputs] {
        var all: [Inputs] = []
        for mask in 0 ..< 32 {
            all.append(Inputs(
                isPlus: mask & 1 != 0,
                showSetupRecurring: mask & 2 != 0,
                notificationsOff: mask & 4 != 0,
                credit: mask & 8 != 0 ? heldCredit : nil,
                creditShare: 0.7,
                expressRemaining: mask & 16 != 0 ? 2 : 0
            ))
        }
        return all
    }

    // MARK: - Which slides, in which order

    func testAFreeCustomerGetsPlusReferralAndQuickSize() {
        XCTAssertEqual(UpsellSlide.kinds(Inputs()), [.plus, .referral, .quickSize])
    }

    func testAMemberWithASchedulePairsReferralWithQuickSize() {
        XCTAssertEqual(UpsellSlide.kinds(Inputs(isPlus: true)), [.referral, .quickSize])
    }

    func testAMemberWithoutAScheduleLeadsWithSetupRecurring() {
        XCTAssertEqual(
            UpsellSlide.kinds(Inputs(isPlus: true, showSetupRecurring: true)),
            [.setupRecurring, .referral, .quickSize]
        )
    }

    func testTheRuledOrderIsNotificationsCreditExpressRecurringPlusReferral() {
        let member = Inputs(
            isPlus: true,
            showSetupRecurring: true,
            notificationsOff: true,
            credit: heldCredit,
            expressRemaining: 1
        )
        XCTAssertEqual(UpsellSlide.kinds(member), [.notifications, .credit, .express, .setupRecurring, .quickSize])

        let free = Inputs(notificationsOff: true, credit: heldCredit)
        XCTAssertEqual(UpsellSlide.kinds(free), [.notifications, .credit, .plus, .referral, .quickSize])
    }

    /// The first four eligible show, then quick-size always closes — never more than five.
    func testTheSetIsCappedAtFiveAndQuickSizeAlwaysCloses() {
        for inputs in everyInputs {
            let kinds = UpsellSlide.kinds(inputs)
            XCTAssertLessThanOrEqual(kinds.count, UpsellSlide.leadingCap + 1, "\(inputs)")
            XCTAssertEqual(kinds.last, .quickSize, "\(inputs)")
            XCTAssertEqual(kinds.filter { $0 == .quickSize }.count, 1, "\(inputs)")
        }
    }

    func testEachSlideShowsOnlyWhileItsPredicateHolds() {
        for inputs in everyInputs {
            let kinds = UpsellSlide.kinds(inputs)
            if !inputs.notificationsOff { XCTAssertFalse(kinds.contains(.notifications), "\(inputs)") }
            if inputs.credit == nil { XCTAssertFalse(kinds.contains(.credit), "\(inputs)") }
            if inputs.expressRemaining == 0 { XCTAssertFalse(kinds.contains(.express), "\(inputs)") }
            if !inputs.showSetupRecurring { XCTAssertFalse(kinds.contains(.setupRecurring), "\(inputs)") }
            if inputs.isPlus { XCTAssertFalse(kinds.contains(.plus), "\(inputs)") }
        }
    }

    func testEveryVisibleSlideDrawsItsOwnMascot() {
        for inputs in everyInputs {
            let mascots = UpsellSlide.slides(inputs).map(\.mascot)
            XCTAssertEqual(Set(mascots).count, mascots.count, "\(inputs) repeats a mascot")
        }
        let all = UpsellSlide.Kind.allCases.map(UpsellSlide.mascot)
        XCTAssertEqual(Set(all).count, all.count, "two kinds share a drawing")
    }

    /// No welcome offer at launch: the slide advertised a code that exists only in DEV.
    func testTheWelcomeOfferCopyIsGone() {
        for key in ["home_upsell_welcome_top", "home_upsell_welcome_title", "home_upsell_welcome_cta"] {
            XCTAssertEqual(L10n.localized(key), key, "\(key) is back in the catalog")
        }
    }

    /// The generic Book slide is gone — quick-size replaced it — and so is its copy.
    func testTheGenericBookSlideCopyIsGone() {
        for key in ["home_hero_greeting", "home_hero_prompt", "home_hero_cta"] {
            XCTAssertEqual(L10n.localized(key), key, "\(key) is back in the catalog")
        }
    }

    // MARK: - What each slide says and does

    func testPlusSlideContentMatchesAndroid() throws {
        let slide = try slide(.plus, in: UpsellSlide.slides(Inputs()))
        XCTAssertEqual(slide.mascot, .plus)
        XCTAssertEqual(slide.gradient, .plusHero)
        XCTAssertEqual(slide.action, .subscribePlus)
        XCTAssertEqual(slide.top, L10n.Home.upsellPlusTop)
        XCTAssertEqual(slide.title, L10n.Home.upsellPlusTitle)
        XCTAssertEqual(slide.cta, L10n.Home.upsellPlusCta)
    }

    func testThePlusSlideOffersTheTrialOnlyWhenThereIsOneToOffer() throws {
        let trial = try slide(.plus, in: UpsellSlide.slides(Inputs(plusTrialDays: 14)))
        XCTAssertEqual(trial.title, L10n.Home.upsellPlusTitleTrial(14))
        XCTAssertEqual(trial.cta, L10n.Home.upsellPlusCtaTrial)
        XCTAssertEqual(trial.action, .subscribePlus)

        let none = try slide(.plus, in: UpsellSlide.slides(Inputs(plusTrialDays: 0)))
        XCTAssertEqual(none.title, L10n.Home.upsellPlusTitle)
        XCTAssertEqual(none.cta, L10n.Home.upsellPlusCta)
    }

    func testSetupRecurringSlideContentMatchesAndroid() throws {
        let slide = try slide(.setupRecurring, in: UpsellSlide.slides(Inputs(isPlus: true, showSetupRecurring: true)))
        XCTAssertEqual(slide.mascot, .idea)
        XCTAssertEqual(slide.gradient, .purple)
        XCTAssertEqual(slide.action, .setupRecurring)
        XCTAssertEqual(slide.top, L10n.Home.upsellSetupRecurringTop)
        XCTAssertEqual(slide.title, L10n.Home.upsellSetupRecurringTitle)
        XCTAssertEqual(slide.cta, L10n.Home.upsellSetupRecurringCta)
    }

    func testReferralSlideSharesTheCodeOnceItHasLoaded() throws {
        let waiting = try slide(.referral, in: UpsellSlide.slides(Inputs(isPlus: true)))
        XCTAssertEqual(waiting.mascot, .thumbsUp)
        XCTAssertEqual(waiting.gradient, .cyan)
        XCTAssertEqual(waiting.action, .openReferral, "no code yet: open Rewards, where it appears")
        XCTAssertEqual(waiting.top, L10n.Home.upsellReferralTop)
        XCTAssertEqual(waiting.title, L10n.Home.upsellReferralTitle)
        XCTAssertEqual(waiting.cta, L10n.Home.upsellReferralCta)

        let loaded = try slide(.referral, in: UpsellSlide.slides(Inputs(isPlus: true, referralCode: "JOIN50")))
        XCTAssertEqual(loaded.action, .shareReferral(code: "JOIN50"))
    }

    func testNotificationsSlideTurnsThemOn() throws {
        let slide = try slide(.notifications, in: UpsellSlide.slides(Inputs(notificationsOff: true)))
        XCTAssertEqual(slide.mascot, .waving)
        XCTAssertEqual(slide.gradient, .orange)
        XCTAssertEqual(slide.action, .turnOnNotifications)
        XCTAssertEqual(slide.title, L10n.Home.upsellNotificationsTitle)
        XCTAssertEqual(slide.cta, L10n.Home.upsellNotificationsCta)
    }

    /// The server's balance and share, never figures of the copy's own.
    func testCreditSlideStatesTheBalanceAndTheServersShare() throws {
        let slide = try slide(.credit, in: UpsellSlide.slides(Inputs(credit: heldCredit, creditShare: 0.55)))
        XCTAssertEqual(slide.mascot, .invoice)
        XCTAssertEqual(slide.gradient, .emerald)
        XCTAssertEqual(slide.action, .book)
        XCTAssertEqual(slide.top, L10n.Credit.yourCredit)
        XCTAssertEqual(
            slide.title,
            L10n.Home.upsellCreditTitle(OrdersFormat.price(250, currencyCode: "CZK"), share: 0.55)
        )
        XCTAssertTrue(slide.title.contains("55"))
    }

    func testExpressSlideStatesTheWaiversLeftAndTheBookingWindow() throws {
        let slide = try slide(.express, in: UpsellSlide.slides(Inputs(isPlus: true, expressRemaining: 3)))
        XCTAssertEqual(slide.mascot, .floorScrubber)
        XCTAssertEqual(slide.action, .book)
        XCTAssertEqual(slide.top, L10n.Home.upsellExpressTop(2, 4))
        XCTAssertEqual(slide.title, L10n.Home.upsellExpressTitle(3))
        XCTAssertTrue(slide.title.contains("3"))
    }

    func testQuickSizeClosesWithItsOwnTitleAndPriceButton() throws {
        let slide = try slide(.quickSize, in: UpsellSlide.slides(Inputs()))
        XCTAssertEqual(slide.mascot, .vacuuming)
        XCTAssertEqual(slide.gradient, .blue)
        XCTAssertEqual(slide.action, .bookSize(rooms: 1, bathrooms: 1))
        XCTAssertEqual(slide.title, L10n.Home.quickSizeTitle)
        XCTAssertEqual(slide.cta, L10n.Home.quickSizeCta)
    }

    @MainActor
    func testShowSetupRecurringSlideRequiresPlusAndAWiredSourceAndNoTemplates() {
        XCTAssertTrue(
            HomeTabViewModel.showSetupRecurringSlide(isPlus: true, hasRecurringSource: true, templatesEmpty: true)
        )
        XCTAssertFalse(
            HomeTabViewModel.showSetupRecurringSlide(isPlus: false, hasRecurringSource: true, templatesEmpty: true)
        )
        XCTAssertFalse(
            HomeTabViewModel.showSetupRecurringSlide(isPlus: true, hasRecurringSource: false, templatesEmpty: true)
        )
        XCTAssertFalse(
            HomeTabViewModel.showSetupRecurringSlide(isPlus: true, hasRecurringSource: true, templatesEmpty: false)
        )
    }

    // MARK: - The loop

    func testTheLoopAddsOneCloneAtEachEnd() {
        XCTAssertEqual(UpsellSlide.pageCount(slides: 3), 5)
        XCTAssertEqual(UpsellSlide.pageCount(slides: 2), 4)
    }

    func testASingleSlideHasNothingToLoop() {
        XCTAssertEqual(UpsellSlide.pageCount(slides: 1), 1)
        XCTAssertEqual(UpsellSlide.logicalIndex(page: 0, count: 1), 0)
        XCTAssertEqual(UpsellSlide.page(logical: 0, count: 1), 0)
        XCTAssertNil(UpsellSlide.reanchor(page: 0, count: 1))
    }

    func testTheClonesShowTheSlidesTheyCopy() {
        XCTAssertEqual(UpsellSlide.logicalIndex(page: 0, count: 3), 2, "the leading clone is the last slide")
        XCTAssertEqual(UpsellSlide.logicalIndex(page: 4, count: 3), 0, "the trailing clone is the first slide")
        XCTAssertEqual((1 ... 3).map { UpsellSlide.logicalIndex(page: $0, count: 3) }, [0, 1, 2])
    }

    func testASettledCloneJumpsToTheSlideItCopiesAndARealPageStays() {
        XCTAssertEqual(UpsellSlide.reanchor(page: 0, count: 3), 3)
        XCTAssertEqual(UpsellSlide.reanchor(page: 4, count: 3), 1)
        for page in 1 ... 3 {
            XCTAssertNil(UpsellSlide.reanchor(page: page, count: 3), "page \(page)")
        }
    }

    /// Swiping forward from the last slide lands on the first; back from the first lands on the last.
    func testTheLoopClosesInBothDirections() throws {
        for count in 2 ... 5 {
            let pastLast = UpsellSlide.page(logical: count - 1, count: count) + 1
            let landed = try XCTUnwrap(UpsellSlide.reanchor(page: pastLast, count: count))
            XCTAssertEqual(UpsellSlide.logicalIndex(page: landed, count: count), 0, "n=\(count)")

            let beforeFirst = UpsellSlide.page(logical: 0, count: count) - 1
            let back = try XCTUnwrap(UpsellSlide.reanchor(page: beforeFirst, count: count))
            XCTAssertEqual(UpsellSlide.logicalIndex(page: back, count: count), count - 1, "n=\(count)")
        }
    }

    /// A slide arriving at the front after first paint keeps the slide on screen; one leaving falls
    /// back to the same position, or the last slide.
    func testASetChangeKeepsTheSlideOnScreen() {
        let before: [UpsellSlide.Kind] = [.plus, .referral, .quickSize]
        let onReferral = UpsellSlide.page(logical: 1, count: before.count)

        let arrived: [UpsellSlide.Kind] = [.credit, .plus, .referral, .quickSize]
        let kept = UpsellSlide.page(afterChangeFrom: onReferral, old: before, new: arrived)
        XCTAssertEqual(arrived[UpsellSlide.logicalIndex(page: kept, count: arrived.count)], .referral)

        let onPlus = UpsellSlide.page(logical: 0, count: before.count)
        let left: [UpsellSlide.Kind] = [.referral, .quickSize]
        let fallback = UpsellSlide.page(afterChangeFrom: onPlus, old: before, new: left)
        XCTAssertEqual(UpsellSlide.logicalIndex(page: fallback, count: left.count), 0)

        let fromClone = UpsellSlide.page(afterChangeFrom: 4, old: before, new: before)
        XCTAssertEqual(UpsellSlide.logicalIndex(page: fromClone, count: before.count), 0)
    }

    private func slide(_ kind: UpsellSlide.Kind, in slides: [UpsellSlide]) throws -> UpsellSlide {
        try XCTUnwrap(slides.first { $0.kind == kind }, "missing \(kind) slide")
    }
}
