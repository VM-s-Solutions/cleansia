import CleansiaCore
import XCTest
@testable import CleansiaCustomer

final class UpsellSlideTests: XCTestCase {
    func testFreeUserWithoutOrdersGetsPlusWelcomeReferralBookInOrder() {
        let slides = UpsellSlide.slides(isPlus: false, hasAnyOrders: false, showSetupRecurring: false)
        XCTAssertEqual(slides.map(\.kind), [.plus, .welcome, .referral, .book])
    }

    func testFreeUserWithOrdersDropsTheWelcomeSlide() {
        let slides = UpsellSlide.slides(isPlus: false, hasAnyOrders: true, showSetupRecurring: false)
        XCTAssertEqual(slides.map(\.kind), [.plus, .referral, .book])
    }

    func testPlusUserWithTemplatesDropsThePlusAndSetupSlides() {
        let slides = UpsellSlide.slides(isPlus: true, hasAnyOrders: true, showSetupRecurring: false)
        XCTAssertEqual(slides.map(\.kind), [.referral, .book])
    }

    func testPlusUserWithoutTemplatesLeadsWithSetupRecurring() {
        let slides = UpsellSlide.slides(isPlus: true, hasAnyOrders: true, showSetupRecurring: true)
        XCTAssertEqual(slides.map(\.kind), [.setupRecurring, .referral, .book])
    }

    func testPlusUserWithoutOrdersOrTemplatesGetsSetupWelcomeReferralBook() {
        let slides = UpsellSlide.slides(isPlus: true, hasAnyOrders: false, showSetupRecurring: true)
        XCTAssertEqual(slides.map(\.kind), [.setupRecurring, .welcome, .referral, .book])
    }

    func testReferralAndBookCloseEveryPermutation() {
        for isPlus in [false, true] {
            for hasAnyOrders in [false, true] {
                for showSetupRecurring in [false, true] {
                    let slides = UpsellSlide.slides(
                        isPlus: isPlus,
                        hasAnyOrders: hasAnyOrders,
                        showSetupRecurring: showSetupRecurring
                    )
                    XCTAssertEqual(
                        slides.suffix(2).map(\.kind),
                        [.referral, .book],
                        "isPlus=\(isPlus) hasAnyOrders=\(hasAnyOrders) showSetupRecurring=\(showSetupRecurring)"
                    )
                }
            }
        }
    }

    func testPlusSlideContentMatchesAndroid() throws {
        let slides = UpsellSlide.slides(isPlus: false, hasAnyOrders: true, showSetupRecurring: false)
        let slide = try slide(.plus, in: slides)
        XCTAssertEqual(slide.mascot, .ready)
        XCTAssertEqual(slide.gradient, .plusHero)
        XCTAssertEqual(slide.action, .subscribePlus)
        XCTAssertEqual(slide.top, L10n.Home.upsellPlusTop)
        XCTAssertEqual(slide.title, L10n.Home.upsellPlusTitle)
        XCTAssertEqual(slide.cta, L10n.Home.upsellPlusCta)
    }

    func testSetupRecurringSlideContentMatchesAndroid() throws {
        let slides = UpsellSlide.slides(isPlus: true, hasAnyOrders: true, showSetupRecurring: true)
        let slide = try slide(.setupRecurring, in: slides)
        XCTAssertEqual(slide.mascot, .idea)
        XCTAssertEqual(slide.gradient, .purple)
        XCTAssertEqual(slide.action, .setupRecurring)
        XCTAssertEqual(slide.top, L10n.Home.upsellSetupRecurringTop)
        XCTAssertEqual(slide.title, L10n.Home.upsellSetupRecurringTitle)
        XCTAssertEqual(slide.cta, L10n.Home.upsellSetupRecurringCta)
    }

    func testWelcomeSlideContentMatchesAndroid() throws {
        let slides = UpsellSlide.slides(isPlus: false, hasAnyOrders: false, showSetupRecurring: false)
        let slide = try slide(.welcome, in: slides)
        XCTAssertEqual(slide.mascot, .mopping)
        XCTAssertEqual(slide.gradient, .purple)
        XCTAssertEqual(slide.action, .book)
        XCTAssertEqual(slide.top, L10n.Home.upsellWelcomeTop)
        XCTAssertEqual(slide.title, L10n.Home.upsellWelcomeTitle)
        XCTAssertEqual(slide.cta, L10n.Home.upsellWelcomeCta)
    }

    func testReferralSlideContentMatchesAndroid() throws {
        let slides = UpsellSlide.slides(isPlus: true, hasAnyOrders: true, showSetupRecurring: false)
        let slide = try slide(.referral, in: slides)
        XCTAssertEqual(slide.mascot, .cleaning)
        XCTAssertEqual(slide.gradient, .cyan)
        XCTAssertEqual(slide.action, .openReferral)
        XCTAssertEqual(slide.top, L10n.Home.upsellReferralTop)
        XCTAssertEqual(slide.title, L10n.Home.upsellReferralTitle)
        XCTAssertEqual(slide.cta, L10n.Home.upsellReferralCta)
    }

    func testBookSlideContentMatchesAndroid() throws {
        let slides = UpsellSlide.slides(isPlus: true, hasAnyOrders: true, showSetupRecurring: false)
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

    private func slide(_ kind: UpsellSlide.Kind, in slides: [UpsellSlide]) throws -> UpsellSlide {
        try XCTUnwrap(slides.first { $0.kind == kind }, "missing \(kind) slide")
    }
}
