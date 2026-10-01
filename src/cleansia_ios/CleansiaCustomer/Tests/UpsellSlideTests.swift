import CleansiaCore
import XCTest
@testable import CleansiaCustomer

final class UpsellSlideTests: XCTestCase {
    func testFreeUserGetsPlusReferralBookInOrder() {
        let slides = UpsellSlide.slides(isPlus: false, showSetupRecurring: false)
        XCTAssertEqual(slides.map(\.kind), [.plus, .referral, .book])
    }

    func testPlusUserWithTemplatesDropsThePlusAndSetupSlides() {
        let slides = UpsellSlide.slides(isPlus: true, showSetupRecurring: false)
        XCTAssertEqual(slides.map(\.kind), [.referral, .book])
    }

    func testPlusUserWithoutTemplatesLeadsWithSetupRecurring() {
        let slides = UpsellSlide.slides(isPlus: true, showSetupRecurring: true)
        XCTAssertEqual(slides.map(\.kind), [.setupRecurring, .referral, .book])
    }

    func testReferralAndBookCloseEveryPermutation() {
        for isPlus in [false, true] {
            for showSetupRecurring in [false, true] {
                let slides = UpsellSlide.slides(isPlus: isPlus, showSetupRecurring: showSetupRecurring)
                XCTAssertEqual(
                    slides.suffix(2).map(\.kind),
                    [.referral, .book],
                    "isPlus=\(isPlus) showSetupRecurring=\(showSetupRecurring)"
                )
            }
        }
    }

    func testEveryVisibleSlideDrawsItsOwnMascot() {
        for isPlus in [false, true] {
            for showSetupRecurring in [false, true] {
                let mascots = UpsellSlide.slides(isPlus: isPlus, showSetupRecurring: showSetupRecurring).map(\.mascot)
                XCTAssertEqual(
                    Set(mascots).count,
                    mascots.count,
                    "isPlus=\(isPlus) showSetupRecurring=\(showSetupRecurring) repeats a mascot"
                )
            }
        }
    }

    /// No welcome offer at launch: the slide advertised a code that exists only in DEV.
    func testTheWelcomeOfferCopyIsGone() {
        for key in ["home_upsell_welcome_top", "home_upsell_welcome_title", "home_upsell_welcome_cta"] {
            XCTAssertEqual(L10n.localized(key), key, "\(key) is back in the catalog")
        }
    }

    func testPlusSlideContentMatchesAndroid() throws {
        let slides = UpsellSlide.slides(isPlus: false, showSetupRecurring: false)
        let slide = try slide(.plus, in: slides)
        XCTAssertEqual(slide.mascot, .plus)
        XCTAssertEqual(slide.gradient, .plusHero)
        XCTAssertEqual(slide.action, .subscribePlus)
        XCTAssertEqual(slide.top, L10n.Home.upsellPlusTop)
        XCTAssertEqual(slide.title, L10n.Home.upsellPlusTitle)
        XCTAssertEqual(slide.cta, L10n.Home.upsellPlusCta)
    }

    func testThePlusSlideOffersTheTrialOnlyWhenThereIsOneToOffer() throws {
        let offered = UpsellSlide.slides(isPlus: false, plusTrialDays: 14, showSetupRecurring: false)
        let trial = try slide(.plus, in: offered)
        XCTAssertEqual(trial.title, L10n.Home.upsellPlusTitleTrial(14))
        XCTAssertEqual(trial.cta, L10n.Home.upsellPlusCtaTrial)
        XCTAssertEqual(trial.action, .subscribePlus)

        let withheld = UpsellSlide.slides(isPlus: false, plusTrialDays: 0, showSetupRecurring: false)
        let none = try slide(.plus, in: withheld)
        XCTAssertEqual(none.title, L10n.Home.upsellPlusTitle)
        XCTAssertEqual(none.cta, L10n.Home.upsellPlusCta)
    }

    func testSetupRecurringSlideContentMatchesAndroid() throws {
        let slides = UpsellSlide.slides(isPlus: true, showSetupRecurring: true)
        let slide = try slide(.setupRecurring, in: slides)
        XCTAssertEqual(slide.mascot, .idea)
        XCTAssertEqual(slide.gradient, .purple)
        XCTAssertEqual(slide.action, .setupRecurring)
        XCTAssertEqual(slide.top, L10n.Home.upsellSetupRecurringTop)
        XCTAssertEqual(slide.title, L10n.Home.upsellSetupRecurringTitle)
        XCTAssertEqual(slide.cta, L10n.Home.upsellSetupRecurringCta)
    }

    func testReferralSlideContentMatchesAndroid() throws {
        let slides = UpsellSlide.slides(isPlus: true, showSetupRecurring: false)
        let slide = try slide(.referral, in: slides)
        XCTAssertEqual(slide.mascot, .thumbsUp)
        XCTAssertEqual(slide.gradient, .cyan)
        XCTAssertEqual(slide.action, .openReferral)
        XCTAssertEqual(slide.top, L10n.Home.upsellReferralTop)
        XCTAssertEqual(slide.title, L10n.Home.upsellReferralTitle)
        XCTAssertEqual(slide.cta, L10n.Home.upsellReferralCta)
    }

    func testBookSlideContentMatchesAndroid() throws {
        let slides = UpsellSlide.slides(isPlus: true, showSetupRecurring: false)
        let slide = try slide(.book, in: slides)
        XCTAssertEqual(slide.mascot, .cleaning)
        XCTAssertEqual(slide.gradient, .blue)
        XCTAssertEqual(slide.action, .book)
        XCTAssertEqual(slide.top, L10n.Home.heroGreeting)
        XCTAssertEqual(slide.title, L10n.Home.heroPrompt)
        XCTAssertEqual(slide.cta, L10n.Home.heroCta)
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

    /// A slide arriving after first paint keeps the slide on screen; one leaving falls back to the last.
    func testACountChangeKeepsTheSlideOnScreen() {
        let onSecond = UpsellSlide.page(logical: 1, count: 3)
        let grown = UpsellSlide.page(afterCountChangeFrom: onSecond, oldCount: 3, newCount: 4)
        XCTAssertEqual(UpsellSlide.logicalIndex(page: grown, count: 4), 1)

        let onThird = UpsellSlide.page(logical: 2, count: 3)
        let shrunk = UpsellSlide.page(afterCountChangeFrom: onThird, oldCount: 3, newCount: 2)
        XCTAssertEqual(UpsellSlide.logicalIndex(page: shrunk, count: 2), 1)

        let fromClone = UpsellSlide.page(afterCountChangeFrom: 4, oldCount: 3, newCount: 3)
        XCTAssertEqual(UpsellSlide.logicalIndex(page: fromClone, count: 3), 0)
    }

    private func slide(_ kind: UpsellSlide.Kind, in slides: [UpsellSlide]) throws -> UpsellSlide {
        try XCTUnwrap(slides.first { $0.kind == kind }, "missing \(kind) slide")
    }
}
