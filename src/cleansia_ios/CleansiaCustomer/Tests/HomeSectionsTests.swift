import CleansiaCustomerApi
import Foundation
import XCTest
@testable import CleansiaCustomer

final class HomeSectionsTests: XCTestCase {
    // MARK: - displayedAddress (HomeTab.kt:111-113 — selected ?? default ?? first)

    func testDisplayedAddressPrefersTheSelectedId() {
        let addresses = [
            SavedAddressFixtures.address(id: "a", isDefault: true),
            SavedAddressFixtures.address(id: "b")
        ]
        XCTAssertEqual(HomeSections.displayedAddress(addresses, selectedId: "b")?.id, "b")
    }

    func testDisplayedAddressFallsBackToTheDefault() {
        let addresses = [
            SavedAddressFixtures.address(id: "a"),
            SavedAddressFixtures.address(id: "b", isDefault: true)
        ]
        XCTAssertEqual(HomeSections.displayedAddress(addresses, selectedId: nil)?.id, "b")
        XCTAssertEqual(HomeSections.displayedAddress(addresses, selectedId: "gone")?.id, "b")
    }

    func testDisplayedAddressFallsBackToTheFirst() {
        let addresses = [
            SavedAddressFixtures.address(id: "a"),
            SavedAddressFixtures.address(id: "b")
        ]
        XCTAssertEqual(HomeSections.displayedAddress(addresses, selectedId: nil)?.id, "a")
        XCTAssertNil(HomeSections.displayedAddress([], selectedId: nil))
    }

    // MARK: - popularPackages (HomeTab.kt:149-153 — non-blank ids, top 3)

    func testPopularPackagesDropsBlankIdsAndTakesThree() {
        let packages = [
            CatalogFixtures.package(id: ""),
            CatalogFixtures.package(id: "p1"),
            CatalogFixtures.package(id: "p2"),
            CatalogFixtures.package(id: "p3"),
            CatalogFixtures.package(id: "p4")
        ]
        XCTAssertEqual(HomeSections.popularPackages(packages, languageCode: "en").map(\.id), ["p1", "p2", "p3"])
    }

    func testPopularPackagesLocalizesNameToTheAppLanguageWithFallback() {
        let package = CatalogPackage(
            id: "p1",
            name: "Standard cleaning",
            description: nil,
            price: 1500,
            translations: [
                "ru": CatalogTranslation(name: "Стандартная уборка", description: nil),
                "cs": CatalogTranslation(name: "Standardní úklid", description: nil)
            ],
            includedServices: []
        )
        XCTAssertEqual(HomeSections.popularPackages([package], languageCode: "ru").first?.name, "Стандартная уборка")
        XCTAssertEqual(HomeSections.popularPackages([package], languageCode: "cs").first?.name, "Standardní úklid")
        // No translation for the language → the default (English) name.
        XCTAssertEqual(HomeSections.popularPackages([package], languageCode: "sk").first?.name, "Standard cleaning")
        XCTAssertEqual(HomeSections.popularPackages([package], languageCode: "en").first?.name, "Standard cleaning")
    }

    // MARK: - activeRecurring (HomeTab.kt:163-165 — active only, top 3)

    func testActiveRecurringFiltersInactiveAndTakesThree() {
        let templates = [
            RecurringFixtures.template(id: "t1", isActive: false),
            RecurringFixtures.template(id: "t2"),
            RecurringFixtures.template(id: "t3"),
            RecurringFixtures.template(id: "t4"),
            RecurringFixtures.template(id: "t5")
        ]
        XCTAssertEqual(HomeSections.activeRecurring(templates).map(\.id), ["t2", "t3", "t4"])
    }

    // MARK: - mostRecentCompleted (HomeTab.kt:170-172)

    func testMostRecentCompletedIsTheFirstCompletedInRepoOrder() {
        let orders = [
            OrderFixtures.listItem(id: "o1", statusValue: 2),
            OrderFixtures.listItem(id: "o2", statusValue: 5),
            OrderFixtures.listItem(id: "o3", statusValue: 5)
        ]
        XCTAssertEqual(HomeSections.mostRecentCompleted(orders)?.id, "o2")
        XCTAssertNil(HomeSections.mostRecentCompleted([OrderFixtures.listItem(id: "o1", statusValue: 3)]))
    }

    // MARK: - recentForDisplay (HomeTab.kt:177-181 — cleaningDateTime desc, nils last, top 3)

    func testRecentForDisplaySortsByCleaningDateDescendingWithNilsLast() {
        var older = OrderFixtures.listItem(id: "old", statusValue: 5)
        older.cleaningDateTime = Date(timeIntervalSince1970: 1000)
        var newer = OrderFixtures.listItem(id: "new", statusValue: 5)
        newer.cleaningDateTime = Date(timeIntervalSince1970: 2000)
        let dateless = OrderFixtures.listItem(id: "none", statusValue: 5)

        let sorted = HomeSections.recentForDisplay([dateless, older, newer])
        XCTAssertEqual(sorted.map(\.id), ["new", "old", "none"])
    }

    func testRecentForDisplayTakesThree() {
        let orders = (1 ... 5).map { index -> OrderListItem in
            var order = OrderFixtures.listItem(id: "o\(index)", statusValue: 5)
            order.cleaningDateTime = Date(timeIntervalSince1970: Double(index))
            return order
        }
        XCTAssertEqual(HomeSections.recentForDisplay(orders).count, 3)
    }

    // MARK: - showRecent (HomeTab.kt:185)

    func testShowRecentGates() {
        let some = [OrderFixtures.listItem(id: "o1", statusValue: 5)]
        XCTAssertFalse(HomeSections.showRecent(recent: [], ordersLoaded: true, ordersLoading: false))
        XCTAssertTrue(HomeSections.showRecent(recent: some, ordersLoaded: true, ordersLoading: true))
        XCTAssertTrue(HomeSections.showRecent(recent: some, ordersLoaded: false, ordersLoading: false))
        XCTAssertFalse(HomeSections.showRecent(recent: some, ordersLoaded: false, ordersLoading: true))
    }

    // MARK: - showMilestone (HomeTab.kt:295-300)

    func testShowMilestoneRequiresANextTierAndPointsToIt() {
        XCTAssertTrue(HomeSections.showMilestone(account(nextTier: 2, pointsToNext: 100)))
        XCTAssertFalse(HomeSections.showMilestone(account(nextTier: nil, pointsToNext: 100)))
        XCTAssertFalse(HomeSections.showMilestone(account(nextTier: 2, pointsToNext: nil)))
        XCTAssertFalse(HomeSections.showMilestone(nil))
    }

    // MARK: - firstPaintReady (HomeTab.kt:196-203)

    func testFirstPaintReadyNeedsAllThreeSources() {
        XCTAssertTrue(HomeSections.firstPaintReady(ordersLoaded: true, membershipReady: true, packagesReady: true))
        XCTAssertFalse(HomeSections.firstPaintReady(ordersLoaded: false, membershipReady: true, packagesReady: true))
        XCTAssertFalse(HomeSections.firstPaintReady(ordersLoaded: true, membershipReady: false, packagesReady: true))
        XCTAssertFalse(HomeSections.firstPaintReady(ordersLoaded: true, membershipReady: true, packagesReady: false))
    }

    // MARK: - recentBookingTitle (HomeTab.kt:971-978 — services first, then packages, "+ N more")

    func testRecentBookingTitleAppendsTheLocalizedMoreSuffix() {
        var order = OrderFixtures.listItem(id: "o1", statusValue: 5)
        order.selectedServices = [ServiceListItem(id: "s1", name: "Deep clean")]
        order.selectedPackages = [PackageListItem(id: "p1", name: "Move-out")]
        XCTAssertEqual(
            HomeSections.recentBookingTitle(order, fallback: "Cleaning", languageCode: "en"),
            "Deep clean \(L10n.Orders.servicesMore(1))"
        )
    }

    func testRecentBookingTitleSkipsBlankNamesAndFallsBack() {
        var order = OrderFixtures.listItem(id: "o1", statusValue: 5)
        order.selectedServices = [ServiceListItem(id: "s1", name: " ")]
        XCTAssertEqual(HomeSections.recentBookingTitle(order, fallback: "Cleaning", languageCode: "en"), "Cleaning")

        order.selectedPackages = [PackageListItem(id: "p1", name: "Move-out")]
        XCTAssertEqual(HomeSections.recentBookingTitle(order, fallback: "Cleaning", languageCode: "en"), "Move-out")
    }

    func testRecentBookingTitleLocalizesLineNamesToTheAppLanguageWithFallback() {
        var order = OrderFixtures.listItem(id: "o1", statusValue: 5)
        order.selectedServices = [ServiceListItem(
            id: "s1",
            name: "Deep clean",
            translations: ["ru": Translation(name: "Глубокая уборка"), "cs": Translation(name: "Hloubkový úklid")]
        )]
        XCTAssertEqual(
            HomeSections.recentBookingTitle(order, fallback: "Cleaning", languageCode: "ru"),
            "Глубокая уборка"
        )
        XCTAssertEqual(
            HomeSections.recentBookingTitle(order, fallback: "Cleaning", languageCode: "cs"),
            "Hloubkový úklid"
        )
        // No translation for the language → the frozen English snapshot name.
        XCTAssertEqual(HomeSections.recentBookingTitle(order, fallback: "Cleaning", languageCode: "sk"), "Deep clean")
    }

    // MARK: - statusChipLabel (HomeTab.kt:1021-1023 — mapped label, else wire name, else hidden)

    func testStatusChipLabelMapsKnownStatuses() {
        let order = OrderFixtures.listItem(id: "o1", statusValue: 5)
        XCTAssertEqual(HomeSections.statusChipLabel(order), L10n.Orders.statusLabel(._5))
    }

    func testStatusChipLabelFallsBackToTheWireNameThenHides() {
        var order = OrderFixtures.listItem(id: "o1", statusValue: 99)
        order.orderStatus = Code(type: "OrderStatus", name: "Archived", value: 99)
        XCTAssertEqual(HomeSections.statusChipLabel(order), "Archived")

        order.orderStatus = Code(type: "OrderStatus", name: " ", value: 99)
        XCTAssertNil(HomeSections.statusChipLabel(order))

        order.orderStatus = nil
        XCTAssertNil(HomeSections.statusChipLabel(order))
    }

    // MARK: - orderAgainWhen (HomeTab.kt:684-692 — "MMM d", nil-safe)

    func testOrderAgainWhenFormatsTheDayAndIsNilSafe() {
        let date = Date(timeIntervalSince1970: 1_751_500_800) // 2025-07-03 UTC
        let formatted = HomeSections.orderAgainWhen(date)
        XCTAssertNotNil(formatted)
        XCTAssertFalse(formatted?.isEmpty ?? true)
        XCTAssertNil(HomeSections.orderAgainWhen(nil))
    }

    private func account(nextTier: Int?, pointsToNext: Int?) -> LoyaltyAccount {
        LoyaltyAccount(
            currentTier: 1,
            lifetimePoints: 120,
            completedBookingsCount: 3,
            tierAchievedOn: nil,
            pointsToNextTier: pointsToNext,
            nextTier: nextTier,
            currentDiscountPercent: 0,
            currentDiscountMinOrderAmount: nil,
            currentPerks: []
        )
    }
}
